using System.IO;
using Template.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The app icon, made from game assets: the Classic Dart (its real mesh and paper material, rendered in 3D)
    /// climbing across the menu's sky, with paper clouds, a dashed flight trail and a soft drop shadow. Composited on
    /// the CPU into an opaque 1024 px PNG (iOS icons have no alpha) and set as the default player icon.
    /// Needs a GPU: skipped in -nographics runs (the existing icon is kept).
    /// </summary>
    internal static class AppIconBuilder
    {
        public const string Path = "Assets/_Game/Art/AppIcon/AppIcon.png";
        private const int Size = 1024;

        [MenuItem("Tools/Game/Build App Icon")]
        public static void BuildMenu() => Build(true);

        public static void Build(bool force)
        {
            if (!force && File.Exists(Path)) { Apply(); return; }
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                if (File.Exists(Path)) Apply();
                return;
            }
            // The folded 3D dart (wings with dihedral and a keel) reads as a paper plane from any angle;
            // the hangar meshes are flat sheets. White paper material from the Classic Dart.
            var mesh = DartMesh;
            var material = PaperMaterial;
            var px = Compose(mesh, material, Rotation, Elevation, Tint);
            Save(px);
        }

        // Chosen look (see the variants rendered while designing): tuned in code, re-render with Tools/Game/Build App Icon.
        private static readonly Vector3 Rotation = new Vector3(-25f, 45f, -15f);
        private const float Elevation = 15f;
        private const float Tint = 0.95f;

        /// <summary>Full icon for one plane pose; also used to try variants.</summary>
        public static Color[] Compose(Mesh mesh, Material material, Vector3 rotation, float elevation, float tint)
        {
            var planeLayer = RenderPlane(mesh, material, rotation, elevation, tint);
            var px = new Color[Size * Size];
            Background(px);
            Clouds(px);
            Trail(px);
            Shadow(px, planeLayer);
            Over(px, planeLayer);
            return px;
        }

        public static Mesh DartMesh => AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Game/Art/Meshes/PaperPlane.asset");
        public static Material PaperMaterial => AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Plane.mat");

        public static void SavePng(Color[] px, string path)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void Save(Color[] px)
        {
            EditorUtil.EnsureFolder("Assets/_Game/Art/AppIcon");
            SavePng(px, Path);
            AssetDatabase.ImportAsset(Path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Size;
            importer.SaveAndReimport();
            Apply();
            Log.Info($"Saved {Path}");
        }

        /// <summary>Default icon for every platform (iOS uses it for all its icon sizes).</summary>
        private static void Apply()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
            if (icon == null) return;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }

        // ---------- Layers ----------
        private static Color Lerp(Color a, Color b, float t) => Color.LerpUnclamped(a, b, t);

        private static void Background(Color[] px)
        {
            var top = PaperKit.Hex("#3FA2F0");
            var bottom = PaperKit.Hex("#BFE6FF");
            var glow = new Vector2(Size * 0.56f, Size * 0.58f);
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var c = Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, y / (Size - 1f)));
                // Soft sunlit glow behind the plane.
                var d = (new Vector2(x, y) - glow).magnitude / (Size * 0.55f);
                c = Lerp(c, Color.white, Mathf.Clamp01(1f - d) * 0.35f);
                px[y * Size + x] = c;
            }
        }

        private static void Clouds(Color[] px)
        {
            Cloud(px, new Vector2(170f, 250f), 300f, 0.95f);
            Cloud(px, new Vector2(840f, 150f), 380f, 0.9f);
            Cloud(px, new Vector2(860f, 820f), 220f, 0.55f);
            Cloud(px, new Vector2(150f, 760f), 170f, 0.45f);
        }

        /// <summary>Flat-bottomed paper cloud: three overlapping puffs on a rounded base, with a faint underside shade.</summary>
        private static void Cloud(Color[] px, Vector2 c, float w, float alpha)
        {
            var shade = PaperKit.Hex("#DCEBF7");
            float Puff(Vector2 p, Vector2 o, float r) => Mathf.Clamp01((r - (p - o).magnitude) / 2f + 0.5f);
            var x0 = Mathf.Max(0, (int)(c.x - w * 0.6f));
            var x1 = Mathf.Min(Size - 1, (int)(c.x + w * 0.6f));
            var y0 = Mathf.Max(0, (int)(c.y - w * 0.2f));
            var y1 = Mathf.Min(Size - 1, (int)(c.y + w * 0.5f));
            for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                var p = new Vector2(x, y);
                var a = Mathf.Max(Puff(p, c + new Vector2(-w * 0.25f, w * 0.05f), w * 0.2f),
                        Mathf.Max(Puff(p, c + new Vector2(0f, w * 0.15f), w * 0.27f), Puff(p, c + new Vector2(w * 0.27f, w * 0.04f), w * 0.18f)));
                // Rounded flat base (a capsule) joining the puffs.
                var bx = Mathf.Clamp(p.x, c.x - w * 0.33f, c.x + w * 0.33f);
                var baseCap = Mathf.Clamp01((w * 0.1f - (p - new Vector2(bx, c.y - w * 0.02f)).magnitude) / 2f + 0.5f);
                a = Mathf.Max(a, baseCap);
                if (a <= 0f) continue;
                var underside = Mathf.Clamp01(1f - (p.y - (c.y - w * 0.12f)) / (w * 0.16f));
                var col = Lerp(Color.white, shade, underside * 0.8f);
                var i = y * Size + x;
                px[i] = Lerp(px[i], col, a * alpha);
            }
        }

        /// <summary>Dashed swoosh from the bottom-left up to the plane's tail.</summary>
        private static void Trail(Color[] px)
        {
            var p0 = new Vector2(70f, 170f);
            var p1 = new Vector2(320f, 110f);
            var p2 = new Vector2(380f, 390f);
            Vector2 B(float t) => (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
            const int dashes = 9;
            for (var k = 0; k < dashes; k++)
            {
                var ta = (k + 0.15f) / dashes;
                var tb = (k + 0.6f) / dashes;
                var a = B(ta);
                var b = B(tb);
                var width = Mathf.Lerp(9f, 15f, k / (float)dashes);
                var alpha = Mathf.Lerp(0.45f, 0.95f, k / (float)dashes);
                Capsule(px, a, b, width, Color.white, alpha);
            }
        }

        private static void Capsule(Color[] px, Vector2 a, Vector2 b, float r, Color col, float alpha)
        {
            var x0 = Mathf.Max(0, (int)(Mathf.Min(a.x, b.x) - r - 2));
            var x1 = Mathf.Min(Size - 1, (int)(Mathf.Max(a.x, b.x) + r + 2));
            var y0 = Mathf.Max(0, (int)(Mathf.Min(a.y, b.y) - r - 2));
            var y1 = Mathf.Min(Size - 1, (int)(Mathf.Max(a.y, b.y) + r + 2));
            var ab = b - a;
            for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                var p = new Vector2(x, y);
                var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                var d = (p - (a + ab * t)).magnitude;
                var cover = Mathf.Clamp01(r - d + 0.5f);
                if (cover <= 0f) continue;
                var i = y * Size + x;
                px[i] = Lerp(px[i], col, cover * alpha);
            }
        }

        private static void Shadow(Color[] px, Color[] plane)
        {
            // Blurred plane alpha, offset down-right, as a soft navy shadow.
            var alpha = new float[Size * Size];
            const int ox = 26, oy = -34;
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var sx = x - ox;
                var sy = y - oy;
                if (sx < 0 || sy < 0 || sx >= Size || sy >= Size) continue;
                alpha[y * Size + x] = plane[sy * Size + sx].a;
            }
            for (var pass = 0; pass < 3; pass++) BoxBlur(alpha, 14);
            var navy = new Color(0.1f, 0.16f, 0.3f);
            for (var i = 0; i < px.Length; i++) px[i] = Lerp(px[i], navy, alpha[i] * 0.3f);
        }

        private static void BoxBlur(float[] a, int r)
        {
            var tmp = new float[a.Length];
            for (var y = 0; y < Size; y++)
            {
                var sum = 0f;
                for (var x = -r; x <= r; x++) sum += a[y * Size + Mathf.Clamp(x, 0, Size - 1)];
                for (var x = 0; x < Size; x++)
                {
                    tmp[y * Size + x] = sum / (2 * r + 1);
                    sum += a[y * Size + Mathf.Min(x + r + 1, Size - 1)] - a[y * Size + Mathf.Max(x - r, 0)];
                }
            }
            for (var x = 0; x < Size; x++)
            {
                var sum = 0f;
                for (var y = -r; y <= r; y++) sum += tmp[Mathf.Clamp(y, 0, Size - 1) * Size + x];
                for (var y = 0; y < Size; y++)
                {
                    a[y * Size + x] = sum / (2 * r + 1);
                    sum += tmp[Mathf.Min(y + r + 1, Size - 1) * Size + x] - tmp[Mathf.Max(y - r, 0) * Size + x];
                }
            }
        }

        private static void Over(Color[] px, Color[] layer)
        {
            for (var i = 0; i < px.Length; i++)
            {
                var a = layer[i].a;
                if (a <= 0f) continue;
                var c = layer[i];
                c.a = 1f;
                px[i] = Lerp(px[i], c, a);
            }
        }

        // ---------- 3D plane ----------
        /// <summary>The plane rendered alone (transparent background, MSAA) at icon size.</summary>
        private static Color[] RenderPlane(Mesh mesh, Material material, Vector3 rotation, float elevation, float tint)
        {
            var root = new GameObject("IconStage") { hideFlags = HideFlags.HideAndDontSave };
            root.transform.position = new Vector3(0f, -800f, 0f);
            var model = new GameObject("Model", typeof(MeshFilter), typeof(MeshRenderer));
            model.transform.SetParent(root.transform, false);
            // Nose toward the top-right, banking into a climb, top of the wings visible.
            model.transform.localRotation = Quaternion.Euler(rotation);
            model.GetComponent<MeshFilter>().sharedMesh = mesh;
            // Slightly off-white paper so the light shows the folds (pure white clips to a flat shape).
            var paper = new Material(material);
            if (paper.HasProperty("_BaseColor")) paper.SetColor("_BaseColor", new Color(tint, tint, tint * 1.02f));
            model.GetComponent<MeshRenderer>().sharedMaterial = paper;

            var key = new GameObject("Key", typeof(Light)).GetComponent<Light>();
            key.transform.SetParent(root.transform, false);
            key.transform.rotation = Quaternion.Euler(55f, -40f, 0f);
            key.type = LightType.Directional;
            key.intensity = 0.95f;
            key.color = new Color(1f, 0.97f, 0.92f);
            var fill = new GameObject("Fill", typeof(Light)).GetComponent<Light>();
            fill.transform.SetParent(root.transform, false);
            fill.transform.rotation = Quaternion.Euler(20f, 140f, 0f);
            fill.type = LightType.Directional;
            fill.intensity = 0.3f;
            fill.color = new Color(0.8f, 0.9f, 1f);

            var bounds = model.GetComponent<MeshRenderer>().bounds;
            var cam = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
            cam.transform.SetParent(root.transform, false);
            const float fov = 22f;
            var distance = bounds.extents.magnitude / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad) * 0.78f;
            cam.transform.position = bounds.center + Quaternion.Euler(elevation, 0f, 0f) * Vector3.back * distance;
            cam.transform.LookAt(bounds.center);
            // Shift the plane a little right and up in the frame (room for the trail).
            cam.transform.position += cam.transform.right * -bounds.extents.magnitude * 0.12f + cam.transform.up * -bounds.extents.magnitude * 0.1f;
            cam.fieldOfView = fov;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = distance * 3f;
            // Render at 2× and average 2×2 blocks: clean edges without relying on MSAA readback.
            const int big = Size * 2;
            var rt = new RenderTexture(big, big, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(big, big, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, big, big), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            var src = tex.GetPixels();
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(paper);

            var pixels = new Color[Size * Size];
            var covered = 0;
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var c = new Color(0f, 0f, 0f, 0f);
                for (var dy = 0; dy < 2; dy++)
                for (var dx = 0; dx < 2; dx++)
                {
                    var s = src[(y * 2 + dy) * big + x * 2 + dx];
                    c += new Color(s.r * s.a, s.g * s.a, s.b * s.a, s.a); // premultiply for a clean average
                }
                c *= 0.25f;
                if (c.a > 0.001f) { c.r /= c.a; c.g /= c.a; c.b /= c.a; covered++; }
                pixels[y * Size + x] = c;
            }
            Log.Info($"App icon: plane covers {covered * 100f / (Size * Size):0.0}% of the icon.");
            return pixels;
        }
    }
}
