using System.Collections.Generic;
using UnityEngine;

// ============================================================
// CharacterArt.cs
// 選手の顔・帽子/ヘルメット・体パーツ・バット・グラブの絵をコードで描く(外部素材なし)。
// 顔は右向き3/4の頭部を1枚の絵にまとめ、表情ごとに差し替える。
// 輪郭線は「一回り大きい同じ形を濃い色で先に塗る」方式で付ける(軽い)。
// 頭は「直径1ユニット」になるよう PPU を決めてあるので、scale=頭の直径 で置ける。
// ============================================================
public enum Expr { Normal, Focus, Surprise, Happy, Dizzy }
public enum Headgear { Cap, Helmet }

public static class CharacterArt
{
    public const float HeadTexScale = 1.45f;   // テクスチャ幅 / 頭の直径
    private const int HS = 256;
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    private static readonly Color Ink = new Color(0.12f, 0.09f, 0.09f, 1f);

    // ---- 頭 ------------------------------------------------------
    // size: テクスチャの一辺(遠景の人形は小さくして生成を軽くする)
    public static Sprite Head(string species, Color skin, Color hat, Headgear gear, Expr e, int variant, int size = HS)
    {
        string key = species + "|" + ColorUtility.ToHtmlStringRGB(skin) + "|" + ColorUtility.ToHtmlStringRGB(hat)
            + "|" + gear + "|" + e + "|" + (variant & 7) + "|" + size;
        Sprite sp;
        if (Cache.TryGetValue(key, out sp) && sp != null) return sp;
        sp = DrawHead(species, skin, hat, gear, e, variant, size);
        Cache[key] = sp;
        return sp;
    }

    // 頭単位の座標(中心0、半径1、右が顔の向き) → 正規化座標
    private const float CX = 0.5f, CY = 0.47f;
    private static float R { get { return 0.5f / HeadTexScale; } }
    private static float X(float u) { return CX + u * R; }
    private static float Y(float v) { return CY + v * R; }
    private static float S(float d) { return d * R; }

