using UnityEngine;

// ============================================================
// ArtCanvas.cs
// 画像素材を使わずにイラストを描くための小さなラスタライザ。
// 座標は 0..1 の正規化(左下原点)。距離場(SDF)で縁を滑らかにする。
// 生成した絵は ToSprite でSprite化する。すべて自前の描画処理。
// ============================================================
public class ArtCanvas
{
    public readonly int W, H;
    public readonly Color[] Px;
    private readonly float _aa;   // 縁のぼかし幅(正規化座標)
    // 描画範囲の制限(正規化座標)。SetClip で設定、ClearClip で解除。
    private float _cx0 = -1f, _cy0 = -1f, _cx1 = 2f, _cy1 = 2f;
    public void SetClip(float x0, float y0, float x1, float y1) { _cx0 = x0; _cy0 = y0; _cx1 = x1; _cy1 = y1; }
    public void ClearClip() { _cx0 = -1f; _cy0 = -1f; _cx1 = 2f; _cy1 = 2f; }
    private bool Clipped(int x, int y)
    {
        float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
        return u < _cx0 || u > _cx1 || v < _cy0 || v > _cy1;
    }

    public ArtCanvas(int w, int h)
    {
        W = w; H = h;
        Px = new Color[w * h];
        for (int i = 0; i < Px.Length; i++) Px[i] = new Color(1f, 1f, 1f, 0f);
        _aa = 1.2f / Mathf.Max(w, h);
    }

    public void Fill(Color c)
    {
        for (int i = 0; i < Px.Length; i++) Px[i] = c;
    }

    // 上に重ねる(通常のアルファ合成)
    public void Blend(int x, int y, Color c, float a)
    {
        if (x < 0 || y < 0 || x >= W || y >= H || Clipped(x, y)) return;
        a *= c.a;
        if (a <= 0.0005f) return;
        int i = y * W + x;
        Color d = Px[i];
        float oa = a + d.a * (1f - a);
        if (oa < 1e-5f) { Px[i] = new Color(0, 0, 0, 0); return; }
        Px[i] = new Color(
            (c.r * a + d.r * d.a * (1f - a)) / oa,
            (c.g * a + d.g * d.a * (1f - a)) / oa,
            (c.b * a + d.b * d.a * (1f - a)) / oa, oa);
    }

    // 既存の不透明部分の上にだけ塗る(陰影・模様用)
    public void BlendInside(int x, int y, Color c, float a)
    {
        if (x < 0 || y < 0 || x >= W || y >= H || Clipped(x, y)) return;
        int i = y * W + x;
        Color d = Px[i];
        if (d.a <= 0.001f) return;
        a *= c.a;
        Px[i] = new Color(Mathf.Lerp(d.r, c.r, a), Mathf.Lerp(d.g, c.g, a), Mathf.Lerp(d.b, c.b, a), d.a);
    }

    private void Bounds(float x0, float y0, float x1, float y1, out int ix0, out int iy0, out int ix1, out int iy1)
    {
        ix0 = Mathf.Max(0, Mathf.FloorToInt(x0 * W) - 2);
        iy0 = Mathf.Max(0, Mathf.FloorToInt(y0 * H) - 2);
        ix1 = Mathf.Min(W - 1, Mathf.CeilToInt(x1 * W) + 2);
        iy1 = Mathf.Min(H - 1, Mathf.CeilToInt(y1 * H) + 2);
    }

    private float Cov(float sd) { return Mathf.Clamp01(0.5f - sd / _aa); }

