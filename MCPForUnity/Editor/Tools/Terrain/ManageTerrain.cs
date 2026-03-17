using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools.Terrain
{
    [McpForUnityTool("manage_terrain", AutoRegister = false, Group = "terrain")]
    public static class ManageTerrain
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
                    "ping" => Ping(),
                    "create" => CreateTerrain(@params),
                    "set_heightmap" => SetHeightmap(@params),
                    "export_heightmap" => ExportHeightmap(@params),
                    "get_heightmap" => GetHeightmap(@params),
                    "raise_lower" => RaiseLower(@params),
                    "smooth" => Smooth(@params),
                    "add_terrain_layer" => AddTerrainLayer(@params),
                    "paint_texture" => PaintTexture(@params),
                    "add_tree_prototype" => AddTreePrototype(@params),
                    "paint_trees" => PaintTrees(@params),
                    "add_detail_prototype" => AddDetailPrototype(@params),
                    "paint_details" => PaintDetails(@params),
                    "set_properties" => SetProperties(@params),
                    "get_info" => GetInfo(@params),
                    _ => new ErrorResponse($"Unknown action: '{action}'."),
                };
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Terrain action '{action}' failed: {ex.Message}",
                    new { stackTrace = ex.StackTrace });
            }
        }

        private static object Ping()
        {
            var terrains = UnityEngine.Terrain.activeTerrains;
            return new SuccessResponse("Terrain tool ready.", new
            {
                activeTerrainCount = terrains.Length,
                terrainNames = terrains.Select(t => t.name).ToArray(),
            });
        }

        // ─────────────────────────────────────────────
        // create
        // ─────────────────────────────────────────────

        private static object CreateTerrain(JObject @params)
        {
            var p = new ToolParams(@params);
            string name = p.Get("name") ?? "Terrain";

            Vector3 size = ParseVector3(@params["size"]) ?? new Vector3(500, 200, 500);
            int resolution = p.GetInt("heightmap_resolution") ?? p.GetInt("heightmapResolution") ?? 513;

            // Validate resolution: must be 2^n + 1
            if (!IsValidResolution(resolution))
                return new ErrorResponse($"heightmap_resolution must be 2^n+1 (33, 65, 129, 257, 513, 1025, 2049, 4097). Got: {resolution}");

            string dataPath = p.Get("data_path") ?? p.Get("dataPath")
                ?? $"Assets/{name}_Data.asset";

            var terrainData = new TerrainData();
            terrainData.heightmapResolution = resolution;
            terrainData.size = size;

            int? detailResolution = p.GetInt("detail_resolution") ?? p.GetInt("detailResolution");
            if (detailResolution.HasValue)
                terrainData.SetDetailResolution(detailResolution.Value, 16);

            int? alphamapResolution = p.GetInt("alphamap_resolution") ?? p.GetInt("alphamapResolution");
            if (alphamapResolution.HasValue)
                terrainData.alphamapResolution = alphamapResolution.Value;

            // Save TerrainData asset
            string dir = Path.GetDirectoryName(dataPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                CreateFolderRecursive(dir);

            AssetDatabase.CreateAsset(terrainData, dataPath);

            var terrainGo = UnityEngine.Terrain.CreateTerrainGameObject(terrainData);
            terrainGo.name = name;

            Vector3? position = ParseVector3(@params["position"]);
            if (position.HasValue) terrainGo.transform.position = position.Value;

            Undo.RegisterCreatedObjectUndo(terrainGo, "Create Terrain");

            return new SuccessResponse($"Terrain '{name}' created.", new
            {
                name,
                size = Vec3(size),
                heightmapResolution = resolution,
                dataPath,
                instanceId = terrainGo.GetInstanceID(),
            });
        }

        // ─────────────────────────────────────────────
        // set_heightmap (from file only)
        // ─────────────────────────────────────────────

        private static object SetHeightmap(JObject @params)
        {
            var p = new ToolParams(@params);
            var terrain = FindTerrain(p);
            if (terrain == null) return new ErrorResponse("Target terrain not found.");

            string sourcePath = p.Get("source_path") ?? p.Get("sourcePath");
            if (string.IsNullOrEmpty(sourcePath))
                return new ErrorResponse("'source_path' is required (path to RAW16, PNG, or EXR file).");

            string fullPath = Path.GetFullPath(sourcePath);
            if (!File.Exists(fullPath))
            {
                // Try as Assets-relative path
                fullPath = Path.Combine(Application.dataPath, "..", sourcePath);
                if (!File.Exists(fullPath))
                    return new ErrorResponse($"File not found: '{sourcePath}'.");
            }

            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            string ext = Path.GetExtension(sourcePath).ToLowerInvariant();

            float[,] heights = new float[res, res];

            if (ext == ".raw")
            {
                byte[] data = File.ReadAllBytes(fullPath);
                // RAW16 format: 2 bytes per sample, little-endian
                int expectedSize = res * res * 2;
                if (data.Length < expectedSize)
                    return new ErrorResponse($"RAW file too small. Expected {expectedSize} bytes for {res}x{res}, got {data.Length}.");

                for (int y = 0; y < res; y++)
                    for (int x = 0; x < res; x++)
                    {
                        int idx = (y * res + x) * 2;
                        ushort val = BitConverter.ToUInt16(data, idx);
                        heights[y, x] = val / 65535f;
                    }
            }
            else if (ext == ".png" || ext == ".exr" || ext == ".tga" || ext == ".jpg")
            {
                byte[] data = File.ReadAllBytes(fullPath);
                var tex = new Texture2D(2, 2);
                if (!tex.LoadImage(data))
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                    return new ErrorResponse("Failed to load image file.");
                }

                for (int y = 0; y < res; y++)
                    for (int x = 0; x < res; x++)
                    {
                        float u = (float)x / (res - 1);
                        float v = (float)y / (res - 1);
                        heights[y, x] = tex.GetPixelBilinear(u, v).grayscale;
                    }

                UnityEngine.Object.DestroyImmediate(tex);
            }
            else
            {
                return new ErrorResponse($"Unsupported file format: '{ext}'. Use .raw, .png, .exr, .tga, or .jpg");
            }

            td.SetHeights(0, 0, heights);

            return new SuccessResponse($"Heightmap imported from '{sourcePath}'.", new
            {
                resolution = res,
                source = sourcePath,
                format = ext,
            });
        }

        // ─────────────────────────────────────────────
        // export_heightmap
        // ─────────────────────────────────────────────

        private static object ExportHeightmap(JObject @params)
        {
            var p = new ToolParams(@params);
            var terrain = FindTerrain(p);
            if (terrain == null) return new ErrorResponse("Target terrain not found.");

            string outputPath = p.Get("output_path") ?? p.Get("outputPath");
            if (string.IsNullOrEmpty(outputPath))
                return new ErrorResponse("'output_path' is required.");

            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            float[,] heights = td.GetHeights(0, 0, res, res);

            byte[] raw = new byte[res * res * 2];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    ushort val = (ushort)(Mathf.Clamp01(heights[y, x]) * 65535f);
                    int idx = (y * res + x) * 2;
                    raw[idx] = (byte)(val & 0xFF);
                    raw[idx + 1] = (byte)(val >> 8);
                }

            File.WriteAllBytes(outputPath, raw);

            return new SuccessResponse($"Heightmap exported to '{outputPath}'.", new
            {
                resolution = res,
                outputPath,
                format = "RAW16",
                sizeBytes = raw.Length,
            });
        }

        // ─────────────────────────────────────────────
        // get_heightmap (region only, max 64x64)
        // ─────────────────────────────────────────────

        private static object GetHeightmap(JObject @params)
        {
            var p = new ToolParams(@params);
            var terrain = FindTerrain(p);
            if (terrain == null) return new ErrorResponse("Target terrain not found.");

            var region = @params["region"] as JArray;
            if (region == null || region.Count < 4)
                return new ErrorResponse("'region' [x, y, width, height] is required (max 64x64).");

            int rx = (int)region[0], ry = (int)region[1];
            int rw = Math.Min((int)region[2], 64);
            int rh = Math.Min((int)region[3], 64);

            var td = terrain.terrainData;
            float[,] heights = td.GetHeights(rx, ry, rw, rh);

            // Flatten to array of arrays
            var result = new float[rh][];
            for (int y = 0; y < rh; y++)
            {
                result[y] = new float[rw];
                for (int x = 0; x < rw; x++)
                    result[y][x] = (float)Math.Round(heights[y, x], 4);
            }

            return new SuccessResponse($"Heightmap region ({rw}x{rh}) retrieved.", new
            {
                region = new { x = rx, y = ry, width = rw, height = rh },
                heights = result,
            });
        }

        // ─────────────────────────────────────────────
        // raise_lower
        // ─────────────────────────────────────────────

        private static object RaiseLower(JObject @params)
        {
            var p = new ToolParams(@params);
            var terrain = FindTerrain(p);
            if (terrain == null) return new ErrorResponse("Target terrain not found.");

            Vector3 worldPos = ParseVector3(@params["position"]) ?? Vector3.zero;
            float radius = p.GetFloat("radius") ?? 10f;
            float strength = p.GetFloat("strength") ?? 0.1f;

            var td = terrain.terrainData;
            int res = td.heightmapResolution;

            // Convert world position to heightmap coordinates
            Vector3 terrainPos = terrain.transform.position;
            float normX = (worldPos.x - terrainPos.x) / td.size.x;
            float normZ = (worldPos.z - terrainPos.z) / td.size.z;
            int centerX = Mathf.RoundToInt(normX * (res - 1));
            int centerY = Mathf.RoundToInt(normZ * (res - 1));

            float normRadius = radius / td.size.x * (res - 1);
            int brushSize = Mathf.CeilToInt(normRadius);

            int startX = Mathf.Clamp(centerX - brushSize, 0, res - 1);
            int startY = Mathf.Clamp(centerY - brushSize, 0, res - 1);
            int endX = Mathf.Clamp(centerX + brushSize, 0, res - 1);
            int endY = Mathf.Clamp(centerY + brushSize, 0, res - 1);
            int w = endX - startX + 1;
            int h = endY - startY + 1;

            float[,] heights = td.GetHeights(startX, startY, w, h);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dist = Vector2.Distance(new Vector2(startX + x, startY + y), new Vector2(centerX, centerY));
                    if (dist <= normRadius)
                    {
                        float falloff = 1f - (dist / normRadius);
                        heights[y, x] = Mathf.Clamp01(heights[y, x] + strength * falloff);
                    }
                }

            td.SetHeights(startX, startY, heights);

            return new SuccessResponse($"Terrain raised/lowered at ({worldPos.x}, {worldPos.z}).", new
            {
                position = Vec3(worldPos),
                radius,
                strength,
            });
        }

        // ─────────────────────────────────────────────
        // smooth
        // ─────────────────────────────────────────────

        private static object Smooth(JObject @params)
        {
            var p = new ToolParams(@params);
            var terrain = FindTerrain(p);
            if (terrain == null) return new ErrorResponse("Target terrain not found.");

            Vector3 worldPos = ParseVector3(@params["position"]) ?? Vector3.zero;
            float radius = p.GetFloat("radius") ?? 10f;
            int iterations = p.GetInt("iterations") ?? 1;

            var td = terrain.terrainData;
            int res = td.heightmapResolution;

            Vector3 terrainPos = terrain.transform.position;
            float normX = (worldPos.x - terrainPos.x) / td.size.x;
            float normZ = (worldPos.z - terrainPos.z) / td.size.z;
            int centerX = Mathf.RoundToInt(normX * (res - 1));
            int centerY = Mathf.RoundToInt(normZ * (res - 1));

            float normRadius = radius / td.size.x * (res - 1);
            int brushSize = Mathf.CeilToInt(normRadius) + 1;

            int startX = Mathf.Clamp(centerX - brushSize, 0, res - 1);
            int startY = Mathf.Clamp(centerY - brushSize, 0, res - 1);
            int endX = Mathf.Clamp(centerX + brushSize, 0, res - 1);
            int endY = Mathf.Clamp(centerY + brushSize, 0, res - 1);
            int w = endX - startX + 1;
            int h = endY - startY + 1;

            float[,] heights = td.GetHeights(startX, startY, w, h);

            for (int iter = 0; iter < iterations; iter++)
            {
                float[,] smoothed = (float[,])heights.Clone();
                for (int y = 1; y < h - 1; y++)
                    for (int x = 1; x < w - 1; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(startX + x, startY + y), new Vector2(centerX, centerY));
                        if (dist <= normRadius)
                        {
                            smoothed[y, x] = (heights[y - 1, x] + heights[y + 1, x] +
                                              heights[y, x - 1] + heights[y, x + 1] +
                                              heights[y, x]) / 5f;
                        }
                    }
                heights = smoothed;
            }

            td.SetHeights(startX, startY, heights);

            return new SuccessResponse($"Terrain smoothed at ({worldPos.x}, {worldPos.z}).", new
            {
                position = Vec3(worldPos),
                radius,
                iterations,
            });
        }

        // ─────────────────────────────────────────────
        // add_terrain_layer
        // ─────────────────────────────────────────────

        private static object AddTerrainLayer(JObject @params)
        {
            var p = new ToolParams(@params);
            var terrain = FindTerrain(p);
            if (terrain == null) return new ErrorResponse("Target terrain not found.");

            string diffusePath = p.Get("diffuse") ?? p.Get("diffuse_path") ?? p.Get("diffusePath");
            if (string.IsNullOrEmpty(diffusePath))
                return new ErrorResponse("'diffuse' texture path is required.");

            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
            if (diffuse == null) return new ErrorResponse($"Texture not found: '{diffusePath}'.");

            var layer = new TerrainLayer { diffuseTexture = diffuse };

            string normalPath = p.Get("normal") ?? p.Get("normal_path") ?? p.Get("normalPath");
            if (!string.IsNullOrEmpty(normalPath))
            {
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (normal != null) layer.normalMapTexture = normal;
            }

            var tileSize = ParseVector2(@params["tile_size"] ?? @params["tileSize"]);
            if (tileSize.HasValue) layer.tileSize = tileSize.Value;

            // Save layer as asset
            string layerPath = $"Assets/TerrainLayers/{terrain.name}_Layer{terrain.terrainData.terrainLayers.Length}.terrainlayer";
            string layerDir = Path.GetDirectoryName(layerPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(layerDir) && !AssetDatabase.IsValidFolder(layerDir))
                CreateFolderRecursive(layerDir);

            AssetDatabase.CreateAsset(layer, layerPath);

            var layers = terrain.terrainData.terrainLayers.ToList();
            layers.Add(layer);
            terrain.terrainData.terrainLayers = layers.ToArray();

            return new SuccessResponse($"Terrain layer added (index {layers.Count - 1}).", new
            {
                index = layers.Count - 1,
                diffuse = diffusePath,
                layerAssetPath = layerPath,
            });
        }

        // ─────────────────────────────────────────────
        // paint_texture
        // ─────────────────────────────────────────────

        private static object PaintTexture(JObject @params)
        {
            var p = new ToolParams(@params);
            var terrain = FindTerrain(p);
            if (terrain == null) return new ErrorResponse("Target terrain not found.");

            int layerIndex = p.GetInt("layer_index") ?? p.GetInt("layerIndex") ?? 0;
            Vector3 worldPos = ParseVector3(@params["position"]) ?? Vector3.zero;
            float radius = p.GetFloat("radius") ?? 10f;
            float opacity = p.GetFloat("opacity") ?? 1f;

            var td = terrain.terrainData;
            int alphaRes = td.alphamapResolution;
            int layerCount = td.alphamapLayers;

            if (layerIndex >= layerCount)
                return new ErrorResponse($"Layer index {layerIndex} out of range (max {layerCount - 1}).");

            Vector3 terrainPos = terrain.transform.position;
            float normX = (worldPos.x - terrainPos.x) / td.size.x;
            float normZ = (worldPos.z - terrainPos.z) / td.size.z;
            int centerX = Mathf.RoundToInt(normX * alphaRes);
            int centerY = Mathf.RoundToInt(normZ * alphaRes);
            float normRadius = radius / td.size.x * alphaRes;
            int brushSize = Mathf.CeilToInt(normRadius);

            int startX = Mathf.Clamp(centerX - brushSize, 0, alphaRes - 1);
            int startY = Mathf.Clamp(centerY - brushSize, 0, alphaRes - 1);
            int endX = Mathf.Clamp(centerX + brushSize, 0, alphaRes - 1);
            int endY = Mathf.Clamp(centerY + brushSize, 0, alphaRes - 1);
            int w = endX - startX + 1;
            int h = endY - startY + 1;

            float[,,] alphamaps = td.GetAlphamaps(startX, startY, w, h);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dist = Vector2.Distance(new Vector2(startX + x, startY + y), new Vector2(centerX, centerY));
                    if (dist <= normRadius)
                    {
                        float falloff = 1f - (dist / normRadius);
                        float weight = opacity * falloff;

                        // Increase target layer, decrease others proportionally
                        float remaining = 1f - weight;
                        float totalOther = 0f;
                        for (int l = 0; l < layerCount; l++)
                            if (l != layerIndex) totalOther += alphamaps[y, x, l];

                        for (int l = 0; l < layerCount; l++)
                        {
                            if (l == layerIndex)
                                alphamaps[y, x, l] = Mathf.Lerp(alphamaps[y, x, l], 1f, weight);
                            else if (totalOther > 0)
                                alphamaps[y, x, l] *= remaining / totalOther * (1f - alphamaps[y, x, layerIndex]);
                        }
                    }
                }

            td.SetAlphamaps(startX, startY, alphamaps);

            return new SuccessResponse($"Texture painted (layer {layerIndex}).", new
            {
                layerIndex,
                position = Vec3(worldPos),
                radius,
                opacity,
            });
        }

        // ─────────────────────────────────────────────
        // add_tree_prototype / paint_trees
        // ─────────────────────────────────────────────

        private static object AddTreePrototype(JObject @params)
        {
            var p = new ToolParams(@params);
            var terrain = FindTerrain(p);
            if (terrain == null) return new ErrorResponse("Target terrain not found.");

            string prefabPath = p.Get("prefab") ?? p.Get("prefab_path") ?? p.Get("prefabPath");
            if (string.IsNullOrEmpty(prefabPath))
                return new ErrorResponse("'prefab' path is required.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return new ErrorResponse($"Prefab not found: '{prefabPath}'.");

            var protos = terrain.terrainData.treePrototypes.ToList();
            protos.Add(new TreePrototype { prefab = prefab });
            terrain.terrainData.treePrototypes = protos.ToArray();

            return new SuccessResponse($"Tree prototype added (index {protos.Count - 1}).", new
            {
                index = protos.Count - 1,
                prefab = prefabPath,
            });
        }

        private static object PaintTrees(JObject @params)
        {
            var p = new ToolParams(@params);
            var terrain = FindTerrain(p);
            if (terrain == null) return new ErrorResponse("Target terrain not found.");

            int protoIndex = p.GetInt("prototype_index") ?? p.GetInt("prototypeIndex") ?? 0;
            int density = p.GetInt("density") ?? 100;
            float minHeight = p.GetFloat("min_height") ?? p.GetFloat("minHeight") ?? 0.8f;
            float maxHeight = p.GetFloat("max_height") ?? p.GetFloat("maxHeight") ?? 1.2f;
            bool randomRotation = p.GetBool("random_rotation") || p.GetBool("randomRotation");

            // Parse area (normalized 0-1 or heightmap coords)
            float areaMinX = 0, areaMinZ = 0, areaMaxX = 1, areaMaxZ = 1;
            var areaToken = @params["area"] as JArray;
            if (areaToken != null && areaToken.Count >= 4)
            {
                int res = terrain.terrainData.heightmapResolution;
                areaMinX = (float)areaToken[0] / res;
                areaMinZ = (float)areaToken[1] / res;
                areaMaxX = (float)areaToken[2] / res;
                areaMaxZ = (float)areaToken[3] / res;
            }

            var existing = terrain.terrainData.treeInstances.ToList();
            var rand = new System.Random();

            for (int i = 0; i < density; i++)
            {
                float x = (float)(areaMinX + rand.NextDouble() * (areaMaxX - areaMinX));
                float z = (float)(areaMinZ + rand.NextDouble() * (areaMaxZ - areaMinZ));
                float h = (float)(minHeight + rand.NextDouble() * (maxHeight - minHeight));
                float rot = randomRotation ? (float)(rand.NextDouble() * 360f) : 0f;

                existing.Add(new TreeInstance
                {
                    position = new Vector3(x, 0, z),
                    prototypeIndex = protoIndex,
                    widthScale = h,
                    heightScale = h,
                    rotation = rot * Mathf.Deg2Rad,
                    color = Color.white,
                    lightmapColor = Color.white,
                });
            }

            terrain.terrainData.treeInstances = existing.ToArray();
            terrain.Flush();

            return new SuccessResponse($"{density} trees placed.", new
            {
                prototypeIndex = protoIndex,
                count = density,
                totalTrees = existing.Count,
            });
        }

        // ─────────────────────────────────────────────
        // add_detail_prototype / paint_details
        // ─────────────────────────────────────────────

        private static object AddDetailPrototype(JObject @params)
        {
            var p = new ToolParams(@params);
            var terrain = FindTerrain(p);
            if (terrain == null) return new ErrorResponse("Target terrain not found.");

            string texturePath = p.Get("texture") ?? p.Get("texture_path") ?? p.Get("texturePath");

            var proto = new DetailPrototype
            {
                minHeight = p.GetFloat("min_height") ?? p.GetFloat("minHeight") ?? 0.5f,
                maxHeight = p.GetFloat("max_height") ?? p.GetFloat("maxHeight") ?? 1f,
                minWidth = p.GetFloat("min_width") ?? p.GetFloat("minWidth") ?? 0.5f,
                maxWidth = p.GetFloat("max_width") ?? p.GetFloat("maxWidth") ?? 1f,
                renderMode = DetailRenderMode.GrassBillboard,
            };

            if (!string.IsNullOrEmpty(texturePath))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (tex != null) proto.prototypeTexture = tex;
            }

            var protos = terrain.terrainData.detailPrototypes.ToList();
            protos.Add(proto);
            terrain.terrainData.detailPrototypes = protos.ToArray();

            return new SuccessResponse($"Detail prototype added (index {protos.Count - 1}).", new
            {
                index = protos.Count - 1,
            });
        }

        private static object PaintDetails(JObject @params)
        {
            var p = new ToolParams(@params);
            var terrain = FindTerrain(p);
            if (terrain == null) return new ErrorResponse("Target terrain not found.");

            int protoIndex = p.GetInt("prototype_index") ?? p.GetInt("prototypeIndex") ?? 0;
            int density = p.GetInt("density") ?? 4;

            var td = terrain.terrainData;
            int detailRes = td.detailResolution;
            int[,] details = td.GetDetailLayer(0, 0, detailRes, detailRes, protoIndex);

            var areaToken = @params["area"] as JArray;
            int startX = 0, startY = 0, endX = detailRes, endY = detailRes;
            if (areaToken != null && areaToken.Count >= 4)
            {
                startX = Mathf.Clamp((int)areaToken[0], 0, detailRes);
                startY = Mathf.Clamp((int)areaToken[1], 0, detailRes);
                endX = Mathf.Clamp((int)areaToken[2], 0, detailRes);
                endY = Mathf.Clamp((int)areaToken[3], 0, detailRes);
            }

            for (int y = startY; y < endY; y++)
                for (int x = startX; x < endX; x++)
                    details[y, x] = density;

            td.SetDetailLayer(0, 0, protoIndex, details);

            return new SuccessResponse($"Details painted (prototype {protoIndex}, density {density}).", new
            {
                prototypeIndex = protoIndex,
                density,
                area = new { startX, startY, endX, endY },
            });
        }

        // ─────────────────────────────────────────────
        // set_properties / get_info
        // ─────────────────────────────────────────────

        private static object SetProperties(JObject @params)
        {
            var p = new ToolParams(@params);
            var terrain = FindTerrain(p);
            if (terrain == null) return new ErrorResponse("Target terrain not found.");

            var td = terrain.terrainData;
            int changed = 0;

            Vector3? size = ParseVector3(@params["size"]);
            if (size.HasValue) { td.size = size.Value; changed++; }

            float? detailDistance = p.GetFloat("detail_object_distance") ?? p.GetFloat("detailObjectDistance");
            if (detailDistance.HasValue) { terrain.detailObjectDistance = detailDistance.Value; changed++; }

            float? treeDistance = p.GetFloat("tree_distance") ?? p.GetFloat("treeDistance");
            if (treeDistance.HasValue) { terrain.treeDistance = treeDistance.Value; changed++; }

            float? treeBillboardDistance = p.GetFloat("tree_billboard_distance") ?? p.GetFloat("treeBillboardDistance");
            if (treeBillboardDistance.HasValue) { terrain.treeBillboardDistance = treeBillboardDistance.Value; changed++; }

            float? heightmapPixelError = p.GetFloat("heightmap_pixel_error") ?? p.GetFloat("heightmapPixelError");
            if (heightmapPixelError.HasValue) { terrain.heightmapPixelError = heightmapPixelError.Value; changed++; }

            if (changed == 0) return new ErrorResponse("No valid properties provided.");

            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(td);

            return new SuccessResponse($"Updated {changed} terrain properties.", new { changed });
        }

        private static object GetInfo(JObject @params)
        {
            var p = new ToolParams(@params);
            var terrain = FindTerrain(p);
            if (terrain == null) return new ErrorResponse("Target terrain not found.");

            var td = terrain.terrainData;

            return new SuccessResponse($"Terrain '{terrain.name}' info.", new
            {
                name = terrain.name,
                size = Vec3(td.size),
                heightmapResolution = td.heightmapResolution,
                alphamapResolution = td.alphamapResolution,
                detailResolution = td.detailResolution,
                terrainLayerCount = td.terrainLayers.Length,
                treePrototypeCount = td.treePrototypes.Length,
                treeInstanceCount = td.treeInstances.Length,
                detailPrototypeCount = td.detailPrototypes.Length,
                position = Vec3(terrain.transform.position),
            });
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private static UnityEngine.Terrain FindTerrain(ToolParams p)
        {
            string target = p.Get("target");
            if (string.IsNullOrEmpty(target))
                return UnityEngine.Terrain.activeTerrain;

            if (int.TryParse(target, out int id))
            {
                var obj = EditorUtility.InstanceIDToObject(id) as GameObject;
                if (obj != null) return obj.GetComponent<UnityEngine.Terrain>();
            }

            var go = GameObject.Find(target);
            return go?.GetComponent<UnityEngine.Terrain>();
        }

        private static bool IsValidResolution(int r)
        {
            // Valid: 33, 65, 129, 257, 513, 1025, 2049, 4097
            if (r < 33 || r > 4097) return false;
            return (r - 1 & r - 2) == 0;
        }

        private static Vector3? ParseVector3(JToken token)
        {
            if (token == null) return null;
            if (token is JArray arr && arr.Count >= 3)
                return new Vector3((float)arr[0], (float)arr[1], (float)arr[2]);
            if (token is JObject obj)
                return new Vector3((float?)obj["x"] ?? 0, (float?)obj["y"] ?? 0, (float?)obj["z"] ?? 0);
            return null;
        }

        private static Vector2? ParseVector2(JToken token)
        {
            if (token == null) return null;
            if (token is JArray arr && arr.Count >= 2)
                return new Vector2((float)arr[0], (float)arr[1]);
            if (token is JObject obj)
                return new Vector2((float?)obj["x"] ?? 0, (float?)obj["y"] ?? 0);
            return null;
        }

        private static float[] Vec3(Vector3 v) => new[] { v.x, v.y, v.z };

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
