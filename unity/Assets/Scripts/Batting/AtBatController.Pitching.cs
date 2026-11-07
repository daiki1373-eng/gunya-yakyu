using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AtBatController
{
    // ------------------------------------------------------------
    // 1球
    // ------------------------------------------------------------
    // 軌道: 変化なしの直線(リリース→aim)に、s^LateK で増える曲がり(Break)を足して Target に着く。
    private static Vector3 PitchPos(PitchPlan plan, float t)
    {
        float s = t / plan.FlightTime;
        Vector3 a = BattingConfig.ReleasePoint;
        Vector3 aim = new Vector3(plan.Target.x - plan.Break.x, plan.Target.y - plan.Break.y, 0f);
        Vector3 p = Vector3.LerpUnclamped(a, aim, s);
        float k = Mathf.Pow(Mathf.Clamp01(s), plan.LateK);
        p.x += plan.Break.x * k;
        p.y += plan.Break.y * k;
        return p;
    }

    private static Color TrailColor(PitchKind k)
    {
        switch (k)
        {
            case PitchKind.Curve: return new Color(0.45f, 0.70f, 1f);
            case PitchKind.Slider: return new Color(0.50f, 0.95f, 0.55f);
            case PitchKind.Fork: return new Color(1f, 0.62f, 0.25f);
            case PitchKind.Changeup: return new Color(1f, 0.95f, 0.45f);
            default: return new Color(1f, 1f, 1f);
        }
    }

    private bool IsBodyHit(PitchPlan plan)
    {
        return Mathf.Sign(plan.Target.x) == _batSide && Mathf.Abs(plan.Target.x) >= 0.92f
            && plan.Target.y > 0.4f && plan.Target.y < 1.9f;
    }

    private IEnumerator PlayPitch()
    {
        _pitchEnd = PitchEnd.None;
        PrepareSteal();
        _play = new PlayResult { Fielder = -1, Fielder2 = -1 };
        _pitchCount++;
        _stage.SetCameraMode(CamMode.Catcher);
        _ui.ClearCenter();
        _ui.ClearSpeed();
        _ui.ShowBanner("");
        if (_gs.UserBatting) _cursor.ResetColor();
        _cursorLocked = false;
        _stage.HideBall();
        _stage.ClearTrail();
        _ui.ShowMeter(false);
        _batter.ResetPose();
        _pitcher.ResetPose();

        PitchPlan plan;
        float tReleaseAt;
        if (_gs.UserBatting)
        {
            plan = PitchAI.ChoosePitch(_pitcherStats, _batterStats, _gs.Balls, _gs.Strikes,
                _batSide, _last1, _last2);
            yield return StartCoroutine(Wait(BattingConfig.PitchReadySeconds));
            _swingQueued = false;
            _pitcher.DoPitchMotion();
            tReleaseAt = Time.time + BattingConfig.WindupSeconds;
            if (AutoPlay) StartCoroutine(AutoPilot(plan, tReleaseAt));
        }
        else
        {
            // 守り: 球種→狙い→(1回目のタップでモーション開始)→タイミングを合わせて2回目のタップ
            yield return StartCoroutine(DefenseInput());
            plan = _defPlan;
            tReleaseAt = _defTRelease;
            StartCoroutine(AIBatter(plan, tReleaseAt));
        }
        _last2 = _last1;
        _last1 = (int)plan.Kind;

        bool swung = false;
        float tSwing = 0f;
        Vector2 swingCursor = Vector2.zero;

        // 投球モーション中(ここで振ると早振り)
        while (_gs.UserBatting && Time.time < tReleaseAt)
        {
            if (!swung && ConsumeSwing())
            {
                swung = true;
                tSwing = Time.time;
                swingCursor = _gs.UserBatting ? _cursorPos : _aiCursor;
                _cursorLocked = true;
                _batter.DoSwing();
            }
            yield return null;
        }

        _stealInput = false;
        _ui.ShowSteal(false, false);
        float tRelease = Time.time;
        float tArrive = tRelease + plan.FlightTime;
        if (ShowPitchName) _ui.ShowPitch(plan.Name, plan.Kmh); else _ui.ShowSpeed(plan.Kmh);
        _stage.Stadium.SetBoardSpeed(plan.Kmh);
        _stage.SetBall(PitchPos(plan, 0f));
        GameAudio.Play(Sfx.Whoosh, 0.35f, Mathf.Lerp(1.15f, 0.9f, (plan.Kmh - 100f) / 55f));
        Color trailCol = TrailColor(plan.Kind);

        // ボールが来る間
        while (true)
        {
            float t = Time.time - tRelease;
            Vector3 bp = PitchPos(plan, t);
            _stage.SetBall(bp);
            _stage.AddTrail(bp, trailCol);
            if (FreezeKind >= -1 && (FreezeKind < 0 || FreezeKind == (int)plan.Kind) && t > plan.FlightTime * 0.85f)
            {
                FreezeKind = -99;
                Time.timeScale = 0f;
            }
            if (!swung && ConsumeSwing())
            {
                swung = true;
                tSwing = Time.time;
                swingCursor = _gs.UserBatting ? _cursorPos : _aiCursor;
                _cursorLocked = true;
                _batter.DoSwing();
            }
            if (swung && Time.time >= tSwing + BattingConfig.SwingLag) break;   // バットがゾーン面に届いた瞬間
            if (!swung && t >= plan.FlightTime + 0.12f) break;                 // 見逃し確定
            yield return null;
        }

        // ---- 見逃し ----
        if (!swung)
        {
            yield return StartCoroutine(FinishFlight(plan, tRelease, trailCol));
            if (plan.InZone) { _pitchEnd = PitchEnd.CalledStrike; _ui.ShowResult("ストライク"); }
            else if (IsBodyHit(plan))
            {
                _pitchEnd = PitchEnd.HitByPitch;
                _ui.ShowQuirk("デッドボール！");
                _batter.Flop(1.2f * BattingConfig.GunyaLevel);
                GameAudio.Play(Sfx.Boing, 0.9f, 1.15f);
                _stage.Shake(0.10f, 0.2f);
                Say(Commentary.ForHitByPitch(), new Color(1f, 0.55f, 0.2f), true);
            }
            else { _pitchEnd = PitchEnd.Ball; _ui.ShowResult("ボール"); }
            LogPitch(plan, "take", 0f, null);
            yield break;
        }

        // ---- スイング判定 ----
        float timingMs = ((tSwing + BattingConfig.SwingLag) - tArrive) * 1000f;
        float aimY = swingCursor.y;
        if (_gs.UserBatting)
        {
            // 狙いのアシスト(カーソルの見た目も寄せる)
            swingCursor = HitJudge.Assist(swingCursor, plan.Target, HitJudge.CursorHalfW(_batterStats.Meet), HitJudge.CursorHalfH(_batterStats.Meet));
            _cursorPos = swingCursor;
        }
        var ctx = new SwingContext
        {
            BallPos = plan.Target,
            CursorPos = swingCursor,
            HasAimY = _gs.UserBatting, AimY = aimY,
            CursorHalfW = HitJudge.CursorHalfW(_batterStats.Meet),
            CursorHalfH = HitJudge.CursorHalfH(_batterStats.Meet),
            TimingMs = timingMs,
            Meet = _batterStats.Meet,
            Power = _batterStats.Power,
            BatSide = _batSide
        };
        _outcome = HitJudge.Judge(ctx);
        Color rc = BattingUI.RankColor(_outcome.Rank);
        if (_gs.UserBatting) _cursor.FlashColor(rc, 0.5f);

        if (_outcome.Rank == HitRank.Whiff)
        {
            string tag = timingMs < -BattingConfig.TimingWeakMs ? "EARLY"
                       : (timingMs > BattingConfig.TimingWeakMs ? "LATE" : "");
            _ui.ShowRank(HitRank.Whiff, tag);
            _ui.ShowResult("空振り");
            _pitchEnd = PitchEnd.SwingMiss;
            GameAudio.Play(Sfx.Whiff, 0.8f);
            _batter.SetExpression(Expr.Surprise, 0.5f);
            StartCoroutine(DelayedFlop(0.18f, 0.5f));
            yield return StartCoroutine(FinishFlight(plan, tRelease, trailCol));
            LogPitch(plan, "whiff", timingMs, _outcome);
            yield break;
        }

        // ---- 接触 ----
        Vector3 contact = new Vector3(plan.Target.x, plan.Target.y, 0f);
        _stage.SetBall(contact);
        _stage.Flash(contact, rc, _outcome.Rank == HitRank.Perfect ? 1.1f : 0.7f);
        string rankTag = (_outcome.Rank == HitRank.Perfect) ? "" : (timingMs < -20f ? "EARLY" : (timingMs > 20f ? "LATE" : ""));
        _ui.ShowRank(_outcome.Rank, rankTag);
        GameAudio.PlayHit(_outcome.Rank);
        if (!_outcome.FoulTip && (_outcome.Rank == HitRank.Perfect || _outcome.Rank == HitRank.Good))
        {
            _batter.SetExpression(Expr.Happy, 1.6f);
            _pitcher.SetExpression(Expr.Surprise, 1.4f);
        }
        if (_outcome.Rank == HitRank.Perfect && !_outcome.FoulTip)
        {
            StartCoroutine(HitStop(0.065f));
            _stage.Shake(0.10f, 0.20f);
            GameAudio.Haptic();
            if (Random.value < 0.6f) Say(Commentary.ForPerfect(), new Color(1f, 0.9f, 0.3f), false);
        }

        if (_outcome.FoulTip)
        {
            _ui.ShowResult("ファウルチップ");
            var tipTr = BattedBallPhysics.Simulate(contact, _outcome.ExitSpeed, _outcome.LaunchDeg,
                _outcome.SprayDeg, _outcome.BackspinRpm, 0f);
            float tt = 0f;
            while (tt < tipTr.Duration)
            {
                tt += Time.deltaTime;
                Vector3 bp = tipTr.PosAt(tt);
                if (bp.z < -3.6f || tt > 0.8f) break;
                _stage.SetBall(bp);
                yield return null;
            }
            _stage.HideBall();
            _pitchEnd = PitchEnd.FoulTip;
            LogPitch(plan, "foultip", timingMs, _outcome);
            yield break;
        }

        var traj = BattedBallPhysics.Simulate(contact, _outcome.ExitSpeed, _outcome.LaunchDeg,
            _outcome.SprayDeg, _outcome.BackspinRpm, _outcome.TiltDeg);
        float qs = _gs.QuirkAllowed ? BattingConfig.GunyaLevel : 0f;
        _play = FieldingModel.Resolve(traj, _batterStats.Speed, qs);
        if (_play.Quirk != QuirkKind.None)
        {
            _gs.QuirkCount++;
            Count("Quirk_" + _play.Quirk);
        }
        _lastTraj = traj;
        _lastPlay = _play;
        ConsiderHighlight(traj, _play, _outcome.Rank);
        if (_play.Kind == PlayKind.HomeRun)
        {
            StartCoroutine(SlowMo(0.35f, 0.8f));
            _stage.Shake(0.18f, 0.40f);
            GameAudio.Haptic();
        }
        yield return StartCoroutine(PlayBattedBall(traj, _play, BattingConfig.LiveBallPlaybackSpeed));
        _pitchEnd = (_play.Kind == PlayKind.Foul) ? PitchEnd.Foul : PitchEnd.InPlay;
        LogPitch(plan, _play.Kind.ToString() + (_play.Quirk != QuirkKind.None ? "/" + _play.Quirk : ""), timingMs, _outcome);
    }

    // ------------------------------------------------------------
    // 守り(投球)の操作
    // ------------------------------------------------------------
    private IEnumerator DefenseInput()
    {
        for (int i = 0; i < 5; i++) _avail[i] = _pitcherStats.Brk >= PitchRepertoire.All[i].MinBrk;
        if (!_avail[_selKind]) _selKind = 0;
        _ui.ShowPitchPanel(true, _avail, _selKind);
        _cursorLocked = false;
        _reticleOn = true;
        _reticleCol = Color.white;
        yield return StartCoroutine(Wait(BattingConfig.PitchReadySeconds));
        _swingQueued = false;
        _defTapOk = true;

        // 自動操作(検証用): 投手AIの球種とコースを「ユーザーの選択」として使う
        PitchPlan ai = default(PitchPlan);
        float tAutoStart = 0f;
        if (AutoPlay)
        {
            ai = PitchAI.ChoosePitch(_pitcherStats, _batterStats, _gs.Balls, _gs.Strikes, _batSide, _last1, _last2);
            _selKind = (int)ai.Kind;
            _ui.SelectPitch(_selKind);
            _cursorTarget = _cursorPos = ClampCursor(ai.Target);
            tAutoStart = Time.time + 0.2f;
        }

        // ① 球種 ② コース: 決まったら1回目のタップ
        bool started = false;
        while (!started)
        {
            if (FreezeKind == -61 && AutoPlay && Time.time >= tAutoStart - 0.2f) { FreezeKind = -99; Time.timeScale = 0f; }
            if (AutoPlay) { if (Time.time >= tAutoStart) started = true; }
            else if (ConsumeSwing()) started = true;
            yield return null;
        }
        _swingQueued = false;
        PitchKind kind = (PitchKind)_selKind;
        Vector2 aim = _cursorPos;
        _cursorLocked = true;
        _reticleCol = new Color(1f, 1f, 1f, 0.45f);
        _ui.ShowPitchPanel(false, _avail, _selKind);
        _ui.SetSpeed(PitchRepertoire.Get(kind).Name);

        // ③ モーションに合わせてもう一度タップ
        _pitcher.DoPitchMotion();
        float t0 = Time.time;
        float tRel = t0 + BattingConfig.WindupSeconds;
        float tSweet = tRel - SweetBeforeRelease;
        float sweetU = (tSweet - t0) / BattingConfig.WindupSeconds;
        // 完璧=±25ms、良い=品質0.5(±105ms)の幅をメーターの帯にする
        _ui.SetMeterZones(sweetU, 0.105f / BattingConfig.WindupSeconds, 0.025f / BattingConfig.WindupSeconds);
        _ui.ShowMeter(true);

        bool pressed = false;
        float tPress = 0f;
        float autoOff = AutoPlay ? PitchAI.Normal(0.050f) : 0f;
        while (Time.time < tRel)
        {
            float u = (Time.time - t0) / BattingConfig.WindupSeconds;
            if (FreezeKind == -60 && u > 0.7f) { FreezeKind = -99; Time.timeScale = 0f; }
            if (!pressed)
            {
                _ui.SetMeter(u);
                if (AutoPlay)
                {
                    if (Time.time >= tSweet + autoOff) { pressed = true; tPress = Time.time; }
                }
                else if (ConsumeSwing())
                {
                    if (Time.time - t0 > 0.12f) { pressed = true; tPress = Time.time; }   // 直後の連打は無視
                }
            }
            yield return null;
        }
        float timingMs = pressed ? (tPress - tSweet) * 1000f : 185f;
        float q = PitchAI.TimingQuality(timingMs);
        string txt; Color col;
        if (!pressed) { txt = "MISS"; col = new Color(1f, 0.38f, 0.34f); }
        else if (q >= 0.99f) { txt = "PERFECT!"; col = new Color(1f, 0.86f, 0.18f); }
        else if (q >= 0.5f) { txt = "GOOD"; col = new Color(0.45f, 0.95f, 0.45f); }
        else { txt = timingMs < 0f ? "EARLY" : "LATE"; col = new Color(0.62f, 0.78f, 1f); }
        _ui.ShowTiming(txt, col);
        _defTapOk = false;
        _swingQueued = false;
        _reticleOn = false;
        _defPlan = PitchAI.PlanFromUser(_pitcherStats, kind, aim, _batSide, timingMs);
        _defTRelease = tRel;
        Count("Pitch_" + kind);
        Count(pressed ? (q >= 0.99f ? "Timing_Perfect" : (q >= 0.5f ? "Timing_Good" : "Timing_Bad")) : "Timing_None");
    }

    // 打者AI: 球が放たれたあと、スイングするなら適切な時刻に振る
    private IEnumerator AIBatter(PitchPlan plan, float tRelease)
    {
        BatterDecision d = BatterAI.Decide(_batterStats, plan, _gs.Balls, _gs.Strikes);
        if (!d.Swing) yield break;
        float tSw = tRelease + plan.FlightTime - BattingConfig.SwingLag + d.TimingSec;
        while (Time.time < tSw) yield return null;
        _aiCursor = d.Cursor;
        _swingQueued = true;
    }

    private IEnumerator DelayedFlop(float delay, float power)
    {
        var who = _batter;
        yield return StartCoroutine(Wait(delay));
        if (who != null) who.Flop(power * BattingConfig.GunyaLevel);
    }

    // 振らなかった/空振りしたボールを捕手の位置まで見届ける
    private IEnumerator FinishFlight(PitchPlan plan, float tRelease, Color trailCol)
    {
        while (true)
        {
            float t = Time.time - tRelease;
            Vector3 p = PitchPos(plan, t);
            if (p.z <= -0.7f) break;
            _stage.SetBall(p);
            _stage.AddTrail(p, trailCol);
            yield return null;
        }
        _stage.HideBall();
    }

    // ------------------------------------------------------------
    // 検証用の自動操作(熟練プレイヤー模擬)
    // ------------------------------------------------------------
    private IEnumerator AutoPilot(PitchPlan plan, float tRelease)
    {
        // 変化球は読みにくい分、狙いが少しぶれる。チェンジアップは早振りになりやすい。
        float noise = 0.33f + (plan.Kind == PitchKind.Fastball ? 0f : 0.07f);
        Vector2 aim = plan.Target + new Vector2(PitchAI.Gauss() * noise, PitchAI.Gauss() * noise);
        _cursorTarget = ClampCursor(aim);
        bool swing = plan.InZone ? (Random.value < 0.92f) : (Random.value < 0.15f);
        if (!swing) yield break;
        float tArrive = tRelease + plan.FlightTime;
        float bias = plan.Kind == PitchKind.Changeup ? -0.035f : 0f;
        float tSw = tArrive - BattingConfig.SwingLag + bias + PitchAI.Gauss() * 0.12f;
        while (Time.time < tSw) yield return null;
        _swingQueued = true;
    }

    private void LogPitch(PitchPlan plan, string what, float timingMs, HitOutcome? o)
    {
        if (!Verbose) return;
        string r = o.HasValue ? o.Value.Rank + " e=" + o.Value.EllipseDist.ToString("F2") : "-";
        Debug.Log("[AtBat] #" + _pitchCount + " " + _gs.Balls + "-" + _gs.Strikes + "-" + _gs.Outs
                  + " " + _batterStats.Name + " " + plan.Name + " " + plan.Kmh.ToString("F0") + "km/h target=("
                  + plan.Target.x.ToString("F2") + "," + plan.Target.y.ToString("F2") + ") zone=" + plan.InZone
                  + " -> " + what + " dt=" + timingMs.ToString("F0") + "ms " + r);
    }}
