using System;
using System.Collections;
using System.Collections.Generic;
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

        /// <summary>Thread-safe: queues an action for the next Update on the main thread (SDK callbacks).</summary>
        void RunOnMainThread(Action action);
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

        private readonly object _queueLock = new object();
        private readonly List<Action> _queue = new List<Action>();
        private readonly List<Action> _running = new List<Action>();

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

        public void RunOnMainThread(Action action)
        {
            if (action == null) return;
            lock (_queueLock) _queue.Add(action);
        }

        private void Update()
        {
            lock (_queueLock)
            {
                if (_queue.Count > 0)
                {
                    _running.AddRange(_queue);
                    _queue.Clear();
                }
            }
            for (var i = 0; i < _running.Count; i++)
            {
                try { _running[i](); }
                catch (Exception e) { Debug.LogException(e); }
            }
            _running.Clear();

            Updated?.Invoke(Time.unscaledDeltaTime);
        }
        private void OnApplicationPause(bool paused) => PauseChanged?.Invoke(paused);
        private void OnApplicationQuit() => Quitting?.Invoke();
    }
}
