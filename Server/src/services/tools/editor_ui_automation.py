from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    group="ui",
    description=(
        "Browser-like UI automation for the Unity Editor. Works with both UIElements and IMGUI windows. "
        "Actions: "
        "snapshot - Capture a text tree of all Editor windows with element references (@e1, @e2, ...) for UIElements windows. "
        "screenshot - Capture a window as a base64 PNG image (works with ANY window including IMGUI). AI can analyze the image to understand the UI layout. "
        "click - Click a UI element by reference (e.g. '@e3'). Works on buttons, toggles, foldouts (UIElements only). "
        "type - Type text into a field or send special keys (Enter, Tab, Escape, Ctrl+S, etc.) (UIElements only). "
        "drag - Drag an element or asset to a target element (UIElements only). "
        "send_event - Send raw mouse/keyboard events to ANY window by coordinates (works with IMGUI). "
        "  event_types: mouse_click, mouse_double_click, mouse_down, mouse_up, mouse_move, mouse_drag, key_down, key_up, scroll, drag_asset. "
        "focus_window - Focus a specific Editor window by title or type name. "
        "Workflow for IMGUI windows: screenshot -> analyze image -> send_event at coordinates. "
        "Workflow for UIElements windows: snapshot -> click/type/drag by @eN reference."
    ),
    annotations=ToolAnnotations(
        title="Editor UI Automation",
        destructiveHint=True,
    ),
)
async def editor_ui_automation(
    ctx: Context,
    action: Annotated[
        Literal["snapshot", "screenshot", "click", "type", "drag", "send_event", "focus_window"],
        "Action to perform. Use 'snapshot' for UIElements windows, 'screenshot' + 'send_event' for IMGUI windows.",
    ],
    # --- Common params ---
    ref: Annotated[
        str,
        "Element reference from snapshot (e.g. '@e3'). For click, type actions.",
    ]
    | None = None,
    window_title: Annotated[
        str,
        "Window title (substring match). For screenshot, send_event, focus_window.",
    ]
    | None = None,
    window_type: Annotated[
        str,
        "Window type name. For screenshot, send_event, focus_window.",
    ]
    | None = None,
    # --- snapshot params ---
    window_filter: Annotated[
        str,
        "Filter snapshot to windows matching this title substring (e.g. 'Inspector').",
    ]
    | None = None,
    max_depth: Annotated[
        int,
        "Maximum traversal depth for snapshot (default 15).",
    ]
    | None = None,
    include_styles: Annotated[
        bool,
        "Include element size info in snapshot output (default false).",
    ]
    | None = None,
    # --- screenshot params ---
    max_resolution: Annotated[
        int,
        "Max edge length for screenshot image in pixels (default 800). For screenshot action.",
    ]
    | None = None,
    # --- type params ---
    text: Annotated[
        str,
        "Text to type. For 'type' and 'send_event' (key_down) actions.",
    ]
    | None = None,
    keys: Annotated[
        list[str],
        "Special keys to send (e.g. ['Enter'], ['Ctrl+S']). For 'type' action.",
    ]
    | None = None,
    clear: Annotated[
        bool,
        "Clear field before typing (default false). For 'type' action.",
    ]
    | None = None,
    # --- click params ---
    click_count: Annotated[
        int,
        "Number of clicks (1=single, 2=double). For 'click' action.",
    ]
    | None = None,
    # --- drag params ---
    from_ref: Annotated[
        str,
        "Source element reference (e.g. '@e5'). For 'drag' action.",
    ]
    | None = None,
    to_ref: Annotated[
        str,
        "Target element reference (e.g. '@e10'). For 'drag' action.",
    ]
    | None = None,
    asset_path: Annotated[
        str,
        "Asset path (e.g. 'Assets/Maps/level1.asset'). For drag and send_event(drag_asset).",
    ]
    | None = None,
    steps: Annotated[
        int,
        "Interpolation steps for drag (default 10).",
    ]
    | None = None,
    # --- send_event params ---
    event_type: Annotated[
        str,
        "Event type for send_event: mouse_click, mouse_double_click, mouse_down, mouse_up, mouse_move, mouse_drag, key_down, key_up, scroll, drag_asset.",
    ]
    | None = None,
    x: Annotated[
        float,
        "X coordinate in window-local space. For send_event actions.",
    ]
    | None = None,
    y: Annotated[
        float,
        "Y coordinate in window-local space. For send_event actions.",
    ]
    | None = None,
    from_x: Annotated[
        float,
        "Start X for mouse_drag. For send_event(mouse_drag).",
    ]
    | None = None,
    from_y: Annotated[
        float,
        "Start Y for mouse_drag. For send_event(mouse_drag).",
    ]
    | None = None,
    to_x: Annotated[
        float,
        "End X for mouse_drag. For send_event(mouse_drag).",
    ]
    | None = None,
    to_y: Annotated[
        float,
        "End Y for mouse_drag. For send_event(mouse_drag).",
    ]
    | None = None,
    button: Annotated[
        int,
        "Mouse button: 0=left, 1=right, 2=middle (default 0). For send_event mouse actions.",
    ]
    | None = None,
    key: Annotated[
        str,
        "Key name (e.g. 'Enter', 'Ctrl+S', 'a'). For send_event(key_down/key_up).",
    ]
    | None = None,
    delta_x: Annotated[
        float,
        "Horizontal scroll delta. For send_event(scroll).",
    ]
    | None = None,
    delta_y: Annotated[
        float,
        "Vertical scroll delta. For send_event(scroll).",
    ]
    | None = None,
) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    # Build params dict, converting snake_case to camelCase for C#
    params_dict: dict[str, Any] = {"action": action}

    # Map all non-None parameters
    param_mapping = {
        "ref": ref,
        "windowTitle": window_title,
        "windowType": window_type,
        "windowFilter": window_filter,
        "maxDepth": max_depth,
        "includeStyles": include_styles,
        "maxResolution": max_resolution,
        "text": text,
        "keys": keys,
        "clear": clear,
        "clickCount": click_count,
        "fromRef": from_ref,
        "toRef": to_ref,
        "assetPath": asset_path,
        "steps": steps,
        "eventType": event_type,
        "x": x,
        "y": y,
        "fromX": from_x,
        "fromY": from_y,
        "toX": to_x,
        "toY": to_y,
        "button": button,
        "key": key,
        "deltaX": delta_x,
        "deltaY": delta_y,
    }

    for k, v in param_mapping.items():
        if v is not None:
            params_dict[k] = v

    try:
        response = await send_with_unity_instance(
            async_send_command_with_retry,
            unity_instance,
            "editor_ui_automation",
            params_dict,
        )

        if isinstance(response, dict) and response.get("success"):
            return {
                "success": True,
                "message": response.get("message", f"editor_ui_automation.{action} completed."),
                "data": response.get("data"),
            }
        return response if isinstance(response, dict) else {"success": False, "message": str(response)}

    except Exception as e:
        return {"success": False, "message": f"Python error in editor_ui_automation: {str(e)}"}
