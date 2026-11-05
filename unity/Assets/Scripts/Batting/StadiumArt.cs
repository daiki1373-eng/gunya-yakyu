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
    // 1024x256(1列32px)。輪郭をなめらかに描き、下から照明が当たった陰影と、列の足元の暗がりを付ける。
    private static readonly Sprite[,] _crowd = new Sprite[2, 2];
    // side: 0=ホーム側(赤系が多い) 1=ビジター側(青系が多い)
    public static Sprite Crowd(int side, int frame)
    {
        if (_crowd[side, frame] != null) return _crowd[side, frame];
        int w = 1024, h = 256, rowH = 32;
        var px = new Color[w * h];
        Color riser = new Color(0.035f, 0.04f, 0.06f), seatLo = new Color(0.06f, 0.08f, 0.15f), seatHi = new Color(0.11f, 0.14f, 0.25f);
        for (int y = 0; y < h; y++)
        {
            int ry = y % rowH;
            Color c = ry < 5 ? riser : Color.Lerp(seatLo, seatHi, (ry - 5) / (float)(rowH - 5));
            for (int x = 0; x < w; x++) px[y * w + x] = c;
        }
        Color team = side == 0 ? new Color(0.80f, 0.13f, 0.14f) : new Color(0.14f, 0.27f, 0.80f);
        Color[] shirts =
        {
            team, team, team, team, new Color(0.92f, 0.92f, 0.94f), new Color(0.12f, 0.12f, 0.15f),
            new Color(0.92f, 0.76f, 0.22f), new Color(0.38f, 0.52f, 0.40f), new Color(0.62f, 0.63f, 0.68f),
            team * 0.65f, new Color(0.90f, 0.50f, 0.32f), new Color(0.30f, 0.32f, 0.40f)
        };
        Color[] skins = { new Color(0.95f, 0.78f, 0.64f), new Color(0.86f, 0.66f, 0.50f), new Color(0.70f, 0.50f, 0.36f) };
        Color[] hairs = { new Color(0.06f, 0.05f, 0.05f), new Color(0.22f, 0.15f, 0.09f), new Color(0.10f, 0.08f, 0.08f), new Color(0.45f, 0.40f, 0.36f) };
        var rng = new System.Random(900 + side * 31);
        for (int row = 0; row < h / rowH; row++)
        {
            int yb = row * rowH;
            float x = rng.Next(0, 10);
            while (x < w)
            {
                float pw = 14f + rng.Next(0, 5);
                bool present = rng.NextDouble() < 0.94;
                bool stand = frame == 1 && rng.NextDouble() < 0.55;
                if (present)
                {
                    Color shirt = shirts[rng.Next(shirts.Length)];
                    Color skin = skins[rng.Next(skins.Length)];
                    Color hair = hairs[rng.Next(hairs.Length)];
                    bool cap = rng.NextDouble() < 0.3;
                    float lift = stand ? 6f : 0f;
                    float cx = x + pw * 0.5f;
                    // 体(肩の丸い四角)。下ほど暗く、上(肩)は照明で明るい
                    RoundRect(px, w, h, x + 0.5f, yb + 5f + lift, x + pw - 0.5f, yb + 19f + lift, 4.5f,
                        shirt * 0.55f, shirt * 1.05f);
                    // 首の影
                    Disc(px, w, h, cx, yb + 19.5f + lift, 2.6f, skin * 0.6f);
                    // 頭(左上に光)
                    DiscShade(px, w, h, cx, yb + 23.5f + lift, 4.8f, skin * 0.8f, skin * 1.12f);
                    if (cap) { Disc(px, w, h, cx, yb + 25.5f + lift, 4.9f, team * 0.9f, true); RoundRect(px, w, h, cx - 1f, yb + 24.5f + lift, cx + 7f, yb + 26f + lift, 0.8f, team * 0.6f, team * 0.8f); }
                    else Disc(px, w, h, cx, yb + 25.2f + lift, 4.6f, hair, true);
                    if (stand)
                    {
                        // 手を上げる / タオルを掲げる
                        if (rng.NextDouble() < 0.35)
                            RoundRect(px, w, h, x - 2f, yb + 30f + lift, x + pw + 2f, yb + 33f + lift, 1f, team * 0.8f, team * 1.15f);
                        else
                        {
                            RoundRect(px, w, h, x + 0.5f, yb + 17f + lift, x + 2.5f, yb + 31f + lift, 1f, skin * 0.7f, skin);
                            RoundRect(px, w, h, x + pw - 2.5f, yb + 17f + lift, x + pw - 0.5f, yb + 31f + lift, 1f, skin * 0.7f, skin);
                        }
                    }
                }
                x += pw + 1.5f + rng.Next(0, 3);
            }
        }
        // 列の足元(前の列の背もたれ)の暗がり
        for (int y = 0; y < h; y++)
        {
            int ry = y % rowH;
            float ao = ry < 9 ? Mathf.Lerp(0.55f, 1f, ry / 9f) : 1f;
            for (int x = 0; x < w; x++)
            {
                Color c = px[y * w + x] * ao;
                // 夜のスタンドらしく少し暗く・彩度を控えめに
                float l = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                c = Color.Lerp(new Color(l, l, l), c, 0.82f) * 0.86f;
                c.a = 1f;
                px[y * w + x] = c;
            }
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 8;
        tex.SetPixels(px);
        tex.Apply(true);
        tex.hideFlags = HideFlags.HideAndDontSave;
        var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
        sp.hideFlags = HideFlags.HideAndDontSave;
        _crowd[side, frame] = sp;
        return sp;
    }

    // 観客テクスチャの一部(幅256px=8m)を切り出したもの。区画ごとに柄をずらして繰り返しを目立たなくする。
    private static readonly Sprite[,,] _slices = new Sprite[2, 2, 4];
    public static Sprite CrowdSlice(int side, int frame, int variant)
    {
        var s = _slices[side, frame, variant];
        if (s != null) return s;
        var full = Crowd(side, frame);
        s = Sprite.Create(full.texture, new Rect(variant * 256, 0, 256, 256), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
        s.hideFlags = HideFlags.HideAndDontSave;
        _slices[side, frame, variant] = s;
        return s;
    }

    // 角の丸い四角(輪郭はなめらか)。下 colBottom → 上 colTop のグラデーション
    private static void RoundRect(Color[] px, int w, int h, float x0, float y0, float x1, float y1, float rad, Color colBottom, Color colTop)
    {
        for (int y = Mathf.FloorToInt(y0) - 1; y <= Mathf.CeilToInt(y1) + 1; y++)
        {
            if (y < 0 || y >= h) continue;
            float py = y + 0.5f;
            Color c = Color.Lerp(colBottom, colTop, Mathf.Clamp01((py - y0) / Mathf.Max(1f, y1 - y0)));
            c.a = 1f;
            for (int x = Mathf.FloorToInt(x0) - 1; x <= Mathf.CeilToInt(x1) + 1; x++)
            {
                float pxx = x + 0.5f;
                float qx = Mathf.Max(Mathf.Abs(pxx - (x0 + x1) * 0.5f) - ((x1 - x0) * 0.5f - rad), 0f);
                float qy = Mathf.Max(Mathf.Abs(py - (y0 + y1) * 0.5f) - ((y1 - y0) * 0.5f - rad), 0f);
                float d = Mathf.Sqrt(qx * qx + qy * qy) - rad;
                float a = Mathf.Clamp01(0.5f - d);
                if (a <= 0f) continue;
                int xx = ((x % w) + w) % w;
                px[y * w + xx] = Color.Lerp(px[y * w + xx], c, a);
            }
        }
    }

    // 陰影付きの円(左上が明るい)
    private static void DiscShade(Color[] px, int w, int h, float cx, float cy, float r, Color dark, Color lit)
    {
        for (int y = Mathf.FloorToInt(cy - r) - 1; y <= Mathf.CeilToInt(cy + r) + 1; y++)
        {
            if (y < 0 || y >= h) continue;
            for (int x = Mathf.FloorToInt(cx - r) - 1; x <= Mathf.CeilToInt(cx + r) + 1; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                if (a <= 0f) continue;
                float k = Mathf.Clamp01(0.5f + (-dx * 0.5f + dy * 0.8f) / (r * 1.6f));
                Color c = Color.Lerp(dark, lit, k); c.a = 1f;
                int xx = ((x % w) + w) % w;
                px[y * w + xx] = Color.Lerp(px[y * w + xx], c, a);
            }
        }
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
