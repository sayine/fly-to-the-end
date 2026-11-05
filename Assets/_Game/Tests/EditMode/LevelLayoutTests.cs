using System.Collections.Generic;
using System.Text;
using Game.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Every level's layout against the real house (Game scene colliders): starts, gems and rings sit in open air
    /// inside the house, rings don't cut into furniture or walls, the straight line between consecutive mission items is
    /// clear (a flyable route exists), and landing targets lie flat on a surface.
    /// </summary>
    public class LevelLayoutTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string CatalogPath = "Assets/_Game/Configs/LevelCatalog.asset";
        private const int Mask = ~0;
        private const QueryTriggerInteraction NoTriggers = QueryTriggerInteraction.Ignore;

        [Test]
        public void AllLevels_HaveClearFlyableLayouts()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // The plane parked on the shelf is not an obstacle.
            foreach (var plane in Object.FindObjectsByType<Game.Flight.PaperPlane>(FindObjectsSortMode.None)) plane.gameObject.SetActive(false);
            Physics.SyncTransforms();
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            Assert.IsNotNull(catalog);
            Assert.AreEqual(30, catalog.Count, "30 launch levels");

            var problems = new StringBuilder();
            for (var i = 0; i < catalog.Count; i++) Check(i + 1, catalog.Get(i), problems);
            Assert.IsEmpty(problems.ToString(), problems.ToString());
        }

        private static void Check(int number, LevelConfig level, StringBuilder problems)
        {
            void Fail(string what) => problems.AppendLine($"Level {number} ({level.Title}): {what}");

            var start = level.Start + Vector3.up * 0.15f;
            if (!InsideHouse(start)) Fail("start is outside the house");
            if (Physics.CheckSphere(start, 0.2f, Mask, NoTriggers)) Fail("start is blocked");

            var route = new List<(Vector3 point, string name)> { (start + Quaternion.Euler(0f, level.StartYaw, 0f) * Vector3.forward * 0.6f, "start") };

            for (var g = 0; g < level.Gems.Length; g++)
            {
                var p = level.Gems[g];
                if (!InsideHouse(p)) Fail($"gem {g + 1} is outside the house");
                if (Physics.CheckSphere(p, 0.4f, Mask, NoTriggers)) Fail($"gem {g + 1} at {p} touches something");
                route.Add((p, $"gem {g + 1}"));
            }

            for (var r = 0; r < level.Rings.Length; r++)
            {
                var ring = level.Rings[r];
                var p = ring.Position;
                if (!InsideHouse(p)) Fail($"ring {r + 1} is outside the house");
                if (Physics.CheckSphere(p, 0.6f, Mask, NoTriggers)) Fail($"ring {r + 1} opening at {p} is blocked");
                // The ring itself (radius around its centre, in the plane facing its heading).
                var facing = Quaternion.Euler(0f, ring.Yaw, 0f);
                for (var k = 0; k < 12; k++)
                {
                    var a = k * Mathf.PI * 2f / 12f;
                    var rim = p + facing * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * ring.Radius;
                    if (!Physics.CheckSphere(rim, 0.12f, Mask, NoTriggers)) continue;
                    Fail($"ring {r + 1} rim cuts into something near {rim}");
                    break;
                }
                route.Add((p, $"ring {r + 1}"));
            }

            // A clear straight line between consecutive items (gems in listed order, rings in flying order).
            for (var k = 1; k < route.Count; k++)
            {
                var from = route[k - 1].point;
                var to = route[k].point;
                var dir = to - from;
                if (Physics.SphereCast(from, 0.2f, dir.normalized, out var hit, dir.magnitude, Mask, NoTriggers))
                    Fail($"{route[k - 1].name} → {route[k].name} is blocked by {hit.collider.name} at {hit.point}");
            }

            if (level.Mission == MissionType.Land)
            {
                var c = level.LandCenter;
                var half = new Vector3(level.LandSize.x * 0.5f, 0f, level.LandSize.y * 0.5f);
                foreach (var offset in new[] { Vector3.zero, new Vector3(half.x, 0f, half.z), new Vector3(-half.x, 0f, half.z),
                             new Vector3(half.x, 0f, -half.z), new Vector3(-half.x, 0f, -half.z) })
                {
                    var probe = c + offset + Vector3.up * 0.8f;
                    var got = Physics.Raycast(probe, Vector3.down, out var hit, 2f, Mask, NoTriggers);
                    if (!got || Mathf.Abs(hit.point.y - c.y) > 0.08f || hit.normal.y < 0.95f)
                    {
                        Fail($"landing target is not flat on a surface at {c + offset}: " +
                             (got ? $"hit {hit.collider.name} at y {hit.point.y:0.00}, normal {hit.normal}" : "nothing below"));
                        break;
                    }
                }
            }

            if (level.Mission == MissionType.ReachRoom && level.TargetRoom.size == Vector3.zero) Fail("no target room");
        }

        /// <summary>Inside = a floor below and a ceiling above within room height.</summary>
        private static bool InsideHouse(Vector3 p) =>
            Physics.Raycast(p, Vector3.down, 7f, Mask, NoTriggers) && Physics.Raycast(p, Vector3.up, 7f, Mask, NoTriggers);
    }
}
