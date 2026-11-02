using UnityEngine;
using UnityEngine.UI;

// ============================================================
// GameController.cs
// フェーズ1の試合ループ: タップで投球 → タイミングでスイング →
// 打球が物理で飛ぶ/空振り → 1打席が完結して次の打席へ。
//
// 判定はHTML版の確率モデルを単純化して移植している(数値の出どころは
// 各メソッドのコメント参照)。見た目の動きは RagdollPlayer と物理が担当し、
// 「当たるかどうか・打球の質」はモデル側で決めるハイブリッド方式。
// これはHTML版が (タイミング窓 → 質q → 飛距離) で判定していた構造と同じ。
//
// 操作: すべてタップ(マウス左クリックでも可)。
//   待機中タップ = 投球、投球中タップ = スイング。
// プレイヤーは投手と打者の両方をタップで操作するプロトタイプ仕様。
// ============================================================
public class GameController : MonoBehaviour
{
    private enum Phase { WaitPitch, PitchFlight, BallFlight, Message }
    private enum PendingResult { None, Foul, Single, Double, HomeRun, Out }

    // レイアウト(BackgroundBuilder の定数と対応)
    private static readonly Vector2 ReleasePoint = new Vector2(-2.55f, -1.45f);
    private static readonly Vector2 ZoneCenter = new Vector2(5.35f, -2.00f);

    private RagdollPlayer _pitcher;
    private RagdollPlayer _batter;
    private PlayerStats _pitcherStats;
    private PlayerStats _batterStats;
    private int _batterIndex = -1;

    private GameObject _ball;
    private Rigidbody2D _ballRb;
    private SpriteRenderer _ballSr;

    // 試合状態
    private Phase _phase = Phase.WaitPitch;
    private int _balls, _strikes, _outs, _inning = 1, _runs;
    private int _gauge;
    private float _messageUntil;
    private System.Action _afterMessage;

    // 投球中の一時状態
    private PitchType _pitchType;
    private float _stuff;
    private float _targetCx, _targetCy; // HTML版の cx/cy (ゾーン単位)
    private Vector2 _flightStartPos, _flightCtrlPos, _flightEndPos;
    private float _flightStartTime, _flightDuration, _idealTime;
    private bool _hasSwing;
    private float _swingDms;
    private float _windowMs;        // スイング窓(StartPitchで算出)
    private float _arrivalTime;     // ボールがプレートに到達する時刻
    private float _resolveDeadline; // 見逃し確定の時刻 = 理想時刻 + 窓
    private float _flightDistance;
    private PendingResult _pendingResult = PendingResult.None;
    private string _pendingText = "";
    private float _ballFlightDeadline;
    private float _ballFlightStartTime;

    // UI
    private Text _countText, _namesText, _messageText, _hintText, _gaugeLabel;
    private Image _gaugeFill;

    // ------------------------------------------------------------
    private void Awake()
    {
        SetupCamera();
        // ラグドール同士(同一レイヤー)は衝突させない。地面とは衝突する。
        Physics2D.IgnoreLayerCollision(RagdollPlayer.RagdollLayer, RagdollPlayer.RagdollLayer, true);
        // ボール(レイヤー9)は判定を確率モデルで行うため、ラグドールとは衝突させない。
        Physics2D.IgnoreLayerCollision(9, RagdollPlayer.RagdollLayer, true);

        BackgroundBuilder.Build(transform);

        // 投手は固定でパン太(パンダ・変化球タイプ)。HTML版の固有キャラ。
        _pitcherStats = SpecialCharacters.Panta();
        _pitcher = RagdollPlayer.Create(transform, "Pitcher",
            new Vector2(BackgroundBuilder.MoundX, BackgroundBuilder.MoundTopY), 1f,
            _pitcherStats, new Color(0.16f, 0.52f, 0.55f), new Color(0.13f, 0.23f, 0.45f), false);

        CreateBall();
        CreateUI();
        NextBatter();
        ShowMessage("甲子園への道 -Unity試作-", 1.2f, () =>
        {
            _phase = Phase.WaitPitch;
            SetHint("タップで投球！");
        });
    }

