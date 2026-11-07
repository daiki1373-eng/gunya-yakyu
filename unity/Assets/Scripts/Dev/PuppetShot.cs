#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;

// 確認用: 守備・走者の人形(元絵の体)の 待つ/走る/捕る/投げる を撮って Recordings/puppets/<id>.png に並べる。エディタ専用。
public class PuppetShot : MonoBehaviour
{
    public static string Status = "";
    public static void Run(string[] ids)
    {
        var go = new GameObject("PuppetShot");
        go.AddComponent<PuppetShot>().StartCoroutine(go.GetComponent<PuppetShot>().Go(ids));
    }

    private IEnumerator Go(string[] ids)
    {
        Status = "running";
        Directory.CreateDirectory("Recordings/puppets");
        const int S = 300, N = 8;
        foreach (var id in ids)
        {
            var cut = CharaCatalog.Cut(id);
            if (cut == null) continue;
            var root = new GameObject("PuppetRoot_" + id).transform;
            root.position = new Vector3(600f, 0f, 0f);
            var p = StandeePuppet.Create(root, root, 4);
            p.WithGlove = true;
            p.SetSprite(cut, 3.3f);
            var sheet = new Texture2D(S * N, S, TextureFormat.RGB24, false);
            // 0:待つ 1-3:走る 4-5:捕る 6-7:投げる
            for (int f = 0; f < N; f++)
            {
                StandeePuppet.DebugRun = (f >= 1 && f <= 3) ? 1f : 0f;
                if (f == 4) p.Catch();
                if (f == 6) p.Throw();
                float wait = f == 0 ? 0.6f : (f <= 3 ? 0.11f : (f == 4 || f == 6 ? 0.12f : 0.12f));
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < wait) yield return null;
                var cg = new GameObject("cam"); var cam = cg.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.25f, 0.45f, 0.3f);
                cam.orthographic = true; cam.orthographicSize = 2.2f;
                cg.transform.position = new Vector3(600f, 1.7f, -6f);
                var rt = new RenderTexture(S, S, 24); cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt; sheet.ReadPixels(new Rect(0, 0, S, S), S * f, 0);
                RenderTexture.active = null; cam.targetTexture = null; Destroy(cg); rt.Release();
            }
            StandeePuppet.DebugRun = -1f;
            sheet.Apply();
            File.WriteAllBytes("Recordings/puppets/" + id + ".png", sheet.EncodeToPNG());
            Destroy(root.gameObject);
            yield return null;
        }
        Status = "done";
        Destroy(gameObject);
    }
}
#endif
