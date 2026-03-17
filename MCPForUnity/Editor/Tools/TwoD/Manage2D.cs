using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools.TwoD
{
    [McpForUnityTool("manage_2d", AutoRegister = false, Group = "2d")]
    public static class Manage2D
    {
        // Runtime detection
        private static readonly Type TilemapType =
            Type.GetType("UnityEngine.Tilemaps.Tilemap, UnityEngine.TilemapModule");
        private static readonly Type TileBaseType =
            Type.GetType("UnityEngine.Tilemaps.TileBase, UnityEngine.TilemapModule");
        private static readonly Type SpriteAtlasType =
            Type.GetType("UnityEngine.U2D.SpriteAtlas, UnityEngine.U2DModule");

        private static readonly bool HasTilemap = TilemapType != null;
        private static readonly bool HasSpriteAtlas = SpriteAtlasType != null;

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
                    "ping" => new SuccessResponse("2D tool ready.", new
                    {
                        hasTilemap = HasTilemap,
                        hasSpriteAtlas = HasSpriteAtlas,
                    }),
                    "tilemap_set_tile" => TilemapSetTile(@params),
                    "tilemap_fill" => TilemapFill(@params),
                    "tilemap_clear" => TilemapClear(@params),
                    "tilemap_get_info" => TilemapGetInfo(@params),
                    "create_sprite_atlas" => CreateSpriteAtlas(@params),
                    "atlas_add_folders" => AtlasAddFolders(@params),
                    "atlas_pack" => AtlasPack(),
                    _ => new ErrorResponse($"Unknown action: '{action}'."),
                };
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"2D action '{action}' failed: {ex.Message}",
                    new { stackTrace = ex.StackTrace });
            }
        }

        // ─────────────────────────────────────────────
        // tilemap_set_tile
        // ─────────────────────────────────────────────

        private static object TilemapSetTile(JObject @params)
        {
            if (!HasTilemap)
                return new ErrorResponse("Tilemap module not available.");

            var p = new ToolParams(@params);
            var tilemap = FindTilemap(p);
            if (tilemap == null) return new ErrorResponse("Target Tilemap not found.");

            var posArray = @params["position"] as JArray;
            if (posArray == null || posArray.Count < 2)
                return new ErrorResponse("'position' [x, y] is required.");

            int x = (int)posArray[0];
            int y = (int)posArray[1];
            int z = posArray.Count > 2 ? (int)posArray[2] : 0;

            // Get tile asset (null = clear)
            string tilePath = p.Get("tile");
            object tileAsset = null;
            if (!string.IsNullOrEmpty(tilePath))
            {
                tileAsset = AssetDatabase.LoadAssetAtPath(tilePath, TileBaseType);
                if (tileAsset == null)
                    return new ErrorResponse($"Tile not found: '{tilePath}'.");
            }

            // tilemap.SetTile(Vector3Int, TileBase) via reflection
            var setTileMethod = TilemapType.GetMethod("SetTile",
                new[] { typeof(Vector3Int), TileBaseType });
            setTileMethod?.Invoke(tilemap, new object[] { new Vector3Int(x, y, z), tileAsset });

            if (tilemap is Component comp1) { Undo.RecordObject(comp1, "Set Tile"); EditorUtility.SetDirty(comp1); }

            return new SuccessResponse(tileAsset != null
                ? $"Tile set at ({x}, {y})."
                : $"Tile cleared at ({x}, {y}).", new
            {
                position = new[] { x, y, z },
                tile = tilePath,
            });
        }

        // ─────────────────────────────────────────────
        // tilemap_fill
        // ─────────────────────────────────────────────

        private static object TilemapFill(JObject @params)
        {
            if (!HasTilemap)
                return new ErrorResponse("Tilemap module not available.");

            var p = new ToolParams(@params);
            var tilemap = FindTilemap(p);
            if (tilemap == null) return new ErrorResponse("Target Tilemap not found.");

            var fromPos = @params["from_pos"] ?? @params["fromPos"];
            var toPos = @params["to_pos"] ?? @params["toPos"];

            if (fromPos == null || toPos == null)
                return new ErrorResponse("'from_pos' [x,y] and 'to_pos' [x,y] are required.");

            int fromX = (int)((JArray)fromPos)[0];
            int fromY = (int)((JArray)fromPos)[1];
            int toX = (int)((JArray)toPos)[0];
            int toY = (int)((JArray)toPos)[1];

            string tilePath = p.Get("tile");
            object tileAsset = null;
            if (!string.IsNullOrEmpty(tilePath))
            {
                tileAsset = AssetDatabase.LoadAssetAtPath(tilePath, TileBaseType);
                if (tileAsset == null) return new ErrorResponse($"Tile not found: '{tilePath}'.");
            }

            var setTileMethod = TilemapType.GetMethod("SetTile",
                new[] { typeof(Vector3Int), TileBaseType });

            int minX = Math.Min(fromX, toX), maxX = Math.Max(fromX, toX);
            int minY = Math.Min(fromY, toY), maxY = Math.Max(fromY, toY);
            int count = 0;

            if (tilemap is Component comp2) Undo.RecordObject(comp2, "Fill Tilemap");

            for (int iy = minY; iy <= maxY; iy++)
                for (int ix = minX; ix <= maxX; ix++)
                {
                    setTileMethod?.Invoke(tilemap, new object[] { new Vector3Int(ix, iy, 0), tileAsset });
                    count++;
                }

            if (tilemap is Component comp2b) EditorUtility.SetDirty(comp2b);

            return new SuccessResponse($"Filled {count} tiles from ({minX},{minY}) to ({maxX},{maxY}).", new
            {
                from = new[] { minX, minY },
                to = new[] { maxX, maxY },
                tileCount = count,
                tile = tilePath,
            });
        }

        // ─────────────────────────────────────────────
        // tilemap_clear
        // ─────────────────────────────────────────────

        private static object TilemapClear(JObject @params)
        {
            if (!HasTilemap)
                return new ErrorResponse("Tilemap module not available.");

            var p = new ToolParams(@params);
            var tilemap = FindTilemap(p);
            if (tilemap == null) return new ErrorResponse("Target Tilemap not found.");

            if (tilemap is Component comp3) Undo.RecordObject(comp3, "Clear Tilemap");

            var clearMethod = TilemapType.GetMethod("ClearAllTiles",
                BindingFlags.Instance | BindingFlags.Public);
            clearMethod?.Invoke(tilemap, null);

            if (tilemap is Component comp3b) EditorUtility.SetDirty(comp3b);

            return new SuccessResponse("Tilemap cleared.");
        }

        // ─────────────────────────────────────────────
        // tilemap_get_info
        // ─────────────────────────────────────────────

        private static object TilemapGetInfo(JObject @params)
        {
            if (!HasTilemap)
                return new ErrorResponse("Tilemap module not available.");

            var p = new ToolParams(@params);
            var tilemap = FindTilemap(p);
            if (tilemap == null) return new ErrorResponse("Target Tilemap not found.");

            var sizeProp = TilemapType.GetProperty("size", BindingFlags.Instance | BindingFlags.Public);
            var originProp = TilemapType.GetProperty("origin", BindingFlags.Instance | BindingFlags.Public);
            var cellBoundsProp = TilemapType.GetProperty("cellBounds", BindingFlags.Instance | BindingFlags.Public);

            Vector3Int size = sizeProp != null ? (Vector3Int)sizeProp.GetValue(tilemap) : Vector3Int.zero;
            Vector3Int origin = originProp != null ? (Vector3Int)originProp.GetValue(tilemap) : Vector3Int.zero;

            return new SuccessResponse("Tilemap info.", new
            {
                gameObject = (tilemap as Component)?.gameObject.name ?? "unknown",
                size = new[] { size.x, size.y, size.z },
                origin = new[] { origin.x, origin.y, origin.z },
            });
        }

        // ─────────────────────────────────────────────
        // create_sprite_atlas
        // ─────────────────────────────────────────────

        private static object CreateSpriteAtlas(JObject @params)
        {
            if (!HasSpriteAtlas)
                return new ErrorResponse("SpriteAtlas module not available.");

            var p = new ToolParams(@params);
            string path = p.Get("path");
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("'path' is required (e.g., 'Assets/Atlas/UI.spriteatlas').");

            var atlas = ScriptableObject.CreateInstance(SpriteAtlasType);
            if (atlas == null)
                return new ErrorResponse("Failed to create SpriteAtlas.");

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

            AssetDatabase.CreateAsset(atlas, path);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"SpriteAtlas created at '{path}'.", new { path });
        }

        // ─────────────────────────────────────────────
        // atlas_add_folders
        // ─────────────────────────────────────────────

        private static object AtlasAddFolders(JObject @params)
        {
            if (!HasSpriteAtlas)
                return new ErrorResponse("SpriteAtlas module not available.");

            var p = new ToolParams(@params);
            string atlasPath = p.Get("atlas") ?? p.Get("path");
            if (string.IsNullOrEmpty(atlasPath))
                return new ErrorResponse("'atlas' path is required.");

            var atlas = AssetDatabase.LoadAssetAtPath(atlasPath, SpriteAtlasType);
            if (atlas == null)
                return new ErrorResponse($"SpriteAtlas not found at '{atlasPath}'.");

            var folders = p.GetStringArray("folders") ?? p.GetStringArray("sources");
            if (folders == null || folders.Length == 0)
                return new ErrorResponse("'folders' array is required.");

            // SpriteAtlas.Add(Object[]) via reflection
            var addMethod = SpriteAtlasType.GetMethod("Add",
                new[] { typeof(UnityEngine.Object[]) });

            var objects = new List<UnityEngine.Object>();
            foreach (var folder in folders)
            {
                var obj = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder);
                if (obj != null) objects.Add(obj);
            }

            if (objects.Count > 0)
                addMethod?.Invoke(atlas, new object[] { objects.ToArray() });

            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Added {objects.Count} sources to atlas.", new
            {
                atlas = atlasPath,
                addedCount = objects.Count,
                folders,
            });
        }

        // ─────────────────────────────────────────────
        // atlas_pack
        // ─────────────────────────────────────────────

        private static object AtlasPack()
        {
            // SpriteAtlasUtility.PackAllAtlases via reflection
            var utilType = Type.GetType("UnityEditor.U2D.SpriteAtlasUtility, UnityEditor.U2DModule")
                ?? Type.GetType("UnityEditor.U2D.SpriteAtlasUtility, UnityEditor");

            if (utilType == null)
                return new ErrorResponse("SpriteAtlasUtility not available.");

            var packMethod = utilType.GetMethod("PackAllAtlases",
                BindingFlags.Static | BindingFlags.Public,
                null, new[] { typeof(BuildTarget) }, null);

            if (packMethod == null)
            {
                // Try without parameters
                packMethod = utilType.GetMethod("PackAllAtlases",
                    BindingFlags.Static | BindingFlags.Public);
            }

            if (packMethod != null)
            {
                var methodParams = packMethod.GetParameters();
                if (methodParams.Length == 1)
                    packMethod.Invoke(null, new object[] { EditorUserBuildSettings.activeBuildTarget });
                else
                    packMethod.Invoke(null, null);
            }

            return new SuccessResponse("All SpriteAtlases packed.");
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private static object FindTilemap(ToolParams p)
        {
            string target = p.Get("target");

            if (!string.IsNullOrEmpty(target))
            {
                if (int.TryParse(target, out int id))
                {
                    var obj = EditorUtility.InstanceIDToObject(id) as GameObject;
                    if (obj != null) return obj.GetComponent(TilemapType);
                }
                var go = GameObject.Find(target);
                if (go != null) return go.GetComponent(TilemapType);
            }

            // Find first Tilemap in scene
#if UNITY_2022_2_OR_NEWER
            var all = UnityEngine.Object.FindObjectsByType(TilemapType, FindObjectsSortMode.None);
#else
            var all = UnityEngine.Object.FindObjectsOfType(TilemapType);
#endif
            return all.Length > 0 ? all[0] : null;
        }
    }
}
