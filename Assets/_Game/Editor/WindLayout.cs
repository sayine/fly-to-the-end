using System.IO;
using Game.Wind;
using Template.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Layout 1 wind (SPEC "Room and wind"): a floor fan pushing sideways across the launch path, a radiator
    /// thermal by the left wall (free altitude), and a ceiling vent downdraft right of the path (hazard).
    /// Sources (fan body, radiator, vent) are solid props; the wind zones themselves have no colliders.
    /// Built under a non-static "Wind" root so the fan rotor can spin.
    /// </summary>
    internal static class WindLayout
    {
        public const string ConfigPath = "Assets/_Game/Configs/WindConfig.asset";
        private const string TexturesFolder = "Assets/_Game/Art/Textures";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string DotPath = TexturesFolder + "/SoftDot.png";
        private const string RingPath = TexturesFolder + "/SoftRing.png";

        private static readonly Vector3 FanDirection = new Vector3(1f, 0.14f, 0f).normalized;

        public static WindConfig LoadOrCreateConfig() => EditorUtil.LoadOrCreate<WindConfig>(ConfigPath);

        /// <summary>Greybox sources + zones (call before PropModels.Apply so models can replace the sources).</summary>
        public static Transform Build(Material propMat)
        {
            var config = AssetDatabase.LoadAssetAtPath<WindConfig>(ConfigPath);
            var particleMat = ParticleMaterial();
            var markerMat = MarkerMaterial();
            var root = new GameObject("Wind").transform;

            // Floor fan on the left, tilted ~8° up, blowing +X across the launch path ~10 m after launch.
            Box(root, "Fan_Body", new Vector3(-9.7f, 0.6f, -5f), new Vector3(0.4f, 1.2f, 0.5f), propMat);
            var rotor = Box(root, "Fan_Rotor", new Vector3(-9.45f, 1f, -5f), new Vector3(0.1f, 0.42f, 0.42f), propMat);
            Object.DestroyImmediate(rotor.GetComponent<Collider>()); // blades are behind a grille: no crash
            Zone(root, "Wind_Fan", WindKind.Fan, new Vector3(-9.35f, 1f, -5f), FanDirection, 14f, 0.3f, 2f,
                config, particleMat, null);

            // Radiator against the left wall with a big thermal column above it.
            Box(root, "Radiator", new Vector3(-10.92f, 0.35f, 4f), new Vector3(0.15f, 0.6f, 1.2f), propMat);
            Zone(root, "Wind_Thermal", WindKind.Thermal, new Vector3(-9.9f, 0.05f, 4f), Vector3.up, 6f, 1.4f, 1.8f,
                config, particleMat, markerMat);

            // Ceiling AC vent right of the launch path, pushing down.
            Box(root, "CeilingVent", new Vector3(3.5f, 6.95f, 0f), new Vector3(0.9f, 0.1f, 0.6f), propMat);
            Zone(root, "Wind_Downdraft", WindKind.Downdraft, new Vector3(3.5f, 6.88f, 0f), Vector3.down, 6.8f, 1f, 1.8f,
                config, particleMat, markerMat);
            return root;
        }

        /// <summary>After PropModels.Apply: put the blade model on a centred pivot that spins around the wind axis.</summary>
        public static void FinishRotor(Transform root)
        {
            var config = AssetDatabase.LoadAssetAtPath<WindConfig>(ConfigPath);
            var blades = root.Find("Fan_Rotor_Model");
            if (blades == null) return;
            var renderers = blades.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var pivot = new GameObject("Fan_Rotor_Pivot").transform;
            pivot.SetParent(root, false);
            pivot.SetPositionAndRotation(bounds.center, Quaternion.LookRotation(Vector3.right)); // blades face +X
            blades.SetParent(pivot, true);
            EditorUtil.Set(pivot.gameObject.AddComponent<Spinner>(), "_config", config);
        }

        private static void Zone(Transform root, string name, WindKind kind, Vector3 origin, Vector3 direction,
            float length, float startRadius, float endRadius, WindConfig config, Material particleMat, Material markerMat)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction,
                Mathf.Abs(direction.y) > 0.9f ? Vector3.forward : Vector3.up));

            var volume = go.AddComponent<WindVolume>();
            var so = new SerializedObject(volume);
            so.FindProperty("_config").objectReferenceValue = config;
            so.FindProperty("_kind").enumValueIndex = (int)kind;
            so.FindProperty("_length").floatValue = length;
            so.FindProperty("_startRadius").floatValue = startRadius;
            so.FindProperty("_endRadius").floatValue = endRadius;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMat;
            var visual = go.AddComponent<WindVisual>();
            EditorUtil.Set(visual, "_config", config);

            if (markerMat != null)
            {
                // Floor ring under the column: at the origin for updrafts, at the far end for downdrafts.
                var floorPoint = direction.y > 0f ? origin : origin + direction * length;
                var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                marker.name = name + "_FloorRing";
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.transform.SetParent(root, false);
                marker.transform.SetPositionAndRotation(new Vector3(floorPoint.x, 0.015f, floorPoint.z), Quaternion.Euler(90f, 0f, 0f));
                var radius = direction.y > 0f ? startRadius : endRadius;
                marker.transform.localScale = Vector3.one * radius * 2.2f;
                var renderer = marker.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = markerMat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                EditorUtil.Set(visual, "_floorMarker", renderer);
            }
            visual.Apply(); // bake the particle settings into the scene too (prewarm visible in edit mode)
        }

        private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ---------- Textures / materials ----------
        private static Material ParticleMaterial() => SpriteMaterial($"{MaterialsFolder}/WindParticle.mat", SoftTexture(DotPath, false));
        private static Material MarkerMaterial() => SpriteMaterial($"{MaterialsFolder}/WindFloorRing.mat", SoftTexture(RingPath, true));

        /// <summary>Sprites/Default: unlit, alpha-blended, tinted by vertex color / _Color; works in URP.</summary>
        private static Material SpriteMaterial(string path, Texture2D texture)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>64 px soft dot (streaks/sparkles) or soft ring (floor marker), generated once.</summary>
        private static Texture2D SoftTexture(string path, bool ring)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            EditorUtil.EnsureFolder(TexturesFolder);
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = new Vector2(x - size * 0.5f + 0.5f, y - size * 0.5f + 0.5f).magnitude / (size * 0.5f);
                var a = ring
                    ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.18f) + Mathf.Clamp01(0.25f * (1f - d)) // ring + faint fill
                    : Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
