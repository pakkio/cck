"""Long-lived WebSocket client for the Unity MCP connector.

Modeled on mcp-unity's UnityConnection + McpUnity classes
(build/unity/unityConnection.js, build/unity/mcpUnity.js):
one persistent connection with exponential-backoff reconnect, a
pending-request map correlated by id, and per-request timeouts.
"""

import asyncio
import enum
import json
import logging
from dataclasses import dataclass, field
from typing import Any, Optional

import websockets
from websockets.exceptions import ConnectionClosed

logger = logging.getLogger(__name__)

# Unity MCP connector endpoint path
UNITY_WS_PATH = "/McpUnity"

DEFAULT_CONFIG = {
    "connect_timeout": 5.0,
    "min_reconnect_delay": 1.0,
    "max_reconnect_delay": 30.0,
    "reconnect_multiplier": 2.0,
    "max_reconnect_attempts": 50,
    "request_timeout": 15.0,
    "heartbeat_interval": 30.0,  # seconds — not implemented in v1, reserved
    "max_message_size": 100 * 1024 * 1024,
}


class ConnectionState(enum.Enum):
    DISCONNECTED = "disconnected"
    CONNECTING = "connecting"
    CONNECTED = "connected"
    RECONNECTING = "reconnecting"


class PipelineError(Exception):
    """Raised when a pipeline step or the Unity connection fails."""

    def __init__(self, message: str, error_type: str = "internal_error", details: Any = None):
        super().__init__(message)
        self.error_type = error_type
        self.details = details


@dataclass
class _PendingRequest:
    future: asyncio.Future[dict]
    method: str
    timeout_handle: Optional[asyncio.Handle] = None


class UnityBridge:
    """One persistent WebSocket connection to the Unity MCP connector.

    Connects to ws://host:port/McpUnity. On connect, opens a session and keeps it
    alive with exponential-backoff reconnect. Each tool call sends one
    {id, method, params} message and awaits the matching response by id.
    """

    def __init__(self, host: str = "localhost", port: int = 8090, config: Optional[dict] = None):
        self._host = host
        self._port = port
        self._cfg = {**DEFAULT_CONFIG, **(config or {})}
        self._ws: Optional[websockets.WebSocketClientProtocol] = None
        self._state = ConnectionState.DISCONNECTED
        self._pending: dict[int, _PendingRequest] = {}
        self._recv_task: Optional[asyncio.Task] = None
        self._closing = False
        self._reconnect_attempt = 0
        self._next_request_id = 1
        self._url = f"ws://{host}:{port}{UNITY_WS_PATH}"

    @property
    def state(self) -> ConnectionState:
        return self._state

    @property
    def url(self) -> str:
        return self._url

    @property
    def is_connected(self) -> bool:
        return self._state == ConnectionState.CONNECTED and self._ws is not None

    async def connect(self) -> None:
        """Connect (or reconnect) to the Unity MCP connector."""
        if self.is_connected:
            return
        self._closing = False
        await self._connect_once()

    async def _connect_once(self) -> None:
        self._state = ConnectionState.CONNECTING
        self._reconnect_attempt += 1
        logger.info("Connecting to Unity MCP at %s (attempt %d)", self._url, self._reconnect_attempt)
        try:
            self._ws = await websockets.connect(
                self._url,
                open_timeout=self._cfg["connect_timeout"],
                ping_interval=None,
                max_size=self._cfg["max_message_size"],
            )
        except (OSError, websockets.exceptions.WebSocketException, asyncio.TimeoutError) as exc:
            self._state = ConnectionState.DISCONNECTED
            raise PipelineError(f"Could not connect to Unity MCP at {self._url}: {exc}",
                                error_type="connection_error") from exc
        self._state = ConnectionState.CONNECTED
        self._reconnect_attempt = 0
        self._recv_task = asyncio.create_task(self._recv_loop())
        logger.info("Connected to Unity MCP at %s", self._url)

    async def _recv_loop(self) -> None:
        assert self._ws is not None
        try:
            async for raw_message in self._ws:
                self._handle_message(raw_message)
        except ConnectionClosed:
            pass
        finally:
            self._on_disconnected()

    def _handle_message(self, raw_message: str) -> None:
        try:
            message = json.loads(raw_message)
        except json.JSONDecodeError:
            logger.warning("Dropping malformed message from Unity MCP: %r", raw_message)
            return
        request_id = message.get("id")
        if request_id is None:
            logger.warning("Dropping Unity message without id: %r", message)
            return
        pending = self._pending.pop(request_id, None)
        if pending is None:
            logger.warning("Dropping Unity response for unknown request id: %s", request_id)
            return
        if not pending.future.done():
            pending.future.set_result(message)

    def _on_disconnected(self) -> None:
        self._state = ConnectionState.DISCONNECTED
        for pending in self._pending.values():
            if not pending.future.done():
                pending.future.set_exception(
                    PipelineError(f"Connection to Unity MCP was lost while '{pending.method}' in flight",
                                  error_type="connection_error")
                )
        self._pending.clear()
        if not self._closing:
            asyncio.create_task(self._reconnect_with_backoff())

    async def _reconnect_with_backoff(self) -> None:
        self._state = ConnectionState.RECONNECTING
        delay = self._cfg["min_reconnect_delay"]
        while not self._closing:
            await asyncio.sleep(delay)
            try:
                await self._connect_once()
                return
            except Exception:
                logger.warning("Reconnect to Unity MCP failed, retrying in %.1fs", delay)
                self._state = ConnectionState.RECONNECTING
                delay = min(delay * self._cfg["reconnect_multiplier"], self._cfg["max_reconnect_delay"])
                if self._reconnect_attempt >= self._cfg["max_reconnect_attempts"]:
                    logger.error("Max Unity reconnect attempts (%d) reached", self._cfg["max_reconnect_attempts"])
                    return

    async def send_request(self, method: str, params: dict, timeout: Optional[float] = None) -> dict:
        """Send one JSON-RPC-style request and await the matching response.

        Raises PipelineError on connection failure, timeout, or server error.
        """
        if not self.is_connected:
            raise PipelineError("Not connected to Unity MCP", error_type="connection_error")
        request_id = self._next_request_id
        self._next_request_id += 1
        loop = asyncio.get_running_loop()
        timeout_s = timeout or self._cfg["request_timeout"]
        pending = _PendingRequest(future=loop.create_future(), method=method)
        self._pending[request_id] = pending

        # Per-request timeout
        def _on_timeout() -> None:
            if not pending.future.done():
                self._pending.pop(request_id, None)
                pending.future.set_exception(
                    PipelineError(f"Timed out waiting for '{method}' response", error_type="timeout_error")
                )

        timeout_handle = loop.call_later(timeout_s, _on_timeout)
        pending.timeout_handle = timeout_handle

        try:
            await self._ws.send(json.dumps({"id": request_id, "method": method, "params": params}))
            message = await pending.future
            return message.get("result", {})
        except asyncio.CancelledError:
            raise
        except PipelineError:
            raise
        except Exception as exc:
            raise PipelineError(f"Unexpected error sending '{method}': {exc}", error_type="internal_error") from exc
        finally:
            if timeout_handle:
                timeout_handle.cancel()
            self._pending.pop(request_id, None)

    async def close(self) -> None:
        """Close the connection and clean up."""
        self._closing = True
        if self._recv_task is not None:
            self._recv_task.cancel()
        if self._ws is not None:
            await self._ws.close()
        self._state = ConnectionState.DISCONNECTED
        self._pending.clear()
