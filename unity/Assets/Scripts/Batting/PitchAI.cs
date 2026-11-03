using UnityEngine;

// ============================================================
// PitchAI.cs
// 投手AI(P1は直球のみ)。カウントと打者を見てコースを選ぶ。
// 球種は P2 で追加するので、PitchPlan に球種の入れ物だけ用意してある。
// ============================================================
public struct PitchPlan
{
    public string Name;
    public Vector2 Target;    // ゾーン面での到達位置 (x,y)
    public float Kmh;         // 表示用の球速
    public float FlightTime;  // 実際の到達時間(秒)
    public bool InZone;       // ストライク判定(ボールの一部でもゾーンに触れる)
    public bool Fat;          // 甘い球
}

public static class PitchAI
{
    public static PitchPlan ChoosePitch(PlayerStats pitcher, PlayerStats batter, int balls, int strikes)
    {
        var plan = new PitchPlan { Name = "ストレート" };

        // 球速: 表示は130〜155km/h、到達時間は0.65〜0.45秒に写像
        float kmh = PitchTable.SpeedKmh(pitcher, PitchTable.All[0]) + Random.Range(-1.5f, 5f);
        kmh = Mathf.Clamp(kmh, BattingConfig.KmhMin, BattingConfig.KmhMax);
        float u = Mathf.InverseLerp(BattingConfig.KmhMin, BattingConfig.KmhMax, kmh);
        plan.Kmh = kmh;
        plan.FlightTime = Mathf.Lerp(BattingConfig.FlightSlow, BattingConfig.FlightFast, u);

        // ストライクを投げる確率: 3ボールなら置きにいく、2ストライクなら誘い球が増える
        float pZone = 0.62f;
        if (balls >= 3) pZone = 0.90f;
        else if (strikes >= 2 && balls < 2) pZone = 0.38f;

        // 甘い球(打てる楽しさ): 追い込まれていない/カウントが悪いときに少し多め
        float pFat = (balls >= 2 || strikes == 0) ? 0.14f : 0.08f;

        Vector2 aim;
        float r = Random.value;
        if (r < pFat)
        {
            plan.Fat = true;
            aim = new Vector2(Gauss() * 0.10f, BattingConfig.ZoneCenterY + Gauss() * 0.10f);
        }
        else if (Random.value < pZone)
        {
            // ゾーン内。有利カウントほど角を突く
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
        else
        {
            // ゾーン外(ボール球)
            if (Random.value < 0.5f)
            {
                float sx = Random.value < 0.5f ? -1f : 1f;
                aim = new Vector2(sx * Random.Range(0.55f, 0.85f),
                                  BattingConfig.ZoneCenterY + Random.Range(-0.4f, 0.4f));
            }
            else
            {
                float sy = Random.value < 0.6f ? -1f : 1f;
                aim = new Vector2(Random.Range(-0.4f, 0.4f),
                                  BattingConfig.ZoneCenterY + sy * Random.Range(0.55f, 0.80f));
            }
        }

        // 制球による散らばり
        float sigma = Mathf.Lerp(0.17f, 0.05f, Mathf.InverseLerp(5f, 99f, pitcher.Ctrl));
        aim += new Vector2(Gauss() * sigma, Gauss() * sigma);
        aim.x = Mathf.Clamp(aim.x, -1.1f, 1.1f);
        aim.y = Mathf.Clamp(aim.y, 0.1f, 2.2f);

        plan.Target = aim;
        plan.InZone = IsStrike(aim);
        return plan;
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
