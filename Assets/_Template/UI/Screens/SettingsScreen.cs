using Template.Core;
using Template.Services.Audio;
using Template.Services.Consent;
using Template.Services.Haptics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Template.UI.Screens
{
    /// <summary>Sound + haptics toggles and the UMP "Privacy options" entry point (when required).</summary>
    public sealed class SettingsScreen : UIScreen
    {
        [SerializeField] private Button _soundButton;
        [SerializeField] private TMP_Text _soundLabel;
        [SerializeField] private Button _hapticsButton;
        [SerializeField] private TMP_Text _hapticsLabel;
        [SerializeField] private Button _privacyButton;
        [SerializeField] private Button _closeButton;

        private IAudioService _audio;
        private IHapticService _haptics;
        private IConsentService _consent;

        public override bool IsOverlay => true;

        protected override void OnInit()
        {
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _haptics);
            ServiceLocator.TryGet(out _consent);

            Bind(_soundButton, () =>
            {
                if (_audio != null) _audio.SoundEnabled = !_audio.SoundEnabled;
                Refresh();
            });
            Bind(_hapticsButton, () =>
            {
                if (_haptics != null) _haptics.Enabled = !_haptics.Enabled;
                Refresh();
            });
            Bind(_privacyButton, () => _consent?.ShowPrivacyOptions(null));
            Bind(_closeButton, Hide);
        }

        protected override void OnShow() => Refresh();

        private void Refresh()
        {
            if (_soundLabel != null) _soundLabel.text = _audio == null || _audio.SoundEnabled ? "SOUND: ON" : "SOUND: OFF";
            if (_hapticsLabel != null) _hapticsLabel.text = _haptics == null || _haptics.Enabled ? "VIBRATION: ON" : "VIBRATION: OFF";
            if (_privacyButton != null) _privacyButton.gameObject.SetActive(_consent != null && _consent.PrivacyOptionsRequired);
        }
    }
}
