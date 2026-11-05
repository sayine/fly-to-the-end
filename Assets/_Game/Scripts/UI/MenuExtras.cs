using Game.Levels;
using Game.Planes;
using Template.Core;
using Template.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Paper Flight additions to the template Menu screen: "LEVEL n" under Play (Play continues from the next
    /// unfinished level), Levels (level map), Hangar (with a NEW badge while unseen planes wait) and Free Flight.
    /// On the first menu of a new day it checks in and shows the check-in popup (from day 2; day 1 is the
    /// first launch, no popup). Added to MenuScreen.prefab by the game's scene generator.
    /// </summary>
    public sealed class MenuExtras : MonoBehaviour
    {
        [SerializeField] private LevelCatalog _catalog;
        [SerializeField] private PlaneCatalog _planes;
        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private Button _levelsButton;
        [SerializeField] private Button _hangarButton;
        [SerializeField] private GameObject _hangarBadge;
        [SerializeField] private Button _freeFlightButton;

        private void Awake()
        {
            if (_levelsButton != null) _levelsButton.onClick.AddListener(() => ShowOverlay<LevelMapScreen>());
            if (_hangarButton != null) _hangarButton.onClick.AddListener(() => ShowOverlay<HangarScreen>());
            if (_freeFlightButton != null)
                _freeFlightButton.onClick.AddListener(() =>
                {
                    GameProgress.Get(_catalog).Select(GameProgress.FreeFlight);
                    EventBus<UIRequestEvent>.Raise(new UIRequestEvent { Request = UIRequest.Play });
                });
            EventBus<GameStateChangedEvent>.Subscribe(OnStateChanged);
        }

        private void OnDestroy() => EventBus<GameStateChangedEvent>.Unsubscribe(OnStateChanged);

        private static T ShowOverlay<T>() where T : UIScreen =>
            ServiceLocator.TryGet(out ScreenManager screens) ? screens.ShowOverlay<T>() : null;

        private void Update()
        {
            // Cheap bool; the hangar clears it when it closes.
            if (_hangarBadge != null && _planes != null)
            {
                var unseen = PlaneCollection.Get(_planes).HasUnseen;
                if (_hangarBadge.activeSelf != unseen) _hangarBadge.SetActive(unseen);
            }
        }

        private void OnStateChanged(GameStateChangedEvent e)
        {
            if (e.Current != GameState.Menu) return;
            // Back on the menu: Play continues from the next unfinished level.
            var progress = GameProgress.Get(_catalog);
            progress.Select(progress.NextUnfinished);
            if (_levelLabel != null) _levelLabel.SetText("LEVEL {0}", progress.Selected + 1);
            if (_planes != null) CheckIn();
        }

        private void CheckIn()
        {
            var checkIn = DailyCheckIn.Get();
            var newDay = checkIn.CheckInToday();
            PlaneConfig unlockedToday = null;
            foreach (var index in PlaneUnlocks.Evaluate(_planes, _catalog))
                if (_planes.Get(index).Unlock.Kind == UnlockKind.CheckIn) unlockedToday = _planes.Get(index);
            if (!newDay || checkIn.DaysPlayed < 2) return;
            ShowOverlay<CheckInScreen>()?.Present(checkIn.DaysPlayed, checkIn.NextRewardDay, unlockedToday);
        }
    }
}
