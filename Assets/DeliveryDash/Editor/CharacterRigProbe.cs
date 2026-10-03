using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeliveryDash.Editor
{
    [InitializeOnLoad]
    public static class CharacterRigProbe
    {
        static CharacterRigProbe()
        {
            EditorApplication.delayCall += () =>
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DeliveryDash/ThirdParty/Quaternius/CourierBase.fbx");
                if (source == null) return;
                Directory.CreateDirectory("Captures");
                var transforms = source.GetComponentsInChildren<Transform>(true);
                var renderers = source.GetComponentsInChildren<Renderer>(true);
                var lines = transforms.Select(x => "BONE " + x.name + " " + x.localPosition).Concat(
                    renderers.Select(x => "RENDERER " + x.name + " " + x.GetType().Name + " " + x.bounds.size));
                File.WriteAllLines("Captures/character-rig-report.txt", lines);
            };
        }
    }
}
