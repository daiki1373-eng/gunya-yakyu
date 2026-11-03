using UnityEngine;

// ============================================================
// BatterAI.cs
// 打者AI(P3)。ユーザーが投げた球に対して、スイングするか・カーソルをどこへ置くか・
// いつ振るかを決める。判定そのものは打つ側と同じ HitJudge を通す(ここでは「操作」だけ作る)。
//   ・ミートが高いほど、球の位置/タイミングの読み違いが小さい
//   ・変化球・チェンジアップほど読み違いが増える(曲がりの大きさに比例)
//   ・ゾーン外でも際どければ振る。2ストライクは広く振る。3ボールは待つ
//   ・甘い球(ど真ん中・ぶら下がり)はほぼ振る
// 数値はすべて自前の仮置き。
// ============================================================
public struct BatterDecision
{
    public bool Swing;
    public Vector2 Cursor;     // スイング時のカーソル中心
    public float TimingSec;    // ボール到達に対するスイング到達のずれ(負=早い/正=遅い)
}

public static class BatterAI
{
    public static BatterDecision Decide(PlayerStats b, PitchPlan plan, int balls, int strikes)
    {
        float m = Mathf.InverseLerp(5f, 99f, b.Meet);
        float brk = plan.Break.magnitude;

        // 投げ手のタイミングが完璧なほどキレが出て読みにくい(ユーザー投球のとき。AI投球は0.6固定)
        float qf = Mathf.Lerp(0.75f, 1.5f, plan.Quality > 0f ? plan.Quality : 0.6f);

        // 球の位置の読み(読み違い)
        float sdLook = Mathf.Lerp(0.10f, 0.035f, m) + 0.04f * Mathf.Clamp01(brk / 0.6f)
                     + (plan.Kind == PitchKind.Changeup ? 0.02f : 0f);
        sdLook *= qf;
        Vector2 seen = plan.Target + new Vector2(PitchAI.Normal(sdLook), PitchAI.Normal(sdLook));

        // 振るか見送るか
        float dx = Mathf.Abs(seen.x) / (BattingConfig.ZoneHalfW + BattingConfig.BallRadius);
        float dy = Mathf.Abs(seen.y - BattingConfig.ZoneCenterY) / (BattingConfig.ZoneHalfH + BattingConfig.BallRadius);
        float d = Mathf.Max(dx, dy);
        float p;
        if (d <= 1f) p = 0.86f;
        else p = Mathf.Clamp01(0.24f - (d - 1f) * 0.9f);
        if (strikes >= 2) p = Mathf.Clamp01(p + 0.25f);
        if (balls >= 3) p *= 0.35f;
        if (plan.Fat) p = Mathf.Max(p, 0.95f);

        var dec = new BatterDecision();
        dec.Swing = Random.value < p;

        // カーソル(打つ位置)とタイミング
        float sdCur = Mathf.Lerp(0.10f, 0.04f, m);
        dec.Cursor = seen + new Vector2(PitchAI.Normal(sdCur), PitchAI.Normal(sdCur));
        float sdT = Mathf.Lerp(0.050f, 0.024f, m) * qf;
        float bias = 0f;
        if (plan.Kind == PitchKind.Changeup) bias = -0.040f;       // 泳がされて早振り
        else if (plan.Kind == PitchKind.Curve) bias = -0.020f;
        else if (plan.Kmh >= 145f) bias = 0.015f;                   // 速球に差し込まれる
        dec.TimingSec = bias + PitchAI.Normal(sdT);
        return dec;
    }
}
