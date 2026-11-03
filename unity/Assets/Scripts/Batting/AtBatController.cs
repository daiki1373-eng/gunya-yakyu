using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// AtBatController.cs
// 打席の進行役(パワプロ式の打席 P1)。
//   構え → 投球モーション → リリース → ボール到達 → スイング判定 → 打球 → 結果
// を1球ずつ回し、カウント(B/S/O)と回・得点を更新して次の打者へ進む。
//
// 操作: マウス移動/タッチドラッグ/矢印キー・WASD でカーソルを動かし、
//       クリック/タップ(SWINGボタン)/Space でスイング。
//
// AutoPlay=true にすると、熟練プレイヤーを模した自動操作で動く(検証用)。
// ============================================================
public class AtBatController : MonoBehaviour
{
    public static bool AutoPlay = false;
    public static bool Verbose = false;
    public static readonly Dictionary<string, int> Tally = new Dictionary<string, int>();

    private enum PitchEnd { None, Ball, CalledStrike, SwingMiss, Foul, FoulTip, InPlay }

    private BattingStage _stage;
    private BattingUI _ui;
    private MeetCursor _cursor;
    private RagdollPlayer _pitcher, _batter;
    private PlayerStats _pitcherStats, _batterStats;
    private int _batSide = -1;   // -1=右打者(x<0側) / +1=左打者

    // 試合状態
    private int _batterIndex = -1;
    private int _balls, _strikes, _outs, _inning = 1, _runs;
    private int _pitchCount;

    // 入力
    private Vector2 _cursorPos = new Vector2(0f, BattingConfig.ZoneCenterY);
    private Vector2 _cursorTarget = new Vector2(0f, BattingConfig.ZoneCenterY);
    private bool _cursorLocked;
    private bool _swingQueued;
    private bool _keyboardMode;
    private Vector3 _lastMouse;

    // 1球の結果
    private PitchEnd _pitchEnd;
    private PlayResult _play;
    private HitOutcome _outcome;

    // ------------------------------------------------------------
    private void Awake()
    {
        Physics2D.IgnoreLayerCollision(RagdollPlayer.RagdollLayer, RagdollPlayer.RagdollLayer, true);

        // 物理(Physics2D)用の地面。上面が y=0。
        var ground = new GameObject("PhysicsGround");
        ground.transform.SetParent(transform, false);
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.AddComponent<BoxCollider2D>().size = new Vector2(200f, 1f);

        _stage = BattingStage.Create(transform);
        _ui = BattingUI.Build(transform);
        _cursor = MeetCursor.Create(_stage.Root);

        _pitcherStats = SpecialCharacters.Panta();
        _pitcher = RagdollPlayer.Create(transform, "Pitcher", Vector2.zero, 1f,
            _pitcherStats, new Color(0.16f, 0.52f, 0.55f), new Color(0.13f, 0.23f, 0.45f), false);
        // 奥行き(マウンド)へ。ResetPoseはzを保つよう修正済み。
        _pitcher.transform.position = new Vector3(0f, 0f, BattingConfig.MoundZ);
    }

    private IEnumerator Start()
    {
        yield return null;
        _ui.ShowResult("PLAY BALL");
        yield return StartCoroutine(Wait(1.0f));
        while (true)
        {
            yield return StartCoroutine(PlayAtBat());
        }
    }

    private IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    // ------------------------------------------------------------
    // 入力とカーソル
    // ------------------------------------------------------------
    private void Update()
    {
        if (!AutoPlay) HandleInput();

        if (!_cursorLocked)
        {
            _cursorPos = Vector2.MoveTowards(_cursorPos, _cursorTarget, BattingConfig.CursorSpeed * Time.deltaTime);
        }
        if (_cursor != null) _cursor.SetPos(_cursorPos);
        if (_ui != null) _ui.Tick();
    }

