using System.Collections.Generic;
using UnityEngine;

// 元絵(切り抜き)をそのまま体にする。
// 画像を細かい網目(メッシュ)にして、各点を近くの骨(腕・脚・胴・頭・バット)に重み付けし、
// ラグドールの各パーツの「最初の姿勢からの回転」を元絵の関節まわりに当てて毎フレーム変形する。
// → 立ち姿は元絵のまま、振る・投げる・転ぶ・歩くはラグドールの動きに追従する。
// 動かす骨の出どころ。ラグドール(物理)でも、コードで角度を付ける人形(守備・走者)でもよい。
// 位置と角度は Space(この中に網目を置く)から見た2Dの値として使う。
public class RigBody
{
    public Transform Space;
    public Transform Pelvis, Chest, Head, ArmFU, ArmFF, ArmBU, ArmBF, LegFU, LegFF, LegBU, LegBF, Bat;
    public SpriteRenderer BatSprite;
    public float Facing = 1f;
    public System.Func<float> HeadPop = () => 1f;
    public System.Func<bool> BatThrown = () => false;
    public System.Func<Expr> Expression = () => Expr.Normal;
    public int Layer;
    public int Order = 6;
    public bool Light;            // 遠くの選手用: 網目を粗く、顔の変形・記号なし

    public static RigBody From(RagdollPlayer p)
    {
        return new RigBody
        {
            Space = p.transform, Pelvis = p.Pelvis.transform, Chest = p.Chest.transform, Head = p.Head.transform,
            ArmFU = p.ArmFrontUpper.transform, ArmFF = p.ArmFrontFore.transform, ArmBU = p.ArmBackUpper.transform, ArmBF = p.ArmBackFore.transform,
            LegFU = p.LegFrontUpper.transform, LegFF = p.LegFrontFore.transform, LegBU = p.LegBackUpper.transform, LegBF = p.LegBackFore.transform,
            Bat = p.Bat != null ? p.Bat.transform : null,
            BatSprite = p.Bat != null ? p.Bat.GetComponentInChildren<SpriteRenderer>() : null,
            Facing = p.Facing, HeadPop = () => p.HeadPopScale, BatThrown = () => p.BatThrown, Expression = () => p.CurrentExpr,
            Layer = RagdollPlayer.RagdollLayer, Order = 6
        };
    }
}

public partial class RigSkin : MonoBehaviour
{
    private class Bone
    {
        public Transform Tr;
        public int Parent = -1;
        public Vector2 Pivot;          // 最初の姿勢でのワールド座標(関節)
        public float Rest;             // パーツの最初の角度
        public bool Torso;             // 腰・胸・頭(Aポーズの絵では傾きを控えめにする)
        public float Offset;           // Aポーズの絵: 絵の腕脚の向きをラグドールの最初の向き(真下)へそろえる角度
        public int Gate;               // Aポーズの絵: 1=腕(肩より外側だけ) 2=脚(股より下だけ)。胴の画素が腕脚に引っぱられて裂けないように
        public float GateAt;           // 腕=肩のx / 脚=股のy(画素)
        public int Code;               // 部品の番号(0=胴 1=画像の左腕 2=右腕 3=左脚 4=右脚)。BodyLabel の値と同じ
        public readonly List<Vector2> Seg = new List<Vector2>();   // 画素座標の線分(2点ずつ)
        public int Layer;
        public bool IsBat;
        // 毎フレーム
        public float Ang;              // 回転(度)
        public Vector2 Cur;            // 関節の今の位置
        public float Scale = 1f;
        public Vector2 Sq = Vector2.one;   // 表情での伸び縮み(頭)
        public float Extra;                // 表情での追加の傾き(頭)
        public Vector2 AxisX = Vector2.right, AxisY = Vector2.up;
    }

    private RigBody _p;
    private CharaRig _rig;
    private readonly List<Bone> _bones = new List<Bone>();
    private Mesh _mesh;
    private MeshRenderer _renderer;
    private Material _material;
    private Vector2[] _rest;           // 最初の姿勢でのワールド座標
    private Vector3[] _verts;
    private int[] _bi;                 // 1頂点あたり3本の骨
    private float[] _bw;
    private Vector2 _pelRest;
    private float _batRel;             // バットを投げた後の前腕に対する角度
    private bool _batFree;
    private int _batBone = -1, _batParent = -1;
    public bool HasPaintedBat { get { return _batBone >= 0 || _paintedBat; } }
    private bool _paintedBat;

    private int _headBone = 2;
    private int _farmF = -1, _farmB = -1, _chestBone = -1;
    private float _breathPhase;
    private Vector2 _wristF, _wristB;
    private string _charaId;
    private Transform _batVis;
    private Vector3 _batVisLocal;
    private Vector2 _batOffFrozen;
    private bool _batOffFrozenSet;
    private int[] _allTris;
    private bool[] _seam, _hidden;
    private float[] _restEdge;
    private readonly List<int> _triBuf = new List<int>();
    private const float StretchHide = 2.2f;   // つなぎ目がこの倍率より伸びたら隠す
    private int Cell = 8;              // 網目の細かさ(画素)
    public const float TargetHeight = 2.45f;

    public static RigSkin TryAttach(RagdollPlayer p, string charaId) { return TryAttach(RigBody.From(p), charaId); }

    // Aポーズの立ち絵(Resources/Chara/Body + rig_body.txt)があれば、決めポーズの元絵(Cut)より優先して体にする。
    // 腕と脚が胴から離れて描かれているので、大きく振っても絵が伸びにくい。false で全員を元絵の体に戻す。
    public static bool UseBodyArt = true;
    public bool UsesBodyArt { get; private set; }
    // 試作(決めポーズの絵の差し替え)用: 元絵1画素の大きさ・足元の位置(Space の中)・Aポーズの絵の足の下端(画素)
    public float PixelWorld { get { return _k; } }
    public Vector2 FootLocal { get { return new Vector2(_rootX, _groundY); } }
    public float FootPixel { get { return _footY; } }

