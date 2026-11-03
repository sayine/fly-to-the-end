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
        public static void BuildArchitecture(Transform parent, Material floor, Material wall, Material ceiling)
        {
            var t = WallThickness;
            var a = LivingRoom;
            var h = Hallway;
            var d = DiningRoom;

            // One floor slab under everything.
            Slab(parent, "Floor", new Vector3(a.min.x - t, -t, a.min.z - t), new Vector3(a.max.x + t, 0f, d.max.z + t), floor);
            foreach (var (name, area) in new[] { ("Living", a), ("Hallway", h), ("Dining", d) })
                Slab(parent, $"Ceiling_{name}", new Vector3(area.min.x - t, area.max.y, area.min.z), new Vector3(area.max.x + t, area.max.y + t, area.max.z), ceiling);

            // Living room: full left/right/back walls, front wall with the doorway to the hallway.
            Slab(parent, "Living_Wall_Left", new Vector3(a.min.x - t, 0f, a.min.z - t), new Vector3(a.min.x, a.max.y, a.max.z + t), wall);
            Slab(parent, "Living_Wall_Right", new Vector3(a.max.x, 0f, a.min.z - t), new Vector3(a.max.x + t, a.max.y, a.max.z + t), wall);
            Slab(parent, "Living_Wall_Back", new Vector3(a.min.x, 0f, a.min.z - t), new Vector3(a.max.x, a.max.y, a.min.z), wall);
            WallWithDoor(parent, "Living_Wall_Front", a.min.x, a.max.x, a.max.z, a.max.z + t, a.max.y, wall);

            // Hallway side walls (the ceiling slab above covers the top).
            Slab(parent, "Hallway_Wall_Left", new Vector3(h.min.x - t, 0f, h.min.z + t), new Vector3(h.min.x, h.max.y, h.max.z - t), wall);
            Slab(parent, "Hallway_Wall_Right", new Vector3(h.max.x, 0f, h.min.z + t), new Vector3(h.max.x + t, h.max.y, h.max.z - t), wall);

            // Dining room: back wall with the doorway, full front/left/right walls.
            WallWithDoor(parent, "Dining_Wall_Back", d.min.x, d.max.x, d.min.z - t, d.min.z, d.max.y, wall);
            Slab(parent, "Dining_Wall_Front", new Vector3(d.min.x, 0f, d.max.z), new Vector3(d.max.x, d.max.y, d.max.z + t), wall);
            Slab(parent, "Dining_Wall_Left", new Vector3(d.min.x - t, 0f, d.min.z - t), new Vector3(d.min.x, d.max.y, d.max.z + t), wall);
            Slab(parent, "Dining_Wall_Right", new Vector3(d.max.x, 0f, d.min.z - t), new Vector3(d.max.x + t, d.max.y, d.max.z + t), wall);
        }

        /// <summary>Wall across X at z0..z1 with a centred doorway (DoorHalfWidth, DoorHeight).</summary>
        private static void WallWithDoor(Transform parent, string name, float xMin, float xMax, float z0, float z1, float height, Material mat)
        {
            Slab(parent, name + "_L", new Vector3(xMin, 0f, z0), new Vector3(-DoorHalfWidth, height, z1), mat);
            Slab(parent, name + "_R", new Vector3(DoorHalfWidth, 0f, z0), new Vector3(xMax, height, z1), mat);
            Slab(parent, name + "_Top", new Vector3(-DoorHalfWidth, DoorHeight, z0), new Vector3(DoorHalfWidth, height, z1), mat);
        }

        private static void Slab(Transform parent, string name, Vector3 min, Vector3 max, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (min + max) * 0.5f;
            go.transform.localScale = max - min;
            Prepare(go, mat);
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
