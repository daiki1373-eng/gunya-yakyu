using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ============================================================
// ProjectSetup.cs (Editor専用)
// プロジェクトを初めて開いたときに、起動シーン Assets/Scenes/Main.unity が
// 無ければ自動で作り、Build Settings に登録する。
// これにより「開いて再生ボタンを押すだけ」で試せる状態になる。
// 既にシーンがある場合は何もしない(登録だけ確認する)。
// 手動でやり直したい場合はメニュー「甲子園への道 > 初期セットアップを実行」。
// ============================================================
[InitializeOnLoad]
public static class ProjectSetup
{
    private const string ScenePath = "Assets/Scenes/Main.unity";

    static ProjectSetup()
    {
        EditorApplication.delayCall += TrySetup;
    }

    [MenuItem("甲子園への道/初期セットアップを実行")]
    private static void SetupFromMenu()
    {
        TrySetup();
        Debug.Log("[甲子園への道] 初期セットアップを確認しました: " + ScenePath);
    }

    private static void TrySetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        if (File.Exists(ScenePath))
        {
            RegisterScene();
            // 無題シーンが開いているだけなら、起動シーンを開いておく。
            var active = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path))
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
            return;
        }

        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterScene();
        EditorSceneManager.OpenScene(ScenePath);
        Debug.Log("[甲子園への道] 起動シーンを作成しました: " + ScenePath +
                  " このまま再生ボタン(▶)で遊べます。");
    }

    private static void RegisterScene()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == ScenePath) return;
        }
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
