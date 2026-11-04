using UnityEngine;

// ============================================================
// StadiumArt.cs
// 球場の画像(芝・土・観客・空・照明・電光掲示)をコードで生成する(外部素材なし)。
// ナイターの中継映像のような雰囲気を、自前のテクスチャだけで作る。
// ============================================================
public static class StadiumArt
{
    private static Sprite Tex(ArtCanvas cv, float ppu, bool repeat = true)
    {
        cv.Mipmaps = true;
        return cv.ToSprite(ppu, new Vector2(0.5f, 0.5f), default(Vector4), repeat);
    }

    // ---- 芝(タイル) -----------------------------------------------
    private static Sprite _grass;
    public static Sprite Grass
    {
        get
        {
            if (_grass != null) return _grass;
            int n = 256;
            var cv = new ArtCanvas(n, n);
            Color a = new Color(0.20f, 0.50f, 0.20f), b = new Color(0.28f, 0.60f, 0.25f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n, v = y / (float)n;
                    float big = ArtCanvas.ValueNoise(u * 8f, v * 8f, 8, 11);
                    float mid = ArtCanvas.ValueNoise(u * 32f, v * 32f, 32, 12);
                    float fine = ArtCanvas.ValueNoise(u * 128f, v * 128f, 128, 13);
                    float k = 0.45f * big + 0.30f * mid + 0.25f * fine;
                    Color c = Color.Lerp(a, b, k);
                    cv.Px[y * n + x] = c;
                }
            _grass = Tex(cv, 32f);
            return _grass;
        }
    }

    // ---- 土(タイル) -----------------------------------------------
    private static Sprite _dirt;
    public static Sprite Dirt
    {
        get
        {
            if (_dirt != null) return _dirt;
            int n = 256;
            var cv = new ArtCanvas(n, n);
            FillDirt(cv, 1f);
            _dirt = Tex(cv, 32f);
            return _dirt;
        }
    }

