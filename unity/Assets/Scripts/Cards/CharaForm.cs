using UnityEngine;

// ============================================================
// CharaForm.cs
// 選手ごとのフォーム(打つ・投げる・ぐにゃ度)。編成の「フォーム」で作り、試合ではこのとおりに体(物理)で動く。
// 値はどれも 0〜10 の11段階。見た目だけで、打撃・投球の判定や能力には使わない。
// 保存は PlayerPrefs(キャラごと)。保存がない選手は従来どおり決めポーズの絵で動く。
// ============================================================
[System.Serializable]
public class CharaForm
{
    public const int Max = 10;

    // ---- 打つ ----
    public int BatHold = 6;     // バットの構え: 0=寝かせる … 10=立てる
    public int Crouch = 3;      // 腰の落とし: 0=棒立ち … 10=深く
    public int LegKick = 3;     // 足の上げ: 0=すり足 … 10=一本足
    public int SwingPlane = 5;  // 振りの軌道: 0=ダウン … 5=レベル … 10=アッパー
    public int SwingSize = 5;   // 振りの大きさ: 0=コンパクト … 10=フルスイング
    // ---- 投げる ----
    public int ArmSlot = 8;     // 腕の角度: 0=アンダー … 5=サイド … 10=オーバー
    public int PLegKick = 5;    // 足の上げ
    public int Lean = 4;        // 前のめり
    public int ArmSwing = 5;    // 腕の振り: 0=小さく … 10=大きく速く
    // ---- 共通 ----
    public int Gunya = 5;       // ぐにゃ度: 0=しゃきっ … 5=ふつう … 10=ぐにゃぐにゃ

    public static float U(int v) { return Mathf.Clamp(v, 0, Max) / (float)Max; }

    // ぐにゃ度の係数(5=1.0。0=0.25、10=2.0)
    public float GunyaScale
    {
        get
        {
            float u = U(Gunya);
            return u <= 0.5f ? Mathf.Lerp(0.25f, 1f, u * 2f) : Mathf.Lerp(1f, 2f, (u - 0.5f) * 2f);
        }
    }

    public CharaForm Clone() { return (CharaForm)MemberwiseClone(); }

    // ---- 保存 ----
    private const string Prefix = "gunya.form.";

    public static bool Has(string charaId)
    {
        return !string.IsNullOrEmpty(charaId) && PlayerPrefs.HasKey(Prefix + charaId);
    }

    // 保存してあるフォーム。なければ null
    public static CharaForm Load(string charaId)
    {
        if (!Has(charaId)) return null;
        try
        {
            var f = JsonUtility.FromJson<CharaForm>(PlayerPrefs.GetString(Prefix + charaId));
            return f;
        }
        catch { return null; }
    }

    public static void Save(string charaId, CharaForm f)
    {
        if (string.IsNullOrEmpty(charaId) || f == null) return;
        PlayerPrefs.SetString(Prefix + charaId, JsonUtility.ToJson(f));
        PlayerPrefs.Save();
    }

    public static void Clear(string charaId)
    {
        if (string.IsNullOrEmpty(charaId)) return;
        PlayerPrefs.DeleteKey(Prefix + charaId);
        PlayerPrefs.Save();
    }
}
