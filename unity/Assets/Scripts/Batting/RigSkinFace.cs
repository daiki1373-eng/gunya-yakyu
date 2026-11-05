using System.Collections.Generic;
using UnityEngine;

// 元絵の表情を動かす(どのキャラでも同じ仕組み)。
// 1) 顔の部分だけ細かい網目を重ね、目・口・眉のまわりを伸び縮みさせる(目を細める/見開く/口を開ける/まばたき)
// 2) 頭全体を伸び縮み・傾ける
// 3) 漫画の記号(汗・驚き線・びっくり・頬の赤み・キラキラ・目が回る星)を顔の位置に出す
// 目・口の位置は Tools/chara-rig で自動推定(rig.txt の eyeL/eyeR/mouth/browL/browR)。
// 顔が分からないキャラは記号と頭の伸び縮みだけ。
public partial class RigSkin
{
    private const int FCell = 3;
    private readonly List<Vector2> _eyes = new List<Vector2>();
    private bool _hasMouth, _hasBrows, _warpable;
    private Vector2 _mouthPx, _mouthSz, _browL, _browR, _faceC, _headTopPx;
    private float _io;                 // 目と目の間(画素)。顔の大きさの基準
    private float _side = 1f;          // 記号を出す側(画像の右=+1)

    private static readonly Dictionary<MeshKey, MeshData> FaceCache = new Dictionary<MeshKey, MeshData>();
    private Mesh _fMesh;
    private MeshRenderer _fMr;
    private Vector2[] _fPx;
    private Vector3[] _fV;
    private int[] _fBi;
    private float[] _fBw;

    // 表情の重み(0〜1)
    private readonly float[] _ew = new float[7];
    private float _blink, _blinkNext = 2f, _t;
    private float _surpriseAge, _happyAge, _focusAge, _sadAge, _angryAge;
    private bool _laughBase;          // 元絵がもともと大笑いしている(喜びとの差を大きくする)
    private Color[] _fCol;

    // 記号
    private SpriteRenderer _sweat, _bang, _blushA, _blushB, _gloom, _anger, _tear;
    private SpriteRenderer[] _shock, _spark, _stars;

    private void BuildFace(Sprite spr)
    {
        foreach (var n in new[] { "eyeL", "eyeR" }) if (Has(n)) _eyes.Add(J(n));
        _hasMouth = Has("mouth");
        Vector2 neck = J("neck");
        Vector2 head = Has("head") ? J("head") : neck + new Vector2(0f, -100f);
        float headLen = Mathf.Max(30f, (head - neck).magnitude);
        if (_eyes.Count == 2) _io = Mathf.Max(14f, Vector2.Distance(_eyes[0], _eyes[1]));
        else _io = headLen * 0.32f;
        if (_eyes.Count > 0)
        {
            Vector2 ec = Vector2.zero; foreach (var e in _eyes) ec += e; ec /= _eyes.Count;
            _faceC = _hasMouth ? Vector2.Lerp(ec, J("mouth"), 0.35f) : ec + new Vector2(0f, _io * 0.3f);
        }
        else _faceC = Vector2.Lerp(neck, head, 0.45f);
        _headTopPx = _faceC + new Vector2(0f, -_io * 1.55f);
        if (Has("head") && head.y < _headTopPx.y) _headTopPx = new Vector2(_faceC.x, Mathf.Max(head.y, _faceC.y - _io * 2.3f));
        if (_hasMouth)
        {
            _mouthPx = J("mouth");
            Vector2 sz = Has("mouthSz") ? J("mouthSz") : Vector2.zero;
            _mouthSz = new Vector2(Mathf.Max(sz.x * 0.6f, _io * 0.32f), Mathf.Max(sz.y * 0.8f, _io * 0.2f));
        }
        _laughBase = _hasMouth && Has("mouthSz") && (J("mouthSz").y > _io * 0.42f || J("mouthSz").x > _io * 0.95f);
        _hasBrows = Has("browL") && Has("browR");
        if (_hasBrows) { _browL = J("browL"); _browR = J("browR"); }
        _warpable = _eyes.Count > 0;
        // 記号を出す側: 進行方向の反対側(顔の後ろ側)
        _side = -_p.Facing * _m;

        if (_warpable) BuildFacePatch(spr);
        BuildMarks();
    }

