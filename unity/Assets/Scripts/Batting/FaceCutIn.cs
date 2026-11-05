using UnityEngine;
using UnityEngine.UI;

// 表情が大きく変わった瞬間に、顔のアップを画面の横に差し込む(カットイン)。
// 打者は右上、投手は左上(選手本人に重ならない側)。別カメラで選手だけを写し、表情の色の背景+金枠で見せる。文字なし。
public class FaceCutIn : MonoBehaviour
{
    private class Slot
    {
        public RectTransform Root, Rays;
        public Image Bg;
        public RawImage Pic;
        public Camera Cam;
        public RenderTexture Rt;
        public RagdollPlayer Who;
        public float T, Dur;
        public float Side;     // -1 左 / +1 右
        public bool On;
    }

    private AtBatController _ctl;
    private Slot _left, _right;
    private float _coolL, _coolR;
    private const float Size = 150f;

    public static FaceCutIn Create(AtBatController ctl)
    {
        var go = new GameObject("FaceCutIn");
        go.transform.SetParent(ctl.transform, false);
        var f = go.AddComponent<FaceCutIn>();
        f._ctl = ctl;
        f.Build();
        return f;
    }

    private void OnEnable() { RagdollPlayer.ExpressionChanged += OnExpr; }
    private void OnDisable() { RagdollPlayer.ExpressionChanged -= OnExpr; }

    private void Build()
    {
        var cgo = new GameObject("FaceCutInCanvas");
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;   // 試合の表示より上、メニュー(50)より下
        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(960f, 540f);
        sc.matchWidthOrHeight = 1f;
        _left = MakeSlot(cgo.transform, -1f);
        _right = MakeSlot(cgo.transform, 1f);
    }

    private Slot MakeSlot(Transform parent, float side)
    {
        var s = new Slot { Side = side };
        var anchor = new Vector2(side < 0f ? 0f : 1f, 0.5f);
        s.Root = UiKit.Rect(parent, side < 0f ? "CutInL" : "CutInR");
        UiKit.Place(s.Root, anchor, new Vector2(side < 0f ? 0f : 1f, 0.5f), new Vector2(-side * 14f, 5f), new Vector2(Size, Size));
        var mask = UiKit.Img(s.Root, "Mask", null, Color.white);
        UiKit.Fill(mask.rectTransform, 4f);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        s.Bg = mask;
        s.Bg.sprite = UiKit.BgRadial;
        var rays = UiKit.Img(mask.transform, "Rays", UiKit.Rays, new Color(1f, 1f, 1f, 0.35f));
        s.Rays = rays.rectTransform;
        UiKit.Place(s.Rays, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Size * 2f, Size * 2f));
        s.Pic = new GameObject("Face", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        s.Pic.transform.SetParent(mask.transform, false);
        s.Pic.raycastTarget = false;
        UiKit.Fill(s.Pic.rectTransform, 0f);
        var frame = UiKit.Img(s.Root, "Frame", UiKit.Frame, Color.white);
        frame.type = Image.Type.Sliced;
        UiKit.Fill(frame.rectTransform, 0f);
        s.Rt = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
        s.Pic.texture = s.Rt;
        var camGo = new GameObject(side < 0f ? "CutInCamL" : "CutInCamR");
        camGo.transform.SetParent(transform, false);
        s.Cam = camGo.AddComponent<Camera>();
        s.Cam.orthographic = true;
        s.Cam.clearFlags = CameraClearFlags.SolidColor;
        s.Cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        s.Cam.cullingMask = 1 << RagdollPlayer.RagdollLayer;
        s.Cam.nearClipPlane = 0.1f; s.Cam.farClipPlane = 6f;
        s.Cam.targetTexture = s.Rt;
        s.Cam.enabled = false;
        s.Root.gameObject.SetActive(false);
        return s;
    }

    private static Color Tint(Expr e)
    {
        switch (e)
        {
            case Expr.Happy: return new Color(1f, 0.78f, 0.25f);
            case Expr.Surprise: return new Color(1f, 0.95f, 0.45f);
            case Expr.Sad: return new Color(0.45f, 0.55f, 0.95f);
            case Expr.Angry: return new Color(1f, 0.32f, 0.25f);
            case Expr.Dizzy: return new Color(0.75f, 0.5f, 1f);
            default: return Color.white;
        }
    }

    private void OnExpr(RagdollPlayer p, Expr e, float seconds)
    {
        if (p == null || _ctl == null) return;
        if (e == Expr.Normal || e == Expr.Focus) return;
        if (seconds < 1f || seconds > 5f) return;            // 一瞬の驚き・登場ポーズは出さない
        if (_ctl.Replaying || AppShell.MenuOpen) return;
        // 打者は右・投手は左
        bool batter = p.Bat != null;
        var s = batter ? _right : _left;
        if (batter ? _coolR > 0f : _coolL > 0f) { if (s.On && s.Who == p) Retint(s, e); return; }
        s.Who = p; s.T = 0f; s.Dur = Mathf.Min(seconds, 1.5f) + 0.35f; s.On = true;
        Retint(s, e);
        s.Root.gameObject.SetActive(true);
        s.Cam.enabled = true;
        if (batter) _coolR = 2.2f; else _coolL = 2.2f;
    }

    private static void Retint(Slot s, Expr e) { s.Bg.color = Tint(e); }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        _coolL -= dt; _coolR -= dt;
        Tick(_left, dt); Tick(_right, dt);
    }

    private void Tick(Slot s, float dt)
    {
        if (!s.On) return;
        if (s.Who == null || AppShell.MenuOpen || _ctl.Replaying) { Hide(s); return; }
        s.T += dt;
        // 横からすべり込んで、少し行き過ぎて戻る。最後はすべり出る
        float inU = Mathf.Clamp01(s.T / 0.16f);
        float outU = Mathf.Clamp01((s.T - (s.Dur - 0.18f)) / 0.18f);
        float x = (1f - EaseOutBack(inU)) * (Size + 30f) + outU * outU * (Size + 30f);
        s.Root.anchoredPosition = new Vector2(-s.Side * (14f - x), 5f);
        s.Rays.localRotation = Quaternion.Euler(0f, 0f, s.T * 40f * s.Side);
        // 顔を追う
        Vector3 c; float half;
        if (s.Who.Skin != null) { c = s.Who.Skin.FaceWorld(); half = s.Who.Skin.FaceSpan() * 0.85f; }
        else { c = s.Who.Head.transform.position; half = 0.7f; }
        s.Cam.orthographicSize = half;
        s.Cam.transform.position = new Vector3(c.x, c.y + half * 0.08f, s.Who.transform.position.z - 3f);
        s.Cam.transform.rotation = Quaternion.identity;
        if (s.T >= s.Dur) Hide(s);
    }

    private static void Hide(Slot s)
    {
        s.On = false; s.Cam.enabled = false; s.Root.gameObject.SetActive(false);
    }

    private static float EaseOutBack(float u)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(u - 1f, 3f) + c1 * Mathf.Pow(u - 1f, 2f);
    }

    private void OnDestroy()
    {
        foreach (var s in new[] { _left, _right })
            if (s != null && s.Rt != null) s.Rt.Release();
    }
}
