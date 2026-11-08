using System.Runtime.InteropServices;

// ============================================================
// WebPrompt.cs
// ブラウザ版では、名前や番号をブラウザ標準の入力ダイアログで入れる(iPhone の Safari でも確実にキーボードが出る)。
// エディタ・アプリ版では使わない(Available=false)。
// ============================================================
public static class WebPrompt
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern string WebPromptJs(string title, string def);
    public static bool Available { get { return true; } }
#else
    public static bool Available { get { return false; } }
#endif

    // 入力された文字(キャンセルなら null)
    public static string Ask(string title, string def)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string r = WebPromptJs(title, def ?? "");
        if (r != null && r.Length > 0 && r[0] == '\u001b') return null;   // キャンセル
        return r;
#else
        return null;
#endif
    }
}
