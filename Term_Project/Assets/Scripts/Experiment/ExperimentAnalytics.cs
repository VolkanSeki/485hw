using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ModularExperiment.ObjectPooling;
using TMPro;
using UnityEngine;

namespace ModularExperiment.Experiment
{
    /// <summary>
    /// Records experiment sessions and summarizes CPU vs memory trade-offs.
    /// </summary>
    public class ExperimentAnalytics : MonoBehaviour
    {
        public readonly struct FrameStats
        {
            public FrameStats(float averageFps, float minFps, float maxFps, float fpsStdDev, float onePercentLowFps, float averageFrameTimeMs, float peakFrameTimeMs)
            {
                AverageFps = averageFps;
                MinFps = minFps;
                MaxFps = maxFps;
                FpsStdDev = fpsStdDev;
                OnePercentLowFps = onePercentLowFps;
                AverageFrameTimeMs = averageFrameTimeMs;
                PeakFrameTimeMs = peakFrameTimeMs;
            }

            public float AverageFps { get; }
            public float MinFps { get; }
            public float MaxFps { get; }
            public float FpsStdDev { get; }
            public float OnePercentLowFps { get; }
            public float AverageFrameTimeMs { get; }
            public float PeakFrameTimeMs { get; }
        }

        [Serializable]
        public struct SessionResult
        {
            public string SessionName;
            public string ScenarioKey;
            public bool PoolingEnabled;
            public float DurationSeconds;
            public float AverageFps;
            public float MinFps;
            public float AverageFrameTimeMs;
            public float PeakFrameTimeMs;
            public long StartMemoryBytes;
            public long EndMemoryBytes;
            public long AverageMemoryBytes;
            public long PeakMemoryBytes;
            public long MemoryDeltaBytes;
            public int PeakObjectCount;
            public int GcGen0Collections;
            public int GcGen1Collections;
            public int GcGen2Collections;

            public string ToMultilineReport()
            {
                return
                    $"[Session: {SessionName}] Pooling={(PoolingEnabled ? "ON" : "OFF")}\n" +
                    $"Scenario: {ScenarioKey}\n" +
                    $"Duration: {DurationSeconds:0.00}s\n" +
                    $"FPS Avg/Min: {AverageFps:0.0} / {MinFps:0.0}\n" +
                    $"Frame Time Avg/Peak: {AverageFrameTimeMs:0.00}ms / {PeakFrameTimeMs:0.00}ms\n" +
                    $"Memory Start/End: {BytesToMb(StartMemoryBytes):0.00}MB / {BytesToMb(EndMemoryBytes):0.00}MB\n" +
                    $"Memory Avg/Peak: {BytesToMb(AverageMemoryBytes):0.00}MB / {BytesToMb(PeakMemoryBytes):0.00}MB\n" +
                    $"Memory Delta: {BytesToMb(MemoryDeltaBytes):0.00}MB\n" +
                    $"Peak Object Count: {PeakObjectCount}\n" +
                    $"GC Collections (Gen0/Gen1/Gen2): {GcGen0Collections}/{GcGen1Collections}/{GcGen2Collections}";
            }
        }

        [Header("References")]
        [SerializeField]
        private ExperimentRunner experimentRunner;

        [Header("Optional UI Output")]
        [SerializeField]
        private TMP_Text reportText;

        [SerializeField]
        private TMP_Text memorySawtoothText;

        [Header("Behavior")]
        [SerializeField]
        private bool autoHookRunnerEvents = true;

        [SerializeField]
        [Min(1)]
        private int objectCountSampleEveryNFrames = 2;

        [Header("Prewarm Impact Test")]
        [SerializeField]
        private string preWarmPoolKey = "Costly";

        [SerializeField]
        [Min(1)]
        private int preWarmCount = 1000;

