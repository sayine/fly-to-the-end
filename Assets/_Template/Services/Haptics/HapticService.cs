using System;
using System.Runtime.InteropServices;
using Template.Services.Save;
using UnityEngine;

namespace Template.Services.Haptics
{
    /// <summary>
    /// Real haptics without third-party SDKs: UIImpactFeedbackGenerator via
    /// Assets/_Template/Plugins/iOS/HCHaptics.mm. Throttled so rapid events don't turn into a continuous buzz.
    /// </summary>
    public sealed class HapticService : IHapticService
    {
        private const float MinInterval = 0.06f;
        private readonly ISaveService _save;
        private float _lastPlayTime = -1f;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void HCHaptics_Impact(int style);
#endif

        public HapticService(ISaveService save) => _save = save;

        public bool Enabled
        {
            get => _save == null || _save.Data.HapticsEnabled;
            set
            {
                if (_save == null) return;
                _save.Data.HapticsEnabled = value;
                _save.Save();
            }
        }

        public void Play(HapticType type)
        {
            if (!Enabled) return;
            var now = Time.unscaledTime;
            if (now - _lastPlayTime < MinInterval) return;
            _lastPlayTime = now;

#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                HCHaptics_Impact((int)type);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Haptics] {e.Message}");
            }
#endif
        }
    }

    /// <summary>Editor/mock haptics: logs instead of vibrating.</summary>
    public sealed class MockHapticService : IHapticService
    {
        private readonly ISaveService _save;
        public MockHapticService(ISaveService save) => _save = save;

        public bool Enabled
        {
            get => _save == null || _save.Data.HapticsEnabled;
            set
            {
                if (_save == null) return;
                _save.Data.HapticsEnabled = value;
                _save.Save();
            }
        }

        public void Play(HapticType type)
        {
            if (Enabled) Debug.Log($"[Haptics:Mock] {type}");
        }
    }
}
