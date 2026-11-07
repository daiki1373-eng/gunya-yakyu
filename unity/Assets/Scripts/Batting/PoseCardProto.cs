using System.Collections.Generic;
using UnityEngine;

// ============================================================
// PoseCardProto.cs(試作・フェーズE)
// 決めポーズの絵(構え・スイング途中・投げ終わり)と横向きの走り絵を、その瞬間だけ体の代わりに出して比べるための仕組み。
// 絵は Resources/Presentation/proto_<id>_<pose>.png(Tools/presentation/prep.py が Aポーズと同じ台紙・縮尺で作る)。
// Enabled=false(既定)なら何もしない。全キャラへ広げるかは田端の判断待ち。
// ============================================================
public class PoseCardProto : MonoBehaviour
{
    public static bool Enabled = false;

    private static Dictionary<string, Vector2Int> _meta;   // 絵の名前 → (足の下端の画素, 体の高さの画素)

    private RagdollPlayer _p;
    private RigSkin _skin;
    private SpriteRenderer _sr, _batSr;
    private Sprite _ready, _follow, _release;
    private float _swingT = -99f, _pitchT = -99f;
    private bool _shown;

    private static void LoadMeta()
    {
        if (_meta != null) return;
        _meta = new Dictionary<string, Vector2Int>();
        var ta = Resources.Load<TextAsset>("Presentation/proto_meta");
        if (ta == null) return;
        foreach (var line in ta.text.Split('\n'))
        {
            var f = line.Trim().Split(' ');
            if (f.Length >= 3) _meta[f[0]] = new Vector2Int(int.Parse(f[1]), int.Parse(f[2]));
        }
    }

    // Aポーズと同じ縮尺で、足の下端を足元に合わせたスプライトを作る。heightScale は体の高さの倍率(走り絵の大きさ合わせ用)
    public static Sprite Make(string name, RigSkin skin, float standPx = 0f)
    {
        LoadMeta();
        var src = Resources.Load<Sprite>("Presentation/" + name);
        Vector2Int m;
        if (src == null || skin == null || !_meta.TryGetValue(name, out m)) return null;
        Rect r = src.rect;
        float ppu = 1f / skin.PixelWorld;
        if (standPx > 0f) ppu *= m.y / standPx;          // 走り絵: 立ち姿の高さのおよそ9割にそろえる
        var sp = Sprite.Create(src.texture, r, new Vector2(0.5f, 1f - m.x / r.height), ppu, 0, SpriteMeshType.FullRect);
        sp.name = name;
        return sp;
    }

    public static PoseCardProto TryAttach(RagdollPlayer p, string id)
    {
        if (!Enabled || p == null || p.Skin == null || !p.Skin.UsesBodyArt) return null;
        var c = p.gameObject.AddComponent<PoseCardProto>();
        c._p = p; c._skin = p.Skin;
        c._ready = Make("proto_" + id + "_swing_ready", p.Skin);
        c._follow = Make("proto_" + id + "_swing_follow", p.Skin);
        c._release = Make("proto_" + id + "_pitch_release", p.Skin);
        if (c._ready == null && c._release == null) { Destroy(c); return null; }
        var go = new GameObject("PoseCard");
        go.transform.SetParent(p.Skin.transform.parent, false);
        go.transform.localPosition = new Vector3(p.Skin.FootLocal.x, p.Skin.FootLocal.y, -0.02f);
        go.layer = p.Skin.gameObject.layer;
        c._sr = go.AddComponent<SpriteRenderer>();
        c._sr.sortingOrder = 7;
        c._sr.flipX = p.Facing < 0f;              // 絵は右向き
        c._sr.enabled = false;
        if (p.Bat != null) c._batSr = p.Bat.GetComponentInChildren<SpriteRenderer>();
        return c;
    }

    public void OnSwing() { _swingT = Time.time; _waiting = false; }
    public void OnReset() { _waiting = true; }   // 投球ごとの構え直し: 次に振るまで構えの絵
    private bool _waiting;
    public void OnPitch() { _pitchT = Time.time; }

    private void LateUpdate()
    {
        if (_p == null || _skin == null) return;
        Sprite show = null;
        bool batter = _p.Bat != null;
        float now = Time.time;
        if (batter)
        {
            float ts = now - _swingT;
            // 倒れていなければ(構えでかがんでいても)構えの絵。転倒・のけぞりの間は体(物理)のまま
            bool standing = Mathf.Abs(Mathf.DeltaAngle(_p.Chest.rotation, 0f)) < 55f && _p.Pelvis.position.y > _p.transform.position.y + 0.45f;
            if (ts >= 0f && ts < 0.10f) show = _follow;                 // 振り出しの一瞬
            else if (_waiting && !_p.BatThrown) show = _ready;               // 投球を待つ間は構え(物理のかがんだ構えの代わり)
            else if (ts > 1.2f && standing && !_p.BatThrown) show = _ready;  // 打席に入るまでなど
        }
        else
        {
            float tp = now - _pitchT;
            if (tp >= BattingConfig.WindupSeconds - 0.12f && tp < BattingConfig.WindupSeconds + 0.40f) show = _release;
        }
        bool on = show != null;
        if (on) _sr.sprite = show;
        if (on == _shown) return;
        _shown = on;
        _sr.enabled = on;
        _skin.transform.localScale = on ? Vector3.zero : Vector3.one;     // 体(顔・グローブを含む)を隠す
        if (_batSr != null) _batSr.enabled = !on;                         // 絵にバットが描かれている
    }
}
