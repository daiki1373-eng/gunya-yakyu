using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// BattingStage.cs
// 打席の舞台(捕手後方視点の3Dカメラ + 球場 + ゾーン枠 + ボール + 野手の人形)。
// 画像素材なし。全部コード生成のスプライトを3D空間に置いている
// (地面は寝かせたスプライト、柵や観客席は立てたスプライト)。
// ラグドール選手は既存のRagdollPlayer(Physics2D)をそのまま使い、
// XY平面に立つ看板として3D空間の中に置く。
//
// カメラは2モード: Catcher(捕手後方の固定視点) / Follow(打球を追う)。
// ============================================================
public enum CamMode { Catcher, Follow }

public class BattingStage : MonoBehaviour
{
    public Camera Cam { get; private set; }
    public Transform Root { get; private set; }

    // 描画順(sortingOrder)。ラグドールは既存コードで4〜8を使う。
    private const int OrderGround = -90;
    private const int OrderStands = -70;
    private const int OrderFence = -60;
    private const int OrderShadow = -50;
    private const int OrderZone = 9;
    private const int OrderBall = 20;

    private SpriteRenderer _ball, _ballSeam, _shadow;
    private readonly List<Transform> _billboards = new List<Transform>();
    private Transform[] _fielders;
    private CamMode _mode = CamMode.Catcher;
    private Vector3 _followTarget;
    private Vector3 _camVel;
    private Quaternion _catcherRot;

    public static BattingStage Create(Transform parent)
    {
        var go = new GameObject("BattingStage");
        go.transform.SetParent(parent, false);
        var stage = go.AddComponent<BattingStage>();
        stage.Root = go.transform;
        stage.Build();
        return stage;
    }

    private void Build()
    {
        SetupCamera();
        BuildField();
        BuildFenceAndStands();
        BuildZone();
        BuildBall();
        BuildFielders();
    }

