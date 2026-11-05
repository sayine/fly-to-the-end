using Template.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Swaps greybox prop visuals for Meshy models (Docs/MESHY_ASSETS.md) when the model file exists.
    /// The greybox primitives keep their colliders (only their renderers are turned off), so gameplay never
    /// depends on Meshy topology. Each model is rotated by its Yaw, uniformly scaled to fit inside the combined
    /// bounds of the parts it replaces, and placed bottom-centre; the parts are then clamped to the model's bounds
    /// so crashes match what the player sees. Missing models leave the greybox as is.
    /// </summary>
    internal static class PropModels
    {
        public const string Folder = "Assets/_Game/Art/Models";
        public const string TexturesFolder = Folder + "/Textures";
        private const string MaterialsFolder = Folder + "/Materials";

        private struct Entry
        {
            public string File;      // Models/<File>.fbx, Textures/<File>_base_color.png
            public string[] Parts;   // greybox objects it replaces (by name, under Props)
            public float Yaw;        // degrees around Y applied to the model's native orientation
            public bool FitLargest;  // flat targets (blades, window, hood): fit the two largest sides, ignore the thin one
            public Entry(string file, float yaw, params string[] parts)
            {
                File = file;
                Yaw = yaw;
                Parts = parts;
                FitLargest = false;
            }

            public static Entry Flat(string file, float yaw, params string[] parts) =>
                new Entry(file, yaw, parts) { FitLargest = true };
        }

        // Yaw: Meshy models come in facing -Z with their long side along X. Yaw turns the front to face the room:
        // front +X → -90, front -X → 90, front +Z → 180, front -Z → 0. Check new models with a render.
        private static readonly Entry[] Entries =
        {
            // Living room
            new Entry("LaunchShelf", 180f, "LaunchPad"),
            new Entry("Sofa", -90f, "Sofa_Seat", "Sofa_Back"),
            new Entry("Armchair", 180f, "Armchair"),
            new Entry("CoffeeTable", 90f, "CoffeeTable"),
            new Entry("TVBench", 90f, "TVBench"),
            new Entry("TV", 90f, "TV"),
            new Entry("Bookshelf", 90f, "Bookshelf_1"),
            new Entry("Bookshelf", 90f, "Bookshelf_2"),
            new Entry("Desk", 0f, "Desk"),
            new Entry("FloorLamp", 0f, "Lamp_Pole", "Lamp_Shade"),
            new Entry("FloorLamp", 0f, "Lamp2_Pole", "Lamp2_Shade"),
            new Entry("PlantSmall", 0f, "Plant_Pot", "Plant_Leaves"),
            new Entry("BoxStack", 0f, "Box_1", "Box_2", "Box_3"),
            // Landing
            new Entry("PlantSmall", 0f, "Plant4_Pot", "Plant4_Leaves"),
            new Entry("PlantSmall", 0f, "Plant5_Pot", "Plant5_Leaves"),
            // Kitchen
            new Entry("KitchenCounter", 0f, "KitchenCounter"),
            new Entry("Stove", 0f, "Stove"),
            Entry.Flat("RangeHood", 0f, "RangeHood"),
            new Entry("Fridge", -90f, "Fridge"),
            new Entry("KitchenIsland", 0f, "KitchenIsland"),
            new Entry("BarStool", 0f, "BarStool_1"),
            new Entry("BarStool", 0f, "BarStool_2"),
            new Entry("BarStool", 0f, "BarStool_3"),
            new Entry("DiningTable", 0f, "DiningTable"),
            new Entry("DiningChair", 0f, "Chair_1"),
            new Entry("DiningChair", 180f, "Chair_2"),
            new Entry("PlantLarge", 0f, "Plant2_Pot", "Plant2_Leaves"),
            // Study
            new Entry("Bookshelf", 90f, "Bookshelf_3"),
            new Entry("Bookshelf", 90f, "Bookshelf_4"),
            new Entry("Bookshelf", 90f, "Bookshelf_5"),
            new Entry("Desk", 0f, "StudyDesk"),
            new Entry("OfficeChair", 180f, "OfficeChair"),
            new Entry("Globe", 0f, "Globe"),
            new Entry("FilingCabinet", 0f, "FilingCabinet"),
            new Entry("Armchair", 180f, "Armchair_Study"),
            new Entry("FloorLamp", 0f, "Lamp3_Pole", "Lamp3_Shade"),
            // Bedroom 1
            new Entry("Bed", -90f, "Bed"),
            new Entry("Nightstand", 0f, "Nightstand_1"),
            new Entry("Nightstand", 0f, "Nightstand_2"),
            new Entry("Cabinet", -90f, "Wardrobe"),
            new Entry("Desk", 90f, "Desk_Bed1"),
            new Entry("PlantSmall", 0f, "Plant6_Pot", "Plant6_Leaves"),
            // Bedroom 2
            new Entry("BunkBed", 0f, "BunkBed"),
            new Entry("ToyShelf", 0f, "ToyShelf"),
            new Entry("ToyBlocks", 0f, "ToyBlocks"),
            new Entry("Ball", 0f, "Ball"),
            new Entry("Desk", 0f, "Desk_Bed2"),
            // Wind sources (WindLayout builds their greybox under the "Wind" root).
            new Entry("FloorFan_Body", -90f, "Fan_Body"),
            new Entry("FloorFan_Blades", -90f, "Fan_Rotor"),
            new Entry("FloorFan_Body", 180f, "Fan2_Body"),
            new Entry("FloorFan_Blades", 180f, "Fan2_Rotor"),
            new Entry("FloorFan_Body", 180f, "Fan3_Body"),
            new Entry("FloorFan_Blades", 180f, "Fan3_Rotor"),
            new Entry("Radiator", -90f, "Radiator"),
            new Entry("Radiator", -90f, "Radiator2"),
            new Entry("DeskFan", 0f, "DeskFan_Study"),
            new Entry("DeskFan", 0f, "DeskFan_Bed2"),
            new Entry("CeilingFan_Base", 0f, "CeilingFan_Base"),
            Entry.Flat("CeilingFan_Blades", 0f, "CeilingFan_Rotor"),
            Entry.Flat("Window", 0f, "Window"),
        };

        /// <summary>Applies every available model under root (greybox parts are found by name at any depth).</summary>
        public static void Apply(Transform root)
        {
            foreach (var entry in Entries)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{entry.File}.fbx");
                if (model == null) continue;
                if (FindDeep(root, entry.Parts[0]) == null) continue; // entry belongs to another root
                if (!TryGetPartsBounds(root, entry.Parts, out var target))
                {
                    Log.Warn($"{entry.File}: some greybox parts not found ({string.Join(", ", entry.Parts)}).");
                    continue;
                }
                var first = FindDeep(root, entry.Parts[0]);
                foreach (var part in entry.Parts) FindDeep(root, part).GetComponent<MeshRenderer>().enabled = false;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, first.parent);
                instance.name = entry.Parts[0] + "_Model";
                Fit(instance.transform, target, entry.Yaw, entry.FitLargest);
                ClampColliders(root, entry.Parts, RendererBounds(instance.transform));
                if (System.Array.IndexOf(SurfaceModels, entry.File) >= 0) ShapeToSurface(first, instance);
                var material = MaterialFor(entry.File);
                foreach (var r in instance.GetComponentsInChildren<MeshRenderer>())
                {
                    r.sharedMaterial = material;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static bool TryGetPartsBounds(Transform root, string[] parts, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var name in parts)
            {
                var t = FindDeep(root, name);
                if (t == null || !t.TryGetComponent<MeshRenderer>(out var r)) return false;
                if (!found) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                found = true;
            }
            return found;
        }

        /// <summary>Uniform scale to fit inside target (after yaw), bottom-centre aligned.</summary>
        private static void Fit(Transform model, Bounds target, float yaw, bool fitLargest = false)
        {
            model.localScale = Vector3.one;
            model.rotation = Quaternion.Euler(0f, yaw, 0f);
            model.position = Vector3.zero;
            var b = RendererBounds(model);
            var rx = target.size.x / b.size.x;
            var ry = target.size.y / b.size.y;
            var rz = target.size.z / b.size.z;
            var s = Mathf.Min(rx, ry, rz);
            if (fitLargest)
            {
                // Ignore the target's thinnest side (a 1 cm wall plate or blade disc would shrink the whole model).
                var t = target.size;
                s = t.x <= t.y && t.x <= t.z ? Mathf.Min(ry, rz) : t.y <= t.z ? Mathf.Min(rx, rz) : Mathf.Min(rx, ry);
            }
            model.localScale = Vector3.one * s;
            b = RendererBounds(model);
            var bottomCentre = new Vector3(b.center.x, b.min.y, b.center.z);
            var targetBottom = new Vector3(target.center.x, target.min.y, target.center.z);
            model.position += targetBottom - bottomCentre;
        }

        /// <summary>
        /// Shrinks the greybox box parts to the model's bounds so no invisible collider sticks out of the model
        /// (e.g. a greybox sofa back taller than the real one). Parts are axis-aligned cubes.
        /// </summary>
        private static void ClampColliders(Transform root, string[] parts, Bounds model)
        {
            foreach (var name in parts)
            {
                var t = FindDeep(root, name);
                if (!t.TryGetComponent<BoxCollider>(out _)) continue;
                var min = Vector3.Max(t.position - t.localScale * 0.5f, model.min);
                var max = Vector3.Min(t.position + t.localScale * 0.5f, model.max);
                var size = Vector3.Max(max - min, Vector3.one * 0.01f);
                t.position = (min + max) * 0.5f;
                t.localScale = size;
            }
        }

        /// <summary>
        /// Models you land or start on whose bounds are taller than their lying/sitting surface (headboard, ladder,
        /// armrests): the box is lowered to the real surface and the parts above it get their own boxes.
        /// </summary>
        private static readonly string[] SurfaceModels = { "Bed", "BunkBed", "Armchair" };

        private const int SurfaceGrid = 8;

        /// <summary>
        /// Measures the model's top surface on a grid over the box footprint (raycasts against temporary mesh
        /// colliders), lowers the box to the most common height (the mattress / seat) and adds boxes for the cells
        /// that rise above it, merged into runs along X.
        /// </summary>
        private static void ShapeToSurface(Transform part, GameObject model)
        {
            var temps = new System.Collections.Generic.List<MeshCollider>();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var mc = filter.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = filter.sharedMesh;
                temps.Add(mc);
            }
            Physics.SyncTransforms();

            var min = part.position - part.localScale * 0.5f;
            var max = part.position + part.localScale * 0.5f;
            var cell = new Vector2((max.x - min.x) / SurfaceGrid, (max.z - min.z) / SurfaceGrid);
            var heights = new float[SurfaceGrid, SurfaceGrid];
            var all = new float[SurfaceGrid * SurfaceGrid];
            for (var ix = 0; ix < SurfaceGrid; ix++)
            for (var iz = 0; iz < SurfaceGrid; iz++)
            {
                var origin = new Vector3(min.x + (ix + 0.5f) * cell.x, max.y + 1f, min.z + (iz + 0.5f) * cell.y);
                var h = min.y;
                foreach (var mc in temps)
                    if (mc.Raycast(new Ray(origin, Vector3.down), out var hit, max.y - min.y + 2f)) h = Mathf.Max(h, hit.point.y);
                heights[ix, iz] = h;
                all[ix * SurfaceGrid + iz] = h;
            }
            foreach (var mc in temps) Object.DestroyImmediate(mc);

            System.Array.Sort(all);
            var surface = all[all.Length / 2];
            if (surface <= min.y + 0.05f) return;
            part.position = new Vector3(part.position.x, (min.y + surface) * 0.5f, part.position.z);
            part.localScale = new Vector3(part.localScale.x, surface - min.y, part.localScale.z);

            const float rise = 0.12f;
            var n = 0;
            for (var iz = 0; iz < SurfaceGrid; iz++)
            {
                var ix = 0;
                while (ix < SurfaceGrid)
                {
                    if (heights[ix, iz] <= surface + rise) { ix++; continue; }
                    var start = ix;
                    var top = heights[ix, iz];
                    while (ix < SurfaceGrid && heights[ix, iz] > surface + rise) top = Mathf.Max(top, heights[ix++, iz]);
                    var box = new GameObject($"{part.name}_Upper_{n++}", typeof(BoxCollider)).transform;
                    box.SetParent(part.parent, false);
                    box.position = new Vector3(min.x + (start + ix) * 0.5f * cell.x, (surface + top) * 0.5f, min.z + (iz + 0.5f) * cell.y);
                    box.localScale = new Vector3((ix - start) * cell.x, top - surface, cell.y);
                    GameObjectUtility.SetStaticEditorFlags(box.gameObject, StaticEditorFlags.BatchingStatic);
                }
            }
            Log.Info($"{part.name}: surface at {surface:0.00} m, {n} upper boxes.");
        }

        private static Bounds RendererBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private static Material MaterialFor(string file)
        {
            var path = $"{MaterialsFolder}/{file}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = EditorUtil.LoadOrCreateMaterial(path, Color.white);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/{file}_base_color.png");
                if (tex != null) mat.SetTexture("_BaseMap", tex);
                else Log.Warn($"{file}: no {TexturesFolder}/{file}_base_color.png, using plain white.");
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();
            }
            return mat;
        }
    }
}