    private static Sprite DrawHead(string species, Color skin, Color hat, Headgear gear, Expr e, int variant, int size)
    {
        var cv = new ArtCanvas(size, size);
        cv.Mipmaps = true;
        bool panda = species == "panda", dog = species == "dog", beetle = species == "beetle";
        float o = 0.055f; // 輪郭の太さ(頭単位)
        Color hair = (variant % 3 == 1) ? new Color(0.30f, 0.20f, 0.13f) : new Color(0.13f, 0.11f, 0.11f);
        Color skinDark = skin * 0.78f; skinDark.a = 1f;
        Color hatDark = hat * 0.62f; hatDark.a = 1f;
        Color furDark = new Color(0.45f, 0.28f, 0.15f);

        // ---- 1) 輪郭(一回り大きい同形を濃色で) -------------------
        for (int pass = 0; pass < 2; pass++)
        {
            float g = pass == 0 ? o : 0f;
            Color ink = Ink;
            bool outline = pass == 0;
            // 耳
            if (!panda && !dog)
                cv.Ellipse(X(-0.16f), Y(-0.05f), S(0.19f + g), S(0.24f + g), outline ? ink : skin);
            // 頭蓋・あご・鼻
            cv.Ellipse(X(0f), Y(0f), S(1f + g), S(1f + g), outline ? ink : skin);
            cv.Ellipse(X(0.24f), Y(-0.40f), S(0.72f + g), S(0.56f + g), outline ? ink : skin);
            if (dog)
            {
                cv.Ellipse(X(0.78f), Y(-0.30f), S(0.40f + g), S(0.32f + g), outline ? ink : new Color(0.97f, 0.90f, 0.78f));
            }
            else
            {
                cv.Ellipse(X(0.97f), Y(-0.10f), S(0.12f + g), S(0.17f + g), outline ? ink : skin);
            }
            if (!outline)
            {
                // 立体感: 右上から光が当たる陰影
                ShadeSphere(cv, skin);
                // 頬の赤み
                cv.Ellipse(X(0.42f), Y(-0.36f), S(0.22f), S(0.13f), new Color(1f, 0.45f, 0.45f, 0.30f), 0f, true);
                if (!panda && !dog)
                {
                    // 耳の内側
                    cv.Ellipse(X(-0.15f), Y(-0.06f), S(0.09f), S(0.13f), new Color(skinDark.r, skinDark.g, skinDark.b, 0.8f), 0f, true);
                    // 後ろ髪・もみあげ
                    cv.SetClip(0f, Y(-0.10f), X(0.12f), 1f);
                    cv.Ellipse(X(-0.30f), Y(0.36f), S(0.72f), S(0.52f), hair, 0f, true);
                    cv.ClearClip();
                    cv.Line(X(0.06f), Y(0.30f), X(0.10f), Y(-0.02f), S(0.11f), hair, true);
                    // ひげそり跡(個体差)
                    if (variant % 4 == 3)
                        cv.Ellipse(X(0.40f), Y(-0.55f), S(0.50f), S(0.32f), new Color(0.35f, 0.38f, 0.45f, 0.18f), 0f, true);
                }
                if (dog)
                {
                    // 口元のふくらみの境目
                    cv.Arc(X(0.78f), Y(-0.30f), S(0.40f), S(0.32f), 100f, 200f, S(0.035f), new Color(furDark.r, furDark.g, furDark.b, 0.5f), true);
                }
            }
        }

        // ---- 2) 顔のパーツ ------------------------------------------
        if (panda)
        {
            cv.Ellipse(X(0.46f), Y(0.02f), S(0.27f), S(0.22f), Ink, -25f, true);
            cv.Ellipse(X(0.86f), Y(0.04f), S(0.13f), S(0.20f), Ink, -10f, true);
        }
        DrawEyes(cv, e, panda, hair);
        DrawBrows(cv, e, panda ? Ink : hair);
        DrawMouth(cv, e, dog);
        if (!panda && !dog && (variant % 2 == 0) && e != Expr.Happy)
        {
            // アイブラック(まぶしさ避けの黒いライン)
            cv.RoundRect(X(0.32f), Y(-0.20f), X(0.62f), Y(-0.13f), S(0.03f), new Color(0.08f, 0.08f, 0.08f, 0.85f), true);
        }
        if (panda) cv.Ellipse(X(1.03f), Y(-0.06f), S(0.10f), S(0.075f), Ink);
        if (dog)
        {
            cv.Ellipse(X(1.12f), Y(-0.08f), S(0.16f), S(0.12f), Ink);
            cv.Ellipse(X(1.10f), Y(-0.04f), S(0.06f), S(0.035f), new Color(1f, 1f, 1f, 0.6f));
        }

        // ---- 3) 帽子 / ヘルメット --------------------------------------
        if (gear == Headgear.Helmet) DrawHelmet(cv, hat, hatDark, o);
        else DrawCap(cv, hat, hatDark, o);

        // ---- 4) 種族の付属物(帽子の上に出るもの) ----------------------
        if (panda)
        {
            cv.Ellipse(X(-0.70f), Y(0.86f), S(0.30f + o), S(0.30f + o), Ink);
            cv.Ellipse(X(-0.70f), Y(0.86f), S(0.30f), S(0.30f), new Color(0.10f, 0.10f, 0.12f));
            cv.Ellipse(X(-0.64f), Y(0.93f), S(0.10f), S(0.06f), new Color(1f, 1f, 1f, 0.25f));
        }
        if (dog)
        {
            cv.Ellipse(X(-0.36f), Y(0.02f), S(0.27f + o), S(0.52f + o), Ink, 18f);
            cv.Ellipse(X(-0.36f), Y(0.02f), S(0.27f), S(0.52f), furDark, 18f);
            cv.Ellipse(X(-0.30f), Y(0.10f), S(0.10f), S(0.30f), new Color(1f, 1f, 1f, 0.12f), 18f);
        }
        if (beetle)
        {
            Color horn = new Color(0.33f, 0.18f, 0.09f);
            cv.Line(X(0.28f), Y(0.92f), X(0.80f), Y(1.36f), S(0.24f + o * 2f), Ink);
            cv.Line(X(0.70f), Y(1.26f), X(1.02f), Y(1.22f), S(0.13f + o * 2f), Ink);
            cv.Line(X(0.28f), Y(0.92f), X(0.80f), Y(1.36f), S(0.24f), horn);
            cv.Line(X(0.70f), Y(1.26f), X(1.02f), Y(1.22f), S(0.13f), horn);
            cv.Line(X(0.34f), Y(1.00f), X(0.74f), Y(1.33f), S(0.05f), new Color(1f, 0.85f, 0.7f, 0.45f));
        }

        return cv.ToSprite(size / HeadTexScale, new Vector2(CX, CY));
    }

