from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    description=(
        "Manage Unity build pipeline, PlayerSettings, platform switching, and scripting defines. "
        "Actions: "
        "get_player_settings - Read company name, product name, bundle ID, version, screen size, scripting backend. "
        "set_player_settings - Update PlayerSettings fields (company_name, product_name, version, etc.). "
        "get_build_settings - Get scenes in build and current target platform. "
        "set_build_scenes - Set the scene list for build (array of scene paths). "
        "switch_platform - Switch active build target (Windows, Mac, Linux, Android, iOS, WebGL). "
        "  WARNING: triggers Unity recompilation and may temporarily disconnect. "
        "build - Execute BuildPipeline.BuildPlayer. Requires output_path. Long-running operation. "
        "get_scripting_defines - Read scripting define symbols for a platform. "
        "set_scripting_defines - Set scripting define symbols. Triggers recompilation."
    ),
    annotations=ToolAnnotations(
        title="Manage Build",
        destructiveHint=True,
    ),
)
async def manage_build(
    ctx: Context,
    action: Annotated[
        Literal[
            "get_player_settings", "set_player_settings",
            "get_build_settings", "set_build_scenes",
            "switch_platform", "build",
            "get_scripting_defines", "set_scripting_defines",
        ],
        "Action to perform.",
    ],
    # --- set_player_settings ---
    company_name: Annotated[str, "Company name"] | None = None,
    product_name: Annotated[str, "Product name"] | None = None,
    bundle_identifier: Annotated[str, "Application identifier (e.g., com.company.app)"] | None = None,
    version: Annotated[str, "Bundle version string (e.g., '1.2.0')"] | None = None,
    default_screen_width: Annotated[int, "Default screen width"] | None = None,
    default_screen_height: Annotated[int, "Default screen height"] | None = None,
    run_in_background: Annotated[bool, "Run in background"] | None = None,
    # --- set_build_scenes ---
    scenes: Annotated[
        list[str],
        "Array of scene paths for build (e.g., ['Assets/Scenes/Main.unity'])",
    ] | None = None,
    # --- switch_platform ---
    platform: Annotated[
        str,
        "Target platform: Windows, Mac, Linux, Android, iOS, WebGL",
    ] | None = None,
    # --- build ---
    output_path: Annotated[str, "Build output path (e.g., 'Builds/game.exe')"] | None = None,
    options: Annotated[
        list[str],
        "Build options: Development, AllowDebugging, CompressWithLz4, etc.",
    ] | None = None,
    # --- scripting defines ---
    defines: Annotated[
        list[str],
        "Scripting define symbols (e.g., ['ENABLE_ANALYTICS', 'DEBUG_MODE'])",
    ] | None = None,
) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params_dict: dict[str, Any] = {"action": action}

    param_mapping = {
        "companyName": company_name,
        "productName": product_name,
        "bundleIdentifier": bundle_identifier,
        "version": version,
        "defaultScreenWidth": default_screen_width,
        "defaultScreenHeight": default_screen_height,
        "runInBackground": run_in_background,
        "scenes": scenes,
        "platform": platform,
        "outputPath": output_path,
        "options": options,
        "defines": defines,
    }

    for k, v in param_mapping.items():
        if v is not None:
            params_dict[k] = v

    try:
        response = await send_with_unity_instance(
            async_send_command_with_retry,
            unity_instance,
            "manage_build",
            params_dict,
        )

        if isinstance(response, dict) and response.get("success"):
            return {
                "success": True,
                "message": response.get("message", f"manage_build.{action} completed."),
                "data": response.get("data"),
            }
        return response if isinstance(response, dict) else {"success": False, "message": str(response)}

    except Exception as e:
        return {"success": False, "message": f"Python error in manage_build: {str(e)}"}
