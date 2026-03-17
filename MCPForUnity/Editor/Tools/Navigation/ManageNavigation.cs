using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace MCPForUnity.Editor.Tools.Navigation
{
    [McpForUnityTool("manage_navigation", AutoRegister = false, Group = "core")]
    public static class ManageNavigation
    {
        // Runtime detection: new AI Navigation package (NavMeshSurface)
        private static readonly bool HasNewNavigation =
            Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation") != null;

        // Legacy NavMeshBuilder is in UnityEditor.AI
        private static readonly Type LegacyNavMeshBuilderType =
            Type.GetType("UnityEditor.AI.NavMeshBuilder, UnityEditor");

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
                    "ping" => new SuccessResponse("Navigation tool ready.", new
                    {
                        hasLegacyNavMesh = LegacyNavMeshBuilderType != null,
                        hasNewNavigation = HasNewNavigation,
                    }),
                    "bake" => BakeNavMesh(@params),
                    "clear" => ClearNavMesh(),
                    "get_settings" => GetSettings(),
                    "set_area" => SetArea(@params),
                    "configure_agent" => ConfigureAgent(@params),
                    "configure_obstacle" => ConfigureObstacle(@params),
                    "add_offmesh_link" => AddOffMeshLink(@params),
                    "test_path" => TestPath(@params),
                    _ => new ErrorResponse($"Unknown action: '{action}'."),
                };
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Navigation action '{action}' failed: {ex.Message}",
                    new { stackTrace = ex.StackTrace });
            }
        }

        // ─────────────────────────────────────────────
        // bake
        // ─────────────────────────────────────────────

        private static object BakeNavMesh(JObject @params)
        {
            // Try new NavMeshSurface first
            if (HasNewNavigation)
            {
                return BakeWithSurface(@params);
            }

            // Fall back to legacy NavMeshBuilder
            if (LegacyNavMeshBuilderType != null)
            {
                return BakeWithLegacy(@params);
            }

            return new ErrorResponse(
                "No navigation system available. Install AI Navigation package: " +
                "manage_packages(action='add_package', identifier='com.unity.ai.navigation')");
        }

        private static object BakeWithLegacy(JObject @params)
        {
            // Apply agent settings via SerializedObject before baking
            ApplyAgentSettings(@params);

            // UnityEditor.AI.NavMeshBuilder.BuildNavMesh() via reflection
            var buildMethod = LegacyNavMeshBuilderType.GetMethod("BuildNavMesh",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (buildMethod == null)
                return new ErrorResponse("NavMeshBuilder.BuildNavMesh method not found.");

            buildMethod.Invoke(null, null);

            return new SuccessResponse("NavMesh baked successfully (legacy).", new
            {
                method = "legacy",
                triangleCount = NavMesh.CalculateTriangulation().vertices.Length / 3,
            });
        }

        private static object BakeWithSurface(JObject @params)
        {
            // Find all NavMeshSurface components via reflection
            var surfaceType = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (surfaceType == null)
                return new ErrorResponse("NavMeshSurface type not found.");

#if UNITY_2022_2_OR_NEWER
            var surfaces = UnityEngine.Object.FindObjectsByType(surfaceType, FindObjectsSortMode.None);
#else
            var surfaces = UnityEngine.Object.FindObjectsOfType(surfaceType);
#endif

            if (surfaces.Length == 0)
            {
                // No surfaces found, try legacy
                if (LegacyNavMeshBuilderType != null)
                    return BakeWithLegacy(@params);
                return new ErrorResponse(
                    "No NavMeshSurface components found in scene. " +
                    "Add a NavMeshSurface component to a GameObject, or use legacy baking.");
            }

            // Call BuildNavMesh on each surface
            var buildMethod = surfaceType.GetMethod("BuildNavMesh",
                BindingFlags.Instance | BindingFlags.Public);
            int bakedCount = 0;

            foreach (var surface in surfaces)
            {
                try
                {
                    buildMethod?.Invoke(surface, null);
                    bakedCount++;
                }
                catch (Exception ex)
                {
                    McpLog.Error($"[ManageNavigation] Failed to bake surface: {ex.Message}");
                }
            }

            return new SuccessResponse($"NavMesh baked ({bakedCount} surfaces).", new
            {
                method = "NavMeshSurface",
                surfaceCount = bakedCount,
                triangleCount = NavMesh.CalculateTriangulation().vertices.Length / 3,
            });
        }

        private static void ApplyAgentSettings(JObject @params)
        {
            // Modify NavMesh bake settings via SerializedObject
            var navMeshSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
            if (navMeshSettings == null || navMeshSettings.Length == 0) return;

            var so = new SerializedObject(navMeshSettings[0]);
            bool changed = false;

            float? agentRadius = (float?)@params["agent_radius"] ?? (float?)@params["agentRadius"];
            if (agentRadius.HasValue)
            {
                var prop = so.FindProperty("m_BuildSettings.agentRadius");
                if (prop != null) { prop.floatValue = agentRadius.Value; changed = true; }
            }

            float? agentHeight = (float?)@params["agent_height"] ?? (float?)@params["agentHeight"];
            if (agentHeight.HasValue)
            {
                var prop = so.FindProperty("m_BuildSettings.agentHeight");
                if (prop != null) { prop.floatValue = agentHeight.Value; changed = true; }
            }

            float? maxSlope = (float?)@params["max_slope"] ?? (float?)@params["maxSlope"];
            if (maxSlope.HasValue)
            {
                var prop = so.FindProperty("m_BuildSettings.agentSlope");
                if (prop != null) { prop.floatValue = maxSlope.Value; changed = true; }
            }

            float? stepHeight = (float?)@params["step_height"] ?? (float?)@params["stepHeight"];
            if (stepHeight.HasValue)
            {
                var prop = so.FindProperty("m_BuildSettings.agentClimb");
                if (prop != null) { prop.floatValue = stepHeight.Value; changed = true; }
            }

            if (changed) so.ApplyModifiedProperties();
        }

        // ─────────────────────────────────────────────
        // clear
        // ─────────────────────────────────────────────

        private static object ClearNavMesh()
        {
            if (LegacyNavMeshBuilderType != null)
            {
                var clearMethod = LegacyNavMeshBuilderType.GetMethod("ClearAllNavMeshes",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                clearMethod?.Invoke(null, null);
            }

            return new SuccessResponse("NavMesh cleared.");
        }

        // ─────────────────────────────────────────────
        // get_settings
        // ─────────────────────────────────────────────

        private static object GetSettings()
        {
            // Read area names via SerializedObject on NavMeshAreas.asset
            var areas = new List<object>();
            try
            {
                var navSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
                if (navSettings != null && navSettings.Length > 0)
                {
                    var so = new SerializedObject(navSettings[0]);
                    var areasArray = so.FindProperty("areas");
                    if (areasArray != null)
                    {
                        for (int i = 0; i < areasArray.arraySize && i < 32; i++)
                        {
                            var element = areasArray.GetArrayElementAtIndex(i);
                            var nameProp = element.FindPropertyRelative("name");
                            var costProp = element.FindPropertyRelative("cost");
                            if (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue))
                            {
                                areas.Add(new
                                {
                                    index = i,
                                    name = nameProp.stringValue,
                                    cost = costProp?.floatValue ?? 1f,
                                });
                            }
                        }
                    }
                }
            }
            catch { }

            // Read bake settings
            object bakeSettings = null;
            try
            {
                var navSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
                if (navSettings != null && navSettings.Length > 0)
                {
                    var so = new SerializedObject(navSettings[0]);
                    bakeSettings = new
                    {
                        agentRadius = so.FindProperty("m_BuildSettings.agentRadius")?.floatValue,
                        agentHeight = so.FindProperty("m_BuildSettings.agentHeight")?.floatValue,
                        agentSlope = so.FindProperty("m_BuildSettings.agentSlope")?.floatValue,
                        agentClimb = so.FindProperty("m_BuildSettings.agentClimb")?.floatValue,
                    };
                }
            }
            catch { }

            var triangulation = NavMesh.CalculateTriangulation();

            return new SuccessResponse("Navigation settings retrieved.", new
            {
                hasNavMesh = triangulation.vertices.Length > 0,
                vertexCount = triangulation.vertices.Length,
                triangleCount = triangulation.indices.Length / 3,
                areas,
                bakeSettings,
                hasNewNavigation = HasNewNavigation,
                hasLegacyNavMesh = LegacyNavMeshBuilderType != null,
            });
        }

        // ─────────────────────────────────────────────
        // set_area
        // ─────────────────────────────────────────────

        private static object SetArea(JObject @params)
        {
            var p = new ToolParams(@params);
            int? index = p.GetInt("index");
            string areaName = p.Get("name");
            float? cost = p.GetFloat("cost");

            if (!index.HasValue || index.Value < 0 || index.Value > 31)
                return new ErrorResponse("'index' (0-31) is required.");

            var navSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
            if (navSettings == null || navSettings.Length == 0)
                return new ErrorResponse("Could not load NavMeshAreas.asset");

            var so = new SerializedObject(navSettings[0]);
            var areasArray = so.FindProperty("areas");

            if (areasArray == null || index.Value >= areasArray.arraySize)
                return new ErrorResponse($"Area index {index.Value} out of range.");

            var element = areasArray.GetArrayElementAtIndex(index.Value);
            bool changed = false;

            if (!string.IsNullOrEmpty(areaName))
            {
                element.FindPropertyRelative("name").stringValue = areaName;
                changed = true;
            }
            if (cost.HasValue)
            {
                element.FindPropertyRelative("cost").floatValue = cost.Value;
                changed = true;
            }

            if (!changed) return new ErrorResponse("Provide 'name' and/or 'cost'.");

            so.ApplyModifiedProperties();

            return new SuccessResponse($"Area {index.Value} updated.", new
            {
                index = index.Value,
                name = areaName,
                cost,
            });
        }

        // ─────────────────────────────────────────────
        // configure_agent
        // ─────────────────────────────────────────────

        private static object ConfigureAgent(JObject @params)
        {
            var p = new ToolParams(@params);
            var go = FindTarget(p);
            if (go == null) return new ErrorResponse("Target GameObject not found.");

            var agent = go.GetComponent<NavMeshAgent>();
            if (agent == null) agent = Undo.AddComponent<NavMeshAgent>(go);

            Undo.RecordObject(agent, "Configure NavMeshAgent");

            float? speed = p.GetFloat("speed");
            if (speed.HasValue) agent.speed = speed.Value;

            float? angularSpeed = p.GetFloat("angular_speed") ?? p.GetFloat("angularSpeed");
            if (angularSpeed.HasValue) agent.angularSpeed = angularSpeed.Value;

            float? acceleration = p.GetFloat("acceleration");
            if (acceleration.HasValue) agent.acceleration = acceleration.Value;

            float? stoppingDistance = p.GetFloat("stopping_distance") ?? p.GetFloat("stoppingDistance");
            if (stoppingDistance.HasValue) agent.stoppingDistance = stoppingDistance.Value;

            if (@params["auto_braking"] != null || @params["autoBraking"] != null)
                agent.autoBraking = (bool?)@params["auto_braking"] ?? (bool?)@params["autoBraking"] ?? true;

            float? baseOffset = p.GetFloat("base_offset") ?? p.GetFloat("baseOffset");
            if (baseOffset.HasValue) agent.baseOffset = baseOffset.Value;

            int? areaMask = p.GetInt("area_mask") ?? p.GetInt("areaMask");
            if (areaMask.HasValue) agent.areaMask = areaMask.Value;

            // Parse area_mask from comma-separated names
            string areaMaskStr = p.Get("area_mask_names") ?? p.Get("areaMaskNames");
            if (!string.IsNullOrEmpty(areaMaskStr))
            {
                int mask = 0;
                foreach (var name in areaMaskStr.Split(','))
                {
                    int area = NavMesh.GetAreaFromName(name.Trim());
                    if (area >= 0) mask |= 1 << area;
                }
                if (mask != 0) agent.areaMask = mask;
            }

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"NavMeshAgent configured on '{go.name}'.", new
            {
                gameObject = go.name,
                speed = agent.speed,
                angularSpeed = agent.angularSpeed,
                acceleration = agent.acceleration,
                stoppingDistance = agent.stoppingDistance,
                autoBraking = agent.autoBraking,
                areaMask = agent.areaMask,
            });
        }

        // ─────────────────────────────────────────────
        // configure_obstacle
        // ─────────────────────────────────────────────

        private static object ConfigureObstacle(JObject @params)
        {
            var p = new ToolParams(@params);
            var go = FindTarget(p);
            if (go == null) return new ErrorResponse("Target GameObject not found.");

            var obstacle = go.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = Undo.AddComponent<NavMeshObstacle>(go);

            Undo.RecordObject(obstacle, "Configure NavMeshObstacle");

            string shape = p.Get("shape")?.ToLowerInvariant();
            if (shape == "box") obstacle.shape = NavMeshObstacleShape.Box;
            else if (shape == "capsule") obstacle.shape = NavMeshObstacleShape.Capsule;

            Vector3? size = ParseVector3(@params["size"]);
            if (size.HasValue) obstacle.size = size.Value;

            Vector3? center = ParseVector3(@params["center"]);
            if (center.HasValue) obstacle.center = center.Value;

            if (@params["carve"] != null) obstacle.carving = (bool)@params["carve"];
            if (@params["carve_only_stationary"] != null || @params["carveOnlyStationary"] != null)
                obstacle.carvingMoveThreshold =
                    ((bool?)@params["carve_only_stationary"] ?? (bool?)@params["carveOnlyStationary"] ?? true) ? 0.1f : 999f;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"NavMeshObstacle configured on '{go.name}'.", new
            {
                gameObject = go.name,
                shape = obstacle.shape.ToString(),
                carving = obstacle.carving,
            });
        }

        // ─────────────────────────────────────────────
        // add_offmesh_link
        // ─────────────────────────────────────────────

        private static object AddOffMeshLink(JObject @params)
        {
            var p = new ToolParams(@params);
            var go = FindTarget(p);
            if (go == null) return new ErrorResponse("Target GameObject not found.");

            var link = Undo.AddComponent<OffMeshLink>(go);

            string endTarget = p.Get("end_target") ?? p.Get("endTarget");
            if (!string.IsNullOrEmpty(endTarget))
            {
                var endGo = GameObject.Find(endTarget);
                if (endGo != null)
                {
                    link.startTransform = go.transform;
                    link.endTransform = endGo.transform;
                }
            }

            if (@params["bidirectional"] != null)
                link.biDirectional = (bool)@params["bidirectional"];

            int? area = p.GetInt("area");
            if (area.HasValue) link.area = area.Value;

            if (@params["auto_update_positions"] != null || @params["autoUpdatePositions"] != null)
                link.autoUpdatePositions =
                    (bool?)@params["auto_update_positions"] ?? (bool?)@params["autoUpdatePositions"] ?? true;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"OffMeshLink added to '{go.name}'.", new
            {
                gameObject = go.name,
                endTarget,
                biDirectional = link.biDirectional,
            });
        }

        // ─────────────────────────────────────────────
        // test_path
        // ─────────────────────────────────────────────

        private static object TestPath(JObject @params)
        {
            Vector3 start = ParseVector3(@params["start"]) ?? Vector3.zero;
            Vector3 end = ParseVector3(@params["end"]) ?? Vector3.zero;
            int areaMask = NavMesh.AllAreas;

            string areaMaskStr = ParamCoercion.CoerceString(
                @params["area_mask"] ?? @params["areaMask"], null);
            if (!string.IsNullOrEmpty(areaMaskStr) && int.TryParse(areaMaskStr, out int mask))
                areaMask = mask;

            var path = new NavMeshPath();
            bool valid = NavMesh.CalculatePath(start, end, areaMask, path);

            // Calculate total distance
            float totalDistance = 0f;
            for (int i = 1; i < path.corners.Length; i++)
                totalDistance += Vector3.Distance(path.corners[i - 1], path.corners[i]);

            var corners = path.corners.Select(c => new[] { c.x, c.y, c.z }).ToArray();

            return new SuccessResponse(valid ? "Path found." : "No valid path.", new
            {
                valid,
                status = path.status.ToString(),
                corners,
                cornerCount = path.corners.Length,
                distance = totalDistance,
                start = new[] { start.x, start.y, start.z },
                end = new[] { end.x, end.y, end.z },
            });
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

        private static Vector3? ParseVector3(JToken token)
        {
            if (token == null) return null;
            if (token is JArray arr && arr.Count >= 3)
                return new Vector3((float)arr[0], (float)arr[1], (float)arr[2]);
            if (token is JObject obj)
                return new Vector3(
                    (float?)obj["x"] ?? 0,
                    (float?)obj["y"] ?? 0,
                    (float?)obj["z"] ?? 0);
            return null;
        }
    }
}
