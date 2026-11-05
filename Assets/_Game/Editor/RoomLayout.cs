using System.Collections.Generic;
using Template.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The house (SPEC §1): living room (launch hub), hallway spine with kitchen and study off it, a landing, and two
    /// bedrooms. Rooms are data (interior box, height, materials, doorways per wall); one builder makes each room's
    /// floor, walls with door openings, ceiling, baseboards and door frames. Neighbouring rooms sit 2 × WallThickness
    /// apart so each builds its own half of a shared wall and nothing overlaps; doorways line up in both halves.
    /// Furniture is authored at real-world size × <see cref="FurnitureScale"/> (a 25 cm plane in a big home).
    /// Greybox primitives are the colliders; PropModels swaps their visuals for Meshy models.
    /// </summary>
    internal static class RoomLayout
    {
        public const float FurnitureScale = 1.6f;
        private const float WallThickness = 0.5f;
        private const float DoorWidth = 5f;
        private const float DoorHeight = 4.5f;

        private enum Side { South, North, West, East }

        private readonly struct Door
        {
            public readonly Side Side;
            public readonly float Center; // along the wall: x for South/North, z for West/East

            public Door(Side side, float center)
            {
                Side = side;
                Center = center;
            }
        }

        private sealed class Room
        {
            public string Name;
            public Bounds Interior;
            public Door[] Doors;
            public System.Func<RoomTextures.Set, Material> Wall;
            public System.Func<RoomTextures.Set, Material> Floor; // null = the shared oak floor
        }

        private static readonly Room[] Rooms =
        {
            new Room { Name = "Living", Interior = Box3(-13f, 13f, -24f, 0f, 6f), Wall = m => m.WallLiving,
                Doors = new[] { new Door(Side.North, 0f) } },
            new Room { Name = "Hallway", Interior = Box3(-3f, 3f, 1f, 34f, 5f), Wall = m => m.WallHall,
                Doors = new[] { new Door(Side.South, 0f), new Door(Side.North, 0f), new Door(Side.West, 17f), new Door(Side.East, 17f) } },
            new Room { Name = "Kitchen", Interior = Box3(-24f, -4f, 6f, 28f, 6f), Wall = m => m.WallKitchen, Floor = m => m.FloorTiles,
                Doors = new[] { new Door(Side.East, 17f) } },
            new Room { Name = "Study", Interior = Box3(4f, 20f, 8f, 26f, 6f), Wall = m => m.WallStudy,
                Doors = new[] { new Door(Side.West, 17f) } },
            new Room { Name = "Landing", Interior = Box3(-10f, 10f, 35f, 40f, 5f), Wall = m => m.WallHall,
                Doors = new[] { new Door(Side.South, 0f), new Door(Side.North, -6f), new Door(Side.North, 6f) } },
            new Room { Name = "Bedroom1", Interior = Box3(-20f, -2f, 41f, 59f, 6f), Wall = m => m.WallBed1, Floor = m => m.CarpetLavender,
                Doors = new[] { new Door(Side.South, -6f) } },
            new Room { Name = "Bedroom2", Interior = Box3(2f, 18f, 41f, 59f, 6f), Wall = m => m.WallBed2, Floor = m => m.CarpetPeach,
                Doors = new[] { new Door(Side.South, 6f) } },
        };

        /// <summary>Interiors (world space). Index 0 is the launch room. The chase camera stays inside these.</summary>
        public static readonly Bounds[] Areas = AreasOf(Rooms);

        public static Bounds LivingRoom => Rooms[0].Interior;
        public static Bounds Hallway => Rooms[1].Interior;
        public static Bounds Kitchen => Rooms[2].Interior;
        public static Bounds Study => Rooms[3].Interior;
        public static Bounds Landing => Rooms[4].Interior;
        public static Bounds Bedroom1 => Rooms[5].Interior;
        public static Bounds Bedroom2 => Rooms[6].Interior;

        // Launch shelf (real 1.2 × 1.9 × 0.6 m) 1.3 m in front of the living-room back wall, leaving room for the
        // chase camera behind the plane; the plane sits on top.
        public static readonly Vector3 PadSize = new Vector3(1.2f, 1.9f, 0.6f) * FurnitureScale;
        public static readonly Vector3 PadCenter = new Vector3(0f, PadSize.y * 0.5f, -24f + 1.3f + PadSize.z * 0.5f);
        public static readonly Vector3 LaunchPosition = new Vector3(0f, PadSize.y + 0.08f, PadCenter.z + PadSize.z * 0.25f);

        // Furniture that wind sources sit on or next to (WindLayout uses the same spots).
        public static float KitchenCounterZ => Kitchen.max.z - 0.05f - 0.65f * 0.5f * FurnitureScale;
        public static Vector3 StoveTop => new Vector3(-18.8f, 0.9f * FurnitureScale, KitchenCounterZ);
        public static Vector3 FridgeFront => new Vector3(Kitchen.min.x + 0.05f + 0.75f * FurnitureScale + 0.3f, 0f, 22f);
        public static Vector3 StudyDeskTop => new Vector3(14.6f, 0.76f * FurnitureScale, Study.max.z - 0.05f - 0.8f * 0.5f * FurnitureScale);
        public static Vector3 Bed2DeskTop => new Vector3(15.2f, 0.76f * FurnitureScale, Bedroom2.max.z - 0.05f - 0.8f * 0.5f * FurnitureScale);
        public static Vector3 StudyLamp => new Vector3(6f, 1.85f * FurnitureScale, 9.8f);

        private static Bounds Box3(float xMin, float xMax, float zMin, float zMax, float height)
        {
            var b = new Bounds();
            b.SetMinMax(new Vector3(xMin, 0f, zMin), new Vector3(xMax, height, zMax));
            return b;
        }

        private static Bounds[] AreasOf(Room[] rooms)
        {
            var areas = new Bounds[rooms.Length];
            for (var i = 0; i < rooms.Length; i++) areas[i] = rooms[i].Interior;
            return areas;
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

            // One oak slab under the whole house; rooms with their own floor get a thin overlay on top.
            var all = Rooms[0].Interior;
            foreach (var r in Rooms) all.Encapsulate(r.Interior);
            Slab(parent, "Floor", new Vector3(all.min.x - t, -t, all.min.z - t), new Vector3(all.max.x + t, 0f, all.max.z + t),
                m.FloorOak, RoomTextures.FloorTileMetres);

            var trims = new GameObject("Trims").transform;
            trims.SetParent(parent, false);
            foreach (var room in Rooms) BuildRoom(parent, trims, room, m);
        }

        private static void BuildRoom(Transform parent, Transform trims, Room room, RoomTextures.Set m)
        {
            var t = WallThickness;
            var r = room.Interior;
            var wall = room.Wall(m);
            var h = r.max.y;

            if (room.Floor != null)
            {
                var floor = room.Floor(m);
                var tile = floor == m.FloorTiles ? RoomTextures.TileFloorTileMetres : RoomTextures.CarpetTileMetres;
                Slab(parent, $"{room.Name}_Floor", new Vector3(r.min.x, 0f, r.min.z), new Vector3(r.max.x, 0.01f, r.max.z), floor, tile);
            }
            Slab(parent, $"{room.Name}_Ceiling", new Vector3(r.min.x - t, h, r.min.z - t), new Vector3(r.max.x + t, h + t, r.max.z + t),
                m.Ceiling, RoomTextures.CeilingTileMetres);

            // South/North walls include the corners; West/East walls fit between them.
            Wall(parent, room, Side.South, r.min.x - t, r.max.x + t, r.min.z - t, r.min.z, h, wall);
            Wall(parent, room, Side.North, r.min.x - t, r.max.x + t, r.max.z, r.max.z + t, h, wall);
            Wall(parent, room, Side.West, r.min.z, r.max.z, r.min.x - t, r.min.x, h, wall);
            Wall(parent, room, Side.East, r.min.z, r.max.z, r.max.x, r.max.x + t, h, wall);

            Baseboards(trims, room, m.Trim);
            foreach (var door in room.Doors) DoorFrame(trims, room, door, m.Trim);
        }

        /// <summary>
        /// One wall along `side`: runs a0..a1 along the wall, b0..b1 across it (outward thickness), split around the
        /// doors on that side with a lintel above each door.
        /// </summary>
        private static void Wall(Transform parent, Room room, Side side, float a0, float a1, float b0, float b1, float height, Material mat)
        {
            var name = $"{room.Name}_Wall_{side}";
            var cuts = new List<(float, float)>();
            foreach (var d in room.Doors)
                if (d.Side == side) cuts.Add((d.Center - DoorWidth * 0.5f, d.Center + DoorWidth * 0.5f));
            cuts.Sort((x, y) => x.Item1.CompareTo(y.Item1));

            var start = a0;
            var i = 0;
            foreach (var (c0, c1) in cuts)
            {
                WallPiece(parent, $"{name}_{i++}", side, start, c0, b0, b1, 0f, height, mat);
                WallPiece(parent, $"{name}_{i++}", side, c0, c1, b0, b1, DoorHeight, height, mat); // lintel
                start = c1;
            }
            WallPiece(parent, $"{name}_{i}", side, start, a1, b0, b1, 0f, height, mat);
        }

        private static void WallPiece(Transform parent, string name, Side side, float a0, float a1, float b0, float b1,
            float y0, float y1, Material mat)
        {
            if (a1 - a0 < 0.01f || y1 - y0 < 0.01f) return;
            var alongX = side == Side.South || side == Side.North;
            var min = alongX ? new Vector3(a0, y0, b0) : new Vector3(b0, y0, a0);
            var max = alongX ? new Vector3(a1, y1, b1) : new Vector3(b1, y1, a1);
            Slab(parent, name, min, max, mat, RoomTextures.WallTileMetres);
        }

        /// <summary>White baseboards along the inside of every wall, skipping doorways (visual only).</summary>
        private static void Baseboards(Transform parent, Room room, Material mat)
        {
            var r = room.Interior;
            var hb = BaseboardHeight;
            var dp = TrimDepth;
            foreach (Side side in System.Enum.GetValues(typeof(Side)))
            {
                var alongX = side == Side.South || side == Side.North;
                var a0 = alongX ? r.min.x : r.min.z;
                var a1 = alongX ? r.max.x : r.max.z;
                float b0, b1;
                switch (side)
                {
                    case Side.South: b0 = r.min.z; b1 = r.min.z + dp; break;
                    case Side.North: b0 = r.max.z - dp; b1 = r.max.z; break;
                    case Side.West: b0 = r.min.x; b1 = r.min.x + dp; break;
                    default: b0 = r.max.x - dp; b1 = r.max.x; break;
                }
                var start = a0;
                var i = 0;
                var cuts = new List<float>();
                foreach (var d in room.Doors) if (d.Side == side) cuts.Add(d.Center);
                cuts.Sort();
                foreach (var c in cuts)
                {
                    TrimPiece(parent, $"{room.Name}_Baseboard_{side}_{i++}", alongX, start, c - DoorWidth * 0.5f - DoorTrimWidth, b0, b1, 0f, hb, mat);
                    start = c + DoorWidth * 0.5f + DoorTrimWidth;
                }
                TrimPiece(parent, $"{room.Name}_Baseboard_{side}_{i}", alongX, start, a1, b0, b1, 0f, hb, mat);
            }
        }

        /// <summary>Frame (two posts + top) around a doorway, on the room's side of the wall.</summary>
        private static void DoorFrame(Transform parent, Room room, Door door, Material mat)
        {
            var r = room.Interior;
            var dp = TrimDepth;
            var w = DoorTrimWidth;
            var alongX = door.Side == Side.South || door.Side == Side.North;
            float b0, b1;
            switch (door.Side)
            {
                case Side.South: b0 = r.min.z; b1 = r.min.z + dp; break;
                case Side.North: b0 = r.max.z - dp; b1 = r.max.z; break;
                case Side.West: b0 = r.min.x; b1 = r.min.x + dp; break;
                default: b0 = r.max.x - dp; b1 = r.max.x; break;
            }
            var c0 = door.Center - DoorWidth * 0.5f;
            var c1 = door.Center + DoorWidth * 0.5f;
            var name = $"{room.Name}_DoorFrame_{door.Side}_{door.Center:0}";
            TrimPiece(parent, name + "_A", alongX, c0 - w, c0, b0, b1, 0f, DoorHeight + w, mat);
            TrimPiece(parent, name + "_B", alongX, c1, c1 + w, b0, b1, 0f, DoorHeight + w, mat);
            TrimPiece(parent, name + "_Top", alongX, c0, c1, b0, b1, DoorHeight, DoorHeight + w, mat);
        }

        private static void TrimPiece(Transform parent, string name, bool alongX, float a0, float a1, float b0, float b1,
            float y0, float y1, Material mat)
        {
            if (a1 - a0 < 0.01f) return;
            var min = alongX ? new Vector3(a0, y0, b0) : new Vector3(b0, y0, a0);
            var max = alongX ? new Vector3(a1, y1, b1) : new Vector3(b1, y1, a1);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // part of the wall visually; the wall collides
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (min + max) * 0.5f;
            go.transform.localScale = max - min;
            Prepare(go, mat);
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
            var s = FurnitureScale;

            // Living room (x -13..13, z -24..0). Sofa facing +X, coffee table, TV on its bench, armchair.
            const float seatDepth = 1.0f;
            Floor(props, "Sofa_Seat", -10.4f, -10f, new Vector3(seatDepth, 0.5f, 2.6f), a);
            Floor(props, "Sofa_Back", -10.4f - (seatDepth + 0.3f) * 0.5f * s, -10f, new Vector3(0.3f, 1.2f, 2.6f), a);
            Floor(props, "CoffeeTable", -7.6f, -10f, new Vector3(0.8f, 0.44f, 1.3f), b);
            Floor(props, "TVBench", -4.8f, -10f, new Vector3(0.5f, 0.6f, 2.2f), b);
            Floor(props, "TV", -4.8f, -10f, new Vector3(0.3f, 0.9f, 1.6f), a, 0.6f);
            Floor(props, "Armchair", -7.6f, -15f, new Vector3(1f, 0.9f, 1f), a);
            var livingShelfX = LivingRoom.max.x - 0.05f - 0.7f * 0.5f * s;
            Floor(props, "Bookshelf_1", livingShelfX, -14.2f, new Vector3(0.7f, 2.2f, 1.8f), b);
            Floor(props, "Bookshelf_2", livingShelfX, -11.1f, new Vector3(0.7f, 2.2f, 1.8f), b);
            Floor(props, "Desk", 8.6f, -21.9f, new Vector3(1.6f, 0.76f, 0.8f), b);
            Lamp(props, "Lamp", -11.6f, -22f, b, c);
            Lamp(props, "Lamp2", 3.2f, -3.2f, b, c);
            Plant(props, "Plant", 11.6f, -1.6f, 0.5f, 1.1f, b, c);
            BoxStack(props, -11.6f, -1.9f, a, b, c);

            // Hallway (x -3..3, z 1..34): a plant on the landing side.
            Plant(props, "Plant4", -8.6f, 36.4f, 0.5f, 1.1f, b, c);
            Plant(props, "Plant5", 8.6f, 36.4f, 0.5f, 1.1f, b, c);

            // Kitchen (x -24..-4, z 6..28; door on the east wall at z 17).
            var k = Kitchen;
            var counterZ = KitchenCounterZ;
            Floor(props, "KitchenCounter", -13.6f, counterZ, new Vector3(4f, 0.9f, 0.65f), b);
            Floor(props, "Stove", -18.8f, counterZ, new Vector3(0.8f, 0.9f, 0.65f), b);
            Floor(props, "RangeHood", -18.8f, k.max.z - 0.05f - 0.5f * 0.5f * s, new Vector3(0.8f, 0.7f, 0.5f), a, 1.55f);
            Floor(props, "Fridge", k.min.x + 0.05f + 0.75f * 0.5f * s, 22f, new Vector3(0.75f, 1.9f, 0.9f), a);
            Floor(props, "KitchenIsland", -14f, 18f, new Vector3(2f, 0.9f, 1f), b);
            for (var i = 0; i < 3; i++)
                Floor(props, $"BarStool_{i + 1}", -15.6f + i * 1.6f, 18f - (0.5f + 0.35f) * s, new Vector3(0.4f, 0.75f, 0.4f), a);
            Floor(props, "DiningTable", -14f, 10.2f, new Vector3(1.8f, 0.76f, 1f), b);
            Floor(props, "Chair_1", -14.7f, 10.2f + 0.9f * s, new Vector3(0.45f, 0.9f, 0.45f), a);
            Floor(props, "Chair_2", -13.3f, 10.2f - 0.9f * s, new Vector3(0.45f, 0.9f, 0.45f), a);
            Plant(props, "Plant2", -5.4f, 26.6f, 0.6f, 1.6f, b, c);

            // Study (x 4..20, z 8..26; door on the west wall at z 17).
            var st = Study;
            var studyShelfX = st.max.x - 0.05f - 0.7f * 0.5f * s;
            Floor(props, "Bookshelf_3", studyShelfX, 11.2f, new Vector3(0.7f, 2.2f, 1.8f), b);
            Floor(props, "Bookshelf_4", studyShelfX, 14.3f, new Vector3(0.7f, 2.2f, 1.8f), b);
            Floor(props, "Bookshelf_5", studyShelfX, 17.4f, new Vector3(0.7f, 2.2f, 1.8f), b);
            Floor(props, "StudyDesk", 13f, st.max.z - 0.05f - 0.8f * 0.5f * s, new Vector3(1.6f, 0.76f, 0.8f), b);
            Floor(props, "OfficeChair", 13f, st.max.z - 0.8f * s - 0.6f * s, new Vector3(0.6f, 1.1f, 0.6f), a);
            Floor(props, "Globe", 7.6f, 23.4f, new Vector3(0.5f, 1f, 0.5f), c);
            Floor(props, "FilingCabinet", 5.2f, st.max.z - 0.05f - 0.6f * 0.5f * s, new Vector3(0.5f, 1.3f, 0.6f), a);
            Floor(props, "Armchair_Study", 8.2f, 11.6f, new Vector3(1f, 0.9f, 1f), a);
            Lamp(props, "Lamp3", 6f, 9.8f, b, c);

            // Bedroom 1 (x -20..-2, z 41..59; door on the south wall at x -6).
            var b1 = Bedroom1;
            var bedZ = b1.max.z - 0.05f - 2.1f * 0.5f * s;
            Floor(props, "Bed", -11f, bedZ, new Vector3(1.6f, 1f, 2.1f), a);
            Floor(props, "Nightstand_1", -11f - (0.8f + 0.3f) * s, b1.max.z - 0.05f - 0.4f * 0.5f * s, new Vector3(0.5f, 0.55f, 0.4f), b);
            Floor(props, "Nightstand_2", -11f + (0.8f + 0.3f) * s, b1.max.z - 0.05f - 0.4f * 0.5f * s, new Vector3(0.5f, 0.55f, 0.4f), b);
            Floor(props, "Wardrobe", b1.min.x + 0.05f + 0.6f * 0.5f * s, 47f, new Vector3(0.6f, 2.5f, 1.1f), a);
            Floor(props, "Desk_Bed1", b1.max.x - 0.05f - 0.8f * 0.5f * s, 52f, new Vector3(0.8f, 0.76f, 1.6f), b);
            Plant(props, "Plant6", -3.4f, 42.6f, 0.5f, 1.1f, b, c);

            // Bedroom 2 (x 2..18, z 41..59; door on the south wall at x 6). Kid's room.
            var b2 = Bedroom2;
            Floor(props, "BunkBed", b2.max.x - 0.05f - 1f * 0.5f * s, 52f, new Vector3(1f, 1.7f, 2f), a);
            Floor(props, "ToyShelf", 8f, b2.max.z - 0.05f - 0.4f * 0.5f * s, new Vector3(1.2f, 1f, 0.4f), b);
            Floor(props, "ToyBlocks", 9.6f, 49f, new Vector3(0.5f, 0.3f, 0.4f), c);
            Floor(props, "Ball", 4.8f, 54f, new Vector3(0.3f, 0.3f, 0.3f), c);
            Floor(props, "Desk_Bed2", 13.6f, b2.max.z - 0.05f - 0.8f * 0.5f * s, new Vector3(1.6f, 0.76f, 0.8f), b);
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
