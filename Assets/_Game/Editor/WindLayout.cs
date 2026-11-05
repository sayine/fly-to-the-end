using System.IO;
using Game.Wind;
using Template.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Wind for the whole house (SPEC §1). Living room: floor fan across the launch path, radiator thermal, AC vent.
    /// Hallway: two moderate helper fans carrying the plane north. Kitchen: stove/hood updraft, cold fridge downdraft.
    /// Study: desk fan, warm lamp updraft. Bedroom 1: ceiling fan downdraft (its blades are solid), gusting window.
    /// Bedroom 2: radiator thermal, desk fan. Sources are props scaled like the furniture; zones have no colliders.
    /// Built under a non-static "Wind" root so rotors can spin.
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
            var b1 = RoomLayout.Bedroom1;
            var b2 = RoomLayout.Bedroom2;

            WindVolume Z(string name, WindKind kind, Vector3 origin, Vector3 dir, float length, float r0, float r1, float scale, bool ring) =>
                Zone(root, name, kind, origin, dir, length, r0, r1, scale, config, particleMat, ring ? markerMat : null);

            // Living room: floor fan by the left wall, tilted ~8° up, blowing +X across the launch path.
            var fanHead = Fan(root, "Fan", new Vector3(living.min.x + 1.3f, 0f, -14.5f), Vector3.right, propMat);
            Z("Wind_Fan", WindKind.Fan, fanHead, new Vector3(1f, 0.14f, 0f).normalized, 15f, 0.3f * S, 2.3f, 1f, false);
            Radiator(root, "Radiator", new Vector3(living.min.x + 0.15f * S * 0.5f + 0.02f, 0f, -19.5f), propMat);
            Z("Wind_Thermal", WindKind.Thermal, new Vector3(living.min.x + 1.1f, 0.05f, -19.5f), Vector3.up, living.max.y - 0.6f, 1.4f, 1.8f, 1f, true);
            Box(root, "CeilingVent", new Vector3(4.5f, living.max.y - 0.05f, -7.5f), new Vector3(0.9f, 0.1f, 0.6f) * S, propMat);
            Z("Wind_Downdraft", WindKind.Downdraft, new Vector3(4.5f, living.max.y - 0.12f, -7.5f), Vector3.down, living.max.y - 0.15f, 1f, 1.8f, 1f, true);

            // Hallway: two moderate floor fans carrying the plane north (entrance, and past the side doors).
            var hallHead = Fan(root, "Fan2", new Vector3(hall.min.x + 0.75f, 0f, hall.min.z + 1.4f), Vector3.forward, propMat);
            Z("Wind_HallFan", WindKind.Fan, hallHead, new Vector3(0.18f, 0.12f, 1f).normalized, 13f, 0.8f, 2.6f, 0.55f, false);
            var hallHead2 = Fan(root, "Fan3", new Vector3(hall.max.x - 0.75f, 0f, 20.5f), Vector3.forward, propMat);
            Z("Wind_HallFan2", WindKind.Fan, hallHead2, new Vector3(-0.18f, 0.12f, 1f).normalized, 13f, 0.8f, 2.6f, 0.5f, false);

            // Kitchen: heat rising off the stove into the hood, cold air spilling down in front of the fridge.
            var stove = RoomLayout.StoveTop;
            Z("Wind_Stove", WindKind.Thermal, stove + Vector3.up * 0.05f, Vector3.up, 6f - stove.y - 0.3f, 0.8f, 1.3f, 0.9f, false);
            var fridge = RoomLayout.FridgeFront;
            Z("Wind_Fridge", WindKind.Downdraft, new Vector3(fridge.x, 3.2f, fridge.z), Vector3.down, 3.15f, 0.6f, 1.4f, 0.7f, true);

            // Study: desk fan blowing into the room, warm air rising over the reading lamp.
            var studyFan = DeskFan(root, "DeskFan_Study", RoomLayout.StudyDeskTop, Vector3.back, propMat);
            Z("Wind_StudyFan", WindKind.Fan, studyFan, new Vector3(0f, 0.08f, -1f).normalized, 8f, 0.3f, 1.6f, 0.6f, false);
            Z("Wind_LampThermal", WindKind.Thermal, RoomLayout.StudyLamp, Vector3.up, 6f - RoomLayout.StudyLamp.y - 0.2f, 0.5f, 1f, 0.7f, false);

            // Bedroom 1: ceiling fan (solid blades: a crash hazard) pushing down, and a gusting open window.
            var fanCentre = new Vector3(-11f, b1.max.y, 50f);
            Box(root, "CeilingFan_Base", fanCentre + Vector3.down * 0.3f * S, new Vector3(0.25f, 0.6f, 0.25f) * S, propMat);
            Box(root, "CeilingFan_Rotor", fanCentre + Vector3.down * 0.62f * S, new Vector3(1.3f, 0.06f, 1.3f) * S, propMat);
            Z("Wind_CeilingFan", WindKind.Downdraft, fanCentre + Vector3.down * 0.7f * S, Vector3.down, b1.max.y - 0.7f * S - 0.1f, 1.4f, 2.2f, 1f, true);
            var window = Box(root, "Window", new Vector3(b1.min.x + 0.06f, 1.6f * S * 0.5f + 0.9f * S, 53f), new Vector3(0.1f, 1.6f, 1.0f) * S, propMat);
            Object.DestroyImmediate(window.GetComponent<Collider>()); // flat on the wall
            var gust = Z("Wind_WindowGust", WindKind.Fan, new Vector3(b1.min.x + 0.3f, window.transform.position.y, 53f), Vector3.right, 12f, 1f, 2.4f, 0.9f, false);
            var gustSo = new SerializedObject(gust);
            gustSo.FindProperty("_gustPeriod").floatValue = 6f;
            gustSo.FindProperty("_gustOnFraction").floatValue = 0.4f;
            gustSo.ApplyModifiedPropertiesWithoutUndo();

            // Bedroom 2: radiator thermal on the west wall, desk fan on the desk.
            Radiator(root, "Radiator2", new Vector3(b2.min.x + 0.15f * S * 0.5f + 0.02f, 0f, 49f), propMat);
            Z("Wind_Thermal2", WindKind.Thermal, new Vector3(b2.min.x + 1.1f, 0.05f, 49f), Vector3.up, b2.max.y - 0.6f, 1.3f, 1.7f, 1f, true);
            var bedFan = DeskFan(root, "DeskFan_Bed2", RoomLayout.Bed2DeskTop, Vector3.back, propMat);
            Z("Wind_Bed2Fan", WindKind.Fan, bedFan, new Vector3(0f, 0.08f, -1f).normalized, 8f, 0.3f, 1.6f, 0.6f, false);
            return root;
        }

        /// <summary>Small desk fan standing on a desk top, facing `facing`. Returns the front of its head.</summary>
        private static Vector3 DeskFan(Transform root, string name, Vector3 deskTop, Vector3 facing, Material mat)
        {
            var size = new Vector3(0.3f, 0.4f, 0.25f) * S;
            Box(root, name, deskTop + Vector3.up * size.y * 0.5f, size, mat);
            return deskTop + Vector3.up * size.y * 0.7f + facing * size.z * 0.6f;
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
            FinishRotor(root, "Fan3_Rotor_Model", Vector3.forward);
            FinishRotor(root, "CeilingFan_Rotor_Model", Vector3.down);
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
            pivot.SetPositionAndRotation(bounds.center, Quaternion.LookRotation(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.forward : Vector3.up));
            blades.SetParent(pivot, true);
            EditorUtil.Set(pivot.gameObject.AddComponent<Spinner>(), "_config", config);
        }

        private static WindVolume Zone(Transform root, string name, WindKind kind, Vector3 origin, Vector3 direction,
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
            return volume;
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
