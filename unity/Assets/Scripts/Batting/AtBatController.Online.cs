using System.Collections;
using UnityEngine;

// ============================================================
// AtBatController.Online.cs
// オンライン対戦: 相手の番号から読み込んだ編成と、次の1試合を戦う(相手の操作は AI)。
// 試合中に始めるときは、今の試合を打ち切って新しい試合を始める。
// ============================================================
public partial class AtBatController
{
    public static AtBatController Current { get; private set; }

    // 次の試合の相手(AppShell が設定する。試合が始まると読み込んで空にする)
    public static CharaRoster.Team OnlineOpp;
    public static string OnlineOppName = "";
    public static int OnlineOppMark;

    private bool _online;
    private string _onlineName = "";
    private int _onlineMark;

    // BuildTeams から呼ぶ。オンラインの相手が予約されていればそれを使う
    private bool TakeOnlineOpponent()
    {
        if (OnlineOpp == null) { _online = false; return false; }
        _oppTeam = OnlineOpp;
        _online = true;
        _onlineName = OnlineOppName;
        _onlineMark = OnlineOppMark;
        OnlineOpp = null;
        return true;
    }

    private string RivalName { get { return _online ? _onlineName : RivalNames[RivalIndex]; } }
    private string RivalMark { get { return _online ? RivalMarks[Mathf.Abs(_onlineMark) % RivalMarks.Length] : RivalMarks[RivalIndex]; } }

    // 今の試合を打ち切り、最初から始める(オンライン対戦の開始用)
    public void RestartMatch()
    {
        StopAllCoroutines();
        Time.timeScale = 1f;
        _ui.SetControlsVisible(false);
        _teamsFor = -1;
        _ui.HideVersus(true);
        _ui.ShowResult("");
        _ui.ShowBanner("");
        _ui.ClearCenter();   // 前の試合の判定(GOOD など)を残さない
        _ui.ClearSpeed();
        _ui.ShowQuirk("");
        _ui.ShowCommentary("", Color.white);
        _ui.SetReplay(false);
        _ui.HideIntroCard();
        _ui.SetBatterLabel("");
        if (_batter != null) _batter.gameObject.SetActive(false);   // 前の試合の打者は、次の打席で作り直すまで隠す
        _ui.ShowPitchPanel(false, _avail, _selKind);
        _ui.ShowMeter(false);
        _stage.Stadium.HideVision();
        _bestScore = -1;
        _gs.NewGame();
        for (int b = 1; b <= 3; b++) _onBase[b] = null;
        _wasLoaded = _wasScoring = false;
        _introSeen.Clear();
        BuildTeams();
        _gameStartT = Time.unscaledTime;
        RefreshHud();
        StartCoroutine(MatchLoop());
    }

    private IEnumerator MatchLoop()
    {
        BgmPlayer.PlayBase(BgmPlayer.Match);
        yield return StartCoroutine(Opening(true));
        _stage.SetCameraMode(CamMode.Catcher);
        while (true) yield return StartCoroutine(PlayAtBat());
    }
}
