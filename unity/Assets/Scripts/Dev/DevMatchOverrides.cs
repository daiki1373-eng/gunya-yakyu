#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Claudeの再生確認用。通常設定を書き換えず、一時上書きを終了時に必ず戻す。
[InitializeOnLoad]
public static class DevMatchOverrides
{
    private static bool _active, _autoPlay, _qualityAuto;
    private static int _freeze, _quality;
    private static float _timeScale;

    static DevMatchOverrides()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        AssemblyReloadEvents.beforeAssemblyReload += End;
    }

    public static void Begin(bool autoPlay, int freezeKind, int quality)
    {
        if (!Application.isPlaying) return;
        if (!_active)
        {
            _autoPlay = AtBatController.AutoPlay;
            _freeze = AtBatController.FreezeKind;
            _quality = GraphicsQuality.Level;
            _qualityAuto = GraphicsQuality.Auto;
            _timeScale = Time.timeScale;
            _active = true;
        }
        AtBatController.AutoPlay = autoPlay;
        AtBatController.FreezeKind = freezeKind;
        GraphicsQuality.Auto = false;
        GraphicsQuality.Set(quality);
        Time.timeScale = 1f;
    }

    [MenuItem("Gunya/Verification/Auto Play (High)")]
    private static void AutoHigh() { Begin(true, -99, 2); }

    [MenuItem("Gunya/Verification/Restore Settings")]
    public static void End()
    {
        if (!_active) return;
        _active = false;
        AtBatController.AutoPlay = _autoPlay;
        AtBatController.FreezeKind = _freeze;
        GraphicsQuality.Set(_quality);
        GraphicsQuality.Auto = _qualityAuto;
        Time.timeScale = _timeScale;
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode) End();
    }
}
#endif
