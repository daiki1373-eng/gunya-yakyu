using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 文字をぐにゃぐにゃ揺らす(タイトルロゴ用)。横の位置ごとに少しずつずれた波で上下させ、文字ごとに伸び縮みさせる。
// TelopFx(縁取り)より後ろに付ける。縁取りの写しも同じ位置の波で動くので、縁がずれない。
[RequireComponent(typeof(Graphic))]
public class JellyText : BaseMeshEffect
{
    public float Amp = 4f;        // 上下の揺れ(画素)
    public float Speed = 3.2f;
    public float Wave = 0.03f;    // 横1画素あたりの位相
    public float Stretch = 0.06f; // 縦の伸び縮み
    public float Boost;           // 一時的に強く揺らす(着地の瞬間など)。毎秒減る

    private static readonly List<UIVertex> _buf = new List<UIVertex>(2048);

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;
        _buf.Clear();
        vh.GetUIVertexStream(_buf);
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < _buf.Count; i++) { float y = _buf[i].position.y; if (y < minY) minY = y; if (y > maxY) maxY = y; }
        float mid = (minY + maxY) * 0.5f;
        float t = Time.unscaledTime * Speed;
        float amp = Amp * (1f + Boost * 4f);
        for (int i = 0; i < _buf.Count; i++)
        {
            var v = _buf[i];
            Vector3 p = v.position;
            float ph = t + p.x * Wave;
            float st = 1f + Mathf.Sin(ph * 1.3f + 1.1f) * Stretch * (1f + Boost * 3f);
            p.y = mid + (p.y - mid) * st + Mathf.Sin(ph) * amp;
            v.position = p;
            _buf[i] = v;
        }
        vh.Clear();
        vh.AddUIVertexTriangleStream(_buf);
    }

    private void Update()
    {
        if (Boost > 0f) Boost = Mathf.Max(0f, Boost - Time.unscaledDeltaTime * 1.6f);
        if (graphic != null) graphic.SetVerticesDirty();
    }
}