    private static bool BodyOk(string charaId)
    {
        if (!UseBodyArt) return false;
        var rig = CharaRig.GetBody(charaId);
        return rig != null && rig.Has("pelvis") && rig.Has("neck") && CharaCatalog.Body(charaId) != null;
    }

    public static bool CanRig(string charaId)
    {
        if (BodyOk(charaId)) return true;
        var rig = CharaRig.Get(charaId);
        return rig != null && rig.Has("pelvis") && rig.Has("neck") && CharaCatalog.Cut(charaId) != null;
    }

    public static RigSkin TryAttach(RigBody body, string charaId)
    {
        if (!CanRig(charaId)) return null;
        bool useBody = BodyOk(charaId);
        var rig = useBody ? CharaRig.GetBody(charaId) : CharaRig.Get(charaId);
        var spr = useBody ? CharaCatalog.Body(charaId) : CharaCatalog.Cut(charaId);
        var go = new GameObject("RigSkin");
        go.transform.SetParent(body.Space, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.layer = body.Layer;
        var s = go.AddComponent<RigSkin>();
        s.UsesBodyArt = useBody;
        s._breathPhase = Random.value * 6.28f;
        s._charaId = charaId;
        if (body.Light) s.Cell = 16;
        s.Build(body, rig, spr);
        return s;
    }

    // Space から見た位置・角度
    private Vector2 Loc(Transform t) { return _p.Space.InverseTransformPoint(t.position); }
    private float AngOf(Transform t) { return (Quaternion.Inverse(_p.Space.rotation) * t.rotation).eulerAngles.z; }

    private Vector2 J(string n) { return _rig.J[n]; }
    private bool Has(string n) { return _rig.J.ContainsKey(n); }

    private int AddBone(Transform tr, int parent, Vector2 pivotPx, int layer)
    {
        var b = new Bone { Tr = tr, Parent = parent, Layer = layer };
        b.Pivot = ToWorldRest(pivotPx);
        b.Rest = tr != null ? AngOf(tr) : 0f;
        _bones.Add(b);
        return _bones.Count - 1;
    }

    private void Seg(int bone, Vector2 a, Vector2 b) { _bones[bone].Seg.Add(a); _bones[bone].Seg.Add(b); }

    // Aポーズの絵: 絵の骨(from→to)が、ラグドールの最初の姿勢の向き(パーツの下向き)と重なるように角度をそろえる。
    // ラグドールが腕を下ろしていれば絵の腕も下り、バットを構えれば絵の腕も同じ向きに上がる。
    private void GateArm(int bone, float shoulderX) { if (_rig.APose && bone >= 0) { _bones[bone].Gate = 1; _bones[bone].GateAt = shoulderX; } }
    private void GateLeg(int bone, float hipY) { if (_rig.APose && bone >= 0) { _bones[bone].Gate = 2; _bones[bone].GateAt = hipY; } }

    // 腕は肩より外側、脚は股より下の画素だけに付ける(境目は ±GateBand 画素でなめらかに)
    private const float GateBand = 7f;
    private float GateWeight(Bone b, Vector2 px)
    {
        float d;
        if (b.Gate == 1)
        {
            float side = Mathf.Sign(b.GateAt - _cx);
            d = (px.x - _cx) * side - Mathf.Abs(b.GateAt - _cx);
        }
        else if (b.Gate == 2) d = px.y - b.GateAt;
        else return 1f;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-GateBand, GateBand, d));
    }

    // keep: Aポーズの開きを何割残すか(腕は少し体から離し、脚は軽く開いて立つ。ぴったり閉じると棒立ちに見える)
    private const float KeepArm = 0.35f, KeepLeg = 0.5f;
    // 胴・頭の傾きはラグドールの何割に従うか(正面の絵を大きく傾けると倒れて見える)。大きく倒れたときは全部従う
    private const float TorsoFollow = 0.55f;

    private void Aim(int bone, Vector2 fromPx, Vector2 toPx) { Aim(bone, fromPx, toPx, 0f); }
    private void Aim(int bone, Vector2 fromPx, Vector2 toPx, float keep)
    {
        if (!_rig.APose || bone < 0) return;
        var b = _bones[bone];
        if (b.Tr == null) return;
        Vector2 d = ToWorldRest(toPx) - ToWorldRest(fromPx);
        if (d.sqrMagnitude < 1e-8f) return;
        float img = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        b.Offset = Mathf.DeltaAngle(img, b.Rest - 90f) * (1f - keep);
    }

    // 画素 → 最初の姿勢でのワールド座標
    private float _k, _m, _cx, _footY, _rootX, _groundY;
    private Vector2 ToWorldRest(Vector2 px)
    {
        return new Vector2(_rootX + _m * (px.x - _cx) * _k, _groundY + (_footY - px.y) * _k);
    }

    private void Build(RigBody p, CharaRig rig, Sprite spr)
    {
        _p = p; _rig = rig;
        float f = p.Facing;
        // 前に出す腕が進行方向側に来るよう、必要なら左右反転
        _m = rig.FrontLeft ? -f : f;
        string F = (_m * 1f == f) ? "R" : "L";      // 進行方向側(=前)の画像の左右
        string B = F == "R" ? "L" : "R";
        string armF = rig.FrontLeft ? "L" : "R", armB = armF == "R" ? "L" : "R";

        float h = Mathf.Max(64f, rig.Box.w - rig.Box.y);
        _k = TargetHeight * rig.Scale / h;
        _cx = J("pelvis").x;
        _footY = rig.Box.w;
        _rootX = Loc(p.Pelvis).x;
        _groundY = -0.02f;
        _pelRest = Loc(p.Pelvis);

        Vector2 pel = J("pelvis"), neck = J("neck");
        Vector2 waist = Vector2.Lerp(pel, neck, 0.25f);

        int iPel = AddBone(p.Pelvis, -1, pel, 2);
        _bones[iPel].Torso = true;
        if (Has("hipL") && Has("hipR")) Seg(iPel, J("hipL"), J("hipR"));
        Seg(iPel, pel, waist);
        int iChest = AddBone(p.Chest, iPel, waist, 3);
        _bones[iChest].Torso = true;
        _chestBone = iChest;
        Seg(iChest, waist, neck);
        if (Has("shoulderL") && Has("shoulderR")) Seg(iChest, J("shoulderL"), J("shoulderR"));
        if (Has("chest")) Seg(iChest, J("chest"), neck);
        int iHead = AddBone(p.Head, iChest, neck, 7);
        _bones[iHead].Torso = true;
        _headBone = iHead;
        Vector2 head = Has("head") ? J("head") : neck + (neck - pel) * 0.5f;
        Seg(iHead, Vector2.Lerp(neck, head, 0.15f), head);

        int farmF = Arm(armF, p.ArmFU, p.ArmFF, iChest, 6);
        _farmF = farmF;
        if (farmF >= 0 && Has("wrist" + armF)) _wristF = ToWorldRest(J("wrist" + armF));
        int farmB = Arm(armB, p.ArmBU, p.ArmBF, iChest, 1);
        _farmB = farmB;
        if (farmB >= 0 && Has("wrist" + armB)) _wristB = ToWorldRest(J("wrist" + armB));
        if (!rig.NoLegs)   // 脚がはっきりない絵(雪だるま・車輪のロボ)は下半身を胴と一緒に動かす
        {
            Leg(F, p.LegFU, p.LegFF, iPel, 5);
            Leg(B, p.LegBU, p.LegBF, iPel, 0);
        }

        // バット(元絵に描かれた持ち物)
        if (Has("batGrip") && Has("batTip"))
        {
            _paintedBat = true;
            Vector2 g = J("batGrip");
            int holder = farmF;
            if (farmB >= 0 && Has("wrist" + armB) && (!Has("wrist" + armF) ||
                (J("wrist" + armB) - g).sqrMagnitude < (J("wrist" + armF) - g).sqrMagnitude)) holder = farmB;
            if (holder < 0) holder = iChest;
            if (p.Bat != null && holder == farmF)
            {
                _batBone = AddBone(p.Bat, holder, g, 8);
                _bones[_batBone].IsBat = true;
                _batParent = holder;
                Seg(_batBone, g, J("batTip"));
                if (Has("batTail")) Seg(_batBone, g, J("batTail"));
            }
            else
            {
                Seg(holder, g, J("batTip"));
                if (Has("batTail")) Seg(holder, g, J("batTail"));
            }
        }

        BuildMesh(spr);
        if (!p.Light) BuildFace(spr);
    }

    private int Arm(string s, Transform up, Transform fore, int parent, int layer)
    {
        if (!Has("shoulder" + s) || !Has("elbow" + s) || up == null) return -1;
        Vector2 sh = J("shoulder" + s), el = J("elbow" + s);
        int u = AddBone(up, parent, sh, layer);
        Aim(u, sh, el, KeepArm);
        GateArm(u, sh.x); _bones[u].Code = s == "L" ? 1 : 2;
        if (!Has("wrist" + s) || fore == null) { Seg(u, sh, el + (el - sh) * 0.6f); return -1; }
        Vector2 wr = J("wrist" + s);
        Seg(u, Vector2.Lerp(sh, el, 0.1f), el);
        int fo = AddBone(fore, u, el, layer);
        Aim(fo, el, wr, KeepArm);
        GateArm(fo, sh.x); _bones[fo].Code = s == "L" ? 1 : 2;
        Seg(fo, el, wr + (wr - el) * 0.35f);
        return fo;
    }

    private void Leg(string s, Transform up, Transform fore, int parent, int layer)
    {
        if (!Has("hip" + s) || !Has("knee" + s) || up == null) return;
        Vector2 hp = J("hip" + s), kn = J("knee" + s);
        int u = AddBone(up, parent, hp, layer);
        Aim(u, hp, kn, KeepLeg);
        GateLeg(u, hp.y); _bones[u].Code = s == "L" ? 3 : 4;
        if (!Has("ankle" + s) || fore == null) { Seg(u, hp, kn + (kn - hp) * 0.6f); return; }
        Vector2 an = J("ankle" + s);
        Seg(u, Vector2.Lerp(hp, kn, 0.1f), kn);
        int fo = AddBone(fore, u, kn, layer);
        Aim(fo, kn, an, KeepLeg);
        GateLeg(fo, hp.y); _bones[fo].Code = s == "L" ? 3 : 4;
        Seg(fo, kn, an + (an - kn) * 0.25f);
    }

    private float _wScale;

    // 配列はキャッシュ側では不変。姿勢・頂点・非表示状態は各選手が持つ。
    private sealed class MeshData
    {
        public Vector2[] Pixels, UV, UV2;     // UV2.x = 部品の番号(Aポーズの体のみ)
        public Texture2D Labels;               // 画素ごとの部品の番号(Aポーズの体のみ)
        public int[] Bones, Triangles;
        public float[] Weights, PixelEdges;
        public bool[] Seams;
    }
    private struct MeshKey : System.IEquatable<MeshKey>
    {
        public string Id;
        public int Cell, Topology;
        public bool Equals(MeshKey other) { return Id == other.Id && Cell == other.Cell && Topology == other.Topology; }
        public override bool Equals(object obj) { return obj is MeshKey && Equals((MeshKey)obj); }
        public override int GetHashCode() { unchecked { return (Id.GetHashCode() * 397 ^ Cell) * 397 ^ Topology; } }
    }
    private static readonly Dictionary<MeshKey, MeshData> MeshCache = new Dictionary<MeshKey, MeshData>();
    // 網目の重みは使い回すが、上限を超えたら一番長く使っていないものから捨てる(使用中の選手は自分の配列を持っているので安全)
    public static int MaxMeshCache = 40, MaxFaceCache = 24;
    private static readonly Dictionary<MeshKey, float> MeshUse = new Dictionary<MeshKey, float>();
    private static readonly Dictionary<MeshKey, float> FaceUse = new Dictionary<MeshKey, float>();
    public static int CachedMeshes { get { return MeshCache.Count + FaceCache.Count; } }

    private static void Remember(Dictionary<MeshKey, MeshData> cache, Dictionary<MeshKey, float> use, MeshKey key, int max)
    {
        use[key] = Time.realtimeSinceStartup;
        while (cache.Count > max)
        {
            MeshKey oldest = default(MeshKey); float t = float.MaxValue; bool found = false;
            foreach (var kv in use)
                if (kv.Value < t && !kv.Key.Equals(key) && cache.ContainsKey(kv.Key)) { t = kv.Value; oldest = kv.Key; found = true; }
            if (!found) break;
            cache.Remove(oldest); use.Remove(oldest);
        }
    }

    private MeshKey CacheKey(int cell)
    {
        // 腕・脚が省略された人形や、独立したバットのない守備用を混ぜない。
        int topology = 0;
        if (_p.ArmFU != null) topology |= 1;
        if (_p.ArmFF != null) topology |= 2;
        if (_p.ArmBU != null) topology |= 4;
        if (_p.ArmBF != null) topology |= 8;
        if (_p.LegFU != null) topology |= 16;
        if (_p.LegFF != null) topology |= 32;
        if (_p.LegBU != null) topology |= 64;
        if (_p.LegBF != null) topology |= 128;
        if (_batBone >= 0) topology |= 256;
        return new MeshKey { Id = _rig.Id, Cell = cell, Topology = topology };
    }

    // 画素 px の骨の重み(上位3本)を bi/bw の v 番目に書く
    // Aポーズの絵: 胴(腰・胸・頭)と、腕2本・脚2本を別々の部品にする。
    // 網目のマスごとにどの部品か決め、部品の境目では頂点を分ける(つながっていない)。
    // 腕を真上に上げても胴の絵が引き伸ばされたり裂けたりせず、切り絵の人形のように関節で回る。
    // 部品の中(肘・膝)はこれまでどおりなめらかに曲がる。重なりは骨の前後順(前の腕は胴の上、後ろの腕は胴の下)。
    private int GroupOf(int b) { return _bones[b].Code; }

    // 部品の塗り分け(Tools/chara-rig/labels.py が作る Resources/Chara/BodyLabel/<id>.png)。なければ実行時に骨からおおまかに作る
    private static readonly Dictionary<string, Texture2D> LabelTex = new Dictionary<string, Texture2D>();
    private static Texture2D LoadLabel(string id)
    {
        Texture2D t;
        if (LabelTex.TryGetValue(id, out t) && t != null) return t;
        t = Resources.Load<Texture2D>("Chara/BodyLabel/" + id);
        if (t != null && !t.isReadable) t = null;
        LabelTex[id] = t;
        return t;
    }

    private const int LabelStep = 2;          // 部品の色分けは元絵の半分の細かさ(2画素ごと)
    private static float GroupCode(int g) { return g * 32f / 255f; }

    private MeshData BuildPartsMeshData(Sprite spr)
    {
        var box = _rig.Box;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(box.x / Cell) * Cell - Cell);
        int y0 = Mathf.Max(0, Mathf.FloorToInt(box.y / Cell) * Cell - Cell);
        int x1 = Mathf.Min((int)_rig.Size.x, Mathf.CeilToInt(box.z / Cell) * Cell + Cell);
        int y1 = Mathf.Min((int)_rig.Size.y, Mathf.CeilToInt(box.w / Cell) * Cell + Cell);
        int nx = (x1 - x0) / Cell + 1, ny = (y1 - y0) / Cell + 1;
        int nb = _bones.Count;
        var tex = spr.texture;
        Rect tr = spr.textureRect;
        var w = new float[nb];
        var gsum = new float[8];

        // 1) 画素ごとの部品(半分の細かさ)。用意された塗り分けがあればそれを使う
        int lw = Mathf.CeilToInt(_rig.Size.x / LabelStep), lh = Mathf.CeilToInt(_rig.Size.y / LabelStep);
        var label = new byte[lw * lh];
        Texture2D lt = LoadLabel(_charaId);
        if (lt != null && lt.width == lw && lt.height == lh)
        {
            var pc = lt.GetPixels32();
            for (int ly = 0; ly < lh; ly++)
                for (int lx = 0; lx < lw; lx++)
                    label[ly * lw + lx] = (byte)Mathf.Clamp((pc[(lh - 1 - ly) * lw + lx].a + 16) / 32, 0, 7);
        }
        else
        {
            int lx0 = x0 / LabelStep, ly0 = y0 / LabelStep, lx1 = Mathf.Min(lw, x1 / LabelStep + 1), ly1 = Mathf.Min(lh, y1 / LabelStep + 1);
            for (int ly = ly0; ly < ly1; ly++)
                for (int lx = lx0; lx < lx1; lx++)
                {
                    var c = new Vector2((lx + 0.5f) * LabelStep, (ly + 0.5f) * LabelStep);
                    RawWeights(c, _wScale, w, true);
                    System.Array.Clear(gsum, 0, gsum.Length);
                    for (int b = 0; b < nb; b++) gsum[GroupOf(b)] += w[b];
                    int g = 0;
                    for (int k = 1; k < gsum.Length; k++) if (gsum[k] > gsum[g]) g = k;
                    label[ly * lw + lx] = (byte)g;
                }
            // 部品の番号の絵(アルファ8bit、上下は画像と逆)
            lt = new Texture2D(lw, lh, TextureFormat.Alpha8, false);
            lt.filterMode = FilterMode.Point;
            lt.wrapMode = TextureWrapMode.Clamp;
            var lpx = new byte[lw * lh];
            for (int ly = 0; ly < lh; ly++)
                for (int lx = 0; lx < lw; lx++) lpx[(lh - 1 - ly) * lw + lx] = (byte)(label[ly * lw + lx] * 32);
            lt.LoadRawTextureData(lpx);
            lt.Apply(false, true);
            lt.hideFlags = HideFlags.DontSave;
        }

        // 2) マスごとに、含まれる部品の数だけ四角を重ねる(部品の境目では頂点を分ける)
        var pixels = new List<Vector2>(nx * ny * 2);
        var uvs = new List<Vector2>(nx * ny * 2);
        var uv2 = new List<Vector2>(nx * ny * 2);
        var bones = new List<int>(nx * ny * 6);
        var weights = new List<float>(nx * ny * 6);
        var map = new Dictionary<long, int>(nx * ny * 2);
        var buckets = new List<int>[9];
        for (int l = 0; l < 9; l++) buckets[l] = new List<int>();
        var corner = new int[4];
        int span = Cell / LabelStep;
        for (int j = 0; j + 1 < ny; j++)
            for (int i = 0; i + 1 < nx; i++)
            {
                int mask = 0;
                int cx0 = (x0 + i * Cell) / LabelStep - 1, cy0 = (y0 + j * Cell) / LabelStep - 1;
                for (int ly = Mathf.Max(0, cy0); ly <= Mathf.Min(lh - 1, cy0 + span + 1); ly++)
                    for (int lx = Mathf.Max(0, cx0); lx <= Mathf.Min(lw - 1, cx0 + span + 1); lx++)
                        mask |= 1 << label[ly * lw + lx];
                for (int g = 0; g < 8; g++)
                {
                    if ((mask & (1 << g)) == 0) continue;
                    int layer = 0;
                    for (int q = 0; q < 4; q++)
                    {
                        int gi = (j + (q >> 1)) * nx + i + (q & 1);
                        long key = (long)gi * 16 + g;
                        int idx;
                        if (!map.TryGetValue(key, out idx))
                        {
                            idx = pixels.Count;
                            var px = new Vector2(x0 + (i + (q & 1)) * Cell, y0 + (j + (q >> 1)) * Cell);
                            pixels.Add(px);
                            uvs.Add(new Vector2((tr.x + px.x / _rig.Size.x * tr.width) / tex.width,
                                                (tr.y + (1f - px.y / _rig.Size.y) * tr.height) / tex.height));
                            uv2.Add(new Vector2(GroupCode(g), 0f));
                            RawWeights(px, _wScale, w, false);   // 部品の中の骨だけで重みを付ける
                            if (!PickTop3(w, g, bones, weights)) PickNearest(px, g, bones, weights);
                            map[key] = idx;
                        }
                        corner[q] = idx;
                        layer = Mathf.Max(layer, _bones[bones[idx * 3]].Layer);
                    }
                    var bucket = buckets[Mathf.Clamp(layer, 0, 8)];
                    bucket.Add(corner[0]); bucket.Add(corner[2]); bucket.Add(corner[1]);
                    bucket.Add(corner[1]); bucket.Add(corner[2]); bucket.Add(corner[3]);
                }
            }
        var data = new MeshData { Pixels = pixels.ToArray(), UV = uvs.ToArray(), UV2 = uv2.ToArray(), Bones = bones.ToArray(), Weights = weights.ToArray(), Labels = lt };
        var tris = new List<int>((nx - 1) * (ny - 1) * 8);
        foreach (var bucket in buckets) tris.AddRange(bucket);
        data.Triangles = tris.ToArray();
        int nt = data.Triangles.Length / 3;
        data.Seams = new bool[nt]; data.PixelEdges = new float[nt];
        for (int t = 0; t < nt; t++)
        {
            int a = data.Triangles[t * 3], b = data.Triangles[t * 3 + 1], cc = data.Triangles[t * 3 + 2];
            int ba = data.Bones[a * 3], bb = data.Bones[b * 3], bc = data.Bones[cc * 3];
            data.Seams[t] = !(ba == bb && bb == bc);
            data.PixelEdges[t] = Mathf.Max((data.Pixels[a] - data.Pixels[b]).sqrMagnitude,
                Mathf.Max((data.Pixels[b] - data.Pixels[cc]).sqrMagnitude, (data.Pixels[cc] - data.Pixels[a]).sqrMagnitude));
        }
        return data;
    }

    // 骨ごとの生の重み(gated=true なら腕は肩の外・脚は股の下だけ)
    private void RawWeights(Vector2 px, float scale, float[] w, bool gated)
    {
        int nb = _bones.Count;
        for (int b = 0; b < nb; b++)
        {
            float d = 1e9f;
            var sg = _bones[b].Seg;
            for (int s = 0; s + 1 < sg.Count; s += 2) d = Mathf.Min(d, SegDistSquared(px, sg[s], sg[s + 1]));
            float d2 = Mathf.Max(d, 9f) / (scale * scale);
            w[b] = sg.Count == 0 ? 0f : 1f / (d2 * d2 * d2);
            if (gated && _bones[b].Gate != 0) w[b] *= GateWeight(_bones[b], px);
        }
    }

    // 部品 g に骨がない(腕が推定できなかった等)ときは胴の一番近い骨に付ける
    private void PickNearest(Vector2 px, int g, List<int> bones, List<float> weights)
    {
        int best = 0; float bd = float.MaxValue;
        for (int b = 0; b < _bones.Count; b++)
        {
            var sg = _bones[b].Seg;
            for (int k = 0; k + 1 < sg.Count; k += 2)
            {
                float d = SegDistSquared(px, sg[k], sg[k + 1]);
                if (d < bd) { bd = d; best = b; }
            }
        }
        bones.Add(best); bones.Add(best); bones.Add(best);
        weights.Add(1f); weights.Add(0f); weights.Add(0f);
    }

    // 部品 g の骨だけから重みの大きい3本を選んで足す。部品の骨に重みがなければ false
    private bool PickTop3(float[] w, int g, List<int> bones, List<float> weights)
    {
        int nb = _bones.Count;
        float total = 0f;
        for (int b = 0; b < nb; b++) { if (GroupOf(b) != g) w[b] = -1f; else total += w[b]; }
        if (total <= 0f) return false;
        int start = bones.Count;
        float sum = 0f;
        for (int k = 0; k < 3; k++)
        {
            int best = -1; float bv = 0f;
            for (int b = 0; b < nb; b++) if (w[b] > bv) { bv = w[b]; best = b; }
            if (best < 0) { bones.Add(bones[start]); weights.Add(0f); continue; }
            bones.Add(best); weights.Add(bv); sum += bv; w[best] = -1f;
        }
        for (int k = 0; k < 3; k++) weights[start + k] = weights[start + k] / sum;
        return true;
    }

    private void Weights(Vector2 px, float scale, float[] w, int[] bi, float[] bw, int v)
    {
        int nb = _bones.Count;
        for (int b = 0; b < nb; b++)
        {
            float d = 1e9f;
            var sg = _bones[b].Seg;
            for (int s = 0; s + 1 < sg.Count; s += 2) d = Mathf.Min(d, SegDistSquared(px, sg[s], sg[s + 1]));
            float d2 = Mathf.Max(d, 9f) / (scale * scale);
            w[b] = sg.Count == 0 ? 0f : 1f / (d2 * d2 * d2);   // 6乗: 骨の近くはほぼその骨だけに付く
            if (_bones[b].Gate != 0) w[b] *= GateWeight(_bones[b], px);
        }
        for (int k = 0; k < 3; k++)
        {
            int best = -1; float bv = -1f;
            for (int b = 0; b < nb; b++) if (w[b] > bv) { bv = w[b]; best = b; }
            bi[v * 3 + k] = Mathf.Max(0, best); bw[v * 3 + k] = Mathf.Max(0f, bv);
            if (best >= 0) w[best] = -1f;
        }
        float sum = bw[v * 3] + bw[v * 3 + 1] + bw[v * 3 + 2];
        for (int k = 0; k < 3; k++) bw[v * 3 + k] = sum > 0f ? bw[v * 3 + k] / sum : (k == 0 ? 1f : 0f);
    }

    private static float SegDistSquared(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).sqrMagnitude;
    }

    private void BuildMesh(Sprite spr)
    {
        _wScale = 0.5f * _rig.Size.y / Mathf.Max(64f, _rig.Box.w - _rig.Box.y);
        var key = CacheKey(Cell);
        MeshData data;
        if (!MeshCache.TryGetValue(key, out data))
        {
            data = _rig.APose ? BuildPartsMeshData(spr) : BuildMeshData(spr);
            MeshCache.Add(key, data);
        }
        Remember(MeshCache, MeshUse, key, MaxMeshCache);
        int n = data.Pixels.Length;
        _rest = new Vector2[n];
        _verts = new Vector3[n];
        for (int i = 0; i < n; i++) _rest[i] = ToWorldRest(data.Pixels[i]);
        _bi = data.Bones; _bw = data.Weights; _allTris = data.Triangles; _seam = data.Seams;
        _hidden = new bool[_seam.Length];
        _restEdge = new float[_seam.Length];
        for (int i = 0; i < _restEdge.Length; i++) _restEdge[i] = data.PixelEdges[i] * _k * _k;
        // 最初の張り替えでも List の拡張・GC を起こさない。
        _triBuf.Capacity = _allTris.Length;
        _mesh = new Mesh { name = "RigSkin" };
        _mesh.MarkDynamic();
        Pose();
        _mesh.vertices = _verts;
        _mesh.uv = data.UV;
        if (data.UV2 != null) _mesh.uv2 = data.UV2;
        _mesh.SetTriangles(_allTris, 0);
        _mesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
        _renderer = gameObject.AddComponent<MeshRenderer>();
        var partsShader = data.Labels != null ? Shader.Find("Gunya/RigParts") : null;
        _material = new Material(partsShader != null ? partsShader : Shader.Find("Sprites/Default"));
        _material.mainTexture = spr.texture;
        if (partsShader != null) _material.SetTexture("_LabelTex", data.Labels);
        _renderer.sharedMaterial = _material;
        _renderer.sortingOrder = _p.Order;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
    }

    private MeshData BuildMeshData(Sprite spr)
    {
        var box = _rig.Box;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(box.x / Cell) * Cell - Cell);
        int y0 = Mathf.Max(0, Mathf.FloorToInt(box.y / Cell) * Cell - Cell);
        int x1 = Mathf.Min((int)_rig.Size.x, Mathf.CeilToInt(box.z / Cell) * Cell + Cell);
        int y1 = Mathf.Min((int)_rig.Size.y, Mathf.CeilToInt(box.w / Cell) * Cell + Cell);
        int nx = (x1 - x0) / Cell + 1, ny = (y1 - y0) / Cell + 1;
        int n = nx * ny;
        var data = new MeshData { Pixels = new Vector2[n], UV = new Vector2[n], Bones = new int[n * 3], Weights = new float[n * 3] };
        var tex = spr.texture;
        Rect tr = spr.textureRect;
        var w = new float[_bones.Count];
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                int v = j * nx + i;
                var px = new Vector2(x0 + i * Cell, y0 + j * Cell);
                data.Pixels[v] = px;
                data.UV[v] = new Vector2((tr.x + px.x / _rig.Size.x * tr.width) / tex.width,
                                    (tr.y + (1f - px.y / _rig.Size.y) * tr.height) / tex.height);
                Weights(px, _wScale, w, data.Bones, data.Weights, v);
            }
        // 三角形を骨の前後順に並べる。セルごとの一時配列を作らない。
        var buckets = new List<int>[9];
        int triangleIndices = (nx - 1) * (ny - 1) * 6;
        for (int l = 0; l < 9; l++) buckets[l] = new List<int>();
        for (int j = 0; j + 1 < ny; j++)
            for (int i = 0; i + 1 < nx; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                int layer = Mathf.Max(_bones[data.Bones[a * 3]].Layer, _bones[data.Bones[d * 3]].Layer);
                var bucket = buckets[Mathf.Clamp(layer, 0, 8)];
                bucket.Add(a); bucket.Add(c); bucket.Add(b); bucket.Add(b); bucket.Add(c); bucket.Add(d);
            }
        var tris = new List<int>(triangleIndices);
        foreach (var bucket in buckets) tris.AddRange(bucket);
        data.Triangles = tris.ToArray();
        int nt = data.Triangles.Length / 3;
        data.Seams = new bool[nt]; data.PixelEdges = new float[nt];
        for (int t = 0; t < nt; t++)
        {
            int a = data.Triangles[t * 3], b = data.Triangles[t * 3 + 1], c = data.Triangles[t * 3 + 2];
            int ba = data.Bones[a * 3], bb = data.Bones[b * 3], bc = data.Bones[c * 3];
            data.Seams[t] = !(ba == bb && bb == bc);
            data.PixelEdges[t] = Mathf.Max((data.Pixels[a] - data.Pixels[b]).sqrMagnitude,
                Mathf.Max((data.Pixels[b] - data.Pixels[c]).sqrMagnitude, (data.Pixels[c] - data.Pixels[a]).sqrMagnitude));
        }
        return data;
    }

    private void UpdateSeams()
    {
        bool changed = false;
        float lim = StretchHide * StretchHide;
        const float s2 = 1f;   // 頂点も最初の形も同じ座標(Space)なので倍率なし
        for (int t = 0; t < _seam.Length; t++)
        {
            if (!_seam[t]) continue;
            int a = _allTris[t * 3], b = _allTris[t * 3 + 1], c = _allTris[t * 3 + 2];
            float e = Mathf.Max((_verts[a] - _verts[b]).sqrMagnitude, Mathf.Max((_verts[b] - _verts[c]).sqrMagnitude, (_verts[c] - _verts[a]).sqrMagnitude)) * s2;
            bool hide = e > _restEdge[t] * lim;
            if (hide != _hidden[t]) { _hidden[t] = hide; changed = true; }
        }
        if (!changed) return;
        _triBuf.Clear();
        for (int t = 0; t < _seam.Length; t++)
        {
            if (_hidden[t]) continue;
            _triBuf.Add(_allTris[t * 3]); _triBuf.Add(_allTris[t * 3 + 1]); _triBuf.Add(_allTris[t * 3 + 2]);
        }
        _mesh.SetTriangles(_triBuf, 0);
    }

    // ラグドールのバット(元絵に描かれていないキャラ用)を、元絵の手の位置へずらして表示する
    private void AttachBat()
    {
        if (HasPaintedBat || _p.Bat == null || _farmF < 0) return;
        if (_batVis == null)
        {
            var sr = _p.BatSprite;
            if (sr == null) return;
            _batVis = sr.transform; _batVisLocal = _batVis.localPosition;
        }
        Vector2 grip = _p.Space.InverseTransformPoint(_p.Bat.TransformPoint(new Vector3(0f, -0.40f, 0f)));
        Vector2 offLocal = Apply(_farmF, _wristF) - grip;
        Vector2 off = _p.Space.TransformVector(offLocal);
        if (_p.BatThrown())
        {
            if (!_batOffFrozenSet) { _batOffFrozenSet = true; _batOffFrozen = off; }
            off = _batOffFrozen;
        }
        Vector3 basePos = _p.Bat.TransformPoint(_batVisLocal);
        _batVis.position = new Vector3(basePos.x + off.x, basePos.y + off.y, basePos.z);
    }

    private Vector2 Apply(int b, Vector2 restWorld)
    {
        var bo = _bones[b];
        Vector2 offset = restWorld - bo.Pivot;
        return bo.AxisX * offset.x + bo.AxisY * offset.y + bo.Cur;
    }

    private void Pose()
    {
        // 骨の今の回転と関節位置(親から順に)
        for (int b = 0; b < _bones.Count; b++)
        {
            var bo = _bones[b];
            float a = (bo.Tr != null ? Mathf.DeltaAngle(bo.Rest, AngOf(bo.Tr)) : 0f) + bo.Offset;
            if (bo.Torso && _rig.APose)
                a *= Mathf.Lerp(TorsoFollow, 1f, Mathf.InverseLerp(35f, 85f, Mathf.Abs(a)));
            if (bo.IsBat)
            {
                float pa = _bones[bo.Parent].Ang;
                if (_p.BatThrown())
                {
                    if (!_batFree) { _batFree = true; _batRel = Mathf.DeltaAngle(pa, a); }
                    a = pa + _batRel;
                }
            }
            bo.Ang = a;
            if (bo.Parent < 0)
            {
                Vector2 now = Loc(bo.Tr);
                bo.Cur = bo.Pivot + (now - _pelRest);
            }
            else bo.Cur = Apply(bo.Parent, bo.Pivot);
            bo.Scale = (b == _headBone) ? _p.HeadPop() : 1f;
            // Aポーズの体は胸がゆっくり上下する(息づかい)。止まって見えないように
            if (b == _chestBone && _rig.APose) bo.Sq = new Vector2(1f, 1f + 0.014f * Mathf.Sin(Time.time * 2.3f + _breathPhase));
            float angle = (bo.Ang + bo.Extra) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            bo.AxisX = new Vector2(cos, sin) * (bo.Scale * bo.Sq.x);
            bo.AxisY = new Vector2(-sin, cos) * (bo.Scale * bo.Sq.y);
        }
        // 各骨の変換を 2x2 行列 + 平行移動の数値にしておき、頂点ごとの計算は float だけで行う
        // (Vector2 の演算子呼び出しを避けると、エディタ・スマホとも数倍速い)
        int nb = _bones.Count;
        if (_m00 == null || _m00.Length != nb)
        {
            _m00 = new float[nb]; _m01 = new float[nb]; _m10 = new float[nb]; _m11 = new float[nb]; _t0 = new float[nb]; _t1 = new float[nb];
        }
        for (int b = 0; b < nb; b++)
        {
            var bo = _bones[b];
            _m00[b] = bo.AxisX.x; _m01[b] = bo.AxisY.x; _m10[b] = bo.AxisX.y; _m11[b] = bo.AxisY.y;
            _t0[b] = bo.Cur.x - (bo.AxisX.x * bo.Pivot.x + bo.AxisY.x * bo.Pivot.y);
            _t1[b] = bo.Cur.y - (bo.AxisX.y * bo.Pivot.x + bo.AxisY.y * bo.Pivot.y);
        }
        var rest = _rest; var bi = _bi; var bw = _bw; var verts = _verts;
        var m00 = _m00; var m01 = _m01; var m10 = _m10; var m11 = _m11; var t0 = _t0; var t1 = _t1;
        for (int v = 0, i = 0; v < rest.Length; v++, i += 3)
        {
            float rx = rest[v].x, ry = rest[v].y, x = 0f, y = 0f;
            for (int k = 0; k < 3; k++)
            {
                float wt = bw[i + k];
                if (wt <= 0f) continue;
                int b = bi[i + k];
                x += wt * (m00[b] * rx + m01[b] * ry + t0[b]);
                y += wt * (m10[b] * rx + m11[b] * ry + t1[b]);
            }
            verts[v].x = x; verts[v].y = y; verts[v].z = 0f;
        }
    }
    private float[] _m00, _m01, _m10, _m11, _t0, _t1;

    private static Camera[] _viewCameras = new Camera[8];
    private static Plane[][] _viewPlanes = new Plane[8][];
    private static int _viewCount, _viewFrame = -1;

    private static void UpdateViewFrustums()
    {
        if (_viewFrame == Time.frameCount) return;
        _viewFrame = Time.frameCount;
        int count = Camera.allCamerasCount;
        if (count > _viewCameras.Length)
        {
            _viewCameras = new Camera[count];
            _viewPlanes = new Plane[count][];
        }
        _viewCount = Camera.GetAllCameras(_viewCameras);
        for (int i = 0; i < _viewCount; i++)
        {
            if (_viewPlanes[i] == null) _viewPlanes[i] = new Plane[6];
            GeometryUtility.CalculateFrustumPlanes(_viewCameras[i], _viewPlanes[i]);
        }
    }

    // 古いメッシュの bounds だけで判定すると、画面外から戻る選手が永久に止まる。
    // 動く骨の bounds で判定。顔カットインなどの有効な別カメラも含める。
    private bool NeedsPose()
    {
        UpdateViewFrustums();
        if (_viewCount == 0) return true;
        Bounds bounds = new Bounds(_p.Pelvis.position, Vector3.zero);
        for (int i = 0; i < _bones.Count; i++)
            if (_bones[i].Tr != null) bounds.Encapsulate(_bones[i].Tr.position);
        float scale = Mathf.Max(Mathf.Abs(_p.Space.lossyScale.x), Mathf.Abs(_p.Space.lossyScale.y));
        bounds.Expand(TargetHeight * Mathf.Max(1f, _rig.Scale) * scale);
        int mask = 1 << gameObject.layer;
        for (int i = 0; i < _viewCount; i++)
            if (_viewCameras[i] != null && (_viewCameras[i].cullingMask & mask) != 0
                && GeometryUtility.TestPlanesAABB(_viewPlanes[i], bounds)) return true;
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCaches()
    {
        MeshCache.Clear(); FaceCache.Clear(); MeshUse.Clear(); FaceUse.Clear();
        _viewFrame = -1; _viewCount = 0;
        System.Array.Clear(_viewCameras, 0, _viewCameras.Length);
    }

    // ---- グローブ(Aポーズの体のみ。捕る側=後ろの腕の手に付ける) ----
    private SpriteRenderer _glove;
    private float _gloveSize;

    public bool AttachGlove(Sprite s)
    {
        if (s == null || _farmB < 0 || !UsesBodyArt) return false;
        if (_glove == null)
        {
            var go = new GameObject("Glove");
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;
            _glove = go.AddComponent<SpriteRenderer>();
            _glove.sortingOrder = _p.Order + 1;
        }
        _glove.sprite = s;
        // 手のひら大(身長のおよそ 1/6)
        _gloveSize = TargetHeight * _rig.Scale * 0.17f;
        Vector2 b = s.bounds.size;
        float k = _gloveSize / Mathf.Max(0.01f, Mathf.Max(b.x, b.y));
        _glove.transform.localScale = new Vector3(k * (_m < 0f ? -1f : 1f), k, 1f);
        PlaceGlove();
        return true;
    }

    private void PlaceGlove()
    {
        if (_glove == null || _farmB < 0) return;
        Vector2 elbow = _bones[_farmB].Cur;
        Vector2 wrist = Apply(_farmB, _wristB);
        Vector2 dir = wrist - elbow;
        if (dir.sqrMagnitude < 1e-8f) dir = Vector2.down;
        dir.Normalize();
        Vector2 pos = wrist + dir * (_gloveSize * 0.28f);
        _glove.transform.localPosition = new Vector3(pos.x, pos.y, -0.01f);
        // 手を入れる口(画像の下)が手首側、指先(画像の上)が腕の先の向き
        _glove.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
    }

    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
        if (_fMesh != null) Destroy(_fMesh);
        if (_material != null) Destroy(_material);
    }

    private void LateUpdate()
    {
        if (_mesh == null || _p == null || !NeedsPose()) return;
        // 低画質では遠くの選手の変形を1フレームおきに
        if (_p.Light && GraphicsQuality.Level == 0 && ((Time.frameCount + GetInstanceID()) & 1) == 0) return;
        FaceState(Time.timeScale > 0f ? Time.deltaTime : Mathf.Min(Time.unscaledDeltaTime, 0.05f));   // 一時停止中も表情は動かす
        Pose();
        AttachBat();
        PlaceGlove();
        _mesh.vertices = _verts;
        UpdateSeams();
        FaceDraw();
        _mesh.RecalculateBounds();
    }
}
