using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MCPForUnity.Editor.Tools.Build
{
    [McpForUnityTool("manage_build", AutoRegister = false, Group = "build")]
    public static class ManageBuild
    {
        private const string PrefKey_PendingSwitchPlatform = "MCP_PendingSwitchPlatform";
        private const string PrefKey_SwitchPlatformResult = "MCP_SwitchPlatformResult";

        [InitializeOnLoadMethod]
        private static void OnDomainReload()
        {
            // After domain reload from platform switch, mark completion
            if (EditorPrefs.HasKey(PrefKey_PendingSwitchPlatform))
            {
                string targetPlatform = EditorPrefs.GetString(PrefKey_PendingSwitchPlatform);
                EditorPrefs.DeleteKey(PrefKey_PendingSwitchPlatform);
                string currentPlatform = EditorUserBuildSettings.activeBuildTarget.ToString();
                EditorPrefs.SetString(PrefKey_SwitchPlatformResult,
                    $"Platform switched to {currentPlatform} (requested: {targetPlatform})");
            }
        }

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
                    "ping" => new SuccessResponse("Build tool ready.", new
                    {
                        currentPlatform = EditorUserBuildSettings.activeBuildTarget.ToString(),
                        currentGroup = EditorUserBuildSettings.selectedBuildTargetGroup.ToString(),
                    }),
                    "get_player_settings" => GetPlayerSettings(),
                    "set_player_settings" => SetPlayerSettings(@params),
                    "get_build_settings" => GetBuildSettings(),
                    "set_build_scenes" => SetBuildScenes(@params),
                    "switch_platform" => SwitchPlatform(@params),
                    "build" => BuildPlayer(@params),
                    "get_scripting_defines" => GetScriptingDefines(@params),
                    "set_scripting_defines" => SetScriptingDefines(@params),
                    _ => new ErrorResponse($"Unknown action: '{action}'."),
                };
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Build action '{action}' failed: {ex.Message}",
                    new { stackTrace = ex.StackTrace });
            }
        }

        // ─────────────────────────────────────────────
        // get_player_settings
        // ─────────────────────────────────────────────

        private static object GetPlayerSettings()
        {
            return new SuccessResponse("PlayerSettings retrieved.", new
            {
                companyName = PlayerSettings.companyName,
                productName = PlayerSettings.productName,
                applicationIdentifier = PlayerSettings.applicationIdentifier,
                bundleVersion = PlayerSettings.bundleVersion,
#if UNITY_ANDROID
                androidBundleVersionCode = PlayerSettings.Android.bundleVersionCode,
#endif
#if UNITY_IOS
                iosBuildNumber = PlayerSettings.iOS.buildNumber,
#endif
                defaultScreenWidth = PlayerSettings.defaultScreenWidth,
                defaultScreenHeight = PlayerSettings.defaultScreenHeight,
                fullscreen = PlayerSettings.fullScreenMode.ToString(),
                runInBackground = PlayerSettings.runInBackground,
                apiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(
                    EditorUserBuildSettings.selectedBuildTargetGroup).ToString(),
                scriptingBackend = PlayerSettings.GetScriptingBackend(
                    EditorUserBuildSettings.selectedBuildTargetGroup).ToString(),
                il2CppCompilerConfiguration = PlayerSettings.GetIl2CppCompilerConfiguration(
                    EditorUserBuildSettings.selectedBuildTargetGroup).ToString(),
            });
        }

        // ─────────────────────────────────────────────
        // set_player_settings
        // ─────────────────────────────────────────────

        private static object SetPlayerSettings(JObject @params)
        {
            var p = new ToolParams(@params);
            int changedCount = 0;

            string companyName = p.Get("company_name", null) ?? p.Get("companyName", null);
            if (companyName != null) { PlayerSettings.companyName = companyName; changedCount++; }

            string productName = p.Get("product_name", null) ?? p.Get("productName", null);
            if (productName != null) { PlayerSettings.productName = productName; changedCount++; }

            string appId = p.Get("application_identifier", null) ?? p.Get("applicationIdentifier", null)
                ?? p.Get("bundle_identifier", null) ?? p.Get("bundleIdentifier", null);
            if (appId != null) { PlayerSettings.applicationIdentifier = appId; changedCount++; }

            string version = p.Get("version", null) ?? p.Get("bundleVersion", null);
            if (version != null) { PlayerSettings.bundleVersion = version; changedCount++; }

            int? screenWidth = p.GetInt("default_screen_width") ?? p.GetInt("defaultScreenWidth");
            if (screenWidth.HasValue) { PlayerSettings.defaultScreenWidth = screenWidth.Value; changedCount++; }

            int? screenHeight = p.GetInt("default_screen_height") ?? p.GetInt("defaultScreenHeight");
            if (screenHeight.HasValue) { PlayerSettings.defaultScreenHeight = screenHeight.Value; changedCount++; }

            bool? runInBackground = p.GetBool("run_in_background", false);
            if (@params["run_in_background"] != null || @params["runInBackground"] != null)
            {
                PlayerSettings.runInBackground = p.GetBool("run_in_background")
                    || p.GetBool("runInBackground");
                changedCount++;
            }

            if (changedCount == 0)
                return new ErrorResponse("No valid settings provided to update.");

            return new SuccessResponse($"Updated {changedCount} PlayerSettings.", new { changedCount });
        }

        // ─────────────────────────────────────────────
        // get_build_settings
        // ─────────────────────────────────────────────

        private static object GetBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Select((s, i) => new
            {
                index = i,
                path = s.path,
                enabled = s.enabled,
                guid = s.guid.ToString(),
            }).ToArray();

            return new SuccessResponse("Build settings retrieved.", new
            {
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup.ToString(),
                scenes,
                sceneCount = scenes.Length,
                enabledSceneCount = scenes.Count(s => s.enabled),
            });
        }

        // ─────────────────────────────────────────────
        // set_build_scenes
        // ─────────────────────────────────────────────

        private static object SetBuildScenes(JObject @params)
        {
            var p = new ToolParams(@params);
            var scenePaths = p.GetStringArray("scenes");

            if (scenePaths == null || scenePaths.Length == 0)
                return new ErrorResponse("'scenes' parameter is required (array of scene paths).");

            var newScenes = new List<EditorBuildSettingsScene>();
            var errors = new List<string>();

            foreach (string path in scenePaths)
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    errors.Add($"Scene not found: '{path}'");
                    continue;
                }
                newScenes.Add(new EditorBuildSettingsScene(path, true));
            }

            if (errors.Count > 0 && newScenes.Count == 0)
                return new ErrorResponse("No valid scenes found.", new { errors });

            EditorBuildSettings.scenes = newScenes.ToArray();

            return new SuccessResponse($"Build scenes updated ({newScenes.Count} scenes).", new
            {
                sceneCount = newScenes.Count,
                scenes = newScenes.Select(s => s.path).ToArray(),
                errors = errors.Count > 0 ? errors : null,
            });
        }

        // ─────────────────────────────────────────────
        // switch_platform
        // ─────────────────────────────────────────────

        private static object SwitchPlatform(JObject @params)
        {
            var p = new ToolParams(@params);
            string platform = p.Get("platform");

            if (string.IsNullOrEmpty(platform))
                return new ErrorResponse("'platform' parameter is required.");

            // Check if there's a completed switch from a previous domain reload
            if (EditorPrefs.HasKey(PrefKey_SwitchPlatformResult))
            {
                string result = EditorPrefs.GetString(PrefKey_SwitchPlatformResult);
                EditorPrefs.DeleteKey(PrefKey_SwitchPlatformResult);
                return new SuccessResponse(result);
            }

            if (!TryParseBuildTarget(platform, out BuildTarget target, out BuildTargetGroup group))
                return new ErrorResponse($"Unknown platform: '{platform}'. " +
                    "Supported: Windows, Mac, Linux, Android, iOS, WebGL, PS4, PS5, Switch, XboxOne");

            // Already on this platform?
            if (EditorUserBuildSettings.activeBuildTarget == target)
                return new SuccessResponse($"Already on platform '{target}'.", new
                {
                    platform = target.ToString(),
                    alreadyCurrent = true,
                });

            // Save intent to EditorPrefs before triggering domain reload
            EditorPrefs.SetString(PrefKey_PendingSwitchPlatform, target.ToString());

            // This triggers domain reload — connection will be lost
            bool success = EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);

            if (!success)
            {
                EditorPrefs.DeleteKey(PrefKey_PendingSwitchPlatform);
                return new ErrorResponse($"Failed to switch to platform '{target}'. " +
                    "Ensure the build support module is installed.");
            }

            // If we get here without domain reload (rare), return success directly
            EditorPrefs.DeleteKey(PrefKey_PendingSwitchPlatform);
            return new SuccessResponse($"Platform switched to '{target}'.", new
            {
                platform = target.ToString(),
                group = group.ToString(),
            });
        }

        // ─────────────────────────────────────────────
        // build
        // ─────────────────────────────────────────────

        private static object BuildPlayer(JObject @params)
        {
            var p = new ToolParams(@params);
            string outputPath = p.Get("output_path") ?? p.Get("outputPath");
            if (string.IsNullOrEmpty(outputPath))
                return new ErrorResponse("'output_path' parameter is required.");

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
                return new ErrorResponse("No enabled scenes in Build Settings.");

            // Parse build options
            BuildOptions options = BuildOptions.None;
            var optionStrings = p.GetStringArray("options");
            if (optionStrings != null)
            {
                foreach (string opt in optionStrings)
                {
                    if (Enum.TryParse<BuildOptions>(opt, true, out var parsed))
                        options |= parsed;
                }
            }

            var target = EditorUserBuildSettings.activeBuildTarget;
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                targetGroup = group,
                options = options,
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);

            if (report.summary.result == BuildResult.Succeeded)
            {
                return new SuccessResponse($"Build succeeded: {outputPath}", new
                {
                    result = report.summary.result.ToString(),
                    outputPath = report.summary.outputPath,
                    totalSize = report.summary.totalSize,
                    totalTime = report.summary.totalTime.TotalSeconds,
                    totalErrors = report.summary.totalErrors,
                    totalWarnings = report.summary.totalWarnings,
                    platform = report.summary.platform.ToString(),
                });
            }

            return new ErrorResponse($"Build failed: {report.summary.result}", new
            {
                result = report.summary.result.ToString(),
                totalErrors = report.summary.totalErrors,
                totalWarnings = report.summary.totalWarnings,
            });
        }

        // ─────────────────────────────────────────────
        // get/set_scripting_defines
        // ─────────────────────────────────────────────

        private static object GetScriptingDefines(JObject @params)
        {
            var p = new ToolParams(@params);
            var group = GetTargetGroup(p);

            string[] defineList;
#if UNITY_2021_2_OR_NEWER
            var namedTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            PlayerSettings.GetScriptingDefineSymbols(namedTarget, out string[] definesArray);
            defineList = definesArray ?? Array.Empty<string>();
#else
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            defineList = string.IsNullOrEmpty(defines)
                ? Array.Empty<string>()
                : defines.Split(';').Where(d => !string.IsNullOrWhiteSpace(d)).ToArray();
#endif

            return new SuccessResponse("Scripting defines retrieved.", new
            {
                platform = group.ToString(),
                defines = defineList,
                raw = string.Join(";", defineList),
            });
        }

        private static object SetScriptingDefines(JObject @params)
        {
            var p = new ToolParams(@params);
            var group = GetTargetGroup(p);
            var defines = p.GetStringArray("defines");

            if (defines == null || defines.Length == 0)
                return new ErrorResponse("'defines' parameter is required (array of define symbols).");

            string definesStr = string.Join(";", defines);

#if UNITY_2021_2_OR_NEWER
            var namedTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            PlayerSettings.SetScriptingDefineSymbols(namedTarget, definesStr);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, definesStr);
