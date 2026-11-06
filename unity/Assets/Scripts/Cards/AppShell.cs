using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================
// AppShell.cs
// アプリの外枠: ホーム / 試合 / ガチャ / カード一覧・拡大。
// 試合はホームの「試合」で始まり、試合中は左上の「≡」でホームへ(試合は一時停止)。
// 演出は金×赤(放射光・花火・紙吹雪)。画面の文字はラベル・数値・操作のみ。
// ============================================================
public partial class AppShell : MonoBehaviour
{
    public static AppShell Instance { get; private set; }
    public static bool MenuOpen { get; private set; }

    private RectTransform _root, _home, _cards, _gacha, _detail, _show;
    private UiFx _fx;
    private GameObject _menuBtn;
    private Text _playLabel, _homeOwned, _homeCoins, _cardsTitle, _gachaInfo;
    private RectTransform _homeRays, _gachaRays;
    private bool _gameStarted;
    private float _savedScale = 1f, _nextFirework;
    private readonly List<KeyValuePair<CharaDef, CardWidget.Parts>> _cells = new List<KeyValuePair<CharaDef, CardWidget.Parts>>();

    public static AppShell Create()
    {
        var go = new GameObject("AppShell");
        DontDestroyOnLoad(go);
        return go.AddComponent<AppShell>();
    }

    private void Awake()
    {
        Instance = this;
        AtBatController.HoldStart = true;
        AtBatController.InputBlocked = true;
        GameAudio.Init(gameObject);
        Build();
        ShowTitle();
    }

