using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AtBatController
{
    // 確認用(通常は -1): DevStealAttempt=1 で企図できるときは必ず盗塁、DevStealSafe=1/0 で成功/失敗を固定
    public static int DevStealAttempt = -1, DevStealSafe = -1;
    private int _stealFrom;
    private bool _stealInput;
    private readonly bool[] _oldBase = new bool[4];
    private readonly bool[] _runnerMoved = new bool[4];
    private readonly CharaDef[] _nextRunner = new CharaDef[4];

    private void CopyBases()
    {
        for (int b = 0; b < 4; b++) _oldBase[b] = _gs.Base[b];
    }

    private int EligibleSteal()
    {
        if (_gs.CanSteal(2)) return 2;
        return _gs.CanSteal(1) ? 1 : 0;
    }

    private void PrepareSteal()
    {
        _stealFrom = 0;
        _stealInput = true;
        int from = EligibleSteal();
        if ((!_gs.UserBatting || AutoPlay) && from > 0
            && (DevStealAttempt == 1 || (DevStealAttempt != 0 && Random.value < BattingConfig.OpponentStealChance)))
            _stealFrom = from;
        _ui.ShowSteal(_gs.UserBatting && !AutoPlay && from > 0, false);
    }

    private void ToggleSteal()
    {
        if (!_stealInput || !_gs.UserBatting) return;
        _stealFrom = _stealFrom > 0 ? 0 : EligibleSteal();
        _ui.ShowSteal(EligibleSteal() > 0, _stealFrom > 0);
    }

    private int CatcherArm()
    {
        var team = _gs.UserBatting ? _oppTeam : _userTeam;
        for (int i = 0; i < team.Lineup.Count; i++)
            if (team.Lineup[i].Position == "捕手") return CharaRoster.Stats(team.Lineup[i]).Arm;
        // 捕手を含まない仮編成は、先頭の守備選手が兼任。
        return team.Lineup.Count > 0 ? CharaRoster.Stats(team.Lineup[0]).Arm : 45;
    }

    private IEnumerator ResolveSteal()
    {
        int from = _stealFrom;
        _stealFrom = 0;
        if (!_gs.CanSteal(from)) yield break;
        int speed = _onBase[from] != null ? CharaRoster.Stats(_onBase[from]).Speed : 45;
        float chance = Mathf.Clamp(0.64f + (speed - CatcherArm()) * 0.004f - (from == 2 ? 0.12f : 0f), 0.30f, 0.90f);
        bool safe = DevStealSafe >= 0 ? DevStealSafe == 1 : Random.value < chance;
        CopyBases();
        _gs.ApplySteal(from, safe);
        UpdateOnBase(_oldBase);
        RefreshHud();
        _stage.SetCameraMode(CamMode.Diamond);
        _stage.ShowRunner(from, true);
        Vector2 start = FieldingModel.BasePos(from);
        Vector2 target = FieldingModel.BasePos(from + 1);
        int receiver = from == 1 ? 5 : 4;
        _stage.SetFielderPos(receiver, target);
        _ui.ClearCenter();   // 直前の打撃の判定(MISS など)を消して、盗塁の表示だけにする
        _ui.ShowResult("盗塁！");
        BgmPlayer.StopJingle(0.1f);
        Music(safe ? "bgm-steal-ok" : "bgm-steal-out", 3f);
        _presentationInput = true;
        _skipQueued = false;
        float t = 0f;
        while (t < 0.95f && !_skipQueued)
        {
            t += Time.deltaTime;
            float run = Mathf.Clamp01(t / (safe ? 0.80f : 1.02f));
            _stage.SetRunnerPos(from, Vector2.Lerp(start, target, run));
            float u = Mathf.Clamp01((t - 0.14f) / 0.65f);
            Vector2 ball = Vector2.Lerp(new Vector2(0f, -1.6f), target, u);
            Vector3 pos = new Vector3(ball.x, 1.0f + Mathf.Sin(u * Mathf.PI) * 2f, ball.y);
            _stage.SetBall(pos);
            _stage.AddTrail(pos, Color.white);
            yield return null;
        }
        _presentationInput = false;
        _skipQueued = _swingQueued = false;
        _stage.CatchPop(receiver);
        GameAudio.Play(Sfx.Catch, 0.65f);
        _ui.ShowResult(safe ? "盗塁成功！" : "盗塁アウト！");
        Count(safe ? "StealSafe" : "CaughtStealing");
        _stage.HideBall();
        _stage.HideRunners();
        SkinRunners();
        _stage.ShowRunnersOnBases(_gs.Base);
    }
    // 走者の進塁を見せる(俯瞰カメラ)
    private IEnumerator AnimateMoves()
    {
        var paths = new List<List<Vector2>>();
        var lens = new List<float>();
        var slots = new List<int>();
        float maxLen = 0f;
        for (int k = 0; k < _gs.Moves.Count; k++)
        {
            RunnerMove m = _gs.Moves[k];
            int target = m.To == -1 ? m.OutBase : m.To;
            if (m.To == -1 && m.OutBase <= 0) continue;
            if (m.To != -1 && target == m.From) continue;
            var path = new List<Vector2>();
            path.Add(FieldingModel.BasePos(m.From));
            for (int i = m.From + 1; i <= target; i++) path.Add(FieldingModel.BasePos(i));
            float len = 0f;
            for (int i = 1; i < path.Count; i++) len += Vector2.Distance(path[i - 1], path[i]);
            paths.Add(path); lens.Add(len); slots.Add(m.From);
            maxLen = Mathf.Max(maxLen, len);
            _stage.ShowRunner(m.From, true);
            _stage.SetRunnerPos(m.From, path[0]);
        }
        if (paths.Count > 0)
        {
            _stage.SetCameraMode(CamMode.Diamond);
            float v = Mathf.Max(22f, maxLen / BattingConfig.RunnerAnimationSeconds);
            float total = maxLen / v;
            float t = 0f;
            _presentationInput = true;
            _skipQueued = false;
            while (t < total + 0.05f && !_skipQueued)
            {
                t += Time.deltaTime;
                for (int i = 0; i < paths.Count; i++)
                {
                    float d = Mathf.Min(t * v, lens[i]);
                    _stage.SetRunnerPos(slots[i], PointAlong(paths[i], d));
                }
                yield return null;
            }
        }
        _presentationInput = false;
        _skipQueued = _swingQueued = false;
        _stage.HideRunners();
        SkinRunners();
        _stage.ShowRunnersOnBases(_gs.Base);
    }

    private static Vector2 PointAlong(List<Vector2> path, float dist)
    {
        for (int i = 1; i < path.Count; i++)
        {
            float seg = Vector2.Distance(path[i - 1], path[i]);
            if (dist <= seg || i == path.Count - 1)
                return Vector2.Lerp(path[i - 1], path[i], seg < 1e-4f ? 1f : Mathf.Clamp01(dist / seg));
            dist -= seg;
        }
        return path[path.Count - 1];
    }

}
