using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
// FormPreview.cs
// フォーム作成画面の「動いて見せる」選手。別の物理シーン(自分で進める)に1人だけ置き、
// 専用のカメラで RenderTexture に描く。試合中にメニューを開いて時間が止まっていても動く。
// 打者: 構え → 足上げ → スイング をくり返す。投手: セット → 投球 をくり返す。
// ============================================================
public class FormPreview : MonoBehaviour
{
    private const float X = 1000f;              // 球場から遠く離れた場所に置く
    private const float Step = 1f / 50f;

    public RenderTexture Texture { get; private set; }

    private Scene _scene;
    private PhysicsScene2D _phys;
    private Camera _cam;
    private RagdollPlayer _p;
    private bool _pitcher;
    private float _t, _acc;
    private bool _acted;

    public static FormPreview Create(Transform parent)
    {
        var go = new GameObject("FormPreview");
        go.transform.SetParent(parent, false);
        return go.AddComponent<FormPreview>();
    }

    private void EnsureWorld()
    {
        if (_cam != null) return;
        _scene = SceneManager.CreateScene("FormPreview", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        _phys = _scene.GetPhysicsScene2D();
        var ground = new GameObject("PreviewGround");
        ground.transform.position = new Vector3(X, -0.5f, 0f);
        ground.AddComponent<BoxCollider2D>().size = new Vector2(30f, 1f);
        SceneManager.MoveGameObjectToScene(ground, _scene);

        Texture = new RenderTexture(480, 560, 16);
        Texture.name = "FormPreviewRT";
        var cg = new GameObject("PreviewCamera");
        _cam = cg.AddComponent<Camera>();
        _cam.orthographic = true;
        _cam.orthographicSize = 1.65f;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.05f, 0.07f, 0.18f, 1f);
        _cam.nearClipPlane = 0.1f; _cam.farClipPlane = 30f;
        _cam.targetTexture = Texture;
        cg.transform.position = new Vector3(X, 1.25f, -10f);
        SceneManager.MoveGameObjectToScene(cg, _scene);
        _cam.enabled = false;
    }

    // 選手を出し直す(フォームを変えたら同じ選手で Apply)
    public void Show(CharaDef d, CharaForm form, bool pitcher)
    {
        EnsureWorld();
        Clear();
        if (d == null) return;
        _pitcher = pitcher;
        var stats = CharaRoster.Stats(d);
        _p = RagdollPlayer.Create(null, "Preview_" + d.Id, new Vector2(X, 0f), 1f, stats,
            BattingConfig.UserJersey, BattingConfig.UserAccent, !pitcher);
        _p.Isolate();
        SceneManager.MoveGameObjectToScene(_p.gameObject, _scene);
        _p.enabled = false; _p.enabled = true;   // 自分の体どうしの衝突無効を張り直す
        Apply(form);
        _cam.enabled = true;
    }

    public void Apply(CharaForm form)
    {
        if (_p == null) return;
        _p.SetForm(form != null ? form.Clone() : new CharaForm());
        Restart();
    }

    private void Restart()
    {
        _t = 0f; _acc = 0f; _acted = false;
        _p.ResetPose();
    }

    public void Hide()
    {
        Clear();
        if (_cam != null) _cam.enabled = false;
    }

    private void Clear()
    {
        if (_p != null) Destroy(_p.gameObject);
        _p = null;
    }

    private void Update()
    {
        if (_p == null) return;
        _acc += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        while (_acc >= Step)
        {
            _acc -= Step;
            _t += Step;
            if (_pitcher)
            {
                const float start = 0.35f;
                if (!_acted && _t >= start) { _acted = true; _p.DoPitchMotion(); }
                if (_t >= start + BattingConfig.WindupSeconds + 1.3f) Restart();
            }
            else
            {
                float swingAt = RagdollPlayer.KickPeak + 0.22f;
                if (!_acted && _t >= swingAt) { _acted = true; _p.DoSwing(); }
                if (_t >= swingAt + 1.5f) Restart();
            }
            _p.Step(Step);
            _phys.Simulate(Step);
        }
    }

    private void OnDestroy()
    {
        if (Texture != null) Texture.Release();
    }
}
