using System.IO;
using Game.Flight;
using Game.Levels;
using Template.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Game-scene side of levels: pooled mission items (gems, rings, landing target, room trigger, book stack) with
    /// generated meshes and materials, the MissionDirector, and the mission HUD on the game overlay canvas.
    /// </summary>
    internal static class MissionSceneBuilder
    {
        private const string MeshFolder = "Assets/_Game/Art/Meshes";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string TexturesFolder = "Assets/_Game/Art/Textures";
        private const int GemPool = 16;
        private const int RingPool = 6;

        public static MissionDirector Build(LevelCatalog catalog, PaperPlane plane, Transform launchPoint, Collider shelf,
            Material bookMat, Transform overlayCanvas)
        {
            var root = new GameObject("Mission").transform;

            var gemMesh = Gem();
            var ringMesh = Torus();
            var gemMat = Lit($"{MaterialsFolder}/Mission_Gem.mat", new Color(0.35f, 0.95f, 0.85f), new Color(0.15f, 0.55f, 0.5f));
            var ringMat = Lit($"{MaterialsFolder}/Mission_Ring.mat", Color.white, Color.black);
            var targetMat = Bullseye();

            var gems = new Game.Levels.Gem[GemPool];
            for (var i = 0; i < GemPool; i++)
            {
                var go = new GameObject($"Gem_{i + 1}", typeof(SphereCollider));
                go.transform.SetParent(root, false);
                var col = go.GetComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 0.45f; // generous: gems should feel easy to grab
                var visual = MeshChild(go.transform, "Visual", gemMesh, gemMat);
                gems[i] = go.AddComponent<Game.Levels.Gem>();
                EditorUtil.Set(gems[i], "_visual", visual);
                go.SetActive(false);
            }

            var rings = new Ring[RingPool];
            for (var i = 0; i < RingPool; i++)
            {
                var go = new GameObject($"Ring_{i + 1}", typeof(BoxCollider));
                go.transform.SetParent(root, false);
                var col = go.GetComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(1.5f, 1.5f, 0.4f); // the hole only (local units × ring radius)
                var visual = MeshChild(go.transform, "Torus", ringMesh, ringMat);
                rings[i] = go.AddComponent<Ring>();
                EditorUtil.Set(rings[i], "_renderer", visual.GetComponent<MeshRenderer>());
                go.SetActive(false);
            }

            // Landing target: thin box collider 2 cm above the furniture top + a bullseye quad.
            var targetGo = new GameObject("LandingTarget", typeof(BoxCollider));
            targetGo.transform.SetParent(root, false);
            var targetCol = targetGo.GetComponent<BoxCollider>();
            targetCol.size = new Vector3(1f, 0.04f, 1f);
            targetCol.center = new Vector3(0f, 0.0f, 0f);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Bullseye";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(targetGo.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0.022f, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var quadRenderer = quad.GetComponent<MeshRenderer>();
            quadRenderer.sharedMaterial = targetMat;
            quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var landing = targetGo.AddComponent<LandingTarget>();
            targetGo.SetActive(false);

            var roomGo = new GameObject("RoomTrigger", typeof(BoxCollider));
            roomGo.transform.SetParent(root, false);
            roomGo.GetComponent<BoxCollider>().isTrigger = true;
            var roomTrigger = roomGo.AddComponent<RoomTrigger>();
            roomGo.SetActive(false);

            var stack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stack.name = "BookStack";
            stack.transform.SetParent(root, false);
            stack.GetComponent<MeshRenderer>().sharedMaterial = bookMat;
            stack.SetActive(false);

            var hud = BuildHud(overlayCanvas);

            var director = root.gameObject.AddComponent<MissionDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("_catalog").objectReferenceValue = catalog;
            so.FindProperty("_plane").objectReferenceValue = plane;
            so.FindProperty("_launchPoint").objectReferenceValue = launchPoint;
            so.FindProperty("_launchShelf").objectReferenceValue = shelf;
            so.FindProperty("_bookStack").objectReferenceValue = stack.GetComponent<BoxCollider>();
            so.FindProperty("_landing").objectReferenceValue = landing;
            so.FindProperty("_roomTrigger").objectReferenceValue = roomTrigger;
            so.FindProperty("_hud").objectReferenceValue = hud;
            var gemsProp = so.FindProperty("_gems");
            gemsProp.arraySize = gems.Length;
            for (var i = 0; i < gems.Length; i++) gemsProp.GetArrayElementAtIndex(i).objectReferenceValue = gems[i];
            var ringsProp = so.FindProperty("_rings");
            ringsProp.arraySize = rings.Length;
            for (var i = 0; i < rings.Length; i++) ringsProp.GetArrayElementAtIndex(i).objectReferenceValue = rings[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return director;
        }

        // ---------- HUD ----------
        private static MissionHudView BuildHud(Transform canvas)
        {
            var rounded = UIBuilder.ButtonSprite;
            var root = new GameObject("MissionHud", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvas, false);
            UIBuilder.Stretch(root);

            // Intro (while aiming): level, room and objective on a soft panel near the top.
            var intro = UIBuilder.Image(root, "Intro", new Vector2(0.5f, 0.8f), new Vector2(940f, 230f), new Color(0f, 0f, 0f, 0.35f));
            var introImage = intro.GetComponent<Image>();
            introImage.sprite = rounded;
            introImage.type = Image.Type.Sliced;
            var title = UIBuilder.Text(intro, "Title", "LEVEL 1", 58, new Vector2(0.5f, 0.72f), new Vector2(900f, 80f), UIBuilder.Accent);
            var objective = UIBuilder.Text(intro, "Objective", "", 50, new Vector2(0.5f, 0.32f), new Vector2(900f, 120f), Color.white);
            objective.textWrappingMode = TextWrappingModes.Normal;

            // Progress (while flying): one line under the airtime counter.
            var progressRoot = UIBuilder.Image(root, "Progress", new Vector2(0.5f, 1f), new Vector2(760f, 96f), new Color(0f, 0f, 0f, 0.3f));
            progressRoot.anchoredPosition = new Vector2(0f, -330f);
            var progressImage = progressRoot.GetComponent<Image>();
            progressImage.sprite = rounded;
            progressImage.type = Image.Type.Sliced;
            var progress = UIBuilder.Text(progressRoot, "Text", "", 50, new Vector2(0.5f, 0.5f), new Vector2(740f, 90f), Color.white);

            var view = root.gameObject.AddComponent<MissionHudView>();
            EditorUtil.Set(view, "_intro", intro.gameObject);
            EditorUtil.Set(view, "_introTitle", title);
            EditorUtil.Set(view, "_introObjective", objective);
            EditorUtil.Set(view, "_progressRoot", progressRoot.gameObject);
            EditorUtil.Set(view, "_progress", progress);
            intro.gameObject.SetActive(false);
            progressRoot.gameObject.SetActive(false);
            return view;
        }

        // ---------- Meshes / materials ----------
        private static Transform MeshChild(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>Faceted gem: an elongated octahedron with a flat crown (flat-shaded).</summary>
        private static Mesh Gem()
        {
            const int sides = 6;
            var top = new Vector3(0f, 0.2f, 0f);
            var bottom = new Vector3(0f, -0.26f, 0f);
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            for (var i = 0; i < sides; i++)
            {
                var a0 = i * Mathf.PI * 2f / sides;
                var a1 = (i + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.22f, 0.04f, Mathf.Sin(a0) * 0.22f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.22f, 0.04f, Mathf.Sin(a1) * 0.22f);
                AddTri(verts, tris, top, p1, p0);
                AddTri(verts, tris, bottom, p0, p1);
            }
            return SaveMesh("MissionGem", verts, tris);
        }

        /// <summary>Torus in the XY plane (hole along Z), major radius 1, tube 0.07.</summary>
        private static Mesh Torus()
        {
            const int segments = 40;
            const int tube = 8;
            const float r = 1f, t = 0.07f;
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            for (var i = 0; i < segments; i++)
            for (var j = 0; j < tube; j++)
            {
                Vector3 P(int a, int b)
                {
                    var u = a * Mathf.PI * 2f / segments;
                    var v = b * Mathf.PI * 2f / tube;
                    var ring = r + t * Mathf.Cos(v);
                    return new Vector3(ring * Mathf.Cos(u), ring * Mathf.Sin(u), t * Mathf.Sin(v));
                }
                var p00 = P(i, j);
                var p10 = P(i + 1, j);
                var p01 = P(i, j + 1);
                var p11 = P(i + 1, j + 1);
                AddTri(verts, tris, p00, p01, p11);
                AddTri(verts, tris, p00, p11, p10);
            }
            return SaveMesh("MissionRing", verts, tris);
        }

        private static void AddTri(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            tris.Add(verts.Count); verts.Add(a);
            tris.Add(verts.Count); verts.Add(b);
            tris.Add(verts.Count); verts.Add(c);
        }

        private static Mesh SaveMesh(string name, System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<int> tris)
        {
            var path = $"{MeshFolder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var create = mesh == null;
            if (create) mesh = new Mesh { name = name };
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); // separate vertices per triangle → flat facets
            mesh.RecalculateBounds();
            if (create) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material Lit(string path, Color color, Color emission)
        {
            var mat = EditorUtil.LoadOrCreateMaterial(path, color);
            mat.SetColor("_BaseColor", color);
            if (emission.maxColorComponent > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Red/white bullseye (unlit, alpha-blended so its round edge is soft).</summary>
        private static Material Bullseye()
        {
            var texPath = $"{TexturesFolder}/Bullseye.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
            {
                const int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = new Vector2(x - size * 0.5f + 0.5f, y - size * 0.5f + 0.5f).magnitude / (size * 0.5f);
                    var band = Mathf.FloorToInt(d * 5f); // 5 rings: red, white, red, white, red centre
                    var c = band % 2 == 0 ? new Color(0.95f, 0.35f, 0.3f) : new Color(1f, 0.97f, 0.92f);
                    c.a = Mathf.Clamp01((1f - d) * size * 0.25f) * 0.92f;
                    tex.SetPixel(x, y, c);
                }
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            var matPath = $"{MaterialsFolder}/Mission_Bullseye.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) };
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }
    }
}
