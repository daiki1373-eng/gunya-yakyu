using UnityEngine;

// ============================================================
// BackgroundBuilder.cs
// 球場の背景をコード生成のSprite/図形だけで簡易再現する。
// (HTML版 stadium()/buildField() の雰囲気: 空・雲・観客席・
//  スコアボード・広告看板・フェンス・芝と土・マウンド)
//
// 視点について: HTML版は本塁後方の低い視点だが、フェーズ1のUnity版は
// ラグドールの関節が一番よく見える「横から見た視点」で割り切っている。
// レイアウト定数はここに集約し、GameController からも参照する。
// ============================================================
public static class BackgroundBuilder
{
    public const float GroundTopY = -3.0f;   // 地面の上面
    public const float MoundX = -3.4f;       // マウンド中心
    public const float MoundTopY = -2.70f;   // マウンド上面(投手の足元)
    public const float PlateX = 5.6f;        // ホームプレート
    public const float BatterX = 6.55f;      // 打者の足元
    public const float FenceX = -8.7f;       // 外野フェンス

    public static void Build(Transform parent)
    {
        var root = new GameObject("Background");
        root.transform.SetParent(parent, false);
        Transform t = root.transform;

        // --- 空 (HTML版 sky(): #4ca7da → #d6ece9 のグラデーション) ---
        var skySprite = SpriteFactory.GradientVertical(
            new Color(0.30f, 0.65f, 0.85f), new Color(0.84f, 0.93f, 0.91f));
        SpriteFactory.Panel("Sky", t, new Vector2(0.6f, 1.4f), new Vector2(30f, 12.4f),
            skySprite, Color.white, -10);

        // --- 雲 ---
        DrawCloud(t, new Vector2(-6.5f, 4.9f), 1.6f);
        DrawCloud(t, new Vector2(1.5f, 5.6f), 2.1f);
        DrawCloud(t, new Vector2(7.8f, 4.6f), 1.3f);

        // --- 観客席 ---
        SpriteFactory.Box("StandBase", t, new Vector2(0.6f, 3.30f), new Vector2(23f, 2.9f),
            new Color(0.52f, 0.56f, 0.60f), -8);
        var crowdPalette = new Color[]
        {
            new Color(0.95f, 0.89f, 0.77f), new Color(0.77f, 0.85f, 0.75f),
            new Color(0.95f, 0.74f, 0.44f), new Color(0.28f, 0.39f, 0.42f),
            new Color(0.82f, 0.55f, 0.41f)
        };
        var crowd = SpriteFactory.Crowd(crowdPalette, 12345);
        SpriteFactory.Panel("Crowd", t, new Vector2(0.6f, 3.45f), new Vector2(22.4f, 2.3f),
            crowd, Color.white, -7);
        // 席の段差ライン
        for (int i = 0; i < 4; i++)
        {
            SpriteFactory.Box("StandStep" + i, t, new Vector2(0.6f, 2.45f + i * 0.62f),
                new Vector2(23f, 0.05f), new Color(0.42f, 0.46f, 0.50f), -6);
        }

        // --- スコアボード (装飾の板。スコア表示はUI側の上部バーで行う) ---
        SpriteFactory.Box("ScoreboardFrame", t, new Vector2(0.6f, 4.95f), new Vector2(6.8f, 1.35f),
            new Color(0.10f, 0.16f, 0.12f), -6);
        SpriteFactory.Box("ScoreboardInner", t, new Vector2(0.6f, 4.95f), new Vector2(6.4f, 1.05f),
            new Color(0.13f, 0.28f, 0.18f), -5);

        // --- 広告看板の列 (HTML版の看板文言の色味だけ再現) ---
        string[] adNames = { "Ad1", "Ad2", "Ad3", "Ad4", "Ad5", "Ad6" };
        Color[] adColors = new Color[]
        {
            new Color(0.92f, 0.95f, 0.88f), new Color(0.15f, 0.20f, 0.35f),
            new Color(0.95f, 0.93f, 0.85f), new Color(0.88f, 0.85f, 0.70f),
            new Color(0.92f, 0.95f, 0.88f), new Color(0.15f, 0.20f, 0.35f)
        };
        for (int i = 0; i < adNames.Length; i++)
        {
            float x = -7.6f + i * 3.3f;
            SpriteFactory.Box(adNames[i], t, new Vector2(x, 1.55f), new Vector2(3.1f, 0.75f), adColors[i], -5);
        }
        // 看板の土台壁
        SpriteFactory.Box("AdWall", t, new Vector2(0.6f, 1.02f), new Vector2(23f, 0.35f),
            new Color(0.35f, 0.45f, 0.38f), -6);

        // --- 外野フェンス (左端) ---
        SpriteFactory.Box("Fence", t, new Vector2(FenceX, -1.1f), new Vector2(0.28f, 3.8f),
            new Color(0.16f, 0.38f, 0.24f), -4);
        SpriteFactory.Box("FenceTop", t, new Vector2(FenceX, 0.86f), new Vector2(0.30f, 0.10f),
            new Color(0.98f, 0.85f, 0.25f), -3);

        // --- グラウンド ---
        // 土(下層)
        SpriteFactory.Box("DirtBase", t, new Vector2(0.6f, -4.26f), new Vector2(26f, 2.28f),
            new Color(0.62f, 0.45f, 0.30f), -4);
        // 芝の帯(上面がちょうど GroundTopY になるよう配置)
        SpriteFactory.Box("GrassTop", t, new Vector2(0.6f, -3.09f), new Vector2(26f, 0.18f),
            new Color(0.42f, 0.66f, 0.34f), -3);
        // 芝の縞(刈り込み表現): フィールド奥側に薄く
        for (int i = 0; i < 6; i++)
        {
            if (i % 2 == 0) continue;
            SpriteFactory.Box("GrassStripe" + i, t, new Vector2(-7.5f + i * 3.2f, -1.95f),
                new Vector2(3.2f, 1.95f), new Color(0.40f, 0.63f, 0.32f, 0.35f), -3);
        }
        // 外野の芝斜面っぽい帯
        SpriteFactory.Box("FieldBack", t, new Vector2(0.6f, -1.95f), new Vector2(26f, 1.95f),
            new Color(0.46f, 0.70f, 0.37f), -4);

        // マウンド(平たい楕円)
        SpriteFactory.Panel("Mound", t, new Vector2(MoundX, -2.93f), new Vector2(2.4f, 0.5f),
            SpriteFactory.Circle, new Color(0.72f, 0.55f, 0.38f), -2);
        // ピッチャープレート
        SpriteFactory.Box("PitchPlate", t, new Vector2(MoundX, MoundTopY + 0.01f), new Vector2(0.55f, 0.05f),
            Color.white, -1);

        // ホームプレートとバッターボックス
        SpriteFactory.Box("HomePlate", t, new Vector2(PlateX, GroundTopY + 0.03f), new Vector2(0.5f, 0.07f),
            Color.white, -1);
        SpriteFactory.Box("BatterBox", t, new Vector2(BatterX - 0.15f, GroundTopY + 0.015f), new Vector2(0.95f, 0.03f),
            new Color(1f, 1f, 1f, 0.8f), -1);

        // 地面の当たり判定(静的コライダー)。上面が GroundTopY と一致する。
        var ground = new GameObject("Ground");
        ground.transform.SetParent(t, false);
        ground.transform.position = new Vector3(0.6f, GroundTopY - 0.5f, 0f);
        var col = ground.AddComponent<BoxCollider2D>();
        col.size = new Vector2(40f, 1f);

        // マウンドの当たり判定。上面が MoundTopY と一致する(投手の足元)。
        var moundCol = new GameObject("MoundCollider");
        moundCol.transform.SetParent(t, false);
        moundCol.transform.position = new Vector3(MoundX, MoundTopY - 0.15f, 0f);
        var mc = moundCol.AddComponent<BoxCollider2D>();
        mc.size = new Vector2(2.2f, 0.3f);
    }

    private static void DrawCloud(Transform parent, Vector2 pos, float scale)
    {
        Color cloud = new Color(1f, 1f, 1f, 0.9f);
        SpriteFactory.Panel("CloudA", parent, pos, new Vector2(1.5f * scale, 0.55f * scale),
            SpriteFactory.Circle, cloud, -9);
        SpriteFactory.Panel("CloudB", parent, pos + new Vector2(0.45f * scale, 0.10f * scale),
            new Vector2(1.0f * scale, 0.45f * scale), SpriteFactory.Circle, cloud, -9);
        SpriteFactory.Panel("CloudC", parent, pos + new Vector2(-0.5f * scale, 0.06f * scale),
            new Vector2(0.9f * scale, 0.4f * scale), SpriteFactory.Circle, cloud, -9);
    }
}