    private void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 5.4f; // 横向き16:9前提
        cam.transform.position = new Vector3(0.6f, 0.3f, -10f);
        cam.transform.rotation = Quaternion.identity;
        cam.backgroundColor = new Color(0.30f, 0.65f, 0.85f);
        cam.clearFlags = CameraClearFlags.SolidColor;
    }

    // ------------------------------------------------------------
    // 打者まわり
    // ------------------------------------------------------------
    private void NextBatter()
    {
        _balls = 0;
        _strikes = 0;
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

        if (_batter != null) Destroy(_batter.gameObject);
        _batter = RagdollPlayer.Create(transform, "Batter",
            new Vector2(BackgroundBuilder.BatterX, BackgroundBuilder.GroundTopY), -1f,
            _batterStats, uniform, accent, true);

        ResetBall();
        _pitcher.ResetPose();
        _phase = Phase.WaitPitch;
        SetHint("タップで投球！");
        UpdateNames();
        UpdateCount();
    }

    // ------------------------------------------------------------
    // ボール
    // ------------------------------------------------------------
    private void CreateBall()
    {
        _ball = new GameObject("Ball");
        _ball.layer = 9; // Ball レイヤー(TagManagerで定義)
        _ball.transform.SetParent(transform, false);
        _ballSr = _ball.AddComponent<SpriteRenderer>();
        _ballSr.sprite = SpriteFactory.Circle;
        _ballSr.color = Color.white;
        _ballSr.sortingOrder = 9;
        _ball.transform.localScale = new Vector3(0.18f, 0.18f, 1f);
        var col = _ball.AddComponent<CircleCollider2D>();
        col.radius = 0.5f; // スケール0.18と合わせて実寸0.09
        _ballRb = _ball.AddComponent<Rigidbody2D>();
        _ballRb.bodyType = RigidbodyType2D.Kinematic;
        _ballRb.gravityScale = 0f;
        ResetBall();
    }

    private void ResetBall()
    {
        if (_ballRb == null) return;
        _ballRb.bodyType = RigidbodyType2D.Kinematic;
        _ballRb.velocity = Vector2.zero;
        _ballRb.angularVelocity = 0f;
        _ballRb.gravityScale = 0f;
        _ball.transform.position = new Vector3(ReleasePoint.x, ReleasePoint.y, 0f);
        _ball.transform.rotation = Quaternion.identity;
        _ball.transform.localScale = new Vector3(0.18f, 0.18f, 1f);
    }

    // ------------------------------------------------------------
    // 入力と進行
    // ------------------------------------------------------------
    private void Update()
    {
        bool tap = Input.GetMouseButtonDown(0);
        if (!tap && Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began) tap = true;
        }

        switch (_phase)
        {
            case Phase.WaitPitch:
                if (tap) StartPitch();
                break;
            case Phase.PitchFlight:
                if (tap && !_hasSwing) RegisterSwing();
                AdvancePitchFlight();
                break;
            case Phase.BallFlight:
                if (_ballRb.bodyType == RigidbodyType2D.Dynamic &&
                    ((Time.time - _ballFlightStartTime > 0.15f &&
                      _ball.transform.position.y <= BackgroundBuilder.GroundTopY + 0.12f)
                     || (Time.time - _ballFlightStartTime > 0.4f && _ballRb.velocity.sqrMagnitude < 0.04f)
                     || Time.time >= _ballFlightDeadline))
                {
                    ApplyPendingResult();
                }
                break;
            case Phase.Message:
                if (Time.time >= _messageUntil)
                {
                    var action = _afterMessage;
                    _afterMessage = null;
                    _phase = Phase.WaitPitch; // 行き先はアクション側で上書きされる
                    if (action != null) action();
                }
                break;
        }
    }

    private void ShowMessage(string text, float seconds, System.Action after)
    {
        _messageText.text = text;
        _messageUntil = Time.time + seconds;
        _afterMessage = after;
        _phase = Phase.Message;
    }

    // ------------------------------------------------------------
    // 投球
    // ------------------------------------------------------------
    private void StartPitch()
    {
        // 必殺ゲージが満タンのまま次球を迎えたら、ここで消費扱いにして空に戻す
        // (必殺技の実装はフェーズ2)。
        if (_gauge >= SpecialMoves.GaugeMax)
        {
            _gauge = 0;
            if (_gaugeFill != null) _gaugeFill.fillAmount = 0f;
            if (_gaugeLabel != null) _gaugeLabel.text = "必殺ゲージ (フェーズ2で実装)";
        }

        _pitcher.ResetPose();
        _batter.ResetPose();
        ResetBall();

        _pitchType = PitchTable.Pick(_pitcherStats.Brk);
        _stuff = PitchTable.Stuff(_pitcherStats, _pitchType);
        float kmh = PitchTable.SpeedKmh(_pitcherStats, _pitchType);

        // 狙い(HTML版 mkPitch の cx/cy)。基本はゾーン内、時々外す。
        // 制球が高いほど散らばりが小さい。
        float scatter = 0.62f - _pitcherStats.Ctrl / 220f;
        _targetCx = Mathf.Clamp(Gauss() * 0.9f + Gauss() * scatter, -2.0f, 2.0f);
        _targetCy = Mathf.Clamp(Gauss() * 0.9f + Gauss() * scatter, -2.0f, 2.0f);
        if (Random.value < 0.25f) _targetCx *= 1.5f; // たまに大きく外す

        // 横視点への読み替え: HTMLの cy はそのまま高さ、cx は奥行きなので
        // 到達位置の横ずれ(小さめ)と変化の見た目に振り分ける。
        _flightStartPos = ReleasePoint;
        _flightEndPos = ZoneCenter + new Vector2(_targetCx * 0.10f, _targetCy * 0.28f)
                        + new Vector2(0f, _pitchType.BreakY * 0.10f);
        Vector2 mid = (_flightStartPos + _flightEndPos) * 0.5f;
        _flightCtrlPos = mid + new Vector2(_pitchType.BreakX * 0.06f, _pitchType.BreakY * 0.24f);

        // 到達時間。HTML版は実時間×2.3(見やすさ係数)。Unity版の画面距離に
        // 合わせて係数1.9に調整し、下限/上限でクランプする。
        float realT = 18.44f / (kmh / 3.6f);
        _flightDuration = Mathf.Clamp(realT * 1.9f, 0.75f, 1.35f);
        _flightStartTime = Time.time + 0.12f; // リリースまでの間
        _idealTime = _flightStartTime + _flightDuration - 0.06f; // HTML版の (T - 0.06) 相当
        _arrivalTime = _flightStartTime + _flightDuration;
        _flightDistance = Vector2.Distance(_flightStartPos, _flightEndPos);

        // スイング窓 W(ms)。HTML版 swingEval: 130 * (0.8 + meet/250)、
        // ゾーン外は狭く、球の威力(stuff)でも増減する。
        float zd0 = Mathf.Max(Mathf.Abs(_targetCx), Mathf.Abs(_targetCy));
        _windowMs = 130f * (0.8f + _batterStats.Meet / 250f);
        if (zd0 > 1f) _windowMs *= 1f - Mathf.Min(0.85f, (zd0 - 1f) * 0.95f);
        _windowMs *= 1f - (_stuff - 50f) / 400f;
        _windowMs = Mathf.Max(_windowMs, 40f);
        // 遅い側の窓(最大 +W ms)までは入力を受け付けるため、
        // 見逃しの確定は到達直後ではなく窓の終わりまで待つ。
        _resolveDeadline = _idealTime + _windowMs / 1000f;

        _hasSwing = false;
        _pitcher.DoPitchMotion();
        _messageText.text = "";
        SetHint(_pitchType.Name + " " + Mathf.RoundToInt(kmh) + "km/h … タイミングでタップ！");
        _phase = Phase.PitchFlight;
    }

    private void AdvancePitchFlight()
    {
        if (_phase != Phase.PitchFlight) return; // RegisterSwing内で解決済みの場合は何もしない
        float now = Time.time;
        if (now < _flightStartTime)
        {
            _ball.transform.position = new Vector3(_flightStartPos.x, _flightStartPos.y, 0f);
            return;
        }
        float u = (now - _flightStartTime) / _flightDuration;
        Vector2 p;
        if (u <= 1f)
        {
            p = Bezier(_flightStartPos, _flightCtrlPos, _flightEndPos, u);
        }
        else
        {
            // 到達後も「遅いタイミングのスイング」を受け付ける間、
            // ボールはキャッチャー方向へ進み続ける。
            Vector2 dir = _flightEndPos - _flightCtrlPos;
            dir.Normalize();
            float extra = (now - _arrivalTime) * (_flightDistance / _flightDuration);
            p = _flightEndPos + dir * extra;
        }
        _ball.transform.position = new Vector3(p.x, p.y, 0f);
        float s = Mathf.Lerp(0.75f, 1.05f, Mathf.Min(u, 1f)); // 遠近感の代わりに少し大きく
        _ball.transform.localScale = new Vector3(0.18f * s, 0.18f * s, 1f);

        if (_hasSwing)
        {
            // 早いスイングはボール到達の瞬間に判定する。
            if (now >= _arrivalTime) ResolvePitch();
        }
        else if (now >= _resolveDeadline)
        {
            ResolvePitch(); // 見逃し確定
        }
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private void RegisterSwing()
    {
        _hasSwing = true;
        _swingDms = (Time.time - _idealTime) * 1000f;
        _batter.DoSwing();
        // 既にボールが到達している(遅いタイミング)なら、その場で判定する。
        if (Time.time >= _arrivalTime) ResolvePitch();
    }

    // 投球の判定。HTML版 swingEval / pitchOnce の単純化移植。
    private void ResolvePitch()
    {
        float zd = Mathf.Max(Mathf.Abs(_targetCx), Mathf.Abs(_targetCy)); // ゾーンからの距離

        if (_hasSwing)
        {
            // スイング窓は StartPitch で算出済みの _windowMs を使う。
            float w = _windowMs;

            if (Mathf.Abs(_swingDms) <= w)
            {
                float t = _swingDms / w; // -1(早い) 〜 +1(遅い)
                // 芯の質 q。HTML版: clamp(1 - |t|*0.85 + (meet-50)/400, 0, 1)
                float q = Mathf.Clamp01(1f - Mathf.Abs(t) * 0.85f + (_batterStats.Meet - 50f) / 400f);
                if (Mathf.Abs(t) > 0.72f && Random.value < 0.65f)
                {
                    LaunchFoul();
                }
                else
                {
                    LaunchHit(q);
                }
            }
            else
            {
                AddStrike("空振り！");
            }
        }
        else
        {
            if (zd <= 1f)
            {
                AddGauge(SpecialMoves.GainStrike);
                AddStrike("見逃しストライク！");
            }
            else
            {
                AddBall("ボール");
            }
        }
    }

    // ------------------------------------------------------------
    // カウント処理
    // ------------------------------------------------------------
    private void AddStrike(string message)
    {
        _strikes++;
        UpdateCount();
        if (_strikes >= 3)
        {
            AddGauge(SpecialMoves.GainStrikeout);
            _outs++;
            UpdateCount();
            _batter.Flop(1.0f);
            ShowMessage("三振！", 1.4f, AfterAtBatEnd);
        }
        else
        {
            ShowMessage(message, 0.9f, PrepareNextPitch);
        }
    }

    private void AddBall(string message)
    {
        _balls++;
        UpdateCount();
        if (_balls >= 4)
        {
            ShowMessage("フォアボール！", 1.2f, AfterAtBatEnd);
        }
        else
        {
            ShowMessage(message, 0.9f, PrepareNextPitch);
        }
    }

    private void PrepareNextPitch()
    {
        ResetBall();
        _pitcher.ResetPose();
        _batter.ResetPose();
        _messageText.text = "";
        _phase = Phase.WaitPitch;
        SetHint("タップで投球！");
    }

    // 打席終了後: 3アウトなら回の処理、そうでなければ次の打者へ。
    private void AfterAtBatEnd()
    {
        if (_outs >= 3)
        {
            _outs = 0;
            _inning++;
            UpdateCount();
            ShowMessage("3アウト！ " + _inning + "回へ", 1.4f, NextBatter);
        }
        else
        {
            NextBatter();
        }
    }

    // ------------------------------------------------------------
    // 打球
    // ------------------------------------------------------------
    private void LaunchFoul()
    {
        _pendingResult = PendingResult.Foul;
        _pendingText = "ファウル";
        _ballRb.bodyType = RigidbodyType2D.Dynamic;
        _ballRb.gravityScale = 1f;
        _ballRb.velocity = new Vector2(2.4f, 4.4f); // バックネット方向へ
        _ballRb.angularVelocity = -260f;
        _ballFlightStartTime = Time.time;
        _ballFlightDeadline = Time.time + 1.6f;
        _phase = Phase.BallFlight;
    }

    private void LaunchHit(float q)
    {
        PlayerStats bt = _batterStats;
        // 初速係数 V。HTML版 mkHit: min(52, 18 + 28 * q^0.9 * (0.72 + power/150))
        float v = Mathf.Min(52f, 18f + 28f * Mathf.Pow(q, 0.9f) * (0.72f + bt.Power / 150f));
        // 打ち上げ角 L。HTML版: 13 + cy*9 ± 8、qが低いと極端な角度が混ざる。
        float launch = 13f + _targetCy * 9f + Random.Range(-8f, 8f);
        if (q < 0.4f) launch += (Random.value < 0.5f ? -1f : 1f) * (0.4f - q) / 0.4f * 22f;
        launch = Mathf.Clamp(launch, -5f, 55f);

        // 推定飛距離 D(m)。HTML版: V^2 * sin(2L) / 9.8 * 0.55
        float dist = v * v * Mathf.Sin(2f * launch * Mathf.Deg2Rad) / 9.8f * 0.55f;
        if (dist < 0f) dist = 0f; // 下向きのゴロでは負値になるため

        // 結果の決定(フェーズ1の簡易版: フェンス95m)
        PendingResult result;
        string text;
        if (dist >= 95f && launch >= 12f)
        {
            result = PendingResult.HomeRun; text = "ホームラン！";
        }
        else if (launch < 10f)
        {
            // ゴロ: アウト確率 0.78 - q*0.35
            if (Random.value < 0.78f - q * 0.35f) { result = PendingResult.Out; text = "ゴロ…アウト"; }
            else { result = PendingResult.Single; text = "ヒット！"; }
        }
        else
        {
            // フライ/ライナー: アウト確率 (1-q)*0.72
            if (Random.value < (1f - q) * 0.72f) { result = PendingResult.Out; text = "打ち上げた…アウト"; }
            else if (q >= 0.8f) { result = PendingResult.Double; text = "二塁打！"; }
            else { result = PendingResult.Single; text = "ヒット！"; }
        }
        _pendingResult = result;
        _pendingText = text + " (飛距離 約" + Mathf.RoundToInt(dist) + "m)";

        // 見た目: ボールを物理で飛ばす(左方向=外野)。
        float speed = v * 0.5f;
        float rad = launch * Mathf.Deg2Rad;
        _ballRb.bodyType = RigidbodyType2D.Dynamic;
        _ballRb.gravityScale = 1f;
        _ballRb.velocity = new Vector2(-Mathf.Cos(rad) * speed, Mathf.Sin(rad) * speed);
        _ballRb.angularVelocity = -300f;
        _ballFlightStartTime = Time.time;
        _ballFlightDeadline = Time.time + 2.4f;
        _phase = Phase.BallFlight;

        if (result == PendingResult.HomeRun) _pitcher.Flop(0.7f);
    }

    private void ApplyPendingResult()
    {
        // ボールは止めずに飛ばしっぱなしにする(次の PrepareNextPitch /
        // NextBatter で ResetBall が回収する)。空中で凍らせると不自然なため。
        switch (_pendingResult)
        {
            case PendingResult.Foul:
                if (_strikes < 2) _strikes++;
                UpdateCount();
                ShowMessage("ファウル", 0.8f, PrepareNextPitch);
                break;
            case PendingResult.Single:
            case PendingResult.Double:
                AddGauge(SpecialMoves.GainHit);
                ShowMessage(_pendingText, 1.3f, AfterAtBatEnd);
                break;
            case PendingResult.HomeRun:
                AddGauge(SpecialMoves.GainHomeRun);
                _runs++;
                UpdateCount();
                ShowMessage(_pendingText, 1.6f, AfterAtBatEnd);
                break;
            case PendingResult.Out:
                AddGauge(SpecialMoves.GainOut);
                _outs++;
                UpdateCount();
                ShowMessage(_pendingText, 1.3f, AfterAtBatEnd);
                break;
            default:
                PrepareNextPitch();
                break;
        }
        _pendingResult = PendingResult.None;
    }

    // ------------------------------------------------------------
    // 必殺ゲージ(フェーズ1は表示のみのプレースホルダ)
    // ------------------------------------------------------------
    private void AddGauge(int gain)
    {
        if (_gauge >= SpecialMoves.GaugeMax) return;
        _gauge = Mathf.Min(SpecialMoves.GaugeMax, _gauge + gain);
        if (_gaugeFill != null) _gaugeFill.fillAmount = _gauge / 100f;
        if (_gauge >= SpecialMoves.GaugeMax && _gaugeLabel != null)
        {
            // フェーズ2で必殺技に接続する。今は満タンを表示し続け、
            // 次の投球開始時(StartPitch)にリセットする。
            _gaugeLabel.text = "必殺ゲージMAX！(必殺技はフェーズ2で実装)";
        }
    }

    // ------------------------------------------------------------
    // UI
    // ------------------------------------------------------------
    private void CreateUI()
    {
        var canvasGo = new GameObject("UI");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960f, 540f);
        scaler.matchWidthOrHeight = 0.5f;

        Font font = GetFont();

        // 上部バー背景
        MakePanel(canvasGo.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -46f), new Vector2(0f, 0f), new Color(0f, 0f, 0f, 0.35f));
        _countText = MakeText(canvasGo.transform, font, "Count",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(420f, 38f),
            22, TextAnchor.MiddleLeft, Color.white);
        _namesText = MakeText(canvasGo.transform, font, "Names",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -8f), new Vector2(520f, 38f),
            15, TextAnchor.MiddleRight, Color.white);

        // 中央メッセージ
        _messageText = MakeText(canvasGo.transform, font, "Message",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(900f, 70f),
            38, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.3f));

        // 下部ヒント
        _hintText = MakeText(canvasGo.transform, font, "Hint",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(900f, 34f),
            19, TextAnchor.MiddleCenter, Color.white);

        // 必殺ゲージ(プレースホルダ)
        _gaugeLabel = MakeText(canvasGo.transform, font, "GaugeLabel",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 34f), new Vector2(360f, 24f),
            14, TextAnchor.MiddleLeft, new Color(1f, 0.8f, 0.5f));
        _gaugeLabel.text = "必殺ゲージ (フェーズ2で実装)";
        var gaugeBg = MakePanel(canvasGo.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(16f, 8f), new Vector2(376f, 30f), new Color(0f, 0f, 0f, 0.45f));
        var fillGo = new GameObject("GaugeFill");
        fillGo.transform.SetParent(gaugeBg.transform, false);
        _gaugeFill = fillGo.AddComponent<Image>();
        _gaugeFill.sprite = SpriteFactory.White;
        _gaugeFill.color = new Color(1f, 0.55f, 0.15f);
        _gaugeFill.type = Image.Type.Filled;
        _gaugeFill.fillMethod = Image.FillMethod.Horizontal;
        _gaugeFill.fillOrigin = 0;
        _gaugeFill.fillAmount = 0f;
        var frt = fillGo.GetComponent<RectTransform>();
        frt.anchorMin = new Vector2(0f, 0f);
        frt.anchorMax = new Vector2(1f, 1f);
        frt.offsetMin = new Vector2(3f, 3f);
        frt.offsetMax = new Vector2(-3f, -3f);

        UpdateCount();
    }

    private static Font GetFont()
    {
        // Unity 2022.3 の組み込みフォント。名前が変わった経緯があるため両方試す。
        try
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f != null) return f;
        }
        catch (System.Exception) { /* 次を試す */ }
        try
        {
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    private Text MakeText(Transform parent, Font font, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size,
        int fontSize, TextAnchor align, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private GameObject MakePanel(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        var img = go.AddComponent<Image>();
        img.sprite = SpriteFactory.White;
        img.color = color;
        return go;
    }

    private void SetHint(string text)
    {
        if (_hintText != null) _hintText.text = text;
    }

    private void UpdateCount()
    {
        if (_countText != null)
            _countText.text = _inning + "回  B " + _balls + "  S " + _strikes + "  O " + _outs + "  得点 " + _runs;
    }

    private void UpdateNames()
    {
        if (_namesText == null) return;
        _namesText.text =
            "投手 " + _pitcherStats.Name + "(" + SpeciesTable.DisplayName(_pitcherStats.Species) +
            " 球速" + _pitcherStats.Velo + " 変化" + _pitcherStats.Brk + ")  vs  打者 " +
            _batterStats.Name + "(" + SpeciesTable.DisplayName(_batterStats.Species) +
            " ミート" + _batterStats.Meet + " パワー" + _batterStats.Power + ")";
    }

    // -1〜1 の疑似ガウス乱数(3つの一様乱数の平均)。狙いの散らばし用。
    private static float Gauss()
    {
        return (Random.Range(-1f, 1f) + Random.Range(-1f, 1f) + Random.Range(-1f, 1f)) / 3f;
    }
}
