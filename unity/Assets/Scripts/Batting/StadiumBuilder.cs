using System.Collections.Generic;
using UnityEngine;

// ============================================================
// StadiumBuilder.cs
// ナイターの球場を組み立てる: 夜空 / 2層スタンドと観客 / 照明塔 / 中堅のバックスクリーンと
// 電光掲示板 / 内野スタンドとベンチ / ポール / フェンス広告(架空の社名) / 芝の刈り込み模様。
// 観客は歓声に合わせて立ち上がる(2コマ切り替え)。名称・数値はすべて自前の仮置き。
// ============================================================
public class StadiumBuilder : MonoBehaviour
{
    // 描画順(BattingStage: 地面-90〜-80 / フェンス-60 / 影-50)
    private const int OSky = -130;
    // 描画の前後(地面 -90〜-80 より上、外野フェンス -60 より下)
    private const int OUpper = -79, OUpperAisle = -78, ORoof = -77, OGlow = -76;
    private const int OLower = -72, OLowerShade = -71, OLowerAisle = -71, OSoffit = -70, OFascia = -69;
    private const int OBoard = -64, OBoardText = -61;

    private Transform _root;
    private Font _font;

    private class CrowdSeg { public SpriteRenderer Sr; public MeshRenderer Mr; public int Side, Variant; public float Phase, Period, Gate; public Color Tint; }
    private readonly List<Material> _ribbonMats = new List<Material>();
    private readonly List<CrowdSeg> _crowd = new List<CrowdSeg>();
    private readonly List<SpriteRenderer> _ribbons = new List<SpriteRenderer>();
    private readonly List<Vector3> _sparkSpots = new List<Vector3>();
    private readonly List<SpriteRenderer> _sparks = new List<SpriteRenderer>();
    private float _cheerUntil, _cheerAmp;
    private TextMesh _boardCount, _boardScore, _boardSpeed, _boardBatter;
    private SpriteRenderer[] _lampB, _lampS, _lampO;

    public static StadiumBuilder Build(Transform root)
    {
        var sb = root.gameObject.AddComponent<StadiumBuilder>();
        sb._root = root;
        sb._font = BattingUI.GetFont();
        sb.BuildField();
        sb.BuildSky();
        sb.BuildFairStands();
        sb.BuildFoulStands();
        sb.BuildLights();
        sb.BuildScoreboard();
        sb.BuildFenceExtras();
        GameAudio.OnCheer += sb.Cheer;
        return sb;
    }

    private void OnDestroy() { GameAudio.OnCheer -= Cheer; }

