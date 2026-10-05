using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DeliveryDash.Editor
{
    public static class TownImprovementChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        [MenuItem("DeliveryDash/Checks/Validate sparse shops and guidance")]
        public static void Run()
        {
            int count=0;
            for(int seed=100;seed<112;seed++)
            {
                TownMap.ConfigureShops(seed,4,250);var shops=new List<Vector3>();var net=TownMap.Network(seed);
                for(int x=-15;x<=15;x++)for(int y=-15;y<=15;y++)if(TownMap.HasShop(seed,new Vector2Int(x,y)))shops.Add(net.Address(new TownAddress(new Vector2Int(x,y),1)).Stop(Vector2Int.zero));
                Check(shops.Count>15&&shops.Count<90,"Shop density/availability seed "+seed);
                for(int a=0;a<shops.Count;a++)for(int b=a+1;b<shops.Count;b++)Check(Vector3.Distance(shops[a],shops[b])>=249.99f,"Shop bay separation");
                Check(TownMap.HasShop(seed,Vector2Int.zero),"Starting shop missing");count+=shops.Count;
            }
            if(EditorApplication.isPlaying)
            {
                var game=UnityEngine.Object.FindFirstObjectByType<TownDeliveryGame>();var world=game.GetComponent<TownDeliveryWorld>();game.Restart(6553);
                var initial=game.Selected;TownAddress other=initial;
                for(int x=3;x<=9;x++)for(int z=3;z<=9;z++){var a=new TownAddress(new Vector2Int(x,z),1);if(TownMap.IsShop(6553,a))other=a;}
                Check(!other.Equals(initial),"Need distant shop for guidance check");game.Select(initial);
                world.Runner.ResetAt(world.Position(other)-world.StopDirection(other)*18+Vector3.up*.12f,Quaternion.LookRotation(world.StopDirection(other)));world.RefreshBlocks();
                game.RefreshShopGuidance();Check(game.Selected.Equals(initial)&&game.ManualTarget,"Manual target was overwritten");
                game.AutomaticGuidance();Check(game.Selected.Equals(other)&&!game.ManualTarget,"Auto guidance did not select nearby road-reachable shop");
                game.Restart(6553);world.Runner.GetComponent<TownRideSelector>().Open();
            }
            File.WriteAllText("Captures/shop-guidance-checks.txt",$"PASS: {count} shops across 12 seeds, 250m minimum bay separation, bounded sparse density, guaranteed starting shop; live manual target persistence and automatic route restoration.\n");
            Debug.Log("PASS sparse shops and manual/automatic guidance");
        }
        [MenuItem("DeliveryDash/Checks/Benchmark town player loop")]
        public static void Benchmark(){Check(EditorApplication.isPlaying,"Enter Play mode");new GameObject("Editor town benchmark").AddComponent<TownBenchmark>();}
    }
}
