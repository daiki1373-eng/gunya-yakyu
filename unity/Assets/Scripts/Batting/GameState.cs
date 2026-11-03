using System.Collections.Generic;
using UnityEngine;

// ============================================================
// GameState.cs
// 試合状態(P2): 回・アウト・走者・得点と、打席結果に応じた走者の進塁ルール。
// 画面にもUnityにも依存しない純粋なロジック(検証用の一括シミュレーションでも使う)。
// 進塁の確率はすべて自前の仮置き値。
// ============================================================
public struct RunnerMove
{
    public int From;      // 0=打者 1..3=塁上の走者
    public int To;        // 1..3=到達塁 / 4=本塁生還 / -1=アウト
    public int OutBase;   // アウトになった塁(0=走らない種類のアウト)
}

public class GameState
{
    public const int InningsPerGame = 3;     // 1試合のイニング数(P2の仮置き。P3で9回まで)
    public const int MaxQuirksPerGame = 2;   // 1試合あたりの目立つ珍プレー上限

    public int GameNo = 1;
    public int Inning = 1;
    public int Outs;
    public int Runs;                         // 自チームの得点
    public int OppRuns;                      // 相手の得点
    public bool UserBatting = true;          // true=表(自分が打つ) / false=裏(自分が投げる)
    public int Balls, Strikes;
    public int QuirkCount;
    public int PlateAppearances;
    public bool[] Base = new bool[4];        // 1..3 を使う
    public readonly List<RunnerMove> Moves = new List<RunnerMove>();
    public int LastRuns;                     // 直前のプレーで入った得点

    public bool QuirkAllowed { get { return QuirkCount < MaxQuirksPerGame; } }
    public bool GameOver { get { return Inning > InningsPerGame; } }

    public void NewGame()
    {
        GameNo++;
        Inning = 1; Outs = 0; Runs = 0; OppRuns = 0; UserBatting = true; QuirkCount = 0; PlateAppearances = 0;
        ClearBases();
        ResetCount();
    }

    public void ResetCount() { Balls = 0; Strikes = 0; }

    public void ClearBases()
    {
        for (int i = 0; i < Base.Length; i++) Base[i] = false;
    }

    public int RunnersOn()
    {
        int n = 0;
        for (int i = 1; i <= 3; i++) if (Base[i]) n++;
        return n;
    }

    // 3アウトで攻守交代。表(自分が打つ)→裏(自分が投げる)→次の回の表…
    public bool EndInningIfNeeded()
    {
        if (Outs < 3) return false;
        Outs = 0;
        ClearBases();
        if (UserBatting) UserBatting = false;
        else { UserBatting = true; Inning++; }
        return true;
    }

    // ------------------------------------------------------------
    // 進塁の計算
    // ------------------------------------------------------------
    private bool[] _nb;
    private int _runs, _outsAdded;

    private void Begin()
    {
        Moves.Clear();
        _nb = new bool[4];
        _runs = 0;
        _outsAdded = 0;
    }

    private void Go(int from, int to, int outBase = 0)
    {
        Moves.Add(new RunnerMove { From = from, To = to, OutBase = outBase });
        if (to >= 1 && to <= 3) _nb[to] = true;
        else if (to == 4) _runs++;
        else if (to == -1) _outsAdded++;
    }

    private void Hold(int from) { Go(from, from); }

    private int Finish(bool allowRunsOnThirdOut = false)
    {
        int newOuts = Outs + _outsAdded;
        if (newOuts >= 3 && !allowRunsOnThirdOut)
        {
            // 3アウト目で入った得点は取り消す(その走者は塁に戻した扱い)
            for (int i = 0; i < Moves.Count; i++)
            {
                if (Moves[i].To == 4)
                {
                    var m = Moves[i];
                    m.To = m.From == 0 ? -1 : m.From;
                    Moves[i] = m;
                }
            }
            _runs = 0;
        }
        Outs = newOuts;
        if (UserBatting) Runs += _runs; else OppRuns += _runs;
        LastRuns = _runs;
        for (int i = 0; i < 4; i++) Base[i] = _nb[i];
        return _runs;
    }

