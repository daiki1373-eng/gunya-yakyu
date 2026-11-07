#if UNITY_EDITOR || GUNYA_OFFLINE_TESTS
using System;
using System.Collections.Generic;
using UnityEngine;

// パッケージ追加なしで動くEditMode検証。保存値と乱数状態は必ず復元する。
public static class RegressionChecks
{
    public static string ProbabilitySummary { get; private set; }
    public static int LongestRarityGap { get; private set; }
    public static int RunAll(Action<string> report)
    {
        Action[] checks = { Catalog, Probability, Pity, Exchange, Currency, MatchRewards,
            Scoring, HalfInnings, WalkOff, Steals
#if UNITY_EDITOR
            , CameraBounds
#endif
        };
        var savedRandom = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(1373);
            foreach (var check in checks)
            {
                using (new SavedPreferences()) check();
                if (report != null)
                {
                    string detail = check.Method.Name == "Probability" ? " " + ProbabilitySummary
                        : check.Method.Name == "Pity" ? " maximum interval=" + LongestRarityGap : "";
                    report("PASS " + check.Method.Name + detail);
                }
            }
        }
        finally { UnityEngine.Random.state = savedRandom; }
        return checks.Length;
    }

    public static void Catalog()
    {
        Equal(101, CharaCatalog.All.Count, "CSV count");
        int[] expected = { 0, 33, 27, 20, 12, 7, 2 };
        var ids = new HashSet<string>();
        var codes = new HashSet<string>();
        for (int r = 1; r <= 6; r++) Equal(expected[r], CharaCatalog.OfRarity((Rarity)r).Count, "rarity " + r);
        foreach (var d in CharaCatalog.All)
        {
            Check(ids.Add(d.Id) && codes.Add(d.Code), "unique IDs " + d.Code);
            Check(d.StackLimit > 0, "stack " + d.Code);
            if (d.Rarity == Rarity.Secret) Equal(10000, d.ExchangePoints, "SECRET points");
        }
        var csv = CharaCatalog.ParseCsv("\uFEFFid,name,note\r\n1,\"a,b\",\"say \"\"hi\"\"\nnext\"\r\n2,end,\r\n");
        Equal(3, csv.Count, "quoted CSV rows");
        Equal("a,b", csv[1][1], "quoted comma");
        Equal("say \"hi\"\nnext", csv[1][2], "quoted newline and quote");
        Equal("", csv[2][2], "empty last field");
    }

    public static void Probability()
    {
        // 天井を含む実排出分布ではなく、仕様の基礎確率を検証する。
        const int samples = 1000000;
        var counts = new int[7];
        var rng = new System.Random(1373);
        for (int i = 0; i < samples; i++) counts[(int)Gacha.RollRarity(rng)]++;
        double[] expected = { 0, 48.99, 35, 12, 3, 1, 0.01 };
        var summary = new System.Text.StringBuilder();
        for (int r = 1; r <= 6; r++)
        {
            double percent = counts[r] * 100.0 / samples;
            Check(Math.Abs(percent - expected[r]) <= 0.2, "probability " + r + ": " + percent.ToString("F4") + "%");
            if (r > 1) summary.Append(" / ");
            summary.Append(percent.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)).Append('%');
        }
        ProbabilitySummary = summary.ToString();
    }

    private sealed class LowestRoll : System.Random
    {
        public override int Next(int maxValue) { return maxValue - 1; }
    }

    public static void Pity()
    {
        int pity = 0;
        bool byPity;
        var low = new LowestRoll();
        for (int i = 1; i <= 49; i++)
        {
            Equal(Rarity.R1, Gacha.RollOne(low, ref pity, out byPity).Rarity, "before pity " + i);
            Check(!byPity, "not early pity");
        }
        Equal(Rarity.R4, Gacha.RollOne(low, ref pity, out byPity).Rarity, "50th is R4");
        Check(byPity && pity == 0, "pity resets");
        int gap = 0, longest = 0;
        var rng = new System.Random(20261106);
        for (int i = 0; i < 1000000; i++)
        {
            var d = Gacha.RollOne(rng, ref pity, out byPity);
            gap++;
            if (d.Rarity >= Rarity.R4) { longest = Math.Max(longest, gap); gap = 0; }
            Check(pity < 50 && gap < 50, "no 50 misses");
        }
        Check(longest <= 50, "maximum R4+ interval");
        LongestRarityGap = longest;
    }

    public static void Exchange()
    {
        var secret = CharaCatalog.OfRarity(Rarity.Secret)[0];
        PlayerPrefs.SetInt("gunya.card." + secret.Id, 2);
        Collection.Points = 7;
        Check(Collection.Exchange(secret), "exchange duplicate");
        Equal(10007, Collection.Points, "SECRET exchange adds 10000");
        Equal(1, Collection.Count(secret.Id), "last card kept");
        Check(!Collection.Exchange(secret), "cannot exchange final card");
        Equal(10007, Collection.Points, "failed exchange preserves points");
    }

    public static void Currency()
    {
        Collection.Coins = 299; Collection.Points = 0;
        Check(!Collection.ExchangeCoins(), "exchange needs 300");
        Equal(299, Collection.Coins, "insufficient coins unchanged");
        Collection.Coins = 300;
        Check(Collection.ExchangeCoins(), "coin exchange");
        Equal(0, Collection.Coins, "coin exchange cost");
        Equal(15, Collection.Points, "coin exchange points");
        List<PullResult> results;
        int pulls = Collection.TotalPulls, pity = Collection.PityCount;
        Check(!Gacha.TryPullCoins(1, out results), "paid pull requires coins");
        Equal(pulls, Collection.TotalPulls, "failed pull count");
        Equal(pity, Collection.PityCount, "failed pull pity");
        Collection.Coins = 1000;
        var savedRng = Gacha.Rng;
        try
        {
            Gacha.Rng = new System.Random(41);
            Check(Gacha.TryPullCoins(10, out results), "ten paid pulls");
            Equal(10, results.Count, "ten results");
            Equal(0, Collection.Coins, "ten pull cost");
            Equal(pulls + 10, Collection.TotalPulls, "ten pull count");
            Collection.Points = 24;
            Check(!Gacha.TryScout(out results), "scout needs 25");
            Collection.Points = 25;
            Check(Gacha.TryScout(out results), "point scout");
            Equal(0, Collection.Points, "scout points cost");
            Equal(0, Collection.Coins, "scout does not charge coins");
        }
        finally { Gacha.Rng = savedRng; }
    }

    public static void MatchRewards()
    {
        Equal(60, Collection.CalculateMatchReward(0, 1, 0, 0), "loss reward");
        Equal(120, Collection.CalculateMatchReward(0, 0, 0, 0), "draw reward");
        Equal(229, Collection.CalculateMatchReward(3, 1, 3, 1), "win and performance");
        Equal(400, Collection.CalculateMatchReward(100, 0, 100, 100), "performance cap");
        var game = new GameState();
        Collection.Coins = 0;
        Equal(0, Collection.AwardMatch(game), "unfinished no reward");
        game.Inning = GameState.InningsPerGame + 1;
        Equal(120, Collection.AwardMatch(game), "completed reward");
        Equal(0, Collection.AwardMatch(game), "no double claim");
        Equal(120, Collection.Coins, "stored reward");
        game.NewGame();
        Check(!game.RewardClaimed, "new match reward reset");
    }

    public static void Scoring()
    {
        var game = new GameState();
        game.Base[1] = game.Base[2] = game.Base[3] = true;
        Equal(1, game.ApplyWalk(), "forced walk score");
        Equal(1, game.Runs, "user score");
        Equal(3, game.RunnersOn(), "loaded after walk");
        Equal(4, game.ApplyHit(4), "grand slam");
        Equal(5, game.Runs, "total runs");
        Equal(0, game.RunnersOn(), "bases clear after HR");
        game.UserBatting = false;
        Equal(1, game.ApplyHit(4), "opponent HR");
        Equal(1, game.OppRuns, "opponent score separated");
        game.Outs = 2; game.Base[3] = true;
        Equal(0, game.ApplyOut(PlayKind.FlyOut, 8, 90f), "third out no sac fly");
        Equal(3, game.Outs, "third out recorded");
        Equal(1, game.OppRuns, "third out no run");
    }

    public static void HalfInnings()
    {
        var game = new GameState();
        Check(game.UserBatsFirst && game.UserBatting && game.IsTop, "first match away");
        game.Outs = 3; game.Base[1] = true; game.Balls = 2; game.Strikes = 1;
        Check(game.EndInningIfNeeded(), "switch top");
        Check(!game.IsTop && !game.UserBatting && game.Inning == 1, "bottom first");
        Equal(0, game.Outs, "outs reset"); Equal(0, game.RunnersOn(), "runners reset");
        game.Outs = 3; game.EndInningIfNeeded();
        Check(game.Inning == 2 && game.IsTop, "next inning");
        game.NewGame();
        Check(!game.UserBatsFirst && !game.UserBatting && game.IsTop, "next match home");
        game.Inning = 3; game.Runs = 1; game.OppRuns = 0; game.Outs = 3;
        game.EndInningIfNeeded();
        Check(game.GameOver && !game.IsWalkOff, "home lead skips final bottom");
        game = new GameState { Inning = 3, UserBatting = false, Outs = 3 };
        game.EndInningIfNeeded();
        Check(game.GameOver, "tie ends after three");
    }

    public static void WalkOff()
    {
        var game = new GameState(); game.NewGame();
        game.Inning = 3; game.UserBatting = true;
        game.Base[1] = game.Base[2] = game.Base[3] = true;
        game.ApplyWalk();
        Check(game.GameOver && game.IsWalkOff && game.Runs == 1, "user walk-off walk");
        int runs = game.Runs, outs = game.Outs, runners = game.RunnersOn();
        Equal(0, game.ApplyWalk(), "late walk ignored");
        Equal(0, game.ApplyHit(4), "late hit ignored");
        Equal(0, game.ApplyStrikeout(), "late strikeout ignored");
        Equal(0, game.ApplyOut(PlayKind.GroundOut, 3, 20f), "late out ignored");
        Equal(runs, game.Runs, "finished score fixed");
        Equal(outs, game.Outs, "finished outs fixed");
        Equal(runners, game.RunnersOn(), "finished bases fixed");
        Check(!game.ApplySteal(1, true), "late steal ignored");
        game = new GameState { Inning = 3, UserBatting = false };
        game.ApplyHit(4);
        Check(game.GameOver && game.IsWalkOff && game.OppRuns == 1, "opponent walk-off HR");
        game = new GameState { Inning = 2, UserBatting = false };
        game.ApplyHit(4);
        Check(!game.GameOver && !game.IsWalkOff, "early lead not walk-off");
    }

    public static void Steals()
    {
        var game = new GameState { Balls = 2, Strikes = 1 };
        Check(!game.CanSteal(1), "empty base");
        game.Base[1] = game.Base[2] = true;
        Check(!game.CanSteal(1), "occupied destination");
        game.Base[2] = false;
        Check(game.ApplySteal(1, true), "accepted steal");
        Check(!game.Base[1] && game.Base[2], "runner moves");
        Equal(1, game.UserSteals, "steal reward counter");
        Equal(2, game.Balls, "steal preserves balls"); Equal(1, game.Strikes, "steal preserves strikes");
        game.Outs = 2;
        Check(game.ApplySteal(2, false), "caught stealing");
        Equal(3, game.Outs, "caught is third out");
        Equal(0, game.RunnersOn(), "caught runner removed");
        game.EndInningIfNeeded();
        Check(!game.UserBatting, "caught third changes side");
    }

