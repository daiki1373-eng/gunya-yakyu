using UnityEngine;

// ============================================================
// PitchRepertoire.cs
// 球種の定義(P2)。直球・カーブ・スライダー・フォーク・チェンジアップ。
// 球速差(到達時間)と、手元での曲がり(LateK)で表現する。数値はすべて自前の仮置き。
//
// 軌道: 「変化がなければ着く点」へ直進しつつ、s^LateK に比例して Break だけ曲がり、
// ちょうど到達位置(ゾーン面の Target)に着く。LateK が大きいほど手元で曲がる。
// ============================================================
public enum PitchKind { Fastball = 0, Curve = 1, Slider = 2, Fork = 3, Changeup = 4 }

public struct PitchDef
{
    public PitchKind Kind;
    public string Name;
    public float SpeedFactor;   // 直球に対する球速比
    public Vector2 Break;       // 到達までの総変化量(m)。x=+が一塁側(右)、y=+が上
    public float LateK;         // 大きいほど手元で曲がる
    public int MinBrk;          // 投げられる変化球能力の下限
    public float Weight;        // 基本の選択重み
}

public static class PitchRepertoire
{
    public static readonly PitchDef[] All = new PitchDef[]
    {
        new PitchDef { Kind = PitchKind.Fastball, Name = "ストレート",   SpeedFactor = 1.00f, Break = new Vector2( 0.00f,  0.00f), LateK = 2.0f, MinBrk =  0, Weight = 40f },
        new PitchDef { Kind = PitchKind.Curve,    Name = "カーブ",       SpeedFactor = 0.76f, Break = new Vector2( 0.20f, -0.62f), LateK = 2.0f, MinBrk = 20, Weight = 14f },
        new PitchDef { Kind = PitchKind.Slider,   Name = "スライダー",   SpeedFactor = 0.90f, Break = new Vector2( 0.50f, -0.14f), LateK = 2.4f, MinBrk = 35, Weight = 22f },
        new PitchDef { Kind = PitchKind.Fork,     Name = "フォーク",     SpeedFactor = 0.86f, Break = new Vector2( 0.00f, -0.66f), LateK = 3.0f, MinBrk = 55, Weight = 12f },
        new PitchDef { Kind = PitchKind.Changeup, Name = "チェンジアップ", SpeedFactor = 0.82f, Break = new Vector2(-0.14f, -0.28f), LateK = 1.8f, MinBrk = 30, Weight = 12f },
    };

    public static PitchDef Get(PitchKind k)
    {
        return All[(int)k];
    }

    // 表示球速(km/h)→実際の到達時間(秒)。直球130→0.65、155→0.45。遅い球ほど長く、上限0.86。
    public static float FlightTimeFor(float kmh)
    {
        return Mathf.Clamp(0.65f - 0.008f * (kmh - 130f), BattingConfig.FlightFast, 0.86f);
    }

    // 変化球能力が高いほど曲がりが大きい(0.75〜1.25倍)
    public static float BreakScale(PlayerStats p)
    {
        return 0.75f + 0.5f * Mathf.InverseLerp(5f, 99f, p.Brk);
    }
}
