using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools.Physics
{
    [McpForUnityTool("manage_physics", AutoRegister = false, Group = "core")]
    public static class ManagePhysics
    {
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
                    "ping" => new SuccessResponse("Physics tool ready.", new
                    {
                        gravity = new[] {
                            UnityEngine.Physics.gravity.x,
                            UnityEngine.Physics.gravity.y,
                            UnityEngine.Physics.gravity.z },
                    }),
                    "raycast" => Raycast(@params),
                    "raycast_all" => RaycastAll(@params),
                    "overlap" => Overlap(@params),
                    "create_physics_material" => CreatePhysicsMaterial(@params),
                    "get_settings" => GetSettings(),
                    "set_settings" => SetSettings(@params),
                    "configure_rigidbody" => ConfigureRigidbody(@params),
                    "configure_collider" => ConfigureCollider(@params),
                    "configure_joint" => ConfigureJoint(@params),
                    _ => new ErrorResponse($"Unknown action: '{action}'."),
                };
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Physics action '{action}' failed: {ex.Message}",
                    new { stackTrace = ex.StackTrace });
            }
        }

        // ─────────────────────────────────────────────
        // raycast
        // ─────────────────────────────────────────────

        private static object Raycast(JObject @params)
        {
            ParseRayParams(@params, out Vector3 origin, out Vector3 direction, out float maxDistance, out int layerMask);

            if (UnityEngine.Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, layerMask))
            {
                return new SuccessResponse("Raycast hit.", new
                {
                    hit = true,
                    point = Vec3(hit.point),
                    normal = Vec3(hit.normal),
                    distance = hit.distance,
                    colliderName = hit.collider.name,
                    colliderTag = hit.collider.tag,
                    gameObjectName = hit.collider.gameObject.name,
                    gameObjectInstanceId = hit.collider.gameObject.GetInstanceID(),
                });
            }

            return new SuccessResponse("Raycast missed.", new { hit = false });
        }

        private static object RaycastAll(JObject @params)
        {
            ParseRayParams(@params, out Vector3 origin, out Vector3 direction, out float maxDistance, out int layerMask);
            int maxResults = (int?)@params["max_results"] ?? (int?)@params["maxResults"] ?? 20;

            RaycastHit[] hits = UnityEngine.Physics.RaycastAll(origin, direction, maxDistance, layerMask);
            var results = hits.Take(maxResults).Select(h => new
            {
                point = Vec3(h.point),
                normal = Vec3(h.normal),
                distance = h.distance,
                colliderName = h.collider.name,
                gameObjectName = h.collider.gameObject.name,
                gameObjectInstanceId = h.collider.gameObject.GetInstanceID(),
            }).ToArray();

            return new SuccessResponse($"Raycast found {results.Length} hits.", new
            {
                hitCount = results.Length,
                hits = results,
            });
        }

        // ─────────────────────────────────────────────
        // overlap
        // ─────────────────────────────────────────────

        private static object Overlap(JObject @params)
        {
            var p = new ToolParams(@params);
            string shape = (p.Get("shape") ?? "sphere").ToLowerInvariant();
            Vector3 center = ParseVector3(@params["center"]) ?? Vector3.zero;
            int layerMask = ParseLayerMask(p.Get("layer_mask") ?? p.Get("layerMask"));
            int maxResults = p.GetInt("max_results") ?? p.GetInt("maxResults") ?? 20;

            Collider[] colliders;

            switch (shape)
            {
                case "sphere":
                    float radius = (float?)@params["radius"] ?? 5f;
                    colliders = UnityEngine.Physics.OverlapSphere(center, radius, layerMask);
                    break;
                case "box":
                    Vector3 halfExtents = ParseVector3(@params["half_extents"] ?? @params["halfExtents"])
                        ?? new Vector3(5, 5, 5);
                    colliders = UnityEngine.Physics.OverlapBox(center, halfExtents, Quaternion.identity, layerMask);
                    break;
                default:
                    return new ErrorResponse($"Unknown shape: '{shape}'. Supported: sphere, box");
            }

            var results = colliders.Take(maxResults).Select(c => new
            {
                name = c.gameObject.name,
                tag = c.tag,
                instanceId = c.gameObject.GetInstanceID(),
                colliderType = c.GetType().Name,
                distance = Vector3.Distance(center, c.transform.position),
            }).OrderBy(r => r.distance).ToArray();

            return new SuccessResponse($"Overlap found {results.Length} colliders.", new
            {
                shape,
                center = Vec3(center),
                resultCount = results.Length,
                totalFound = colliders.Length,
                results,
            });
        }

        // ─────────────────────────────────────────────
        // create_physics_material
        // ─────────────────────────────────────────────

        private static object CreatePhysicsMaterial(JObject @params)
        {
            var p = new ToolParams(@params);
            string path = p.Get("path");
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("'path' parameter is required (e.g., 'Assets/Physics/Ice.physicMaterial').");

            var material = new PhysicMaterial
            {
                dynamicFriction = p.GetFloat("dynamic_friction") ?? p.GetFloat("dynamicFriction") ?? 0.6f,
                staticFriction = p.GetFloat("static_friction") ?? p.GetFloat("staticFriction") ?? 0.6f,
                bounciness = p.GetFloat("bounciness") ?? 0f,
            };

            string frictionCombine = p.Get("friction_combine") ?? p.Get("frictionCombine");
            if (!string.IsNullOrEmpty(frictionCombine) && Enum.TryParse<PhysicMaterialCombine>(frictionCombine, true, out var fc))
                material.frictionCombine = fc;

            string bounceCombine = p.Get("bounce_combine") ?? p.Get("bounceCombine");
            if (!string.IsNullOrEmpty(bounceCombine) && Enum.TryParse<PhysicMaterialCombine>(bounceCombine, true, out var bc))
                material.bounceCombine = bc;

            string dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                // Create folder recursively
                string[] parts = dir.Replace("\\", "/").Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Physics material created at '{path}'.", new
            {
                path,
                dynamicFriction = material.dynamicFriction,
                staticFriction = material.staticFriction,
                bounciness = material.bounciness,
                frictionCombine = material.frictionCombine.ToString(),
                bounceCombine = material.bounceCombine.ToString(),
            });
        }

        // ─────────────────────────────────────────────
        // get_settings / set_settings (persistent via SerializedObject)
        // ─────────────────────────────────────────────

        private static object GetSettings()
        {
            var physicsManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");
            if (physicsManager == null || physicsManager.Length == 0)
                return new ErrorResponse("Could not load DynamicsManager.asset");

            var so = new SerializedObject(physicsManager[0]);
            var gravity = so.FindProperty("m_Gravity").vector3Value;

            return new SuccessResponse("Physics settings retrieved.", new
            {
                gravity = Vec3(gravity),
                defaultSolverIterations = UnityEngine.Physics.defaultSolverIterations,
                defaultSolverVelocityIterations = UnityEngine.Physics.defaultSolverVelocityIterations,
                bounceThreshold = UnityEngine.Physics.bounceThreshold,
                sleepThreshold = UnityEngine.Physics.sleepThreshold,
                defaultContactOffset = UnityEngine.Physics.defaultContactOffset,
                defaultMaxAngularSpeed = UnityEngine.Physics.defaultMaxAngularSpeed,
                autoSimulation = UnityEngine.Physics.autoSimulation,
            });
        }

        private static object SetSettings(JObject @params)
        {
            var physicsManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");
            if (physicsManager == null || physicsManager.Length == 0)
                return new ErrorResponse("Could not load DynamicsManager.asset");

            var so = new SerializedObject(physicsManager[0]);
            int changedCount = 0;

            var gravityToken = @params["gravity"];
            if (gravityToken != null)
            {
                Vector3? g = ParseVector3(gravityToken);
                if (g.HasValue)
                {
                    so.FindProperty("m_Gravity").vector3Value = g.Value;
                    changedCount++;
                }
            }

            var solverIter = (int?)@params["default_solver_iterations"] ?? (int?)@params["defaultSolverIterations"];
            if (solverIter.HasValue)
            {
                so.FindProperty("m_DefaultSolverIterations").intValue = solverIter.Value;
                changedCount++;
            }

            var bounceThreshold = (float?)@params["bounce_threshold"] ?? (float?)@params["bounceThreshold"];
            if (bounceThreshold.HasValue)
            {
                so.FindProperty("m_BounceThreshold").floatValue = bounceThreshold.Value;
                changedCount++;
            }

            var sleepThreshold = (float?)@params["sleep_threshold"] ?? (float?)@params["sleepThreshold"];
            if (sleepThreshold.HasValue)
            {
                so.FindProperty("m_SleepThreshold").floatValue = sleepThreshold.Value;
                changedCount++;
            }

            if (changedCount == 0)
                return new ErrorResponse("No valid settings provided.");

            so.ApplyModifiedProperties();

            return new SuccessResponse($"Updated {changedCount} physics settings (persistent).", new
            {
                changedCount,
                persistent = true,
            });
        }

        // ─────────────────────────────────────────────
        // configure_rigidbody
        // ─────────────────────────────────────────────

        private static object ConfigureRigidbody(JObject @params)
        {
            var p = new ToolParams(@params);
            var go = FindTarget(p);
            if (go == null) return new ErrorResponse("Target GameObject not found.");

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = Undo.AddComponent<Rigidbody>(go);

            Undo.RecordObject(rb, "Configure Rigidbody");

            float? mass = p.GetFloat("mass");
            if (mass.HasValue) rb.mass = mass.Value;

            float? drag = p.GetFloat("drag");
            if (drag.HasValue) rb.drag = drag.Value;

            float? angularDrag = p.GetFloat("angular_drag") ?? p.GetFloat("angularDrag");
            if (angularDrag.HasValue) rb.angularDrag = angularDrag.Value;

            if (@params["is_kinematic"] != null || @params["isKinematic"] != null)
                rb.isKinematic = (bool?)@params["is_kinematic"] ?? (bool?)@params["isKinematic"] ?? false;

            if (@params["use_gravity"] != null || @params["useGravity"] != null)
                rb.useGravity = (bool?)@params["use_gravity"] ?? (bool?)@params["useGravity"] ?? true;

            string interpolation = p.Get("interpolation");
            if (!string.IsNullOrEmpty(interpolation) && Enum.TryParse<RigidbodyInterpolation>(interpolation, true, out var interp))
                rb.interpolation = interp;

            string collisionDetection = p.Get("collision_detection") ?? p.Get("collisionDetection");
            if (!string.IsNullOrEmpty(collisionDetection) && Enum.TryParse<CollisionDetectionMode>(collisionDetection, true, out var cd))
                rb.collisionDetectionMode = cd;

            string constraints = p.Get("constraints");
            if (!string.IsNullOrEmpty(constraints) && Enum.TryParse<RigidbodyConstraints>(constraints, true, out var c))
                rb.constraints = c;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Rigidbody configured on '{go.name}'.", new
            {
                gameObject = go.name,
                mass = rb.mass,
                drag = rb.drag,
                angularDrag = rb.angularDrag,
                isKinematic = rb.isKinematic,
                useGravity = rb.useGravity,
                interpolation = rb.interpolation.ToString(),
                collisionDetection = rb.collisionDetectionMode.ToString(),
            });
        }

        // ─────────────────────────────────────────────
        // configure_collider
        // ─────────────────────────────────────────────

        private static object ConfigureCollider(JObject @params)
        {
            var p = new ToolParams(@params);
            var go = FindTarget(p);
            if (go == null) return new ErrorResponse("Target GameObject not found.");

            string colliderType = (p.Get("collider_type") ?? p.Get("colliderType") ?? "box").ToLowerInvariant();

            Collider collider;
            switch (colliderType)
            {
                case "box": case "boxcollider":
                    var box = go.GetComponent<BoxCollider>() ?? Undo.AddComponent<BoxCollider>(go);
                    Vector3? size = ParseVector3(@params["size"]);
                    if (size.HasValue) box.size = size.Value;
                    Vector3? boxCenter = ParseVector3(@params["center"]);
                    if (boxCenter.HasValue) box.center = boxCenter.Value;
                    collider = box;
                    break;

                case "sphere": case "spherecollider":
                    var sphere = go.GetComponent<SphereCollider>() ?? Undo.AddComponent<SphereCollider>(go);
                    float? radius = p.GetFloat("radius");
                    if (radius.HasValue) sphere.radius = radius.Value;
                    Vector3? sphereCenter = ParseVector3(@params["center"]);
                    if (sphereCenter.HasValue) sphere.center = sphereCenter.Value;
                    collider = sphere;
                    break;

                case "capsule": case "capsulecollider":
                    var capsule = go.GetComponent<CapsuleCollider>() ?? Undo.AddComponent<CapsuleCollider>(go);
                    float? capRadius = p.GetFloat("radius");
                    if (capRadius.HasValue) capsule.radius = capRadius.Value;
                    float? height = p.GetFloat("height");
                    if (height.HasValue) capsule.height = height.Value;
                    int? direction = p.GetInt("direction");
                    if (direction.HasValue) capsule.direction = direction.Value;
                    collider = capsule;
                    break;

                case "mesh": case "meshcollider":
                    var mesh = go.GetComponent<MeshCollider>() ?? Undo.AddComponent<MeshCollider>(go);
                    if (@params["convex"] != null) mesh.convex = (bool)@params["convex"];
                    collider = mesh;
                    break;

                default:
                    return new ErrorResponse($"Unknown collider type: '{colliderType}'. Supported: box, sphere, capsule, mesh");
            }

            if (@params["is_trigger"] != null || @params["isTrigger"] != null)
                collider.isTrigger = (bool?)@params["is_trigger"] ?? (bool?)@params["isTrigger"] ?? false;

            // Assign physics material
            string materialPath = p.Get("physics_material") ?? p.Get("physicsMaterial");
            if (!string.IsNullOrEmpty(materialPath))
            {
                var mat = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(materialPath);
                if (mat != null) collider.sharedMaterial = mat;
            }

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"{collider.GetType().Name} configured on '{go.name}'.", new
            {
                gameObject = go.name,
                colliderType = collider.GetType().Name,
                isTrigger = collider.isTrigger,
            });
        }

        // ─────────────────────────────────────────────
        // configure_joint
        // ─────────────────────────────────────────────

        private static object ConfigureJoint(JObject @params)
        {
            var p = new ToolParams(@params);
            var go = FindTarget(p);
            if (go == null) return new ErrorResponse("Target GameObject not found.");

            string jointType = (p.Get("joint_type") ?? p.Get("jointType") ?? "fixed").ToLowerInvariant();

            Joint joint;
            switch (jointType)
            {
                case "fixed": case "fixedjoint":
                    joint = go.GetComponent<FixedJoint>() ?? Undo.AddComponent<FixedJoint>(go);
                    break;
                case "hinge": case "hingejoint":
                    var hinge = go.GetComponent<HingeJoint>() ?? Undo.AddComponent<HingeJoint>(go);
                    Vector3? axis = ParseVector3(@params["axis"]);
                    if (axis.HasValue) hinge.axis = axis.Value;
                    if (@params["use_motor"] != null || @params["useMotor"] != null)
                    {
                        hinge.useMotor = (bool?)@params["use_motor"] ?? (bool?)@params["useMotor"] ?? false;
                        if (hinge.useMotor)
                        {
                            var motor = hinge.motor;
                            motor.targetVelocity = p.GetFloat("motor_velocity") ?? p.GetFloat("motorVelocity") ?? 100f;
                            motor.force = p.GetFloat("motor_force") ?? p.GetFloat("motorForce") ?? 10f;
                            hinge.motor = motor;
                        }
                    }
                    joint = hinge;
                    break;
                case "spring": case "springjoint":
                    var spring = go.GetComponent<SpringJoint>() ?? Undo.AddComponent<SpringJoint>(go);
                    float? springForce = p.GetFloat("spring");
                    if (springForce.HasValue) spring.spring = springForce.Value;
                    float? damper = p.GetFloat("damper");
                    if (damper.HasValue) spring.damper = damper.Value;
                    float? minDist = p.GetFloat("min_distance") ?? p.GetFloat("minDistance");
                    if (minDist.HasValue) spring.minDistance = minDist.Value;
                    float? maxDist = p.GetFloat("max_distance") ?? p.GetFloat("maxDistance");
                    if (maxDist.HasValue) spring.maxDistance = maxDist.Value;
                    joint = spring;
                    break;
                default:
                    return new ErrorResponse($"Unknown joint type: '{jointType}'. Supported: fixed, hinge, spring");
            }

            // Connected body
            string connectedTo = p.Get("connected_body") ?? p.Get("connectedBody");
            if (!string.IsNullOrEmpty(connectedTo))
            {
                var connectedGo = GameObject.Find(connectedTo);
                if (connectedGo != null)
                    joint.connectedBody = connectedGo.GetComponent<Rigidbody>();
            }

            float? breakForce = p.GetFloat("break_force") ?? p.GetFloat("breakForce");
            if (breakForce.HasValue) joint.breakForce = breakForce.Value;

            float? breakTorque = p.GetFloat("break_torque") ?? p.GetFloat("breakTorque");
            if (breakTorque.HasValue) joint.breakTorque = breakTorque.Value;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"{joint.GetType().Name} configured on '{go.name}'.", new
            {
                gameObject = go.name,
                jointType = joint.GetType().Name,
                connectedBody = joint.connectedBody?.name,
            });
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private static GameObject FindTarget(ToolParams p)
        {
            string target = p.Get("target");
            if (string.IsNullOrEmpty(target)) return null;

            // Try instance ID first
            if (int.TryParse(target, out int instanceId))
            {
                var obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                if (obj != null) return obj;
            }

            // Try by name/path
            var go = GameObject.Find(target);
            if (go != null) return go;

            // Search all scenes
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var found = FindChildByPath(root, target);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private static GameObject FindChildByPath(GameObject parent, string name)
        {
            if (parent.name == name) return parent;
            var t = parent.transform.Find(name);
            if (t != null) return t.gameObject;
            foreach (Transform child in parent.transform)
            {
                var found = FindChildByPath(child.gameObject, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void ParseRayParams(JObject @params, out Vector3 origin, out Vector3 direction,
            out float maxDistance, out int layerMask)
        {
            origin = ParseVector3(@params["origin"]) ?? Vector3.zero;
            direction = ParseVector3(@params["direction"]) ?? Vector3.forward;
            direction.Normalize();
            maxDistance = (float?)@params["max_distance"] ?? (float?)@params["maxDistance"] ?? 1000f;
            string layerMaskStr = ParamCoercion.CoerceString(@params["layer_mask"] ?? @params["layerMask"], null);
            layerMask = ParseLayerMask(layerMaskStr);
        }

        private static int ParseLayerMask(string layerMaskStr)
        {
            if (string.IsNullOrEmpty(layerMaskStr)) return ~0; // all layers
            if (int.TryParse(layerMaskStr, out int mask)) return mask;

            int result = 0;
            foreach (var name in layerMaskStr.Split(','))
            {
                int layer = LayerMask.NameToLayer(name.Trim());
                if (layer >= 0) result |= 1 << layer;
            }
            return result == 0 ? ~0 : result;
        }

        private static Vector3? ParseVector3(JToken token)
        {
            if (token == null) return null;
            if (token is JArray arr && arr.Count >= 3)
                return new Vector3((float)arr[0], (float)arr[1], (float)arr[2]);
            if (token is JObject obj)
            {
                float x = (float?)obj["x"] ?? 0;
                float y = (float?)obj["y"] ?? 0;
                float z = (float?)obj["z"] ?? 0;
                return new Vector3(x, y, z);
            }
            return null;
        }

        private static float[] Vec3(Vector3 v) => new[] { v.x, v.y, v.z };
    }
}
