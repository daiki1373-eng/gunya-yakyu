using System;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UiKit.cs
// カード/ガチャ画面用のUI部品と、金×赤の装飾画像(すべてコードで生成)。
// ============================================================
public static class UiKit
{
    public static readonly Color Gold = new Color(0.91f, 0.73f, 0.14f);      // #e8b923 相当
    public static readonly Color GoldLight = new Color(1f, 0.93f, 0.55f);
    public static readonly Color Red = new Color(0.78f, 0.08f, 0.10f);
    public static readonly Color RedDark = new Color(0.32f, 0.02f, 0.04f);
    public static readonly Color Navy = new Color(0.043f, 0.082f, 0.149f);    // #0b1526

    private static Font _font;
    public static Font Font { get { if (_font == null) _font = BattingUI.GetFont(); return _font; } }

    public static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static RectTransform Fill(RectTransform r, float inset = 0f)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset);
        r.offsetMax = new Vector2(-inset, -inset);
        return r;
    }

    public static RectTransform Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return r;
    }

    public static Image Img(Transform parent, string name, Sprite s, Color c, bool raycast = false)
    {
        var r = Rect(parent, name);
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = s;
        img.color = c;
        img.raycastTarget = raycast;
        if (s != null && s.border != Vector4.zero) img.type = Image.Type.Sliced;
        return img;
    }

    public static Text Txt(Transform parent, string name, string text, int size, Color c, TextAnchor a,
        bool bold = true, bool outline = true)
    {
        var r = Rect(parent, name);
        var t = r.gameObject.AddComponent<Text>();
        t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.color = c;
        t.alignment = a;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        if (outline)
        {
            var o = r.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.15f, 0f, 0f, 0.9f);
            o.effectDistance = new Vector2(1.6f, -1.6f);
        }
        return t;
    }

    public static Button Btn(Transform parent, string name, string label, Vector2 size, bool primary, Action onClick,
        int fontSize = 26)
    {
        var img = Img(parent, name, primary ? ButtonRed : ButtonDark, Color.white, true);
        img.rectTransform.sizeDelta = size;
        var b = img.gameObject.AddComponent<Button>();
        var cb = b.colors;
        cb.highlightedColor = new Color(1f, 0.95f, 0.85f);
        cb.pressedColor = new Color(0.8f, 0.7f, 0.6f);
        b.colors = cb;
        b.onClick.AddListener(() => { GameAudio.Play(Sfx.Click, 0.6f, 1.2f); if (onClick != null) onClick(); });
        var t = Txt(img.transform, "Label", label, fontSize, primary ? Color.white : GoldLight, TextAnchor.MiddleCenter);
        Fill(t.rectTransform);
        return b;
    }

    // ---- 生成画像 ------------------------------------------------------
    private static Sprite _rays, _frame, _btnRed, _btnDark, _bgRadial, _shine, _plate;

    // 放射状の光(白。色はImage側で付ける)
    public static Sprite Rays
    {
        get
        {
            if (_rays != null) return _rays;
            int n = 512;
            var cv = new ArtCanvas(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = Mathf.Atan2(dy, dx);
                    float ray = 0.5f + 0.5f * Mathf.Cos(a * 18f);
                    ray = ray * ray * ray;
                    float fall = Mathf.Clamp01(1f - d);
                    float core = Mathf.Clamp01(1f - d * 2.2f);
                    float alpha = fall * (0.25f + 0.75f * ray) * Mathf.Clamp01(d * 6f) + core * core * 0.6f;
                    cv.Px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }
            _rays = cv.ToSprite(100f, new Vector2(0.5f, 0.5f));
            return _rays;
        }
    }

    // 背景: 中心が明るい赤、外側が暗い赤
    public static Sprite BgRadial
    {
        get
        {
            if (_bgRadial != null) return _bgRadial;
            int n = 256;
            var cv = new ArtCanvas(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = ((x + 0.5f) / n - 0.5f) * 2f, dy = ((y + 0.5f) / n - 0.45f) * 2f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx * 0.7f + dy * dy));
                    Color c = Color.Lerp(new Color(0.85f, 0.20f, 0.08f), new Color(0.22f, 0.01f, 0.03f), d);
                    c = Color.Lerp(c, new Color(1f, 0.72f, 0.25f), Mathf.Clamp01(0.35f - d) * 1.6f);
                    cv.Px[y * n + x] = c;
                }
            _bgRadial = cv.ToSprite(100f, new Vector2(0.5f, 0.5f));
            return _bgRadial;
        }
    }

    // カード枠(9スライス。白〜灰の立体枠を色で染める)
    public static Sprite Frame
    {
        get
        {
            if (_frame != null) return _frame;
            int n = 64;
            var cv = new ArtCanvas(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int e = Mathf.Min(Mathf.Min(x, n - 1 - x), Mathf.Min(y, n - 1 - y));
                    float v = y / (float)(n - 1);
                    Color c;
                    if (e < 2) c = new Color(0.12f, 0.08f, 0.05f, 1f);
                    else if (e < 11)
                    {
                        float k = Mathf.Lerp(0.62f, 1f, v) * (1f - 0.18f * Mathf.Abs((e - 6.5f) / 4.5f));
                        c = new Color(k, k, k * 0.96f, 1f);
                    }
                    else if (e < 13) c = new Color(0.10f, 0.06f, 0.04f, 1f);
                    else c = new Color(0f, 0f, 0f, 0f);
                    cv.Px[y * n + x] = c;
                }
            _frame = cv.ToSprite(100f, new Vector2(0.5f, 0.5f), new Vector4(14, 14, 14, 14));
            return _frame;
        }
    }

    private static Sprite MakeButton(Color top, Color bottom, Color border)
    {
        int w = 96, h = 48;
        var cv = new ArtCanvas(w, h);
        float rr = 12f / h;
        cv.RoundRect(0f, 0f, 1f, 1f, 0.12f, new Color(0.12f, 0.05f, 0.02f));
        cv.RoundRect(2f / w, 2f / h, 1f - 2f / w, 1f - 2f / h, 0.11f, border);
        cv.RoundRect(6f / w, 6f / h, 1f - 6f / w, 1f - 6f / h, 0.08f, Color.white);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                Color c = cv.Px[i];
                if (c.r > 0.99f && c.g > 0.99f && c.b > 0.99f)
                {
                    float v = y / (float)(h - 1);
                    Color g = Color.Lerp(bottom, top, v);
                    if (v > 0.62f) g = Color.Lerp(g, Color.white, 0.18f); // つや
                    g.a = c.a;
                    cv.Px[i] = g;
                }
            }
        return cv.ToSprite(100f, new Vector2(0.5f, 0.5f), new Vector4(16, 16, 16, 16));
    }

    public static Sprite ButtonRed
    {
        get
        {
            if (_btnRed == null) _btnRed = MakeButton(new Color(0.95f, 0.22f, 0.16f), new Color(0.55f, 0.03f, 0.05f), Gold);
            return _btnRed;
        }
    }

    public static Sprite ButtonDark
    {
        get
        {
            if (_btnDark == null) _btnDark = MakeButton(new Color(0.22f, 0.10f, 0.08f), new Color(0.08f, 0.03f, 0.03f), Gold * 0.8f);
            return _btnDark;
        }
    }

    // ---- カードの背景(★1〜★3用)。上のレア度ほど派手に(★1は地味、★4以上は元の豪華な絵を使う) ----
    private static readonly Sprite[] _rarityBg = new Sprite[8];
    public static Sprite RarityBg(Rarity r)
    {
        int k = (int)r;
        if (_rarityBg[k] != null) return _rarityBg[k];
        int w = 160, h = 240;
        var cv = new ArtCanvas(w, h);
        Color inner, outer; float rays;
        switch (r)
        {
            case Rarity.R1: inner = new Color(0.30f, 0.34f, 0.42f); outer = new Color(0.10f, 0.12f, 0.17f); rays = 0f; break;
            case Rarity.R2: inner = new Color(0.72f, 0.45f, 0.25f); outer = new Color(0.20f, 0.10f, 0.06f); rays = 0.12f; break;
            case Rarity.R3: inner = new Color(0.40f, 0.62f, 1.00f); outer = new Color(0.06f, 0.12f, 0.32f); rays = 0.25f; break;
            default: inner = new Color(1f, 0.80f, 0.35f); outer = new Color(0.45f, 0.04f, 0.04f); rays = 0.4f; break;
        }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w - 0.5f, dy = ((y + 0.5f) / h - 0.62f) * 1.5f;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 1.6f);
                Color c = Color.Lerp(inner, outer, d);
                if (rays > 0f)
                {
                    float a = Mathf.Atan2(dy, dx);
                    float ray = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(a * 14f), 3f);
                    c = Color.Lerp(c, Color.Lerp(inner, Color.white, 0.4f), ray * rays * (1f - d));
                }
                if (r == Rarity.R1 && ((x + y) / 12) % 2 == 0) c *= 0.96f;   // 地味な斜めじま(芝の刈り目)
                c.a = 1f;
                cv.Px[y * w + x] = c;
            }
        _rarityBg[k] = cv.ToSprite(100f, new Vector2(0.5f, 0.5f));
        return _rarityBg[k];
    }

    // 帯(名前の下地など)。左右が少し透ける黒
    public static Sprite Plate
    {
        get
        {
            if (_plate != null) return _plate;
            int w = 64, h = 8;
            var cv = new ArtCanvas(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    cv.Px[y * w + x] = new Color(0.05f, 0.02f, 0.02f, 0.82f);
            _plate = cv.ToSprite(100f, new Vector2(0.5f, 0.5f));
            return _plate;
        }
    }

    // きらめきの斜め帯
    public static Sprite Shine
    {
        get
        {
            if (_shine != null) return _shine;
            int w = 64, h = 64;
            var cv = new ArtCanvas(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 4f);
                    cv.Px[y * w + x] = new Color(1f, 1f, 1f, a * a * 0.55f);
                }
            _shine = cv.ToSprite(100f, new Vector2(0.5f, 0.5f));
            return _shine;
        }
    }
}
