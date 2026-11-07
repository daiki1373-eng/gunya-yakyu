using System.Collections.Generic;
using UnityEngine;

// ============================================================
// TeamSetup.cs
// 編成(スタメン9人と打順)の保存と読み込み。投手は今までどおり自動(所持の中で一番レアな投手)。
// 選べるのは、所持している野手と、打線を埋める選手(CharaRoster.FreeAgents)。
// ============================================================
public static class TeamSetup
{
    private const string Key = "gunya.lineup";

    public static bool Selectable(CharaDef d)
    {
        if (d == null || !CharaRoster.Fields(d) || CharaRoster.Role(d) == CharaRole.Pitcher) return false;
        if (Collection.Owned(d.Id)) return true;
        foreach (var id in CharaRoster.FreeAgents) if (id == d.Id) return true;
        return false;
    }

    // 選べる選手: 所持の野手(図鑑の順)→ 打線を埋める選手
    public static List<CharaDef> Candidates()
    {
        var list = new List<CharaDef>();
        foreach (var d in CharaCatalog.All)
            if (Collection.Owned(d.Id) && Selectable(d)) list.Add(d);
        foreach (var id in CharaRoster.FreeAgents)
        {
            var d = CharaCatalog.Get(id);
            if (d != null && !list.Contains(d) && Selectable(d)) list.Add(d);
        }
        return list;
    }

    // 保存した9人(全員が今も選べるときだけ。そうでなければ null)
    public static List<CharaDef> SavedLineup()
    {
        string raw = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(raw)) return null;
        var ids = raw.Split(',');
        if (ids.Length != 9) return null;
        var list = new List<CharaDef>();
        foreach (var id in ids)
        {
            var d = CharaCatalog.Get(id);
            if (!Selectable(d) || list.Contains(d)) return null;
            list.Add(d);
        }
        return list;
    }

    public static void Save(List<CharaDef> lineup)
    {
        if (lineup == null || lineup.Count != 9) return;
        var ids = new string[9];
        for (int i = 0; i < 9; i++) ids[i] = lineup[i].Id;
        PlayerPrefs.SetString(Key, string.Join(",", ids));
        PlayerPrefs.Save();
    }
}
