using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Helpers
{
    /// <summary>
    /// Maintains a mapping of semantic references (@e1, @e2, ...) to Unity Editor VisualElements.
    /// Inspired by agent-browser's RefMap pattern for browser automation.
    /// </summary>
    public static class EditorUIRefMap
    {
        public class RefEntry
        {
            public WeakReference<VisualElement> ElementRef;
            public string Role;
            public string Name;
            public int? Nth;
            public string WindowTitle;
            public string ElementPath;
            public string Value;
            public bool IsInteractive;
        }

        private static readonly Dictionary<string, RefEntry> _refs = new Dictionary<string, RefEntry>();
        private static int _nextRefId = 1;
        private static int _version = 0;

        // Interactive element types that always get refs
        private static readonly HashSet<Type> InteractiveTypes = new HashSet<Type>
        {
            typeof(Button),
            typeof(Toggle),
            typeof(TextField),
            typeof(Slider),
            typeof(SliderInt),
            typeof(MinMaxSlider),
            typeof(Foldout),
            typeof(DropdownField),
            typeof(RadioButton),
            typeof(RadioButtonGroup),
        };

        // Additional interactive type names (for subclasses like FloatField, IntegerField, etc.)
        private static readonly HashSet<string> InteractiveTypeNames = new HashSet<string>
        {
            "FloatField", "IntegerField", "LongField", "DoubleField",
            "Vector2Field", "Vector3Field", "Vector4Field",
            "RectField", "BoundsField",
            "ColorField", "GradientField", "CurveField",
            "ObjectField", "EnumField", "EnumFlagsField",
            "TagField", "LayerField", "LayerMaskField",
            "MaskField", "PropertyField",
            "ToolbarButton", "ToolbarToggle", "ToolbarMenu",
            "TreeView", "ListView", "ScrollView",
        };

        // Content types that get refs only if they have meaningful text
        private static readonly HashSet<Type> ContentTypes = new HashSet<Type>
        {
            typeof(Label),
            typeof(HelpBox),
            typeof(Image),
        };

        [InitializeOnLoadMethod]
        private static void OnDomainReload()
        {
            Clear();
        }

        public static void Clear()
        {
            _refs.Clear();
            _nextRefId = 1;
            _version++;
        }

        public static int Version => _version;
        public static int Count => _refs.Count;

        /// <summary>
        /// Resolve a ref like "@e3" to a VisualElement, with stale recovery.
        /// </summary>
        public static VisualElement Resolve(string refId, out string error)
        {
            error = null;
            string key = NormalizeRefId(refId);

            if (!_refs.TryGetValue(key, out var entry))
            {
                error = $"Reference '{refId}' not found. Run snapshot first to populate element references.";
                return null;
            }

            // Fast path: WeakReference still alive and element still in a panel
            if (entry.ElementRef.TryGetTarget(out var element) && element.panel != null)
                return element;

            // Fallback: re-search by (WindowTitle, Role, Name, Nth)
            var recovered = RecoverElement(entry);
            if (recovered != null)
            {
                entry.ElementRef = new WeakReference<VisualElement>(recovered);
                return recovered;
            }

            error = $"Reference '{refId}' is stale (element no longer exists). Run snapshot again.";
            return null;
        }

        /// <summary>
        /// Get the center coordinates of a referenced element in window-local space.
        /// </summary>
        public static bool GetElementCenter(string refId, out float x, out float y, out string error)
        {
            x = y = 0;
            var element = Resolve(refId, out error);
            if (element == null)
                return false;

            var rect = element.worldBound;
            if (rect.width <= 0 || rect.height <= 0)
            {
                error = $"Element '{refId}' has zero size (may be collapsed or hidden).";
                return false;
            }

            x = rect.center.x;
            y = rect.center.y;
            return true;
        }

        /// <summary>
        /// Take a snapshot of all open EditorWindows and build the RefMap + text tree.
        /// </summary>
        public static string TakeSnapshot(string windowFilter = null, int maxDepth = 15, bool includeStyles = false)
        {
            Clear();

            var sb = new StringBuilder();
            var allWindows = UnityEngine.Resources.FindObjectsOfTypeAll<EditorWindow>();
            var focusedWindow = EditorWindow.focusedWindow;

            // Track (role, name) duplicates for Nth assignment
            var roleNameCounts = new Dictionary<string, int>();

            foreach (var window in allWindows)
            {
                if (window == null) continue;

                string title;
                try { title = window.titleContent.text; }
                catch { continue; }

                if (!string.IsNullOrEmpty(windowFilter) &&
                    title.IndexOf(windowFilter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    window.GetType().Name.IndexOf(windowFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                bool isFocused = window == focusedWindow;
                var pos = window.position;

                sb.AppendLine($"[Window: {title}]{(isFocused ? " (focused)" : "")} (x={pos.x:F0} y={pos.y:F0} w={pos.width:F0} h={pos.height:F0})");

                VisualElement root = null;
                try { root = window.rootVisualElement; }
                catch
                {
                    sb.AppendLine("  [IMGUI - not automatable]");
                    continue;
                }

                if (root == null || root.childCount == 0)
                {
                    sb.AppendLine("  [IMGUI - not automatable]");
                    continue;
                }

                TraverseElement(root, sb, title, roleNameCounts, depth: 1, maxDepth: maxDepth, includeStyles: includeStyles);
            }

            _version++;
            return sb.ToString();
        }

        private static void TraverseElement(
            VisualElement element,
            StringBuilder sb,
            string windowTitle,
            Dictionary<string, int> roleNameCounts,
            int depth,
            int maxDepth,
            bool includeStyles)
        {
            if (depth > maxDepth) return;

            string indent = new string(' ', depth * 2);
            string role = GetRole(element);
            string name = GetDisplayName(element);
            string value = GetValue(element);
            bool isInteractive = IsInteractive(element);
            bool isContent = IsContent(element);
            bool shouldRef = isInteractive || (isContent && !string.IsNullOrEmpty(name));

            if (shouldRef)
            {
                // Track duplicates for Nth
                string key = $"{windowTitle}/{role}/{name}";
                if (!roleNameCounts.ContainsKey(key))
                    roleNameCounts[key] = 0;
                roleNameCounts[key]++;
                int? nth = roleNameCounts[key] > 1 ? roleNameCounts[key] : (int?)null;

                string refId = $"e{_nextRefId++}";
                _refs[refId] = new RefEntry
                {
                    ElementRef = new WeakReference<VisualElement>(element),
                    Role = role,
                    Name = name,
                    Nth = nth,
                    WindowTitle = windowTitle,
                    ElementPath = BuildElementPath(element, windowTitle),
                    Value = value,
                    IsInteractive = isInteractive,
                };

                // Build display line
                var line = new StringBuilder();
                line.Append($"{indent}@{refId} {role}");
                if (!string.IsNullOrEmpty(name))
                    line.Append($" \"{name}\"");
                if (!string.IsNullOrEmpty(value))
                    line.Append($" value=\"{value}\"");
                if (nth.HasValue)
                    line.Append($" (nth={nth.Value})");

                // State annotations
                AppendStateAnnotations(element, line);

                // Optional style info
                if (includeStyles)
                {
                    var rect = element.worldBound;
                    if (rect.width > 0 && rect.height > 0)
                        line.Append($" (w={rect.width:F0} h={rect.height:F0})");
                }

                sb.AppendLine(line.ToString());
            }

            // Recurse into children
            foreach (var child in element.Children())
            {
                TraverseElement(child, sb, windowTitle, roleNameCounts,
                    shouldRef ? depth + 1 : depth, maxDepth, includeStyles);
            }
        }

        private static string GetRole(VisualElement element)
        {
            var type = element.GetType();

            // Check exact type first
            if (type == typeof(Button)) return "Button";
            if (type == typeof(Toggle)) return "Toggle";
            if (type == typeof(TextField)) return "TextField";
            if (type == typeof(Label)) return "Label";
            if (type == typeof(Foldout)) return "Foldout";
            if (type == typeof(Slider)) return "Slider";
            if (type == typeof(SliderInt)) return "SliderInt";
            if (type == typeof(DropdownField)) return "DropdownField";
            if (type == typeof(RadioButton)) return "RadioButton";
            if (type == typeof(HelpBox)) return "HelpBox";
            if (type == typeof(Image)) return "Image";
            if (type == typeof(MinMaxSlider)) return "MinMaxSlider";
            if (type == typeof(ScrollView)) return "ScrollView";
            if (type == typeof(ListView)) return "ListView";
            if (type == typeof(TreeView)) return "TreeView";

            // Check type name for subclasses (FloatField inherits TextValueField<float>, etc.)
            string typeName = type.Name;
            if (InteractiveTypeNames.Contains(typeName))
                return typeName;

            // Walk up the hierarchy for known base types
            var baseType = type.BaseType;
            while (baseType != null && baseType != typeof(VisualElement))
            {
                if (InteractiveTypes.Contains(baseType))
                    return typeName; // Use the concrete type name
                string baseName = baseType.Name;
                if (baseName.Contains("Field") || baseName.Contains("Button"))
                    return typeName;
                baseType = baseType.BaseType;
            }

            return "VisualElement";
        }

        private static string GetDisplayName(VisualElement element)
        {
            // Try label property for fields
            if (element is BaseField<float> ff && !string.IsNullOrEmpty(ff.label)) return ff.label;
            if (element is BaseField<int> fi && !string.IsNullOrEmpty(fi.label)) return fi.label;
            if (element is BaseField<string> fs && !string.IsNullOrEmpty(fs.label)) return fs.label;
            if (element is BaseField<bool> fb && !string.IsNullOrEmpty(fb.label)) return fb.label;
            if (element is BaseField<Enum> fe && !string.IsNullOrEmpty(fe.label)) return fe.label;

            // Button text
            if (element is Button btn && !string.IsNullOrEmpty(btn.text)) return btn.text;

            // Toggle label
            if (element is Toggle toggle && !string.IsNullOrEmpty(toggle.label)) return toggle.label;

            // Foldout text
            if (element is Foldout foldout && !string.IsNullOrEmpty(foldout.text)) return foldout.text;

            // Label text
            if (element is Label label && !string.IsNullOrEmpty(label.text)) return label.text;

            // Fallback to element name
            if (!string.IsNullOrEmpty(element.name)) return element.name;

            // Try tooltip
            if (!string.IsNullOrEmpty(element.tooltip)) return element.tooltip;

            return "";
        }

        private static string GetValue(VisualElement element)
        {
            try
            {
                if (element is BaseField<float> ff) return ff.value.ToString("G");
                if (element is BaseField<int> fi) return fi.value.ToString();
                if (element is BaseField<string> fs) return fs.value ?? "";
                if (element is BaseField<bool> fb) return fb.value.ToString().ToLower();
                if (element is BaseField<long> fl) return fl.value.ToString();
                if (element is BaseField<double> fd) return fd.value.ToString("G");
                if (element is BaseField<Enum> fe) return fe.value?.ToString() ?? "";
                if (element is BaseField<Vector2> fv2) return fv2.value.ToString();
                if (element is BaseField<Vector3> fv3) return fv3.value.ToString();
                if (element is BaseField<Vector4> fv4) return fv4.value.ToString();
                if (element is BaseField<Color> fc) return fc.value.ToString();
                if (element is Toggle toggle) return toggle.value.ToString().ToLower();
                if (element is Foldout foldout) return foldout.value ? "expanded" : "collapsed";
            }
            catch { }

            return null;
        }

        private static bool IsInteractive(VisualElement element)
        {
            var type = element.GetType();
            if (InteractiveTypes.Contains(type)) return true;
            if (InteractiveTypeNames.Contains(type.Name)) return true;

            // Check base types
            var baseType = type.BaseType;
            while (baseType != null && baseType != typeof(VisualElement))
            {
                if (InteractiveTypes.Contains(baseType)) return true;
                if (baseType.Name.Contains("Field") || baseType.Name.Contains("Button"))
                    return true;
                baseType = baseType.BaseType;
            }

            return false;
        }

        private static bool IsContent(VisualElement element)
        {
            return ContentTypes.Contains(element.GetType());
        }

        private static void AppendStateAnnotations(VisualElement element, StringBuilder sb)
        {
            if (!element.enabledSelf)
                sb.Append(" (disabled)");
            if (element.resolvedStyle.display == DisplayStyle.None)
                sb.Append(" (hidden)");
            if (element.resolvedStyle.visibility == Visibility.Hidden)
                sb.Append(" (invisible)");
            if (element.focusable && element.panel?.focusController?.focusedElement == element)
                sb.Append(" (focused)");
            if (element is Foldout foldout)
                sb.Append(foldout.value ? " (expanded)" : " (collapsed)");
            if (element is Toggle toggle)
                sb.Append(toggle.value ? " (checked)" : " (unchecked)");
        }

        private static string BuildElementPath(VisualElement element, string windowTitle)
        {
            var parts = new List<string>();
            var current = element;
            int maxDepth = 10;
            while (current != null && maxDepth-- > 0)
            {
                string part = current.GetType().Name;
                if (!string.IsNullOrEmpty(current.name))
                    part += $"[{current.name}]";
                parts.Add(part);
                current = current.parent;
            }
            parts.Reverse();
            return $"{windowTitle}/{string.Join("/", parts)}";
        }

        private static VisualElement RecoverElement(RefEntry entry)
        {
            var allWindows = UnityEngine.Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var window in allWindows)
            {
                if (window == null) continue;
                string title;
                try { title = window.titleContent.text; }
                catch { continue; }

                if (title != entry.WindowTitle) continue;

                VisualElement root;
                try { root = window.rootVisualElement; }
                catch { continue; }

                if (root == null) continue;

                var found = FindByRoleAndName(root, entry.Role, entry.Name, entry.Nth ?? 1);
                if (found != null) return found;
            }
            return null;
        }

        private static VisualElement FindByRoleAndName(VisualElement root, string role, string name, int nth)
        {
            int count = 0;
            return FindRecursive(root, role, name, nth, ref count);
        }

        private static VisualElement FindRecursive(VisualElement element, string role, string name, int nth, ref int count)
        {
            if (GetRole(element) == role && GetDisplayName(element) == name)
            {
                count++;
                if (count >= nth) return element;
            }

            foreach (var child in element.Children())
            {
                var found = FindRecursive(child, role, name, nth, ref count);
                if (found != null) return found;
            }

            return null;
        }

        private static string NormalizeRefId(string refId)
        {
            if (string.IsNullOrEmpty(refId)) return "";
            refId = refId.Trim();
            if (refId.StartsWith("@")) refId = refId.Substring(1);
            if (refId.StartsWith("ref=")) refId = refId.Substring(4);
            return refId;
        }
    }
}
