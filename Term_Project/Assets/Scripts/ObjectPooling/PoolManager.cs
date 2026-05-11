using System;
using System.Collections.Generic;

namespace ModularExperiment.ObjectPooling
{
    /// <summary>
    /// Central static registry for multiple named pools.
    /// Useful when different systems need to access shared pools by key.
    /// </summary>
    public static class PoolManager
    {
        public readonly struct PoolStats
        {
            public PoolStats(int totalAllocations, int totalReuses, int inactiveCount, int poolCount)
            {
                TotalAllocations = totalAllocations;
                TotalReuses = totalReuses;
                InactiveCount = inactiveCount;
                PoolCount = poolCount;
            }

            public int TotalAllocations { get; }
            public int TotalReuses { get; }
            public int InactiveCount { get; }
            public int PoolCount { get; }
        }

        private static readonly Dictionary<string, IObjectPool> Pools = new Dictionary<string, IObjectPool>();

        /// <summary>
        /// Registers an existing pool instance with a unique key.
        /// </summary>
        public static void RegisterPool<T>(string key, ObjectPool<T> pool) where T : class
        {
            ValidateKey(key);
            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            if (Pools.ContainsKey(key))
            {
                throw new InvalidOperationException($"A pool with key '{key}' is already registered.");
            }

            Pools.Add(key, pool);
        }

        /// <summary>
        /// Creates and registers a new pool.
        /// </summary>
        public static ObjectPool<T> CreatePool<T>(
            string key,
            Func<T> factory,
            int preWarmCount = 0,
            int initialCapacity = 0,
            int maxSize = int.MaxValue,
            bool allowGrowth = false,
            bool collectionCheck = true,
            Action<T> onGet = null,
            Action<T> onReturn = null)
            where T : class
        {
            ValidateKey(key);

            if (Pools.ContainsKey(key))
            {
                throw new InvalidOperationException($"A pool with key '{key}' is already registered.");
            }

            var pool = new ObjectPool<T>(
                factory: factory,
                initialCapacity: initialCapacity,
                maxSize: maxSize,
                allowGrowth: allowGrowth,
                collectionCheck: collectionCheck,
                onGet: onGet,
                onReturn: onReturn);

            if (preWarmCount > 0)
            {
                pool.PreWarm(preWarmCount);
            }

            Pools.Add(key, pool);
            return pool;
        }

        /// <summary>
        /// Retrieves an item from the pool mapped to the given key.
        /// </summary>
        public static T Get<T>(string key) where T : class
        {
            return GetPool<T>(key).Get();
        }

        /// <summary>
        /// Returns an item to the pool mapped to the given key.
        /// </summary>
        public static void Return<T>(string key, T item) where T : class
        {
            GetPool<T>(key).Return(item);
        }

        /// <summary>
        /// Pre-warms the pool mapped to the given key.
        /// </summary>
        public static void PreWarm(string key, int count)
        {
            GetPoolUntyped(key).PreWarm(count);
        }

        /// <summary>
        /// Removes and clears inactive items from the pool at this key.
        /// </summary>
        public static bool RemovePool(string key)
        {
            ValidateKey(key);

            if (!Pools.TryGetValue(key, out var pool))
            {
                return false;
            }

            pool.ClearInactive();
            return Pools.Remove(key);
        }

        /// <summary>
        /// Clears and unregisters all pools.
        /// </summary>
        public static void ClearAllPools()
        {
            foreach (var pair in Pools)
            {
                pair.Value.ClearInactive();
            }

            Pools.Clear();
        }

        /// <summary>
        /// Returns true if a pool is registered for the given key.
        /// </summary>
        public static bool ContainsPool(string key)
        {
            ValidateKey(key);
            return Pools.ContainsKey(key);
        }

        /// <summary>
        /// Tries to get a strongly typed pool by key.
        /// </summary>
        public static bool TryGetPool<T>(string key, out ObjectPool<T> pool) where T : class
        {
            ValidateKey(key);
            pool = null;

            if (!Pools.TryGetValue(key, out var untypedPool))
            {
                return false;
            }

            pool = untypedPool as ObjectPool<T>;
            return pool != null;
        }

        /// <summary>
        /// Returns combined stats across all registered pools.
        /// </summary>
        public static PoolStats GetAggregateStats()
        {
            var allocations = 0;
            var reuses = 0;
            var inactive = 0;

            foreach (var pair in Pools)
            {
                allocations += pair.Value.TotalAllocations;
                reuses += pair.Value.TotalReuses;
                inactive += pair.Value.InactiveCount;
            }

            return new PoolStats(allocations, reuses, inactive, Pools.Count);
        }

        /// <summary>
        /// Returns stats for one pool key.
        /// </summary>
        public static bool TryGetStats(string key, out PoolStats stats)
        {
            ValidateKey(key);
            if (!Pools.TryGetValue(key, out var pool))
            {
                stats = default;
                return false;
            }

            stats = new PoolStats(
                totalAllocations: pool.TotalAllocations,
                totalReuses: pool.TotalReuses,
                inactiveCount: pool.InactiveCount,
                poolCount: 1);
            return true;
        }

        private static ObjectPool<T> GetPool<T>(string key) where T : class
        {
            var untypedPool = GetPoolUntyped(key);
            if (untypedPool is ObjectPool<T> typedPool)
            {
                return typedPool;
            }

            throw new InvalidOperationException(
                $"Pool '{key}' exists but does not match requested type '{typeof(T).Name}'.");
        }

        private static IObjectPool GetPoolUntyped(string key)
        {
            ValidateKey(key);
            if (!Pools.TryGetValue(key, out var pool))
            {
                throw new KeyNotFoundException($"No pool registered with key '{key}'.");
            }

            return pool;
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Pool key cannot be null, empty, or whitespace.", nameof(key));
            }
        }
    }
}
