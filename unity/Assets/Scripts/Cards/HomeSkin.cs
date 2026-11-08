using UnityEngine;
using UnityEngine.UI;

// ============================================================
// HomeSkin.cs
// ホーム画面の部品の絵(角の丸い板・ボタン・バナーの背景)をコードで描く。
// 板は9スライスなので、どの大きさにも伸ばせる。
// ============================================================
public static class HomeSkin
{
    private static Sprite _bar, _tabOn, _orange, _pill, _badge, _cta;
    private static readonly System.Collections.Generic.Dictionary<int, Sprite> _banners = new System.Collections.Generic.Dictionary<int, Sprite>();

    private static readonly Color NavyLine = new Color(0.05f, 0.05f, 0.22f, 1f);

    // 上と下の帯(青紫のつやのある板)
    public static Sprite Bar
    {
        get
        {
            if (_bar == null) _bar = Panel(new Color(0.27f, 0.28f, 0.74f), new Color(0.11f, 0.11f, 0.40f), new Color(0.58f, 0.66f, 1f), NavyLine, 26f, 0.94f);
            return _bar;
        }
    }

    // 選んでいるメニュー(水色)
    public static Sprite TabOn
    {
        get
        {
            if (_tabOn == null) _tabOn = Panel(new Color(0.38f, 0.82f, 1f), new Color(0.07f, 0.40f, 0.92f), new Color(0.86f, 0.96f, 1f), NavyLine, 20f, 1f);
            return _tabOn;
        }
    }

    // 試合のボタン(橙)
    public static Sprite Orange
    {
        get
        {
            if (_orange == null) _orange = Panel(new Color(1f, 0.80f, 0.20f), new Color(1f, 0.40f, 0.04f), new Color(1f, 0.97f, 0.68f), new Color(0.42f, 0.13f, 0f, 1f), 30f, 1f);
            return _orange;
        }
    }

    // 所持数の札(紺の丸い札)
    public static Sprite Pill
    {
        get
        {
            if (_pill == null) _pill = Panel(new Color(0.10f, 0.12f, 0.34f), new Color(0.03f, 0.04f, 0.17f), new Color(0.38f, 0.45f, 0.90f), NavyLine, 21f, 0.92f, false);
            return _pill;
        }
    }

    // 数のしるし(赤い丸)
    public static Sprite Badge
    {
        get
        {
            if (_badge == null) _badge = Panel(new Color(1f, 0.35f, 0.38f), new Color(0.82f, 0.05f, 0.12f), Color.white, new Color(0.35f, 0f, 0.04f, 1f), 15f, 1f);
            return _badge;
        }
    }

    // バナーの中の押すところ(桃色の札)
    public static Sprite Cta
    {
        get
        {
            if (_cta == null) _cta = Panel(new Color(1f, 0.48f, 0.60f), new Color(0.86f, 0.08f, 0.34f), new Color(1f, 0.93f, 0.95f), new Color(0.40f, 0f, 0.10f, 1f), 19f, 1f);
            return _cta;
        }
    }

    private static float SdBox(float px, float py, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(px) - (hx - r), qy = Mathf.Abs(py) - (hy - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    private static Color Over(Color dst, Color src, float a)
    {
        a *= src.a;
        float oa = a + dst.a * (1f - a);
        if (oa < 1e-5f) return new Color(0f, 0f, 0f, 0f);
        return new Color((src.r * a + dst.r * dst.a * (1f - a)) / oa, (src.g * a + dst.g * dst.a * (1f - a)) / oa,
            (src.b * a + dst.b * dst.a * (1f - a)) / oa, oa);
    }

    // 全画面のボタン(主: 橙 / ふつう: 青)。小さいボタンにも合うよう角の丸みを小さく
    private static Sprite _btnMain, _btnSub;
    public static Sprite BtnMain
    {
        get
        {
            if (_btnMain == null) _btnMain = Panel(new Color(1f, 0.80f, 0.20f), new Color(1f, 0.40f, 0.04f), new Color(1f, 0.97f, 0.68f), new Color(0.42f, 0.13f, 0f, 1f), 14f, 1f);
            return _btnMain;
        }
    }
    public static Sprite BtnSub
    {
        get
        {
            if (_btnSub == null) _btnSub = Panel(new Color(0.30f, 0.34f, 0.84f), new Color(0.12f, 0.12f, 0.44f), new Color(0.62f, 0.70f, 1f), NavyLine, 14f, 1f);
            return _btnSub;
        }
    }

    // 足もとの影(ぼけた楕円)
    private static Sprite _shadow;
    public static Sprite Shadow
    {
        get
        {
            if (_shadow != null) return _shadow;
            int w = 128, h = 32;
            var cv = new ArtCanvas(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = ((x + 0.5f) / w - 0.5f) * 2f, dy = ((y + 0.5f) / h - 0.5f) * 2f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    cv.Px[y * w + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
                }
            _shadow = cv.ToSprite(100f, new Vector2(0.5f, 0.5f));
            return _shadow;
        }
    }

    // 角の丸い板: 外側の濃い線 → 明るい縁 → 上下のグラデーション(上半分につや)
    private static Sprite Panel(Color top, Color bottom, Color rim, Color line, float r, float alpha, bool gloss = true)
    {
        int n = Mathf.CeilToInt(r * 2f + 24f);
        var cv = new ArtCanvas(n, n);
        float h = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float px = x + 0.5f - h, py = y + 0.5f - h;
                float sd = SdBox(px, py, h - 1f, h - 1f, r);
                float cov = Mathf.Clamp01(0.5f - sd);
                if (cov <= 0f) continue;
                float v = (y + 0.5f) / n;                 // 0=下 1=上
                Color c = line;
                if (sd < -2.2f) c = Color.Lerp(line, rim, Mathf.Clamp01(-sd - 2.2f));
                if (sd < -5f)
                {
                    Color g = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, v));
                    if (gloss && v > 0.55f) g = Color.Lerp(g, Color.white, 0.16f * Mathf.Clamp01((v - 0.55f) * 6f));
                    c = Color.Lerp(rim, g, Mathf.Clamp01(-sd - 5f));
                    // 縁のすぐ内側の細い光(上側だけ)
                    float hl = Mathf.Clamp01(1f - Mathf.Abs(sd + 7f) / 1.4f) * Mathf.Clamp01((v - 0.5f) * 4f);
                    c = Color.Lerp(c, Color.white, hl * 0.35f);
                    c.a = alpha;
                }
                cv.Px[y * n + x] = new Color(c.r, c.g, c.b, (c.a <= 0f ? 1f : c.a) * cov);
            }
        float b = r + 8f;
        return cv.ToSprite(100f, new Vector2(0.5f, 0.5f), new Vector4(b, b, b, b));
    }

