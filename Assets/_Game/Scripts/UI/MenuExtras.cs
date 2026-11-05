using Game.Levels;
using Template.Core;
using Template.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Paper Flight additions to the template Menu screen: "LEVEL n" under Play (Play continues from the next
    /// unfinished level), a Levels button (level map) and a Free Flight button (the original endless mode).
    /// Added to MenuScreen.prefab by the game's scene generator.
    /// </summary>
    public sealed class MenuExtras : MonoBehaviour
    {
        [SerializeField] private LevelCatalog _catalog;
        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private Button _levelsButton;
        [SerializeField] private Button _freeFlightButton;

        private void Awake()
        {
            if (_levelsButton != null)
                _levelsButton.onClick.AddListener(() =>
                {
                    if (ServiceLocator.TryGet(out ScreenManager screens)) screens.ShowOverlay<LevelMapScreen>();
                });
            if (_freeFlightButton != null)
                _freeFlightButton.onClick.AddListener(() =>
                {
                    GameProgress.Get(_catalog).Select(GameProgress.FreeFlight);
                    EventBus<UIRequestEvent>.Raise(new UIRequestEvent { Request = UIRequest.Play });
                });
            EventBus<GameStateChangedEvent>.Subscribe(OnStateChanged);
        }

        private void OnDestroy() => EventBus<GameStateChangedEvent>.Unsubscribe(OnStateChanged);

        private void OnStateChanged(GameStateChangedEvent e)
        {
            if (e.Current != GameState.Menu) return;
            // Back on the menu: Play continues from the next unfinished level.
            var progress = GameProgress.Get(_catalog);
            progress.Select(progress.NextUnfinished);
            if (_levelLabel != null) _levelLabel.SetText("LEVEL {0}", progress.Selected + 1);
        }
    }
}
