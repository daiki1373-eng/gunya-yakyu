using System.Collections.Generic;
using UnityEngine;

// ============================================================
// CharaRoster.cs
// ガチャのキャラ50体を試合に出すための設定。
// - 役割(投手 / 長打 / 巧打 / 俊足)と能力(レア度が土台。数値は自前の仮置き)
// - 見た目(頭の色・動物の顔)と登場演出(レア度で格が上がる)
// - 打順: 自チーム=所持カードからレア度順に9人(足りない分はおまかせで補充)、
//          相手=残りから毎試合ランダムに9人。投手も同様。
// ============================================================
public enum CharaRole { Pitcher, Power, Contact, Speed }

public static class CharaRoster
{
    private static readonly Dictionary<string, CharaRole> Roles = new Dictionary<string, CharaRole>
    {
        // 投手
        { "alien", CharaRole.Pitcher }, { "dragon", CharaRole.Pitcher }, { "robot-gold", CharaRole.Pitcher },
        { "robot", CharaRole.Pitcher }, { "sleepyhead", CharaRole.Pitcher }, { "grandma", CharaRole.Pitcher },
        { "magician", CharaRole.Pitcher }, { "monk", CharaRole.Pitcher },
        // 長打
        { "sumo", CharaRole.Power }, { "ojisan", CharaRole.Power }, { "legend-grandpa", CharaRole.Power },
        { "carpenter", CharaRole.Power }, { "bigeater", CharaRole.Power }, { "fisherman", CharaRole.Power },
        { "farmer", CharaRole.Power }, { "suntan", CharaRole.Power }, { "santa", CharaRole.Power },
        { "dogu", CharaRole.Power }, { "pumpkin", CharaRole.Power }, { "snowman", CharaRole.Power },
        // 巧打
        { "heroine", CharaRole.Contact }, { "twins", CharaRole.Contact }, { "idol", CharaRole.Contact },
        { "ceo", CharaRole.Contact }, { "teacher", CharaRole.Contact }, { "doctor", CharaRole.Contact },
        { "student", CharaRole.Contact }, { "housewife", CharaRole.Contact }, { "chef", CharaRole.Contact },
        { "mannequin", CharaRole.Contact }, { "thirdtwin", CharaRole.Contact }, { "dad", CharaRole.Contact },
        { "guard", CharaRole.Contact }, { "crow", CharaRole.Contact }, { "salaryman", CharaRole.Contact },
        // 俊足
        { "ninja", CharaRole.Speed }, { "dog", CharaRole.Speed }, { "rookie", CharaRole.Speed },
        { "helper", CharaRole.Speed }, { "deliveryman", CharaRole.Speed }, { "stationstaff", CharaRole.Speed },
        { "cat", CharaRole.Speed }, { "tanuki", CharaRole.Speed }, { "ghost", CharaRole.Speed },
        { "slacker", CharaRole.Speed }, { "rainman", CharaRole.Speed }, { "magical", CharaRole.Speed },
        { "fortuneteller", CharaRole.Speed }, { "umpire", CharaRole.Speed }, { "deliveryrobot", CharaRole.Speed },
    };

    // 頭の色(画像の雰囲気に合わせた仮置き)
    private static readonly Dictionary<string, Color> Skins = new Dictionary<string, Color>
    {
        { "alien", new Color(0.55f, 0.85f, 0.35f) }, { "dragon", new Color(0.40f, 0.70f, 0.30f) },
        { "robot", new Color(0.72f, 0.74f, 0.78f) }, { "robot-gold", new Color(0.95f, 0.78f, 0.30f) },
        { "deliveryrobot", new Color(0.80f, 0.45f, 0.25f) }, { "mannequin", new Color(0.92f, 0.90f, 0.86f) },
        { "ghost", new Color(0.88f, 0.92f, 1.00f) }, { "snowman", new Color(0.97f, 0.98f, 1.00f) },
        { "pumpkin", new Color(0.98f, 0.55f, 0.12f) }, { "dogu", new Color(0.68f, 0.52f, 0.36f) },
        { "suntan", new Color(0.62f, 0.40f, 0.25f) }, { "helper", new Color(0.70f, 0.50f, 0.35f) },
        { "tanuki", new Color(0.62f, 0.45f, 0.30f) }, { "cat", new Color(0.98f, 0.90f, 0.78f) },
        { "crow", new Color(0.20f, 0.20f, 0.24f) }, { "dog", new Color(0.88f, 0.62f, 0.32f) },
    };

