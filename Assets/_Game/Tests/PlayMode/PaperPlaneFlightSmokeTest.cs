using System;
using System.Collections;
using Game.Flight;
using NUnit.Framework;
using Template.Core;
using Template.Flow;
using Template.Services.SceneLoading;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>
    /// Boot → Play → launch the real Rigidbody plane in the generated room with no steering:
    /// it must glide, get pushed sideways by the floor fan, crash into the room, and the flow must reach GameOver.
    /// </summary>
    public class PaperPlaneFlightSmokeTest
    {
        private int _score;

        [UnityTest]
        public IEnumerator Launch_Glides_Crashes_GameOver()
        {
            SceneManager.LoadScene(SceneNames.Boot);
            GameFlow flow = null;
            yield return WaitFor(() => ServiceLocator.TryGet(out flow) && flow.State == GameState.Menu, 20f, "Menu");
            yield return WaitFor(() => !ServiceLocator.Get<ISceneLoader>().IsLoading, 5f, "menu fade");
            EventBus<ScoreChangedEvent>.Subscribe(OnScore);

            EventBus<UIRequestEvent>.Raise(new UIRequestEvent { Request = UIRequest.Play });
            yield return WaitFor(() => flow.State == GameState.Playing && !ServiceLocator.Get<ISceneLoader>().IsLoading, 15f, "Playing");

            var plane = Object.FindFirstObjectByType<PaperPlane>();
            var game = Object.FindFirstObjectByType<PaperPlaneGame>();
            Assert.IsNotNull(plane, "PaperPlane missing in Game scene (run Tools/Game/Build Paper Plane Scene)");
            Assert.IsNotNull(game, "PaperPlaneGame missing in Game scene");
            Assert.AreEqual(PaperPlane.Phase.Ready, plane.CurrentPhase);

            // Medium straight throw, as a flick would produce.
            var velocity = Quaternion.Euler(-5f, 0f, 0f) * Vector3.forward * 6.5f;
            Assert.IsTrue(game.TryLaunch(velocity), "launch refused while aiming");
            Assert.IsFalse(game.TryLaunch(velocity), "second launch must be refused while flying");
            var launchTime = Time.time;

            var maxWind = 0f;
            yield return WaitFor(() =>
            {
                maxWind = Mathf.Max(maxWind, plane.Wind.magnitude);
                return plane.CurrentPhase == PaperPlane.Phase.Crashed;
            }, 20f, "crash");
            var flight = Time.time - launchTime;
            Debug.Log($"[Test] untouched flight {flight:0.00}s, crash at {plane.transform.position}, score {_score}, peak wind {maxWind:0.00} m/s²");
            Assert.Greater(flight, 4f, "an untouched straight throw should glide well across the room");
            Assert.Greater(plane.transform.position.y, -0.5f, "fell through the floor");
            Assert.Greater(plane.transform.position.x, 0.3f, "the floor fan (left of the path, blowing +X) should push the throw right");

            yield return WaitFor(() => flow.State == GameState.GameOver, 5f, "GameOver after crash settle");
            Assert.Greater(_score, 10, "score is airborne time in tenths");
        }

        [TearDown]
        public void TearDown()
        {
            EventBus<ScoreChangedEvent>.Unsubscribe(OnScore);
            Bootstrapper.Shutdown(); // next test that loads Boot starts fresh
        }

        private void OnScore(ScoreChangedEvent e) => _score = e.Score;

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