    private void BuildFacePatch(Sprite spr)
    {
        float minX = 1e9f, maxX = -1e9f, minY = 1e9f, maxY = -1e9f;
        foreach (var e in _eyes) { minX = Mathf.Min(minX, e.x); maxX = Mathf.Max(maxX, e.x); minY = Mathf.Min(minY, e.y); maxY = Mathf.Max(maxY, e.y); }
        if (_hasMouth) { minX = Mathf.Min(minX, _mouthPx.x); maxX = Mathf.Max(maxX, _mouthPx.x); maxY = Mathf.Max(maxY, _mouthPx.y); }
        int x0 = Mathf.Max(0, Mathf.FloorToInt((minX - _io * 1.05f) / FCell) * FCell);
        int x1 = Mathf.Min((int)_rig.Size.x, Mathf.CeilToInt((maxX + _io * 1.05f) / FCell) * FCell);
        int y0 = Mathf.Max(0, Mathf.FloorToInt((minY - _io * 1.1f) / FCell) * FCell);
        int y1 = Mathf.Min((int)_rig.Size.y, Mathf.CeilToInt((Mathf.Max(maxY + _io * 0.85f, minY + _io * 1.3f)) / FCell) * FCell);
        int nx = (x1 - x0) / FCell + 1, ny = (y1 - y0) / FCell + 1;
        if (nx < 3 || ny < 3) { _warpable = false; return; }
        int n = nx * ny;
        var key = CacheKey(FCell);
        MeshData data;
        if (!FaceCache.TryGetValue(key, out data))
        {
            data = new MeshData { Pixels = new Vector2[n], UV = new Vector2[n], Bones = new int[n * 3], Weights = new float[n * 3] };
            var tex = spr.texture; Rect tr = spr.textureRect;
            var w = new float[_bones.Count];
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    int v = j * nx + i;
                    var px = new Vector2(x0 + i * FCell, y0 + j * FCell);
                    data.Pixels[v] = px;
                    data.UV[v] = new Vector2((tr.x + px.x / _rig.Size.x * tr.width) / tex.width,
                                        (tr.y + (1f - px.y / _rig.Size.y) * tr.height) / tex.height);
                    Weights(px, _wScale, w, data.Bones, data.Weights, v);
                }
            data.Triangles = new int[(nx - 1) * (ny - 1) * 6];
            int t = 0;
            for (int j = 0; j + 1 < ny; j++)
                for (int i = 0; i + 1 < nx; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    data.Triangles[t++] = a; data.Triangles[t++] = c; data.Triangles[t++] = b;
                    data.Triangles[t++] = b; data.Triangles[t++] = c; data.Triangles[t++] = d;
                }
            FaceCache.Add(key, data);
        }
        _fPx = data.Pixels; _fBi = data.Bones; _fBw = data.Weights;
        _fV = new Vector3[n];
        var go = new GameObject("Face");
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;
        _fMesh = new Mesh { name = "RigFace" };
        _fMesh.MarkDynamic();
        _fCol = new Color[n];
        for (int i = 0; i < n; i++) _fCol[i] = Color.white;
        _fMesh.vertices = _fV; _fMesh.uv = data.UV; _fMesh.triangles = data.Triangles; _fMesh.colors = _fCol;
        go.AddComponent<MeshFilter>().sharedMesh = _fMesh;
        _fMr = go.AddComponent<MeshRenderer>();
        _fMr.sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;
        _fMr.sortingOrder = 7;
        _fMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _fMr.enabled = false;
    }

    // ---------------- 表情の状態 ----------------
    // 表情ごとの目標値: 目の縦/横, 口の横/縦, 眉の上下(io比), 頭の横/縦
    private struct Look { public float EyeY, EyeX, MouthX, MouthY, Brow, HeadX, HeadY; }
    private static readonly Look[] Looks =
    {
        new Look { EyeY = 1f,    EyeX = 1f,   MouthX = 1f,    MouthY = 1f,   Brow = 0f,     HeadX = 1f,    HeadY = 1f },     // Normal
        new Look { EyeY = 0.55f, EyeX = 1.05f, MouthX = 0.85f, MouthY = 0.8f, Brow = 0.13f,  HeadX = 1.03f, HeadY = 0.97f },  // Focus
        new Look { EyeY = 1.4f,  EyeX = 1.3f, MouthX = 0.85f, MouthY = 1.9f, Brow = -0.18f, HeadX = 0.92f, HeadY = 1.12f },  // Surprise
        new Look { EyeY = 0.25f, EyeX = 1.1f, MouthX = 1.3f,  MouthY = 1.55f, Brow = -0.06f, HeadX = 1.05f, HeadY = 1.0f },  // Happy
        new Look { EyeY = 0.3f,  EyeX = 1.15f, MouthX = 1.15f, MouthY = 1.35f, Brow = -0.1f, HeadX = 1f,    HeadY = 1f },     // Dizzy
        new Look { EyeY = 0.6f,  EyeX = 0.95f, MouthX = 0.7f,  MouthY = 0.55f, Brow = -0.1f, HeadX = 1.03f, HeadY = 0.93f },  // Sad
        new Look { EyeY = 0.68f, EyeX = 1.02f, MouthX = 1.2f,  MouthY = 1.4f,  Brow = 0.22f, HeadX = 1.05f, HeadY = 0.97f },  // Angry
    };
    private Look _now;

    private void FaceState(float dt)
    {
        _t += dt;
        int cur = (int)_p.Expression();
        for (int e = 0; e < 7; e++) _ew[e] = Mathf.MoveTowards(_ew[e], e == cur ? 1f : 0f, dt * 9f);
        _surpriseAge = cur == (int)Expr.Surprise ? _surpriseAge + dt : 0f;
        _happyAge = cur == (int)Expr.Happy ? _happyAge + dt : 0f;
        _focusAge = cur == (int)Expr.Focus ? _focusAge + dt : 0f;
        _sadAge = cur == (int)Expr.Sad ? _sadAge + dt : 0f;
        _angryAge = cur == (int)Expr.Angry ? _angryAge + dt : 0f;

        // 重みで混ぜる
        var L = new Look { EyeY = 1, EyeX = 1, MouthX = 1, MouthY = 1, Brow = 0, HeadX = 1, HeadY = 1 };
        for (int e = 1; e < 7; e++)
        {
            float k = _ew[e]; if (k <= 0f) continue;
            var o = Looks[e];
            L.EyeY += (o.EyeY - 1f) * k; L.EyeX += (o.EyeX - 1f) * k;
            L.MouthX += (o.MouthX - 1f) * k; L.MouthY += (o.MouthY - 1f) * k;
            L.Brow += o.Brow * k; L.HeadX += (o.HeadX - 1f) * k; L.HeadY += (o.HeadY - 1f) * k;
        }
        // 驚きは最初にびよんと伸びる / 喜びは弾む
        float pop = _surpriseAge > 0f ? Mathf.Exp(-_surpriseAge * 6f) * Mathf.Sin(_surpriseAge * 30f) * 0.08f : 0f;
        L.HeadY += pop; L.HeadX -= pop * 0.6f;
        // 喜びは弾む(もともと大笑いの絵は大きく弾ませてのけぞる)
        float hk = _laughBase ? 2.2f : 1f;
        if (_happyAge > 0f) { float b = Mathf.Abs(Mathf.Sin(_happyAge * 11f)) * 0.04f * hk * _ew[(int)Expr.Happy]; L.HeadY += b; L.HeadX -= b * 0.5f; }

        // まばたき(普段だけ)
        if (_ew[0] > 0.9f)
        {
            _blinkNext -= dt;
            if (_blinkNext <= 0f) { _blink = 0.13f; _blinkNext = Random.Range(1.8f, 4.2f); }
        }
        if (_blink > 0f) { _blink -= dt; L.EyeY = Mathf.Min(L.EyeY, 0.12f); }
        _now = L;

        var hb = _bones[_headBone];
        hb.Sq = new Vector2(L.HeadX, L.HeadY);
        hb.Extra = _ew[(int)Expr.Dizzy] * Mathf.Sin(_t * 7f) * 9f
                 + _ew[(int)Expr.Angry] * Mathf.Sin(_t * 46f) * 3.5f                     // 怒り: ぶるぶる震える
                 + _ew[(int)Expr.Sad] * 7f * _p.Facing                                     // がっかり: うなだれる
                 + (_laughBase ? _ew[(int)Expr.Happy] * Mathf.Sin(_happyAge * 5.5f) * 6f : 0f);   // 大笑い: のけぞって揺れる
    }

    private static float Smooth(float a, float b, float x)
    {
        float u = Mathf.Clamp01((x - a) / (b - a)); return u * u * (3f - 2f * u);
    }

    // 中心 c・半径 (rx,ry) の内側を (sx,sy) 倍に。外側へなめらかに元に戻す
    private static Vector2 ScaleWarp(Vector2 p, Vector2 c, float rx, float ry, float sx, float sy)
    {
        Vector2 q = p - c;
        float n = Mathf.Sqrt((q.x / rx) * (q.x / rx) + (q.y / ry) * (q.y / ry));
        float f = 1f - Smooth(0.55f, 1.75f, n);
        if (f <= 0f) return p;
        return c + new Vector2(q.x * Mathf.Lerp(1f, sx, f), q.y * Mathf.Lerp(1f, sy, f));
    }

    private static Vector2 MoveWarp(Vector2 p, Vector2 c, float rx, float ry, Vector2 d)
    {
        Vector2 q = p - c;
        float n = Mathf.Sqrt((q.x / rx) * (q.x / rx) + (q.y / ry) * (q.y / ry));
        float f = 1f - Smooth(0.4f, 1.3f, n);
        return f <= 0f ? p : p + d * f;
    }

    // 確認用: 顔の中心のワールド座標と、顔の大きさ(ワールド)
    public Vector3 FaceWorld()
    {
        Vector2 w = Apply(_headBone, ToWorldRest(_faceC));
        return transform.TransformPoint(new Vector3(w.x, w.y, 0f));
    }
    public float FaceSpan() { return _io * _k * 2.2f * transform.lossyScale.y; }

    private bool FaceActive()
    {
        var L = _now;
        return Mathf.Abs(L.EyeY - 1f) + Mathf.Abs(L.EyeX - 1f) + Mathf.Abs(L.MouthX - 1f) + Mathf.Abs(L.MouthY - 1f) + Mathf.Abs(L.Brow) > 0.02f
            || _ew[(int)Expr.Sad] + _ew[(int)Expr.Angry] > 0.01f;
    }

    private void FaceDraw()
    {
        if (_warpable && _fMesh != null)
        {
            bool on = FaceActive();
            _fMr.enabled = on;
            if (on) WarpFace();
        }
        DrawMarks();
    }

    private void WarpFace()
    {
        var L = _now;
        float erx = _io * 0.42f, ery = _io * 0.36f;
        Vector2 browD = new Vector2(0f, L.Brow * _io);
        for (int v = 0; v < _fPx.Length; v++)
        {
            Vector2 p = _fPx[v];
            // 眉(眉の位置が分からなければ目の少し上)
            if (Mathf.Abs(L.Brow) > 0.001f)
            {
                for (int e = 0; e < _eyes.Count; e++)
                {
                    Vector2 bc = _hasBrows ? (e == 0 ? _browL : _browR) : _eyes[e] + new Vector2(0f, -_io * 0.42f);
                    p = MoveWarp(p, bc, _io * 0.42f, _io * 0.22f, browD);
                }
            }
            for (int e = 0; e < _eyes.Count; e++) p = ScaleWarp(p, _eyes[e], erx, ery, L.EyeX, L.EyeY);
            if (_hasMouth) p = ScaleWarp(p, _mouthPx, _mouthSz.x, _mouthSz.y, L.MouthX, L.MouthY);
            Vector2 rw = ToWorldRest(p);
            Vector2 acc = Vector2.zero;
            for (int k = 0; k < 3; k++)
            {
                float wt = _fBw[v * 3 + k];
                if (wt > 0f) acc += Apply(_fBi[v * 3 + k], rw) * wt;
            }
            _fV[v] = new Vector3(acc.x, acc.y, 0f);
        }
        _fMesh.vertices = _fV;
        _fMesh.RecalculateBounds();
        TintFace();
    }

    // がっかり: 顔の上半分が青ざめる / 怒り: 顔が赤くなる(網目の頂点色)
    private void TintFace()
    {
        float ws = _ew[(int)Expr.Sad], wa = _ew[(int)Expr.Angry];
        if (ws + wa <= 0.001f && !_tinted) return;
        _tinted = ws + wa > 0.001f;
        float eyeY = 0f; foreach (var e in _eyes) eyeY += e.y; eyeY /= Mathf.Max(1, _eyes.Count);
        Color blue = new Color(0.5f, 0.62f, 1f), red = new Color(1f, 0.55f, 0.5f);
        float pulse = 0.85f + 0.15f * Mathf.Sin(_t * 9f);
        for (int v = 0; v < _fPx.Length; v++)
        {
            Vector2 p = _fPx[v];
            float top = Mathf.Clamp01((eyeY + _io * 0.25f - p.y) / (_io * 0.9f));      // 目より上ほど強い
            float r = Vector2.Distance(p, _faceC) / (_io * 1.4f);
            float edge = 1f - Smooth(0.6f, 1f, r);                                        // 網目の端では元の色
            Color c = Color.Lerp(Color.white, blue, ws * top * 0.75f * edge);
            c = Color.Lerp(c, red, wa * 0.55f * pulse * edge);
            _fCol[v] = c;
        }
        _fMesh.colors = _fCol;
    }
    private bool _tinted;

    // ---------------- 漫画の記号 ----------------
    private SpriteRenderer Mark(Sprite s, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.sortingOrder = 9; sr.enabled = false;
        return sr;
    }

    private void BuildMarks()
    {
        _sweat = Mark(FaceMarks.Sweat, "Sweat");
        _bang = Mark(FaceMarks.Bang, "Bang");
        _blushA = Mark(FaceMarks.Blush, "BlushA");
        _blushB = Mark(FaceMarks.Blush, "BlushB");
        _shock = new SpriteRenderer[3];
        for (int i = 0; i < 3; i++) _shock[i] = Mark(FaceMarks.Line, "Shock" + i);
        _spark = new SpriteRenderer[6];
        for (int i = 0; i < 6; i++) _spark[i] = Mark(FaceMarks.Sparkle, "Spark" + i);
        _gloom = Mark(FaceMarks.Gloom, "Gloom");
        _anger = Mark(FaceMarks.Anger, "Anger");
        _tear = Mark(FaceMarks.Sweat, "Tear");
        _stars = new SpriteRenderer[3];
        for (int i = 0; i < 3; i++) _stars[i] = Mark(FaceMarks.Star, "Star" + i);
    }

    // 画素(元絵)の位置に、頭と一緒に動く記号を置く。size は io 比
    private void Place(SpriteRenderer sr, Vector2 px, float size, float alpha, float rotDeg = 0f, float sy = 1f)
    {
        if (alpha <= 0.01f || size <= 0.001f) { sr.enabled = false; return; }
        sr.enabled = true;
        Vector2 w = Apply(_headBone, ToWorldRest(px));
        sr.transform.localPosition = new Vector3(w.x, w.y, 0f);
        float ws = size * _io * _k;
        sr.transform.localScale = new Vector3(ws, ws * sy, 1f);
        sr.transform.localRotation = Quaternion.Euler(0f, 0f, _bones[_headBone].Ang + _bones[_headBone].Extra + rotDeg);
        var c = sr.color; c.a = alpha; sr.color = c;
    }

    private void DrawMarks()
    {
        if (_sweat == null) return;   // 遠くの選手(記号なし)
        float io = _io, sd = _side;
        Vector2 top = _headTopPx;
        // 集中: こめかみに汗が一粒たれる
        float wf = _ew[(int)Expr.Focus];
        Place(_sweat, _faceC + new Vector2(sd * io * 0.95f, -io * 0.75f + Mathf.Min(_focusAge, 0.8f) * io * 0.35f), 0.75f, wf);
        // 驚き: 頭の上に驚き線 + びっくりマーク
        float ws = _ew[(int)Expr.Surprise];
        float popS = Mathf.Clamp01(_surpriseAge * 8f);
        for (int i = 0; i < 3; i++)
        {
            float a = (i - 1) * 32f;
            Vector2 dir = new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), -Mathf.Cos(a * Mathf.Deg2Rad));
            Place(_shock[i], top + dir * io * (0.45f + 0.35f * popS), 0.85f * popS, ws, -a * _m * 1f);
        }
        Place(_bang, _faceC + new Vector2(sd * io * 1.45f, -io * 1.1f), 1.15f * (0.6f + 0.4f * popS), ws, sd * -12f);
        // 喜び: 頬の赤み + キラキラ
        float wh = _ew[(int)Expr.Happy];
        if (_eyes.Count > 0)
        {
            Vector2 ea = _eyes[0], eb = _eyes.Count > 1 ? _eyes[1] : _eyes[0] + new Vector2(io, 0f);
            Place(_blushA, ea + new Vector2(-io * 0.1f, io * 0.5f), 0.8f, wh, 0f, 0.55f);
            Place(_blushB, eb + new Vector2(io * 0.1f, io * 0.5f), 0.8f, wh, 0f, 0.55f);
        }
        else { _blushA.enabled = false; _blushB.enabled = false; }
        for (int i = 0; i < 2; i++)
        {
            float tw = 0.6f + 0.4f * Mathf.Sin(_t * 9f + i * 2.1f);
            float sx = (i == 0 ? sd : -sd);
            Place(_spark[i], _faceC + new Vector2(sx * io * 1.5f, -io * (0.8f + 0.5f * i)), 0.9f * tw, wh, _t * 90f);
        }
        // 喜びの最初にキラキラが顔のまわりへはじける
        for (int i = 2; i < 6; i++)
        {
            float a = (i - 2) * 90f + 45f;
            float u = Mathf.Clamp01(_happyAge / 0.5f);
            Vector2 dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
            Place(_spark[i], _faceC + dir * io * (0.6f + 1.3f * u), 0.7f * (1f - u * 0.6f), wh * (1f - u), _t * 120f);
        }
        // がっかり: おでこに縦線 + 目から涙
        float wsd = _ew[(int)Expr.Sad];
        Vector2 eyeC = _faceC; if (_eyes.Count > 0) { eyeC = Vector2.zero; foreach (var e in _eyes) eyeC += e; eyeC /= _eyes.Count; }
        Place(_gloom, eyeC + new Vector2(0f, -io * 0.75f), 1.1f, wsd * 0.9f);
        if (_eyes.Count > 0)
        {
            Vector2 te = _eyes[_eyes.Count - 1];
            float drop = Mathf.Repeat(_sadAge, 0.9f) / 0.9f;
            Place(_tear, te + new Vector2(0f, io * (0.25f + 0.7f * drop)), 0.32f, wsd * (1f - drop * 0.7f), 180f);
        }
        else _tear.enabled = false;
        // 怒り: こめかみに怒りマーク(脈打つ)
        float wan = _ew[(int)Expr.Angry];
        float beat = 1f + 0.15f * Mathf.Abs(Mathf.Sin(_angryAge * 9f));
        Place(_anger, _faceC + new Vector2(sd * io * 1.1f, -io * 1.05f), 0.8f * beat, wan);
        // 目が回る: 頭の上を星が回る
        float wd = _ew[(int)Expr.Dizzy];
        for (int i = 0; i < 3; i++)
        {
            float a = _t * 4f + i * 2.094f;
            Vector2 pos = top + new Vector2(Mathf.Cos(a) * io * 1.3f, Mathf.Sin(a) * io * 0.35f + io * 0.2f);
            Place(_stars[i], pos, 0.65f * (0.85f + 0.15f * Mathf.Sin(a)), wd, a * 40f);
        }
    }
}

