using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeliveryDash.Editor
{
    public static class EndlessRunnerSetup
    {
        public const string ScenePath = "Assets/DeliveryDash/Scenes/EndlessRun.unity";

        [MenuItem("DeliveryDash/Create endless runner scene")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("Opened existing EndlessRun scene; saved settings preserved.");
                return;
            }
            EditorSceneManager.OpenScene(BuildLookDev.ScenePath);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath, true);
            EditorSceneManager.OpenScene(ScenePath);
            var cart = Object.FindFirstObjectByType<DeliveryDash.CartFeelController>();
            var district = GameObject.Find("Downhill market district");
            if (cart == null || district == null) throw new System.InvalidOperationException("Expected courier and authored market district in FeelPlayground.");
            Transform[] houses = district.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "Town house with deep windows").ToArray();
            var root = new GameObject("Endless delivery shift");
            var generator = root.AddComponent<DeliveryDash.EndlessRoadGenerator>();
            generator.Configure(cart, district, houses,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/DeliveryDash/Art/Street_stone.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/DeliveryDash/Art/Street_pale_edge.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/DeliveryDash/Art/Town_faded_coral.mat"));
            var shift = root.AddComponent<DeliveryDash.DeliveryShift>();
            shift.Configure(cart);
            var camera = Object.FindFirstObjectByType<DeliveryDash.ChaseCamera>();
            // Give the runner more forward visibility when choosing around barriers.
            var settings = new SerializedObject(camera);
            settings.FindProperty("distance").floatValue = 5.5f;
            settings.FindProperty("height").floatValue = 3.5f;
            settings.FindProperty("lookAhead").floatValue = 6f;
            settings.FindProperty("sideOffset").floatValue = .65f;
            settings.FindProperty("lookHeight").floatValue = .7f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            camera.SetTarget(cart.transform);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            var previous = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            previous.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = previous.ToArray();
            Selection.activeGameObject = root;
            Debug.Log("EndlessRun ready. Press Play. A/D steer, R retry, N new seed. Customer gates every 288 m.");
        }
    }
}
