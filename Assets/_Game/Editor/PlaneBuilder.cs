using System.Collections.Generic;
using System.IO;
using Game.Planes;
using Template.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The plane collection (SPEC §3): 20 PlaneConfig assets from the table below, procedural fold shapes (meshes
    /// with UVs), printed-paper pattern textures, materials and hangar thumbnails, plus the catalog and the stat
    /// scaling config. Thumbnails need a GPU: in -nographics batch runs existing thumbnails are kept.
    /// </summary>
    internal static class PlaneBuilder
    {
        public const string ConfigFolder = "Assets/_Game/Configs/Planes";
        public const string CatalogPath = "Assets/_Game/Configs/PlaneCatalog.asset";
        private const string ScalingPath = "Assets/_Game/Configs/PlaneStatScaling.asset";
        private const string ArtFolder = "Assets/_Game/Art/Planes";

        private struct Row
        {
            public string Id, Name;
            public PlaneShape Shape;
            public PlanePattern Pattern;
            public Color A, B;
            public PlaneStats Stats;
            public UnlockRule Unlock;
        }

        private static Row R(string id, string name, PlaneShape shape, PlanePattern pattern, Color a, Color b,
            int speed, int agility, int glide, int lift, int stability, UnlockKind kind, int value = 0) => new Row
        {
            Id = id, Name = name, Shape = shape, Pattern = pattern, A = a, B = b,
            Stats = new PlaneStats(speed, agility, glide, lift, stability), Unlock = new UnlockRule(kind, value),
        };

        private static Color C(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        // SPEC §3 roster (first pass; tune after play-testing).
        private static readonly Row[] Roster =
        {
            R("classic_dart", "Classic Dart", PlaneShape.Dart, PlanePattern.Plain, C("#FAFAF5"), C("#D8D8D0"), 3, 3, 3, 3, 3, UnlockKind.Start),
            R("classic_sky", "Classic Dart (Sky)", PlaneShape.Dart, PlanePattern.Plain, C("#8FC7FF"), C("#5E9FE0"), 3, 3, 3, 3, 3, UnlockKind.Start),
            R("notebook_glider", "Notebook Glider", PlaneShape.Glider, PlanePattern.Lined, C("#FBFBF4"), C("#7FA8E8"), 2, 3, 4, 3, 3, UnlockKind.Stars, 5),
            R("swallow", "Swallow", PlaneShape.Swallow, PlanePattern.Plain, C("#9EE6C8"), C("#6CC7A3"), 3, 4, 3, 3, 2, UnlockKind.Level, 5),
            R("sticky_note", "Sticky Note", PlaneShape.Square, PlanePattern.Plain, C("#FFE45C"), C("#F2C94C"), 2, 5, 2, 3, 2, UnlockKind.Stars, 12),
            R("newspaper_bomber", "Newspaper Bomber", PlaneShape.WideWing, PlanePattern.Newsprint, C("#EDEBE4"), C("#3A3A3A"), 2, 2, 4, 3, 5, UnlockKind.Level, 10),
            R("crane_jet", "Origami Crane Jet", PlaneShape.Delta, PlanePattern.Plain, C("#E85D5D"), C("#B83B3B"), 4, 3, 2, 3, 3, UnlockKind.Stars, 20),
            R("weekly_streak", "Weekly Streak", PlaneShape.Dart, PlanePattern.Metallic, C("#F5C542"), C("#C9962B"), 3, 4, 4, 3, 3, UnlockKind.CheckIn, 7),
            R("map_glider", "Map Glider", PlaneShape.Glider, PlanePattern.Map, C("#EFE3C4"), C("#7DB86A"), 3, 3, 4, 4, 3, UnlockKind.Stars, 30),
            R("stunt_flyer", "Stunt Flyer", PlaneShape.Stunt, PlanePattern.Stripes, C("#FF9A3C"), C("#7B4FD6"), 3, 5, 2, 4, 2, UnlockKind.Level, 15),
            R("kite_wing", "Kite Wing", PlaneShape.WideWing, PlanePattern.Stripes, C("#FF7FA8"), C("#FFFFFF"), 2, 3, 5, 4, 3, UnlockKind.Stars, 40),
            R("banknote", "Banknote", PlaneShape.SlimDart, PlanePattern.Lined, C("#A8D5A2"), C("#4F8A4B"), 5, 3, 2, 2, 3, UnlockKind.Level, 20),
            R("fortnight_flyer", "Fortnight Flyer", PlaneShape.Delta, PlanePattern.Metallic, C("#D9DEE6"), C("#8F98A6"), 4, 4, 3, 4, 3, UnlockKind.CheckIn, 14),
            R("comic_strip", "Comic Strip", PlaneShape.Swallow, PlanePattern.Comic, C("#FFF06A"), C("#E83F3F"), 3, 4, 3, 4, 4, UnlockKind.Stars, 50),
            R("blueprint", "Blueprint", PlaneShape.Delta, PlanePattern.Blueprint, C("#2F6DB5"), C("#E8F1FF"), 4, 4, 3, 3, 4, UnlockKind.Level, 25),
            R("night_owl", "Night Owl", PlaneShape.Glider, PlanePattern.Stars, C("#22305C"), C("#FFE89A"), 3, 3, 5, 4, 3, UnlockKind.Stars, 60),
            R("tissue_floater", "Tissue Floater", PlaneShape.WideWing, PlanePattern.Plain, C("#FFFFFF"), C("#EEF2F7"), 1, 3, 5, 5, 1, UnlockKind.Stars, 70),
            R("cardboard_tank", "Cardboard Tank", PlaneShape.WideDart, PlanePattern.Plain, C("#C79A63"), C("#A67C4A"), 2, 2, 3, 2, 5, UnlockKind.Level, 30),
            R("rainbow", "Rainbow", PlaneShape.Stunt, PlanePattern.Rainbow, C("#FF6B6B"), C("#6BC5FF"), 4, 5, 4, 4, 3, UnlockKind.Stars, 80),
            R("golden_jet", "Golden Jet", PlaneShape.Delta, PlanePattern.Metallic, C("#FFD54A"), C("#E0A100"), 5, 5, 4, 4, 4, UnlockKind.Stars, 90),
        };

        public static PlaneCatalog Build(bool force)
        {
            EditorUtil.EnsureFolder(ConfigFolder);
            EditorUtil.EnsureFolder(ArtFolder);
            var scaling = EditorUtil.LoadOrCreate<PlaneStatScaling>(ScalingPath);
            var meshes = new Dictionary<PlaneShape, Mesh>();
            var planes = new PlaneConfig[Roster.Length];
            for (var i = 0; i < Roster.Length; i++)
            {
                var row = Roster[i];
                if (!meshes.TryGetValue(row.Shape, out var mesh)) meshes[row.Shape] = mesh = ShapeMesh(row.Shape);
                var texture = PatternTexture(row, force);
                var material = PlaneMaterial(row, texture);
                var plane = EditorUtil.LoadOrCreate<PlaneConfig>($"{ConfigFolder}/{row.Id}.asset");
                plane.Define(row.Id, row.Name, row.Shape, row.Pattern, row.A, row.B, row.Stats, row.Unlock);
                plane.SetLook(mesh, material, Thumbnail(row.Id, mesh, material, force));
                EditorUtility.SetDirty(plane);
                planes[i] = plane;
            }
            var catalog = EditorUtil.LoadOrCreate<PlaneCatalog>(CatalogPath);
            catalog.Define(planes, scaling);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Log.Info($"Built {planes.Length} planes ({CatalogPath}).");
            return catalog;
        }

        // ---------- Shapes (nose +Z, ~0.25 m long, UVs projected from above) ----------
        private static Mesh ShapeMesh(PlaneShape shape)
        {
            var polys = new List<Vector3[]>();
            void P(params Vector3[] p) => polys.Add(p);
            void Mirror(params Vector3[] p)
            {
                P(p);
                var m = new Vector3[p.Length];
                for (var i = 0; i < p.Length; i++) m[i] = new Vector3(-p[i].x, p[i].y, p[i].z);
                P(m);
            }
            Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

            switch (shape)
            {
                case PlaneShape.Dart:
                case PlaneShape.SlimDart:
                case PlaneShape.WideDart:
                {
                    var span = shape == PlaneShape.SlimDart ? 0.075f : shape == PlaneShape.WideDart ? 0.15f : 0.11f;
                    var keel = shape == PlaneShape.WideDart ? 0.04f : shape == PlaneShape.SlimDart ? 0.025f : 0.03f;
                    var nose = shape == PlaneShape.SlimDart ? 0.14f : 0.13f;
                    Mirror(V(0, 0, nose), V(0, 0, -0.12f), V(-span, 0.018f, -0.12f));
                    P(V(0, 0, nose), V(0, -keel, -0.12f), V(0, 0, -0.12f));
                    break;
                }
                case PlaneShape.Glider:
                    P(V(0, 0, 0.12f), V(0.03f, 0, 0.06f), V(0.025f, 0, -0.13f), V(-0.025f, 0, -0.13f), V(-0.03f, 0, 0.06f));
                    Mirror(V(-0.03f, 0, 0.06f), V(-0.03f, 0, -0.05f), V(-0.17f, 0.02f, -0.07f), V(-0.17f, 0.02f, -0.01f));
                    P(V(0, 0, -0.1f), V(0.06f, 0.005f, -0.135f), V(-0.06f, 0.005f, -0.135f));
                    P(V(0, 0, 0.12f), V(0, -0.025f, -0.13f), V(0, 0, -0.13f));
                    break;
                case PlaneShape.WideWing:
                    P(V(0, 0, 0.12f), V(0.05f, 0, 0.04f), V(0.04f, 0, -0.12f), V(-0.04f, 0, -0.12f), V(-0.05f, 0, 0.04f));
                    Mirror(V(-0.05f, 0, 0.04f), V(-0.05f, 0, -0.1f), V(-0.2f, 0.01f, -0.09f), V(-0.2f, 0.01f, 0.02f));
                    Mirror(V(-0.2f, 0.01f, 0.02f), V(-0.2f, 0.01f, -0.09f), V(-0.21f, 0.05f, -0.08f), V(-0.21f, 0.05f, 0f)); // folded tips
                    P(V(0, 0, 0.12f), V(0, -0.03f, -0.12f), V(0, 0, -0.12f));
                    break;
                case PlaneShape.Delta:
                    Mirror(V(0, 0, 0.13f), V(0, 0, -0.12f), V(-0.13f, 0.01f, -0.12f));
                    Mirror(V(-0.13f, 0.01f, -0.12f), V(-0.12f, 0.01f, -0.06f), V(-0.13f, 0.06f, -0.13f)); // winglets
                    P(V(0, 0, 0.13f), V(0, -0.03f, -0.12f), V(0, 0, -0.12f));
                    break;
                case PlaneShape.Stunt:
                    Mirror(V(0, 0, 0.1f), V(0, 0, -0.1f), V(-0.14f, -0.025f, -0.1f)); // drooped, swept wings
                    P(V(0.02f, 0, 0.06f), V(0.02f, -0.03f, -0.1f), V(0.02f, 0, -0.1f));
                    P(V(-0.02f, 0, 0.06f), V(-0.02f, -0.03f, -0.1f), V(-0.02f, 0, -0.1f));
                    break;
                case PlaneShape.Swallow:
                    Mirror(V(0, 0, 0.13f), V(0, 0, -0.06f), V(-0.04f, 0, -0.14f), V(-0.13f, 0.015f, -0.09f)); // forked tail
                    P(V(0, 0, 0.13f), V(0, -0.03f, -0.06f), V(0, 0, -0.06f));
                    break;
                case PlaneShape.Square:
                    P(V(0, 0, 0.12f), V(0.04f, 0.005f, 0.06f), V(-0.04f, 0.005f, 0.06f));
                    P(V(-0.09f, 0.01f, 0.06f), V(0.09f, 0.01f, 0.06f), V(0.09f, 0.01f, -0.1f), V(-0.09f, 0.01f, -0.1f));
                    P(V(0, 0, 0.12f), V(0, -0.025f, -0.1f), V(0, 0, -0.1f));
                    break;
            }

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            foreach (var poly in polys)
            for (var i = 1; i < poly.Length - 1; i++)
            {
                // Both sides, separate vertices: flat normals per side.
                AddTri(verts, uvs, tris, poly[0], poly[i], poly[i + 1]);
                AddTri(verts, uvs, tris, poly[0], poly[i + 1], poly[i]);
            }

            var path = $"{ArtFolder}/Plane_{shape}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var create = mesh == null;
            if (create) mesh = new Mesh { name = $"Plane_{shape}" };
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (create) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void AddTri(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c)
        {
            foreach (var p in new[] { a, b, c })
            {
                t.Add(v.Count);
                v.Add(p);
                uv.Add(new Vector2(p.x / 0.44f + 0.5f, p.z / 0.3f + 0.5f));
            }
        }

        // ---------- Printed paper ----------
        private static Texture2D PatternTexture(Row row, bool force)
        {
            var path = $"{ArtFolder}/Paper_{row.Id}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null && !force) return existing;
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            var px = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var u = (x + 0.5f) / size;
                var v = (y + 0.5f) / size;
                var c = Pattern(row, u, v);
                // Fold crease down the middle and a little paper grain.
                var crease = Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * size / 1.5f);
                c = Color.Lerp(c, c * 0.8f, crease * 0.6f) * (0.97f + 0.03f * Mathf.PerlinNoise(x * 0.35f, y * 0.35f));
                c.a = 1f;
                px[y * size + x] = c;
            }
            tex.SetPixels(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Color Pattern(Row row, float u, float v)
        {
            var a = row.A;
            var b = row.B;
            switch (row.Pattern)
            {
                case PlanePattern.Lined:
                {
                    var line = Mathf.Abs(Mathf.Repeat(v * 16f, 1f) - 0.5f) > 0.44f;
                    var margin = Mathf.Abs(u - 0.22f) < 0.006f;
                    return margin ? new Color(0.92f, 0.45f, 0.45f) : line ? b : a;
                }
                case PlanePattern.Stripes:
                    return Mathf.Repeat((u + v) * 7f, 1f) < 0.5f ? a : b;
                case PlanePattern.Newsprint:
                {
                    var row8 = Mathf.FloorToInt(v * 28f);
                    var inLine = Mathf.Repeat(v * 28f, 1f) < 0.45f;
                    var col = Mathf.FloorToInt(u * 3f);
                    var word = Mathf.PerlinNoise(row8 * 3.1f + col * 7f, u * 22f) > 0.42f;
                    var headline = v > 0.78f && v < 0.88f && u > 0.12f && u < 0.88f;
                    return headline ? Color.Lerp(a, b, 0.8f) : inLine && word && Mathf.Repeat(u * 3f, 1f) < 0.9f ? Color.Lerp(a, b, 0.45f) : a;
                }
                case PlanePattern.Map:
                {
                    var land = Mathf.PerlinNoise(u * 4f + 3f, v * 4f + 7f) > 0.55f;
                    var river = Mathf.Abs(Mathf.Sin(u * 14f + Mathf.PerlinNoise(v * 3f, 1f) * 4f) - (v * 2f - 1f)) < 0.04f;
                    return river ? new Color(0.45f, 0.65f, 0.95f) : land ? b : a;
                }
                case PlanePattern.Comic:
                {
                    var cell = new Vector2(Mathf.Repeat(u * 22f, 1f) - 0.5f, Mathf.Repeat(v * 22f, 1f) - 0.5f);
                    return cell.magnitude < 0.28f ? b : a;
                }
                case PlanePattern.Blueprint:
                {
                    var grid = Mathf.Repeat(u * 12f, 1f) < 0.06f || Mathf.Repeat(v * 12f, 1f) < 0.06f;
                    return grid ? Color.Lerp(a, b, 0.7f) : a;
                }
                case PlanePattern.Stars:
                {
                    var h = Mathf.PerlinNoise(u * 60f, v * 60f);
                    return h > 0.82f ? b : a;
                }
                case PlanePattern.Rainbow:
                    return Color.HSVToRGB(Mathf.Repeat(v * 0.9f, 1f), 0.55f, 1f);
                case PlanePattern.Metallic:
                {
                    var sheen = Mathf.Pow(Mathf.Abs(Mathf.Sin((u * 0.6f + v) * 5f)), 6f);
                    return Color.Lerp(Color.Lerp(a, b, v), Color.white, sheen * 0.5f);
                }
                default:
                    return a;
            }
        }

        private static Material PlaneMaterial(Row row, Texture2D texture)
        {
            var mat = EditorUtil.LoadOrCreateMaterial($"{ArtFolder}/Paper_{row.Id}.mat", Color.white);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", texture);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", row.Pattern == PlanePattern.Metallic ? 0.7f : 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- Thumbnails ----------
        private static Sprite Thumbnail(string id, Mesh mesh, Material material, bool force)
        {
            var path = $"{ArtFolder}/Thumb_{id}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null && !force) return existing;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return existing; // -nographics

            const int size = 256;
            var root = new GameObject("ThumbStage") { hideFlags = HideFlags.HideAndDontSave };
            root.transform.position = new Vector3(0f, -500f, 0f);
            var model = new GameObject("Model", typeof(MeshFilter), typeof(MeshRenderer));
            model.transform.SetParent(root.transform, false);
            model.transform.localRotation = Quaternion.Euler(-8f, 135f, 10f);
            model.GetComponent<MeshFilter>().sharedMesh = mesh;
            model.GetComponent<MeshRenderer>().sharedMaterial = material;
            var lightGo = new GameObject("Light", typeof(Light));
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            // Three-quarter view from above, framed on the mesh bounds (every shape fills the picture).
            var bounds = model.GetComponent<MeshRenderer>().bounds;
            var radius = bounds.extents.magnitude;
            const float fov = 25f;
            var distance = radius / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad);
            var camGo = new GameObject("Camera", typeof(Camera));
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.position = bounds.center + Quaternion.Euler(48f, 0f, 0f) * Vector3.back * distance;
            camGo.transform.LookAt(bounds.center);
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = distance + radius * 2f;
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(root);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
