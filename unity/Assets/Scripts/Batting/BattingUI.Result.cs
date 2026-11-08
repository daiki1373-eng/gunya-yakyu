using UnityEngine;
using UnityEngine.UI;

// ============================================================
// BattingUI.Result.cs
// 試合後の結果パネル: 勝敗・スコア・相手・もらったコインと、「次の試合」「ホーム」のボタン。
// 押されたボタンは AtBatController が画面座標で調べる(ResultButtonAt)。
// ============================================================
public partial class BattingUI
{
    private RectTransform _resRoot, _resNext, _resHome;
    private Text _resTitle, _resScore, _resSub;
    private float _resT0;

    private void BuildResult()
    {
        if (_resRoot != null) return;
        var canvas = _swingRect.parent;
        var dim = NewImage(canvas, "ResultDim", new Color(0.01f, 0.02f, 0.06f, 0.72f));
        dim.raycastTarget = false;
        _resRoot = dim.rectTransform;
        _resRoot.anchorMin = Vector2.zero; _resRoot.anchorMax = Vector2.one;
        _resRoot.offsetMin = _resRoot.offsetMax = Vector2.zero;

        var plate = NewImage(_resRoot, "Plate", Color.white);
        plate.sprite = TelopFx.PlateSprite(new Color(0.03f, 0.05f, 0.13f, 0.96f), UiKit.GoldLight);
        var pr = plate.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(520f, 300f);
        pr.anchoredPosition = new Vector2(0f, 6f);

        TelopFx fx;
        _resTitle = NewTelop(pr, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f),
            54, TextAnchor.MiddleCenter, UiKit.GoldLight, TelopFx.Kind.Stamp, out fx);
        _resScore = NewText(pr, "Score", new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(500f, 50f),
            30, TextAnchor.MiddleCenter, Color.white);
        _resScore.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _resSub = NewText(pr, "Sub", new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(500f, 34f),
            20, TextAnchor.MiddleCenter, UiKit.GoldLight);
        _resSub.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        _resNext = ResultButton(pr, "次の試合", true, new Vector2(110f, 28f));
        _resHome = ResultButton(pr, "ホーム", false, new Vector2(-110f, 28f));
        _resRoot.gameObject.SetActive(false);
    }

    private RectTransform ResultButton(RectTransform parent, string label, bool primary, Vector2 pos)
    {
        var img = NewImage(parent, "Btn_" + label, Color.white);
        img.sprite = primary ? UiKit.ButtonRed : UiKit.ButtonDark;
        img.type = img.sprite != null && img.sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 0f);
        r.sizeDelta = new Vector2(200f, 58f);
        r.anchoredPosition = pos;
        var t = NewText(r, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 58f),
            26, TextAnchor.MiddleCenter, primary ? Color.white : UiKit.GoldLight);
        t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        t.text = label;
        return r;
    }

    public void ShowMatchResult(string title, Color titleColor, string score, string sub)
    {
        BuildResult();
        _resTitle.text = title;
        _resTitle.color = titleColor;
        _resScore.text = score;
        _resSub.text = sub;
        _resRoot.SetAsLastSibling();
        _resRoot.gameObject.SetActive(true);
        _resT0 = Time.unscaledTime;
    }

    public void HideMatchResult()
    {
        if (_resRoot != null) _resRoot.gameObject.SetActive(false);
    }

    // 0=次の試合、1=ホーム、-1=どちらでもない
    public int ResultButtonAt(Vector2 screenPos)
    {
        if (_resRoot == null || !_resRoot.gameObject.activeSelf) return -1;
        if (RectTransformUtility.RectangleContainsScreenPoint(_resNext, screenPos, null)) return 0;
        if (RectTransformUtility.RectangleContainsScreenPoint(_resHome, screenPos, null)) return 1;
        return -1;
    }

    // 出たときに少し弾ませる
    private void TickResult()
    {
        if (_resRoot == null || !_resRoot.gameObject.activeSelf) return;
        float u = Mathf.Clamp01((Time.unscaledTime - _resT0) / 0.35f);
        float s = 1f + (1f - u) * (1f - u) * 0.25f;
        _resRoot.GetChild(0).localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, u) * s;
    }
}