    // バナーの背景(斜めのグラデーション・右側の光・放射光・きらめき・白い縁)。色ごとに1枚
    public static Sprite Banner(Color a, Color b, Color glow, int seed)
    {
        Sprite s;
        if (_banners.TryGetValue(seed, out s)) return s;
        int w = 440, hgt = 200;
        float r = 22f;
        var cv = new ArtCanvas(w, hgt);
        var rnd = new System.Random(seed);
        var sparks = new Vector3[26];
        for (int i = 0; i < sparks.Length; i++)
            sparks[i] = new Vector3((float)rnd.NextDouble() * w, (float)rnd.NextDouble() * hgt, 1.2f + (float)rnd.NextDouble() * 2.4f);
        Vector2 gc = new Vector2(w * 0.74f, hgt * 0.52f);
        for (int y = 0; y < hgt; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f - w * 0.5f, py = y + 0.5f - hgt * 0.5f;
                float sd = SdBox(px, py, w * 0.5f - 1f, hgt * 0.5f - 1f, r);
                float cov = Mathf.Clamp01(0.5f - sd);
                if (cov <= 0f) continue;
                float u = (x + 0.5f) / w, v = (y + 0.5f) / hgt;
                Color c = Color.Lerp(a, b, Mathf.Clamp01(u * 0.75f + (1f - v) * 0.35f));
                float dx = (x - gc.x) / w, dy = (y - gc.y) / hgt * 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(y - gc.y, x - gc.x);
                float ray = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(ang * 16f), 4f);
                c = Color.Lerp(c, glow, Mathf.Clamp01(0.55f - d * 1.6f) + ray * 0.16f * Mathf.Clamp01(1f - d * 1.4f));
                for (int i = 0; i < sparks.Length; i++)
                {
                    float sx = x - sparks[i].x, sy = y - sparks[i].y;
                    float sp = Mathf.Clamp01(1f - Mathf.Sqrt(sx * sx + sy * sy) / sparks[i].z);
                    if (sp > 0f) c = Color.Lerp(c, Color.white, sp * 0.8f);
                }
                if (v > 0.62f) c = Color.Lerp(c, Color.white, 0.08f * (v - 0.62f) / 0.38f);
                // 縁: 外の紺 → 白
                if (sd > -2f) c = NavyLine;
                else if (sd > -6f) c = Color.Lerp(new Color(1f, 1f, 1f), new Color(0.9f, 0.92f, 1f), Mathf.Clamp01(-sd - 2f) / 4f);
                c.a = cov;
                cv.Px[y * w + x] = c;
            }
        s = cv.ToSprite(100f, new Vector2(0.5f, 0.5f));
        _banners[seed] = s;
        return s;
    }

    // 文字の仕上げ: 白い文字 + 紺の縁(下のメニュー・所持数)
    public static Text NavText(Text t, float outer = 2.4f)
    {
        var fx = TelopFx.Apply(t, TelopFx.Kind.Label);
        fx.Outer = NavyLine;
        fx.OuterWidth = outer;
        fx.TopWhite = 0.1f; fx.BottomDark = 0.93f;
        t.SetVerticesDirty();
        return t;
    }

    // 見出し: 白→黄のグラデーション + 濃い色の太い縁 + 白い外縁
    public static Text Title(Text t, Color inner, Color fillBottom, float innerW = 4f)
    {
        var fx = TelopFx.Apply(t, TelopFx.Kind.Heading);
        fx.UseTextColor = false;
        fx.FillTop = Color.white;
        fx.FillBottom = fillBottom;
        fx.InnerWidth = innerW;
        fx.Inner = inner;
        fx.OuterWidth = 2f;
        fx.Outer = Color.white;
        fx.Skew = 0.1f;
        fx.Shadow = new Color(0f, 0f, 0.1f, 0.5f);
        fx.ShadowOffset = new Vector2(2f, -3.5f);
        t.SetVerticesDirty();
        return t;
    }
}
