using System.Collections.Generic;
using UnityEngine;

// ============================================================
// StandeePuppet.cs
// キャラの立ち姿(1枚の切り抜き絵)を、上半身・左脚・右脚の3枚に切り分けて動かす。
// 新しい絵は使わず、同じ画像の一部を表示するだけ(Sprite.Create の範囲指定)。
// - 走る: 脚を交互に振る・上下に弾む・少し前のめり
// - 待つ: 小さく呼吸して揺れる
// - 捕る: 伸び上がって縮む / 投げる: 上半身をひねって振り抜く
// 座っている絵・脚のない絵は切り分けず、全体を弾ませる。動きの大きさはぐにゃ度に比例。
// ============================================================
public class StandeePuppet : MonoBehaviour
{
    private struct Meta { public int X0, X1, Top; }
    private static Dictionary<string, Meta> _meta;
    // 切り分けると不自然になる絵(座っている・脚がない・3コマ漫画の切り出し)
    private static readonly HashSet<string> NoLegs = new HashSet<string>
    {
        "bigeater", "slacker", "fortuneteller", "doctor", "snowman", "deliveryrobot", "dogu", "umpire", "robot"
    };
    private const float HipRatio = 0.44f;   // 足元から腰までの高さ(絵の高さに対する比)

    private Transform _root;                 // 動きを測る基準(人形のいちばん上)
    private Transform _body;
    private SpriteRenderer _up, _legL, _legR;
    private bool _legs;
    private float _run, _phase, _seed, _scale = 1f;
    private Vector3 _last;
    private bool _hasLast;
    private int _act;                        // 0=なし 1=捕る 2=投げる
    private float _actT, _actDur;

    public static float DebugRun = -1f;      // 検証用: 0..1 で走りの強さを固定(-1=実際の動きから)
    private static readonly Dictionary<string, Sprite[]> Cache = new Dictionary<string, Sprite[]>();

    public static StandeePuppet Create(Transform parent, Transform root, int order)
    {
        var go = new GameObject("Standee");
        go.transform.SetParent(parent, false);
        var p = go.AddComponent<StandeePuppet>();
        p._root = root;
        p._body = new GameObject("Body").transform;
        p._body.SetParent(go.transform, false);
        p._legL = MakeSr("LegL", p._body, order);
        p._legR = MakeSr("LegR", p._body, order);
        p._up = MakeSr("Upper", p._body, order + 1);
        p._seed = Random.value * 10f;
        go.SetActive(false);
        return p;
    }

    private static SpriteRenderer MakeSr(string name, Transform parent, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = order;
        return sr;
    }

    private static void LoadMeta()
    {
        if (_meta != null) return;
        _meta = new Dictionary<string, Meta>();
        var ta = Resources.Load<TextAsset>("Chara/cut_meta");
        if (ta == null) return;
        foreach (var line in ta.text.Split('\n'))
        {
            var f = line.Trim().Split(' ');
            if (f.Length < 4) continue;
            _meta[f[0]] = new Meta { X0 = int.Parse(f[1]), X1 = int.Parse(f[2]), Top = int.Parse(f[3]) };
        }
    }

