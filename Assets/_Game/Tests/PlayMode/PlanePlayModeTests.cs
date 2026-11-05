using System;
using System.Collections;
using Game.Flight;
using Game.Levels;
using Game.Planes;
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
    /// <summary>The plane chosen in the hangar (or a rewarded trial) is what flies: its stats and its look.</summary>
    public class PlanePlayModeTests
    {
        private const string LevelCatalogPath = "Assets/_Game/Configs/LevelCatalog.asset";
        private const string PlaneCatalogPath = "Assets/_Game/Configs/PlaneCatalog.asset";
        private GameFlow _flow;
        private PlaneCatalog _planes;

        [UnityTest]
        public IEnumerator SelectedPlane_FliesWithItsLook()
        {
            yield return ToMenu();
            var sky = _planes.IndexOf("classic_sky");
            Assert.IsTrue(PlaneCollection.Get(_planes).Select(sky), "a Start plane is owned");
            yield return Play();
            var plane = Object.FindFirstObjectByType<PaperPlane>();
            Assert.AreEqual(_planes.Get(sky).Material, plane.GetComponentInChildren<MeshRenderer>().sharedMaterial);
            Assert.AreEqual(_planes.Get(sky).Mesh, plane.GetComponentInChildren<MeshFilter>().sharedMesh);
        }

        [UnityTest]
        public IEnumerator TrialPlane_UsesItsStats_ForOneRun()
        {
            yield return ToMenu();
            var stunt = _planes.IndexOf("stunt_flyer"); // Agility 5, locked at first
            var collection = PlaneCollection.Get(_planes);
            Assert.IsFalse(collection.IsOwned(stunt));
            collection.SetTrial(stunt);
            yield return Play();
            var plane = Object.FindFirstObjectByType<PaperPlane>();
            var baseConfig = UnityEditorLoad<FlightConfig>("Assets/_Game/Configs/FlightConfig.asset");
            Assert.AreNotSame(baseConfig, plane.Config, "flies a stat variant, not the shared asset");
            Assert.Greater(plane.Config.TurnRate, baseConfig.TurnRate, "Agility 5 turns faster");
            Assert.AreEqual(_planes.Get(stunt).Mesh, plane.GetComponentInChildren<MeshFilter>().sharedMesh);

            Assert.IsTrue(Object.FindFirstObjectByType<PaperPlaneGame>().TryLaunch(Vector3.forward * 5f));
            yield return null;
            plane.Crash(null);
            yield return WaitFor(() => _flow.State == GameState.GameOver, 5f, "GameOver");
            Assert.IsFalse(collection.IsTrial, "trial ends with the run");
            Assert.AreEqual(collection.SelectedIndex, _planes.IndexOf(collection.ForRun.Id));
        }

        [TearDown]
        public void TearDown()
        {
            if (ServiceLocator.TryGet(out ISaveService save))
            {
                save.Delete("progress");
                save.Delete("planes");
            }
            Bootstrapper.Shutdown();
        }

        private IEnumerator ToMenu()
        {
            SceneManager.LoadScene(SceneNames.Boot);
            yield return WaitFor(() => ServiceLocator.TryGet(out _flow) && _flow.State == GameState.Menu, 20f, "Menu");
            yield return WaitFor(() => !ServiceLocator.Get<ISceneLoader>().IsLoading, 5f, "menu fade");
            if (ServiceLocator.TryGet(out ISaveService save))
            {
                save.Delete("progress");
                save.Delete("planes");
            }
            ServiceLocator.Unregister<GameProgress>();
            ServiceLocator.Unregister<PlaneCollection>();
            _planes = UnityEditorLoad<PlaneCatalog>(PlaneCatalogPath);
            GameProgress.Get(UnityEditorLoad<LevelCatalog>(LevelCatalogPath)).Select(0);
        }

        private IEnumerator Play()
        {
            EventBus<UIRequestEvent>.Raise(new UIRequestEvent { Request = UIRequest.Play });
            yield return WaitFor(() => _flow.State == GameState.Playing && !ServiceLocator.Get<ISceneLoader>().IsLoading, 15f, "Playing");
        }

        private static T UnityEditorLoad<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
#else
            return null;
#endif
        }

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
