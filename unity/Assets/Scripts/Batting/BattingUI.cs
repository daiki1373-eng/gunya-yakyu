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
    private Text _swingLabel, _comm, _replayTag;
    private float _commStart = -99f;
    private TelopFx _commFx;
    private const float CommDur = 1.9f;
    // 投球UI
    private Image[] _pitchBtn = new Image[5];
    private Text[] _pitchTxt = new Text[5];
    private bool[] _pitchAvail = new bool[5];
    private int _pitchSel;
    private RectTransform _meterRoot, _meterMarker;
    private Image _meterGood, _meterPerfect;
    private const float MeterW = 440f;
    private RectTransform _swingRect, _stealRect;
    private Text _stealLabel;
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

        // 周辺減光(中継映像のような締まり)。入力は受けない。
        var vig = new GameObject("Vignette");
        vig.transform.SetParent(canvasGo.transform, false);
        var vimg = vig.AddComponent<Image>();
        vimg.sprite = StadiumArt.Vignette;
        vimg.raycastTarget = false;
        var vrt = vimg.rectTransform;
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;

        // ---- 中継風スコア表示(左上) ----
        var bug = NewImage(canvasGo.transform, "ScoreBug", new Color(0.03f, 0.05f, 0.12f, 0.86f));
        var bRt = bug.rectTransform;
        bRt.anchorMin = bRt.anchorMax = new Vector2(0f, 1f);
        bRt.pivot = new Vector2(0f, 1f);
        bRt.sizeDelta = new Vector2(330f, 68f);
        bRt.anchoredPosition = new Vector2(12f, -10f);
        var accent = NewImage(bug.transform, "Accent", new Color(1f, 0.78f, 0.15f, 1f));
        Place(accent.rectTransform, new Vector2(0f, 0f), new Vector2(330f, 3f));
        _teamChip[0] = Chip(bug.transform, "自", new Color(0.80f, 0.14f, 0.14f), 6f, -6f);
        _teamChip[1] = Chip(bug.transform, "相", new Color(0.14f, 0.28f, 0.75f), 6f, -36f);
        _scoreTxt[0] = NewText(bug.transform, "RunsA", new Vector2(0f, 1f), new Vector2(46f, -4f), new Vector2(40f, 28f), 24, TextAnchor.MiddleCenter, Color.white);
        _scoreTxt[1] = NewText(bug.transform, "RunsB", new Vector2(0f, 1f), new Vector2(46f, -34f), new Vector2(40f, 28f), 24, TextAnchor.MiddleCenter, Color.white);
        _scoreTxt[0].fontStyle = _scoreTxt[1].fontStyle = FontStyle.Bold;
        var sep = NewImage(bug.transform, "Sep", new Color(1f, 1f, 1f, 0.18f));
        Place(sep.rectTransform, new Vector2(92f, -8f), new Vector2(2f, 52f));
        _count = NewText(bug.transform, "Inning", new Vector2(0f, 1f), new Vector2(98f, -18f),
            new Vector2(70f, 30f), 20, TextAnchor.MiddleCenter, Color.white);
        _count.fontStyle = FontStyle.Bold;
        string[] bso = { "B", "S", "O" };
        Color[] bsoCol = { new Color(0.25f, 0.95f, 0.40f), new Color(1f, 0.82f, 0.15f), new Color(1f, 0.25f, 0.2f) };
        for (int r = 0; r < 3; r++)
        {
            NewText(bug.transform, "L" + bso[r], new Vector2(0f, 1f), new Vector2(176f, -4f - r * 20f), new Vector2(16f, 20f), 15, TextAnchor.MiddleCenter, Color.white);
            int n = r == 0 ? 3 : 2;
            _lamps[r] = new Image[n];
            _lampOn[r] = bsoCol[r];
            for (int k = 0; k < n; k++)
            {
                var d = NewImage(bug.transform, "Lamp" + r + k, new Color(1f, 1f, 1f, 0.15f));
                d.sprite = SpriteFactory.Circle;
                Place(d.rectTransform, new Vector2(196f + k * 17f, -7f - r * 20f), new Vector2(13f, 13f));
                _lamps[r][k] = d;
            }
        }

        // ---- 打者名(右上) ----
        var plate = NewImage(canvasGo.transform, "BatterPlate", new Color(0.03f, 0.05f, 0.12f, 0.80f));
        var pRt = plate.rectTransform;
        pRt.anchorMin = pRt.anchorMax = new Vector2(1f, 1f);
        pRt.pivot = new Vector2(1f, 1f);
        pRt.sizeDelta = new Vector2(380f, 34f);
        pRt.anchoredPosition = new Vector2(-12f, -10f);
        _batter = NewText(plate.transform, "Batter", new Vector2(1f, 1f), new Vector2(-12f, -2f),
            new Vector2(360f, 30f), 18, TextAnchor.MiddleRight, Color.white);

        // ---- テロップ ----
        // 文字の重なりを避けるため、縦の置き場所を決めておく(基準 960x540 の中央からの高さ)
        //   判定 +150 / 珍プレー +55 / 得点・コイン -20 / 実況 -92 / 結果の札 -168 / 球速は左下の札
        TelopFx fx;
        var navy = new Color(0.03f, 0.05f, 0.13f, 0.90f);
        var speedPlate = NewPlate(canvasGo.transform, "SpeedPlate", new Vector2(0f, 0f), new Vector2(0f, 0.5f),
            new Vector2(0f, 44f), 42f, navy, UiKit.Gold);
        _speed = NewTelop(canvasGo.transform, "Speed", new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(30f, 44f),
            20, TextAnchor.MiddleLeft, Color.white, TelopFx.Kind.Plain, out fx);
        fx.Mode = TelopFx.Anim.Slide; fx.SlideFrom = -40f; fx.PopTime = 0.2f;
        fx.SetPlate(speedPlate); fx.PlatePad = 90f; fx.PlateMinW = 160f;

        _rank = NewTelop(canvasGo.transform, "Rank", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f),
            56, TextAnchor.MiddleCenter, Color.white, TelopFx.Kind.Rank, out fx);

        var resultPlate = NewPlate(canvasGo.transform, "ResultPlate", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -168f), 54f, navy, UiKit.Gold);
        _result = NewTelop(canvasGo.transform, "Result", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -168f),
            30, TextAnchor.MiddleCenter, Color.white, TelopFx.Kind.Plain, out fx);
        fx.SetPlate(resultPlate);

        _quirk = NewTelop(canvasGo.transform, "Quirk", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 55f),
            62, TextAnchor.MiddleCenter, Color.white, TelopFx.Kind.Stamp, out fx);
        _banner = NewTelop(canvasGo.transform, "Banner", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f),
            46, TextAnchor.MiddleCenter, Color.white, TelopFx.Kind.Gold, out fx);

        // 塁ランプ(ひし形)
        Vector2[] bp = { Vector2.zero, new Vector2(22f, 0f), new Vector2(0f, 22f), new Vector2(-22f, 0f) };
        for (int i = 1; i <= 3; i++)
        {
            var img = NewImage(bug.transform, "Base" + i, new Color(1f, 1f, 1f, 0.25f));
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(15f, 15f);
            r.anchoredPosition = new Vector2(300f, -46f) + bp[i] * 0.85f;
            r.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _bases[i] = img;
        }

        // SWINGボタン(タッチ操作用。マウスはどこをクリックしてもスイング)
        _swingImg = NewImage(canvasGo.transform, "SwingButton", new Color(1f, 1f, 1f, 0.80f));
        _swingImg.sprite = UiKit.ButtonRed;
        _swingImg.type = Image.Type.Sliced;
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

        var steal = NewImage(canvasGo.transform, "StealButton", Color.white);
        steal.sprite = UiKit.ButtonRed;
        steal.type = Image.Type.Sliced;
        _stealRect = steal.rectTransform;
        _stealRect.anchorMin = _stealRect.anchorMax = new Vector2(1f, 0f);
        _stealRect.pivot = new Vector2(1f, 0f);
        _stealRect.sizeDelta = new Vector2(118f, 54f);
        _stealRect.anchoredPosition = new Vector2(-238f, 24f);
        _stealLabel = NewText(_stealRect, "Label", new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(118f, 54f), 23, TextAnchor.MiddleCenter, Color.white);
        _stealLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        ShowSteal(false, false);

        // 実況字幕(バラエティ番組のような太い縁で、横から滑り込む)
        _comm = NewTelop(canvasGo.transform, "Commentary", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -92f),
            48, TextAnchor.MiddleCenter, new Color(1f, 0.93f, 0.35f), TelopFx.Kind.Comment, out _commFx);
        _comm.text = "";
        var replayPlate = NewPlate(canvasGo.transform, "ReplayPlate", new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(0f, -66f), 36f, new Color(0.72f, 0.05f, 0.08f, 0.95f), UiKit.GoldLight);
        _replayTag = NewTelop(canvasGo.transform, "ReplayTag", new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-34f, -66f),
            19, TextAnchor.MiddleRight, Color.white, TelopFx.Kind.Plain, out fx);
        fx.SetPlate(replayPlate); fx.PlatePad = 70f; fx.PlateMinW = 150f;
        _replayTag.text = "";

        BuildIntroCard(canvasGo.transform);
        BuildPitchPanel(canvasGo.transform);
        ClearCenter();
        SetSpeed(null);
    }

    private readonly Image[] _teamChip = new Image[2];
    private readonly Text[] _scoreTxt = new Text[2];
    private readonly Image[][] _lamps = new Image[3][];
    private readonly Color[] _lampOn = new Color[3];

    private static void Place(RectTransform r, Vector2 topLeft, Vector2 size)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.sizeDelta = size;
        r.anchoredPosition = topLeft;
    }

    private Image Chip(Transform parent, string label, Color c, float x, float y)
    {
        var img = NewImage(parent, "Chip" + label, c);
        Place(img.rectTransform, new Vector2(x, y), new Vector2(34f, 26f));
        var t = NewText(img.transform, "T", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 26f), 17, TextAnchor.MiddleCenter, Color.white);
        t.fontStyle = FontStyle.Bold;
        t.text = label;
        return img;
    }

    public void SetCount(int inning, bool top, int balls, int strikes, int outs, int runs, int oppRuns, bool? userBatting = null)
    {
        _count.text = inning + (top ? "回表" : "回裏");
        _scoreTxt[0].text = runs.ToString();
        _scoreTxt[1].text = oppRuns.ToString();
        // 表・裏と自チームの攻撃は別々に扱う。
        bool attacking = userBatting ?? top;
        _teamChip[0].color = attacking ? new Color(0.90f, 0.18f, 0.18f) : new Color(0.45f, 0.12f, 0.12f);
        _teamChip[1].color = attacking ? new Color(0.10f, 0.18f, 0.45f) : new Color(0.18f, 0.34f, 0.90f);
        for (int r = 0; r < 3; r++)
            for (int k = 0; k < _lamps[r].Length; k++)
                _lamps[r][k].color = k < (r == 0 ? balls : (r == 1 ? strikes : outs)) ? _lampOn[r] : new Color(1f, 1f, 1f, 0.15f);
    }

    public void SetBatterLabel(string text) { _batter.text = text; }

    public void ShowSteal(bool available, bool queued)
    {
        if (_stealRect == null) return;
        _stealRect.gameObject.SetActive(available);
        _stealLabel.text = queued ? "盗塁  ✓" : "盗塁";
    }

    public bool OverSteal(Vector2 screenPos)
    {
        return _stealRect != null && _stealRect.gameObject.activeSelf
            && RectTransformUtility.RectangleContainsScreenPoint(_stealRect, screenPos, null);
    }

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

    public void ShowCommentary(string text, Color c)
    {
        if (_comm == null) return;
        _comm.text = text ?? "";
        _comm.color = c;
        _commStart = Time.unscaledTime;
        if (_commFx != null) _commFx.Restart();
    }

    public void SetReplay(bool on, string label = "● REPLAY")
    {
        if (_replayTag != null) _replayTag.text = on ? label : "";
    }

    public void ShowQuirk(string text) { _quirk.text = text ?? ""; }
    public void ShowBanner(string text) { _banner.text = text ?? ""; }

    public void ShowPitch(string name, float kmh)
    {
        _speed.text = name + "  " + SpeedNum(kmh);
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
        _speed.text = SpeedNum(kmh);
    }

    public void ShowRank(HitRank r, string suffix)
    {
        _rank.text = RankText(r) + (string.IsNullOrEmpty(suffix) ? "" : "  <size=28>" + suffix + "</size>");
        _rank.color = RankColor(r);
    }

    public void ShowResult(string text)
    {
        text = text ?? "";
        // 珍プレーのはんこと同じ言葉なら、札は出さない(同じ文字が2つ並ばないように)
        if (text.Length > 0 && _quirk != null && _quirk.text.Length > 0 && _quirk.text.TrimEnd('！', '!') == text.TrimEnd('！', '!'))
            text = "";
        // 飛距離(58m など)は金色で目立たせる
        _result.text = System.Text.RegularExpressions.Regex.Replace(text, @"(\d+m)", "<color=#FFD54A>$1</color>");
    }

    private static string SpeedNum(float kmh)
    {
        return "<color=#FFD54A><size=27>" + Mathf.RoundToInt(kmh) + "</size></color> km/h";
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
        if (_swingImg != null) _swingImg.color = new Color(1f, 0.9f, 0.55f, 1f);
        _swingPulseUntil = Time.unscaledTime + 0.12f;
    }

    // ボタンの色を戻す(コントローラのUpdateから毎フレーム呼ぶ)
    // ---- 登場カード(左下から滑り込む名前の札) ----
    private RectTransform _card;
    private Image _cardStripe;
    private Text _cardTitle, _cardName, _cardStats, _cardCopy, _cardSkip;
    private bool _cardOn;
    private float _cardT;
    private const float CardX = 24f, CardHidden = -640f;

    private void BuildIntroCard(Transform canvas)
    {
        var bg = NewImage(canvas, "IntroCard", new Color(0.20f, 0.02f, 0.03f, 0.92f));
        _card = bg.rectTransform;
        _card.anchorMin = _card.anchorMax = new Vector2(0f, 0f);
        _card.pivot = new Vector2(0f, 0f);
        _card.sizeDelta = new Vector2(560f, 140f);
        _card.anchoredPosition = new Vector2(CardHidden, 130f);
        _cardStripe = NewImage(_card, "Stripe", Color.red);
        Place(_cardStripe.rectTransform, Vector2.zero, new Vector2(14f, 140f));
        var line = NewImage(_card, "Line", new Color(1f, 0.78f, 0.15f, 1f));
        Place(line.rectTransform, new Vector2(14f, -137f), new Vector2(546f, 3f));
        var line2 = NewImage(_card, "LineTop", new Color(1f, 0.78f, 0.15f, 1f));
        Place(line2.rectTransform, new Vector2(14f, 0f), new Vector2(546f, 3f));
        var glow = NewImage(_card, "Glow", new Color(1f, 0.75f, 0.3f, 0.18f));
        glow.sprite = StadiumArt.Glow;
        Place(glow.rectTransform, new Vector2(-60f, 60f), new Vector2(420f, 260f));
        _cardTitle = NewText(_card, "Title", new Vector2(0f, 1f), new Vector2(32f, -8f), new Vector2(500f, 24f), 18, TextAnchor.MiddleLeft, new Color(0.8f, 0.85f, 1f));
        _cardName = NewText(_card, "Name", new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(500f, 54f), 44, TextAnchor.MiddleLeft, Color.white);
        _cardName.fontStyle = FontStyle.Bold;
        _cardStats = NewText(_card, "Stats", new Vector2(0f, 1f), new Vector2(32f, -86f), new Vector2(500f, 22f), 18, TextAnchor.MiddleLeft, Color.white);
        _cardCopy = NewText(_card, "Copy", new Vector2(0f, 1f), new Vector2(32f, -110f), new Vector2(500f, 24f), 19, TextAnchor.MiddleLeft, new Color(1f, 0.88f, 0.35f));
        _cardSkip = NewText(_card, "Skip", new Vector2(1f, 1f), new Vector2(-12f, -8f), new Vector2(120f, 20f), 14, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 0.55f));
        _cardSkip.text = "TAP ▶";
        // キャラ画像の札(カードの上にはみ出して立てる)
        _cardPortraitFrame = NewImage(_card, "PortraitFrame", Color.white);
        _cardPortraitFrame.sprite = UiKit.Frame;
        _cardPortraitFrame.type = Image.Type.Sliced;
        Place(_cardPortraitFrame.rectTransform, new Vector2(20f, 50f), new Vector2(116f, 174f));
        _cardPortrait = NewImage(_cardPortraitFrame.rectTransform, "Portrait", Color.white);
        Place(_cardPortrait.rectTransform, new Vector2(7f, -7f), new Vector2(102f, 160f));
        _cardPortraitFrame.gameObject.SetActive(false);
    }

    private Image _cardPortrait, _cardPortraitFrame;

    public void ShowIntroCard(string title, string name, string stats, string copy, Color stripe, Sprite portrait = null)
    {
        // キャラ画像があれば左に縦長の札で出し、文字を右へずらす
        bool hasP = portrait != null;
        _cardPortrait.gameObject.SetActive(hasP);
        _cardPortraitFrame.gameObject.SetActive(hasP);
        if (hasP)
        {
            _cardPortrait.sprite = portrait;
            _cardPortraitFrame.color = stripe;
        }
        float x = hasP ? 150f : 32f;
        foreach (var t in new[] { _cardTitle, _cardStats, _cardCopy })
            t.rectTransform.anchoredPosition = new Vector2(x, t.rectTransform.anchoredPosition.y);
        _cardName.rectTransform.anchoredPosition = new Vector2(x - 2f, _cardName.rectTransform.anchoredPosition.y);
        _cardTitle.text = title;
        _cardName.text = name;
        _cardStats.text = stats;
        _cardCopy.text = copy;
        _cardStripe.color = stripe;
        _cardOn = true;
        _cardT = Time.unscaledTime;
    }

    public void HideIntroCard()
    {
        if (!_cardOn) return;
        _cardOn = false;
        _cardT = Time.unscaledTime;
    }

    private void TickCard()
    {
        if (_card == null) return;
        float u = Mathf.Clamp01((Time.unscaledTime - _cardT) / 0.28f);
        float e = 1f - (1f - u) * (1f - u);
        float x = _cardOn ? Mathf.Lerp(CardHidden, CardX, e) : Mathf.Lerp(CardX, CardHidden, e);
        _card.anchoredPosition = new Vector2(x, 130f);
    }

    public void Tick()
    {
        TickCard();
        if (_comm != null && _comm.text.Length > 0)
        {
            float u = (Time.unscaledTime - _commStart) / CommDur;
            if (u >= 1f) _comm.text = "";
            else
            {
                var c = _comm.color; c.a = Mathf.Clamp01((1f - u) / 0.25f); _comm.color = c;
            }
        }
        if (_swingImg != null && Time.unscaledTime > _swingPulseUntil)
            _swingImg.color = new Color(1f, 1f, 1f, 0.80f);
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

    private Text NewTelop(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos,
        int fontSize, TextAnchor align, Color color, TelopFx.Kind kind, out TelopFx fx)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(900f, fontSize * 1.5f);
        var text = go.AddComponent<Text>();
        text.font = _font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = align;
        text.color = color;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        fx = go.AddComponent<TelopFx>();
        fx.Preset(kind);
        return text;
    }

    private RectTransform NewPlate(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos,
        float height, Color body, Color line)
    {
        var img = NewImage(parent, name, Color.white);
        img.sprite = TelopFx.PlateSprite(body, line);
        img.raycastTarget = false;
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = new Vector2(300f, height);
        img.gameObject.SetActive(false);
        return r;
    }

    public static Font GetFont()
    {
        try
        {
            var jp = Resources.Load<Font>("Fonts/NotoSansJP-Bold");
            if (jp != null) return jp;
        }
        catch (System.Exception) { }
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
