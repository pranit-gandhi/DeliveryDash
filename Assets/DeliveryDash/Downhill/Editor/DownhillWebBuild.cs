using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DeliveryDash.Editor
{
    public static class DownhillWebBuild
    {
        [MenuItem("DeliveryDash/Build local Web game")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Stop Play mode before building Web");
            Directory.CreateDirectory("WebBuild");
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { BuildDownhill.RunScenePath },
                locationPathName = "WebBuild",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.InvalidOperationException("Web build failed: " + report.summary.result);
            string loader = Path.GetFileName(Directory.GetFiles("WebBuild/Build", "*.loader.js")[0]);
            string data = Path.GetFileName(Directory.GetFiles("WebBuild/Build", "*.data")[0]);
            string framework = Path.GetFileName(Directory.GetFiles("WebBuild/Build", "*.framework.js")[0]);
            string wasm = Path.GetFileName(Directory.GetFiles("WebBuild/Build", "*.wasm")[0]);
            string html = @"<!doctype html><html lang='en'><meta charset='utf-8'>
<meta name='viewport' content='width=device-width,initial-scale=1'>
<title>Delivery Dash</title><style>
html,body{margin:0;height:100%;background:#19232a;overflow:hidden}
body{display:grid;place-items:center;font:18px sans-serif;color:#fff}
canvas{width:min(100vw,177.7778vh);height:min(56.25vw,100vh);image-rendering:pixelated;outline:none}
#loading{position:fixed;pointer-events:none}button{font:inherit}
</style><canvas id='game' width='1280' height='720' tabindex='0'></canvas><div id='loading'>Loading</div>
<script src='Build/" + loader + @"'></script><script>
const canvas=document.getElementById('game'),loading=document.getElementById('loading');
createUnityInstance(canvas,{dataUrl:'Build/" + data + @"',frameworkUrl:'Build/" + framework + @"',codeUrl:'Build/" + wasm + @"',streamingAssetsUrl:'StreamingAssets',companyName:'DeliveryDash',productName:'Delivery Dash',productVersion:'0.2',matchWebGLToCanvasSize:false},p=>loading.textContent='Loading '+Math.round(p*100)+'%').then(game=>{window.deliveryDash=game;loading.remove();canvas.focus();canvas.addEventListener('pointerdown',()=>canvas.focus())}).catch(e=>loading.textContent='Could not start: '+e);
</script></html>";
            File.WriteAllText("WebBuild/index.html", html);
            Debug.Log("Local Web build saved: WebBuild/index.html; bytes " + report.summary.totalSize);
        }
    }
}
