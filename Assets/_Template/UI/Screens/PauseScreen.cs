using Template.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Template.UI.Screens
{
    public sealed class PauseScreen : UIScreen
    {
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _menuButton;

        public override bool IsOverlay => true;

        protected override void OnInit()
        {
            Bind(_resumeButton, () => Request(UIRequest.Resume));
            Bind(_settingsButton, () => Screens.ShowOverlay<SettingsScreen>());
            Bind(_menuButton, () => Request(UIRequest.Menu));
        }
    }
}
