#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

// iPhone 向けの書き出しの仕上げ: ホーム画面の名前を「ぐにゃ野球」にする。
// (productName は保存データの置き場所に関わるので変えず、表示名だけをここで差し替える)
public static class IosPostBuild
{
    [PostProcessBuild(100)]
    public static void OnPostBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        var root = plist.root;
        root.SetString("CFBundleDisplayName", "ぐにゃ野球");
        root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        plist.WriteToFile(plistPath);
    }
}
#endif
