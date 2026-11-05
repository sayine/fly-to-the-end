using System;
using System.IO;
using Template.Editor;
using Template.UI;
using Template.UI.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// Paper-craft UI kit: palette, the Fredoka font (TMP asset + shadow/outline materials), generated sprites
    /// (rounded card, soft shadow, circle, sky gradient, ribbon, cloud, tape) and icons, plus the primitives every
    /// Paper Flight screen is built from: cards, chunky 3D buttons, round icon buttons, pills, ribbons, badges.
    /// Everything is generated from code (no hand-made assets besides the font file).
    /// </summary>
    internal static class PaperKit
    {
        public const string Folder = "Assets/_Game/Art/UI";
        public const string FontFile = "Assets/_Game/Art/Fonts/Fredoka.ttf";
        private const string FontAssetPath = "Assets/_Game/Art/Fonts/Fredoka SDF.asset";

        // ---------- Palette ----------
        public static readonly Color Ink = Hex("#2B3A55");
        public static readonly Color InkSoft = Hex("#6B7894");
        public static readonly Color Paper = Hex("#FFF8EA");
        public static readonly Color PaperEdge = Hex("#E6D3AE");
        public static readonly Color PaperDeep = Hex("#F3E6CB");
        public static readonly Color SkyTop = Hex("#5DB8F5");
        public static readonly Color SkyBottom = Hex("#CFEBFF");
        public static readonly Color Star = Hex("#FFC93C");
        public static readonly Color StarEmpty = new Color(0.17f, 0.23f, 0.33f, 0.18f);
        public static readonly Color DimColor = new Color(0.1f, 0.14f, 0.25f, 0.62f);
        public static readonly Color ShadowColor = new Color(0.1f, 0.14f, 0.25f, 0.28f);

        public struct Tone
        {
            public Color Face, Edge, Text;
            public Tone(string face, string edge, Color text) { Face = Hex(face); Edge = Hex(edge); Text = text; }
        }

        public static readonly Tone Coral = new Tone("#FF6F5E", "#D24A3E", Color.white);
        public static readonly Tone Sun = new Tone("#FFC93C", "#DE9E14", Hex("#5A3B00"));
        public static readonly Tone Sky = new Tone("#45A6FF", "#2C7ED6", Color.white);
        public static readonly Tone Mint = new Tone("#3CCB8F", "#249E6B", Color.white);
        public static readonly Tone Grape = new Tone("#8C6CF0", "#6448C9", Color.white);
        public static readonly Tone Cream = new Tone("#FFF8EA", "#D9C29A", Hex("#2B3A55"));

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        // ---------- Assets ----------
        public static Sprite Rounded { get; private set; }
        public static Sprite Shadow { get; private set; }
        public static Sprite Circle { get; private set; }
        public static Sprite CircleShadow { get; private set; }
        public static Sprite Gradient { get; private set; }
        public static Sprite Ribbon { get; private set; }
        public static Sprite Cloud { get; private set; }
        public static Sprite Gloss { get; private set; }
        public static Sprite Fade { get; private set; }   // alpha 1 at the bottom → 0 at the top
        public static Sprite StarSprite { get; private set; }
        public static TMP_FontAsset Font { get; private set; }
        public static Material FontShadow { get; private set; }  // soft drop shadow (text on colour)
        public static Material FontTitle { get; private set; }   // thick ink outline + drop (big titles)
        public static Material FontPlain { get; private set; }   // ink on paper

        public enum TextStyle { Plain, Shadow, Title }

        /// <summary>Creates (or loads) the font, materials, sprites and icons. Call once before building screens.</summary>
        public static void Prepare(bool force)
        {
            EditorUtil.EnsureFolder(Folder);
            EditorUtil.EnsureFolder(Folder + "/Icons");
            BuildFont();
            Rounded = SpriteAsset("Rounded", 128, 44, force, p => RoundedBoxCover(p, 64f, 64f, 44f, 0f));
            Shadow = SpriteAsset("Shadow", 128, 56, force, p => RoundedBoxCover(p, 40f, 40f, 30f, 22f) * 1f);
            Circle = SpriteAsset("Circle", 128, 0, force, p => Mathf.Clamp01(62f - (p - new Vector2(64, 64)).magnitude + 0.5f));
            CircleShadow = SpriteAsset("CircleShadow", 128, 0, force, p => Soft(48f - (p - new Vector2(64, 64)).magnitude, 14f));
            Gloss = SpriteAsset("Gloss", 128, 44, force, p => RoundedBoxCover(p, 64f, 64f, 44f, 0f) * Mathf.Clamp01((p.y - 70f) / 30f) * 0.9f);
            Ribbon = SpriteAsset("Ribbon", 256, 0, force, RibbonCover, new Vector4(70, 0, 70, 0), 256, 112);
            Cloud = SpriteAsset("Cloud", 256, 0, force, CloudCover, Vector4.zero, 256, 140);
            Gradient = GradientSprite(force);
            Fade = SpriteAsset("Fade", 0, 0, force, p => Mathf.SmoothStep(1f, 0f, p.y / 128f), Vector4.zero, 4, 128);
            StarSprite = UIBuilder.StarSprite();
            PaperIcons.Build(force);
        }

        // ---------- Font ----------
        private static void BuildFont()
        {
            Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (Font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(FontFile);
                if (source == null) throw new Exception($"Missing {FontFile}");
                Font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                Font.name = "Fredoka SDF";
                AssetDatabase.CreateAsset(Font, FontAssetPath);
                Font.atlasTextures[0].name = "Fredoka SDF Atlas";
                AssetDatabase.AddObjectToAsset(Font.atlasTextures[0], Font);
                Font.material.name = "Fredoka SDF Material";
                AssetDatabase.AddObjectToAsset(Font.material, Font);
                // Pre-bake every character the UI uses so the atlas ships filled.
                Font.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~×•…", out _);
                EditorUtility.SetDirty(Font);
                AssetDatabase.SaveAssets();
            }
            FontPlain = FontMaterial("Plain", m => { });
            FontShadow = FontMaterial("Shadow", m =>
            {
                m.EnableKeyword("UNDERLAY_ON");
                m.SetColor("_UnderlayColor", new Color(0.1f, 0.14f, 0.25f, 0.35f));
                m.SetFloat("_UnderlayOffsetY", -0.9f);
                m.SetFloat("_UnderlaySoftness", 0.15f);
                m.SetFloat("_FaceDilate", 0.1f);
            });
            FontTitle = FontMaterial("Title", m =>
            {
                m.EnableKeyword("OUTLINE_ON");
                m.EnableKeyword("UNDERLAY_ON");
                m.SetFloat("_FaceDilate", 0.3f);
                m.SetFloat("_OutlineWidth", 0.3f);
                m.SetColor("_OutlineColor", Ink);
                m.SetColor("_UnderlayColor", new Color(0.1f, 0.14f, 0.25f, 0.55f));
                m.SetFloat("_UnderlayOffsetY", -1f);
                m.SetFloat("_UnderlayDilate", 0.3f);
                m.SetFloat("_UnderlaySoftness", 0.05f);
            });
            MakeDefaultFont();
        }

        private static Material FontMaterial(string name, Action<Material> setup)
        {
            var path = $"Assets/_Game/Art/Fonts/Fredoka SDF {name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Font.material);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Font.material.shader;
            mat.SetTexture("_MainTex", Font.atlasTexture);
            mat.SetFloat("_TextureWidth", Font.atlasWidth);
            mat.SetFloat("_TextureHeight", Font.atlasHeight);
            mat.SetFloat("_GradientScale", Font.atlasPadding + 1);
            setup(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Fredoka becomes TMP's default, so runtime and template-built text match.</summary>
        private static void MakeDefaultFont()
        {
            var settings = TMP_Settings.instance;
            if (settings == null) return;
            var so = new SerializedObject(settings);
            var prop = so.FindProperty("m_defaultFontAsset");
            if (prop == null || prop.objectReferenceValue == Font) return;
            prop.objectReferenceValue = Font;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        // ---------- Sprite rasterising ----------
        private static float Soft(float d, float blur) => Mathf.Clamp01(0.5f + d / Mathf.Max(0.5f, blur));

        /// <summary>Coverage of a rounded box (half size hx,hy, corner r) centred in a 128 canvas, with optional blur.</summary>
        private static float RoundedBoxCover(Vector2 p, float hx, float hy, float r, float blur)
        {
            var q = new Vector2(Mathf.Abs(p.x - 64f) - (hx - r), Mathf.Abs(p.y - 64f) - (hy - r));
            var outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
            return blur > 0f ? Soft(-outside, blur) : Mathf.Clamp01(0.5f - outside);
        }

        private static float RibbonCover(Vector2 p)
        {
            // Banner with V-notched tails; p in 256×112 pixels.
            var y = p.y - 56f;
            var x = p.x < 128f ? p.x : 256f - p.x; // mirror
            var body = Mathf.Abs(y) <= 44f ? 1f : 0f;
            var notch = x < 30f - Mathf.Abs(y) * 0.6f ? 0f : 1f; // V cut at the ends
            return body * notch;
        }

        private static float CloudCover(Vector2 p)
        {
            float C(float cx, float cy, float r) => Mathf.Clamp01(r - (p - new Vector2(cx, cy)).magnitude + 0.5f);
            var v = Mathf.Max(C(70, 50, 40), Mathf.Max(C(120, 70, 55), Mathf.Max(C(175, 55, 42), C(210, 40, 28))));
            var flat = p.y > 12f && p.y < 52f && p.x > 40f && p.x < 225f ? 1f : 0f;
            return Mathf.Max(v, flat);
        }

        /// <summary>Writes a white sprite whose alpha is cover(pixel centre). border = 9-slice border on all sides.</summary>
        public static Sprite SpriteAsset(string name, int size, int border, bool force, Func<Vector2, float> cover,
            Vector4? borders = null, int width = 0, int height = 0, string folder = null)
        {
            var path = $"{folder ?? Folder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null && !force) return existing;
            var w = width > 0 ? width : size;
            var h = height > 0 ? height : size;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                // 3×3 supersampling for smooth edges.
                var a = 0f;
                for (var sy = 0; sy < 3; sy++)
                for (var sx = 0; sx < 3; sx++)
                    a += cover(new Vector2(x + (sx + 0.5f) / 3f, y + (sy + 0.5f) / 3f));
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a / 9f) * 255f));
            }
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return ImportSprite(path, borders ?? new Vector4(border, border, border, border));
        }

        private static Sprite GradientSprite(bool force)
        {
            var path = $"{Folder}/SkyGradient.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null && !force) return existing;
            const int h = 256;
            var tex = new Texture2D(4, h, TextureFormat.RGBA32, false);
            for (var y = 0; y < h; y++)
            {
                var c = Color.Lerp(SkyBottom, SkyTop, Mathf.SmoothStep(0f, 1f, y / (h - 1f)));
                for (var x = 0; x < 4; x++) tex.SetPixel(x, y, c);
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return ImportSprite(path, Vector4.zero);
        }

        private static Sprite ImportSprite(string path, Vector4 border)
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---------- Primitives ----------
        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Img(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 size, bool sliced = false, float cornerScale = 1f)
        {
            var rt = Rect(parent, name, anchor, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sliced)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = cornerScale;
            }
            return img;
        }

        public static void Fill(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static TMP_Text Txt(Transform parent, string name, string text, float size, Vector2 anchor, Vector2 box, Color color, TextStyle style = TextStyle.Plain)
        {
            var rt = Rect(parent, name, anchor, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font;
            t.fontSharedMaterial = style switch { TextStyle.Shadow => FontShadow, TextStyle.Title => FontTitle, _ => FontPlain };
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold; // Fredoka ships as one variable font: TMP bold gives the chunky weight
            t.alignment = TextAlignmentOptions.Center;
            t.color = color;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.characterSpacing = style == TextStyle.Title ? 2f : 0f;
            return t;
        }

        /// <summary>Sky gradient with drifting-looking clouds (static), for full-screen pages.</summary>
        public static void SkyBackground(Transform screen)
        {
            var bg = Img(screen, "Background", Gradient, Color.white, Vector2.zero, Vector2.zero);
            Fill(bg.rectTransform);
            bg.raycastTarget = true;
            bg.transform.SetAsFirstSibling();
            void C(string n, Vector2 anchor, float scale, float alpha)
            {
                var c = Img(bg.transform, n, Cloud, new Color(1f, 1f, 1f, alpha), anchor, new Vector2(256, 140) * scale);
                c.preserveAspect = true;
            }
            C("Cloud1", new Vector2(0.12f, 0.9f), 1.6f, 0.55f);
            C("Cloud2", new Vector2(0.92f, 0.78f), 1.2f, 0.45f);
            C("Cloud3", new Vector2(0.05f, 0.42f), 1.1f, 0.35f);
            C("Cloud4", new Vector2(0.95f, 0.22f), 1.8f, 0.4f);
            C("Cloud5", new Vector2(0.4f, 0.04f), 2.4f, 0.5f);
        }

        /// <summary>Dim veil behind popups (blocks clicks to the screen below).</summary>
        public static void Dim(Transform screen)
        {
            var bg = Img(screen, "Background", null, DimColor, Vector2.zero, Vector2.zero);
            Fill(bg.rectTransform);
            bg.raycastTarget = true;
            bg.transform.SetAsFirstSibling();
        }

        /// <summary>Paper card with a soft drop shadow. Returns the card face; add content to it.</summary>
        public static RectTransform Card(Transform parent, string name, Vector2 anchor, Vector2 size, Color? color = null, float corner = 1f)
        {
            var root = Rect(parent, name, anchor, size);
            var shadow = Img(root, "Shadow", Shadow, ShadowColor, new Vector2(0.5f, 0.5f), Vector2.zero, true, corner);
            Fill(shadow.rectTransform, -26f);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -16f);
            var edge = Img(root, "Edge", Rounded, PaperEdge, Vector2.zero, Vector2.zero, true, corner);
            Fill(edge.rectTransform);
            edge.rectTransform.offsetMax = new Vector2(0f, -8f);
            edge.rectTransform.offsetMin = new Vector2(0f, -8f);
            var face = Img(root, "Face", Rounded, color ?? Paper, Vector2.zero, Vector2.zero, true, corner);
            Fill(face.rectTransform);
            face.raycastTarget = true; // cards swallow taps (no click-through to the screen behind)
            return root;
        }

        /// <summary>Chunky button: soft shadow, darker edge, face that sinks when pressed, gloss, icon and label.</summary>
        public static Button Btn(Transform parent, string name, string label, Vector2 anchor, Vector2 size, Tone tone, float fontSize,
            out TMP_Text text, Sprite icon = null, float corner = 1f)
        {
            const float depth = 14f;
            var root = Rect(parent, name, anchor, size);
            var shadow = Img(root, "Shadow", Shadow, ShadowColor, Vector2.zero, Vector2.zero, true, corner);
            Fill(shadow.rectTransform, -20f);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -depth - 8f);
            var edge = Img(root, "Edge", Rounded, tone.Edge, Vector2.zero, Vector2.zero, true, corner);
            Fill(edge.rectTransform);
            edge.rectTransform.offsetMin = new Vector2(0f, -depth);
            edge.rectTransform.offsetMax = new Vector2(0f, -depth);
            var face = Img(root, "Face", Rounded, tone.Face, Vector2.zero, Vector2.zero, true, corner);
            Fill(face.rectTransform);
            face.raycastTarget = true;
            var gloss = Img(face.transform, "Gloss", Gloss, new Color(1f, 1f, 1f, 0.28f), Vector2.zero, Vector2.zero, true, corner);
            Fill(gloss.rectTransform, 6f);

            var content = Rect(face.transform, "Content", Vector2.zero, Vector2.zero);
            Fill(content);
            var iconSize = Mathf.Min(size.y * 0.5f, fontSize * 1.15f);
            text = null;
            Image iconImage = null;
            if (icon != null)
            {
                iconImage = Img(content, "Icon", icon, tone.Text, new Vector2(0.5f, 0.5f), new Vector2(iconSize, iconSize));
                iconImage.preserveAspect = true;
            }
            if (!string.IsNullOrEmpty(label))
            {
                text = Txt(content, "Label", label, fontSize, new Vector2(0.5f, 0.5f), new Vector2(size.x - 40f, size.y), tone.Text,
                    tone.Text == Color.white ? TextStyle.Shadow : TextStyle.Plain);
                text.enableAutoSizing = true;
                text.fontSizeMin = fontSize * 0.6f;
                text.fontSizeMax = fontSize;
                text.rectTransform.anchoredPosition = new Vector2(0f, 4f);
                if (iconImage != null)
                {
                    // Icon left of the label, the pair centred.
                    var labelWidth = Mathf.Min(text.GetPreferredValues(label).x, size.x - iconSize - 70f);
                    var total = iconSize + 18f + labelWidth;
                    iconImage.rectTransform.anchoredPosition = new Vector2(-total * 0.5f + iconSize * 0.5f, 4f);
                    text.rectTransform.sizeDelta = new Vector2(labelWidth + 4f, size.y);
                    text.rectTransform.anchoredPosition = new Vector2(total * 0.5f - labelWidth * 0.5f, 4f);
                }
            }
            else if (iconImage != null) iconImage.rectTransform.anchoredPosition = new Vector2(0f, 3f);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            var colors = button.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.7f);
            button.colors = colors;
            var press = root.gameObject.AddComponent<PressableButton>();
            EditorUtil.Set(press, "_face", face.rectTransform);
            var so = new SerializedObject(press);
            so.FindProperty("_depth").floatValue = depth - 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        /// <summary>Round icon button (pause, settings, back, home…).</summary>
        public static Button IconBtn(Transform parent, string name, Sprite icon, Vector2 anchor, float diameter, Tone tone)
        {
            var button = Btn(parent, name, null, anchor, new Vector2(diameter, diameter), tone, diameter * 0.45f, out _, icon,
                128f / diameter * 0.98f);
            var iconImage = button.transform.Find("Face/Content/Icon").GetComponent<Image>();
            iconImage.rectTransform.sizeDelta = Vector2.one * diameter * 0.5f;
            return button;
        }

        /// <summary>Rounded pill (counters, badges). Returns the label.</summary>
        public static TMP_Text Pill(Transform parent, string name, string text, Vector2 anchor, Vector2 size, Color color, Color textColor,
            float fontSize, Sprite icon = null, Color? iconColor = null)
        {
            var bg = Img(parent, name, Rounded, color, anchor, size, true, 128f / size.y * 0.98f);
            var label = Txt(bg.transform, "Label", text, fontSize, new Vector2(0.5f, 0.5f), size, textColor,
                textColor == Color.white ? TextStyle.Shadow : TextStyle.Plain);
            label.rectTransform.anchoredPosition = new Vector2(icon != null ? size.y * 0.3f : 0f, 2f);
            if (icon != null)
            {
                var ic = Img(bg.transform, "Icon", icon, iconColor ?? textColor, new Vector2(0f, 0.5f), Vector2.one * size.y * 0.78f);
                ic.preserveAspect = true;
                ic.rectTransform.anchoredPosition = new Vector2(size.y * 0.55f, 0f);
            }
            return label;
        }

        /// <summary>Banner ribbon with a title (screen headers, results).</summary>
        public static TMP_Text RibbonTitle(Transform parent, string name, string text, Vector2 anchor, float width, Tone tone, float fontSize = 76f)
        {
            var root = Rect(parent, name, anchor, new Vector2(width, 150f));
            var shadow = Img(root, "Shadow", Ribbon, ShadowColor, new Vector2(0.5f, 0.5f), new Vector2(width, 150f), true);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            // Folded tails behind the band.
            var tailL = Img(root, "TailL", Ribbon, tone.Edge, new Vector2(0f, 0.5f), new Vector2(200f, 120f), true);
            tailL.rectTransform.anchoredPosition = new Vector2(30f, -26f);
            var tailR = Img(root, "TailR", Ribbon, tone.Edge, new Vector2(1f, 0.5f), new Vector2(200f, 120f), true);
            tailR.rectTransform.anchoredPosition = new Vector2(-30f, -26f);
            var band = Rect(root, "Band", new Vector2(0.5f, 0.5f), new Vector2(width - 120f, 140f));
            var bandImg = band.gameObject.AddComponent<Image>();
            bandImg.sprite = Rounded;
            bandImg.type = Image.Type.Sliced;
            bandImg.pixelsPerUnitMultiplier = 2.2f;
            bandImg.color = tone.Face;
            bandImg.raycastTarget = false;
            var gloss = Img(band, "Gloss", Gloss, new Color(1f, 1f, 1f, 0.22f), Vector2.zero, Vector2.zero, true, 2.2f);
            Fill(gloss.rectTransform, 6f);
            var label = Txt(band, "Label", text, fontSize, new Vector2(0.5f, 0.5f), new Vector2(width - 180f, 130f), Color.white, TextStyle.Title);
            label.enableAutoSizing = true;
            label.fontSizeMin = fontSize * 0.6f;
            label.fontSizeMax = fontSize;
            label.rectTransform.anchoredPosition = new Vector2(0f, 4f);
            return label;
        }

        /// <summary>Red "NEW" sticker, tilted.</summary>
        public static GameObject NewBadge(Transform parent, Vector2 anchor, Vector2 offset, float scale = 1f)
        {
            var label = Pill(parent, "NewBadge", "NEW", anchor, new Vector2(96f, 46f) * scale, Coral.Face, Color.white, 28f * scale);
            var rt = (RectTransform)label.transform.parent;
            rt.anchoredPosition = offset;
            rt.localRotation = Quaternion.Euler(0f, 0f, -10f);
            return rt.gameObject;
        }

        /// <summary>Screen entrance: fade + pop of the given card.</summary>
        public static void Intro(Component screen, RectTransform pop, float fromScale = 0.86f)
        {
            var intro = screen.gameObject.GetComponent<ScreenIntro>() ?? screen.gameObject.AddComponent<ScreenIntro>();
            if (pop != null) EditorUtil.Set(intro, "_pop", pop);
            var so = new SerializedObject(intro);
            so.FindProperty("_fromScale").floatValue = fromScale;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetColor(Object target, string field, Color value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
