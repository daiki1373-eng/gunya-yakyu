using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AtBatController
{
    // ------------------------------------------------------------
    // 打席(1人の打者)
    // ------------------------------------------------------------
    private void RefreshHud()
    {
        _ui.SetCount(_gs.Inning, _gs.IsTop, _gs.Balls, _gs.Strikes, _gs.Outs, _gs.Runs, _gs.OppRuns, _gs.UserBatting);
        _stage.Stadium.SetBoard(_gs.Inning, _gs.IsTop, _gs.Balls, _gs.Strikes, _gs.Outs, _gs.Runs, _gs.OppRuns);
        _ui.SetBases(_gs.Base);
    }

    private IEnumerator PlayAtBat()
    {
        ApplyTeams();
        SetupBatter();
        _gs.ResetCount();
        _gs.PlateAppearances++;
        _last1 = _last2 = -1;
        _stage.ResetFielders();
        SkinRunners();
        _stage.ShowRunnersOnBases(_gs.Base);
        _ui.SetSwingLabel(_gs.UserBatting ? "SWING" : "投げる");
        _ui.ShowPitchPanel(false, _avail, _selKind);
        _ui.ShowMeter(false);   // 前の打席の投球メーターを残さない
        _reticleOn = false;
        RefreshHud();
        _ui.ShowResult("");
        _ui.ShowBanner("");
        _replaying = false;
        SituationMusic();
        if (IntroEnabled)
        {
            _introSkip = false;
            _skipQueued = false;
            if (_pitcherIntroPending)
            {
                _pitcherIntroPending = false;
                yield return StartCoroutine(PitcherIntro());
            }
            _introSkip = false;
            yield return StartCoroutine(BatterIntro());
        }
        yield return StartCoroutine(Wait(0.12f));

        bool over = false;
        while (!over)
        {
            yield return StartCoroutine(PlayPitch());
            CopyBases();
            over = ApplyPitchResult();
            UpdateOnBase(_oldBase);
            RefreshHud();
            bool stole = false;
            if (_stealFrom > 0 && _gs.Outs < 3 && !_gs.GameOver
                && ((_pitchEnd == PitchEnd.Ball && !over) || _pitchEnd == PitchEnd.CalledStrike || _pitchEnd == PitchEnd.SwingMiss))
            {
                yield return StartCoroutine(ResolveSteal());
                stole = true;
                if (_gs.Outs >= 3 && !over)
                {
                    // 盗塁死で終わった未完了打席は次の攻撃で同じ打者から。
                    if (_gs.UserBatting) _batterIndex = (_batterIndex + _userTeam.Lineup.Count - 1) % _userTeam.Lineup.Count;
                    else _oppIndex = (_oppIndex + _oppTeam.Lineup.Count - 1) % _oppTeam.Lineup.Count;
                    _gs.PlateAppearances--;
                    over = true;
                }
            }
            if (over && !_gs.GameOver && ReplayEnabled && _pitchEnd == PitchEnd.InPlay
                && (_lastPlay.Quirk != QuirkKind.None || _lastPlay.Kind == PlayKind.HomeRun))
                yield return StartCoroutine(PlayReplay(_lastTraj, _lastPlay, 0.65f, 11f, "● REPLAY"));
            if (over && !stole) yield return StartCoroutine(AnimateMoves());
            yield return StartCoroutine(PresentationWait(over ? BattingConfig.AtBatResultSeconds : BattingConfig.PitchResultSeconds));
            _stage.HideBall();
            _stage.SetCameraMode(CamMode.Catcher);
            _stage.ResetFielders();
        }

        if (_gs.Outs >= 3 && !_gs.GameOver)
        {
            _gs.EndInningIfNeeded();
            for (int b = 1; b <= 3; b++) _onBase[b] = null;
            _stage.HideRunners();
            _wasLoaded = _wasScoring = false;
            if (!_gs.GameOver)
            {
                _ui.ShowResult("チェンジ");
                yield return StartCoroutine(PresentationWait(BattingConfig.ChangeSeconds));
            }
        }
        if (_gs.GameOver) yield return StartCoroutine(FinishGame());
        RefreshHud();
    }

    private IEnumerator FinishGame()
    {
        int coins = Collection.AwardMatch(_gs);
        bool won = _gs.Runs > _gs.OppRuns;
        string res = won ? "勝ち！" : (_gs.Runs < _gs.OppRuns ? "負け…" : "引き分け");
        BgmPlayer.StopJingle(0f);
        if (_gs.IsWalkOff) BgmPlayer.Jingle("bgm-sayonara", 3f);
        else if (won) BgmPlayer.Jingle("bgm-win", 3f);
        else if (_gs.Runs < _gs.OppRuns) BgmPlayer.Jingle("bgm-lose", 3f);
        else
        {
            // 引き分け専用素材は追加せず、自作の短い締めジングル。
            // PlayJingle はMIDI番号ではなくC5からの半音差を受け取る。
            GameAudio.PlayJingle(new[] { 0, 4, 7, 0 }, 0.14f);
            GameAudio.Play(Sfx.Cheer, 0.45f);
        }
        if (won) { _stage.Stadium.LaunchFireworks(6); GameAudio.Play(Sfx.BigCheer, 0.8f); }
        _ui.ShowResult((_gs.IsWalkOff ? "サヨナラ  " : "試合終了  ") + _gs.Runs + " - " + _gs.OppRuns + "  " + res);
        _ui.ShowBanner("コイン +" + coins);
        _stage.SetCameraMode(CamMode.Diamond);
        GameLog.Add("game" + _gs.GameNo + " " + _gs.Runs + "-" + _gs.OppRuns + " quirks=" + _gs.QuirkCount
            + " pa=" + _gs.PlateAppearances + " sec=" + (Time.unscaledTime - _gameStartT).ToString("F0"));
        yield return StartCoroutine(PresentationWait(2.0f));
        if (ReplayEnabled && _bestScore >= 3 && !_gs.IsWalkOff)
            yield return StartCoroutine(PlayReplay(_bestTraj, _bestPlay, 0.65f, 9f, "● 今日の珍プレー"));
        _bestScore = -1;

        // 結果パネル: 「次の試合」か「ホーム」を選ぶまで待つ(自動操作では少し見せて次へ)
        _ui.ShowResult("");
        _ui.ShowBanner("");
        _ui.ClearCenter();   // 最後の打球の判定(NORMAL など)を残さない
        _ui.ClearSpeed();
        string me = string.IsNullOrEmpty(AppShell.PlayerName) ? "じぶん" : AppShell.PlayerName;
        string rival = RivalName;
        if (rival.EndsWith("のチーム")) rival = rival.Substring(0, rival.Length - 4);
        Color tc = won ? UiKit.GoldLight : (_gs.Runs < _gs.OppRuns ? new Color(0.62f, 0.78f, 1f) : Color.white);
        _ui.ShowMatchResult((_gs.IsWalkOff ? "サヨナラ" : "") + res, tc,
            me + "  " + _gs.Runs + " - " + _gs.OppRuns + "  " + rival, "コイン +" + coins);
        int choice = -1;
        float shownAt = Time.unscaledTime;
        while (choice < 0)
        {
            if (AutoPlay && Time.unscaledTime - shownAt > 1.2f) choice = 0;
            if (Time.unscaledTime - shownAt > 0.4f) choice = ResultChoice();
            yield return null;
        }
        GameAudio.Play(Sfx.Click, 0.6f, 1.2f);
        _ui.HideMatchResult();
        if (choice == 1 && AppShell.Instance != null) AppShell.Instance.OpenMenu();   // ホームへ(次の試合は「試合」で始まる)

        _gs.NewGame();
        for (int b = 1; b <= 3; b++) _onBase[b] = null;
        _wasLoaded = _wasScoring = false;
        _introSeen.Clear();
        BuildTeams();
        _gameStartT = Time.unscaledTime;
        _ui.ShowBanner("");
        _ui.ShowResult("");
        RefreshHud();
        yield return StartCoroutine(Opening(false));
    }

    // 1球ぶんの結果をカウントと走者へ反映。打席が終わったら true。
    private bool ApplyPitchResult()
    {
        bool over = false;
        string key = _pitchEnd.ToString();
        int runsBefore = _gs.Runs, oppBefore = _gs.OppRuns;
        _replaying = false;
        _gs.Moves.Clear();
        _gs.LastRuns = 0;
        switch (_pitchEnd)
        {
            case PitchEnd.Ball:
                _gs.Balls++;
                if (_gs.Balls >= 4) { _gs.ApplyWalk(); _ui.ShowResult("フォアボール"); over = true; key = "Walk"; _pitcher.SetExpression(Expr.Angry, 1.6f); _batter.SetExpression(Expr.Happy, 1.2f); }
                break;
            case PitchEnd.HitByPitch:
                _gs.ApplyWalk();
                _ui.ShowResult("デッドボール");
                Music("bgm-hit-by-pitch", 7f);
                _batter.SetExpression(Expr.Angry, 1.8f);
                over = true;
                key = "HitByPitch";
                break;
            case PitchEnd.CalledStrike:
                _gs.Strikes++;
                if (_gs.Strikes >= 3) { _gs.ApplyStrikeout(); _ui.ShowResult("見逃し三振！"); _pitcher.SetExpression(Expr.Happy, 1.5f); _batter.SetExpression(Expr.Sad, 1.8f); over = true; key = "StrikeoutLooking"; Music("bgm-strikeout"); }
                break;
            case PitchEnd.SwingMiss:
                _gs.Strikes++;
                if (_gs.Strikes >= 3)
                {
                    _gs.ApplyStrikeout();
                    _ui.ShowResult("空振り三振！"); _pitcher.SetExpression(Expr.Happy, 1.5f);
                    if (Random.value < 0.5f) Say(Commentary.ForWhiffK(), new Color(1f, 0.9f, 0.4f), false);
                    _batter.Flop(1.0f * BattingConfig.GunyaLevel);
                    StartCoroutine(LaterExpression(_batter, Expr.Sad, 0.9f, 1.6f));
                    over = true;
                    key = "StrikeoutSwinging";
                    Music("bgm-strikeout");
                }
                break;
            case PitchEnd.Foul:
                if (_gs.Strikes < 2) _gs.Strikes++;
                break;
            case PitchEnd.FoulTip:
                _gs.Strikes++;
                if (_gs.Strikes >= 3) { _gs.ApplyStrikeout(); _ui.ShowResult("ファウルチップ三振！"); over = true; key = "StrikeoutTip"; Music("bgm-strikeout"); }
                break;
            case PitchEnd.InPlay:
                over = true;
                key = _play.Kind.ToString();
                if (_gs.UserBatting && _play.IsHit && _play.Quirk == QuirkKind.None) _gs.UserHits++;
                if (_play.Kind == PlayKind.HomeRun) _gs.ApplyHit(4);
                else if (_play.IsHit) _gs.ApplyHit(Mathf.Max(1, _play.Bases));
                else
                {
                    int ob = _gs.Outs;
                    _gs.ApplyOut(_play.Kind, _play.Fielder, _play.Distance);
                    if (_gs.Outs - ob >= 2) { key = "DoublePlay"; Say(Commentary.ForDoublePlay(), new Color(1f, 0.9f, 0.4f), true); Music("bgm-doubleplay", 8f); }
                }
                // 逆転打(自軍がリードを奪った)
                if (_gs.UserBatting && runsBefore <= oppBefore && _gs.Runs > _gs.OppRuns) Music("bgm-comeback", 10f);
                break;
        }
        if (over && _gs.LastRuns > 0) _ui.ShowBanner("+" + _gs.LastRuns + "点");
        Count(key);
        if (over && _gs.Moves.Count > 0) Count("Runs", _gs.LastRuns);
        return over;
    }

    private static void Count(string key, int n = 1)
    {
        int c;
        Tally.TryGetValue(key, out c);
        Tally[key] = c + n;
    }

    public static string TallyReport()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kv in Tally) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
        return sb.ToString();
    }


    // 結果パネルのボタンが押されたか(0=次の試合、1=ホーム、-1=まだ)
    private int ResultChoice()
    {
        if (AppShell.MenuOpen) return -1;
        // 指を離したとき(またはクリック)に、その場所のボタンを押したことにする
        Vector2? p = null;
        if (_releaseFrame == Time.frameCount) p = _releasePos;
        for (int i = 0; i < Input.touchCount; i++)
            if (Input.GetTouch(i).phase == TouchPhase.Ended) p = Input.GetTouch(i).position;
        if (Input.touchCount == 0 && Input.GetMouseButtonUp(0)) p = Input.mousePosition;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) return 0;
        return p.HasValue ? _ui.ResultButtonAt(p.Value) : -1;
    }
}
