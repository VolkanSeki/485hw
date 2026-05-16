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
        private readonly HashSet<T> _activeItems;
        private readonly int _maxSize;
        private readonly bool _collectionCheck;
        private readonly bool _allowGrowth;
        private readonly int _growthChunkSize;
        private readonly float _growthFactor;
        private readonly int _maxGrowthChunkSize;
        private int _peakActiveCount;
        private int _peakInactiveCount;

        public ObjectPool(
            Func<T> factory,
            int initialCapacity = 0,
            int maxSize = int.MaxValue,
            bool allowGrowth = false,
            int growthChunkSize = 500,
            float growthFactor = 1.0f,
            int maxGrowthChunkSize = 2000,
            bool collectionCheck = true,
            Action<T> onGet = null,
            Action<T> onReturn = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _maxSize = maxSize;
            _allowGrowth = allowGrowth;
            _growthChunkSize = Math.Max(1, growthChunkSize);
            _growthFactor = Math.Max(0.01f, growthFactor);
            _maxGrowthChunkSize = Math.Max(_growthChunkSize, maxGrowthChunkSize);
            _collectionCheck = collectionCheck;
            _onGet = onGet;
            _onReturn = onReturn;
            _inactiveItems = new Stack<T>(Math.Max(0, initialCapacity));
            _inPool = collectionCheck ? new HashSet<T>() : null;
            _activeItems = new HashSet<T>();

            if (maxSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be greater than zero.");
            }
        }

        public int InactiveCount => _inactiveItems.Count;
        public int ActiveCount => _activeItems.Count;
        public int TotalAllocations { get; private set; }
        public int TotalReuses { get; private set; }
        public int TotalRejections { get; private set; }

        /// <summary>
        /// Returns an item from the pool or creates one if needed.
        /// </summary>
        public T Get()
        {
            T item = null;

            while (_inactiveItems.Count > 0)
            {
                var candidate = _inactiveItems.Pop();
                if (!IsAlive(candidate))
                {
                    if (_collectionCheck)
                    {
                        _inPool.Remove(candidate);
                    }
                    continue;
                }

                item = candidate;
                TotalReuses++;

                if (_collectionCheck)
                {
                    _inPool.Remove(item);
                }
                break;
            }

            if (item == null)
            {
                if (!_allowGrowth && (_inactiveItems.Count + _activeItems.Count) >= _maxSize)
                {
                    TotalRejections++;
                    return null;
                }

                if (_allowGrowth)
                {
                    ExpandByChunk();
                    if (_inactiveItems.Count > 0)
                    {
                        item = _inactiveItems.Pop();
                        TotalReuses++;
                        if (_collectionCheck)
                        {
                            _inPool.Remove(item);
                        }
                    }
                }

                if (item == null)
                {
                    item = CreateNew();
                }
            }

            _activeItems.Add(item);
            if (_activeItems.Count > _peakActiveCount)
            {
                _peakActiveCount = _activeItems.Count;
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

            if (!_activeItems.Remove(item))
            {
                // Unknown object for this pool; ignore to avoid cross-pool corruption.
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

            if (_inactiveItems.Count > _peakInactiveCount)
            {
                _peakInactiveCount = _inactiveItems.Count;
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
                if (!_allowGrowth && (_inactiveItems.Count + _activeItems.Count) >= _maxSize)
                {
                    break;
                }

                CreateAndStoreInactive(CreateNew());
            }

            if (_inactiveItems.Count > _peakInactiveCount)
            {
                _peakInactiveCount = _inactiveItems.Count;
            }
        }

        /// <summary>
        /// Clears only currently inactive items.
        /// Active references remain valid and must be returned by owner code.
        /// </summary>
        public void ClearInactive()
        {
            while (_inactiveItems.Count > 0)
            {
                var item = _inactiveItems.Pop();
                DestroyTrackedObject(item);
            }

            _inPool?.Clear();
            _peakActiveCount = 0;
            _peakInactiveCount = 0;
        }

        public void ClearAllObjects()
        {
            ClearInactive();

            if (_activeItems.Count > 0)
            {
                var snapshot = new List<T>(_activeItems);
                for (var i = 0; i < snapshot.Count; i++)
                {
                    DestroyTrackedObject(snapshot[i]);
                }
                _activeItems.Clear();
            }

            _inactiveItems.Clear();
            _inPool?.Clear();

#if UNITY_5_3_OR_NEWER
            System.GC.Collect();
#endif
            _peakActiveCount = 0;
            _peakInactiveCount = 0;
        }

        private void ExpandByChunk()
        {
            var trackedCount = _inactiveItems.Count + _activeItems.Count;
            var dynamicChunk = (int)Math.Ceiling(Math.Max(1, trackedCount) * _growthFactor);
            var chunkSize = Math.Min(_maxGrowthChunkSize, Math.Max(_growthChunkSize, dynamicChunk));

            for (var i = 0; i < chunkSize; i++)
            {
                CreateAndStoreInactive(CreateNew());
            }

            if (_inactiveItems.Count > _peakInactiveCount)
            {
                _peakInactiveCount = _inactiveItems.Count;
            }
        }

        private void CreateAndStoreInactive(T item)
        {
            InvokeOnDespawn(item);
            _onReturn?.Invoke(item);
            _inactiveItems.Push(item);

            if (_collectionCheck)
            {
                _inPool.Add(item);
            }
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
            DestroyTrackedObject(item);
        }

        private static void DestroyTrackedObject(T item)
        {
#if UNITY_5_3_OR_NEWER
            if (item is UnityEngine.Component component)
            {
                if (component != null && component.gameObject != null)
                {
                    UnityEngine.Object.Destroy(component.gameObject);
                }
                return;
            }

            if (item is UnityEngine.GameObject gameObject)
            {
                if (gameObject != null)
                {
                    UnityEngine.Object.Destroy(gameObject);
                }
                return;
            }

            if (item is UnityEngine.Object unityObject)
            {
                if (unityObject != null)
                {
                    UnityEngine.Object.Destroy(unityObject);
                }
            }
#endif
        }

        private static bool IsAlive(T item)
        {
            if (item == null)
            {
                return false;
            }

#if UNITY_5_3_OR_NEWER
            if (item is UnityEngine.Component component)
            {
                return component != null && component.gameObject != null;
            }

            if (item is UnityEngine.GameObject gameObject)
            {
                return gameObject != null;
            }

            if (item is UnityEngine.Object unityObject)
            {
                return unityObject != null;
            }
#endif
            return true;
        }
    }
}
