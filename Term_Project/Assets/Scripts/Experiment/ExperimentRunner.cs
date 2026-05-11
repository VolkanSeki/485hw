using System;
using System.Collections;
using System.Collections.Generic;
using ModularExperiment.ObjectPooling;
using UnityEngine;

namespace ModularExperiment.Experiment
{
    /// <summary>
    /// Central benchmark orchestrator for pooling experiments.
    /// Supports:
    /// 1) Periodic single entity stream
    /// 2) Multiple concurrent streams
    /// 3) Burst/explosion spawning
    /// </summary>
    public class ExperimentRunner : MonoBehaviour
    {
        public event Action<string> ScenarioStarted;
        public event Action<string> ScenarioStopped;
        public event Action<int> BurstStarted;
        public event Action<int, double> BurstCompleted;

        [Serializable]
        public class SpawnerConfig
        {
            [SerializeField]
            private string poolKey;

            [SerializeField]
            private BasePoolable fallbackPrefab;

            [SerializeField]
            [Min(0.01f)]
            private float spawnFrequency = 0.1f;

            [SerializeField]
            [Min(0f)]
            private float objectLifetime = 1.0f;

            [SerializeField]
            [Min(1)]
            private int objectsPerTick = 1;

            [SerializeField]
            private Transform spawnOrigin;

            [SerializeField]
            [Min(0f)]
            private float randomSphereRadius;

            public string PoolKey => poolKey;
            public BasePoolable FallbackPrefab => fallbackPrefab;
            public float SpawnFrequency => Mathf.Max(0.01f, spawnFrequency);
            public float ObjectLifetime => Mathf.Max(0f, objectLifetime);
            public int ObjectsPerTick => Mathf.Max(1, objectsPerTick);
            public Transform SpawnOrigin => spawnOrigin;
            public float RandomSphereRadius => Mathf.Max(0f, randomSphereRadius);

            // Internal setters for automated batch configuration.
            public string PoolKeyInternal { set => poolKey = value; }
            public BasePoolable FallbackPrefabInternal { set => fallbackPrefab = value; }
            public float SpawnFrequencyInternal { set => spawnFrequency = Mathf.Max(0.01f, value); }
            public float ObjectLifetimeInternal { set => objectLifetime = Mathf.Max(0f, value); }
            public int ObjectsPerTickInternal { set => objectsPerTick = Mathf.Max(1, value); }
            public Transform SpawnOriginInternal { set => spawnOrigin = value; }
            public float RandomSphereRadiusInternal { set => randomSphereRadius = Mathf.Max(0f, value); }
        }

        [Header("Global Toggle")]
        [SerializeField]
        private bool usePooling = true;

        [Header("Core Keys")]
        [SerializeField]
        private string simplePoolKey = "Simple";

        [SerializeField]
        private string costlyPoolKey = "Costly";

        [Header("Runtime Object Type Selection")]
        [SerializeField]
        private int selectedObjectTypeIndex;

        [Header("Fallback Prefabs (used when pooling is OFF)")]
        [SerializeField]
        private BasePoolable simplePrefab;

        [SerializeField]
        private BasePoolable costlyPrefab;

        [Header("Default Scenario Settings")]
        [SerializeField]
        [Min(0.01f)]
        private float spawnFrequency = 0.5f;

        [SerializeField]
        [Min(0f)]
        private float objectLifetime = 1.5f;

        [SerializeField]
        [Min(1)]
        private int burstCount = 40;

        [Header("Scenario 3 Burst Settings")]
        [SerializeField]
        private string burstPoolKey = "Costly";

        [SerializeField]
        private Transform burstOrigin;

        [SerializeField]
        [Min(0f)]
        private float burstRandomRadius = 1.5f;

        [Header("Scenario 2 Multiple Streams")]
        [SerializeField]
        private List<SpawnerConfig> streamSpawners = new List<SpawnerConfig>();

        private Coroutine scenarioOneRoutine;
        private readonly List<Coroutine> scenarioTwoRoutines = new List<Coroutine>();
        private BasePoolable scenarioOneCurrent;

        /// <summary>
        /// Runtime toggle for pool usage.
        /// </summary>
        public bool UsePooling
        {
            get => usePooling;
            set => usePooling = value;
        }

        /// <summary>
        /// Scenario 1 periodic spawn interval in seconds.
        /// </summary>
        public float SpawnFrequency
        {
            get => spawnFrequency;
            set => spawnFrequency = Mathf.Max(0.01f, value);
        }

        /// <summary>
        /// Object lifetime used by scenario 1 and scenario 3.
        /// </summary>
        public float ObjectLifetime
        {
            get => objectLifetime;
            set => objectLifetime = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Number of objects spawned by scenario 3 burst.
        /// </summary>
        public int BurstCount
        {
            get => burstCount;
            set => burstCount = Mathf.Max(1, value);
        }

        /// <summary>
        /// 0 = SimpleCube, 1 = CostlySphere.
        /// Scenario 1 and Scenario 3 use this selection at runtime.
        /// </summary>
        public int SelectedObjectTypeIndex
        {
            get => selectedObjectTypeIndex;
            set => selectedObjectTypeIndex = value <= 0 ? 0 : 1;
        }

        public string CurrentObjectTypeLabel => selectedObjectTypeIndex == 0 ? "SimpleCube" : "CostlySphere";
        public string SimplePoolKey => simplePoolKey;
        public string CostlyPoolKey => costlyPoolKey;
        public BasePoolable SimplePrefab => simplePrefab;
        public BasePoolable CostlyPrefab => costlyPrefab;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                StartScenario1();
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                StartScenario2();
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                TriggerScenario3Burst();
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                TriggerScenario3Burst();
            }
        }

