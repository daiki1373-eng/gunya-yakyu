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
public enum CamMode { Catcher, Follow, Diamond, Shot }

public class BattingStage : MonoBehaviour
{
    public Camera Cam { get; private set; }
    public StadiumBuilder Stadium { get; private set; }
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
    public float FollowDist = 18f;      // 打球カメラの後ろ距離(リプレイでは寄る)
    private float _shakeAmp, _shakeUntil, _shakeDur = 0.01f;
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
        Stadium = StadiumBuilder.Build(Root);
        BuildFenceAndStands();
        BuildZone();
        BuildReticle();
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
        Cam.backgroundColor = new Color(0.02f, 0.03f, 0.09f);
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

    // リプレイ開始用: カメラを打球の始点後方へ即座に置く
    public void SnapFollow(Vector3 ballPos)
    {
        _mode = CamMode.Follow;
        _followTarget = ballPos;
        _camVel = Vector3.zero;
        Vector3 b = ballPos;
        Vector3 fwd = Vector3.forward;
        Cam.transform.position = new Vector3(b.x, 3.5f * (FollowDist / 18f), b.z - FollowDist);
        Cam.transform.rotation = Quaternion.LookRotation(b + fwd * 4f + Vector3.up * 0.5f - Cam.transform.position, Vector3.up);
    }

    private GameObject _zoneRoot;
    public void SetZoneVisible(bool on) { if (_zoneRoot != null) _zoneRoot.SetActive(on); }

