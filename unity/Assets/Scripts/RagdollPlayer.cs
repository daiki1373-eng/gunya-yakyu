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
        else if (stats.Species == "dog") skin = new Color(0.84f, 0.64f, 0.42f); // イヌは毛色
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
            var bsr = Bat.GetComponentInChildren<SpriteRenderer>();
            bsr.drawMode = SpriteDrawMode.Simple;
            bsr.sprite = CharacterArt.Bat;
            bsr.color = Color.white;
            Vector2 bb = CharacterArt.Bat.bounds.size;
            bsr.transform.localScale = new Vector3(0.11f / bb.x, 0.94f / bb.y, 1f);
        }
        AddUniformDetails(uniform, accent, isBatter, skin, legBackFore, legFrontFore, legBackUpper, legFrontUpper);

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
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        if (circle)
        {
            sr.sprite = SpriteFactory.Circle;
            vis.transform.localScale = new Vector3(size.x, size.y, 1f);
        }
        else
        {
            // 角丸+輪郭の9スライス。scaleを使わず size で伸ばすので輪郭の太さが一定になる。
            sr.sprite = CharacterArt.Part;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            vis.transform.localScale = Vector3.one;
        }

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
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        if (circle)
        {
            sr.sprite = SpriteFactory.Circle;
            vis.transform.localScale = new Vector3(size.x, size.y, 1f);
        }
        else
        {
            sr.sprite = CharacterArt.Part;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            vis.transform.localScale = Vector3.one;
        }
    }

    private SpriteRenderer AddSprite(Rigidbody2D part, string name, Sprite sprite, Vector2 localPos, Vector2 size,
        Color color, int sortingOrder, float rotationZ = 0f)
    {
        var vis = new GameObject(name);
        vis.transform.SetParent(part.transform, false);
        vis.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        vis.transform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
        vis.layer = RagdollLayer;
        var sr = vis.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        Vector2 bs = sprite.bounds.size;
        vis.transform.localScale = new Vector3(size.x / bs.x, size.y / bs.y, 1f);
        return sr;
    }

    private void AddShoe(Rigidbody2D shin, Color shoeColor)
    {
        // つま先は進行方向(facing)に少し出す
        AddVisual(shin, "Shoe", new Vector2(0.05f * Facing, -0.20f), new Vector2(0.26f, 0.10f), shoeColor, 6);
        AddVisual(shin, "Sole", new Vector2(0.05f * Facing, -0.245f), new Vector2(0.26f, 0.035f), new Color(0.92f, 0.92f, 0.9f), 6);
    }

    private void AddGlove(Rigidbody2D fore)
    {
        var g = AddSprite(fore, "Glove", CharacterArt.Glove, new Vector2(0f, -0.24f), new Vector2(0.32f, 0.32f),
            Color.white, 5);
        g.flipX = Facing < 0f;
    }

    // ---- 顔(表情つきの頭部イラスト) --------------------------------
    private SpriteRenderer _headSr;
    private string _species;
    private Color _skin, _hatColor;
    private Headgear _gear;
    private int _variant;
    private Expr _expr = Expr.Normal;
    private float _exprTimer;

    private void AddFace(Rigidbody2D head, float f, PlayerStats stats, Color accent, bool isBatter)
    {
        _headSr = head.GetComponentInChildren<SpriteRenderer>();
        _species = string.IsNullOrEmpty(stats.Species) ? "human" : stats.Species;
        _skin = _headSr.color;
        _hatColor = accent;
        _gear = isBatter ? Headgear.Helmet : Headgear.Cap;
        _variant = string.IsNullOrEmpty(stats.Name) ? 0 : Mathf.Abs(stats.Name.GetHashCode());
        _headSr.color = Color.white;
        _headSr.flipX = f < 0f;
        // 表情の絵は打席の最初にまとめて作っておく(試合中のカクつき防止。2回目以降はキャッシュ)
        for (int e = 0; e < 5; e++) CharacterArt.Head(_species, _skin, _hatColor, _gear, (Expr)e, _variant);
        ApplyExpression(Expr.Normal);
    }

    private void ApplyExpression(Expr e)
    {
        if (_headSr == null) return;
        _expr = e;
        _headSr.sprite = CharacterArt.Head(_species, _skin, _hatColor, _gear, e, _variant);
    }

    // 表情を一定時間だけ変える(時間が過ぎたら通常顔に戻る)
    public void SetExpression(Expr e, float seconds)
    {
        ApplyExpression(e);
        _exprTimer = seconds;
    }

    private void Update()
    {
        if (_exprTimer > 0f)
        {
            _exprTimer -= Time.deltaTime;
            if (_exprTimer <= 0f && _expr != Expr.Normal) ApplyExpression(Expr.Normal);
        }
    }

    // 体の細部(ベルト・ソックス・手袋・胸のライン)
    private void AddUniformDetails(Color uniform, Color accent, bool isBatter, Color skin, Rigidbody2D shinBack, Rigidbody2D shinFront,
        Rigidbody2D thighBack, Rigidbody2D thighFront)
    {
        float f = Facing;
        Color belt = new Color(0.12f, 0.12f, 0.16f);
        AddVisual(Head, "Neck", new Vector2(0f, -0.34f), new Vector2(0.20f, 0.18f), skin, 6);
        AddVisual(Pelvis, "Belt", new Vector2(0f, 0.085f), new Vector2(0.42f, 0.075f), belt, 6);
        AddVisual(Pelvis, "Buckle", new Vector2(0.15f * f, 0.085f), new Vector2(0.06f, 0.06f), new Color(0.85f, 0.82f, 0.7f), 6);
        // ストッキング(すねの下半分をチーム色に)
        AddVisual(shinBack, "Sock", new Vector2(0f, -0.08f), new Vector2(0.135f, 0.24f), accent, 4);
        AddVisual(shinFront, "Sock", new Vector2(0f, -0.08f), new Vector2(0.135f, 0.24f), accent, 6);
        // ズボンの側線
        AddVisual(thighBack, "Piping", new Vector2(0f, 0f), new Vector2(0.035f, 0.40f), accent, 4);
        AddVisual(thighFront, "Piping", new Vector2(0f, 0f), new Vector2(0.035f, 0.40f), accent, 6);
        // 胸の前立て・袖口のライン
        AddVisual(Chest, "Placket", new Vector2(0.17f * f, -0.02f), new Vector2(0.035f, 0.40f), accent, 6);
        AddVisual(Chest, "Collar", new Vector2(0.10f * f, 0.19f), new Vector2(0.22f, 0.05f), accent, 6);
        AddVisual(ArmFrontUpper, "Cuff", new Vector2(0f, -0.14f), new Vector2(0.13f, 0.06f), accent, 6);
        AddVisual(ArmBackUpper, "Cuff", new Vector2(0f, -0.14f), new Vector2(0.13f, 0.06f), accent, 4);
        // 手(打者はバッティンググローブ)
        Color hand = isBatter ? new Color(0.95f, 0.95f, 0.95f) : skin;
        AddVisual(ArmFrontFore, "Hand", new Vector2(0f, -0.19f), new Vector2(0.13f, 0.13f), hand, 6, 0f, true);
        if (isBatter) AddVisual(ArmBackFore, "Hand", new Vector2(0f, -0.19f), new Vector2(0.13f, 0.13f), hand, 4, 0f, true);
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
        _exprTimer = 0f;
        ApplyExpression(Expr.Normal);
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
        SetExpression(Expr.Focus, 0.7f);
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
        if (_expr == Expr.Normal) SetExpression(Expr.Focus, 0.5f);
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
        SetExpression(Expr.Dizzy, 1.6f);
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