    public static CharaRole Role(CharaDef d)
    {
        CharaRole r;
        return Roles.TryGetValue(d.Id, out r) ? r : CharaRole.Contact;
    }

    private static int Base(Rarity r)
    {
        switch (r)
        {
            case Rarity.R1: return 38;
            case Rarity.R2: return 44;
            case Rarity.R3: return 50;
            case Rarity.R4: return 56;
            case Rarity.R5: return 63;
            default: return 70;
        }
    }

    private static int C(int v) { return Mathf.Clamp(v, 5, 99); }

    public static PlayerStats Stats(CharaDef d)
    {
        int b = Base(d.Rarity);
        var role = Role(d);
        int h = Mathf.Abs(d.Id.GetHashCode());
        int j = (h % 7) - 3;   // キャラごとの小さな個性(-3〜+3)
        var p = new PlayerStats
        {
            Name = d.Name, CharaId = d.Id, Species = "human",
            BatsRight = (h % 4) != 0,   // 4人に1人は左打ち
            Field = C(b + j), Arm = C(b - j), Stam = C(b), Ctrl = C(b - 8), Velo = C(b - 12), Brk = C(b - 20)
        };
        switch (role)
        {
            case CharaRole.Power: p.Meet = C(b - 4 + j); p.Power = C(b + 14 - j); p.Speed = C(b - 10); p.Position = 3; break;
            case CharaRole.Contact: p.Meet = C(b + 10 + j); p.Power = C(b - 6); p.Speed = C(b + j); p.Position = 4; break;
            case CharaRole.Speed: p.Meet = C(b + 4); p.Power = C(b - 10 + j); p.Speed = C(b + 16 - j); p.Position = 8; break;
            default:
                p.Position = 1; p.Meet = C(b - 20); p.Power = C(b - 20); p.Speed = C(b - 10);
                p.Velo = C(b + 6 + j); p.Ctrl = C(b + 2 - j); p.Brk = C(b + 6);
                if (d.Id == "grandma") { p.Velo = C(b - 14); p.Brk = C(b + 20); p.Ctrl = C(b + 8); }   // ナックル(遅い・よく曲がる)
                if (d.Id == "alien") p.Brk = 99;
                break;
        }
        if (d.Id == "dog" || d.Id == "tanuki" || d.Id == "cat") p.Species = "dog";
        Color sk;
        if (Skins.TryGetValue(d.Id, out sk)) p.SkinTint = sk;
        return p;
    }

    // ---- 打順・投手 ------------------------------------------------
    public class Team { public List<CharaDef> Lineup = new List<CharaDef>(); public CharaDef Pitcher; }


    public static Team UserTeam()
    {
        var t = new Team();
        var owned = new List<CharaDef>();
        var all = new List<CharaDef>();
        foreach (var d in CharaCatalog.All) { all.Add(d); if (Collection.Owned(d.Id)) owned.Add(d); }
        // 投手: 所持の中で一番レアな投手。いなければ「寝坊助エース」
        foreach (var d in owned) if (Role(d) == CharaRole.Pitcher) { t.Pitcher = d; break; }
        if (t.Pitcher == null) t.Pitcher = CharaCatalog.Get("sleepyhead");
        // 打者: 所持(レア度順)→ 足りなければおまかせ(★3〜★1の野手から)
        foreach (var d in owned) if (Role(d) != CharaRole.Pitcher && t.Lineup.Count < 9) t.Lineup.Add(d);
        string[] fallback = { "rookie", "ceo", "carpenter", "umpire", "teacher", "bigeater", "chef", "student", "suntan", "cat", "farmer", "guard" };
        foreach (var id in fallback)
        {
            if (t.Lineup.Count >= 9) break;
            var d = CharaCatalog.Get(id);
            if (!t.Lineup.Contains(d)) t.Lineup.Add(d);
        }
        Order(t.Lineup);
        return t;
    }

