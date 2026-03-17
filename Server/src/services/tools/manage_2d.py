from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    group="2d",
    description=(
        "2D tools: Tilemap editing and SpriteAtlas management. "
        "Actions: "
        "tilemap_set_tile - Place or clear a tile at a grid position. "
        "tilemap_fill - Fill a rectangular region with a tile. "
        "tilemap_clear - Clear all tiles from a Tilemap. "
        "tilemap_get_info - Get Tilemap bounds and size info. "
        "create_sprite_atlas - Create a SpriteAtlas asset. "
        "atlas_add_folders - Add sprite folders to an atlas. "
        "atlas_pack - Pack all SpriteAtlases for the current build target."
    ),
    annotations=ToolAnnotations(
        title="Manage 2D",
        destructiveHint=True,
    ),
)
async def manage_2d(
    ctx: Context,
    action: Annotated[
        Literal["tilemap_set_tile", "tilemap_fill", "tilemap_clear",
                "tilemap_get_info", "create_sprite_atlas",
                "atlas_add_folders", "atlas_pack"],
        "Action to perform.",
    ],
    target: Annotated[str | int, "Target Tilemap GameObject (name or ID). Defaults to first Tilemap in scene."] | None = None,
    # --- tilemap_set_tile ---
    position: Annotated[list[int], "Grid position [x, y] or [x, y, z]"] | None = None,
    tile: Annotated[str, "Tile asset path (null to clear)"] | None = None,
    # --- tilemap_fill ---
    from_pos: Annotated[list[int], "Fill start [x, y]"] | None = None,
    to_pos: Annotated[list[int], "Fill end [x, y]"] | None = None,
    # --- sprite atlas ---
    path: Annotated[str, "Atlas asset path for create, or atlas path for add_folders"] | None = None,
    atlas: Annotated[str, "SpriteAtlas asset path"] | None = None,
    folders: Annotated[list[str], "Sprite folder paths to add to atlas"] | None = None,
) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params_dict: dict[str, Any] = {"action": action}

    param_mapping = {
        "target": target,
        "position": position,
        "tile": tile,
        "fromPos": from_pos,
        "toPos": to_pos,
        "path": path,
        "atlas": atlas,
        "folders": folders,
    }

    for k, v in param_mapping.items():
        if v is not None:
            params_dict[k] = v

    try:
        response = await send_with_unity_instance(
            async_send_command_with_retry, unity_instance,
            "manage_2d", params_dict,
        )
        if isinstance(response, dict) and response.get("success"):
            return {
                "success": True,
                "message": response.get("message", f"manage_2d.{action} completed."),
                "data": response.get("data"),
            }
        return response if isinstance(response, dict) else {"success": False, "message": str(response)}
    except Exception as e:
        return {"success": False, "message": f"Python error in manage_2d: {str(e)}"}
