using Game.Levels;
using Game.Planes;
using Game.UI;
using Template.Editor;
using Template.UI;
using Template.UI.Screens;
using Template.UI.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using K = Game.Editor.PaperKit;
using I = Game.Editor.PaperIcons;

namespace Game.Editor
{
    /// <summary>
    /// Every Paper Flight screen in the paper-craft style (SPEC: hybrid-casual polish): the template screens
    /// (Loading, Menu, HUD, Pause, Settings, Game Over, Level Complete) rebuilt with game layouts, plus the level
    /// map, hangar, daily check-in and the Game scene's unlock toast. Rebuilt on every content build (idempotent).
    /// </summary>
    internal static class PaperScreens
    {
        private static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 Top = new Vector2(0.5f, 1f);
        private static readonly string[] StatNames = { "SPEED", "AGILITY", "GLIDE", "LIFT", "STABILITY" };

        private static LevelCatalog _levels;

        private static Game.Audio.AudioConfig _audio;

        public static void Build(LevelCatalog levels, PlaneCatalog planes, Game.Audio.AudioConfig audio, bool force)
        {
            _levels = levels;
            _audio = audio;
            K.Prepare(force);
            UIBuilder.BuildAll(false); // UIRoot + any template screen added later
            Add<LoadingScreen>(BuildLoading);
            Add<MenuScreen>((s, c) => BuildMenu(s, c, levels, planes));
            Add<HUDScreen>(BuildHud);
            Add<PauseScreen>(BuildPause);
            Add<SettingsScreen>(BuildSettings);
            Add<GameOverScreen>(BuildGameOver);
            Add<LevelCompleteScreen>(BuildLevelComplete);
            Add<LevelMapScreen>((s, c) => BuildLevelMap(s, c, levels));
            Add<HangarScreen>((s, c) => BuildHangar(s, c, planes, levels));
            Add<CheckInScreen>(BuildCheckIn);
            SetClickSound();
        }

