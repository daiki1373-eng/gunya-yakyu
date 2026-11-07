#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Claudeの再生確認用(エディタ専用)。試合中の状態を作って、進行が正しく続くかを自動で確かめる。
// 結果は DevScenario.Log に入る。どれも試合を AutoPlay で進めながら行う。
public class DevScenario : MonoBehaviour
{
    public static readonly List<string> Log = new List<string>();
    public static string Status = "";
    private const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;

    private static DevScenario Host()
    {
        var go = new GameObject("DevScenario");
        return go.AddComponent<DevScenario>();
    }

    private static AtBatController Ctl() { return FindObjectOfType<AtBatController>(); }
    private static T Get<T>(AtBatController c, string f) { return (T)typeof(AtBatController).GetField(f, F).GetValue(c); }
    private static void Set(AtBatController c, string f, object v) { typeof(AtBatController).GetField(f, F).SetValue(c, v); }
    private static void Say(string s) { Log.Add(Time.unscaledTime.ToString("F0") + "s " + s); Debug.Log("[DevScenario] " + s); }

    // ---- 1. 二死から盗塁死 → 次の攻撃は同じ打者から ----
    public static void StealOut() { Host().StartCoroutine(StealOutCo()); }
    private static IEnumerator StealOutCo()
    {
        Status = "stealout";
        AtBatController.DevStealAttempt = 1; AtBatController.DevStealSafe = 0;
        for (int attempt = 1; attempt <= 8; attempt++)
        {
            var c = Ctl(); var gs = Get<GameState>(c, "_gs");
            Say("stealout: attempt " + attempt + " 自チームの攻撃を待つ");
            float t0 = Time.unscaledTime;
            // 投球の準備に入った打席(打者が決まっている)を待つ
            while (!(gs.UserBatting && !gs.GameOver && gs.Outs < 2 && Get<bool>(c, "_stealInput")) && Time.unscaledTime - t0 < 200f) { yield return null; gs = Get<GameState>(c, "_gs"); }
            int idx = Get<int>(c, "_batterIndex");
            var team = Get<CharaRoster.Team>(c, "_userTeam");
            int n = team.Lineup.Count, prevIdx = (idx + n - 1) % n;
            gs.Outs = 2; gs.Base[1] = true; gs.Base[2] = false;
            Get<CharaDef[]>(c, "_onBase")[1] = team.Lineup[prevIdx];
            Say("set: 2 outs, runner on 1st, batter=" + team.Lineup[idx].Name + " idx=" + idx + " count=" + gs.Balls + "-" + gs.Strikes);
            t0 = Time.unscaledTime;
            bool stealOut = false, ended = false;
            while (Time.unscaledTime - t0 < 60f)
            {
                int cur = Get<int>(c, "_batterIndex");
                // 盗塁死でチェンジなら打順が1つ戻る(攻守交代より先に起きる)
                if (cur == prevIdx) { stealOut = true; ended = true; break; }
                if (!gs.UserBatting || gs.GameOver) { ended = true; break; }
                if (cur != idx) break;
                yield return null;
            }
            if (!stealOut) { Say("stealout: 盗塁死より先に打席が終わった ended=" + ended + " → やり直し"); continue; }
            Say("stealout: 盗塁死でチェンジ。打順を1つ戻した(index=" + prevIdx + ")");
            t0 = Time.unscaledTime;
            // 相手の攻撃 → 次の自チームの攻撃の最初の打席(投球の準備)まで待つ
            while (gs.UserBatting && Time.unscaledTime - t0 < 150f) yield return null;
            while (!(gs.UserBatting && Get<bool>(c, "_stealInput")) && Time.unscaledTime - t0 < 200f) { yield return null; gs = Get<GameState>(c, "_gs"); }
            int next = Get<int>(c, "_batterIndex");
            bool ok = next == idx;
            Say("stealout: 次の攻撃の先頭 idx=" + next + " (" + team.Lineup[next].Name + ") 期待 idx=" + idx + " → " + (ok ? "PASS" : "FAIL"));
            Status = ok ? "pass" : "fail";
            AtBatController.DevStealAttempt = -1; AtBatController.DevStealSafe = -1;
            yield break;
        }
        AtBatController.DevStealAttempt = -1; AtBatController.DevStealSafe = -1;
        Status = "gaveup";
    }

