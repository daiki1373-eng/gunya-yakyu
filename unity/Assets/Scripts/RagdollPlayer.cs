using System.Collections.Generic;
using UnityEngine;

// ============================================================
// RagdollPlayer.cs
// 頭・胴・両腕・両脚を Rigidbody2D + HingeJoint2D で連結した
// 2頭身ラグドール選手をコードで生成するクラス。投手・打者で使い回す。
//
// 設計メモ:
// - 関節の可動域はわざと大げさに広く取り、「関節が異常に曲がる」土台にする。
// - 全パーツを同一レイヤー(8)に置き、Bootstrap側で同レイヤー同士の衝突を
//   無効化する(自分の体同士で絡まないように)。地面(レイヤー0)とは衝突する。
// - 立ち続けるために、胸・頭・腰へ「起き上がりトルク」を掛ける簡易バランス
//   制御と、腰をホームポジションへ戻す弱いバネを入れている。
//   スイング/投球/転倒の演出中は _poseHoldTimer / _flopTimer で制御を緩める。
// - ResetPose() で初期姿勢に完全に戻せるので、1球ごとにリセットして使う。
// ============================================================
public class RagdollPlayer : MonoBehaviour
{
    public const int RagdollLayer = 8;

    public Rigidbody2D Pelvis { get; private set; }
    public Rigidbody2D Chest { get; private set; }
    public Rigidbody2D Head { get; private set; }
    public Rigidbody2D ArmFrontUpper { get; private set; }
    public Rigidbody2D ArmFrontFore { get; private set; }
    public Rigidbody2D ArmBackUpper { get; private set; }
    public Rigidbody2D ArmBackFore { get; private set; }
    public Rigidbody2D Bat { get; private set; } // 打者のみ

    public float Facing { get; private set; } // +1: 右向き, -1: 左向き
    public PlayerStats Stats { get; private set; }

    private struct PartState
    {
        public Rigidbody2D Body;
        public Vector2 Pos;
        public float Rot;
    }

    private readonly List<PartState> _initialPose = new List<PartState>();
    private readonly List<Rigidbody2D> _allParts = new List<Rigidbody2D>();
    private Vector2 _pelvisHome;
    private float _poseHoldTimer;
    private float _flopTimer;

