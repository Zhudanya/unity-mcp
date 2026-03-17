from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    group="timeline",
    description=(
        "Timeline asset management: create timelines, add tracks/clips, set bindings. "
        "Requires com.unity.timeline package. "
        "Actions: "
        "create_asset - Create a TimelineAsset, optionally assign to a PlayableDirector. "
        "add_track - Add a track (AnimationTrack, AudioTrack, ActivationTrack, ControlTrack, SignalTrack). "
        "remove_track - Remove a track by name or index. "
        "add_clip - Add a default clip to a track with start_time and duration. "
        "set_clip_properties - Modify clip timing (start, duration, clipIn, timeScale). "
        "set_binding - Bind a track to a scene GameObject via PlayableDirector. "
        "get_info - Read timeline structure (tracks, clips, duration). "
        "add_marker - Add a SignalEmitter marker at a specific time."
    ),
    annotations=ToolAnnotations(
        title="Manage Timeline",
        destructiveHint=True,
    ),
)
async def manage_timeline(
    ctx: Context,
    action: Annotated[
        Literal["create_asset", "add_track", "remove_track", "add_clip",
                "set_clip_properties", "set_binding", "get_info", "add_marker"],
        "Action to perform.",
    ],
    # --- common ---
    timeline: Annotated[str, "TimelineAsset path (e.g., 'Assets/Timelines/Intro.playable')"] | None = None,
    # --- create_asset ---
    path: Annotated[str, "Asset path for create_asset"] | None = None,
    director_target: Annotated[str, "GameObject to assign PlayableDirector"] | None = None,
    # --- add_track ---
    track_type: Annotated[str, "Track type: AnimationTrack, AudioTrack, ActivationTrack, ControlTrack, SignalTrack"] | None = None,
    name: Annotated[str, "Track name"] | None = None,
    # --- remove_track ---
    track_name: Annotated[str, "Track name to remove or reference"] | None = None,
    track_index: Annotated[int, "Track index to remove"] | None = None,
    # --- add_clip ---
    start_time: Annotated[float, "Clip start time in seconds"] | None = None,
    duration: Annotated[float, "Clip duration in seconds"] | None = None,
    clip_name: Annotated[str, "Display name for the clip"] | None = None,
    # --- set_clip_properties ---
    clip_index: Annotated[int, "Clip index on the track (default 0)"] | None = None,
    clip_in: Annotated[float, "Clip in point (seconds)"] | None = None,
    time_scale: Annotated[float, "Clip time scale (playback speed)"] | None = None,
    display_name: Annotated[str, "Clip display name"] | None = None,
    # --- set_binding ---
    bind_target: Annotated[str, "Scene GameObject to bind the track to"] | None = None,
    # --- add_marker ---
    time: Annotated[float, "Marker time in seconds"] | None = None,
) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params_dict: dict[str, Any] = {"action": action}

    param_mapping = {
        "timeline": timeline,
        "path": path,
        "directorTarget": director_target,
        "trackType": track_type,
        "name": name,
        "trackName": track_name,
        "trackIndex": track_index,
        "startTime": start_time,
        "duration": duration,
        "clipName": clip_name,
        "clipIndex": clip_index,
        "clipIn": clip_in,
        "timeScale": time_scale,
        "displayName": display_name,
        "bindTarget": bind_target,
        "time": time,
    }

    for k, v in param_mapping.items():
        if v is not None:
            params_dict[k] = v

    try:
        response = await send_with_unity_instance(
            async_send_command_with_retry, unity_instance,
            "manage_timeline", params_dict,
        )
        if isinstance(response, dict) and response.get("success"):
            return {
                "success": True,
                "message": response.get("message", f"manage_timeline.{action} completed."),
                "data": response.get("data"),
            }
        return response if isinstance(response, dict) else {"success": False, "message": str(response)}
    except Exception as e:
        return {"success": False, "message": f"Python error in manage_timeline: {str(e)}"}
