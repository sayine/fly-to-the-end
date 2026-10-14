using System;
using System.Collections.Generic;

namespace Template.Core
{
    /// <summary>Generic object pool for plain C# objects.</summary>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _inactive;
        private readonly HashSet<T> _inactiveSet;
        private readonly Func<T> _create;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Action<T> _onDestroy;
        private readonly int _maxSize;

        public int CountAll { get; private set; }
        public int CountInactive => _inactive.Count;
        public int CountActive => CountAll - _inactive.Count;

        public ObjectPool(Func<T> create, Action<T> onGet = null, Action<T> onRelease = null,
            Action<T> onDestroy = null, int prewarm = 0, int maxSize = 1000)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _onGet = onGet;
            _onRelease = onRelease;
            _onDestroy = onDestroy;
            _maxSize = Math.Max(1, maxSize);
            _inactive = new Stack<T>(Math.Max(prewarm, 4));
            _inactiveSet = new HashSet<T>();
            Prewarm(prewarm);
        }

        public void Prewarm(int count)
        {
            for (var i = 0; i < count && _inactive.Count < _maxSize; i++)
            {
                var item = _create();
                CountAll++;
                _onRelease?.Invoke(item);
                _inactive.Push(item);
                _inactiveSet.Add(item);
            }
        }

        public T Get()
        {
            T item;
            if (_inactive.Count > 0)
            {
                item = _inactive.Pop();
                _inactiveSet.Remove(item);
            }
            else
            {
                item = _create();
                CountAll++;
            }
            _onGet?.Invoke(item);
            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;
            if (_inactiveSet.Contains(item))
                throw new InvalidOperationException("Object released to pool twice.");

            _onRelease?.Invoke(item);
            if (_inactive.Count >= _maxSize)
            {
                CountAll--;
                _onDestroy?.Invoke(item);
                return;
            }
            _inactive.Push(item);
            _inactiveSet.Add(item);
        }

        /// <summary>Destroys inactive items. Active items are no longer tracked.</summary>
        public void Clear()
        {
            while (_inactive.Count > 0) _onDestroy?.Invoke(_inactive.Pop());
            _inactiveSet.Clear();
            CountAll = 0;
        }
    }
}
