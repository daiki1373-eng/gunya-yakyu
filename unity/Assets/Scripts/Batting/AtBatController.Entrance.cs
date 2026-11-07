using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AtBatController
{
    // ------------------------------------------------------------
    // 登場演出(打者が打席に入る / 投手がマウンドに立つ)
    // キャラごとの違いは IntroCatalog(CharacterIntro.cs)の設定で決まる。
    // 1試合で初めての登場はフル演出、2回目以降は短い版。タップでスキップ。
    // ------------------------------------------------------------
    public static bool IntroEnabled = true;
    private readonly HashSet<string> _introSeen = new HashSet<string>();
    private bool _pitcherIntroPending = true;
    private bool _skipQueued, _introSkip, _presentationInput;

    private IEnumerator IntroWait(float seconds)
    {
        _presentationInput = true;
        float t = 0f;
        while (t < seconds && !_introSkip)
        {
            if (_skipQueued) { _skipQueued = false; _introSkip = true; break; }
            t += Time.deltaTime;
            yield return null;
        }
        _presentationInput = false;
        _swingQueued = false;
    }

    private IEnumerator BatterIntro()
    {
        bool user = _gs.UserBatting;
        var d = IntroCatalog.For(_batterStats, user);
        string key = (user ? "U:" : "O:") + _batterStats.Name;
        bool full = !_introSeen.Contains(key);
        _introSeen.Add(key);
        // テンポ優先: フル演出は自チーム★3以上・相手★4以上の初登場だけ(他は短い版)
        var cdef = string.IsNullOrEmpty(_batterStats.CharaId) ? null : CharaCatalog.Get(_batterStats.CharaId);
        if (cdef != null && cdef.Rarity < (user ? Rarity.R3 : Rarity.R4)) full = false;
        _cursor.SetVisible(false);
        _stage.SetZoneVisible(false);
        _ui.ClearCenter();
        _ui.SetSpeed(null);

        float f = -_batSide;   // 打者の向き(+1=右向き)
        Vector3 bpos = new Vector3(_batSide * BattingConfig.BatterX, 0f, 0f);
        float walk = full ? Mathf.Min(1.2f, d.WalkFrom) : Mathf.Min(0.5f, d.WalkFrom);
        float walkSec = full ? 0.45f : 0.25f;
        _batter.StandAssist = true;
        if (walk > 0f) _batter.Teleport(new Vector2(-f * walk, 0f));

        // 選手を画面の右寄り、名前カードを左下に置く構図
        Vector3 look = bpos + new Vector3(-0.8f, 1.05f, 0f);
        Vector3 cam = bpos + new Vector3(f * 1.7f, 1.5f, -5.4f);
        Vector3 back = new Vector3(-f * walk * 0.6f, 0f, 0f);
        if (full && (d.Pose == IntroPose.Jump || d.Pose == IntroPose.Spin)) { cam += new Vector3(0f, 0.5f, -1.6f); look += new Vector3(0f, 0.5f, 0f); }
        _stage.SetShot(cam + back, look + back, 0.3f, true);
        _stage.SetShot(cam, look, 0.55f, false);
        _batter.SetExpression(d.Face, 30f);

        string pos = string.IsNullOrEmpty(_batterStats.PositionLabel) ? IntroCatalog.PositionName(_batterStats.Position) : _batterStats.PositionLabel;
        string title = user ? (_batterIndex + 1) + "番  " + pos : "相手  " + pos;
        string stats = "ミート " + _batterStats.Meet + "   パワー " + _batterStats.Power + "   走力 " + _batterStats.Speed;
        GameAudio.Play(Sfx.Cheer, full ? d.Cheer : d.Cheer * 0.5f);
        if (walk > 0f)
        {
            _batter.WalkBy(f * walk, walkSec);
            yield return StartCoroutine(IntroWait(walkSec + 0.1f));
        }
        // 歩き終わって定位置に来たら名前とジングル
        _ui.ShowIntroCard(title, _batterStats.Name, stats, full ? d.Copy : "", d.CardColor, Portrait(_batterStats));
        if (full && !_introSkip) IntroMusic(_batterStats, d);
        if (full && d.Fireworks > 0) _stage.Stadium.LaunchFireworks(d.Fireworks);
        if (full && d.Pose != IntroPose.None && !_introSkip)
        {
            _batter.DoGesture(d.Pose, d.Hold);
            if (FreezeKind == -70) { yield return new WaitForSeconds(0.55f); Time.timeScale = 0f; }   // 確認用
            yield return StartCoroutine(IntroWait(Mathf.Min(d.Hold, 0.65f)));
        }
        else
        {
            yield return StartCoroutine(IntroWait(full ? 0.4f : 0.15f));
        }
        yield return StartCoroutine(EndIntro(_batter));
        _cursor.SetVisible(user);
    }

    private IEnumerator PitcherIntro()
    {
        bool userPitching = !_gs.UserBatting;
        var d = IntroCatalog.For(_pitcherStats, userPitching);
        string key = (userPitching ? "UP:" : "OP:") + _pitcherStats.Name;
        bool full = !_introSeen.Contains(key);
        _introSeen.Add(key);
        _cursor.SetVisible(false);
        _stage.SetZoneVisible(false);
        _ui.ClearCenter();
        _ui.SetSpeed(null);

        Vector3 ppos = new Vector3(0f, 0f, BattingConfig.MoundZ);
        float walk = full ? Mathf.Min(1.2f, d.WalkFrom) : 0.5f;
        float walkSec = full ? 0.5f : 0.25f;
        _pitcher.StandAssist = true;
        if (walk > 0f) _pitcher.Teleport(new Vector2(-walk, 0f));
        Vector3 look = ppos + new Vector3(-0.9f, 1.05f, 0f);
        Vector3 cam = ppos + new Vector3(1.8f, 1.6f, -5.6f);
        Vector3 back = new Vector3(-walk * 0.6f, 0f, 0f);
        if (full && (d.Pose == IntroPose.Jump || d.Pose == IntroPose.Spin)) { cam += new Vector3(0f, 0.6f, -1.8f); look += new Vector3(0f, 0.6f, 0f); }
        _stage.SetShot(cam + back + new Vector3(0f, 1.2f, -2f), look + back, 0.3f, true);
        _stage.SetShot(cam, look, 0.7f, false);
        _pitcher.SetExpression(d.Face, 30f);

        string title = (userPitching ? "自チーム" : "相手") + "  " + IntroCatalog.PositionName(1);
        string stats = "球速 " + _pitcherStats.Velo + "   制球 " + _pitcherStats.Ctrl + "   変化 " + _pitcherStats.Brk;
        GameAudio.Play(Sfx.Cheer, full ? d.Cheer : d.Cheer * 0.5f);
        if (walk > 0f)
        {
            _pitcher.WalkBy(walk, walkSec);
            yield return StartCoroutine(IntroWait(walkSec + 0.1f));
        }
        _ui.ShowIntroCard(title, _pitcherStats.Name, stats, full ? d.Copy : "", d.CardColor, Portrait(_pitcherStats));
        if (full && !_introSkip) IntroMusic(_pitcherStats, d);
        if (full && d.Fireworks > 0) _stage.Stadium.LaunchFireworks(d.Fireworks);
        if (full && d.Pose != IntroPose.None && !_introSkip)
        {
            _pitcher.DoGesture(d.Pose, d.Hold);
            if (FreezeKind == -71) { yield return new WaitForSeconds(0.45f); Time.timeScale = 0f; }   // 確認用
            yield return StartCoroutine(IntroWait(Mathf.Min(d.Hold, 0.65f)));
        }
        else
        {
            yield return StartCoroutine(IntroWait(full ? 0.4f : 0.15f));
        }
        yield return StartCoroutine(EndIntro(_pitcher));
    }

    // 登場の曲: SECRET と ★5 は専用曲、それ以外は合成のジングル
    private void IntroMusic(PlayerStats p, IntroDef d)
    {
        var cd = string.IsNullOrEmpty(p.CharaId) ? null : CharaCatalog.Get(p.CharaId);
        if (cd != null && cd.Rarity == Rarity.Secret) Music("bgm-entrance-secret", 7f);
        else if (cd != null && cd.Rarity == Rarity.R5) Music("bgm-entrance-5", 7f);
        else GameAudio.PlayJingle(d.Jingle, d.NoteSec);
    }

    private static Sprite Portrait(PlayerStats p)
    {
        return string.IsNullOrEmpty(p.CharaId) ? null : CharaCatalog.Thumb(p.CharaId);
    }

    // 演出の後片付け: 姿勢を戻してカメラを捕手視点へ
    private IEnumerator EndIntro(RagdollPlayer who)
    {
        _ui.HideIntroCard();
        if (who != null) who.ResetPose();
        _stage.SetCameraMode(CamMode.Catcher);
        _stage.SetZoneVisible(true);
        float t = 0f;
        while (t < 0.35f && !_stage.CatcherCameraSettled()) { t += Time.deltaTime; yield return null; }
        _swingQueued = false;
        _skipQueued = false;
    }

    private IEnumerator PresentationWait(float seconds)
    {
        _presentationInput = true;
        _skipQueued = _swingQueued = false;
        float elapsed = 0f;
        while (elapsed < seconds && !_skipQueued)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        _presentationInput = false;
        _skipQueued = _swingQueued = false;
    }

    private static void ApplyStackLook(RagdollPlayer player, PlayerStats stats, bool user)
    {
        var def = string.IsNullOrEmpty(stats.CharaId) ? null : CharaCatalog.Get(stats.CharaId);
        if (def == null) return;
        player.SetStackCount(user ? Mathf.Max(1, Collection.Count(def.Id)) : 1, CharaCatalog.StackMax(def));
    }

    private void OnDisable()
    {
        if (_stage != null) _stage.SetPlaybackSpeed(1f);
        _presentationInput = _replaying = _stealInput = false;
        _skipQueued = _swingQueued = false;
        if (_ui != null) { _ui.ShowSteal(false, false); _ui.SetReplay(false); }
    }

    private IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

}
