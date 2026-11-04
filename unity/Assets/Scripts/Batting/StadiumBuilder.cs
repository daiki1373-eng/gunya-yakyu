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
    private const int OUpper = -76, ORoof = -75, OGlow = -73, OLower = -72, OLowerShade = -71, OFascia = -70;
    private const int OBoard = -69, OBoardText = -64;

    private Transform _root;
    private Font _font;

    private class CrowdSeg { public SpriteRenderer Sr; public int Side, Variant; public float Phase, Period, Gate; public Color Tint; }
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
            Flat("MowA" + i, white, new Color(1f, 1f, 1f, 0.055f), G + 1, off * 0.7071f, 60f + off * 0.7071f, 6f, 300f, 45f);
            Flat("MowB" + i, white, new Color(0f, 0.05f, 0f, 0.06f), G + 1, -off * 0.7071f, 60f + off * 0.7071f, 6f, 300f, -45f);
        }
        // 外野のウォーニングゾーン(土)
        for (float phi = -47f; phi <= 47f; phi += 2f)
        {
            float rf = BattingConfig.FenceRadius(Mathf.Min(Mathf.Abs(phi), 45f));
            Vector3 p = Dir(phi) * (rf - 2.6f);
            float segW = 2f * Mathf.PI * rf * (2f / 360f) * 1.12f;
            Flat("Track", dirt, new Color(0.85f, 0.80f, 0.78f), G + 2, p.x, p.z, segW, 5.4f, -phi, true);
        }
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

    private void BuildFairStands()
    {
        Sprite white = SpriteFactory.White;
        const float step = 4f;
        for (float phi = -56f; phi <= 56.1f; phi += step)
        {
            float rf = BattingConfig.FenceRadius(Mathf.Min(Mathf.Abs(phi), 45f));
            Vector3 dir = Dir(phi);
            int side = phi < -14f ? 1 : 0;   // 左翼側はビジター応援席
            bool center = Mathf.Abs(phi) <= 6.1f;
            bool boardZone = Mathf.Abs(phi) <= 10.1f;

            // 下段
            float r0 = rf + 1.2f, h0 = 4.4f, run0 = 22f, rise0 = 16f;
            float w0 = 2f * Mathf.PI * (r0 + run0 * 0.5f) * (step / 360f) * 1.05f;
            Vector3 b0 = dir * r0 + Vector3.up * h0;
            if (center)
            {
                // バックスクリーン(打者の目のため無地の濃い緑)
                float L = Mathf.Sqrt(run0 * run0 + rise0 * rise0);
                Quad("BackScreen", white, new Color(0.04f, 0.10f, 0.08f), OLower,
                    b0 + dir * (run0 * 0.5f) + Vector3.up * (rise0 * 0.5f),
                    new Vector3(Mathf.Atan2(run0, rise0) * Mathf.Rad2Deg, phi, 0f), new Vector2(w0, L));
            }
            else
            {
                CrowdPlane("LowerStand", side, b0, phi, run0, rise0, w0, OLower, Color.white, dir);
                Shade(b0, phi, run0, rise0, w0, OLowerShade, 0.55f, 0.55f, dir);
            }
            // 段の間の帯(LEDリボン)
            float r1 = r0 + run0 + 0.6f;
            Vector3 fp = dir * r1 + Vector3.up * (h0 + rise0 + 1.6f);
            float wf = 2f * Mathf.PI * r1 * (step / 360f) * 1.05f;
            Quad("Fascia", white, new Color(0.06f, 0.06f, 0.08f), OFascia, fp, new Vector3(0f, phi, 0f), new Vector2(wf, 3.4f));
            if (!boardZone)
            {
                var rib = Quad("Ribbon", white, new Color(0.9f, 0.2f, 0.2f), OFascia + 1, fp - dir * 0.05f,
                    new Vector3(0f, phi, 0f), new Vector2(wf, 1.8f));
                _ribbons.Add(rib);
            }

            // 上段
            float h1 = h0 + rise0 + 3.4f, run1 = 18f, rise1 = 20f;
            float w1 = 2f * Mathf.PI * (r1 + run1 * 0.5f) * (step / 360f) * 1.05f;
            Vector3 b1 = dir * (r1 + 0.4f) + Vector3.up * h1;
            if (boardZone)
            {
                float L = Mathf.Sqrt(run1 * run1 + rise1 * rise1);
                Quad("BoardStructure", white, new Color(0.07f, 0.08f, 0.10f), OUpper,
                    b1 + dir * (run1 * 0.5f) + Vector3.up * (rise1 * 0.5f),
                    new Vector3(Mathf.Atan2(run1, rise1) * Mathf.Rad2Deg, phi, 0f), new Vector2(w1, L));
            }
            else
            {
                CrowdPlane("UpperStand", side, b1, phi, run1, rise1, w1, OUpper, new Color(0.80f, 0.80f, 0.86f), dir);
                Shade(b1, phi, run1, rise1, w1, OUpper + 0, 0.6f, 0.6f, dir);
            }
            // 屋根のへり(下面に照明の帯)
            float r2 = r1 + run1 + 0.8f;
            Vector3 rp = dir * r2 + Vector3.up * (h1 + rise1 + 1.5f);
            float w2 = 2f * Mathf.PI * r2 * (step / 360f) * 1.05f;
            Quad("Roof", white, new Color(0.10f, 0.11f, 0.13f), ORoof, rp, new Vector3(0f, phi, 0f), new Vector2(w2, 3.2f));
            Quad("RoofLight", white, new Color(1f, 0.95f, 0.82f, 0.85f), ORoof, rp - Vector3.up * 1.45f - dir * 0.05f,
                new Vector3(0f, phi, 0f), new Vector2(w2, 0.35f));
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
        Quad("LightGlow", StadiumArt.Glow, new Color(1f, 0.96f, 0.85f, 0.55f), OGlow + 1, panel - Dir(yaw) * 1f,
            new Vector3(0f, yaw, 0f), new Vector2(58f, 44f));
        Quad("LightCore", StadiumArt.Glow, new Color(1f, 1f, 0.95f, 0.75f), OGlow + 2, panel - Dir(yaw) * 1.5f,
            new Vector3(0f, yaw, 0f), new Vector2(22f, 16f));
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
        // 掲示板の光のにじみ
        Quad("BoardGlow", StadiumArt.Glow, new Color(0.6f, 0.7f, 1f, 0.12f), OBoard - 1, c + Vector3.forward * 0.5f, Vector3.zero, new Vector2(70f, 40f));
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

    public void SetBoard(int inning, bool userBatting, int balls, int strikes, int outs, int runs, int oppRuns)
    {
        if (_boardScore == null) return;
        _boardScore.text = "自 " + runs + "  -  " + oppRuns + " 相";
        _boardCount.text = inning + "回" + (userBatting ? "表" : "裏");
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
            Quad("AdPanel", white, bg[i], -59, p, new Vector3(0f, phi, 0f), new Vector2(w, h * 0.72f));
            Color tc = i == 2 ? new Color(0.15f, 0.1f, 0.05f) : Color.white;
            Text("AdText", ads[i], p - Dir(phi) * 0.05f, Quaternion.Euler(0f, phi, 0f), 1.5f, tc, TextAnchor.MiddleCenter, -58);
        }
    }

    // ---- 歓声の演出 --------------------------------------------------
    public void Cheer(float intensity)
    {
        _cheerAmp = Mathf.Max(_cheerAmp * (Time.time < _cheerUntil ? 1f : 0f), Mathf.Clamp01(intensity));
        _cheerUntil = Time.time + Mathf.Lerp(1.6f, 4.0f, intensity);
    }

    private void Update()
    {
        GameAudio.TickAmbience(Time.unscaledDeltaTime);
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