// 表情の記号の絵(コードで描く・最初に一度だけ作る)
public static class FaceMarks
{
    private static Sprite _sweat, _bang, _blush, _line, _sparkle, _star;
    public static Sprite Sweat { get { return _sweat ?? (_sweat = Make(SweatF)); } }
    public static Sprite Bang { get { return _bang ?? (_bang = Make(BangF)); } }
    public static Sprite Blush { get { return _blush ?? (_blush = Make(BlushF)); } }
    public static Sprite Line { get { return _line ?? (_line = Make(LineF)); } }
    public static Sprite Sparkle { get { return _sparkle ?? (_sparkle = Make(SparkleF)); } }
    public static Sprite Star { get { return _star ?? (_star = Make(StarF)); } }
    private static Sprite _gloom, _anger;
    public static Sprite Gloom { get { return _gloom ?? (_gloom = Make(GloomF)); } }
    public static Sprite Anger { get { return _anger ?? (_anger = Make(AngerF)); } }

    private static Color GloomF(float x, float y)
    {
        // 縦線4本(上ほど濃い)
        for (int k = 0; k < 4; k++)
        {
            float cx = -0.6f + k * 0.4f;
            float len = (k == 1 || k == 2) ? 0.9f : 0.6f;
            if (Mathf.Abs(x - cx) < 0.05f && y < 0.9f && y > 0.9f - len * 1.6f)
                return new Color(0.25f, 0.3f, 0.75f, Mathf.Clamp01((y - (0.9f - len * 1.6f)) / (len * 1.6f)) * 0.9f);
        }
        return new Color(0, 0, 0, 0);
    }

