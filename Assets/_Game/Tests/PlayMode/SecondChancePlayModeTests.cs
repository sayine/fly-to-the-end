using System;
using System.Collections;
using Game.Flight;
using Game.Levels;
using NUnit.Framework;
using Template.Core;
using Template.Flow;
using Template.Services.Ads;
using Template.Services.Save;
using Template.Services.SceneLoading;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>
    /// Phase 4 run rules: rewarded Second chance respawns mid-air (not on the shelf), briefly invulnerable, with the
    /// mission's progress kept; a failed level never sets the Free Flight best; Boost throw strengthens the throw.
    /// Uses the Editor mock ad service (rewarded completes after its short fake ad).
    /// </summary>
    public class SecondChancePlayModeTests
    {
        private const string CatalogPath = "Assets/_Game/Configs/LevelCatalog.asset";
        private const int GemsLevel = 2;
        private GameFlow _flow;
        private PaperPlane _plane;
        private PaperPlaneGame _game;
        private MissionDirector _mission;
        private int _savedBest;

        [UnityTest]
        public IEnumerator SecondChance_RespawnsMidAir_KeepingProgress()
        {
            yield return StartRun(GemsLevel - 1);
            Assert.IsTrue(_game.TryLaunch(new Vector3(0f, 0.6f, 5f)));
            yield return new WaitForSeconds(3f); // fly normally: safe points are recorded
            var gems = _mission.Tracker.Gems;
            Assume.That(gems < (_mission.Tracker.GemTotal + 1) / 2, "few gems so far (otherwise the crash would win)");
            var padPosition = _mission.LaunchPosition;
            _plane.Crash(null);
            yield return ExpectState(GameState.GameOver);

            var ads = ServiceLocator.Get<IAdService>();
            yield return WaitFor(() => ads.IsRewardedReady, 20f, "rewarded loaded");
            EventBus<UIRequestEvent>.Raise(new UIRequestEvent { Request = UIRequest.Continue });
            yield return WaitFor(() => _flow.State == GameState.Playing, 15f, "back to Playing");

            Assert.AreEqual(PaperPlane.Phase.Flying, _plane.CurrentPhase, "respawned in the air");
            Assert.IsTrue(_plane.IsInvulnerable, "briefly invulnerable");
            Assert.Greater(Vector3.Distance(_plane.transform.position, padPosition), 3f, "not back on the shelf");
            Assert.AreEqual(gems, _mission.Tracker.Gems, "gems kept");
            Assert.AreEqual(MissionOutcome.Running, _mission.Tracker.Outcome, "mission goes on");
        }

        [UnityTest]
        public IEnumerator FailedLevel_DoesNotSetTheBest()
        {
            yield return StartRun(GemsLevel - 1);
            var save = ServiceLocator.Get<ISaveService>();
            save.Data.BestScore = 0;
            Assert.IsTrue(_game.TryLaunch(new Vector3(0f, 0.6f, 5f)));
            yield return new WaitForSeconds(0.6f);
            _plane.Crash(null);
            yield return ExpectState(GameState.GameOver);
            Assert.AreEqual(0, save.Data.BestScore, "a level's airtime is not the Free Flight best");
        }

        [UnityTest]
        public IEnumerator BoostThrow_ThrowsFaster_InFreeFlight()
        {
            yield return StartRun(GameProgress.FreeFlight);
            EventBus<BoostThrowGrantedEvent>.Raise(default);
            var throwVelocity = new Vector3(0f, 0.5f, 5f);
            Assert.IsTrue(_game.TryLaunch(throwVelocity));
            Assert.AreEqual(throwVelocity.magnitude * 1.2f, _plane.Body.linearVelocity.magnitude, 0.05f);
        }

        [SetUp]
        public void SetUp() => _savedBest = -1;

        [TearDown]
        public void TearDown()
        {
            if (ServiceLocator.TryGet(out ISaveService save))
            {
                save.Delete("progress");
                if (_savedBest >= 0)
                {
                    save.Data.BestScore = _savedBest;
                    save.Save();
                }
            }
            Bootstrapper.Shutdown();
        }

        private IEnumerator StartRun(int level)
        {
            SceneManager.LoadScene(SceneNames.Boot);
            yield return WaitFor(() => ServiceLocator.TryGet(out _flow) && _flow.State == GameState.Menu, 20f, "Menu");
            yield return WaitFor(() => !ServiceLocator.Get<ISceneLoader>().IsLoading, 5f, "menu fade");
            var save = ServiceLocator.Get<ISaveService>();
            _savedBest = save.Data.BestScore;
            save.Delete("progress");
            ServiceLocator.Unregister<GameProgress>();
#if UNITY_EDITOR
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            GameProgress.Get(catalog).Select(level);
#endif
            EventBus<UIRequestEvent>.Raise(new UIRequestEvent { Request = UIRequest.Play });
            yield return WaitFor(() => _flow.State == GameState.Playing && !ServiceLocator.Get<ISceneLoader>().IsLoading, 15f, "Playing");
            _plane = Object.FindFirstObjectByType<PaperPlane>();
            _game = Object.FindFirstObjectByType<PaperPlaneGame>();
            _mission = Object.FindFirstObjectByType<MissionDirector>();
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
