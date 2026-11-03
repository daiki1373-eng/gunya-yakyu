using UnityEngine;

// ============================================================
// HitJudge.cs
// ミート判定の中核(純粋な計算。シーンに依存しないのでエディタから大量試行できる)。
//
// 判定材料は2つ:
//   ①ボール到達位置とカーソル中心の距離(楕円で正規化)
//   ②スイングのタイミング誤差(ms)
// それぞれを5段階にし、悪い方を最終ランクにする。
//   Perfect(芯) / Good / Normal / Weak / Whiff(空振り)
// ランクとズレ具合から打球の初速・角度・方向・スピンを決める。
// ============================================================
public enum HitRank { Whiff = 0, Weak = 1, Normal = 2, Good = 3, Perfect = 4 }

public struct SwingContext
{
    public Vector2 BallPos;      // ゾーン面に到達したボールの位置 (x,y)
    public Vector2 CursorPos;    // スイング瞬間のカーソル中心
    public float CursorHalfW;
    public float CursorHalfH;
    public float TimingMs;       // バットが届く時刻 - ボール到達時刻。負=早い、正=遅い
    public int Meet;
    public int Power;
    public int BatSide;          // -1=右打者(x<0側に立つ) / +1=左打者
}

public struct HitOutcome
{
    public HitRank Rank;
    public float EllipseDist;    // 芯からの正規化距離(0=ど真ん中)
    public float Quality;        // 0..1 打球の質
    public float ExitSpeed;      // m/s
    public float LaunchDeg;      // 打出角(上向き正)
    public float SprayDeg;       // 0=センター方向。+は一塁(右翼)側
    public float BackspinRpm;
    public float TiltDeg;        // スピン軸の傾き。+で右へ曲がる
    public bool FoulTip;         // ファウルチップ(真後ろへ)
}

public static class HitJudge
{
    public static float CursorHalfW(int meet)
    {
        float t = Mathf.InverseLerp(5f, 99f, meet);
        return Mathf.Lerp(BattingConfig.CursorHalfWMin, BattingConfig.CursorHalfWMax, t);
    }

    public static float CursorHalfH(int meet)
    {
        return CursorHalfW(meet) * BattingConfig.CursorAspect;
    }

    public static float EllipseDistance(Vector2 ball, Vector2 cursor, float hw, float hh)
    {
        float dx = (ball.x - cursor.x) / hw;
        float dy = (ball.y - cursor.y) / hh;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    public static HitRank RankFromEllipse(float e)
    {
        if (e <= BattingConfig.EllipsePerfect) return HitRank.Perfect;
        if (e <= BattingConfig.EllipseGood) return HitRank.Good;
        if (e <= BattingConfig.EllipseNormal) return HitRank.Normal;
        if (e <= BattingConfig.EllipseWeak) return HitRank.Weak;
        return HitRank.Whiff;
    }

    // ミートが高いほど窓が少し広い(0.9倍〜1.2倍)
    public static HitRank RankFromTiming(float timingMs, int meet)
    {
        float scale = 0.9f + 0.3f * Mathf.InverseLerp(5f, 99f, meet);
        float a = Mathf.Abs(timingMs) / scale;
        if (a <= BattingConfig.TimingPerfectMs) return HitRank.Perfect;
        if (a <= BattingConfig.TimingGoodMs) return HitRank.Good;
        if (a <= BattingConfig.TimingNormalMs) return HitRank.Normal;
        if (a <= BattingConfig.TimingWeakMs) return HitRank.Weak;
        return HitRank.Whiff;
    }

    private static void TierRange(HitRank r, out float lo, out float hi)
    {
        switch (r)
        {
            case HitRank.Perfect: lo = 0.80f; hi = 1.00f; break;
            case HitRank.Good: lo = 0.55f; hi = 0.82f; break;
            case HitRank.Normal: lo = 0.35f; hi = 0.62f; break;
            default: lo = 0.10f; hi = 0.40f; break;
        }
    }

    public static HitOutcome Judge(SwingContext c)
    {
        float e = EllipseDistance(c.BallPos, c.CursorPos, c.CursorHalfW, c.CursorHalfH);
        HitRank rp = RankFromEllipse(e);
        HitRank rt = RankFromTiming(c.TimingMs, c.Meet);
        HitRank rank = (HitRank)Mathf.Min((int)rp, (int)rt);

        var o = new HitOutcome { Rank = rank, EllipseDist = e };
        if (rank == HitRank.Whiff) return o;

        // 質 q: 距離とタイミングを半々。ランクごとの範囲に収めて順序を保つ。
        float qPos = 1f - Mathf.Clamp01(e);
        float qTime = 1f - Mathf.Clamp01(Mathf.Abs(c.TimingMs) / BattingConfig.TimingWeakMs);
        float q = 0.5f * qPos + 0.5f * qTime;
        TierRange(rank, out float lo, out float hi);
        q = Mathf.Clamp(q, lo, hi);
        o.Quality = q;

        // カーソルの縁すれすれ(Weak)は、ときどきファウルチップ(真後ろ)になる
        if (rank == HitRank.Weak && e > 0.88f && Random.value < 0.45f)
        {
            o.FoulTip = true;
            o.ExitSpeed = Random.Range(12f, 24f);
            o.LaunchDeg = Random.Range(18f, 50f);
            o.SprayDeg = 180f + Random.Range(-22f, 22f);
            o.BackspinRpm = 800f;
            return o;
        }

        // 初速: 質とパワーで決まる
        float pf = 0.75f + c.Power / 200f;
        o.ExitSpeed = BattingConfig.ExitBase + BattingConfig.ExitRange * Mathf.Pow(q, 1.4f) * pf;

        // 打出角: ボールがカーソル中心より上ならフライ寄り、下ならゴロ寄り
        float dyN = Mathf.Clamp((c.BallPos.y - c.CursorPos.y) / c.CursorHalfH, -1.2f, 1.2f);
        float noise = (1f - q) * 12f;
        float launch = BattingConfig.LaunchBase + BattingConfig.LaunchPerDy * dyN
                       + Random.Range(-noise, noise);
        o.LaunchDeg = Mathf.Clamp(launch, -25f, 75f);

        // 打球方向: 早いと引っ張り、遅いと流し
        float early = Mathf.Clamp(-c.TimingMs / 130f, -1f, 1f);
        float spreadNoise = 5f + (1f - q) * 22f;
        o.SprayDeg = c.BatSide * early * BattingConfig.SprayMax + Random.Range(-spreadNoise, spreadNoise);

        // スピン: 打出角が高いほどバックスピンが強い。早いと引っ張り側へ曲がる
        o.BackspinRpm = Mathf.Lerp(700f, 2800f, Mathf.InverseLerp(-10f, 45f, o.LaunchDeg));
        o.TiltDeg = c.BatSide * early * 14f;
        return o;
    }
}
