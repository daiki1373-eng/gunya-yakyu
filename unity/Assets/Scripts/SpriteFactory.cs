using UnityEngine;

// ============================================================
// SpriteFactory.cs
// 画像素材を使わず、実行時にテクスチャを生成してSprite化するユーティリティ。
// - White : 1x1の白テクスチャ。四角いパーツ全般に使い、色はSpriteRenderer.colorで付ける。
// - Circle: 円テクスチャ。頭・ボール・マウンドなどに使う。
// - GradientVertical: 上下グラデーション。空などに使う。
// - Crowd : 観客席のドット模様テクスチャ。
// ============================================================
public static class SpriteFactory
{
    private static Sprite _white;
    private static Sprite _circle;

    public static Sprite White
    {
        get
        {
            if (_white == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                tex.hideFlags = HideFlags.HideAndDontSave;
                // PPU=1 なので「1テクセル = 1ワールドユニット」。scaleで大きさを決める。
                _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                _white.hideFlags = HideFlags.HideAndDontSave;
            }
            return _white;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (_circle == null)
            {
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                        float a = Mathf.Clamp01((size * 0.5f - d) / 1.5f); // 縁を少しだけ滑らかに
                        pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply();
                tex.hideFlags = HideFlags.HideAndDontSave;
                // PPU=64 なので直径1ワールドユニットの円になる。
                _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                _circle.hideFlags = HideFlags.HideAndDontSave;
            }
            return _circle;
        }
    }

    // 上下グラデーションのSpriteを生成する(空など用)。縦の大きさ1ユニット相当で作り、scaleで伸ばす。
    public static Sprite GradientVertical(Color top, Color bottom)
    {
        const int h = 128;
        var tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
        {
            float t = y / (float)(h - 1); // テクスチャの下端が v=0
            tex.SetPixel(0, y, Color.Lerp(bottom, top, t));
        }
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        var sp = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), h);
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    // 観客席用のドット模様テクスチャ(数色のランダムな点)。横に繰り返して使う。
    public static Sprite Crowd(Color[] palette, int seed)
    {
        const int w = 256;
        const int h = 48;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        var pixels = new Color[w * h];
        Color bg = new Color(0.45f, 0.48f, 0.52f, 1f);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;
        var rng = new System.Random(seed);
        for (int i = 0; i < 1400; i++)
        {
            int x = rng.Next(0, w);
            int y = rng.Next(0, h);
            Color c = palette[rng.Next(0, palette.Length)];
            pixels[y * w + x] = c;
            if (x + 1 < w) pixels[y * w + x + 1] = c;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        // PPU=16: 横16ユニット・縦3ユニットで1タイル。
        var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    // 色付きの四角(背景・フィールド用、物理なし)を1つ作る。
    public static GameObject Box(string name, Transform parent, Vector2 pos, Vector2 size, Color color, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = White;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return go;
    }

    // 任意スプライトの板を1つ作る。size は「ワールド単位の最終サイズ」。
    // スプライト本来の大きさ(bounds)で割って、どのスプライトでも同じ指定で済むようにする。
    public static GameObject Panel(string name, Transform parent, Vector2 pos, Vector2 size, Sprite sprite, Color color, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        Vector2 bs = sprite.bounds.size;
        go.transform.localScale = new Vector3(size.x / bs.x, size.y / bs.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return go;
    }
}
