using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Template.UI.Widgets
{
    /// <summary>
    /// Chunky "3D" button feel: while pressed, the face sinks onto its darker edge and the button squashes a little,
    /// then springs back on release. No allocations; unscaled time (works while paused).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class PressableButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform _face;
        [Tooltip("How far the face sinks (canvas units) when pressed.")]
        [SerializeField] private float _depth = 10f;
        [SerializeField, Range(0.8f, 1f)] private float _pressedScale = 0.96f;

        private Button _button;
        private Vector2 _faceRest;
        private bool _pressed;
        private float _t; // 0 = rest, 1 = fully pressed

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_face != null) _faceRest = _face.anchoredPosition;
        }

        private void OnDisable()
        {
            _pressed = false;
            _t = 0f;
            Apply();
        }

        public void OnPointerDown(PointerEventData eventData) => _pressed = _button.IsInteractable();
        public void OnPointerUp(PointerEventData eventData) => _pressed = false;
        public void OnPointerExit(PointerEventData eventData) => _pressed = false;

        private void Update()
        {
            var target = _pressed ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;
            // Fast in, springy out.
            _t = Mathf.MoveTowards(_t, target, Time.unscaledDeltaTime * (_pressed ? 18f : 9f));
            Apply();
        }

        private void Apply()
        {
            if (_face != null) _face.anchoredPosition = _faceRest + Vector2.down * (_depth * _t);
            var s = Mathf.Lerp(1f, _pressedScale, _t);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
