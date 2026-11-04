using System.Collections.Generic;
using UnityEngine;

// ============================================================
// Gacha.cs
// 排出確率(決定済み仕様):
//   SECRET 0.01%(天井なし) / ★5 1%(天井なし) / ★4 3%(天井50連) /
//   ★3 12% / ★2 35% / ★1 48.99%
// ★4天井: ★4以上が出ないまま49回続いたら、50回目は★4を確定にする。
// 同じレア度の中では均等に選ぶ。
// ============================================================
public struct PullResult
{
    public CharaDef Chara;
    public Collection.AddResult Add;
    public bool ByPity;
}

public static class Gacha
{
    public const int PityLimit = 50;
    // 1,000,000 分率
    private const int PSecret = 100, P5 = 10000, P4 = 30000, P3 = 120000, P2 = 350000;

    public static System.Random Rng = new System.Random();

    public static Rarity RollRarity(System.Random rng)
    {
        int x = rng.Next(1000000);
        if (x < PSecret) return Rarity.Secret;
        x -= PSecret;
        if (x < P5) return Rarity.R5;
        x -= P5;
        if (x < P4) return Rarity.R4;
        x -= P4;
        if (x < P3) return Rarity.R3;
        x -= P3;
        if (x < P2) return Rarity.R2;
        return Rarity.R1;
    }

    // pity: 直前までの「★4以上なし」回数。戻り値で更新後の値を返す。
    public static CharaDef RollOne(System.Random rng, ref int pity, out bool byPity)
    {
        Rarity r = RollRarity(rng);
        byPity = false;
        if (r < Rarity.R4 && pity + 1 >= PityLimit)
        {
            r = Rarity.R4;
            byPity = true;
        }
        pity = r >= Rarity.R4 ? 0 : pity + 1;
        var pool = CharaCatalog.OfRarity(r);
        return pool[rng.Next(pool.Count)];
    }

    public static List<PullResult> Pull(int times)
    {
        var list = new List<PullResult>();
        int pity = Collection.PityCount;
        for (int i = 0; i < times; i++)
        {
            bool byPity;
            var d = RollOne(Rng, ref pity, out byPity);
            var add = Collection.Add(d);
            list.Add(new PullResult { Chara = d, Add = add, ByPity = byPity });
        }
        Collection.PityCount = pity;
        Collection.TotalPulls = Collection.TotalPulls + times;
        Collection.Save();
        return list;
    }

    // 検証用: 確率どおりに出るかを数える(保存はしない)
    public static string Simulate(int n, int seed)
    {
        var rng = new System.Random(seed);
        var cnt = new int[7];
        int pity = 0, pityHits = 0, maxGap = 0, gap = 0;
        for (int i = 0; i < n; i++)
        {
            bool bp;
            var d = RollOne(rng, ref pity, out bp);
            cnt[(int)d.Rarity]++;
            if (bp) pityHits++;
            if (d.Rarity >= Rarity.R4) { maxGap = Mathf.Max(maxGap, gap + 1); gap = 0; } else gap++;
        }
        var sb = new System.Text.StringBuilder();
        for (int r = 1; r <= 6; r++)
            sb.Append(CharaCatalog.Label((Rarity)r)).Append(" ").Append((100.0 * cnt[r] / n).ToString("F3")).Append("%  ");
        sb.Append(" 天井発動 ").Append(pityHits).Append(" 最大間隔 ").Append(maxGap);
        return sb.ToString();
    }
}