        private readonly List<SessionResult> completedSessions = new List<SessionResult>();
        private bool isRecording;
        private string currentSessionName = string.Empty;
        private bool currentPoolingState;
        private float recordingStartRealtime;
        private float sumFps;
        private float minFps;
        private float sumFrameTimeMs;
        private int sampledFrames;
        private long startMemoryBytes;
        private long peakMemoryBytes;
        private long sumMemoryBytes;
        private int objectSampleFrameCounter;
        private int peakObjectCount;
        private int startGen0Collections;
        private int startGen1Collections;
        private int startGen2Collections;
        private float nextMemoryLabelUpdateTime;

        public IReadOnlyList<SessionResult> CompletedSessions => completedSessions;

        public string PreWarmPoolKey
        {
            get => preWarmPoolKey;
            set => preWarmPoolKey = value;
        }

        public int PreWarmCount
        {
            get => preWarmCount;
            set => preWarmCount = Mathf.Max(1, value);
        }

        private void Awake()
        {
            if (experimentRunner == null)
            {
                experimentRunner = FindObjectOfType<ExperimentRunner>();
            }
        }

        private void OnEnable()
        {
            if (!autoHookRunnerEvents || experimentRunner == null)
            {
                return;
            }

            experimentRunner.ScenarioStarted += OnScenarioStarted;
            experimentRunner.ScenarioStopped += OnScenarioStopped;
        }

        private void OnDisable()
        {
            if (!autoHookRunnerEvents || experimentRunner == null)
            {
                return;
            }

            experimentRunner.ScenarioStarted -= OnScenarioStarted;
            experimentRunner.ScenarioStopped -= OnScenarioStopped;
        }

        private void Update()
        {
            UpdateMemorySawtoothLabel();

            if (!isRecording)
            {
                return;
            }

            RecordFrameSample();
        }

        /// <summary>
        /// Manual session start (useful for custom UI hooks).
        /// </summary>
        public void BeginRecording(string sessionName)
        {
            if (isRecording)
            {
                EndRecording();
            }

            if (experimentRunner == null)
            {
                UnityEngine.Debug.LogWarning("[ExperimentAnalytics] Cannot record: ExperimentRunner is missing.");
                return;
            }

            currentSessionName = string.IsNullOrWhiteSpace(sessionName) ? "UnnamedSession" : sessionName;
            currentPoolingState = experimentRunner.UsePooling;
            recordingStartRealtime = Time.realtimeSinceStartup;
            sumFps = 0f;
            minFps = float.MaxValue;
            sumFrameTimeMs = 0f;
            sampledFrames = 0;
            objectSampleFrameCounter = 0;
            peakObjectCount = 0;

            startMemoryBytes = GC.GetTotalMemory(false);
            peakMemoryBytes = startMemoryBytes;
            sumMemoryBytes = 0L;

            startGen0Collections = GC.CollectionCount(0);
            startGen1Collections = GC.CollectionCount(1);
            startGen2Collections = GC.CollectionCount(2);

            isRecording = true;
            UnityEngine.Debug.Log(
                $"[ExperimentAnalytics] Recording started: '{currentSessionName}' | Pooling={(currentPoolingState ? "ON" : "OFF")}");
        }

