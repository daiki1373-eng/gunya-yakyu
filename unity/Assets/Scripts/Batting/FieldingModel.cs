using UnityEngine;

// ============================================================
// FieldingModel.cs
// 打球の軌道から「アウト/ヒット/長打」を決める簡易守備モデル(P1)。
// 野手は守備位置から一定速度で走る。ボールが捕れる高さに来た最初の瞬間に
// 届く野手がいればそこで捕球。空中なら飛球アウト、バウンド後なら送球勝負。
// 送球の到着と打者走者の到達を比べて、アウト/単打/二塁打/三塁打を決める。
// (P1は走者なし。走者・併殺はP2で接続する)
//
// 数値はすべて仮置き。守備位置は高校野球の標準的な配置を自前で置いたもの。
// ============================================================
public enum PlayKind
{
    Foul, FoulTip, FoulFlyOut,
    HomeRun, Single, Double, Triple,
    GroundOut, FlyOut, LineOut, PopOut
}

public struct PlayResult
{
    public PlayKind Kind;
    public int Fielder;        // 捕球した野手(-1=なし)
    public Vector3 PlayPos;    // 捕球/拾った地点
    public float PlayTime;     // その時刻(打球からの秒)
    public float EndTime;      // 再生を打ち切る時刻
    public float Distance;     // 飛距離(初回接地または柵越え地点、m)
    public bool IsOut;
    public bool IsHit;
}

public static class FieldingModel
{
    public struct Fielder
    {
        public string Name;
        public Vector2 Pos;   // (x,z)
        public float Speed;   // m/s
    }

    // 並び: 0=投手 1=捕手 2=一塁 3=二塁 4=三塁 5=遊撃 6=左翼 7=中堅 8=右翼
    public static readonly Fielder[] Defaults = new Fielder[]
    {
        new Fielder { Name = "P",  Pos = new Vector2(0f, 17f),    Speed = 6.0f },
        new Fielder { Name = "C",  Pos = new Vector2(0f, -1.6f),  Speed = 6.0f },
        new Fielder { Name = "1B", Pos = new Vector2(17f, 22f),   Speed = 6.8f },
        new Fielder { Name = "2B", Pos = new Vector2(8f, 33f),    Speed = 7.2f },
        new Fielder { Name = "3B", Pos = new Vector2(-17f, 22f),  Speed = 6.8f },
        new Fielder { Name = "SS", Pos = new Vector2(-8f, 33f),   Speed = 7.2f },
        new Fielder { Name = "LF", Pos = new Vector2(-34f, 68f),  Speed = 7.6f },
        new Fielder { Name = "CF", Pos = new Vector2(0f, 80f),    Speed = 7.8f },
        new Fielder { Name = "RF", Pos = new Vector2(34f, 68f),   Speed = 7.6f },
    };

    public const float React = 0.50f;       // 打球への反応時間
    public const float Reach = 0.7f;        // グラブの届く距離
    public const float RunEfficiency = 0.80f; // 最短経路で走れない分の効率
    public const float CatchHeight = 2.6f;  // 捕れる最大の高さ
    public const float ThrowSpeed = 32f;    // 送球速度 m/s
    public const float ThrowTransfer = 0.55f; // 捕ってから投げるまで
    public const float BaseLen = 27.4f;

    private static readonly Vector2[] Bases = new Vector2[]
    {
        new Vector2(19.4f, 19.4f),   // 一塁
        new Vector2(0f, 38.8f),      // 二塁
        new Vector2(-19.4f, 19.4f),  // 三塁
    };

    // 打者走者が塁に着く時刻(打球から)。走力はPlayerStats.Speed。
    public static float RunnerTimeToBase(int n, int speedStat)
    {
        float runSpeed = 6.6f + (speedStat - 40) * 0.03f;
        return 0.35f + n * BaseLen / runSpeed + 0.15f * (n - 1);
    }

