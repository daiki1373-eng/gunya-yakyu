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
        if (outline) TelopFx.Apply(t, TelopFx.Kind.Label);   // 縁取り + 影(全画面で同じ仕上げ)
        return t;
    }

    // 文字の仕上げを変える(見出し・ロゴなど)
    public static Text Style(Text t, TelopFx.Kind k)
    {
        TelopFx.Apply(t, k);
        return t;
    }

    // キャラ名などを枠に収める。1行で入れば少しだけ縮め、入らなければ言葉の切れ目で2行にする。
    // (日本語は文字の途中で折り返されるので、自動の折り返しは使わない)
    public static void FitName(Text t, string name, float maxW, float maxH, int maxSize, int minSize)
    {
        if (t == null) return;
        name = name ?? "";
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.resizeTextForBestFit = false;
        t.text = name;
        int oneLineMin = Mathf.Max(minSize, Mathf.RoundToInt(maxSize * 0.8f));
        for (int sz = maxSize; sz >= oneLineMin; sz--)
        {
            t.fontSize = sz;
            if (t.preferredWidth <= maxW) return;
        }
        int cut = BreakAt(name);
        if (cut > 0)
        {
            t.text = name.Substring(0, cut) + "\n" + name.Substring(cut);
            t.lineSpacing = 0.9f;
            for (int sz = maxSize; sz >= minSize; sz--)
            {
                t.fontSize = sz;
                if (t.preferredWidth <= maxW && (maxH <= 0f || t.preferredHeight <= maxH)) return;
            }
            return;
        }
        // 切れ目がない短い名前は、1行のまま最小の大きさにする
        for (int sz = oneLineMin; sz >= minSize; sz--)
        {
            t.fontSize = sz;
            if (t.preferredWidth <= maxW) return;
        }
    }

    // 2行に分けるならどこか。かっこの前・中黒の後・文字の種類(漢字/ひらがな/カタカナ/英数)が変わる所のうち、真ん中に近い所。
    public static int BreakAt(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length < 4) return -1;
        int best = -1;
        float bestScore = float.MaxValue;
        float mid = s.Length * 0.5f;
        for (int i = 2; i <= s.Length - 2; i++)
        {
            char a = s[i - 1], b = s[i];
            bool ok = b == '（' || b == '(' || b == '「' || a == '・' || a == '）' || a == ')' || a == '　' || a == ' ' || a == 'の'
                      || (Script(a) != Script(b) && Script(a) != 0 && Script(b) != 0 && !(a == 'ー' || b == 'ー'));
            if ("のとなにがをはでもやっゃゅょー）)」、。".IndexOf(b) >= 0) ok = false;   // 助詞や小さい字の前では切らない
            if (Script(a) == 3 && Script(b) == 1) ok = false;   // 送りがな(早食|い)は切らない
            bool tail = i == s.Length - 2 && s.EndsWith("選手") && i >= 3;
            if (tail) ok = true;
            if (!ok) continue;
            float score = Mathf.Abs(i - mid);
            if (b == '（' || b == '(') score -= 1.5f;   // かっこは切れ目として優先
            if (a == 'の') score -= 1.5f;
            if (tail) score -= 2f;                       // 「〜選手」は語尾で切る
            if (score < bestScore) { bestScore = score; best = i; }
        }
        if (best < 0)
        {
            // カタカナだけの名前は、よくある語尾の前で切る(なければ切らない)
            string[] tails = { "エース", "ヒッター", "キャプテン", "マスコット", "ピッチャー", "キャッチャー", "バッター", "スラッガー", "ボーイ", "ガール", "マン" };
            foreach (var tail in tails)
            {
                if (s.Length > tail.Length + 1 && s.EndsWith(tail)) return s.Length - tail.Length;
            }
        }
        return best;
    }

    private static int Script(char c)
    {
        if (c >= '\u3040' && c <= '\u309F') return 1;   // ひらがな
        if ((c >= '\u30A0' && c <= '\u30FF') || c == 'ー') return 2;   // カタカナ
        if (c >= '\u4E00' && c <= '\u9FFF') return 3;   // 漢字
        if (char.IsLetterOrDigit(c)) return 4;
        return 0;
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
        var t = Txt(img.transform, "Label", label, fontSize, Color.white, TextAnchor.MiddleCenter);
        HomeSkin.NavText(t, 2f);
        if (primary) { var fx = t.GetComponent<TelopFx>(); fx.Outer = new Color(0.42f, 0.13f, 0f, 1f); }
        Fill(t.rectTransform);
        return b;
    }

    // ---- 生成画像 ------------------------------------------------------
    private static Sprite _rays, _frame, _bgRadial, _shine, _plate;

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
            return HomeSkin.BtnMain;   // ホームと同じつやのある橙
        }
    }

    public static Sprite ButtonDark
    {
        get
        {
            return HomeSkin.BtnSub;    // ホームと同じつやのある青
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
