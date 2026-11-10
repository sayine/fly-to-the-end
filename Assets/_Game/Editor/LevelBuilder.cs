using Game.Levels;
using Template.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Generates the level assets (SPEC §2) from the table below: Configs/Levels/LevelNN.asset, FreeFlight.asset and
    /// LevelCatalog.asset. Positions are on the original floor plan and mapped into the real rooms (RoomLayout.ToWorld),
    /// so they follow the furniture when rooms change size.
    /// Re-running overwrites the level assets (the table is the source of truth); progress saves are unaffected.
    /// </summary>
    internal static class LevelBuilder
    {
        public const string Folder = "Assets/_Game/Configs/Levels";
        public const string CatalogPath = "Assets/_Game/Configs/LevelCatalog.asset";

        private static readonly Vector2 NoTarget = Vector2.zero;

        public static LevelCatalog Build()
        {
            EditorUtil.EnsureFolder(Folder);
            var shelf = RoomLayout.LaunchPosition;
            var kitchenStart = new Vector3(-5.6f, 2.7f, 17f);
            var studyStart = new Vector3(5.6f, 2.7f, 17f);
            var landingStart = new Vector3(-6f, 2.7f, 37f);
            var s = RoomLayout.FurnitureScale;
            // Landing heights are the tops of the props' colliders, which PropModels fits to the Meshy models; they
            // were measured at furniture scale 1.6 and grow with it (LevelLayoutTests checks every target lies flat).
            var m = s / 1.6f;
            var coffee = new Vector3(-7.6f, 0.52f * m + 0.02f, -10f);
            var dining = new Vector3(-14f, 0.97f * m + 0.02f, 10.2f);
            var bed = new Vector3(-10.73f, 0.44f * m + 0.02f, 56.7f); // open mattress, clear of the side rail (PropModels measures it)
            // More starts (book stacks on the floor or on furniture).
            var hallStart = new Vector3(0f, 2.7f, 3.5f);
            var bed2Start = new Vector3(6f, 2.7f, 37.5f);
            var islandTop = 0.9f * s;
            var islandStart = new Vector3(-12.9f, islandTop + 1f, 18f);
            var bunkTop = 1.66f * m; // top bunk mattress
            var bunkStart = new Vector3(RoomLayout.PlanBedroom2.max.x - 0.05f - 0.5f * s, bunkTop + 0.8f, 52f);
            var deskTop = 0.76f * s;
            var bed1DeskX = RoomLayout.PlanBedroom1.max.x - 0.05f - 0.4f * s;
            var bed1DeskStart = new Vector3(bed1DeskX, deskTop + 1.1f, 52f);
            var island = new Vector3(-14f, islandTop + 0.02f, 18f);
            var armchair = new Vector3(8.2f, 0.93f * m + 0.02f, 11.85f); // seat (the back is on the -Z side)

            var drafts = new[]
            {
                Level("First Flight", "Living room", MissionType.Airtime, "Stay in the air for 8 seconds",
                    shelf, 0f, false, new[] { 8f, 14f, 20f }),
                Level("Gem Trail", "Living room", MissionType.Gems, "Collect the gems",
                    shelf, 0f, false, new[] { 0.5f, 0.8f, 1f }, gems: new[]
                    {
                        new Vector3(0f, 3.1f, -18.5f), new Vector3(0.8f, 3f, -15.5f), new Vector3(1.6f, 2.9f, -12.5f),
                        new Vector3(2.2f, 2.8f, -9.5f), new Vector3(1.8f, 2.7f, -6.5f), new Vector3(0.8f, 2.6f, -3.5f),
                        new Vector3(0f, 2.5f, -1.2f),
                    }),
                Level("Touchdown", "Living room", MissionType.Land, "Land on the coffee table",
                    shelf, 0f, false, new[] { 0f, 0.7f, 0.35f }, land: coffee, landSize: new Vector2(0.9f, 1.8f)),
                Level("Hoops", "Living room", MissionType.Rings, "Fly through the rings in order",
                    shelf, 0f, false, new[] { 0f, 12f, 8f }, rings: new[]
                    {
                        Ring(0f, 3.1f, -17.5f, 0f), Ring(1.8f, 2.9f, -12.5f, 20f),
                        Ring(2.4f, 2.7f, -7.5f, 0f), Ring(0.6f, 2.6f, -2.5f, -20f),
                    }),
                Level("Down the Hall", "Living room", MissionType.ReachRoom, "Fly down the hallway to the landing",
                    shelf, 0f, false, new[] { 0f, 20f, 14f }, targetRoom: RoomLayout.Landing),
                Level("Kitchen Loop", "Kitchen", MissionType.Gems, "Collect the gems around the kitchen",
                    kitchenStart, 270f, true, new[] { 0.5f, 0.8f, 1f }, gems: new[]
                    {
                        new Vector3(-8f, 2.6f, 17f), new Vector3(-11f, 2.5f, 15f), new Vector3(-14f, 2.4f, 13.6f),
                        new Vector3(-17f, 2.4f, 13.2f), new Vector3(-19.5f, 2.5f, 15f), new Vector3(-20f, 2.6f, 18.5f),
                        new Vector3(-18f, 2.7f, 21.5f),
                    }),
                Level("Hot Stove", "Kitchen", MissionType.Airtime, "Ride the warm air over the stove: stay up 20 s",
                    kitchenStart, 270f, true, new[] { 20f, 30f, 45f }),
                Level("Dinner Is Served", "Kitchen", MissionType.Land, "Land on the dining table",
                    kitchenStart, 250f, true, new[] { 0f, 0.8f, 0.4f }, land: dining, landSize: new Vector2(2.3f, 1.3f)),
                Level("Study Slalom", "Study", MissionType.Rings, "Weave through the rings",
                    studyStart, 90f, true, new[] { 0f, 6f, 4f }, rings: new[]
                    {
                        Ring(9f, 2.6f, 17f, 90f), Ring(12.5f, 2.5f, 19f, 70f), Ring(16f, 2.4f, 17.4f, 110f),
                    }),
                Level("Bedtime", "Bedroom", MissionType.Land, "Land on the bed (mind the ceiling fan)",
                    landingStart, 0f, true, new[] { 0f, 0.9f, 0.45f }, land: bed, landSize: new Vector2(1.5f, 1.5f)),

                // ---------- Phase 4 additions: every room, one new twist at a time ----------
                Level("Hall Pass", "Hallway", MissionType.Rings, "Fly through the rings down the hallway",
                    hallStart, 0f, true, new[] { 0f, 10f, 7f }, rings: new[]
                    {
                        Ring(0f, 2.8f, 9f, 0f), Ring(-1.2f, 2.5f, 14.5f, -10f), Ring(1f, 2.6f, 23.5f, 10f), Ring(0f, 2.4f, 29f, 0f),
                    }),
                Level("Island Hop", "Kitchen", MissionType.Land, "Land on the kitchen island",
                    kitchenStart, 270f, true, new[] { 0f, 0.8f, 0.4f }, land: island, landSize: new Vector2(2.8f, 1.3f)),
                Level("Bookworm", "Study", MissionType.Gems, "Collect the gems around the study",
                    studyStart, 90f, true, new[] { 0.5f, 0.8f, 1f }, gems: new[]
                    {
                        new Vector3(8.5f, 2.6f, 17f), new Vector3(11.5f, 2.5f, 15.5f), new Vector3(14.5f, 2.4f, 14.5f),
                        new Vector3(16.5f, 2.5f, 16.5f), new Vector3(15.5f, 2.6f, 19.5f), new Vector3(12.5f, 2.7f, 21f),
                        new Vector3(9.5f, 2.6f, 20.5f),
                    }),
                Level("Warm Air", "Kids' room", MissionType.Airtime, "Find the warm radiator air: stay up 15 s",
                    bed2Start, 0f, true, new[] { 15f, 22f, 30f }),
                Level("Kitchen Run", "Hallway", MissionType.ReachRoom, "Turn left into the kitchen",
                    hallStart, 0f, true, new[] { 0f, 9f, 6f }, targetRoom: RoomLayout.Kitchen),
                Level("Window Breeze", "Bedroom", MissionType.Gems, "Collect the gems by the window",
                    bed1DeskStart, 270f, true, new[] { 0.5f, 0.8f, 1f }, surface: deskTop, gems: new[]
                    {
                        new Vector3(-5.5f, 2.3f, 52.5f), new Vector3(-8.5f, 2.2f, 53.5f), new Vector3(-11.5f, 2.1f, 54f),
                        new Vector3(-14.5f, 2.1f, 53.5f), new Vector3(-17f, 2.3f, 52.5f), new Vector3(-15f, 2.8f, 50.5f),
                    }),
                Level("Reading Nook", "Study", MissionType.Land, "Land on the armchair",
                    studyStart, 160f, true, new[] { 0f, 0.6f, 0.3f }, land: armchair, landSize: new Vector2(0.6f, 0.5f)),
                Level("Toy Hoops", "Kids' room", MissionType.Rings, "Weave through the toy room rings",
                    bed2Start, 0f, true, new[] { 0f, 10f, 7f }, rings: new[]
                    {
                        Ring(6f, 2.6f, 44f, 0f), Ring(8.6f, 2.5f, 48f, 25f), Ring(11.5f, 2.5f, 51.5f, 50f), Ring(10.5f, 2.9f, 55.5f, -45f),
                    }),
                Level("Cook's Tour", "Kitchen", MissionType.Gems, "Ride the stove's warm air for the gems",
                    kitchenStart, 270f, true, new[] { 0.5f, 0.8f, 1f }, gems: new[]
                    {
                        new Vector3(-8f, 2.8f, 18.5f), new Vector3(-11f, 3f, 22f), new Vector3(-14f, 3.2f, 24.5f),
                        new Vector3(-17.5f, 3.4f, 25f), new Vector3(-20.5f, 3.3f, 23f), new Vector3(-21f, 3f, 19f),
                        new Vector3(-18.5f, 2.6f, 14.5f),
                    }),
                Level("Fan Dance", "Bedroom", MissionType.Rings, "Circle under the ceiling fan through the rings",
                    landingStart, 0f, true, new[] { 0f, 12f, 8f }, rings: new[]
                    {
                        Ring(-6f, 2.7f, 44f, 0f), Ring(-8.5f, 2.5f, 47.5f, -40f), Ring(-13f, 2.5f, 46.5f, -110f),
                        Ring(-15.5f, 2.7f, 50.5f, -10f), Ring(-13.5f, 2.9f, 53.5f, 50f),
                    }),
                Level("Shelf Slalom", "Study", MissionType.Rings, "Slalom past the bookcases",
                    studyStart, 90f, true, new[] { 0f, 9f, 6f }, rings: new[]
                    {
                        Ring(9f, 2.6f, 15.5f, 110f), Ring(13f, 2.5f, 13f, 90f), Ring(16.2f, 2.6f, 15.5f, 0f), Ring(14.5f, 2.8f, 20.5f, -40f),
                    }),
                Level("Top Bunk", "Kids' room", MissionType.Airtime, "Launch from the top bunk: stay up 25 s",
                    bunkStart, 270f, true, new[] { 25f, 35f, 50f }, surface: bunkTop),
                Level("Bedtime Story", "Hallway", MissionType.ReachRoom, "Find your way to the kids' room",
                    hallStart, 0f, true, new[] { 0f, 14f, 10f }, targetRoom: RoomLayout.Bedroom2),
                Level("Kitchen Hoops", "Kitchen", MissionType.Rings, "Loop the kitchen through the rings",
                    kitchenStart, 270f, true, new[] { 0f, 11f, 8f }, rings: new[]
                    {
                        Ring(-8f, 2.6f, 17f, 270f), Ring(-11.5f, 2.4f, 20.5f, 300f), Ring(-15.5f, 2.6f, 22.5f, 270f),
                        Ring(-19.5f, 2.6f, 19.5f, 200f), Ring(-17f, 2.4f, 13.5f, 130f),
                    }),
                Level("Nap Time", "Bedroom", MissionType.Land, "From the desk, land on the bed",
                    bed1DeskStart, 300f, true, new[] { 0f, 0.8f, 0.4f }, surface: deskTop, land: bed, landSize: new Vector2(1.5f, 1.5f)),
                Level("Toy Box", "Kids' room", MissionType.Gems, "Collect every toy-room gem",
                    bed2Start, 0f, true, new[] { 0.5f, 0.8f, 1f }, gems: new[]
                    {
                        new Vector3(6f, 2.6f, 43.5f), new Vector3(5f, 2.4f, 47f), new Vector3(4.2f, 2.8f, 50f),
                        new Vector3(5.5f, 3.4f, 53.5f), new Vector3(8f, 3.6f, 56f), new Vector3(11f, 3.2f, 56.5f),
                        new Vector3(13.5f, 2.8f, 54f), new Vector3(14.5f, 2.5f, 50.5f),
                    }),
                Level("Night Shift", "Study", MissionType.Airtime, "Stay up 30 s in the study",
                    studyStart, 90f, true, new[] { 30f, 42f, 55f }),
                Level("Breezy", "Bedroom", MissionType.Gems, "A full loop of the bedroom",
                    bed1DeskStart, 270f, true, new[] { 0.5f, 0.8f, 1f }, surface: deskTop, gems: new[]
                    {
                        new Vector3(-6f, 2.3f, 52.5f), new Vector3(-9.5f, 2.2f, 53.5f), new Vector3(-13f, 2.3f, 53.5f),
                        new Vector3(-16.5f, 2.5f, 52.5f), new Vector3(-18f, 2.8f, 49.5f), new Vector3(-16f, 3f, 45f),
                        new Vector3(-12f, 2.8f, 43.5f), new Vector3(-7.5f, 2.6f, 45f),
                    }),
                Level("House Tour", "Living room", MissionType.ReachRoom, "From the shelf all the way to the bedroom",
                    shelf, 0f, false, new[] { 0f, 24f, 18f }, targetRoom: RoomLayout.Bedroom1),
                Level("Grand Finale", "Kitchen", MissionType.Rings, "Kitchen, hallway, study: through every ring",
                    islandStart, 90f, true, new[] { 0f, 8f, 6f }, surface: islandTop, rings: new[]
                    {
                        Ring(-8f, 2.4f, 17f, 90f), Ring(0f, 2.4f, 17f, 90f), Ring(7f, 2.5f, 17f, 90f), Ring(12f, 2.7f, 19.5f, 60f),
                    }),
            };

            var freeFlight = Asset($"{Folder}/FreeFlight.asset");
            freeFlight.Define("Free Flight", "Living room", MissionType.FreeFlight, "", shelf, 0f, false, 0f,
                new[] { 0f, 0f, 0f }, null, null, Vector3.zero, NoTarget, default);
            EditorUtility.SetDirty(freeFlight);

            // Final order: landing levels are the hardest, so they come later (one every few levels from 14 on);
            // the rest keep the table's order. Assets are named by their final number.
            var landSlots = new[] { 14, 18, 21, 24, 26, 29 };
            var lands = new System.Collections.Generic.Queue<Spec>();
            var others = new System.Collections.Generic.Queue<Spec>();
            foreach (var d in drafts) (d.Mission == MissionType.Land ? lands : others).Enqueue(d);
            var levels = new LevelConfig[drafts.Length];
            for (var i = 0; i < levels.Length; i++)
            {
                var spec = System.Array.IndexOf(landSlots, i + 1) >= 0 && lands.Count > 0 ? lands.Dequeue() : others.Dequeue();
                levels[i] = Asset($"{Folder}/Level{i + 1:00}.asset");
                spec.Apply(levels[i]);
                EditorUtility.SetDirty(levels[i]);
            }

            var catalog = EditorUtil.LoadOrCreate<LevelCatalog>(CatalogPath);
            catalog.Define(levels, freeFlight);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Log.Info($"Built {levels.Length} levels + Free Flight ({CatalogPath}).");
            return catalog;
        }

        private static RingSpec Ring(float x, float y, float z, float yaw, float radius = 1.3f) =>
            new RingSpec { Position = new Vector3(x, y, z), Yaw = yaw, Radius = radius };

        /// <summary>A level from the table, written to its asset once the final order is known.</summary>
        private sealed class Spec
        {
            public MissionType Mission;
            public System.Action<LevelConfig> Apply;
        }

        private static Spec Level(string title, string room, MissionType mission, string objective,
            Vector3 start, float yaw, bool bookStack, float[] stars, Vector3[] gems = null, RingSpec[] rings = null,
            Vector3 land = default, Vector2 landSize = default, Bounds targetRoom = default, float surface = 0f)
        {
            // Everything except the living-room shelf (already in the real house) is authored on the floor plan.
            if (bookStack) start = RoomLayout.ToWorld(start);
            if (gems != null)
            {
                gems = (Vector3[])gems.Clone();
                for (var i = 0; i < gems.Length; i++) gems[i] = RoomLayout.ToWorld(gems[i]);
            }
            if (rings != null)
            {
                rings = (RingSpec[])rings.Clone();
                for (var i = 0; i < rings.Length; i++) rings[i].Position = RoomLayout.ToWorld(rings[i].Position);
            }
            if (mission == MissionType.Land) land = RoomLayout.ToWorld(land);
            return new Spec
            {
                Mission = mission,
                Apply = level => level.Define(title, room, mission, objective, start, yaw, bookStack, surface, stars, gems, rings,
                    land, landSize, targetRoom),
            };
        }

        private static LevelConfig Asset(string path) => EditorUtil.LoadOrCreate<LevelConfig>(path);
    }
}