    // ------------------------------------------------------------
    // 生成
    // ------------------------------------------------------------
    public static RagdollPlayer Create(Transform parent, string name, Vector2 rootPos, float facing,
        PlayerStats stats, Color uniform, Color accent, bool isBatter)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(rootPos.x, rootPos.y, 0f);
        var player = go.AddComponent<RagdollPlayer>();
        player.Build(rootPos, facing, stats, uniform, accent, isBatter);
        return player;
    }

    private void Build(Vector2 rootPos, float facing, PlayerStats stats, Color uniform, Color accent, bool isBatter)
    {
        Facing = facing;
        Stats = stats;
        float f = facing;
        Transform t = transform;

        Color skin = new Color(0.96f, 0.78f, 0.62f);
        if (stats.Species == "panda") skin = Color.white; // パンダは白頭
        Color pants = new Color(0.92f, 0.92f, 0.94f);
        Color shoe = new Color(0.25f, 0.2f, 0.2f);
        Color sleeve = uniform;
        Color torso = uniform;

        // 脚 (後ろ脚→腰→前脚の順に作るが、親子はジョイントで繋ぐ)
        var legBackUpper = MakePart(t, "LegBackUpper", rootPos + new Vector2(-0.04f * f, 0.61f), new Vector2(0.16f, 0.42f), pants, false, 0.6f, 4);
        var legBackFore = MakePart(t, "LegBackFore", rootPos + new Vector2(-0.04f * f, 0.21f), new Vector2(0.13f, 0.42f), pants, false, 0.5f, 4);
        var legFrontUpper = MakePart(t, "LegFrontUpper", rootPos + new Vector2(0.05f * f, 0.61f), new Vector2(0.16f, 0.42f), pants, false, 0.6f, 6);
        var legFrontFore = MakePart(t, "LegFrontFore", rootPos + new Vector2(0.05f * f, 0.21f), new Vector2(0.13f, 0.42f), pants, false, 0.5f, 6);
        AddShoe(legBackFore, shoe);
        AddShoe(legFrontFore, shoe);

        Pelvis = MakePart(t, "Pelvis", rootPos + new Vector2(0f, 0.90f), new Vector2(0.40f, 0.24f), pants, false, 1.4f, 5);
        Chest = MakePart(t, "Chest", rootPos + new Vector2(0f, 1.22f), new Vector2(0.48f, 0.44f), torso, false, 1.6f, 5);
        Head = MakePart(t, "Head", rootPos + new Vector2(0f, 1.86f), new Vector2(0.74f, 0.74f), skin, true, 0.8f, 7);
        AddFace(Head, f, stats, accent, isBatter);

        ArmBackUpper = MakePart(t, "ArmBackUpper", rootPos + new Vector2(-0.15f * f, 1.21f), new Vector2(0.12f, 0.38f), sleeve, false, 0.35f, 4);
        ArmBackFore = MakePart(t, "ArmBackFore", rootPos + new Vector2(-0.15f * f, 0.86f), new Vector2(0.10f, 0.36f), skin, false, 0.3f, 4);
        ArmFrontUpper = MakePart(t, "ArmFrontUpper", rootPos + new Vector2(0.17f * f, 1.21f), new Vector2(0.12f, 0.38f), sleeve, false, 0.35f, 6);
        ArmFrontFore = MakePart(t, "ArmFrontFore", rootPos + new Vector2(0.17f * f, 0.86f), new Vector2(0.10f, 0.36f), skin, false, 0.3f, 6);

        if (!isBatter)
        {
            // 投手のグラブ(見た目のみ): 後ろ手の先に茶色い丸
            AddGlove(ArmBackFore);
        }

        // 関節。角度制限は大げさに広く取る。
        // 膝だけは曲がる向きが facing で反転するので制限をミラーする。
        float kneeMin = (f > 0f) ? -160f : -40f;
        float kneeMax = (f > 0f) ? 40f : 160f;
        Connect(legBackFore, legBackUpper, new Vector2(0f, 0.20f), new Vector2(0f, -0.20f), kneeMin, kneeMax);   // 膝(後)
        Connect(legFrontFore, legFrontUpper, new Vector2(0f, 0.20f), new Vector2(0f, -0.20f), kneeMin, kneeMax); // 膝(前)
        Connect(legBackUpper, Pelvis, new Vector2(0f, 0.20f), new Vector2(-0.04f * f, -0.09f), -120f, 120f); // 股(後)
        Connect(legFrontUpper, Pelvis, new Vector2(0f, 0.20f), new Vector2(0.05f * f, -0.09f), -120f, 120f); // 股(前)
        Connect(Chest, Pelvis, new Vector2(0f, -0.20f), new Vector2(0f, 0.12f), -55f, 55f);                    // 腰
        Connect(Head, Chest, new Vector2(0f, -0.39f), new Vector2(0f, 0.25f), -60f, 60f);                      // 首(関節点は首元で一致させる)
        Connect(ArmBackFore, ArmBackUpper, new Vector2(0f, 0.17f), new Vector2(0f, -0.18f), -170f, 170f);      // 肘(後)
        Connect(ArmFrontFore, ArmFrontUpper, new Vector2(0f, 0.17f), new Vector2(0f, -0.18f), -170f, 170f);    // 肘(前)
        Connect(ArmBackUpper, Chest, new Vector2(0f, 0.18f), new Vector2(-0.15f * f, 0.17f), -170f, 170f);     // 肩(後)
        Connect(ArmFrontUpper, Chest, new Vector2(0f, 0.18f), new Vector2(0.17f * f, 0.17f), -170f, 170f);     // 肩(前)

        if (isBatter)
        {
            // バット: 前手の先から真上に立つ形で接続(関節点を手の位置に一致させる)。
            // スイングは角速度で振る。
            Bat = MakePart(t, "Bat", rootPos + new Vector2(0.17f * f, 1.08f), new Vector2(0.09f, 0.92f),
                new Color(0.72f, 0.5f, 0.28f), false, 0.35f, 8);
            Connect(Bat, ArmFrontFore, new Vector2(0f, -0.40f), new Vector2(0f, -0.18f), -170f, 170f);
        }

        _pelvisHome = rootPos + new Vector2(0f, 0.90f);
        CapturePose();
    }

    private Rigidbody2D MakePart(Transform parent, string name, Vector2 pos, Vector2 size, Color color,
        bool circle, float mass, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.layer = RagdollLayer;

        var vis = new GameObject("Visual");
        vis.transform.SetParent(go.transform, false);
        vis.layer = RagdollLayer;
        var sr = vis.AddComponent<SpriteRenderer>();
        sr.sprite = circle ? SpriteFactory.Circle : SpriteFactory.White;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        vis.transform.localScale = new Vector3(size.x, size.y, 1f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.mass = mass;
        rb.drag = 0.05f;
        rb.angularDrag = 0.15f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

        if (circle)
        {
            var c = go.AddComponent<CircleCollider2D>();
            c.radius = size.x * 0.5f;
        }
        else
        {
            var b = go.AddComponent<BoxCollider2D>();
            b.size = size;
        }
        _allParts.Add(rb);
        return rb;
    }

    private static HingeJoint2D Connect(Rigidbody2D child, Rigidbody2D parentBody,
        Vector2 anchorOnChild, Vector2 anchorOnParent, float minAngle, float maxAngle)
    {
        var joint = child.gameObject.AddComponent<HingeJoint2D>();
        joint.autoConfigureConnectedAnchor = false; // connectedAnchor をコード指定どおりに使う
        joint.connectedBody = parentBody;
        joint.anchor = anchorOnChild;
        joint.connectedAnchor = anchorOnParent;
        joint.enableCollision = false;
        joint.useLimits = true;
        var limits = joint.limits;
        limits.min = minAngle;
        limits.max = maxAngle;
        joint.limits = limits;
        return joint;
    }

    private void AddVisual(Rigidbody2D part, string name, Vector2 localPos, Vector2 size, Color color,
        int sortingOrder, float rotationZ = 0f, bool circle = false)
    {
        var vis = new GameObject(name);
        vis.transform.SetParent(part.transform, false);
        vis.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        vis.transform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
        vis.layer = RagdollLayer;
        var sr = vis.AddComponent<SpriteRenderer>();
        sr.sprite = circle ? SpriteFactory.Circle : SpriteFactory.White;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        vis.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    private void AddShoe(Rigidbody2D shin, Color shoeColor)
    {
        // つま先は進行方向(facing)に少し出す
        AddVisual(shin, "Shoe", new Vector2(0.05f * Facing, -0.20f), new Vector2(0.24f, 0.09f), shoeColor, 6);
    }

    private void AddGlove(Rigidbody2D fore)
    {
        AddVisual(fore, "Glove", new Vector2(0f, -0.24f), new Vector2(0.30f, 0.30f),
            new Color(0.62f, 0.4f, 0.2f), 5, 0f, true);
    }

    private void AddFace(Rigidbody2D head, float f, PlayerStats stats, Color accent, bool isBatter)
    {
        // 帽子 or ヘルメット
        Color hatColor = accent;
        AddVisual(head, "CapTop", new Vector2(0f, 0.24f), new Vector2(0.66f, 0.20f), hatColor, 8);
        AddVisual(head, "CapBrim", new Vector2(0.34f * f, 0.18f), new Vector2(0.30f, 0.06f), hatColor, 8);
        if (isBatter)
        {
            // ヘルメットの耳当てっぽく少し大きく
            AddVisual(head, "HelmetSide", new Vector2(-0.05f * f, 0.05f), new Vector2(0.55f, 0.30f), hatColor, 7);
        }

        // 目(横向きなので1つ)。パンダは黒い目当てが代わりになる。
        if (stats.Species == "panda")
        {
            AddVisual(head, "EyePatch", new Vector2(0.16f * f, 0.03f), new Vector2(0.17f, 0.22f),
                Color.black, 8, 0f, true);
            // 耳(黒い丸×2)
            AddVisual(head, "EarL", new Vector2(-0.27f, 0.30f), new Vector2(0.20f, 0.20f), Color.black, 7, 0f, true);
            AddVisual(head, "EarR", new Vector2(0.27f, 0.30f), new Vector2(0.20f, 0.20f), Color.black, 7, 0f, true);
        }
        else
        {
            AddVisual(head, "Eye", new Vector2(0.21f * f, 0.03f), new Vector2(0.06f, 0.11f),
                new Color(0.1f, 0.1f, 0.12f), 8);
        }

        if (stats.Species == "beetle")
        {
            // カブトムシの角(前に倒れた太い角+小さい分岐)
            Color horn = new Color(0.3f, 0.18f, 0.1f);
            AddVisual(head, "Horn", new Vector2(0.18f * f, 0.44f), new Vector2(0.08f, 0.55f), horn, 8, -38f * f);
            AddVisual(head, "HornFork", new Vector2(0.36f * f, 0.60f), new Vector2(0.07f, 0.25f), horn, 8, 35f * f);
        }
        else if (stats.Species == "dog")
        {
            // イヌ耳(垂れ耳×2)
            Color ear = new Color(0.55f, 0.38f, 0.22f);
            AddVisual(head, "EarL", new Vector2(-0.28f, 0.22f), new Vector2(0.13f, 0.30f), ear, 7, 18f);
            AddVisual(head, "EarR", new Vector2(0.28f, 0.22f), new Vector2(0.13f, 0.30f), ear, 7, -18f);
        }
    }

    private void CapturePose()
    {
        _initialPose.Clear();
        for (int i = 0; i < _allParts.Count; i++)
        {
            var rb = _allParts[i];
            _initialPose.Add(new PartState { Body = rb, Pos = rb.position, Rot = rb.rotation });
        }
    }

    // ------------------------------------------------------------
    // 姿勢リセット / バランス制御
    // ------------------------------------------------------------
    public void ResetPose()
    {
        _poseHoldTimer = 0f;
        _flopTimer = 0f;
        for (int i = 0; i < _initialPose.Count; i++)
        {
            var s = _initialPose[i];
            s.Body.velocity = Vector2.zero;
            s.Body.angularVelocity = 0f;
            s.Body.position = s.Pos;
            s.Body.rotation = s.Rot;
            // z座標は保つ(奥行きに置いた選手が原点面へ戻らないように)
            s.Body.transform.position = new Vector3(s.Pos.x, s.Pos.y, s.Body.transform.position.z);
            s.Body.transform.rotation = Quaternion.Euler(0f, 0f, s.Rot);
        }
    }

    private void FixedUpdate()
    {
        if (_flopTimer > 0f)
        {
            _flopTimer -= Time.fixedDeltaTime;
            return; // 転倒中はバランス制御を切って、ぐにゃっとさせる
        }
        if (_poseHoldTimer > 0f) _poseHoldTimer -= Time.fixedDeltaTime;

        // 腰をホームポジションへ弱く引き戻す(横方向のみ。縦は脚と地面に任せる)
        if (Pelvis != null)
        {
            float dx = _pelvisHome.x - Pelvis.position.x;
            Pelvis.AddForce(new Vector2(dx * 16f, 0f));
        }

        if (_poseHoldTimer <= 0f)
        {
            BalancePart(Chest, 1.0f);
            BalancePart(Head, 0.8f);
            BalancePart(Pelvis, 0.5f);
        }
    }

    private void BalancePart(Rigidbody2D rb, float strength)
    {
        if (rb == null) return;
        // DeltaAngle(rotation, 0) は「0 - rotation」を返すので、
        // そのまま比例項に使うと起き上がる向きのトルクになる(符号に注意)。
        float ang = Mathf.DeltaAngle(rb.rotation, 0f);
        float torque = (ang * 0.12f - rb.angularVelocity * 0.010f) * strength;
        rb.AddTorque(Mathf.Clamp(torque, -6f, 6f));
    }

    // ------------------------------------------------------------
    // 動作(演出)。判定はGameController側の確率モデルで行い、
    // ここは見た目の動きだけを担当する。
    // ------------------------------------------------------------
    public void DoPitchMotion()
    {
        _poseHoldTimer = 0.35f;
        float f = Facing;
        // 腕は下向きに垂れているので、「前へ振る」角速度はバットと逆符号になる。
        float g = 0.6f + 0.4f * BattingConfig.GunyaLevel;
        if (ArmFrontUpper != null) ArmFrontUpper.angularVelocity = 800f * f * g;
        if (ArmFrontFore != null) ArmFrontFore.angularVelocity = 1250f * f * g;
        if (Chest != null) Chest.angularVelocity = 120f * f * g;
    }

    public void DoSwing()
    {
        _poseHoldTimer = 0.30f;
        float f = Facing;
        // バットは上向きに立っているので前へ倒れる向き、腕は下向きなので逆符号。
        float g = 0.6f + 0.4f * BattingConfig.GunyaLevel;
        if (Bat != null) Bat.angularVelocity = -1500f * f * g;
        if (ArmFrontFore != null) ArmFrontFore.angularVelocity = 1100f * f * g;
        if (ArmFrontUpper != null) ArmFrontUpper.angularVelocity = 700f * f * g;
        if (Chest != null) Chest.angularVelocity = 150f * f * g;
    }

    // バット投げ: バットを手から外してポーンと飛ばす(ぐにゃ度で勢いが増す)。
    // 打席の終わりに使う。ResetPoseでは戻らない(打者は打席ごとに作り直す)。
    public void ThrowBat(float power)
    {
        if (Bat == null) return;
        foreach (var j in Bat.GetComponents<HingeJoint2D>()) j.enabled = false;
        float f = Facing;
        Bat.velocity = new Vector2(-f * Random.Range(2.5f, 4.5f) * power, Random.Range(4.5f, 7f) * power);
        Bat.angularVelocity = Random.Range(500f, 900f) * power * (Random.value < 0.5f ? -1f : 1f);
    }

    // 三振や大飛球のあとに、ぐにゃっと崩れる演出。
    public void Flop(float power)
    {
        _flopTimer = 0.9f;
        for (int i = 0; i < _allParts.Count; i++)
        {
            var rb = _allParts[i];
            rb.angularVelocity = Random.Range(-320f, 320f) * power;
        }
        if (Pelvis != null)
        {
            Pelvis.velocity = new Vector2(Random.Range(-0.8f, 0.8f), 2.2f * power);
        }
    }
}
