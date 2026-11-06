using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// TelopFx.cs
// 試合のテロップ(文字)の仕上げ。Text と同じ GameObject に付ける。
// ・文字ごとの縦グラデーション、2重の縁取り(内側の太い縁 + 外側の細い縁)、落ち影、斜体
// ・出るときの動き(弾む / はんこを押す / 横から滑り込む)
// ・後ろの札(Plate)を文字の幅に合わせて伸ばし、左右に開いて出す
// 文字の中身が変わると自動で動きを始める。同じ文字をもう一度出すときは Restart()。
// ============================================================
[RequireComponent(typeof(Text))]
public class TelopFx : BaseMeshEffect
{
    public enum Anim { None, Pop, Stamp, Slide }
    public enum Kind { Plain, Rank, Comment, Stamp, Gold }

    public Anim Mode = Anim.Pop;
    public bool UseTextColor = true;        // true: Text の色(リッチテキストの色も)から塗りを作る
    public float TopWhite = 0.5f, BottomDark = 0.85f;
    public Color FillTop = Color.white, FillBottom = Color.white;
    public float InnerWidth = 4f;
    public bool InnerFromText;              // true: 内側の縁を文字色を暗くした色にする
    public float InnerDark = 0.25f;
    public Color Inner = new Color(0.05f, 0.05f, 0.1f, 1f);
    public float OuterWidth = 2f;           // 内側の縁のさらに外
    public Color Outer = Color.white;
    public Color Shadow = new Color(0f, 0f, 0f, 0.45f);
    public Vector2 ShadowOffset = new Vector2(3f, -4f);
    public float Skew = 0.15f;              // 斜体の傾き(高さ1あたりの横ずれ)
    public float Rotation;                  // 止まったときの傾き(度)
    public float PopScale = 1.5f, PopTime = 0.3f;
    public float SlideFrom = -90f;

    public RectTransform Plate;             // 後ろの札(なくてもよい)
    public float PlatePad = 140f, PlateMinW = 260f;

    private Text _text;
    private RectTransform _rt;
    private Image _plateImg;
    private float _plateAlpha = 1f;
    private Vector2 _base;
    private bool _baseSet;
    private string _last = "";
    private float _t0 = -10f;

    private static readonly List<UIVertex> _src = new List<UIVertex>(256);
    private static readonly List<UIVertex> _fill = new List<UIVertex>(256);
    private static readonly List<Color> _orig = new List<Color>(256);
    private static readonly List<UIVertex> _dst = new List<UIVertex>(4096);
    private static Vector2[] _dir12, _dir8;

