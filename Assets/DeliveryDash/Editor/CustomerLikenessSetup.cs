using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DeliveryDash.Editor
{
    public static class CustomerLikenessSetup
    {
        [MenuItem("DeliveryDash/Install free customer likenesses")]
        public static void Install()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var world=UnityEngine.Object.FindFirstObjectByType<TownDeliveryWorld>();
            if(world==null) throw new InvalidOperationException("Open TownDelivery scene first.");
            var so=new SerializedObject(world); var models=so.FindProperty("customerModels"); models.arraySize=5;
            string[] names={"dario-amodei","donald-trump","elon-musk","sam-altman","jensen-huang"};
            for(int i=0;i<names.Length;i++)
            {
                string folder="Assets/DeliveryDash/Art/Characters/Likeness/"+names[i];
                string path=folder+"/"+names[i]+".fbx";
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType=ModelImporterAnimationType.Generic;
                importer.importAnimation=false; importer.isReadable=false; importer.SaveAndReimport();
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var instance=new GameObject(names[i]);
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(source,instance.transform);
                visual.transform.localRotation=Quaternion.Euler(0,180,0);
                try
                {
                    foreach(var renderer in instance.GetComponentsInChildren<Renderer>())
                    {
                        renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>
                        {
                            string materialPath=folder+"/"+m.name.Replace('/','_')+".mat";
                            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                            if(material==null) { material=new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material,materialPath); }
                            material.color=m.color;
                            material.SetFloat("_Glossiness",m.name.Contains("Hair")?.15f:.2f);
                            if(m.name.Contains("Photo"))
                            {
                                material.color=Color.white;
                                material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/"+names[i]+".fbm/"+names[i]+".jpg");
                                if(material.mainTexture==null) throw new InvalidOperationException("Missing face texture: "+names[i]);
                            }
                            EditorUtility.SetDirty(material); return material;
                        }).ToArray();
                    }
                    var prefab=PrefabUtility.SaveAsPrefabAsset(instance,folder+"/"+names[i]+".prefab");
                    models.GetArrayElementAtIndex(i).objectReferenceValue=prefab;
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            so.ApplyModifiedProperties(); EditorUtility.SetDirty(world);
            EditorSceneManager.MarkSceneDirty(world.gameObject.scene); EditorSceneManager.SaveScene(world.gameObject.scene);
            AssetDatabase.SaveAssets(); Debug.Log("Installed five free photo-fitted customer models in TownDelivery.");
        }
    }
}
