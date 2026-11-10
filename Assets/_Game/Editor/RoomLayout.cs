using System.Collections.Generic;
using Template.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The house (SPEC §1): living room (launch hub), hallway spine with kitchen and study off it, a landing, and two
    /// bedrooms. Rooms are data (interior box, materials, doorways per wall); one builder makes each room's floor,
    /// walls with door openings, ceiling, baseboards, rounded inside corners and rounded door columns. Every room has
    /// the same ceiling height and doorways are wide and full height, so the chase camera never meets a step.
    /// Neighbouring rooms sit 2 × WallThickness apart so each builds its own half of a shared wall; doorways line up.
    /// Furniture, wind and level positions are authored on the original, smaller floor plan (each room's Plan box) and
    /// mapped into the room's real box with <see cref="ToWorld"/>, so the rooms can grow without re-authoring them.
    /// Furniture is authored at real-world size × <see cref="FurnitureScale"/> (a 25 cm plane in a big home).
    /// Greybox primitives are the colliders; PropModels swaps their visuals for Meshy models.
    /// </summary>
    internal static class RoomLayout
    {
        public const float FurnitureScale = 1.85f; // real size × this: a 25 cm plane in a big home
        private const float WallThickness = 0.5f;
        private const float DoorWidth = 8f;
        /// <summary>One ceiling height for the whole house (no steps between rooms, hallway and doorways).</summary>
        public const float CeilingHeight = 7f;

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
            public Bounds Interior;   // real box (world)
            public Bounds Plan;       // original floor-plan box that furniture / wind / levels are authored in
            public float Corner;      // radius of the rounded inside corners
            public Door[] Doors;
            public System.Func<RoomTextures.Set, Material> Wall;
            public System.Func<RoomTextures.Set, Material> Floor; // null = the shared oak floor
        }

        private const float H = CeilingHeight;

        private static readonly Room[] Rooms =
        {
            new Room { Name = "Living", Interior = Box3(-18f, 18f, -32f, 0f, H), Plan = Box3(-13f, 13f, -24f, 0f, 6f), Corner = 3f,
                Wall = m => m.WallLiving, Doors = new[] { new Door(Side.North, 0f) } },
            new Room { Name = "Hallway", Interior = Box3(-5f, 5f, 1f, 44f, H), Plan = Box3(-3f, 3f, 1f, 34f, 5f), Corner = 1.5f,
                Wall = m => m.WallHall,
                Doors = new[] { new Door(Side.South, 0f), new Door(Side.North, 0f), new Door(Side.West, 22f), new Door(Side.East, 22f) } },
            new Room { Name = "Kitchen", Interior = Box3(-32f, -6f, 8f, 36f, H), Plan = Box3(-24f, -4f, 6f, 28f, 6f), Corner = 3f,
                Wall = m => m.WallKitchen, Floor = m => m.FloorTiles, Doors = new[] { new Door(Side.East, 22f) } },
            new Room { Name = "Study", Interior = Box3(6f, 28f, 11f, 33f, H), Plan = Box3(4f, 20f, 8f, 26f, 6f), Corner = 3f,
                Wall = m => m.WallStudy, Doors = new[] { new Door(Side.West, 22f) } },
            new Room { Name = "Landing", Interior = Box3(-14f, 14f, 45f, 53f, H), Plan = Box3(-10f, 10f, 35f, 40f, 5f), Corner = 1.5f,
                Wall = m => m.WallHall, Doors = new[] { new Door(Side.South, 0f), new Door(Side.North, -8f), new Door(Side.North, 8f) } },
            new Room { Name = "Bedroom1", Interior = Box3(-28f, -1f, 54f, 78f, H), Plan = Box3(-20f, -2f, 41f, 59f, 6f), Corner = 3f,
                Wall = m => m.WallBed1, Floor = m => m.CarpetLavender, Doors = new[] { new Door(Side.South, -8f) } },
            new Room { Name = "Bedroom2", Interior = Box3(0f, 24f, 54f, 76f, H), Plan = Box3(2f, 18f, 41f, 59f, 6f), Corner = 3f,
                Wall = m => m.WallBed2, Floor = m => m.CarpetPeach, Doors = new[] { new Door(Side.South, 8f) } },
        };

        /// <summary>
        /// Maps a point authored on the original floor plan into the real house: the room whose Plan box holds it (or
        /// the nearest one) is stretched onto its real box, X and Z independently. Heights are unchanged.
        /// </summary>
        public static Vector3 ToWorld(Vector3 plan)
        {
            Room best = null;
            var bestDistance = float.MaxValue;
            foreach (var room in Rooms)
            {
                var b = room.Plan;
                var dx = Mathf.Max(b.min.x - plan.x, 0f, plan.x - b.max.x);
                var dz = Mathf.Max(b.min.z - plan.z, 0f, plan.z - b.max.z);
                var d = dx * dx + dz * dz;
                if (d < bestDistance) { bestDistance = d; best = room; }
            }
            var pb = best.Plan;
            var wb = best.Interior;
            return new Vector3(
                Mathf.LerpUnclamped(wb.min.x, wb.max.x, (plan.x - pb.min.x) / pb.size.x),
                plan.y,
                Mathf.LerpUnclamped(wb.min.z, wb.max.z, (plan.z - pb.min.z) / pb.size.z));
        }

        public static Vector3 ToWorld(float x, float y, float z) => ToWorld(new Vector3(x, y, z));

        /// <summary>Interiors (world space). Index 0 is the launch room. The chase camera stays inside these.</summary>
        public static readonly Bounds[] Areas = AreasOf(Rooms);

        public static Bounds LivingRoom => Rooms[0].Interior;
        public static Bounds Hallway => Rooms[1].Interior;
        public static Bounds Kitchen => Rooms[2].Interior;
        public static Bounds Study => Rooms[3].Interior;
        public static Bounds Landing => Rooms[4].Interior;
        public static Bounds Bedroom1 => Rooms[5].Interior;
        public static Bounds Bedroom2 => Rooms[6].Interior;

        // Original floor-plan boxes (authoring space for furniture, wind and levels; see ToWorld).
        public static Bounds PlanLivingRoom => Rooms[0].Plan;
        public static Bounds PlanHallway => Rooms[1].Plan;
        public static Bounds PlanKitchen => Rooms[2].Plan;
        public static Bounds PlanStudy => Rooms[3].Plan;
        public static Bounds PlanLanding => Rooms[4].Plan;
        public static Bounds PlanBedroom1 => Rooms[5].Plan;
        public static Bounds PlanBedroom2 => Rooms[6].Plan;

        // Launch shelf (real 1.2 × 1.9 × 0.6 m) 1.3 m in front of the living-room back wall, leaving room for the
        // chase camera behind the plane; the plane sits on top.
        public static readonly Vector3 PadSize = new Vector3(1.2f, 1.9f, 0.6f) * FurnitureScale;
        public static readonly Vector3 PadCenter = new Vector3(0f, PadSize.y * 0.5f, -32f + 1.6f + PadSize.z * 0.5f);
        public static readonly Vector3 LaunchPosition = new Vector3(0f, PadSize.y + 0.08f, PadCenter.z + PadSize.z * 0.25f);

        // Furniture that wind sources sit on or next to (WindLayout uses the same spots).
        // (Plan coordinates mapped to the real house.)
        private static float PlanKitchenCounterZ => PlanKitchen.max.z - 0.05f - 0.65f * 0.5f * FurnitureScale;
        public static Vector3 StoveTop => ToWorld(-18.8f, 0.9f * FurnitureScale, PlanKitchenCounterZ);
        public static Vector3 FridgeFront => ToWorld(PlanKitchen.min.x + 0.05f + 0.75f * FurnitureScale + 0.3f, 0f, 22f);
        public static Vector3 StudyDeskTop => ToWorld(14.6f, 0.76f * FurnitureScale, PlanStudy.max.z - 0.05f - 0.8f * 0.5f * FurnitureScale);
        public static Vector3 Bed2DeskTop => ToWorld(15.2f, 0.76f * FurnitureScale, PlanBedroom2.max.z - 0.05f - 0.8f * 0.5f * FurnitureScale);
        public static Vector3 StudyLamp => ToWorld(6f, 1.85f * FurnitureScale, 9.8f);

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
        private static readonly float DoorTrimWidth = WallThickness + 0.1f; // baseboards stop at the door columns

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
            Columns.Clear();
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
            RoundedCorners(parent, room, wall);
            foreach (var door in room.Doors) DoorColumns(parent, room, door, m.Trim);
        }

        /// <summary>
        /// Rounded inside corners: each corner gets a quarter-circle fillet (four chord panels) so the walls curve into
        /// each other. The radius shrinks where a doorway is close to the corner.
        /// </summary>
        private static void RoundedCorners(Transform parent, Room room, Material mat)
        {
            var r = room.Interior;
            var corners = new[]
            {
                (new Vector2(r.min.x, r.min.z), new Vector2(1f, 1f), Side.South, Side.West),
                (new Vector2(r.max.x, r.min.z), new Vector2(-1f, 1f), Side.South, Side.East),
                (new Vector2(r.min.x, r.max.z), new Vector2(1f, -1f), Side.North, Side.West),
                (new Vector2(r.max.x, r.max.z), new Vector2(-1f, -1f), Side.North, Side.East),
            };
            const int segments = 4;
            const float thickness = 0.6f;
            var k = 0;
            foreach (var (corner, inward, sideAlongX, sideAlongZ) in corners)
            {
                var radius = room.Corner;
                foreach (var d in room.Doors)
                {
                    // Distance from the corner to the nearest edge of a door on one of the two walls.
                    if (d.Side == sideAlongX) radius = Mathf.Min(radius, Mathf.Abs(d.Center - corner.x) - DoorWidth * 0.5f - 0.5f);
                    if (d.Side == sideAlongZ) radius = Mathf.Min(radius, Mathf.Abs(d.Center - corner.y) - DoorWidth * 0.5f - 0.5f);
                }
                if (radius < 0.75f) continue;
                var centre = corner + inward * radius;
                for (var i = 0; i < segments; i++)
                {
                    var a0 = Mathf.PI * 0.5f * i / segments;
                    var a1 = Mathf.PI * 0.5f * (i + 1) / segments;
                    var p0 = centre - new Vector2(inward.x * Mathf.Cos(a0), inward.y * Mathf.Sin(a0)) * radius;
                    var p1 = centre - new Vector2(inward.x * Mathf.Cos(a1), inward.y * Mathf.Sin(a1)) * radius;
                    var mid = (p0 + p1) * 0.5f;
                    var outward = (mid - centre).normalized; // toward the corner: the panel sits behind the curve
                    var chord = p1 - p0;
                    var c = mid + outward * thickness * 0.5f;
                    var rotation = Quaternion.LookRotation(new Vector3(chord.x, 0f, chord.y), Vector3.up);
                    RotatedSlab(parent, $"{room.Name}_Corner_{k++}", new Vector3(c.x, r.max.y * 0.5f, c.y),
                        new Vector3(thickness, r.max.y, chord.magnitude + 0.2f), rotation, mat, RoomTextures.WallTileMetres);
                }
            }
        }

        private static readonly HashSet<Vector2Int> Columns = new HashSet<Vector2Int>();

        /// <summary>Round columns on both edges of a doorway, as thick as the shared wall: no sharp wall ends.</summary>
        private static void DoorColumns(Transform parent, Room room, Door door, Material mat)
        {
            var r = room.Interior;
            var t = WallThickness;
            var alongX = door.Side == Side.South || door.Side == Side.North;
            var across = door.Side switch
            {
                Side.South => r.min.z - t,
                Side.North => r.max.z + t,
                Side.West => r.min.x - t,
                _ => r.max.x + t,
            };
            foreach (var edge in new[] { door.Center - DoorWidth * 0.5f, door.Center + DoorWidth * 0.5f })
            {
                var p = alongX ? new Vector2(edge, across) : new Vector2(across, edge);
                if (!Columns.Add(Vector2Int.RoundToInt(p * 10f))) continue; // the room on the other side made it
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = $"DoorColumn_{room.Name}_{Columns.Count}";
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(p.x, r.max.y * 0.5f, p.y);
                go.transform.localScale = new Vector3(t * 2f + 0.1f, r.max.y * 0.5f, t * 2f + 0.1f);
                Prepare(go, mat);
            }
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
                WallPiece(parent, $"{name}_{i++}", side, start, c0, b0, b1, 0f, height, mat); // doorways are full height
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

        /// <summary>Like Slab, but turned (rounded-corner panels). UVs follow the panel's own axes.</summary>
        private static void RotatedSlab(Transform parent, string name, Vector3 center, Vector3 size, Quaternion rotation, Material mat, float tile)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation;
            go.GetComponent<MeshFilter>().sharedMesh = WorldUvBox($"{MeshFolder}/{name}.asset", new Vector3(0f, center.y, 0f), size, tile);
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

            // Positions below are on the original floor plan (ToWorld maps them into the bigger rooms).
            // Living room (plan x -13..13, z -24..0). Sofa facing +X, coffee table, TV on its bench, armchair.
            const float seatDepth = 1.0f;
            Floor(props, "Sofa_Seat", -10.4f, -10f, new Vector3(seatDepth, 0.5f, 2.6f), a);
            Floor(props, "Sofa_Back", -10.4f - (seatDepth + 0.3f) * 0.5f * s, -10f, new Vector3(0.3f, 1.2f, 2.6f), a);
            Floor(props, "CoffeeTable", -7.6f, -10f, new Vector3(0.8f, 0.44f, 1.3f), b);
            Floor(props, "TVBench", -4.8f, -10f, new Vector3(0.5f, 0.6f, 2.2f), b);
            Floor(props, "TV", -4.8f, -10f, new Vector3(0.3f, 0.9f, 1.6f), a, 0.6f);
            Floor(props, "Armchair", -7.6f, -15f, new Vector3(1f, 0.9f, 1f), a);
            var livingShelfX = PlanLivingRoom.max.x - 0.05f - 0.7f * 0.5f * s;
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
            var k = PlanKitchen;
            var counterZ = PlanKitchenCounterZ;
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
            var st = PlanStudy;
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
            var b1 = PlanBedroom1;
            var bedZ = b1.max.z - 0.05f - 2.1f * 0.5f * s;
            Floor(props, "Bed", -11f, bedZ, new Vector3(1.6f, 1f, 2.1f), a);
            Floor(props, "Nightstand_1", -11f - (0.8f + 0.3f) * s, b1.max.z - 0.05f - 0.4f * 0.5f * s, new Vector3(0.5f, 0.55f, 0.4f), b);
            Floor(props, "Nightstand_2", -11f + (0.8f + 0.3f) * s, b1.max.z - 0.05f - 0.4f * 0.5f * s, new Vector3(0.5f, 0.55f, 0.4f), b);
            Floor(props, "Wardrobe", b1.min.x + 0.05f + 0.6f * 0.5f * s, 47f, new Vector3(0.6f, 2.5f, 1.1f), a);
            Floor(props, "Desk_Bed1", b1.max.x - 0.05f - 0.8f * 0.5f * s, 52f, new Vector3(0.8f, 0.76f, 1.6f), b);
            Plant(props, "Plant6", -3.4f, 42.6f, 0.5f, 1.1f, b, c);

            // Bedroom 2 (x 2..18, z 41..59; door on the south wall at x 6). Kid's room.
            var b2 = PlanBedroom2;
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
            return Box(parent, name, ToWorld(x, baseHeight * FurnitureScale + size.y * 0.5f, z), size, mat);
        }

        private static void Lamp(Transform parent, string name, float x, float z, Material shade, Material pole)
        {
            var s = FurnitureScale;
            var w = ToWorld(x, 0f, z);
            x = w.x;
            z = w.z;
            Primitive(parent, PrimitiveType.Cylinder, name + "_Pole", new Vector3(x, 0.8f * s, z), new Vector3(0.06f, 0.8f, 0.06f) * s, pole);
            Primitive(parent, PrimitiveType.Cylinder, name + "_Shade", new Vector3(x, 1.7f * s, z), new Vector3(0.45f, 0.15f, 0.45f) * s, shade);
        }

        private static void Plant(Transform parent, string name, float x, float z, float potRadius, float leavesHeight, Material pot, Material leaves)
        {
            var s = FurnitureScale;
            var w = ToWorld(x, 0f, z);
            x = w.x;
            z = w.z;
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
