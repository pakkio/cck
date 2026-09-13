"""Bootstrap: one FastMCP instance, two long-lived bridges (Blender WS + Unity WS),
stdio transport. Essential vehicle + Blender workflow tools with checkpoints.

Both bridges maintain persistent connections with exponential-backoff reconnect.
The pipeline server is the orchestration layer: it chains Blender export → Unity
import → CVR rig in a single tool call with snapshot-based rollback.
"""

import asyncio
import logging
import signal
import sys

from mcp.server.fastmcp import FastMCP

from .bridge import BlenderBridge
from .unity_bridge import UnityBridge, PipelineError
from .config import load_dotenv, resolve_host_port, resolve_unity_port
from . import tools

logger = logging.getLogger(__name__)

SERVER_INSTRUCTIONS = """\
MCP pipeline server bridging Blender (mcp-blender extension, ws://127.0.0.1:9876)
and Unity (MCP Unity connector, ws://localhost:8090/McpUnity) for essential
vehicle + Blender workflows.

Both connections are long-lived with automatic reconnect. Tool calls that touch
both systems (e.g. pipeline_build_vehicle) use snapshot checkpoints so a failure
partway through can rollback to the pre-call state.

Tools:
- blender_export_vehicle_fbx: Export a vehicle FBX/GLB from Blender (Unity-axis-corrected)
- blender_get_vehicle_info: Inspect vehicle mesh (verts, polys, bounds, origin)
- blender_import_vehicle_to_unity: Copy exported FBX into Unity Assets/ folder
- unity_setup_cvr_world: Create/configure the ChilloutVR CVRWorld root
- unity_configure_cvr_vehicle: Create a drivable CVR vehicle rig (WheelColliders + seats)
- unity_inspect_cvr_cck: Pre-upload audit of the CCK world/avatar/prop
- unity_add_asset_to_scene: Add an imported asset into the Unity scene at a position
- unity_get_scene_info: Get active Unity scene name, path, dirty state, root count
- pipeline_build_vehicle: End-to-end: export from Blender → copy to Unity → import scene → rig as CVR vehicle
  (with snapshot checkpoint + rollback on failure)
- pipeline_get_bridge_status: Health check for both Blender and Unity connections
"""

DEFAULT_BLENDER_HOST = "127.0.0.1"
DEFAULT_BLENDER_PORT = 9876
DEFAULT_UNITY_PORT = 8090


def build_server() -> FastMCP:
    load_dotenv()
    blender_host, blender_port = resolve_host_port()
    unity_port = resolve_unity_port()

    blender_bridge = BlenderBridge(blender_host, blender_port)
    unity_bridge = UnityBridge(port=unity_port)

    mcp = FastMCP(
        name="mcp-pipeline",
        instructions=SERVER_INSTRUCTIONS,
    )
    tools.register_all(mcp, blender_bridge, unity_bridge)
    # Attach bridges so tool functions can access them
    mcp._blender_bridge = blender_bridge
    mcp._unity_bridge = unity_bridge
    return mcp


async def run() -> None:
    mcp = build_server()
    blender_bridge = mcp._blender_bridge
    unity_bridge = mcp._unity_bridge

    # Connect Blender bridge (fire-and-forget reconnect if it fails)
    try:
        await blender_bridge.connect()
        logger.info("Connected to Blender bridge at %s", blender_bridge.url)
    except Exception as exc:  # noqa: BLE001
        logger.warning(
            "Could not connect to Blender bridge yet (%s) -- will keep retrying in the "
            "background until the mcp_bridge extension is running in Blender.",
            exc,
        )
        asyncio.create_task(blender_bridge.reconnect_with_backoff())

    # Connect Unity bridge (fire-and-forget reconnect if it fails)
    try:
        await unity_bridge.connect()
        logger.info("Connected to Unity MCP at %s", unity_bridge.url)
    except Exception as exc:  # noqa: BLE001
        logger.warning(
            "Could not connect to Unity MCP yet (%s) -- will keep retrying in the "
            "background until the Unity Editor is open with the MCP Unity package active.",
            exc,
        )
        asyncio.create_task(unity_bridge._reconnect_with_backoff())

    loop = asyncio.get_running_loop()
    shutdown_event = asyncio.Event()

    def _request_shutdown() -> None:
        shutdown_event.set()

    for sig in (signal.SIGINT, signal.SIGTERM):
        try:
            loop.add_signal_handler(sig, _request_shutdown)
        except NotImplementedError:
            pass

    server_task = asyncio.create_task(mcp.run_stdio_async())
    shutdown_task = asyncio.create_task(shutdown_event.wait())

    done, pending = await asyncio.wait(
        {server_task, shutdown_task}, return_when=asyncio.FIRST_COMPLETED
    )
    for task in pending:
        task.cancel()
    await blender_bridge.close()
    await unity_bridge.close()


def main() -> None:
    logging.basicConfig(level=logging.INFO, stream=sys.stderr)
    try:
        asyncio.run(run())
    except KeyboardInterrupt:
        pass
