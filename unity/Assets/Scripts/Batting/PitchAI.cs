using UnityEngine;

// ============================================================
// PitchAI.cs
// 投手AI(P2): カウント・打者の能力・前の球を見て、球種とコースを選ぶ。
// 「打てる楽しさ」を残すため、甘い直球とぶら下がる変化球がたまに来る。
// ============================================================
public struct PitchPlan
{
    public PitchKind Kind;
    public string Name;
    public Vector2 Target;    // ゾーン面での到達位置 (x,y)
    public float Kmh;         // 表示用の球速
    public float FlightTime;  // 実際の到達時間(秒)
    public Vector2 Break;     // 到達までの総変化量(能力・ぶら下がり反映済み)
    public float LateK;
    public bool InZone;       // ストライク判定(ボールの一部でもゾーンに触れる)
    public bool Fat;          // 甘い球(ど真ん中・ぶら下がり)
}

public static class PitchAI
{
    public const float HangingChance = 0.05f;   // 変化球がぶら下がる確率
    public static float InsideChance = 0.012f;   // ボール球のうち、打者の体へ向かう確率(×ぐにゃ度)

    public static PitchPlan ChoosePitch(PlayerStats pitcher, PlayerStats batter, int balls, int strikes,
        int batSide, int last1, int last2)
    {
        PitchKind kind = PickKind(pitcher, batter, balls, strikes, last1, last2);
        PitchDef def = PitchRepertoire.Get(kind);
        var plan = new PitchPlan { Kind = kind, Name = def.Name, LateK = def.LateK };

        // 球速: 直球は表示130〜155km/h。変化球は直球に対する比で遅くなる
        float fast = PitchTable.SpeedKmh(pitcher, PitchTable.All[0]) + Random.Range(-1.5f, 5f);
        fast = Mathf.Clamp(fast, BattingConfig.KmhMin, BattingConfig.KmhMax);
        plan.Kmh = fast * def.SpeedFactor;
        plan.FlightTime = PitchRepertoire.FlightTimeFor(plan.Kmh);

        // ---- コース ----
        float pZone = 0.62f;
        if (balls >= 3) pZone = 0.90f;
        else if (strikes >= 2 && balls < 2) pZone = 0.38f;

        float pFat = (balls >= 2 || strikes == 0) ? 0.14f : 0.08f;
        bool hanging = kind != PitchKind.Fastball && Random.value < HangingChance;

        Vector2 aim;
        if (hanging || Random.value < pFat)
        {
            plan.Fat = true;
            aim = new Vector2(Gauss() * 0.10f, BattingConfig.ZoneCenterY + Gauss() * 0.10f);
        }
        else if (Random.value < pZone)
        {
            float edge = (strikes > balls) ? 0.75f : 0.35f;
            float ax = Random.Range(-1f, 1f);
            float ay = Random.Range(-1f, 1f);
            if (Random.value < edge)
            {
                ax = Mathf.Sign(ax) * Random.Range(0.55f, 0.95f);
                ay = Mathf.Sign(ay) * Random.Range(0.45f, 0.95f);
            }
            aim = new Vector2(ax * BattingConfig.ZoneHalfW * 0.9f,
                              BattingConfig.ZoneCenterY + ay * BattingConfig.ZoneHalfH * 0.9f);
        }
        else if (Random.value < InsideChance * BattingConfig.GunyaLevel / Mathf.Max(0.01f, 1f - pZone))
        {
            // 体の近くへ(当たれば死球)
            aim = new Vector2(batSide * Random.Range(0.84f, 1.0f), BattingConfig.ZoneCenterY + Random.Range(-0.3f, 0.35f));
        }
        else if (Random.value < 0.5f)
        {
            float sx = Random.value < 0.5f ? -1f : 1f;
            // 打者の体の側(batSide)は大きく外れすぎないよう少し控えめ
            float mag = Random.Range(0.55f, 0.80f);
            if (sx == batSide) mag = Random.Range(0.55f, 0.72f);
            aim = new Vector2(sx * mag, BattingConfig.ZoneCenterY + Random.Range(-0.4f, 0.4f));
        }
        else
        {
            float sy = Random.value < 0.6f ? -1f : 1f;
            aim = new Vector2(Random.Range(-0.4f, 0.4f),
                              BattingConfig.ZoneCenterY + sy * Random.Range(0.55f, 0.80f));
        }

        // 球種ごとの狙い
        if (!hanging && !plan.Fat)
        {
            switch (kind)
            {
                case PitchKind.Fork:
                    aim.y = Mathf.Min(aim.y, BattingConfig.ZoneBottom + (strikes >= 2 ? -0.18f : 0.12f));
                    break;
                case PitchKind.Curve:
                    aim.y = Mathf.Min(aim.y, BattingConfig.ZoneCenterY - 0.1f);
                    break;
                case PitchKind.Slider:
                    // 打者から逃げる向き(右打者なら+x側)へ
                    aim.x = -batSide * Mathf.Abs(aim.x) * 1.1f;
                    break;
                case PitchKind.Changeup:
                    aim.y = Mathf.Min(aim.y, BattingConfig.ZoneCenterY);
                    break;
            }
        }

        float sigma = Mathf.Lerp(0.17f, 0.05f, Mathf.InverseLerp(5f, 99f, pitcher.Ctrl));
        aim += new Vector2(Gauss() * sigma, Gauss() * sigma);
        aim.x = Mathf.Clamp(aim.x, -1.1f, 1.1f);
        aim.y = Mathf.Clamp(aim.y, 0.1f, 2.2f);

        plan.Target = aim;
        plan.InZone = IsStrike(aim);

        float scale = PitchRepertoire.BreakScale(pitcher) * (hanging ? 0.25f : 1f);
        plan.Break = def.Break * scale;
        if (kind == PitchKind.Slider) plan.Break.x *= -batSide * 1f; // 逃げる向きに合わせる(右打者: +x)
        return plan;
    }

