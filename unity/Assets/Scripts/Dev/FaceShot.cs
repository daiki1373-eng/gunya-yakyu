#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;

// 確認用: 指定キャラの表情7種を撮り、Recordings/faces/<id>.png(横に7枚)に保存する。エディタ専用。
// FaceShot.Run(new[]{"monk","dog"}) を再生中に呼ぶ。
public class FaceShot : MonoBehaviour
{
    public static string Status = "";
    public static void Run(string[] ids)
    {
        var go = new GameObject("FaceShot");
        go.AddComponent<FaceShot>().StartCoroutine(go.GetComponent<FaceShot>().Go(ids));
    }

    private IEnumerator Go(string[] ids)
    {
        Status = "running";
        float keep = Time.timeScale;
        Time.timeScale = 0f;
        Directory.CreateDirectory("Recordings/faces");
        const int S = 360;
        foreach (var id in ids)
        {
            var def = CharaCatalog.Get(id);
            if (def == null) continue;
            var stats = CharaRoster.Stats(def);
            var p = RagdollPlayer.Create(null, "Shot_" + id, new Vector2(500f, 0f), 1f, stats, Color.white, Color.red, true);
            yield return null;
            if (p.Skin == null) { Destroy(p.gameObject); continue; }
            var sheet = new Texture2D(S * 7, S, TextureFormat.RGB24, false);
            for (int e = 0; e < 7; e++)
            {
                p.SetExpression((Expr)e, 99f);
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < (e == 0 ? 0.6f : 0.8f)) yield return null;
                Vector3 fw = p.Skin.FaceWorld();
                var cg = new GameObject("cam"); var cam = cg.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.25f, 0.45f, 0.3f);
                cam.orthographic = true; cam.orthographicSize = p.Skin.FaceSpan() * 1.25f;
                cg.transform.position = new Vector3(fw.x, fw.y, fw.z - 5f);
                var rt = new RenderTexture(S, S, 24); cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt; sheet.ReadPixels(new Rect(0, 0, S, S), S * e, 0);
                RenderTexture.active = null; cam.targetTexture = null; Destroy(cg); rt.Release();
            }
            sheet.Apply();
            File.WriteAllBytes("Recordings/faces/" + id + ".png", sheet.EncodeToPNG());
            Destroy(p.gameObject);
            yield return null;
        }
        Time.timeScale = keep;
        Status = "done";
        Destroy(gameObject);
    }
}
#endif
