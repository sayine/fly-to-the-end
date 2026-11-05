using Game.Levels;
using Template.Core;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Level-aware Game Over: a failed level shows "LEVEL n" and its objective instead of the Free Flight best
    /// (which only Free Flight sets). Added to GameOverScreen.prefab by the game's UI builder.
    /// </summary>
    public sealed class GameOverExtras : MonoBehaviour
    {
        [SerializeField] private LevelCatalog _catalog;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _objective;
        [SerializeField] private GameObject _best;

        private void Awake() => EventBus<GameOverEvent>.Subscribe(OnGameOver);
        private void OnDestroy() => EventBus<GameOverEvent>.Unsubscribe(OnGameOver);

        private void OnGameOver(GameOverEvent e)
        {
            var progress = GameProgress.Get(_catalog);
            var level = progress.Selected;
            var levelRun = _catalog != null && level >= 0 && level < _catalog.Count;
            var config = levelRun ? _catalog.Get(level) : null;
            if (_best != null) _best.SetActive(!levelRun);
            if (_label != null)
            {
                if (levelRun) _label.SetText("LEVEL {0}", level + 1);
                else _label.text = "AIRTIME";
            }
            if (_objective != null)
            {
                _objective.gameObject.SetActive(levelRun);
                if (levelRun) _objective.text = config.Objective;
            }
        }
    }
}
