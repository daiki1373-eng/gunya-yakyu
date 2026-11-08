using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AtBatController
{
    // ---- 場面の曲(BRIEF_music30_addendum.md の対応表) ----
    private bool _wasLoaded, _wasScoring;
    private bool _replaying;

    private void Music(string name, float fadeAfter = 0f)
    {
        if (_replaying) return;
        BgmPlayer.Jingle(name, fadeAfter);
    }

    // 打席の始まり: 満塁 / 得点圏(自軍=チャンス、相手=ピンチ)になった瞬間だけ鳴らす
    private void SituationMusic()
    {
        bool loaded = _gs.Base[1] && _gs.Base[2] && _gs.Base[3];
        bool scoring = _gs.Base[2] || _gs.Base[3];
        if (loaded && !_wasLoaded) Music("bgm-bases-loaded", 8f);
        else if (scoring && !_wasScoring) Music(_gs.UserBatting ? "bgm-chance" : "bgm-pinch", 8f);
        _wasLoaded = loaded;
        _wasScoring = scoring;
    }

    // ------------------------------------------------------------
    // 打球の再生(打球カメラで追う)
    // ------------------------------------------------------------
    private IEnumerator PlayBattedBall(BattedBallTrajectory tr, PlayResult play, float speed = 1f, bool replay = false, float startTime = 0f)
    {
        _replaying = replay;
        _stage.SetCameraMode(CamMode.Follow);
        _stage.ResetFielders();
        _stage.SetPlaybackSpeed(speed);

        FieldingModel.Fielder f = default(FieldingModel.Fielder);
        FieldingModel.Fielder f2 = default(FieldingModel.Fielder);
        bool hasF = play.Fielder >= 0;
        bool hasF2 = play.Fielder2 >= 0 && play.Quirk == QuirkKind.Collision;
        if (hasF) f = FieldingModel.Defaults[play.Fielder];
        if (hasF2) f2 = FieldingModel.Defaults[play.Fielder2];
        Vector2 target = new Vector2(play.PlayPos.x, play.PlayPos.z);
        bool resultShown = false, quirkShown = false, popped = false;

        // バット投げ(会心の当たりのとき)
        if (!replay && (play.Kind == PlayKind.HomeRun || (_outcome.Rank == HitRank.Perfect && Random.value < 0.5f)))
            StartCoroutine(DelayedBatToss(0.15f));

        // 送球(ゴロ処理)
        float throwStart = -1f, throwDur = 0.1f;
        Vector2 throwFrom = Vector2.zero, throwTo = Vector2.zero;
        if (play.ThrowBase > 0)
        {
            throwStart = play.PlayTime + FieldingModel.ThrowTransfer;
            throwFrom = new Vector2(play.ThrowFrom.x, play.ThrowFrom.z);
            throwTo = FieldingModel.BasePos(play.ThrowBase);
            if (play.Quirk == QuirkKind.BadThrow) throwTo += new Vector2(5.5f, 7f);   // ファウルゾーンへ大暴投
            throwDur = Mathf.Max(0.15f, Vector2.Distance(throwFrom, throwTo) / FieldingModel.ThrowSpeed);
        }

        float t = startTime;
        bool thrown = false;
        while (t < play.EndTime)
        {
            t += Time.deltaTime * speed;
            if (!thrown && throwStart >= 0f && t >= throwStart - 0.18f)
            {
                thrown = true;
                _stage.ThrowPose(play.Fielder);
            }
            Vector3 p;
            if (throwStart >= 0f && t >= throwStart)
            {
                float u = Mathf.Clamp01((t - throwStart) / throwDur);
                Vector2 xz = Vector2.Lerp(throwFrom, throwTo, u);
                p = new Vector3(xz.x, 1.1f + Mathf.Sin(u * Mathf.PI) * 1.6f, xz.y);
            }
            else p = tr.PosAt(Mathf.Min(t, tr.Duration));
            _stage.SetBall(p);
            _stage.SetFollowTarget(p);

            if (hasF) MoveFielder(play.Fielder, f, target, t);
            if (hasF2) MoveFielder(play.Fielder2, f2, target, t);

            if (!quirkShown && play.Quirk != QuirkKind.None && t >= play.QuirkTime)
            {
                quirkShown = true;
                PlayQuirk(play);
            }
            if (!popped && play.IsOut && play.Quirk == QuirkKind.None && play.Fielder >= 2 && t >= play.PlayTime)
            {
                popped = true;
                _stage.CatchPop(play.Fielder);
                GameAudio.Play(Sfx.Catch, 0.8f, Random.Range(0.95f, 1.08f));
            }
            if (!resultShown && t >= play.PlayTime)
            {
                resultShown = true;
                ShowPlayResult(play);
            }
            yield return null;
        }
        if (!resultShown) ShowPlayResult(play);
        if (!replay) _stage.SetPlaybackSpeed(1f);
    }

    private void MoveFielder(int index, FieldingModel.Fielder f, Vector2 target, float t)
    {
        float run = Mathf.Max(0f, t - FieldingModel.React) * f.Speed * FieldingModel.RunEfficiency;
        Vector2 to = target - f.Pos;
        float dist = to.magnitude;
        Vector2 pos = dist < 1e-3f ? f.Pos : f.Pos + to / dist * Mathf.Min(dist, run);
        _stage.SetFielderPos(index, pos);
    }

    private IEnumerator DelayedBatToss(float delay)
    {
        var who = _batter;
        yield return StartCoroutine(Wait(delay));
        if (who != null) who.ThrowBat(0.8f * BattingConfig.GunyaLevel);
    }

    // ------------------------------------------------------------
    // P4: 演出ヘルパー
    // ------------------------------------------------------------
    // 一瞬止める(実時間)。Perfectの手応え用。
    private IEnumerator HitStop(float realSeconds)
    {
        float prev = Time.timeScale;
        if (prev <= 0f) yield break;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(realSeconds);
        while (AppShell.MenuOpen) yield return null;
        if (Time.timeScale == 0f) Time.timeScale = prev;
    }

    // 一瞬スローにして戻す(ホームラン用)。ヒットストップが終わってから始める。
    private IEnumerator SlowMo(float scale, float realSeconds)
    {
        yield return new WaitForSecondsRealtime(0.09f);
        while (AppShell.MenuOpen) yield return null;
        float baseScale = Time.timeScale;
        if (baseScale <= 0f) yield break;
        Time.timeScale = baseScale * scale;
        yield return new WaitForSecondsRealtime(realSeconds);
        float t = 0f;
        while (t < 0.25f)
        {
            while (AppShell.MenuOpen) yield return null;
            t += Time.unscaledDeltaTime;
            if (Time.timeScale <= 0f) yield break;
            Time.timeScale = Mathf.Lerp(baseScale * scale, baseScale, Mathf.Clamp01(t / 0.25f));
            yield return null;
        }
        Time.timeScale = baseScale;
    }

    // 実況字幕。続けて出しすぎないよう間隔を空ける(force=trueは割り込み)。
    private void Say(string text, Color c, bool force)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (!force && Time.unscaledTime - _lastSay < 1.2f) return;
        _lastSay = Time.unscaledTime;
        _ui.ShowCommentary(text, c);
    }

    // 1試合で一番ぐにゃついたプレーを覚えておく(試合後に「今日の珍プレー」で再生)
    private void ConsiderHighlight(BattedBallTrajectory tr, PlayResult play, HitRank rank)
    {
        int score = 0;
        switch (play.Quirk)
        {
            case QuirkKind.Collision: score = 5; break;
            case QuirkKind.FenceCrash: score = 5; break;
            case QuirkKind.BadThrow: score = 4; break;
            case QuirkKind.Slip: score = 3; break;
        }
        if (play.Kind == PlayKind.HomeRun) score = Mathf.Max(score, 3 + (int)(play.Distance / 60f));
        if (score > _bestScore)
        {
            _bestScore = score;
            _bestTraj = tr;
            _bestPlay = play;
        }
    }

    // スローリプレイ(同じ打球・同じ結果を、寄りのカメラでゆっくり)。タップでスキップ。
    private IEnumerator PlayReplay(BattedBallTrajectory tr, PlayResult play, float speed, float dist, string label)
    {
        if (tr == null) yield break;
        float moment = play.Quirk != QuirkKind.None ? play.QuirkTime : play.PlayTime;
        float start = Mathf.Max(0f, moment - 0.65f);
        _ui.SetReplay(true, label);
        _ui.ClearCenter();
        _ui.ShowBanner("");
        _stage.HideBall();
        _stage.FollowDist = dist;
        _stage.ReplayWide = true;   // スタンドと照明が入る、少し引いた高めの画
        _stage.SnapFollow(tr.PosAt(start));
        _stage.SetPlaybackSpeed(speed);
        _presentationInput = true;
        _skipQueued = _swingQueued = false;
        _replaying = true;
        var playback = PlayBattedBall(tr, play, speed, true, start);
        float elapsed = 0f;
        try
        {
            while (!_skipQueued && elapsed < BattingConfig.ReplaySeconds && playback.MoveNext())
            {
                elapsed += Time.deltaTime;
                yield return playback.Current;
            }
        }
        finally
        {
            var disposable = playback as System.IDisposable;
            if (disposable != null) disposable.Dispose();
            _presentationInput = _replaying = false;
            _skipQueued = _swingQueued = false;
            _stage.SetPlaybackSpeed(1f);
            _stage.FollowDist = 18f;
            _stage.ReplayWide = false;
            _stage.HideBall();
            _stage.ResetFielders();
            _ui.SetReplay(false);
            _ui.ClearCenter();
            _ui.ShowBanner("");
        }
    }

    private IEnumerator FreezeSoon(float realSeconds)
    {
        yield return new WaitForSecondsRealtime(realSeconds);
        Time.timeScale = 0f;
    }

    private void PlayQuirk(PlayResult play)
    {
        _ui.ShowQuirk(FieldingModel.QuirkLabel(play.Quirk));
        Say(Commentary.ForQuirk(play.Quirk), new Color(1f, 0.55f, 0.2f), true);
        switch (play.Quirk)
        {
            case QuirkKind.Collision: GameAudio.Play(Sfx.Thud, 1f); GameAudio.Play(Sfx.Boing, 0.7f, 1.1f); _stage.Shake(0.14f, 0.25f); break;
            case QuirkKind.FenceCrash: GameAudio.Play(Sfx.Thud, 1f, 0.8f); _stage.Shake(0.20f, 0.30f); break;
            case QuirkKind.Slip: GameAudio.Play(Sfx.Boing, 0.8f, 1.2f); break;
            case QuirkKind.BadThrow: GameAudio.Play(Sfx.Boing, 0.7f, 0.9f); break;
        }
        GameAudio.Haptic();
        Music(play.Quirk == QuirkKind.BadThrow || play.Quirk == QuirkKind.Slip ? "bgm-error" : "bgm-rare-play", 9f);
        if (play.Quirk == QuirkKind.BadThrow || play.Quirk == QuirkKind.Slip) StartCoroutine(LaterExpression(_pitcher, Expr.Angry, 0.6f, 1.8f));
        if (FreezeKind == -50) { FreezeKind = -99; StartCoroutine(FreezeSoon(0.45f)); }
        switch (play.Quirk)
        {
            case QuirkKind.Collision:
                _stage.Tumble(play.Fielder, 1.3f);
                _stage.Tumble(play.Fielder2, 1.3f);
                break;
            case QuirkKind.FenceCrash:
            case QuirkKind.Slip:
                _stage.Tumble(play.Fielder, 1.4f);
                break;
            case QuirkKind.BadThrow:
                _stage.Tumble(play.Fielder2 >= 2 ? play.Fielder2 : 2, 1.1f);
                break;
        }
    }

    private IEnumerator LaterExpression(RagdollPlayer who, Expr e, float delay, float seconds)
    {
        yield return new WaitForSeconds(delay);
        if (who != null) who.SetExpression(e, seconds);
    }

    public bool Replaying { get { return _replaying; } }
    // 試合が終わった後(締めの演出・珍プレー・結果パネル)は、顔のカットインを出さない
    public bool MatchOver { get { return _gs.GameOver; } }

    private void ShowPlayResult(PlayResult play)
    {
        string label = FieldingModel.Label(play.Kind);
        if (play.Quirk == QuirkKind.BadThrow || play.Quirk == QuirkKind.Slip) label = "エラー";
        else if (play.Quirk == QuirkKind.Collision || (play.Quirk == QuirkKind.FenceCrash && !play.IsOut))
            label = play.Kind == PlayKind.Foul ? "ファウル" : "落球";
        else if (play.Kind == PlayKind.HomeRun || play.IsHit)
            label += "  " + Mathf.RoundToInt(play.Distance) + "m";
        _ui.ShowResult(label);
        // 曲: 自軍の本塁打 / 自軍投手が痛打された / 自軍のファインプレー
        if (play.Kind == PlayKind.HomeRun)
        {
            Music(_gs.UserBatting ? "bgm-homerun" : "bgm-pitcher-hit", 10f);
            _stage.Stadium.ShowVision("homerun", 5f);
        }
        else if (!_gs.UserBatting && play.IsHit && play.Quirk == QuirkKind.None
                 && (_outcome.Rank == HitRank.Perfect || play.Bases >= 2)) Music("bgm-pitcher-hit", 7f);
        else if (!_gs.UserBatting && play.IsOut && play.Quirk == QuirkKind.None
                 && (play.Kind == PlayKind.FlyOut || play.Kind == PlayKind.LineOut) && play.Distance > 62f) Music("bgm-fineplay", 8f);
        if (play.IsOut && play.Quirk == QuirkKind.None) { _batter.SetExpression(Expr.Sad, 1.5f); _pitcher.SetExpression(Expr.Happy, 1.2f); }
        if (play.Kind == PlayKind.HomeRun)
        {
            _pitcher.Flop(0.7f * BattingConfig.GunyaLevel);
            StartCoroutine(LaterExpression(_pitcher, Expr.Sad, 1.2f, 1.8f));
            GameAudio.Play(Sfx.BigCheer, 1f);
            Say(Commentary.ForHomeRun(), new Color(1f, 0.85f, 0.2f), true);
        }
        else if (play.IsHit && play.Quirk == QuirkKind.None) GameAudio.Play(Sfx.Cheer, 0.6f);
    }

}
