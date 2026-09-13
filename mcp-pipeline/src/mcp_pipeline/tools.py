"""Tool registry — essential vehicle + Blender integration tools.

Each tool is a FastMCP @tool decorated function. Blender tools use the
BlenderBridge (long-lived WebSocket to ws://127.0.0.1:9876). Unity tools
use the UnityBridge (long-lived WebSocket to ws://localhost:8090/McpUnity).

Pipeline tools use snapshot-style checkpoints: before a multi-step operation,
the tool records enough state to describe the pre-call scene; on failure,
it returns the checkpoint so the caller can resume or undo manually.
"""

import json
import logging
from pathlib import Path
from typing import Any

from mcp.server.fastmcp import FastMCP

from .bridge import BlenderBridge, BridgeError
from .unity_bridge import UnityBridge, PipelineError

logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _blender(mcp: FastMCP) -> BlenderBridge:
    return getattr(mcp, "_blender_bridge", None) or BlenderBridge()


def _unity(mcp: FastMCP) -> UnityBridge:
    return getattr(mcp, "_unity_bridge", None) or UnityBridge()


async def _checkpoint_snapshot(mcp: FastMCP, label: str) -> dict:
    """Capture a lightweight pre-operation snapshot for rollback hints.

    Not a full scene snapshot — that's what mcp-blender's
    blender_scene(action='checkpoint_create') does. This is a pipeline-level
    hint: which scene is active in Blender/Unity before the call.
    """
    ub = _unity(mcp)
    snapshot = {"label": label}
    try:
        scene = await ub.send_request("get_scene_info", {}, timeout=5.0)
        snapshot["unity_scene"] = scene
    except Exception:
        snapshot["unity_scene"] = None
    try:
        info = await _blender(mcp).send_request("scene_info", {}, timeout=5.0)
        snapshot["blender_scene"] = info
    except Exception:
        snapshot["blender_scene"] = None
    return snapshot


# ---------------------------------------------------------------------------
# Blender vehicle tool implementations
# ---------------------------------------------------------------------------

async def blender_export_vehicle_fbx_impl(mcp: FastMCP, object_name: str, export_path: str) -> dict:
    """Export a Blender vehicle object as Unity-ready FBX (axis-corrected)."""
    bridge = _blender(mcp)
    try:
        result = await bridge.send_request(
            "blender_render_pipeline",
            {"action": "export_unity_fbx", "params": {"object_name": object_name, "export_path": export_path}},
            timeout=120.0,
        )
        return {"ok": True, "result": result}
    except BridgeError as exc:
        return {"ok": False, "error": str(exc), "error_type": exc.error_type}


async def blender_get_vehicle_info_impl(mcp: FastMCP, object_name: str) -> dict:
    """Inspect a vehicle mesh: vertex count, triangle count, bounds, origin."""
    bridge = _blender(mcp)
    try:
        result = await bridge.send_request("get_object_info", {"object_name": object_name}, timeout=10.0)
        return {"ok": True, "result": result}
    except BridgeError as exc:
        return {"ok": False, "error": str(exc), "error_type": exc.error_type}


async def blender_import_vehicle_to_unity_impl(mcp: FastMCP, fbx_path: str, unity_asset_path: str) -> dict:
    """Copy an exported FBX into the Unity project's Assets folder."""
    import shutil
    src = Path(fbx_path)
    dst = Path(unity_asset_path)
    if not src.exists():
        return {"ok": False, "error": f"FBX not found: {fbx_path}", "error_type": "validation_error"}
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)
    rel = dst.relative_to(Path("Assets")).as_posix() if dst.is_relative_to(Path("Assets")) else str(dst)
    return {
        "ok": True,
        "result": {"asset_path": rel, "absolute_path": str(dst),
                   "note": "Unity imports on next AssetDatabase refresh; call unity_add_asset_to_scene to place it"},
    }


# ---------------------------------------------------------------------------
# Unity CVR vehicle tool implementations
# ---------------------------------------------------------------------------

