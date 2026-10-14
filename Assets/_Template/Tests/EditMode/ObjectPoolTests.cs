using System;
using NUnit.Framework;
using Template.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Template.Tests
{
    public class ObjectPoolTests
    {
        private class Item { public bool Active; }

        [Test]
        public void Get_OnEmptyPool_Creates()
        {
            var created = 0;
            var pool = new ObjectPool<Item>(() => { created++; return new Item(); });
            pool.Get();
            Assert.AreEqual(1, created);
            Assert.AreEqual(1, pool.CountActive);
        }

        [Test]
        public void Release_ThenGet_ReusesInstance()
        {
            var pool = new ObjectPool<Item>(() => new Item());
            var a = pool.Get();
            pool.Release(a);
            var b = pool.Get();
            Assert.AreSame(a, b);
            Assert.AreEqual(1, pool.CountAll);
        }

        [Test]
        public void Prewarm_CreatesInactiveItems()
        {
            var pool = new ObjectPool<Item>(() => new Item(), prewarm: 5);
            Assert.AreEqual(5, pool.CountInactive);
            Assert.AreEqual(0, pool.CountActive);
        }

        [Test]
        public void Callbacks_AreInvoked()
        {
            var pool = new ObjectPool<Item>(() => new Item(), i => i.Active = true, i => i.Active = false);
            var item = pool.Get();
            Assert.IsTrue(item.Active);
            pool.Release(item);
            Assert.IsFalse(item.Active);
        }

        [Test]
        public void DoubleRelease_Throws()
        {
            var pool = new ObjectPool<Item>(() => new Item());
            var item = pool.Get();
            pool.Release(item);
            Assert.Throws<InvalidOperationException>(() => pool.Release(item));
        }

        [Test]
        public void Release_BeyondMaxSize_DestroysItem()
        {
            var destroyed = 0;
            var pool = new ObjectPool<Item>(() => new Item(), onDestroy: _ => destroyed++, maxSize: 1);
            var a = pool.Get();
            var b = pool.Get();
            pool.Release(a);
            pool.Release(b);
            Assert.AreEqual(1, destroyed);
            Assert.AreEqual(1, pool.CountInactive);
        }

        [Test]
        public void ComponentPool_SpawnDespawn_TogglesActiveAndReuses()
        {
            var prefab = new GameObject("PoolPrefab").AddComponent<BoxCollider>();
            var root = new GameObject("PoolRoot").transform;
            try
            {
                var pool = new ComponentPool<BoxCollider>(prefab, root, prewarm: 2);
                Assert.AreEqual(2, pool.CountInactive);

                var spawned = pool.Spawn(Vector3.one, Quaternion.identity);
                Assert.IsTrue(spawned.gameObject.activeSelf);
                Assert.AreEqual(Vector3.one, spawned.transform.position);
                Assert.AreSame(root, spawned.transform.parent);

                pool.Despawn(spawned);
                Assert.IsFalse(spawned.gameObject.activeSelf);
                Assert.AreSame(spawned, pool.Spawn());
            }
            finally
            {
                Object.DestroyImmediate(prefab.gameObject);
                Object.DestroyImmediate(root.gameObject);
            }
        }
    }
}
