using UnityEngine;

// ============================================================
// PlayerData.cs
// HTML版「甲子園への道」(landscape/claude-v.html) のデータ定義を移植したもの。
// 能力値の項目・種族補正・固有キャラ・球種テーブル・必殺技はHTML版が正。
// 数値の考え方は各所のコメントに残してある。
// ============================================================

// 選手の能力値。HTML版の player オブジェクトに相当。
// 項目: meet(ミート) power(パワー) speed(走力) field(守備) arm(肩)
//       velo(球速) ctrl(制球) brk(変化球) stam(スタミナ)。範囲は 5〜99。
public class PlayerStats
{
    public string Name = "";
    public int Position = 1;          // 1=投手,2=捕手,3=一塁手,4=二塁手,5=三塁手,6=遊撃手,7=左翼手,8=中堅手,9=右翼手
    public string Species = "human";  // "human" / "beetle" / "dog" / "panda"
    public bool BatsRight = true;
    public bool ThrowsRight = true;

    public int Meet = 40;
    public int Power = 40;
    public int Speed = 40;
    public int Field = 40;
    public int Arm = 40;
    public int Velo = 40;
    public int Ctrl = 40;
    public int Brk = 20;
    public int Stam = 40;

    public PlayerStats Clone()
    {
        return (PlayerStats)MemberwiseClone();
    }
}

// 種族補正。HTML版 SPEC / applySp の移植。
// 補正は「生成時に一度だけ」適用し、全項目を 5〜99 にクランプする。
public static class SpeciesTable
{
    public static string DisplayName(string species)
    {
        switch (species)
        {
            case "beetle": return "カブトムシ";
            case "dog": return "イヌ";
            case "panda": return "パンダ";
            default: return "人間";
        }
    }

    public static void Apply(PlayerStats p)
    {
        if (p == null) return;
        switch (p.Species)
        {
            case "beetle": // カブトムシ: 長打特化・鈍足
                p.Power += 16; p.Meet += 4; p.Speed -= 14;
                break;
            case "dog": // イヌ: 俊足・守備
                p.Speed += 16; p.Field += 6; p.Meet += 2; p.Power -= 8;
                break;
            case "panda": // パンダ: 変化・制球型
                p.Velo -= 8; p.Ctrl += 14; p.Brk += 16; p.Stam += 4;
                break;
            default: // 人間: 補正なし
                break;
        }
        p.Meet = Mathf.Clamp(p.Meet, 5, 99);
        p.Power = Mathf.Clamp(p.Power, 5, 99);
        p.Speed = Mathf.Clamp(p.Speed, 5, 99);
        p.Field = Mathf.Clamp(p.Field, 5, 99);
        p.Arm = Mathf.Clamp(p.Arm, 5, 99);
        p.Velo = Mathf.Clamp(p.Velo, 5, 99);
        p.Ctrl = Mathf.Clamp(p.Ctrl, 5, 99);
        p.Brk = Mathf.Clamp(p.Brk, 5, 99);
        p.Stam = Mathf.Clamp(p.Stam, 5, 99);
    }
}

// 固有キャラ3名。HTML版 SPCH の移植。
// ここに書くのは素の能力値で、生成時に SpeciesTable.Apply を通す。
// (例: カブト丸の実効パワーは 58+16=74、パン太の実効変化は 46+16=62)
public static class SpecialCharacters
{
    public static PlayerStats Kabutomaru()
    {
        var p = new PlayerStats
        {
            Name = "カブト丸", Position = 3, Species = "beetle",
            Meet = 48, Power = 58, Speed = 44, Field = 42, Arm = 44,
            Velo = 30, Ctrl = 30, Brk = 10, Stam = 45
        };
        SpeciesTable.Apply(p);
        return p;
    }

    public static PlayerStats Wanta()
    {
        var p = new PlayerStats
        {
            Name = "ワンタ", Position = 8, Species = "dog",
            Meet = 53, Power = 46, Speed = 56, Field = 56, Arm = 48,
            Velo = 32, Ctrl = 30, Brk = 10, Stam = 48
        };
        SpeciesTable.Apply(p);
        return p;
    }