    private static Vector2 ClampCursor(Vector2 p)
    {
        float mx = BattingConfig.ZoneHalfW + BattingConfig.CursorMargin;
        float yMin = BattingConfig.ZoneBottom - BattingConfig.CursorMargin;
        float yMax = BattingConfig.ZoneTop + BattingConfig.CursorMargin;
        return new Vector2(Mathf.Clamp(p.x, -mx, mx), Mathf.Clamp(p.y, yMin, yMax));
    }

    private void HandleInput()
    {
        bool swing = false;
        bool havePointer = false;
        Vector2 screen = Vector2.zero;

        if (Input.touchCount > 0)
        {
            Touch t0 = Input.GetTouch(0);
            if (_ui.OverSwing(t0.position))
            {
                if (t0.phase == TouchPhase.Began) swing = true;
            }
            else
            {
                // 指で隠れないよう少し上を指す
                screen = t0.position + new Vector2(0f, Screen.height * 0.08f);
                havePointer = true;
            }
            for (int i = 1; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Began) swing = true;
            }
        }
        else
        {
            Vector3 mp = Input.mousePosition;
            if ((mp - _lastMouse).sqrMagnitude > 4f) _keyboardMode = false;
            _lastMouse = mp;
            if (!_keyboardMode && Input.mousePresent && !_ui.OverSwing(mp))
            {
                screen = mp;
                havePointer = true;
            }
            if (Input.GetMouseButtonDown(0)) swing = true;
        }

