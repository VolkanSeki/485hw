using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ModularExperiment.ObjectPooling;
using UnityEngine;
using UnityEngine.Profiling;
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
            public int TargetFrameRate;
            public float SpawnFrequency;
            public int BurstCount;
            public int PreWarmCount;
            public int PoolCapacity;
            public bool AllowGrowth;
            public float AverageFps;
            public float MinFps;
            public float MaxFps;
            public float FpsStdDev;
            public float OnePercentLowFps;
            public float AverageFrameTimeMs;
            public float PeakFrameTimeMs;
            public float FrameTimeJitterMs;
            public float FrameBudgetMs;
            public int FramesOverBudget;
            public float OverBudgetPercent;
            public float ManagedHeapDeltaMb;
            public float ReservedMemoryDeltaMb;
            public int GcGen0;
            public int GcGen1;
            public int GcGen2;
            public int PoolAllocations;
            public int PoolReuses;
            public int PoolRejections;
            public int RequestedSpawnCount;
            public int ServedSpawnCount;
            public float RejectedSpawnPercent;
            public int PeakActiveCount;
            public int PeakInactiveCount;
            public float PreWarmEfficiencyRatio;
            public bool PoolingOn;
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

        [SerializeField]
        [Min(1)]
        private int stabilizeFrames = 4;

        [Header("Benchmark Environment")]
        [SerializeField]
        private bool lockFrameRateDuringBatch = true;

        [SerializeField]
        [Range(-1, 240)]
        private int benchmarkTargetFrameRate = 60;

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
        [Min(0f)]
        private float gcCooldownSeconds = 0.5f;

        [Header("Trade-off Setup")]
        [SerializeField]
        [Min(1)]
        private int tradeoffBurstCount = 10000;

        [SerializeField]
        [Min(1)]
        private int batchPreWarmCount = 5000;

        private readonly List<BatchResult> _results = new List<BatchResult>();
        private readonly HashSet<string> _executedConfigKeys = new HashSet<string>();
        private bool _isRunning;
        private int _completedTests;
        private int _totalTests;
        private string _activeReportFileName;
        private bool _currentAllowGrowth;
        private int _originalTargetFrameRate;
        private int _originalVsyncCount;

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
            TryLoadPrefabsFromProjectIfMissing();
            if (!ValidateRequiredReferences())
            {
                yield break;
            }

            _isRunning = true;
            _results.Clear();
            _executedConfigKeys.Clear();
            _completedTests = 0;
            _totalTests = EstimateTotalUniqueTests();
            _activeReportFileName = BuildReportFileName();

            var originalPooling = experimentRunner.UsePooling;
            var originalObjectType = experimentRunner.SelectedObjectTypeIndex;
            ApplyBenchmarkFrameRateSettings();

            try
            {
                yield return RunScenario1Matrix();
                yield return RunScenario2Matrix();
                yield return RunScenario3Matrix();
                yield return RunGrowthTradeoffMatrix();

                var reportPath = WriteDetailedReport();
                var csv = BuildCsv(_results);
                Debug.Log($"[BatchExperimentRunner] Batch CSV summary:\n{csv}");
                Debug.Log($"[BatchExperimentRunner] Detailed report exported to: {reportPath}");
                BatchCompleted?.Invoke(csv);
            }
            finally
            {
                experimentRunner.StopAllScenarios();
                experimentRunner.UsePooling = originalPooling;
                experimentRunner.SelectedObjectTypeIndex = originalObjectType;
                RestoreFrameRateSettings();
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
            var mode = pooling ? "PoolingON" : "PoolingOFF";
            var parameter = $"SpawnFreq={experimentRunner.SpawnFrequency:0.00}";
            if (!TryRegisterConfiguration("Scenario1", experimentRunner.CurrentObjectTypeLabel, mode, "N/A", parameter))
            {
                yield break;
            }

            yield return PrepareEnvironmentBetweenTests();
            experimentRunner.StopAllScenarios();
            experimentRunner.UsePooling = pooling;
            experimentRunner.StartScenario1();

            yield return CaptureWindow(
                scenario: "Scenario1",
                objectType: experimentRunner.CurrentObjectTypeLabel,
                mode: mode,
                growthMode: "N/A",
                parameter: parameter,
                poolingOn: pooling);

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
            var mode = pooling ? "PoolingON" : "PoolingOFF";
            var parameter = $"FastFreq={frequency:0.00}";
            if (!TryRegisterConfiguration("Scenario2", experimentRunner.CurrentObjectTypeLabel, mode, "N/A", parameter))
            {
                yield break;
            }

            yield return PrepareEnvironmentBetweenTests();
            experimentRunner.StopAllScenarios();
            experimentRunner.UsePooling = pooling;
            experimentRunner.StartScenario2();

            yield return CaptureWindow(
                scenario: "Scenario2",
                objectType: experimentRunner.CurrentObjectTypeLabel,
                mode: mode,
                growthMode: "N/A",
                parameter: parameter,
                poolingOn: pooling);

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
            yield return RunSingleBurst(true, tradeoffBurstCount, "Cutoff", prepareAllowGrowth: false);

            yield return RebuildPoolsForGrowth(true);
            yield return RunSingleBurst(true, tradeoffBurstCount, "Growing", prepareAllowGrowth: true);

            // Return to default cutoff behavior after trade-off tests.
            yield return RebuildPoolsForGrowth(false);
        }

        private IEnumerator RunSingleBurst(bool pooling, int count, string growthMode, bool prepareAllowGrowth = false)
        {
            var mode = pooling ? "PoolingON" : "PoolingOFF";
            var parameter = $"Burst={count}";
            if (!TryRegisterConfiguration("Scenario3", experimentRunner.CurrentObjectTypeLabel, mode, growthMode, parameter))
            {
                yield break;
            }

            yield return PrepareEnvironmentBetweenTests(prepareAllowGrowth);
            experimentRunner.StopAllScenarios();
            experimentRunner.UsePooling = pooling;
            experimentRunner.BurstCount = count;
            if (pooling)
            {
                yield return PreStagePoolCapacityForBurst(count);
            }

            yield return CaptureWindow(
                scenario: "Scenario3",
                objectType: experimentRunner.CurrentObjectTypeLabel,
                mode: mode,
                growthMode: growthMode,
                parameter: parameter,
                poolingOn: pooling,
                expectedSpawnRequests: count,
                onRecordStart: () => experimentRunner.TriggerScenario3BurstWithCount(count));

            yield return WaitSettle();
        }

        private IEnumerator CaptureWindow(
            string scenario,
            string objectType,
            string mode,
            string growthMode,
            string parameter,
            bool poolingOn,
            int expectedSpawnRequests = 0,
            Action onRecordStart = null)
        {
            // Align profiler window: cleanup complete -> explicit GC -> cooldown -> baseline -> stimulus.
            yield return ForceGcAndCooldown();
            yield return new WaitForEndOfFrame();

            var startManaged = GC.GetTotalMemory(false);
            var startReserved = Profiler.GetTotalReservedMemoryLong();
            var startGc0 = GC.CollectionCount(0);
            var startGc1 = GC.CollectionCount(1);
            var startGc2 = GC.CollectionCount(2);
            var startPoolStats = PoolManager.GetAggregateStats();

            onRecordStart?.Invoke();
            yield return new WaitForEndOfFrame();

            var memoryEndAfterStimulus = GC.GetTotalMemory(false);
            var reservedEndAfterStimulus = Profiler.GetTotalReservedMemoryLong();

            UpdateProgress($"Warm-up: {scenario} ({mode}) | File: {_activeReportFileName}");
            var warmupElapsed = 0f;
            while (warmupElapsed < warmUpSeconds)
            {
                warmupElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            UpdateProgress($"Recording: {scenario} ({mode}) | File: {_activeReportFileName}");
            var elapsed = 0f;
            var fpsSamples = new List<float>(256);
            var frameSamplesMs = new List<float>(256);
            var peakActiveCount = 0;
            var peakInactiveCount = 0;

            while (elapsed < recordingSeconds)
            {
                var dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
                var fps = 1f / dt;
                var frameMs = dt * 1000f;

                fpsSamples.Add(fps);
                frameSamplesMs.Add(frameMs);

                var livePoolStats = PoolManager.GetAggregateStats();
                var inactive = livePoolStats.InactiveCount;
                if (inactive > peakInactiveCount)
                {
                    peakInactiveCount = inactive;
                }

                var activeNow = livePoolStats.ActiveCount;
                if (activeNow > peakActiveCount)
                {
                    peakActiveCount = activeNow;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            var endGc0 = GC.CollectionCount(0);
            var endGc1 = GC.CollectionCount(1);
            var endGc2 = GC.CollectionCount(2);
            var endPoolStats = PoolManager.GetAggregateStats();
            var frameStats = ExperimentAnalytics.ComputeFrameStats(fpsSamples, frameSamplesMs);
            var frameBudgetMs = GetFrameBudgetMs();
            var frameTimeJitterMs = CalculateFrameTimeJitter(frameSamplesMs);
            var overBudgetFrames = CountFramesOverBudget(frameSamplesMs, frameBudgetMs);
            var overBudgetPercent = frameSamplesMs.Count > 0
                ? (overBudgetFrames / (float)frameSamplesMs.Count) * 100f
                : 0f;
            var selectedKey = experimentRunner.GetSelectedPoolKey();
            var poolConfig = GetPoolConfigForKey(selectedKey);

            var poolAllocDelta = endPoolStats.TotalAllocations - startPoolStats.TotalAllocations;
            var poolReuseDelta = endPoolStats.TotalReuses - startPoolStats.TotalReuses;
            var poolRejectionDelta = endPoolStats.TotalRejections - startPoolStats.TotalRejections;
            var retrievals = Mathf.Max(1, poolAllocDelta + poolReuseDelta);
            var efficiency = poolingOn ? (poolReuseDelta / (float)retrievals) * 100f : 0f;
            var requestedSpawns = expectedSpawnRequests > 0 ? expectedSpawnRequests : 0;
            var servedSpawns = Mathf.Max(0, requestedSpawns - Mathf.Max(0, poolRejectionDelta));
            var rejectedPercent = requestedSpawns > 0
                ? (Mathf.Max(0, poolRejectionDelta) / (float)requestedSpawns) * 100f
                : 0f;

            _results.Add(new BatchResult
            {
                Scenario = scenario,
                ObjectType = objectType,
                Mode = mode,
                GrowthMode = growthMode,
                Parameter = parameter,
                TargetFrameRate = Application.targetFrameRate,
                SpawnFrequency = experimentRunner.SpawnFrequency,
                BurstCount = experimentRunner.BurstCount,
                PreWarmCount = poolConfig.PreWarmCount,
                PoolCapacity = poolConfig.MaxSize,
                AllowGrowth = _currentAllowGrowth,
                AverageFps = frameStats.AverageFps,
                MinFps = frameStats.MinFps,
                MaxFps = frameStats.MaxFps,
                FpsStdDev = frameStats.FpsStdDev,
                OnePercentLowFps = frameStats.OnePercentLowFps,
                AverageFrameTimeMs = frameStats.AverageFrameTimeMs,
                PeakFrameTimeMs = frameStats.PeakFrameTimeMs,
                FrameTimeJitterMs = frameTimeJitterMs,
                FrameBudgetMs = frameBudgetMs,
                FramesOverBudget = overBudgetFrames,
                OverBudgetPercent = overBudgetPercent,
                ManagedHeapDeltaMb = ToMb(memoryEndAfterStimulus - startManaged),
                ReservedMemoryDeltaMb = ToMb(reservedEndAfterStimulus - startReserved),
                GcGen0 = endGc0 - startGc0,
                GcGen1 = endGc1 - startGc1,
                GcGen2 = endGc2 - startGc2,
                PoolAllocations = poolingOn ? poolAllocDelta : 0,
                PoolReuses = poolingOn ? poolReuseDelta : 0,
                PoolRejections = poolingOn ? Mathf.Max(0, poolRejectionDelta) : 0,
                RequestedSpawnCount = requestedSpawns,
                ServedSpawnCount = poolingOn ? servedSpawns : requestedSpawns,
                RejectedSpawnPercent = poolingOn ? rejectedPercent : 0f,
                PeakActiveCount = poolingOn ? peakActiveCount : 0,
                PeakInactiveCount = poolingOn ? peakInactiveCount : 0,
                PreWarmEfficiencyRatio = poolingOn ? efficiency : 0f,
                PoolingOn = poolingOn
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
                preWarmCount: Mathf.Max(1, batchPreWarmCount),
                maxSize: 10000,
                allowGrowth: allowGrowth,
                growthChunkSize: growthChunkSize,
                growthFactor: growthFactor,
                maxGrowthChunkSize: maxGrowthChunkSize);

            UnityPoolFactory.CreatePoolFromPrefab(
                costlyKey,
                experimentRunner.CostlyPrefab,
                parent,
                preWarmCount: Mathf.Max(1, batchPreWarmCount),
                maxSize: 10000,
                allowGrowth: allowGrowth,
                growthChunkSize: growthChunkSize,
                growthFactor: growthFactor,
                maxGrowthChunkSize: maxGrowthChunkSize);

            yield return null;
        }

        private IEnumerator WaitSettle()
        {
            if (settleBetweenTestsSeconds > 0f)
            {
                yield return new WaitForSeconds(settleBetweenTestsSeconds);
            }
        }

        private IEnumerator PrepareEnvironmentBetweenTests(bool allowGrowthForPools = false)
        {
            _currentAllowGrowth = allowGrowthForPools;
            PoolManager.ClearAllPools();
            RebuildDefaultPools(allowGrowth: allowGrowthForPools);

            yield return ForceGcAndCooldown();
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

        private int EstimateTotalUniqueTests()
        {
            const int scenario1Tests = 2;
            const int scenario2Tests = 5 * 2;
            const int scenario3Tests = 2 * 4 * 2; // two object types, four burst counts, two pooling modes
            const int tradeoffTests = 1; // growing mode only; cutoff variant already exists in scenario3 matrix.
            return scenario1Tests + scenario2Tests + scenario3Tests + tradeoffTests;
        }

        private void UpdateProgress(string label)
        {
            var progress = _totalTests > 0 ? Mathf.Clamp01((float)_completedTests / _totalTests) : 0f;
            ProgressUpdated?.Invoke($"Running Test {_completedTests}/{_totalTests} - {label}", progress);
        }

        private void ApplyBenchmarkFrameRateSettings()
        {
            _originalTargetFrameRate = Application.targetFrameRate;
            _originalVsyncCount = QualitySettings.vSyncCount;

            if (!lockFrameRateDuringBatch || benchmarkTargetFrameRate <= 0)
            {
                return;
            }

            // vSync must be disabled, otherwise targetFrameRate can be ignored.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = benchmarkTargetFrameRate;
        }

        private void RestoreFrameRateSettings()
        {
            QualitySettings.vSyncCount = _originalVsyncCount;
            Application.targetFrameRate = _originalTargetFrameRate;
        }

        private IEnumerator WaitForManagedHeapCooldown()
        {
            const int requiredStableFrames = 3;
            const int maxCooldownFrames = 120;
            const long stabilityThresholdBytes = 64 * 1024; // 64KB drift tolerance

            var stableFrames = 0;
            var frameCount = 0;
            var previous = GC.GetTotalMemory(false);

            while (frameCount < maxCooldownFrames && stableFrames < requiredStableFrames)
            {
                yield return null;
                frameCount++;
                var current = GC.GetTotalMemory(false);
                var delta = Math.Abs(current - previous);
                if (delta <= stabilityThresholdBytes)
                {
                    stableFrames++;
                }
                else
                {
                    stableFrames = 0;
                }

                previous = current;
            }
        }

        private IEnumerator ForceGcAndCooldown()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (gcCooldownSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(gcCooldownSeconds);
            }

            var frames = Mathf.Max(1, stabilizeFrames);
            for (var i = 0; i < frames; i++)
            {
                yield return null;
            }

            yield return WaitForManagedHeapCooldown();
        }

        private IEnumerator PreStagePoolCapacityForBurst(int burstCountTarget)
        {
            var key = experimentRunner.GetSelectedPoolKey();
            var desiredInactive = Mathf.Max(1, burstCountTarget);
            var batchSize = Mathf.Max(64, growthChunkSize);
            var safetyIterations = 0;

            while (safetyIterations < 1000)
            {
                safetyIterations++;
                if (!PoolManager.TryGetStats(key, out var stats))
                {
                    yield break;
                }

                var currentInactive = stats.InactiveCount;
                if (currentInactive >= desiredInactive)
                {
                    break;
                }

                var toCreate = Mathf.Min(batchSize, desiredInactive - currentInactive);
                PoolManager.PreWarm(key, toCreate);

                // Spread heavy prefab creation over frames to avoid one-frame stalls.
                yield return null;

                if (!PoolManager.TryGetStats(key, out var afterStats))
                {
                    yield break;
                }

                if (afterStats.InactiveCount <= currentInactive)
                {
                    // Capacity cutoff reached or pool cannot grow further.
                    break;
                }
            }

            yield return ForceGcAndCooldown();
        }

        private static string BuildCsv(IReadOnlyList<BatchResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Scenario,ObjectType,Mode,GrowthMode,Parameter,AvgFPS,MinFPS,MaxFPS,FPSStdDev,OnePercentLowFPS,AvgFrameTimeMs,PeakFrameTimeMs,FrameTimeJitterMs,FrameBudgetMs,FramesOverBudget,OverBudgetPercent,ManagedHeapDeltaMB,ReservedMemoryDeltaMB,GCGen0,GCGen1,GCGen2,PoolAllocations,PoolReuses,PoolRejections,RequestedSpawns,ServedSpawns,RejectedSpawnPercent,PeakActiveCount,PeakInactiveCount,PreWarmEfficiencyRatio");

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
                    .Append(r.FpsStdDev.ToString("0.00")).Append(',')
                    .Append(r.OnePercentLowFps.ToString("0.00")).Append(',')
                    .Append(r.AverageFrameTimeMs.ToString("0.00")).Append(',')
                    .Append(r.PeakFrameTimeMs.ToString("0.00")).Append(',')
                    .Append(r.FrameTimeJitterMs.ToString("0.00")).Append(',')
                    .Append(r.FrameBudgetMs.ToString("0.00")).Append(',')
                    .Append(r.FramesOverBudget).Append(',')
                    .Append(r.OverBudgetPercent.ToString("0.00")).Append(',')
                    .Append(r.ManagedHeapDeltaMb.ToString("0.00")).Append(',')
                    .Append(r.ReservedMemoryDeltaMb.ToString("0.00")).Append(',')
                    .Append(r.GcGen0).Append(',')
                    .Append(r.GcGen1).Append(',')
                    .Append(r.GcGen2).Append(',')
                    .Append(r.PoolAllocations).Append(',')
                    .Append(r.PoolReuses).Append(',')
                    .Append(r.PoolRejections).Append(',')
                    .Append(r.RequestedSpawnCount).Append(',')
                    .Append(r.ServedSpawnCount).Append(',')
                    .Append(r.RejectedSpawnPercent.ToString("0.00")).Append(',')
                    .Append(r.PeakActiveCount).Append(',')
                    .Append(r.PeakInactiveCount).Append(',')
                    .Append(r.PreWarmEfficiencyRatio.ToString("0.00"))
                    .AppendLine();
            }

            return sb.ToString();
        }

        private string WriteDetailedReport()
        {
            var outputDirectory = Path.Combine(Application.dataPath, "ExperimentResults");
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var reportPath = Path.Combine(outputDirectory, _activeReportFileName);
            using (var writer = new StreamWriter(reportPath, append: false, encoding: Encoding.UTF8))
            {
                WriteSystemHeader(writer);
                WriteRunLogs(writer);
                WriteComparativeDeltaSummary(writer);
            }

            return reportPath;
        }

        private void WriteSystemHeader(StreamWriter writer)
        {
            writer.WriteLine("================================");
            writer.WriteLine("POOLING BATCH BENCHMARK REPORT");
            writer.WriteLine("================================");
            writer.WriteLine($"Generated At: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"Unity Version: {Application.unityVersion}");
            writer.WriteLine($"Operating System: {SystemInfo.operatingSystem}");
            writer.WriteLine($"CPU: {SystemInfo.processorType}");
            writer.WriteLine($"GPU: {SystemInfo.graphicsDeviceName}");
            writer.WriteLine($"System RAM (MB): {SystemInfo.systemMemorySize}");
            writer.WriteLine($"Frame Rate Lock Enabled: {lockFrameRateDuringBatch}");
            writer.WriteLine($"Configured Target Frame Rate: {benchmarkTargetFrameRate}");
            writer.WriteLine("================================");
            writer.WriteLine();
        }

        private void WriteRunLogs(StreamWriter writer)
        {
            writer.WriteLine("INDIVIDUAL RUN LOGS");
            writer.WriteLine("================================");
            for (var i = 0; i < _results.Count; i++)
            {
                var r = _results[i];
                writer.WriteLine($"Run #{i + 1}: {r.Scenario} | {r.Mode} | {r.ObjectType} | {r.Parameter}");
                writer.WriteLine("  - Test Metadata:");
                writer.WriteLine($"    * Pooling Mode: {(r.PoolingOn ? "ON" : "OFF")}");
                writer.WriteLine($"    * Target Frame Rate: {r.TargetFrameRate}");
                writer.WriteLine($"    * Object Type: {r.ObjectType}");
                writer.WriteLine("  - Configuration:");
                writer.WriteLine($"    * Spawn Frequency: {r.SpawnFrequency:0.000}s");
                writer.WriteLine($"    * Burst Count: {r.BurstCount}");
                writer.WriteLine($"    * Pre-warm Count: {r.PreWarmCount}");
                writer.WriteLine($"    * Pool Capacity: {r.PoolCapacity}");
                writer.WriteLine($"    * Allow Growth: {r.AllowGrowth}");
                writer.WriteLine("  - Frame Rate Analytics:");
                writer.WriteLine($"    * Average FPS: {r.AverageFps:0.00}");
                writer.WriteLine($"    * Minimum FPS: {r.MinFps:0.00}");
                writer.WriteLine($"    * Maximum FPS: {r.MaxFps:0.00}");
                writer.WriteLine($"    * FPS Std Dev: {r.FpsStdDev:0.00}");
                writer.WriteLine($"    * 1% Low FPS: {r.OnePercentLowFps:0.00}");
                writer.WriteLine("  - CPU & Frame Time:");
                writer.WriteLine($"    * Average Frame Time (ms): {r.AverageFrameTimeMs:0.00}");
                writer.WriteLine($"    * Peak Frame Time (ms): {r.PeakFrameTimeMs:0.00}");
                writer.WriteLine($"    * Frame-Time Jitter (ms): {r.FrameTimeJitterMs:0.00}");
                writer.WriteLine($"    * Frame Budget (ms): {r.FrameBudgetMs:0.00}");
                writer.WriteLine($"    * Frames Over Budget: {r.FramesOverBudget} ({r.OverBudgetPercent:0.00}%)");
                writer.WriteLine("  - Memory & Heap:");
                writer.WriteLine($"    * Managed Heap Delta (MB): {r.ManagedHeapDeltaMb:0.00}");
                writer.WriteLine($"    * System Reserved Delta (MB): {r.ReservedMemoryDeltaMb:0.00}");
                writer.WriteLine("  - Garbage Collection:");
                writer.WriteLine($"    * GC Gen0: {r.GcGen0}");
                writer.WriteLine($"    * GC Gen1: {r.GcGen1}");
                writer.WriteLine($"    * GC Gen2: {r.GcGen2}");

                if (r.PoolingOn)
                {
                    writer.WriteLine("  - Pool Performance (Pooling ON):");
                    writer.WriteLine($"    * Total Allocations (Instantiate): {r.PoolAllocations}");
                    writer.WriteLine($"    * Total Reuses: {r.PoolReuses}");
                    writer.WriteLine($"    * Total Rejections: {r.PoolRejections}");
                    writer.WriteLine($"    * Requested Spawns: {r.RequestedSpawnCount}");
                    writer.WriteLine($"    * Served Spawns: {r.ServedSpawnCount}");
                    writer.WriteLine($"    * Rejected Spawn Percent: {r.RejectedSpawnPercent:0.00}%");
                    writer.WriteLine($"    * Peak Active Count: {r.PeakActiveCount}");
                    writer.WriteLine($"    * Peak Inactive Count: {r.PeakInactiveCount}");
                    writer.WriteLine($"    * Pre-warm Efficiency Ratio: {r.PreWarmEfficiencyRatio:0.00}%");
                }

                writer.WriteLine("================================");
            }
            writer.WriteLine();
        }

        private void WriteComparativeDeltaSummary(StreamWriter writer)
        {
            writer.WriteLine("COMPARATIVE DELTA SUMMARY (ON vs OFF)");
            writer.WriteLine("================================");

            var offMap = new Dictionary<string, BatchResult>();
            for (var i = 0; i < _results.Count; i++)
            {
                var run = _results[i];
                var key = BuildComparisonKey(run);
                if (run.PoolingOn)
                {
                    if (!offMap.TryGetValue(key, out var off))
                    {
                        continue;
                    }

                    writer.WriteLine($"{run.Scenario} | {run.ObjectType} | {run.Parameter}");
                    if (run.PoolRejections > 0)
                    {
                        writer.WriteLine($"  - Comparison Validity: INVALID (Pooling ON rejected {run.PoolRejections} spawns; not equivalent workload).");
                        writer.WriteLine("================================");
                        continue;
                    }

                    writer.WriteLine($"  - FPS Improvement: {ComputePercentIncrease(run.AverageFps, off.AverageFps):0.00}%");
                    writer.WriteLine($"  - Spike Reduction: {ComputePercentDecrease(run.PeakFrameTimeMs, off.PeakFrameTimeMs):0.00}%");
                    writer.WriteLine($"  - GC Elimination: {ComputePercentDecrease(TotalGc(run), TotalGc(off)):0.00}%");
                    writer.WriteLine($"  - Memory Savings: {ComputePercentDecrease(run.ManagedHeapDeltaMb, off.ManagedHeapDeltaMb):0.00}%");
                    writer.WriteLine("================================");
                }
                else
                {
                    offMap[key] = run;
                }
            }
        }

        private void RebuildDefaultPools(bool allowGrowth)
        {
            var parent = poolFactory != null ? poolFactory.transform : transform;
            var simpleKey = experimentRunner.SimplePoolKey;
            var costlyKey = experimentRunner.CostlyPoolKey;
            var simpleCfg = GetPoolConfigForKey(simpleKey);
            var costlyCfg = GetPoolConfigForKey(costlyKey);

            UnityPoolFactory.CreatePoolFromPrefab(
                simpleKey,
                experimentRunner.SimplePrefab,
                parent,
                preWarmCount: Mathf.Max(1, batchPreWarmCount),
                maxSize: simpleCfg.MaxSize,
                allowGrowth: allowGrowth || simpleCfg.AllowGrowth,
                growthChunkSize: growthChunkSize,
                growthFactor: growthFactor,
                maxGrowthChunkSize: maxGrowthChunkSize);

            UnityPoolFactory.CreatePoolFromPrefab(
                costlyKey,
                experimentRunner.CostlyPrefab,
                parent,
                preWarmCount: Mathf.Max(1, batchPreWarmCount),
                maxSize: costlyCfg.MaxSize,
                allowGrowth: allowGrowth || costlyCfg.AllowGrowth,
                growthChunkSize: growthChunkSize,
                growthFactor: growthFactor,
                maxGrowthChunkSize: maxGrowthChunkSize);
        }

        private UnityPoolFactory.PoolConfig GetPoolConfigForKey(string key)
        {
            if (poolFactory != null && poolFactory.TryGetPoolConfig(key, out var cfg))
            {
                return cfg;
            }

            return new UnityPoolFactory.PoolConfig(preWarmCount: 0, maxSize: 10000, allowGrowth: false);
        }

        private string BuildReportFileName()
        {
            return $"Pooling_Batch_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        }

        private static string BuildRunKey(string scenario, string objectType, string mode, string growthMode, string parameter)
        {
            return $"{scenario}|{objectType}|{mode}|{growthMode}|{parameter}";
        }

        private bool TryRegisterConfiguration(string scenario, string objectType, string mode, string growthMode, string parameter)
        {
            var key = BuildRunKey(scenario, objectType, mode, growthMode, parameter);
            if (_executedConfigKeys.Contains(key))
            {
                Debug.LogWarning($"[BatchExperimentRunner] Duplicate configuration skipped: {key}");
                return false;
            }

            _executedConfigKeys.Add(key);
            return true;
        }

        private static string BuildComparisonKey(BatchResult run)
        {
            return $"{run.Scenario}|{run.ObjectType}|{run.Parameter}|{run.GrowthMode}";
        }

        private static int TotalGc(BatchResult run)
        {
            return run.GcGen0 + run.GcGen1 + run.GcGen2;
        }

        private static float ComputePercentIncrease(float newer, float baseline)
        {
            if (Mathf.Abs(baseline) < 0.0001f)
            {
                return 0f;
            }

            return ((newer - baseline) / baseline) * 100f;
        }

        private static float ComputePercentDecrease(float newer, float baseline)
        {
            if (Mathf.Abs(baseline) < 0.0001f)
            {
                return 0f;
            }

            return ((baseline - newer) / baseline) * 100f;
        }

        private float GetFrameBudgetMs()
        {
            var target = lockFrameRateDuringBatch ? benchmarkTargetFrameRate : Application.targetFrameRate;
            if (target <= 0)
            {
                return 0f;
            }

            return 1000f / target;
        }

        private static int CountFramesOverBudget(List<float> frameTimesMs, float budgetMs)
        {
            if (frameTimesMs == null || frameTimesMs.Count == 0 || budgetMs <= 0f)
            {
                return 0;
            }

            var overBudget = 0;
            for (var i = 0; i < frameTimesMs.Count; i++)
            {
                if (frameTimesMs[i] > budgetMs)
                {
                    overBudget++;
                }
            }

            return overBudget;
        }

        private static float CalculateFrameTimeJitter(List<float> frameTimesMs)
        {
            if (frameTimesMs == null || frameTimesMs.Count < 2)
            {
                return 0f;
            }

            var jitterSamples = frameTimesMs.Count - 1;
            var sum = 0f;
            for (var i = 1; i < frameTimesMs.Count; i++)
            {
                sum += Mathf.Abs(frameTimesMs[i] - frameTimesMs[i - 1]);
            }

            return sum / jitterSamples;
        }

        private static float ToMb(long bytes)
        {
            return (float)(bytes / (1024.0 * 1024.0));
        }
    }
}