    // 演出用の寄りカメラ。snap=true なら即座にその位置へ。
    private Vector3 _shotPos, _shotLook;
    private float _shotSmooth = 0.5f;
    public void SetShot(Vector3 pos, Vector3 look, float smooth, bool snap)
    {
        _mode = CamMode.Shot;
        _shotPos = pos;
        _shotLook = look;
        _shotSmooth = Mathf.Max(0.05f, smooth);
        if (snap)
        {
            _camVel = Vector3.zero;
            Cam.transform.position = pos;
            Cam.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
        }
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
        else if (_mode == CamMode.Shot)
        {
            ct.position = Vector3.SmoothDamp(ct.position, _shotPos, ref _camVel, _shotSmooth, Mathf.Infinity, dt);
            Quaternion look = Quaternion.LookRotation(_shotLook - ct.position, Vector3.up);
            ct.rotation = Quaternion.Slerp(ct.rotation, look, 1f - Mathf.Exp(-8f * dt));
        }
        else if (_mode == CamMode.Diamond)
        {
            Vector3 dpos = new Vector3(0f, 26f, -18f);
            ct.position = Vector3.SmoothDamp(ct.position, dpos, ref _camVel, 0.40f, Mathf.Infinity, dt);
            Quaternion look = Quaternion.LookRotation(new Vector3(0f, 0f, 22f) - ct.position, Vector3.up);
            ct.rotation = Quaternion.Slerp(ct.rotation, look, 1f - Mathf.Exp(-6f * dt));
        }
        else
        {
            Vector3 b = _followTarget;
            Vector3 flat = new Vector3(b.x, 0f, b.z);
            Vector3 fwd = flat.sqrMagnitude > 4f ? flat.normalized : Vector3.forward;
            Vector3 desired = b - fwd * FollowDist + Vector3.up * (3.5f * (FollowDist / 18f) + Mathf.Min(b.y, 25f) * 0.25f);
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
        UpdateTrail();
        UpdateShadows();

        if (Time.unscaledTime < _shakeUntil)
        {
            float k = (_shakeUntil - Time.unscaledTime) / _shakeDur;
            ct.position += new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * (_shakeAmp * k);
        }
    }

    // 画面シェイク(実時間で減衰)
    public void Shake(float amp, float seconds)
    {
        _shakeAmp = amp;
        _shakeDur = Mathf.Max(0.01f, seconds);
        _shakeUntil = Time.unscaledTime + seconds;
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

    private void BuildFenceAndStands()
    {
        // スタンド・照明・掲示板は StadiumBuilder が作る。ここは外野フェンスだけ。
        Sprite fenceSp = StadiumArt.Fence;
        Sprite white = SpriteFactory.White;
        float h = BattingConfig.FenceHeight;
        for (float phi = -58f; phi <= 58.1f; phi += 2f)
        {
            float rf = BattingConfig.FenceRadius(Mathf.Min(Mathf.Abs(phi), 45f));
            float rad = phi * Mathf.Deg2Rad;
            float segW = 2f * Mathf.PI * rf * (2f / 360f) * 1.08f;
            Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            Vector3 face = new Vector3(0f, phi, 0f);
            var fence = BattingSprites.Make("Fence", Root, fenceSp, Color.white, OrderFence,
                dir * rf + Vector3.up * (h * 0.5f), face, Vector3.one);
            fence.drawMode = SpriteDrawMode.Tiled;
            fence.size = new Vector2(segW, h);
            BattingSprites.Make("FenceTop", Root, white, new Color(0.98f, 0.85f, 0.25f), OrderFence + 1,
                dir * rf + Vector3.up * h, face, new Vector3(segW, 0.22f, 1f));
            BattingSprites.Make("FencePad", Root, white, new Color(0.04f, 0.14f, 0.10f), OrderFence + 1,
                dir * (rf - 0.05f) + Vector3.up * (h - 0.35f), face, new Vector3(segW, 0.5f, 1f));
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
        _zoneRoot = zone;
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
    // 投球の照準(守るとき、ゾーン面に出す輪と十字)
    // ------------------------------------------------------------
    private Transform _reticle;
    private SpriteRenderer[] _retParts;

    private void BuildReticle()
    {
        var go = new GameObject("Reticle");
        go.transform.SetParent(Root, false);
        _reticle = go.transform;
        var parts = new System.Collections.Generic.List<SpriteRenderer>();
        Color c = Color.white;
        parts.Add(BattingSprites.Make("Ring", _reticle, BattingSprites.Ring, c, OrderZone + 3,
            Vector3.zero, Vector3.zero, Vector3.one * 0.30f));
        parts.Add(BattingSprites.Make("Dot", _reticle, SpriteFactory.Circle, c, OrderZone + 3,
            Vector3.zero, Vector3.zero, Vector3.one * 0.04f));
        float t = 0.02f, L = 0.11f, off = 0.22f;
        parts.Add(BattingSprites.Make("TickL", _reticle, SpriteFactory.White, c, OrderZone + 3,
            new Vector3(-off, 0f, 0f), Vector3.zero, new Vector3(L, t, 1f)));
        parts.Add(BattingSprites.Make("TickR", _reticle, SpriteFactory.White, c, OrderZone + 3,
            new Vector3(off, 0f, 0f), Vector3.zero, new Vector3(L, t, 1f)));
        parts.Add(BattingSprites.Make("TickU", _reticle, SpriteFactory.White, c, OrderZone + 3,
            new Vector3(0f, off, 0f), Vector3.zero, new Vector3(t, L, 1f)));
        parts.Add(BattingSprites.Make("TickD", _reticle, SpriteFactory.White, c, OrderZone + 3,
            new Vector3(0f, -off, 0f), Vector3.zero, new Vector3(t, L, 1f)));
        _retParts = parts.ToArray();
        _reticle.gameObject.SetActive(false);
    }

    public void SetReticle(Vector2 pos, bool visible, Color col)
    {
        if (_reticle == null) return;
        _reticle.gameObject.SetActive(visible);
        if (!visible) return;
        _reticle.position = new Vector3(pos.x, pos.y, -0.06f);
        for (int i = 0; i < _retParts.Length; i++) _retParts[i].color = col;
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
    // 野手・走者(人形)。0=投手 1=捕手 は描かない(ラグドール投手がいる/捕手視点なので)。
    // 人形は root(カメラ向き) → rig(足元が軸。転倒・揺れはここを回す) → パーツ の構成。
    // ------------------------------------------------------------
    private Transform[] _fielderRigs;
    private bool[] _fielderBusy;
    private Vector2[] _fielderLast;
    private Transform[] _runners, _runnerRigs;
    private bool[] _runnerBusy;
    private Vector2[] _runnerLast;
    private const float DollScale = 1.9f; // 遠くでも見えるよう誇張

    private int _dollCount;

    private SpriteRenderer DollPart(Transform rig, string name, Color c, int order, float x, float y, float w, float h)
    {
        const float s = DollScale;
        var sr = BattingSprites.Make(name, rig, CharacterArt.Part, c, order,
            new Vector3(x * s, y * s, 0f), Vector3.zero, Vector3.one);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(w * s, h * s);
        return sr;
    }

    // 自チームのときだけ出す縦じま(SetTeams で切り替え)
    private void DollStripe(Transform rig, float x, float y, float w, float h)
    {
        const float s = DollScale;
        var sr = BattingSprites.Make("DollStripes", rig, CharacterArt.Pinstripe, Color.white, 3,
            new Vector3(x * s, y * s, -0.01f), Vector3.zero, Vector3.one);
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(w * s, h * s);
        sr.gameObject.SetActive(false);
    }

    private Transform MakeDoll(string name, Color uniform, Color capColor, out Transform rig, bool runner = false)
    {
        var root = new GameObject(name).transform;
        root.SetParent(Root, false);
        rig = new GameObject("Rig").transform;
        rig.SetParent(root, false);
        const float s = DollScale;
        Color pants = new Color(0.93f, 0.93f, 0.95f);
        Color skin = new Color(0.96f, 0.78f, 0.62f);
        Color shoe = new Color(0.15f, 0.13f, 0.13f);
        int v = _dollCount++;
        // 脚(2本)・ソックス・靴
        DollPart(rig, "LegL", pants, 2, -0.10f, 0.36f, 0.17f, 0.62f);
        DollPart(rig, "LegR", pants, 2, 0.10f, 0.36f, 0.17f, 0.62f);
        DollPart(rig, "SockL", capColor, 3, -0.10f, 0.16f, 0.17f, 0.22f);
        DollPart(rig, "SockR", capColor, 3, 0.10f, 0.16f, 0.17f, 0.22f);
        DollPart(rig, "ShoeL", shoe, 3, -0.12f, 0.04f, 0.22f, 0.09f);
        DollPart(rig, "ShoeR", shoe, 3, 0.12f, 0.04f, 0.22f, 0.09f);
        // 胴・ベルト・腕
        DollPart(rig, "Body", uniform, 3, 0f, 0.93f, 0.54f, 0.58f);
        DollStripe(rig, 0f, 0.93f, 0.48f, 0.52f);
        DollStripe(rig, -0.10f, 0.45f, 0.13f, 0.40f);
        DollStripe(rig, 0.10f, 0.45f, 0.13f, 0.40f);
        DollPart(rig, "Belt", new Color(0.12f, 0.12f, 0.16f), 3, 0f, 0.665f, 0.50f, 0.06f);
        DollPart(rig, "ArmL", uniform, 2, -0.32f, 0.90f, 0.14f, 0.44f);
        DollPart(rig, "ArmR", uniform, 2, 0.32f, 0.90f, 0.14f, 0.44f);
        BattingSprites.Make("HandL", rig, SpriteFactory.Circle, skin, 3,
            new Vector3(-0.32f * s, 0.66f * s, 0f), Vector3.zero, new Vector3(0.13f * s, 0.13f * s, 1f));
        if (runner)
        {
            BattingSprites.Make("HandR", rig, SpriteFactory.Circle, skin, 3,
                new Vector3(0.32f * s, 0.66f * s, 0f), Vector3.zero, new Vector3(0.13f * s, 0.13f * s, 1f));
        }
        else
        {
            var g = BattingSprites.Make("Glove", rig, CharacterArt.Glove, Color.white, 4,
                new Vector3(0.34f * s, 0.68f * s, 0f), Vector3.zero, Vector3.one);
            Vector2 gb = CharacterArt.Glove.bounds.size;
            g.transform.localScale = new Vector3(0.30f * s / gb.x, 0.30f * s / gb.y, 1f);
        }
        // 頭(帽子 or ヘルメット。向きは個体ごとに左右ばらす)
        var headSp = CharacterArt.Head("human", skin, capColor, runner ? Headgear.Helmet : Headgear.Cap, Expr.Normal, v, 128);
        var head = BattingSprites.Make("Head", rig, headSp, Color.white, 4,
            new Vector3(0f, 1.42f * s, 0f), Vector3.zero, Vector3.one * (0.50f * s));
        head.flipX = (v % 2) == 1;
        _dollInfo[root] = new DollInfo { Variant = v, Helmet = runner };
        // キャラの立ち姿(切り抜き画像)。SetDollSkin で差し替える。ふだんは非表示
        var stand = BattingSprites.Make("Standee", rig, SpriteFactory.White, Color.white, 4,
            Vector3.zero, Vector3.zero, Vector3.one);
        stand.enabled = false;
        _standees[root] = stand;
        _billboards.Add(root);
        AddShadow(root, 0.9f * s);
        return root;
    }

    private struct DollInfo { public int Variant; public bool Helmet; }
    private readonly Dictionary<Transform, SpriteRenderer> _standees = new Dictionary<Transform, SpriteRenderer>();
    public const float StandeeHeight = 3.3f;   // 遠くでも見えるよう実寸より大きめ(人形と同じ考え方)

    // 人形をキャラの立ち姿に差し替える(null で元の人形に戻す)
    private void SetDollSkin(Transform root, Sprite cut)
    {
        if (root == null) return;
        SpriteRenderer stand;
        if (!_standees.TryGetValue(root, out stand)) return;
        bool on = cut != null;
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            if (sr != stand) sr.enabled = !on;
        stand.enabled = on;
        if (!on) return;
        stand.sprite = cut;
        Vector2 b = cut.bounds.size;
        float k = Mathf.Min(StandeeHeight / b.y, StandeeHeight * 0.95f / b.x);
        stand.transform.localScale = new Vector3(k, k, 1f);
        stand.transform.localPosition = Vector3.zero;
    }

    public void SetFielderSkin(int index, Sprite cut)
    {
        if (index < 0 || index >= _fielders.Length) return;
        SetDollSkin(_fielders[index], cut);
    }

    public void SetRunnerSkin(int slot, Sprite cut)
    {
        if (slot < 0 || slot >= _runners.Length) return;
        SetDollSkin(_runners[slot], cut);
    }
    private readonly Dictionary<Transform, DollInfo> _dollInfo = new Dictionary<Transform, DollInfo>();

    // 守備側・攻撃側のチームカラーで人形を塗り直す
    public void SetTeams(Color fieldJersey, Color fieldAccent, Color runJersey, Color runAccent)
    {
        for (int i = 0; i < _fielders.Length; i++)
            if (_fielders[i] != null) RecolorDoll(_fielders[i], fieldJersey, fieldAccent);
        for (int i = 0; i < _runners.Length; i++)
            if (_runners[i] != null) RecolorDoll(_runners[i], runJersey, runAccent);
    }

    private void RecolorDoll(Transform root, Color jersey, Color accent)
    {
        DollInfo info;
        bool has = _dollInfo.TryGetValue(root, out info);
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            switch (sr.name)
            {
                case "Body": case "ArmL": case "ArmR": sr.color = jersey; break;
                case "SockL": case "SockR": sr.color = accent; break;
                case "DollStripes": sr.gameObject.SetActive(jersey == BattingConfig.UserJersey); break;
                case "Head":
                    if (has)
                        sr.sprite = CharacterArt.Head("human", new Color(0.96f, 0.78f, 0.62f), accent,
                            info.Helmet ? Headgear.Helmet : Headgear.Cap, Expr.Normal, info.Variant, 128);
                    break;
            }
        }
    }

    // ---- 足元の影(ナイター照明で数方向に薄く伸びる) --------------------
    private class ShadowSet { public Transform Target; public Transform[] Parts; public bool UseZ; }
    private readonly List<ShadowSet> _shadows = new List<ShadowSet>();
    private static readonly Vector2[] ShadowDirs = { new Vector2(0f, 0f), new Vector2(0.55f, 0.35f), new Vector2(-0.55f, 0.35f), new Vector2(0.0f, -0.6f) };

    public void AddShadow(Transform target, float size)
    {
        var set = new ShadowSet { Target = target, Parts = new Transform[ShadowDirs.Length] };
        for (int i = 0; i < ShadowDirs.Length; i++)
        {
            Vector2 d = ShadowDirs[i];
            float a = i == 0 ? 0.70f : 0.30f;
            float len = i == 0 ? 1f : 2.2f;
            var sr = BattingSprites.Make("Shadow", Root, CharacterArt.SoftShadow, new Color(0f, 0f, 0f, a), OrderShadow,
                Vector3.zero, new Vector3(90f, 0f, i == 0 ? 0f : -Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg),
                new Vector3(size * 1.0f, size * 0.62f * len, 1f));
            set.Parts[i] = sr.transform;
        }
        _shadows.Add(set);
    }

    private void UpdateShadows()
    {
        for (int i = _shadows.Count - 1; i >= 0; i--)
        {
            var set = _shadows[i];
            if (set.Target == null)
            {
                foreach (var p in set.Parts) if (p != null) Destroy(p.gameObject);
                _shadows.RemoveAt(i);
                continue;
            }
            bool on = set.Target.gameObject.activeInHierarchy;
            Vector3 tp = set.Target.position;
            for (int k = 0; k < set.Parts.Length; k++)
            {
                var p = set.Parts[k];
                if (p.gameObject.activeSelf != on) p.gameObject.SetActive(on);
                if (!on) continue;
                Vector2 d = ShadowDirs[k];
                float sx = p.localScale.y * 0.5f;
                p.position = new Vector3(tp.x + d.x * sx, 0.03f + k * 0.002f, tp.z + d.y * sx);
            }
        }
    }

    private void BuildFielders()
    {
        int n = FieldingModel.Defaults.Length;
        _fielders = new Transform[n];
        _fielderRigs = new Transform[n];
        _fielderBusy = new bool[n];
        _fielderLast = new Vector2[n];
        for (int i = 2; i < n; i++)
        {
            Transform rig;
            _fielders[i] = MakeDoll("Fielder_" + FieldingModel.Defaults[i].Name,
                new Color(0.25f, 0.40f, 0.82f), new Color(0.15f, 0.22f, 0.5f), out rig);
            _fielderRigs[i] = rig;
        }
        // 走者: 0=打者走者 1..3=塁上の走者(出発塁の番号)
        _runners = new Transform[4];
        _runnerRigs = new Transform[4];
        _runnerBusy = new bool[4];
        _runnerLast = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            Transform rig;
            _runners[i] = MakeDoll("Runner_" + i, new Color(0.85f, 0.25f, 0.22f), new Color(0.55f, 0.1f, 0.1f), out rig, true);
            _runnerRigs[i] = rig;
            _runners[i].gameObject.SetActive(false);
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
            _fielderLast[i] = p;
            _fielderBusy[i] = false;
            _fielderRigs[i].localRotation = Quaternion.identity;
            _fielderRigs[i].localPosition = Vector3.zero;
            _fielderRigs[i].localScale = Vector3.one;
        }
    }

    private static void Wobble(Transform rig, Vector2 now, ref Vector2 last)
    {
        bool moving = (now - last).sqrMagnitude > 1e-5f;
        last = now;
        float a = moving ? Mathf.Sin(Time.time * 16f) * 7f * BattingConfig.GunyaLevel : 0f;
        rig.localRotation = Quaternion.Euler(0f, 0f, a);
    }

    public void SetFielderPos(int index, Vector2 xz)
    {
        if (index < 0 || index >= _fielders.Length || _fielders[index] == null) return;
        _fielders[index].position = new Vector3(xz.x, 0f, xz.y);
        if (!_fielderBusy[index]) Wobble(_fielderRigs[index], xz, ref _fielderLast[index]);
    }

    // 転倒: ぐにゃっと横倒しになって、少ししてから起き上がる
    public void Tumble(int index, float seconds)
    {
        if (index < 2 || index >= _fielders.Length || _fielders[index] == null) return;
        StartCoroutine(TumbleRoutine(_fielderRigs[index], index, true, seconds));
    }

    public void TumbleRunner(int slot, float seconds)
    {
        if (slot < 0 || slot >= 4) return;
        StartCoroutine(TumbleRoutine(_runnerRigs[slot], slot, false, seconds));
    }

    private IEnumerator TumbleRoutine(Transform rig, int idx, bool fielder, float seconds)
    {
        if (fielder) _fielderBusy[idx] = true; else _runnerBusy[idx] = true;
        float g = BattingConfig.GunyaLevel;
        float dir = Random.value < 0.5f ? -1f : 1f;
        float angle = 100f * dir * Mathf.Min(1.4f, 0.8f + 0.2f * g);
        float down = 0.16f, hold = Mathf.Max(0.2f, seconds - 0.45f), up = 0.28f;
        float t = 0f;
        while (t < down)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / down);
            rig.localRotation = Quaternion.Euler(0f, 0f, angle * u);
            rig.localPosition = new Vector3(0f, Mathf.Sin(u * Mathf.PI) * 1.6f * g, 0f);
            yield return null;
        }
        rig.localPosition = Vector3.zero;
        t = 0f;
        while (t < hold)
        {
            t += Time.deltaTime;
            rig.localRotation = Quaternion.Euler(0f, 0f, angle + Mathf.Sin(t * 22f) * 6f * g);
            yield return null;
        }
        t = 0f;
        while (t < up)
        {
            t += Time.deltaTime;
            rig.localRotation = Quaternion.Euler(0f, 0f, angle * (1f - Mathf.Clamp01(t / up)));
            yield return null;
        }
        rig.localRotation = Quaternion.identity;
        if (fielder) _fielderBusy[idx] = false; else _runnerBusy[idx] = false;
    }

    // 捕球の「むにゅっ」(縦に伸びて戻る)
    public void CatchPop(int index)
    {
        if (index < 2 || index >= _fielders.Length || _fielders[index] == null) return;
        StartCoroutine(PopRoutine(_fielderRigs[index]));
    }

    private IEnumerator PopRoutine(Transform rig)
    {
        float g = BattingConfig.GunyaLevel;
        float t = 0f;
        const float dur = 0.30f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float k = Mathf.Sin(u * Mathf.PI) * 0.28f * g;
            rig.localScale = new Vector3(1f - k * 0.5f, 1f + k, 1f);
            yield return null;
        }
        rig.localScale = Vector3.one;
    }

