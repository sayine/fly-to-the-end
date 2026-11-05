using Game.Levels;
using Game.UI;
using Template.Editor;
using Template.UI.Screens;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Paper Flight UI on top of the template screens: level map screen (added to UIRoot), Levels / Free Flight
    /// buttons and the "LEVEL n" label on the menu, "CRASHED!" Game Over title, time formats on result screens.
    /// Idempotent: safe to run on every build.
    /// </summary>
    internal static class MenuUiBuilder
    {
        private const string ScreensFolder = UIBuilder.ScreensFolder;

        public static void Build(LevelCatalog catalog)
        {
            UIBuilder.BuildAll(false); // make sure template screens (incl. LevelComplete) exist in UIRoot
            var map = UIBuilder.Screen<LevelMapScreen>(true, (s, c) => BuildLevelMap(s, c, catalog));
            UIBuilder.EnsureInRoot(map);
            PatchMenu(catalog);
            Patch<GameOverScreen>($"{ScreensFolder}/GameOverScreen.prefab", root =>
                root.transform.Find("Content/Title").GetComponent<TMP_Text>().text = "CRASHED!");
            Patch<LevelCompleteScreen>($"{ScreensFolder}/LevelCompleteScreen.prefab", root =>
            {
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                    if (image.name.StartsWith("Star") && image.sprite == null) image.sprite = UIBuilder.StarSprite();
                var so = new SerializedObject(root.GetComponent<LevelCompleteScreen>());
                so.FindProperty("_scoreFormat").stringValue = "{0:0.0}s";
                so.FindProperty("_scoreScale").floatValue = 0.1f;
                so.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        // ---------- Level map ----------
        private static void BuildLevelMap(LevelMapScreen s, RectTransform c, LevelCatalog catalog)
        {
            UIBuilder.FullscreenImage(s.transform, new Color(UIBuilder.Background.r, UIBuilder.Background.g, UIBuilder.Background.b, 0.94f));
            UIBuilder.Text(c, "Title", "LEVELS", 100, new Vector2(0.5f, 0.9f), new Vector2(900, 160), UIBuilder.TextColor);
            var star = UIBuilder.StarSprite();
            var totalIcon = UIBuilder.Image(c, "TotalStar", new Vector2(0.5f, 0.83f), new Vector2(64, 64), UIBuilder.Accent);
            totalIcon.anchoredPosition = new Vector2(-120f, 0f);
            totalIcon.GetComponent<Image>().sprite = star;
            var total = UIBuilder.Text(c, "TotalStars", "0 / 30", 56, new Vector2(0.5f, 0.83f), new Vector2(300, 80), UIBuilder.Accent);
            total.rectTransform.anchoredPosition = new Vector2(40f, 0f);

            const int columns = 4;
            var count = catalog.Count;
            var cells = new LevelMapScreen.Cell[count];
            for (var i = 0; i < count; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var anchor = new Vector2(0.5f, 0.72f);
                var button = UIBuilder.Button(c, $"Level{i + 1}", (i + 1).ToString(), anchor, new Vector2(210, 230), UIBuilder.Primary, 84, out var number);
                var rt = (RectTransform)button.transform;
                rt.anchoredPosition = new Vector2((col - (columns - 1) * 0.5f) * 240f, -row * 270f);
                number.rectTransform.anchoredPosition = new Vector2(0f, 26f);
                var group = button.gameObject.AddComponent<CanvasGroup>();
                var stars = new Image[3];
                for (var k = 0; k < 3; k++)
                {
                    var st = UIBuilder.Image(button.transform, $"Star{k + 1}", new Vector2(0.5f, 0.2f), new Vector2(54, 54), Color.white);
                    st.anchoredPosition = new Vector2((k - 1) * 58f, k == 1 ? 6f : 0f);
                    stars[k] = st.GetComponent<Image>();
                    stars[k].sprite = star;
                }
                cells[i] = new LevelMapScreen.Cell { Button = button, Number = number, Stars = stars, Group = group };
            }
            var back = UIBuilder.Button(c, "BackButton", "BACK", new Vector2(0.5f, 0.08f), new Vector2(460, 130), UIBuilder.Secondary, 54, out _);

            var so = new SerializedObject(s);
            so.FindProperty("_catalog").objectReferenceValue = catalog;
            so.FindProperty("_backButton").objectReferenceValue = back;
            so.FindProperty("_totalStars").objectReferenceValue = total;
            var cellsProp = so.FindProperty("_cells");
            cellsProp.arraySize = cells.Length;
            for (var i = 0; i < cells.Length; i++)
            {
                var e = cellsProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Button").objectReferenceValue = cells[i].Button;
                e.FindPropertyRelative("Number").objectReferenceValue = cells[i].Number;
                e.FindPropertyRelative("Group").objectReferenceValue = cells[i].Group;
                var starsProp = e.FindPropertyRelative("Stars");
                starsProp.arraySize = 3;
                for (var k = 0; k < 3; k++) starsProp.GetArrayElementAtIndex(k).objectReferenceValue = cells[i].Stars[k];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Menu ----------
        private static void PatchMenu(LevelCatalog catalog)
        {
            Patch<MenuScreen>($"{ScreensFolder}/MenuScreen.prefab", root =>
            {
                var content = root.transform.Find("Content");
                // Rebuild our additions each time (idempotent).
                foreach (var n in new[] { "LevelLabel", "LevelsButton", "FreeFlightButton" })
                {
                    var old = content.Find(n);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                }
                var existing = root.GetComponent<MenuExtras>();
                if (existing != null) Object.DestroyImmediate(existing);

                var label = UIBuilder.Text(content, "LevelLabel", "LEVEL 1", 64, new Vector2(0.5f, 0.395f), new Vector2(700, 100), UIBuilder.Accent);
                var levels = UIBuilder.Button(content, "LevelsButton", "LEVELS", new Vector2(0.29f, 0.17f), new Vector2(430, 130), UIBuilder.Secondary, 52, out _);
                var free = UIBuilder.Button(content, "FreeFlightButton", "FREE FLIGHT", new Vector2(0.71f, 0.17f), new Vector2(430, 130), UIBuilder.Secondary, 48, out _);
                var settings = (RectTransform)content.Find("SettingsButton");
                settings.anchorMin = settings.anchorMax = new Vector2(0.5f, 0.065f);
                settings.sizeDelta = new Vector2(380, 100);
                settings.Find("Label").GetComponent<TMP_Text>().fontSize = 40;

                var extras = root.AddComponent<MenuExtras>();
                var so = new SerializedObject(extras);
                so.FindProperty("_catalog").objectReferenceValue = catalog;
                so.FindProperty("_levelLabel").objectReferenceValue = label;
                so.FindProperty("_levelsButton").objectReferenceValue = levels;
                so.FindProperty("_freeFlightButton").objectReferenceValue = free;
                so.ApplyModifiedPropertiesWithoutUndo();

                var menu = new SerializedObject(root.GetComponent<MenuScreen>());
                menu.FindProperty("_bestFormat").stringValue = "BEST {0:0.0}s";
                menu.FindProperty("_scoreScale").floatValue = 0.1f;
                menu.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private static void Patch<T>(string path, System.Action<GameObject> patch) where T : Component
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                patch(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