    private static Color AngerF(float x, float y)
    {
        // 怒りマーク: 4つの角に、中心を向いた曲がった線
        float best = 9f;
        for (int k = 0; k < 4; k++)
        {
            float sx = (k & 1) == 0 ? 1f : -1f, sy = (k & 2) == 0 ? 1f : -1f;
            float cx = sx * 0.62f, cy = sy * 0.62f;
            float r = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            // 中心側の四分円だけ
            if ((x - cx) * sx > 0.05f || (y - cy) * sy > 0.05f) continue;
            best = Mathf.Min(best, Mathf.Abs(r - 0.42f) - 0.09f);
        }
        return Fill(best, new Color(0.95f, 0.15f, 0.15f, 1f), 0.07f);
    }

    private delegate Color Shape(float x, float y);   // x,y は -1〜1

    private static Sprite Make(Shape f)
    {
        const int N = 96;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[N * N];
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                Color acc = new Color(0, 0, 0, 0);
                for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                    {
                        float x = ((i + 0.25f + sx * 0.5f) / N) * 2f - 1f;
                        float y = ((j + 0.25f + sy * 0.5f) / N) * 2f - 1f;
                        Color c = f(x, y);
                        acc.r += c.r * c.a; acc.g += c.g * c.a; acc.b += c.b * c.a; acc.a += c.a;
                    }
                if (acc.a > 0f) { acc.r /= acc.a; acc.g /= acc.a; acc.b /= acc.a; }
                acc.a /= 4f;
                px[j * N + i] = acc;
            }
        tex.SetPixels(px);
        tex.Apply(true, true);
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
    }

    private static readonly Color Ink = new Color(0.16f, 0.1f, 0.12f, 1f);

    // 中身と輪郭: d<0 が内側(距離は-1〜1座標)
    private static Color Fill(float d, Color fill, float outline)
    {
        if (d > outline) return new Color(0, 0, 0, 0);
        if (d > 0f) return Ink;
        return fill;
    }

    private static Color SweatF(float x, float y)
    {
        // しずく: 下の丸 + 上へとがる
        float cy = -0.25f, r = 0.5f;
        float dc = Mathf.Sqrt(x * x + (y - cy) * (y - cy)) - r;
        float tip = 0.95f;
        float u = Mathf.Clamp01((y - cy) / (tip - cy));
        float half = r * (1f - u) * Mathf.Sqrt(1f - u * 0.3f);
        float dt = (y > cy && y < tip) ? Mathf.Abs(x) - half : 9f;
        float d = Mathf.Min(dc, dt);
        Color c = Fill(d, new Color(0.62f, 0.86f, 1f, 1f), 0.09f);
        if (d < 0f && (x + 0.18f) * (x + 0.18f) + (y + 0.05f) * (y + 0.05f) * 0.5f < 0.02f) c = Color.white;
        return c;
    }

    private static Color BangF(float x, float y)
    {
        // びっくりマーク(棒 + 点)
        float bar = (y > -0.2f && y < 0.95f) ? Mathf.Abs(x) - (0.12f + 0.12f * (y + 0.2f) / 1.15f) : 9f;
        float dot = Mathf.Sqrt(x * x + (y + 0.62f) * (y + 0.62f)) - 0.17f;
        return Fill(Mathf.Min(bar, dot), new Color(1f, 0.22f, 0.18f, 1f), 0.1f);
    }

    private static Color BlushF(float x, float y)
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float a = Mathf.Clamp01(1.15f - r * 1.15f) * 0.85f;
        // 斜線3本
        for (int k = -1; k <= 1; k++)
        {
            float lx = x - k * 0.32f;
            if (Mathf.Abs(lx + y * 0.45f) < 0.06f && Mathf.Abs(y) < 0.4f) return new Color(0.9f, 0.25f, 0.35f, 0.85f);
        }
        return new Color(1f, 0.45f, 0.55f, a);
    }

    private static Color LineF(float x, float y)
    {
        // 驚き線: 上下に細長いくさび
        float w = 0.05f + 0.13f * (y + 1f) / 2f;
        float d = (y > -0.9f && y < 0.9f) ? Mathf.Abs(x) - w : 9f;
        return Fill(d, new Color(1f, 0.85f, 0.2f, 1f), 0.06f);
    }

    private static Color SparkleF(float x, float y)
    {
        // 4方向に伸びるキラキラ
        float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
        float s = Mathf.Pow(ax, 0.55f) + Mathf.Pow(ay, 0.55f);
        if (s < 0.95f) return new Color(1f, 0.97f, 0.7f, 1f);
        float r = Mathf.Sqrt(x * x + y * y);
        return new Color(1f, 0.9f, 0.4f, Mathf.Clamp01(0.5f - r) * 0.8f);
    }

    private static Color StarF(float x, float y)
    {
        // 5つの角の星
        float a = Mathf.Atan2(x, y);
        float r = Mathf.Sqrt(x * x + y * y);
        float seg = Mathf.PI * 2f / 5f;
        float t = Mathf.Abs(Mathf.Repeat(a + seg / 2f, seg) - seg / 2f) / (seg / 2f);
        float edge = Mathf.Lerp(0.9f, 0.4f, t);
        return Fill(r - edge, new Color(1f, 0.86f, 0.15f, 1f), 0.1f);
    }
}
