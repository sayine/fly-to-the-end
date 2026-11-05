using Game.Levels;
using TMPro;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Mission UI over the game (SPEC §8): the level intro while aiming (level number, room, objective) and a
    /// compact progress line while flying (gems, rings, airtime goal, time). Allocation-free per-frame updates.
    /// </summary>
    public sealed class MissionHudView : MonoBehaviour
    {
        [SerializeField] private GameObject _intro;
        [SerializeField] private TMP_Text _introTitle;
        [SerializeField] private TMP_Text _introObjective;
        [SerializeField] private GameObject _progressRoot;
        [SerializeField] private TMP_Text _progress;

        public void Hide()
        {
            if (_intro != null) _intro.SetActive(false);
            if (_progressRoot != null) _progressRoot.SetActive(false);
        }

        public void ShowIntro(int levelIndex, LevelConfig level, MissionTracker tracker)
        {
            Hide();
            if (tracker.IsFreeFlight || _intro == null) return;
            _intro.SetActive(true);
            // Once per run: string building here is fine.
            if (_introTitle != null) _introTitle.text = $"LEVEL {levelIndex + 1}  ·  {level.Room.ToUpperInvariant()}";
            if (_introObjective != null) _introObjective.text = level.Objective;
        }

        public void ShowProgress(MissionTracker tracker)
        {
            if (_intro != null) _intro.SetActive(false);
            if (_progressRoot != null) _progressRoot.SetActive(!tracker.IsFreeFlight);
            Refresh(tracker);
        }

        public void Refresh(MissionTracker tracker)
        {
            if (_progress == null || tracker.IsFreeFlight || _progressRoot == null || !_progressRoot.activeSelf) return;
            switch (tracker.Type)
            {
                case MissionType.Airtime:
                    if (tracker.GoalReached) _progress.SetText("GOAL! Keep flying for more stars");
                    else _progress.SetText("Stay up {0:0} s", tracker.Goal);
                    break;
                case MissionType.Gems:
                    _progress.SetText("Gems {0} / {1}", tracker.Gems, tracker.GemTotal);
                    break;
                case MissionType.Rings:
                    _progress.SetText("Rings {0} / {1}", tracker.NextRing, tracker.RingTotal);
                    break;
                case MissionType.Land:
                    _progress.SetText("Land on the target");
                    break;
                case MissionType.ReachRoom:
                    _progress.SetText("Find the room  {0:0.0} s", tracker.Time);
                    break;
            }
        }
    }
}
