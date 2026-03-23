using System;
using System.Linq;
using System.Reflection;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools.Audio
{
    [McpForUnityTool("manage_audio", AutoRegister = false, Group = "core")]
    public static class ManageAudio
    {
        // Runtime detection for AudioMixer internal API
        private static readonly Type AudioMixerControllerType =
            Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("Parameters cannot be null.");

            var p = new ToolParams(@params);
            string action = p.Get("action")?.ToLowerInvariant();

            if (string.IsNullOrEmpty(action))
                return new ErrorResponse("'action' parameter is required.");

            try
            {
                return action switch
                {
                    "ping" => new SuccessResponse("Audio tool ready.", new
                    {
                        hasMixerApi = AudioMixerControllerType != null,
                    }),
                    "configure_source" => ConfigureSource(@params),
                    "get_info" => GetInfo(@params),
                    "set_import_settings" => SetImportSettings(@params),
                    "play" => PlaybackControl(@params, "play"),
                    "stop" => PlaybackControl(@params, "stop"),
                    "pause" => PlaybackControl(@params, "pause"),
                    "create_mixer" => CreateMixer(@params),
                    _ => new ErrorResponse($"Unknown action: '{action}'."),
                };
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Audio action '{action}' failed: {ex.Message}",
                    new { stackTrace = ex.StackTrace });
            }
        }

        // ─────────────────────────────────────────────
        // configure_source
        // ─────────────────────────────────────────────

        private static object ConfigureSource(JObject @params)
        {
            var p = new ToolParams(@params);
            var go = FindTarget(p);
            if (go == null) return new ErrorResponse("Target GameObject not found.");

            var source = go.GetComponent<AudioSource>();
            if (source == null) source = Undo.AddComponent<AudioSource>(go);

            Undo.RecordObject(source, "Configure AudioSource");

            string clipPath = p.Get("clip") ?? p.Get("clip_path") ?? p.Get("clipPath");
            if (!string.IsNullOrEmpty(clipPath))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip != null) source.clip = clip;
                else return new ErrorResponse($"AudioClip not found: '{clipPath}'.");
            }

            float? volume = p.GetFloat("volume");
            if (volume.HasValue) source.volume = volume.Value;

            float? pitch = p.GetFloat("pitch");
            if (pitch.HasValue) source.pitch = pitch.Value;

            float? spatialBlend = p.GetFloat("spatial_blend") ?? p.GetFloat("spatialBlend");
            if (spatialBlend.HasValue) source.spatialBlend = spatialBlend.Value;

            if (@params["loop"] != null)
                source.loop = (bool)@params["loop"];

            if (@params["play_on_awake"] != null || @params["playOnAwake"] != null)
                source.playOnAwake = (bool?)@params["play_on_awake"] ?? (bool?)@params["playOnAwake"] ?? true;

            if (@params["mute"] != null)
                source.mute = (bool)@params["mute"];

            float? minDistance = p.GetFloat("min_distance") ?? p.GetFloat("minDistance");
            if (minDistance.HasValue) source.minDistance = minDistance.Value;

            float? maxDistance = p.GetFloat("max_distance") ?? p.GetFloat("maxDistance");
            if (maxDistance.HasValue) source.maxDistance = maxDistance.Value;

            string rolloff = p.Get("rolloff") ?? p.Get("rolloff_mode") ?? p.Get("rolloffMode");
            if (!string.IsNullOrEmpty(rolloff) && Enum.TryParse<AudioRolloffMode>(rolloff, true, out var mode))
                source.rolloffMode = mode;

            int? priority = p.GetInt("priority");
            if (priority.HasValue) source.priority = Mathf.Clamp(priority.Value, 0, 256);

            // Mixer group assignment
            string mixerGroupPath = p.Get("output_mixer_group") ?? p.Get("outputMixerGroup");
            if (!string.IsNullOrEmpty(mixerGroupPath))
            {
                var mixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(mixerGroupPath);
                if (mixer != null)
                {
                    var groups = mixer.FindMatchingGroups("Master");
                    if (groups.Length > 0) source.outputAudioMixerGroup = groups[0];
                }
            }

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"AudioSource configured on '{go.name}'.", new
            {
                gameObject = go.name,
                clip = source.clip?.name,
                volume = source.volume,
                pitch = source.pitch,
                spatialBlend = source.spatialBlend,
                loop = source.loop,
                playOnAwake = source.playOnAwake,
                minDistance = source.minDistance,
                maxDistance = source.maxDistance,
                rolloffMode = source.rolloffMode.ToString(),
            });
        }

        // ─────────────────────────────────────────────
        // get_info
        // ─────────────────────────────────────────────

        private static object GetInfo(JObject @params)
        {
            var p = new ToolParams(@params);
            var go = FindTarget(p);
            if (go == null) return new ErrorResponse("Target GameObject not found.");

            var source = go.GetComponent<AudioSource>();
            if (source == null) return new ErrorResponse($"No AudioSource on '{go.name}'.");

            return new SuccessResponse($"AudioSource info for '{go.name}'.", new
            {
                gameObject = go.name,
                clip = source.clip?.name,
                clipPath = source.clip != null ? AssetDatabase.GetAssetPath(source.clip) : null,
                volume = source.volume,
                pitch = source.pitch,
                spatialBlend = source.spatialBlend,
                loop = source.loop,
                playOnAwake = source.playOnAwake,
                mute = source.mute,
                minDistance = source.minDistance,
                maxDistance = source.maxDistance,
                rolloffMode = source.rolloffMode.ToString(),
                priority = source.priority,
                isPlaying = source.isPlaying,
                outputGroup = source.outputAudioMixerGroup?.name,
            });
        }

        // ─────────────────────────────────────────────
        // set_import_settings
        // ─────────────────────────────────────────────

        private static object SetImportSettings(JObject @params)
        {
            var p = new ToolParams(@params);
            string path = p.Get("path");
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("'path' parameter is required (AudioClip asset path).");

            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
                return new ErrorResponse($"AudioImporter not found for '{path}'. Is it an audio file?");

            var settings = importer.defaultSampleSettings;
            bool changed = false;

            string loadType = p.Get("load_type") ?? p.Get("loadType");
            if (!string.IsNullOrEmpty(loadType) && Enum.TryParse<AudioClipLoadType>(loadType, true, out var lt))
            {
                settings.loadType = lt;
                changed = true;
            }

            string compressionFormat = p.Get("compression_format") ?? p.Get("compressionFormat");
            if (!string.IsNullOrEmpty(compressionFormat) && Enum.TryParse<AudioCompressionFormat>(compressionFormat, true, out var cf))
            {
                settings.compressionFormat = cf;
                changed = true;
            }

            float? quality = p.GetFloat("quality");
            if (quality.HasValue) { settings.quality = quality.Value; changed = true; }

            string sampleRate = p.Get("sample_rate_setting") ?? p.Get("sampleRateSetting");
            if (!string.IsNullOrEmpty(sampleRate) && Enum.TryParse<AudioSampleRateSetting>(sampleRate, true, out var sr))
            {
                settings.sampleRateSetting = sr;
                changed = true;
            }

            if (@params["force_mono"] != null || @params["forceMono"] != null)
            {
                importer.forceToMono = (bool?)@params["force_mono"] ?? (bool?)@params["forceMono"] ?? false;
                changed = true;
            }

            if (@params["load_in_background"] != null || @params["loadInBackground"] != null)
            {
                importer.loadInBackground = (bool?)@params["load_in_background"]
                    ?? (bool?)@params["loadInBackground"] ?? false;
                changed = true;
            }

            if (@params["preload_audio_data"] != null || @params["preloadAudioData"] != null)
            {
                var preload = (bool?)@params["preload_audio_data"]
                    ?? (bool?)@params["preloadAudioData"] ?? true;
                settings.preloadAudioData = preload;
                changed = true;
            }

            if (!changed) return new ErrorResponse("No valid import settings provided.");

            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();

            return new SuccessResponse($"Audio import settings updated for '{path}'.", new
            {
                path,
                loadType = settings.loadType.ToString(),
                compressionFormat = settings.compressionFormat.ToString(),
                quality = settings.quality,
                sampleRateSetting = settings.sampleRateSetting.ToString(),
                forceMono = importer.forceToMono,
            });
        }

        // ─────────────────────────────────────────────
        // play / stop / pause (Play Mode only)
        // ─────────────────────────────────────────────

        private static object PlaybackControl(JObject @params, string command)
        {
            if (!EditorApplication.isPlaying)
                return new ErrorResponse(
                    $"Audio {command} requires Play Mode. " +
                    "Use manage_editor(action='play') to enter Play Mode first.");

            var p = new ToolParams(@params);
            var go = FindTarget(p);
            if (go == null) return new ErrorResponse("Target GameObject not found.");

            var source = go.GetComponent<AudioSource>();
            if (source == null) return new ErrorResponse($"No AudioSource on '{go.name}'.");

            switch (command)
            {
                case "play": source.Play(); break;
                case "stop": source.Stop(); break;
                case "pause": source.Pause(); break;
            }

            return new SuccessResponse($"AudioSource.{command}() called on '{go.name}'.", new
            {
                gameObject = go.name,
                command,
                isPlaying = source.isPlaying,
            });
        }

        // ─────────────────────────────────────────────
        // create_mixer (experimental — uses internal API)
        // ─────────────────────────────────────────────

        private static object CreateMixer(JObject @params)
        {
            if (AudioMixerControllerType == null)
                return new ErrorResponse(
                    "AudioMixer creation requires internal Unity API (AudioMixerController) " +
                    "which is not available in this Unity version. Please create the mixer manually.");

            var p = new ToolParams(@params);
            string path = p.Get("path");
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("'path' parameter is required (e.g., 'Assets/Audio/MainMixer.mixer').");

            try
            {
                // Create via ScriptableObject.CreateInstance using reflection
                var mixer = ScriptableObject.CreateInstance(AudioMixerControllerType);
                if (mixer == null)
                    return new ErrorResponse("Failed to create AudioMixerController instance.");

                string dir = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
                if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                {
                    string[] parts = dir.Split('/');
                    string current = parts[0];
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string next = current + "/" + parts[i];
                        if (!AssetDatabase.IsValidFolder(next))
                            AssetDatabase.CreateFolder(current, parts[i]);
                        current = next;
                    }
                }

                AssetDatabase.CreateAsset(mixer, path);
                AssetDatabase.SaveAssets();

                return new SuccessResponse($"AudioMixer created at '{path}'.", new
                {
                    path,
                    experimental = true,
                    warning = "Created via internal API. Some features may require manual setup in the Unity Editor.",
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Failed to create AudioMixer (experimental): {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private static GameObject FindTarget(ToolParams p)
        {
            string target = p.Get("target");
            if (string.IsNullOrEmpty(target)) return null;
            if (int.TryParse(target, out int id))
            {
                var obj = EditorUtility.InstanceIDToObject(id) as GameObject;
                if (obj != null) return obj;
            }
            return GameObject.Find(target);
        }
    }
}
