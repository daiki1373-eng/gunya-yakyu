using UnityEngine;

// 画質の段階(2=高 1=中 0=低)。スマホは中から始め、動きが重いと自動で1段ずつ下げる(上げ直しはしない)。
// 高: アンチエイリアス4倍・光のにじみ(フル) / 中: アンチエイリアス2倍・にじみを軽く / 低: アンチエイリアスなし・にじみなし(色調だけ)・遠くの選手の更新を間引く
public class GraphicsQuality : MonoBehaviour
{
    public static int Level { get; private set; }
    public static bool Auto = true;
    public static float Fps { get; private set; }

    private float _avg = 1f / 60f, _slowFor, _settle;

    public static void Init()
    {
        if (FindObjectOfType<GraphicsQuality>() != null) return;
        var go = new GameObject("GraphicsQuality");
        DontDestroyOnLoad(go);
        go.AddComponent<GraphicsQuality>();
        Set(Application.isMobilePlatform ? 1 : 2);
    }

    public static void Set(int level)
    {
        Level = Mathf.Clamp(level, 0, 2);
        QualitySettings.antiAliasing = Level == 2 ? (Application.isMobilePlatform ? 2 : 4) : (Level == 1 ? 2 : 0);
        QualitySettings.anisotropicFiltering = Level == 0 ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f || dt > 0.5f) return;   // 一時停止・読み込み中は数えない
        _avg = Mathf.Lerp(_avg, dt, 0.05f);
        Fps = 1f / Mathf.Max(1e-4f, _avg);
        _settle -= dt;
        if (!Auto || Level == 0 || _settle > 0f) { _slowFor = 0f; return; }
        // 40fps を下回る状態が4秒続いたら1段下げる
        if (Fps < 40f) _slowFor += dt; else _slowFor = Mathf.Max(0f, _slowFor - dt * 2f);
        if (_slowFor > 4f)
        {
            Set(Level - 1);
            _slowFor = 0f;
            _settle = 5f;
        }
    }
}
