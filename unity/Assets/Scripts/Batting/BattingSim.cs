using System.Collections.Generic;
using System.Text;
using UnityEngine;

// ============================================================
// BattingSim.cs
// 画面なしの一括シミュレーション(バランス確認用)。実際の AutoPilot と同じ癖の
// 「熟練プレイヤー」が、投手AI・判定・打球・守備・走者進塁・珍プレーを通して
// 何試合か打つ。数値の確認専用で、ゲーム本編からは呼ばれない。
// ============================================================
public static class BattingSim
{
    public static string Run(int games, float gunya, int seed)
    {
        Random.InitState(seed);
        float saveG = BattingConfig.GunyaLevel;
        BattingConfig.GunyaLevel = gunya;

        var pitcher = SpecialCharacters.Panta();
        var batters = new PlayerStats[] { SpecialCharacters.Kabutomaru(), SpecialCharacters.Wanta(), SpecialCharacters.HeroDefault() };
        var gs = new GameState();
        var cnt = new Dictionary<string, int>();
        var kindCnt = new int[5];
        int quirkGames0 = 0, quirkGames1 = 0, quirkGames2 = 0;
        int totalRuns = 0, totalPA = 0, totalPitches = 0, chancePA = 0, dp = 0, sacFly = 0;
        int bi = 0;

        for (int g = 0; g < games; g++)
        {
            gs.NewGame();
            while (!gs.GameOver)
            {
                var b = batters[bi % 3]; bi++;
                int batSide = b.BatsRight ? -1 : 1;
                gs.ResetCount();
                gs.PlateAppearances++;
                totalPA++;
                if (gs.Base[2] || gs.Base[3]) chancePA++;
                int l1 = -1, l2 = -1;
                bool over = false;
                int guard = 0;
                while (!over && guard++ < 40)
                {
                    var plan = PitchAI.ChoosePitch(pitcher, b, gs.Balls, gs.Strikes, batSide, l1, l2);
                    l2 = l1; l1 = (int)plan.Kind;
                    kindCnt[(int)plan.Kind]++;
                    totalPitches++;

                    float noise = 0.33f + (plan.Kind == PitchKind.Fastball ? 0f : 0.07f);
                    Vector2 cur = plan.Target + new Vector2(PitchAI.Gauss() * noise, PitchAI.Gauss() * noise);
                    bool swing = plan.InZone ? (Random.value < 0.92f) : (Random.value < 0.15f);
                    if (!swing)
                    {
                        bool body = Mathf.Sign(plan.Target.x) == batSide && Mathf.Abs(plan.Target.x) >= 0.92f
                                    && plan.Target.y > 0.4f && plan.Target.y < 1.9f;
                        if (plan.InZone)
                        {
                            gs.Strikes++;
                            if (gs.Strikes >= 3) { gs.ApplyStrikeout(); over = true; Add(cnt, "K_look"); }
                        }
                        else if (body) { gs.ApplyWalk(); over = true; Add(cnt, "HBP"); }
                        else
                        {
                            gs.Balls++;
                            if (gs.Balls >= 4) { gs.ApplyWalk(); over = true; Add(cnt, "BB"); }
                        }
                        continue;
                    }
                    float bias = plan.Kind == PitchKind.Changeup ? -0.035f : 0f;
                    float timingMs = (bias + PitchAI.Gauss() * 0.12f) * 1000f;
                    var ctx = new SwingContext
                    {
                        BallPos = plan.Target, CursorPos = ClampC(cur),
                        CursorHalfW = HitJudge.CursorHalfW(b.Meet), CursorHalfH = HitJudge.CursorHalfH(b.Meet),
                        TimingMs = timingMs, Meet = b.Meet, Power = b.Power, BatSide = batSide
                    };
                    var o = HitJudge.Judge(ctx);
                    if (o.Rank == HitRank.Whiff)
                    {
                        gs.Strikes++;
                        if (gs.Strikes >= 3) { gs.ApplyStrikeout(); over = true; Add(cnt, "K_swing"); }
                        continue;
                    }
                    if (o.FoulTip)
                    {
                        gs.Strikes++;
                        if (gs.Strikes >= 3) { gs.ApplyStrikeout(); over = true; Add(cnt, "K_tip"); }
                        continue;
                    }
                    var contact = new Vector3(plan.Target.x, plan.Target.y, 0f);
                    var tr = BattedBallPhysics.Simulate(contact, o.ExitSpeed, o.LaunchDeg, o.SprayDeg, o.BackspinRpm, o.TiltDeg);
                    float qs = gs.QuirkAllowed ? gunya : 0f;
                    var play = FieldingModel.Resolve(tr, b.Speed, qs);
                    if (play.Quirk != QuirkKind.None) { gs.QuirkCount++; Add(cnt, "Q_" + play.Quirk); }
                    if (play.Kind == PlayKind.Foul) { if (gs.Strikes < 2) gs.Strikes++; continue; }
                    over = true;
                    int outsBefore = gs.Outs;
                    if (play.Kind == PlayKind.HomeRun) gs.ApplyHit(4);
                    else if (play.IsHit) gs.ApplyHit(Mathf.Max(1, play.Bases));
                    else
                    {
                        gs.ApplyOut(play.Kind, play.Fielder, play.Distance);
                        if (gs.Outs - outsBefore >= 2) dp++;
                        if (gs.LastRuns > 0 && play.Kind == PlayKind.FlyOut) sacFly++;
                    }
                    Add(cnt, "P_" + play.Kind);
                }
                if (gs.Outs >= 3) gs.EndInningIfNeeded();
            }
            totalRuns += gs.Runs;
            if (gs.QuirkCount == 0) quirkGames0++; else if (gs.QuirkCount == 1) quirkGames1++; else quirkGames2++;
        }

        BattingConfig.GunyaLevel = saveG;
        var sb = new StringBuilder();
        sb.Append("G=" + gunya + " games=" + games + " PA=" + totalPA + " PA/inning=" + ((float)totalPA / (games * GameState.InningsPerGame)).ToString("F2"));
        sb.Append(" runs/inning=" + ((float)totalRuns / (games * GameState.InningsPerGame)).ToString("F2"));
        sb.Append(" chancePA%=" + (100f * chancePA / totalPA).ToString("F1"));
        sb.Append(" quirk games 0/1/2=" + quirkGames0 + "/" + quirkGames1 + "/" + quirkGames2);
        sb.Append(" DP=" + dp + " SF=" + sacFly + "\n");
        var keys = new List<string>(cnt.Keys);
        keys.Sort();
        foreach (var k in keys) sb.Append(k + "=" + cnt[k] + " ");
        sb.Append("\npitch use F/C/S/Fk/Ch=");
        for (int i = 0; i < 5; i++) sb.Append((100f * kindCnt[i] / totalPitches).ToString("F0") + "% ");
        return sb.ToString();
    }

    private static Vector2 ClampC(Vector2 p)
    {
        float mx = BattingConfig.ZoneHalfW + BattingConfig.CursorMargin;
        float yMin = BattingConfig.ZoneBottom - BattingConfig.CursorMargin;
        float yMax = BattingConfig.ZoneTop + BattingConfig.CursorMargin;
        return new Vector2(Mathf.Clamp(p.x, -mx, mx), Mathf.Clamp(p.y, yMin, yMax));
    }

    private static void Add(Dictionary<string, int> d, string k)
    {
        int c; d.TryGetValue(k, out c); d[k] = c + 1;
    }
}
