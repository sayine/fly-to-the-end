using UnityEngine;

namespace Game
{
    /// <summary>
    /// Draws the steering pad (rounded strip at the bottom) plus, while a finger steers, a ring at the anchor and
    /// a knob at the clamped drag. Owned by PaperPlaneGame; raycasts off so it never blocks the HUD buttons.
    /// </summary>
    public sealed class ControlPadView : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _pad;
        [SerializeField] private RectTransform _ring;
        [SerializeField] private RectTransform _knob;
        [SerializeField] private CanvasGroup _group;

        private Rect _lastScreenRect;

        public void SetVisible(bool visible)
        {
            if (_canvas != null && _canvas.enabled != visible) _canvas.enabled = visible;
        }

        /// <summary>padRect in screen pixels; anchor/knob in screen pixels; travel = full-input drag in pixels.</summary>
        public void Refresh(Rect padRect, bool active, Vector2 anchor, Vector2 drag, float travelPixels)
        {
            if (_pad == null) return;
            if (padRect != _lastScreenRect)
            {
                _lastScreenRect = padRect;
                var w = Mathf.Max(1, Screen.width);
                var h = Mathf.Max(1, Screen.height);
                _pad.anchorMin = new Vector2(padRect.xMin / w, padRect.yMin / h);
                _pad.anchorMax = new Vector2(padRect.xMax / w, padRect.yMax / h);
                _pad.offsetMin = _pad.offsetMax = Vector2.zero;
            }
            if (_group != null) _group.alpha = active ? 1f : 0.6f;
            if (_ring.gameObject.activeSelf != active) _ring.gameObject.SetActive(active);
            if (_knob.gameObject.activeSelf != active) _knob.gameObject.SetActive(active);
            if (!active) return;
            SetScreenPosition(_ring, anchor);
            SetScreenPosition(_knob, anchor + drag * travelPixels);
        }

        private void SetScreenPosition(RectTransform target, Vector2 screen)
        {
            var parent = (RectTransform)target.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var local))
                target.localPosition = local;
        }
    }
}
