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
public partial class AtBatController : MonoBehaviour
{
    public static bool AutoPlay = false;
    // ホーム画面(AppShell)からの制御: 試合開始待ち / 入力を受け付けない
    public static bool HoldStart = false;
    public static bool InputBlocked = false;
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
        // 物理(Physics2D)用の地面。上面が y=0。
        var ground = new GameObject("PhysicsGround");
        ground.transform.SetParent(transform, false);
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.AddComponent<BoxCollider2D>().size = new Vector2(200f, 1f);

        _stage = BattingStage.Create(transform);
        GameAudio.Init(gameObject);
        _ui = BattingUI.Build(transform);
        FaceCutIn.Create(this);
        _cursor = MeetCursor.Create(_stage.Root);

        _pitcherStats = SpecialCharacters.Panta();
        CreatePitcher(BattingConfig.OppJersey, BattingConfig.OppAccent);
    }

    // 投手は守備側のユニフォームで作り直す(能力は共通)
    private void CreatePitcher(Color jersey, Color accent)
    {
        if (_pitcher != null) Destroy(_pitcher.gameObject);
        _pitcher = RagdollPlayer.Create(transform, "Pitcher", Vector2.zero, 1f, _pitcherStats, jersey, accent, false,
            jersey == BattingConfig.UserJersey);
        // 奥行き(マウンド)へ。ResetPoseはzを保つよう修正済み。
        _pitcher.transform.position = new Vector3(0f, 0f, BattingConfig.MoundZ);
        _stage.AddShadow(_pitcher.Pelvis.transform, 1.6f, jersey);
        ApplyStackLook(_pitcher, _pitcherStats, jersey == BattingConfig.UserJersey);
    }

    private int _teamsFor = -1;

    // ---- ガチャのキャラで組むチーム ----
    private CharaRoster.Team _userTeam, _oppTeam;
    private float _gameStartT;

    // 守っているチームの野手(一塁〜右翼の7人)をキャラの立ち姿にする。捕手は画面に出ない
    private void SkinFielders(CharaRoster.Team team)
    {
        for (int i = 2; i < FieldingModel.Defaults.Length; i++)
        {
            int k = i - 2;
            var d = k < team.Lineup.Count ? team.Lineup[k] : null;
            _stage.SetFielderSkin(i, d != null ? CharaCatalog.Cut(d.Id) : null);
        }
    }

    // 塁上の走者が誰か(キャラ)。打席が終わるたびに進塁の記録から更新する
    private readonly CharaDef[] _onBase = new CharaDef[4];

    private void UpdateOnBase(bool[] oldBase)
    {
        var moved = _runnerMoved;
        for (int b = 0; b < 4; b++) moved[b] = false;
        foreach (var m in _gs.Moves) if (m.From >= 1 && m.From <= 3) moved[m.From] = true;
        var next = _nextRunner;
        for (int b = 1; b <= 3; b++) next[b] = (oldBase[b] && !moved[b]) ? _onBase[b] : null;
        var batter = string.IsNullOrEmpty(_batterStats.CharaId) ? null : CharaCatalog.Get(_batterStats.CharaId);
        foreach (var m in _gs.Moves)
            if (m.To >= 1 && m.To <= 3) next[m.To] = m.From == 0 ? batter : _onBase[m.From];
        for (int b = 1; b <= 3; b++) _onBase[b] = _gs.Base[b] ? next[b] : null;
    }

    private void SkinRunners()
    {
        _stage.SetRunnerSkin(0, string.IsNullOrEmpty(_batterStats.CharaId) ? null : CharaCatalog.Cut(_batterStats.CharaId));
        for (int b = 1; b <= 3; b++)
            _stage.SetRunnerSkin(b, _onBase[b] != null ? CharaCatalog.Cut(_onBase[b].Id) : null);
    }

    private void BuildTeams()
    {
        _userTeam = CharaRoster.UserTeam();
        _oppTeam = CharaRoster.OpponentTeam(_userTeam, Random.Range(0, 100000));
        _batterIndex = -1;
        _oppIndex = -1;
        _teamsFor = -1;   // 投手を作り直す
    }

    private void ApplyTeams()
    {
        int key = _gs.UserBatting ? 1 : 0;
        if (_teamsFor == key) return;
        _teamsFor = key;
        _pitcherIntroPending = true;
        if (_userTeam == null) BuildTeams();
        if (_gs.UserBatting)
        {
            _pitcherStats = CharaRoster.Stats(_oppTeam.Pitcher);
            _stage.SetTeams(BattingConfig.OppJersey, BattingConfig.OppAccent, BattingConfig.UserJersey, BattingConfig.UserAccent);
            CreatePitcher(BattingConfig.OppJersey, BattingConfig.OppAccent);
            SkinFielders(_oppTeam);
        }
        else
        {
            _pitcherStats = CharaRoster.Stats(_userTeam.Pitcher);
            _stage.SetTeams(BattingConfig.UserJersey, BattingConfig.UserAccent, BattingConfig.OppJersey, BattingConfig.OppAccent);
            CreatePitcher(BattingConfig.UserJersey, BattingConfig.UserAccent);
            SkinFielders(_userTeam);
        }
    }

    private IEnumerator Start()
    {
        yield return null;
        while (HoldStart) yield return null;
        BuildTeams();   // 所持カードは試合開始時点のもので組む
        _gameStartT = Time.unscaledTime;
        _ui.ShowResult("PLAY BALL");
        BgmPlayer.PlayBase(BgmPlayer.Match);
        BgmPlayer.Jingle("bgm-playball", 7f);
        RefreshHud();
        // 開幕: 球場全体を映して花火を上げる(タップで飛ばせる)
        _stage.SetShot(new Vector3(0f, 9f, -14f), new Vector3(0f, 40f, 130f), 0.3f, true);
        _stage.SetShot(new Vector3(0f, 5f, -9f), new Vector3(0f, 30f, 130f), 1.6f, false);
        _stage.Stadium.LaunchFireworks(8);
        GameAudio.Play(Sfx.Cheer, 0.7f);
        _introSkip = false; _skipQueued = false;
        yield return StartCoroutine(IntroWait(IntroEnabled ? 1.2f : 0.35f));
        _ui.ShowResult("");
        _stage.SetCameraMode(CamMode.Catcher);
        while (true)
        {
            yield return StartCoroutine(PlayAtBat());
        }
    }

}
