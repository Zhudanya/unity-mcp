from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    group="terrain",
    description=(
        "Terrain creation and editing: heightmap import/export, texture painting, tree/grass placement. "
        "Actions: "
        "create - Create a new Terrain with TerrainData (size, heightmap_resolution). "
        "set_heightmap - Import heightmap from file (RAW16, PNG, EXR). Does NOT accept float arrays. "
        "export_heightmap - Export heightmap to RAW16 file. "
        "get_heightmap - Read height values for a region (max 64x64). Requires 'region' parameter. "
        "raise_lower - Raise or lower terrain at a world position (strength > 0 = raise, < 0 = lower). "
        "smooth - Smooth terrain at a world position with configurable iterations. "
        "add_terrain_layer - Add a texture layer (diffuse + optional normal + tile_size). "
        "paint_texture - Paint a texture layer at a position with radius and opacity. "
        "add_tree_prototype - Register a tree prefab for placement. "
        "paint_trees - Place trees randomly in an area with density control. "
        "add_detail_prototype - Register a grass/detail texture. "
        "paint_details - Fill an area with detail objects at specified density. "
        "set_properties - Set terrain properties (size, detail distance, tree distance). "
        "get_info - Get terrain stats (resolution, layer count, tree count, etc.)."
    ),
    annotations=ToolAnnotations(
        title="Manage Terrain",
        destructiveHint=True,
    ),
)
async def manage_terrain(
    ctx: Context,
    action: Annotated[
        Literal[
            "create", "set_heightmap", "export_heightmap", "get_heightmap",
            "raise_lower", "smooth",
            "add_terrain_layer", "paint_texture",
            "add_tree_prototype", "paint_trees",
            "add_detail_prototype", "paint_details",
            "set_properties", "get_info",
        ],
        "Action to perform.",
    ],
    target: Annotated[str | int, "Target Terrain (name or instance ID). Default: active terrain."] | None = None,
    # --- create ---
    name: Annotated[str, "Terrain name (default 'Terrain')"] | None = None,
    size: Annotated[list[float], "Terrain size [x, height, z] (default [500,200,500])"] | None = None,
    heightmap_resolution: Annotated[int, "Must be 2^n+1: 33,65,129,257,513,1025,2049,4097"] | None = None,
    position: Annotated[list[float], "World position [x, y, z]"] | None = None,
    data_path: Annotated[str, "TerrainData asset path"] | None = None,
    # --- heightmap ---
    source_path: Annotated[str, "Heightmap file path (RAW16/PNG/EXR) for set_heightmap"] | None = None,
    output_path: Annotated[str, "Output file path for export_heightmap"] | None = None,
    region: Annotated[list[int], "Region [x, y, width, height] for get_heightmap (max 64x64)"] | None = None,
    # --- raise_lower / smooth ---
    radius: Annotated[float, "Brush radius in world units"] | None = None,
    strength: Annotated[float, "Raise/lower strength (positive=raise, negative=lower)"] | None = None,
    iterations: Annotated[int, "Smooth iterations (default 1)"] | None = None,
    # --- terrain layer ---
    diffuse: Annotated[str, "Diffuse texture asset path"] | None = None,
    normal: Annotated[str, "Normal map texture asset path"] | None = None,
    tile_size: Annotated[list[float], "Texture tile size [x, y]"] | None = None,
    # --- paint texture ---
    layer_index: Annotated[int, "Terrain layer index to paint"] | None = None,
    opacity: Annotated[float, "Paint opacity 0-1 (default 1)"] | None = None,
    # --- trees ---
    prefab: Annotated[str, "Tree prefab asset path"] | None = None,
    prototype_index: Annotated[int, "Tree/detail prototype index"] | None = None,
    density: Annotated[int, "Number of trees or detail density per patch"] | None = None,
    min_height: Annotated[float, "Min tree/detail height scale"] | None = None,
    max_height: Annotated[float, "Max tree/detail height scale"] | None = None,
    random_rotation: Annotated[bool, "Randomize tree rotation"] | None = None,
    area: Annotated[list[int], "Placement area [minX, minY, maxX, maxY] in heightmap coords"] | None = None,
    # --- detail ---
    texture: Annotated[str, "Detail/grass texture asset path"] | None = None,
    # --- properties ---
    detail_object_distance: Annotated[float, "Detail object draw distance"] | None = None,
    tree_distance: Annotated[float, "Tree draw distance"] | None = None,
    tree_billboard_distance: Annotated[float, "Tree billboard distance"] | None = None,
    heightmap_pixel_error: Annotated[float, "Heightmap pixel error (LOD)"] | None = None,
) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params_dict: dict[str, Any] = {"action": action}

    param_mapping = {
        "target": target,
        "name": name,
        "size": size,
        "heightmapResolution": heightmap_resolution,
        "position": position,
        "dataPath": data_path,
        "sourcePath": source_path,
        "outputPath": output_path,
        "region": region,
        "radius": radius,
        "strength": strength,
        "iterations": iterations,
        "diffuse": diffuse,
        "normal": normal,
        "tileSize": tile_size,
        "layerIndex": layer_index,
        "opacity": opacity,
        "prefab": prefab,
        "prototypeIndex": prototype_index,
        "density": density,
        "minHeight": min_height,
        "maxHeight": max_height,
        "randomRotation": random_rotation,
        "area": area,
        "texture": texture,
        "detailObjectDistance": detail_object_distance,
        "treeDistance": tree_distance,
        "treeBillboardDistance": tree_billboard_distance,
        "heightmapPixelError": heightmap_pixel_error,
    }

    for k, v in param_mapping.items():
        if v is not None:
            params_dict[k] = v

    try:
        response = await send_with_unity_instance(
            async_send_command_with_retry, unity_instance,
            "manage_terrain", params_dict,
        )
        if isinstance(response, dict) and response.get("success"):
            return {
                "success": True,
                "message": response.get("message", f"manage_terrain.{action} completed."),
                "data": response.get("data"),
            }
        return response if isinstance(response, dict) else {"success": False, "message": str(response)}
    except Exception as e:
        return {"success": False, "message": f"Python error in manage_terrain: {str(e)}"}
