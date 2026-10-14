using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Template.Services.SceneLoading
{
    /// <summary>Real scene loader: async load behind a full-screen fade built in code (no prefab needed).</summary>
    public sealed class SceneLoader : MonoBehaviour, ISceneLoader
    {
        private const float FadeDuration = 0.25f;
        private CanvasGroup _fade;

        public bool IsLoading { get; private set; }

        public static SceneLoader Create(Color fadeColor)
        {
            var go = new GameObject("[SceneLoader]");
            DontDestroyOnLoad(go);
            var loader = go.AddComponent<SceneLoader>();
            loader.BuildFade(fadeColor);
            return loader;
        }

        public void Load(string sceneName, Action onLoaded = null)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] Already loading, ignored request for '{sceneName}'.");
                return;
            }
            StartCoroutine(LoadRoutine(sceneName, onLoaded));
        }

        private IEnumerator LoadRoutine(string sceneName, Action onLoaded)
        {
            IsLoading = true;
            _fade.blocksRaycasts = true;
            yield return Fade(1f);

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' not in Build Settings. Run Tools/Template/Generate Base Scenes.");
            }
            else
            {
                while (!op.isDone) yield return null;
                try { onLoaded?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }

            yield return Fade(0f);
            _fade.blocksRaycasts = false;
            IsLoading = false;
        }

        private IEnumerator Fade(float target)
        {
            var start = _fade.alpha;
            for (var t = 0f; t < FadeDuration; t += Time.unscaledDeltaTime)
            {
                _fade.alpha = Mathf.Lerp(start, target, t / FadeDuration);
                yield return null;
            }
            _fade.alpha = target;
        }

        private void BuildFade(Color color)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            _fade = gameObject.AddComponent<CanvasGroup>();
            _fade.alpha = 0f;
            _fade.blocksRaycasts = false;
            _fade.interactable = false;
            gameObject.AddComponent<GraphicRaycaster>();

            var image = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(transform, false);
            var rt = (RectTransform)image.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            image.GetComponent<Image>().color = color;
        }
    }

    /// <summary>Mock loader: synchronous load, no fade. Useful in tests.</summary>
    public sealed class InstantSceneLoader : ISceneLoader
    {
        public bool IsLoading => false;

        public void Load(string sceneName, Action onLoaded = null)
        {
            Debug.Log($"[SceneLoader:Mock] Loading {sceneName}");
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            onLoaded?.Invoke(); // note: scene objects are available from the next frame
        }
    }
}