    // ---- 汎用 ------------------------------------------------------
    private SpriteRenderer Quad(string name, Sprite sp, Color c, int order, Vector3 pos, Vector3 euler, Vector2 size,
        SpriteDrawMode mode = SpriteDrawMode.Simple)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.color = c;
        sr.sortingOrder = order;
        if (mode == SpriteDrawMode.Simple)
        {
            Vector2 bs = sp.bounds.size;
            go.transform.localScale = new Vector3(size.x / bs.x, size.y / bs.y, 1f);
        }
        else
        {
            sr.drawMode = mode;
            sr.size = size;
        }
        return sr;
    }

    private SpriteRenderer Flat(string name, Sprite sp, Color c, int order, float x, float z, float w, float l, float roll,
        bool tiled = false)
    {
        return Quad(name, sp, c, order, new Vector3(x, 0f, z), new Vector3(90f, 0f, roll), new Vector2(w, l),
            tiled ? SpriteDrawMode.Tiled : SpriteDrawMode.Simple);
    }

    private TextMesh Text(string name, string s, Vector3 pos, Quaternion rot, float height, Color c, TextAnchor anchor, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        var tm = go.AddComponent<TextMesh>();
        tm.font = _font;
        tm.fontSize = 64;
        tm.characterSize = height * 10f / 64f;
        tm.anchor = anchor;
        tm.alignment = TextAlignment.Center;
        tm.color = c;
        tm.text = s;
        var mr = go.GetComponent<MeshRenderer>();
        if (_font != null) mr.sharedMaterial = _font.material;
        mr.sortingOrder = order;
        return tm;
    }

    private static Vector3 Dir(float phi)
    {
        float r = phi * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
    }

    // ---- グラウンド ------------------------------------------------
    private void BuildField()
    {
        Sprite grass = StadiumArt.Grass, dirt = StadiumArt.Dirt, disc = StadiumArt.DirtDisc, white = SpriteFactory.White;
        Color line = new Color(1f, 1f, 1f, 0.95f);
        const int G = -90;

        Flat("Grass", grass, Color.white, G, 0f, 150f, 700f, 330f, 0f, true);
        // 刈り込み模様(斜めの格子)
        for (int i = -14; i <= 14; i++)
        {
            float off = i * 12f;
            Flat("MowA" + i, white, new Color(1f, 1f, 1f, 0.085f), G + 1, off * 0.7071f, 60f + off * 0.7071f, 6f, 300f, 45f);
            Flat("MowB" + i, white, new Color(0f, 0.05f, 0f, 0.09f), G + 1, -off * 0.7071f, 60f + off * 0.7071f, 6f, 300f, -45f);
        }
        // 外野のウォーニングゾーン(土)。なめらかな弧で、芝との境目は少しぼかす
        System.Func<float, float> fr = phi => BattingConfig.FenceRadius(Mathf.Min(Mathf.Abs(phi), 45f));
        Color trackCol = new Color(0.85f, 0.80f, 0.78f);
        StadiumMesh.GroundArc(_root, "Track", dirt.texture, G + 2, -47f, 47f, 0.5f, phi => fr(phi) - 5.3f, phi => fr(phi) + 0.3f,
            trackCol, trackCol, 8f);
        StadiumMesh.GroundArc(_root, "TrackEdge", dirt.texture, G + 2, -47f, 47f, 0.5f, phi => fr(phi) - 5.9f, phi => fr(phi) - 5.3f,
            new Color(trackCol.r, trackCol.g, trackCol.b, 0f), trackCol, 8f);
        // 照明の届きにくい外野の奥は少し暗く(奥行き)
        StadiumMesh.GroundArc(_root, "OutfieldFalloff", null, G + 3, -47f, 47f, 1f, phi => fr(phi) - 26f, phi => fr(phi) + 0.3f,
            new Color(0f, 0.01f, 0.03f, 0f), new Color(0f, 0.01f, 0.03f, 0.30f), 8f);
        // 内野まわりに照明が集まった明るさ
        Flat("LightPool", StadiumArt.Glow, new Color(1f, 0.95f, 0.8f, 0.10f), G + 3, 0f, 22f, 110f, 90f, 0f);
        // 内野
        Flat("InfieldDirt", disc, Color.white, G + 4, 0f, 17f, 56f, 56f, 0f);
        Flat("InfieldGrass", grass, new Color(1.05f, 1.08f, 1.02f), G + 5, 0f, 19.4f, 23f, 23f, 45f, true);
        Flat("InfieldGrassMow", white, new Color(1f, 1f, 1f, 0.05f), G + 5, -2.9f, 22.3f, 23f, 5.75f, 45f);
        Flat("InfieldGrassMow2", white, new Color(1f, 1f, 1f, 0.05f), G + 5, 2.9f, 16.5f, 23f, 5.75f, 45f);
        Flat("PathHome1B", dirt, Color.white, G + 6, 9.7f, 9.7f, 2.0f, 27.4f, -45f, true);
        Flat("Path3BHome", dirt, Color.white, G + 6, -9.7f, 9.7f, 2.0f, 27.4f, 45f, true);
        Flat("HomeDirt", disc, Color.white, G + 7, 0f, 0f, 18f, 18f, 0f);
        Flat("Base1B", disc, Color.white, G + 7, 19.4f, 19.4f, 5f, 5f, 0f);
        Flat("Base2B", disc, Color.white, G + 7, 0f, 38.8f, 5f, 5f, 0f);
        Flat("Base3B", disc, Color.white, G + 7, -19.4f, 19.4f, 5f, 5f, 0f);
        // マウンド(盛り上がりを陰影で)
        Flat("Mound", disc, new Color(0.92f, 0.88f, 0.86f), G + 8, 0f, BattingConfig.MoundZ, 5.6f, 5.6f, 0f);
        Flat("MoundTop", disc, new Color(1.08f, 1.04f, 1.0f), G + 8, 0f, BattingConfig.MoundZ + 0.25f, 2.6f, 2.2f, 0f);
        Flat("PitchPlate", white, line, G + 9, 0f, BattingConfig.MoundZ, 0.7f, 0.16f, 0f);

        Flat("FoulLineR", white, line, G + 9, 38.9f, 38.9f, 0.16f, 110f, -45f);
        Flat("FoulLineL", white, line, G + 9, -38.9f, 38.9f, 0.16f, 110f, 45f);
        Flat("Bag1B", white, line, G + 10, 19.4f, 19.4f, 0.9f, 0.9f, 45f);
        Flat("Bag2B", white, line, G + 10, 0f, 38.8f, 0.9f, 0.9f, 45f);
        Flat("Bag3B", white, line, G + 10, -19.4f, 19.4f, 0.9f, 0.9f, 45f);
        Flat("HomePlate", white, line, G + 10, 0f, 0f, 0.8f, 0.8f, 0f);
        BoxOutline(-1.40f, line);
        BoxOutline(1.40f, line);
        // ネクストバッターズサークル
        Flat("OnDeckL", BattingSprites.Ring, new Color(1f, 1f, 1f, 0.7f), G + 9, -11f, -3f, 1.6f, 1.6f, 0f);
        Flat("OnDeckR", BattingSprites.Ring, new Color(1f, 1f, 1f, 0.7f), G + 9, 11f, -3f, 1.6f, 1.6f, 0f);
    }

    private void BoxOutline(float cx, Color col)
    {
        Sprite white = SpriteFactory.White;
        float w = 1.2f, l = 2.2f, t = 0.07f;
        int o = -80;
        Flat("BoxL", white, col, o, cx - w * 0.5f, 0f, t, l, 0f);
        Flat("BoxR", white, col, o, cx + w * 0.5f, 0f, t, l, 0f);
        Flat("BoxF", white, col, o, cx, l * 0.5f, w, t, 0f);
        Flat("BoxB", white, col, o, cx, -l * 0.5f, w, t, 0f);
    }

    // ---- 夜空 ------------------------------------------------------
    private void BuildSky()
    {
        Sprite sky = StadiumArt.Sky;
        float R = 330f;
        for (float phi = -120f; phi <= 120f; phi += 10f)
        {
            Vector3 p = Dir(phi) * R + Vector3.up * 100f;
            float segW = 2f * Mathf.PI * R * (10f / 360f) * 1.04f;
            Quad("Sky", sky, Color.white, OSky, p, new Vector3(0f, phi, 0f), new Vector2(segW, 200f), SpriteDrawMode.Tiled);
        }
        // 地平線あたりの街明かりのにじみ
        for (float phi = -100f; phi <= 100f; phi += 25f)
        {
            Vector3 p = Dir(phi) * (R - 5f) + Vector3.up * 8f;
            Quad("CityGlow", StadiumArt.Glow, new Color(1f, 0.65f, 0.4f, 0.10f), OSky + 1, p, new Vector3(0f, phi, 0f),
                new Vector2(160f, 50f));
        }
    }

    // ---- 外野〜内野の2層スタンド -------------------------------------
    private SpriteRenderer CrowdPlane(string name, int side, Vector3 basePos, float yaw, float run, float rise, float width,
        int order, Color tint, Vector3 outward)
    {
        float L = Mathf.Sqrt(run * run + rise * rise);
        float tilt = Mathf.Atan2(run, rise) * Mathf.Rad2Deg;
        Vector3 center = basePos + outward * (run * 0.5f) + Vector3.up * (rise * 0.5f);
        int variant = Random.Range(0, 4);
        var sr = Quad(name, StadiumArt.CrowdSlice(side, 0, variant), tint, order, center, new Vector3(tilt, yaw, 0f),
            new Vector2(width, L), SpriteDrawMode.Tiled);
        _crowd.Add(new CrowdSeg
        {
            Sr = sr, Side = side, Variant = variant, Phase = Random.value * 10f,
            Period = Random.Range(0.16f, 0.32f), Gate = Random.value, Tint = tint
        });
        // 観客席の中のフラッシュ候補地点
        for (int k = 0; k < 3; k++)
        {
            float u = Random.Range(-0.45f, 0.45f), v = Random.Range(0.1f, 0.9f);
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            _sparkSpots.Add(basePos + outward * (run * v) + Vector3.up * (rise * v) + right * (u * width) - outward * 0.3f);
        }
        return sr;
    }

    private void Shade(Vector3 basePos, float yaw, float run, float rise, float width, int order, float from, float alpha, Vector3 outward)
    {
        // スタンド面の上側に、上の屋根(上段)が落とす影
        float L = Mathf.Sqrt(run * run + rise * rise);
        float tilt = Mathf.Atan2(run, rise) * Mathf.Rad2Deg;
        float len = L * (1f - from);
        float mid = from + (1f - from) * 0.5f;
        Vector3 c = basePos + outward * (run * mid) + Vector3.up * (rise * mid) - outward * 0.05f;
        var g = SpriteFactory.GradientVertical(new Color(0f, 0f, 0.02f, alpha), new Color(0f, 0f, 0.02f, 0f));
        Quad("StandShade", g, Color.white, order, c, new Vector3(tilt, yaw, 0f), new Vector2(width, len));
    }

    // 外野〜内野の2層スタンド。なめらかな曲面で、上段が下段にかぶさる(ひさしの裏が見える)立体的な形。
    private void BuildFairStands()
    {
        float[] cuts = { -56f, -48f, -40f, -32f, -24f, -16f, -10f, -6f, 6f, 10f, 16f, 24f, 32f, 40f, 48f, 56f };
        System.Func<float, float> rf = phi => BattingConfig.FenceRadius(Mathf.Min(Mathf.Abs(phi), 45f));
        const float h0 = 4.4f, run0 = 22f, rise0 = 16f;
        const float over = 0.72f;                 // 上段の先端が下段の奥行きのどこまで張り出すか
        const float run1 = 18f, rise1 = 20f;
        float hU = h0 + rise0 + 1.4f;             // 上段の床の高さ
        System.Func<float, float> r0 = p => rf(p) + 1.2f;
        System.Func<float, float> rBack = p => rf(p) + 1.2f + run0;
        System.Func<float, float> rU = p => rf(p) + 1.2f + run0 * over;
        System.Func<float, float> rU1 = p => rU(p) + 0.3f;
        System.Func<float, float> rTop = p => rU(p) + 0.3f + run1 + 0.8f;
        float yTop = hU + rise1;
        Color gold = new Color(1.1f, 0.85f, 0.2f);
        for (int c = 0; c + 1 < cuts.Length; c++)
        {
            float a = cuts[c], b = cuts[c + 1], mid = (a + b) * 0.5f;
            int side = mid < -14f ? 1 : 0;        // 左翼側はビジター応援席
            bool center = Mathf.Abs(mid) <= 6.1f;
            bool board = Mathf.Abs(mid) <= 10.1f;
            float uo = Random.value;

            // ---- 下段 ----
            if (center)
            {
                // バックスクリーン: 打者の目のため暗い無地。縦の継ぎ目だけうっすら入れて、のっぺりさせない
                StadiumMesh.ConeBand(_root, "BackScreen", StadiumArt.Fence.texture, OLower, a, b, 0.5f, r0, h0, rBack, h0 + rise0,
                    new Color(0.30f, 0.34f, 0.33f), new Color(0.16f, 0.18f, 0.18f), 3f, 27f);
                StadiumMesh.CurvedWall(_root, "BackScreenTop", null, OLowerAisle, a, b, 0.5f, rBack, h0 + rise0 - 0.4f, h0 + rise0 + 0.1f,
                    new Color(0.02f, 0.03f, 0.03f), new Color(0.05f, 0.07f, 0.06f), 4f, 0.05f);
            }
            else
            {
                var lo = StadiumMesh.ConeBand(_root, "LowerStand", StadiumArt.Crowd(side, 0).texture, OLower, a, b, 0.5f,
                    r0, h0, rBack, h0 + rise0, new Color(1.08f, 1.06f, 1.04f), new Color(0.38f, 0.40f, 0.48f), 32f, 8f, uo, 4);
                AddCrowd(lo, side);
                Aisles(a, b, r0, h0, rBack, h0 + rise0, OLowerAisle, 1f);
                Spots(a, b, r0, h0, rBack, h0 + rise0);
            }
            // 下段の前のガラスの手すり
            StadiumMesh.CurvedWall(_root, "Rail", null, OLowerAisle, a, b, 0.5f, p => rf(p) + 1.05f, h0 - 0.3f, h0 + 0.9f,
                new Color(0.6f, 0.75f, 0.9f, 0.10f), new Color(0.9f, 0.95f, 1f, 0.30f), 4f);

            // ---- 上段の張り出し(前面の厚み + 裏の天井と照明) ----
            StadiumMesh.CurvedWall(_root, "Fascia", null, OFascia, a, b, 0.5f, rU, hU - 3.2f, hU + 0.25f,
                new Color(0.045f, 0.045f, 0.06f), new Color(0.085f, 0.085f, 0.10f), 4f);
            StadiumMesh.CurvedWall(_root, "FasciaGold", null, OFascia + 1, a, b, 0.5f, rU, hU + 0.0f, hU + 0.25f, gold, gold, 4f, 0.05f);
            StadiumMesh.CurvedWall(_root, "FasciaGold2", null, OFascia + 1, a, b, 0.5f, rU, hU - 3.2f, hU - 2.95f, gold, gold, 4f, 0.05f);
            if (!board)
            {
                var rib = StadiumMesh.CurvedWall(_root, "Ribbon", null, OFascia + 1, a, b, 0.5f, rU, hU - 2.4f, hU - 0.7f,
                    Color.white, Color.white, 4f, 0.06f);
                var m = new Material(rib.sharedMaterial); rib.sharedMaterial = m; _ribbonMats.Add(m);
            }
            // 天井(下から見える面)。手前は暗く、照明の列、奥はさらに暗い
            System.Func<float, float> s1 = p => rU(p) + 1.8f, s2 = p => rU(p) + 2.3f;
            float ySoff = hU - 3.2f;
            StadiumMesh.ConeBand(_root, "Soffit", null, OSoffit, a, b, 0.5f, rU, ySoff, s1, ySoff + 0.25f,
                new Color(0.075f, 0.08f, 0.095f), new Color(0.06f, 0.065f, 0.08f), 8f, 8f);
            StadiumMesh.ConeBand(_root, "SoffitLight", null, OSoffit, a, b, 0.5f, s1, ySoff + 0.25f, s2, ySoff + 0.32f,
                new Color(1.7f, 1.5f, 1.2f), new Color(1.7f, 1.5f, 1.2f), 8f, 8f);
            StadiumMesh.ConeBand(_root, "SoffitBack", null, OSoffit, a, b, 0.5f, s2, ySoff + 0.32f, p => rBack(p) + 1.5f, ySoff + 1.4f,
                new Color(0.055f, 0.06f, 0.075f), new Color(0.02f, 0.02f, 0.03f), 8f, 8f);

            // ---- 上段 ----
            if (board)
                StadiumMesh.ConeBand(_root, "BoardStructure", null, OUpper, a, b, 0.5f, rU1, hU, p => rU1(p) + run1, yTop,
                    new Color(0.08f, 0.09f, 0.11f), new Color(0.05f, 0.055f, 0.07f), 8f, 8f);
            else
            {
                var up = StadiumMesh.ConeBand(_root, "UpperStand", StadiumArt.Crowd(side, 0).texture, OUpper, a, b, 0.5f,
                    rU1, hU, p => rU1(p) + run1, yTop, new Color(0.95f, 0.95f, 1.0f), new Color(0.50f, 0.52f, 0.60f), 32f, 8f, uo + 0.5f, 4);
                AddCrowd(up, side);
                Aisles(a, b, rU1, hU, p => rU1(p) + run1, yTop, OUpperAisle, 0.85f);
                Spots(a, b, rU1, hU, p => rU1(p) + run1, yTop);
                StadiumMesh.CurvedWall(_root, "UpperRail", null, OUpperAisle, a, b, 0.5f, p => rU(p) + 0.2f, hU + 0.25f, hU + 1.15f,
                    new Color(0.6f, 0.75f, 0.9f, 0.10f), new Color(0.9f, 0.95f, 1f, 0.26f), 4f);
            }

            // ---- 屋根(後ろの壁 + 照明の帯 + 手前に張り出す屋根の裏) ----
            StadiumMesh.CurvedWall(_root, "RoofLight", null, ORoof, a, b, 0.5f, rTop, yTop + 0.1f, yTop + 0.55f,
                new Color(2.2f, 2.05f, 1.75f), new Color(2.2f, 2.05f, 1.75f), 4f);
            StadiumMesh.CurvedWall(_root, "Roof", null, ORoof, a, b, 0.5f, rTop, yTop + 0.55f, yTop + 3.4f,
                new Color(0.10f, 0.11f, 0.13f), new Color(0.14f, 0.15f, 0.17f), 4f);
            StadiumMesh.ConeBand(_root, "Canopy", null, ORoof, a, b, 0.5f, rTop, yTop + 3.4f, p => rTop(p) - 8f, yTop + 4.6f,
                new Color(0.07f, 0.075f, 0.09f), new Color(0.11f, 0.12f, 0.14f), 8f, 8f);
        }
    }

    private void AddCrowd(MeshRenderer mr, int side)
    {
        _crowd.Add(new CrowdSeg
        {
            Mr = mr, Side = side, Phase = Random.value * 10f, Period = Random.Range(0.16f, 0.32f), Gate = Random.value, Tint = Color.white
        });
    }

    // 通路(上下の階段)。4度ごと
    private void Aisles(float a, float b, System.Func<float, float> rA, float yA, System.Func<float, float> rB, float yB, int order, float bright)
    {
        Color lo = new Color(0.20f * bright, 0.21f * bright, 0.24f * bright, 1f), hi = new Color(0.09f * bright, 0.10f * bright, 0.12f * bright, 1f);
        for (float p = a; p <= b + 0.01f; p += 4f)
            StadiumMesh.ConeBand(_root, "Aisle", null, order, p - 0.13f, p + 0.13f, 0.26f, rA, yA, rB, yB, lo, hi, 8f, 8f);
    }

    // カメラのフラッシュが光る候補地点
    private void Spots(float a, float b, System.Func<float, float> rA, float yA, System.Func<float, float> rB, float yB)
    {
        for (int k = 0; k < 5; k++)
        {
            float p = Random.Range(a, b), t = Random.Range(0.1f, 0.9f);
            Vector3 d = Dir(p);
            _sparkSpots.Add(d * Mathf.Lerp(rA(p), rB(p), t) + Vector3.up * Mathf.Lerp(yA, yB, t) - d * 0.3f);
        }
    }

    // ---- 内野スタンド(ファウルライン沿い)とベンチ -----------------------
    private void BuildFoulStands()
    {
        Sprite white = SpriteFactory.White;
        for (int s = -1; s <= 1; s += 2)   // -1=三塁側 +1=一塁側
        {
            Vector3 along = new Vector3(s * 0.7071f, 0f, 0.7071f);
            Vector3 outward = new Vector3(s * 0.7071f, 0f, -0.7071f);
            float yaw = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
            int side = s > 0 ? 0 : 1;
            for (float d = 6f; d <= 86f; d += 8f)
            {
                Vector3 baseLine = along * d;
                Vector3 wallPos = baseLine + outward * 13f + Vector3.up * 1.1f;
                Quad("FoulWall", white, new Color(0.10f, 0.16f, 0.30f), OFascia, wallPos, new Vector3(0f, yaw, 0f), new Vector2(8.4f, 2.2f));
                CrowdPlane("FoulStand", side, baseLine + outward * 13.4f + Vector3.up * 2.2f, yaw, 16f, 11f, 8.4f, OLower,
                    new Color(0.95f, 0.95f, 0.98f), outward);
                Vector3 roof = baseLine + outward * 30.2f + Vector3.up * 14.5f;
                Quad("FoulRoof", white, new Color(0.10f, 0.11f, 0.13f), ORoof, roof, new Vector3(0f, yaw, 0f), new Vector2(8.4f, 3.2f));
            }
            // ベンチ(ダッグアウト)
            Vector3 dug = along * 25f + outward * 9.5f;
            Quad("Dugout", white, new Color(0.03f, 0.03f, 0.04f), OFascia + 1, dug + Vector3.up * 0.8f, new Vector3(0f, yaw, 0f), new Vector2(18f, 1.6f));
            Quad("DugoutRoof", white, s > 0 ? new Color(0.75f, 0.15f, 0.15f) : new Color(0.15f, 0.25f, 0.65f), OFascia + 2,
                dug + Vector3.up * 1.8f - outward * 0.05f, new Vector3(0f, yaw, 0f), new Vector2(18.4f, 0.45f));
            Quad("DugoutRail", white, new Color(0.9f, 0.9f, 0.9f), OFascia + 2, dug + Vector3.up * 0.9f - outward * 0.1f,
                new Vector3(0f, yaw, 0f), new Vector2(18f, 0.08f));
        }
    }

    // ---- 照明塔 ----------------------------------------------------
    private void BuildLights()
    {
        Sprite white = SpriteFactory.White;
        float[] phis = { -44f, -24f, 24f, 44f };
        foreach (float phi in phis)
        {
            float rf = BattingConfig.FenceRadius(Mathf.Min(Mathf.Abs(phi), 45f));
            Vector3 dir = Dir(phi);
            Vector3 p = dir * (rf + 47f);
            LightBank(p, phi, 46f, 60f);
        }
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 along = new Vector3(s * 0.7071f, 0f, 0.7071f), outward = new Vector3(s * 0.7071f, 0f, -0.7071f);
            Vector3 p = along * 52f + outward * 34f;
            float yaw = Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg + 180f;
            LightBank(p, yaw, 0f, 44f);
        }
    }

    private void LightBank(Vector3 foot, float yaw, float poleFrom, float top)
    {
        Sprite white = SpriteFactory.White;
        Quad("LightPole", white, new Color(0.14f, 0.15f, 0.18f), ORoof, foot + Vector3.up * ((poleFrom + top) * 0.5f),
            new Vector3(0f, yaw, 0f), new Vector2(1.0f, top - poleFrom));
        Vector3 panel = foot + Vector3.up * (top + 3.5f);
        Quad("LightPanel", StadiumArt.LightPanel, Color.white, OGlow, panel, new Vector3(-14f, yaw, 0f), new Vector2(13f, 7.8f));
        // 照明の光(明るさ1を超える色にしてブルームでにじませる)
        Quad("LightGlow", StadiumArt.Glow, new Color(1.6f, 1.5f, 1.3f, 0.55f), OGlow + 1, panel - Dir(yaw) * 1f,
            new Vector3(0f, yaw, 0f), new Vector2(58f, 44f));
        Quad("LightCore", StadiumArt.Glow, new Color(3.0f, 2.9f, 2.6f, 0.85f), OGlow + 2, panel - Dir(yaw) * 1.5f,
            new Vector3(0f, yaw, 0f), new Vector2(22f, 16f));
        // 横に伸びる光の筋(カメラのレンズ越しの照明らしさ)
        Quad("LightStreak", StadiumArt.Glow, new Color(1.4f, 1.6f, 2.2f, 0.22f), OGlow + 3, panel - Dir(yaw) * 2f,
            new Vector3(0f, yaw, 0f), new Vector2(140f, 5f));
    }

    // ---- 電光掲示板 ---------------------------------------------------
    private void BuildScoreboard()
    {
        Sprite white = SpriteFactory.White;
        float rf = BattingConfig.FenceRadius(0f);
        float r = rf + 1.2f + 22f + 2.5f;
        Vector3 c = new Vector3(0f, 33f, r);
        Quad("BoardFrame", white, new Color(0.10f, 0.10f, 0.12f), OBoard, c + Vector3.forward * 0.3f, Vector3.zero, new Vector2(42f, 22f));
        Quad("BoardScreen", white, new Color(0.02f, 0.03f, 0.06f), OBoard + 1, c, Vector3.zero, new Vector2(39f, 19f));
        Quad("BoardTitleBar", white, new Color(0.55f, 0.08f, 0.10f), OBoard + 2, c + new Vector3(0f, 7.6f, -0.05f), Vector3.zero, new Vector2(39f, 3.6f));
        var rot = Quaternion.identity;
        Text("BoardTitle", "ぐにゃスタジアム", c + new Vector3(0f, 7.6f, -0.1f), rot, 2.6f, Color.white, TextAnchor.MiddleCenter, OBoardText);
        _boardScore = Text("BoardScore", "", c + new Vector3(-8f, 2.2f, -0.1f), rot, 3.2f, new Color(1f, 0.92f, 0.55f), TextAnchor.MiddleCenter, OBoardText);
        _boardCount = Text("BoardInning", "", c + new Vector3(-8f, -2.6f, -0.1f), rot, 2.4f, Color.white, TextAnchor.MiddleCenter, OBoardText);
        _boardBatter = Text("BoardBatter", "", c + new Vector3(-8f, -6.6f, -0.1f), rot, 2.0f, new Color(0.75f, 0.9f, 1f), TextAnchor.MiddleCenter, OBoardText);
        _boardSpeed = Text("BoardSpeed", "", c + new Vector3(10.5f, -5.4f, -0.1f), rot, 3.0f, new Color(0.6f, 1f, 0.6f), TextAnchor.MiddleCenter, OBoardText);
        // B S O ランプ
        _lampB = Lamps("B", c + new Vector3(5f, 3.6f, -0.1f), 3, new Color(0.2f, 1f, 0.35f));
        _lampS = Lamps("S", c + new Vector3(5f, 0.6f, -0.1f), 2, new Color(1f, 0.85f, 0.15f));
        _lampO = Lamps("O", c + new Vector3(5f, -2.4f, -0.1f), 2, new Color(1f, 0.2f, 0.15f));
        BuildVision(c);
        // 掲示板の光のにじみ
        Quad("BoardGlow", StadiumArt.Glow, new Color(0.6f, 0.7f, 1f, 0.12f), OBoard - 1, c + Vector3.forward * 0.5f, Vector3.zero, new Vector2(70f, 40f));
    }

    // ---- 大型ビジョン(掲示板の画面) ----
    // 常時: 画面の真ん中(題字の帯と下の得点の帯の間)にロゴの絵。得点・回・BSO は下の帯に小さく。
    // 試合開始・ホームランの瞬間だけ、画面全体に専用の絵を出す(ShowVision)。
    private SpriteRenderer _visionEvent, _visionEventBack;
    private float _visionUntil, _visionT0;
    private Vector3 _visionEventScale;
    private static Sprite LoadVision(string n) { return Resources.Load<Sprite>("Presentation/" + n); }

    private void BuildVision(Vector3 c)
    {
        var logo = LoadVision("vision_logo");
        if (logo == null) return;
        Quad("VisionLogo", logo, Color.white, OBoard + 2, c + new Vector3(0f, 0.25f, -0.03f), Vector3.zero, new Vector2(39f, 11.1f));
        // 下の得点の帯
        Quad("BoardBand", SpriteFactory.White, new Color(0.02f, 0.03f, 0.06f, 0.92f), OBoard + 2, c + new Vector3(0f, -7.4f, -0.06f), Vector3.zero, new Vector2(39f, 4.2f));
        SetLocal(_boardScore, c + new Vector3(-12.5f, -7.4f, -0.1f), 0.75f);
        SetLocal(_boardCount, c + new Vector3(-3.2f, -7.4f, -0.1f), 0.85f);
        if (_boardBatter != null) _boardBatter.gameObject.SetActive(false);   // 打者名は画面の札に出ている
        SetLocal(_boardSpeed, c + new Vector3(14.5f, 7.6f, -0.1f), 0.7f);
        PlaceLamps(_lampB, "B", c + new Vector3(2.2f, -7.4f, -0.1f));
        PlaceLamps(_lampS, "S", c + new Vector3(8.9f, -7.4f, -0.1f));
        PlaceLamps(_lampO, "O", c + new Vector3(14.0f, -7.4f, -0.1f));
        var title = _root.Find("BoardTitle");
        if (title != null) title.localPosition = c + new Vector3(-6f, 7.6f, -0.1f);
        // 出来事の絵(画面全体。絵の縦横比のまま収め、余白は暗くする)
        _visionEventBack = Quad("VisionEventBack", SpriteFactory.White, new Color(0.01f, 0.01f, 0.03f, 1f), OBoardText + 1, c + new Vector3(0f, 0f, -0.15f), Vector3.zero, new Vector2(39f, 19f));
        _visionEvent = Quad("VisionEvent", logo, Color.white, OBoardText + 2, c + new Vector3(0f, 0f, -0.2f), Vector3.zero, new Vector2(39f, 19f));
        _visionEventBack.gameObject.SetActive(false);
        _visionEvent.gameObject.SetActive(false);
    }

    private static void SetLocal(TextMesh t, Vector3 pos, float scale)
    {
        if (t == null) return;
        t.transform.localPosition = pos;
        t.transform.localScale *= scale;
    }

    private void PlaceLamps(SpriteRenderer[] arr, string label, Vector3 pos)
    {
        if (arr == null) return;
        var lab = _root.Find("Lamp" + label);
        if (lab != null) { lab.localPosition = pos; lab.localScale *= 0.7f; }
        for (int i = 0; i < arr.Length; i++)
        {
            arr[i].transform.localPosition = pos + new Vector3(1.6f + i * 1.55f, 0f, 0f);
            arr[i].transform.localScale *= 0.62f;
        }
    }

    // kind: "playball" / "homerun"
    public void ShowVision(string kind, float seconds)
    {
        if (_visionEvent == null) return;
        var sp = LoadVision("vision_" + kind);
        if (sp == null) return;
        _visionEvent.sprite = sp;
        Vector2 bs = sp.bounds.size;
        float k = Mathf.Min(39f / bs.x, 19f / bs.y);
        _visionEventScale = new Vector3(bs.x * k / bs.x, bs.y * k / bs.y, 1f) * 1f;
        _visionEventScale = new Vector3(k, k, 1f);
        _visionEvent.transform.localScale = _visionEventScale;
        _visionEvent.gameObject.SetActive(true);
        _visionEventBack.gameObject.SetActive(true);
        _visionT0 = Time.unscaledTime;
        _visionUntil = Time.unscaledTime + seconds;
    }

    public void HideVision() { _visionUntil = 0f; TickVision(); }

    private void TickVision()
    {
        if (_visionEvent == null || !_visionEvent.gameObject.activeSelf) return;
        float t = Time.unscaledTime;
        if (t >= _visionUntil)
        {
            _visionEvent.gameObject.SetActive(false);
            _visionEventBack.gameObject.SetActive(false);
            return;
        }
        // 出た瞬間に少し大きく出て落ち着き、その後はゆっくり明滅
        float u = Mathf.Clamp01((t - _visionT0) / 0.35f);
        float pop = 1f + (1f - u) * (1f - u) * 0.12f;
        _visionEvent.transform.localScale = _visionEventScale * pop;
        float glow = 0.88f + 0.12f * Mathf.Sin((t - _visionT0) * 6f);
        _visionEvent.color = new Color(glow, glow, glow, 1f);
    }

    private SpriteRenderer[] Lamps(string label, Vector3 pos, int n, Color on)
    {
        Text("Lamp" + label, label, pos, Quaternion.identity, 2.2f, Color.white, TextAnchor.MiddleCenter, OBoardText);
        var arr = new SpriteRenderer[n];
        for (int i = 0; i < n; i++)
        {
            arr[i] = Quad("Lamp" + label + i, SpriteFactory.Circle, on, OBoardText, pos + new Vector3(3.0f + i * 2.6f, 0f, 0f),
                Vector3.zero, new Vector2(2.0f, 2.0f));
            arr[i].color = new Color(on.r * 0.18f, on.g * 0.18f, on.b * 0.18f, 1f);
        }
        _lampTint[label] = on;
        return arr;
    }

    private readonly Dictionary<string, Color> _lampTint = new Dictionary<string, Color>();

    private void SetLamps(SpriteRenderer[] arr, string label, int count)
    {
        if (arr == null) return;
        Color on = _lampTint[label];
        for (int i = 0; i < arr.Length; i++)
            arr[i].color = i < count ? on : new Color(on.r * 0.18f, on.g * 0.18f, on.b * 0.18f, 1f);
    }

    public void SetBoard(int inning, bool isTop, int balls, int strikes, int outs, int runs, int oppRuns)
    {
        if (_boardScore == null) return;
        _boardScore.text = "自 " + runs + "  -  " + oppRuns + " 相";
        _boardCount.text = inning + "回" + (isTop ? "表" : "裏");
        SetLamps(_lampB, "B", balls);
        SetLamps(_lampS, "S", strikes);
        SetLamps(_lampO, "O", outs);
    }

    public void SetBoardBatter(string name)
    {
        if (_boardBatter != null) _boardBatter.text = name;
    }

    public void SetBoardSpeed(float kmh)
    {
        if (_boardSpeed != null) _boardSpeed.text = kmh > 0f ? Mathf.RoundToInt(kmh) + " km/h" : "";
    }

    // ---- フェンスまわり(ポール・距離表示・広告) -----------------------
    private void BuildFenceExtras()
    {
        Sprite white = SpriteFactory.White;
        float h = BattingConfig.FenceHeight;
        // ポール
        for (int s = -1; s <= 1; s += 2)
        {
            float phi = 45f * s;
            float rf = BattingConfig.FenceRadius(45f);
            Vector3 p = Dir(phi) * (rf + 0.4f);
            Quad("FoulPole", white, new Color(1f, 0.86f, 0.15f), OFascia + 3, p + Vector3.up * 16f, new Vector3(0f, phi, 0f), new Vector2(0.55f, 32f));
            Quad("FoulPoleGlow", StadiumArt.Glow, new Color(1f, 0.9f, 0.3f, 0.12f), OFascia + 2, p + Vector3.up * 16f, new Vector3(0f, phi, 0f), new Vector2(5f, 34f));
            Text("Dist" + s, Mathf.RoundToInt(rf).ToString(), Dir(phi * 0.93f) * (BattingConfig.FenceRadius(41.8f) - 0.2f) + Vector3.up * 2.4f,
                Quaternion.Euler(0f, phi * 0.93f, 0f), 1.4f, Color.white, TextAnchor.MiddleCenter, -58);
        }
        Text("DistC", Mathf.RoundToInt(BattingConfig.FenceRadius(0f)).ToString(), Dir(0f) * (BattingConfig.FenceRadius(0f) - 0.2f) + Vector3.up * 2.4f,
            Quaternion.identity, 1.4f, Color.white, TextAnchor.MiddleCenter, -58);

        // 架空の広告看板(社名は自作)
        string[] ads = { "ぐにゃ乳業", "こんにゃく電鉄", "ぐにゃぐにゃ保険", "ふにゃ食品", "ぐにゃっとカメラ", "ぐにゃ銀行" };
        Color[] bg = { new Color(0.10f, 0.35f, 0.75f), new Color(0.80f, 0.15f, 0.15f), new Color(0.95f, 0.75f, 0.10f),
                       new Color(0.10f, 0.55f, 0.30f), new Color(0.15f, 0.15f, 0.18f), new Color(0.55f, 0.15f, 0.55f) };
        float[] at = { -34f, -22f, -12f, 12f, 22f, 34f };
        for (int i = 0; i < at.Length; i++)
        {
            float phi = at[i];
            float rf = BattingConfig.FenceRadius(Mathf.Abs(phi));
            Vector3 p = Dir(phi) * (rf - 0.15f) + Vector3.up * (h * 0.5f);
            float w = 2f * Mathf.PI * rf * (7f / 360f);
            // LEDの看板: 少し発光 + 上半分のつや
            Quad("AdPanel", white, bg[i] * 1.12f, -59, p, new Vector3(0f, phi, 0f), new Vector2(w, h * 0.72f));
            Quad("AdGloss", SpriteFactory.GradientVertical(new Color(1f, 1f, 1f, 0.26f), new Color(1f, 1f, 1f, 0f)), Color.white, -59,
                p + Vector3.up * (h * 0.18f) - Dir(phi) * 0.03f, new Vector3(0f, phi, 0f), new Vector2(w, h * 0.36f));
            Color tc = i == 2 ? new Color(0.15f, 0.1f, 0.05f) : Color.white;
            Text("AdText", ads[i], p - Dir(phi) * 0.05f, Quaternion.Euler(0f, phi, 0f), 1.5f, tc, TextAnchor.MiddleCenter, -58);
        }
    }

    // ---- 歓声の演出 --------------------------------------------------
    // ---- 花火(スタンドの上から打ち上げて開く) ----------------------------
    private class Spark { public SpriteRenderer Sr; public Vector3 Pos, Vel; public float Life, Max, Size; public bool Rocket; public Color C; public float Delay; }
    private readonly List<Spark> _sparkLive = new List<Spark>();
    private readonly Stack<Spark> _sparkPool = new Stack<Spark>();

    private Spark GetSpark()
    {
        Spark s = _sparkPool.Count > 0 ? _sparkPool.Pop() : new Spark();
        if (s.Sr == null)
            s.Sr = Quad("Firework", StadiumArt.Glow, Color.white, OFascia + 5, Vector3.zero, Vector3.zero, Vector2.one);
        s.Sr.enabled = true;
        _sparkLive.Add(s);
        return s;
    }

    // n発をばらばらの時間に打ち上げる(金・赤・白)
    public void LaunchFireworks(int n)
    {
        for (int i = 0; i < n; i++)
        {
            float phi = Random.Range(-40f, 40f);
            float rf = BattingConfig.FenceRadius(Mathf.Abs(phi));
            var s = GetSpark();
            s.Rocket = true;
            s.Pos = Dir(phi) * (rf + 30f) + Vector3.up * 28f;
            s.Vel = new Vector3(Random.Range(-3f, 3f), Random.Range(30f, 40f), Random.Range(-3f, 3f));
            s.Max = s.Life = 4f;
            s.Size = 3.2f;
            s.Delay = i * Random.Range(0.12f, 0.30f);
            float k = Random.value;
            s.C = k < 0.45f ? new Color(1f, 0.82f, 0.30f) : (k < 0.8f ? new Color(1f, 0.25f, 0.18f) : new Color(1f, 0.96f, 0.9f));
            s.Sr.enabled = false;
        }
    }

    private void TickFireworks(float dt)
    {
        var cam = Camera.main;
        for (int i = _sparkLive.Count - 1; i >= 0; i--)
        {
            var s = _sparkLive[i];
            if (s.Delay > 0f) { s.Delay -= dt; continue; }
            s.Sr.enabled = true;
            s.Life -= dt;
            if (s.Rocket && s.Vel.y < 4f)
            {
                // 開く
                s.Life = 0f;
                int n = 70;
                for (int k = 0; k < n; k++)
                {
                    var p = GetSpark();
                    p.Rocket = false;
                    p.Delay = 0f;
                    p.Pos = s.Pos;
                    p.Vel = Random.onUnitSphere * Random.Range(20f, 28f);
                    p.Max = p.Life = Random.Range(1.4f, 2.0f);
                    p.Size = Random.Range(4.0f, 6.0f);
                    p.C = Random.value < 0.25f ? Color.white : s.C;
                }
                GameAudio.Play(Sfx.HitGood, 0.22f, Random.Range(0.45f, 0.6f));
            }
            if (s.Life <= 0f)
            {
                s.Sr.enabled = false;
                _sparkPool.Push(s);
                _sparkLive.RemoveAt(i);
                continue;
            }
            float drag = s.Rocket ? 0f : 1.4f;
            s.Vel += Vector3.down * (s.Rocket ? 20f : 5f) * dt;
            s.Vel *= Mathf.Max(0f, 1f - drag * dt);
            s.Pos += s.Vel * dt;
            var tr = s.Sr.transform;
            tr.localPosition = s.Pos;
            if (cam != null) tr.rotation = cam.transform.rotation;
            float u = s.Life / s.Max;
            float sz = s.Rocket ? s.Size : s.Size * Mathf.Lerp(0.4f, 1f, u);
            tr.localScale = Vector3.one * (sz / StadiumArt.Glow.bounds.size.x);
            var c = s.C;
            c.a = s.Rocket ? 0.9f : Mathf.Clamp01(u * 1.5f) * (0.75f + 0.25f * Mathf.Sin(s.Life * 40f));
            s.Sr.color = c;
        }
    }

    public void Cheer(float intensity)
    {
        if (intensity >= 0.95f) LaunchFireworks(5);
        _cheerAmp = Mathf.Max(_cheerAmp * (Time.time < _cheerUntil ? 1f : 0f), Mathf.Clamp01(intensity));
        _cheerUntil = Time.time + Mathf.Lerp(1.6f, 4.0f, intensity);
    }

    private void Update()
    {
        GameAudio.TickAmbience(Time.unscaledDeltaTime);
        TickFireworks(Time.deltaTime);
        TickVision();
        float t = Time.time;
        bool cheering = t < _cheerUntil;
        float amp = cheering ? _cheerAmp * Mathf.Clamp01((_cheerUntil - t) / 1.2f) : 0f;

        for (int i = 0; i < _crowd.Count; i++)
        {
            var c = _crowd[i];
            int frame = 0;
            if (amp > 0f && c.Gate < amp + 0.15f)
                frame = (Mathf.FloorToInt((t + c.Phase) / c.Period) % 2 == 0) ? 1 : 0;
            else if (Mathf.Repeat(t * 0.07f + c.Phase, 1f) < 0.04f)
                frame = 1; // ときどき一部の客が立つ
            if (c.Mr != null)
            {
                var mat = StadiumMesh.Mat(StadiumArt.Crowd(c.Side, frame).texture);
                if (c.Mr.sharedMaterial != mat) c.Mr.sharedMaterial = mat;
                continue;
            }
            var sp = StadiumArt.CrowdSlice(c.Side, frame, c.Variant);
            if (c.Sr.sprite != sp) c.Sr.sprite = sp;
        }

        // LEDリボンの色(通常はゆっくり流れ、歓声時は点滅)
        for (int i = 0; i < _ribbons.Count; i++)
        {
            float h = Mathf.Repeat(t * 0.05f + i * 0.03f, 1f);
            Color baseC = Color.HSVToRGB(Mathf.Lerp(0.95f, 1.08f, h) % 1f, 0.85f, 0.85f);
            if (amp > 0f)
            {
                bool on = Mathf.FloorToInt(t * 8f + i * 0.5f) % 2 == 0;
                baseC = on ? new Color(1f, 0.95f, 0.4f) : new Color(0.9f, 0.15f, 0.15f);
            }
            _ribbons[i].color = baseC;
        }
        for (int i = 0; i < _ribbonMats.Count; i++)
        {
            float h = Mathf.Repeat(t * 0.05f + i * 0.06f, 1f);
            Color baseC = Color.HSVToRGB(Mathf.Lerp(0.95f, 1.08f, h) % 1f, 0.85f, 0.85f) * 1.25f;
            if (amp > 0f)
            {
                bool on = Mathf.FloorToInt(t * 8f + i * 0.5f) % 2 == 0;
                baseC = on ? new Color(1.3f, 1.2f, 0.5f) : new Color(1.1f, 0.2f, 0.2f);
            }
            _ribbonMats[i].color = baseC;
        }

        // カメラのフラッシュ
        if (amp > 0.6f && _sparkSpots.Count > 0)
        {
            int want = Mathf.RoundToInt(amp * 18f);
            while (_sparks.Count < want)
            {
                var s = Quad("Spark", StadiumArt.Glow, new Color(1f, 1f, 1f, 0.9f), OFascia + 4, Vector3.zero, Vector3.zero, new Vector2(2.2f, 2.2f));
                _sparks.Add(s);
            }
            for (int i = 0; i < _sparks.Count; i++)
            {
                var s = _sparks[i];
                bool on = i < want && Random.value < 0.25f;
                s.enabled = on;
                if (on)
                {
                    Vector3 p = _sparkSpots[Random.Range(0, _sparkSpots.Count)];
                    s.transform.localPosition = p;
                    s.transform.rotation = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
                }
            }
        }
        else
        {
            for (int i = 0; i < _sparks.Count; i++) _sparks[i].enabled = false;
        }
    }
}