    public static PlayResult Resolve(BattedBallTrajectory tr, int batterSpeed)
    {
        var res = new PlayResult
        {
            Fielder = -1,
            PlayPos = tr.LandPos,
            PlayTime = tr.HangTime,
            EndTime = tr.Duration,
            Distance = tr.LandDistance
        };

        if (tr.HomeRun)
        {
            res.Kind = PlayKind.HomeRun;
            res.IsHit = true;
            res.PlayTime = tr.HomeRunIndex * BattedBallTrajectory.Dt;
            res.PlayPos = tr.Points[Mathf.Min(tr.HomeRunIndex, tr.Points.Count - 1)];
            res.Distance = new Vector2(res.PlayPos.x, res.PlayPos.z).magnitude;
            res.EndTime = Mathf.Min(tr.Duration, res.PlayTime + 1.4f);
            return res;
        }

        // 最初に誰かが追いつく瞬間を探す
        int hitIdx = -1;
        int who = -1;
        for (int k = 0; k < tr.Points.Count; k++)
        {
            Vector3 p = tr.Points[k];
            if (p.y > CatchHeight) continue;
            float t = k * BattedBallTrajectory.Dt;
            if (t < React) continue;
            float bestMargin = -1f;
            for (int f = 0; f < Defaults.Length; f++)
            {
                float d = Vector2.Distance(Defaults[f].Pos, new Vector2(p.x, p.z));
                float avail = (t - React) * Defaults[f].Speed * RunEfficiency + Reach;
                float margin = avail - d;
                if (margin >= 0f && margin > bestMargin) { bestMargin = margin; who = f; }
            }
            if (who >= 0) { hitIdx = k; break; }
        }

        bool airborne = hitIdx >= 0 && hitIdx <= tr.LandIndex;

        if (airborne)
        {
            res.Fielder = who;
            res.PlayPos = tr.Points[hitIdx];
            res.PlayTime = hitIdx * BattedBallTrajectory.Dt;
            res.EndTime = Mathf.Min(tr.Duration, res.PlayTime + 0.5f);
            res.IsOut = true;
            if (tr.Foul) res.Kind = PlayKind.FoulFlyOut;
            else if (tr.LaunchDeg >= 50f) res.Kind = PlayKind.PopOut;
            else if (tr.LaunchDeg <= 24f) res.Kind = PlayKind.LineOut;
            else res.Kind = PlayKind.FlyOut;
            return res;
        }

        if (tr.Foul)
        {
            res.Kind = PlayKind.Foul;
            res.EndTime = Mathf.Min(tr.Duration, tr.HangTime + 0.6f);
            return res;
        }

        // 地面で拾われる(拾えなければ止まったボールを近い野手が拾う)
        if (hitIdx < 0)
        {
            hitIdx = tr.Points.Count - 1;
            Vector3 q = tr.Points[hitIdx];
            float bestT = float.MaxValue;
            for (int f = 0; f < Defaults.Length; f++)
            {
                float d = Vector2.Distance(Defaults[f].Pos, new Vector2(q.x, q.z));
                float tt = hitIdx * BattedBallTrajectory.Dt + Mathf.Max(0f, d - Reach) / (Defaults[f].Speed * RunEfficiency);
                if (tt < bestT) { bestT = tt; who = f; }
            }
            res.PlayTime = bestT;
        }
        else
        {
            res.PlayTime = hitIdx * BattedBallTrajectory.Dt;
        }
        res.Fielder = who;
        res.PlayPos = tr.Points[Mathf.Min(hitIdx, tr.Points.Count - 1)];
        res.EndTime = Mathf.Min(tr.Duration, res.PlayTime + 0.5f);

        Vector2 pick = new Vector2(res.PlayPos.x, res.PlayPos.z);
        float tFirst = res.PlayTime + ThrowTransfer + Vector2.Distance(pick, Bases[0]) / ThrowSpeed;
        if (tFirst < RunnerTimeToBase(1, batterSpeed))
        {
            res.Kind = PlayKind.GroundOut;
            res.IsOut = true;
            return res;
        }

        // セーフ: 何塁まで行けるか(送球が来る前に着ける最も先の塁)
        int bases = 1;
        for (int n = 3; n >= 2; n--)
        {
            float tThrow = res.PlayTime + ThrowTransfer + Vector2.Distance(pick, Bases[n - 1]) / ThrowSpeed;
            if (RunnerTimeToBase(n, batterSpeed) + 0.4f < tThrow) { bases = n; break; }
        }
        if (bases == 3 && !tr.HitWall) bases = 2; // 三塁打はフェンス直撃の深い当たりだけ
        res.IsHit = true;
        res.Kind = bases == 3 ? PlayKind.Triple : (bases == 2 ? PlayKind.Double : PlayKind.Single);
        return res;
    }

    public static string Label(PlayKind k)
    {
        switch (k)
        {
            case PlayKind.HomeRun: return "ホームラン！";
            case PlayKind.Single: return "ヒット！";
            case PlayKind.Double: return "二塁打！";
            case PlayKind.Triple: return "三塁打！";
            case PlayKind.GroundOut: return "ゴロアウト";
            case PlayKind.FlyOut: return "フライアウト";
            case PlayKind.LineOut: return "ライナーアウト";
            case PlayKind.PopOut: return "内野フライ";
            case PlayKind.FoulFlyOut: return "ファウルフライ";
            case PlayKind.FoulTip: return "ファウルチップ";
            default: return "ファウル";
        }
    }
}
