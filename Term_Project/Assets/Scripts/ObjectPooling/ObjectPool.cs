using System;
using System.Collections.Generic;

namespace ModularExperiment.ObjectPooling
{
    /// <summary>
    /// Engine-agnostic object pool for a specific type.
    ///
    /// Unity note:
    /// - Provide a factory like: () => Object.Instantiate(prefab)
    /// - Provide callbacks for activation/deactivation if needed.
    /// </summary>
    /// <typeparam name="T">Reference type to pool.</typeparam>
    public class ObjectPool<T> : IObjectPool where T : class
    {
        private readonly Stack<T> _inactiveItems;
        private readonly Func<T> _factory;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onReturn;
        private readonly HashSet<T> _inPool;
        private readonly int _maxSize;
        private readonly bool _collectionCheck;
        private readonly bool _allowGrowth;

        public ObjectPool(
            Func<T> factory,
            int initialCapacity = 0,
            int maxSize = int.MaxValue,
            bool allowGrowth = false,
            bool collectionCheck = true,
            Action<T> onGet = null,
            Action<T> onReturn = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _maxSize = maxSize;
            _allowGrowth = allowGrowth;
            _collectionCheck = collectionCheck;
            _onGet = onGet;
            _onReturn = onReturn;
            _inactiveItems = new Stack<T>(Math.Max(0, initialCapacity));
            _inPool = collectionCheck ? new HashSet<T>() : null;

            if (maxSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be greater than zero.");
            }
        }

        public int InactiveCount => _inactiveItems.Count;
        public int TotalAllocations { get; private set; }
        public int TotalReuses { get; private set; }

        /// <summary>
        /// Returns an item from the pool or creates one if needed.
        /// </summary>
        public T Get()
        {
            T item;

            if (_inactiveItems.Count > 0)
            {
                item = _inactiveItems.Pop();
                TotalReuses++;

                if (_collectionCheck)
                {
                    _inPool.Remove(item);
                }
            }
            else
            {
                item = CreateNew();
            }

            InvokeOnSpawn(item);
            _onGet?.Invoke(item);
            return item;
        }

        /// <summary>
        /// Returns an item back to the pool.
        /// </summary>
        public void Return(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (_collectionCheck && _inPool.Contains(item))
            {
                return;
            }

            if (!_collectionCheck && _inactiveItems.Contains(item))
            {
                return;
            }

            InvokeOnDespawn(item);
            _onReturn?.Invoke(item);

            if (!_allowGrowth && _inactiveItems.Count >= _maxSize)
            {
                DestroyOverflowItem(item);
                return;
            }

            _inactiveItems.Push(item);

            if (_collectionCheck)
            {
                _inPool.Add(item);
            }
        }

        /// <summary>
        /// Pre-allocates inactive items to reduce runtime spikes.
        /// </summary>
        public void PreWarm(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");
            }

            for (var i = 0; i < count; i++)
            {
                if (!_allowGrowth && _inactiveItems.Count >= _maxSize)
                {
                    break;
                }

                var item = CreateNew();
                InvokeOnDespawn(item);
                _onReturn?.Invoke(item);
                _inactiveItems.Push(item);

                if (_collectionCheck)
                {
                    _inPool.Add(item);
                }
            }
        }

        /// <summary>
        /// Clears only currently inactive items.
        /// Active references remain valid and must be returned by owner code.
        /// </summary>
        public void ClearInactive()
        {
            _inactiveItems.Clear();
            _inPool?.Clear();
        }

        private T CreateNew()
        {
            var item = _factory.Invoke();
            if (item == null)
            {
                throw new InvalidOperationException("Factory returned null. Pool cannot manage null items.");
            }

            TotalAllocations++;
            return item;
        }

        private static void InvokeOnSpawn(T item)
        {
            if (item is IPoolable poolable)
            {
                poolable.OnSpawn();
            }
        }

        private static void InvokeOnDespawn(T item)
        {
            if (item is IPoolable poolable)
            {
                poolable.OnDespawn();
            }
        }

        private static void DestroyOverflowItem(T item)
        {
#if UNITY_5_3_OR_NEWER
            if (item is UnityEngine.Component component)
            {
                UnityEngine.Object.Destroy(component.gameObject);
                return;
            }

            if (item is UnityEngine.GameObject gameObject)
            {
                UnityEngine.Object.Destroy(gameObject);
                return;
            }

            if (item is UnityEngine.Object unityObject)
            {
                UnityEngine.Object.Destroy(unityObject);
            }
#endif
        }
    }
}