    public static Team OpponentTeam(Team user, int seed)
    {
        var rng = new System.Random(seed);
        var t = new Team();
        var pool = new List<CharaDef>();
        var pitchers = new List<CharaDef>();
        foreach (var d in CharaCatalog.All)
        {
            if (user.Lineup.Contains(d) || d == user.Pitcher) continue;
            if (d.Rarity >= Rarity.R5) continue;   // 相手は★4まで
            if (Role(d) == CharaRole.Pitcher) pitchers.Add(d); else pool.Add(d);
        }
        for (int i = 0; i < 9 && pool.Count > 0; i++)
        {
            int k = rng.Next(pool.Count);
            t.Lineup.Add(pool[k]);
            pool.RemoveAt(k);
        }
        t.Pitcher = pitchers.Count > 0 ? pitchers[rng.Next(pitchers.Count)] : CharaCatalog.Get("monk");
        Order(t.Lineup);
        return t;
    }

    // 打順: 1-2番に俊足・巧打、3-5番に長打、残りはそのまま
    private static void Order(List<CharaDef> lineup)
    {
        var speed = new List<CharaDef>(); var contact = new List<CharaDef>(); var power = new List<CharaDef>();
        foreach (var d in lineup)
        {
            var r = Role(d);
            if (r == CharaRole.Speed) speed.Add(d); else if (r == CharaRole.Power) power.Add(d); else contact.Add(d);
        }
        var result = new List<CharaDef>();
        if (speed.Count > 0) { result.Add(speed[0]); speed.RemoveAt(0); }
        if (contact.Count > 0) { result.Add(contact[0]); contact.RemoveAt(0); }
        foreach (var d in power) result.Add(d);
        foreach (var d in contact) result.Add(d);
        foreach (var d in speed) result.Add(d);
        lineup.Clear();
        lineup.AddRange(result);
    }

    // ---- 登場演出(レア度で格が上がる) ----------------------------------
    public static IntroDef Intro(CharaDef d)
    {
        var role = Role(d);
        var def = new IntroDef
        {
            CardColor = CharaCatalog.FrameColor(d.Rarity),
            Copy = CharaCatalog.Label(d.Rarity),
            Face = Expr.Focus,
        };
        switch (role)
        {
            case CharaRole.Power: def.Pose = IntroPose.BatPoint; break;
            case CharaRole.Speed: def.Pose = IntroPose.Jump; def.Face = Expr.Happy; break;
            case CharaRole.Contact: def.Pose = IntroPose.Wave; def.Face = Expr.Normal; break;
            default: def.Pose = IntroPose.Flex; break;
        }
        switch (d.Rarity)
        {
            case Rarity.R1: def.Jingle = new[] { 0, 4, 7 }; def.Cheer = 0.30f; break;
            case Rarity.R2: def.Jingle = new[] { 0, 4, 7, 12 }; def.Cheer = 0.40f; break;
            case Rarity.R3: def.Jingle = new[] { 0, 4, 7, 9, 12 }; def.Cheer = 0.55f; break;
            case Rarity.R4: def.Jingle = new[] { 0, 4, 7, 12, 7, 12, 16 }; def.Cheer = 0.75f; def.Fireworks = 2; break;
            case Rarity.R5: def.Jingle = new[] { 0, 4, 7, 12, -99, 12, 16, 19, 24 }; def.Cheer = 0.9f; def.Fireworks = 4; def.Pose = IntroPose.Spin; break;
            default: def.Jingle = new[] { 12, 7, 12, 16, 19, -99, 24, 19, 24 }; def.Cheer = 1f; def.Fireworks = 8; def.Pose = IntroPose.Spin; break;
        }
        return def;
    }
}