        /// <summary>
        /// Scenario 1:
        /// Spawns one object periodically and removes the previous one when a new one appears.
        /// </summary>
        public void StartScenario1()
        {
            StopScenario1();
            scenarioOneRoutine = StartCoroutine(ScenarioOnePeriodicRoutine());
            ScenarioStarted?.Invoke("Scenario1");
        }

        public void StopScenario1()
        {
            var wasRunning = scenarioOneRoutine != null;
            if (scenarioOneRoutine != null)
            {
                StopCoroutine(scenarioOneRoutine);
                scenarioOneRoutine = null;
            }

            DespawnNow(scenarioOneCurrent);
            scenarioOneCurrent = null;

            if (wasRunning)
            {
                ScenarioStopped?.Invoke("Scenario1");
            }
        }

        /// <summary>
        /// Scenario 2:
        /// Runs all configured stream spawners in parallel.
        /// </summary>
        public void StartScenario2()
        {
            StopScenario2();

            for (var i = 0; i < streamSpawners.Count; i++)
            {
                var config = streamSpawners[i];
                if (config == null)
                {
                    continue;
                }

                var routine = StartCoroutine(StreamSpawnerRoutine(config));
                scenarioTwoRoutines.Add(routine);
            }

            ScenarioStarted?.Invoke("Scenario2");
        }

        public void StopScenario2()
        {
            var hadRoutines = scenarioTwoRoutines.Count > 0;
            for (var i = 0; i < scenarioTwoRoutines.Count; i++)
            {
                if (scenarioTwoRoutines[i] != null)
                {
                    StopCoroutine(scenarioTwoRoutines[i]);
                }
            }

            scenarioTwoRoutines.Clear();

            if (hadRoutines)
            {
                ScenarioStopped?.Invoke("Scenario2");
            }
        }

        /// <summary>
        /// Scenario 3:
        /// Burst-spawns N objects in the same frame.
        /// </summary>
        public void TriggerScenario3Burst()
        {
            TriggerScenario3BurstInternal(burstCount);
        }

        /// <summary>
        /// Scenario 3 with a temporary override count.
        /// Useful for controlled analytics experiments.
        /// </summary>
        public void TriggerScenario3BurstWithCount(int overrideCount)
        {
            TriggerScenario3BurstInternal(Mathf.Max(1, overrideCount));
        }

        private void TriggerScenario3BurstInternal(int count)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var startTime = Time.realtimeSinceStartupAsDouble;
            ScenarioStarted?.Invoke("Scenario3-Burst");
            BurstStarted?.Invoke(count);
            Debug.Log($"[ExperimentRunner] Burst START @ {timestamp}, frame={Time.frameCount}, count={count}");

            var origin = burstOrigin != null ? burstOrigin.position : transform.position;
            var key = GetSelectedPoolKey();

            for (var i = 0; i < count; i++)
            {
                var position = origin + UnityEngine.Random.insideUnitSphere * burstRandomRadius;
                SpawnByKey(key, objectLifetime, position, Quaternion.identity, null);
            }