    // 絵を設定する。height=画面上の身長(ワールド単位)
    public void SetSprite(Sprite cut, float height)
    {
        if (cut == null) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);
        LoadMeta();
        string id = cut.name;
        Vector2 b = cut.bounds.size;
        _scale = Mathf.Min(height / b.y, height * 0.95f / b.x);
        _body.localScale = new Vector3(_scale, _scale, 1f);
        Meta m;
        bool hasMeta = _meta.TryGetValue(id, out m);
        _legs = hasMeta && !NoLegs.Contains(id);
        float ppu = cut.pixelsPerUnit;
        if (!_legs)
        {
            _up.sprite = cut;
            _up.transform.localPosition = Vector3.zero;
            _legL.enabled = _legR.enabled = false;
            return;
        }
        Sprite[] parts;
        if (!Cache.TryGetValue(id, out parts) || parts[0] == null)
        {
            var tex = cut.texture;
            float mid = cut.rect.x + cut.rect.width * 0.5f;
            float x0 = cut.rect.x + m.X0, x1 = cut.rect.x + m.X1;
            float hip = Mathf.Round(m.Top * HipRatio), ov = Mathf.Round(m.Top * 0.08f);
            float y0 = cut.rect.y;
            var upR = new Rect(x0, y0 + hip - ov, x1 - x0, m.Top - (hip - ov));
            var lR = new Rect(x0, y0, Mathf.Max(1f, mid - x0), hip);
            var rR = new Rect(mid, y0, Mathf.Max(1f, x1 - mid), hip);
            parts = new Sprite[3];
            parts[0] = Sprite.Create(tex, upR, new Vector2((mid - x0) / upR.width, ov / upR.height), ppu, 0, SpriteMeshType.FullRect);
            parts[1] = Sprite.Create(tex, lR, new Vector2(1f, 1f), ppu, 0, SpriteMeshType.FullRect);
            parts[2] = Sprite.Create(tex, rR, new Vector2(0f, 1f), ppu, 0, SpriteMeshType.FullRect);
            foreach (var sp in parts) sp.hideFlags = HideFlags.HideAndDontSave;
            Cache[id] = parts;
        }
        float hipY = Mathf.Round(m.Top * HipRatio) / ppu;
        _up.sprite = parts[0];
        _legL.sprite = parts[1];
        _legR.sprite = parts[2];
        _legL.enabled = _legR.enabled = true;
        _up.transform.localPosition = new Vector3(0f, hipY, 0f);
        _legL.transform.localPosition = new Vector3(0f, hipY, 0.001f);
        _legR.transform.localPosition = new Vector3(0f, hipY, 0.001f);
    }

    public void Catch() { _act = 1; _actT = 0f; _actDur = 0.45f; }
    public void Throw() { _act = 2; _actT = 0f; _actDur = 0.50f; }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        float g = BattingConfig.GunyaLevel;
        Vector3 pos = _root != null ? _root.position : transform.position;
        float speed = _hasLast ? Vector3.Distance(pos, _last) / dt : 0f;
        _last = pos; _hasLast = true;
        _run = Mathf.MoveTowards(_run, Mathf.Clamp01(speed / 4f), dt * 6f);
        if (DebugRun >= 0f) { _run = DebugRun; speed = 6f * DebugRun; }
        _phase += dt * (7f + Mathf.Min(speed, 10f) * 0.9f);
        float t = Time.time + _seed;

        float legA = Mathf.Sin(_phase) * 28f * _run * (0.7f + 0.3f * g);
        float bob = Mathf.Abs(Mathf.Sin(_phase)) * (_legs ? 0.10f : 0.22f) * _run;
        float lean = -7f * _run;
        float sway = Mathf.Sin(t * 1.9f) * 1.6f * (1f - _run);
        float breathe = 1f + 0.014f * Mathf.Sin(t * 2.6f) * (1f - _run);
        float upRot = lean + sway, sx = 1f, sy = breathe, spread = 0f;

        if (_act != 0)
        {
            _actT += dt;
            float u = Mathf.Clamp01(_actT / _actDur);
            if (_act == 1)
            {
                // 捕る: 伸び上がって、捕った瞬間にぐっと縮む
                float up = Mathf.Sin(Mathf.Min(1f, u * 2f) * Mathf.PI * 0.5f) * (1f - u);
                sy *= 1f + 0.16f * up * g - 0.10f * Mathf.Sin(u * Mathf.PI) * (u > 0.5f ? 1f : 0f);
                sx *= 1f - 0.06f * up;
                spread = 14f * Mathf.Sin(u * Mathf.PI);
            }
            else
            {
                // 投げる: 後ろへ溜めてから前へ振り抜く
                float a = u < 0.35f ? Mathf.Lerp(0f, 22f, u / 0.35f)
                        : (u < 0.6f ? Mathf.Lerp(22f, -30f, (u - 0.35f) / 0.25f) : Mathf.Lerp(-30f, 0f, (u - 0.6f) / 0.4f));
                upRot += a * (0.8f + 0.2f * g);
                spread = 10f * Mathf.Sin(u * Mathf.PI);
            }
            if (u >= 1f) _act = 0;
        }

        _body.localPosition = new Vector3(0f, bob, 0f);
        if (_legs)
        {
            _up.transform.localRotation = Quaternion.Euler(0f, 0f, upRot);
            _up.transform.localScale = new Vector3(sx, sy, 1f);
            _legL.transform.localRotation = Quaternion.Euler(0f, 0f, legA - spread);
            _legR.transform.localRotation = Quaternion.Euler(0f, 0f, -legA + spread);
        }
        else
        {
            _up.transform.localRotation = Quaternion.Euler(0f, 0f, upRot * 0.6f);
            _up.transform.localScale = new Vector3(sx * (1f + 0.04f * Mathf.Sin(_phase * 2f) * _run), sy, 1f);
        }
    }
}
