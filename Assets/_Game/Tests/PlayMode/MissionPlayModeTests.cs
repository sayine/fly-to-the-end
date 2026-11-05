using System;
using System.Collections;
using Game.Flight;
using Game.Levels;
using NUnit.Framework;
using Template.Core;
using Template.Flow;
using Template.Services.Save;
using Template.Services.SceneLoading;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>
    /// Every mission type won and lost in the real Game scene through the real flow (SPEC acceptance criteria).
    /// The plane is launched normally, then placed where needed (into gems, through rings, onto targets).
    /// </summary>
    public class MissionPlayModeTests
    {
        private const string CatalogPath = "Assets/_Game/Configs/LevelCatalog.asset";
        private GameFlow _flow;
        private PaperPlane _plane;
        private PaperPlaneGame _game;
        private MissionDirector _mission;

        // Level numbers (1-based) in LevelBuilder's table.
        private const int AirtimeLevel = 1, GemsLevel = 2, LandLevel = 3, RingsLevel = 4, ReachLevel = 5;

        [UnityTest]
        public IEnumerator Airtime_StayingUpPastTheGoal_Wins()
        {
            yield return StartLevel(AirtimeLevel);
            Launch(Vector3.forward * 5f);
            var hold = new Vector3(0f, 3f, -12f);
            var end = Time.time + _mission.Tracker.Goal + 0.5f;
            while (Time.time < end)
            {
                _plane.Body.position = hold; // keep it safely in the air
                yield return null;
            }
            _plane.Crash(null);
            yield return ExpectState(GameState.LevelComplete);
            Assert.GreaterOrEqual(Progress.StarsOf(AirtimeLevel - 1), 1);
        }

        [UnityTest]
        public IEnumerator Land_SoftTouchdownOnTarget_Wins()
        {
            yield return StartLevel(LandLevel);
            var target = Object.FindFirstObjectByType<LandingTarget>();
            Assert.IsNotNull(target, "landing target placed");
            _plane.PlaceAt(target.transform.position + Vector3.up * 0.35f, Quaternion.identity);
            Launch(new Vector3(0f, -1.2f, 0.2f));
            yield return WaitFor(() => _plane.CurrentPhase != PaperPlane.Phase.Flying, 5f, "touchdown");
            Assert.AreEqual(PaperPlane.Phase.Landed, _plane.CurrentPhase, "a soft touchdown is a landing");
            yield return ExpectState(GameState.LevelComplete);
            Assert.AreEqual(3, Progress.StarsOf(LandLevel - 1), "dead centre = 3 stars");
        }

        [UnityTest]
        public IEnumerator Land_HardImpact_Fails()
        {
            yield return StartLevel(LandLevel);
            var target = Object.FindFirstObjectByType<LandingTarget>();
            _plane.PlaceAt(target.transform.position + Vector3.up * 1.2f, Quaternion.identity);
            Launch(new Vector3(0f, -7f, 0f));
            yield return WaitFor(() => _plane.CurrentPhase != PaperPlane.Phase.Flying, 5f, "impact");
            Assert.AreEqual(PaperPlane.Phase.Crashed, _plane.CurrentPhase);
            yield return ExpectState(GameState.GameOver);
        }

        [UnityTest]
        public IEnumerator Gems_CollectingAll_Wins()
        {
            yield return StartLevel(GemsLevel);
            var gems = _mission.Level.Gems;
            Launch(Vector3.forward * 5f);
            foreach (var gem in gems)
            {
                _plane.Body.position = gem;
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
            }
            Assert.AreEqual(gems.Length, _mission.Tracker.Gems, "every gem counted");
            yield return ExpectState(GameState.LevelComplete);
            Assert.AreEqual(3, Progress.StarsOf(GemsLevel - 1));
        }

        [UnityTest]
        public IEnumerator Gems_CrashWithNone_Fails()
        {
            yield return StartLevel(GemsLevel);
            Launch(Vector3.forward * 5f);
            yield return null;
            _plane.Crash(null);
            yield return ExpectState(GameState.GameOver);
        }

        [UnityTest]
        public IEnumerator Rings_OnlyInOrder_AllRingsWin()
        {
            yield return StartLevel(RingsLevel);
            var rings = _mission.Level.Rings;
            Launch(Vector3.forward * 5f);
            yield return Visit(rings[1].Position);
            Assert.AreEqual(0, _mission.Tracker.NextRing, "ring 2 before ring 1 doesn't count");
            foreach (var ring in rings) yield return Visit(ring.Position);
            Assert.AreEqual(rings.Length, _mission.Tracker.NextRing);
            yield return ExpectState(GameState.LevelComplete);
        }

        [UnityTest]
        public IEnumerator Rings_CrashBeforeTheLast_Fails()
        {
            yield return StartLevel(RingsLevel);
            Launch(Vector3.forward * 5f);
            yield return Visit(_mission.Level.Rings[0].Position);
            _plane.Crash(null);
            yield return ExpectState(GameState.GameOver);
        }

        [UnityTest]
        public IEnumerator ReachRoom_EnteringTheRoom_Wins()
        {
            yield return StartLevel(ReachLevel);
            Launch(Vector3.forward * 5f);
            yield return Visit(_mission.Level.TargetRoom.center + Vector3.down * 0.5f);
            yield return ExpectState(GameState.LevelComplete);
        }

        [UnityTest]
        public IEnumerator ReachRoom_CrashingFirst_Fails()
        {
            yield return StartLevel(ReachLevel);
            Launch(Vector3.forward * 5f);
            yield return null;
            _plane.Crash(null);
            yield return ExpectState(GameState.GameOver);
        }

        [TearDown]
        public void TearDown()
        {
            // Don't leave test stars in the Editor's save.
            if (ServiceLocator.TryGet(out ISaveService save)) save.Delete("progress");
            Bootstrapper.Shutdown();
        }

        // ---------- Helpers ----------
        private static GameProgress Progress => ServiceLocator.Get<GameProgress>();

        private IEnumerator StartLevel(int number)
        {
            SceneManager.LoadScene(SceneNames.Boot);
            yield return WaitFor(() => ServiceLocator.TryGet(out _flow) && _flow.State == GameState.Menu, 20f, "Menu");
            yield return WaitFor(() => !ServiceLocator.Get<ISceneLoader>().IsLoading, 5f, "menu fade");
            if (ServiceLocator.TryGet(out ISaveService save)) save.Delete("progress");
            ServiceLocator.Unregister<GameProgress>();
#if UNITY_EDITOR
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            GameProgress.Get(catalog).Select(number - 1);
#endif
            EventBus<UIRequestEvent>.Raise(new UIRequestEvent { Request = UIRequest.Play });
            yield return WaitFor(() => _flow.State == GameState.Playing && !ServiceLocator.Get<ISceneLoader>().IsLoading, 15f, "Playing");
            _plane = Object.FindFirstObjectByType<PaperPlane>();
            _game = Object.FindFirstObjectByType<PaperPlaneGame>();
            _mission = Object.FindFirstObjectByType<MissionDirector>();
            Assert.AreEqual(number - 1, _mission.LevelIndex, "level selected");
        }

        private void Launch(Vector3 velocity) => Assert.IsTrue(_game.TryLaunch(velocity), "launch");

        private IEnumerator Visit(Vector3 position)
        {
            _plane.Body.position = position;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        private IEnumerator ExpectState(GameState state) => WaitFor(() => _flow.State == state, 5f, state.ToString());

        private static IEnumerator WaitFor(Func<bool> condition, float timeout, string what)
        {
            var end = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail($"Timed out waiting for: {what}");
                yield return null;
            }
        }
    }
}
