from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    group="audio",
    description=(
        "Audio system tools: configure AudioSource, set clip import settings, playback control. "
        "Actions: "
        "configure_source - Set AudioSource properties in one call (clip, volume, pitch, spatial, loop, etc.). "
        "get_info - Read AudioSource state and configuration. "
        "set_import_settings - Configure AudioClip import (load_type, compression, quality, sample_rate). "
        "play / stop / pause - Playback control (Play Mode only). "
        "create_mixer - Create AudioMixer asset (EXPERIMENTAL: uses internal API, may not work on all Unity versions)."
    ),
    annotations=ToolAnnotations(
        title="Manage Audio",
        destructiveHint=True,
    ),
)
async def manage_audio(
    ctx: Context,
    action: Annotated[
        Literal["configure_source", "get_info", "set_import_settings",
                "play", "stop", "pause", "create_mixer"],
        "Action to perform.",
    ],
    target: Annotated[str | int, "Target GameObject (name or instance ID)"] | None = None,
    # --- configure_source ---
    clip: Annotated[str, "AudioClip asset path"] | None = None,
    volume: Annotated[float, "Volume 0-1"] | None = None,
    pitch: Annotated[float, "Pitch multiplier"] | None = None,
    spatial_blend: Annotated[float, "Spatial blend: 0=2D, 1=3D"] | None = None,
    loop: Annotated[bool, "Loop playback"] | None = None,
    play_on_awake: Annotated[bool, "Play on awake"] | None = None,
    mute: Annotated[bool, "Mute"] | None = None,
    min_distance: Annotated[float, "3D sound min distance"] | None = None,
    max_distance: Annotated[float, "3D sound max distance"] | None = None,
    rolloff: Annotated[str, "Rolloff mode: Logarithmic, Linear, Custom"] | None = None,
    priority: Annotated[int, "Audio priority 0-256"] | None = None,
    output_mixer_group: Annotated[str, "AudioMixer asset path for output routing"] | None = None,
    # --- set_import_settings ---
    path: Annotated[str, "AudioClip asset path for import settings"] | None = None,
    load_type: Annotated[str, "Load type: DecompressOnLoad, CompressedInMemory, Streaming"] | None = None,
    compression_format: Annotated[str, "Compression: Vorbis, ADPCM, PCM, MP3"] | None = None,
    quality: Annotated[float, "Compression quality 0-1 (Vorbis only)"] | None = None,
    sample_rate_setting: Annotated[str, "Sample rate: PreserveSampleRate, OptimizeSampleRate, OverrideSampleRate"] | None = None,
    force_mono: Annotated[bool, "Force mono downmix"] | None = None,
    load_in_background: Annotated[bool, "Load in background"] | None = None,
    preload_audio_data: Annotated[bool, "Preload audio data"] | None = None,
) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params_dict: dict[str, Any] = {"action": action}

    param_mapping = {
        "target": target,
        "clip": clip,
        "volume": volume,
        "pitch": pitch,
        "spatialBlend": spatial_blend,
        "loop": loop,
        "playOnAwake": play_on_awake,
        "mute": mute,
        "minDistance": min_distance,
        "maxDistance": max_distance,
        "rolloff": rolloff,
        "priority": priority,
        "outputMixerGroup": output_mixer_group,
        "path": path,
        "loadType": load_type,
        "compressionFormat": compression_format,
        "quality": quality,
        "sampleRateSetting": sample_rate_setting,
        "forceMono": force_mono,
        "loadInBackground": load_in_background,
        "preloadAudioData": preload_audio_data,
    }

    for k, v in param_mapping.items():
        if v is not None:
            params_dict[k] = v

    try:
        response = await send_with_unity_instance(
            async_send_command_with_retry, unity_instance,
            "manage_audio", params_dict,
        )
        if isinstance(response, dict) and response.get("success"):
            return {
                "success": True,
                "message": response.get("message", f"manage_audio.{action} completed."),
                "data": response.get("data"),
            }
        return response if isinstance(response, dict) else {"success": False, "message": str(response)}
    except Exception as e:
        return {"success": False, "message": f"Python error in manage_audio: {str(e)}"}
