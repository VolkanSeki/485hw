using System;
using System.Collections;
using System.Linq;
using ModularExperiment.ObjectPooling;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ModularExperiment.Experiment
{
    /// <summary>
    /// Overlay UI for experiment control and live performance metrics.
    /// </summary>
    public class ExperimentUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private ExperimentRunner experimentRunner;

        [SerializeField]
        private ExperimentAnalytics analytics;

        [SerializeField]
        private BatchExperimentRunner batchRunner;

        [SerializeField]
        private VideoDemoRunner videoDemoRunner;

        [Header("Optional: Track one pool key only")]
        [SerializeField]
        private bool showSpecificPoolOnly;

        [SerializeField]
        private string trackedPoolKey = "Costly";

        [Header("Visuals")]
        [SerializeField]
        private Color poolingOnColor = new Color(0.2f, 0.85f, 0.3f);

        [SerializeField]
        private Color poolingOffColor = new Color(0.9f, 0.2f, 0.2f);

        [SerializeField]
        [Min(0.1f)]
        private float burstFlashDuration = 0.8f;

        [Header("Runtime Auto Layout")]
        [SerializeField]
        private bool autoCreateLayoutIfMissing = true;

        [SerializeField]
        private TMP_Text fpsText;

        [SerializeField]
        private TMP_Text poolingStatsText;

        [SerializeField]
        private TMP_Text modeText;

        [SerializeField]
        private TMP_Text scenarioStatusText;

        [SerializeField]
        private TMP_Text memorySawtoothText;

        [SerializeField]
        private TMP_Text burstNoticeText;

        [SerializeField]
        private Toggle poolingToggle;

        [SerializeField]
        private Button scenario1Button;

        [SerializeField]
        private Button scenario2Button;

        [SerializeField]
        private Button scenario3Button;

        [SerializeField]
        private Button preWarmTestButton;

        [SerializeField]
        private Button compareBurstsButton;

        [SerializeField]
        private TMP_Dropdown objectTypeDropdown;

        [SerializeField]
        private TMP_Dropdown compareScenarioDropdown;

        [SerializeField]
        private Button burstCount10Button;

        [SerializeField]
        private Button burstCount100Button;

        [SerializeField]
        private Button burstCount1000Button;

        [SerializeField]
        private Button burstCount10000Button;

        [SerializeField]
        private TMP_Text burstCountValueText;

        [SerializeField]
        private Button spawnFreq005Button;

        [SerializeField]
        private Button spawnFreq01Button;

        [SerializeField]
        private Button spawnFreq05Button;

        [SerializeField]
        private Button spawnFreq10Button;

        [SerializeField]
        private TMP_Text spawnFrequencyValueText;

        [SerializeField]
        private Button preWarm100Button;

        [SerializeField]
        private Button preWarm1000Button;

        [SerializeField]
        private Button preWarm5000Button;

        [SerializeField]
        private Button preWarm10000Button;

        [SerializeField]
        private TMP_Text preWarmCountValueText;

        [SerializeField]
        private Button applyPreWarmButton;

        [SerializeField]
        private Button startFullBatchButton;

        [SerializeField]
        private Button start27sDemoButton;

        [SerializeField]
        private Button batchStartButton;

        [SerializeField]
        private TMP_Text batchProgressText;

        [SerializeField]
        private Slider batchProgressBar;

        [SerializeField]
        private GameObject resultsPopupPanel;

        [SerializeField]
        private TMP_Text resultsSummaryText;

        [SerializeField]
        private TMP_Text lastComparedScenarioText;

        [SerializeField]
        private TMP_Text resultsOnText;

        [SerializeField]
        private TMP_Text resultsOffText;

        [SerializeField]
        private Button resultsCloseButton;

        private const int FpsSampleCount = 60;
        private readonly float[] fpsSamples = new float[FpsSampleCount];
        private int fpsSampleIndex;
        private int fpsFilledCount;
        private Coroutine burstFlashRoutine;
        private long lastManagedMemoryBytes;
        private const long MemorySpikeThresholdBytes = 512 * 1024;
        private readonly Color memoryNormalColor = new Color(0.75f, 0.9f, 1f, 1f);
        private readonly Color memorySpikeColor = new Color(1f, 0.55f, 0.25f, 1f);
        private string selectedComparisonScenarioKey = "Scenario3";
        private bool batchButtonListenerBound;
        private int selectedPreWarmCount = 1000;

        private void Awake()
        {
            if (experimentRunner == null)
            {
                experimentRunner = FindObjectOfType<ExperimentRunner>();
            }

            if (analytics == null)
            {
                analytics = FindObjectOfType<ExperimentAnalytics>();
            }

            if (batchRunner == null)
            {
                batchRunner = FindObjectOfType<BatchExperimentRunner>();
            }

            if (videoDemoRunner == null)
            {
                videoDemoRunner = FindObjectOfType<VideoDemoRunner>();
            }

            if (batchStartButton == null)
            {
                batchStartButton = startFullBatchButton;
            }

            if (autoCreateLayoutIfMissing)
            {
                CreateLayoutIfNeeded();
            }

            WireControls();
            PushRunnerValuesToControls();
            RefreshScenarioButtonLabels();
        }

        private void OnEnable()
        {
            if (experimentRunner != null)
            {
                experimentRunner.BurstCompleted += OnBurstCompleted;
                experimentRunner.ScenarioStateChanged += OnScenarioStateChanged;
            }

            if (batchRunner != null)
            {
                batchRunner.ProgressUpdated += OnBatchProgressUpdated;
                batchRunner.BatchCompleted += OnBatchCompleted;
            }
        }

        private void OnDisable()
        {
            if (experimentRunner != null)
            {
                experimentRunner.BurstCompleted -= OnBurstCompleted;
                experimentRunner.ScenarioStateChanged -= OnScenarioStateChanged;
            }

            if (batchRunner != null)
            {
                batchRunner.ProgressUpdated -= OnBatchProgressUpdated;
                batchRunner.BatchCompleted -= OnBatchCompleted;
            }
        }

        private void Update()
        {
            UpdateFps();
            UpdateMode();
            UpdateScenarioStatus();
            UpdateScenarioButtonInteractivity();
            UpdateManagedMemoryLabel();
            UpdatePoolStats();
            PullRunnerValuesToControlLabels();
        }

        private void WireControls()
        {
            if (batchRunner == null)
            {
                batchRunner = FindObjectOfType<BatchExperimentRunner>();
            }

            if (batchStartButton == null)
            {
                batchStartButton = startFullBatchButton;
            }

            if (poolingToggle != null)
            {
                poolingToggle.onValueChanged.RemoveListener(OnPoolingToggleChanged);
                poolingToggle.onValueChanged.AddListener(OnPoolingToggleChanged);
            }

            if (scenario1Button != null)
            {
                scenario1Button.onClick.RemoveListener(OnScenario1Clicked);
                scenario1Button.onClick.AddListener(OnScenario1Clicked);
            }

            if (scenario2Button != null)
            {
                scenario2Button.onClick.RemoveListener(OnScenario2Clicked);
                scenario2Button.onClick.AddListener(OnScenario2Clicked);
            }

            if (scenario3Button != null)
            {
                scenario3Button.onClick.RemoveListener(OnScenario3Clicked);
                scenario3Button.onClick.AddListener(OnScenario3Clicked);
            }

            if (preWarmTestButton != null)
            {
                preWarmTestButton.onClick.RemoveListener(OnPreWarmTestClicked);
                preWarmTestButton.onClick.AddListener(OnPreWarmTestClicked);
            }

            if (compareBurstsButton != null)
            {
                compareBurstsButton.onClick.RemoveListener(OnCompareBurstsClicked);
                compareBurstsButton.onClick.AddListener(OnCompareBurstsClicked);
            }

            if (objectTypeDropdown != null)
            {
                objectTypeDropdown.ClearOptions();
                objectTypeDropdown.AddOptions(new System.Collections.Generic.List<string> { "SimpleCube", "CostlySphere" });
                objectTypeDropdown.onValueChanged.RemoveListener(OnObjectTypeChanged);
                objectTypeDropdown.onValueChanged.AddListener(OnObjectTypeChanged);
            }

            if (compareScenarioDropdown != null)
            {
                compareScenarioDropdown.ClearOptions();
                compareScenarioDropdown.AddOptions(
                    new System.Collections.Generic.List<string> { "Scenario1", "Scenario2", "Scenario3" });
                compareScenarioDropdown.onValueChanged.RemoveListener(OnCompareScenarioChanged);
                compareScenarioDropdown.onValueChanged.AddListener(OnCompareScenarioChanged);
            }

            BindPresetButton(burstCount10Button, () => SetBurstCountPreset(10));
            BindPresetButton(burstCount100Button, () => SetBurstCountPreset(100));
            BindPresetButton(burstCount1000Button, () => SetBurstCountPreset(1000));
            BindPresetButton(burstCount10000Button, () => SetBurstCountPreset(10000));

            BindPresetButton(spawnFreq005Button, () => SetSpawnFrequencyPreset(0.05f));
            BindPresetButton(spawnFreq01Button, () => SetSpawnFrequencyPreset(0.1f));
            BindPresetButton(spawnFreq05Button, () => SetSpawnFrequencyPreset(0.5f));
            BindPresetButton(spawnFreq10Button, () => SetSpawnFrequencyPreset(1.0f));

            BindPresetButton(preWarm100Button, () => SetPreWarmPreset(100));
            BindPresetButton(preWarm1000Button, () => SetPreWarmPreset(1000));
            BindPresetButton(preWarm5000Button, () => SetPreWarmPreset(5000));
            BindPresetButton(preWarm10000Button, () => SetPreWarmPreset(10000));

            if (applyPreWarmButton != null)
            {
                applyPreWarmButton.onClick.RemoveListener(OnApplyPreWarmClicked);
                applyPreWarmButton.onClick.AddListener(OnApplyPreWarmClicked);
            }

            if (startFullBatchButton != null)
            {
                startFullBatchButton.onClick.RemoveListener(OnStartFullBatchClicked);
                if (batchStartButton == null || !ReferenceEquals(startFullBatchButton, batchStartButton))
                {
                    startFullBatchButton.onClick.AddListener(OnStartFullBatchClicked);
                }
            }

            if (start27sDemoButton != null)
            {
                start27sDemoButton.onClick.RemoveListener(OnStart27sDemoClicked);
                start27sDemoButton.onClick.AddListener(OnStart27sDemoClicked);
            }

            if (batchStartButton != null && batchRunner != null && !batchButtonListenerBound)
            {
                batchStartButton.onClick.RemoveListener(batchRunner.StartFullBatchTest);
                batchStartButton.onClick.AddListener(batchRunner.StartFullBatchTest);
                batchButtonListenerBound = true;
            }

            if (resultsCloseButton != null)
            {
                resultsCloseButton.onClick.RemoveListener(HideResultsPopup);
                resultsCloseButton.onClick.AddListener(HideResultsPopup);
            }
        }

        private void PushRunnerValuesToControls()
        {
            if (experimentRunner == null)
            {
                return;
            }

            if (poolingToggle != null)
            {
                poolingToggle.isOn = experimentRunner.UsePooling;
            }

            SetBurstCountPreset(experimentRunner.BurstCount);

            if (objectTypeDropdown != null)
            {
                objectTypeDropdown.SetValueWithoutNotify(experimentRunner.SelectedObjectTypeIndex);
            }

            if (compareScenarioDropdown != null)
            {
                compareScenarioDropdown.SetValueWithoutNotify(GetScenarioDropdownIndex(selectedComparisonScenarioKey));
            }

            SetSpawnFrequencyPreset(experimentRunner.SpawnFrequency);

            selectedPreWarmCount = analytics != null ? analytics.PreWarmCount : 1000;

            if (analytics != null && experimentRunner != null)
            {
                var preWarm = selectedPreWarmCount;
                analytics.ConfigurePreWarm(experimentRunner.GetSelectedPoolKey(), preWarm);
            }

            if (batchProgressBar != null)
            {
                batchProgressBar.minValue = 0f;
                batchProgressBar.maxValue = 1f;
                batchProgressBar.SetValueWithoutNotify(0f);
            }

            if (batchProgressText != null)
            {
                batchProgressText.text = "Batch: Idle";
            }
        }

        private void PullRunnerValuesToControlLabels()
        {
            if (preWarmCountValueText != null)
            {
                preWarmCountValueText.text = $"Pre-warm Count: {selectedPreWarmCount}";
            }

            if (experimentRunner == null)
            {
                return;
            }

            if (burstCountValueText != null)
            {
                burstCountValueText.text = $"Burst Count: {experimentRunner.BurstCount}";
            }

            if (spawnFrequencyValueText != null)
            {
                spawnFrequencyValueText.text = $"Spawn Freq: {experimentRunner.SpawnFrequency:0.00}s";
            }
        }

        private void RefreshScenarioButtonLabels()
        {
            SetButtonLabel(scenario1Button, "Scenario 1: Periodic");
            SetButtonLabel(scenario2Button, "Scenario 2: Multiple Streams");
            SetButtonLabel(scenario3Button, "Scenario 3: Burst");
        }

        private static void SetButtonLabel(Button button, string textValue)
        {
            if (button == null)
            {
                return;
            }

            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = textValue;
            }
        }

        private static void BindPresetButton(Button button, UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private void UpdateFps()
        {
            var dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            var fps = 1f / dt;

            fpsSamples[fpsSampleIndex] = fps;
            fpsSampleIndex = (fpsSampleIndex + 1) % FpsSampleCount;
            fpsFilledCount = Mathf.Min(fpsFilledCount + 1, FpsSampleCount);

            var sum = 0f;
            for (var i = 0; i < fpsFilledCount; i++)
            {
                sum += fpsSamples[i];
            }

            var averageFps = fpsFilledCount > 0 ? sum / fpsFilledCount : 0f;
            if (fpsText != null)
            {
                fpsText.text = $"FPS (avg {FpsSampleCount}): {averageFps:0.0}";
            }
        }

        private void UpdateMode()
        {
            if (experimentRunner == null)
            {
                if (modeText != null)
                {
                    modeText.text = "Mode: Runner Missing";
                    modeText.color = poolingOffColor;
                }

                return;
            }

            if (poolingToggle != null && poolingToggle.isOn != experimentRunner.UsePooling)
            {
                poolingToggle.SetIsOnWithoutNotify(experimentRunner.UsePooling);
            }

            if (modeText != null)
            {
                modeText.text = experimentRunner.UsePooling ? "Mode: Pooling ON" : "Mode: Pooling OFF";
                modeText.color = experimentRunner.UsePooling ? poolingOnColor : poolingOffColor;
            }
        }

        private void UpdateScenarioStatus()
        {
            if (scenarioStatusText == null)
            {
                return;
            }

            if (experimentRunner == null)
            {
                scenarioStatusText.text = "SYSTEM: IDLE";
                scenarioStatusText.color = poolingOffColor;
                return;
            }

            var scenarioText = experimentRunner.ActiveScenario == ExperimentRunner.ScenarioState.Idle
                ? "SYSTEM: IDLE"
                : $"RUNNING: {experimentRunner.ActiveScenario}";
            var modeTextValue = experimentRunner.UsePooling ? "ON" : "OFF";

            scenarioStatusText.text = $"CURRENT MODE: {modeTextValue}\n{scenarioText}";
            scenarioStatusText.color = experimentRunner.UsePooling ? poolingOnColor : poolingOffColor;
        }

        private void UpdateScenarioButtonInteractivity()
        {
            if (experimentRunner == null)
            {
                return;
            }

            var active = experimentRunner.ActiveScenario;
            var idle = active == ExperimentRunner.ScenarioState.Idle;

            if (scenario1Button != null)
            {
                scenario1Button.interactable = idle || active == ExperimentRunner.ScenarioState.Scenario1;
            }

            if (scenario2Button != null)
            {
                scenario2Button.interactable = idle || active == ExperimentRunner.ScenarioState.Scenario2;
            }

            if (scenario3Button != null)
            {
                // Scenario 3 is instant burst; allow trigger only when system is idle.
                scenario3Button.interactable = idle;
            }

            if (objectTypeDropdown != null)
            {
                objectTypeDropdown.interactable = idle;
            }

            SetButtonInteractable(burstCount10Button, idle);
            SetButtonInteractable(burstCount100Button, idle);
            SetButtonInteractable(burstCount1000Button, idle);
            SetButtonInteractable(burstCount10000Button, idle);
            SetButtonInteractable(spawnFreq005Button, idle);
            SetButtonInteractable(spawnFreq01Button, idle);
            SetButtonInteractable(spawnFreq05Button, idle);
            SetButtonInteractable(spawnFreq10Button, idle);
            SetButtonInteractable(preWarm100Button, idle);
            SetButtonInteractable(preWarm1000Button, idle);
            SetButtonInteractable(preWarm5000Button, idle);
            SetButtonInteractable(preWarm10000Button, idle);
        }

        private void OnScenarioStateChanged(ExperimentRunner.ScenarioState _)
        {
            UpdateScenarioStatus();
            UpdateScenarioButtonInteractivity();
        }

        private void UpdateManagedMemoryLabel()
        {
            if (memorySawtoothText == null)
            {
                return;
            }

            var managed = GC.GetTotalMemory(false);
            memorySawtoothText.text = $"Managed Mem: {managed / (1024f * 1024f):0.00} MB";

            var delta = managed - lastManagedMemoryBytes;
            memorySawtoothText.color = delta > MemorySpikeThresholdBytes ? memorySpikeColor : memoryNormalColor;
            lastManagedMemoryBytes = managed;
        }

        private void UpdatePoolStats()
        {
            if (poolingStatsText == null)
            {
                return;
            }

            if (showSpecificPoolOnly)
            {
                if (string.IsNullOrWhiteSpace(trackedPoolKey))
                {
                    poolingStatsText.text = "Pool Stats: Missing key";
                    return;
                }

                if (!PoolManager.TryGetStats(trackedPoolKey, out var onePoolStats))
                {
                    poolingStatsText.text = $"Pool '{trackedPoolKey}': Not registered";
                    return;
                }

                poolingStatsText.text =
                    $"Pool '{trackedPoolKey}' | Alloc: {onePoolStats.TotalAllocations}  Reuse: {onePoolStats.TotalReuses}  Inactive: {onePoolStats.InactiveCount}";
                return;
            }

            var total = PoolManager.GetAggregateStats();
            poolingStatsText.text =
                $"Pools: {total.PoolCount} | Alloc: {total.TotalAllocations}  Reuse: {total.TotalReuses}  Inactive: {total.InactiveCount}";
        }

        private void OnPoolingToggleChanged(bool isOn)
        {
            if (experimentRunner == null)
            {
                return;
            }

            experimentRunner.UsePooling = isOn;
        }

        private void OnScenario1Clicked()
        {
            experimentRunner?.StartScenario1();
        }

        private void OnScenario2Clicked()
        {
            experimentRunner?.StartScenario2();
        }

        private void OnScenario3Clicked()
        {
            experimentRunner?.TriggerScenario3Burst();
        }

        private void OnPreWarmTestClicked()
        {
            analytics?.RunPreWarmImpactTest();
        }

        private void OnCompareBurstsClicked()
        {
            if (analytics == null)
            {
                return;
            }

            var summary = analytics.CompareLatestForScenario(selectedComparisonScenarioKey);
            ShowComparisonPopup(selectedComparisonScenarioKey, summary);
        }

        private void OnCompareScenarioChanged(int selectedIndex)
        {
            selectedComparisonScenarioKey = GetScenarioKeyFromIndex(selectedIndex);
        }

        private void OnObjectTypeChanged(int selectedIndex)
        {
            if (experimentRunner == null)
            {
                return;
            }

            experimentRunner.SelectedObjectTypeIndex = selectedIndex;

            if (analytics != null)
            {
                analytics.PreWarmPoolKey = experimentRunner.GetSelectedPoolKey();
            }
        }

        private void SetBurstCountPreset(int burstCount)
        {
            if (experimentRunner != null)
            {
                experimentRunner.BurstCount = Mathf.Max(1, burstCount);
            }
        }

        private void SetSpawnFrequencyPreset(float frequency)
        {
            if (experimentRunner != null)
            {
                experimentRunner.SpawnFrequency = Mathf.Max(0.01f, frequency);
            }
        }

        private void SetPreWarmPreset(int preWarmCount)
        {
            selectedPreWarmCount = Mathf.Max(1, preWarmCount);
            if (analytics != null)
            {
                analytics.PreWarmCount = selectedPreWarmCount;
            }
        }

        private void OnApplyPreWarmClicked()
        {
            if (experimentRunner == null)
            {
                return;
            }

            var key = experimentRunner.GetSelectedPoolKey();
            var count = selectedPreWarmCount;
            PoolManager.PreWarm(key, count);

            if (analytics != null)
            {
                analytics.ConfigurePreWarm(key, count);
            }
        }

        private void OnStartFullBatchClicked()
        {
            if (batchRunner == null)
            {
                batchRunner = FindObjectOfType<BatchExperimentRunner>();
            }

            if (batchRunner == null)
            {
                Debug.LogError("[ExperimentUI] Missing BatchExperimentRunner for Start Full Batch button.");
                return;
            }

            batchRunner.StartFullBatchTest();
        }

        private void OnStart27sDemoClicked()
        {
            if (videoDemoRunner == null)
            {
                videoDemoRunner = FindObjectOfType<VideoDemoRunner>();
            }

            if (videoDemoRunner == null)
            {
                Debug.LogError("[ExperimentUI] Missing VideoDemoRunner for START 27s DEMO button.");
                return;
            }

            videoDemoRunner.Start27sDemo();
        }

        private void OnBatchProgressUpdated(string label, float progress)
        {
            if (batchProgressText != null)
            {
                batchProgressText.text = label;
            }

            if (batchProgressBar != null)
            {
                batchProgressBar.SetValueWithoutNotify(progress);
            }
        }

        private void OnBatchCompleted(string csv)
        {
            if (batchProgressText != null)
            {
                batchProgressText.text = "Batch complete. CSV printed to Console.";
            }

            if (batchProgressBar != null)
            {
                batchProgressBar.SetValueWithoutNotify(1f);
            }
        }

        private void OnBurstCompleted(int count, double elapsedMs)
        {
            if (burstNoticeText == null)
            {
                return;
            }

            if (burstFlashRoutine != null)
            {
                StopCoroutine(burstFlashRoutine);
            }

            burstFlashRoutine = StartCoroutine(BurstNoticeRoutine(count, elapsedMs));
        }

        private IEnumerator BurstNoticeRoutine(int count, double elapsedMs)
        {
            burstNoticeText.gameObject.SetActive(true);
            burstNoticeText.text = $"Burst {count} completed in {elapsedMs:F2} ms";
            burstNoticeText.color = new Color(1f, 0.85f, 0.2f, 1f);

            var timer = 0f;
            while (timer < burstFlashDuration)
            {
                timer += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(timer / burstFlashDuration);
                var alpha = Mathf.Lerp(1f, 0f, t);
                var c = burstNoticeText.color;
                burstNoticeText.color = new Color(c.r, c.g, c.b, alpha);
                yield return null;
            }

            burstNoticeText.gameObject.SetActive(false);
            burstFlashRoutine = null;
        }

        private void ShowComparisonPopup(string scenarioKeyword, string summary)
        {
            if (resultsPopupPanel == null)
            {
                return;
            }

            if (resultsSummaryText != null)
            {
                resultsSummaryText.text = summary;
            }

            if (lastComparedScenarioText != null)
            {
                lastComparedScenarioText.text = $"Last Compared Scenario: {scenarioKeyword}";
            }

            if (analytics != null &&
                TryGetLatestScenarioPair(scenarioKeyword, out var onSession, out var offSession))
            {
                if (resultsOnText != null)
                {
                    resultsOnText.text = BuildSessionCard("Pooling ON", onSession);
                }

                if (resultsOffText != null)
                {
                    resultsOffText.text = BuildSessionCard("Pooling OFF", offSession);
                }
            }
            else
            {
                if (resultsOnText != null)
                {
                    resultsOnText.text = "Pooling ON\nNo session data yet.";
                }

                if (resultsOffText != null)
                {
                    resultsOffText.text = "Pooling OFF\nNo session data yet.";
                }
            }

            resultsPopupPanel.SetActive(true);
        }

        private void HideResultsPopup()
        {
            if (resultsPopupPanel != null)
            {
                resultsPopupPanel.SetActive(false);
            }
        }

        private bool TryGetLatestScenarioPair(
            string scenarioKeyword,
            out ExperimentAnalytics.SessionResult onSession,
            out ExperimentAnalytics.SessionResult offSession)
        {
            onSession = default;
            offSession = default;

            if (analytics == null)
            {
                return false;
            }

            var all = analytics.CompletedSessions;
            if (all == null || all.Count == 0)
            {
                return false;
            }

            var normalized = scenarioKeyword ?? string.Empty;
            onSession = all.LastOrDefault(
                s => s.PoolingEnabled && string.Equals(s.ScenarioKey, normalized, StringComparison.Ordinal));
            offSession = all.LastOrDefault(
                s => !s.PoolingEnabled && string.Equals(s.ScenarioKey, normalized, StringComparison.Ordinal));

            return !string.IsNullOrEmpty(onSession.SessionName) && !string.IsNullOrEmpty(offSession.SessionName);
        }

        private static string BuildSessionCard(string title, ExperimentAnalytics.SessionResult s)
        {
            return
                $"{title}\n" +
                $"Session: {s.SessionName}\n" +
                $"FPS Avg/Min: {s.AverageFps:0.0} / {s.MinFps:0.0}\n" +
                $"Frame Time Avg/Peak: {s.AverageFrameTimeMs:0.00}ms / {s.PeakFrameTimeMs:0.00}ms\n" +
                $"Memory Avg/Peak: {ToMb(s.AverageMemoryBytes):0.00}MB / {ToMb(s.PeakMemoryBytes):0.00}MB";
        }

        private static double ToMb(long bytes)
        {
            return bytes / (1024.0 * 1024.0);
        }

        private void CreateLayoutIfNeeded()
        {
            if (fpsText != null &&
                poolingStatsText != null &&
                modeText != null &&
                scenarioStatusText != null &&
                memorySawtoothText != null &&
                burstNoticeText != null &&
                poolingToggle != null &&
                scenario1Button != null &&
                scenario2Button != null &&
                scenario3Button != null &&
                objectTypeDropdown != null &&
                compareScenarioDropdown != null &&
                preWarmTestButton != null &&
                compareBurstsButton != null &&
                burstCount10Button != null &&
                spawnFreq005Button != null &&
                preWarm100Button != null &&
                applyPreWarmButton != null &&
                startFullBatchButton != null &&
                start27sDemoButton != null &&
                batchProgressText != null &&
                batchProgressBar != null &&
                resultsPopupPanel != null)
            {
                return;
            }

            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("ExperimentCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            EnsureEventSystem();

            var metricsPanel = CreatePanel("MetricsPanel", canvas.transform as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(520f, 210f));
            var controlsPanel = CreatePanel("ControlsPanel", canvas.transform as RectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(320f, 1120f));
            var noticePanel = CreatePanel("BurstNoticePanel", canvas.transform as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(500f, 40f));
            var popupPanel = CreatePanel("ResultsPopupPanel", canvas.transform as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(860f, 420f));

            fpsText = CreateTmpText("FPSText", metricsPanel, "FPS: --", 24, TextAlignmentOptions.TopLeft, new Vector2(10f, -10f));
            poolingStatsText = CreateTmpText("PoolStatsText", metricsPanel, "Pools: --", 20, TextAlignmentOptions.TopLeft, new Vector2(10f, -45f));
            modeText = CreateTmpText("ModeText", metricsPanel, "Mode: --", 22, TextAlignmentOptions.TopLeft, new Vector2(10f, -80f));
            memorySawtoothText = CreateTmpText("MemoryText", metricsPanel, "Managed Mem: --", 20, TextAlignmentOptions.TopLeft, new Vector2(10f, -115f));
            scenarioStatusText = CreateTmpText("ScenarioStatusText", metricsPanel, "CURRENT MODE: OFF\nSYSTEM: IDLE", 22, TextAlignmentOptions.TopLeft, new Vector2(10f, -150f));

            burstNoticeText = CreateTmpText("BurstNoticeText", noticePanel, string.Empty, 24, TextAlignmentOptions.Center, Vector2.zero);
            burstNoticeText.gameObject.SetActive(false);

            CreateTmpText("ControlsTitle", controlsPanel, "Experiment Controls", 24, TextAlignmentOptions.Center, new Vector2(0f, -20f));

            poolingToggle = CreateToggle("PoolingToggle", controlsPanel, "Use Pooling", new Vector2(0f, -60f));
            objectTypeDropdown = CreateDropdown("ObjectTypeDropdown", controlsPanel, new Vector2(0f, -108f));
            CreateTmpText("ObjectTypeLabel", controlsPanel, "Object Type (SimpleCube / CostlySphere)", 16, TextAlignmentOptions.Center, new Vector2(0f, -138f));

            scenario1Button = CreateButton("Scenario1Button", controlsPanel, "Scenario 1: Periodic", new Vector2(0f, -184f));
            scenario2Button = CreateButton("Scenario2Button", controlsPanel, "Scenario 2: Multiple Streams", new Vector2(0f, -234f));
            scenario3Button = CreateButton("Scenario3Button", controlsPanel, "Scenario 3: Burst", new Vector2(0f, -284f));
            compareScenarioDropdown = CreateDropdown("CompareScenarioDropdown", controlsPanel, new Vector2(0f, -334f));
            CreateTmpText("CompareScenarioLabel", controlsPanel, "Compare Scenario (latest ON vs OFF)", 16, TextAlignmentOptions.Center, new Vector2(0f, -364f));
            preWarmTestButton = CreateButton("PreWarmTestButton", controlsPanel, "Run Pre-warm Test", new Vector2(0f, -414f));
            compareBurstsButton = CreateButton("CompareBurstsButton", controlsPanel, "Compare Last Runs", new Vector2(0f, -464f));

            burstCountValueText = CreateTmpText("BurstSliderLabel", controlsPanel, "Burst Count: --", 18, TextAlignmentOptions.Center, new Vector2(0f, -550f));
            CreateTmpText("BurstPresetTitle", controlsPanel, "Burst Presets", 16, TextAlignmentOptions.Center, new Vector2(0f, -585f));
            var burstPresetGroup = CreateHorizontalButtonGroup("BurstPresetGroup", controlsPanel, new Vector2(0f, -610f), new Vector2(280f, 32f));
            burstCount10Button = CreateLayoutButton("Burst10Button", burstPresetGroup, "10");
            burstCount100Button = CreateLayoutButton("Burst100Button", burstPresetGroup, "100");
            burstCount1000Button = CreateLayoutButton("Burst1000Button", burstPresetGroup, "1000");
            burstCount10000Button = CreateLayoutButton("Burst10000Button", burstPresetGroup, "10000");

            spawnFrequencyValueText = CreateTmpText("SpawnFreqLabel", controlsPanel, "Spawn Freq: --", 18, TextAlignmentOptions.Center, new Vector2(0f, -650f));
            CreateTmpText("SpawnPresetTitle", controlsPanel, "Spawn Frequency Presets", 16, TextAlignmentOptions.Center, new Vector2(0f, -680f));
            var spawnPresetGroup = CreateHorizontalButtonGroup("SpawnPresetGroup", controlsPanel, new Vector2(0f, -705f), new Vector2(280f, 32f));
            spawnFreq005Button = CreateLayoutButton("Spawn005Button", spawnPresetGroup, "0.05s");
            spawnFreq01Button = CreateLayoutButton("Spawn01Button", spawnPresetGroup, "0.1s");
            spawnFreq05Button = CreateLayoutButton("Spawn05Button", spawnPresetGroup, "0.5s");
            spawnFreq10Button = CreateLayoutButton("Spawn10Button", spawnPresetGroup, "1.0s");

            preWarmCountValueText = CreateTmpText("PreWarmLabel", controlsPanel, "Pre-warm Count: --", 18, TextAlignmentOptions.Center, new Vector2(0f, -745f));
            CreateTmpText("PreWarmPresetTitle", controlsPanel, "Pre-warm Presets", 16, TextAlignmentOptions.Center, new Vector2(0f, -775f));
            var preWarmPresetGroup = CreateHorizontalButtonGroup("PreWarmPresetGroup", controlsPanel, new Vector2(0f, -800f), new Vector2(280f, 32f));
            preWarm100Button = CreateLayoutButton("PreWarm100Button", preWarmPresetGroup, "100");
            preWarm1000Button = CreateLayoutButton("PreWarm1000Button", preWarmPresetGroup, "1000");
            preWarm5000Button = CreateLayoutButton("PreWarm5000Button", preWarmPresetGroup, "5000");
            preWarm10000Button = CreateLayoutButton("PreWarm10000Button", preWarmPresetGroup, "10000");
            applyPreWarmButton = CreateButton("ApplyPreWarmButton", controlsPanel, "Apply Pre-warm To Selected", new Vector2(0f, -850f));
            startFullBatchButton = CreateButton("StartFullBatchButton", controlsPanel, "START FULL BATCH TEST", new Vector2(0f, -900f));
            start27sDemoButton = CreateButton("Start27sDemoButton", controlsPanel, "START 27s DEMO", new Vector2(0f, -950f));
            batchProgressText = CreateTmpText("BatchProgressText", controlsPanel, "Batch: Idle", 16, TextAlignmentOptions.Center, new Vector2(0f, -990f));
            batchProgressBar = CreateSlider("BatchProgressBar", controlsPanel, new Vector2(0f, -1028f));

            resultsPopupPanel = popupPanel.gameObject;
            CreateTmpText("PopupTitle", popupPanel, "Results Summary", 30, TextAlignmentOptions.Center, new Vector2(0f, -18f));
            resultsSummaryText = CreateTmpText("PopupSummary", popupPanel, "Comparison output will appear here.", 18, TextAlignmentOptions.TopLeft, new Vector2(20f, -58f));
            lastComparedScenarioText = CreateTmpText("PopupScenario", popupPanel, "Last Compared Scenario: --", 18, TextAlignmentOptions.TopLeft, new Vector2(20f, -118f));
            resultsOnText = CreateTmpText("PopupOnText", popupPanel, "Pooling ON", 20, TextAlignmentOptions.TopLeft, new Vector2(20f, -170f));
            resultsOffText = CreateTmpText("PopupOffText", popupPanel, "Pooling OFF", 20, TextAlignmentOptions.TopLeft, new Vector2(440f, -170f));
            resultsCloseButton = CreateButton("PopupCloseButton", popupPanel, "Close", new Vector2(0f, -372f));

            resultsSummaryText.rectTransform.sizeDelta = new Vector2(-40f, 70f);
            resultsOnText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            resultsOnText.rectTransform.sizeDelta = new Vector2(-30f, 210f);
            resultsOffText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            resultsOffText.rectTransform.anchoredPosition = new Vector2(15f, -170f);
            resultsOffText.rectTransform.sizeDelta = new Vector2(-30f, 210f);
            popupPanel.gameObject.SetActive(false);
        }

        private static string GetScenarioKeyFromIndex(int index)
        {
            switch (index)
            {
                case 0:
                    return "Scenario1";
                case 1:
                    return "Scenario2";
                default:
                    return "Scenario3";
            }
        }

        private static int GetScenarioDropdownIndex(string scenarioKey)
        {
            if (string.Equals(scenarioKey, "Scenario1", StringComparison.Ordinal))
            {
                return 0;
            }

            if (string.Equals(scenarioKey, "Scenario2", StringComparison.Ordinal))
            {
                return 1;
            }

            return 2;
        }

        private static RectTransform CreatePanel(
            string name,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 size)
        {
            var panelGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = panelGo.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var image = panelGo.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.45f);
            return rect;
        }

        private static TMP_Text CreateTmpText(
            string name,
            RectTransform parent,
            string content,
            float fontSize,
            TextAlignmentOptions alignment,
            Vector2 anchoredPosition)
        {
            var textGo = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = textGo.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(-20f, 30f);

            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.text = content;
            text.color = Color.white;
            return text;
        }

        private static Button CreateButton(string name, RectTransform parent, string label, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(260f, 36f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.16f, 0.45f, 0.7f, 0.9f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var labelText = CreateTmpText("Label", rect, label, 18, TextAlignmentOptions.Center, new Vector2(0f, -2f));
            labelText.rectTransform.anchorMin = Vector2.zero;
            labelText.rectTransform.anchorMax = Vector2.one;
            labelText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            labelText.rectTransform.anchoredPosition = Vector2.zero;
            labelText.rectTransform.sizeDelta = Vector2.zero;
            return button;
        }

        private static RectTransform CreateHorizontalButtonGroup(
            string name,
            RectTransform parent,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var groupGo = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var rect = groupGo.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var layout = groupGo.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.spacing = 6f;
            return rect;
        }

        private static Button CreateLayoutButton(string name, RectTransform parent, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(64f, 32f);

            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 64f;
            le.preferredHeight = 32f;
            le.flexibleWidth = 1f;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.16f, 0.45f, 0.7f, 0.9f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var labelText = CreateTmpText("Label", rect, label, 14, TextAlignmentOptions.Center, Vector2.zero);
            labelText.rectTransform.anchorMin = Vector2.zero;
            labelText.rectTransform.anchorMax = Vector2.one;
            labelText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            labelText.rectTransform.anchoredPosition = Vector2.zero;
            labelText.rectTransform.sizeDelta = Vector2.zero;
            return button;
        }

        private static Toggle CreateToggle(string name, RectTransform parent, string label, Vector2 anchoredPosition)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = anchoredPosition;
            rootRect.sizeDelta = new Vector2(260f, 30f);

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRect = background.GetComponent<RectTransform>();
            bgRect.SetParent(rootRect, false);
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(0f, 0.5f);
            bgRect.pivot = new Vector2(0f, 0.5f);
            bgRect.anchoredPosition = new Vector2(4f, 0f);
            bgRect.sizeDelta = new Vector2(22f, 22f);
            var bgImage = background.GetComponent<Image>();
            bgImage.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);

            var checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            var ckRect = checkmark.GetComponent<RectTransform>();
            ckRect.SetParent(bgRect, false);
            ckRect.anchorMin = new Vector2(0.5f, 0.5f);
            ckRect.anchorMax = new Vector2(0.5f, 0.5f);
            ckRect.pivot = new Vector2(0.5f, 0.5f);
            ckRect.anchoredPosition = Vector2.zero;
            ckRect.sizeDelta = new Vector2(12f, 12f);
            var ckImage = checkmark.GetComponent<Image>();
            ckImage.color = new Color(0.15f, 0.7f, 0.2f, 1f);

            var labelText = CreateTmpText("Label", rootRect, label, 18, TextAlignmentOptions.MidlineLeft, new Vector2(36f, -2f));
            labelText.rectTransform.anchorMin = new Vector2(0f, 0f);
            labelText.rectTransform.anchorMax = new Vector2(1f, 1f);
            labelText.rectTransform.pivot = new Vector2(0f, 0.5f);
            labelText.rectTransform.anchoredPosition = new Vector2(36f, 0f);
            labelText.rectTransform.sizeDelta = new Vector2(-36f, 0f);

            var toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = bgImage;
            toggle.graphic = ckImage;
            toggle.isOn = true;
            return toggle;
        }

        private static Slider CreateSlider(string name, RectTransform parent, Vector2 anchoredPosition)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(260f, 20f);

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRect = background.GetComponent<RectTransform>();
            bgRect.SetParent(rect, false);
            bgRect.anchorMin = new Vector2(0f, 0.25f);
            bgRect.anchorMax = new Vector2(1f, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = background.GetComponent<Image>();
            bgImage.color = new Color(1f, 1f, 1f, 0.25f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.SetParent(rect, false);
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(10f, 0f);
            fillAreaRect.offsetMax = new Vector2(-10f, 0f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.SetParent(fillAreaRect, false);
            fillRect.anchorMin = new Vector2(0f, 0.2f);
            fillRect.anchorMax = new Vector2(1f, 0.8f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(0.2f, 0.72f, 1f, 0.9f);

            var handleSlideArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            var handleAreaRect = handleSlideArea.GetComponent<RectTransform>();
            handleAreaRect.SetParent(rect, false);
            handleAreaRect.anchorMin = new Vector2(0f, 0f);
            handleAreaRect.anchorMax = new Vector2(1f, 1f);
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.SetParent(handleAreaRect, false);
            handleRect.sizeDelta = new Vector2(16f, 22f);
            var handleImage = handle.GetComponent<Image>();
            handleImage.color = new Color(1f, 1f, 1f, 0.95f);

            var slider = root.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            return slider;
        }

        private static TMP_Dropdown CreateDropdown(string name, RectTransform parent, Vector2 anchoredPosition)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(260f, 30f);

            var bg = root.GetComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);

            var captionText = CreateTmpText("Caption", rect, "SimpleCube", 16, TextAlignmentOptions.MidlineLeft, new Vector2(10f, -2f));
            captionText.rectTransform.anchorMin = new Vector2(0f, 0f);
            captionText.rectTransform.anchorMax = new Vector2(1f, 1f);
            captionText.rectTransform.pivot = new Vector2(0f, 0.5f);
            captionText.rectTransform.anchoredPosition = new Vector2(10f, 0f);
            captionText.rectTransform.sizeDelta = new Vector2(-40f, 0f);

            var arrow = CreateTmpText("Arrow", rect, "v", 18, TextAlignmentOptions.Center, new Vector2(0f, 0f));
            arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
            arrow.rectTransform.anchorMax = new Vector2(1f, 1f);
            arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
            arrow.rectTransform.anchoredPosition = new Vector2(-10f, 0f);
            arrow.rectTransform.sizeDelta = new Vector2(20f, 0f);

            var template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var templateRect = template.GetComponent<RectTransform>();
            templateRect.SetParent(rect, false);
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -2f);
            templateRect.sizeDelta = new Vector2(0f, 120f);
            template.SetActive(false);
            template.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.SetParent(templateRect, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            var viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.05f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform));
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 60f);

            var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            var itemRect = item.GetComponent<RectTransform>();
            itemRect.SetParent(contentRect, false);
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.anchoredPosition = Vector2.zero;
            itemRect.sizeDelta = new Vector2(0f, 30f);

            var itemBg = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            var itemBgRect = itemBg.GetComponent<RectTransform>();
            itemBgRect.SetParent(itemRect, false);
            itemBgRect.anchorMin = Vector2.zero;
            itemBgRect.anchorMax = Vector2.one;
            itemBgRect.offsetMin = Vector2.zero;
            itemBgRect.offsetMax = Vector2.zero;
            itemBg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

            var itemCheck = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
            var itemCheckRect = itemCheck.GetComponent<RectTransform>();
            itemCheckRect.SetParent(itemRect, false);
            itemCheckRect.anchorMin = new Vector2(0f, 0.5f);
            itemCheckRect.anchorMax = new Vector2(0f, 0.5f);
            itemCheckRect.pivot = new Vector2(0f, 0.5f);
            itemCheckRect.anchoredPosition = new Vector2(8f, 0f);
            itemCheckRect.sizeDelta = new Vector2(12f, 12f);
            itemCheck.GetComponent<Image>().color = new Color(0.2f, 0.8f, 0.35f, 1f);

            var itemLabel = CreateTmpText("Item Label", itemRect, "Option", 15, TextAlignmentOptions.MidlineLeft, new Vector2(30f, -1f));
            itemLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            itemLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            itemLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
            itemLabel.rectTransform.anchoredPosition = new Vector2(30f, 0f);
            itemLabel.rectTransform.sizeDelta = new Vector2(-30f, 0f);

            var itemToggle = item.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBg.GetComponent<Image>();
            itemToggle.graphic = itemCheck.GetComponent<Image>();

            var scrollRect = template.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            var dropdown = root.GetComponent<TMP_Dropdown>();
            dropdown.targetGraphic = bg;
            dropdown.captionText = captionText.GetComponent<TextMeshProUGUI>();
            dropdown.template = templateRect;
            dropdown.itemText = itemLabel.GetComponent<TextMeshProUGUI>();
            return dropdown;
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem != null)
            {
                return;
            }

            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemGo.transform.SetAsLastSibling();
        }
    }
}
