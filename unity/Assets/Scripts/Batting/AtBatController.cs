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

    private enum PitchEnd { None, Ball, CalledStrike, SwingMiss, Foul, FoulTip, InPlay, HitByPitch }

    private BattingStage _stage;
    private BattingUI _ui;
    private MeetCursor _cursor;
    private RagdollPlayer _pitcher, _batter;
    private PlayerStats _pitcherStats, _batterStats;
    private int _batSide = -1;   // -1=右打者(x<0側) / +1=左打者

    // 試合状態
    private readonly GameState _gs = new GameState();
    private int _batterIndex = -1;
    private int _pitchCount;
    private int _last1 = -1, _last2 = -1;   // 直前2球の球種(配球AI用)
    public static readonly List<string> GameLog = new List<string>();
    public static bool ShowPitchName = true;
    // ---- 投球(守り)側の状態 ----
    private int _selKind;                           // 選んでいる球種
    private readonly bool[] _avail = new bool[5];   // 投げられる球種
    private bool _reticleOn;
    private Color _reticleCol = Color.white;
    private PitchPlan _defPlan;
    private float _defTRelease;
    private Vector2 _aiCursor;                      // 打者AIのスイング位置
    private bool _defTapOk;                         // 守りで、タップを受け付けてよい間だけtrue
    private const float SweetBeforeRelease = 0.10f; // メーターのベスト点=リリースの何秒前か
    private int _oppIndex = -1;

    // ---- P4: 演出・リプレイ ----
    public static bool ReplayEnabled = true;
    private BattedBallTrajectory _lastTraj;
    private PlayResult _lastPlay;
    private float _lastSay = -99f;
    private bool _replayDone;
    private int _bestScore = -1;
    private BattedBallTrajectory _bestTraj;
    private PlayResult _bestPlay;

    public static int FreezeKind = -99;   // 検証用: この球種(-1=何でも)の終盤でtimeScaleを0にして止める

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
        GameAudio.Init(gameObject);
        _ui = BattingUI.Build(transform);
        _cursor = MeetCursor.Create(_stage.Root);

        _pitcherStats = SpecialCharacters.Panta();
        CreatePitcher(BattingConfig.OppJersey, BattingConfig.OppAccent);
    }

    // 投手は守備側のユニフォームで作り直す(能力は共通)
    private void CreatePitcher(Color jersey, Color accent)
    {
        if (_pitcher != null) Destroy(_pitcher.gameObject);
        _pitcher = RagdollPlayer.Create(transform, "Pitcher", Vector2.zero, 1f, _pitcherStats, jersey, accent, false);
        // 奥行き(マウンド)へ。ResetPoseはzを保つよう修正済み。
        _pitcher.transform.position = new Vector3(0f, 0f, BattingConfig.MoundZ);
        _stage.AddShadow(_pitcher.Pelvis.transform, 1.6f);
    }

    private int _teamsFor = -1;

    private void ApplyTeams()
    {
        int key = _gs.UserBatting ? 1 : 0;
        if (_teamsFor == key) return;
        _teamsFor = key;
        if (_gs.UserBatting)
        {
            _stage.SetTeams(BattingConfig.OppJersey, BattingConfig.OppAccent, BattingConfig.UserJersey, BattingConfig.UserAccent);
            CreatePitcher(BattingConfig.OppJersey, BattingConfig.OppAccent);
        }
        else
        {
            _stage.SetTeams(BattingConfig.UserJersey, BattingConfig.UserAccent, BattingConfig.OppJersey, BattingConfig.OppAccent);
            CreatePitcher(BattingConfig.UserJersey, BattingConfig.UserAccent);
        }
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
            float spd = _gs.UserBatting ? BattingConfig.CursorSpeed : 6.0f;
            _cursorPos = Vector2.MoveTowards(_cursorPos, _cursorTarget, spd * Time.deltaTime);
        }
        if (_cursor != null) _cursor.SetPos(_cursorPos);
        if (_stage != null && !_gs.UserBatting) _stage.SetReticle(_cursorPos, _reticleOn, _reticleCol);
        if (_ui != null) _ui.Tick();
    }

    private Vector2 ClampCursor(Vector2 p)
    {
        if (!_gs.UserBatting)
            return new Vector2(Mathf.Clamp(p.x, -1.1f, 1.1f), Mathf.Clamp(p.y, 0.25f, 2.0f));
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
            int tb = _ui.PitchButtonAt(t0.position);
            if (tb >= 0)
            {
                if (t0.phase == TouchPhase.Began) SelectPitchKind(tb);
            }
            else if (_ui.OverSwing(t0.position))
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
            int mb = _ui.PitchButtonAt(mp);
            if (!_keyboardMode && Input.mousePresent && !_ui.OverSwing(mp) && mb < 0)
            {
                screen = mp;
                havePointer = true;
            }
            if (Input.GetMouseButtonDown(0))
            {
                if (mb >= 0) SelectPitchKind(mb); else swing = true;
            }
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
        for (int k = 0; k < 5; k++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + k)) SelectPitchKind(k);
        }
        if (Input.GetKeyDown(KeyCode.M)) GameAudio.Muted = !GameAudio.Muted;

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
        if (swing && (_gs.UserBatting || _defTapOk))
        {
            _swingQueued = true;
            _ui.PulseSwing();
        }
    }

    private void SelectPitchKind(int k)
    {
        if (_gs.UserBatting || k < 0 || k > 4 || !_avail[k]) return;
        _selKind = k;
        _ui.SelectPitch(k);
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
    private PlayerStats OpponentBatter(int i)
    {
        PlayerStats p;
        switch (i % 3)
        {
            case 0:
                p = new PlayerStats { Name = "ライバルA", Position = 5, Species = "human", BatsRight = true,
                    Meet = 50, Power = 44, Speed = 45, Field = 40, Arm = 40 };
                break;
            case 1:
                p = new PlayerStats { Name = "ライバルB", Position = 4, Species = "human", BatsRight = false,
                    Meet = 60, Power = 36, Speed = 56, Field = 44, Arm = 40 };
                break;
            default:
                p = new PlayerStats { Name = "ライバルC", Position = 3, Species = "human", BatsRight = true,
                    Meet = 44, Power = 60, Speed = 36, Field = 36, Arm = 40 };
                break;
        }
        SpeciesTable.Apply(p);
        return p;
    }

    private void SetupBatter()
    {
        Color uniform, accent;
        if (_gs.UserBatting)
        {
            _batterIndex = (_batterIndex + 1) % 3;
            switch (_batterIndex)
            {
                case 0:
                    _batterStats = SpecialCharacters.Kabutomaru();
                    uniform = BattingConfig.UserJersey; accent = BattingConfig.UserAccent;
                    break;
                case 1:
                    _batterStats = SpecialCharacters.Wanta();
                    uniform = BattingConfig.UserJersey; accent = BattingConfig.UserAccent;
                    break;
                default:
                    _batterStats = SpecialCharacters.HeroDefault();
                    uniform = BattingConfig.UserJersey; accent = BattingConfig.UserAccent;
                    break;
            }
        }
        else
        {
            _oppIndex = (_oppIndex + 1) % 3;
            _batterStats = OpponentBatter(_oppIndex);
            uniform = BattingConfig.OppJersey; accent = BattingConfig.OppAccent;
        }
        _batSide = _batterStats.BatsRight ? -1 : 1;

        if (_batter != null) Destroy(_batter.gameObject);
        _batter = RagdollPlayer.Create(transform, "Batter",
            new Vector2(_batSide * BattingConfig.BatterX, 0f), -_batSide,
            _batterStats, uniform, accent, true);
        _stage.AddShadow(_batter.Pelvis.transform, 1.6f);
        _stage.Stadium.SetBoardBatter(_batterStats.Name);

        _cursor.Configure(HitJudge.CursorHalfW(_batterStats.Meet), HitJudge.CursorHalfH(_batterStats.Meet), _batSide);
        _cursor.SetVisible(_gs.UserBatting);
        _cursorPos = _cursorTarget = new Vector2(0f, BattingConfig.ZoneCenterY);
        if (_gs.UserBatting) _ui.SetBatter(_batterStats);
        else _ui.SetBatterLabel("相手 " + _batterStats.Name + "  ミート " + _batterStats.Meet + "  パワー " + _batterStats.Power);
    }

    // ------------------------------------------------------------
    // 打席(1人の打者)
    // ------------------------------------------------------------
    private void RefreshHud()
    {
        _ui.SetCount(_gs.Inning, _gs.UserBatting, _gs.Balls, _gs.Strikes, _gs.Outs, _gs.Runs, _gs.OppRuns);
        _stage.Stadium.SetBoard(_gs.Inning, _gs.UserBatting, _gs.Balls, _gs.Strikes, _gs.Outs, _gs.Runs, _gs.OppRuns);
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
        _stage.ShowRunnersOnBases(_gs.Base);
        _ui.SetSwingLabel(_gs.UserBatting ? "SWING" : "投げる");
        _ui.ShowPitchPanel(false, _avail, _selKind);
        _reticleOn = false;
        RefreshHud();
        _ui.ShowResult("");
        _ui.ShowBanner("");
        yield return StartCoroutine(Wait(0.4f));

        bool over = false;
        while (!over)
        {
            yield return StartCoroutine(PlayPitch());
            over = ApplyPitchResult();
            RefreshHud();
            if (over && ReplayEnabled && _pitchEnd == PitchEnd.InPlay && (_lastPlay.Quirk != QuirkKind.None || _lastPlay.Kind == PlayKind.HomeRun))
                yield return StartCoroutine(PlayReplay(_lastTraj, _lastPlay, 0.5f, 11f, "● REPLAY"));
            if (over) yield return StartCoroutine(AnimateMoves());
            yield return StartCoroutine(Wait(over ? 0.8f : 0.7f));
            _stage.HideBall();
            _stage.SetCameraMode(CamMode.Catcher);
            _stage.ResetFielders();
        }

        if (_gs.Outs >= 3)
        {
            _ui.ShowResult("チェンジ");
            yield return StartCoroutine(Wait(1.0f));
            _gs.EndInningIfNeeded();
            _stage.HideRunners();
            if (_gs.GameOver)
            {
                string res = _gs.Runs > _gs.OppRuns ? "勝ち！" : (_gs.Runs < _gs.OppRuns ? "負け…" : "引き分け");
                _ui.ShowResult("試合終了  " + _gs.Runs + " - " + _gs.OppRuns + "  " + res);
                _ui.ShowBanner("珍プレー " + _gs.QuirkCount);
                GameLog.Add("game" + _gs.GameNo + " " + _gs.Runs + "-" + _gs.OppRuns + " quirks=" + _gs.QuirkCount + " pa=" + _gs.PlateAppearances);
                yield return StartCoroutine(Wait(1.6f));
                if (ReplayEnabled && _bestScore >= 3)
                {
                    _ui.ShowBanner("今日の珍プレー");
                    yield return StartCoroutine(PlayReplay(_bestTraj, _bestPlay, 0.45f, 9f, "● 今日の珍プレー"));
                    yield return StartCoroutine(Wait(1.0f));
                }
                _bestScore = -1;
                _gs.NewGame();
                _ui.ShowBanner("");
            }
            RefreshHud();
        }
    }

    // 1球ぶんの結果をカウントと走者へ反映。打席が終わったら true。
    private bool ApplyPitchResult()
    {
        bool over = false;
        string key = _pitchEnd.ToString();
        _gs.Moves.Clear();
        _gs.LastRuns = 0;
        switch (_pitchEnd)
        {
            case PitchEnd.Ball:
                _gs.Balls++;
                if (_gs.Balls >= 4) { _gs.ApplyWalk(); _ui.ShowResult("フォアボール"); over = true; key = "Walk"; }
                break;
            case PitchEnd.HitByPitch:
                _gs.ApplyWalk();
                _ui.ShowResult("デッドボール");
                over = true;
                key = "HitByPitch";
                break;
            case PitchEnd.CalledStrike:
                _gs.Strikes++;
                if (_gs.Strikes >= 3) { _gs.ApplyStrikeout(); _ui.ShowResult("見逃し三振！"); _pitcher.SetExpression(Expr.Happy, 1.5f); _batter.SetExpression(Expr.Surprise, 1.2f); over = true; key = "StrikeoutLooking"; }
                break;
            case PitchEnd.SwingMiss:
                _gs.Strikes++;
                if (_gs.Strikes >= 3)
                {
                    _gs.ApplyStrikeout();
                    _ui.ShowResult("空振り三振！"); _pitcher.SetExpression(Expr.Happy, 1.5f);
                    if (Random.value < 0.5f) Say(Commentary.ForWhiffK(), new Color(1f, 0.9f, 0.4f), false);
                    _batter.Flop(1.0f * BattingConfig.GunyaLevel);
                    over = true;
                    key = "StrikeoutSwinging";
                }
                break;
            case PitchEnd.Foul:
                if (_gs.Strikes < 2) _gs.Strikes++;
                break;
            case PitchEnd.FoulTip:
                _gs.Strikes++;
                if (_gs.Strikes >= 3) { _gs.ApplyStrikeout(); _ui.ShowResult("ファウルチップ三振！"); over = true; key = "StrikeoutTip"; }
                break;
            case PitchEnd.InPlay:
                over = true;
                key = _play.Kind.ToString();
                if (_play.Kind == PlayKind.HomeRun) _gs.ApplyHit(4);
                else if (_play.IsHit) _gs.ApplyHit(Mathf.Max(1, _play.Bases));
                else
                {
                    int ob = _gs.Outs;
                    _gs.ApplyOut(_play.Kind, _play.Fielder, _play.Distance);
                    if (_gs.Outs - ob >= 2) { key = "DoublePlay"; Say(Commentary.ForDoublePlay(), new Color(1f, 0.9f, 0.4f), true); }
                }
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
            float v = Mathf.Max(22f, maxLen / 2.0f);
            float total = maxLen / v;
            float t = 0f;
            while (t < total + 0.1f)
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
        _stage.HideRunners();
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
            yield return StartCoroutine(Wait(0.45f));
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
        yield return StartCoroutine(PlayBattedBall(traj, _play));
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
        yield return StartCoroutine(Wait(0.30f));
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
            tAutoStart = Time.time + 0.5f;
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
        yield return StartCoroutine(Wait(delay));
        if (_batter != null) _batter.Flop(power * BattingConfig.GunyaLevel);
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
    // 打球の再生(打球カメラで追う)
    // ------------------------------------------------------------
    private IEnumerator PlayBattedBall(BattedBallTrajectory tr, PlayResult play, float speed = 1f, bool replay = false)
    {
        _stage.SetCameraMode(CamMode.Follow);
        _stage.ResetFielders();

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

        float t = 0f;
        while (t < play.EndTime)
        {
            t += Time.deltaTime * speed;
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
        yield return StartCoroutine(Wait(delay));
        if (_batter != null) _batter.ThrowBat(0.8f * BattingConfig.GunyaLevel);
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
        if (Time.timeScale == 0f) Time.timeScale = prev;
    }

    // 一瞬スローにして戻す(ホームラン用)。ヒットストップが終わってから始める。
    private IEnumerator SlowMo(float scale, float realSeconds)
    {
        yield return new WaitForSecondsRealtime(0.09f);
        float baseScale = Time.timeScale;
        if (baseScale <= 0f) yield break;
        Time.timeScale = baseScale * scale;
        yield return new WaitForSecondsRealtime(realSeconds);
        float t = 0f;
        while (t < 0.25f)
        {
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
        _ui.SetReplay(true, label);
        _ui.ClearCenter();
        _stage.HideBall();
        _stage.FollowDist = dist;
        _stage.SnapFollow(tr.PosAt(0f));
        _replayDone = false;
        _swingQueued = false;
        _defTapOk = true;
        Coroutine co = StartCoroutine(ReplayRun(tr, play, speed));
        while (!_replayDone)
        {
            if (ConsumeSwing()) { StopCoroutine(co); break; }
            yield return null;
        }
        _defTapOk = false;
        _swingQueued = false;
        _stage.FollowDist = 18f;
        _stage.HideBall();
        _stage.ResetFielders();
        _ui.SetReplay(false);
        _ui.ClearCenter();
        _ui.ShowBanner("");
        yield return StartCoroutine(Wait(0.3f));
    }

    private IEnumerator ReplayRun(BattedBallTrajectory tr, PlayResult play, float speed)
    {
        yield return StartCoroutine(PlayBattedBall(tr, play, speed, true));
        yield return new WaitForSecondsRealtime(0.5f);
        _replayDone = true;
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

    private void ShowPlayResult(PlayResult play)
    {
        string label = FieldingModel.Label(play.Kind);
        if (play.Quirk == QuirkKind.BadThrow || play.Quirk == QuirkKind.Slip) label = "エラー";
        else if (play.Quirk == QuirkKind.Collision || (play.Quirk == QuirkKind.FenceCrash && !play.IsOut))
            label = play.Kind == PlayKind.Foul ? "ファウル" : "落球";
        else if (play.Kind == PlayKind.HomeRun || play.IsHit)
            label += "  " + Mathf.RoundToInt(play.Distance) + "m";
        _ui.ShowResult(label);
        if (play.Kind == PlayKind.HomeRun)
        {
            _pitcher.Flop(0.7f * BattingConfig.GunyaLevel);
            GameAudio.Play(Sfx.BigCheer, 1f);
            Say(Commentary.ForHomeRun(), new Color(1f, 0.85f, 0.2f), true);
        }
        else if (play.IsHit && play.Quirk == QuirkKind.None) GameAudio.Play(Sfx.Cheer, 0.6f);
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
    }
}
