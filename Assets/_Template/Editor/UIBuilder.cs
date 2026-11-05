using Template.UI;
using Template.UI.Screens;
using Template.UI.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Template.Editor
{
    /// <summary>
    /// Builds one prefab per screen + UIRoot.prefab (Canvas, scaler for portrait, EventSystem with the
    /// Input System UI module, ScreenManager). Existing prefabs are kept unless force = true,
    /// so per-game restyling survives regeneration.
    /// </summary>
    public static class UIBuilder
    {
        public const string PrefabFolder = "Assets/_Game/Prefabs/UI";
        public const string ScreensFolder = PrefabFolder + "/Screens";
        public const string RootPath = PrefabFolder + "/UIRoot.prefab";
        private static readonly Vector2 Reference = new Vector2(1080, 1920);

        public const string StarSpritePath = PrefabFolder + "/Star.png";

        // Flat palette (restyle per game). Public so game screens built by game Editor scripts match.
        public static readonly Color Background = new Color(0.11f, 0.12f, 0.17f, 1f);
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.7f);
        public static readonly Color Panel = new Color(0.17f, 0.19f, 0.26f, 1f);
        public static readonly Color Primary = new Color(0.25f, 0.78f, 0.55f, 1f);
        public static readonly Color Secondary = new Color(0.3f, 0.34f, 0.44f, 1f);
        public static readonly Color Accent = new Color(1f, 0.82f, 0.25f, 1f);
        public static readonly Color TextColor = Color.white;

        private static Sprite _buttonSprite;

        public static Sprite ButtonSprite => _buttonSprite != null
            ? _buttonSprite
            : _buttonSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        public static ScreenManager BuildAll(bool force)
        {
            EditorUtil.EnsureFolder(ScreensFolder);
            _buttonSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            StarSprite();

            var screens = new GameObject[]
            {
                Screen<LoadingScreen>(force, BuildLoading),
                Screen<MenuScreen>(force, BuildMenu),
                Screen<HUDScreen>(force, BuildHud),
                Screen<GameOverScreen>(force, BuildGameOver),
                Screen<PauseScreen>(force, BuildPause),
                Screen<SettingsScreen>(force, BuildSettings),
                Screen<LevelCompleteScreen>(force, BuildLevelComplete),
            };

            var existing = AssetDatabase.LoadAssetAtPath<ScreenManager>(RootPath);
            if (existing != null && !force)
            {
                foreach (var screenPrefab in screens) EnsureInRoot(screenPrefab); // screens added to the template later
                return existing;
            }

            var root = new GameObject("UIRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ScreenManager));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            foreach (var screenPrefab in screens)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(screenPrefab, root.transform);
                Stretch((RectTransform)instance.transform);
            }

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(root.transform, false);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, RootPath);
            Object.DestroyImmediate(root);
            Log.Info($"Saved {RootPath}");
            return prefab.GetComponent<ScreenManager>();
        }

        /// <summary>Adds an instance of screenPrefab to UIRoot.prefab if it is not there yet (never removes anything).</summary>
        public static void EnsureInRoot(GameObject screenPrefab)
        {
            var root = PrefabUtility.LoadPrefabContents(RootPath);
            try
            {
                foreach (Transform child in root.transform)
                    if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == screenPrefab) return;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(screenPrefab, root.transform);
                Stretch((RectTransform)instance.transform);
                // Keep the EventSystem last; screens before it in creation order.
                var es = root.GetComponentInChildren<EventSystem>();
                if (es != null) es.transform.SetAsLastSibling();
                PrefabUtility.SaveAsPrefabAsset(root, RootPath);
                Log.Info($"Added {screenPrefab.name} to {RootPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Soft five-point star (white, tint with Image.color), generated once.</summary>
        public static Sprite StarSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(StarSpritePath);
            if (existing != null) return existing;
            if (AssetImporter.GetAtPath(StarSpritePath) is TextureImporter wrong)
            {
                // Texture exists but was imported without a single sprite: fix the import settings.
                ConfigureSpriteImport(wrong);
                return AssetDatabase.LoadAssetAtPath<Sprite>(StarSpritePath);
            }
            EditorUtil.EnsureFolder(PrefabFolder);
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var points = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                var a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                var r = (i % 2 == 0 ? 0.48f : 0.2f) * size;
                points[i] = new Vector2(size * 0.5f + Mathf.Cos(a) * r, size * 0.5f + Mathf.Sin(a) * r);
            }
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                // 4x4 supersampling for smooth edges.
                var cover = 0f;
                for (var sy = 0; sy < 4; sy++)
                for (var sx = 0; sx < 4; sx++)
                    if (InsidePolygon(points, new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f))) cover += 1f / 16f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, cover));
            }
            System.IO.File.WriteAllBytes(StarSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(StarSpritePath);
            ConfigureSpriteImport((TextureImporter)AssetImporter.GetAtPath(StarSpritePath));
            return AssetDatabase.LoadAssetAtPath<Sprite>(StarSpritePath);
        }

        private static void ConfigureSpriteImport(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single; // one sprite, loadable by path
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static bool InsidePolygon(Vector2[] poly, Vector2 p)
        {
            var inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        // ---------- Screens ----------
        /// <summary>Builds ScreensFolder/T.prefab (Canvas + CanvasGroup + raycaster + safe-area Content) unless it exists and !force.</summary>
        public static GameObject Screen<T>(bool force, System.Action<T, RectTransform> build) where T : UIScreen
        {
            var path = $"{ScreensFolder}/{typeof(T).Name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !force) return existing;

            var go = new GameObject(typeof(T).Name, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster), typeof(T));
            Stretch((RectTransform)go.transform);
            var content = new GameObject("Content", typeof(RectTransform), typeof(SafeArea));
            content.transform.SetParent(go.transform, false);
            Stretch((RectTransform)content.transform);

            build(go.GetComponent<T>(), (RectTransform)content.transform);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void BuildLoading(LoadingScreen s, RectTransform c)
        {
            FullscreenImage(s.transform, Background);
            Text(c, "Title", PlayerSettings.productName.ToUpperInvariant(), 110, new Vector2(0.5f, 0.6f), new Vector2(1000, 200), TextColor).gameObject.AddComponent<ProductNameLabel>();
            var barBg = Image(c, "ProgressBar", new Vector2(0.5f, 0.42f), new Vector2(600, 24), Secondary);
            var fill = Image(barBg, "Fill", Vector2.zero, Vector2.zero, Primary);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var status = Text(c, "Status", "Loading...", 44, new Vector2(0.5f, 0.37f), new Vector2(800, 80), new Color(1, 1, 1, 0.6f));
            EditorUtil.Set(s, "_status", status);
            EditorUtil.Set(s, "_progressFill", fill);
        }

        private static void BuildMenu(MenuScreen s, RectTransform c)
        {
            FullscreenImage(s.transform, new Color(0, 0, 0, 0)); // world (Menu scene) shows through
            Text(c, "Title", PlayerSettings.productName.ToUpperInvariant(), 120, new Vector2(0.5f, 0.78f), new Vector2(1000, 220), TextColor).gameObject.AddComponent<ProductNameLabel>();
            var best = Text(c, "Best", "BEST 0", 60, new Vector2(0.5f, 0.66f), new Vector2(800, 100), Accent);
            var play = Button(c, "PlayButton", "PLAY", new Vector2(0.5f, 0.3f), new Vector2(620, 180), Primary, 84, out _);
            var settings = Button(c, "SettingsButton", "SETTINGS", new Vector2(0.5f, 0.17f), new Vector2(460, 120), Secondary, 50, out _);
            EditorUtil.Set(s, "_playButton", play);
            EditorUtil.Set(s, "_settingsButton", settings);
            EditorUtil.Set(s, "_bestScoreText", best);
        }

        private static void BuildHud(HUDScreen s, RectTransform c)
        {
            var score = Text(c, "Score", "0", 150, new Vector2(0.5f, 1f), new Vector2(600, 220), TextColor);
            score.rectTransform.anchoredPosition = new Vector2(0, -150);
            var pause = Button(c, "PauseButton", "II", new Vector2(1f, 1f), new Vector2(140, 140), Secondary, 64, out _);
            ((RectTransform)pause.transform).anchoredPosition = new Vector2(-100, -100);
            EditorUtil.Set(s, "_scoreText", score);
            EditorUtil.Set(s, "_pauseButton", pause);
        }

        private static void BuildGameOver(GameOverScreen s, RectTransform c)
        {
            FullscreenImage(s.transform, new Color(Background.r, Background.g, Background.b, 0.92f));
            Text(c, "Title", "GAME OVER", 110, new Vector2(0.5f, 0.82f), new Vector2(1000, 180), TextColor);
            var score = Text(c, "Score", "0", 200, new Vector2(0.5f, 0.68f), new Vector2(800, 260), TextColor);
            var best = Text(c, "Best", "BEST 0", 56, new Vector2(0.5f, 0.575f), new Vector2(800, 90), new Color(1, 1, 1, 0.7f));
            var newBest = Text(c, "NewBest", "NEW BEST!", 64, new Vector2(0.5f, 0.52f), new Vector2(800, 100), Accent);
            var cont = Button(c, "ContinueButton", "CONTINUE (AD)", new Vector2(0.5f, 0.38f), new Vector2(760, 180), Primary, 66, out _);
            var retry = Button(c, "RetryButton", "RETRY", new Vector2(0.5f, 0.25f), new Vector2(620, 160), Secondary, 66, out _);
            var menu = Button(c, "MenuButton", "MENU", new Vector2(0.5f, 0.14f), new Vector2(420, 120), Secondary, 48, out _);
            EditorUtil.Set(s, "_scoreText", score);
            EditorUtil.Set(s, "_bestText", best);
            EditorUtil.Set(s, "_newBestLabel", newBest.gameObject);
            EditorUtil.Set(s, "_continueButton", cont);
            EditorUtil.Set(s, "_retryButton", retry);
            EditorUtil.Set(s, "_menuButton", menu);
        }

        private static void BuildLevelComplete(LevelCompleteScreen s, RectTransform c)
        {
            FullscreenImage(s.transform, new Color(Background.r, Background.g, Background.b, 0.9f));
            Text(c, "Title", "LEVEL COMPLETE!", 100, new Vector2(0.5f, 0.82f), new Vector2(1000, 180), TextColor);
            var stars = new Image[3];
            for (var i = 0; i < 3; i++)
            {
                var star = Image(c, $"Star{i + 1}", new Vector2(0.5f, 0.66f), i == 1 ? new Vector2(260, 260) : new Vector2(200, 200), Color.white);
                star.anchoredPosition = new Vector2((i - 1) * 250f, i == 1 ? 30f : 0f);
                stars[i] = star.GetComponent<Image>();
                stars[i].sprite = StarSprite();
                stars[i].preserveAspect = true;
            }
            var score = Text(c, "Score", "0", 120, new Vector2(0.5f, 0.52f), new Vector2(800, 180), TextColor);
            var newBest = Text(c, "NewBest", "NEW BEST!", 60, new Vector2(0.5f, 0.45f), new Vector2(800, 100), Accent);
            var next = Button(c, "NextButton", "NEXT", new Vector2(0.5f, 0.33f), new Vector2(620, 180), Primary, 80, out _);
            var retry = Button(c, "RetryButton", "RETRY", new Vector2(0.5f, 0.21f), new Vector2(520, 140), Secondary, 60, out _);
            var menu = Button(c, "MenuButton", "MENU", new Vector2(0.5f, 0.11f), new Vector2(420, 120), Secondary, 48, out _);
            var so = new SerializedObject(s);
            var starsProp = so.FindProperty("_stars");
            starsProp.arraySize = 3;
            for (var i = 0; i < 3; i++) starsProp.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtil.Set(s, "_scoreText", score);
            EditorUtil.Set(s, "_newBestLabel", newBest.gameObject);
            EditorUtil.Set(s, "_nextButton", next);
            EditorUtil.Set(s, "_retryButton", retry);
            EditorUtil.Set(s, "_menuButton", menu);
        }

        private static void BuildPause(PauseScreen s, RectTransform c)
        {
            FullscreenImage(s.transform, Dim);
            Text(c, "Title", "PAUSED", 110, new Vector2(0.5f, 0.7f), new Vector2(1000, 180), TextColor);
            var resume = Button(c, "ResumeButton", "RESUME", new Vector2(0.5f, 0.5f), new Vector2(620, 170), Primary, 70, out _);
            var settings = Button(c, "SettingsButton", "SETTINGS", new Vector2(0.5f, 0.38f), new Vector2(620, 140), Secondary, 54, out _);
            var menu = Button(c, "MenuButton", "MENU", new Vector2(0.5f, 0.27f), new Vector2(620, 140), Secondary, 54, out _);
            EditorUtil.Set(s, "_resumeButton", resume);
            EditorUtil.Set(s, "_settingsButton", settings);
            EditorUtil.Set(s, "_menuButton", menu);
        }

        private static void BuildSettings(SettingsScreen s, RectTransform c)
        {
            FullscreenImage(s.transform, Dim);
            var panel = Image(c, "Panel", new Vector2(0.5f, 0.5f), new Vector2(880, 1050), Panel);
            panel.GetComponent<Image>().sprite = _buttonSprite;
            panel.GetComponent<Image>().type = UnityEngine.UI.Image.Type.Sliced;
            Text(panel, "Title", "SETTINGS", 80, new Vector2(0.5f, 0.88f), new Vector2(800, 140), TextColor);
            var sound = Button(panel, "SoundButton", "SOUND: ON", new Vector2(0.5f, 0.68f), new Vector2(700, 150), Secondary, 56, out var soundLabel);
            var haptics = Button(panel, "HapticsButton", "VIBRATION: ON", new Vector2(0.5f, 0.52f), new Vector2(700, 150), Secondary, 56, out var hapticsLabel);
            var privacy = Button(panel, "PrivacyButton", "PRIVACY OPTIONS", new Vector2(0.5f, 0.36f), new Vector2(700, 120), Secondary, 44, out _);
            var close = Button(panel, "CloseButton", "CLOSE", new Vector2(0.5f, 0.14f), new Vector2(500, 150), Primary, 60, out _);
            EditorUtil.Set(s, "_soundButton", sound);
            EditorUtil.Set(s, "_soundLabel", soundLabel);
            EditorUtil.Set(s, "_hapticsButton", haptics);
            EditorUtil.Set(s, "_hapticsLabel", hapticsLabel);
            EditorUtil.Set(s, "_privacyButton", privacy);
            EditorUtil.Set(s, "_closeButton", close);
        }

        // ---------- Primitives (public for game screens) ----------
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void FullscreenImage(Transform screen, Color color)
        {
            var go = new GameObject("Background", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(screen, false);
            go.transform.SetAsFirstSibling();
            Stretch((RectTransform)go.transform);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = color.a > 0f; // dim overlays block clicks to the screen below
        }

        public static RectTransform Image(Transform parent, string name, Vector2 anchor, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        public static TMP_Text Text(Transform parent, string name, string text, float size, Vector2 anchor, Vector2 box, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = box;
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = TMP_Settings.defaultFontAsset;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = color;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, Vector2 anchor, Vector2 size, Color color, float fontSize, out TMP_Text text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = ButtonSprite;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = color;
            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            button.colors = colors;
            text = Text(go.transform, "Label", label, fontSize, new Vector2(0.5f, 0.5f), size, TextColor);
            return button;
        }
    }
}
