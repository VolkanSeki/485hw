using System;
using System.Collections.Generic;
using ModularExperiment.ObjectPooling;
using UnityEngine;

namespace ModularExperiment.Experiment
{
    /// <summary>
    /// Unity-facing bridge that creates and registers pools from inspector data.
    /// This keeps ObjectPool generic while isolating Unity Instantiate usage here.
    /// </summary>
    public class UnityPoolFactory : MonoBehaviour
    {
        public readonly struct PoolConfig
        {
            public PoolConfig(int preWarmCount, int maxSize, bool allowGrowth)
            {
                PreWarmCount = preWarmCount;
                MaxSize = maxSize;
                AllowGrowth = allowGrowth;
            }

            public int PreWarmCount { get; }
            public int MaxSize { get; }
            public bool AllowGrowth { get; }
        }

        [Serializable]
        private class PoolDefinition
        {
            [SerializeField]
            private string key;

            [SerializeField]
            private BasePoolable prefab;

            [SerializeField]
            private int preWarmCount = 8;

            [SerializeField]
            private int maxSize = 256;

            [SerializeField]
            private bool allowGrowth;

            [SerializeField]
            [Min(1)]
            private int growthChunkSize = 500;

            [SerializeField]
            [Range(0.05f, 1.0f)]
            private float growthFactor = 1.0f;

            [SerializeField]
            [Min(1)]
            private int maxGrowthChunkSize = 2000;

            [SerializeField]
            private Transform parentOverride;

            public string Key => key;
            public BasePoolable Prefab => prefab;
            public int PreWarmCount => Mathf.Max(0, preWarmCount);
            public int MaxSize => Mathf.Max(1, maxSize);
            public bool AllowGrowth => allowGrowth;
            public int GrowthChunkSize => Mathf.Max(1, growthChunkSize);
            public float GrowthFactor => Mathf.Clamp(growthFactor, 0.05f, 1.0f);
            public int MaxGrowthChunkSize => Mathf.Max(GrowthChunkSize, maxGrowthChunkSize);
            public Transform ParentOverride => parentOverride;
        }

        [Header("Pool Setup")]
        [SerializeField]
        private List<PoolDefinition> poolDefinitions = new List<PoolDefinition>();

        [SerializeField]
        private bool clearAllPoolsOnDestroy;

        private void Awake()
        {
            RegisterConfiguredPools();
        }

        private void OnDestroy()
        {
            if (clearAllPoolsOnDestroy)
            {
                PoolManager.ClearAllPools();
            }
        }

        /// <summary>
        /// Helper for one-off/manual pool creation from code.
        /// Uses Unity Instantiate and returns BasePoolable instances.
        /// </summary>
        public static ObjectPool<BasePoolable> CreatePoolFromPrefab(
            string key,
            BasePoolable prefab,
            Transform parent = null,
            int preWarmCount = 0,
            int maxSize = int.MaxValue,
            bool allowGrowth = false,
            int growthChunkSize = 500,
            float growthFactor = 1.0f,
            int maxGrowthChunkSize = 2000)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            BasePoolable Factory()
            {
                var instance = Instantiate(prefab, parent);
                instance.AssignPoolKey(key);
                instance.gameObject.SetActive(false);
                return instance;
            }

            return PoolManager.CreatePool(
                key: key,
                factory: Factory,
                preWarmCount: Mathf.Max(0, preWarmCount),
                maxSize: Mathf.Max(1, maxSize),
                allowGrowth: allowGrowth,
                growthChunkSize: Mathf.Max(1, growthChunkSize),
                growthFactor: Mathf.Clamp(growthFactor, 0.05f, 1.0f),
                maxGrowthChunkSize: Mathf.Max(Mathf.Max(1, growthChunkSize), maxGrowthChunkSize));
        }

        private void RegisterConfiguredPools()
        {
            for (var i = 0; i < poolDefinitions.Count; i++)
            {
                var definition = poolDefinitions[i];
                if (definition == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.Key))
                {
                    Debug.LogWarning($"[{nameof(UnityPoolFactory)}] Pool entry {i} has an empty key.", this);
                    continue;
                }

                if (definition.Prefab == null)
                {
                    Debug.LogWarning($"[{nameof(UnityPoolFactory)}] Pool '{definition.Key}' has no prefab assigned.", this);
                    continue;
                }

                if (PoolManager.ContainsPool(definition.Key))
                {
                    Debug.LogWarning($"[{nameof(UnityPoolFactory)}] Pool key '{definition.Key}' is already registered.", this);
                    continue;
                }

                var parent = definition.ParentOverride != null ? definition.ParentOverride : transform;

                CreatePoolFromPrefab(
                    key: definition.Key,
                    prefab: definition.Prefab,
                    parent: parent,
                    preWarmCount: definition.PreWarmCount,
                    maxSize: definition.MaxSize,
                    allowGrowth: definition.AllowGrowth,
                    growthChunkSize: definition.GrowthChunkSize,
                    growthFactor: definition.GrowthFactor,
                    maxGrowthChunkSize: definition.MaxGrowthChunkSize);
            }
        }

        public bool TryGetPoolConfig(string key, out PoolConfig config)
        {
            config = default;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            for (var i = 0; i < poolDefinitions.Count; i++)
            {
                var definition = poolDefinitions[i];
                if (definition == null || !string.Equals(definition.Key, key, StringComparison.Ordinal))
                {
                    continue;
                }

                config = new PoolConfig(definition.PreWarmCount, definition.MaxSize, definition.AllowGrowth);
                return true;
            }

            return false;
        }
    }
}