        /// <summary>
        /// Manual session stop and summary generation.
        /// </summary>
        public SessionResult EndRecording()
        {
            if (!isRecording)
            {
                UnityEngine.Debug.LogWarning("[ExperimentAnalytics] EndRecording called while no session is active.");
                return default;
            }

            if (sampledFrames == 0)
            {
                RecordFrameSample();
            }

            isRecording = false;
            var duration = Mathf.Max(0.0001f, Time.realtimeSinceStartup - recordingStartRealtime);
            var endMemory = GC.GetTotalMemory(false);

            var avgFps = sampledFrames > 0 ? sumFps / sampledFrames : 0f;
            var safeMinFps = sampledFrames > 0 ? minFps : 0f;
            var avgFrameTimeMs = sampledFrames > 0 ? sumFrameTimeMs / sampledFrames : 0f;
            var peakFrameTimeMs = safeMinFps > 0f ? 1000f / safeMinFps : 0f;
            var avgMemory = sampledFrames > 0 ? sumMemoryBytes / sampledFrames : endMemory;

            var result = new SessionResult
            {
                SessionName = currentSessionName,
                ScenarioKey = NormalizeScenarioKey(currentSessionName),
                PoolingEnabled = currentPoolingState,
                DurationSeconds = duration,
                AverageFps = avgFps,
                MinFps = safeMinFps,
                AverageFrameTimeMs = avgFrameTimeMs,
                PeakFrameTimeMs = peakFrameTimeMs,
                StartMemoryBytes = startMemoryBytes,
                EndMemoryBytes = endMemory,
                AverageMemoryBytes = avgMemory,
                PeakMemoryBytes = peakMemoryBytes,
                MemoryDeltaBytes = endMemory - startMemoryBytes,
                PeakObjectCount = peakObjectCount,
                GcGen0Collections = GC.CollectionCount(0) - startGen0Collections,
                GcGen1Collections = GC.CollectionCount(1) - startGen1Collections,
                GcGen2Collections = GC.CollectionCount(2) - startGen2Collections
            };

            completedSessions.Add(result);
            var report = result.ToMultilineReport();
            UnityEngine.Debug.Log($"[ExperimentAnalytics] Session summary:\n{report}");
            if (reportText != null)
            {
                reportText.text = report;
            }

            return result;
        }

        /// <summary>
        /// Compares two explicit runs (typically ON vs OFF).
        /// </summary>
        public string CompareResults(SessionResult poolingOn, SessionResult poolingOff)
        {
            var frameSpikeReduction = SafePercentDelta(poolingOff.PeakFrameTimeMs, poolingOn.PeakFrameTimeMs);
            var avgFpsGain = SafePercentDelta(poolingOn.AverageFps, poolingOff.AverageFps);
            var baselineMemoryIncreaseMb =
                BytesToMb(poolingOn.AverageMemoryBytes - poolingOff.AverageMemoryBytes);

            var comparison =
                $"Comparison ({poolingOn.SessionName} ON vs {poolingOff.SessionName} OFF): " +
                $"Pooling reduced peak frame time by {frameSpikeReduction:0.0}% and changed average FPS by {avgFpsGain:0.0}%. " +
                $"Baseline memory changed by {baselineMemoryIncreaseMb:0.00} MB.";

            UnityEngine.Debug.Log($"[ExperimentAnalytics] {comparison}");
            if (reportText != null)
            {
                reportText.text = comparison;
            }

            return comparison;
        }

        /// <summary>
        /// Helper to compare the latest ON and OFF sessions for a scenario keyword.
        /// </summary>
        public string CompareLatestForScenario(string scenarioKeyword)
        {
            var normalized = NormalizeScenarioKey(scenarioKeyword);
            if (normalized == UnknownScenario)
            {
                const string unsupported = "Unknown scenario key. Use Scenario1, Scenario2, or Scenario3.";
                UnityEngine.Debug.LogWarning($"[ExperimentAnalytics] {unsupported}");
                if (reportText != null)
                {
                    reportText.text = unsupported;
                }

                return unsupported;
            }

            var onSession = completedSessions.LastOrDefault(
                s => s.PoolingEnabled && string.Equals(s.ScenarioKey, normalized, StringComparison.Ordinal));
            var offSession = completedSessions.LastOrDefault(
                s => !s.PoolingEnabled && string.Equals(s.ScenarioKey, normalized, StringComparison.Ordinal));

            if (string.IsNullOrEmpty(onSession.SessionName) || string.IsNullOrEmpty(offSession.SessionName))
            {
                const string message = "Need one Pooling ON and one Pooling OFF session to compare.";
                UnityEngine.Debug.LogWarning($"[ExperimentAnalytics] {message}");
                if (reportText != null)
                {
                    reportText.text = message;
                }

                return message;
            }

            return CompareResults(onSession, offSession);
        }

