using System.Collections.Generic;
using UnityEngine;

// ============================================================
// CharacterIntro.cs
// 打席に入る前・マウンドに立つ前の「登場演出」の設定。
// キャラごとの違いはすべて IntroDef に入れてあり、IntroCatalog の表に
// 名前で登録すれば差し替わる(未登録のキャラは標準の演出)。
// ジングルは自作の短い音の並び(半音、0=ド)。キャッチコピーも自作。
// ============================================================
public enum IntroPose
{
    None,
    BatPoint,   // バットでスタンドを指す
    Flex,       // 両腕を上げてガッツポーズ
    Bow,        // おじぎ
    Jump,       // その場でジャンプ
    Spin,       // ぐにゃっと一回転(派手)
    Wave        // 手を振る
}

public class IntroDef
{
    public string Copy = "";                 // キャッチコピー(短く)
    public IntroPose Pose = IntroPose.None;
    public Expr Face = Expr.Focus;           // 登場中の表情
    public int[] Jingle = { 0, 4, 7 };       // 半音。-99=休符
    public float NoteSec = 0.11f;            // 1音の長さ
    public float WalkFrom = 2.6f;            // 何m手前から歩いて入るか(0なら歩かない)
    public float Cheer = 0.5f;               // 歓声の大きさ 0..1
    public Color CardColor = new Color(0.80f, 0.14f, 0.14f);
    public float Hold = 1.1f;                // ポーズ後に見せる時間(秒)
}

public static class IntroCatalog
{
    // ---- キャラ別の登場演出(ここに足せば差し替わる) ----
    private static readonly Dictionary<string, IntroDef> Table = new Dictionary<string, IntroDef>
    {
        { "カブト丸", new IntroDef {
            Copy = "角で打つ男", Pose = IntroPose.BatPoint, Face = Expr.Focus,
            Jingle = new[] { 0, 4, 7, 12, -99, 7, 12, 16 }, NoteSec = 0.11f, Cheer = 0.8f,
            CardColor = new Color(0.55f, 0.30f, 0.12f) } },
        { "ワンタ", new IntroDef {
            Copy = "しっぽで走る切り込み隊長", Pose = IntroPose.Jump, Face = Expr.Happy,
            Jingle = new[] { 7, 9, 7, 4, 7, 12 }, NoteSec = 0.09f, Cheer = 0.6f, WalkFrom = 3.2f,
            CardColor = new Color(0.85f, 0.60f, 0.12f) } },
        { "主人公", new IntroDef {
            Copy = "まっすぐ育ち中", Pose = IntroPose.Bow, Face = Expr.Normal,
            Jingle = new[] { 0, 2, 4, 7, 4, 7, 12 }, NoteSec = 0.10f, Cheer = 0.45f,
            CardColor = new Color(0.20f, 0.40f, 0.85f) } },
        { "パン太", new IntroDef {
            Copy = "ぐにゃっと曲がるエース", Pose = IntroPose.Spin, Face = Expr.Happy,
            Jingle = new[] { 12, 7, 4, 0, 4, 7, 12, 12 }, NoteSec = 0.10f, Cheer = 0.9f, WalkFrom = 3.0f,
            CardColor = new Color(0.15f, 0.15f, 0.18f) } },
        { "ライバルA", new IntroDef {
            Copy = "", Pose = IntroPose.Flex, Face = Expr.Focus,
            Jingle = new[] { 0, 3, 7, 3, 0 }, NoteSec = 0.12f, Cheer = 0.3f } },
        { "ライバルB", new IntroDef {
            Copy = "", Pose = IntroPose.Wave, Face = Expr.Normal,
            Jingle = new[] { 0, 3, 5, 7 }, NoteSec = 0.11f, Cheer = 0.3f } },
        { "ライバルC", new IntroDef {
            Copy = "", Pose = IntroPose.BatPoint, Face = Expr.Focus,
            Jingle = new[] { -5, 0, 3, 7, 12 }, NoteSec = 0.12f, Cheer = 0.35f } },
    };

    public static IntroDef For(PlayerStats p, bool userTeam)
    {
        IntroDef d;
        if (p != null && Table.TryGetValue(p.Name, out d)) return d;
        // 未登録キャラの標準
        return new IntroDef
        {
            Pose = IntroPose.None,
            CardColor = userTeam ? BattingConfig.UserAccent : BattingConfig.OppJersey,
            Cheer = userTeam ? 0.5f : 0.25f
        };
    }

    public static string PositionName(int pos)
    {
        switch (pos)
        {
            case 1: return "投手";
            case 2: return "捕手";
            case 3: return "一塁手";
            case 4: return "二塁手";
            case 5: return "三塁手";
            case 6: return "遊撃手";
            case 7: return "左翼手";
            case 8: return "中堅手";
            case 9: return "右翼手";
            default: return "";
        }
    }
}
