using System;
using System.Collections;
using UnityEngine;

namespace Template.Core
{
    /// <summary>Update/pause/coroutine hooks for plain C# services.</summary>
    public interface IAppLifecycle
    {
        event Action<float> Updated;
        event Action<bool> PauseChanged;
        event Action Quitting;
        Coroutine Run(IEnumerator routine);
        void Stop(Coroutine routine);
    }

    /// <summary>
    /// DontDestroyOnLoad host created by the bootstrapper. Runs before all gameplay scripts so
    /// services ticked from Updated (e.g. input) are fresh when gameplay reads them.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class AppLifecycle : MonoBehaviour, IAppLifecycle
    {
        public event Action<float> Updated;
        public event Action<bool> PauseChanged;
        public event Action Quitting;

        public static AppLifecycle Create()
        {
            var go = new GameObject("[AppLifecycle]");
            DontDestroyOnLoad(go);
            return go.AddComponent<AppLifecycle>();
        }

        public Coroutine Run(IEnumerator routine) => StartCoroutine(routine);

        public void Stop(Coroutine routine)
        {
            if (routine != null) StopCoroutine(routine);
        }

        private void Update() => Updated?.Invoke(Time.unscaledDeltaTime);
        private void OnApplicationPause(bool paused) => PauseChanged?.Invoke(paused);
        private void OnApplicationQuit() => Quitting?.Invoke();
    }
}
