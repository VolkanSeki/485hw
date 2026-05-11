using ModularExperiment.Experiment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ModularExperiment.Editor
{
    /// <summary>
    /// One-click scene initializer for the pooling experiment presentation.
    /// </summary>
    public static class AutoSceneSetup
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string SimplePrefabPath = "Assets/Prefabs/SimpleCube.prefab";
        private const string CostlyPrefabPath = "Assets/Prefabs/CostlySphere.prefab";

        [MenuItem("Tools/INITIALIZE EXPERIMENT NOW")]
        public static void InitializeExperimentNow()
        {
            EnsureFolder(PrefabFolder);

            var simplePrefab = CreateSimpleCubePrefab();
            var costlyPrefab = CreateCostlySpherePrefab();

            var manager = CreateOrReplace("Experiment_Manager");
            var poolFactory = GetOrAddComponent<UnityPoolFactory>(manager);
            var runner = GetOrAddComponent<ExperimentRunner>(manager);
            var analytics = GetOrAddComponent<ExperimentAnalytics>(manager);
            var batchRunner = GetOrAddComponent<BatchExperimentRunner>(manager);
            var videoDemoRunner = GetOrAddComponent<VideoDemoRunner>(manager);

            var uiManager = CreateOrReplace("UI_Manager");
            var ui = GetOrAddComponent<ExperimentUI>(uiManager);

            WirePoolFactory(poolFactory, simplePrefab, costlyPrefab, manager.transform);
            WireRunner(runner, simplePrefab, costlyPrefab);
            WireAnalytics(analytics, runner);
            WireUi(ui, runner, analytics, batchRunner, videoDemoRunner);
            WireBatchRunner(batchRunner, runner, poolFactory);
            WireVideoDemoRunner(videoDemoRunner, runner);

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(uiManager);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("[AutoSceneSetup] Experiment scene initialized successfully.");
            Debug.Log("[Setup] All references linked successfully!");
        }

        private static BasePoolable CreateSimpleCubePrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "SimpleCube";
            go.transform.localScale = Vector3.one;
            go.AddComponent<SimplePoolable>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, SimplePrefabPath) as GameObject;
            Object.DestroyImmediate(go);
            return prefab != null ? prefab.GetComponent<BasePoolable>() : null;
        }

        private static BasePoolable CreateCostlySpherePrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "CostlySphere";

            for (var i = 0; i < 10; i++)
            {
                var child = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                child.name = $"Child_{i + 1}";
                child.transform.SetParent(go.transform, false);
                child.transform.localScale = Vector3.one * 0.2f;
                var angle = (Mathf.PI * 2f * i) / 10f;
                child.transform.localPosition = new Vector3(Mathf.Cos(angle), 0.2f, Mathf.Sin(angle)) * 0.8f;
            }

            var costly = go.AddComponent<CostlyPoolable>();
            var costlySo = new SerializedObject(costly);
            costlySo.FindProperty("extraChildCount").intValue = 0;
            costlySo.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, CostlyPrefabPath) as GameObject;
            Object.DestroyImmediate(go);
            return prefab != null ? prefab.GetComponent<BasePoolable>() : null;
        }

        private static void WirePoolFactory(
            UnityPoolFactory poolFactory,
            BasePoolable simplePrefab,
            BasePoolable costlyPrefab,
            Transform poolParent)
        {
            var so = new SerializedObject(poolFactory);
            var definitions = so.FindProperty("poolDefinitions");
            definitions.arraySize = 2;

            var def0 = definitions.GetArrayElementAtIndex(0);
            def0.FindPropertyRelative("key").stringValue = "Simple";
            def0.FindPropertyRelative("prefab").objectReferenceValue = simplePrefab;
            def0.FindPropertyRelative("preWarmCount").intValue = 64;
            def0.FindPropertyRelative("maxSize").intValue = 1000;
            def0.FindPropertyRelative("parentOverride").objectReferenceValue = poolParent;

            var def1 = definitions.GetArrayElementAtIndex(1);
            def1.FindPropertyRelative("key").stringValue = "Costly";
            def1.FindPropertyRelative("prefab").objectReferenceValue = costlyPrefab;
            def1.FindPropertyRelative("preWarmCount").intValue = 64;
            def1.FindPropertyRelative("maxSize").intValue = 1000;
            def1.FindPropertyRelative("parentOverride").objectReferenceValue = poolParent;

            so.FindProperty("clearAllPoolsOnDestroy").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireRunner(ExperimentRunner runner, BasePoolable simplePrefab, BasePoolable costlyPrefab)
        {
            var so = new SerializedObject(runner);
            so.FindProperty("usePooling").boolValue = true;
            so.FindProperty("simplePoolKey").stringValue = "Simple";
            so.FindProperty("costlyPoolKey").stringValue = "Costly";
            so.FindProperty("simplePrefab").objectReferenceValue = simplePrefab;
            so.FindProperty("costlyPrefab").objectReferenceValue = costlyPrefab;
            so.FindProperty("spawnFrequency").floatValue = 0.35f;
            so.FindProperty("objectLifetime").floatValue = 1.2f;
            so.FindProperty("burstCount").intValue = 120;
            so.FindProperty("burstPoolKey").stringValue = "Costly";
            so.FindProperty("burstRandomRadius").floatValue = 2.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireAnalytics(ExperimentAnalytics analytics, ExperimentRunner runner)
        {
            var so = new SerializedObject(analytics);
            so.FindProperty("experimentRunner").objectReferenceValue = runner;
            so.FindProperty("autoHookRunnerEvents").boolValue = true;
            so.FindProperty("preWarmPoolKey").stringValue = "Costly";
            so.FindProperty("preWarmCount").intValue = 1000;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireUi(
            ExperimentUI ui,
            ExperimentRunner runner,
            ExperimentAnalytics analytics,
            BatchExperimentRunner batchRunner,
            VideoDemoRunner videoDemoRunner)
        {
            var so = new SerializedObject(ui);
            so.FindProperty("experimentRunner").objectReferenceValue = runner;
            so.FindProperty("analytics").objectReferenceValue = analytics;
            so.FindProperty("batchRunner").objectReferenceValue = batchRunner;
            so.FindProperty("videoDemoRunner").objectReferenceValue = videoDemoRunner;
            so.FindProperty("trackedPoolKey").stringValue = "Costly";
            so.FindProperty("showSpecificPoolOnly").boolValue = false;
            so.FindProperty("autoCreateLayoutIfMissing").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireBatchRunner(
            BatchExperimentRunner batchRunner,
            ExperimentRunner runner,
            UnityPoolFactory poolFactory)
        {
            var so = new SerializedObject(batchRunner);
            so.FindProperty("experimentRunner").objectReferenceValue = runner;
            so.FindProperty("poolFactory").objectReferenceValue = poolFactory;
            so.FindProperty("warmUpSeconds").floatValue = 2f;
            so.FindProperty("recordingSeconds").floatValue = 5f;
            so.FindProperty("settleBetweenTestsSeconds").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireVideoDemoRunner(VideoDemoRunner videoDemoRunner, ExperimentRunner runner)
        {
            var so = new SerializedObject(videoDemoRunner);
            so.FindProperty("experimentRunner").objectReferenceValue = runner;
            so.FindProperty("phaseDurationSeconds").floatValue = 4.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateOrReplace(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var created = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(created, $"Create {objectName}");
            return created;
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            return target.AddComponent<T>();
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
    }
}