    // ------------------------------------------------------------
    // 画面の組み立て
    // ------------------------------------------------------------
    private void Build()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }
        var canvasGo = new GameObject("AppCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960f, 540f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        _root = (RectTransform)canvasGo.transform;

        BuildHome();
        BuildCards();
        BuildTitle();
        BuildGacha();
        _detail = UiKit.Fill(UiKit.Rect(_root, "Detail"));
        _show = UiKit.Fill(UiKit.Rect(_root, "GachaShow"));
        _fx = UiFx.Create(_root);

        // 試合中のメニューボタン(左上のスコア表示の下)
        var mb = UiKit.Btn(_root, "MenuButton", "≡", new Vector2(52f, 40f), false, OpenMenu, 24);
        UiKit.Place(mb.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -84f), new Vector2(52f, 40f));
        _menuBtn = mb.gameObject;
        _menuBtn.SetActive(false);
    }

    private RectTransform MakeScreen(string name, float bgDark)
    {
        var r = UiKit.Fill(UiKit.Rect(_root, name));
        var bg = UiKit.Img(r, "Bg", UiKit.BgRadial, new Color(bgDark, bgDark, bgDark, 1f), true);
        UiKit.Fill(bg.rectTransform);
        return r;
    }

    private RectTransform AddRays(RectTransform parent, Color c, float size, Vector2 pos)
    {
        var rays = UiKit.Img(parent, "Rays", UiKit.Rays, c);
        UiKit.Place(rays.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
        rays.transform.SetSiblingIndex(1);
        return rays.rectTransform;
    }

    private void TopBar(RectTransform screen, string title, out Text titleText)
    {
        var bar = UiKit.Img(screen, "TopBar", SpriteFactory.White, new Color(0.10f, 0.02f, 0.02f, 0.85f));
        UiKit.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(2000f, 60f));
        var line = UiKit.Img(screen, "TopLine", SpriteFactory.White, UiKit.Gold);
        UiKit.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(2000f, 3f));
        var back = UiKit.Btn(screen, "Back", "←", new Vector2(64f, 44f), false, ShowHome, 26);
        UiKit.Place(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(64f, 44f));
        titleText = UiKit.Style(UiKit.Txt(screen, "Title", title, 30, UiKit.GoldLight, TextAnchor.MiddleLeft), TelopFx.Kind.Heading);
        UiKit.Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -8f), new Vector2(600f, 44f));
    }

    // ---- ホーム ----
    private void BuildHome()
    {
        _home = MakeScreen("Home", 1f);
        _homeRays = AddRays(_home, new Color(1f, 0.85f, 0.4f, 0.45f), 1500f, new Vector2(-170f, 0f));

        // キービジュアル(★5カードを傾けて置く)
        var kv = CharaCatalog.Get("heroine");
        var card = CardWidget.Build(_home, kv, new Vector2(250f, 375f), true, true, 1);
        card.Root.anchorMin = card.Root.anchorMax = new Vector2(0.5f, 0.5f);
        card.Root.anchoredPosition = new Vector2(-200f, -8f);
        card.Root.localRotation = Quaternion.Euler(0f, 0f, 6f);
        card.CountText.text = "";
        card.Frame.raycastTarget = false;

        var t1 = UiKit.Txt(_home, "Logo1", "ぐにゃぐにゃ", 54, UiKit.GoldLight, TextAnchor.MiddleCenter);
        UiKit.Place(t1.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(190f, 178f), new Vector2(520f, 70f));
        var t2 = UiKit.Txt(_home, "Logo2", "ベースボール", 62, Color.white, TextAnchor.MiddleCenter);
        UiKit.Place(t2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(190f, 112f), new Vector2(560f, 80f));
        t2.color = new Color(1f, 0.95f, 0.78f);
        UiKit.Style(t1, TelopFx.Kind.Logo);
        UiKit.Style(t2, TelopFx.Kind.Logo);
        t1.gameObject.AddComponent<JellyText>().Amp = 2.5f;
        t2.gameObject.AddComponent<JellyText>().Amp = 2f;

        var play = UiKit.Btn(_home, "Play", "試合", new Vector2(300f, 70f), true, StartGame, 32);
        UiKit.Place(play.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(190f, 10f), new Vector2(300f, 70f));
        _playLabel = play.GetComponentInChildren<Text>();
        var gacha = UiKit.Btn(_home, "Gacha", "ガチャ", new Vector2(300f, 62f), true, ShowGacha, 28);
        UiKit.Place(gacha.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(190f, -74f), new Vector2(300f, 62f));
        var cards = UiKit.Btn(_home, "Cards", "カード", new Vector2(300f, 62f), false, ShowCards, 28);
        UiKit.Place(cards.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(190f, -150f), new Vector2(300f, 62f));
        _homeCoins = UiKit.Txt(_home, "Coins", "", 20, UiKit.GoldLight, TextAnchor.UpperRight);
        UiKit.Place(_homeCoins.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -16f), new Vector2(250f, 30f));
        _homeOwned = UiKit.Txt(_home, "Owned", "", 20, UiKit.GoldLight, TextAnchor.MiddleCenter);
        UiKit.Place(_homeOwned.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(190f, -206f), new Vector2(300f, 30f));
    }

    // ---- タイトル(主ボタンは「つづきから」だけ)と、初回の名前入力 ----
    private RectTransform _title, _namePanel, _titleRays;
    private InputField _nameInput;
    private const string NameKey = "gunya.player.name";

    public static string PlayerName { get { return PlayerPrefs.GetString(NameKey, ""); } }

    private void BuildTitle()
    {
        _title = MakeScreen("Title", 1f);
        _titleRays = AddRays(_title, new Color(1f, 0.85f, 0.4f, 0.55f), 1600f, new Vector2(0f, 40f));
        // キービジュアル: キャラのカードを扇状に
        string[] ids = { "ojisan", "robot-gold", "heroine", "dog", "sumo" };
        for (int i = 0; i < ids.Length; i++)
        {
            var d = CharaCatalog.Get(ids[i]);
            if (d == null) continue;
            float k = i - 2;
            var c = CardWidget.Build(_title, d, new Vector2(170f, 255f), false, true, 1);
            c.Root.anchorMin = c.Root.anchorMax = new Vector2(0.5f, 0.5f);
            c.Root.anchoredPosition = new Vector2(k * 150f, -12f - Mathf.Abs(k) * 18f);
            c.Root.localRotation = Quaternion.Euler(0f, 0f, -k * 7f);
            c.CountText.text = "";
            c.Frame.raycastTarget = false;
            if (i == 2) c.Root.SetAsLastSibling();
            _titleCards.Add(c.Root);
        }
        var logo1 = UiKit.Txt(_title, "Logo1", "ぐにゃぐにゃ", 64, UiKit.GoldLight, TextAnchor.MiddleCenter);
        UiKit.Place(logo1.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(800f, 80f));
        var logo2 = UiKit.Txt(_title, "Logo2", "ベースボール", 74, Color.white, TextAnchor.MiddleCenter);
        UiKit.Place(logo2.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(860f, 92f));
        logo2.color = new Color(1f, 0.95f, 0.78f);
        UiKit.Style(logo1, TelopFx.Kind.Logo);
        UiKit.Style(logo2, TelopFx.Kind.Logo);
        _logo1 = logo1.gameObject.AddComponent<JellyText>();
        _logo2 = logo2.gameObject.AddComponent<JellyText>();
        _logo2.Amp = 3f; _logo2.Wave = 0.025f;
        var cont = UiKit.Btn(_title, "Continue", "つづきから", new Vector2(320f, 74f), true, OnContinue, 34);
        UiKit.Place(cont.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(320f, 74f));
        _titleCont = cont.GetComponent<RectTransform>();

        // 名前入力(初回だけ)
        _namePanel = UiKit.Fill(UiKit.Rect(_title, "NamePanel"));
        var dim = UiKit.Img(_namePanel, "Dim", SpriteFactory.White, new Color(0f, 0f, 0f, 0.75f), true);
        UiKit.Fill(dim.rectTransform);
        var box = UiKit.Img(_namePanel, "Box", UiKit.ButtonDark, Color.white, true);
        UiKit.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(520f, 240f));
        var label = UiKit.Txt(box.transform, "Label", "なまえ", 30, UiKit.GoldLight, TextAnchor.MiddleCenter);
        UiKit.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(400f, 44f));
        var field = UiKit.Img(box.transform, "Field", SpriteFactory.White, new Color(1f, 0.97f, 0.9f), true);
        UiKit.Place(field.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(420f, 58f));
        var txt = UiKit.Txt(field.transform, "Text", "", 28, new Color(0.15f, 0.05f, 0.05f), TextAnchor.MiddleCenter, true, false);
        UiKit.Fill(txt.rectTransform, 6f);
        txt.supportRichText = false;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        var ph = UiKit.Txt(field.transform, "Placeholder", "10文字まで", 22, new Color(0.5f, 0.42f, 0.4f), TextAnchor.MiddleCenter, false, false);
        UiKit.Fill(ph.rectTransform, 6f);
        _nameInput = field.gameObject.AddComponent<InputField>();
        _nameInput.textComponent = txt;
        _nameInput.placeholder = ph;
        _nameInput.characterLimit = 10;
        var ok = UiKit.Btn(box.transform, "OK", "決定", new Vector2(200f, 56f), true, OnNameDecided, 26);
        UiKit.Place(ok.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(200f, 56f));
        _namePanel.gameObject.SetActive(false);
    }

    public void ShowTitle()
    {
        Only(_title);
        bool intro = !_titleIntroDone;
        _namePanel.gameObject.SetActive(false);
        _menuBtn.SetActive(false);
        MenuOpen = true;
        AtBatController.InputBlocked = true;
        if (intro) StartCoroutine(TitleIntro());   // 曲はロゴが叩きつけられた瞬間に始める
        else BgmPlayer.PlayBase(BgmPlayer.Home);
    }

    private void OnContinue()
    {
        if (string.IsNullOrEmpty(PlayerName))
        {
            _namePanel.gameObject.SetActive(true);
            _nameInput.text = "";
            _nameInput.ActivateInputField();
            return;
        }
        ShowHome();
    }

    private void OnNameDecided()
    {
        string n = _nameInput.text.Trim();
        if (n.Length == 0) { GameAudio.Play(Sfx.Boing, 0.5f, 1.2f); return; }
        PlayerPrefs.SetString(NameKey, n);
        PlayerPrefs.Save();
        _namePanel.gameObject.SetActive(false);
        ShowHome();
    }

    // ---- カード一覧 ----
    private void BuildCards()
    {
        _cards = MakeScreen("Cards", 0.42f);
        TopBar(_cards, "カード", out _cardsTitle);

        var view = UiKit.Rect(_cards, "Viewport");
        view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one;
        view.offsetMin = new Vector2(16f, 8f); view.offsetMax = new Vector2(-16f, -70f);
        view.gameObject.AddComponent<RectMask2D>();
        var hit = view.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        var content = UiKit.Rect(view, "Content");
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(104f, 156f);
        grid.spacing = new Vector2(10f, 12f);
        grid.padding = new RectOffset(4, 4, 8, 16);
        grid.childAlignment = TextAnchor.UpperCenter;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = view.gameObject.AddComponent<ScrollRect>();
        sr.content = content;
        sr.viewport = view;
        sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Elastic;
        sr.scrollSensitivity = 30f;

        foreach (var d in CharaCatalog.All)
        {
            var def = d;
            var p = CardWidget.Build(content, d, grid.cellSize, false, Collection.Owned(d.Id), Collection.Count(d.Id), true);
            var b = p.Root.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { GameAudio.Play(Sfx.Click, 0.5f, 1.3f); ShowDetail(def); });
            _cells.Add(new KeyValuePair<CharaDef, CardWidget.Parts>(d, p));
        }
    }

    private void RefreshCards()
    {
        foreach (var kv in _cells)
            CardWidget.SetCount(kv.Value, kv.Key, Collection.Owned(kv.Key.Id), Collection.Count(kv.Key.Id));
        int owned = Collection.OwnedKinds();
        _cardsTitle.text = "カード   " + owned + " / " + CharaCatalog.All.Count;
        _homeOwned.text = (string.IsNullOrEmpty(PlayerName) ? "" : PlayerName + "   ") + "カード " + owned + " / " + CharaCatalog.All.Count;
    }

    // ---- カード拡大 ----
    private void ShowDetail(CharaDef d)
    {
        ClearChildren(_detail);
        _detail.gameObject.SetActive(true);
        var dim = UiKit.Img(_detail, "Dim", SpriteFactory.White, new Color(0f, 0f, 0f, 0.82f), true);
        UiKit.Fill(dim.rectTransform);
        var close = dim.gameObject.AddComponent<Button>();
        close.transition = Selectable.Transition.None;
        close.onClick.AddListener(() => { GameAudio.Play(Sfx.Click, 0.5f, 1.1f); _detail.gameObject.SetActive(false); });
        var rays = UiKit.Img(_detail, "Rays", UiKit.Rays, RevealColor(d.Rarity) * new Color(1f, 1f, 1f, 0.5f));
        UiKit.Place(rays.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-150f, 0f), new Vector2(900f, 900f));
        bool owned = Collection.Owned(d.Id);
        int cnt = Collection.Count(d.Id);
        var p = CardWidget.Build(_detail, d, new Vector2(290f, 435f), true, owned, cnt);
        p.Root.anchorMin = p.Root.anchorMax = new Vector2(0.5f, 0.5f);
        p.Root.anchoredPosition = new Vector2(-150f, 0f);
        p.Frame.raycastTarget = false;
        StartCoroutine(PopIn(p.Root, 0.25f));

        var r = UiKit.Txt(_detail, "Rarity", CharaCatalog.Label(d.Rarity) + "   " + CodeLabel(d.Code), 30, d.Rarity == Rarity.Secret ? new Color(1f, 0.6f, 0.95f) : UiKit.GoldLight, TextAnchor.MiddleLeft);
        UiKit.Place(r.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 178f), new Vector2(440f, 40f));
        var n = UiKit.Style(UiKit.Txt(_detail, "Name", d.Name, 34, Color.white, TextAnchor.MiddleLeft), TelopFx.Kind.Heading);
        UiKit.Place(n.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 134f), new Vector2(460f, 48f));
        UiKit.FitName(n, d.Name, 450f, 52f, 34, 21);
        var pos = UiKit.Txt(_detail, "Pos", d.Position + "   " + string.Join(" / ", d.Tags), 18, new Color(1f, 0.85f, 0.6f), TextAnchor.MiddleLeft);
        UiKit.Place(pos.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 98f), new Vector2(460f, 28f));
        // 紹介文(roster-101.csv の intro)
        var intro = UiKit.Txt(_detail, "Intro", d.Intro, 17, Color.white, TextAnchor.UpperLeft, false);
        intro.horizontalOverflow = HorizontalWrapMode.Wrap;
        UiKit.Place(intro.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(22f, 80f), new Vector2(430f, 90f));
        int max = CharaCatalog.StackMax(d);
        var s = UiKit.Txt(_detail, "Stack", owned ? "重ね " + cnt + " / " + max : "未所持", 24, owned ? UiKit.GoldLight : new Color(0.7f, 0.7f, 0.75f), TextAnchor.MiddleLeft);
        UiKit.Place(s.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, -30f), new Vector2(420f, 36f));
        // 重ねの段階を丸で表示
        for (int i = 0; i < max; i++)
        {
            var dot = UiKit.Img(_detail, "Pip" + i, SpriteFactory.Circle, i < cnt ? UiKit.Gold : new Color(1f, 1f, 1f, 0.18f));
            UiKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f + i * 26f, -64f), new Vector2(18f, 18f));
        }
        // 交換所: ダブりを交換ポイントに
        var ex = UiKit.Txt(_detail, "ExPt", "交換 " + d.ExchangePoints.ToString("N0") + " pt", 22, UiKit.GoldLight, TextAnchor.MiddleLeft);
        UiKit.Place(ex.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, -104f), new Vector2(240f, 36f));
        if (cnt >= 2)
        {
            var def = d;
            var b = UiKit.Btn(_detail, "Exchange", "交換する", new Vector2(150f, 46f), true, () =>
            {
                if (Collection.Exchange(def)) { GameAudio.Play(Sfx.Catch, 0.8f, 1.3f); ShowDetail(def); RefreshCards(); }
            }, 20);
            UiKit.Place(b.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(262f, -104f), new Vector2(150f, 46f));
        }
        var have = UiKit.Txt(_detail, "Points", Collection.Points.ToString("N0") + " pt", 20, Color.white, TextAnchor.MiddleLeft);
        UiKit.Place(have.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, -146f), new Vector2(240f, 32f));
    }

    // カード番号の表示("ch006" → "No.006")
    private static string CodeLabel(string code)
    {
        if (string.IsNullOrEmpty(code)) return "";
        string digits = "";
        foreach (char ch in code) if (char.IsDigit(ch)) digits += ch;
        return digits.Length > 0 ? "No." + digits : code;
    }

    // ---- ガチャ画面 ----
    private void BuildGacha()
    {
        _gacha = MakeScreen("Gacha", 1f);
        _gachaRays = AddRays(_gacha, new Color(1f, 0.85f, 0.35f, 0.55f), 1500f, new Vector2(0f, 30f));
        Text title;
        TopBar(_gacha, "ガチャ", out title);
        // 中央の光る球
        var glow = UiKit.Img(_gacha, "Glow", StadiumArt.Glow, new Color(1f, 0.9f, 0.5f, 0.9f));
        UiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(320f, 320f));
        var ball = UiKit.Img(_gacha, "Ball", SpriteFactory.Circle, Color.white);
        UiKit.Place(ball.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(130f, 130f));
        var seam1 = UiKit.Img(ball.transform, "Seam", BattingSprites.Ring, new Color(0.85f, 0.1f, 0.1f));
        UiKit.Place(seam1.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-70f, 0f), new Vector2(120f, 120f));
        var seam2 = UiKit.Img(ball.transform, "Seam2", BattingSprites.Ring, new Color(0.85f, 0.1f, 0.1f));
        UiKit.Place(seam2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(70f, 0f), new Vector2(120f, 120f));
        ball.gameObject.AddComponent<RectMask2D>();

        _gachaInfo = UiKit.Txt(_gacha, "Info", "", 20, UiKit.GoldLight, TextAnchor.MiddleCenter);
        UiKit.Place(_gachaInfo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(600f, 30f));
        var one = UiKit.Btn(_gacha, "Pull1", "1回  " + Collection.PullCoinCost + "枚", new Vector2(220f, 70f), false, () => Pull(1), 24);
        UiKit.Place(one.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-130f, -180f), new Vector2(220f, 70f));
        var ten = UiKit.Btn(_gacha, "Pull10", "10連  " + (Collection.PullCoinCost * 10) + "枚", new Vector2(220f, 70f), true, () => Pull(10), 24);
        UiKit.Place(ten.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(130f, -180f), new Vector2(220f, 70f));
        // 交換ポイントでスカウト(1回)
        var exchange = UiKit.Btn(_gacha, "ExchangeCoins", "300枚 → 15pt", new Vector2(210f, 46f), false, () =>
        {
            if (_busy) return;
            bool exchanged = Collection.ExchangeCoins();
            GameAudio.Play(exchanged ? Sfx.Catch : Sfx.Boing, 0.6f, 1.2f);
            RefreshGacha();
        }, 20);
        UiKit.Place(exchange.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 16f), new Vector2(210f, 46f));
        var sc = UiKit.Btn(_gacha, "Scout", "スカウト " + Collection.ScoutCost + "pt", new Vector2(210f, 46f), false, Scout, 20);
        UiKit.Place(sc.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 16f), new Vector2(210f, 46f));
        _gachaCoins = UiKit.Txt(_gacha, "Coins", "", 20, UiKit.GoldLight, TextAnchor.MiddleLeft);
        UiKit.Place(_gachaCoins.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 68f), new Vector2(210f, 28f));
        _gachaPoints = UiKit.Txt(_gacha, "Points", "", 20, Color.white, TextAnchor.MiddleRight);
        UiKit.Place(_gachaPoints.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 68f), new Vector2(210f, 28f));
    }

    private Text _gachaPoints, _gachaCoins;

    private void RefreshGacha()
    {
        int left = Gacha.PityLimit - Collection.PityCount;
        _gachaInfo.text = "★4以上確定まで あと " + left;
        if (_gachaCoins != null) _gachaCoins.text = "コイン " + Collection.Coins.ToString("N0") + "枚";
        if (_gachaPoints != null) _gachaPoints.text = Collection.Points.ToString("N0") + " pt";
    }

    // ------------------------------------------------------------
    // 画面切り替え
    // ------------------------------------------------------------
    private System.Collections.IEnumerator TrimSoon()
    {
        yield return null;
        CharaCatalog.TrimArt(true);
    }

    private void Only(RectTransform r)
    {
        // 画面を切り替えたら(隠した画面の画像を手放した次のフレームで)、上限を超えた画像をメモリから消す
        if (isActiveAndEnabled) StartCoroutine(TrimSoon());
        if (_title != null) _title.gameObject.SetActive(r == _title);
        _home.gameObject.SetActive(r == _home);
        _cards.gameObject.SetActive(r == _cards);
        _gacha.gameObject.SetActive(r == _gacha);
        _detail.gameObject.SetActive(false);
        _show.gameObject.SetActive(false);
        _fx.Clear();
    }

    public void ShowHome()
    {
        BgmPlayer.PlayBase(BgmPlayer.Home);
        Only(_home);
        RefreshCards();
        _homeCoins.text = "コイン " + Collection.Coins.ToString("N0") + "枚";
        _playLabel.text = _gameStarted ? "試合に戻る" : "試合";
        _menuBtn.SetActive(false);
        MenuOpen = true;
        AtBatController.InputBlocked = true;
    }

    public void ShowCards() { Only(_cards); RefreshCards(); BgmPlayer.PlayBase(BgmPlayer.Team); }
    public void ShowGacha() { Only(_gacha); RefreshGacha(); BgmPlayer.PlayBase(BgmPlayer.GachaScreen); }

    public void StartGame()
    {
        BgmPlayer.PlayBase(BgmPlayer.Match);
        Only(null);
        MenuOpen = false;
        _menuBtn.SetActive(true);
        Time.timeScale = _gameStarted ? _savedScale : 1f;
        _gameStarted = true;
        AtBatController.HoldStart = false;
        StartCoroutine(UnblockNextFrame());
    }

    private IEnumerator UnblockNextFrame()
    {
        yield return null;   // ボタンを押したクリックがスイングにならないように1フレーム待つ
        yield return null;
        AtBatController.InputBlocked = false;
    }

    public void OpenMenu()
    {
        _savedScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        ShowHome();
    }

    private static void ClearChildren(RectTransform r)
    {
        for (int i = r.childCount - 1; i >= 0; i--) Destroy(r.GetChild(i).gameObject);
    }

    // ------------------------------------------------------------
    // 毎フレームの飾り(放射光の回転・カードのきらめき・ホームの花火)
    // ------------------------------------------------------------
    private void Update()
    {
        float t = Time.unscaledTime;
        if (_homeRays != null) _homeRays.localRotation = Quaternion.Euler(0f, 0f, t * 6f);
        if (_titleRays != null) _titleRays.localRotation = Quaternion.Euler(0f, 0f, -t * 5f);
        TitleIdle(t);
        if (_gachaRays != null) _gachaRays.localRotation = Quaternion.Euler(0f, 0f, -t * 8f);
        if (_cards.gameObject.activeSelf)
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                var p = _cells[i].Value;
                if (p.Shine == null) continue;
                float u = Mathf.Repeat(t * 0.45f + i * 0.13f, 1.6f);
                float w = p.Root.sizeDelta.x;
                p.Shine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-w * 1.2f, w * 1.2f, u), 0f);
            }
        }
        if ((_home.gameObject.activeSelf || (_title != null && _title.gameObject.activeSelf)) && t > _nextFirework)
        {
            _nextFirework = t + UnityEngine.Random.Range(1.4f, 2.6f);
            Color c = UnityEngine.Random.value < 0.5f ? UiKit.Gold : new Color(1f, 0.3f, 0.2f);
            _fx.Firework(new Vector2(UnityEngine.Random.Range(-420f, 420f), -300f), UnityEngine.Random.Range(330f, 470f), c);
        }
    }

    private IEnumerator PopIn(RectTransform r, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float s = 1f + 0.12f * Mathf.Sin(u * Mathf.PI) ;
            r.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, u) * s, Mathf.Lerp(0.6f, 1f, u) * s, 1f);
            yield return null;
        }
        r.localScale = Vector3.one;
    }

    public static Color RevealColor(Rarity r)
    {
        switch (r)
        {
            case Rarity.R1: return new Color(0.85f, 0.88f, 0.95f);
            case Rarity.R2: return new Color(1f, 0.75f, 0.45f);
            case Rarity.R3: return new Color(0.45f, 0.70f, 1f);
            case Rarity.R4: return new Color(1f, 0.82f, 0.25f);
            case Rarity.R5: return new Color(1f, 0.25f, 0.18f);
            default: return new Color(1f, 0.55f, 0.95f);
        }
    }
}
