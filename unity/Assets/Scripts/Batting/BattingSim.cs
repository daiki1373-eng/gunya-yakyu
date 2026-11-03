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
            totalRuns += gs.Runs + gs.OppRuns;
            if (gs.QuirkCount == 0) quirkGames0++; else if (gs.QuirkCount == 1) quirkGames1++; else quirkGames2++;
        }

        BattingConfig.GunyaLevel = saveG;
        var sb = new StringBuilder();
        sb.Append("G=" + gunya + " games=" + games + " PA=" + totalPA + " PA/inning=" + ((float)totalPA / (games * GameState.InningsPerGame * 2)).ToString("F2"));
        sb.Append(" runs/inning=" + ((float)totalRuns / (games * GameState.InningsPerGame * 2)).ToString("F2"));
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

    // ------------------------------------------------------------
    // 守り(ユーザーが投げる)側のシミュレーション。
    // timingSdMs: 2回目のタップのずれの標準偏差。noPressRate: 押し忘れ率。
    // 球種・コースは投手AIの選択を「ユーザーの狙い」として使い、
    // PlanFromUser(タイミングで精度が変わる)→ BatterAI → 同じ判定式、で打たせる。
    // ------------------------------------------------------------
    public static string RunDefense(int halfInnings, float timingSdMs, float noPressRate, int seed)
    {
        Random.InitState(seed);
        var pitcher = SpecialCharacters.Panta();
        var opp = new PlayerStats[3];
        for (int i = 0; i < 3; i++) opp[i] = Opp(i);
        var gs = new GameState();
        gs.UserBatting = false;
        var cnt = new Dictionary<string, int>();
        int pa = 0, pitches = 0, ab = 0, hits = 0, bb = 0, k = 0, hbp = 0, runs = 0, strikesThrown = 0;
        int bi = 0;
        float qSum = 0f; int qN = 0;

        for (int h = 0; h < halfInnings; h++)
        {
            gs.Outs = 0; gs.ClearBases();
            int runsBefore = gs.OppRuns;
            while (gs.Outs < 3)
            {
                var b = opp[bi % 3]; bi++;
                int batSide = b.BatsRight ? -1 : 1;
                gs.ResetCount();
                pa++;
                int l1 = -1, l2 = -1;
                bool over = false;
                int guard = 0;
                while (!over && guard++ < 40)
                {
                    var aiPlan = PitchAI.ChoosePitch(pitcher, b, gs.Balls, gs.Strikes, batSide, l1, l2);
                    float tms;
                    if (Random.value < noPressRate) tms = 185f;
                    else tms = PitchAI.Normal(timingSdMs / 1000f) * 1000f;
                    var plan = PitchAI.PlanFromUser(pitcher, aiPlan.Kind, aiPlan.Target, batSide, tms);
                    qSum += PitchAI.TimingQuality(tms); qN++;
                    l2 = l1; l1 = (int)plan.Kind;
                    pitches++;
                    if (plan.InZone) strikesThrown++;

                    var d = BatterAI.Decide(b, plan, gs.Balls, gs.Strikes);
                    if (!d.Swing)
                    {
                        bool body = Mathf.Sign(plan.Target.x) == batSide && Mathf.Abs(plan.Target.x) >= 0.92f
                                    && plan.Target.y > 0.4f && plan.Target.y < 1.9f;
                        if (plan.InZone)
                        {
                            gs.Strikes++;
                            if (gs.Strikes >= 3) { gs.ApplyStrikeout(); over = true; k++; ab++; }
                        }
                        else if (body) { gs.ApplyWalk(); over = true; hbp++; }
                        else
                        {
                            gs.Balls++;
                            if (gs.Balls >= 4) { gs.ApplyWalk(); over = true; bb++; }
                        }
                        continue;
                    }
                    var ctx = new SwingContext
                    {
                        BallPos = plan.Target, CursorPos = ClampC(d.Cursor),
                        CursorHalfW = HitJudge.CursorHalfW(b.Meet), CursorHalfH = HitJudge.CursorHalfH(b.Meet),
                        TimingMs = d.TimingSec * 1000f, Meet = b.Meet, Power = b.Power, BatSide = batSide
                    };
                    var o = HitJudge.Judge(ctx);
                    if (o.Rank == HitRank.Whiff || o.FoulTip)
                    {
                        gs.Strikes++;
                        if (gs.Strikes >= 3) { gs.ApplyStrikeout(); over = true; k++; ab++; }
                        continue;
                    }
                    var contact = new Vector3(plan.Target.x, plan.Target.y, 0f);
                    var tr = BattedBallPhysics.Simulate(contact, o.ExitSpeed, o.LaunchDeg, o.SprayDeg, o.BackspinRpm, o.TiltDeg);
                    var play = FieldingModel.Resolve(tr, b.Speed, gs.QuirkAllowed ? BattingConfig.GunyaLevel : 0f);
                    if (play.Quirk != QuirkKind.None) gs.QuirkCount++;
                    if (play.Kind == PlayKind.Foul) { if (gs.Strikes < 2) gs.Strikes++; continue; }
                    over = true; ab++;
                    if (play.Kind == PlayKind.HomeRun) { gs.ApplyHit(4); hits++; }
                    else if (play.IsHit) { gs.ApplyHit(Mathf.Max(1, play.Bases)); hits++; }
                    else gs.ApplyOut(play.Kind, play.Fielder, play.Distance);
                }
            }
            runs += gs.OppRuns - runsBefore;
        }
        var sb = new StringBuilder();
        sb.Append("timingSd=" + timingSdMs + "ms noPress=" + noPressRate + " halfInn=" + halfInnings);
        sb.Append(" avgQ=" + (qSum / Mathf.Max(1, qN)).ToString("F2"));
        sb.Append(" PA/half=" + ((float)pa / halfInnings).ToString("F2"));
        sb.Append(" runs/half=" + ((float)runs / halfInnings).ToString("F2"));
        sb.Append(" AVG=" + ((float)hits / Mathf.Max(1, ab)).ToString("F3"));
        sb.Append(" K%=" + (100f * k / pa).ToString("F1"));
        sb.Append(" BB%=" + (100f * bb / pa).ToString("F1"));
        sb.Append(" HBP%=" + (100f * hbp / pa).ToString("F1"));
        sb.Append(" strike%=" + (100f * strikesThrown / pitches).ToString("F0"));
        return sb.ToString();
    }

    private static PlayerStats Opp(int i)
    {
        PlayerStats p;
        if (i == 0) p = new PlayerStats { Name = "A", Species = "human", BatsRight = true, Meet = 50, Power = 44, Speed = 45 };
        else if (i == 1) p = new PlayerStats { Name = "B", Species = "human", BatsRight = false, Meet = 60, Power = 36, Speed = 56 };
        else p = new PlayerStats { Name = "C", Species = "human", BatsRight = true, Meet = 44, Power = 60, Speed = 36 };
        SpeciesTable.Apply(p);
        return p;
    }
}
