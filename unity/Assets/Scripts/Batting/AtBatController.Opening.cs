using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// AtBatController.Opening.cs
// 試合開始の演出: 球場の全景と花火 → 両チームの対戦カード(VS) → PLAY BALL。タップで飛ばせる。
// 2試合目からは全景を短くする。
// ============================================================
public partial class AtBatController
{
    // 相手チームの名前(球場の看板の会社のチーム。仮置き)
    private static readonly string[] RivalNames = { "こんにゃく電鉄", "ぐにゃぐにゃ保険", "ふにゃ食品", "ぐにゃっとカメラ", "ぐにゃ乳業", "ぐにゃ銀行" };
    private static readonly string[] RivalMarks = { "team_konnyaku", "team_hoken", "team_funya", "team_camera", "team_milk", "team_bank" };
    private static Sprite Mark(string n) { return Resources.Load<Sprite>("Presentation/" + n); }
    private int RivalIndex { get { return Mathf.Abs(_gs.GameNo - 1) % RivalNames.Length; } }

    private IEnumerator Opening(bool first)
    {
        _introSkip = false; _skipQueued = false;
        _ui.SetControlsVisible(false);
        _ui.SetTeamMarks(Mark("team_me"), Mark(RivalMarks[RivalIndex]));
        BgmPlayer.Jingle("bgm-playball", 7f);
        // 夜空(タイトルの続き)から、選手の並んだグラウンドへカメラを下ろす
        _stage.SetShot(new Vector3(0f, 2f, -9f), new Vector3(0f, 78f, 130f), 0.3f, true);
        _stage.SetShot(new Vector3(0f, 15f, -27f), new Vector3(0f, 0f, 38f), 1.4f, false);
        _stage.Stadium.LaunchFireworks(first ? 8 : 4);
        GameAudio.Play(Sfx.Cheer, 0.7f);
        if (IntroEnabled)
        {
            yield return StartCoroutine(IntroWait(first ? 1.3f : 0.6f));
            if (!_introSkip)
            {
                ShowVersus();
                GameAudio.Play(Sfx.Whoosh, 0.8f, 0.9f);
                yield return StartCoroutine(IntroWait(0.34f));
                if (!_introSkip) { GameAudio.Play(Sfx.Thud, 0.9f, 1.1f); GameAudio.Play(Sfx.BigCheer, 0.5f); GameAudio.Haptic(); }
                yield return StartCoroutine(IntroWait(1.9f));
            }
            _ui.HideVersus(_introSkip);
            if (!_introSkip) yield return StartCoroutine(IntroWait(0.22f));   // 帯がはけてから PLAY BALL
        }
        _ui.ShowResult("PLAY BALL");
        _stage.Stadium.ShowVision("playball", 3.5f);
        if (!_introSkip) { _stage.Stadium.LaunchFireworks(4); GameAudio.Play(Sfx.HitPerfect, 0.7f, 0.9f); }
        yield return StartCoroutine(IntroWait(IntroEnabled ? 1.1f : 0.35f));
        _ui.HideVersus(true);
        _ui.ShowResult("");
        _ui.SetControlsVisible(true);
        _stage.SetCameraMode(CamMode.Catcher);
    }

    private void ShowVersus()
    {
        string me = AppShell.PlayerName;
        string mine = string.IsNullOrEmpty(me) ? "じぶんのチーム" : me + "のチーム";
        string rival = RivalNames[RivalIndex];
        _ui.ShowVersus(mine, Heads(_userTeam), rival, Heads(_oppTeam), _gs.UserBatsFirst, Mark("team_me"), Mark(RivalMarks[RivalIndex]));
    }

    // 投手 + 打順の上から3人の顔
    private static Sprite[] Heads(CharaRoster.Team t)
    {
        var list = new List<Sprite>();
        if (t == null) return list.ToArray();
        if (t.Pitcher != null) { var s = CharaCatalog.Head(t.Pitcher.Id); if (s != null) list.Add(s); }
        foreach (var d in t.Lineup)
        {
            if (list.Count >= 4) break;
            if (d == null || (t.Pitcher != null && d.Id == t.Pitcher.Id)) continue;
            var s = CharaCatalog.Head(d.Id);
            if (s != null) list.Add(s);
        }
        return list.ToArray();
    }
}
