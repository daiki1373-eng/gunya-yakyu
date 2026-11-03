using UnityEngine;
using UnityEngine.UI;

// ============================================================
// BattingUI.cs
// ミニマルなHUD。ラベル・数値・短い状態表示・SWINGボタンだけ(説明文なし)。
// ============================================================
public class BattingUI
{
    private Text _count, _batter, _speed, _rank, _result, _quirk, _banner;
    private Image[] _bases = new Image[4];
    private Text _swingLabel;
    // 投球UI
    private Image[] _pitchBtn = new Image[5];
    private Text[] _pitchTxt = new Text[5];
    private bool[] _pitchAvail = new bool[5];
    private int _pitchSel;
    private RectTransform _meterRoot, _meterMarker;
    private Image _meterGood, _meterPerfect;
    private const float MeterW = 440f;
    private RectTransform _swingRect;
    private Image _swingImg;
    private Font _font;
    private float _swingPulseUntil;

    public static BattingUI Build(Transform parent)
    {
        var ui = new BattingUI();
        ui.BuildInternal(parent);
        return ui;
    }

    public static Color RankColor(HitRank r)
    {
        switch (r)
        {
            case HitRank.Perfect: return new Color(1f, 0.86f, 0.18f);
            case HitRank.Good: return new Color(0.45f, 0.95f, 0.45f);
            case HitRank.Normal: return new Color(1f, 1f, 1f);
            case HitRank.Weak: return new Color(0.62f, 0.78f, 1f);
            default: return new Color(1f, 0.38f, 0.34f);
        }
    }

    public static string RankText(HitRank r)
    {
        switch (r)
        {
            case HitRank.Perfect: return "PERFECT!";
            case HitRank.Good: return "GOOD";
            case HitRank.Normal: return "NORMAL";
            case HitRank.Weak: return "WEAK";
            default: return "MISS";
        }
    }

