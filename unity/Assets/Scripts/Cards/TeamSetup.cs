using System.Collections.Generic;
using UnityEngine;

// ============================================================
// TeamSetup.cs
// 編成: 投手1人と、打順つきの9人(捕手・一塁・二塁・三塁・遊撃・左翼・中堅・右翼・指名打者)。
// 各枠には、その守備位置の選手だけが入れる(カードの守備: 投手/捕手/内野手/外野手/打者専門)。
//   一塁〜遊撃=内野手、左翼〜右翼=外野手、指名打者=打者専門。
// 選べるのは所持カードと、打線を埋める選手(CharaRoster.FreeAgents)。
// ============================================================
public enum Slot { P, C, B1, B2, B3, SS, LF, CF, RF, DH }

public static class TeamSetup
{
    private const string Key = "gunya.lineup2";
    public static readonly string[] SlotNames = { "投手", "捕手", "一塁", "二塁", "三塁", "遊撃", "左翼", "中堅", "右翼", "指名打者" };
    public static readonly string[] SlotShort = { "投", "捕", "一", "二", "三", "遊", "左", "中", "右", "指" };

    public class Entry { public CharaDef D; public Slot S; }
    public class Lineup { public CharaDef Pitcher; public List<Entry> Order = new List<Entry>(); }

    public static string PositionOf(Slot s)
    {
        switch (s)
        {
            case Slot.P: return "投手";
            case Slot.C: return "捕手";
            case Slot.B1: case Slot.B2: case Slot.B3: case Slot.SS: return "内野手";
            case Slot.LF: case Slot.CF: case Slot.RF: return "外野手";
            default: return "打者専門";
        }
    }

    public static bool Fits(CharaDef d, Slot s) { return d != null && d.Position == PositionOf(s); }

    public static bool Usable(CharaDef d)
    {
        if (d == null) return false;
        if (Collection.Owned(d.Id)) return true;
        foreach (var id in CharaRoster.FreeAgents) if (id == d.Id) return true;
        return false;
    }

    // その枠に入れられる選手: 所持(図鑑の順=レアな順)→ 打線を埋める選手
    public static List<CharaDef> Candidates(Slot s)
    {
        var list = new List<CharaDef>();
        foreach (var d in CharaCatalog.All)
            if (Fits(d, s) && Collection.Owned(d.Id)) list.Add(d);
        foreach (var id in CharaRoster.FreeAgents)
        {
            var d = CharaCatalog.Get(id);
            if (Fits(d, s) && !list.Contains(d)) list.Add(d);
        }
        return list;
    }

    public static Lineup Load()
    {
        var l = LoadSaved();
        return l ?? Default();
    }

    // 自動で組む: 各枠にその位置のいちばんレアな選手(重ならないように)→ 打順はタイプで並べる
    public static Lineup Default()
    {
        var used = new HashSet<CharaDef>();
        var l = new Lineup();
        var p = Candidates(Slot.P);
        l.Pitcher = p.Count > 0 ? p[0] : CharaCatalog.Get("sleepyhead");
        var entries = new List<Entry>();
        foreach (Slot s in new[] { Slot.C, Slot.B1, Slot.B2, Slot.B3, Slot.SS, Slot.LF, Slot.CF, Slot.RF, Slot.DH })
        {
            CharaDef pick = null;
            foreach (var d in Candidates(s)) if (!used.Contains(d)) { pick = d; break; }
            if (pick == null) continue;
            used.Add(pick);
            entries.Add(new Entry { D = pick, S = s });
        }
        var defs = new List<CharaDef>();
        foreach (var e in entries) defs.Add(e.D);
        CharaRoster.Order(defs);
        foreach (var d in defs) l.Order.Add(entries.Find(e => e.D == d));
        return l;
    }

    // 保存の形: "P=id;C=id;B2=id;..."(先頭が投手、続く9つが打順)
    private static Lineup LoadSaved()
    {
        string raw = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(raw)) return null;
        var parts = raw.Split(';');
        if (parts.Length != 10) return null;
        var l = new Lineup();
        var seen = new HashSet<CharaDef>();
        var slots = new HashSet<Slot>();
        for (int i = 0; i < parts.Length; i++)
        {
            var kv = parts[i].Split('=');
            if (kv.Length != 2) return null;
            Slot s;
            try { s = (Slot)System.Enum.Parse(typeof(Slot), kv[0]); } catch (System.Exception) { return null; }
            var d = CharaCatalog.Get(kv[1]);
            if (!Fits(d, s) || !Usable(d) || !seen.Add(d) || !slots.Add(s)) return null;
            if (i == 0) { if (s != Slot.P) return null; l.Pitcher = d; }
            else
            {
                if (s == Slot.P) return null;
                l.Order.Add(new Entry { D = d, S = s });
            }
        }
        return l;
    }

    public static void Save(Lineup l)
    {
        if (l == null || l.Pitcher == null || l.Order.Count != 9) return;
        var sb = new System.Text.StringBuilder();
        sb.Append("P=").Append(l.Pitcher.Id);
        foreach (var e in l.Order) sb.Append(';').Append(e.S).Append('=').Append(e.D.Id);
        PlayerPrefs.SetString(Key, sb.ToString());
        PlayerPrefs.Save();
    }
}
