using System;
using System.Collections.Generic;
using Template.Core;
using Template.Services.Audio;
using UnityEngine;

namespace Template.UI
{
    /// <summary>
    /// Owns all screens under the persistent UI root (one prefab per screen, nested in UIRoot.prefab).
    /// Show&lt;T&gt;() is exclusive (hides everything else); ShowOverlay&lt;T&gt;() stacks on top.
    /// </summary>
    public sealed class ScreenManager : MonoBehaviour
    {
        [SerializeField] private AudioClip _clickSfx;

        private readonly Dictionary<Type, UIScreen> _screens = new Dictionary<Type, UIScreen>();
        private readonly List<UIScreen> _all = new List<UIScreen>();
        private IAudioService _audio;
        private bool _initialized;

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            ServiceLocator.TryGet(out _audio);
            GetComponentsInChildren(true, _all);
            foreach (var screen in _all)
            {
                _screens[screen.GetType()] = screen;
                screen.Initialize(this);
            }
        }

        public T Get<T>() where T : UIScreen
        {
            if (_screens.TryGetValue(typeof(T), out var screen)) return (T)screen;
            Debug.LogError($"[UI] Screen {typeof(T).Name} not found under {name}.");
            return null;
        }

        /// <summary>Shows T and hides every other screen (including overlays).</summary>
        public T Show<T>() where T : UIScreen
        {
            var target = Get<T>();
            for (var i = 0; i < _all.Count; i++)
                if (_all[i] != target) _all[i].Hide();
            if (target != null) target.Show();
            return target;
        }

        /// <summary>Shows T on top of the current screen.</summary>
        public T ShowOverlay<T>() where T : UIScreen
        {
            var target = Get<T>();
            if (target == null) return null;
            target.transform.SetAsLastSibling();
            target.Show();
            return target;
        }

        public void Hide<T>() where T : UIScreen => Get<T>()?.Hide();

        public bool IsVisible<T>() where T : UIScreen
        {
            var screen = Get<T>();
            return screen != null && screen.IsVisible;
        }

        public void HideAll()
        {
            for (var i = 0; i < _all.Count; i++) _all[i].Hide();
        }

        internal void PlayClick()
        {
            if (_clickSfx != null) _audio?.PlaySfx(_clickSfx, 0.7f);
        }
    }
}
