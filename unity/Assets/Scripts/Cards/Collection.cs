using UnityEngine;

// ============================================================
// Collection.cs
// 所持カード(重ね数)とガチャの天井カウンタを保存する。
// 保存先は PlayerPrefs(WebGLではブラウザ内に保存される)。
// 重ねは上限まで。上限を超えた分は数えない(仮置き)。
// ============================================================
public static class Collection
{
    private const string Prefix = "gunya.card.";
    private const string PityKey = "gunya.gacha.pity4";
    private const string PullsKey = "gunya.gacha.pulls";
    private const string CoinsKey = "gunya.coins";

    // 仮のゲーム内通貨。初期配布は新規保存データだけに適用する。
    public const int StartingCoins = 1000;
    public const int PullCoinCost = 100;
    public const int ExchangeCoinCost = 300;
    public const int ExchangeCoinPoints = 15;

    public static int Coins
    {
        get { return Mathf.Max(0, PlayerPrefs.GetInt(CoinsKey, StartingCoins)); }
        set { PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, value)); }
    }

    public static bool TrySpendCoins(int amount)
    {
        if (amount <= 0 || Coins < amount) return false;
        Coins -= amount;
        return true;
    }

    public static bool ExchangeCoins()
    {
        if (Points > int.MaxValue - ExchangeCoinPoints || !TrySpendCoins(ExchangeCoinCost)) return false;
        Points += ExchangeCoinPoints;
        Save();
        return true;
    }

    // 勝敗と内容による仮置き報酬。長い試合で際限なく増えないよう各加点を制限する。
    public static int CalculateMatchReward(int runs, int opponentRuns, int hits, int steals)
    {
        int result = runs > opponentRuns ? 120 : runs == opponentRuns ? 60 : 0;
        return 60 + result + Mathf.Clamp(runs, 0, 10) * 10
            + Mathf.Clamp(hits, 0, 20) * 3 + Mathf.Clamp(steals, 0, 6) * 10;
    }

    public static int AwardMatch(GameState state)
    {
        if (state == null || !state.GameOver || state.RewardClaimed) return 0;
        int reward = CalculateMatchReward(state.Runs, state.OppRuns, state.UserHits, state.UserSteals);
        int awarded = Mathf.Min(reward, int.MaxValue - Coins);
        Coins += awarded;
        state.RewardClaimed = true;
        Save();
        return awarded;
    }

    public static int Count(string id) { return PlayerPrefs.GetInt(Prefix + id, 0); }
    public static bool Owned(string id) { return Count(id) > 0; }

    public struct AddResult { public int Before, After; public bool IsNew, Capped; }

    public static AddResult Add(CharaDef d)
    {
        int before = Count(d.Id);
        int max = CharaCatalog.StackMax(d);
        int after = Mathf.Min(max, before + 1);
        PlayerPrefs.SetInt(Prefix + d.Id, after);
        return new AddResult { Before = before, After = after, IsNew = before == 0, Capped = before >= max };
    }

    // ★4天井: ★4以上が出ないまま数えた回数
    public static int PityCount
    {
        get { return PlayerPrefs.GetInt(PityKey, 0); }
        set { PlayerPrefs.SetInt(PityKey, value); }
    }

    public static int TotalPulls
    {
        get { return PlayerPrefs.GetInt(PullsKey, 0); }
        set { PlayerPrefs.SetInt(PullsKey, value); }
    }

    // ---- 交換所: ダブったカード(ベンチ選手)を交換ポイントにする ----
    private const string PointsKey = "gunya.exchange.points";
    public const int ScoutCost = 25;   // スカウトチケット1枚(=1回引ける)

    public static int Points
    {
        get { return PlayerPrefs.GetInt(PointsKey, 0); }
        set { PlayerPrefs.SetInt(PointsKey, Mathf.Max(0, value)); }
    }

    // 1枚を交換ポイントにする。最後の1枚は残す(図鑑から消えないように)
    public static bool Exchange(CharaDef d)
    {
        int c = Count(d.Id);
        if (c < 2) return false;
        PlayerPrefs.SetInt(Prefix + d.Id, c - 1);
        Points = Points + d.ExchangePoints;
        Save();
        return true;
    }

    public static int OwnedKinds()
    {
        int n = 0;
        foreach (var d in CharaCatalog.All) if (Owned(d.Id)) n++;
        return n;
    }

    public static void Save() { PlayerPrefs.Save(); }

    // 検証用: 所持をすべて消す
    public static void ResetAll()
    {
        foreach (var d in CharaCatalog.All) PlayerPrefs.DeleteKey(Prefix + d.Id);
        PlayerPrefs.DeleteKey(PityKey);
        PlayerPrefs.DeleteKey(PullsKey);
        PlayerPrefs.DeleteKey(PointsKey);
        PlayerPrefs.DeleteKey(CoinsKey);
        PlayerPrefs.Save();
    }
}