    // ---- 2. サヨナラ(3回裏・同点・満塁・無死、自チームが後攻) ----
    public static void WalkOff() { Host().StartCoroutine(WalkOffCo()); }
    private static IEnumerator WalkOffCo()
    {
        Status = "walkoff";
        var c = Ctl(); var gs = Get<GameState>(c, "_gs");
        float t0 = Time.unscaledTime;
        // 自チームの打席(投球の準備)を待ち、その試合を「自チームが後攻」の3回裏にする
        while (!(gs.UserBatting && !gs.GameOver && Get<bool>(c, "_stealInput")) && Time.unscaledTime - t0 < 400f) { yield return null; gs = Get<GameState>(c, "_gs"); }
        gs.UserBatsFirst = false;
        gs.Balls = 3;   // 押し出しでも決まりやすく
        int game = gs.GameNo;
        gs.Inning = GameState.InningsPerGame; gs.Outs = 0; gs.Runs = gs.OppRuns;
        var team = Get<CharaRoster.Team>(c, "_userTeam");
        var onBase = Get<CharaDef[]>(c, "_onBase");
        for (int b = 1; b <= 3; b++) { gs.Base[b] = true; onBase[b] = team.Lineup[b % team.Lineup.Count]; }
        int logBefore = BgmPlayer.Log.Count, coins = Collection.Coins;
        Say("set: game" + game + " 3回裏 同点 " + gs.Runs + "-" + gs.OppRuns + " 無死満塁");
        t0 = Time.unscaledTime;
        while (gs.GameNo == game && Time.unscaledTime - t0 < 120f)
        {
            if (gs.Runs > gs.OppRuns && !gs.IsWalkOff && !gs.GameOver && !gs.UserBatting) break;
            yield return null;
        }
        bool sayonara = false;
        for (int i = logBefore; i < BgmPlayer.Log.Count; i++) if (BgmPlayer.Log[i].Contains("bgm-sayonara")) sayonara = true;
        string last = AtBatController.GameLog.Count > 0 ? AtBatController.GameLog[AtBatController.GameLog.Count - 1] : "";
        Say("walkoff: game advanced=" + (gs.GameNo != game) + " sayonaraBGM=" + sayonara + " coins+" + (Collection.Coins - coins) + " last=" + last);
        Status = (gs.GameNo != game || gs.GameOver) ? "done" : "stuck";
    }

    // ---- 3. 演出の連打スキップ(数分) ----
    public static void SkipSpam(float seconds) { Host().StartCoroutine(SkipSpamCo(seconds)); }
    private static IEnumerator SkipSpamCo(float seconds)
    {
        Status = "skipspam";
        var c = Ctl(); var gs = Get<GameState>(c, "_gs");
        float t0 = Time.unscaledTime, lastProgress = t0;
        int lastKey = -1, taps = 0, stalls = 0, carried = 0;
        while (Time.unscaledTime - t0 < seconds)
        {
            if (Get<bool>(c, "_presentationInput")) { Set(c, "_skipQueued", true); taps++; }
            int key = gs.GameNo * 100000 + gs.PlateAppearances * 100 + gs.Balls * 10 + gs.Strikes + gs.Outs * 1000;
            if (key != lastKey) { lastKey = key; lastProgress = Time.unscaledTime; }
            if (Time.unscaledTime - lastProgress > 25f) { stalls++; Say("skipspam: 25秒進まない pa=" + gs.PlateAppearances + " inning=" + gs.Inning); lastProgress = Time.unscaledTime; }
            if (!Get<bool>(c, "_presentationInput") && Get<bool>(c, "_swingQueued") && gs.Balls == 0 && gs.Strikes == 0) carried++;
            yield return null;
        }
        Say("skipspam: " + seconds + "s taps=" + taps + " stalls=" + stalls + " swingCarried(frames)=" + carried + " pa=" + gs.PlateAppearances + " games=" + AtBatController.GameLog.Count);
        Status = stalls == 0 ? "pass" : "fail";
    }