    private void BuildInternal(Transform parent)
    {
        var canvasGo = new GameObject("BattingUI");
        canvasGo.transform.SetParent(parent, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960f, 540f);
        scaler.matchWidthOrHeight = 0.5f;
        _font = GetFont();

        // 上部バー
        var bar = NewImage(canvasGo.transform, "TopBar", new Color(0f, 0f, 0f, 0.35f));
        var brt = bar.rectTransform;
        brt.anchorMin = new Vector2(0f, 1f);
        brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.sizeDelta = new Vector2(0f, 44f);
        brt.anchoredPosition = Vector2.zero;

        _count = NewText(canvasGo.transform, "Count", new Vector2(0f, 1f), new Vector2(16f, -6f),
            new Vector2(460f, 34f), 22, TextAnchor.MiddleLeft, Color.white);
        _batter = NewText(canvasGo.transform, "Batter", new Vector2(1f, 1f), new Vector2(-16f, -6f),
            new Vector2(460f, 34f), 18, TextAnchor.MiddleRight, Color.white);
        _speed = NewText(canvasGo.transform, "Speed", new Vector2(0.5f, 1f), new Vector2(0f, -56f),
            new Vector2(300f, 34f), 24, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.95f));
        _rank = NewText(canvasGo.transform, "Rank", new Vector2(0.5f, 0.5f), new Vector2(0f, 150f),
            new Vector2(700f, 70f), 52, TextAnchor.MiddleCenter, Color.white);
        _rank.fontStyle = FontStyle.Bold;
        _result = NewText(canvasGo.transform, "Result", new Vector2(0.5f, 0.5f), new Vector2(0f, 96f),
            new Vector2(800f, 50f), 34, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.6f));
        _result.fontStyle = FontStyle.Bold;

        _quirk = NewText(canvasGo.transform, "Quirk", new Vector2(0.5f, 0.5f), new Vector2(0f, 40f),
            new Vector2(800f, 70f), 46, TextAnchor.MiddleCenter, new Color(1f, 0.55f, 0.15f));
        _quirk.fontStyle = FontStyle.Bold;
        _banner = NewText(canvasGo.transform, "Banner", new Vector2(0.5f, 0.5f), new Vector2(0f, -60f),
            new Vector2(800f, 60f), 38, TextAnchor.MiddleCenter, Color.white);
        _banner.fontStyle = FontStyle.Bold;

        // 塁ランプ(ひし形)
        Vector2[] bp = { Vector2.zero, new Vector2(22f, 0f), new Vector2(0f, 22f), new Vector2(-22f, 0f) };
        for (int i = 1; i <= 3; i++)
        {
            var img = NewImage(canvasGo.transform, "Base" + i, new Color(1f, 1f, 1f, 0.25f));
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(18f, 18f);
            r.anchoredPosition = new Vector2(70f, -88f) + bp[i];
            r.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _bases[i] = img;
        }

        // SWINGボタン(タッチ操作用。マウスはどこをクリックしてもスイング)
        _swingImg = NewImage(canvasGo.transform, "SwingButton", new Color(1f, 0.55f, 0.15f, 0.55f));
        _swingRect = _swingImg.rectTransform;
        _swingRect.anchorMin = new Vector2(1f, 0f);
        _swingRect.anchorMax = new Vector2(1f, 0f);
        _swingRect.pivot = new Vector2(1f, 0f);
        _swingRect.sizeDelta = new Vector2(200f, 100f);
        _swingRect.anchoredPosition = new Vector2(-24f, 24f);
        var label = NewText(_swingRect, "Label", new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(200f, 100f), 30, TextAnchor.MiddleCenter, Color.white);
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.text = "SWING";
        _swingLabel = label;
        label.fontStyle = FontStyle.Bold;

        BuildPitchPanel(canvasGo.transform);
        ClearCenter();
        SetSpeed(null);
    }

    public void SetCount(int inning, bool top, int balls, int strikes, int outs, int runs, int oppRuns)
    {
        _count.text = inning + "回" + (top ? "表" : "裏") + "  B " + balls + "  S " + strikes + "  O " + outs
                    + "   自 " + runs + " - " + oppRuns + " 相";
    }

    public void SetBatterLabel(string text) { _batter.text = text; }

    public void SetSwingLabel(string text) { if (_swingLabel != null) _swingLabel.text = text; }

    public void ShowTiming(string text, Color c)
    {
        _rank.text = text;
        _rank.color = c;
    }

    // ---- 投球UI(球種ボタン・タイミングメーター) ----
    private void BuildPitchPanel(Transform canvas)
    {
        for (int i = 0; i < 5; i++)
        {
            var img = NewImage(canvas, "PitchBtn" + i, new Color(0f, 0f, 0f, 0.45f));
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.sizeDelta = new Vector2(190f, 40f);
            r.anchoredPosition = new Vector2(14f, 70f - i * 46f);
            var t = NewText(r, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 40f),
                20, TextAnchor.MiddleCenter, Color.white);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            t.text = (i + 1) + "  " + PitchRepertoire.All[i].Name;
            _pitchBtn[i] = img;
            _pitchTxt[i] = t;
            img.gameObject.SetActive(false);
        }

        var root = NewImage(canvas, "Meter", new Color(0f, 0f, 0f, 0.55f));
        _meterRoot = root.rectTransform;
        _meterRoot.anchorMin = _meterRoot.anchorMax = new Vector2(0.5f, 0f);
        _meterRoot.pivot = new Vector2(0.5f, 0.5f);
        _meterRoot.sizeDelta = new Vector2(MeterW, 22f);
        _meterRoot.anchoredPosition = new Vector2(0f, 80f);
        // 良い帯(緑)と完璧の帯(黄)。位置は SetMeterZones で決める
        _meterGood = NewImage(_meterRoot, "Good", new Color(0.35f, 0.85f, 0.4f, 0.75f));
        _meterPerfect = NewImage(_meterRoot, "Perfect", new Color(1f, 0.88f, 0.2f, 0.95f));
        var mk = NewImage(_meterRoot, "Marker", Color.white);
        _meterMarker = mk.rectTransform;
        _meterMarker.anchorMin = _meterMarker.anchorMax = new Vector2(0f, 0.5f);
        _meterMarker.pivot = new Vector2(0.5f, 0.5f);
        _meterMarker.sizeDelta = new Vector2(6f, 36f);
        _meterRoot.gameObject.SetActive(false);
    }

    // sweetU=ベストの位置(0..1)、goodHalf/perfectHalf=その左右の幅(0..1換算)
    public void SetMeterZones(float sweetU, float goodHalf, float perfectHalf)
    {
        PlaceBand(_meterGood.rectTransform, sweetU, goodHalf);
        PlaceBand(_meterPerfect.rectTransform, sweetU, perfectHalf);
    }

    private static void PlaceBand(RectTransform r, float u, float half)
    {
        float lo = Mathf.Clamp01(u - half), hi = Mathf.Clamp01(u + half);
        r.anchorMin = new Vector2(lo, 0f);
        r.anchorMax = new Vector2(hi, 1f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    public void ShowMeter(bool on)
    {
        if (_meterRoot != null) _meterRoot.gameObject.SetActive(on);
        if (on) SetMeter(0f);
    }

    public void SetMeter(float u)
    {
        if (_meterMarker == null) return;
        _meterMarker.anchoredPosition = new Vector2(Mathf.Clamp01(u) * MeterW, 0f);
    }

    // available[i]=その球種を投げられるか
    public void ShowPitchPanel(bool on, bool[] available, int selected)
    {
        for (int i = 0; i < 5; i++)
        {
            _pitchAvail[i] = available != null && available[i];
            _pitchBtn[i].gameObject.SetActive(on && _pitchAvail[i]);
        }
        SelectPitch(selected);
    }

    public void SelectPitch(int k)
    {
        _pitchSel = k;
        for (int i = 0; i < 5; i++)
        {
            _pitchBtn[i].color = i == k ? new Color(1f, 0.6f, 0.15f, 0.85f) : new Color(0f, 0f, 0f, 0.45f);
        }
    }

    // 画面座標がどの球種ボタンの上か(なければ -1)
    public int PitchButtonAt(Vector2 screenPos)
    {
        for (int i = 0; i < 5; i++)
        {
            if (!_pitchBtn[i].gameObject.activeSelf) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(_pitchBtn[i].rectTransform, screenPos, null)) return i;
        }
        return -1;
    }

    public void SetBases(bool[] b)
    {
        for (int i = 1; i <= 3; i++)
        {
            if (_bases[i] != null)
                _bases[i].color = b[i] ? new Color(1f, 0.82f, 0.15f, 1f) : new Color(1f, 1f, 1f, 0.25f);
        }
    }

    public void ShowQuirk(string text) { _quirk.text = text ?? ""; }
    public void ShowBanner(string text) { _banner.text = text ?? ""; }

    public void ShowPitch(string name, float kmh)
    {
        _speed.text = name + "  " + Mathf.RoundToInt(kmh) + " km/h";
    }

    public void SetBatter(PlayerStats p)
    {
        _batter.text = p.Name + "  ミート " + p.Meet + "  パワー " + p.Power;
    }

    public void SetSpeed(string text)
    {
        _speed.text = text ?? "";
    }

    public void ShowSpeed(float kmh)
    {
        _speed.text = Mathf.RoundToInt(kmh) + " km/h";
    }

    public void ShowRank(HitRank r, string suffix)
    {
        _rank.text = RankText(r) + (string.IsNullOrEmpty(suffix) ? "" : "  " + suffix);
        _rank.color = RankColor(r);
    }

    public void ShowResult(string text)
    {
        _result.text = text ?? "";
    }

    public void ClearCenter()
    {
        _rank.text = "";
        _result.text = "";
        if (_quirk != null) _quirk.text = "";
    }

    public void ClearSpeed()
    {
        _speed.text = "";
    }

    public bool OverSwing(Vector2 screenPos)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(_swingRect, screenPos, null);
    }

    public void PulseSwing()
    {
        if (_swingImg != null) _swingImg.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        _swingPulseUntil = Time.unscaledTime + 0.12f;
    }

    // ボタンの色を戻す(コントローラのUpdateから毎フレーム呼ぶ)
    public void Tick()
    {
        if (_swingImg != null && Time.unscaledTime > _swingPulseUntil)
            _swingImg.color = new Color(1f, 0.55f, 0.15f, 0.55f);
    }

    // ---- 部品 ----
    private Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = SpriteFactory.White;
        img.color = color;
        return img;
    }

    private Text NewText(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
        int fontSize, TextAnchor align, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(anchor.x, anchor.y);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var text = go.AddComponent<Text>();
        text.font = _font;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
        ol.effectDistance = new Vector2(1.5f, -1.5f);
        return text;
    }

    private static Font GetFont()
    {
        try
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f != null) return f;
        }
        catch (System.Exception) { }
        try { return Resources.GetBuiltinResource<Font>("Arial.ttf"); }
        catch (System.Exception) { return null; }
    }
}