async def unity_setup_cvr_world_impl(mcp: FastMCP, **kwargs: Any) -> dict:
    """Create or configure the ChilloutVR CVRWorld root GameObject."""
    ub = _unity(mcp)
    try:
        result = await ub.send_request("manage_cvr_world", {"action": "setup_world", **kwargs}, timeout=30.0)
        return {"ok": True, "result": result}
    except PipelineError as exc:
        return {"ok": False, "error": str(exc), "error_type": exc.error_type}


async def unity_configure_cvr_vehicle_impl(mcp: FastMCP, **kwargs: Any) -> dict:
    """Create a drivable CVR vehicle rig (WheelColliders + CVRSeats + headlights)."""
    ub = _unity(mcp)
    try:
        result = await ub.send_request("configure_cvr_vehicle", {"action": "create_car_rig", **kwargs}, timeout=30.0)
        return {"ok": True, "result": result}
    except PipelineError as exc:
        return {"ok": False, "error": str(exc), "error_type": exc.error_type}


async def unity_inspect_cvr_cck_impl(mcp: FastMCP, content_type: str = "world") -> dict:
    """Pre-upload audit of the CCK world/avatar/prop (poly budgets, disallowed scripts)."""
    ub = _unity(mcp)
    try:
        result = await ub.send_request("inspect_cvr_cck", {"action": "validate_content", "contentType": content_type}, timeout=15.0)
        return {"ok": True, "result": result}
    except PipelineError as exc:
        return {"ok": False, "error": str(exc), "error_type": exc.error_type}


async def unity_add_asset_to_scene_impl(mcp: FastMCP, asset_path: str, position: list = None, parent_path: str = None) -> dict:
    """Add an imported asset (FBX/prefab) into the Unity scene at a position."""
    ub = _unity(mcp)
    try:
        pos = None
        if position:
            pos = {"x": position[0], "y": position[1], "z": position[2]}
        result = await ub.send_request(
            "add_asset_to_scene",
            {"assetPath": asset_path, "position": pos, "parentPath": parent_path},
            timeout=15.0,
        )
        return {"ok": True, "result": result}
    except PipelineError as exc:
        return {"ok": False, "error": str(exc), "error_type": exc.error_type}


async def unity_get_scene_info_impl(mcp: FastMCP) -> dict:
    """Get active Unity scene name, path, dirty state, root object count."""
    ub = _unity(mcp)
    try:
        result = await ub.send_request("get_scene_info", {}, timeout=5.0)
        return {"ok": True, "result": result}
    except PipelineError as exc:
        return {"ok": False, "error": str(exc), "error_type": exc.error_type}


# ---------------------------------------------------------------------------
# Pipeline orchestration implementation (checkpoint + resume)
# ---------------------------------------------------------------------------

