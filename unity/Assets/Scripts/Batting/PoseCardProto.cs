using System.Collections.Generic;
using UnityEngine;

// ============================================================
// PoseCardProto.cs
// 決めポーズの絵(構え・振り切り・投げ終わり)と横向きの走り絵(2コマ)を、その瞬間だけ体の代わりに出す。
// 絵は Resources/Chara/Pose/<id>_<pose>.png と pose_meta.txt(Tools/chara-rig/prep_poses.py が Aポーズと同じ縮尺で作る)。
// Enabled=false で全員を体(物理)だけに戻せる。
// ============================================================
public class PoseCardProto : MonoBehaviour
{
    public static bool Enabled = true;

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
        var ta = Resources.Load<TextAsset>("Chara/pose_meta");
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
        var src = Resources.Load<Sprite>("Chara/Pose/" + name);
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
        bool batter = p.Bat != null;
        c._ready = batter ? Make(id + "_swing_ready", p.Skin) : null;
        c._follow = batter ? Make(id + "_swing_finish", p.Skin) : null;
        c._release = batter ? null : Make(id + "_pitch_release", p.Skin);
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
            if (ts >= 0.06f && ts < 0.42f) show = _follow;              // 振り抜いた直後(振り出しの一瞬は物理の体で回す)
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
