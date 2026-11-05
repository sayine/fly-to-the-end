using Game.Levels;
using Game.Planes;
using Game.UI;
using Template.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Phase 3 UI: Hangar screen (5×4 plane grid + detail panel with stat bars, Select, rewarded Try), the daily
    /// check-in popup (both added to UIRoot) and the Game scene's "NEW PLANE!" toast. Idempotent.
    /// </summary>
    internal static class PlaneUiBuilder
    {
        private static readonly string[] StatNames = { "SPEED", "AGILITY", "GLIDE", "LIFT", "STABILITY" };
        private static readonly Color Badge = new Color(0.92f, 0.3f, 0.3f, 1f);

        public static void Build(PlaneCatalog planes, LevelCatalog levels)
        {
            UIBuilder.EnsureInRoot(UIBuilder.Screen<HangarScreen>(true, (s, c) => BuildHangar(s, c, planes, levels)));
            UIBuilder.EnsureInRoot(UIBuilder.Screen<CheckInScreen>(true, BuildCheckIn));
        }

        /// <summary>NEW badge (red pill) at the top-right corner of a button.</summary>
        public static GameObject NewBadge(Transform parent, float fontSize = 24f)
        {
            var badge = UIBuilder.Image(parent, "NewBadge", new Vector2(1f, 1f), new Vector2(84, 40), Badge);
            badge.anchoredPosition = new Vector2(-30f, -8f);
            var img = badge.GetComponent<Image>();
            img.sprite = UIBuilder.ButtonSprite;
            img.type = Image.Type.Sliced;
            UIBuilder.Text(badge, "Label", "NEW", fontSize, new Vector2(0.5f, 0.5f), new Vector2(84, 40), Color.white);
            return badge.gameObject;
        }

        // ---------- Hangar ----------
        private static void BuildHangar(HangarScreen s, RectTransform c, PlaneCatalog planes, LevelCatalog levels)
        {
            UIBuilder.FullscreenImage(s.transform, new Color(UIBuilder.Background.r, UIBuilder.Background.g, UIBuilder.Background.b, 0.96f));
            UIBuilder.Text(c, "Title", "HANGAR", 96, new Vector2(0.5f, 0.915f), new Vector2(900, 150), UIBuilder.TextColor);

            // Detail panel.
            var panel = UIBuilder.Image(c, "Detail", new Vector2(0.5f, 0.72f), new Vector2(1000, 420), UIBuilder.Panel);
            var panelImg = panel.GetComponent<Image>();
            panelImg.sprite = UIBuilder.ButtonSprite;
            panelImg.type = Image.Type.Sliced;
            var thumb = Thumb(panel, "Thumbnail", new Vector2(320, 300));
            thumb.rectTransform.anchoredPosition = new Vector2(-320f, 30f);
            var name = UIBuilder.Text(panel, "Name", "Plane", 40, new Vector2(0.5f, 0.5f), new Vector2(380, 60), UIBuilder.Accent);
            name.rectTransform.anchoredPosition = new Vector2(-320f, -160f);
            name.enableAutoSizing = true;
            name.fontSizeMin = 24;
            name.fontSizeMax = 40;

            var stats = new HangarScreen.StatBar[StatNames.Length];
            for (var k = 0; k < StatNames.Length; k++)
            {
                var y = 160f - k * 50f;
                var label = UIBuilder.Text(panel, $"{StatNames[k]}Label", StatNames[k], 30, new Vector2(0.5f, 0.5f), new Vector2(240, 46), new Color(1f, 1f, 1f, 0.8f));
                label.alignment = TextAlignmentOptions.Left;
                label.rectTransform.anchoredPosition = new Vector2(-30f, y);
                stats[k] = new HangarScreen.StatBar { Pips = new Image[5] };
                for (var p = 0; p < 5; p++)
                {
                    var pip = UIBuilder.Image(panel, $"{StatNames[k]}Pip{p + 1}", new Vector2(0.5f, 0.5f), new Vector2(46, 30), Color.white);
                    pip.anchoredPosition = new Vector2(150f + p * 54f, y);
                    var pipImg = pip.GetComponent<Image>();
                    pipImg.sprite = UIBuilder.ButtonSprite;
                    pipImg.type = Image.Type.Sliced;
                    stats[k].Pips[p] = pipImg;
                }
            }
            var info = UIBuilder.Text(panel, "Info", "", 34, new Vector2(0.5f, 0.5f), new Vector2(560, 50), UIBuilder.Accent);
            info.rectTransform.anchoredPosition = new Vector2(200f, -90f);
            var select = UIBuilder.Button(panel, "SelectButton", "SELECT", new Vector2(0.5f, 0.5f), new Vector2(420, 100), UIBuilder.Primary, 46, out var selectLabel);
            ((RectTransform)select.transform).anchoredPosition = new Vector2(200f, -155f);
            var tryIt = UIBuilder.Button(panel, "TryButton", "TRY IT (AD)", new Vector2(0.5f, 0.5f), new Vector2(420, 100), UIBuilder.Accent, 42, out var tryLabel);
            tryLabel.color = UIBuilder.Background;
            ((RectTransform)tryIt.transform).anchoredPosition = new Vector2(200f, -155f);

            // Grid: 5 columns × 4 rows.
            const int columns = 5;
            var cells = new HangarScreen.Cell[planes.Count];
            for (var i = 0; i < planes.Count; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var button = UIBuilder.Button(c, $"Plane{i + 1}", "", new Vector2(0.5f, 0.52f), new Vector2(192, 214), UIBuilder.Secondary, 10, out var unused);
                Object.DestroyImmediate(unused.gameObject);
                var rt = (RectTransform)button.transform;
                rt.anchoredPosition = new Vector2((col - (columns - 1) * 0.5f) * 204f, -row * 226f);
                var group = button.gameObject.AddComponent<CanvasGroup>();
                var cellThumb = Thumb(button.transform, "Thumbnail", new Vector2(170, 150));
                cellThumb.rectTransform.anchorMin = cellThumb.rectTransform.anchorMax = new Vector2(0.5f, 0.6f);
                var cellName = UIBuilder.Text(button.transform, "Name", planes.Get(i).DisplayName, 22, new Vector2(0.5f, 0.12f), new Vector2(180, 44), UIBuilder.TextColor);
                cellName.enableAutoSizing = true;
                cellName.fontSizeMin = 14;
                cellName.fontSizeMax = 22;
                var condition = UIBuilder.Text(button.transform, "Condition", "", 30, new Vector2(0.5f, 0.58f), new Vector2(184, 60), UIBuilder.Accent);
                condition.outlineWidth = 0.25f;
                condition.outlineColor = Color.black;
                var badge = NewBadge(button.transform, 20f);
                ((RectTransform)badge.transform).sizeDelta = new Vector2(70, 34);
                cells[i] = new HangarScreen.Cell
                {
                    Button = button, Thumbnail = cellThumb, Name = cellName, Condition = condition, Group = group,
                    NewBadge = badge, Frame = button.GetComponent<Image>(),
                };
            }
            var back = UIBuilder.Button(c, "BackButton", "BACK", new Vector2(0.5f, 0.07f), new Vector2(460, 130), UIBuilder.Secondary, 54, out _);

            var so = new SerializedObject(s);
            so.FindProperty("_planes").objectReferenceValue = planes;
            so.FindProperty("_levels").objectReferenceValue = levels;
            so.FindProperty("_backButton").objectReferenceValue = back;
            so.FindProperty("_detailThumbnail").objectReferenceValue = thumb;
            so.FindProperty("_detailName").objectReferenceValue = name;
            so.FindProperty("_detailInfo").objectReferenceValue = info;
            so.FindProperty("_selectButton").objectReferenceValue = select;
            so.FindProperty("_selectLabel").objectReferenceValue = selectLabel;
            so.FindProperty("_tryButton").objectReferenceValue = tryIt;
            so.FindProperty("_frameOn").colorValue = new Color(0.62f, 0.52f, 0.2f, 1f);
            so.FindProperty("_frameOff").colorValue = UIBuilder.Secondary;
            var statsProp = so.FindProperty("_stats");
            statsProp.arraySize = stats.Length;
            for (var k = 0; k < stats.Length; k++)
            {
                var pips = statsProp.GetArrayElementAtIndex(k).FindPropertyRelative("Pips");
                pips.arraySize = 5;
                for (var p = 0; p < 5; p++) pips.GetArrayElementAtIndex(p).objectReferenceValue = stats[k].Pips[p];
            }
            var cellsProp = so.FindProperty("_cells");
            cellsProp.arraySize = cells.Length;
            for (var i = 0; i < cells.Length; i++)
            {
                var e = cellsProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Button").objectReferenceValue = cells[i].Button;
                e.FindPropertyRelative("Thumbnail").objectReferenceValue = cells[i].Thumbnail;
                e.FindPropertyRelative("Name").objectReferenceValue = cells[i].Name;
                e.FindPropertyRelative("Condition").objectReferenceValue = cells[i].Condition;
                e.FindPropertyRelative("Group").objectReferenceValue = cells[i].Group;
                e.FindPropertyRelative("NewBadge").objectReferenceValue = cells[i].NewBadge;
                e.FindPropertyRelative("Frame").objectReferenceValue = cells[i].Frame;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Image Thumb(Transform parent, string name, Vector2 size)
        {
            var img = UIBuilder.Image(parent, name, new Vector2(0.5f, 0.5f), size, Color.white).GetComponent<Image>();
            img.preserveAspect = true;
            return img;
        }

        // ---------- Daily check-in ----------
        private static void BuildCheckIn(CheckInScreen s, RectTransform c)
        {
            UIBuilder.FullscreenImage(s.transform, UIBuilder.Dim);
            var panel = UIBuilder.Image(c, "Panel", new Vector2(0.5f, 0.5f), new Vector2(960, 1060), UIBuilder.Panel);
            var panelImg = panel.GetComponent<Image>();
            panelImg.sprite = UIBuilder.ButtonSprite;
            panelImg.type = Image.Type.Sliced;
            var subtitle = UIBuilder.Text(panel, "Subtitle", "DAILY CHECK-IN", 44, new Vector2(0.5f, 0.92f), new Vector2(800, 70), new Color(1f, 1f, 1f, 0.7f));
            var title = UIBuilder.Text(panel, "Title", "DAY 1", 110, new Vector2(0.5f, 0.82f), new Vector2(800, 150), UIBuilder.Accent);

            var star = UIBuilder.StarSprite();
            var days = new Image[14];
            for (var i = 0; i < days.Length; i++)
            {
                var col = i % 7;
                var row = i / 7;
                var box = UIBuilder.Image(panel, $"Day{i + 1}", new Vector2(0.5f, 0.66f), new Vector2(104, 104), Color.white);
                box.anchoredPosition = new Vector2((col - 3) * 120f, -row * 124f);
                days[i] = box.GetComponent<Image>();
                days[i].sprite = UIBuilder.ButtonSprite;
                days[i].type = Image.Type.Sliced;
                var reward = DailyCheckIn.RewardDays[0] == i + 1 || DailyCheckIn.RewardDays[1] == i + 1;
                if (reward)
                {
                    var gift = UIBuilder.Image(box, "Reward", new Vector2(0.5f, 0.5f), new Vector2(90, 90), UIBuilder.Accent);
                    gift.GetComponent<Image>().sprite = star;
                }
                UIBuilder.Text(box, "Number", (i + 1).ToString(), 40, new Vector2(0.5f, 0.5f), new Vector2(104, 104), reward ? UIBuilder.Background : Color.white);
            }

            var thumb = Thumb(panel, "RewardThumbnail", new Vector2(320, 260));
            thumb.rectTransform.anchorMin = thumb.rectTransform.anchorMax = new Vector2(0.5f, 0.33f);
            var message = UIBuilder.Text(panel, "Message", "", 46, new Vector2(0.5f, 0.18f), new Vector2(880, 90), UIBuilder.TextColor);
            message.enableAutoSizing = true;
            message.fontSizeMin = 28;
            message.fontSizeMax = 46;
            var cont = UIBuilder.Button(panel, "ContinueButton", "CONTINUE", new Vector2(0.5f, 0.07f), new Vector2(460, 120), UIBuilder.Primary, 52, out _);

            var so = new SerializedObject(s);
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_message").objectReferenceValue = message;
            so.FindProperty("_rewardThumbnail").objectReferenceValue = thumb;
            so.FindProperty("_continueButton").objectReferenceValue = cont;
            var daysProp = so.FindProperty("_days");
            daysProp.arraySize = days.Length;
            for (var i = 0; i < days.Length; i++) daysProp.GetArrayElementAtIndex(i).objectReferenceValue = days[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Unlock toast (Game scene) ----------
        public static UnlockToast BuildToast()
        {
            var go = new GameObject("UnlockToast", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // above UIRoot (10)
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = UIBuilder.Image(go.transform, "Panel", new Vector2(0.5f, 1f), new Vector2(820, 220), UIBuilder.Panel);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, 260f);
            var panelImg = panel.GetComponent<Image>();
            panelImg.sprite = UIBuilder.ButtonSprite;
            panelImg.type = Image.Type.Sliced;
            var thumb = Thumb(panel, "Thumbnail", new Vector2(220, 180));
            thumb.rectTransform.anchoredPosition = new Vector2(-270f, 0f);
            var heading = UIBuilder.Text(panel, "Heading", "NEW PLANE!", 56, new Vector2(0.5f, 0.5f), new Vector2(520, 80), UIBuilder.Accent);
            heading.rectTransform.anchoredPosition = new Vector2(110f, 36f);
            var name = UIBuilder.Text(panel, "Name", "", 40, new Vector2(0.5f, 0.5f), new Vector2(520, 60), UIBuilder.TextColor);
            name.rectTransform.anchoredPosition = new Vector2(110f, -40f);
            name.enableAutoSizing = true;
            name.fontSizeMin = 24;
            name.fontSizeMax = 40;

            var toast = go.AddComponent<UnlockToast>();
            EditorUtil.Set(toast, "_canvas", canvas);
            EditorUtil.Set(toast, "_panel", panel);
            EditorUtil.Set(toast, "_name", name);
            EditorUtil.Set(toast, "_thumbnail", thumb);
            return toast;
        }
    }
}
