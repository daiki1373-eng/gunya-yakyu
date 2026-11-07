using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// ============================================================
// BuildWebGL.cs (Editor専用)
// ブラウザ版(WebGL)を書き出す。iPhone の Safari で「ホーム画面に追加」して遊ぶ前提(テンプレート Gunya)。
//   メニュー「Gunya/ブラウザ版を書き出す」、またはコマンドラインから:
//   <Unityの実行ファイル> -batchmode -nographics -buildTarget WebGL \
//     -projectPath <unityフォルダ> -executeMethod BuildWebGL.Run -logFile build.log
// 出力先: プロジェクト直下の build-webgl/ (.gitignore 済み)。公開は firebase.json(Firebase Hosting)
// ============================================================
public static class BuildWebGL
{
    public static string LastResult = "";

    [MenuItem("Gunya/ブラウザ版を書き出す")]
    public static void Run()
    {
        const string scenePath = "Assets/Scenes/Main.unity";
        if (!File.Exists(scenePath))
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);
        }
        // iPhone の Safari は ASTC を直接扱える(圧縮のままGPUへ)
        EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ASTC;
        PlayerSettings.WebGL.template = "PROJECT:Gunya";
        // 配信側の設定(Content-Encoding)に頼らず読めるよう、gzip を自前で展開する
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.initialMemorySize = 256;
        PlayerSettings.WebGL.maximumMemorySize = 2048;
        PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.runInBackground = false;
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = "build-webgl",
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        LastResult = report.summary.result + " errors=" + report.summary.totalErrors + " size=" + report.summary.totalSize
            + " sec=" + report.summary.totalTime.TotalSeconds.ToString("F0");
        Debug.Log("[BuildWebGL] " + LastResult);
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
