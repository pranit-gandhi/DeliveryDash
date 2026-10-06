using System.IO;
using System.Linq;
using DeliveryDash.Pixel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeliveryDash.Editor
{
    public static class BuildPixelRun
    {
        public const string ScenePath = "Assets/DeliveryDash/Pixel/Scenes/PixelRun.unity";
        public const string CapturePath = "Captures/pixel-run-edit.png";
        private const string ArtRoot = "Assets/DeliveryDash/Pixel/Art/";

        [MenuItem("DeliveryDash/Build pixel run")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Stop Play mode before rebuilding PixelRun.");
            ConfigureTexture("DistantBay.png");
            ConfigureTexture("CourierCart.png");
            Sprite[] buildings = SliceBuildings();
            Sprite[] plates = { LoadSprite("DistantBay.png") };
            Sprite hero = LoadSprite("CourierCart.png");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Pixel game camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.25f, .44f, .64f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100f;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<PixelCameraFeel>();

            var game = new GameObject("Delivery Dash pixel run");
            game.AddComponent<PixelRunController>();
            var session = game.AddComponent<PixelRunSession>();
            game.AddComponent<PixelMenuView>();
            var view = game.AddComponent<PixelSceneView>();

            var town = new GameObject("Town backdrop").AddComponent<SpriteRenderer>();
            town.transform.SetParent(game.transform, false);
            town.sprite = plates[0];
            town.sortingOrder = -20;
            town.transform.localScale = new Vector3(18f / plates[0].bounds.size.x,
                6.5f / plates[0].bounds.size.y, 1f);
            town.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            var motionRoot = new GameObject("Courier and cart motion").transform;
            motionRoot.SetParent(game.transform, false);
            motionRoot.localPosition = new Vector3(0f, -1.85f, 0f);
            var rider = new GameObject("Courier and cart").AddComponent<SpriteRenderer>();
            rider.transform.SetParent(motionRoot, false);
            rider.sprite = hero;
            rider.sortingOrder = 20;
            rider.transform.localScale = Vector3.one * (5f / hero.bounds.size.x);
            view.Bind(plates, hero, town, rider, motionRoot);

            var road = new GameObject("Moving road");
            road.transform.SetParent(game.transform, false);
            var renderer = road.AddComponent<PixelRoadRenderer>();
            renderer.SetScenerySprites(buildings, new Sprite[0]);

            QualitySettings.antiAliasing = 0;
            Application.targetFrameRate = 60;
            Directory.CreateDirectory("Captures");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var settings = new EditorBuildSettingsScene[] {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            EditorBuildSettings.scenes = settings;
            AssetDatabase.SaveAssets();
            Capture(camera, CapturePath);
            Debug.Log("PixelRun scene saved and fresh edit capture: " + CapturePath);
        }

        [MenuItem("DeliveryDash/Capture live pixel run")]
        public static void CaptureLive()
        {
            Camera camera = Camera.main;
            if (camera == null) throw new System.Exception("Pixel game camera is missing.");
            Directory.CreateDirectory("Captures");
            string path = "Captures/pixel-run-live-" + System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".png";
            Capture(camera, path);
            var session = Object.FindFirstObjectByType<PixelRunSession>();
            Debug.Log("Captured " + path + " at distance " +
                (session != null && session.Cart != null ? session.Cart.DistanceMeters.ToString("F1") : "unknown") +
                ", state " + (session != null ? session.State.ToString() : "unknown"));
        }

        private static void ConfigureTexture(string name)
        {
            string path = ArtRoot + name;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new System.Exception("Texture import failed: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = name == "CourierCart.png";
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        private static Sprite LoadSprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + name);
            if (sprite == null) throw new System.Exception("Sprite missing: " + name);
            return sprite;
        }

        private static Sprite[] SliceBuildings()
        {
            const string path = ArtRoot + "TownBuildings.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.isReadable = true;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Color32[] pixels = atlas.GetPixels32();
            var slices = new SpriteMetaData[6];
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 3; col++)
                {
                    int x0 = col * atlas.width / 3;
                    int x1 = (col + 1) * atlas.width / 3 - 1;
                    int y0 = row == 0 ? 480 : 0;
                    int y1 = row == 0 ? atlas.height - 1 : 479;
                    int minX = x1, maxX = x0, minY = y1, maxY = y0;
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                            if (pixels[y * atlas.width + x].a > 180)
                            {
                                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                            }
                    slices[row * 3 + col] = new SpriteMetaData {
                        name = "TownBuilding" + (row * 3 + col), alignment = 9,
                        pivot = new Vector2(.5f, 0f),
                        rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1)
                    };
                }
            importer.spritesheet = slices;
            importer.spritePixelsPerUnit = 100f;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        }

        public static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var oldTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
            }
        }
    }
}