    private static void ShadeSphere(ArtCanvas cv, Color skin)
    {
        for (int y = 0; y < cv.H; y++)
            for (int x = 0; x < cv.W; x++)
            {
                int i = y * cv.W + x;
                if (cv.Px[i].a <= 0.01f) continue;
                float nx = ((x + 0.5f) / cv.W - CX) / R, ny = ((y + 0.5f) / cv.H - CY) / R;
                float lit = Mathf.Clamp01(0.5f + 0.32f * nx + 0.42f * ny);
                float k = Mathf.Lerp(0.80f, 1.06f, lit);
                Color c = cv.Px[i];
                cv.Px[i] = new Color(Mathf.Min(1f, c.r * k), Mathf.Min(1f, c.g * k), Mathf.Min(1f, c.b * k), c.a);
            }
    }

    private static void Eye(ArtCanvas cv, float ex, float ey, float rx, float ry, float lookX, float pupilK, Color lid)
    {
        cv.Ellipse(X(ex), Y(ey), S(rx + 0.03f), S(ry + 0.03f), Ink);
        cv.Ellipse(X(ex), Y(ey), S(rx), S(ry), new Color(0.98f, 0.98f, 0.97f));
        float ir = Mathf.Min(rx, ry) * 0.78f * pupilK;
        float ix = ex + lookX * rx * 0.35f;
        cv.SetClip(X(ex - rx), Y(ey - ry), X(ex + rx), Y(ey + ry));
        cv.Ellipse(X(ix), Y(ey - 0.01f), S(ir), S(ir * 1.08f), new Color(0.30f, 0.19f, 0.11f));
        cv.Ellipse(X(ix), Y(ey - 0.01f), S(ir * 0.52f), S(ir * 0.56f), new Color(0.05f, 0.04f, 0.04f));
        cv.Ellipse(X(ix + ir * 0.35f), Y(ey + ir * 0.38f), S(ir * 0.30f), S(ir * 0.30f), Color.white);
        cv.ClearClip();
        // 上まぶた
        cv.Arc(X(ex), Y(ey), S(rx), S(ry), 15f, 165f, S(0.07f), lid);
    }