    // ---- 走者 ----
    public void HideRunners()
    {
        for (int i = 0; i < 4; i++) _runners[i].gameObject.SetActive(false);
    }

    public void ShowRunnersOnBases(bool[] bases)
    {
        for (int i = 1; i <= 3; i++)
        {
            _runners[i].gameObject.SetActive(bases[i]);
            if (bases[i])
            {
                Vector2 p = FieldingModel.BasePos(i);
                _runners[i].position = new Vector3(p.x, 0f, p.y);
                _runnerLast[i] = p;
                _runnerRigs[i].localRotation = Quaternion.identity;
            }
        }
        _runners[0].gameObject.SetActive(false);
    }

    public void ShowRunner(int slot, bool on)
    {
        _runners[slot].gameObject.SetActive(on);
        if (on) _runnerRigs[slot].localRotation = Quaternion.identity;
    }

    public void SetRunnerPos(int slot, Vector2 xz)
    {
        _runners[slot].position = new Vector3(xz.x, 0f, xz.y);
        if (!_runnerBusy[slot]) Wobble(_runnerRigs[slot], xz, ref _runnerLast[slot]);
    }

    // ------------------------------------------------------------
    // 投球の軌跡(変化球を目で追えるように、うっすら残像を残す)
    // ------------------------------------------------------------
    private class TrailDot { public SpriteRenderer Sr; public float Born; }
    private readonly List<TrailDot> _trail = new List<TrailDot>();
    private float _lastTrail;
    private const float TrailLife = 0.55f;

