using UnityEngine;

// ============================================================
// BattingSprites.cs
// 打席画面で使う画像を実行時に生成する(外部素材なし)。
// 既存の SpriteFactory.White / Circle を土台に、輪(Ring)を追加する。
// すべて Sprites/Default シェーダで描ける(WebGLでシェーダが落ちない)。
// ============================================================
public static class BattingSprites
{
    private static Sprite _ring;

    // 縁取り用の輪。直径1ユニット、太さは半径の約7%。
    public static Sprite Ring
    {
        get
        {
            if (_ring == null)
            {
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var px = new Color[size * size];
                float c = size * 0.5f;
                float outer = c - 1f;
                float inner = outer - 5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                        float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                        px[y * size + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                tex.SetPixels(px);
                tex.Apply();
                tex.hideFlags = HideFlags.HideAndDontSave;
                _ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                _ring.hideFlags = HideFlags.HideAndDontSave;
            }
            return _ring;
        }
    }

    public static SpriteRenderer Make(string name, Transform parent, Sprite sprite, Color color,
        int order, Vector3 localPos, Vector3 euler, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }
}
