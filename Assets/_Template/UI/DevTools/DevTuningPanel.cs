using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Template.UI.DevTools
{
    /// <summary>Optional live readout shown by <see cref="DevTuningPanel"/> (speed, state, ...).</summary>
    public interface IDevTelemetry
    {
        void WriteTelemetry(StringBuilder sb);
    }

    /// <summary>
    /// Dev-only live tuning (Editor and Development builds; destroys itself in release builds).
    /// Draws a slider for every serialized float/int/bool of each target ScriptableObject. [Range] sets the
    /// slider bounds and [Header] starts a section, so new config fields appear without touching this class.
    /// Values are written straight into the config instance, so gameplay reading the config sees them at once.
    ///   Editor: Save writes the asset. Unsaved changes are reverted when play mode ends.
    ///   Device: Save writes a JSON preset to persistentDataPath, which is auto-loaded on the next launch.
    ///           Revert restores the build's values and deletes the preset. Copy puts the JSON on the clipboard
    ///           (paste it into the asset with Tools/Template/Debug/Paste Tuning JSON Into Selected Config).
    /// IMGUI + reflection: allocates while drawn. Fine for dev builds, never used in release.
    /// </summary>
    public sealed class DevTuningPanel : MonoBehaviour
    {
#pragma warning disable 0414 // fields unused in release builds, where the panel removes itself
        [SerializeField] private ScriptableObject[] _targets = Array.Empty<ScriptableObject>();
        [Tooltip("Optional component implementing IDevTelemetry, shown as a live readout.")]
        [SerializeField] private MonoBehaviour _telemetrySource;
        [SerializeField] private bool _showTelemetry = true;
#pragma warning restore 0414

        /// <summary>True while the panel is open: gameplay should ignore touches that start a gesture.</summary>
        public static bool IsOpen { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private enum Kind { Float, Int, Bool }

        private sealed class Entry
        {
            public FieldInfo Field;
            public string Label;
            public string Header;
            public Kind Kind;
            public float Min;
            public float Max;
        }

        private sealed class Page
        {
            public ScriptableObject Target;
            public readonly List<Entry> Entries = new List<Entry>();
            public string Original;   // last saved values (Revert target in the Editor)
            public string Defaults;   // values the build shipped with (Revert target on device)
            public bool Dirty;
            public bool PresetLoaded;
        }

        private const float ReferenceHeight = 1600f;
        private readonly List<Page> _pages = new List<Page>();
        private readonly StringBuilder _telemetry = new StringBuilder(256);
        private IDevTelemetry _telemetryTarget;
        private int _pageIndex;
        private Vector2 _scroll;
        private string _status = "";
        private bool _stylesReady;
        private GUIStyle _label, _value, _header, _button, _slider, _thumb, _toggle, _box, _readout;

        private void Awake()
        {
            IsOpen = false;
            _telemetryTarget = _telemetrySource as IDevTelemetry;
            foreach (var target in _targets)
            {
                if (target == null) continue;
                var json = JsonUtility.ToJson(target);
                var page = new Page { Target = target, Original = json, Defaults = json };
                Collect(target.GetType(), target, page.Entries);
                _pages.Add(page);
#if !UNITY_EDITOR
                if (File.Exists(PresetPath(target)))
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(PresetPath(target)), target);
                    page.PresetLoaded = true;
                    Debug.Log($"[DevTuning] Loaded preset {PresetPath(target)}");
                }
#endif
            }
        }

        private void OnDestroy()
        {
            IsOpen = false;
#if UNITY_EDITOR
            // Play-mode experiments must not leak into committed configs unless saved.
            foreach (var page in _pages)
                if (page.Dirty && page.Target != null) JsonUtility.FromJsonOverwrite(page.Original, page.Target);
#endif
        }

        private static string PresetPath(ScriptableObject target) =>
            Path.Combine(Application.persistentDataPath, $"tuning_{target.name}.json");

        private static void Collect(Type type, ScriptableObject target, List<Entry> entries)
        {
            string header = null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var field in type.GetFields(flags))
            {
                var h = field.GetCustomAttribute<HeaderAttribute>();
                if (h != null) header = h.header;
                var serialized = field.IsPublic ? !field.IsNotSerialized : field.GetCustomAttribute<SerializeField>() != null;
                if (!serialized || field.GetCustomAttribute<HideInInspector>() != null) continue;

                Kind kind;
                if (field.FieldType == typeof(float)) kind = Kind.Float;
                else if (field.FieldType == typeof(int)) kind = Kind.Int;
                else if (field.FieldType == typeof(bool)) kind = Kind.Bool;
                else continue;

                var entry = new Entry { Field = field, Label = Nicify(field.Name), Header = header, Kind = kind };
                header = null;
                if (kind != Kind.Bool) SetBounds(entry, field, target);
                entries.Add(entry);
            }
        }

        private static void SetBounds(Entry entry, FieldInfo field, ScriptableObject target)
        {
            var range = field.GetCustomAttribute<RangeAttribute>();
            if (range != null)
            {
                entry.Min = range.min;
                entry.Max = range.max;
                return;
            }
            var value = Convert.ToSingle(field.GetValue(target));
            var span = Mathf.Max(1f, Mathf.Abs(value) * 4f);
            var min = field.GetCustomAttribute<MinAttribute>();
            entry.Min = min != null ? min.min : (value < 0f ? -span : 0f);
            entry.Max = Mathf.Max(entry.Min + 1f, span);
        }

        private static string Nicify(string name)
        {
            var sb = new StringBuilder(name.Length + 8);
            var start = name.StartsWith("_") ? 1 : 0;
            for (var i = start; i < name.Length; i++)
            {
                var c = name[i];
                if (i == start) { sb.Append(char.ToUpperInvariant(c)); continue; }
                if (char.IsUpper(c) && !char.IsUpper(name[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }

        private void OnGUI()
        {
            EnsureStyles();
            var scale = Mathf.Max(1f, Screen.height / ReferenceHeight);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var w = Screen.width / scale;
            var h = Screen.height / scale;
            var safe = Screen.safeArea;
            var top = (Screen.height - safe.yMax) / scale + 8f;   // IMGUI origin is top-left
            var bottom = safe.yMin / scale + 8f;

            if (_showTelemetry && _telemetryTarget != null)
            {
                _telemetry.Clear();
                _telemetryTarget.WriteTelemetry(_telemetry);
                GUI.Label(new Rect(12f, h - bottom - 130f, w - 24f, 130f), _telemetry.ToString(), _readout);
            }

            if (!IsOpen)
            {
                if (GUI.Button(new Rect(w - 132f, top + 90f, 120f, 64f), "Tune", _button)) IsOpen = true;
                return;
            }
            if (_pages.Count == 0)
            {
                IsOpen = false;
                return;
            }

            var panel = new Rect(8f, top, w - 16f, Mathf.Min(h * 0.62f, h - top - bottom - 140f));
            GUI.Box(panel, GUIContent.none, _box);
            GUILayout.BeginArea(new Rect(panel.x + 10f, panel.y + 10f, panel.width - 20f, panel.height - 20f));

            GUILayout.BeginHorizontal();
            for (var i = 0; i < _pages.Count; i++)
            {
                var p = _pages[i];
                var title = p.Target.name + (p.Dirty ? "*" : "") + (p.PresetLoaded ? " (preset)" : "");
                if (GUILayout.Toggle(i == _pageIndex, title, _button, GUILayout.Height(60f)) && i != _pageIndex)
                {
                    _pageIndex = i;
                    _scroll = Vector2.zero;
                }
            }
            GUILayout.EndHorizontal();

            var page = _pages[Mathf.Clamp(_pageIndex, 0, _pages.Count - 1)];
            DragScroll();
            _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar);
            foreach (var entry in page.Entries) DrawEntry(page, entry);
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", _button, GUILayout.Height(64f))) Save(page);
            if (GUILayout.Button("Revert", _button, GUILayout.Height(64f))) Revert(page);
            if (GUILayout.Button("Copy", _button, GUILayout.Height(64f)))
            {
                GUIUtility.systemCopyBuffer = JsonUtility.ToJson(page.Target, true);
                _status = $"{page.Target.name} JSON copied";
            }
            _showTelemetry = GUILayout.Toggle(_showTelemetry, "Info", _button, GUILayout.Height(64f));
            if (GUILayout.Button("Close", _button, GUILayout.Height(64f))) IsOpen = false;
            GUILayout.EndHorizontal();
            GUILayout.Label(_status, _label);
            GUILayout.EndArea();
        }

        private void DrawEntry(Page page, Entry entry)
        {
            if (entry.Header != null) GUILayout.Label(entry.Header, _header);
            var target = page.Target;
            switch (entry.Kind)
            {
                case Kind.Bool:
                {
                    var v = (bool)entry.Field.GetValue(target);
                    var nv = GUILayout.Toggle(v, " " + entry.Label, _toggle, GUILayout.Height(52f));
                    if (nv != v) Write(page, entry, nv);
                    break;
                }
                case Kind.Int:
                {
                    var v = (int)entry.Field.GetValue(target);
                    Row(entry.Label, v.ToString());
                    var nv = Mathf.RoundToInt(GUILayout.HorizontalSlider(v, entry.Min, entry.Max, _slider, _thumb));
                    if (nv != v) Write(page, entry, nv);
                    break;
                }
                default:
                {
                    var v = (float)entry.Field.GetValue(target);
                    Row(entry.Label, v.ToString("0.###"));
                    var nv = GUILayout.HorizontalSlider(v, entry.Min, entry.Max, _slider, _thumb);
                    if (!Mathf.Approximately(nv, v)) Write(page, entry, nv);
                    break;
                }
            }
            GUILayout.Space(10f);
        }

        private void Row(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _label);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value, _value);
            GUILayout.EndHorizontal();
        }

        private static void Write(Page page, Entry entry, object value)
        {
            entry.Field.SetValue(page.Target, value);
            page.Dirty = true;
        }

        /// <summary>Touch-friendly scrolling: drag on empty panel space scrolls (sliders keep their own drags).</summary>
        private void DragScroll()
        {
            var e = Event.current;
            if (e.type == EventType.MouseDrag && GUIUtility.hotControl == 0)
            {
                _scroll.y -= e.delta.y;
                e.Use();
            }
        }

        private void Save(Page page)
        {
            page.Original = JsonUtility.ToJson(page.Target);
            page.Dirty = false;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(page.Target);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(page.Target);
            _status = $"Saved {page.Target.name}.asset";
#else
            File.WriteAllText(PresetPath(page.Target), JsonUtility.ToJson(page.Target, true));
            page.PresetLoaded = true;
            _status = $"Saved preset {page.Target.name} (auto-loads next launch)";
#endif
        }

        private void Revert(Page page)
        {
#if UNITY_EDITOR
            JsonUtility.FromJsonOverwrite(page.Original, page.Target);
            _status = $"Reverted {page.Target.name} to last save";
#else
            if (File.Exists(PresetPath(page.Target))) File.Delete(PresetPath(page.Target));
            JsonUtility.FromJsonOverwrite(page.Defaults, page.Target);
            page.Original = page.Defaults;
            page.PresetLoaded = false;
            _status = $"Reverted {page.Target.name}, preset deleted";
#endif
            page.Dirty = false;
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;
            var bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.82f));
            bg.Apply();
            _box = new GUIStyle(GUI.skin.box) { normal = { background = bg } };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 28, wordWrap = false };
            _value = new GUIStyle(_label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(1f, 0.85f, 0.3f) } };
            _header = new GUIStyle(_label) { fontSize = 30, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.45f, 0.8f, 1f) } };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 28 };
            _toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 28 };
            _slider = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 36f };
            _thumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = 48f, fixedHeight = 48f };
            _readout = new GUIStyle(_label) { fontSize = 26, alignment = TextAnchor.LowerLeft, wordWrap = true };
        }
#else
        private void Awake() => Destroy(this);
#endif
    }
}
