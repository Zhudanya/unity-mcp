using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Runtime.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("editor_ui_automation", AutoRegister = false, Group = "ui")]
    public static class EditorUIAutomation
    {
        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("Parameters cannot be null.");

            string action = ParamCoercion.CoerceString(@params["action"], null)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
                return new ErrorResponse("'action' parameter is required. Supported: snapshot, screenshot, click, type, drag, send_event, focus_window");

            try
            {
                return action switch
                {
                    "snapshot" => TakeSnapshot(@params),
                    "screenshot" => TakeScreenshot(@params),
                    "click" => ClickElement(@params),
                    "type" => TypeText(@params),
                    "drag" => DragElement(@params),
                    "send_event" => SendRawEvent(@params),
                    "focus_window" => FocusWindow(@params),
                    _ => new ErrorResponse($"Unknown action: '{action}'. Supported: snapshot, screenshot, click, type, drag, send_event, focus_window")
                };
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Action '{action}' failed: {ex.Message}", new { stackTrace = ex.StackTrace });
            }
        }

        // ─────────────────────────────────────────────
        // Action: snapshot
        // ─────────────────────────────────────────────

        private static object TakeSnapshot(JObject @params)
        {
            string windowFilter = ParamCoercion.CoerceString(@params["window_filter"] ?? @params["windowFilter"], null);
            int maxDepth = (int?)@params["max_depth"] ?? (int?)@params["maxDepth"] ?? 15;
            bool includeStyles = (bool?)@params["include_styles"] ?? (bool?)@params["includeStyles"] ?? false;

            string snapshot = EditorUIRefMap.TakeSnapshot(windowFilter, maxDepth, includeStyles);

            return new SuccessResponse("Editor UI snapshot captured.", new
            {
                snapshot,
                elementCount = EditorUIRefMap.Count,
                refMapVersion = EditorUIRefMap.Version,
            });
        }

        // ─────────────────────────────────────────────
        // Action: click
        // ─────────────────────────────────────────────

        private static object ClickElement(JObject @params)
        {
            string refId = ParamCoercion.CoerceString(@params["ref"], null);
            int clickCount = (int?)@params["click_count"] ?? (int?)@params["clickCount"] ?? 1;

            if (string.IsNullOrEmpty(refId))
                return new ErrorResponse("'ref' parameter is required (e.g., '@e3').");

            var element = EditorUIRefMap.Resolve(refId, out string error);
            if (element == null)
                return new ErrorResponse(error);

            // Ensure the owning window is focused
            FocusOwningWindow(element);

            // Try direct API approach first (most reliable)
            if (element is Button button)
            {
                // Use InvokeClick via reflection for Button
                using (var clickEvent = ClickEvent.GetPooled())
                {
                    clickEvent.target = button;
                    button.SendEvent(clickEvent);
                }
                return new SuccessResponse($"Clicked button '{EditorUIRefMap.Resolve(refId, out _)?.name ?? refId}'.", new
                {
                    @ref = refId,
                    method = "click_event",
                });
            }

            if (element is Toggle toggle)
            {
                toggle.value = !toggle.value;
                return new SuccessResponse($"Toggled '{toggle.label}' to {toggle.value}.", new
                {
                    @ref = refId,
                    method = "value_toggle",
                    value = toggle.value,
                });
            }

            if (element is Foldout foldout)
            {
                foldout.value = !foldout.value;
                return new SuccessResponse($"Foldout '{foldout.text}' {(foldout.value ? "expanded" : "collapsed")}.", new
                {
                    @ref = refId,
                    method = "value_toggle",
                    value = foldout.value,
                });
            }

            // Generic click via pointer events for other elements
            var rect = element.worldBound;
            var center = rect.center;

            DispatchPointerClick(element, center, clickCount);

            return new SuccessResponse($"Clicked element @{EditorUIRefMap.Resolve(refId, out _)?.name ?? refId}.", new
            {
                @ref = refId,
                method = "pointer_event",
                x = center.x,
                y = center.y,
            });
        }

        // ─────────────────────────────────────────────
        // Action: type
        // ─────────────────────────────────────────────

        private static object TypeText(JObject @params)
        {
            string refId = ParamCoercion.CoerceString(@params["ref"], null);
            string text = ParamCoercion.CoerceString(@params["text"], null);
            bool clear = (bool?)@params["clear"] ?? false;
            var keysToken = @params["keys"];

            if (string.IsNullOrEmpty(refId))
                return new ErrorResponse("'ref' parameter is required.");

            if (string.IsNullOrEmpty(text) && keysToken == null)
                return new ErrorResponse("Either 'text' or 'keys' parameter is required.");

            var element = EditorUIRefMap.Resolve(refId, out string error);
            if (element == null)
                return new ErrorResponse(error);

            FocusOwningWindow(element);

            // Direct value setting (most reliable for text fields)
            if (text != null)
            {
                if (TrySetFieldValue(element, text, clear, out string setError))
                {
                    return new SuccessResponse($"Set value to '{text}'.", new
                    {
                        @ref = refId,
                        method = "direct_value",
                        text,
                    });
                }

                // Fallback: focus and send key events
                element.Focus();
                if (clear)
                {
                    SendKeyboardEvent(element, KeyCode.A, EventModifiers.Control);
                    SendKeyboardEvent(element, KeyCode.Delete, EventModifiers.None);
                }

                foreach (char c in text)
                {
                    SendCharacterEvent(element, c);
                }

                return new SuccessResponse($"Typed '{text}'.", new
                {
                    @ref = refId,
                    method = "key_events",
                    text,
                });
            }

            // Special keys
            if (keysToken != null)
            {
                var keys = keysToken.Type == JTokenType.Array
                    ? keysToken.Values<string>().ToArray()
                    : new[] { keysToken.ToString() };

                element.Focus();
                foreach (var keyName in keys)
                {
                    var (keyCode, modifiers) = ParseKeyName(keyName);
                    SendKeyboardEvent(element, keyCode, modifiers);
                }

                return new SuccessResponse($"Sent keys: {string.Join(", ", keys)}.", new
                {
                    @ref = refId,
                    method = "key_events",
                    keys,
                });
            }

            return new ErrorResponse("Unexpected state.");
        }

        // ─────────────────────────────────────────────
        // Action: drag
        // ─────────────────────────────────────────────

        private static object DragElement(JObject @params)
        {
            string fromRef = ParamCoercion.CoerceString(@params["from_ref"] ?? @params["fromRef"], null);
            string toRef = ParamCoercion.CoerceString(@params["to_ref"] ?? @params["toRef"], null);
            string assetPath = ParamCoercion.CoerceString(@params["asset_path"] ?? @params["assetPath"], null);
            int steps = (int?)@params["steps"] ?? 10;

            if (string.IsNullOrEmpty(fromRef) && string.IsNullOrEmpty(assetPath))
                return new ErrorResponse("Either 'from_ref' or 'asset_path' is required.");
            if (string.IsNullOrEmpty(toRef))
                return new ErrorResponse("'to_ref' parameter is required.");

            var targetElement = EditorUIRefMap.Resolve(toRef, out string toError);
            if (targetElement == null)
                return new ErrorResponse(toError);

            FocusOwningWindow(targetElement);
            var targetRect = targetElement.worldBound;
            var targetCenter = targetRect.center;

            // Asset drag mode: use Unity's DragAndDrop API
            if (!string.IsNullOrEmpty(assetPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null)
                    return new ErrorResponse($"Asset not found at path: '{assetPath}'.");

                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new[] { asset };
                DragAndDrop.paths = new[] { assetPath };
                DragAndDrop.StartDrag($"Drag {asset.name}");

                // Simulate drag events on target
                DispatchDragEvents(targetElement, targetCenter);

                return new SuccessResponse($"Dragged asset '{asset.name}' to @{toRef}.", new
                {
                    assetPath,
                    assetName = asset.name,
                    toRef,
                    method = "drag_and_drop_api",
                });
            }

            // Element-to-element drag mode: pointer event sequence
            var sourceElement = EditorUIRefMap.Resolve(fromRef, out string fromError);
            if (sourceElement == null)
                return new ErrorResponse(fromError);

            var sourceRect = sourceElement.worldBound;
            var sourceCenter = sourceRect.center;

            // Check if source is a hierarchy/project item (use DragAndDrop for GameObjects)
            var sourceGo = GetGameObjectFromElement(sourceElement);
            if (sourceGo != null)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new[] { sourceGo };
                DragAndDrop.StartDrag($"Drag {sourceGo.name}");

                DispatchDragEvents(targetElement, targetCenter);

                return new SuccessResponse($"Dragged '{sourceGo.name}' to @{toRef}.", new
                {
                    fromRef,
                    toRef,
                    method = "drag_and_drop_api",
                    gameObject = sourceGo.name,
                });
            }

            // Generic pointer-based drag
            DispatchPointerDrag(sourceElement, targetElement, sourceCenter, targetCenter, steps);

            return new SuccessResponse($"Dragged @{fromRef} to @{toRef}.", new
            {
                fromRef,
                toRef,
                method = "pointer_events",
                steps,
            });
        }

        // ─────────────────────────────────────────────
        // Action: screenshot
        // Captures any EditorWindow (IMGUI or UIElements) as base64 PNG
        // ─────────────────────────────────────────────

        private const int RepaintSettlingDelayMs = 75;

        private static object TakeScreenshot(JObject @params)
        {
            string windowTitle = ParamCoercion.CoerceString(@params["window_title"] ?? @params["windowTitle"], null);
            string windowType = ParamCoercion.CoerceString(@params["window_type"] ?? @params["windowType"], null);
            int maxResolution = (int?)@params["max_resolution"] ?? (int?)@params["maxResolution"] ?? 800;

            if (string.IsNullOrEmpty(windowTitle) && string.IsNullOrEmpty(windowType))
                return new ErrorResponse("Either 'window_title' or 'window_type' is required for screenshot.");

            var window = FindEditorWindow(windowTitle, windowType);
            if (window == null)
                return new ErrorResponse($"Window not found: '{windowTitle ?? windowType}'.");

            try
            {
                // Focus and repaint the window to ensure fresh content
                window.Focus();
                window.Repaint();
                try
                {
                    // RepaintImmediately is internal in some Unity versions
                    var repaintMethod = window.GetType().GetMethod("RepaintImmediately",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    repaintMethod?.Invoke(window, null);
                }
                catch { }
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                Thread.Sleep(RepaintSettlingDelayMs);

                // Get the host view for GrabPixels
                object hostView = GetHostView(window);
                if (hostView == null)
                    return new ErrorResponse("Failed to access window host view for screenshot.");

                MethodInfo grabPixels = hostView.GetType().GetMethod(
                    "GrabPixels",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(RenderTexture), typeof(Rect) },
                    null);

                if (grabPixels == null)
                    return new ErrorResponse("GrabPixels method not available in this Unity version.");

                float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
                Rect windowPos = window.position;
                int pixelWidth = Mathf.RoundToInt(windowPos.width * pixelsPerPoint);
                int pixelHeight = Mathf.RoundToInt(windowPos.height * pixelsPerPoint);

                if (pixelWidth <= 0 || pixelHeight <= 0)
                    return new ErrorResponse("Window has zero size.");

                Rect captureRect = new Rect(0, 0, pixelWidth, pixelHeight);

                RenderTexture rt = null;
                RenderTexture previousActive = RenderTexture.active;
                Texture2D captured = null;
                Texture2D downscaled = null;

                try
                {
                    rt = new RenderTexture(pixelWidth, pixelHeight, 0, RenderTextureFormat.ARGB32)
                    {
                        antiAliasing = 1,
                        filterMode = FilterMode.Bilinear,
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    rt.Create();

                    grabPixels.Invoke(hostView, new object[] { rt, captureRect });

                    RenderTexture.active = rt;
                    captured = new Texture2D(pixelWidth, pixelHeight, TextureFormat.RGBA32, false);
                    captured.ReadPixels(new Rect(0, 0, pixelWidth, pixelHeight), 0, 0);
                    captured.Apply();
                    FlipTextureVertically(captured);

                    // Downscale if needed
                    string imageBase64;
                    int imageWidth, imageHeight;

                    if (pixelWidth > maxResolution || pixelHeight > maxResolution)
                    {
                        downscaled = ScreenshotUtility.DownscaleTexture(captured, maxResolution);
                        imageBase64 = Convert.ToBase64String(downscaled.EncodeToPNG());
                        imageWidth = downscaled.width;
                        imageHeight = downscaled.height;
                    }
                    else
                    {
                        imageBase64 = Convert.ToBase64String(captured.EncodeToPNG());
                        imageWidth = pixelWidth;
                        imageHeight = pixelHeight;
                    }

                    return new SuccessResponse($"Screenshot of '{window.titleContent.text}' captured.", new
                    {
                        imageBase64,
                        imageWidth,
                        imageHeight,
                        windowTitle = window.titleContent.text,
                        windowSize = new { width = windowPos.width, height = windowPos.height },
                        pixelsPerPoint,
                    });
                }
                finally
                {
                    RenderTexture.active = previousActive;
                    if (rt != null)
                    {
                        rt.Release();
                        UnityEngine.Object.DestroyImmediate(rt);
                    }
                    if (captured != null) UnityEngine.Object.DestroyImmediate(captured);
                    if (downscaled != null) UnityEngine.Object.DestroyImmediate(downscaled);
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Screenshot failed: {ex.Message}", new { stackTrace = ex.StackTrace });
            }
        }

        private static object GetHostView(EditorWindow window)
        {
            if (window == null) return null;
            FieldInfo parentField = typeof(EditorWindow).GetField("m_Parent",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (parentField != null)
            {
                object parent = parentField.GetValue(window);
                if (parent != null) return parent;
            }
            PropertyInfo hostViewProp = typeof(EditorWindow).GetProperty("hostView",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return hostViewProp?.GetValue(window, null);
        }

        private static void FlipTextureVertically(Texture2D texture)
        {
            if (texture == null) return;
            int width = texture.width;
            int height = texture.height;
            Color32[] pixels = texture.GetPixels32();
            var temp = new Color32[width];
            for (int y = 0; y < height / 2; y++)
            {
                int topRow = y * width;
                int bottomRow = (height - 1 - y) * width;
                Array.Copy(pixels, topRow, temp, 0, width);
                Array.Copy(pixels, bottomRow, pixels, topRow, width);
                Array.Copy(temp, 0, pixels, bottomRow, width);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
        }

        // ─────────────────────────────────────────────
        // Action: send_event
        // Sends raw mouse/keyboard events to any EditorWindow (works with IMGUI)
        // ─────────────────────────────────────────────

        private static object SendRawEvent(JObject @params)
        {
            string windowTitle = ParamCoercion.CoerceString(@params["window_title"] ?? @params["windowTitle"], null);
            string windowType = ParamCoercion.CoerceString(@params["window_type"] ?? @params["windowType"], null);
            string eventType = ParamCoercion.CoerceString(@params["event_type"] ?? @params["eventType"], null);

            if (string.IsNullOrEmpty(eventType))
                return new ErrorResponse("'event_type' is required. Supported: mouse_click, mouse_double_click, mouse_down, mouse_up, mouse_move, mouse_drag, key_down, key_up, scroll, drag_asset");

            if (string.IsNullOrEmpty(windowTitle) && string.IsNullOrEmpty(windowType))
                return new ErrorResponse("Either 'window_title' or 'window_type' is required.");

            var window = FindEditorWindow(windowTitle, windowType);
            if (window == null)
                return new ErrorResponse($"Window not found: '{windowTitle ?? windowType}'.");

            window.Focus();

            try
            {
                return eventType.ToLowerInvariant() switch
                {
                    "mouse_click" => SendMouseClick(window, @params, 1),
                    "mouse_double_click" => SendMouseClick(window, @params, 2),
                    "mouse_down" => SendMouseButton(window, @params, EventType.MouseDown),
                    "mouse_up" => SendMouseButton(window, @params, EventType.MouseUp),
                    "mouse_move" => SendMouseMove(window, @params),
                    "mouse_drag" => SendMouseDrag(window, @params),
                    "key_down" => SendKey(window, @params, EventType.KeyDown),
                    "key_up" => SendKey(window, @params, EventType.KeyUp),
                    "scroll" => SendScroll(window, @params),
                    "drag_asset" => SendDragAsset(window, @params),
                    _ => new ErrorResponse($"Unknown event_type: '{eventType}'."),
                };
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"send_event failed: {ex.Message}");
            }
        }

        private static object SendMouseClick(EditorWindow window, JObject @params, int clickCount)
        {
            float x = (float?)@params["x"] ?? 0;
            float y = (float?)@params["y"] ?? 0;
            int button = (int?)@params["button"] ?? 0; // 0=left, 1=right, 2=middle

            var pos = new Vector2(x, y);

            var downEvt = new Event
            {
                type = EventType.MouseDown,
                mousePosition = pos,
                button = button,
                clickCount = clickCount,
            };
            window.SendEvent(downEvt);

            var upEvt = new Event
            {
                type = EventType.MouseUp,
                mousePosition = pos,
                button = button,
                clickCount = clickCount,
            };
            window.SendEvent(upEvt);

            return new SuccessResponse($"Clicked at ({x}, {y}) in '{window.titleContent.text}'{(clickCount > 1 ? $" x{clickCount}" : "")}.", new
            {
                x, y, button, clickCount,
                windowTitle = window.titleContent.text,
            });
        }

        private static object SendMouseButton(EditorWindow window, JObject @params, EventType type)
        {
            float x = (float?)@params["x"] ?? 0;
            float y = (float?)@params["y"] ?? 0;
            int button = (int?)@params["button"] ?? 0;

            var evt = new Event
            {
                type = type,
                mousePosition = new Vector2(x, y),
                button = button,
            };
            window.SendEvent(evt);

            return new SuccessResponse($"Sent {type} at ({x}, {y}).", new { x, y, button });
        }

        private static object SendMouseMove(EditorWindow window, JObject @params)
        {
            float x = (float?)@params["x"] ?? 0;
            float y = (float?)@params["y"] ?? 0;

            var evt = new Event
            {
                type = EventType.MouseMove,
                mousePosition = new Vector2(x, y),
            };
            window.SendEvent(evt);

            return new SuccessResponse($"Mouse moved to ({x}, {y}).", new { x, y });
        }

        private static object SendMouseDrag(EditorWindow window, JObject @params)
        {
            float fromX = (float?)@params["from_x"] ?? (float?)@params["fromX"] ?? 0;
            float fromY = (float?)@params["from_y"] ?? (float?)@params["fromY"] ?? 0;
            float toX = (float?)@params["to_x"] ?? (float?)@params["toX"] ?? 0;
            float toY = (float?)@params["to_y"] ?? (float?)@params["toY"] ?? 0;
            int steps = (int?)@params["steps"] ?? 10;
            int button = (int?)@params["button"] ?? 0;

            // Mouse down at start
            window.SendEvent(new Event
            {
                type = EventType.MouseDown,
                mousePosition = new Vector2(fromX, fromY),
                button = button,
            });

            // Interpolated drag steps
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                float cx = Mathf.Lerp(fromX, toX, t);
                float cy = Mathf.Lerp(fromY, toY, t);

                window.SendEvent(new Event
                {
                    type = EventType.MouseDrag,
                    mousePosition = new Vector2(cx, cy),
                    delta = new Vector2((toX - fromX) / steps, (toY - fromY) / steps),
                    button = button,
                });
            }

            // Mouse up at end
            window.SendEvent(new Event
            {
                type = EventType.MouseUp,
                mousePosition = new Vector2(toX, toY),
                button = button,
            });

            return new SuccessResponse($"Dragged from ({fromX}, {fromY}) to ({toX}, {toY}).", new
            {
                fromX, fromY, toX, toY, steps,
            });
        }

        private static object SendKey(EditorWindow window, JObject @params, EventType type)
        {
            string keyName = ParamCoercion.CoerceString(@params["key"], null);
            if (string.IsNullOrEmpty(keyName))
                return new ErrorResponse("'key' parameter is required.");

            var (keyCode, modifiers) = ParseKeyName(keyName);
            char character = '\0';
            if (keyName.Length == 1) character = keyName[0];

            var evt = new Event
            {
                type = type,
                keyCode = keyCode,
                character = character,
                modifiers = modifiers,
            };
            window.SendEvent(evt);

            return new SuccessResponse($"Sent {type} '{keyName}' to '{window.titleContent.text}'.", new
            {
                key = keyName, eventType = type.ToString(),
            });
        }

        private static object SendScroll(EditorWindow window, JObject @params)
        {
            float x = (float?)@params["x"] ?? 0;
            float y = (float?)@params["y"] ?? 0;
            float deltaX = (float?)@params["delta_x"] ?? (float?)@params["deltaX"] ?? 0;
            float deltaY = (float?)@params["delta_y"] ?? (float?)@params["deltaY"] ?? -3; // default scroll down

            var evt = new Event
            {
                type = EventType.ScrollWheel,
                mousePosition = new Vector2(x, y),
                delta = new Vector2(deltaX, deltaY),
            };
            window.SendEvent(evt);

            return new SuccessResponse($"Scrolled at ({x}, {y}) delta=({deltaX}, {deltaY}).", new
            {
                x, y, deltaX, deltaY,
            });
        }

        private static object SendDragAsset(EditorWindow window, JObject @params)
        {
            string assetPath = ParamCoercion.CoerceString(@params["asset_path"] ?? @params["assetPath"], null);
            float x = (float?)@params["x"] ?? 0;
            float y = (float?)@params["y"] ?? 0;

            if (string.IsNullOrEmpty(assetPath))
                return new ErrorResponse("'asset_path' is required for drag_asset event.");

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
                return new ErrorResponse($"Asset not found: '{assetPath}'.");

            var pos = new Vector2(x, y);

            // Prepare drag
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new[] { asset };
            DragAndDrop.paths = new[] { assetPath };
            DragAndDrop.StartDrag($"Drag {asset.name}");

            // Send DragUpdated to the window at the target position
            window.SendEvent(new Event
            {
                type = EventType.DragUpdated,
                mousePosition = pos,
            });

            // Accept the drag
            DragAndDrop.visualMode = DragAndDropVisualMode.Link;

            window.SendEvent(new Event
            {
                type = EventType.DragPerform,
                mousePosition = pos,
            });

            DragAndDrop.AcceptDrag();

            // Clean up drag state
            window.SendEvent(new Event
            {
                type = EventType.DragExited,
            });

            return new SuccessResponse($"Dragged asset '{asset.name}' to ({x}, {y}) in '{window.titleContent.text}'.", new
            {
                assetPath,
                assetName = asset.name,
                x, y,
                windowTitle = window.titleContent.text,
            });
        }

        // ─────────────────────────────────────────────
        // Helper: Find EditorWindow by title or type
        // ─────────────────────────────────────────────

        private static EditorWindow FindEditorWindow(string windowTitle, string windowType)
        {
            var allWindows = UnityEngine.Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var window in allWindows)
            {
                if (window == null) continue;
                try
                {
                    if (!string.IsNullOrEmpty(windowTitle) &&
                        window.titleContent.text.IndexOf(windowTitle, StringComparison.OrdinalIgnoreCase) >= 0)
                        return window;
                    if (!string.IsNullOrEmpty(windowType) &&
                        window.GetType().Name.IndexOf(windowType, StringComparison.OrdinalIgnoreCase) >= 0)
                        return window;
                }
                catch { continue; }
            }
            return null;
        }

        // ─────────────────────────────────────────────
        // Action: focus_window
        // ─────────────────────────────────────────────

        private static object FocusWindow(JObject @params)
        {
            string windowTitle = ParamCoercion.CoerceString(@params["window_title"] ?? @params["windowTitle"], null);
            string windowType = ParamCoercion.CoerceString(@params["window_type"] ?? @params["windowType"], null);

            if (string.IsNullOrEmpty(windowTitle) && string.IsNullOrEmpty(windowType))
                return new ErrorResponse("Either 'window_title' or 'window_type' is required.");

            var target = FindEditorWindow(windowTitle, windowType);
            if (target == null)
                return new ErrorResponse($"Window not found: '{windowTitle ?? windowType}'.");

            target.Focus();
            target.Repaint();

            return new SuccessResponse($"Focused window '{target.titleContent.text}'.", new
            {
                title = target.titleContent.text,
                typeName = target.GetType().FullName,
            });
        }

        // ─────────────────────────────────────────────
        // Helper: Focus the window that owns an element
        // ─────────────────────────────────────────────

        private static void FocusOwningWindow(VisualElement element)
        {
            var allWindows = UnityEngine.Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var window in allWindows)
            {
                if (window == null) continue;
                try
                {
                    var root = window.rootVisualElement;
                    if (root != null && IsDescendantOf(element, root))
                    {
                        window.Focus();
                        return;
                    }
                }
                catch { }
            }
        }

        private static bool IsDescendantOf(VisualElement child, VisualElement root)
        {
            var current = child;
            while (current != null)
            {
                if (current == root) return true;
                current = current.parent;
            }
            return false;
        }

        // ─────────────────────────────────────────────
        // Helper: Dispatch pointer click events
        // ─────────────────────────────────────────────

        private static void DispatchPointerClick(VisualElement element, Vector2 position, int clickCount)
        {
            using (var moveEvt = PointerMoveEvent.GetPooled())
            {
                moveEvt.target = element;
                element.SendEvent(moveEvt);
            }

            using (var downEvt = PointerDownEvent.GetPooled())
            {
                downEvt.target = element;
                element.SendEvent(downEvt);
            }

            using (var upEvt = PointerUpEvent.GetPooled())
            {
                upEvt.target = element;
                element.SendEvent(upEvt);
            }

            using (var clickEvt = ClickEvent.GetPooled())
            {
                clickEvt.target = element;
                element.SendEvent(clickEvt);
            }
        }

        // ─────────────────────────────────────────────
        // Helper: Dispatch drag events
        // ─────────────────────────────────────────────

        private static void DispatchDragEvents(VisualElement target, Vector2 position)
        {
            using (var enterEvt = DragEnterEvent.GetPooled())
            {
                enterEvt.target = target;
                target.SendEvent(enterEvt);
            }

            using (var updateEvt = DragUpdatedEvent.GetPooled())
            {
                updateEvt.target = target;
                target.SendEvent(updateEvt);
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Link;

            using (var performEvt = DragPerformEvent.GetPooled())
            {
                performEvt.target = target;
                target.SendEvent(performEvt);
            }

            DragAndDrop.AcceptDrag();
        }

        // ─────────────────────────────────────────────
        // Helper: Pointer-based drag (element to element)
        // ─────────────────────────────────────────────

        private static void DispatchPointerDrag(
            VisualElement source, VisualElement target,
            Vector2 from, Vector2 to, int steps)
        {
            // Press at source
            using (var downEvt = PointerDownEvent.GetPooled())
            {
                downEvt.target = source;
                source.SendEvent(downEvt);
            }

            // Interpolated moves
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                var pos = Vector2.Lerp(from, to, t);
                var currentTarget = i <= steps / 2 ? source : target;

                using (var moveEvt = PointerMoveEvent.GetPooled())
                {
                    moveEvt.target = currentTarget;
                    currentTarget.SendEvent(moveEvt);
                }
            }

            // Release at target
            using (var upEvt = PointerUpEvent.GetPooled())
            {
                upEvt.target = target;
                target.SendEvent(upEvt);
            }
        }

        // ─────────────────────────────────────────────
        // Helper: Try to set field value directly
        // ─────────────────────────────────────────────

        private static bool TrySetFieldValue(VisualElement element, string text, bool clear, out string error)
        {
            error = null;

            if (element is TextField textField)
            {
                textField.value = clear ? text : textField.value + text;
                return true;
            }

            // Try float fields
            if (element is BaseField<float> floatField && float.TryParse(text, out float fVal))
            {
                floatField.value = fVal;
                return true;
            }

            if (element is BaseField<int> intField && int.TryParse(text, out int iVal))
            {
                intField.value = iVal;
                return true;
            }

            if (element is BaseField<double> doubleField && double.TryParse(text, out double dVal))
            {
                doubleField.value = dVal;
                return true;
            }

            if (element is BaseField<long> longField && long.TryParse(text, out long lVal))
            {
                longField.value = lVal;
                return true;
            }

            if (element is BaseField<string> stringField)
            {
                stringField.value = clear ? text : stringField.value + text;
                return true;
            }

            return false;
        }

        // ─────────────────────────────────────────────
        // Helper: Send keyboard events
        // ─────────────────────────────────────────────

        private static void SendKeyboardEvent(VisualElement element, KeyCode keyCode, EventModifiers modifiers)
        {
            using (var downEvt = KeyDownEvent.GetPooled('\0', keyCode, modifiers))
            {
                downEvt.target = element;
                element.SendEvent(downEvt);
            }

            using (var upEvt = KeyUpEvent.GetPooled('\0', keyCode, modifiers))
            {
                upEvt.target = element;
                element.SendEvent(upEvt);
            }
        }

        private static void SendCharacterEvent(VisualElement element, char c)
        {
            using (var downEvt = KeyDownEvent.GetPooled(c, KeyCode.None, EventModifiers.None))
            {
                downEvt.target = element;
                element.SendEvent(downEvt);
            }

            using (var upEvt = KeyUpEvent.GetPooled(c, KeyCode.None, EventModifiers.None))
            {
                upEvt.target = element;
                element.SendEvent(upEvt);
            }
        }

        private static (KeyCode keyCode, EventModifiers modifiers) ParseKeyName(string keyName)
        {
            var modifiers = EventModifiers.None;
            var parts = keyName.Split('+');
            var keyPart = parts[parts.Length - 1].Trim().ToLower();

            for (int i = 0; i < parts.Length - 1; i++)
            {
                var mod = parts[i].Trim().ToLower();
                if (mod == "ctrl" || mod == "control") modifiers |= EventModifiers.Control;
                else if (mod == "shift") modifiers |= EventModifiers.Shift;
                else if (mod == "alt") modifiers |= EventModifiers.Alt;
                else if (mod == "cmd" || mod == "command") modifiers |= EventModifiers.Command;
            }

            var keyCode = keyPart switch
            {
                "enter" or "return" => KeyCode.Return,
                "tab" => KeyCode.Tab,
                "escape" or "esc" => KeyCode.Escape,
                "backspace" => KeyCode.Backspace,
                "delete" or "del" => KeyCode.Delete,
                "space" => KeyCode.Space,
                "up" => KeyCode.UpArrow,
                "down" => KeyCode.DownArrow,
                "left" => KeyCode.LeftArrow,
                "right" => KeyCode.RightArrow,
                "home" => KeyCode.Home,
                "end" => KeyCode.End,
                "pageup" => KeyCode.PageUp,
                "pagedown" => KeyCode.PageDown,
                "a" => KeyCode.A,
                "c" => KeyCode.C,
                "v" => KeyCode.V,
                "x" => KeyCode.X,
                "z" => KeyCode.Z,
                "s" => KeyCode.S,
                "f" => KeyCode.F,
                _ => KeyCode.None,
            };

            return (keyCode, modifiers);
        }

        // ─────────────────────────────────────────────
        // Helper: Try to get a GameObject from a hierarchy label
        // ─────────────────────────────────────────────

        private static GameObject GetGameObjectFromElement(VisualElement element)
        {
            // Try to get the label text and find a matching GameObject
            string name = null;
            if (element is Label label) name = label.text;
            else if (!string.IsNullOrEmpty(element.name)) name = element.name;

            if (string.IsNullOrEmpty(name)) return null;

            // Search in loaded scenes
            var go = GameObject.Find(name);
            if (go != null) return go;

            // Search all root objects
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var found = FindChildByName(root, name);
                    if (found != null) return found;
                }
            }

            return null;
        }

        private static GameObject FindChildByName(GameObject parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent.transform)
            {
                var found = FindChildByName(child.gameObject, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