        /// <summary>Button click (ScreenManager on UIRoot plays it for every bound button).</summary>
        private static void SetClickSound()
        {
            var root = PrefabUtility.LoadPrefabContents(UIBuilder.RootPath);
            try
            {
                EditorUtil.Set(root.GetComponent<ScreenManager>(), "_clickSfx", _audio != null ? _audio.Click : null);
                PrefabUtility.SaveAsPrefabAsset(root, UIBuilder.RootPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Add<T>(System.Action<T, RectTransform> build) where T : UIScreen =>
            UIBuilder.EnsureInRoot(UIBuilder.Screen(true, build));

        // ---------- Shared pieces ----------
        private static Sprite _rays;

        private static Sprite Rays => _rays != null ? _rays : _rays = K.SpriteAsset("Rays", 256, 0, false, p =>
        {
            var d = p - new Vector2(128f, 128f);
            var r = d.magnitude / 128f;
            if (r > 1f) return 0f;
            var a = Mathf.Atan2(d.y, d.x);
            var wedge = Mathf.Cos(a * 12f) > 0.2f ? 1f : 0f;
            return wedge * (1f - Mathf.SmoothStep(0.25f, 1f, r)) * 0.9f;
        });

        /// <summary>Back button, centred title and an optional right-hand counter, for full-screen pages.</summary>
        private static Button Header(RectTransform c, string title)
        {
            var back = K.IconBtn(c, "BackButton", I.Back, new Vector2(0f, 1f), 120f, K.Cream);
            ((RectTransform)back.transform).anchoredPosition = new Vector2(100f, -105f);
            var t = K.Txt(c, "Title", title, 96, Top, new Vector2(560, 140), Color.white, K.TextStyle.Title);
            t.rectTransform.anchoredPosition = new Vector2(0f, -105f);
            return back;
        }

        private static Image Stars(Transform parent, string name, Vector2 anchor, Vector2 position, float size)
        {
            var img = K.Img(parent, name, K.StarSprite, K.Star, anchor, Vector2.one * size);
            img.preserveAspect = true;
            img.rectTransform.anchoredPosition = position;
            return img;
        }

        // ---------- Loading ----------
        private static void BuildLoading(LoadingScreen s, RectTransform c)
        {
            K.SkyBackground(s.transform);
            var plane = K.Img(c, "Logo", I.Plane, Color.white, new Vector2(0.5f, 0.64f), new Vector2(300, 300));
            plane.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            var title = K.Txt(c, "Title", "PAPER FLIGHT", 124, new Vector2(0.5f, 0.52f), new Vector2(1000, 200), Color.white, K.TextStyle.Title);
            title.gameObject.AddComponent<ProductNameLabel>();
            title.enableAutoSizing = true;
            title.fontSizeMin = 70;
            title.fontSizeMax = 124;
            var track = K.Img(c, "ProgressBar", K.Rounded, K.Paper, new Vector2(0.5f, 0.4f), new Vector2(620, 46), true, 2.8f);
            var fill = K.Img(track.transform, "Fill", K.Rounded, K.Coral.Face, Vector2.zero, Vector2.zero, true, 3.4f);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = new Vector2(6f, 6f);
            fill.rectTransform.offsetMax = new Vector2(-6f, -6f);
            var status = K.Txt(c, "Status", "Loading...", 44, new Vector2(0.5f, 0.355f), new Vector2(800, 80), Color.white, K.TextStyle.Shadow);
            EditorUtil.Set(s, "_status", status);
            EditorUtil.Set(s, "_progressFill", fill.rectTransform);
        }

        // ---------- Menu ----------
        private static void BuildMenu(MenuScreen s, RectTransform c, LevelCatalog levels, PlaneCatalog planes)
        {
            var veil = K.Img(s.transform, "Background", null, new Color(0, 0, 0, 0), Vector2.zero, Vector2.zero);
            K.Fill(veil.rectTransform);
            veil.transform.SetAsFirstSibling();
            // Soft gradient at the bottom so the buttons read over the room.
            var fade = K.Img(s.transform, "BottomFade", K.Fade, new Color(0.1f, 0.14f, 0.25f, 0.55f), new Vector2(0.5f, 0f), Vector2.zero);
            fade.rectTransform.anchorMin = Vector2.zero;
            fade.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            fade.rectTransform.offsetMin = fade.rectTransform.offsetMax = Vector2.zero;
            fade.transform.SetSiblingIndex(1);

            var title = K.Txt(c, "Title", "PAPER FLIGHT", 138, new Vector2(0.5f, 0.83f), new Vector2(1040, 220), Color.white, K.TextStyle.Title);
            title.gameObject.AddComponent<ProductNameLabel>();
            title.enableAutoSizing = true;
            title.fontSizeMin = 80;
            title.fontSizeMax = 138;
            title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 3f);
            var best = K.Pill(c, "Best", "BEST 0.0s", new Vector2(0.5f, 0.735f), new Vector2(430, 88), K.Paper, K.Ink, 46, K.StarSprite, K.Star);
            var settings = K.IconBtn(c, "SettingsButton", I.Gear, new Vector2(1f, 1f), 120f, K.Cream);
            ((RectTransform)settings.transform).anchoredPosition = new Vector2(-95f, -95f);

            var level = K.Pill(c, "LevelLabel", "LEVEL 1", new Vector2(0.5f, 0.392f), new Vector2(340, 84), K.Sun.Face, K.Sun.Text, 48);
            ((RectTransform)level.transform.parent).localRotation = Quaternion.Euler(0f, 0f, -3f);
            var play = K.Btn(c, "PlayButton", "PLAY", new Vector2(0.5f, 0.3f), new Vector2(720, 210), K.Coral, 104, out _, I.Play);
            var levelsBtn = K.Btn(c, "LevelsButton", "LEVELS", new Vector2(0.285f, 0.185f), new Vector2(440, 150), K.Sky, 56, out _, I.Map);
            var hangar = K.Btn(c, "HangarButton", "HANGAR", new Vector2(0.715f, 0.185f), new Vector2(440, 150), K.Sun, 56, out _, I.Plane);
            var badge = K.NewBadge(hangar.transform.Find("Face"), new Vector2(1f, 1f), new Vector2(-24f, -6f));
            badge.SetActive(false);
            var free = K.Btn(c, "FreeFlightButton", "FREE FLIGHT", new Vector2(0.5f, 0.083f), new Vector2(900, 130), K.Mint, 52, out _, I.Cloud);

            EditorUtil.Set(s, "_playButton", play);
            EditorUtil.Set(s, "_settingsButton", settings);
            EditorUtil.Set(s, "_bestScoreText", best);
            K.SetString(s, "_bestFormat", "BEST {0:0.0}s");
            K.SetFloat(s, "_scoreScale", 0.1f);

            var extras = s.gameObject.AddComponent<MenuExtras>();
            EditorUtil.Set(extras, "_catalog", levels);
            EditorUtil.Set(extras, "_planes", planes);
            EditorUtil.Set(extras, "_levelLabel", level);
            EditorUtil.Set(extras, "_levelsButton", levelsBtn);
            EditorUtil.Set(extras, "_hangarButton", hangar);
            EditorUtil.Set(extras, "_hangarBadge", badge);
            EditorUtil.Set(extras, "_freeFlightButton", free);
            K.Intro(s, null);
        }

        // ---------- HUD ----------
        private static void BuildHud(HUDScreen s, RectTransform c)
        {
            var score = K.Pill(c, "Score", "0.0s", Top, new Vector2(330, 120), new Color(0.1f, 0.14f, 0.25f, 0.45f), Color.white, 80);
            ((RectTransform)score.transform.parent).anchoredPosition = new Vector2(0f, -110f);
            var pause = K.IconBtn(c, "PauseButton", I.Pause, new Vector2(1f, 1f), 116f, K.Cream);
            ((RectTransform)pause.transform).anchoredPosition = new Vector2(-95f, -110f);
            // Free Flight "Boost throw" offer, top-left (the top band never starts a throw).
            var boost = K.Btn(c, "BoostButton", "BOOST", new Vector2(0f, 1f), new Vector2(270, 110), K.Sun, 44, out _, I.Video);
            ((RectTransform)boost.transform).anchoredPosition = new Vector2(165f, -110f);
            var boostView = s.gameObject.AddComponent<BoostThrowView>();
            EditorUtil.Set(boostView, "_button", boost);
            EditorUtil.Set(s, "_scoreText", score);
            EditorUtil.Set(s, "_pauseButton", pause);
            K.SetString(s, "_scoreFormat", "{0:0.0}s");
            K.SetFloat(s, "_scoreScale", 0.1f);
        }

        // ---------- Pause ----------
        private static void BuildPause(PauseScreen s, RectTransform c)
        {
            K.Dim(s.transform);
            var card = K.Card(c, "Card", Mid, new Vector2(800, 760));
            K.RibbonTitle(card, "Ribbon", "PAUSED", Top, 700, K.Sky);
            var resume = K.Btn(card, "ResumeButton", "RESUME", Mid, new Vector2(620, 170), K.Coral, 70, out _, I.Play);
            ((RectTransform)resume.transform).anchoredPosition = new Vector2(0f, 130f);
            var settings = K.Btn(card, "SettingsButton", "SETTINGS", Mid, new Vector2(620, 140), K.Sky, 54, out _, I.Gear);
            ((RectTransform)settings.transform).anchoredPosition = new Vector2(0f, -55f);
            var menu = K.Btn(card, "MenuButton", "MENU", Mid, new Vector2(620, 140), K.Cream, 54, out _, I.Home);
            ((RectTransform)menu.transform).anchoredPosition = new Vector2(0f, -225f);
            EditorUtil.Set(s, "_resumeButton", resume);
            EditorUtil.Set(s, "_settingsButton", settings);
            EditorUtil.Set(s, "_menuButton", menu);
            K.Intro(s, card);
        }

        // ---------- Settings ----------
        private static void BuildSettings(SettingsScreen s, RectTransform c)
        {
            K.Dim(s.transform);
            var card = K.Card(c, "Card", Mid, new Vector2(840, 900));
            K.RibbonTitle(card, "Ribbon", "SETTINGS", Top, 760, K.Grape);
            var sound = K.Btn(card, "SoundButton", "SOUND: ON", Mid, new Vector2(680, 150), K.Sky, 54, out var soundLabel, I.Sound);
            ((RectTransform)sound.transform).anchoredPosition = new Vector2(0f, 190f);
            var haptics = K.Btn(card, "HapticsButton", "VIBRATION: ON", Mid, new Vector2(680, 150), K.Sky, 54, out var hapticsLabel, I.Vibrate);
            ((RectTransform)haptics.transform).anchoredPosition = new Vector2(0f, 10f);
            var privacy = K.Btn(card, "PrivacyButton", "PRIVACY OPTIONS", Mid, new Vector2(680, 120), K.Cream, 42, out _);
            ((RectTransform)privacy.transform).anchoredPosition = new Vector2(0f, -150f);
            var close = K.Btn(card, "CloseButton", "DONE", Mid, new Vector2(460, 150), K.Mint, 62, out _, I.Check);
            ((RectTransform)close.transform).anchoredPosition = new Vector2(0f, -320f);
            EditorUtil.Set(s, "_soundButton", sound);
            EditorUtil.Set(s, "_soundLabel", soundLabel);
            EditorUtil.Set(s, "_hapticsButton", haptics);
            EditorUtil.Set(s, "_hapticsLabel", hapticsLabel);
            EditorUtil.Set(s, "_privacyButton", privacy);
            EditorUtil.Set(s, "_closeButton", close);
            K.Intro(s, card);
        }

        // ---------- Game Over ----------
        private static void BuildGameOver(GameOverScreen s, RectTransform c)
        {
            K.Dim(s.transform);
            var panel = K.Rect(c, "Panel", Mid, new Vector2(1080, 1920));
            K.Fill(panel);
            var card = K.Card(panel, "Card", new Vector2(0.5f, 0.6f), new Vector2(820, 520));
            K.RibbonTitle(card, "Title", "CRASHED!", Top, 760, K.Coral, 84);
            var label = K.Txt(card, "Label", "AIRTIME", 40, Mid, new Vector2(600, 60), K.InkSoft);
            label.rectTransform.anchoredPosition = new Vector2(0f, 120f);
            var score = K.Txt(card, "Score", "0.0s", 170, Mid, new Vector2(760, 200), K.Ink);
            score.rectTransform.anchoredPosition = new Vector2(0f, 10f);
            var objective = K.Txt(card, "Objective", "", 38, Mid, new Vector2(720, 60), K.InkSoft);
            objective.rectTransform.anchoredPosition = new Vector2(0f, -170f);
            objective.enableAutoSizing = true;
            objective.fontSizeMin = 24;
            objective.fontSizeMax = 38;
            var best = K.Pill(card, "Best", "BEST 0.0s", Mid, new Vector2(380, 80), K.PaperDeep, K.Ink, 40, K.StarSprite, K.Star);
            ((RectTransform)best.transform.parent).anchoredPosition = new Vector2(0f, -170f);
            var newBest = K.Pill(card, "NewBest", "NEW BEST!", new Vector2(1f, 1f), new Vector2(300, 80), K.Sun.Face, K.Sun.Text, 42);
            var nb = (RectTransform)newBest.transform.parent;
            nb.anchoredPosition = new Vector2(-60f, -110f);
            nb.localRotation = Quaternion.Euler(0f, 0f, -12f);

            var cont = K.Btn(panel, "ContinueButton", "SECOND CHANCE", new Vector2(0.5f, 0.365f), new Vector2(780, 180), K.Sun, 64, out _, I.Video);
            var retry = K.Btn(panel, "RetryButton", "RETRY", new Vector2(0.5f, 0.235f), new Vector2(620, 160), K.Coral, 66, out _, I.Retry);
            var menu = K.Btn(panel, "MenuButton", "MENU", new Vector2(0.5f, 0.12f), new Vector2(420, 120), K.Cream, 48, out _, I.Home);
            EditorUtil.Set(s, "_scoreText", score);
            EditorUtil.Set(s, "_bestText", best);
            EditorUtil.Set(s, "_newBestLabel", nb.gameObject);
            EditorUtil.Set(s, "_continueButton", cont);
            EditorUtil.Set(s, "_retryButton", retry);
            EditorUtil.Set(s, "_menuButton", menu);
            K.SetString(s, "_scoreFormat", "{0:0.0}s");
            K.SetString(s, "_bestFormat", "BEST {0:0.0}s");
            K.SetFloat(s, "_scoreScale", 0.1f);
            var extras = s.gameObject.AddComponent<GameOverExtras>();
            EditorUtil.Set(extras, "_catalog", _levels);
            EditorUtil.Set(extras, "_label", label);
            EditorUtil.Set(extras, "_objective", objective);
            EditorUtil.Set(extras, "_best", best.transform.parent.gameObject);
            K.Intro(s, panel, 0.9f);
        }

        // ---------- Level Complete ----------
        private static void BuildLevelComplete(LevelCompleteScreen s, RectTransform c)
        {
            K.Dim(s.transform);
            var panel = K.Rect(c, "Panel", Mid, new Vector2(1080, 1920));
            K.Fill(panel);
            var rays = K.Img(panel, "Rays", Rays, new Color(1f, 0.93f, 0.6f, 0.55f), new Vector2(0.5f, 0.66f), new Vector2(1100, 1100));
            rays.preserveAspect = true;
            var card = K.Card(panel, "Card", new Vector2(0.5f, 0.6f), new Vector2(820, 560));
            K.RibbonTitle(card, "Title", "LEVEL COMPLETE!", Top, 860, K.Mint, 76);

            var stars = new Image[3];
            for (var i = 0; i < 3; i++)
            {
                var size = i == 1 ? 230f : 180f;
                var pos = new Vector2((i - 1) * 220f, i == 1 ? 85f : 55f);
                Stars(card, $"Slot{i + 1}", Mid, pos, size).color = K.StarEmpty;
                stars[i] = Stars(card, $"Star{i + 1}", Mid, pos, size);
            }
            var score = K.Txt(card, "Score", "0.0s", 110, Mid, new Vector2(700, 140), K.Ink);
            score.rectTransform.anchoredPosition = new Vector2(0f, -120f);
            var newBest = K.Pill(card, "NewBest", "NEW BEST!", Mid, new Vector2(300, 76), K.Sun.Face, K.Sun.Text, 40);
            var nb = (RectTransform)newBest.transform.parent;
            nb.anchoredPosition = new Vector2(0f, -220f);

            var next = K.Btn(panel, "NextButton", "NEXT", new Vector2(0.5f, 0.33f), new Vector2(720, 190), K.Coral, 88, out _, I.Play);
            var retry = K.Btn(panel, "RetryButton", "RETRY", new Vector2(0.285f, 0.195f), new Vector2(430, 140), K.Sky, 52, out _, I.Retry);
            var menu = K.Btn(panel, "MenuButton", "MENU", new Vector2(0.715f, 0.195f), new Vector2(430, 140), K.Cream, 52, out _, I.Home);

            var so = new SerializedObject(s);
            var starsProp = so.FindProperty("_stars");
            starsProp.arraySize = 3;
            for (var i = 0; i < 3; i++) starsProp.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
            so.FindProperty("_starOn").colorValue = K.Star;
            so.FindProperty("_starOff").colorValue = new Color(1f, 1f, 1f, 0f); // the slot behind shows the empty star
            so.FindProperty("_scoreFormat").stringValue = "{0:0.0}s";
            so.FindProperty("_scoreScale").floatValue = 0.1f;
            so.FindProperty("_starSfx").objectReferenceValue = _audio != null ? _audio.Star : null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtil.Set(s, "_scoreText", score);
            EditorUtil.Set(s, "_newBestLabel", nb.gameObject);
            EditorUtil.Set(s, "_nextButton", next);
            EditorUtil.Set(s, "_retryButton", retry);
            EditorUtil.Set(s, "_menuButton", menu);
            K.Intro(s, panel, 0.9f);
        }

        // ---------- Level map ----------
        private static void BuildLevelMap(LevelMapScreen s, RectTransform c, LevelCatalog catalog)
        {
            K.SkyBackground(s.transform);
            var back = Header(c, "LEVELS");
            var total = K.Pill(c, "TotalStars", "0 / 30", new Vector2(1f, 1f), new Vector2(250, 86), K.Paper, K.Ink, 42, K.StarSprite, K.Star);
            ((RectTransform)total.transform.parent).anchoredPosition = new Vector2(-150f, -105f);

            // Scrolling grid (30+ levels later).
            const int columns = 4;
            const float cellW = 220f, cellH = 236f, gapX = 26f, gapY = 34f;
            var count = catalog.Count;
            var rows = (count + columns - 1) / columns;
            var viewport = K.Rect(c, "Viewport", Mid, Vector2.zero);
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(0f, 40f);
            viewport.offsetMax = new Vector2(0f, -200f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0); // drag anywhere to scroll
            var content = K.Rect(viewport, "Grid", Top, new Vector2(1080f, rows * (cellH + gapY) + 60f));
            content.pivot = Top;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.viewport = viewport;

            var cells = new LevelMapScreen.Cell[count];
            for (var i = 0; i < count; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var button = K.Btn(content, $"Level{i + 1}", null, Top, new Vector2(cellW, cellH), K.Cream, 10, out _);
                var rt = (RectTransform)button.transform;
                rt.anchoredPosition = new Vector2((col - (columns - 1) * 0.5f) * (cellW + gapX), -40f - cellH * 0.5f - row * (cellH + gapY));
                var face = button.transform.Find("Face");
                var group = button.gameObject.AddComponent<CanvasGroup>();
                var number = K.Txt(face, "Number", (i + 1).ToString(), 96, new Vector2(0.5f, 0.6f), new Vector2(200, 130), K.Ink);
                var lockIcon = K.Img(face, "Lock", I.Lock, K.InkSoft, new Vector2(0.5f, 0.6f), new Vector2(90, 90));
                var stars = new Image[3];
                for (var k = 0; k < 3; k++)
                {
                    stars[k] = Stars(face, $"Star{k + 1}", new Vector2(0.5f, 0.2f), new Vector2((k - 1) * 58f, k == 1 ? 8f : 0f), 56f);
                }
                cells[i] = new LevelMapScreen.Cell
                {
                    Button = button, Number = number, Stars = stars, Group = group, Lock = lockIcon.gameObject,
                    Face = face.GetComponent<Image>(),
                };
            }

            var so = new SerializedObject(s);
            so.FindProperty("_catalog").objectReferenceValue = catalog;
            so.FindProperty("_backButton").objectReferenceValue = back;
            so.FindProperty("_totalStars").objectReferenceValue = total;
            so.FindProperty("_starOn").colorValue = K.Star;
            so.FindProperty("_starOff").colorValue = K.StarEmpty;
            so.FindProperty("_currentFace").colorValue = K.Sun.Face;
            so.FindProperty("_face").colorValue = K.Paper;
            var cellsProp = so.FindProperty("_cells");
            cellsProp.arraySize = cells.Length;
            for (var i = 0; i < cells.Length; i++)
            {
                var e = cellsProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Button").objectReferenceValue = cells[i].Button;
                e.FindPropertyRelative("Number").objectReferenceValue = cells[i].Number;
                e.FindPropertyRelative("Group").objectReferenceValue = cells[i].Group;
                e.FindPropertyRelative("Lock").objectReferenceValue = cells[i].Lock;
                e.FindPropertyRelative("Face").objectReferenceValue = cells[i].Face;
                var starsProp = e.FindPropertyRelative("Stars");
                starsProp.arraySize = 3;
                for (var k = 0; k < 3; k++) starsProp.GetArrayElementAtIndex(k).objectReferenceValue = cells[i].Stars[k];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            K.Intro(s, content, 0.95f);
        }

        // ---------- Hangar ----------
        private static void BuildHangar(HangarScreen s, RectTransform c, PlaneCatalog planes, LevelCatalog levels)
        {
            K.SkyBackground(s.transform);
            var back = Header(c, "HANGAR");

            // Detail card.
            var card = K.Card(c, "Detail", new Vector2(0.5f, 0.705f), new Vector2(1000, 450));
            var stage = K.Img(card, "Stage", K.Circle, K.PaperDeep, Mid, new Vector2(320, 320));
            stage.rectTransform.anchoredPosition = new Vector2(-305f, 35f);
            var thumb = K.Img(card, "Thumbnail", null, Color.white, Mid, new Vector2(330, 300));
            thumb.preserveAspect = true;
            thumb.rectTransform.anchoredPosition = new Vector2(-305f, 35f);
            var name = K.Txt(card, "Name", "Plane", 44, Mid, new Vector2(400, 64), K.Ink);
            name.rectTransform.anchoredPosition = new Vector2(-305f, -170f);
            name.enableAutoSizing = true;
            name.fontSizeMin = 26;
            name.fontSizeMax = 44;

            var bars = new HangarScreen.StatBar[StatNames.Length];
            for (var k = 0; k < StatNames.Length; k++)
            {
                var y = 172f - k * 48f;
                var label = K.Txt(card, $"{StatNames[k]}Label", StatNames[k], 30, Mid, new Vector2(230, 44), K.InkSoft);
                label.alignment = TextAlignmentOptions.Left;
                label.rectTransform.anchoredPosition = new Vector2(-15f, y);
                bars[k] = new HangarScreen.StatBar { Pips = new Image[5] };
                for (var p = 0; p < 5; p++)
                {
                    var pip = K.Img(card, $"{StatNames[k]}Pip{p + 1}", K.Rounded, Color.white, Mid, new Vector2(50, 28), true, 4f);
                    pip.rectTransform.anchoredPosition = new Vector2(160f + p * 58f, y);
                    bars[k].Pips[p] = pip;
                }
            }
            var info = K.Txt(card, "Info", "", 34, Mid, new Vector2(560, 50), K.Coral.Face);
            info.rectTransform.anchoredPosition = new Vector2(200f, -78f);
            var select = K.Btn(card, "SelectButton", "SELECT", Mid, new Vector2(440, 120), K.Mint, 52, out var selectLabel, I.Check);
            ((RectTransform)select.transform).anchoredPosition = new Vector2(200f, -150f);
            var tryIt = K.Btn(card, "TryButton", "TRY IT", Mid, new Vector2(440, 120), K.Sun, 52, out _, I.Video);
            ((RectTransform)tryIt.transform).anchoredPosition = new Vector2(200f, -150f);

            // Grid: 5 × 4.
            const int columns = 5;
            const float w = 188f, h = 212f, gx = 14f, gy = 26f;
            var cells = new HangarScreen.Cell[planes.Count];
            for (var i = 0; i < planes.Count; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var button = K.Btn(c, $"Plane{i + 1}", null, new Vector2(0.5f, 0.505f), new Vector2(w, h), K.Cream, 10, out _, null, 1.6f);
                ((RectTransform)button.transform).anchoredPosition = new Vector2((col - (columns - 1) * 0.5f) * (w + gx), -row * (h + gy));
                var face = button.transform.Find("Face");
                var group = button.gameObject.AddComponent<CanvasGroup>();
                var cellThumb = K.Img(face, "Thumbnail", null, Color.white, new Vector2(0.5f, 0.6f), new Vector2(170, 150));
                cellThumb.preserveAspect = true;
                var lockIcon = K.Img(face, "Lock", I.Lock, Color.white, new Vector2(0.5f, 0.62f), new Vector2(54, 54));
                var cellName = K.Txt(face, "Name", planes.Get(i).DisplayName, 24, new Vector2(0.5f, 0.13f), new Vector2(176, 44), K.Ink);
                cellName.enableAutoSizing = true;
                cellName.fontSizeMin = 15;
                cellName.fontSizeMax = 24;
                var condition = K.Txt(face, "Condition", "", 26, new Vector2(0.5f, 0.33f), new Vector2(176, 40), K.Coral.Face);
                var badge = K.NewBadge(face, new Vector2(1f, 1f), new Vector2(-20f, -4f), 0.8f);
                cells[i] = new HangarScreen.Cell
                {
                    Button = button, Thumbnail = cellThumb, Name = cellName, Condition = condition, Group = group,
                    NewBadge = badge, Frame = face.GetComponent<Image>(), Lock = lockIcon.gameObject,
                };
            }

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
            so.FindProperty("_pipOn").colorValue = K.Sky.Face;
            so.FindProperty("_pipOff").colorValue = new Color(0.17f, 0.23f, 0.33f, 0.12f);
            so.FindProperty("_frameOn").colorValue = K.Sun.Face;
            so.FindProperty("_frameOff").colorValue = K.Paper;
            so.FindProperty("_lockedTint").colorValue = new Color(0.17f, 0.23f, 0.33f, 0.35f);
            var statsProp = so.FindProperty("_stats");
            statsProp.arraySize = bars.Length;
            for (var k = 0; k < bars.Length; k++)
            {
                var pips = statsProp.GetArrayElementAtIndex(k).FindPropertyRelative("Pips");
                pips.arraySize = 5;
                for (var p = 0; p < 5; p++) pips.GetArrayElementAtIndex(p).objectReferenceValue = bars[k].Pips[p];
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
                e.FindPropertyRelative("Lock").objectReferenceValue = cells[i].Lock;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            K.Intro(s, card, 0.92f);
        }

        // ---------- Daily check-in ----------
        private static void BuildCheckIn(CheckInScreen s, RectTransform c)
        {
            K.Dim(s.transform);
            var card = K.Card(c, "Card", Mid, new Vector2(920, 1060));
            var title = K.RibbonTitle(card, "Ribbon", "DAY 1", Top, 640, K.Sun, 84);
            K.Txt(card, "Subtitle", "Daily check-in", 46, Top, new Vector2(800, 70), K.InkSoft).rectTransform.anchoredPosition = new Vector2(0f, -130f);

            var days = new Image[14];
            for (var i = 0; i < days.Length; i++)
            {
                var col = i % 7;
                var row = i / 7;
                var day = i + 1;
                var reward = System.Array.IndexOf(DailyCheckIn.RewardDays, day) >= 0;
                var box = K.Img(card, $"Day{day}", K.Rounded, Color.white, Top, new Vector2(104, 112), true, 2.6f);
                box.rectTransform.anchoredPosition = new Vector2((col - 3) * 118f, -250f - row * 132f);
                days[i] = box;
                if (reward)
                {
                    K.Img(box.transform, "Gift", I.Gift, K.Coral.Face, new Vector2(0.5f, 0.56f), new Vector2(66, 66));
                    K.Txt(box.transform, "Number", day.ToString(), 26, new Vector2(0.5f, 0.14f), new Vector2(100, 30), K.Ink);
                }
                else K.Txt(box.transform, "Number", day.ToString(), 44, Mid, new Vector2(100, 100), K.Ink);
            }

            var rays = K.Img(card, "Rays", Rays, new Color(1f, 0.85f, 0.4f, 0.6f), Mid, new Vector2(520, 520));
            rays.rectTransform.anchoredPosition = new Vector2(0f, -140f);
            var thumb = K.Img(card, "RewardThumbnail", null, Color.white, Mid, new Vector2(330, 280));
            thumb.preserveAspect = true;
            thumb.rectTransform.anchoredPosition = new Vector2(0f, -140f);
            var message = K.Txt(card, "Message", "", 46, new Vector2(0.5f, 0f), new Vector2(840, 80), K.Ink);
            message.rectTransform.anchoredPosition = new Vector2(0f, 240f);
            message.enableAutoSizing = true;
            message.fontSizeMin = 28;
            message.fontSizeMax = 46;
            var cont = K.Btn(card, "ContinueButton", "COLLECT", new Vector2(0.5f, 0f), new Vector2(480, 140), K.Mint, 60, out _, I.Check);
            ((RectTransform)cont.transform).anchoredPosition = new Vector2(0f, 110f);

            var so = new SerializedObject(s);
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_message").objectReferenceValue = message;
            so.FindProperty("_rewardThumbnail").objectReferenceValue = thumb;
            so.FindProperty("_continueButton").objectReferenceValue = cont;
            so.FindProperty("_dayOn").colorValue = K.Mint.Face;
            so.FindProperty("_sfx").objectReferenceValue = _audio != null ? _audio.CheckIn : null;
            so.FindProperty("_dayOff").colorValue = K.PaperDeep;
            var daysProp = so.FindProperty("_days");
            daysProp.arraySize = days.Length;
            for (var i = 0; i < days.Length; i++) daysProp.GetArrayElementAtIndex(i).objectReferenceValue = days[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            K.Intro(s, card);
        }

        // ---------- Unlock toast (Game scene) ----------
        public static UnlockToast BuildToast(Game.Audio.AudioConfig audio)
        {
            var go = new GameObject("UnlockToast", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // above UIRoot (10)
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = K.Card(go.transform, "Panel", Top, new Vector2(840, 230));
            panel.pivot = Top;
            panel.anchoredPosition = new Vector2(0f, 260f);
            panel.Find("Face").GetComponent<Image>().raycastTarget = false;
            var stage = K.Img(panel, "Stage", K.Circle, K.PaperDeep, Mid, new Vector2(190, 190));
            stage.rectTransform.anchoredPosition = new Vector2(-290f, 0f);
            var thumb = K.Img(panel, "Thumbnail", null, Color.white, Mid, new Vector2(220, 180));
            thumb.preserveAspect = true;
            thumb.rectTransform.anchoredPosition = new Vector2(-290f, 0f);
            var heading = K.Txt(panel, "Heading", "NEW PLANE!", 64, Mid, new Vector2(520, 84), K.Coral.Face);
            heading.rectTransform.anchoredPosition = new Vector2(100f, 36f);
            var name = K.Txt(panel, "Name", "", 42, Mid, new Vector2(520, 60), K.Ink);
            name.rectTransform.anchoredPosition = new Vector2(100f, -42f);
            name.enableAutoSizing = true;
            name.fontSizeMin = 24;
            name.fontSizeMax = 42;

            var toast = go.AddComponent<UnlockToast>();
            EditorUtil.Set(toast, "_canvas", canvas);
            EditorUtil.Set(toast, "_panel", panel);
            EditorUtil.Set(toast, "_name", name);
            EditorUtil.Set(toast, "_thumbnail", thumb);
            if (audio != null) EditorUtil.Set(toast, "_sfx", audio.Unlock);
            return toast;
        }
    }
}