    // 球種の選択(カウント・打者・前の球)
    private static PitchKind PickKind(PlayerStats pitcher, PlayerStats batter, int balls, int strikes,
        int last1, int last2)
    {
        var w = new float[PitchRepertoire.All.Length];
        for (int i = 0; i < w.Length; i++)
        {
            w[i] = pitcher.Brk >= PitchRepertoire.All[i].MinBrk ? PitchRepertoire.All[i].Weight : 0f;
        }
        if (strikes >= 2)
        {
            w[(int)PitchKind.Fork] *= 1.9f;
            w[(int)PitchKind.Slider] *= 1.5f;
            w[(int)PitchKind.Fastball] *= 0.8f;
        }
        if (balls >= 2 && strikes < 2)
        {
            w[(int)PitchKind.Fastball] *= 1.6f;
            w[(int)PitchKind.Curve] *= 0.5f;
            w[(int)PitchKind.Fork] *= 0.4f;
        }
        if (balls >= 3) w[(int)PitchKind.Fork] *= 0.3f;
        if (last1 >= 0)
        {
            w[last1] *= 0.6f;
            if (last1 != (int)PitchKind.Fastball) w[(int)PitchKind.Fastball] *= 1.3f;
        }
        if (last1 == 0 && last2 == 0)
        {
            w[(int)PitchKind.Changeup] *= 1.8f;
            w[(int)PitchKind.Curve] *= 1.4f;
        }
        if (batter.Meet >= 50)
        {
            w[(int)PitchKind.Fork] *= 1.2f;
            w[(int)PitchKind.Slider] *= 1.2f;
        }
        if (batter.Power >= 60)
        {
            w[(int)PitchKind.Changeup] *= 1.3f;
            w[(int)PitchKind.Fastball] *= 0.85f;
        }

        float total = 0f;
        for (int i = 0; i < w.Length; i++) total += w[i];
        float r = Random.value * Mathf.Max(0.0001f, total);
        for (int i = 0; i < w.Length; i++)
        {
            r -= w[i];
            if (r < 0f && w[i] > 0f) return (PitchKind)i;
        }
        return PitchKind.Fastball;
    }

    // ボールの一部でもゾーンに触れればストライク
    public static bool IsStrike(Vector2 p)
    {
        return Mathf.Abs(p.x) <= BattingConfig.ZoneHalfW + BattingConfig.BallRadius
            && p.y >= BattingConfig.ZoneBottom - BattingConfig.BallRadius
            && p.y <= BattingConfig.ZoneTop + BattingConfig.BallRadius;
    }

    // -1〜1 の疑似ガウス(3つの一様乱数の平均)
    public static float Gauss()
    {
        return (Random.Range(-1f, 1f) + Random.Range(-1f, 1f) + Random.Range(-1f, 1f)) / 3f;
    }
}
