using Template.Core;
using Template.Services.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Template.UI.Screens
{
    public sealed class MenuScreen : UIScreen
    {
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private TMP_Text _bestScoreText;

        private ISaveService _save;

        protected override void OnInit()
        {
            ServiceLocator.TryGet(out _save);
            Bind(_playButton, () => Request(UIRequest.Play));
            Bind(_settingsButton, () => Screens.ShowOverlay<SettingsScreen>());
        }

        protected override void OnShow()
        {
            if (_bestScoreText == null) return;
            var best = _save?.Data.BestScore ?? 0;
            _bestScoreText.gameObject.SetActive(best > 0);
            _bestScoreText.SetText("BEST {0}", best);
        }
    }
}
