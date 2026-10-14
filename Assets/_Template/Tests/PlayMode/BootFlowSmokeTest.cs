using System;
using System.Collections;
using NUnit.Framework;
using Template.Core;
using Template.Flow;
using Template.Services.Ads;
using Template.Services.SceneLoading;
using Template.UI;
using Template.UI.Screens;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Template.Tests
{
    /// <summary>
    /// End-to-end smoke test of the generated scenes with mock services:
    /// Boot → Menu → Play → die → rewarded Continue → die → Retry → Pause/Resume → Menu.
    /// Requires Boot/Menu/Game in Build Settings (Tools/Template/Generate Base Scenes).
    /// </summary>
    public class BootFlowSmokeTest
    {
        private GameOverEvent _lastGameOver;

        [UnityTest]
        public IEnumerator Boot_ReachesMenu_AndFullLoopWorks()
        {
            SceneManager.LoadScene(SceneNames.Boot);

            GameFlow flow = null;
            yield return WaitFor(() => ServiceLocator.TryGet(out flow) && flow.State == GameState.Menu, 20f, "Menu after boot");
            yield return WaitFor(() => !ServiceLocator.Get<ISceneLoader>().IsLoading, 5f, "menu fade");
            EventBus<GameOverEvent>.Subscribe(OnGameOver); // after boot: Boot clears all channels
            var ui = ServiceLocator.Get<ScreenManager>();
            Assert.AreEqual(SceneNames.Menu, SceneManager.GetActiveScene().name);
            Assert.IsTrue(ui.IsVisible<MenuScreen>());

            Request(UIRequest.Play);
            yield return WaitFor(() => flow.State == GameState.Playing, 10f, "Playing");
            Assert.AreEqual(SceneNames.Game, SceneManager.GetActiveScene().name);
            Assert.IsTrue(ui.IsVisible<HUDScreen>());
            yield return WaitFor(() => !ServiceLocator.Get<ISceneLoader>().IsLoading, 5f, "game fade");

            // Die → GameOver with continue offered (mock rewarded fills after ~1s).
            EventBus<PlayerDiedEvent>.Raise(default);
            Assert.AreEqual(GameState.GameOver, flow.State);
            Assert.IsTrue(ui.IsVisible<GameOverScreen>());
            Assert.IsTrue(_lastGameOver.CanContinue);

            yield return WaitFor(() => ServiceLocator.Get<IAdService>().IsRewardedReady, 10f, "mock rewarded loaded");
            Request(UIRequest.Continue); // mock rewarded overlay auto-completes with reward
            yield return WaitFor(() => flow.State == GameState.Playing, 10f, "Playing after rewarded continue");

            EventBus<PlayerDiedEvent>.Raise(default);
            Assert.AreEqual(GameState.GameOver, flow.State);
            Assert.IsFalse(_lastGameOver.CanContinue, "only one continue per run");

            Request(UIRequest.Retry);
            yield return WaitFor(() => flow.State == GameState.Playing && !ServiceLocator.Get<ISceneLoader>().IsLoading, 15f, "Playing after retry");

            Request(UIRequest.Pause);
            Assert.AreEqual(GameState.Paused, flow.State);
            Assert.AreEqual(0f, Time.timeScale);
            Request(UIRequest.Resume);
            Assert.AreEqual(GameState.Playing, flow.State);
            Assert.AreEqual(1f, Time.timeScale);

            Request(UIRequest.Pause);
            Request(UIRequest.Menu);
            yield return WaitFor(() => flow.State == GameState.Menu, 10f, "Menu from pause");
            Assert.AreEqual(SceneNames.Menu, SceneManager.GetActiveScene().name);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus<GameOverEvent>.Unsubscribe(OnGameOver);
            Time.timeScale = 1f;
        }

        private void OnGameOver(GameOverEvent e) => _lastGameOver = e;

        private static void Request(UIRequest r) => EventBus<UIRequestEvent>.Raise(new UIRequestEvent { Request = r });

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
