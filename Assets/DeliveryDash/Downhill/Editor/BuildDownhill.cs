using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using DeliveryDash.Downhill;

namespace DeliveryDash.Editor
{
    public static class BuildDownhill
    {
        public const string ScenePath = "Assets/DeliveryDash/Downhill/Scenes/DownhillFeel.unity";
        public const string RunScenePath = "Assets/DeliveryDash/Downhill/Scenes/DownhillRun.unity";
        [MenuItem("DeliveryDash/Build downhill feel course")]
        public static void Build()
        { BuildScene(false); }
        [MenuItem("DeliveryDash/Build PCG downhill run")]
        public static void BuildGenerated()
        { BuildScene(true); }
        static void BuildScene(bool generated)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory("Assets/DeliveryDash/Downhill/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var course = generated ? CourseGraph.CreateGenerated(2647) : CourseGraph.CreateFeel();
            CourseMeshBuilder.Build(course, null);
            var cart = new GameObject("Courier cart");
            cart.transform.position = course.Sample(0).Position;
            var visual = new GameObject("Suspension frame").transform;
            visual.SetParent(cart.transform, false);
            var frame = CartVisualBuilder.Build(visual, true).transform;
            frame.localScale = new Vector3(.67f, .88f, .67f);
            Transform pizza;
            var rider = CourierVisualBuilder.Build(visual, out pizza);
            var drive = cart.AddComponent<DownhillCart>();
            drive.Configure(course);
            var motion = cart.AddComponent<DownhillVisuals>();
            motion.Frame = visual;
            motion.Pizza = pizza;
            motion.Rider = rider;
            cart.AddComponent<DownhillCrash>();
            cart.AddComponent<DownhillAudio>();
            cart.AddComponent<DownhillDelivery>();
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 58;
            camera.nearClipPlane = .08f;
            camera.farClipPlane = 900;
            camera.backgroundColor = new Color(.40f, .59f, .67f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = cart.transform.position + new Vector3(1.7f, 3.7f, -6.4f);
            camera.transform.LookAt(cart.transform.position + new Vector3(0, 1.1f, 5));
            camera.gameObject.AddComponent<AudioListener>();
            camera.gameObject.AddComponent<PixelOutput>();
            camera.gameObject.AddComponent<DownhillChaseCamera>().Configure(cart.transform, drive);
            var session = new GameObject("Delivery session").AddComponent<DownhillSession>();
            session.Cart = drive;
            session.UseGenerated = generated;
            session.gameObject.AddComponent<DownhillBrowserTelemetry>();
            session.gameObject.AddComponent<DownhillMusic>();
            var sun = new GameObject("Warm sunset").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(38, -38, 0);
            sun.color = new Color(1, .83f, .66f);
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .85f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.46f, .57f, .66f);
            RenderSettings.ambientEquatorColor = new Color(.31f, .32f, .34f);
            RenderSettings.ambientGroundColor = new Color(.17f, .19f, .23f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 170;
            RenderSettings.fogEndDistance = 800;
            RenderSettings.fogColor = camera.backgroundColor;
            QualitySettings.antiAliasing = 0;
            QualitySettings.shadowDistance = 95;
            QualitySettings.shadows = ShadowQuality.All;
            DownhillTown.Build(course, 2647);
            DownhillBackdrop.Build(course);
            DownhillScenery.Apply(course);
            PersistSceneResources(scene);
            AssetDatabase.SaveAssets();
            string savePath = generated ? RunScenePath : ScenePath;
            EditorSceneManager.SaveScene(scene, savePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(savePath, true) };
            AssetDatabase.SaveAssets();
            Capture();
        }
        static void PersistSceneResources(UnityEngine.SceneManagement.Scene scene)
        {
            const string dir = "Assets/DeliveryDash/Downhill/Generated";
            Directory.CreateDirectory(dir);
            if(RenderSettings.skybox!=null&&!AssetDatabase.Contains(RenderSettings.skybox))
                AssetDatabase.CreateAsset(RenderSettings.skybox,AssetDatabase.GenerateUniqueAssetPath(dir+"/SeasonSky.mat"));
            int index = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                    if (filter.sharedMesh != null && !AssetDatabase.Contains(filter.sharedMesh))
                        AssetDatabase.CreateAsset(filter.sharedMesh, AssetDatabase.GenerateUniqueAssetPath(dir + "/Mesh" + index++ + ".asset"));
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material != null && material.mainTexture != null && !AssetDatabase.Contains(material.mainTexture))
                            AssetDatabase.CreateAsset(material.mainTexture, AssetDatabase.GenerateUniqueAssetPath(dir + "/Texture" + index++ + ".asset"));
                        if (material != null && !AssetDatabase.Contains(material))
                            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(dir + "/Material" + index++ + ".mat"));
                    }
            }
        }
        [MenuItem("DeliveryDash/Capture downhill camera")]
        public static void Capture()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Directory.CreateDirectory("Captures");
            string path = "Captures/downhill-" + System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".png";
            BuildLookDev.Capture(cam, path);
            Debug.Log("Fresh downhill camera: " + path);
        }
        static Material Mat(string name, string color)
        {
            Color c; ColorUtility.TryParseHtmlString(color, out c);
            var mat = new Material(Shader.Find("Standard")) { name = name, color = c };
            mat.SetFloat("_Glossiness", .08f);
            return mat;
        }
        static void Box(string name, Transform root, Vector3 p, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(root, false);
            go.transform.localPosition = p; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
        static void Architecture(CourseGraph course)
        {
            var district = new GameObject("Terraced market street").transform;
            var plaster = new[] { Mat("Peach plaster", "#B97858"), Mat("Ochre plaster", "#B39261"), Mat("Warm chalk", "#BDB097") };
            var roof = Mat("Terracotta", "#793B2F");
            var recess = Mat("Deep recess", "#263438");
            var trim = Mat("Limestone", "#A49C82");
            var shutter = Mat("Painted shutters", "#345A57");
            var awning = Mat("Wine canvas", "#8D3B30");
            for (int i = 0; i < 38; i++)
            {
                var sample = course.Sample(12 + i * 12);
                int side = i % 2 == 0 ? -1 : 1;
                var right = Vector3.Cross(Vector3.up, sample.Forward).normalized;
                var house = new GameObject("Terrace house " + i).transform;
                house.SetParent(district, false);
                house.position = sample.Position + right * side * (sample.Width * .5f + 4.8f + i % 3);
                house.rotation = Quaternion.LookRotation(-side * right, Vector3.up);
                float height = 6 + i % 3 * 1.6f;
                Box("Raised stone foundation", house, new Vector3(0, -.8f, 0), new Vector3(7, 2.6f, 6), trim);
                Box("Plaster walls", house, new Vector3(0, height * .5f, 0), new Vector3(6.6f, height, 5.6f), plaster[i % 3]);
                Box("Eave shadow", house, new Vector3(0, height, 0), new Vector3(7.2f, .24f, 6.2f), recess);
                foreach (int slope in new[] {-1, 1})
                {
                    var panel = new GameObject("Pitched tiled roof").transform;
                    panel.SetParent(house, false); panel.localPosition = new Vector3(slope * 1.72f, height + .6f, 0);
                    panel.localRotation = Quaternion.Euler(0, 0, -slope * 22);
                    Box("Roof plane", panel, Vector3.zero, new Vector3(3.85f, .22f, 6.5f), roof);
                    for (int row = 0; row < 12; row++) Box("Tile course", panel, new Vector3(0, .13f, -3 + row * .54f), new Vector3(3.85f, .08f, .07f), roof);
                }
                for (int floor = 0; floor < 2; floor++)
                    for (int col = -1; col <= 1; col++)
                    {
                        var p = new Vector3(col * 2, 2.2f + floor * 2.6f, 2.82f);
                        Box("Window recess", house, p, new Vector3(.88f, 1.5f, .06f), recess);
                        Box("Window sill", house, p + new Vector3(0, -.81f, .13f), new Vector3(1.18f, .17f, .35f), trim);
                        foreach (int edge in new[] {-1,1}) Box("Shutter", house, p + new Vector3(edge * .66f, 0, .13f), new Vector3(.35f, 1.54f, .15f), shutter);
                    }
                Box("Doorway", house, new Vector3(0, 1.1f, 2.83f), new Vector3(1.05f, 2.2f, .08f), recess);
                Box("Canvas canopy", house, new Vector3(0, 2.6f, 3.45f), new Vector3(4.5f, .14f, 1.6f), awning);
                Box("Drainpipe", house, new Vector3(3.1f, height * .5f, 2.89f), new Vector3(.13f, height, .13f), recess);
            }
        }
    }
}