    // ------------------------------------------------------------
    // カメラ
    // ------------------------------------------------------------
    private void SetupCamera()
    {
        Cam = Camera.main;
        if (Cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            Cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        Cam.orthographic = false;
        Cam.fieldOfView = BattingConfig.CameraFov;
        Cam.nearClipPlane = 0.1f;
        Cam.farClipPlane = 700f;
        Cam.clearFlags = CameraClearFlags.SolidColor;
        Cam.backgroundColor = new Color(0.55f, 0.78f, 0.95f);
        _catcherRot = Quaternion.Euler(BattingConfig.CameraPitchDeg, 0f, 0f);
        Cam.transform.position = BattingConfig.CameraPos;
        Cam.transform.rotation = _catcherRot;
    }

    public void SetCameraMode(CamMode mode)
    {
        _mode = mode;
        _camVel = Vector3.zero;
    }

    public void SetFollowTarget(Vector3 ballPos)
    {
        _followTarget = ballPos;
    }

    public void SnapCatcherCamera()
    {
        _mode = CamMode.Catcher;
        _camVel = Vector3.zero;
        Cam.transform.position = BattingConfig.CameraPos;
        Cam.transform.rotation = _catcherRot;
    }

    public bool CatcherCameraSettled()
    {
        return _mode == CamMode.Catcher
            && (Cam.transform.position - BattingConfig.CameraPos).sqrMagnitude < 0.05f;
    }

    private void LateUpdate()
    {
        if (Cam == null) return;
        var ct = Cam.transform;
        float dt = Time.unscaledDeltaTime > 0f ? Mathf.Min(Time.unscaledDeltaTime, 0.05f) : 0.016f;

        if (_mode == CamMode.Catcher)
        {
            ct.position = Vector3.SmoothDamp(ct.position, BattingConfig.CameraPos, ref _camVel, 0.30f,
                Mathf.Infinity, dt);
            ct.rotation = Quaternion.Slerp(ct.rotation, _catcherRot, 1f - Mathf.Exp(-9f * dt));
        }
        else
        {
            Vector3 b = _followTarget;
            Vector3 flat = new Vector3(b.x, 0f, b.z);
            Vector3 fwd = flat.sqrMagnitude > 4f ? flat.normalized : Vector3.forward;
            Vector3 desired = b - fwd * 18f + Vector3.up * (3.5f + Mathf.Min(b.y, 25f) * 0.25f);
            desired.y = Mathf.Max(desired.y, 2.5f);
            ct.position = Vector3.SmoothDamp(ct.position, desired, ref _camVel, 0.32f, Mathf.Infinity, dt);
            Vector3 lookAt = b + fwd * 4f + Vector3.up * 0.5f;
            Quaternion look = Quaternion.LookRotation(lookAt - ct.position, Vector3.up);
            ct.rotation = Quaternion.Slerp(ct.rotation, look, 1f - Mathf.Exp(-7f * dt));
        }

        for (int i = 0; i < _billboards.Count; i++)
        {
            if (_billboards[i] != null) _billboards[i].rotation = ct.rotation;
        }
    }

    // ------------------------------------------------------------
    // 球場
    // ------------------------------------------------------------
    private SpriteRenderer Flat(string name, Sprite s, Color col, int order,
        float x, float z, float w, float l, float roll)
    {
        return BattingSprites.Make(name, Root, s, col, order,
            new Vector3(x, 0f, z), new Vector3(90f, 0f, roll), new Vector3(w, l, 1f));
    }

    private void BuildField()
    {
        Sprite white = SpriteFactory.White;
        Sprite circle = SpriteFactory.Circle;
        Color grass = new Color(0.40f, 0.66f, 0.34f);
        Color grassLight = new Color(0.46f, 0.72f, 0.38f);
        Color dirt = new Color(0.68f, 0.50f, 0.34f);
        Color dirtDark = new Color(0.62f, 0.45f, 0.30f);
        Color line = new Color(1f, 1f, 1f, 0.95f);

        Flat("Grass", white, grass, OrderGround, 0f, 150f, 700f, 330f, 0f);
        for (int i = 0; i < 16; i++)
        {
            if (i % 2 == 1) continue;
            Flat("Stripe" + i, white, new Color(grassLight.r, grassLight.g, grassLight.b, 0.55f),
                OrderGround + 1, 0f, 6f + i * 7f, 700f, 7f, 0f);
        }

        // 内野
        Flat("InfieldDirt", circle, dirt, OrderGround + 4, 0f, 17f, 56f, 56f, 0f);
        Flat("InfieldGrass", white, grass, OrderGround + 5, 0f, 19.4f, 23f, 23f, 45f);
        // ベースパス
        Flat("PathHome1B", white, dirt, OrderGround + 6, 9.7f, 9.7f, 2.0f, 27.4f, -45f);
        Flat("Path1B2B", white, dirt, OrderGround + 6, 9.7f, 29.1f, 2.0f, 27.4f, 45f);
        Flat("Path2B3B", white, dirt, OrderGround + 6, -9.7f, 29.1f, 2.0f, 27.4f, -45f);
        Flat("Path3BHome", white, dirt, OrderGround + 6, -9.7f, 9.7f, 2.0f, 27.4f, 45f);
        Flat("HomeDirt", circle, dirt, OrderGround + 7, 0f, 0f, 18f, 18f, 0f);
        Flat("Base1B", circle, dirt, OrderGround + 7, 19.4f, 19.4f, 5f, 5f, 0f);
        Flat("Base2B", circle, dirt, OrderGround + 7, 0f, 38.8f, 5f, 5f, 0f);
        Flat("Base3B", circle, dirt, OrderGround + 7, -19.4f, 19.4f, 5f, 5f, 0f);
        Flat("Mound", circle, dirtDark, OrderGround + 8, 0f, BattingConfig.MoundZ, 5.4f, 5.4f, 0f);
        Flat("PitchPlate", white, line, OrderGround + 9, 0f, BattingConfig.MoundZ, 0.7f, 0.16f, 0f);

        // ライン
        Flat("FoulLineR", white, line, OrderGround + 9, 38.9f, 38.9f, 0.18f, 110f, -45f);
        Flat("FoulLineL", white, line, OrderGround + 9, -38.9f, 38.9f, 0.18f, 110f, 45f);
        Flat("Bag1B", white, line, OrderGround + 10, 19.4f, 19.4f, 0.9f, 0.9f, 45f);
        Flat("Bag2B", white, line, OrderGround + 10, 0f, 38.8f, 0.9f, 0.9f, 45f);
        Flat("Bag3B", white, line, OrderGround + 10, -19.4f, 19.4f, 0.9f, 0.9f, 45f);
        Flat("HomePlate", white, line, OrderGround + 10, 0f, 0f, 0.8f, 0.8f, 0f);

        // バッターボックス(左右)
        BoxOutline(-1.40f, 0f, line);
        BoxOutline(1.40f, 0f, line);
    }

    private void BoxOutline(float cx, float cz, Color col)
    {
        Sprite white = SpriteFactory.White;
        float w = 1.2f, l = 2.2f, t = 0.07f;
        int o = OrderGround + 10;
        Flat("BoxL", white, col, o, cx - w * 0.5f, cz, t, l, 0f);
        Flat("BoxR", white, col, o, cx + w * 0.5f, cz, t, l, 0f);
        Flat("BoxF", white, col, o, cx, cz + l * 0.5f, w, t, 0f);
        Flat("BoxB", white, col, o, cx, cz - l * 0.5f, w, t, 0f);
    }

    private void BuildFenceAndStands()
    {
        Sprite white = SpriteFactory.White;
        var palette = new Color[]
        {
            new Color(0.95f, 0.89f, 0.77f), new Color(0.77f, 0.85f, 0.75f),
            new Color(0.95f, 0.74f, 0.44f), new Color(0.28f, 0.39f, 0.42f),
            new Color(0.82f, 0.55f, 0.41f)
        };
        Sprite crowd = SpriteFactory.Crowd(palette, 777);
        Vector2 cb = crowd.bounds.size;
        float h = BattingConfig.FenceHeight;

        for (float phi = -58f; phi <= 58f; phi += 4f)
        {
            float rf = BattingConfig.FenceRadius(Mathf.Min(Mathf.Abs(phi), 45f));
            float rad = phi * Mathf.Deg2Rad;
            float segW = 2f * Mathf.PI * rf * (4f / 360f) * 1.08f;
            Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            Quaternion face = Quaternion.Euler(0f, phi, 0f);

            // フェンス本体(緑)と上端の黄色い線
            var fence = BattingSprites.Make("Fence", Root, white, new Color(0.13f, 0.36f, 0.24f), OrderFence,
                dir * rf + Vector3.up * (h * 0.5f), face.eulerAngles, new Vector3(segW, h, 1f));
            var top = BattingSprites.Make("FenceTop", Root, white, new Color(0.98f, 0.85f, 0.25f), OrderFence + 1,
                dir * rf + Vector3.up * h, face.eulerAngles, new Vector3(segW, 0.25f, 1f));

            // 観客席(柵の後ろ)
            float rs = rf + 9f;
            float standH = 16f;
            float standW = 2f * Mathf.PI * rs * (4f / 360f) * 1.08f;
            Vector3 sdir = dir * rs + Vector3.up * (h + standH * 0.5f);
            var stand = BattingSprites.Make("Stand", Root, crowd, Color.white, OrderStands,
                sdir, face.eulerAngles, new Vector3(standW / cb.x, standH / cb.y, 1f));
            var standBase = BattingSprites.Make("StandBase", Root, white, new Color(0.45f, 0.48f, 0.52f), OrderStands - 1,
                dir * (rf + 4.5f) + Vector3.up * (h * 0.5f + 2f), face.eulerAngles, new Vector3(standW, h + 4f, 1f));
        }
    }

    // ------------------------------------------------------------
    // ストライクゾーン枠
    // ------------------------------------------------------------
    private void BuildZone()
    {
        Sprite white = SpriteFactory.White;
        float hw = BattingConfig.ZoneHalfW, hh = BattingConfig.ZoneHalfH, cy = BattingConfig.ZoneCenterY;
        float t = 0.014f;
        var zone = new GameObject("Zone");
        zone.transform.SetParent(Root, false);
        Color frame = new Color(1f, 1f, 1f, 0.95f);
        Color grid = new Color(1f, 1f, 1f, 0.22f);

        BattingSprites.Make("ZoneFill", zone.transform, white, new Color(1f, 1f, 1f, 0.07f), OrderZone,
            new Vector3(0f, cy, 0f), Vector3.zero, new Vector3(hw * 2f, hh * 2f, 1f));
        BattingSprites.Make("Top", zone.transform, white, frame, OrderZone + 1,
            new Vector3(0f, cy + hh, 0f), Vector3.zero, new Vector3(hw * 2f + t, t, 1f));
        BattingSprites.Make("Bottom", zone.transform, white, frame, OrderZone + 1,
            new Vector3(0f, cy - hh, 0f), Vector3.zero, new Vector3(hw * 2f + t, t, 1f));
        BattingSprites.Make("Left", zone.transform, white, frame, OrderZone + 1,
            new Vector3(-hw, cy, 0f), Vector3.zero, new Vector3(t, hh * 2f, 1f));
        BattingSprites.Make("Right", zone.transform, white, frame, OrderZone + 1,
            new Vector3(hw, cy, 0f), Vector3.zero, new Vector3(t, hh * 2f, 1f));
        for (int i = 1; i <= 2; i++)
        {
            float fx = -hw + hw * 2f * i / 3f;
            float fy = cy - hh + hh * 2f * i / 3f;
            BattingSprites.Make("GridV" + i, zone.transform, white, grid, OrderZone + 1,
                new Vector3(fx, cy, 0f), Vector3.zero, new Vector3(t * 0.7f, hh * 2f, 1f));
            BattingSprites.Make("GridH" + i, zone.transform, white, grid, OrderZone + 1,
                new Vector3(0f, fy, 0f), Vector3.zero, new Vector3(hw * 2f, t * 0.7f, 1f));
        }
    }

    // ------------------------------------------------------------
    // ボールと影
    // ------------------------------------------------------------
    private void BuildBall()
    {
        _ball = BattingSprites.Make("Ball", Root, SpriteFactory.Circle, Color.white, OrderBall,
            Vector3.zero, Vector3.zero, Vector3.one * (BattingConfig.BallRadius * 2f));
        _ballSeam = BattingSprites.Make("Seam", _ball.transform, BattingSprites.Ring, new Color(0.85f, 0.2f, 0.2f, 0.85f),
            OrderBall + 1, Vector3.zero, Vector3.zero, Vector3.one * 0.72f);
        _billboards.Add(_ball.transform);
        _shadow = BattingSprites.Make("BallShadow", Root, SpriteFactory.Circle, new Color(0f, 0f, 0f, 0.35f), OrderShadow,
            Vector3.zero, new Vector3(90f, 0f, 0f), Vector3.one * 0.2f);
        HideBall();
    }

    public void HideBall()
    {
        _ball.enabled = false;
        _ballSeam.enabled = false;
        _shadow.enabled = false;
    }

    // 距離が遠くても見えるよう、見かけの大きさに下限を付ける
    public void SetBall(Vector3 pos)
    {
        _ball.enabled = true;
        _ballSeam.enabled = true;
        _shadow.enabled = true;
        _ball.transform.position = pos;
        float dist = (pos - Cam.transform.position).magnitude;
        float dia = Mathf.Max(BattingConfig.BallRadius * 2f, dist * 0.013f);
        _ball.transform.localScale = Vector3.one * dia;

        _shadow.transform.position = new Vector3(pos.x, 0.02f, pos.z);
        _shadow.transform.localScale = new Vector3(dia * 1.1f, dia * 1.1f, 1f);
        float a = 0.38f * Mathf.Clamp01(1f - pos.y / 70f);
        _shadow.color = new Color(0f, 0f, 0f, a);
    }

    // 接触の瞬間のフラッシュ
    public void Flash(Vector3 pos, Color color, float size)
    {
        StartCoroutine(FlashRoutine(pos, color, size));
    }

    private IEnumerator FlashRoutine(Vector3 pos, Color color, float size)
    {
        var sr = BattingSprites.Make("Flash", Root, BattingSprites.Ring, color, OrderBall + 5,
            pos, Vector3.zero, Vector3.one * size * 0.3f);
        sr.transform.rotation = Cam.transform.rotation;
        var core = BattingSprites.Make("FlashCore", sr.transform, SpriteFactory.Circle, color, OrderBall + 4,
            Vector3.zero, Vector3.zero, Vector3.one * 0.6f);
        float t = 0f;
        const float dur = 0.28f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            sr.transform.localScale = Vector3.one * Mathf.Lerp(size * 0.3f, size, 1f - (1f - u) * (1f - u));
            var c = color; c.a = (1f - u) * 0.9f;
            sr.color = c;
            var c2 = color; c2.a = (1f - u) * 0.5f;
            core.color = c2;
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    // ------------------------------------------------------------
    // 野手(人形)。0=投手 1=捕手 は描かない(ラグドール投手がいる/捕手視点なので)。
    // ------------------------------------------------------------
    private void BuildFielders()
    {
        Sprite white = SpriteFactory.White;
        Sprite circle = SpriteFactory.Circle;
        int n = FieldingModel.Defaults.Length;
        _fielders = new Transform[n];
        for (int i = 2; i < n; i++)
        {
            var root = new GameObject("Fielder_" + FieldingModel.Defaults[i].Name).transform;
            root.SetParent(Root, false);
            const float s = 1.9f; // 遠くでも見えるよう誇張
            BattingSprites.Make("Legs", root, white, new Color(0.92f, 0.92f, 0.95f), 3,
                new Vector3(0f, 0.35f * s, 0f), Vector3.zero, new Vector3(0.40f * s, 0.70f * s, 1f));
            BattingSprites.Make("Body", root, white, new Color(0.25f, 0.40f, 0.82f), 3,
                new Vector3(0f, 0.95f * s, 0f), Vector3.zero, new Vector3(0.50f * s, 0.55f * s, 1f));
            BattingSprites.Make("Head", root, circle, new Color(0.96f, 0.78f, 0.62f), 3,
                new Vector3(0f, 1.42f * s, 0f), Vector3.zero, new Vector3(0.42f * s, 0.42f * s, 1f));
            BattingSprites.Make("Cap", root, white, new Color(0.15f, 0.22f, 0.5f), 4,
                new Vector3(0f, 1.58f * s, 0f), Vector3.zero, new Vector3(0.44f * s, 0.14f * s, 1f));
            _billboards.Add(root);
            _fielders[i] = root;
        }
        ResetFielders();
    }

    public void ResetFielders()
    {
        for (int i = 0; i < _fielders.Length; i++)
        {
            if (_fielders[i] == null) continue;
            Vector2 p = FieldingModel.Defaults[i].Pos;
            _fielders[i].position = new Vector3(p.x, 0f, p.y);
        }
    }

    public void SetFielderPos(int index, Vector2 xz)
    {
        if (index < 0 || index >= _fielders.Length || _fielders[index] == null) return;
        _fielders[index].position = new Vector3(xz.x, 0f, xz.y);
    }
}
