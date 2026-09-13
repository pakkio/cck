"""WebSocket client for the Blender mcp-blender extension bridge.

Modeled on mcp-blender's own bridge.py — connects to ws://host:port,
sends JSON-RPC-style {id, method, params} messages, receives responses
by correlating on id. Long-lived with exponential-backoff reconnect.
"""

import asyncio
import enum
import json
import logging
import uuid
from dataclasses import dataclass, field
from typing import Any, Optional

import websockets
from websockets.exceptions import ConnectionClosed

logger = logging.getLogger(__name__)

DEFAULT_REQUEST_TIMEOUT_S = 15.0
HEAVY_REQUEST_TIMEOUT_S = 600.0
MAX_MESSAGE_SIZE_BYTES = 100 * 1024 * 1024
MIN_RECONNECT_DELAY_S = 1.0
MAX_RECONNECT_DELAY_S = 30.0
RECONNECT_MULTIPLIER = 2.0


class ConnectionState(enum.Enum):
    DISCONNECTED = "disconnected"
    CONNECTING = "connecting"
    CONNECTED = "connected"
    RECONNECTING = "reconnecting"


class BridgeError(Exception):
    """Raised when the bridge can't connect, times out, or returns an error."""

    def __init__(self, message: str, details: Any = None, error_type: str = "internal_error"):
        super().__init__(message)
        self.details = details
        self.error_type = error_type


@dataclass
class _PendingRequest:
    future: asyncio.Future[dict]
    method: str = ""


class BlenderBridge:
    """Long-lived WebSocket client for the Blender mcp-blender extension.

    Connects to ws://host:port, maintains the connection with exponential-backoff
    reconnect. Per-request timeout bounds wait time independently of keepalive
    (ping_interval=None avoids GIL-starvation false disconnects).
    """

    def __init__(
        self,
        host: str = "127.0.0.1",
        port: int = 9876,
        request_timeout: float = DEFAULT_REQUEST_TIMEOUT_S,
    ):
        self._host = host
        self._port = port
        self._request_timeout = request_timeout
        self._ws: Optional[websockets.WebSocketClientProtocol] = None
        self._state = ConnectionState.DISCONNECTED
        self._pending: dict[str, _PendingRequest] = {}
        self._recv_task: Optional[asyncio.Task] = None
        self._closing = False

    @property
    def state(self) -> ConnectionState:
        return self._state

    @property
    def url(self) -> str:
        return f"ws://{self._host}:{self._port}"

    async def connect(self) -> None:
        self._closing = False
        await self._connect_once()

    async def _connect_once(self) -> None:
        self._state = ConnectionState.CONNECTING
        try:
            self._ws = await websockets.connect(
                self.url,
                open_timeout=self._request_timeout,
                ping_interval=None,
                max_size=MAX_MESSAGE_SIZE_BYTES,
            )
        except (OSError, websockets.exceptions.WebSocketException, asyncio.TimeoutError) as exc:
            self._state = ConnectionState.DISCONNECTED
            raise BridgeError(f"Could not connect to Blender bridge at {self.url}: {exc}",
                              error_type="connection_error") from exc
        self._state = ConnectionState.CONNECTED
        self._recv_task = asyncio.create_task(self._recv_loop())

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
            logger.warning("Dropping malformed message from Blender bridge: %r", raw_message)
            return
        request_id = message.get("id")
        pending = self._pending.pop(request_id, None) if request_id is not None else None
        if pending is None:
            logger.warning("Dropping message with unmatched id: %r", request_id)
            return
        if not pending.future.done():
            pending.future.set_result(message)

    def _on_disconnected(self) -> None:
        self._state = ConnectionState.DISCONNECTED
        for pending in self._pending.values():
            if not pending.future.done():
                pending.future.set_exception(
                    BridgeError("Connection to Blender bridge was lost", error_type="connection_error")
                )
        self._pending.clear()
        if not self._closing:
            asyncio.create_task(self._reconnect_with_backoff())

    async def reconnect_with_backoff(self) -> None:
        """Retry connecting until successful, in the background."""
        if self._state not in (ConnectionState.DISCONNECTED, ConnectionState.RECONNECTING):
            return
        self._state = ConnectionState.RECONNECTING
        try:
            await self._reconnect_with_backoff()
        except asyncio.CancelledError:
            raise
        except Exception:
            self._state = ConnectionState.DISCONNECTED

    async def _reconnect_with_backoff(self) -> None:
        self._state = ConnectionState.RECONNECTING
        delay = MIN_RECONNECT_DELAY_S
        while not self._closing:
            await asyncio.sleep(delay)
            try:
                await self._connect_once()
                return
            except Exception:
                logger.warning("Reconnect attempt to %s failed, retrying in %.1fs", self.url, delay)
                self._state = ConnectionState.RECONNECTING
                delay = min(delay * RECONNECT_MULTIPLIER, MAX_RECONNECT_DELAY_S)

    async def send_request(
        self,
        method: str,
        params: dict,
        timeout: Optional[float] = None,
    ) -> dict:
        if self._state != ConnectionState.CONNECTED or self._ws is None:
            raise BridgeError("Not connected to Blender bridge", error_type="connection_error")
        request_id = str(uuid.uuid4())
        loop = asyncio.get_running_loop()
        pending = _PendingRequest(future=loop.create_future(), method=method)
        self._pending[request_id] = pending
        await self._ws.send(json.dumps({"id": request_id, "method": method, "params": params}))
        try:
            message = await asyncio.wait_for(pending.future, timeout=timeout or self._request_timeout)
        except asyncio.TimeoutError:
            self._pending.pop(request_id, None)
            raise BridgeError(f"Timed out waiting for '{method}' response", error_type="timeout_error")
        if "error" in message:
            err = message["error"]
            try:
                etype = err.get("type", "internal_error")
            except Exception:
                etype = "internal_error"
            raise BridgeError(err.get("message", "Unknown error"), err.get("details"), etype)
        return message.get("result", {})

    async def close(self) -> None:
        self._closing = True
        if self._recv_task is not None:
            self._recv_task.cancel()
        if self._ws is not None:
            await self._ws.close()
        self._state = ConnectionState.DISCONNECTED
