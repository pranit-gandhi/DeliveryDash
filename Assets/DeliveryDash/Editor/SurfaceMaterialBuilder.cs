using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeliveryDash.Editor
{
    // Built-in Standard materials. UVs supplied by the district meshes are in metres.
    public static class SurfaceMaterialBuilder
    {
        const string Root = "Assets/DeliveryDash/Art/";
        const string Textures = Root + "Textures/";

        public static Material Surface(string name, string source, string tint, float metresPerTile,
            float normalStrength = 0.45f, float maximumSmoothness = 0.24f)
        {
            Material material = Solid(name, tint, maximumSmoothness);
            string diffuse = Textures + source + "_Diffuse_1k.jpg";
            string normal = Textures + source + "_nor_gl_1k.jpg";
            string roughness = Textures + source + "_Rough_1k.jpg";
            Configure(diffuse, false, true);
            Configure(normal, true, false);
            Configure(roughness, false, false);
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(diffuse);
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            material.SetFloat("_BumpScale", normalStrength);
            material.EnableKeyword("_NORMALMAP");
            material.mainTextureScale = Vector2.one / metresPerTile;
            material.SetTextureScale("_BumpMap", Vector2.one / metresPerTile);

            // Standard consumes smoothness in alpha. Source roughness cannot be
            // assigned directly. Its inversion is packed once and shared by tints.
            string packed = Textures + source + "_StandardMask_1k.png";
            if (!File.Exists(packed) && File.Exists(roughness))
            {
                Texture2D input = new Texture2D(2, 2, TextureFormat.RGB24, false, true);
                input.LoadImage(File.ReadAllBytes(roughness));
                Color32[] pixels = input.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(0, 0, 0, (byte)(255 - pixels[i].r));
                Texture2D output = new Texture2D(input.width, input.height, TextureFormat.RGBA32, false, true);
                output.SetPixels32(pixels);
                output.Apply();
                File.WriteAllBytes(packed, output.EncodeToPNG());
                Object.DestroyImmediate(input);
                Object.DestroyImmediate(output);
                AssetDatabase.ImportAsset(packed);
            }
            Configure(packed, false, false);
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packed));
            material.SetTextureScale("_MetallicGlossMap", Vector2.one / metresPerTile);
            material.SetFloat("_GlossMapScale", maximumSmoothness);
            material.EnableKeyword("_METALLICGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        public static Material Solid(string name, string tint, float smoothness = 0.14f, float metallic = 0f)
        {
            Directory.CreateDirectory(Root + "TownMaterials");
            string path = Root + "TownMaterials/" + name.Replace(' ', '_') + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            ColorUtility.TryParseHtmlString(tint, out Color color);
            material.color = color;
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void Configure(string path, bool normal, bool sRGB)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool changed = importer.textureType != type || importer.sRGBTexture != sRGB ||
                importer.maxTextureSize != 1024 || !importer.mipmapEnabled ||
                importer.wrapMode != TextureWrapMode.Repeat || importer.isReadable ||
                importer.textureCompression != TextureImporterCompression.Compressed || importer.anisoLevel != 4;
            if (!changed) return;
            importer.textureType = type;
            importer.sRGBTexture = sRGB;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 60;
            importer.SaveAndReimport();
        }
    }
}
