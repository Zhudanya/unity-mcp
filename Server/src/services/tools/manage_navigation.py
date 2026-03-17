from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    description=(
        "Navigation and pathfinding tools: NavMesh baking, path testing, agent/obstacle configuration. "
        "Supports both legacy NavMeshBuilder and new AI Navigation package (NavMeshSurface). "
        "Actions: "
        "bake - Bake NavMesh with optional agent settings (radius, height, slope, step_height). "
        "clear - Clear all baked NavMesh data. "
        "get_settings - Read navigation areas, bake settings, and NavMesh stats. "
        "set_area - Configure a NavMesh area (name and cost) by index. "
        "configure_agent - Add/configure NavMeshAgent on a GameObject. "
        "configure_obstacle - Add/configure NavMeshObstacle with carving. "
        "add_offmesh_link - Create an OffMeshLink between two GameObjects. "
        "test_path - Calculate a path between two points and return waypoints + distance."
    ),
    annotations=ToolAnnotations(
        title="Manage Navigation",
        destructiveHint=True,
    ),
)
async def manage_navigation(
    ctx: Context,
    action: Annotated[
        Literal["bake", "clear", "get_settings", "set_area",
                "configure_agent", "configure_obstacle", "add_offmesh_link", "test_path"],
        "Action to perform.",
    ],
    target: Annotated[str | int, "Target GameObject (name, path, or instance ID)"] | None = None,
    # --- bake ---
    agent_radius: Annotated[float, "Agent radius for NavMesh bake"] | None = None,
    agent_height: Annotated[float, "Agent height for NavMesh bake"] | None = None,
    max_slope: Annotated[float, "Max walkable slope angle (degrees)"] | None = None,
    step_height: Annotated[float, "Max step height agent can climb"] | None = None,
    # --- set_area ---
    index: Annotated[int, "Area index (0-31)"] | None = None,
    name: Annotated[str, "Area name"] | None = None,
    cost: Annotated[float, "Area traversal cost"] | None = None,
    # --- configure_agent ---
    speed: Annotated[float, "Agent movement speed"] | None = None,
    angular_speed: Annotated[float, "Agent turning speed (deg/s)"] | None = None,
    acceleration: Annotated[float, "Agent acceleration"] | None = None,
    stopping_distance: Annotated[float, "Distance at which agent stops"] | None = None,
    auto_braking: Annotated[bool, "Auto braking when approaching destination"] | None = None,
    base_offset: Annotated[float, "Agent vertical offset"] | None = None,
    area_mask: Annotated[int, "Area mask (bitmask)"] | None = None,
    area_mask_names: Annotated[str, "Area names comma-separated (e.g., 'Walkable,Road')"] | None = None,
    # --- configure_obstacle ---
    shape: Annotated[str, "Obstacle shape: box, capsule"] | None = None,
    size: Annotated[list[float], "Obstacle size [x, y, z]"] | None = None,
    center: Annotated[list[float], "Obstacle center offset [x, y, z]"] | None = None,
    carve: Annotated[bool, "Enable NavMesh carving"] | None = None,
    carve_only_stationary: Annotated[bool, "Only carve when stationary"] | None = None,
    # --- add_offmesh_link ---
    end_target: Annotated[str, "End point GameObject name"] | None = None,
    bidirectional: Annotated[bool, "Bidirectional link"] | None = None,
    area: Annotated[int, "Area type for the link"] | None = None,
    auto_update_positions: Annotated[bool, "Auto update link positions"] | None = None,
    # --- test_path ---
    start: Annotated[list[float], "Path start position [x, y, z]"] | None = None,
    end: Annotated[list[float], "Path end position [x, y, z]"] | None = None,
) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params_dict: dict[str, Any] = {"action": action}

    param_mapping = {
        "target": target,
        "agentRadius": agent_radius,
        "agentHeight": agent_height,
        "maxSlope": max_slope,
        "stepHeight": step_height,
        "index": index,
        "name": name,
        "cost": cost,
        "speed": speed,
        "angularSpeed": angular_speed,
        "acceleration": acceleration,
        "stoppingDistance": stopping_distance,
        "autoBraking": auto_braking,
        "baseOffset": base_offset,
        "areaMask": area_mask,
        "areaMaskNames": area_mask_names,
        "shape": shape,
        "size": size,
        "center": center,
        "carve": carve,
        "carveOnlyStationary": carve_only_stationary,
        "endTarget": end_target,
        "bidirectional": bidirectional,
        "area": area,
        "autoUpdatePositions": auto_update_positions,
        "start": start,
        "end": end,
    }

    for k, v in param_mapping.items():
        if v is not None:
            params_dict[k] = v

    try:
        response = await send_with_unity_instance(
            async_send_command_with_retry, unity_instance,
            "manage_navigation", params_dict,
        )
        if isinstance(response, dict) and response.get("success"):
            return {
                "success": True,
                "message": response.get("message", f"manage_navigation.{action} completed."),
                "data": response.get("data"),
            }
        return response if isinstance(response, dict) else {"success": False, "message": str(response)}
    except Exception as e:
        return {"success": False, "message": f"Python error in manage_navigation: {str(e)}"}
