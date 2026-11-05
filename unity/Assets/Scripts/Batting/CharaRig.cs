using System.Collections.Generic;
using UnityEngine;

// 元絵の関節位置(Resources/Chara/rig.txt)。Tools/chara-rig で自動生成+手直し。
// 座標は Cut 画像の画素(左上原点)。
public class CharaRig
{
    public string Id;
    public Vector2 Size = new Vector2(512, 512);
    public Vector4 Box;            // x0 y0 x1 y1
    public float Scale = 1f;      // 体の大きさの補正(上半身だけの絵など)
    public bool FrontLeft;         // 前に出す腕(打者=振る腕/投手=投げる腕)が画像の左側
    public readonly Dictionary<string, Vector2> J = new Dictionary<string, Vector2>();

    private static Dictionary<string, CharaRig> _all;

    public static CharaRig Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_all == null) Load();
        CharaRig r;
        return _all.TryGetValue(id, out r) ? r : null;
    }

    public bool Has(string n) { return J.ContainsKey(n); }

    private static void Load()
    {
        _all = new Dictionary<string, CharaRig>();
        var ta = Resources.Load<TextAsset>("Chara/rig");
        if (ta == null) return;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var raw in ta.text.Split('\n'))
        {
            string line = raw;
            int hash = line.IndexOf('#');
            if (hash >= 0) line = line.Substring(0, hash);
            var p = line.Trim().Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 3) continue;
            CharaRig r;
            if (!_all.TryGetValue(p[0], out r)) { r = new CharaRig { Id = p[0] }; _all[p[0]] = r; }
            if (p[1] == "flag") { if (p[2] == "frontL") r.FrontLeft = true; continue; }
            if (p[1] == "scale") { r.Scale = float.Parse(p[2], inv); continue; }
            if (p[1] == "size" && p.Length >= 4) { r.Size = new Vector2(float.Parse(p[2], inv), float.Parse(p[3], inv)); continue; }
            if (p[1] == "box" && p.Length >= 6)
            {
                r.Box = new Vector4(float.Parse(p[2], inv), float.Parse(p[3], inv), float.Parse(p[4], inv), float.Parse(p[5], inv));
                continue;
            }
            if (p.Length >= 4) r.J[p[1]] = new Vector2(float.Parse(p[2], inv), float.Parse(p[3], inv));
        }
    }
}
