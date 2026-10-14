using UnityEngine;

namespace Template.Core
{
    /// <summary>Optional callbacks for pooled components.</summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    /// <summary>
    /// GameObject pool keyed by a prefab component. Use for anything spawned repeatedly
    /// (obstacles, coins, particles). Instances are parented under a hidden root.
    /// </summary>
    public sealed class ComponentPool<T> where T : Component
    {
        private readonly ObjectPool<T> _pool;
        private readonly T _prefab;
        private readonly Transform _root;

        public int CountActive => _pool.CountActive;
        public int CountInactive => _pool.CountInactive;

        public ComponentPool(T prefab, Transform root = null, int prewarm = 0, int maxSize = 256)
        {
            _prefab = prefab;
            _root = root != null ? root : new GameObject($"[Pool] {prefab.name}").transform;
            _pool = new ObjectPool<T>(Create, OnGet, OnRelease, OnDestroy, prewarm, maxSize);
        }

        public T Spawn(Vector3 position, Quaternion rotation)
        {
            var item = _pool.Get();
            item.transform.SetPositionAndRotation(position, rotation);
            return item;
        }

        public T Spawn() => Spawn(Vector3.zero, Quaternion.identity);

        public void Despawn(T item) => _pool.Release(item);

        public void Clear() => _pool.Clear();

        private T Create()
        {
            var item = Object.Instantiate(_prefab, _root);
            item.gameObject.SetActive(false);
            return item;
        }

        private static void OnGet(T item)
        {
            item.gameObject.SetActive(true);
            if (item is IPoolable poolable) poolable.OnSpawned();
        }

        private static void OnRelease(T item)
        {
            if (item is IPoolable poolable && item.gameObject.activeSelf) poolable.OnDespawned();
            item.gameObject.SetActive(false);
        }

        private static void OnDestroy(T item)
        {
            if (item != null) Object.Destroy(item.gameObject);
        }
    }
}
