using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeliveryDash.Editor
{
    public static class BuildLookDev
    {
        public const string ScenePath = "Assets/DeliveryDash/Scenes/FeelPlayground.unity";
        public const string CapturePath = "Captures/feel-playground-lookdev.png";

        [MenuItem("DeliveryDash/Rebuild feel playground")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory("Assets/DeliveryDash/Scenes");
            Directory.CreateDirectory("Assets/DeliveryDash/Art/Generated");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting();
            TownVisualBuilder.Build();
            var cart = BuildHero();
            var camera = BuildCamera(cart.transform);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Capture(camera, CapturePath);
            Debug.Log("Saved FeelPlayground and fresh 1280 by 720 camera capture.");
        }

        [MenuItem("DeliveryDash/Rebuild courier only")]
        public static void RebuildHero()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var c in UnityEngine.Object.FindObjectsByType<DeliveryDash.CartFeelController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(c.gameObject);
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(c.gameObject);
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(l.gameObject);
            BuildLighting();
            var hero = BuildHero();
            var camera = BuildCamera(hero.transform);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Capture(camera, CapturePath);
        }

        static void BuildLighting()
        {
            var sun = new GameObject("Late afternoon sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, .83f, .65f);
            sun.intensity = .92f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .82f;
            sun.shadowBias = .025f;
            sun.shadowNormalBias = .12f;
            sun.transform.rotation = Quaternion.Euler(32f, -40f, 0f);
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 75f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.antiAliasing = 4;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.35f,.43f,.52f);
            RenderSettings.ambientEquatorColor = new Color(.28f,.27f,.26f);
            RenderSettings.ambientGroundColor = new Color(.13f,.12f,.12f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.44f,.53f,.60f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 65f;
            RenderSettings.fogEndDistance = 230f;
        }

        static GameObject BuildHero()
        {
            var cart = new GameObject("Courier and cart");
            cart.transform.position = new Vector3(0f, .06f, 0f);
            var visual = new GameObject("Cart visual response root").transform;
            visual.SetParent(cart.transform, false);
            var frame = CartVisualBuilder.Build(visual).transform;
            frame.localScale = new Vector3(.67f, .88f, .67f);
            Transform pizza;
            var courier = CourierVisualBuilder.Build(visual, out pizza);
            cart.AddComponent<DeliveryDash.CartFeelController>();
            var response = cart.AddComponent<DeliveryDash.CartVisualResponse>();
            var spins = new System.Collections.Generic.List<Transform>();
            var casters = new System.Collections.Generic.List<Transform>();
            foreach (Transform part in frame.GetComponentsInChildren<Transform>(true))
            {
                if (part.name.StartsWith("WheelSpin_")) spins.Add(part);
                if (part.name.StartsWith("CasterPivot_")) casters.Add(part);
            }
            response.Configure(visual, courier, pizza, spins.ToArray(), casters.ToArray(), .145f);
            return cart;
        }

        static Camera BuildCamera(Transform target)
        {
            var go = new GameObject("Spring chase camera");
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 53f;
            camera.nearClipPlane = .10f;
            camera.farClipPlane = 300f;
            camera.backgroundColor = new Color(.48f,.57f,.64f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            go.AddComponent<AudioListener>();
            go.AddComponent<DeliveryDash.ChaseCamera>().SetTarget(target);
            return camera;
        }

        public static void Capture(Camera camera, string path)
        {
            Directory.CreateDirectory("Captures");
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var previous = RenderTexture.active;
            var oldTarget = camera.targetTexture;
            camera.targetTexture = rt;
            RenderTexture.active = rt;
            camera.Render();
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = oldTarget;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