            var endTime = Time.realtimeSinceStartupAsDouble;
            var elapsedMs = (endTime - startTime) * 1000.0;
            Debug.Log(
                $"[ExperimentRunner] Burst END @ {DateTime.Now:HH:mm:ss.fff}, frame={Time.frameCount}, elapsed={elapsedMs:F2}ms");
            BurstCompleted?.Invoke(count, elapsedMs);
            ScenarioStopped?.Invoke("Scenario3-Burst");
        }

        /// <summary>
        /// Convenience method for UI buttons to run a simple stream.
        /// </summary>
        public void StartSimpleScenario1()
        {
            StartScenario1();
        }

        /// <summary>
        /// Convenience method for UI buttons to run configured streams.
        /// </summary>
        public void StartMultiStreamScenario2()
        {
            StartScenario2();
        }

        /// <summary>
        /// Convenience method for UI buttons to run a burst.
        /// </summary>
        public void StartBurstScenario3()
        {
            TriggerScenario3Burst();
        }

        public void StopAllScenarios()
        {
            StopScenario1();
            StopScenario2();
        }

        /// <summary>
        /// Rebuilds scenario 2 with two standard streams for automated benchmarking.
        /// </summary>
        public void ConfigureScenario2ForBatch(
            float fastFrequency,
            float slowFrequency,
            float fastLifetime = 0.8f,
            float slowLifetime = 1.6f)
        {
            streamSpawners.Clear();
            streamSpawners.Add(CreateBatchSpawner(GetSelectedPoolKey(), fastFrequency, fastLifetime));
            streamSpawners.Add(CreateBatchSpawner(GetSelectedPoolKey(), slowFrequency, slowLifetime));
        }

        /// <summary>
        /// Rebuilds scenario 2 using N streams with identical cadence.
        /// Useful for controlled chaos / stress demos.
        /// </summary>
        public void ConfigureScenario2Chaos(
            int streamCount,
            float frequency,
            float lifetime,
            int objectsPerTick = 1,
            float randomRadius = 2f)
        {
            streamSpawners.Clear();
            var count = Mathf.Max(1, streamCount);
            for (var i = 0; i < count; i++)
            {
                streamSpawners.Add(new SpawnerConfig
                {
                    PoolKeyInternal = GetSelectedPoolKey(),
                    FallbackPrefabInternal = GetSelectedFallbackPrefab(),
                    SpawnFrequencyInternal = Mathf.Max(0.01f, frequency),
                    ObjectLifetimeInternal = Mathf.Max(0f, lifetime),
                    ObjectsPerTickInternal = Mathf.Max(1, objectsPerTick),
                    SpawnOriginInternal = null,
                    RandomSphereRadiusInternal = Mathf.Max(0f, randomRadius)
                });
            }
        }

        private IEnumerator ScenarioOnePeriodicRoutine()
        {
            while (true)
            {
                if (scenarioOneCurrent != null)
                {
                    DespawnNow(scenarioOneCurrent);
                    scenarioOneCurrent = null;
                }

                scenarioOneCurrent = SpawnByKey(
                    GetSelectedPoolKey(),
                    objectLifetime,
                    transform.position,
                    transform.rotation,
                    GetSelectedFallbackPrefab());

                yield return new WaitForSeconds(spawnFrequency);
            }
        }

        private IEnumerator StreamSpawnerRoutine(SpawnerConfig config)
        {
            while (true)
            {
                var spawnOrigin = config.SpawnOrigin != null ? config.SpawnOrigin.position : transform.position;
                var spawnRotation = config.SpawnOrigin != null ? config.SpawnOrigin.rotation : Quaternion.identity;

                for (var i = 0; i < config.ObjectsPerTick; i++)
                {
                    var position = spawnOrigin + UnityEngine.Random.insideUnitSphere * config.RandomSphereRadius;
                    SpawnByKey(config.PoolKey, config.ObjectLifetime, position, spawnRotation, config.FallbackPrefab);
                }

                yield return new WaitForSeconds(config.SpawnFrequency);
            }
        }

        private BasePoolable SpawnByKey(
            string key,
            float lifetime,
            Vector3 position,
            Quaternion rotation,
            BasePoolable fallbackPrefab)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning("[ExperimentRunner] Spawn requested with empty pool key.", this);
                return null;
            }

            BasePoolable instance;

            if (usePooling)
            {
                instance = PoolManager.Get<BasePoolable>(key);
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.ReturnToPoolAfter(lifetime);
                return instance;
            }

            var prefab = ResolveFallbackPrefab(key, fallbackPrefab);
            if (prefab == null)
            {
                Debug.LogWarning($"[ExperimentRunner] No fallback prefab configured for key '{key}'.", this);
                return null;
            }

            instance = Instantiate(prefab, position, rotation);
            if (lifetime > 0f)
            {
                Destroy(instance.gameObject, lifetime);
            }

            return instance;
        }

        private void DespawnNow(BasePoolable instance)
        {
            if (instance == null)
            {
                return;
            }

            if (usePooling)
            {
                instance.ReturnToPool();
                return;
            }

            Destroy(instance.gameObject);
        }

        private BasePoolable ResolveFallbackPrefab(string key, BasePoolable explicitFallback)
        {
            if (explicitFallback != null)
            {
                return explicitFallback;
            }

            if (string.Equals(key, simplePoolKey, StringComparison.Ordinal))
            {
                return simplePrefab;
            }

            if (string.Equals(key, costlyPoolKey, StringComparison.Ordinal))
            {
                return costlyPrefab;
            }

            return null;
        }

        private SpawnerConfig CreateBatchSpawner(string key, float frequency, float lifetime)
        {
            return new SpawnerConfig
            {
                PoolKeyInternal = key,
                FallbackPrefabInternal = GetSelectedFallbackPrefab(),
                SpawnFrequencyInternal = Mathf.Max(0.01f, frequency),
                ObjectLifetimeInternal = Mathf.Max(0f, lifetime),
                ObjectsPerTickInternal = 1,
                SpawnOriginInternal = null,
                RandomSphereRadiusInternal = 1.5f
            };
        }

        public string GetSelectedPoolKey()
        {
            return selectedObjectTypeIndex == 0 ? simplePoolKey : costlyPoolKey;
        }

        public BasePoolable GetSelectedFallbackPrefab()
        {
            return selectedObjectTypeIndex == 0 ? simplePrefab : costlyPrefab;
        }

        public void AssignFallbackPrefabs(BasePoolable simple, BasePoolable costly)
        {
            if (simple != null)
            {
                simplePrefab = simple;
            }

            if (costly != null)
            {
                costlyPrefab = costly;
            }
        }

    }
}
