using System.Collections.Generic;
using UnityEngine;

// ============================================================
// CharaCatalog.cs
// キャラ50体の表(ID=画像ファイル名 / 表示名 / レア度)。
// 画像は Resources/Chara/Full(拡大用) と Resources/Chara/Thumb(一覧用 2:3)。
// レア度ごとの重ね上限・表示色もここで決める。
// ============================================================
public enum Rarity { R1 = 1, R2 = 2, R3 = 3, R4 = 4, R5 = 5, Secret = 6 }

public class CharaDef
{
    public string Id;
    public string Name;
    public Rarity Rarity;
    public int Index;
}

public static class CharaCatalog
{
    public static readonly List<CharaDef> All = new List<CharaDef>();
    private static readonly Dictionary<string, CharaDef> ById = new Dictionary<string, CharaDef>();

    static CharaCatalog()
    {
        Add("alien", "宇宙人エース", Rarity.Secret);

        Add("heroine", "ヒロイン", Rarity.R5);
        Add("robot-gold", "黄金ブリキロボ", Rarity.R5);
        Add("legend-grandpa", "伝説の90歳", Rarity.R5);
        Add("dragon", "ドラゴン選手", Rarity.R5);

        Add("ojisan", "熱血おじさんキャプテン", Rarity.R4);
        Add("dog", "柴犬マスコット", Rarity.R4);
        Add("robot", "ブリキロボット", Rarity.R4);
        Add("sumo", "力士系巨漢", Rarity.R4);
        Add("twins", "双子選手", Rarity.R4);
        Add("ninja", "忍者選手", Rarity.R4);
        Add("idol", "アイドル兼任選手", Rarity.R4);

        Add("umpire", "審判おばちゃん", Rarity.R3);
        Add("grandma", "ナックルおばあちゃん", Rarity.R3);
        Add("ceo", "社長選手", Rarity.R3);
        Add("rookie", "ランドセルルーキー", Rarity.R3);
        Add("magical", "自称・魔法少女", Rarity.R3);
        Add("helper", "外国人助っ人", Rarity.R3);
        Add("fisherman", "漁師選手", Rarity.R3);
        Add("carpenter", "大工選手", Rarity.R3);
        Add("fortuneteller", "占い師選手", Rarity.R3);
        Add("stationstaff", "駅員選手", Rarity.R3);

        Add("ghost", "幽霊選手", Rarity.R2);
        Add("salaryman", "兼業サラリーマン", Rarity.R2);
        Add("sleepyhead", "寝坊助エース", Rarity.R2);
        Add("bigeater", "早食い選手", Rarity.R2);
        Add("slacker", "サボり魔ベテラン", Rarity.R2);
        Add("rainman", "雨男選手", Rarity.R2);
        Add("magician", "マジシャン選手", Rarity.R2);
        Add("chef", "料理人選手", Rarity.R2);
        Add("doctor", "医者選手", Rarity.R2);
        Add("teacher", "先生選手", Rarity.R2);
        Add("dad", "パパ選手", Rarity.R2);
        Add("thirdtwin", "双子の三男", Rarity.R2);

        Add("suntan", "日焼け選手", Rarity.R1);
        Add("student", "学生", Rarity.R1);
        Add("housewife", "主婦", Rarity.R1);
        Add("deliveryman", "配達員", Rarity.R1);
        Add("guard", "警備員", Rarity.R1);
        Add("farmer", "農家", Rarity.R1);
        Add("monk", "僧侶", Rarity.R1);
        Add("pumpkin", "かぼちゃ男", Rarity.R1);
        Add("snowman", "雪だるま", Rarity.R1);
        Add("tanuki", "たぬき", Rarity.R1);
        Add("cat", "猫", Rarity.R1);
        Add("crow", "カラス", Rarity.R1);
        Add("mannequin", "マネキン", Rarity.R1);
        Add("dogu", "土偶", Rarity.R1);
        Add("santa", "サンタ", Rarity.R1);
        Add("deliveryrobot", "配達ロボ", Rarity.R1);
    }

    private static void Add(string id, string name, Rarity r)
    {
        var d = new CharaDef { Id = id, Name = name, Rarity = r, Index = All.Count };
        All.Add(d);
        ById[id] = d;
    }

    public static CharaDef Get(string id)
    {
        CharaDef d;
        return ById.TryGetValue(id, out d) ? d : null;
    }

    public static List<CharaDef> OfRarity(Rarity r)
    {
        var list = new List<CharaDef>();
        foreach (var d in All) if (d.Rarity == r) list.Add(d);
        return list;
    }

    // 重ね上限(★1=10 … ★5=6 / SECRET=5)
    public static int StackMax(Rarity r)
    {
        switch (r)
        {
            case Rarity.R1: return 10;
            case Rarity.R2: return 9;
            case Rarity.R3: return 8;
            case Rarity.R4: return 7;
            case Rarity.R5: return 6;
            default: return 5;
        }
    }

    public static string Label(Rarity r)
    {
        if (r == Rarity.Secret) return "SECRET";
        return new string('★', (int)r);
    }

    // 枠の色(金×赤の路線。低レアは落ち着いた色、上ほど金と赤が強い)
    public static Color FrameColor(Rarity r)
    {
        switch (r)
        {
            case Rarity.R1: return new Color(0.62f, 0.64f, 0.68f);
            case Rarity.R2: return new Color(0.72f, 0.47f, 0.28f);
            case Rarity.R3: return new Color(0.28f, 0.52f, 0.92f);
            case Rarity.R4: return new Color(0.91f, 0.73f, 0.14f);
            case Rarity.R5: return new Color(0.90f, 0.16f, 0.16f);
            default: return new Color(0.98f, 0.90f, 0.55f);
        }
    }

    // ---- 画像の読み込み(拡大用は必要なときだけ読む) ----
    private static readonly Dictionary<string, Sprite> Thumbs = new Dictionary<string, Sprite>();

    public static Sprite Thumb(string id)
    {
        Sprite s;
        if (Thumbs.TryGetValue(id, out s) && s != null) return s;
        s = Resources.Load<Sprite>("Chara/Thumb/" + id);
        Thumbs[id] = s;
        return s;
    }

    public static Sprite Full(string id)
    {
        return Resources.Load<Sprite>("Chara/Full/" + id);
    }
}
