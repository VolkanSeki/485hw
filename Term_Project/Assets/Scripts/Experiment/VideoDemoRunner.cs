using System.Collections;
using TMPro;
using UnityEngine;

namespace ModularExperiment.Experiment
{
    /// <summary>
    /// Runs a fixed 27-second demonstration sequence for video capture.
    /// </summary>
    public class VideoDemoRunner : MonoBehaviour
    {
        [SerializeField]
        private ExperimentRunner experimentRunner;

        [SerializeField]
        private TMP_Text demoStatusText;

        [SerializeField]
        private float phaseDurationSeconds = 4.5f;

        private bool isRunning;

        public bool IsRunning => isRunning;

        private void Awake()
        {
            if (experimentRunner == null)
            {
                experimentRunner = FindObjectOfType<ExperimentRunner>();
            }

            EnsureStatusLabel();
            SetStatus("READY", "OFF");
        }

        public void Start27sDemo()
        {
            if (isRunning || experimentRunner == null)
            {
                return;
            }

            StartCoroutine(Run27SecondSequence());
        }

        private IEnumerator Run27SecondSequence()
        {
            isRunning = true;
            var originalPooling = experimentRunner.UsePooling;
            var originalObjectType = experimentRunner.SelectedObjectTypeIndex;
            var originalFrequency = experimentRunner.SpawnFrequency;
            var originalBurstCount = experimentRunner.BurstCount;

            try
            {
                // Scenario 1: 0s-9s
                yield return RunScenario1Block();

                // Scenario 2: 9s-18s
                yield return RunScenario2Block();

                // Scenario 3: 18s-27s
                yield return RunScenario3Block();
            }
            finally
            {
                experimentRunner.StopAllScenarios();
                experimentRunner.UsePooling = originalPooling;
                experimentRunner.SelectedObjectTypeIndex = originalObjectType;
                experimentRunner.SpawnFrequency = originalFrequency;
                experimentRunner.BurstCount = originalBurstCount;
                SetStatus("COMPLETE", "ON");
                isRunning = false;
            }
        }

        private IEnumerator RunScenario1Block()
        {
            ClearPreviousScenarioObjects();
            experimentRunner.StopAllScenarios();
            experimentRunner.SelectedObjectTypeIndex = 0; // SimpleCube
            experimentRunner.SpawnFrequency = 0.05f;

            SetStatus("SCENARIO: 1", "OFF");
            experimentRunner.UsePooling = false;
            experimentRunner.StartScenario1();
            yield return new WaitForSeconds(phaseDurationSeconds);

            SetStatus("SCENARIO: 1", "ON");
            experimentRunner.UsePooling = true;
            experimentRunner.StartScenario1();
            yield return new WaitForSeconds(phaseDurationSeconds);

            experimentRunner.StopScenario1();
        }

        private IEnumerator RunScenario2Block()
        {
            ClearPreviousScenarioObjects();
            experimentRunner.StopAllScenarios();
            experimentRunner.SelectedObjectTypeIndex = 1; // CostlySphere
            experimentRunner.ConfigureScenario2Chaos(
                streamCount: 5,
                frequency: 0.1f,
                lifetime: 1.0f,
                objectsPerTick: 1,
                randomRadius: 3.0f);

            SetStatus("SCENARIO: 2", "OFF");
            experimentRunner.UsePooling = false;
            experimentRunner.StartScenario2();
            yield return new WaitForSeconds(phaseDurationSeconds);

            SetStatus("SCENARIO: 2", "ON");
            experimentRunner.UsePooling = true;
            experimentRunner.StartScenario2();
            yield return new WaitForSeconds(phaseDurationSeconds);

            experimentRunner.StopScenario2();
        }

        private IEnumerator RunScenario3Block()
        {
            ClearPreviousScenarioObjects();
            experimentRunner.StopAllScenarios();
            experimentRunner.SelectedObjectTypeIndex = 1; // CostlySphere
            experimentRunner.BurstCount = 10000;

            SetStatus("SCENARIO: 3", "OFF");
            experimentRunner.UsePooling = false;
            experimentRunner.TriggerScenario3BurstWithCount(10000);
            yield return new WaitForSeconds(phaseDurationSeconds);

            ClearPreviousScenarioObjects();
            SetStatus("SCENARIO: 3", "ON");
            experimentRunner.UsePooling = true;
            experimentRunner.TriggerScenario3BurstWithCount(10000);
            yield return new WaitForSeconds(phaseDurationSeconds);
        }

        private void SetStatus(string scenarioLabel, string modeLabel)
        {
            if (demoStatusText == null)
            {
                return;
            }

            demoStatusText.text = $"CURRENT MODE: {modeLabel}\n{scenarioLabel}";
            demoStatusText.color = modeLabel == "ON" ? new Color(0.35f, 1f, 0.45f, 1f) : new Color(1f, 0.35f, 0.35f, 1f);
        }

        private void ClearPreviousScenarioObjects()
        {
#if UNITY_2023_1_OR_NEWER
            var all = FindObjectsByType<BasePoolable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var all = FindObjectsOfType<BasePoolable>(true);
#endif
            for (var i = 0; i < all.Length; i++)
            {
                var item = all[i];
                if (item == null)
                {
                    continue;
                }

                var poolKey = item.PoolKey;
                if (!string.IsNullOrWhiteSpace(poolKey) && ModularExperiment.ObjectPooling.PoolManager.ContainsPool(poolKey))
                {
                    item.ReturnToPool();
                }
                else
                {
                    Destroy(item.gameObject);
                }
            }
        }

        private void EnsureStatusLabel()
        {
            if (demoStatusText != null)
            {
                return;
            }

            var existing = GameObject.Find("VideoDemoStatusText");
            if (existing != null)
            {
                demoStatusText = existing.GetComponent<TMP_Text>();
                if (demoStatusText != null)
                {
                    return;
                }
            }

            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var textGo = new GameObject("VideoDemoStatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = textGo.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -70f);
            rect.sizeDelta = new Vector2(900f, 120f);

            demoStatusText = textGo.GetComponent<TextMeshProUGUI>();
            demoStatusText.alignment = TextAlignmentOptions.Center;
            demoStatusText.fontSize = 44f;
            demoStatusText.fontStyle = FontStyles.Bold;
            demoStatusText.color = Color.white;
        }
    }
}