async def pipeline_build_vehicle_impl(
    mcp: FastMCP,
    blender_object_name: str,
    blender_export_path: str,
    unity_asset_path: str,
    vehicle_name: str = "CVR_Drivable_Car",
    position: list = None,
    seat_count: int = 3,
) -> dict:
    """End-to-end: export vehicle from Blender → copy to Unity Assets → import into scene → rig as CVR vehicle.

    Uses a checkpoint snapshot before step 1 so the caller can see the pre-call
    state. On failure, returns the checkpoint plus the last successful step.

    Steps:
    1. Export FBX from Blender (blender_render_pipeline / export_unity_fbx)
    2. Copy FBX into Unity project Assets/ (file copy)
    3. Add asset to Unity scene (add_asset_to_scene)
    4. Configure as CVR drivable vehicle (configure_cvr_vehicle)
    """
    steps = []
    position = position or [0, 0, 0]

    # Pre-call snapshot
    snapshot = await _checkpoint_snapshot(mcp, "pipeline_build_vehicle")
    logger.info("Checkpoint before pipeline_build_vehicle: unity=%s blender=%s",
                snapshot.get("unity_scene") is not None,
                snapshot.get("blender_scene") is not None)

    # Step 1: Export from Blender
    logger.info("Step 1: Exporting %s from Blender → %s", blender_object_name, blender_export_path)
    export_result = await blender_export_vehicle_fbx_impl(mcp, blender_object_name, blender_export_path)
    steps.append({"step": "blender_export", "status": "ok" if export_result.get("ok") else "failed", "result": export_result})
    if not export_result.get("ok"):
        return {"ok": False, "checkpoint": snapshot, "steps": steps,
                "error": "Blender export failed", "error_type": export_result.get("error_type", "internal_error")}

    # Step 2: Copy FBX to Unity Assets
    logger.info("Step 2: Copying FBX into Unity Assets → %s", unity_asset_path)
    copy_result = await blender_import_vehicle_to_unity_impl(mcp, blender_export_path, unity_asset_path)
    steps.append({"step": "copy_to_unity", "status": "ok" if copy_result.get("ok") else "failed", "result": copy_result})
    if not copy_result.get("ok"):
        return {"ok": False, "checkpoint": snapshot, "steps": steps,
                "error": "Copy to Unity Assets failed", "error_type": copy_result.get("error_type", "internal_error")}

    # Step 3: Import asset into Unity scene
    asset_path = copy_result["result"].get("asset_path", unity_asset_path)
    logger.info("Step 3: Adding asset to Unity scene → %s", asset_path)
    scene_result = await unity_add_asset_to_scene_impl(mcp, asset_path, position=position)
    steps.append({"step": "add_to_scene", "status": "ok" if scene_result.get("ok") else "failed", "result": scene_result})
    if not scene_result.get("ok"):
        return {"ok": False, "checkpoint": snapshot, "steps": steps,
                "error": "Add to Unity scene failed (FBX is in Assets, ready for retry)",
                "error_type": scene_result.get("error_type", "internal_error"),
                "note": "FBX copied to Assets — scene placement can be retried"}

    # Step 4: Configure as CVR drivable vehicle
    object_path = scene_result.get("result", {}).get("objectPath", vehicle_name)
    logger.info("Step 4: Configuring CVR vehicle rig → %s", vehicle_name)
    vehicle_result = await unity_configure_cvr_vehicle_impl(
        mcp,
        objectPath=object_path,
        vehicleName=vehicle_name,
        seatCount=seat_count,
    )
    steps.append({"step": "configure_cvr_vehicle", "status": "ok" if vehicle_result.get("ok") else "failed", "result": vehicle_result})
    if not vehicle_result.get("ok"):
        return {"ok": False, "checkpoint": snapshot, "steps": steps,
                "error": "CVR vehicle rig failed",
                "error_type": vehicle_result.get("error_type", "internal_error"),
                "note": "Vehicle is in scene but not rigged — resume with unity_configure_cvr_vehicle",
                "partial_object_path": object_path}

    return {
        "ok": True,
        "vehicle_name": vehicle_name,
        "object_path": object_path,
        "fbx_path": blender_export_path,
        "unity_asset_path": unity_asset_path,
        "steps": steps,
    }


async def pipeline_get_bridge_status_impl(mcp: FastMCP) -> dict:
    """Health check for both Blender and Unity long-lived connections."""
    blender_bridge = _blender(mcp)
    unity_bridge = _unity(mcp)

    blender_state = blender_bridge.state.value if blender_bridge else "no_bridge"
    blender_url = blender_bridge.url if blender_bridge else "n/a"
    unity_state = unity_bridge.state.value if unity_bridge else "no_bridge"
    unity_url = unity_bridge.url if unity_bridge else "n/a"

    # Quick Unity reachability check (read-only, safe to retry on reconnect)
    unity_ok = False
    try:
        await unity_bridge.send_request("get_scene_info", {}, timeout=5.0)
        unity_ok = True
    except Exception:
        unity_ok = False

    return {
        "blender": {"state": blender_state, "url": blender_url},
        "unity": {"state": unity_state, "url": unity_url, "reachable": unity_ok},
    }


# ---------------------------------------------------------------------------
# Registration
# ---------------------------------------------------------------------------

