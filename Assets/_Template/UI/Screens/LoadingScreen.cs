using Template.Core;
using TMPro;
using UnityEngine;

namespace Template.UI.Screens
{
    public sealed class LoadingScreen : UIScreen
    {
        [SerializeField] private TMP_Text _status;
        [Tooltip("Child of the bar background; its anchorMax.x is driven by progress (no sprite needed).")]
        [SerializeField] private RectTransform _progressFill;

        protected override void OnInit() => EventBus<BootProgressEvent>.Subscribe(OnProgress);

        private void OnDestroy() => EventBus<BootProgressEvent>.Unsubscribe(OnProgress);

        private void OnProgress(BootProgressEvent e)
        {
            if (_status != null) _status.text = e.Status;
            if (_progressFill != null) _progressFill.anchorMax = new Vector2(Mathf.Clamp01(e.Progress), 1f);
        }
    }
}
