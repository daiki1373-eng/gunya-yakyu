#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;

// 確認用(フェーズE): 人形を横に走らせて、横向きの走り絵が出るかを撮る。Recordings/runcard_<id>.png。エディタ専用。
public class RunCardShot : MonoBehaviour
{
    public static string Status = "";
    public static void Run(string id) { new GameObject("RunCardShot").AddComponent<RunCardShot>().StartCoroutine2(id); }
    private void StartCoroutine2(string id) { StartCoroutine(Go(id)); }

    private IEnumerator Go(string id)
    {
        Status = "running";
        var root = new GameObject("RunRoot").transform;
        root.position = new Vector3(600f, 0f, 0f);
        var p = StandeePuppet.Create(root, root, 4);
        p.SetSprite(CharaCatalog.Cut(id), 3.3f);
        var cg = new GameObject("cam"); var cam = cg.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.25f, 0.45f, 0.3f);
        cam.orthographic = true; cam.orthographicSize = 2.4f; cam.depth = 50;
        const int S = 300, N = 6;
        var sheet = new Texture2D(S * N, S, TextureFormat.RGB24, false);
        // 0: 止まる / 1-2: 右へ走る / 3: 止まる / 4-5: 左へ走る
        float[] dir = { 0f, 1f, 1f, 0f, -1f, -1f };
        for (int f = 0; f < N; f++)
        {
            float t = 0f;
            while (t < 0.8f)
            {
                t += Time.deltaTime;
                root.position += new Vector3(dir[f] * 6f * Time.deltaTime, 0f, 0f);
                cg.transform.position = new Vector3(root.position.x, 1.7f, root.position.z - 6f);
                yield return null;
            }
            yield return new WaitForEndOfFrame();
            var fi = typeof(StandeePuppet).GetField("_dirX", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rc = p.GetComponentsInChildren<SpriteRenderer>(true);
            string fl = ""; foreach (var r in rc) if (r.name == "RunCard") fl = r.enabled + "/" + r.flipX;
            Status += " f" + f + ":dir=" + ((float)fi.GetValue(p)).ToString("F2") + " card=" + fl;
            var rt = new RenderTexture(S, S, 24); cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt; sheet.ReadPixels(new Rect(0, 0, S, S), S * f, 0);
            RenderTexture.active = null; cam.targetTexture = null; rt.Release();
        }
        sheet.Apply();
        File.WriteAllBytes("Recordings/runcard_" + id + ".png", sheet.EncodeToPNG());
        Destroy(cg); Destroy(root.gameObject);
        Status = "done";
        Destroy(gameObject);
    }
}
#endif
