using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using ModularExperiment.ObjectPooling;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ModularExperiment.Experiment
{
    /// <summary>
    /// Runs the full CMPE 485 benchmark matrix and exports CSV results.
    /// </summary>
    public class BatchExperimentRunner : MonoBehaviour
    {
        [Serializable]
        public struct BatchResult
        {
            public string Scenario;
            public string ObjectType;
            public string Mode;
            public string GrowthMode;
            public string Parameter;
            public float AverageFps;
            public float MinFps;
            public float MaxFps;
            public float PeakFrameTimeMs;
            public float MemoryDeltaMb;
            public int GcCollections;
        }

        public event Action<string, float> ProgressUpdated;
        public event Action<string> BatchCompleted;

        [Header("References")]
        [SerializeField]
        private ExperimentRunner experimentRunner;

        [SerializeField]
        private UnityPoolFactory poolFactory;

        [SerializeField]
        private ExperimentAnalytics experimentAnalytics;

        [Header("Timing")]
        [SerializeField]
        [Min(0.1f)]
        private float warmUpSeconds = 2f;

        [SerializeField]
        [Min(0.1f)]
        private float recordingSeconds = 5f;

        [SerializeField]
        [Min(0f)]
        private float settleBetweenTestsSeconds = 1f;

        [Header("Trade-off Setup")]
        [SerializeField]
        [Min(1)]
        private int tradeoffBurstCount = 10000;

        [SerializeField]
        [Min(1)]
        private int tradeoffPreWarm = 100;

        private readonly List<BatchResult> _results = new List<BatchResult>();
        private bool _isRunning;
        private int _completedTests;
        private int _totalTests;

        public bool IsRunning => _isRunning;
        public IReadOnlyList<BatchResult> Results => _results;

        private void Reset()
        {
            AutoAssignLocalReferences();
        }

        private void OnValidate()
        {
            AutoAssignLocalReferences();
        }

        private void Start()
        {
            ResolveReferences();
            TryLoadPrefabsFromProjectIfMissing();
        }

        [ContextMenu("FORCE START BATCH")]
        public void StartFullBatchTest()
        {
            Debug.Log("[BatchRunner] Start button clicked!");

            if (_isRunning)
            {
                return;
            }

            if (!ValidateRequiredReferences())
            {
                return;
            }

            Debug.Log("[BatchRunner] Batch Test Started!");
            StartCoroutine(RunBatchRoutine());
        }

        private IEnumerator RunBatchRoutine()
        {
            ResolveReferences();
            if (!ValidateRequiredReferences())
            {
                yield break;
            }

            _isRunning = true;
            _results.Clear();
            _completedTests = 0;
            _totalTests = EstimateTotalTests();

            var originalPooling = experimentRunner.UsePooling;
            var originalObjectType = experimentRunner.SelectedObjectTypeIndex;

            try
            {
                yield return RunScenario1Matrix();
                yield return RunScenario2Matrix();
                yield return RunScenario3Matrix();
                yield return RunGrowthTradeoffMatrix();

                var csv = BuildCsv(_results);
                Debug.Log($"[BatchExperimentRunner] Batch completed.\n{csv}");
                BatchCompleted?.Invoke(csv);
            }
            finally
            {
                experimentRunner.StopAllScenarios();
                experimentRunner.UsePooling = originalPooling;
                experimentRunner.SelectedObjectTypeIndex = originalObjectType;
                _isRunning = false;
                ProgressUpdated?.Invoke("Batch complete", 1f);
            }
        }

        private IEnumerator RunScenario1Matrix()
        {
            experimentRunner.SelectedObjectTypeIndex = 0; // SimpleCube

            yield return RunSinglePeriodic(false);
            yield return RunSinglePeriodic(true);
        }

        private IEnumerator RunSinglePeriodic(bool pooling)
        {
            experimentRunner.StopAllScenarios();
            experimentRunner.UsePooling = pooling;
            experimentRunner.StartScenario1();

            yield return CaptureWindow(
                scenario: "Scenario1",
                objectType: experimentRunner.CurrentObjectTypeLabel,
                mode: pooling ? "PoolingON" : "PoolingOFF",
                growthMode: "N/A",
                parameter: $"SpawnFreq={experimentRunner.SpawnFrequency:0.00}");

            experimentRunner.StopScenario1();
            yield return WaitSettle();
        }

        private IEnumerator RunScenario2Matrix()
        {
            experimentRunner.SelectedObjectTypeIndex = 0; // SimpleCube
            var frequencies = new[] { 0.1f, 0.25f, 0.5f, 0.75f, 1.0f };

            for (var i = 0; i < frequencies.Length; i++)
            {
                var freq = frequencies[i];
                experimentRunner.ConfigureScenario2ForBatch(freq, Mathf.Min(1.0f, freq + 0.2f));

                yield return RunSingleMultiStream(false, freq);
                yield return RunSingleMultiStream(true, freq);
            }
        }

        private IEnumerator RunSingleMultiStream(bool pooling, float frequency)
        {
            experimentRunner.StopAllScenarios();
            experimentRunner.UsePooling = pooling;
            experimentRunner.StartScenario2();

            yield return CaptureWindow(
                scenario: "Scenario2",
                objectType: experimentRunner.CurrentObjectTypeLabel,
                mode: pooling ? "PoolingON" : "PoolingOFF",
                growthMode: "N/A",
                parameter: $"FastFreq={frequency:0.00}");

            experimentRunner.StopScenario2();
            yield return WaitSettle();
        }

        private IEnumerator RunScenario3Matrix()
        {
            var counts = new[] { 10, 100, 1000, 10000 };

            for (var objectType = 0; objectType <= 1; objectType++)
            {
                experimentRunner.SelectedObjectTypeIndex = objectType;

                for (var i = 0; i < counts.Length; i++)
                {
                    var count = counts[i];
                    yield return RunSingleBurst(false, count, "N/A");
                    yield return RunSingleBurst(true, count, "Cutoff");
                }
            }
        }

        private IEnumerator RunGrowthTradeoffMatrix()
        {
            experimentRunner.SelectedObjectTypeIndex = 1; // CostlySphere
            experimentRunner.UsePooling = true;

            yield return RebuildPoolsForGrowth(false);
            yield return RunSingleBurst(true, tradeoffBurstCount, "Cutoff");

            yield return RebuildPoolsForGrowth(true);
            yield return RunSingleBurst(true, tradeoffBurstCount, "Growing");

            // Return to default cutoff behavior after trade-off tests.
            yield return RebuildPoolsForGrowth(false);
        }

        private IEnumerator RunSingleBurst(bool pooling, int count, string growthMode)
        {
            experimentRunner.StopAllScenarios();
            experimentRunner.UsePooling = pooling;
            experimentRunner.BurstCount = count;

            yield return CaptureWindow(
                scenario: "Scenario3",
                objectType: experimentRunner.CurrentObjectTypeLabel,
                mode: pooling ? "PoolingON" : "PoolingOFF",
                growthMode: growthMode,
                parameter: $"Burst={count}",
                onRecordStart: () => experimentRunner.TriggerScenario3BurstWithCount(count));

            yield return WaitSettle();
        }

        private IEnumerator CaptureWindow(
            string scenario,
            string objectType,
            string mode,
            string growthMode,
            string parameter,
            Action onRecordStart = null)
        {
            UpdateProgress($"Warm-up: {scenario} ({mode})");
            yield return new WaitForSeconds(warmUpSeconds);

            onRecordStart?.Invoke();

            UpdateProgress($"Recording: {scenario} ({mode})");

            var startMemory = GC.GetTotalMemory(false);
            var startGc = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
            var elapsed = 0f;
            var sumFps = 0f;
            var samples = 0;
            var minFps = float.MaxValue;
            var maxFps = 0f;
            var peakFrameTimeMs = 0f;

            while (elapsed < recordingSeconds)
            {
                var dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
                var fps = 1f / dt;
                var frameMs = dt * 1000f;

                sumFps += fps;
                samples++;
                if (fps < minFps)
                {
                    minFps = fps;
                }

                if (fps > maxFps)
                {
                    maxFps = fps;
                }

                if (frameMs > peakFrameTimeMs)
                {
                    peakFrameTimeMs = frameMs;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            var endMemory = GC.GetTotalMemory(false);
            var endGc = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);

            _results.Add(new BatchResult
            {
                Scenario = scenario,
                ObjectType = objectType,
                Mode = mode,
                GrowthMode = growthMode,
                Parameter = parameter,
                AverageFps = samples > 0 ? sumFps / samples : 0f,
                MinFps = samples > 0 ? minFps : 0f,
                MaxFps = samples > 0 ? maxFps : 0f,
                PeakFrameTimeMs = peakFrameTimeMs,
                MemoryDeltaMb = (float)((endMemory - startMemory) / (1024.0 * 1024.0)),
                GcCollections = endGc - startGc
            });

            _completedTests++;
            UpdateProgress($"Finished {scenario} {parameter}");
        }

        private IEnumerator RebuildPoolsForGrowth(bool allowGrowth)
        {
            var simpleKey = experimentRunner.SimplePoolKey;
            var costlyKey = experimentRunner.CostlyPoolKey;

            PoolManager.RemovePool(simpleKey);
            PoolManager.RemovePool(costlyKey);

            var parent = poolFactory != null ? poolFactory.transform : transform;
            UnityPoolFactory.CreatePoolFromPrefab(
                simpleKey,
                experimentRunner.SimplePrefab,
                parent,
                preWarmCount: tradeoffPreWarm,
                maxSize: 1000,
                allowGrowth: allowGrowth);

            UnityPoolFactory.CreatePoolFromPrefab(
                costlyKey,
                experimentRunner.CostlyPrefab,
                parent,
                preWarmCount: tradeoffPreWarm,
                maxSize: 1000,
                allowGrowth: allowGrowth);

            yield return null;
        }

        private IEnumerator WaitSettle()
        {
            if (settleBetweenTestsSeconds > 0f)
            {
                yield return new WaitForSeconds(settleBetweenTestsSeconds);
            }
        }

        private void ResolveReferences()
        {
            if (experimentRunner == null)
            {
                experimentRunner = FindObjectOfType<ExperimentRunner>();
            }

            if (poolFactory == null)
            {
                poolFactory = FindObjectOfType<UnityPoolFactory>();
            }

            if (experimentAnalytics == null)
            {
                experimentAnalytics = FindObjectOfType<ExperimentAnalytics>();
            }
        }

        private void AutoAssignLocalReferences()
        {
            if (experimentRunner == null)
            {
                experimentRunner = GetComponent<ExperimentRunner>();
            }

            if (experimentAnalytics == null)
            {
                experimentAnalytics = GetComponent<ExperimentAnalytics>();
            }

            if (poolFactory == null)
            {
                poolFactory = GetComponent<UnityPoolFactory>();
            }
        }

        private void TryLoadPrefabsFromProjectIfMissing()
        {
            if (experimentRunner == null)
            {
                return;
            }

            if (experimentRunner.SimplePrefab != null && experimentRunner.CostlyPrefab != null)
            {
                return;
            }

#if UNITY_EDITOR
            var simple = AssetDatabase.LoadAssetAtPath<BasePoolable>("Assets/Prefabs/SimpleCube.prefab");
            var costly = AssetDatabase.LoadAssetAtPath<BasePoolable>("Assets/Prefabs/CostlySphere.prefab");
            experimentRunner.AssignFallbackPrefabs(simple, costly);
#endif
        }

        private bool ValidateRequiredReferences()
        {
            ResolveReferences();

            var isValid = true;

            if (experimentRunner == null)
            {
                Debug.LogError("[BatchRunner] Missing reference: ExperimentRunner.");
                isValid = false;
            }

            if (poolFactory == null)
            {
                Debug.LogError("[BatchRunner] Missing reference: UnityPoolFactory (Pools manager object).");
                isValid = false;
            }

            if (experimentAnalytics == null)
            {
                Debug.LogError("[BatchRunner] Missing reference: ExperimentAnalytics.");
                isValid = false;
            }

            if (experimentRunner != null)
            {
                if (experimentRunner.SimplePrefab == null)
                {
                    Debug.LogError("[BatchRunner] Missing reference: Simple prefab in ExperimentRunner.");
                    isValid = false;
                }

                if (experimentRunner.CostlyPrefab == null)
                {
                    Debug.LogError("[BatchRunner] Missing reference: Costly prefab in ExperimentRunner.");
                    isValid = false;
                }

                if (string.IsNullOrWhiteSpace(experimentRunner.SimplePoolKey))
                {
                    Debug.LogError("[BatchRunner] Missing reference: Simple pool key in ExperimentRunner.");
                    isValid = false;
                }

                if (string.IsNullOrWhiteSpace(experimentRunner.CostlyPoolKey))
                {
                    Debug.LogError("[BatchRunner] Missing reference: Costly pool key in ExperimentRunner.");
                    isValid = false;
                }
            }

            return isValid;
        }

        private int EstimateTotalTests()
        {
            const int scenario1Tests = 2;
            const int scenario2Tests = 5 * 2;
            const int scenario3Tests = 2 * 4 * 2; // two object types, four burst counts, two pooling modes
            const int tradeoffTests = 2;
            return scenario1Tests + scenario2Tests + scenario3Tests + tradeoffTests;
        }

        private void UpdateProgress(string label)
        {
            var progress = _totalTests > 0 ? Mathf.Clamp01((float)_completedTests / _totalTests) : 0f;
            ProgressUpdated?.Invoke($"Running Test {_completedTests}/{_totalTests} - {label}", progress);
        }

        private static string BuildCsv(IReadOnlyList<BatchResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Scenario,ObjectType,Mode,GrowthMode,Parameter,AvgFPS,MinFPS,MaxFPS,PeakFrameTimeMs,MemoryDeltaMB,GCCollections");

            for (var i = 0; i < results.Count; i++)
            {
                var r = results[i];
                sb.Append(r.Scenario).Append(',')
                    .Append(r.ObjectType).Append(',')
                    .Append(r.Mode).Append(',')
                    .Append(r.GrowthMode).Append(',')
                    .Append(r.Parameter).Append(',')
                    .Append(r.AverageFps.ToString("0.00")).Append(',')
                    .Append(r.MinFps.ToString("0.00")).Append(',')
                    .Append(r.MaxFps.ToString("0.00")).Append(',')
                    .Append(r.PeakFrameTimeMs.ToString("0.00")).Append(',')
                    .Append(r.MemoryDeltaMb.ToString("0.00")).Append(',')
                    .Append(r.GcCollections)
                    .AppendLine();
            }

            return sb.ToString();
        }
    }
}
