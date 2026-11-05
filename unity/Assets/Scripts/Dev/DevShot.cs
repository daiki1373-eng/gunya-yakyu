#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;

// 確認用: 画面を高解像度で保存する。Compare は仕上げ(PostFX)なし/ありを続けて撮る。エディタ専用。
public class DevShot : MonoBehaviour
{
    public static string Last = "";

    public static void Take(string path, int superSize = 2) { Run(Go(path, superSize, false)); }
    public static void Compare(string pathPrefix, int superSize = 2) { Run(Go(pathPrefix, superSize, true)); }

    private static void Run(IEnumerator e)
    {
        var go = new GameObject("DevShot");
        go.AddComponent<DevShot>().StartCoroutine(Wrap(go, e));
    }

    private static IEnumerator Wrap(GameObject go, IEnumerator e) { yield return e; Destroy(go); }

    private static IEnumerator Go(string path, int ss, bool compare)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        if (compare)
        {
            PostFX.Enabled = false;
            yield return new WaitForEndOfFrame();
            Save(path + "_before.jpg", ss);
            PostFX.Enabled = true;
            yield return null;
            yield return new WaitForEndOfFrame();
            Save(path + "_after.jpg", ss);
        }
        else
        {
            yield return new WaitForEndOfFrame();
            Save(path, ss);
        }
        Last = path;
    }

    private static void Save(string path, int ss)
    {
        var tex = ScreenCapture.CaptureScreenshotAsTexture(ss);
        File.WriteAllBytes(path, tex.EncodeToJPG(90));
        Destroy(tex);
    }
}
#endif