#endif

            return new SuccessResponse($"Scripting defines set ({defines.Length} symbols). " +
                "This will trigger recompilation.", new
            {
                platform = group.ToString(),
                defines,
                triggersRecompilation = true,
            });
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private static BuildTargetGroup GetTargetGroup(ToolParams p)
        {
            string platform = p.Get("platform");
            if (!string.IsNullOrEmpty(platform) && TryParseBuildTarget(platform, out _, out var group))
                return group;
            return EditorUserBuildSettings.selectedBuildTargetGroup;
        }

        private static bool TryParseBuildTarget(string platform, out BuildTarget target, out BuildTargetGroup group)
        {
            target = BuildTarget.StandaloneWindows64;
            group = BuildTargetGroup.Standalone;

            switch (platform.ToLowerInvariant().Replace(" ", "").Replace("_", ""))
            {
                case "windows": case "win": case "win64": case "standalonewindows64":
                    target = BuildTarget.StandaloneWindows64; group = BuildTargetGroup.Standalone; return true;
                case "mac": case "macos": case "osx": case "standaloneosx":
                    target = BuildTarget.StandaloneOSX; group = BuildTargetGroup.Standalone; return true;
                case "linux": case "standalonelinux64":
                    target = BuildTarget.StandaloneLinux64; group = BuildTargetGroup.Standalone; return true;
                case "android":
                    target = BuildTarget.Android; group = BuildTargetGroup.Android; return true;
                case "ios": case "iphone":
                    target = BuildTarget.iOS; group = BuildTargetGroup.iOS; return true;
                case "webgl":
                    target = BuildTarget.WebGL; group = BuildTargetGroup.WebGL; return true;
                default:
                    // Try enum parse as fallback
                    if (Enum.TryParse<BuildTarget>(platform, true, out target))
                    {
                        group = BuildPipeline.GetBuildTargetGroup(target);
                        return true;
                    }
                    return false;
            }
        }
    }
}
