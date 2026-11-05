using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 文字の仕上げ: 上が明るく下が濃いグラデーション + 表示した瞬間に大きく出て弾んで止まる動き。
// Text と同じ GameObject に付ける。文字の中身が変わったら自動で弾む。
[RequireComponent(typeof(Text))]
public class UiFxText : BaseMeshEffect
{
    public float TopWhite = 0.5f;     // 上をどれだけ白に寄せるか
    public float BottomDark = 0.8f;   // 下の明るさ
    public float PopScale = 1.55f;
    public float PopTime = 0.28f;

    private Text _text;
    private string _last = "";
    private float _t0 = -10f;

    protected override void Awake() { base.Awake(); _text = GetComponent<Text>(); }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;
        var verts = new List<UIVertex>();
        vh.GetUIVertexStream(verts);
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < verts.Count; i++) { minY = Mathf.Min(minY, verts[i].position.y); maxY = Mathf.Max(maxY, verts[i].position.y); }
        float span = Mathf.Max(1f, maxY - minY);
        for (int i = 0; i < verts.Count; i++)
        {
            var v = verts[i];
            float k = (v.position.y - minY) / span;
            Color c = v.color;
            // 縁取り(黒っぽい頂点)はそのまま
            if (c.r + c.g + c.b < 0.3f) continue;
            Color top = Color.Lerp(c, Color.white, TopWhite);
            Color bot = new Color(c.r * BottomDark, c.g * BottomDark, c.b * BottomDark, c.a);
            Color g = Color.Lerp(bot, top, k); g.a = c.a;
            v.color = g;
            verts[i] = v;
        }
        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }

    private void LateUpdate()
    {
        if (_text == null) return;
        if (_text.text != _last)
        {
            _last = _text.text;
            if (!string.IsNullOrEmpty(_last)) _t0 = Time.unscaledTime;
        }
        float u = (Time.unscaledTime - _t0) / PopTime;
        float s = 1f;
        if (u < 1f)
        {
            // 大きく出て、少し縮みすぎてから止まる
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(u - 1f, 3f) + c1 * Mathf.Pow(u - 1f, 2f);
            s = Mathf.LerpUnclamped(PopScale, 1f, e);
        }
        transform.localScale = new Vector3(s, s, 1f);
    }
}
