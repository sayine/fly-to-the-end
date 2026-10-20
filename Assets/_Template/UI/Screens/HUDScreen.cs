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
        [Tooltip("TMP SetText format. {0:0.0} = one decimal place (e.g. a time score stored in tenths with scale 0.1).")]
        [SerializeField] private string _scoreFormat = "{0}";
        [Tooltip("Displayed value = Score * scale (Score is an int, so time scores are stored in tenths/hundredths).")]
        [SerializeField] private float _scoreScale = 1f;

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
            if (_scoreText != null) _scoreText.SetText(_scoreFormat, score * _scoreScale); // SetText with args doesn't allocate
        }
    }
}
