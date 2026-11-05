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
        public readonly List<Vector2> Seg = new List<Vector2>();   // 画素座標の線分(2点ずつ)
        public int Layer;
        public bool IsBat;
        // 毎フレーム
        public float Ang;              // 回転(度)
        public Vector2 Cur;            // 関節の今の位置
        public float Scale = 1f;
        public Vector2 Sq = Vector2.one;   // 表情での伸び縮み(頭)
        public float Extra;                // 表情での追加の傾き(頭)
    }

    private RigBody _p;
    private CharaRig _rig;
    private readonly List<Bone> _bones = new List<Bone>();
    private Mesh _mesh;
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
    private int _farmF = -1;
    private Vector2 _wristF;
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

    public static bool CanRig(string charaId)
    {
        var rig = CharaRig.Get(charaId);
        return rig != null && rig.Has("pelvis") && rig.Has("neck") && CharaCatalog.Cut(charaId) != null;
    }

    public static RigSkin TryAttach(RigBody body, string charaId)
    {
        if (!CanRig(charaId)) return null;
        var rig = CharaRig.Get(charaId);
        var spr = CharaCatalog.Cut(charaId);
        var go = new GameObject("RigSkin");
        go.transform.SetParent(body.Space, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.layer = body.Layer;
        var s = go.AddComponent<RigSkin>();
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
        if (Has("hipL") && Has("hipR")) Seg(iPel, J("hipL"), J("hipR"));
        Seg(iPel, pel, waist);
        int iChest = AddBone(p.Chest, iPel, waist, 3);
        Seg(iChest, waist, neck);
        if (Has("shoulderL") && Has("shoulderR")) Seg(iChest, J("shoulderL"), J("shoulderR"));
        if (Has("chest")) Seg(iChest, J("chest"), neck);
        int iHead = AddBone(p.Head, iChest, neck, 7);
        _headBone = iHead;
        Vector2 head = Has("head") ? J("head") : neck + (neck - pel) * 0.5f;
        Seg(iHead, Vector2.Lerp(neck, head, 0.15f), head);

        int farmF = Arm(armF, p.ArmFU, p.ArmFF, iChest, 6);
        _farmF = farmF;
        if (farmF >= 0 && Has("wrist" + armF)) _wristF = ToWorldRest(J("wrist" + armF));
        int farmB = Arm(armB, p.ArmBU, p.ArmBF, iChest, 1);
        Leg(F, p.LegFU, p.LegFF, iPel, 5);
        Leg(B, p.LegBU, p.LegBF, iPel, 0);

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
        if (!Has("wrist" + s) || fore == null) { Seg(u, sh, el + (el - sh) * 0.6f); return -1; }
        Vector2 wr = J("wrist" + s);
        Seg(u, Vector2.Lerp(sh, el, 0.1f), el);
        int fo = AddBone(fore, u, el, layer);
        Seg(fo, el, wr + (wr - el) * 0.35f);
        return fo;
    }

    private void Leg(string s, Transform up, Transform fore, int parent, int layer)
    {
        if (!Has("hip" + s) || !Has("knee" + s) || up == null) return;
        Vector2 hp = J("hip" + s), kn = J("knee" + s);
        int u = AddBone(up, parent, hp, layer);
        if (!Has("ankle" + s) || fore == null) { Seg(u, hp, kn + (kn - hp) * 0.6f); return; }
        Vector2 an = J("ankle" + s);
        Seg(u, Vector2.Lerp(hp, kn, 0.1f), kn);
        int fo = AddBone(fore, u, kn, layer);
        Seg(fo, kn, an + (an - kn) * 0.25f);
    }

    private float _wScale;

    // 画素 px の骨の重み(上位3本)を bi/bw の v 番目に書く
    private void Weights(Vector2 px, float scale, float[] w, int[] bi, float[] bw, int v)
    {
        int nb = _bones.Count;
        for (int b = 0; b < nb; b++)
        {
            float d = 1e9f;
            var sg = _bones[b].Seg;
            for (int s = 0; s + 1 < sg.Count; s += 2) d = Mathf.Min(d, SegDist(px, sg[s], sg[s + 1]));
            d = Mathf.Max(d, 3f) / scale;
            float d2 = d * d; w[b] = sg.Count == 0 ? 0f : 1f / (d2 * d2 * d2);   // 6乗: 骨の近くはほぼその骨だけに付く
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

    private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }

    private void BuildMesh(Sprite spr)
    {
        var box = _rig.Box;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(box.x / Cell) * Cell - Cell);
        int y0 = Mathf.Max(0, Mathf.FloorToInt(box.y / Cell) * Cell - Cell);
        int x1 = Mathf.Min((int)_rig.Size.x, Mathf.CeilToInt(box.z / Cell) * Cell + Cell);
        int y1 = Mathf.Min((int)_rig.Size.y, Mathf.CeilToInt(box.w / Cell) * Cell + Cell);
        int nx = (x1 - x0) / Cell + 1, ny = (y1 - y0) / Cell + 1;
        int n = nx * ny;
        _rest = new Vector2[n];
        _verts = new Vector3[n];
        var uv = new Vector2[n];
        _bi = new int[n * 3];
        _bw = new float[n * 3];
        var tex = spr.texture;
        Rect tr = spr.textureRect;
        float scale = 0.5f * _rig.Size.y / Mathf.Max(64f, box.w - box.y);   // 骨の太さの目安
        _wScale = scale;
        var w = new float[_bones.Count];
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                int v = j * nx + i;
                var px = new Vector2(x0 + i * Cell, y0 + j * Cell);
                _rest[v] = ToWorldRest(px);
                uv[v] = new Vector2((tr.x + px.x / _rig.Size.x * tr.width) / tex.width,
                                    (tr.y + (1f - px.y / _rig.Size.y) * tr.height) / tex.height);
                Weights(px, scale, w, _bi, _bw, v);
            }

        // 三角形は骨の前後(レイヤー)順に並べる(後ろ脚→後ろ腕→腰→胸→前脚→前腕→頭→バット)
        var buckets = new List<int>[9];
        for (int l = 0; l < 9; l++) buckets[l] = new List<int>();
        for (int j = 0; j + 1 < ny; j++)
            for (int i = 0; i + 1 < nx; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                int layer = _bones[_bi[a * 3]].Layer;
                layer = Mathf.Max(layer, _bones[_bi[d * 3]].Layer);
                buckets[Mathf.Clamp(layer, 0, 8)].AddRange(new[] { a, c, b, b, c, d });
            }
        var tris = new List<int>();
        foreach (var bk in buckets) tris.AddRange(bk);
        // 違う骨にまたがる三角形(つなぎ目)は、大きく伸びたら描かない(腕を振り上げたときのゴム状の伸び防止)
        _allTris = tris.ToArray();
        int nt = _allTris.Length / 3;
        _seam = new bool[nt];
        _restEdge = new float[nt];
        for (int t = 0; t < nt; t++)
        {
            int a = _allTris[t * 3], b = _allTris[t * 3 + 1], c = _allTris[t * 3 + 2];
            int ba = _bi[a * 3], bb = _bi[b * 3], bc = _bi[c * 3];
            _seam[t] = !(ba == bb && bb == bc);
            _restEdge[t] = Mathf.Max((_rest[a] - _rest[b]).sqrMagnitude, Mathf.Max((_rest[b] - _rest[c]).sqrMagnitude, (_rest[c] - _rest[a]).sqrMagnitude));
        }
        _hidden = new bool[nt];

        _mesh = new Mesh { name = "RigSkin" };
        _mesh.MarkDynamic();
        Pose();
        _mesh.vertices = _verts;
        _mesh.uv = uv;
        _mesh.SetTriangles(tris, 0);
        _mesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
        var mr = gameObject.AddComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = tex;
        mr.sharedMaterial = mat;
        mr.sortingOrder = _p.Order;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
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

    private static Vector2 Rot(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private Vector2 Apply(int b, Vector2 restWorld)
    {
        var bo = _bones[b];
        return Rot(Vector2.Scale(restWorld - bo.Pivot, bo.Sq) * bo.Scale, bo.Ang + bo.Extra) + bo.Cur;
    }

    private void Pose()
    {
        // 骨の今の回転と関節位置(親から順に)
        for (int b = 0; b < _bones.Count; b++)
        {
            var bo = _bones[b];
            float a = bo.Tr != null ? Mathf.DeltaAngle(bo.Rest, AngOf(bo.Tr)) : 0f;
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
        }
        const float z = 0f;
        for (int v = 0; v < _rest.Length; v++)
        {
            Vector2 acc = Vector2.zero;
            for (int k = 0; k < 3; k++)
            {
                float wt = _bw[v * 3 + k];
                if (wt <= 0f) continue;
                acc += Apply(_bi[v * 3 + k], _rest[v]) * wt;
            }
            _verts[v] = new Vector3(acc.x, acc.y, z);
        }
    }

    private void LateUpdate()
    {
        if (_mesh == null || _p == null) return;
        FaceState(Time.timeScale > 0f ? Time.deltaTime : Mathf.Min(Time.unscaledDeltaTime, 0.05f));   // 一時停止中も表情は動かす
        Pose();
        AttachBat();
        _mesh.vertices = _verts;
        UpdateSeams();
        FaceDraw();
        _mesh.RecalculateBounds();
    }
}
