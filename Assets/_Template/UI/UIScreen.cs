using System;
using Template.Core;
using Template.Services.Haptics;
using UnityEngine;
using UnityEngine.UI;

namespace Template.UI
{
    /// <summary>
    /// Base screen. Each screen has its own nested Canvas so showing/hiding only toggles
    /// Canvas.enabled (no layout rebuild of the other screens, no SetActive churn).
    /// Screens never reference gameplay classes: they raise UIRequestEvent and listen to events.
    /// </summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster))]
    public abstract class UIScreen : MonoBehaviour
    {
        [SerializeField, HideInInspector] private Canvas _canvas;
        [SerializeField, HideInInspector] private CanvasGroup _group;

        private IHapticService _haptics;

        protected ScreenManager Screens { get; private set; }
        public bool IsVisible { get; private set; }

        /// <summary>Overlays (Pause, Settings) stack on top and are not closed by other overlays.</summary>
        public virtual bool IsOverlay => false;

        internal void Initialize(ScreenManager manager)
        {
            Screens = manager;
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            if (_group == null) _group = GetComponent<CanvasGroup>();
            ServiceLocator.TryGet(out _haptics);
            SetVisible(false);
            OnInit();
        }

        public void Show()
        {
            if (IsVisible) return;
            SetVisible(true);
            OnShow();
        }

        public void Hide()
        {
            if (!IsVisible) return;
            SetVisible(false);
            OnHide();
        }

        protected virtual void OnInit() { }
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }

        protected static void Request(UIRequest request) =>
            EventBus<UIRequestEvent>.Raise(new UIRequestEvent { Request = request });

        /// <summary>Wires a button in code (never in the Inspector) with click haptics.</summary>
        protected void Bind(Button button, Action onClick)
        {
            if (button == null)
            {
                Debug.LogError($"[UI] {GetType().Name}: button not assigned. Re-run Tools/Template/Generate Base Scenes.", this);
                return;
            }
            button.onClick.AddListener(() =>
            {
                _haptics?.Play(HapticType.Light);
                Screens.PlayClick();
                onClick();
            });
        }

        private void SetVisible(bool visible)
        {
            IsVisible = visible;
            _canvas.enabled = visible;
            _group.interactable = visible;
            _group.blocksRaycasts = visible;
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            if (_group == null) _group = GetComponent<CanvasGroup>();
        }
#endif
    }
}