    // ---- 4. スロー/ヒットストップ中にメニュー → 復帰後の速度と音楽 ----
    public static void MenuDuringSlow(float waitSeconds) { Host().StartCoroutine(MenuCo(waitSeconds)); }
    private static IEnumerator MenuCo(float waitSeconds)
    {
        Status = "menu";
        float t0 = Time.unscaledTime;
        while (!(Time.timeScale < 0.99f) && Time.unscaledTime - t0 < waitSeconds) yield return null;
        float ts = Time.timeScale;
        if (ts >= 0.99f) { Say("menu: スロー/ヒットストップが起きなかった"); Status = "retry"; yield break; }
        AppShell.Instance.OpenMenu();
        Say("menu: opened during timeScale=" + ts.ToString("F2") + " bgm=" + BgmPlayer.Current);
        float t1 = Time.unscaledTime;
        while (Time.unscaledTime - t1 < 1.5f) yield return null;
        string inMenu = "inMenu ts=" + Time.timeScale.ToString("F2") + " bgm=" + BgmPlayer.Current;
        AppShell.Instance.StartGame();
        t1 = Time.unscaledTime;
        while (Time.unscaledTime - t1 < 3f) yield return null;
        Say("menu: " + inMenu + " → after resume ts=" + Time.timeScale.ToString("F2") + " bgm=" + BgmPlayer.Current);
        Status = Mathf.Approximately(Time.timeScale, 1f) ? "pass" : "fail";
    }

    // ---- 4b. コイン: 不足時は引けない / 10連で1,000枚 / 300枚→15pt / スカウトはコインを使わない ----
    // 保存されている所持・天井・ポイント・コインは最後に元へ戻す。
    public static void CoinFlow() { Host().StartCoroutine(CoinCo()); }
    private static IEnumerator CoinCo()
    {
        Status = "coins";
        var counts = new Dictionary<string, int>();
        foreach (var d in CharaCatalog.All) counts[d.Id] = Collection.Count(d.Id);
        int pity = Collection.PityCount, pulls = Collection.TotalPulls, points = Collection.Points;
        bool hadCoins = PlayerPrefs.HasKey("gunya.coins"); int coins = Collection.Coins;
        var app = AppShell.Instance;
        app.ShowGacha();
        yield return null;
        // 不足
        Collection.Coins = 50; int p0 = Collection.TotalPulls;
        app.Pull(1);
        yield return null;
        Say("coins: 50枚で1回 → 引けた=" + (Collection.TotalPulls != p0) + " 残高=" + Collection.Coins + " → " + (Collection.TotalPulls == p0 && Collection.Coins == 50 ? "PASS" : "FAIL"));
        // 10連
        Collection.Coins = 1000; p0 = Collection.TotalPulls;
        app.Pull(10);
        yield return null;
        bool ok10 = Collection.TotalPulls == p0 + 10 && Collection.Coins == 0;
        Say("coins: 1,000枚で10連 → 引いた=" + (Collection.TotalPulls - p0) + " 残高=" + Collection.Coins + " → " + (ok10 ? "PASS" : "FAIL"));
        // 排出演出が終わる(画面のボタンが使える)まで待つ。演出中はタップで飛ばす
        var busyF = typeof(AppShell).GetField("_busy", F);
        var skipF = typeof(AppShell).GetField("_skipAll", F);
        var tapF = typeof(AppShell).GetField("_tapped", F);
        float t0 = Time.unscaledTime;
        while ((bool)busyF.GetValue(app) && Time.unscaledTime - t0 < 40f) { skipF.SetValue(app, true); tapF.SetValue(app, true); yield return null; }
        Say("coins: 演出の終了まで " + (Time.unscaledTime - t0).ToString("F1") + "秒");
        // 交換
        Collection.Coins = 310; int pt0 = Collection.Points;
        foreach (var b in app.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            if (b.name == "ExchangeCoins") b.onClick.Invoke();
        Say("coins: 310枚で交換 → 残高=" + Collection.Coins + " pt+" + (Collection.Points - pt0) + " → " + (Collection.Coins == 10 && Collection.Points == pt0 + 15 ? "PASS" : "FAIL"));
        foreach (var b in app.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            if (b.name == "ExchangeCoins") b.onClick.Invoke();
        Say("coins: 10枚で交換 → 残高=" + Collection.Coins + " pt=" + Collection.Points + " → " + (Collection.Coins == 10 && Collection.Points == pt0 + 15 ? "PASS" : "FAIL"));
        // スカウト(ポイント消費・コインは使わない)
        Collection.Points = 30; Collection.Coins = 500; p0 = Collection.TotalPulls;
        foreach (var b in app.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            if (b.name == "Scout") b.onClick.Invoke();
        yield return null;
        t0 = Time.unscaledTime;
        while ((bool)busyF.GetValue(app) && Time.unscaledTime - t0 < 20f) { skipF.SetValue(app, true); tapF.SetValue(app, true); yield return null; }
        Say("coins: スカウト → pt=" + Collection.Points + " コイン=" + Collection.Coins + " 引いた=" + (Collection.TotalPulls - p0) + " → " + (Collection.Points == 5 && Collection.Coins == 500 && Collection.TotalPulls == p0 + 1 ? "PASS" : "FAIL"));
        t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < 2f) yield return null;
        // 保存値の確認(PlayerPrefs から読み直す)
        PlayerPrefs.Save();
        Say("coins: 保存値 gunya.coins=" + PlayerPrefs.GetInt("gunya.coins", -1));
        // 元に戻す
        foreach (var kv in counts) { if (kv.Value > 0) PlayerPrefs.SetInt("gunya.card." + kv.Key, kv.Value); else PlayerPrefs.DeleteKey("gunya.card." + kv.Key); }
        Collection.PityCount = pity; Collection.TotalPulls = pulls; Collection.Points = points;
        if (hadCoins) Collection.Coins = coins; else PlayerPrefs.DeleteKey("gunya.coins");
        PlayerPrefs.Save();
        int diff = 0;
        foreach (var d in CharaCatalog.All) if (Collection.Count(d.Id) != counts[d.Id]) diff++;
        Say("coins: 所持・天井・pt・コインを元に戻した(所持の差=" + diff + ")");
        Status = "done";
    }

    // ---- 5a. 選手の体の準備時間(初回=重み計算あり / 2回目=使い回し) ----
    public static string SkinTiming(string[] ids)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var id in ids)
        {
            var d = CharaCatalog.Get(id);
            if (d == null) continue;
            var stats = CharaRoster.Stats(d);
            double first = 0, second = 0;
            for (int k = 0; k < 2; k++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var p = RagdollPlayer.Create(null, "T_" + id, new Vector2(800f, 0f), 1f, stats, Color.white, Color.red, true);
                sw.Stop();
                if (k == 0) first = sw.Elapsed.TotalMilliseconds; else second = sw.Elapsed.TotalMilliseconds;
                DestroyImmediate(p.gameObject);
            }
            sb.Append(id).Append(" ").Append(first.ToString("F1")).Append("→").Append(second.ToString("F1")).Append("ms  ");
        }
        return sb.ToString();
    }