    public void Preset(Kind k)
    {
        switch (k)
        {
            case Kind.Plain:      // 札の上に乗る文字(結果・球速・リプレイ)
                Mode = Anim.Pop; PopScale = 1.12f; PopTime = 0.22f;
                UseTextColor = true; TopWhite = 0f; BottomDark = 0.86f;
                InnerWidth = 0f; OuterWidth = 0f; Skew = 0.06f;
                Shadow = new Color(0f, 0f, 0f, 0.6f); ShadowOffset = new Vector2(1.5f, -2f);
                break;
            case Kind.Rank:       // 打撃の判定(PERFECT / GOOD …)
                Mode = Anim.Pop; PopScale = 1.6f; PopTime = 0.3f;
                UseTextColor = true; TopWhite = 0.6f; BottomDark = 0.82f;
                InnerWidth = 4f; InnerFromText = true; InnerDark = 0.22f;
                OuterWidth = 2f; Outer = Color.white; Skew = 0.2f;
                Shadow = new Color(0f, 0f, 0f, 0.4f); ShadowOffset = new Vector2(3f, -4f);
                break;
            case Kind.Comment:    // 実況(バラエティ番組の字幕のような太い縁)
                Mode = Anim.Slide; PopTime = 0.24f; SlideFrom = -90f;
                UseTextColor = false; FillTop = Color.white; FillBottom = new Color(1f, 0.9f, 0.32f);
                InnerWidth = 5.5f; InnerFromText = false; Inner = new Color(0.72f, 0.04f, 0.07f, 1f);
                OuterWidth = 3f; Outer = Color.white; Skew = 0.12f;
                Shadow = new Color(0f, 0f, 0f, 0.45f); ShadowOffset = new Vector2(3f, -4f);
                break;
            case Kind.Stamp:      // 珍プレー(はんこを押すように出る)
                Mode = Anim.Stamp; PopScale = 2.4f; PopTime = 0.5f; Rotation = -6f;
                UseTextColor = false; FillTop = new Color(1f, 1f, 0.88f); FillBottom = new Color(1f, 0.68f, 0.08f);
                InnerWidth = 5f; InnerFromText = false; Inner = new Color(0.80f, 0.06f, 0.08f, 1f);
                OuterWidth = 3f; Outer = new Color(0.18f, 0f, 0.02f, 1f); Skew = 0.1f;
                Shadow = new Color(0f, 0f, 0f, 0.5f); ShadowOffset = new Vector2(4f, -5f);
                break;
            case Kind.Gold:       // 得点・コイン
                Mode = Anim.Pop; PopScale = 1.5f; PopTime = 0.3f;
                UseTextColor = false; FillTop = new Color(1f, 1f, 0.86f); FillBottom = new Color(1f, 0.72f, 0.1f);
                InnerWidth = 4f; InnerFromText = false; Inner = new Color(0.36f, 0.02f, 0.04f, 1f);
                OuterWidth = 1.5f; Outer = new Color(1f, 0.95f, 0.8f, 1f); Skew = 0.12f;
                Shadow = new Color(0f, 0f, 0f, 0.45f); ShadowOffset = new Vector2(3f, -4f);
                break;
        }
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public void SetPlate(RectTransform plate)
    {
        Plate = plate;
        _plateImg = plate != null ? plate.GetComponent<Image>() : null;
        _plateAlpha = _plateImg != null ? _plateImg.color.a : 1f;
    }

    // 同じ文字のままもう一度出す
    public void Restart() { _t0 = Time.unscaledTime; }

    protected override void Awake()
    {
        base.Awake();
        _text = GetComponent<Text>();
        _rt = (RectTransform)transform;
    }

    private static void EnsureDirs()
    {
        if (_dir12 != null) return;
        _dir12 = new Vector2[12];
        for (int i = 0; i < 12; i++) { float a = i * Mathf.PI * 2f / 12f; _dir12[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
        _dir8 = new Vector2[8];
        for (int i = 0; i < 8; i++) { float a = i * Mathf.PI * 2f / 8f; _dir8[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;
        EnsureDirs();
        _src.Clear();
        vh.GetUIVertexStream(_src);
        int n = _src.Count - _src.Count % 6;
        if (n == 0) return;

        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < n; i++)
        {
            float y = _src[i].position.y;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
        float midY = (minY + maxY) * 0.5f;

        // 塗り: 1文字ごとの縦グラデーション + 斜体
        _fill.Clear();
        _orig.Clear();
        for (int q = 0; q < n; q += 6)
        {
            float qa = float.MaxValue, qb = float.MinValue;
            for (int j = 0; j < 6; j++)
            {
                float y = _src[q + j].position.y;
                if (y < qa) qa = y;
                if (y > qb) qb = y;
            }
            float span = Mathf.Max(1f, qb - qa);
            for (int j = 0; j < 6; j++)
            {
                var v = _src[q + j];
                Vector3 p = v.position;
                float k = (p.y - qa) / span;
                p.x += (p.y - midY) * Skew;
                v.position = p;
                Color c = v.color;
                Color top, bot;
                if (UseTextColor)
                {
                    top = Color.Lerp(c, Color.white, TopWhite);
                    bot = new Color(c.r * BottomDark, c.g * BottomDark, c.b * BottomDark, 1f);
                }
                else { top = FillTop; bot = FillBottom; }
                Color g = Color.Lerp(bot, top, k);
                g.a = c.a;
                v.color = g;
                _fill.Add(v);
                _orig.Add(c);
            }
        }

        _dst.Clear();
        float outerR = InnerWidth + OuterWidth;
        if (Shadow.a > 0.001f) Ring(ShadowOffset, outerR, _dir8, Shadow, false);
        if (OuterWidth > 0.01f) Ring(Vector2.zero, outerR, _dir12, Outer, false);
        if (InnerWidth > 0.01f) Ring(Vector2.zero, InnerWidth, _dir12, Inner, InnerFromText);
        _dst.AddRange(_fill);
        vh.Clear();
        vh.AddUIVertexTriangleStream(_dst);
    }

    private void Ring(Vector2 off, float r, Vector2[] dirs, Color col, bool fromText)
    {
        int count = r > 0.01f ? dirs.Length : 1;
        for (int d = 0; d < count; d++)
        {
            Vector3 o = r > 0.01f ? (Vector3)(off + dirs[d] * r) : (Vector3)off;
            for (int i = 0; i < _fill.Count; i++)
            {
                var v = _fill[i];
                Color src = _orig[i];
                Color c = fromText
                    ? new Color(src.r * InnerDark + 0.02f, src.g * InnerDark + 0.02f, src.b * InnerDark + 0.05f, col.a)
                    : col;
                c.a = col.a * src.a;
                v.color = c;
                v.position += o;
                _dst.Add(v);
            }
        }
    }

    private void LateUpdate()
    {
        if (_text == null) return;
        if (!_baseSet) { _base = _rt.anchoredPosition; _baseSet = true; }
        string s = _text.text;
        if (s != _last)
        {
            _last = s;
            if (!string.IsNullOrEmpty(s)) _t0 = Time.unscaledTime;
        }
        bool on = !string.IsNullOrEmpty(s);
        float u = (Time.unscaledTime - _t0) / Mathf.Max(0.01f, PopTime);
        float sc = 1f, rot = Rotation;
        Vector2 pos = _base;
        if (on && u < 1f)
        {
            switch (Mode)
            {
                case Anim.Pop:
                {
                    // 大きく出て、少し縮みすぎてから止まる
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    float e = 1f + c3 * Mathf.Pow(u - 1f, 3f) + c1 * Mathf.Pow(u - 1f, 2f);
                    sc = Mathf.LerpUnclamped(PopScale, 1f, e);
                    break;
                }
                case Anim.Stamp:
                {
                    // 前半で勢いよく押し付け、後半は小さく揺れて止まる
                    if (u < 0.4f)
                    {
                        float e = u / 0.4f;
                        sc = Mathf.Lerp(PopScale, 1f, e * e);
                        rot = Rotation + (1f - e) * 14f;
                    }
                    else
                    {
                        float w = (u - 0.4f) / 0.6f;
                        float amp = (1f - w) * 6f;
                        pos += new Vector2(Mathf.Sin(w * 70f), Mathf.Cos(w * 53f)) * amp;
                        sc = 1f + Mathf.Sin(w * 22f) * (1f - w) * 0.05f;
                    }
                    break;
                }
                case Anim.Slide:
                {
                    float e = 1f - (1f - u) * (1f - u) * (1f - u);
                    pos.x += SlideFrom * (1f - e);
                    sc = Mathf.Lerp(1.15f, 1f, e);
                    break;
                }
            }
        }
        _rt.localScale = new Vector3(sc, sc, 1f);
        _rt.localRotation = Quaternion.Euler(0f, 0f, rot);
        _rt.anchoredPosition = pos;

        if (Plate != null)
        {
            if (Plate.gameObject.activeSelf != on) Plate.gameObject.SetActive(on);
            if (on)
            {
                float w = Mathf.Max(PlateMinW, _text.preferredWidth + PlatePad);
                Plate.sizeDelta = new Vector2(w, Plate.sizeDelta.y);
                float pu = Mathf.Clamp01((Time.unscaledTime - _t0) / 0.2f);
                float pe = 1f - (1f - pu) * (1f - pu);
                Plate.localScale = new Vector3(pe, 1f, 1f);
                if (_plateImg != null)
                {
                    var c = _plateImg.color;
                    c.a = _plateAlpha * _text.color.a;
                    _plateImg.color = c;
                }
            }
        }
    }

    // 札の絵: 左右がぼけて消える紺のガラス帯 + 上下の金の線。色ごとに1枚だけ作る。
    private static readonly Dictionary<string, Sprite> _plates = new Dictionary<string, Sprite>();

    public static Sprite PlateSprite(Color body, Color line)
    {
        string key = ColorUtility.ToHtmlStringRGBA(body) + ColorUtility.ToHtmlStringRGBA(line);
        Sprite s;
        if (_plates.TryGetValue(key, out s) && s != null) return s;
        const int W = 256, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            float fy = y / (H - 1f);
            float m = Mathf.Lerp(0.72f, 1.18f, fy);
            for (int x = 0; x < W; x++)
            {
                float fx = x / (W - 1f);
                Color c;
                if (y < 3 || y >= H - 3)
                {
                    float le = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fx / 0.07f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - fx) / 0.07f));
                    c = new Color(line.r, line.g, line.b, line.a * le);
                }
                else
                {
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fx / 0.16f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - fx) / 0.16f));
                    c = new Color(Mathf.Clamp01(body.r * m), Mathf.Clamp01(body.g * m), Mathf.Clamp01(body.b * m), body.a * edge);
                }
                px[y * W + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        _plates[key] = s;
        return s;
    }
}