    private static void FillDirt(ArtCanvas cv, float scale)
    {
        int n = cv.W;
        Color a = new Color(0.62f, 0.42f, 0.27f), b = new Color(0.74f, 0.53f, 0.35f);
        var rng = new System.Random(5);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                float k = 0.5f * ArtCanvas.ValueNoise(u * 6f * scale, v * 6f * scale, Mathf.RoundToInt(6 * scale), 21)
                    + 0.3f * ArtCanvas.ValueNoise(u * 40f * scale, v * 40f * scale, Mathf.RoundToInt(40 * scale), 22)
                    + 0.2f * (float)rng.NextDouble();
                cv.Px[y * n + x] = Color.Lerp(a, b, k);
            }
    }

    // ---- 円形の土(内野・ホーム・マウンド用。縁は芝へなじませる) ------
    private static Sprite _dirtDisc;
    public static Sprite DirtDisc
    {
        get
        {
            if (_dirtDisc != null) return _dirtDisc;
            int n = 512;
            var cv = new ArtCanvas(n, n);
            FillDirt(cv, 4f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = Mathf.Clamp01((1f - d) / 0.012f);
                    Color c = cv.Px[y * n + x];
                    // 縁のほうはやや濃く(踏み固められた感じ)
                    float rim = Mathf.Clamp01((d - 0.90f) / 0.10f);
                    c = Color.Lerp(c, c * 0.88f, rim);
                    c.a = a;
                    cv.Px[y * n + x] = c;
                }
            _dirtDisc = Tex(cv, n, false);
            return _dirtDisc;
        }
    }

    // ---- フェンス(緑のラバー、縦の継ぎ目) ---------------------------
    private static Sprite _fence;
    public static Sprite Fence
    {
        get
        {
            if (_fence != null) return _fence;
            int w = 64, h = 64;
            var cv = new ArtCanvas(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = y / (float)h;
                    Color c = Color.Lerp(new Color(0.05f, 0.20f, 0.14f), new Color(0.10f, 0.33f, 0.22f), v);
                    if (x < 2) c *= 0.6f;
                    c.a = 1f;
                    cv.Px[y * w + x] = c;
                }
            _fence = Tex(cv, 16f);
            return _fence;
        }
    }

    // ---- 観客(2コマ: 座り / 立ち上がって手を上げる) --------------------
    private static readonly Sprite[,] _crowd = new Sprite[2, 2];
    // side: 0=ホーム側(赤系が多い) 1=ビジター側(青系が多い)
    public static Sprite Crowd(int side, int frame)
    {
        if (_crowd[side, frame] != null) return _crowd[side, frame];
        int w = 512, h = 128, rowH = 16;
        var px = new Color[w * h];
        Color riser = new Color(0.10f, 0.11f, 0.15f), seat = new Color(0.16f, 0.22f, 0.38f);
        for (int y = 0; y < h; y++)
        {
            int ry = y % rowH;
            Color c = ry < 3 ? riser : seat;
            for (int x = 0; x < w; x++) px[y * w + x] = c;
        }
        Color team = side == 0 ? new Color(0.86f, 0.16f, 0.16f) : new Color(0.16f, 0.30f, 0.85f);
        Color[] shirts =
        {
            team, team, team, new Color(0.95f, 0.95f, 0.95f), new Color(0.15f, 0.15f, 0.18f),
            new Color(0.95f, 0.80f, 0.25f), new Color(0.45f, 0.60f, 0.45f), new Color(0.70f, 0.70f, 0.75f),
            team * 0.7f, new Color(0.95f, 0.55f, 0.35f)
        };
        Color[] skins = { new Color(0.96f, 0.80f, 0.65f), new Color(0.88f, 0.68f, 0.52f), new Color(0.75f, 0.55f, 0.40f) };
        Color[] hairs = { new Color(0.08f, 0.07f, 0.07f), new Color(0.25f, 0.17f, 0.10f), new Color(0.12f, 0.10f, 0.10f), team };
        var rng = new System.Random(900 + side * 31);
        for (int row = 0; row < h / rowH; row++)
        {
            int yb = row * rowH;
            int x = rng.Next(0, 6);
            while (x < w)
            {
                int pw = 7 + rng.Next(0, 3);
                bool present = rng.NextDouble() < 0.93;
                bool stand = frame == 1 && rng.NextDouble() < 0.55;
                if (present)
                {
                    Color shirt = shirts[rng.Next(shirts.Length)];
                    Color skin = skins[rng.Next(skins.Length)];
                    Color hair = hairs[rng.Next(hairs.Length)];
                    int lift = stand ? 3 : 0;
                    // 体
                    Rect(px, w, h, x, yb + 3 + lift, pw, 7, shirt);
                    // 頭
                    Disc(px, w, h, x + pw * 0.5f, yb + 12.2f + lift, 2.6f, skin);
                    Disc(px, w, h, x + pw * 0.5f, yb + 13.4f + lift, 2.3f, hair, true);
                    if (stand)
                    {
                        // 手を上げる / タオルを掲げる
                        if (rng.NextDouble() < 0.35) Rect(px, w, h, x - 1, yb + 15 + lift, pw + 2, 2, team * 1.1f);
                        else
                        {
                            Rect(px, w, h, x, yb + 9 + lift, 1, 7, skin);
                            Rect(px, w, h, x + pw - 1, yb + 9 + lift, 1, 7, skin);
                        }
                    }
                }
                x += pw + 1 + rng.Next(0, 2);
            }
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 4;
        tex.SetPixels(px);
        tex.Apply(true);
        tex.hideFlags = HideFlags.HideAndDontSave;
        var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
        sp.hideFlags = HideFlags.HideAndDontSave;
        _crowd[side, frame] = sp;
        return sp;
    }

    // 観客テクスチャの一部(幅128px=8m)を切り出したもの。区画ごとに柄をずらして繰り返しを目立たなくする。
    private static readonly Sprite[,,] _slices = new Sprite[2, 2, 4];
    public static Sprite CrowdSlice(int side, int frame, int variant)
    {
        var s = _slices[side, frame, variant];
        if (s != null) return s;
        var full = Crowd(side, frame);
        s = Sprite.Create(full.texture, new Rect(variant * 128, 0, 128, 128), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
        s.hideFlags = HideFlags.HideAndDontSave;
        _slices[side, frame, variant] = s;
        return s;
    }

    private static void Rect(Color[] px, int w, int h, int x0, int y0, int rw, int rh, Color c)
    {
        c.a = 1f;
        for (int y = y0; y < y0 + rh; y++)
        {
            if (y < 0 || y >= h) continue;
            for (int x = x0; x < x0 + rw; x++)
            {
                int xx = ((x % w) + w) % w;
                px[y * w + xx] = c;
            }
        }
    }

    private static void Disc(Color[] px, int w, int h, float cx, float cy, float r, Color c, bool topHalf = false)
    {
        c.a = 1f;
        for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
        {
            if (y < 0 || y >= h) continue;
            if (topHalf && y + 0.5f < cy) continue;
            for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                if (a <= 0f) continue;
                int xx = ((x % w) + w) % w;
                px[y * w + xx] = Color.Lerp(px[y * w + xx], c, a);
            }
        }
    }

    // ---- 夜空(縦グラデーション + 星) -----------------------------------
    private static Sprite _sky;
    public static Sprite Sky
    {
        get
        {
            if (_sky != null) return _sky;
            int w = 128, h = 256;
            var cv = new ArtCanvas(w, h);
            Color top = new Color(0.02f, 0.03f, 0.09f), mid = new Color(0.05f, 0.08f, 0.20f), hor = new Color(0.20f, 0.20f, 0.32f);
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                Color c = t < 0.25f ? Color.Lerp(hor, mid, t / 0.25f) : Color.Lerp(mid, top, (t - 0.25f) / 0.75f);
                for (int x = 0; x < w; x++) cv.Px[y * w + x] = c;
            }
            var rng = new System.Random(77);
            for (int i = 0; i < 26; i++)
            {
                int x = rng.Next(w), y = 90 + rng.Next(h - 92);
                float b = 0.3f + 0.5f * (float)rng.NextDouble();
                cv.Px[y * w + x] = Color.Lerp(cv.Px[y * w + x], Color.white, b);
            }
            _sky = Tex(cv, h / 200f);
            return _sky;
        }
    }

    // ---- 光のにじみ(照明・フラッシュ用) ---------------------------------
    private static Sprite _glow;
    public static Sprite Glow
    {
        get
        {
            if (_glow != null) return _glow;
            int n = 128;
            var cv = new ArtCanvas(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * a * 0.85f + Mathf.Clamp01(1f - d * 4f) * 0.15f;
                    cv.Px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            _glow = Tex(cv, n, false);
            return _glow;
        }
    }

    // ---- 照明パネル(四角い枠に電球が並ぶ) ---------------------------
    private static Sprite _lightPanel;
    public static Sprite LightPanel
    {
        get
        {
            if (_lightPanel != null) return _lightPanel;
            int w = 120, h = 72;
            var cv = new ArtCanvas(w, h);
            cv.RoundRect(0f, 0f, 1f, 1f, 0.03f, new Color(0.18f, 0.19f, 0.22f));
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 7; c++)
                {
                    float cx = (c + 0.5f) / 7f, cy = (r + 0.5f) / 4f;
                    cv.Ellipse(cx, cy, 0.060f, 0.10f, new Color(1f, 0.93f, 0.75f, 0.6f));
                    cv.Ellipse(cx, cy, 0.042f, 0.07f, new Color(1f, 1f, 0.96f));
                }
            _lightPanel = Tex(cv, 12f, false);
            return _lightPanel;
        }
    }

    // ---- 周辺減光(画面の四隅を少し暗く) -------------------------------
    private static Sprite _vignette;
    public static Sprite Vignette
    {
        get
        {
            if (_vignette != null) return _vignette;
            int n = 128;
            var cv = new ArtCanvas(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = ((x + 0.5f) / n - 0.5f) * 2f, dy = ((y + 0.5f) / n - 0.5f) * 2f;
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 0.9f);
                    float a = Mathf.Clamp01((d - 0.65f) / 0.75f);
                    cv.Px[y * n + x] = new Color(0f, 0f, 0.02f, a * a * 0.55f);
                }
            _vignette = Tex(cv, 100f, false);
            return _vignette;
        }
    }
}