    // 四球・死球(押し出し式)
    public int ApplyWalk()
    {
        Begin();
        if (Base[1])
        {
            if (Base[2])
            {
                if (Base[3]) Go(3, 4);
                Go(2, 3);
            }
            else if (Base[3]) Hold(3);
            Go(1, 2);
        }
        else
        {
            if (Base[2]) Hold(2);
            if (Base[3]) Hold(3);
        }
        Go(0, 1);
        return Finish();
    }

    // ヒット(単打=1, 二塁打=2, 三塁打=3, 本塁打=4) / エラー扱いの進塁も同じ
    public int ApplyHit(int bases)
    {
        Begin();
        if (bases >= 4)
        {
            if (Base[3]) Go(3, 4);
            if (Base[2]) Go(2, 4);
            if (Base[1]) Go(1, 4);
            Go(0, 4);
            return Finish(true);
        }
        if (bases == 3)
        {
            for (int b = 3; b >= 1; b--) if (Base[b]) Go(b, 4);
            Go(0, 3);
            return Finish(true);
        }
        if (bases == 2)
        {
            if (Base[3]) Go(3, 4);
            if (Base[2]) Go(2, 4);
            if (Base[1])
            {
                if (Random.value < 0.55f) Go(1, 4); else Go(1, 3);
            }
            Go(0, 2);
            return Finish(true);
        }
        // 単打
        if (Base[3]) Go(3, 4);
        if (Base[2])
        {
            if (Random.value < 0.70f) Go(2, 4); else Go(2, 3);
        }
        if (Base[1])
        {
            if (!_nb[3] && Random.value < 0.40f) Go(1, 3); else Go(1, 2);
        }
        Go(0, 1);
        return Finish(true);
    }

    // 三振(振り逃げなし)
    public int ApplyStrikeout()
    {
        Begin();
        for (int b = 3; b >= 1; b--) if (Base[b]) Hold(b);
        Go(0, -1);
        return Finish();
    }

    // 飛球・ゴロなどのアウト。fielder=処理した野手(2..8)、depth=打球の飛距離(m)
    public int ApplyOut(PlayKind kind, int fielder, float depth)
    {
        Begin();
        bool fly = kind == PlayKind.FlyOut || kind == PlayKind.LineOut
                || kind == PlayKind.PopOut || kind == PlayKind.FoulFlyOut;
        if (fly)
        {
            bool sacFly = kind == PlayKind.FlyOut && fielder >= 6 && depth >= 55f && Outs < 2;
            if (Base[3])
            {
                if (sacFly && Random.value < 0.85f) Go(3, 4); else Hold(3);
            }
            if (Base[2])
            {
                if (sacFly && !_nb[3] && Random.value < 0.35f) Go(2, 3); else Hold(2);
            }
            if (Base[1]) Hold(1);
            Go(0, -1, 0);
            return Finish();
        }

        // ゴロ: 併殺・進塁打
        bool infield = fielder >= 2 && fielder <= 5;
        if (Base[1] && Outs < 2 && infield && Random.value < 0.50f)
        {
            Go(1, -1, 2);                     // 二塁でフォースアウト
            Go(0, -1, 1);                     // 一塁でアウト(併殺)
            if (Base[3]) { if (Base[2] && Base[1]) Go(3, 4); else if (Outs + _outsAdded < 3 && Random.value < 0.45f) Go(3, 4); else Hold(3); }
            if (Base[2]) { if (!_nb[3]) Go(2, 3); else Hold(2); }
            return Finish();
        }
        if (Base[3])
        {
            bool forced = Base[1] && Base[2];
            if (forced || (Outs < 2 && Random.value < 0.40f)) Go(3, 4); else Hold(3);
        }
        if (Base[2])
        {
            bool forced = Base[1];
            if (!_nb[3] && (forced || Random.value < 0.45f)) Go(2, 3); else Hold(2);
        }
        if (Base[1])
        {
            if (!_nb[2]) Go(1, 2); else Hold(1);
        }
        Go(0, -1, 1);
        return Finish();
    }
}