    public void AddTrail(Vector3 pos, Color col)
    {
        if (Time.time - _lastTrail < 0.022f) return;
        _lastTrail = Time.time;
        float dist = (pos - Cam.transform.position).magnitude;
        float dia = Mathf.Max(BattingConfig.BallRadius * 2f * 0.7f, dist * 0.009f);
        var sr = BattingSprites.Make("Trail", Root, SpriteFactory.Circle, col, OrderBall - 2,
            pos, Vector3.zero, Vector3.one * dia);
        sr.transform.rotation = Cam.transform.rotation;
        _trail.Add(new TrailDot { Sr = sr, Born = Time.time });
    }

    public void ClearTrail()
    {
        for (int i = 0; i < _trail.Count; i++) if (_trail[i].Sr != null) Destroy(_trail[i].Sr.gameObject);
        _trail.Clear();
    }

    private void UpdateTrail()
    {
        for (int i = _trail.Count - 1; i >= 0; i--)
        {
            var d = _trail[i];
            float u = (Time.time - d.Born) / TrailLife;
            if (u >= 1f || d.Sr == null)
            {
                if (d.Sr != null) Destroy(d.Sr.gameObject);
                _trail.RemoveAt(i);
                continue;
            }
            var c = d.Sr.color; c.a = (1f - u) * 0.55f; d.Sr.color = c;
        }
    }
}