    private static void DrawEyes(ArtCanvas cv, Expr e, bool panda, Color hair)
    {
        Color lid = Ink;
        switch (e)
        {
            case Expr.Happy:
                cv.Arc(X(0.47f), Y(0.00f), S(0.15f), S(0.12f), 10f, 170f, S(0.075f), lid);
                cv.Arc(X(0.85f), Y(0.02f), S(0.08f), S(0.11f), 10f, 170f, S(0.065f), lid);
                break;
            case Expr.Dizzy:
                // ぐるぐる目
                for (int k = 0; k < 2; k++)
                {
                    float ex = k == 0 ? 0.47f : 0.85f, rs = k == 0 ? 0.16f : 0.10f;
                    cv.Ellipse(X(ex), Y(0.03f), S(rs + 0.03f), S(rs + 0.03f), Ink);
                    cv.Ellipse(X(ex), Y(0.03f), S(rs), S(rs), Color.white);
                    float r = rs * 0.85f;
                    float a = 0f;
                    float px = X(ex), py = Y(0.03f);
                    for (int s = 0; s < 22; s++)
                    {
                        a += 40f; r *= 0.90f;
                        float qx = X(ex) + Mathf.Cos(a * Mathf.Deg2Rad) * S(r), qy = Y(0.03f) + Mathf.Sin(a * Mathf.Deg2Rad) * S(r);
                        cv.Line(px, py, qx, qy, S(0.035f), Ink);
                        px = qx; py = qy;
                    }
                }
                break;
            case Expr.Surprise:
                Eye(cv, 0.45f, 0.05f, 0.20f, 0.19f, 0.2f, 0.55f, lid);
                Eye(cv, 0.86f, 0.06f, 0.10f, 0.17f, 0.2f, 0.55f, lid);
                break;
            case Expr.Focus:
                Eye(cv, 0.45f, 0.02f, 0.18f, 0.11f, 0.8f, 1.05f, lid);
                Eye(cv, 0.86f, 0.03f, 0.095f, 0.10f, 0.8f, 1.05f, lid);
                break;
            default:
                Eye(cv, 0.45f, 0.03f, 0.18f, 0.15f, 0.5f, 1f, lid);
                Eye(cv, 0.86f, 0.04f, 0.095f, 0.135f, 0.5f, 1f, lid);
                break;
        }
    }

    private static void DrawBrows(ArtCanvas cv, Expr e, Color col)
    {
        float w = 0.09f;
        switch (e)
        {
            case Expr.Focus:
                cv.Line(X(0.28f), Y(0.30f), X(0.64f), Y(0.17f), S(w), col);
                cv.Line(X(0.76f), Y(0.18f), X(0.95f), Y(0.26f), S(w * 0.8f), col);
                break;
            case Expr.Surprise:
                cv.Arc(X(0.47f), Y(0.30f), S(0.18f), S(0.08f), 20f, 160f, S(w), col);
                cv.Arc(X(0.87f), Y(0.31f), S(0.09f), S(0.06f), 20f, 160f, S(w * 0.8f), col);
                break;
            case Expr.Dizzy:
                cv.Line(X(0.30f), Y(0.26f), X(0.62f), Y(0.33f), S(w), col);
                cv.Line(X(0.77f), Y(0.33f), X(0.95f), Y(0.26f), S(w * 0.8f), col);
                break;
            case Expr.Happy:
                cv.Arc(X(0.47f), Y(0.24f), S(0.17f), S(0.06f), 20f, 160f, S(w), col);
                cv.Arc(X(0.87f), Y(0.25f), S(0.09f), S(0.05f), 20f, 160f, S(w * 0.8f), col);
                break;
            default:
                cv.Line(X(0.30f), Y(0.25f), X(0.63f), Y(0.24f), S(w), col);
                cv.Line(X(0.77f), Y(0.24f), X(0.95f), Y(0.25f), S(w * 0.8f), col);
                break;
        }
    }

