using System.Collections;
using Template.Core;
using Template.Services.Ads;
using Template.Services.Analytics;
using Template.Services.Audio;
using Template.Services.Consent;
using Template.Services.Haptics;
using Template.Services.Input;
using Template.Services.Save;
using Template.Services.SceneLoading;
using Template.UI;
using Template.UI.Screens;
using UnityEngine;

namespace Template.Flow
{
    /// <summary>
    /// Lives in Boot.unity (generated). Creates every service once, runs consent → ad init,
    /// then hands control to GameFlow which loads the Menu.
    /// References are assigned by Tools/Template/Generate Base Scenes (and auto-filled in OnValidate).
    /// </summary>
    public sealed class Bootstrapper : MonoBehaviour
    {
        [SerializeField] private AdConfig _adConfig;
        [SerializeField] private InputConfig _inputConfig;
        [SerializeField] private ScreenManager _uiRootPrefab;
        [SerializeField] private int _targetFrameRate = 60;
        [SerializeField] private Color _fadeColor = new Color(0.08f, 0.09f, 0.12f);

        private static bool _booted;

        public static bool IsBooted => _booted;

        internal static void ResetStatics() => _booted = false;

        private void Start()
        {
            if (_booted)
            {
                // Boot scene re-entered (shouldn't happen in normal flow): services already exist.
                Destroy(gameObject);
                return;
            }
            StartCoroutine(Boot());
        }

        private IEnumerator Boot()
        {
            Application.targetFrameRate = _targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            ServiceLocator.Clear();
            EventBus.ClearAll();

            // 1. Core + local services (no SDKs).
            var lifecycle = AppLifecycle.Create();
            ServiceLocator.Register<IAppLifecycle>(lifecycle);

            var save = new PlayerPrefsSaveService();
            save.Data.SessionCount++;
            save.Save();
            ServiceLocator.Register<ISaveService>(save);
            lifecycle.PauseChanged += paused => { if (paused) save.Save(); };
            lifecycle.Quitting += save.Save;

            var analytics = ServiceFactory.CreateAnalytics();
            ServiceLocator.Register(analytics);
            ServiceLocator.Register<IAudioService>(AudioService.Create(save));
            ServiceLocator.Register(ServiceFactory.CreateHaptics(save));
            ServiceLocator.Register<ISceneLoader>(SceneLoader.Create(_fadeColor));
            if (_inputConfig != null) ServiceLocator.Register(PlayerInputFactory.Create(_inputConfig, lifecycle));
            else Debug.LogError("[Boot] InputConfig missing.");

            var consent = ServiceFactory.CreateConsent(_adConfig, lifecycle);
            ServiceLocator.Register(consent);
            var ads = ServiceFactory.CreateAds(_adConfig, lifecycle, analytics, save.Data.SessionCount);
            ServiceLocator.Register(ads);

            // 2. Persistent UI (created after services so screens can resolve them).
            var ui = Instantiate(_uiRootPrefab);
            ui.name = "[UIRoot]";
            DontDestroyOnLoad(ui.gameObject);
            ui.Initialize();
            ServiceLocator.Register(ui);
            ui.Show<LoadingScreen>();
            Progress(0.2f, "Loading...");

            // 3. Consent (UMP + ATT) must finish before ads initialize. Never block forever.
            var timeout = _adConfig != null ? _adConfig.ConsentTimeoutSeconds : 10f;
            var consentDone = false;
            consent.RequestConsent(() => consentDone = true);
            yield return WaitUntilOrTimeout(() => consentDone, timeout, "Consent");
            Progress(0.5f, "Loading...");

            analytics.Initialize(consent.TrackingAllowed);
            analytics.SessionStart(save.Data.SessionCount);

            // 4. Ads.
            var adsDone = false;
            ads.Initialize(consent.CanRequestAds, consent.TrackingAllowed, () => adsDone = true);
            yield return WaitUntilOrTimeout(() => adsDone, timeout, "Ads init");
            Progress(1f, "Ready");

            // 5. Game flow takes over (loads Menu).
            _booted = true;
            var flow = GameFlow.Create();
            ServiceLocator.Register(flow);
            flow.GoToMenu();
        }

        private static IEnumerator WaitUntilOrTimeout(System.Func<bool> done, float timeout, string label)
        {
            var end = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > end)
                {
                    Debug.LogWarning($"[Boot] {label} timed out after {timeout:0}s. Continuing.");
                    yield break;
                }
                yield return null;
            }
        }

        private static void Progress(float value, string status) =>
            EventBus<BootProgressEvent>.Raise(new BootProgressEvent { Progress = value, Status = status });

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_adConfig == null) _adConfig = FindAsset<AdConfig>();
            if (_inputConfig == null) _inputConfig = FindAsset<InputConfig>();
        }

        private static T FindAsset<T>() where T : Object
        {
            var guids = UnityEditor.AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            return guids.Length == 0 ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
        }
#endif
    }
}
