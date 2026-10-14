using System;
using System.Collections.Generic;
using UnityEngine;

namespace Template.Core
{
    /// <summary>Marker for event payloads. Events must be structs (no GC on raise).</summary>
    public interface IEvent { }

    /// <summary>
    /// Typed, allocation-free event bus. One static channel per event type.
    /// <code>
    /// EventBus&lt;GameOverEvent&gt;.Subscribe(OnGameOver);
    /// EventBus&lt;GameOverEvent&gt;.Raise(new GameOverEvent { Score = 10 });
    /// </code>
    /// Subscribing/unsubscribing during Raise is safe: removals are deferred,
    /// handlers added during Raise run from the next Raise on.
    /// </summary>
    public static class EventBus<T> where T : struct, IEvent
    {
        private static readonly List<Action<T>> Handlers = new List<Action<T>>(8);
        private static int _raiseDepth;
        private static bool _hasPendingRemovals;

        static EventBus() => EventBus.RegisterChannel(Clear);

        public static int HandlerCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Handlers.Count; i++)
                    if (Handlers[i] != null) count++;
                return count;
            }
        }

        public static void Subscribe(Action<T> handler)
        {
            if (handler == null) return;
            if (Handlers.Contains(handler)) return;
            Handlers.Add(handler);
        }

        public static void Unsubscribe(Action<T> handler)
        {
            var index = Handlers.IndexOf(handler);
            if (index < 0) return;
            if (_raiseDepth > 0)
            {
                Handlers[index] = null;
                _hasPendingRemovals = true;
            }
            else
            {
                Handlers.RemoveAt(index);
            }
        }

        public static void Raise(T evt)
        {
            _raiseDepth++;
            var count = Handlers.Count; // handlers added during raise are skipped this time
            for (var i = 0; i < count; i++)
            {
                var handler = Handlers[i];
                if (handler == null) continue;
                try
                {
                    handler(evt);
                }
                catch (Exception e)
                {
                    // One faulty listener must not break the others.
                    Debug.LogException(e);
                }
            }
            _raiseDepth--;

            if (_raiseDepth == 0 && _hasPendingRemovals)
            {
                Handlers.RemoveAll(h => h == null);
                _hasPendingRemovals = false;
            }
        }

        public static void Clear()
        {
            if (_raiseDepth > 0)
            {
                for (var i = 0; i < Handlers.Count; i++) Handlers[i] = null;
                _hasPendingRemovals = true;
                return;
            }
            Handlers.Clear();
        }
    }

    /// <summary>Non-generic helpers for all channels.</summary>
    public static class EventBus
    {
        private static readonly List<Action> ClearActions = new List<Action>();

        internal static void RegisterChannel(Action clear) => ClearActions.Add(clear);

        /// <summary>Removes every handler of every event type (tests, full restarts).</summary>
        public static void ClearAll()
        {
            for (var i = 0; i < ClearActions.Count; i++) ClearActions[i]();
        }
    }
}
