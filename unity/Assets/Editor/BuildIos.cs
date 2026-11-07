using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// ============================================================
// BuildIos.cs (Editor専用)
// iPhone 向けに Xcode のプロジェクトを書き出す。出力先: unity/Builds/iOS(.gitignore 済み)
// メニュー「Gunya/iPhone 向けに書き出す」か、-executeMethod BuildIos.Run で使う。
// 書き出した後は Xcode で Unity-iPhone.xcodeproj を開き、署名(Team)を選んで iPhone に入れる。
// ============================================================
public static class BuildIos
{
    public static string LastResult = "";

    [MenuItem("Gunya/iPhone 向けに書き出す")]
    public static void Run()
    {
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "jp.daiki1373.gunyayakyu");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.iOS.targetOSVersionString = "13.0";
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.statusBarHidden = true;
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/app_icon.png");
        if (icon != null) PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = "Builds/iOS",
            target = BuildTarget.iOS,
            targetGroup = BuildTargetGroup.iOS,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        LastResult = report.summary.result + " errors=" + report.summary.totalErrors + " size=" + report.summary.totalSize
            + " sec=" + report.summary.totalTime.TotalSeconds.ToString("F0");
        Debug.Log("[BuildIos] " + LastResult);
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