def register_all(mcp: FastMCP, blender_bridge: BlenderBridge, unity_bridge: UnityBridge) -> None:
    """Register all pipeline tools with the FastMCP server."""

    @mcp.tool()
    async def blender_export_vehicle_fbx(object_name: str, export_path: str) -> dict:
        """Export a Blender vehicle object as Unity-ready FBX (axis-corrected)."""
        return await blender_export_vehicle_fbx_impl(mcp, object_name, export_path)

    @mcp.tool()
    async def blender_get_vehicle_info(object_name: str) -> dict:
        """Inspect a vehicle mesh: vertex count, triangle count, bounds, origin."""
        return await blender_get_vehicle_info_impl(mcp, object_name)

    @mcp.tool()
    async def blender_import_vehicle_to_unity(fbx_path: str, unity_asset_path: str) -> dict:
        """Copy an exported FBX into the Unity project's Assets folder."""
        return await blender_import_vehicle_to_unity_impl(mcp, fbx_path, unity_asset_path)

    @mcp.tool()
    async def unity_setup_cvr_world(
        respawnHeight: float = -50.0,
        runSpeed: float = 4.0,
        sprintMultiplier: float = 2.0,
        jumpHeight: float = 1.5,
        allowFlight: bool = True,
        allowTeleport: bool = True,
        gravity: list = None,
    ) -> dict:
        """Create/configure the ChilloutVR CVRWorld root GameObject."""
        return await unity_setup_cvr_world_impl(mcp, **{
            "respawnHeight": respawnHeight, "runSpeed": runSpeed,
            "sprintMultiplier": sprintMultiplier, "jumpHeight": jumpHeight,
            "allowFlight": allowFlight, "allowTeleport": allowTeleport,
            "gravity": {"x": gravity[0], "y": gravity[1], "z": gravity[2]} if gravity else None,
        })

    @mcp.tool()
    async def unity_configure_cvr_vehicle(
        objectPath: str = None,
        vehicleName: str = "CVR_Drivable_Car",
        mass: float = 1200.0,
        spring: float = 30000.0,
        damper: float = 4500.0,
        suspensionDistance: float = 0.2,
        wheelRadius: float = 0.35,
        addHeadlights: bool = True,
        addEngineAudio: bool = True,
        seatCount: int = 3,
    ) -> dict:
        """Create a drivable CVR vehicle rig with WheelColliders + CVRSeats."""
        kwargs: dict = {
            "vehicleName": vehicleName, "mass": mass, "spring": spring,
            "damper": damper, "suspensionDistance": suspensionDistance,
            "wheelRadius": wheelRadius, "addHeadlights": addHeadlights,
            "addEngineAudio": addEngineAudio, "seatCount": seatCount,
        }
        if objectPath:
            kwargs["objectPath"] = objectPath
        return await unity_configure_cvr_vehicle_impl(mcp, **kwargs)

    @mcp.tool()
    async def unity_inspect_cvr_cck(contentType: str = "world") -> dict:
        """Pre-upload audit of the CCK world/avatar/prop (poly budgets, disallowed scripts)."""
        return await unity_inspect_cvr_cck_impl(mcp, contentType)

    @mcp.tool()
    async def unity_add_asset_to_scene(assetPath: str, position: list = None, parentPath: str = None) -> dict:
        """Add an imported asset (FBX/prefab) into the Unity scene."""
        return await unity_add_asset_to_scene_impl(mcp, assetPath, position, parentPath)

    @mcp.tool()
    async def unity_get_scene_info() -> dict:
        """Get active Unity scene name, path, dirty state, root object count."""
        return await unity_get_scene_info_impl(mcp)

    @mcp.tool()
    async def pipeline_build_vehicle(
        blender_object_name: str,
        blender_export_path: str,
        unity_asset_path: str,
        vehicle_name: str = "CVR_Drivable_Car",
        position: list = None,
        seat_count: int = 3,
    ) -> dict:
        """End-to-end: export from Blender → copy to Unity → import scene → rig as CVR vehicle.

        Includes a checkpoint snapshot before step 1 and per-step status so
        failures can be resumed rather than restarted from scratch.
        """
        return await pipeline_build_vehicle_impl(
            mcp, blender_object_name, blender_export_path, unity_asset_path,
            vehicle_name, position, seat_count,
        )

    @mcp.tool()
    async def pipeline_get_bridge_status() -> dict:
        """Health check for both Blender and Unity WebSocket connections."""
        return await pipeline_get_bridge_status_impl(mcp)
