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

    // ------------------------------------------------------------
    // 人の操作を模した打撃シミュレーション(操作性の確認用)。
    // reaction: リリースから狙いに動き出すまで(秒) / aimSd: 狙いのぶれ(ゾーン単位)
    // timingSdMs: スイングのずれ / zoneSwing: ゾーン内を振る率 / chase: ボール球を振る率
    // cursorSpeed: カーソルの最大速度(0 なら BattingConfig の値)。カーソルは毎球ゾーン中央から動かす。
    // 打者は CharaRoster の自チーム、投手は相手チームの投手(毎試合入れ替え)。
    // ------------------------------------------------------------
    public static string RunHuman(int games, float reaction, float aimSd, float timingSdMs, float zoneSwing, float chase,
        float cursorSpeed, int seed)
    {
        Random.InitState(seed);
        float spd = cursorSpeed > 0f ? cursorSpeed : BattingConfig.CursorSpeed;
        var user = CharaRoster.UserTeam();
        var gs = new GameState();
        int pa = 0, ab = 0, hits = 0, hr = 0, k = 0, bb = 0, swings = 0, whiffs = 0, contacts = 0, perfect = 0, good = 0;
        int runs = 0, halfInn = 0, reachFail = 0, early = 0, late = 0;
        int bi = 0;
        for (int g = 0; g < games; g++)
        {
            var opp = CharaRoster.OpponentTeam(user, seed * 31 + g);
            var pitcher = CharaRoster.Stats(opp.Pitcher);
            gs.NewGame();
            int runsBefore = 0;
            while (!gs.GameOver)
            {
                if (!gs.UserBatting) { gs.Outs = 3; gs.EndInningIfNeeded(); continue; }   // 守りは飛ばす
                halfInn++;
                runsBefore = gs.Runs;
                while (gs.Outs < 3 && !gs.GameOver)
                {
                    var b = CharaRoster.Stats(user.Lineup[bi % user.Lineup.Count]); bi++;
                    int batSide = b.BatsRight ? -1 : 1;
                    gs.ResetCount();
                    pa++;
                    int l1 = -1, l2 = -1;
                    bool over = false;
                    int guard = 0;
                    while (!over && guard++ < 40)
                    {
                        var plan = PitchAI.ChoosePitch(pitcher, b, gs.Balls, gs.Strikes, batSide, l1, l2);
                        l2 = l1; l1 = (int)plan.Kind;
                        // 振るか(ゾーンからの距離で追いかけ率を下げる)
                        float dx = Mathf.Abs(plan.Target.x) / BattingConfig.ZoneHalfW;
                        float dy = Mathf.Abs(plan.Target.y - BattingConfig.ZoneCenterY) / BattingConfig.ZoneHalfH;
                        float dOut = Mathf.Max(dx, dy);
                        float pSwing = plan.InZone ? zoneSwing : chase * Mathf.Clamp01(1.6f - dOut);
                        if (gs.Strikes >= 2) pSwing = Mathf.Min(1f, pSwing + 0.12f);
                        if (Random.value >= pSwing)
                        {
                            bool body = Mathf.Sign(plan.Target.x) == batSide && Mathf.Abs(plan.Target.x) >= 0.92f
                                        && plan.Target.y > 0.4f && plan.Target.y < 1.9f;
                            if (plan.InZone)
                            {
                                gs.Strikes++;
                                if (gs.Strikes >= 3) { gs.ApplyStrikeout(); over = true; k++; ab++; }
                            }
                            else if (body) { gs.ApplyWalk(); over = true; bb++; }
                            else
                            {
                                gs.Balls++;
                                if (gs.Balls >= 4) { gs.ApplyWalk(); over = true; bb++; }
                            }
                            continue;
                        }
                        swings++;
                        // タイミング(変化球は遅い分、早振りしやすい)
                        float bias = plan.Kind == PitchKind.Changeup ? -0.040f : (plan.Kind == PitchKind.Curve ? -0.025f : 0f);
                        float tErr = bias + PitchAI.Normal(timingSdMs / 1000f);
                        // カーソル: 中央から、反応後に最大速度で狙いへ。スイングした時点で止まる
                        float brk = plan.Break.magnitude;
                        Vector2 aim = plan.Target + new Vector2(PitchAI.Normal(1f), PitchAI.Normal(1f)) * aimSd * (1f + brk * 1.2f);
                        Vector2 start = new Vector2(0f, BattingConfig.ZoneCenterY);
                        float travel = Mathf.Max(0f, plan.FlightTime - BattingConfig.SwingLag + tErr - reaction);
                        Vector2 cur = Vector2.MoveTowards(start, aim, spd * travel);
                        if ((cur - aim).sqrMagnitude > 0.0004f) reachFail++;
                        cur = ClampC(cur);
                        float aimY = cur.y;
                        cur = HitJudge.Assist(cur, plan.Target, HitJudge.CursorHalfW(b.Meet), HitJudge.CursorHalfH(b.Meet));
                        float timingMs = tErr * 1000f;
                        if (timingMs < -BattingConfig.TimingWeakMs) early++;
                        if (timingMs > BattingConfig.TimingWeakMs) late++;
                        var ctx = new SwingContext
                        {
                            BallPos = plan.Target, CursorPos = cur, HasAimY = true, AimY = aimY,
                            CursorHalfW = HitJudge.CursorHalfW(b.Meet), CursorHalfH = HitJudge.CursorHalfH(b.Meet),
                            TimingMs = timingMs, Meet = b.Meet, Power = b.Power, BatSide = batSide
                        };
                        var o = HitJudge.Judge(ctx);
                        if (o.Rank == HitRank.Whiff || o.FoulTip)
                        {
                            if (o.Rank == HitRank.Whiff) whiffs++; else contacts++;
                            gs.Strikes++;
                            if (gs.Strikes >= 3) { gs.ApplyStrikeout(); over = true; k++; ab++; }
                            continue;
                        }
                        contacts++;
                        if (o.Rank == HitRank.Perfect) perfect++;
                        if (o.Rank == HitRank.Good) good++;
                        var contact = new Vector3(plan.Target.x, plan.Target.y, 0f);
                        var tr = BattedBallPhysics.Simulate(contact, o.ExitSpeed, o.LaunchDeg, o.SprayDeg, o.BackspinRpm, o.TiltDeg);
                        var play = FieldingModel.Resolve(tr, b.Speed, 0f);
                        if (play.Kind == PlayKind.Foul) { if (gs.Strikes < 2) gs.Strikes++; continue; }
                        over = true;
                        ab++;
                        if (play.Kind == PlayKind.HomeRun) { gs.ApplyHit(4); hits++; hr++; }
                        else if (play.IsHit) { gs.ApplyHit(Mathf.Max(1, play.Bases)); hits++; }
                        else gs.ApplyOut(play.Kind, play.Fielder, play.Distance);
                    }
                }
                runs += gs.Runs - runsBefore;
                gs.EndInningIfNeeded();
            }
        }
        var sb = new StringBuilder();
        sb.Append("react=" + reaction + "s aim=" + aimSd + " timing=" + timingSdMs + "ms cursor=" + spd + "  ");
        sb.Append("打率 " + (ab > 0 ? (float)hits / ab : 0f).ToString(".000"));
        sb.Append(" 三振率 " + (100f * k / pa).ToString("F0") + "%");
        sb.Append(" 四球率 " + (100f * bb / pa).ToString("F0") + "%");
        sb.Append(" 空振り率 " + (100f * whiffs / Mathf.Max(1, swings)).ToString("F0") + "%");
        sb.Append(" 芯(P+G) " + (100f * (perfect + good) / Mathf.Max(1, contacts)).ToString("F0") + "%");
        sb.Append(" カーソル届かず " + (100f * reachFail / Mathf.Max(1, swings)).ToString("F0") + "%");
        sb.Append(" 早/遅 " + (100f * early / Mathf.Max(1, swings)).ToString("F0") + "/" + (100f * late / Mathf.Max(1, swings)).ToString("F0") + "%");
        sb.Append(" 得点/回 " + ((float)runs / Mathf.Max(1, halfInn)).ToString("F2"));
        sb.Append(" HR/試合 " + ((float)hr / games).ToString("F2"));
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
            while (gs.Outs < 3 && !gs.GameOver)
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
