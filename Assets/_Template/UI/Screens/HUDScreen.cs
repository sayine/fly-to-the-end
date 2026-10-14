using Template.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Template.UI.Screens
{
    public sealed class HUDScreen : UIScreen
    {
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private Button _pauseButton;

        protected override void OnInit()
        {
            Bind(_pauseButton, () => Request(UIRequest.Pause));
            EventBus<ScoreChangedEvent>.Subscribe(OnScore);
            EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
        }

        private void OnDestroy()
        {
            EventBus<ScoreChangedEvent>.Unsubscribe(OnScore);
            EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
        }

        private void OnRunStarted(RunStartedEvent e) => SetScore(0);
        private void OnScore(ScoreChangedEvent e) => SetScore(e.Score);

        private void SetScore(int score)
        {
            if (_scoreText != null) _scoreText.SetText("{0}", score); // SetText with args doesn't allocate
        }
    }
}
