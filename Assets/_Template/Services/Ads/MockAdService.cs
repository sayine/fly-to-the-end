using System;
using System.Collections;
using Template.Core;
using Template.Services.Analytics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Template.Services.Ads
{
    /// <summary>
    /// Editor/default ad implementation: fake init/load delays, fill rates from AdConfig.Mock,
    /// and a full-screen IMGUI overlay for interstitial/rewarded (with a "Close early" button to
    /// test the no-reward path). In non-development device builds it reports "no fill" so a
    /// store build without an SDK never shows fake ads.
    /// </summary>
    public sealed class MockAdService : AdServiceBase
    {
        private readonly MockAdSettings _mock;
        private readonly bool _simulateFill;
        private MockAdOverlay _overlay;

        public MockAdService(AdConfig config, IAppLifecycle lifecycle, IAnalyticsService analytics, int sessionCount)
            : base(config, lifecycle, analytics, sessionCount, "Mock")
        {
            _mock = config != null ? config.Mock : MockAdSettings.Default;
            _simulateFill = Application.isEditor || Debug.isDebugBuild;
        }

        protected override void InitializeSdk(bool trackingAllowed, Action<bool> onInitialized)
        {
            Debug.Log($"[Ads:Mock] Initializing (trackingAllowed={trackingAllowed})...");
            Lifecycle.Run(Delay(_mock.InitDelay, () => onInitialized(true)));
        }

        protected override void LoadInterstitial()
        {
            Lifecycle.Run(Delay(_mock.LoadDelay, () =>
            {
                if (_simulateFill && Random.value <= _mock.InterstitialFillRate) OnInterstitialLoaded();
                else OnInterstitialLoadFailed("mock no fill");
            }));
        }

        protected override void LoadRewarded()
        {
            Lifecycle.Run(Delay(_mock.LoadDelay, () =>
            {
                if (_simulateFill && Random.value <= _mock.RewardedFillRate) OnRewardedLoaded();
                else OnRewardedLoadFailed("mock no fill");
            }));
        }

        protected override void ShowInterstitialInternal(string placement, Action onClosed)
        {
            Debug.Log($"[Ads:Mock] Showing INTERSTITIAL '{placement}'");
            Overlay().Show($"MOCK INTERSTITIAL\n{placement}", _mock.ShowDuration, false, completed => onClosed());
        }

        protected override void ShowRewardedInternal(string placement, Action<bool> onClosed)
        {
            Debug.Log($"[Ads:Mock] Showing REWARDED '{placement}'");
            Overlay().Show($"MOCK REWARDED\n{placement}", _mock.ShowDuration, true, completed =>
            {
                Debug.Log($"[Ads:Mock] Rewarded '{placement}' closed. Reward earned: {completed}");
                onClosed(completed);
            });
        }

        protected override void ShowBannerInternal() => Debug.Log("[Ads:Mock] Banner shown (not rendered).");
        protected override void HideBannerInternal() => Debug.Log("[Ads:Mock] Banner hidden.");

        private MockAdOverlay Overlay()
        {
            if (_overlay == null) _overlay = MockAdOverlay.Create();
            return _overlay;
        }

        private static IEnumerator Delay(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }
    }

    /// <summary>Full-screen fake ad drawn with IMGUI. Editor/dev builds only.</summary>
    public sealed class MockAdOverlay : MonoBehaviour
    {
        private string _label;
        private float _remaining;
        private bool _canSkip;
        private Action<bool> _onClosed;
        private GUIStyle _style;

        public static MockAdOverlay Create()
        {
            var go = new GameObject("[MockAdOverlay]");
            DontDestroyOnLoad(go);
            var overlay = go.AddComponent<MockAdOverlay>();
            overlay.enabled = false;
            return overlay;
        }

        public void Show(string label, float duration, bool canSkip, Action<bool> onClosed)
        {
            _label = label;
            _remaining = duration;
            _canSkip = canSkip;
            _onClosed = onClosed;
            enabled = true;
        }

        private void Update()
        {
            _remaining -= Time.unscaledDeltaTime;
            if (_remaining <= 0f) Close(true);
        }

        private void Close(bool completed)
        {
            enabled = false;
            var cb = _onClosed;
            _onClosed = null;
            cb?.Invoke(completed);
        }

        private void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(Screen.height * 0.035f),
                wordWrap = true,
            };
            GUI.depth = -1000;
            var full = new Rect(0, 0, Screen.width, Screen.height);
            GUI.color = Color.black;
            GUI.DrawTexture(full, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Box(full, $"{_label}\n\n{Mathf.CeilToInt(Mathf.Max(0f, _remaining))}", _style);

            if (_canSkip)
            {
                var w = Screen.width * 0.5f;
                var h = Screen.height * 0.07f;
                if (GUI.Button(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.8f, w, h), "Close early (no reward)", _style))
                    Close(false);
            }
        }
    }
}
