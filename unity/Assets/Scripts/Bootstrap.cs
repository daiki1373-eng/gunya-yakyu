using UnityEngine;

// ============================================================
// Bootstrap.cs
// シーンに何も置かなくてもゲームが始まるようにするための起動コード。
// 空シーンで再生しても、ここが GameController を生成し、
// 物理の基本設定(重力・ラグドール同士の衝突無効化の前提)を整える。
// (ProjectSettings の Physics2DSettings でも重力は設定済みだが、
//  保険としてここでも明示する)
// ============================================================
public static class Bootstrap
{
    public static bool UseLegacySideView = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        Physics2D.gravity = new Vector2(0f, -9.81f);
        // スイング等の大きな角速度(最大1500度/秒)が既定の上限360に
        // クランプされないようにする(Physics2DSettings側でも設定済み)。
        Physics2D.maxRotationSpeed = 1800f;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        // 既定は新しい打席システム(捕手後方視点・パワプロ式)。
        // 旧・横視点の試作(GameController)に戻すには UseLegacySideView を true にする。
        if (UseLegacySideView)
        {
            if (Object.FindObjectOfType<GameController>() == null)
            {
                var go = new GameObject("GameController");
                go.AddComponent<GameController>();
            }
        }
        else if (Object.FindObjectOfType<AtBatController>() == null)
        {
            // ホーム / ガチャ / カード一覧の外枠(試合はホームの「試合」で始まる)
            if (Object.FindObjectOfType<AppShell>() == null) AppShell.Create();
            var go = new GameObject("AtBatController");
            go.AddComponent<AtBatController>();
        }
    }
}
