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
            Assert.IsNotNull(plane, "PaperPlane missing in Game scene (run Tools/Game/Build Paper Plane Scenes)");
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
            Assert.Greater(flight, 3f, "an untouched straight throw glides across the living room (the fan pushes it into the wall beside the doorway)");
            Assert.Greater(plane.transform.position.y, -0.5f, "fell through the floor");
            Assert.Greater(plane.transform.position.x, 0.3f, "the floor fan (left of the path, blowing +X) should push the throw right");

            yield return WaitFor(() => flow.State == GameState.GameOver, 5f, "GameOver after crash settle");
            Assert.Greater(_score, 10, "score is airborne time in tenths");
        }

        [UnityTest]
        public IEnumerator HallwayFan_CarriesPlaneIntoDiningRoom()
        {
            SceneManager.LoadScene(SceneNames.Boot);
            GameFlow flow = null;
            yield return WaitFor(() => ServiceLocator.TryGet(out flow) && flow.State == GameState.Menu, 20f, "Menu");
            yield return WaitFor(() => !ServiceLocator.Get<ISceneLoader>().IsLoading, 5f, "menu fade");
            EventBus<UIRequestEvent>.Raise(new UIRequestEvent { Request = UIRequest.Play });
            yield return WaitFor(() => flow.State == GameState.Playing && !ServiceLocator.Get<ISceneLoader>().IsLoading, 15f, "Playing");

            // Release the plane at the hallway entrance, flying straight down the hallway at cruise speed.
            var plane = Object.FindFirstObjectByType<PaperPlane>();
            plane.PlaceAt(new Vector3(0f, 3f, 6f), Quaternion.identity);
            plane.Launch(Vector3.forward * 5f, null);
            var peakSpeed = 0f;
            yield return WaitFor(() =>
            {
                peakSpeed = Mathf.Max(peakSpeed, plane.Velocity.magnitude);
                return plane.transform.position.z > 20f || plane.CurrentPhase != PaperPlane.Phase.Flying;
            }, 10f, "reach the dining room or crash");
            Debug.Log($"[Test] hallway: reached z {plane.transform.position.z:0.0}, peak speed {peakSpeed:0.00} m/s, phase {plane.CurrentPhase}");
            Assert.AreEqual(PaperPlane.Phase.Flying, plane.CurrentPhase, "crashed in the hallway");
            Assert.Greater(peakSpeed, 6f, "the hallway fan should push the plane along (cruise is 5 m/s)");
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