    public static PlayerStats Panta()
    {
        var p = new PlayerStats
        {
            Name = "パン太", Position = 1, Species = "panda",
            Meet = 30, Power = 28, Speed = 35, Field = 40, Arm = 42,
            Velo = 56, Ctrl = 44, Brk = 46, Stam = 51
        };
        SpeciesTable.Apply(p);
        return p;
    }

    // 主人公の初期値。HTML版 newHero の素の能力値。
    public static PlayerStats HeroDefault()
    {
        var p = new PlayerStats
        {
            Name = "主人公", Position = 4, Species = "human",
            Meet = 38, Power = 36, Speed = 40, Field = 40, Arm = 40,
            Velo = 40, Ctrl = 38, Brk = 20, Stam = 40
        };
        SpeciesTable.Apply(p);
        return p;
    }
}

// 球種。HTML版 PT の移植。
// SpeedFactor: 球速への倍率。BreakX/BreakY: 変化量(HTML版 bx/by)。
// MinBrk: 投げられるようになる変化球能力の下限(HTML版 th)。
public struct PitchType
{
    public string Name;
    public float SpeedFactor;
    public float BreakX;
    public float BreakY;
    public int MinBrk;
    public int Weight; // 選択の重み(簡略化した配球)

    public PitchType(string name, float sp, float bx, float by, int minBrk, int weight)
    {
        Name = name; SpeedFactor = sp; BreakX = bx; BreakY = by; MinBrk = minBrk; Weight = weight;
    }
}

public static class PitchTable
{
    public static readonly PitchType[] All = new PitchType[]
    {
        new PitchType("ストレート", 1.00f,  0.00f,  0.05f,  0, 3),
        new PitchType("カーブ",     0.76f,  0.55f, -1.25f, 20, 2),
        new PitchType("スライダー", 0.90f,  0.95f, -0.35f, 40, 2),
        new PitchType("フォーク",   0.85f,  0.00f, -1.60f, 60, 1),
        new PitchType("シュート",   0.95f, -0.85f, -0.25f, 80, 2),
    };

    // 投手の変化球能力で投げられる球種から重み付きで1つ選ぶ。
    public static PitchType Pick(int brk)
    {
        int total = 0;
        for (int i = 0; i < All.Length; i++)
            if (brk >= All[i].MinBrk) total += All[i].Weight;
        int r = Random.Range(0, Mathf.Max(1, total));
        for (int i = 0; i < All.Length; i++)
        {
            if (brk < All[i].MinBrk) continue;
            r -= All[i].Weight;
            if (r < 0) return All[i];
        }
        return All[0];
    }

    // 球速(km/h)。HTML版: (105 + velo * 0.5) * 球種倍率。
    public static float SpeedKmh(PlayerStats pitcher, PitchType type)
    {
        return (105f + pitcher.Velo * 0.5f) * type.SpeedFactor;
    }

    // 球の威力(stuff)。HTML版 mkPitch の移植。
    // ストレートは球速寄り、変化球は変化量寄り。
    public static float Stuff(PlayerStats pitcher, PitchType type)
    {
        float breaking = (type.MinBrk > 0) ? pitcher.Brk * 0.4f : pitcher.Velo * 0.2f;
        return pitcher.Velo * 0.4f + pitcher.Ctrl * 0.2f + breaking;
    }
}

// 必殺技4種。HTML版 SPK の移植。効果の実装はフェーズ2。
// フェーズ1ではゲージ表示(プレースホルダ)と加算量だけを使う。
public static class SpecialMoves
{
    public const string VanishName = "消える魔球バニッシュ";       // 投球技
    public const string BoomerangName = "二重変化ブーメラン";       // 投球技
    public const string SpinName = "ぐるぐるトルネード打法";        // 打撃技
    public const string GiantName = "デカバットブレイカー";         // 打撃技

    // ゲージ加算量。HTML版 GGAIN の移植。
    public const int GainHit = 30;
    public const int GainHomeRun = 55;
    public const int GainStrikeout = 40;
    public const int GainStrike = 10;
    public const int GainOut = 15;
    public const int GaugeMax = 100;
}
