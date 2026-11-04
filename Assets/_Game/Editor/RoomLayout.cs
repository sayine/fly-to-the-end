using Template.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Layout 1 architecture and furniture: a living room (launch), a hallway with a helper fan, and a dining room.
    /// Furniture is authored at real-world size and multiplied by <see cref="FurnitureScale"/> so a 25 cm plane
    /// feels like a toy in a big home (play-test: real-size furniture looked lost under the ceiling).
    /// Greybox primitives are the colliders; PropModels swaps their visuals for Meshy models.
    /// </summary>
    internal static class RoomLayout
    {
        public const float FurnitureScale = 1.6f;
        private const float WallThickness = 0.5f;
        private const float DoorHalfWidth = 2.5f;
        private const float DoorHeight = 4.5f;

        /// <summary>Interiors (world space). Index 0 is the launch room. The chase camera stays inside these.</summary>
        public static readonly Bounds[] Areas =
        {
            FromMinMax(new Vector3(-11f, 0f, -17f), new Vector3(11f, 5.5f, 5f)),   // living room
            FromMinMax(new Vector3(-2.5f, 0f, 5f), new Vector3(2.5f, 4.5f, 19f)),  // hallway
            FromMinMax(new Vector3(-10f, 0f, 19f), new Vector3(10f, 5.5f, 39f)),   // dining room
        };

        public static Bounds LivingRoom => Areas[0];
        public static Bounds Hallway => Areas[1];
        public static Bounds DiningRoom => Areas[2];

        // Launch shelf (real 1.2 × 1.9 × 0.6 m) 1.3 m in front of the living-room back wall, leaving room for the
        // chase camera behind the plane; the plane sits on top.
        public static readonly Vector3 PadSize = new Vector3(1.2f, 1.9f, 0.6f) * FurnitureScale;
        public static readonly Vector3 PadCenter = new Vector3(0f, PadSize.y * 0.5f, -17f + 1.3f + PadSize.z * 0.5f);
        public static readonly Vector3 LaunchPosition = new Vector3(0f, PadSize.y + 0.08f, PadCenter.z + PadSize.z * 0.25f);

        private static Bounds FromMinMax(Vector3 min, Vector3 max)
        {
            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        // ---------- Architecture ----------
        private const string MeshFolder = "Assets/_Game/Art/Meshes/Room";
        private static readonly float BaseboardHeight = 0.12f * FurnitureScale;
        private static readonly float TrimDepth = 0.03f * FurnitureScale;
        private static readonly float DoorTrimWidth = 0.1f * FurnitureScale;

        public static void BuildArchitecture(Transform parent, RoomTextures.Set m)
        {
            EditorUtil.EnsureFolder(MeshFolder);
            var t = WallThickness;
            var a = LivingRoom;
            var h = Hallway;
            var d = DiningRoom;
            const float floorTile = RoomTextures.FloorTileMetres;
            const float wallTile = RoomTextures.WallTileMetres;
            const float ceilingTile = RoomTextures.CeilingTileMetres;

            // One floor slab under everything.
            Slab(parent, "Floor", new Vector3(a.min.x - t, -t, a.min.z - t), new Vector3(a.max.x + t, 0f, d.max.z + t), m.Floor, floorTile);
            Slab(parent, "Ceiling_Living", new Vector3(a.min.x - t, a.max.y, a.min.z - t), new Vector3(a.max.x + t, a.max.y + t, a.max.z), m.Ceiling, ceilingTile);
            Slab(parent, "Ceiling_Dining", new Vector3(d.min.x - t, d.max.y, d.min.z), new Vector3(d.max.x + t, d.max.y + t, d.max.z + t), m.Ceiling, ceilingTile);
            // Hallway ceiling runs between the two door walls (not through their lintels: no bright strip at the doors).
            Slab(parent, "Ceiling_Hallway", new Vector3(h.min.x - t, h.max.y, h.min.z + t), new Vector3(h.max.x + t, h.max.y + t, h.max.z - t), m.Ceiling, ceilingTile);

            // Living room: full left/right/back walls, front wall with the doorway to the hallway.
            Slab(parent, "Living_Wall_Left", new Vector3(a.min.x - t, 0f, a.min.z - t), new Vector3(a.min.x, a.max.y, a.max.z + t), m.WallLiving, wallTile);
            Slab(parent, "Living_Wall_Right", new Vector3(a.max.x, 0f, a.min.z - t), new Vector3(a.max.x + t, a.max.y, a.max.z + t), m.WallLiving, wallTile);
            Slab(parent, "Living_Wall_Back", new Vector3(a.min.x, 0f, a.min.z - t), new Vector3(a.max.x, a.max.y, a.min.z), m.WallLiving, wallTile);
            WallWithDoor(parent, "Living_Wall_Front", a.min.x, a.max.x, a.max.z, a.max.z + t, a.max.y, m.WallLiving);

            // Hallway side walls (the ceiling slab above covers the top).
            Slab(parent, "Hallway_Wall_Left", new Vector3(h.min.x - t, 0f, h.min.z + t), new Vector3(h.min.x, h.max.y, h.max.z - t), m.WallHall, wallTile);
            Slab(parent, "Hallway_Wall_Right", new Vector3(h.max.x, 0f, h.min.z + t), new Vector3(h.max.x + t, h.max.y, h.max.z - t), m.WallHall, wallTile);

            // Dining room: back wall with the doorway, full front/left/right walls.
            WallWithDoor(parent, "Dining_Wall_Back", d.min.x, d.max.x, d.min.z - t, d.min.z, d.max.y, m.WallDining);
            Slab(parent, "Dining_Wall_Front", new Vector3(d.min.x, 0f, d.max.z), new Vector3(d.max.x, d.max.y, d.max.z + t), m.WallDining, wallTile);
            Slab(parent, "Dining_Wall_Left", new Vector3(d.min.x - t, 0f, d.min.z - t), new Vector3(d.min.x, d.max.y, d.max.z + t), m.WallDining, wallTile);
            Slab(parent, "Dining_Wall_Right", new Vector3(d.max.x, 0f, d.min.z - t), new Vector3(d.max.x + t, d.max.y, d.max.z + t), m.WallDining, wallTile);

            BuildTrims(parent, m.Trim);
        }

        /// <summary>White baseboards along every wall and frames around both doorways (visual only, no colliders).</summary>
        private static void BuildTrims(Transform parent, Material mat)
        {
            var trims = new GameObject("Trims").transform;
            trims.SetParent(parent, false);
            var a = LivingRoom;
            var h = Hallway;
            var d = DiningRoom;
            var hb = BaseboardHeight;
            var dp = TrimDepth;
            var t = WallThickness;

            void RoomBaseboards(string name, Bounds r, bool doorFront, bool doorBack)
            {
                Trim(trims, name + "_Left", new Vector3(r.min.x, 0f, r.min.z), new Vector3(r.min.x + dp, hb, r.max.z), mat);
                Trim(trims, name + "_Right", new Vector3(r.max.x - dp, 0f, r.min.z), new Vector3(r.max.x, hb, r.max.z), mat);
                Side(name + "_Back", r.min.z, r.min.z + dp, r, doorBack);
                Side(name + "_Front", r.max.z - dp, r.max.z, r, doorFront);
            }

            void Side(string name, float z0, float z1, Bounds r, bool door)
            {
                if (!door)
                {
                    Trim(trims, name, new Vector3(r.min.x, 0f, z0), new Vector3(r.max.x, hb, z1), mat);
                    return;
                }
                Trim(trims, name + "_L", new Vector3(r.min.x, 0f, z0), new Vector3(-DoorHalfWidth - DoorTrimWidth, hb, z1), mat);
                Trim(trims, name + "_R", new Vector3(DoorHalfWidth + DoorTrimWidth, 0f, z0), new Vector3(r.max.x, hb, z1), mat);
            }

            void DoorFrame(string name, float z0, float z1)
            {
                var w = DoorTrimWidth;
                Trim(trims, name + "_L", new Vector3(-DoorHalfWidth - w, 0f, z0), new Vector3(-DoorHalfWidth, DoorHeight + w, z1), mat);
                Trim(trims, name + "_R", new Vector3(DoorHalfWidth, 0f, z0), new Vector3(DoorHalfWidth + w, DoorHeight + w, z1), mat);
                Trim(trims, name + "_Top", new Vector3(-DoorHalfWidth, DoorHeight, z0), new Vector3(DoorHalfWidth, DoorHeight + w, z1), mat);
            }

            RoomBaseboards("Living_Baseboard", a, true, false);
            RoomBaseboards("Dining_Baseboard", d, false, true);
            Trim(trims, "Hallway_Baseboard_Left", new Vector3(h.min.x, 0f, h.min.z + t), new Vector3(h.min.x + dp, hb, h.max.z - t), mat);
            Trim(trims, "Hallway_Baseboard_Right", new Vector3(h.max.x - dp, 0f, h.min.z + t), new Vector3(h.max.x, hb, h.max.z - t), mat);
            DoorFrame("Living_DoorFrame", a.max.z - dp, a.max.z);
            DoorFrame("Dining_DoorFrame", d.min.z, d.min.z + dp);
        }

        private static void Trim(Transform parent, string name, Vector3 min, Vector3 max, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // part of the wall visually; the wall collides
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (min + max) * 0.5f;
            go.transform.localScale = max - min;
            Prepare(go, mat);
        }

        /// <summary>Wall across X at z0..z1 with a centred doorway (DoorHalfWidth, DoorHeight).</summary>
        private static void WallWithDoor(Transform parent, string name, float xMin, float xMax, float z0, float z1, float height, Material mat)
        {
            const float tile = RoomTextures.WallTileMetres;
            Slab(parent, name + "_L", new Vector3(xMin, 0f, z0), new Vector3(-DoorHalfWidth, height, z1), mat, tile);
            Slab(parent, name + "_R", new Vector3(DoorHalfWidth, 0f, z0), new Vector3(xMax, height, z1), mat, tile);
            Slab(parent, name + "_Top", new Vector3(-DoorHalfWidth, DoorHeight, z0), new Vector3(DoorHalfWidth, height, z1), mat, tile);
        }

        /// <summary>Solid box with world-scaled UVs (one texture repeat = tile metres on every face) and a box collider.</summary>
        private static void Slab(Transform parent, string name, Vector3 min, Vector3 max, Material mat, float tile)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
            go.transform.SetParent(parent, false);
            var center = (min + max) * 0.5f;
            var size = max - min;
            go.transform.localPosition = center;
            go.GetComponent<MeshFilter>().sharedMesh = WorldUvBox($"{MeshFolder}/{name}.asset", center, size, tile);
            go.GetComponent<BoxCollider>().size = size;
            Prepare(go, mat);
        }

        private static Mesh WorldUvBox(string path, Vector3 center, Vector3 size, float tile)
        {
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var uvs = new Vector2[24];
            var triangles = new int[36];
            var half = size * 0.5f;
            var faces = new[]
            {
                (Vector3.right, Vector3.up), (Vector3.left, Vector3.up),
                (Vector3.forward, Vector3.up), (Vector3.back, Vector3.up),
                (Vector3.up, Vector3.forward), (Vector3.down, Vector3.forward),
            };
            for (var f = 0; f < 6; f++)
            {
                var (n, up) = faces[f];
                var right = Vector3.Cross(n, up); // the viewer's right, looking at the face
                var c = Vector3.Scale(n, half);
                var hw = Mathf.Abs(Vector3.Dot(half, right));
                var hh = Mathf.Abs(Vector3.Dot(half, up));
                var corners = new[] { c - right * hw - up * hh, c - right * hw + up * hh, c + right * hw + up * hh, c + right * hw - up * hh };
                for (var i = 0; i < 4; i++)
                {
                    var v = f * 4 + i;
                    vertices[v] = corners[i];
                    normals[v] = n;
                    var world = center + corners[i];
                    uvs[v] = new Vector2(Vector3.Dot(world, right), Vector3.Dot(world, up)) / tile;
                }
                var tri = f * 6;
                triangles[tri] = f * 4; triangles[tri + 1] = f * 4 + 1; triangles[tri + 2] = f * 4 + 2;
                triangles[tri + 3] = f * 4; triangles[tri + 4] = f * 4 + 2; triangles[tri + 5] = f * 4 + 3;
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var create = mesh == null;
            if (create) mesh = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateTangents(); // normal maps
            mesh.RecalculateBounds();
            if (create) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ---------- Furniture ----------
        public static GameObject BuildLaunchPad(Transform parent, Material mat) =>
            Box(parent, "LaunchPad", PadCenter, PadSize, mat);

        public static void BuildFurniture(Transform parent, Material a, Material b, Material c)
        {
            var props = new GameObject("Props").transform;
            props.SetParent(parent, false);

            // Living room. Sofa (seat + back) on the left facing +X, armchair, coffee table, TV on its bench.
            const float seatDepth = 1.0f;
            Floor(props, "Sofa_Seat", -8.3f, -2f, new Vector3(seatDepth, 0.5f, 2.6f), a);
            Floor(props, "Sofa_Back", -8.3f - (seatDepth + 0.3f) * 0.5f * FurnitureScale, -2f, new Vector3(0.3f, 1.2f, 2.6f), a);
            Floor(props, "Armchair", -5.6f, -6.8f, new Vector3(1f, 0.9f, 1f), a);
            Floor(props, "CoffeeTable", -5.6f, -2f, new Vector3(0.8f, 0.44f, 1.3f), b);
            Floor(props, "TVBench", -3f, -2f, new Vector3(0.5f, 0.6f, 2.2f), b);
            Floor(props, "TV", -3f, -2f, new Vector3(0.3f, 0.9f, 1.6f), a, 0.6f);
            // Right wall: two wide bookshelves. Work desk near the launch shelf.
            var shelfX = LivingRoom.max.x - 0.05f - 0.7f * 0.5f * FurnitureScale;
            Floor(props, "Bookshelf_1", shelfX, -8.2f, new Vector3(0.7f, 2.2f, 1.8f), b);
            Floor(props, "Bookshelf_2", shelfX, -5.1f, new Vector3(0.7f, 2.2f, 1.8f), b);
            Floor(props, "Desk", 7.5f, -14.6f, new Vector3(1.6f, 0.76f, 0.8f), b);
            Lamp(props, "Lamp", -9.8f, -15.2f, b, c);
            Lamp(props, "Lamp2", 2.4f, 1.8f, b, c);
            Plant(props, "Plant", 9.6f, 3.4f, 0.5f, 1.1f, b, c);
            BoxStack(props, -9.5f, 3.4f, a, b, c);

            // Dining room: table with two chairs, tall cabinet on the right wall, two plants.
            Floor(props, "DiningTable", 4f, 28f, new Vector3(1.8f, 0.76f, 1f), b);
            Floor(props, "Chair_1", 3.3f, 28f + 0.9f * FurnitureScale, new Vector3(0.45f, 0.9f, 0.45f), a);
            Floor(props, "Chair_2", 4.7f, 28f - 0.9f * FurnitureScale, new Vector3(0.45f, 0.9f, 0.45f), a);
            Floor(props, "Cabinet", DiningRoom.max.x - 0.05f - 0.6f * 0.5f * FurnitureScale, 34f, new Vector3(0.6f, 2.5f, 1.1f), a);
            Plant(props, "Plant2", -8.4f, 36.4f, 0.6f, 1.6f, b, c);
            Plant(props, "Plant3", 8.6f, 21.2f, 0.5f, 1.1f, b, c);
        }

        /// <summary>Box standing on the floor at (x, z); size in real-world metres (scaled), raised by baseHeight (real m).</summary>
        private static GameObject Floor(Transform parent, string name, float x, float z, Vector3 realSize, Material mat, float baseHeight = 0f)
        {
            var size = realSize * FurnitureScale;
            return Box(parent, name, new Vector3(x, baseHeight * FurnitureScale + size.y * 0.5f, z), size, mat);
        }

        private static void Lamp(Transform parent, string name, float x, float z, Material shade, Material pole)
        {
            var s = FurnitureScale;
            Primitive(parent, PrimitiveType.Cylinder, name + "_Pole", new Vector3(x, 0.8f * s, z), new Vector3(0.06f, 0.8f, 0.06f) * s, pole);
            Primitive(parent, PrimitiveType.Cylinder, name + "_Shade", new Vector3(x, 1.7f * s, z), new Vector3(0.45f, 0.15f, 0.45f) * s, shade);
        }

        private static void Plant(Transform parent, string name, float x, float z, float potRadius, float leavesHeight, Material pot, Material leaves)
        {
            var s = FurnitureScale;
            var potHeight = potRadius;
            Primitive(parent, PrimitiveType.Cylinder, name + "_Pot", new Vector3(x, potHeight * 0.5f * s, z), new Vector3(potRadius, potHeight * 0.5f, potRadius) * s, pot);
            Primitive(parent, PrimitiveType.Sphere, name + "_Leaves", new Vector3(x, (potHeight + leavesHeight * 0.5f) * s, z), new Vector3(potRadius * 2f, leavesHeight, potRadius * 2f) * s, leaves);
        }

        private static void BoxStack(Transform parent, float x, float z, Material a, Material b, Material c)
        {
            var s = FurnitureScale;
            Floor(parent, "Box_1", x, z, new Vector3(0.6f, 0.6f, 0.6f), b);
            Floor(parent, "Box_2", x + 0.65f * s, z + 0.1f * s, new Vector3(0.5f, 0.5f, 0.5f), a);
            Floor(parent, "Box_3", x + 0.2f * s, z, new Vector3(0.45f, 0.4f, 0.45f), c, 0.6f);
        }

        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat) =>
            Primitive(parent, PrimitiveType.Cube, name, center, size, mat);

        public static GameObject Primitive(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Prepare(go, mat);
            return go;
        }

        private static void Prepare(GameObject go, Material mat)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }
    }
}