    private static void DrawMouth(ArtCanvas cv, Expr e, bool dog)
    {
        Color m = new Color(0.45f, 0.15f, 0.13f);
        float mx = dog ? 0.86f : 0.70f, my = dog ? -0.48f : -0.47f;
        switch (e)
        {
            case Expr.Happy:
                cv.SetClip(0f, 0f, 1f, Y(my + 0.06f));
                cv.Ellipse(X(mx), Y(my + 0.06f), S(0.22f + 0.035f), S(0.17f + 0.035f), Ink);
                cv.Ellipse(X(mx), Y(my + 0.06f), S(0.22f), S(0.17f), m);
                cv.Ellipse(X(mx + 0.02f), Y(my - 0.07f), S(0.12f), S(0.06f), new Color(0.92f, 0.45f, 0.45f));
                cv.ClearClip();
                cv.RoundRect(X(mx - 0.17f), Y(my + 0.0f), X(mx + 0.18f), Y(my + 0.06f), S(0.02f), Color.white);
                break;
            case Expr.Surprise:
                cv.Ellipse(X(mx), Y(my), S(0.10f + 0.035f), S(0.13f + 0.035f), Ink);
                cv.Ellipse(X(mx), Y(my), S(0.10f), S(0.13f), m);
                break;
            case Expr.Focus:
                // 歯を食いしばる
                cv.RoundRect(X(mx - 0.15f), Y(my - 0.05f), X(mx + 0.15f), Y(my + 0.05f), S(0.04f), Ink);
                cv.RoundRect(X(mx - 0.12f), Y(my - 0.025f), X(mx + 0.12f), Y(my + 0.025f), S(0.02f), Color.white);
                cv.Line(X(mx - 0.12f), Y(my), X(mx + 0.12f), Y(my), S(0.015f), new Color(0.5f, 0.5f, 0.5f));
                break;
            case Expr.Dizzy:
                {
                    float px = X(mx - 0.16f), py = Y(my);
                    for (int i = 1; i <= 8; i++)
                    {
                        float t = i / 8f;
                        float qx = X(mx - 0.16f + 0.32f * t), qy = Y(my + Mathf.Sin(t * Mathf.PI * 3f) * 0.04f);
                        cv.Line(px, py, qx, qy, S(0.045f), Ink);
                        px = qx; py = qy;
                    }
                }
                break;
            default:
                cv.Arc(X(mx), Y(my + 0.10f), S(0.14f), S(0.10f), 220f, 320f, S(0.05f), Ink);
                break;
        }
    }

    private static void DrawCap(ArtCanvas cv, Color hat, Color hatDark, float o)
    {
        float cut = 0.30f;
        // 輪郭
        cv.SetClip(0f, Y(cut) - S(o), 1f, 1f);
        cv.Ellipse(X(-0.04f), Y(cut), S(1.05f + o), S(0.84f + o), Ink);
        cv.ClearClip();
        cv.Ellipse(X(0.98f), Y(cut + 0.02f), S(0.56f + o), S(0.12f + o), Ink, -6f);
        // つば(下面を濃く)
        cv.Ellipse(X(0.98f), Y(cut + 0.02f), S(0.56f), S(0.12f), hatDark, -6f);
        cv.Ellipse(X(1.00f), Y(cut + 0.05f), S(0.52f), S(0.07f), hat, -6f);
        // クラウン
        cv.SetClip(0f, Y(cut), 1f, 1f);
        cv.Ellipse(X(-0.04f), Y(cut), S(1.05f), S(0.84f), hat);
        // 陰影とハイライト
        cv.Ellipse(X(-0.55f), Y(cut + 0.15f), S(0.65f), S(0.55f), new Color(0f, 0f, 0f, 0.22f), 0f, true);
        cv.Ellipse(X(0.25f), Y(cut + 0.62f), S(0.50f), S(0.14f), new Color(1f, 1f, 1f, 0.28f), -10f, true);
        // 縫い目
        cv.Arc(X(-0.04f), Y(cut), S(0.55f), S(0.84f), 20f, 90f, S(0.025f), new Color(0f, 0f, 0f, 0.25f), true);
        cv.ClearClip();
        // 前面のマーク(自作の記号: 白い円に斜線)
        cv.Ellipse(X(0.40f), Y(cut + 0.38f), S(0.17f), S(0.17f), Color.white);
        cv.Ellipse(X(0.40f), Y(cut + 0.38f), S(0.11f), S(0.11f), hat);
        cv.Line(X(0.31f), Y(cut + 0.30f), X(0.49f), Y(cut + 0.46f), S(0.05f), Color.white);
        // ボタン・帯
        cv.Ellipse(X(-0.04f), Y(cut + 0.86f), S(0.09f), S(0.05f), hatDark);
        cv.Line(X(-0.98f), Y(cut + 0.01f), X(0.80f), Y(cut + 0.01f), S(0.05f), hatDark);
    }

