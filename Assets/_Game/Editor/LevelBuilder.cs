using Game.Levels;
using Template.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Generates the level assets (SPEC §2) from the table below: Configs/Levels/LevelNN.asset, FreeFlight.asset and
    /// LevelCatalog.asset. Positions use the house layout (RoomLayout) so they stay correct if rooms move.
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
            var coffee = new Vector3(-7.6f, 0.44f * RoomLayout.FurnitureScale + 0.02f, -10f);
            var dining = new Vector3(-14f, 0.76f * RoomLayout.FurnitureScale + 0.02f, 10.2f);
            var bed = new Vector3(-11f, 1f * RoomLayout.FurnitureScale + 0.02f, 57f);

            var levels = new[]
            {
                Level(1, "First Flight", "Living room", MissionType.Airtime, "Stay in the air for 8 seconds",
                    shelf, 0f, false, new[] { 8f, 14f, 20f }),
                Level(2, "Gem Trail", "Living room", MissionType.Gems, "Collect the gems",
                    shelf, 0f, false, new[] { 0.5f, 0.8f, 1f }, gems: new[]
                    {
                        new Vector3(0f, 3.1f, -18.5f), new Vector3(0.8f, 3f, -15.5f), new Vector3(1.6f, 2.9f, -12.5f),
                        new Vector3(2.2f, 2.8f, -9.5f), new Vector3(1.8f, 2.7f, -6.5f), new Vector3(0.8f, 2.6f, -3.5f),
                        new Vector3(0f, 2.5f, -1.2f),
                    }),
                Level(3, "Touchdown", "Living room", MissionType.Land, "Land on the coffee table",
                    shelf, 0f, false, new[] { 0f, 0.7f, 0.35f }, land: coffee, landSize: new Vector2(1.1f, 1.8f)),
                Level(4, "Hoops", "Living room", MissionType.Rings, "Fly through the rings in order",
                    shelf, 0f, false, new[] { 0f, 12f, 8f }, rings: new[]
                    {
                        Ring(0f, 3.1f, -17.5f, 0f), Ring(1.8f, 2.9f, -12.5f, 20f),
                        Ring(2.4f, 2.7f, -7.5f, 0f), Ring(0.6f, 2.6f, -2.5f, -20f),
                    }),
                Level(5, "Down the Hall", "Living room", MissionType.ReachRoom, "Fly down the hallway to the landing",
                    shelf, 0f, false, new[] { 0f, 20f, 14f }, targetRoom: RoomLayout.Landing),
                Level(6, "Kitchen Loop", "Kitchen", MissionType.Gems, "Collect the gems around the kitchen",
                    kitchenStart, 270f, true, new[] { 0.5f, 0.8f, 1f }, gems: new[]
                    {
                        new Vector3(-8f, 2.6f, 17f), new Vector3(-11f, 2.5f, 15f), new Vector3(-14f, 2.4f, 13.6f),
                        new Vector3(-17f, 2.4f, 13.2f), new Vector3(-19.5f, 2.5f, 15f), new Vector3(-20f, 2.6f, 18.5f),
                        new Vector3(-18f, 2.7f, 21.5f),
                    }),
                Level(7, "Hot Stove", "Kitchen", MissionType.Airtime, "Ride the warm air over the stove: stay up 20 s",
                    kitchenStart, 270f, true, new[] { 20f, 30f, 45f }),
                Level(8, "Dinner Is Served", "Kitchen", MissionType.Land, "Land on the dining table",
                    kitchenStart, 250f, true, new[] { 0f, 0.8f, 0.4f }, land: dining, landSize: new Vector2(2.5f, 1.3f)),
                Level(9, "Study Slalom", "Study", MissionType.Rings, "Weave through the rings",
                    studyStart, 90f, true, new[] { 0f, 6f, 4f }, rings: new[]
                    {
                        Ring(9f, 2.6f, 17f, 90f), Ring(12.5f, 2.5f, 19f, 70f), Ring(16f, 2.4f, 17.4f, 110f),
                    }),
                Level(10, "Bedtime", "Bedroom", MissionType.Land, "Land on the bed (mind the ceiling fan)",
                    landingStart, 0f, true, new[] { 0f, 0.9f, 0.45f }, land: bed, landSize: new Vector2(2.2f, 2.8f)),
            };

            var freeFlight = Asset($"{Folder}/FreeFlight.asset");
            freeFlight.Define("Free Flight", "Living room", MissionType.FreeFlight, "", shelf, 0f, false, 0f,
                new[] { 0f, 0f, 0f }, null, null, Vector3.zero, NoTarget, default);
            EditorUtility.SetDirty(freeFlight);

            var catalog = EditorUtil.LoadOrCreate<LevelCatalog>(CatalogPath);
            catalog.Define(levels, freeFlight);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Log.Info($"Built {levels.Length} levels + Free Flight ({CatalogPath}).");
            return catalog;
        }

        private static RingSpec Ring(float x, float y, float z, float yaw, float radius = 1.3f) =>
            new RingSpec { Position = new Vector3(x, y, z), Yaw = yaw, Radius = radius };

        private static LevelConfig Level(int number, string title, string room, MissionType mission, string objective,
            Vector3 start, float yaw, bool bookStack, float[] stars, Vector3[] gems = null, RingSpec[] rings = null,
            Vector3 land = default, Vector2 landSize = default, Bounds targetRoom = default)
        {
            var level = Asset($"{Folder}/Level{number:00}.asset");
            level.Define(title, room, mission, objective, start, yaw, bookStack, 0f, stars, gems, rings, land, landSize, targetRoom);
            EditorUtility.SetDirty(level);
            return level;
        }

        private static LevelConfig Asset(string path) => EditorUtil.LoadOrCreate<LevelConfig>(path);
    }
}