    // ---- 5b. カード一覧を開いて上から下まで送り、画面を戻す。フレーム時間と画像メモリ ----
    public static void CardScroll() { Host().StartCoroutine(CardScrollCo()); }
    private static IEnumerator CardScrollCo()
    {
        Status = "cards";
        var app = AppShell.Instance;
        app.ShowHome();
        float t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < 1.5f) yield return null;
        Say("cards: before " + Memory() + " cached=" + CharaCatalog.Cached);
        app.ShowCards();
        float worst = 0f; int frames = 0;
        t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < 1f) { worst = Mathf.Max(worst, Time.unscaledDeltaTime); frames++; yield return null; }
        Say("cards: opened worst=" + (worst * 1000f).ToString("F0") + "ms " + Memory() + " cached=" + CharaCatalog.Cached);
        var scroll = app.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
        worst = 0f; frames = 0;
        for (int pass = 0; pass < 2 && scroll != null; pass++)
        {
            for (float v = 1f; v >= 0f; v -= 0.01f) { scroll.verticalNormalizedPosition = pass == 0 ? v : 1f - v; worst = Mathf.Max(worst, Time.unscaledDeltaTime); frames++; yield return null; }
        }
        Say("cards: scrolled x2 worst=" + (worst * 1000f).ToString("F0") + "ms frames=" + frames + " " + Memory() + " cached=" + CharaCatalog.Cached + " loads=" + CharaCatalog.Loads);
        app.ShowHome();
        t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < 2.5f) yield return null;
        Say("cards: closed " + Memory() + " cached=" + CharaCatalog.Cached + " released=" + CharaCatalog.Released);
        Status = "done";
    }

    // ---- 5. 負荷: 選手の初登場/再登場、一覧のスクロール ----
    public static string Memory()
    {
        long tex = 0; int n = 0;
        foreach (var t in Resources.FindObjectsOfTypeAll<Texture2D>())
        {
            if (t.name.Length == 0) continue;
            tex += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t); n++;
        }
        return "tex=" + (tex / 1048576f).ToString("F1") + "MB(" + n + ") mono=" + (UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576f).ToString("F1")
            + "MB alloc=" + (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f).ToString("F1") + "MB";
    }
}
#endif
