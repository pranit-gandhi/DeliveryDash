using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeliveryDash.Editor
{
    public static class TownDeliverySetup
    {
        public const string ScenePath = "Assets/DeliveryDash/Scenes/TownDelivery.unity";
        [MenuItem("DeliveryDash/Open town delivery")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath); return;
            }
            EditorSceneManager.OpenScene(BuildLookDev.ScenePath);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath, true);
            EditorSceneManager.OpenScene(ScenePath);
            var cart = Object.FindFirstObjectByType<DeliveryDash.CartFeelController>();
            var district = GameObject.Find("Downhill market district");
            var templates = district.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Town house with deep windows").ToArray();
            var pizza = cart.GetComponentsInChildren<Transform>(true).First(t => t.name == "Pizza supported at fingertips");
            cart.ConfigureTownDriving();
            var root = new GameObject("Town delivery shift");
            var world = root.AddComponent<DeliveryDash.TownDeliveryWorld>();
            world.Configure(cart, district, templates,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/DeliveryDash/Art/Street_stone.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/DeliveryDash/Art/Street_pale_edge.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/DeliveryDash/Art/Town_faded_coral.mat"));
            root.AddComponent<DeliveryDash.TownDeliveryGame>().Configure(cart, pizza);
            var camera = Object.FindFirstObjectByType<DeliveryDash.ChaseCamera>();
            var settings = new SerializedObject(camera);
            settings.FindProperty("distance").floatValue = 7;
            settings.FindProperty("height").floatValue = 5;
            settings.FindProperty("lookAhead").floatValue = 6;
            settings.FindProperty("sideOffset").floatValue = 0;
            settings.FindProperty("lookHeight").floatValue = .6f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            camera.SetTarget(cart.transform);
            RenderSettings.fogStartDistance = 85;
            RenderSettings.fogEndDistance = 150;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorApplication.ExecuteMenuItem("File/Save Project");
            Selection.activeGameObject = root;
            Debug.Log("TownDelivery ready: start empty, steer to any orange P shop, deliver to assigned houses. Minimap top right; Tab or click icons to choose route.");
        }
    }
}
