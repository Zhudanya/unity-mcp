using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools.Timeline
{
    [McpForUnityTool("manage_timeline", AutoRegister = false, Group = "timeline")]
    public static class ManageTimeline
    {
        // Runtime detection
        private static readonly Type TimelineAssetType =
            Type.GetType("UnityEngine.Timeline.TimelineAsset, Unity.Timeline");
        private static readonly Type PlayableDirectorType =
            Type.GetType("UnityEngine.Playables.PlayableDirector, UnityEngine.DirectorModule");

        private static readonly bool HasTimeline = TimelineAssetType != null;

        // Track type mappings (resolved lazily)
        private static readonly Dictionary<string, Type> TrackTypes = new Dictionary<string, Type>();

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("Parameters cannot be null.");

            if (!HasTimeline)
                return new ErrorResponse(
                    "Timeline package not installed. Install via: " +
                    "manage_packages(action='add_package', identifier='com.unity.timeline')");

            var p = new ToolParams(@params);
            string action = p.Get("action")?.ToLowerInvariant();

            if (string.IsNullOrEmpty(action))
                return new ErrorResponse("'action' parameter is required.");

            try
            {
                return action switch
                {
                    "ping" => new SuccessResponse("Timeline tool ready.", new { hasTimeline = true }),
                    "create_asset" => CreateAsset(@params),
                    "add_track" => AddTrack(@params),
                    "remove_track" => RemoveTrack(@params),
                    "add_clip" => AddClip(@params),
                    "set_clip_properties" => SetClipProperties(@params),
                    "set_binding" => SetBinding(@params),
                    "get_info" => GetInfo(@params),
                    "add_marker" => AddMarker(@params),
                    _ => new ErrorResponse($"Unknown action: '{action}'."),
                };
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Timeline action '{action}' failed: {ex.Message}",
                    new { stackTrace = ex.StackTrace });
            }
        }

        // ─────────────────────────────────────────────
        // create_asset
        // ─────────────────────────────────────────────

        private static object CreateAsset(JObject @params)
        {
            var p = new ToolParams(@params);
            string path = p.Get("path");
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("'path' parameter is required (e.g., 'Assets/Timelines/Intro.playable').");

            var asset = ScriptableObject.CreateInstance(TimelineAssetType);
            if (asset == null)
                return new ErrorResponse("Failed to create TimelineAsset.");

            string dir = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                CreateFolderRecursive(dir);

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            // Optionally assign to a PlayableDirector
            string directorTarget = p.Get("director_target") ?? p.Get("directorTarget");
            if (!string.IsNullOrEmpty(directorTarget))
            {
                var go = GameObject.Find(directorTarget);
                if (go != null && PlayableDirectorType != null)
                {
                    var director = go.GetComponent(PlayableDirectorType);
                    if (director == null) director = Undo.AddComponent(go, PlayableDirectorType);

                    var playableAssetProp = PlayableDirectorType.GetProperty("playableAsset",
                        BindingFlags.Instance | BindingFlags.Public);
                    playableAssetProp?.SetValue(director, asset);
                    EditorUtility.SetDirty(go);
                }
            }

            return new SuccessResponse($"TimelineAsset created at '{path}'.", new
            {
                path,
                directorTarget,
            });
        }

        // ─────────────────────────────────────────────
        // add_track
        // ─────────────────────────────────────────────

        private static object AddTrack(JObject @params)
        {
            var p = new ToolParams(@params);
            var (timeline, timelinePath, error) = LoadTimeline(p);
            if (timeline == null) return new ErrorResponse(error);

            string trackType = p.Get("track_type") ?? p.Get("trackType");
            if (string.IsNullOrEmpty(trackType))
                return new ErrorResponse("'track_type' is required (AnimationTrack, AudioTrack, ActivationTrack, ControlTrack, SignalTrack).");

            string trackName = p.Get("name") ?? p.Get("track_name") ?? p.Get("trackName");

            var resolvedType = ResolveTrackType(trackType);
            if (resolvedType == null)
                return new ErrorResponse($"Track type '{trackType}' not found. " +
                    "Available: AnimationTrack, AudioTrack, ActivationTrack, ControlTrack, SignalTrack");

            // Prefer generic CreateTrack<T>() — most reliable across Unity versions
            MethodInfo createMethod = null;
            var genericMethod = TimelineAssetType.GetMethods()
                .FirstOrDefault(m => m.Name == "CreateTrack" && m.IsGenericMethod
                    && m.GetParameters().Length <= 2);
            if (genericMethod != null)
                createMethod = genericMethod.MakeGenericMethod(resolvedType);

            // Fallback: non-generic overloads
            if (createMethod == null)
            {
                foreach (var m in TimelineAssetType.GetMethods()
                    .Where(m => m.Name == "CreateTrack" && !m.IsGenericMethod))
                {
                    var mp = m.GetParameters();
                    if (mp.Length >= 1 && mp[0].ParameterType == typeof(Type))
                    {
                        createMethod = m;
                        break;
                    }
                }
            }

            if (createMethod == null)
                return new ErrorResponse("Could not find CreateTrack method on TimelineAsset.");

            object track;
            var ctorParams = createMethod.GetParameters();
            if (ctorParams.Length == 0)
            {
                // Generic CreateTrack<T>() with no params
                track = createMethod.Invoke(timeline, null);
            }
            else if (ctorParams.Length == 1)
            {
                // CreateTrack<T>(TrackAsset parent) or CreateTrack(Type)
                if (ctorParams[0].ParameterType == typeof(Type))
                    track = createMethod.Invoke(timeline, new object[] { resolvedType });
                else
                    track = createMethod.Invoke(timeline, new object[] { null });
            }
            else if (ctorParams.Length == 2)
            {
                // CreateTrack<T>(TrackAsset parent, string name)
                track = createMethod.Invoke(timeline, new object[] { null, trackName });
            }
            else
            {
                // CreateTrack(Type, TrackAsset parent, string name)
                track = createMethod.Invoke(timeline, new object[] { resolvedType, null, trackName });
            }

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Track '{trackName ?? trackType}' added.", new
            {
                trackType = resolvedType.Name,
                trackName = trackName ?? resolvedType.Name,
                timelinePath,
            });
        }

        // ─────────────────────────────────────────────
        // remove_track
        // ─────────────────────────────────────────────

        private static object RemoveTrack(JObject @params)
        {
            var p = new ToolParams(@params);
            var (timeline, _, error) = LoadTimeline(p);
            if (timeline == null) return new ErrorResponse(error);

            string trackName = p.Get("track_name") ?? p.Get("trackName") ?? p.Get("name");
            int? trackIndex = p.GetInt("track_index") ?? p.GetInt("trackIndex");

            // Get tracks via reflection
            var getTracksMethod = TimelineAssetType.GetMethod("GetOutputTracks",
                BindingFlags.Instance | BindingFlags.Public)
                ?? TimelineAssetType.GetMethod("GetRootTracks", BindingFlags.Instance | BindingFlags.Public);

            if (getTracksMethod == null)
                return new ErrorResponse("Cannot enumerate tracks.");

            var tracks = ((System.Collections.IEnumerable)getTracksMethod.Invoke(timeline, null))
                .Cast<object>().ToList();

            object targetTrack = null;
            if (!string.IsNullOrEmpty(trackName))
            {
                foreach (var t in tracks)
                {
                    var nameProp = t.GetType().GetProperty("name");
                    if (nameProp?.GetValue(t)?.ToString() == trackName) { targetTrack = t; break; }
                }
            }
            else if (trackIndex.HasValue && trackIndex.Value < tracks.Count)
            {
                targetTrack = tracks[trackIndex.Value];
            }

            if (targetTrack == null)
                return new ErrorResponse($"Track not found: '{trackName ?? trackIndex?.ToString()}'.");

            var deleteMethod = TimelineAssetType.GetMethod("DeleteTrack",
                BindingFlags.Instance | BindingFlags.Public);
            deleteMethod?.Invoke(timeline, new[] { targetTrack });

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Track removed.", new { trackName });
        }

        // ─────────────────────────────────────────────
        // add_clip
        // ─────────────────────────────────────────────

        private static object AddClip(JObject @params)
        {
            var p = new ToolParams(@params);
            var (timeline, _, error) = LoadTimeline(p);
            if (timeline == null) return new ErrorResponse(error);

            string trackName = p.Get("track_name") ?? p.Get("trackName");
            if (string.IsNullOrEmpty(trackName))
                return new ErrorResponse("'track_name' is required.");

            var track = FindTrack(timeline, trackName);
            if (track == null)
                return new ErrorResponse($"Track '{trackName}' not found.");

            double startTime = (double?)@params["start_time"] ?? (double?)@params["startTime"] ?? 0;
            double duration = (double?)@params["duration"] ?? 1.0;

            // CreateDefaultClip via reflection
            var createClipMethod = track.GetType().GetMethod("CreateDefaultClip",
                BindingFlags.Instance | BindingFlags.Public);

            if (createClipMethod == null)
                return new ErrorResponse("CreateDefaultClip not available on this track type.");

            var clip = createClipMethod.Invoke(track, null);
            if (clip == null)
                return new ErrorResponse("Failed to create clip.");

            // Set clip timing
            var clipType = clip.GetType();
            var startProp = clipType.GetProperty("start");
            var durationProp = clipType.GetProperty("duration");
            startProp?.SetValue(clip, startTime);
            durationProp?.SetValue(clip, duration);

            string clipName = p.Get("clip_name") ?? p.Get("clipName");
            if (!string.IsNullOrEmpty(clipName))
            {
                var displayNameProp = clipType.GetProperty("displayName");
                displayNameProp?.SetValue(clip, clipName);
            }

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Clip added to track '{trackName}'.", new
            {
                trackName,
                startTime,
                duration,
                clipName,
            });
        }

        // ─────────────────────────────────────────────
        // set_clip_properties
        // ─────────────────────────────────────────────

        private static object SetClipProperties(JObject @params)
        {
            var p = new ToolParams(@params);
            var (timeline, _, error) = LoadTimeline(p);
            if (timeline == null) return new ErrorResponse(error);

            string trackName = p.Get("track_name") ?? p.Get("trackName");
            int clipIndex = p.GetInt("clip_index") ?? p.GetInt("clipIndex") ?? 0;

            var track = FindTrack(timeline, trackName);
            if (track == null) return new ErrorResponse($"Track '{trackName}' not found.");

            // Get clips
            var getClipsMethod = track.GetType().GetMethod("GetClips",
                BindingFlags.Instance | BindingFlags.Public);
            if (getClipsMethod == null)
                return new ErrorResponse("Cannot get clips from track.");

            var clips = ((System.Collections.IEnumerable)getClipsMethod.Invoke(track, null))
                .Cast<object>().ToList();

            if (clipIndex >= clips.Count)
                return new ErrorResponse($"Clip index {clipIndex} out of range (track has {clips.Count} clips).");

            var clip = clips[clipIndex];
            var clipType = clip.GetType();
            int changed = 0;

            double? start = (double?)@params["start_time"] ?? (double?)@params["startTime"];
            if (start.HasValue) { clipType.GetProperty("start")?.SetValue(clip, start.Value); changed++; }

            double? dur = (double?)@params["duration"];
            if (dur.HasValue) { clipType.GetProperty("duration")?.SetValue(clip, dur.Value); changed++; }

            double? clipIn = (double?)@params["clip_in"] ?? (double?)@params["clipIn"];
            if (clipIn.HasValue) { clipType.GetProperty("clipIn")?.SetValue(clip, clipIn.Value); changed++; }

            double? timeScale = (double?)@params["time_scale"] ?? (double?)@params["timeScale"];
            if (timeScale.HasValue) { clipType.GetProperty("timeScale")?.SetValue(clip, timeScale.Value); changed++; }

            string displayName = p.Get("display_name") ?? p.Get("displayName");
            if (!string.IsNullOrEmpty(displayName))
            {
                clipType.GetProperty("displayName")?.SetValue(clip, displayName);
                changed++;
            }

            if (changed == 0) return new ErrorResponse("No valid clip properties provided.");

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Clip properties updated ({changed} changes).", new
            {
                trackName, clipIndex, changed,
            });
        }

        // ─────────────────────────────────────────────
        // set_binding
        // ─────────────────────────────────────────────

        private static object SetBinding(JObject @params)
        {
            var p = new ToolParams(@params);

            string directorTarget = p.Get("director_target") ?? p.Get("directorTarget");
            if (string.IsNullOrEmpty(directorTarget))
                return new ErrorResponse("'director_target' is required (GameObject with PlayableDirector).");

            if (PlayableDirectorType == null)
                return new ErrorResponse("PlayableDirector type not found.");

            var directorGo = GameObject.Find(directorTarget);
            if (directorGo == null)
                return new ErrorResponse($"GameObject '{directorTarget}' not found.");

            var director = directorGo.GetComponent(PlayableDirectorType);
            if (director == null)
                return new ErrorResponse($"No PlayableDirector on '{directorTarget}'.");

            string trackName = p.Get("track_name") ?? p.Get("trackName");
            string bindTarget = p.Get("bind_target") ?? p.Get("bindTarget");

            if (string.IsNullOrEmpty(trackName) || string.IsNullOrEmpty(bindTarget))
                return new ErrorResponse("'track_name' and 'bind_target' are required.");

            // Get the timeline from the director
            var playableAssetProp = PlayableDirectorType.GetProperty("playableAsset",
                BindingFlags.Instance | BindingFlags.Public);
            var timeline = playableAssetProp?.GetValue(director) as ScriptableObject;
            if (timeline == null)
                return new ErrorResponse("PlayableDirector has no TimelineAsset assigned.");

            var track = FindTrack(timeline, trackName);
            if (track == null)
                return new ErrorResponse($"Track '{trackName}' not found in timeline.");

            var bindGo = GameObject.Find(bindTarget);
            if (bindGo == null)
                return new ErrorResponse($"Bind target '{bindTarget}' not found.");

            // director.SetGenericBinding(track, bindGo)
            var setBindingMethod = PlayableDirectorType.GetMethod("SetGenericBinding",
                BindingFlags.Instance | BindingFlags.Public);
            setBindingMethod?.Invoke(director, new object[] { track, bindGo });

            EditorUtility.SetDirty(directorGo);

            return new SuccessResponse($"Track '{trackName}' bound to '{bindTarget}'.", new
            {
                directorTarget,
                trackName,
                bindTarget,
            });
        }

        // ─────────────────────────────────────────────
        // get_info
        // ─────────────────────────────────────────────

        private static object GetInfo(JObject @params)
        {
            var p = new ToolParams(@params);
            var (timeline, timelinePath, error) = LoadTimeline(p);
            if (timeline == null) return new ErrorResponse(error);

            // Get duration
            var durationProp = TimelineAssetType.GetProperty("duration",
                BindingFlags.Instance | BindingFlags.Public);
            double duration = (double?)durationProp?.GetValue(timeline) ?? 0;

            // Get tracks
            var getTracksMethod = TimelineAssetType.GetMethod("GetOutputTracks",
                BindingFlags.Instance | BindingFlags.Public)
                ?? TimelineAssetType.GetMethod("GetRootTracks", BindingFlags.Instance | BindingFlags.Public);

            var tracks = new List<object>();
            if (getTracksMethod != null)
            {
                foreach (var track in (System.Collections.IEnumerable)getTracksMethod.Invoke(timeline, null))
                {
                    var trackType = track.GetType();
                    var nameProp = trackType.GetProperty("name");
                    var getClips = trackType.GetMethod("GetClips", BindingFlags.Instance | BindingFlags.Public);
                    int clipCount = 0;
                    if (getClips != null)
                        clipCount = ((System.Collections.IEnumerable)getClips.Invoke(track, null)).Cast<object>().Count();

                    tracks.Add(new
                    {
                        name = nameProp?.GetValue(track)?.ToString(),
                        type = trackType.Name,
                        clipCount,
                    });
                }
            }

            return new SuccessResponse($"Timeline info for '{timelinePath}'.", new
            {
                path = timelinePath,
                duration,
                trackCount = tracks.Count,
                tracks,
            });
        }

        // ─────────────────────────────────────────────
        // add_marker
        // ─────────────────────────────────────────────

        private static object AddMarker(JObject @params)
        {
            var p = new ToolParams(@params);
            var (timeline, _, error) = LoadTimeline(p);
            if (timeline == null) return new ErrorResponse(error);

            double time = (double?)@params["time"] ?? 0;

            // MarkerTrack: timeline.markerTrack
            var markerTrackProp = TimelineAssetType.GetProperty("markerTrack",
                BindingFlags.Instance | BindingFlags.Public);

            object markerTrack = markerTrackProp?.GetValue(timeline);

            // If no marker track, create one
            if (markerTrack == null)
            {
                var createMarkerTrack = TimelineAssetType.GetMethod("CreateMarkerTrack",
                    BindingFlags.Instance | BindingFlags.Public);
                createMarkerTrack?.Invoke(timeline, null);
                markerTrack = markerTrackProp?.GetValue(timeline);
            }

            if (markerTrack == null)
                return new ErrorResponse("Could not create marker track.");

            // Try to create a SignalEmitter marker
            var signalEmitterType = Type.GetType("UnityEngine.Timeline.SignalEmitter, Unity.Timeline");
            if (signalEmitterType != null)
            {
                var createMarker = markerTrack.GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == "CreateMarker" && m.IsGenericMethod);
                if (createMarker != null)
                {
                    var genericCreate = createMarker.MakeGenericMethod(signalEmitterType);
                    genericCreate.Invoke(markerTrack, new object[] { time });
                }
            }

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Marker added at time {time}.", new { time });
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private static (ScriptableObject timeline, string path, string error) LoadTimeline(ToolParams p)
        {
            string path = p.Get("timeline") ?? p.Get("timeline_path") ?? p.Get("timelinePath") ?? p.Get("path");
            if (string.IsNullOrEmpty(path))
                return (null, null, "'timeline' path parameter is required.");

            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null || !TimelineAssetType.IsAssignableFrom(asset.GetType()))
                return (null, path, $"TimelineAsset not found at '{path}'.");

            return (asset, path, null);
        }

        private static object FindTrack(ScriptableObject timeline, string trackName)
        {
            if (string.IsNullOrEmpty(trackName)) return null;

            var getTracksMethod = TimelineAssetType.GetMethod("GetOutputTracks",
                BindingFlags.Instance | BindingFlags.Public)
                ?? TimelineAssetType.GetMethod("GetRootTracks", BindingFlags.Instance | BindingFlags.Public);

            if (getTracksMethod == null) return null;

            foreach (var track in (System.Collections.IEnumerable)getTracksMethod.Invoke(timeline, null))
            {
                var nameProp = track.GetType().GetProperty("name");
                if (nameProp?.GetValue(track)?.ToString() == trackName) return track;
            }
            return null;
        }

        private static Type ResolveTrackType(string trackTypeName)
        {
            if (TrackTypes.TryGetValue(trackTypeName, out var cached)) return cached;

            // Common track types in UnityEngine.Timeline assembly
            string[] candidates = {
                $"UnityEngine.Timeline.{trackTypeName}, Unity.Timeline",
                $"UnityEngine.Timeline.{trackTypeName}Track, Unity.Timeline",
            };

            foreach (var candidate in candidates)
            {
                var type = Type.GetType(candidate);
                if (type != null)
                {
                    TrackTypes[trackTypeName] = type;
                    return type;
                }
            }

            // Try without suffix
            if (!trackTypeName.EndsWith("Track"))
            {
                var type = Type.GetType($"UnityEngine.Timeline.{trackTypeName}Track, Unity.Timeline");
                if (type != null)
                {
                    TrackTypes[trackTypeName] = type;
                    return type;
                }
            }

            return null;
        }

        private static void CreateFolderRecursive(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
