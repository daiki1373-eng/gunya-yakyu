using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// ============================================================
// BuildWebGL.cs (Editor専用)
// WebGLビルドをコマンドラインから実行するための入口。
// 使い方(例):
//   <Unityの実行ファイル> -batchmode -nographics -buildTarget WebGL \
//     -projectPath <unityフォルダ> -executeMethod BuildWebGL.Run -logFile build.log
// 出力先: プロジェクト直下の build-webgl/ (.gitignore 済み)
// ============================================================
public static class BuildWebGL
{
    public static void Run()
    {
        const string scenePath = "Assets/Scenes/Main.unity";
        if (!File.Exists(scenePath))
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);
        }
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = "build-webgl",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("[BuildWebGL] result=" + report.summary.result + " errors=" + report.summary.totalErrors);
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
