using System.IO;
using Game.Wind;
using Template.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Layout 1 wind (SPEC "Room and wind"). Living room: a floor fan pushing sideways across the launch path, a
    /// radiator thermal by the left wall near the shelf, a ceiling vent downdraft right of the path (hazard).
    /// Hallway: a moderate floor fan blowing along it that carries the plane into the dining room. Dining room:
    /// a radiator thermal. Sources are solid props (scaled like the furniture); zones have no colliders.
    /// Built under a non-static "Wind" root so the fan rotors can spin.
    /// </summary>
    internal static class WindLayout
    {
        public const string ConfigPath = "Assets/_Game/Configs/WindConfig.asset";
        private const string TexturesFolder = "Assets/_Game/Art/Textures";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string DotPath = TexturesFolder + "/SoftDot.png";
        private const string RingPath = TexturesFolder + "/SoftRing.png";

        private static float S => RoomLayout.FurnitureScale;

        public static WindConfig LoadOrCreateConfig() => EditorUtil.LoadOrCreate<WindConfig>(ConfigPath);

        /// <summary>Greybox sources + zones (call before PropModels.Apply so models can replace the sources).</summary>
        public static Transform Build(Material propMat)
        {
            var config = AssetDatabase.LoadAssetAtPath<WindConfig>(ConfigPath);
            var particleMat = ParticleMaterial();
            var markerMat = MarkerMaterial();
            var root = new GameObject("Wind").transform;
            var living = RoomLayout.LivingRoom;
            var hall = RoomLayout.Hallway;
            var dining = RoomLayout.DiningRoom;

            // Living room: floor fan by the left wall, tilted ~8° up, blowing +X across the path ~6 m after launch.
            var fanHead = Fan(root, "Fan", new Vector3(living.min.x + 1.3f, 0f, -9.2f), Vector3.right, propMat);
            Zone(root, "Wind_Fan", WindKind.Fan, fanHead, new Vector3(1f, 0.14f, 0f).normalized, 14f, 0.3f * S, 2.2f, 1f,
                config, particleMat, null);

            // Living room: radiator on the left wall near the shelf, big thermal column above it.
            Radiator(root, "Radiator", new Vector3(living.min.x + 0.15f * S * 0.5f + 0.02f, 0f, -12.6f), propMat);
            Zone(root, "Wind_Thermal", WindKind.Thermal, new Vector3(living.min.x + 1.1f, 0.05f, -12.6f), Vector3.up,
                living.max.y - 0.6f, 1.4f, 1.8f, 1f, config, particleMat, markerMat);

            // Living room: ceiling AC vent right of the path, pushing down.
            Box(root, "CeilingVent", new Vector3(4f, living.max.y - 0.05f, -4f), new Vector3(0.9f, 0.1f, 0.6f) * S, propMat);
            Zone(root, "Wind_Downdraft", WindKind.Downdraft, new Vector3(4f, living.max.y - 0.12f, -4f), Vector3.down,
                living.max.y - 0.15f, 1f, 1.8f, 1f, config, particleMat, markerMat);

            // Hallway: moderate floor fan at the entrance (left side) blowing along the hallway toward the dining room.
            var hallHead = Fan(root, "Fan2", new Vector3(hall.min.x + 0.75f, 0f, hall.min.z + 1.4f), Vector3.forward, propMat);
            Zone(root, "Wind_HallFan", WindKind.Fan, hallHead, new Vector3(0.12f, 0.1f, 1f).normalized, 13f, 0.5f, 2.2f, 0.55f,
                config, particleMat, null);

            // Dining room: radiator thermal on the left wall.
            Radiator(root, "Radiator2", new Vector3(dining.min.x + 0.15f * S * 0.5f + 0.02f, 0f, 30f), propMat);
            Zone(root, "Wind_Thermal2", WindKind.Thermal, new Vector3(dining.min.x + 1.1f, 0.05f, 30f), Vector3.up,
                dining.max.y - 0.6f, 1.4f, 1.8f, 1f, config, particleMat, markerMat);
            return root;
        }

        /// <summary>Floor fan (body + collider-less rotor box at the head) facing `facing`. Returns the head centre.</summary>
        private static Vector3 Fan(Transform root, string name, Vector3 floorPoint, Vector3 facing, Material mat)
        {
            var side = Vector3.Cross(Vector3.up, facing);
            var bodySize = Abs(side) * 0.5f * S + Vector3.up * 1.2f * S + Abs(facing) * 0.4f * S;
            Box(root, name + "_Body", floorPoint + Vector3.up * bodySize.y * 0.5f, bodySize, mat);
            var head = floorPoint + Vector3.up * 1.0f * S + facing * 0.25f * S;
            var rotorSize = Abs(side) * 0.42f * S + Vector3.up * 0.42f * S + Abs(facing) * 0.1f * S;
            var rotor = Box(root, name + "_Rotor", head, rotorSize, mat);
            Object.DestroyImmediate(rotor.GetComponent<Collider>()); // blades are behind a grille: no crash
            return head + facing * 0.1f * S;
        }

        private static void Radiator(Transform root, string name, Vector3 floorPoint, Material mat)
        {
            var size = new Vector3(0.15f, 0.6f, 1.2f) * S;
            Box(root, name, floorPoint + Vector3.up * size.y * 0.5f, size, mat);
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>After PropModels.Apply: put each fan's blade model on a centred pivot that spins around its axis.</summary>
        public static void FinishRotors(Transform root)
        {
            FinishRotor(root, "Fan_Rotor_Model", Vector3.right);
            FinishRotor(root, "Fan2_Rotor_Model", Vector3.forward);
        }

        private static void FinishRotor(Transform root, string modelName, Vector3 axis)
        {
            var config = AssetDatabase.LoadAssetAtPath<WindConfig>(ConfigPath);
            var blades = root.Find(modelName);
            if (blades == null) return;
            var renderers = blades.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var pivot = new GameObject(modelName.Replace("_Model", "_Pivot")).transform;
            pivot.SetParent(root, false);
            pivot.SetPositionAndRotation(bounds.center, Quaternion.LookRotation(axis));
            blades.SetParent(pivot, true);
            EditorUtil.Set(pivot.gameObject.AddComponent<Spinner>(), "_config", config);
        }

        private static void Zone(Transform root, string name, WindKind kind, Vector3 origin, Vector3 direction,
            float length, float startRadius, float endRadius, float strengthScale, WindConfig config, Material particleMat, Material markerMat)
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
            so.FindProperty("_strengthScale").floatValue = strengthScale;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMat;
            var ribbons = new GameObject("Ribbons", typeof(ParticleSystem));
            ribbons.transform.SetParent(go.transform, false);
            var ribbonRenderer = ribbons.GetComponent<ParticleSystemRenderer>();
            ribbonRenderer.sharedMaterial = particleMat;
            ribbonRenderer.trailMaterial = particleMat;

            var visual = go.AddComponent<WindVisual>();
            EditorUtil.Set(visual, "_config", config);
            EditorUtil.Set(visual, "_ribbons", ribbons.GetComponent<ParticleSystem>());

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