        /// <summary>
        /// Measures PreWarm(count) duration and compares it with a burst of count objects.
        /// </summary>
        public void RunPreWarmImpactTest()
        {
            StartCoroutine(PreWarmImpactRoutine());
        }

        public void ConfigurePreWarm(string poolKey, int count)
        {
            PreWarmPoolKey = poolKey;
            PreWarmCount = count;
        }

        private IEnumerator PreWarmImpactRoutine()
        {
            if (experimentRunner == null)
            {
                UnityEngine.Debug.LogWarning("[ExperimentAnalytics] Cannot run prewarm test: ExperimentRunner missing.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(preWarmPoolKey))
            {
                UnityEngine.Debug.LogWarning("[ExperimentAnalytics] Cannot run prewarm test: preWarmPoolKey is empty.");
                yield break;
            }

            if (!PoolManager.ContainsPool(preWarmPoolKey))
            {
                UnityEngine.Debug.LogWarning(
                    $"[ExperimentAnalytics] Cannot run prewarm test: pool '{preWarmPoolKey}' is not registered.");
                yield break;
            }

            var originalUsePooling = experimentRunner.UsePooling;
            var count = Mathf.Max(1, preWarmCount);

            var stopwatch = Stopwatch.StartNew();
            PoolManager.PreWarm(preWarmPoolKey, count);
            stopwatch.Stop();
            var preWarmMs = stopwatch.Elapsed.TotalMilliseconds;

            var burstElapsedMs = -1.0;
            void HandleBurst(int burstCount, double elapsedMs)
            {
                if (burstCount == count)
                {
                    burstElapsedMs = elapsedMs;
                }
            }

            experimentRunner.BurstCompleted += HandleBurst;
            experimentRunner.UsePooling = false;
            experimentRunner.TriggerScenario3BurstWithCount(count);

            var waitTimer = 0f;
            while (burstElapsedMs < 0.0 && waitTimer < 5f)
            {
                waitTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            experimentRunner.BurstCompleted -= HandleBurst;
            experimentRunner.UsePooling = originalUsePooling;

            if (burstElapsedMs < 0.0)
            {
                UnityEngine.Debug.LogWarning("[ExperimentAnalytics] Burst timing was not captured for prewarm test.");
                yield break;
            }

            var summary =
                $"Prewarm Test ({count} objects): PreWarm({count}) took {preWarmMs:0.00} ms; " +
                $"Burst spawn in gameplay took {burstElapsedMs:0.00} ms. " +
                "Trade-off: upfront load time for smoother runtime bursts.";

            UnityEngine.Debug.Log($"[ExperimentAnalytics] {summary}");
            if (reportText != null)
            {
                reportText.text = summary;
            }
        }

        private void OnScenarioStarted(string scenarioName)
        {
            BeginRecording(scenarioName);
        }

        private void OnScenarioStopped(string scenarioName)
        {
            if (!isRecording)
            {
                return;
            }

            // Only stop if this is the same scenario currently being tracked.
            if (!string.Equals(scenarioName, currentSessionName, StringComparison.Ordinal))
            {
                return;
            }

            EndRecording();
        }

        private void RecordFrameSample()
        {
            var deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            var fps = 1f / deltaTime;
            var frameTimeMs = deltaTime * 1000f;

            sampledFrames++;
            sumFps += fps;
            sumFrameTimeMs += frameTimeMs;
            minFps = Mathf.Min(minFps, fps);

            var memory = GC.GetTotalMemory(false);
            sumMemoryBytes += memory;
            if (memory > peakMemoryBytes)
            {
                peakMemoryBytes = memory;
            }

            objectSampleFrameCounter++;
            if (objectSampleFrameCounter >= objectCountSampleEveryNFrames)
            {
                objectSampleFrameCounter = 0;
                var count = CountAllPoolableInstances();
                if (count > peakObjectCount)
                {
                    peakObjectCount = count;
                }
            }
        }

        private void UpdateMemorySawtoothLabel()
        {
            if (memorySawtoothText == null)
            {
                return;
            }

            if (Time.unscaledTime < nextMemoryLabelUpdateTime)
            {
                return;
            }

            nextMemoryLabelUpdateTime = Time.unscaledTime + 0.2f;
            var bytes = GC.GetTotalMemory(false);
            memorySawtoothText.SetText("Managed Mem: {0:0.00} MB", (float)BytesToMb(bytes));
        }

        private static int CountAllPoolableInstances()
        {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<BasePoolable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
#else
            return FindObjectsOfType<BasePoolable>(true).Length;
#endif
        }

        private static float SafePercentDelta(float newer, float baseline)
        {
            if (Mathf.Abs(baseline) < 0.0001f)
            {
                return 0f;
            }

            return ((newer - baseline) / baseline) * 100f;
        }

        private static double BytesToMb(long bytes)
        {
            return bytes / (1024.0 * 1024.0);
        }

        public static FrameStats ComputeFrameStats(IReadOnlyList<float> fpsSamples, IReadOnlyList<float> frameTimeSamplesMs)
        {
            var fpsCount = fpsSamples != null ? fpsSamples.Count : 0;
            var ftCount = frameTimeSamplesMs != null ? frameTimeSamplesMs.Count : 0;
            if (fpsCount == 0 || ftCount == 0)
            {
                return new FrameStats(0f, 0f, 0f, 0f, 0f, 0f, 0f);
            }

            var sumFps = 0f;
            var minFps = float.MaxValue;
            var maxFps = 0f;
            for (var i = 0; i < fpsCount; i++)
            {
                var fps = fpsSamples[i];
                sumFps += fps;
                if (fps < minFps)
                {
                    minFps = fps;
                }

                if (fps > maxFps)
                {
                    maxFps = fps;
                }
            }

            var avgFps = sumFps / fpsCount;
            var variance = 0f;
            for (var i = 0; i < fpsCount; i++)
            {
                var diff = fpsSamples[i] - avgFps;
                variance += diff * diff;
            }

            variance /= fpsCount;
            var stdDev = Mathf.Sqrt(variance);

            var sorted = new List<float>(fpsSamples);
            sorted.Sort();
            var bottomCount = Mathf.Max(1, Mathf.CeilToInt(sorted.Count * 0.01f));
            var bottomSum = 0f;
            for (var i = 0; i < bottomCount; i++)
            {
                bottomSum += sorted[i];
            }

            var onePercentLow = bottomSum / bottomCount;

            var sumFrameMs = 0f;
            var peakFrameMs = 0f;
            for (var i = 0; i < ftCount; i++)
            {
                var frameMs = frameTimeSamplesMs[i];
                sumFrameMs += frameMs;
                if (frameMs > peakFrameMs)
                {
                    peakFrameMs = frameMs;
                }
            }

            var avgFrameMs = sumFrameMs / ftCount;
            return new FrameStats(avgFps, minFps, maxFps, stdDev, onePercentLow, avgFrameMs, peakFrameMs);
        }

        private const string Scenario1Key = "Scenario1";
        private const string Scenario2Key = "Scenario2";
        private const string Scenario3Key = "Scenario3";
        private const string UnknownScenario = "Unknown";

        private static string NormalizeScenarioKey(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return UnknownScenario;
            }

            var compact = raw.Replace(" ", string.Empty).Replace("-", string.Empty);
            if (compact.IndexOf("scenario1", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Scenario1Key;
            }

            if (compact.IndexOf("scenario2", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Scenario2Key;
            }

            if (compact.IndexOf("scenario3", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Scenario3Key;
            }

            return UnknownScenario;
        }
    }
}