    // 楕円(回転可)。inside=true なら既存の絵の上にだけ塗る。
    public void Ellipse(float cx, float cy, float rx, float ry, Color c, float rotDeg = 0f, bool inside = false)
    {
        float r = Mathf.Max(rx, ry);
        int x0, y0, x1, y1;
        Bounds(cx - r, cy - r, cx + r, cy + r, out x0, out y0, out x1, out y1);
        float cs = Mathf.Cos(-rotDeg * Mathf.Deg2Rad), sn = Mathf.Sin(-rotDeg * Mathf.Deg2Rad);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float px = (x + 0.5f) / W - cx, py = (y + 0.5f) / H - cy;
                float qx = px * cs - py * sn, qy = px * sn + py * cs;
                float k = Mathf.Sqrt((qx * qx) / (rx * rx) + (qy * qy) / (ry * ry));
                float sd = (k - 1f) * Mathf.Min(rx, ry);
                float a = Cov(sd);
                if (a <= 0f) continue;
                if (inside) BlendInside(x, y, c, a); else Blend(x, y, c, a);
            }
    }

    // 楕円の輪郭線
    public void EllipseStroke(float cx, float cy, float rx, float ry, float width, Color c)
    {
        float r = Mathf.Max(rx, ry) + width;
        int x0, y0, x1, y1;
        Bounds(cx - r, cy - r, cx + r, cy + r, out x0, out y0, out x1, out y1);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float px = (x + 0.5f) / W - cx, py = (y + 0.5f) / H - cy;
                float k = Mathf.Sqrt((px * px) / (rx * rx) + (py * py) / (ry * ry));
                float sd = Mathf.Abs((k - 1f) * Mathf.Min(rx, ry)) - width * 0.5f;
                float a = Cov(sd);
                if (a > 0f) Blend(x, y, c, a);
            }
    }

    // 太さのある線分(端は丸)
    public void Line(float ax, float ay, float bx, float by, float width, Color c, bool inside = false)
    {
        float hw = width * 0.5f;
        int x0, y0, x1, y1;
        Bounds(Mathf.Min(ax, bx) - hw, Mathf.Min(ay, by) - hw, Mathf.Max(ax, bx) + hw, Mathf.Max(ay, by) + hw,
            out x0, out y0, out x1, out y1);
        float dx = bx - ax, dy = by - ay;
        float len2 = Mathf.Max(1e-8f, dx * dx + dy * dy);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float px = (x + 0.5f) / W - ax, py = (y + 0.5f) / H - ay;
                float t = Mathf.Clamp01((px * dx + py * dy) / len2);
                float ex = px - dx * t, ey = py - dy * t;
                float sd = Mathf.Sqrt(ex * ex + ey * ey) - hw;
                float a = Cov(sd);
                if (a <= 0f) continue;
                if (inside) BlendInside(x, y, c, a); else Blend(x, y, c, a);
            }
    }

    // 円弧(度。0=右、反時計回り)
    public void Arc(float cx, float cy, float rx, float ry, float a0, float a1, float width, Color c, bool inside = false)
    {
        int n = Mathf.Max(6, Mathf.CeilToInt(Mathf.Abs(a1 - a0) / 8f));
        float px = cx + Mathf.Cos(a0 * Mathf.Deg2Rad) * rx, py = cy + Mathf.Sin(a0 * Mathf.Deg2Rad) * ry;
        for (int i = 1; i <= n; i++)
        {
            float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
            float qx = cx + Mathf.Cos(a) * rx, qy = cy + Mathf.Sin(a) * ry;
            Line(px, py, qx, qy, width, c, inside);
            px = qx; py = qy;
        }
    }

    // 角丸四角(塗り)
    public void RoundRect(float x0, float y0, float x1, float y1, float r, Color c, bool inside = false)
    {
        int ix0, iy0, ix1, iy1;
        Bounds(x0, y0, x1, y1, out ix0, out iy0, out ix1, out iy1);
        float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
        float hx = (x1 - x0) * 0.5f - r, hy = (y1 - y0) * 0.5f - r;
        for (int y = iy0; y <= iy1; y++)
            for (int x = ix0; x <= ix1; x++)
            {
                float px = Mathf.Abs((x + 0.5f) / W - cx) - hx, py = Mathf.Abs((y + 0.5f) / H - cy) - hy;
                float ox = Mathf.Max(px, 0f), oy = Mathf.Max(py, 0f);
                float sd = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(px, py), 0f) - r;
                float a = Cov(sd);
                if (a <= 0f) continue;
                if (inside) BlendInside(x, y, c, a); else Blend(x, y, c, a);
            }
    }

    // 縦グラデーションの陰影(既存の絵の上にだけ)
    public void ShadeVertical(Color top, float topA, Color bottom, float bottomA)
    {
        for (int y = 0; y < H; y++)
        {
            float t = y / (float)(H - 1);
            for (int x = 0; x < W; x++)
            {
                BlendInside(x, y, top, topA * t);
                BlendInside(x, y, bottom, bottomA * (1f - t));
            }
        }
    }

    // 不透明部分のまわりに縁取りを付ける(半径 px ピクセル)
    public void Outline(float radiusPx, Color c)
    {
        var src = (Color[])Px.Clone();
        int r = Mathf.CeilToInt(radiusPx) + 1;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float best = 0f;
                for (int dy = -r; dy <= r; dy++)
                {
                    int yy = y + dy; if (yy < 0 || yy >= H) continue;
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int xx = x + dx; if (xx < 0 || xx >= W) continue;
                        float a = src[yy * W + xx].a;
                        if (a <= best) continue;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float w = Mathf.Clamp01(radiusPx + 0.5f - d);
                        if (a * w > best) best = a * w;
                    }
                }
                Color s = src[y * W + x];
                // 縁の色を下に敷いて、元の絵を上に重ねる
                float oa = s.a + best * (1f - s.a);
                if (oa < 1e-5f) { Px[y * W + x] = new Color(0, 0, 0, 0); continue; }
                Px[y * W + x] = new Color(
                    (s.r * s.a + c.r * best * (1f - s.a)) / oa,
                    (s.g * s.a + c.g * best * (1f - s.a)) / oa,
                    (s.b * s.a + c.b * best * (1f - s.a)) / oa, oa);
            }
    }

    public bool Mipmaps = false;

    public Texture2D ToTexture(bool repeat = false, FilterMode filter = FilterMode.Bilinear)
    {
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, Mipmaps);
        tex.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        tex.filterMode = filter;
        tex.SetPixels(Px);
        tex.Apply(Mipmaps);
        if (Mipmaps) tex.anisoLevel = 2;
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }

    // ppu: 1ワールドユニットあたりのピクセル数。border を渡すと9スライス用になる。
    public Sprite ToSprite(float ppu, Vector2 pivot, Vector4 border = default(Vector4), bool repeat = false)
    {
        var tex = ToTexture(repeat);
        var sp = Sprite.Create(tex, new Rect(0, 0, W, H), pivot, ppu, 0, SpriteMeshType.FullRect, border);
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    // 簡易ノイズ(格子値ノイズ。タイル可能)
    public static float ValueNoise(float x, float y, int period, int seed)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        float a = Hash(xi, yi, period, seed), b = Hash(xi + 1, yi, period, seed);
        float c = Hash(xi, yi + 1, period, seed), d = Hash(xi + 1, yi + 1, period, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    private static float Hash(int x, int y, int period, int seed)
    {
        x = ((x % period) + period) % period; y = ((y % period) + period) % period;
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144269504);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }
}
