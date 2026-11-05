#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class EditModeRunner
{
    [MenuItem("ぐにゃ野球/検証/EditMode ロジックテスト")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("EditModeで実行してください。");
            return;
        }
        int count = RegressionChecks.RunAll(Debug.Log);
        Debug.Log("EditMode: " + count + " checks passed (保存値・乱数は復元済み)");
    }
}
#endif
