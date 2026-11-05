using UnityEngine;

// 画面全体の仕上げ(自前のシェーダー Resources/Shaders/GunyaPost)。
// ブルーム(照明や打球の光がにじむ) / 明るいところを丸める / 色味(夜の青い影と暖かい光) / コントラスト / 彩度 / ビネット。
// UI(画面に重ねる表示)にはかからない。
[RequireComponent(typeof(Camera))]
public class PostFX : MonoBehaviour
{
    public static bool Enabled = true;

    public float Threshold = 1.1f, Knee = 0.25f, Intensity = 0.7f;
    public float Exposure = 1.04f, Shoulder = 0.78f;
    public float Contrast = 1.10f, Saturation = 1.12f, Vignette = 0.32f;
    public Color Lift = new Color(0.010f, 0.022f, 0.055f);     // 影を少し青く
    public Color Gain = new Color(1.04f, 1.0f, 0.95f);         // 光を少し暖かく
    public Color VigColor = new Color(0.01f, 0.015f, 0.04f);

    private Material _mat;
    private const int Levels = 5;
    private readonly RenderTexture[] _down = new RenderTexture[Levels];

    public static PostFX Attach(Camera cam)
    {
        if (cam == null) return null;
        var fx = cam.GetComponent<PostFX>();
        if (fx == null) fx = cam.gameObject.AddComponent<PostFX>();
        cam.allowHDR = true;
        cam.allowMSAA = true;
        return fx;
    }

    private void OnEnable()
    {
        var sh = Resources.Load<Shader>("Shaders/GunyaPost");
        if (sh != null && sh.isSupported) { _mat = new Material(sh); _mat.hideFlags = HideFlags.HideAndDontSave; }
    }

    private void OnDisable() { if (_mat != null) Destroy(_mat); _mat = null; }

    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (!Enabled || _mat == null) { Graphics.Blit(src, dst); return; }
        int q = GraphicsQuality.Level;
        _mat.SetVector("_Threshold", new Vector4(Threshold, Knee, 6f, 0f));
        _mat.SetFloat("_Intensity", Intensity);
        _mat.SetFloat("_Exposure", Exposure);
        _mat.SetFloat("_Shoulder", Shoulder);
        _mat.SetFloat("_Contrast", Contrast);
        _mat.SetFloat("_Saturation", Saturation);
        _mat.SetFloat("_Vignette", Vignette);
        _mat.SetColor("_Lift", Lift);
        _mat.SetColor("_Gain", Gain);
        _mat.SetColor("_VigColor", VigColor);
        if (QualitySettings.activeColorSpace == ColorSpace.Gamma) _mat.EnableKeyword("UNITY_COLORSPACE_GAMMA");
        else _mat.DisableKeyword("UNITY_COLORSPACE_GAMMA");

        if (q == 0)
        {
            // 低画質: にじみなし、色調・ビネットだけ(1回で済む)
            _mat.SetTexture("_BloomTex", Texture2D.blackTexture);
            Graphics.Blit(src, dst, _mat, 3);
            return;
        }
        var fmt = src.format;
        int div = q == 2 ? 2 : 4;
        int maxLevels = q == 2 ? Levels : 3;
        int w = src.width / div, h = src.height / div;
        int n = 0;
        RenderTexture cur = RenderTexture.GetTemporary(w, h, 0, fmt);
        cur.filterMode = FilterMode.Bilinear;
        Graphics.Blit(src, cur, _mat, 0);
        _down[n++] = cur;
        for (; n < maxLevels; n++)
        {
            w = Mathf.Max(1, w / 2); h = Mathf.Max(1, h / 2);
            if (w < 4 || h < 4) break;
            var next = RenderTexture.GetTemporary(w, h, 0, fmt);
            next.filterMode = FilterMode.Bilinear;
            Graphics.Blit(cur, next, _mat, 1);
            _down[n] = next;
            cur = next;
        }
        for (int i = n - 2; i >= 0; i--)
        {
            Graphics.Blit(cur, _down[i], _mat, 2);   // 1段上へ足し込む
            cur = _down[i];
        }
        _mat.SetTexture("_BloomTex", cur);
        Graphics.Blit(src, dst, _mat, 3);
        for (int i = 0; i < n; i++) { RenderTexture.ReleaseTemporary(_down[i]); _down[i] = null; }
    }
}
