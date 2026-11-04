using UnityEngine;

// ============================================================
// BattingConfig.cs
// 打席システム(パワプロ式)の数値をここに集約する。すべて自前の仮置き値。
// 他社ゲームの数値は一切使っていない。調整はこのファイルだけで済むようにしてある。
//
// 座標系: 原点=ホームプレート / +x=一塁側(捕手から見て右) / +y=上 / +z=投手方向。
// 1ユニット=1m換算だが、見やすさのためゾーン・ボールは誇張サイズ。
// ============================================================
public static class BattingConfig
{
    // ---- ぐにゃ度 -------------------------------------------------
    // 関節の柔らかさ・倒れやすさの全体係数。1.0=標準、大きいほど誇張。
    // P1ではスイング空振り・三振・本塁打時のぐにゃ演出の強さにだけ使う。
    // (P2でスイング・捕球・送球・走塁の全てに掛ける)
    public static float GunyaLevel = 1.0f;

    // ---- ストライクゾーン(ホームプレート上の面 z=0) ---------------
    public const float ZoneHalfW = 0.45f;
    public const float ZoneBottom = 0.65f;
    public const float ZoneTop = 1.55f;
    public const float ZoneCenterY = 1.10f;
    public const float ZoneHalfH = 0.45f;
    public const float CursorMargin = 0.40f;   // カーソルがゾーン外へ出られる余白
    public const float BallRadius = 0.075f;

    // ---- 配置 -----------------------------------------------------
    public const float MoundZ = 14f;           // 投手までの距離(実際の18.44mより圧縮)
    public const float BatterX = 1.30f;        // 打者が立つ位置(中心からの距離)
    public static readonly Vector3 ReleasePoint = new Vector3(0.30f, 1.85f, 13.4f);

    // ---- カメラ(捕手後方視点) ------------------------------------
    // 中継カメラ風: 少し高く・後ろから、望遠ぎみに。ゾーンの見かけの大きさは従来とほぼ同じ。
    public static readonly Vector3 CameraPos = new Vector3(0f, 1.85f, -6.2f);
    public const float CameraPitchDeg = 4.5f;
    public const float CameraFov = 28f;

    // ---- 球速表示と到達時間のマッピング ----------------------------
    // 表示は130〜155km/h、実際の到達時間は遊びやすい0.65〜0.45秒。
    public const float KmhMin = 130f;
    public const float KmhMax = 155f;
    public const float FlightSlow = 0.65f;
    public const float FlightFast = 0.45f;
    public const float WindupSeconds = 0.90f;  // 投球モーション開始〜リリース

    // ---- スイングと判定 -------------------------------------------
    public const float SwingLag = 0.10f;       // 入力からバットがプレート面に届くまで
    // タイミング窓(ms)。ボール到達とバット到達のズレの絶対値。
    public const float TimingPerfectMs = 35f;
    public const float TimingGoodMs = 70f;
    public const float TimingNormalMs = 110f;
    public const float TimingWeakMs = 150f;
    // 芯からの距離しきい値(カーソル楕円で正規化。1.0=カーソルの縁)。
    public const float EllipsePerfect = 0.22f;
    public const float EllipseGood = 0.50f;
    public const float EllipseNormal = 0.78f;
    public const float EllipseWeak = 1.00f;

    // ---- ミートカーソル --------------------------------------------
    public const float CursorHalfWMin = 0.18f; // ミート5のとき
    public const float CursorHalfWMax = 0.34f; // ミート99のとき
    public const float CursorAspect = 0.70f;   // 縦/横(バットは横長)。操作性確認で 0.62→0.70
    public const float CursorSpeed = 2.6f;     // カーソルの最大移動速度(ユニット/秒)
    // 狙いのアシスト(0=なし)。人の狙いのぶれを少しだけ補う。操作性確認の結果から入れた仮置き値。
    public static float AimAssist = 0.45f;
    public const float AimAssistRange = 2.4f;  // カーソル楕円の何倍の距離まで効かせるか

    // ---- 打球 -----------------------------------------------------
    public const float ExitBase = 14f;         // 初速の下限(m/s)
    public static float ExitRange = 40f;       // q=1,パワー補正1.0 で上乗せされる量
    public const float LaunchBase = 16f;       // 芯に当たったときの基準打出角(度)
    public const float LaunchPerDy = 30f;      // 上下ずれ1.0あたりの角度変化(度)
    public const float SprayMax = 62f;         // タイミング最大ズレでの打球方向(度)

    // ---- チームカラー(自作) ------------------------------------------
    // 自チーム=ホーム(白地に赤)、相手=ビジター(紺地)。
    public static readonly Color UserJersey = new Color(0.95f, 0.95f, 0.96f);
    public static readonly Color UserAccent = new Color(0.80f, 0.14f, 0.14f);
    public static readonly Color OppJersey = new Color(0.20f, 0.28f, 0.55f);
    public static readonly Color OppAccent = new Color(0.10f, 0.14f, 0.32f);

    // ---- 球場 -----------------------------------------------------
    public static float FenceLine = 92f;       // 両翼
    public static float FenceCenterExtra = 20f; // 中堅は両翼+20m
    public const float FenceHeight = 4.0f;

    public static float FenceRadius(float sprayDegFromCenter)
    {
        float a = Mathf.Clamp(Mathf.Abs(sprayDegFromCenter), 0f, 45f);
        return FenceLine + FenceCenterExtra * Mathf.Cos(a * 2f * Mathf.Deg2Rad);
    }
}