        // キーボード
        float kx = 0f, ky = 0f;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) kx -= 1f;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) kx += 1f;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) ky -= 1f;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) ky += 1f;
        if (kx != 0f || ky != 0f)
        {
            _keyboardMode = true;
            havePointer = false;
            _cursorTarget = ClampCursor(_cursorPos + new Vector2(kx, ky) * 0.6f);
        }
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))
            swing = true;

        if (havePointer)
        {
            Ray ray = _stage.Cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.z) > 1e-4f)
            {
                float t = -ray.origin.z / ray.direction.z;
                Vector3 w = ray.origin + ray.direction * t;
                _cursorTarget = ClampCursor(new Vector2(w.x, w.y));
            }
        }
        if (swing)
        {
            _swingQueued = true;
            _ui.PulseSwing();
        }
    }

    private bool ConsumeSwing()
    {
        if (!_swingQueued) return false;
        _swingQueued = false;
        return true;
    }

    // ------------------------------------------------------------
    // 打者の入れ替え
    // ------------------------------------------------------------
    private void SetupBatter()
    {
        _batterIndex = (_batterIndex + 1) % 3;
        Color uniform, accent;
        switch (_batterIndex)
        {
            case 0:
                _batterStats = SpecialCharacters.Kabutomaru();
                uniform = new Color(0.78f, 0.22f, 0.20f); accent = new Color(0.25f, 0.15f, 0.10f);
                break;
            case 1:
                _batterStats = SpecialCharacters.Wanta();
                uniform = new Color(0.92f, 0.72f, 0.16f); accent = new Color(0.35f, 0.22f, 0.10f);
                break;
            default:
                _batterStats = SpecialCharacters.HeroDefault();
                uniform = new Color(0.20f, 0.35f, 0.80f); accent = new Color(0.92f, 0.92f, 0.95f);
                break;
        }
        _batSide = _batterStats.BatsRight ? -1 : 1;

        if (_batter != null) Destroy(_batter.gameObject);
        _batter = RagdollPlayer.Create(transform, "Batter",
            new Vector2(_batSide * BattingConfig.BatterX, 0f), -_batSide,
            _batterStats, uniform, accent, true);

        _cursor.Configure(HitJudge.CursorHalfW(_batterStats.Meet), HitJudge.CursorHalfH(_batterStats.Meet), _batSide);
        _cursorPos = _cursorTarget = new Vector2(0f, BattingConfig.ZoneCenterY);
        _ui.SetBatter(_batterStats);
    }

    // ------------------------------------------------------------
    // 打席(1人の打者)
    // ------------------------------------------------------------
    private IEnumerator PlayAtBat()
    {
        SetupBatter();
        _balls = 0;
        _strikes = 0;
        _ui.SetCount(_inning, _balls, _strikes, _outs, _runs);
        _ui.ShowResult("");
        yield return StartCoroutine(Wait(0.4f));

        bool over = false;
        while (!over)
        {
            yield return StartCoroutine(PlayPitch());
            over = ApplyPitchResult();
            _ui.SetCount(_inning, _balls, _strikes, _outs, _runs);
            yield return StartCoroutine(Wait(over ? 1.3f : 0.75f));
            if (_stage != null) _stage.HideBall();
            if (_play.Fielder >= 0 || _pitchEnd == PitchEnd.InPlay)
            {
                _stage.SetCameraMode(CamMode.Catcher);
                _stage.ResetFielders();
            }
        }

        if (_outs >= 3)
        {
            _outs = 0;
            _inning++;
            _ui.SetCount(_inning, _balls, _strikes, _outs, _runs);
            _ui.ShowResult("チェンジ");
            yield return StartCoroutine(Wait(1.2f));
        }
    }

    // 1球ぶんの結果をカウントへ反映。打席が終わったら true。
    private bool ApplyPitchResult()
    {
        bool over = false;
        string key = _pitchEnd.ToString();
        switch (_pitchEnd)
        {
            case PitchEnd.Ball:
                _balls++;
                if (_balls >= 4) { _ui.ShowResult("フォアボール"); over = true; key = "Walk"; }
                break;
            case PitchEnd.CalledStrike:
                _strikes++;
                if (_strikes >= 3) { _outs++; _ui.ShowResult("見逃し三振！"); over = true; key = "StrikeoutLooking"; }
                break;
            case PitchEnd.SwingMiss:
                _strikes++;
                if (_strikes >= 3)
                {
                    _outs++;
                    _ui.ShowResult("空振り三振！");
                    _batter.Flop(1.0f * BattingConfig.GunyaLevel);
                    over = true;
                    key = "StrikeoutSwinging";
                }
                break;
            case PitchEnd.Foul:
                if (_strikes < 2) _strikes++;
                break;
            case PitchEnd.FoulTip:
                _strikes++;
                if (_strikes >= 3) { _outs++; _ui.ShowResult("ファウルチップ三振！"); over = true; key = "StrikeoutTip"; }
                break;
            case PitchEnd.InPlay:
                over = true;
                key = _play.Kind.ToString();
                if (_play.IsOut) _outs++;
                else if (_play.Kind == PlayKind.HomeRun) _runs++;
                break;
        }
        int c;
        Tally.TryGetValue(key, out c);
        Tally[key] = c + 1;
        return over;
    }

    public static string TallyReport()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kv in Tally) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
        return sb.ToString();
    }

    // ------------------------------------------------------------
    // 1球
    // ------------------------------------------------------------
    private static Vector3 PitchPos(PitchPlan plan, float t)
    {
        float s = t / plan.FlightTime;
        Vector3 a = BattingConfig.ReleasePoint;
        Vector3 b = new Vector3(plan.Target.x, plan.Target.y, 0f);
        return Vector3.LerpUnclamped(a, b, s);
    }

    private IEnumerator PlayPitch()
    {
        _pitchEnd = PitchEnd.None;
        _play = new PlayResult { Fielder = -1 };
        _pitchCount++;
        _stage.SetCameraMode(CamMode.Catcher);
        _ui.ClearCenter();
        _ui.ClearSpeed();
        _cursor.ResetColor();
        _cursorLocked = false;
        _stage.HideBall();
        _batter.ResetPose();
        _pitcher.ResetPose();

        PitchPlan plan = PitchAI.ChoosePitch(_pitcherStats, _batterStats, _balls, _strikes);

        yield return StartCoroutine(Wait(0.45f));
        _swingQueued = false;

        _pitcher.DoPitchMotion();
        float tWindStart = Time.time;
        float tReleaseAt = tWindStart + BattingConfig.WindupSeconds;
        if (AutoPlay) StartCoroutine(AutoPilot(plan, tReleaseAt));

        bool swung = false;
        float tSwing = 0f;
        Vector2 swingCursor = Vector2.zero;

        // 投球モーション中(ここで振ると早振り)
        while (Time.time < tReleaseAt)
        {
            if (!swung && ConsumeSwing())
            {
                swung = true;
                tSwing = Time.time;
                swingCursor = _cursorPos;
                _cursorLocked = true;
                _batter.DoSwing();
            }
            yield return null;
        }

        float tRelease = Time.time;
        float tArrive = tRelease + plan.FlightTime;
        _ui.ShowSpeed(plan.Kmh);
        _stage.SetBall(PitchPos(plan, 0f));

        // ボールが来る間
        while (true)
        {
            float t = Time.time - tRelease;
            _stage.SetBall(PitchPos(plan, t));
            if (!swung && ConsumeSwing())
            {
                swung = true;
                tSwing = Time.time;
                swingCursor = _cursorPos;
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
            yield return StartCoroutine(FinishFlight(plan, tRelease));
            if (plan.InZone) { _pitchEnd = PitchEnd.CalledStrike; _ui.ShowResult("ストライク"); }
            else { _pitchEnd = PitchEnd.Ball; _ui.ShowResult("ボール"); }
            LogPitch(plan, "take", 0f, null);
            yield break;
        }

        // ---- スイング判定 ----
        float timingMs = ((tSwing + BattingConfig.SwingLag) - tArrive) * 1000f;
        var ctx = new SwingContext
        {
            BallPos = plan.Target,
            CursorPos = swingCursor,
            CursorHalfW = HitJudge.CursorHalfW(_batterStats.Meet),
            CursorHalfH = HitJudge.CursorHalfH(_batterStats.Meet),
            TimingMs = timingMs,
            Meet = _batterStats.Meet,
            Power = _batterStats.Power,
            BatSide = _batSide
        };
        _outcome = HitJudge.Judge(ctx);
        Color rc = BattingUI.RankColor(_outcome.Rank);
        _cursor.FlashColor(rc, 0.5f);

        if (_outcome.Rank == HitRank.Whiff)
        {
            string tag = timingMs < -BattingConfig.TimingWeakMs ? "EARLY"
                       : (timingMs > BattingConfig.TimingWeakMs ? "LATE" : "");
            _ui.ShowRank(HitRank.Whiff, tag);
            _ui.ShowResult("空振り");
            _pitchEnd = PitchEnd.SwingMiss;
            StartCoroutine(DelayedFlop(0.18f, 0.5f));
            yield return StartCoroutine(FinishFlight(plan, tRelease));
            LogPitch(plan, "whiff", timingMs, _outcome);
            yield break;
        }

        // ---- 接触 ----
        Vector3 contact = new Vector3(plan.Target.x, plan.Target.y, 0f);
        _stage.SetBall(contact);
        _stage.Flash(contact, rc, _outcome.Rank == HitRank.Perfect ? 1.1f : 0.7f);
        string rankTag = (_outcome.Rank == HitRank.Perfect) ? "" : (timingMs < -20f ? "EARLY" : (timingMs > 20f ? "LATE" : ""));
        _ui.ShowRank(_outcome.Rank, rankTag);

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
        _play = FieldingModel.Resolve(traj, _batterStats.Speed);
        yield return StartCoroutine(PlayBattedBall(traj, _play));
        _pitchEnd = (_play.Kind == PlayKind.Foul) ? PitchEnd.Foul : PitchEnd.InPlay;
        LogPitch(plan, _play.Kind.ToString(), timingMs, _outcome);
    }

    private IEnumerator DelayedFlop(float delay, float power)
    {
        yield return StartCoroutine(Wait(delay));
        if (_batter != null) _batter.Flop(power * BattingConfig.GunyaLevel);
    }

    // 振らなかった/空振りしたボールを捕手の位置まで見届ける
    private IEnumerator FinishFlight(PitchPlan plan, float tRelease)
    {
        while (true)
        {
            float t = Time.time - tRelease;
            Vector3 p = PitchPos(plan, t);
            if (p.z <= -0.7f) break;
            _stage.SetBall(p);
            yield return null;
        }
        _stage.HideBall();
    }

    // ------------------------------------------------------------
    // 打球の再生(打球カメラで追う)
    // ------------------------------------------------------------
    private IEnumerator PlayBattedBall(BattedBallTrajectory tr, PlayResult play)
    {
        _stage.SetCameraMode(CamMode.Follow);
        _stage.ResetFielders();

        FieldingModel.Fielder f = default(FieldingModel.Fielder);
        bool hasF = play.Fielder >= 0;
        if (hasF) f = FieldingModel.Defaults[play.Fielder];
        Vector2 target = new Vector2(play.PlayPos.x, play.PlayPos.z);
        bool resultShown = false;

        float t = 0f;
        while (t < play.EndTime)
        {
            t += Time.deltaTime;
            Vector3 p = tr.PosAt(Mathf.Min(t, tr.Duration));
            _stage.SetBall(p);
            _stage.SetFollowTarget(p);

            if (hasF)
            {
                float run = Mathf.Max(0f, t - FieldingModel.React) * f.Speed * FieldingModel.RunEfficiency;
                Vector2 to = target - f.Pos;
                float dist = to.magnitude;
                Vector2 pos = dist < 1e-3f ? f.Pos : f.Pos + to / dist * Mathf.Min(dist, run);
                _stage.SetFielderPos(play.Fielder, pos);
            }

            if (!resultShown && t >= play.PlayTime)
            {
                resultShown = true;
                ShowPlayResult(play);
            }
            yield return null;
        }
        if (!resultShown) ShowPlayResult(play);
    }

    private void ShowPlayResult(PlayResult play)
    {
        string label = FieldingModel.Label(play.Kind);
        if (play.Kind == PlayKind.HomeRun || play.IsHit)
            label += "  " + Mathf.RoundToInt(play.Distance) + "m";
        _ui.ShowResult(label);
        if (play.Kind == PlayKind.HomeRun) _pitcher.Flop(0.7f * BattingConfig.GunyaLevel);
    }

    // ------------------------------------------------------------
    // 検証用の自動操作(熟練プレイヤー模擬)
    // ------------------------------------------------------------
    private IEnumerator AutoPilot(PitchPlan plan, float tRelease)
    {
        Vector2 aim = plan.Target + new Vector2(PitchAI.Gauss() * 0.33f, PitchAI.Gauss() * 0.33f);
        _cursorTarget = ClampCursor(aim);
        bool swing = plan.InZone ? (Random.value < 0.92f) : (Random.value < 0.15f);
        if (!swing) yield break;
        float tArrive = tRelease + plan.FlightTime;
        float tSw = tArrive - BattingConfig.SwingLag + PitchAI.Gauss() * 0.12f;
        while (Time.time < tSw) yield return null;
        _swingQueued = true;
    }

    private void LogPitch(PitchPlan plan, string what, float timingMs, HitOutcome? o)
    {
        if (!Verbose) return;
        string r = o.HasValue ? o.Value.Rank + " e=" + o.Value.EllipseDist.ToString("F2") : "-";
        Debug.Log("[AtBat] #" + _pitchCount + " " + _balls + "-" + _strikes + "-" + _outs
                  + " " + _batterStats.Name + " " + plan.Kmh.ToString("F0") + "km/h target=("
                  + plan.Target.x.ToString("F2") + "," + plan.Target.y.ToString("F2") + ") zone=" + plan.InZone
                  + " -> " + what + " dt=" + timingMs.ToString("F0") + "ms " + r);
    }
}