    private static void DrawHelmet(ArtCanvas cv, Color hat, Color hatDark, float o)
    {
        float cut = 0.20f;
        // 耳当て(輪郭→本体)
        cv.SetClip(0f, 0f, X(0.06f), Y(cut + 0.02f));
        cv.Ellipse(X(-0.32f), Y(0.02f), S(0.38f + o), S(0.40f + o), Ink);
        cv.ClearClip();
        cv.SetClip(0f, Y(cut) - S(o), 1f, 1f);
        cv.Ellipse(X(-0.05f), Y(cut), S(1.12f + o), S(1.00f + o), Ink);
        cv.ClearClip();
        cv.Ellipse(X(0.86f), Y(cut + 0.02f), S(0.36f + o), S(0.10f + o), Ink, -8f);
        cv.Ellipse(X(0.86f), Y(cut + 0.02f), S(0.36f), S(0.10f), hatDark, -8f);
        cv.SetClip(0f, 0f, X(0.06f), Y(cut + 0.02f));
        cv.Ellipse(X(-0.32f), Y(0.02f), S(0.38f), S(0.40f), hat);
        cv.Ellipse(X(-0.34f), Y(-0.04f), S(0.08f), S(0.08f), hatDark); // 耳穴
        cv.ClearClip();
        cv.SetClip(0f, Y(cut), 1f, 1f);
        cv.Ellipse(X(-0.05f), Y(cut), S(1.12f), S(1.00f), hat);
        cv.Ellipse(X(-0.60f), Y(cut + 0.10f), S(0.70f), S(0.60f), new Color(0f, 0f, 0f, 0.25f), 0f, true);
        cv.ClearClip();
        // つやのハイライト(ヘルメットらしさ)
        cv.Ellipse(X(0.15f), Y(cut + 0.78f), S(0.55f), S(0.15f), new Color(1f, 1f, 1f, 0.50f), -12f, true);
        cv.Ellipse(X(0.58f), Y(cut + 0.50f), S(0.12f), S(0.07f), new Color(1f, 1f, 1f, 0.55f), -30f, true);
        cv.Line(X(-1.02f), Y(cut + 0.02f), X(0.70f), Y(cut + 0.02f), S(0.05f), hatDark);
        // 側面の自作マーク
        cv.Ellipse(X(-0.10f), Y(cut + 0.42f), S(0.18f), S(0.18f), Color.white);
        cv.Ellipse(X(-0.10f), Y(cut + 0.42f), S(0.12f), S(0.12f), hat);
        cv.Line(X(-0.19f), Y(cut + 0.33f), X(-0.01f), Y(cut + 0.51f), S(0.05f), Color.white);
    }

