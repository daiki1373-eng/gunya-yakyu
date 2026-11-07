using System.Collections.Generic;
using UnityEngine;

// ============================================================
// RagdollPlayer.cs
// 頭・胴・両腕・両脚を Rigidbody2D + HingeJoint2D で連結した
// 2頭身ラグドール選手をコードで生成するクラス。投手・打者で使い回す。
//
// 設計メモ:
// - 関節の可動域はわざと大げさに広く取り、「関節が異常に曲がる」土台にする。
// - 全パーツを同一レイヤー(8)に置く。生成時にColliderの組ごとに衝突を
//   無効化する(自分の体同士で絡まないように)。地面とは衝突する。
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
    public Rigidbody2D LegBackUpper { get; private set; }
    public Rigidbody2D LegFrontUpper { get; private set; }
    public Rigidbody2D LegBackFore { get; private set; }
    public Rigidbody2D LegFrontFore { get; private set; }
    // 登場演出中だけ「立っている」補助を入れる(脚をまっすぐ・腰を持ち上げる)
    public bool StandAssist;

    // 元絵をそのまま体にする(Tools/chara-rig のリグがあるキャラ)。false で従来のぐにゃ体
    public static bool UseRigSkin = true;
    public RigSkin Skin { get; private set; }
    public bool BatThrown { get; private set; }
    public float HeadPopScale { get; private set; }
    public Expr CurrentExpr { get { return _expr; } }

    // 元絵の体を使うときは、ラグドールの絵(胴・腕・脚・顔・飾り)を消して当たり判定だけ残す
    private void HideBodyArt(bool keepBat)
    {
        foreach (var rb in _allParts)
        {
            if (keepBat && rb == Bat) continue;
            foreach (var sr in rb.GetComponentsInChildren<SpriteRenderer>(true)) sr.enabled = false;
        }
    }

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
    private readonly List<Collider2D> _colliders = new List<Collider2D>();
    private struct JointState { public HingeJoint2D Joint; public float Min, Max; }
    private readonly List<JointState> _joints = new List<JointState>();
    private static readonly List<RagdollPlayer> ActivePlayers = new List<RagdollPlayer>();
    private float _stackSpin = 1f;
    private Vector2 _pelvisHome, _pelvisHomeInit;
    private float _poseHoldTimer;
    private PoseCardProto _poseCard;
    private float _flopTimer;

    // ------------------------------------------------------------
    // 生成
    // ------------------------------------------------------------
    public static RagdollPlayer Create(Transform parent, string name, Vector2 rootPos, float facing,
        PlayerStats stats, Color uniform, Color accent, bool isBatter, bool pinstripe = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(rootPos.x, rootPos.y, 0f);
        var player = go.AddComponent<RagdollPlayer>();
        if (stats.HasLook) { uniform = stats.LookJersey; accent = stats.LookTrim; pinstripe = stats.LookStripe; }
        player.Build(rootPos, facing, stats, uniform, accent, isBatter);
        player.IgnoreBodyCollisions();
        if (pinstripe) player.AddPinstripes();
        if (UseRigSkin && !string.IsNullOrEmpty(stats.CharaId))
        {
            player.Skin = RigSkin.TryAttach(player, stats.CharaId);
            if (player.Skin != null)
            {
                player.HideBodyArt(!player.Skin.HasPaintedBat);
                // Aポーズの体: 打者はキャラのバット(持ち物)の絵、投手はキャラのグローブの絵を持たせる
                if (player.Skin.UsesBodyArt)
                {
                    if (player.Bat != null)
                    {
                        // 2本目の持ち物(<id>_2。双子の金バットなど)があれば、打席ごとにどちらかを持つ
                        var second = CharaCatalog.BatArt(stats.CharaId + "_2");
                        player.ApplyBatArt(second != null && Random.value < 0.5f ? second : CharaCatalog.BatArt(stats.CharaId));
                    }
                    else player.Skin.AttachGlove(CharaCatalog.GloveArt(stats.CharaId));
                    player._poseCard = PoseCardProto.TryAttach(player, stats.CharaId);
                }
            }
        }
        return player;
    }

    // バットの見た目をキャラの持ち物の絵にする(絵は握る側が左。標準のバット=横512px)。
    // 当たり判定・振り方は変えず、見た目の長さだけ絵に合わせる(大根は少し短く、巨大バットは長く)。
    public void ApplyBatArt(Sprite art)
    {
        if (art == null || Bat == null) return;
        var bsr = Bat.GetComponentInChildren<SpriteRenderer>();
        if (bsr == null) return;
        bsr.sprite = art;
        bsr.color = Color.white;
        float len = 0.94f * Mathf.Clamp(art.rect.width / 512f, 0.35f, 1.3f);
        float k = len / Mathf.Max(0.01f, art.bounds.size.x);
        var t = bsr.transform;
        t.localRotation = Quaternion.Euler(0f, 0f, 90f);   // 絵の右(先端)を上へ
        t.localScale = new Vector3(k, k, 1f);
        t.localPosition = new Vector3(0f, -0.47f + len * 0.5f, t.localPosition.z);   // 握る端を手の位置にそろえる
    }

    // レイヤー行列を書き換えず、自分の体と他の選手の体だけ衝突を無効にする。
    // 再有効化時にUnityがIgnoreCollisionの状態を失う場合にも設定し直す。
    private void OnEnable()
    {
        if (!ActivePlayers.Contains(this)) ActivePlayers.Add(this);
        IgnoreBodyCollisions();
    }

    private void OnDisable() { ActivePlayers.Remove(this); }

    private void IgnoreBodyCollisions()
    {
        for (int i = 0; i < _colliders.Count; i++)
        {
            for (int j = 0; j < i; j++) Physics2D.IgnoreCollision(_colliders[i], _colliders[j], true);
            for (int p = 0; p < ActivePlayers.Count; p++)
            {
                var other = ActivePlayers[p];
                if (other == null || other == this) continue;
                other.IgnoreCollisionWith(_colliders[i]);
            }
        }
    }

    public void IgnoreCollisionWith(Collider2D other)
    {
        if (other == null) return;
        for (int i = 0; i < _colliders.Count; i++)
            if (_colliders[i] != null) Physics2D.IgnoreCollision(_colliders[i], other, true);
    }

    // 重ねは見た目だけに反映。判定に使うStatsには一切書き込まない。
    // 1枚で従来どおり、上限で関節の両端+25度・スイング角速度1.2倍。
    public void SetStackCount(int ownedCopies, int stackLimit)
    {
        float progress = stackLimit <= 1 ? 0f : Mathf.Clamp01((ownedCopies - 1f) / (stackLimit - 1f));
        _stackSpin = Mathf.Lerp(1f, 1.2f, progress);
        float extra = 25f * progress;
        for (int i = 0; i < _joints.Count; i++)
        {
            JointState state = _joints[i];
            if (state.Joint == null) continue;
            var limits = state.Joint.limits;
            limits.min = Mathf.Max(-180f, state.Min - extra);
            limits.max = Mathf.Min(180f, state.Max + extra);
            state.Joint.limits = limits;
        }
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
        if (stats.SkinTint.a > 0f) skin = stats.SkinTint;
        Color pants = stats.HasLook ? stats.LookPants : new Color(0.92f, 0.92f, 0.94f);
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

        LegBackUpper = legBackUpper;
        LegFrontUpper = legFrontUpper;
        LegBackFore = legBackFore;
        LegFrontFore = legFrontFore;
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
        _pelvisHomeInit = _pelvisHome;
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
            _colliders.Add(c);
        }
        else
        {
            var b = go.AddComponent<BoxCollider2D>();
            b.size = size;
            _colliders.Add(b);
        }
        _allParts.Add(rb);
        return rb;
    }

    private HingeJoint2D Connect(Rigidbody2D child, Rigidbody2D parentBody,
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
        _joints.Add(new JointState { Joint = joint, Min = minAngle, Max = maxAngle });
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
        // ガチャのキャラは本物の顔(切り抜き画像)を頭にする。表情の差し替えはしない
        if (!string.IsNullOrEmpty(stats.CharaId))
        {
            var real = CharaCatalog.Head(stats.CharaId);
            if (real != null)
            {
                _realHead = true;
                _headSr.sprite = real;
                _headSr.flipX = false;
                float d = head.GetComponent<CircleCollider2D>().radius * 2f * 1.45f;
                Vector2 bs = real.bounds.size;
                _headSr.transform.localScale = new Vector3(d / bs.x, d / bs.y, 1f);
                return;
            }
        }
        // 表情の絵は打席の最初にまとめて作っておく(試合中のカクつき防止。2回目以降はキャッシュ)
        for (int e = 0; e < 7; e++) CharacterArt.Head(_species, _skin, _hatColor, _gear, (Expr)e, _variant);
        ApplyExpression(Expr.Normal);
    }

    private bool _realHead;

    private void ApplyExpression(Expr e)
    {
        if (_headSr == null) return;
        if (_realHead)
        {
            // 本物の顔は差し替えず、驚き・喜びは頭をぽんと弾ませる
            _expr = e;
            if (e == Expr.Surprise || e == Expr.Happy) _headPop = 0.25f;
            return;
        }
        _expr = e;
        _headSr.sprite = CharacterArt.Head(_species, _skin, _hatColor, _gear, e, _variant);
    }

    // 表情を一定時間だけ変える(時間が過ぎたら通常顔に戻る)
    // 表情が変わったとき(顔のアップ演出用)
    public static event System.Action<RagdollPlayer, Expr, float> ExpressionChanged;

    public void SetExpression(Expr e, float seconds)
    {
        if (ExpressionChanged != null) ExpressionChanged(this, e, seconds);
        ApplyExpression(e);
        _exprTimer = seconds;
    }

    private float _headPop;
    private Vector3 _headBaseScale;

    private void LateUpdate()
    {
        HeadPopScale = _headPop > 0f ? 1f + 0.18f * Mathf.Sin(Mathf.Clamp01(_headPop / 0.25f) * Mathf.PI) : 1f;
        if (!_realHead || _headSr == null) return;
        if (_headBaseScale == Vector3.zero) _headBaseScale = _headSr.transform.localScale;
        if (_headPop > 0f)
        {
            _headPop -= Time.deltaTime;
            float u = Mathf.Clamp01(_headPop / 0.25f);
            _headSr.transform.localScale = _headBaseScale * (1f + 0.18f * Mathf.Sin(u * Mathf.PI));
        }
        else _headSr.transform.localScale = _headBaseScale;
    }

    private void Update()
    {
        if (_exprTimer > 0f)
        {
            _exprTimer -= Time.deltaTime;
            if (_exprTimer <= 0f && _expr != Expr.Normal) ApplyExpression(Expr.Normal);
        }
    }

    // 縦じまのユニフォーム(胸・腰・太もも・上腕)。キャラ画像の白地×赤じまに合わせる。
    private void AddPinstripes()
    {
        StripeOn(Chest, new Vector2(0.44f, 0.40f));
        StripeOn(Pelvis, new Vector2(0.36f, 0.20f));
        StripeOn(LegBackUpper, new Vector2(0.13f, 0.38f));
        StripeOn(LegFrontUpper, new Vector2(0.13f, 0.38f));
        StripeOn(ArmFrontUpper, new Vector2(0.09f, 0.26f), 0.04f);
        StripeOn(ArmBackUpper, new Vector2(0.09f, 0.26f), 0.04f);
    }

    private void StripeOn(Rigidbody2D part, Vector2 size, float yOff = 0f)
    {
        if (part == null) return;
        var baseSr = part.GetComponentInChildren<SpriteRenderer>();
        var vis = new GameObject("Stripes");
        vis.transform.SetParent(part.transform, false);
        vis.transform.localPosition = new Vector3(0f, yOff, -0.002f);
        vis.layer = RagdollLayer;
        var sr = vis.AddComponent<SpriteRenderer>();
        sr.sprite = CharacterArt.Pinstripe;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = size;
        sr.sortingOrder = baseSr != null ? baseSr.sortingOrder : 5;
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
        if (_poseCard != null) _poseCard.OnReset();
        _poseHoldTimer = 0f;
        _flopTimer = 0f;
        _exprTimer = 0f;
        _holds.Clear();
        _walking = false;
        StandAssist = false;
        _pelvisHome = _pelvisHomeInit;
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
        TickWalk(Time.fixedDeltaTime);
        ApplyHolds();
        if (StandAssist && _flopTimer <= 0f) ApplyStand();
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
        if (_poseCard != null) _poseCard.OnPitch();
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
        if (_poseCard != null) _poseCard.OnSwing();
        _poseHoldTimer = 0.30f;
        if (_expr == Expr.Normal) SetExpression(Expr.Focus, 0.5f);
        float f = Facing;
        // バットは上向きに立っているので前へ倒れる向き、腕は下向きなので逆符号。
        float g = (0.6f + 0.4f * BattingConfig.GunyaLevel) * _stackSpin;
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
        BatThrown = true;
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

    // ------------------------------------------------------------
    // 登場演出: 歩いて入る / ポーズ
    // ------------------------------------------------------------
    private struct Hold { public Rigidbody2D Rb; public float Target; public float Until; public bool Wave; }
    private readonly List<Hold> _holds = new List<Hold>();
    private bool _walking;
    private float _walkDx, _walkDone, _walkT, _walkDur, _stepTimer;
    private int _stepSide;

    public bool IsWalking { get { return _walking; } }

    // 体ごと平行移動(物理の状態はそのまま)
    public void Teleport(Vector2 delta)
    {
        for (int i = 0; i < _allParts.Count; i++)
        {
            var rb = _allParts[i];
            rb.position += delta;
            rb.transform.position += new Vector3(delta.x, delta.y, 0f);
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        _pelvisHome += delta;
    }

    // 今いる位置から dx だけ歩く(足をばたつかせながら)
    public void WalkBy(float dx, float seconds)
    {
        _walking = true;
        _walkDx = dx;
        _walkDone = 0f;
        _walkT = 0f;
        _walkDur = Mathf.Max(0.1f, seconds);
        _stepTimer = 0f;
    }

    private void TickWalk(float dt)
    {
        if (!_walking) return;
        _walkT += dt;
        float u = Mathf.Clamp01(_walkT / _walkDur);
        float eased = u * u * (3f - 2f * u);
        float desired = _walkDx * eased;
        float step = desired - _walkDone;
        _walkDone = desired;
        var d = new Vector2(step, 0f);
        for (int i = 0; i < _allParts.Count; i++)
        {
            var rb = _allParts[i];
            rb.position += d;
        }
        _pelvisHome += d;
        // 足踏み(左右交互に脚を振る。ぐにゃ度で大げさに)
        _stepTimer -= dt;
        if (_stepTimer <= 0f && u < 0.95f)
        {
            _stepTimer = 0.20f;
            _stepSide = 1 - _stepSide;
            float g = 0.6f + 0.4f * BattingConfig.GunyaLevel;
            float dir = Mathf.Sign(_walkDx);
            var leg = _stepSide == 0 ? LegFrontUpper : LegBackUpper;
            if (leg != null) leg.angularVelocity = 260f * dir * g;
            if (Pelvis != null) Pelvis.velocity = new Vector2(Pelvis.velocity.x, 1.1f * g);
            if (ArmFrontUpper != null) ArmFrontUpper.angularVelocity = -180f * dir * g * (_stepSide == 0 ? 1f : -1f);
        }
        if (u >= 1f) _walking = false;
    }

    private void AddHold(Rigidbody2D rb, float target, float seconds, bool wave = false)
    {
        if (rb == null) return;
        _holds.Add(new Hold { Rb = rb, Target = target, Until = Time.time + seconds, Wave = wave });
    }

    private void ApplyHolds()
    {
        for (int i = _holds.Count - 1; i >= 0; i--)
        {
            var h = _holds[i];
            if (h.Rb == null || Time.time > h.Until) { _holds.RemoveAt(i); continue; }
            float target = h.Target;
            if (h.Wave) target += Mathf.Sin(Time.time * 12f) * 40f * Facing;
            float err = Mathf.DeltaAngle(h.Rb.rotation, target) * Mathf.Deg2Rad;
            float av = h.Rb.angularVelocity * Mathf.Deg2Rad;
            float alpha = 300f * err - 30f * av;
            h.Rb.AddTorque(h.Rb.inertia * alpha);
        }
    }

    private float _massTotal = -1f;

    private void ApplyStand()
    {
        KeepAngle(LegBackUpper, 0f, 200f, 25f);
        KeepAngle(LegFrontUpper, 0f, 200f, 25f);
        KeepAngle(LegBackFore, 0f, 200f, 25f);
        KeepAngle(LegFrontFore, 0f, 200f, 25f);
        if (_poseHoldTimer <= 0f) KeepAngle(Chest, 0f, 120f, 20f);
        if (Pelvis == null) return;
        if (_massTotal < 0f) { _massTotal = 0f; foreach (var rb in _allParts) _massTotal += rb.mass; }
        float dy = _pelvisHome.y - Pelvis.position.y;
        if (dy > 0f)
        {
            float a = Mathf.Clamp(60f * dy - 8f * Pelvis.velocity.y, 0f, 40f);
            Pelvis.AddForce(new Vector2(0f, _massTotal * a));
        }
    }

    private static void KeepAngle(Rigidbody2D rb, float target, float k, float d)
    {
        if (rb == null) return;
        float err = Mathf.DeltaAngle(rb.rotation, target) * Mathf.Deg2Rad;
        float av = rb.angularVelocity * Mathf.Deg2Rad;
        rb.AddTorque(rb.inertia * (k * err - d * av));
    }

    // 登場のポーズ。seconds はポーズを保つ時間。
    public void DoGesture(IntroPose pose, float seconds)
    {
        float f = Facing;
        float g = 0.6f + 0.4f * BattingConfig.GunyaLevel;
        switch (pose)
        {
            case IntroPose.BatPoint:
                AddHold(ArmFrontUpper, 150f * f, seconds);
                AddHold(ArmFrontFore, 155f * f, seconds);
                AddHold(Bat, -25f * f, seconds);
                break;
            case IntroPose.Flex:
                AddHold(ArmFrontUpper, 165f * f, seconds);
                AddHold(ArmBackUpper, 165f * f, seconds);
                AddHold(ArmFrontFore, 120f * f, seconds);
                AddHold(ArmBackFore, 120f * f, seconds);
                break;
            case IntroPose.Bow:
                _poseHoldTimer = seconds;
                AddHold(Chest, -38f * f, seconds);
                AddHold(Head, -48f * f, seconds);
                break;
            case IntroPose.Wave:
                AddHold(ArmFrontUpper, 150f * f, seconds);
                AddHold(ArmFrontFore, 130f * f, seconds, true);
                break;
            case IntroPose.Jump:
                for (int i = 0; i < _allParts.Count; i++)
                    _allParts[i].velocity = new Vector2(0f, 3.4f * g);
                AddHold(ArmFrontUpper, 170f * f, seconds);
                AddHold(ArmBackUpper, 170f * f, seconds);
                break;
            case IntroPose.Spin:
            {
                // 宙返り: 腰まわりに剛体回転を与えて飛ばす(着地はぐにゃ任せ)
                _flopTimer = Mathf.Max(_flopTimer, seconds);
                float w = -7.0f * f * g;   // rad/s(進行方向へ前転)
                Vector2 c = Pelvis != null ? Pelvis.position : (Vector2)transform.position;
                for (int i = 0; i < _allParts.Count; i++)
                {
                    var rb = _allParts[i];
                    Vector2 r = rb.position - c;
                    rb.velocity = new Vector2(0f, 4.4f * g) + w * new Vector2(-r.y, r.x);
                    rb.angularVelocity = w * Mathf.Rad2Deg;
                }
                break;
            }
        }
    }
}