#if UNITY_EDITOR
    public static void CameraBounds()
    {
        for (int angle = -45; angle <= 45; angle += 15)
        {
            float phi = angle * Mathf.Deg2Rad;
            float fence = BattingConfig.FenceRadius(angle);
            var direction = new Vector3(Mathf.Sin(phi), 0f, Mathf.Cos(phi));
            foreach (float height in new[] { -100f, 0f, 30f, 300f })
            {
                foreach (float radius in new[] { fence, 300f, 2000f })
                {
                    Vector3 position, look;
                    BattingStage.FollowFraming(direction * radius + Vector3.up * height, 18f, out position, out look);
                    float cameraRadius = new Vector2(position.x, position.z).magnitude;
                    float lookRadius = new Vector2(look.x, look.z).magnitude;
                    Check(cameraRadius <= fence - 20f, "camera remains inside fence " + angle);
                    Check(position.y >= 25f && position.y <= 48f, "camera height bounded");
                    Check(lookRadius <= fence + 8.01f, "look remains over stands");
                    Check((look - position).sqrMagnitude > 1f, "valid look direction");
                }
            }
        }
    }
#endif

    private static void Check(bool condition, string context)
    {
        if (!condition) throw new InvalidOperationException(context);
    }

    private static void Equal<T>(T expected, T actual, string context)
    {
        Check(EqualityComparer<T>.Default.Equals(expected, actual), context + ": expected " + expected + ", got " + actual);
    }

    private sealed class SavedPreferences : IDisposable
    {
        private readonly Dictionary<string, int> _values = new Dictionary<string, int>();
        private readonly List<string> _absent = new List<string>();
        public SavedPreferences()
        {
            Save("gunya.coins"); Save("gunya.exchange.points"); Save("gunya.gacha.pity4"); Save("gunya.gacha.pulls");
            foreach (var d in CharaCatalog.All) Save("gunya.card." + d.Id);
        }
        private void Save(string key)
        {
            if (PlayerPrefs.HasKey(key)) _values[key] = PlayerPrefs.GetInt(key);
            else _absent.Add(key);
        }
        public void Dispose()
        {
            foreach (var pair in _values) PlayerPrefs.SetInt(pair.Key, pair.Value);
            foreach (var key in _absent) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
#endif