    // ---- 体パーツ(9スライスの角丸。白で描いて色はSpriteRenderer.colorで付ける) ----
    private static Sprite _part;
    public static Sprite Part
    {
        get
        {
            if (_part != null) return _part;
            const int n = 96;
            var cv = new ArtCanvas(n, n);
            float r = 36f / n, ow = 6f / n;
            cv.RoundRect(0f, 0f, 1f, 1f, r, new Color(0.16f, 0.13f, 0.13f));
            cv.RoundRect(ow, ow, 1f - ow, 1f - ow, r - ow, Color.white);
            // 左上から光: 右端・下端を少し暗く、左上を少し明るく
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    Color c = cv.Px[i];
                    if (c.r < 0.5f) continue; // 輪郭は触らない
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float k = Mathf.Lerp(0.80f, 1.0f, Mathf.Clamp01(0.35f + 0.55f * (1f - u) * 0.6f + 0.55f * v * 0.6f));
                    cv.Px[i] = new Color(c.r * k, c.g * k, c.b * k, c.a);
                }
            float b = 38f;
            _part = cv.ToSprite(800f, new Vector2(0.5f, 0.5f), new Vector4(b, b, b, b));
            return _part;
        }
    }

    // ---- バット(先が太い木製) ----------------------------------------
    private static Sprite _bat;
    public static Sprite Bat
    {
        get
        {
            if (_bat != null) return _bat;
            int w = 40, h = 320;
            var cv = new ArtCanvas(w, h);
            cv.Mipmaps = true;
            Color wood = new Color(0.80f, 0.58f, 0.33f);
            for (int pass = 0; pass < 2; pass++)
            {
                float g = pass == 0 ? 3f / w : 0f;
                Color c = pass == 0 ? Ink : wood;
                // グリップ(下)→バレル(上)
                cv.Line(0.5f, 0.06f, 0.5f, 0.45f, 0.30f + g * 2f, c);
                cv.Line(0.5f, 0.45f, 0.5f, 0.70f, 0.52f + g * 2f, c);
                cv.Line(0.5f, 0.62f, 0.5f, 0.93f, 0.80f + g * 2f, c);
                cv.Ellipse(0.5f, 0.04f, 0.28f + g, 0.022f + g * w / h, c); // ノブ
            }
            // 光沢と木目
            cv.Line(0.38f, 0.10f, 0.36f, 0.92f, 0.10f, new Color(1f, 0.95f, 0.85f, 0.45f), true);
            cv.Line(0.66f, 0.10f, 0.68f, 0.92f, 0.12f, new Color(0.35f, 0.20f, 0.08f, 0.35f), true);
            cv.RoundRect(0.2f, 0.10f, 0.8f, 0.30f, 0.05f, new Color(0.15f, 0.13f, 0.15f, 0.9f), true); // グリップテープ
            _bat = cv.ToSprite(h, new Vector2(0.5f, 0.5f));
            return _bat;
        }
    }

    // ---- グラブ --------------------------------------------------------
    private static Sprite _glove;
    public static Sprite Glove
    {
        get
        {
            if (_glove != null) return _glove;
            int n = 96;
            var cv = new ArtCanvas(n, n);
            cv.Mipmaps = true;
            Color lea = new Color(0.66f, 0.40f, 0.18f), dark = new Color(0.42f, 0.24f, 0.10f);
            for (int pass = 0; pass < 2; pass++)
            {
                float g = pass == 0 ? 0.04f : 0f;
                Color c = pass == 0 ? Ink : lea;
                cv.Ellipse(0.52f, 0.46f, 0.36f + g, 0.40f + g, c);
                cv.Ellipse(0.22f, 0.40f, 0.14f + g, 0.24f + g, c, 25f);   // 親指
                for (int f = 0; f < 4; f++)
                    cv.Ellipse(0.40f + f * 0.11f, 0.78f, 0.07f + g, 0.16f + g, c);
            }
            cv.Ellipse(0.55f, 0.48f, 0.20f, 0.22f, dark, 0f, true); // ポケット
            cv.Line(0.30f, 0.62f, 0.75f, 0.62f, 0.025f, dark, true);
            cv.Ellipse(0.40f, 0.70f, 0.12f, 0.06f, new Color(1f, 1f, 1f, 0.25f), 0f, true);
            _glove = cv.ToSprite(n, new Vector2(0.5f, 0.5f));
            return _glove;
        }
    }

    // ---- ピンストライプ(ユニフォームの縦じま。タイル用) ----------------------
    private static Sprite _stripe;
    public static Sprite Pinstripe
    {
        get
        {
            if (_stripe != null) return _stripe;
            int n = 32;
            var cv = new ArtCanvas(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float a = (x == 0 || x == 1) ? 0.75f : (x == 2 ? 0.25f : 0f);
                    cv.Px[y * n + x] = new Color(0.72f, 0.10f, 0.14f, a);
                }
            _stripe = cv.ToSprite(n / 0.085f, new Vector2(0.5f, 0.5f), default(Vector4), true);
            return _stripe;
        }
    }

    // ---- 影(ぼかした楕円) --------------------------------------------
    private static Sprite _shadow;
    public static Sprite SoftShadow
    {
        get
        {
            if (_shadow != null) return _shadow;
            int n = 64;
            var cv = new ArtCanvas(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = Mathf.Clamp01(1f - d);
                    cv.Px[y * n + x] = new Color(0f, 0f, 0f, a * a * (3f - 2f * a));
                }
            _shadow = cv.ToSprite(n, new Vector2(0.5f, 0.5f));
            return _shadow;
        }
    }
}
