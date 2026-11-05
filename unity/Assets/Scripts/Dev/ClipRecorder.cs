#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 確認用: 再生中の画面(JPG連番)と音(WAV)を書き出す。エディタ専用。
// ClipRecorder.Begin("Recordings/clip1", 15) で開始、ClipRecorder.End() で終了。
public class ClipRecorder : MonoBehaviour
{
    public static ClipRecorder Current;
    public string Dir;
    public float Fps = 15f;
    public int SuperSize = 1;   // 2 なら画面の2倍の解像度で書き出す
    public int Frames;
    private float _next, _t0;
    private readonly List<string> _times = new List<string>();
    private readonly List<float> _samples = new List<float>(1 << 22);
    private int _channels = 2;
    private bool _on;
    private static int _pending;

    public static ClipRecorder Begin(string dir, float fps) { return Begin(dir, fps, 1); }

    public static ClipRecorder Begin(string dir, float fps, int superSize)
    {
        End();
        var lis = FindObjectOfType<AudioListener>();
        GameObject host = lis != null ? lis.gameObject : new GameObject("ClipRecorder");
        var r = host.AddComponent<ClipRecorder>();
        r.Dir = dir; r.Fps = fps; r.SuperSize = Mathf.Max(1, superSize);
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
        r._t0 = Time.realtimeSinceStartup; r._next = r._t0;
        r._on = true;
        r.StartCoroutine(r.Loop());
        Current = r;
        return r;
    }

    public static string End()
    {
        var r = Current;
        if (r == null) return "none";
        Current = null;
        r._on = false;
        r.StopAllCoroutines();
        for (int i = 0; i < 300 && _pending > 0; i++) System.Threading.Thread.Sleep(10);   // 書き込みの終わりを待つ
        string info;
        lock (r._samples) { info = r.WriteWav(); }
        File.WriteAllLines(Path.Combine(r.Dir, "times.txt"), r._times.ToArray());
        Destroy(r);
        return "frames=" + r.Frames + " " + info;
    }

    private IEnumerator Loop()
    {
        var eof = new WaitForEndOfFrame();
        while (_on)
        {
            yield return eof;
            float now = Time.realtimeSinceStartup;
            if (now < _next) continue;
            _next += 1f / Fps;
            if (_next < now) _next = now + 1f / Fps;
            var tex = ScreenCapture.CaptureScreenshotAsTexture(SuperSize);
            string name = "f" + Frames.ToString("D5") + ".jpg";
            // JPG にする処理と書き込みは別スレッドで(録画中の試合を重くしない)
            byte[] raw = tex.GetRawTextureData();
            var fmt = tex.graphicsFormat; uint w = (uint)tex.width, h = (uint)tex.height;
            string path = Path.Combine(Dir, name);
            Destroy(tex);
            System.Threading.Interlocked.Increment(ref _pending);
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try { File.WriteAllBytes(path, ImageConversion.EncodeArrayToJPG(raw, fmt, w, h, 0, 88)); }
                finally { System.Threading.Interlocked.Decrement(ref _pending); }
            });
            _times.Add(name + " " + (now - _t0).ToString("F3"));
            Frames++;
        }
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (!_on) return;
        _channels = channels;
        lock (_samples) { _samples.AddRange(data); }
    }

    private string WriteWav()
    {
        int rate = AudioSettings.outputSampleRate;
        string path = Path.Combine(Dir, "audio.wav");
        using (var fs = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            int n = _samples.Count;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
            w.Write((short)1); w.Write((short)_channels); w.Write(rate);
            w.Write(rate * _channels * 2); w.Write((short)(_channels * 2)); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
            for (int i = 0; i < n; i++) w.Write((short)(Mathf.Clamp(_samples[i], -1f, 1f) * 32767f));
        }
        return "audio=" + (_samples.Count / (float)(rate * _channels)).ToString("F1") + "s rate=" + rate + " ch=" + _channels;
    }
}
#endif
