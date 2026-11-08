using UnityEngine;
using UnityEngine.UI;

// ============================================================
// AppShell.Home.cs
// ホーム画面: 上の帯(ロゴ・名前・所持数)、左にキャラ、右にバナー2枚、
// 下の帯(メニュー + まん中の大きな「試合」ボタン)。背景は球場。
// 下の帯はホーム・編成・カード・ガチャ・オンラインで共通(今いる画面のタブを水色にする)。
// ============================================================
public partial class AppShell
{
    private KvChara _homeChara;
    private Text _playLabel, _homeCoins, _homePoints, _homeOwned, _homeName, _gachaBadgeText, _onlineSub;
    private RectTransform _homeShadow, _homeNamePill, _gachaBadge, _battleBtn, _battleShine;
    private readonly RectTransform[] _banners = new RectTransform[2];
    private readonly RectTransform[] _bannerArt = new RectTransform[2];
    private RectTransform _vsMe, _vsRival, _vsText;
    private readonly RectTransform[] _tabs = new RectTransform[5];
    private readonly GameObject[] _tabOn = new GameObject[5];
    private RectTransform _navLayer;
    private int _navIdx = -1;
    private const float BarH = 64f, NavH = 80f, Edge = 10f, BattleW = 280f;
    // 下の帯(と、はみ出す試合ボタン)の分だけ、各画面の中身を上に空ける高さ
    private const float NavReserve = 8f + NavH + 18f;

    private void BuildHome()
    {
        _home = MakeScreen("Home", 1f);
        UseStadiumBackdrop(_home);
        _homeShadow = UiKit.Img(_home, "CharaShadow", HomeSkin.Shadow, new Color(0f, 0f, 0.05f, 0.55f)).rectTransform;
        _homeShadow.anchorMin = _homeShadow.anchorMax = Vector2.zero;
        _homeChara = AddChara(_home, "ojisan_swing_ready", true);
        BuildHomeBanners();
        BuildHomeTopBar();
        BuildHomeNav();
    }

    // ---- 上の帯 ----
    private void BuildHomeTopBar()
    {
        var bar = UiKit.Img(_home, "TopBar", HomeSkin.Bar, Color.white).rectTransform;
        bar.anchorMin = new Vector2(0f, 1f); bar.anchorMax = new Vector2(1f, 1f); bar.pivot = new Vector2(0.5f, 1f);
        bar.offsetMin = new Vector2(Edge, -8f - BarH); bar.offsetMax = new Vector2(-Edge, -8f);

        var logoSp = Resources.Load<Sprite>("Presentation/title_logo");
        if (logoSp != null)
        {
            var lg = UiKit.Img(bar, "Logo", logoSp, Color.white);
            lg.preserveAspect = true;
            UiKit.Place(lg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, -2f), new Vector2(184f, 78f));
        }

        var np = UiKit.Img(bar, "NamePill", HomeSkin.Pill, Color.white);
        _homeNamePill = np.rectTransform;
        UiKit.Place(_homeNamePill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(206f, 0f), new Vector2(200f, 44f));
        _homeName = HomeSkin.NavText(UiKit.Txt(_homeNamePill, "Name", "", 22, Color.white, TextAnchor.MiddleCenter));
        UiKit.Fill(_homeName.rectTransform, 6f);

        RectTransform tmp;
        _homeCoins = HomePill(bar, "icon_coin", 12f, 190f, out tmp);
        _homePoints = HomePill(bar, "icon_point", 212f, 150f, out tmp);
        _homeOwned = HomePill(bar, "icon_nav_cards", 372f, 170f, out tmp);
    }

    private Text HomePill(RectTransform bar, string icon, float right, float w, out RectTransform rt)
    {
        var p = UiKit.Img(bar, "Pill_" + icon, HomeSkin.Pill, Color.white);
        rt = p.rectTransform;
        UiKit.Place(rt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-right, 0f), new Vector2(w, 44f));
        Icon(rt, icon, new Vector2(0f, 0.5f), new Vector2(20f, 0f), 42f);
        var t = HomeSkin.NavText(UiKit.Txt(rt, "Value", "", 24, Color.white, TextAnchor.MiddleRight));
        UiKit.Place(t.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(w - 56f, 40f));
        return t;
    }

    // ---- 右のバナー(ガチャ・オンライン対戦) ----
    private void BuildHomeBanners()
    {
        Text sub;
        _banners[0] = MakeBanner("GachaBanner", new Color(0.98f, 0.36f, 0.62f), new Color(0.45f, 0.18f, 0.86f), new Color(1f, 0.86f, 0.55f), 11,
            "ガチャ", new Color(0.55f, 0.02f, 0.30f), "引く", ShowGacha, out _bannerArt[0], out sub);
        sub.text = "1回 " + Collection.PullCoinCost + "枚";
        var ms = Resources.Load<Sprite>("Presentation/gacha_machine");
        if (ms != null)
        {
            var m = UiKit.Img(_bannerArt[0], "Machine", ms, Color.white);
            m.preserveAspect = true;
            UiKit.Fill(m.rectTransform, 8f);
        }

        _banners[1] = MakeBanner("OnlineBanner", new Color(0.10f, 0.62f, 0.98f), new Color(0.16f, 0.16f, 0.62f), new Color(0.62f, 0.95f, 1f), 23,
            "オンライン対戦", new Color(0.04f, 0.16f, 0.46f), "対戦する", ShowOnline, out _bannerArt[1], out _onlineSub);
        _bannerArt[1].anchorMin = new Vector2(0.54f, 0f); _bannerArt[1].anchorMax = new Vector2(1f, 0.70f);
        _vsMe = Emblem(_bannerArt[1], "team_me");
        _vsRival = Emblem(_bannerArt[1], "team_konnyaku");
        var vs = HomeSkin.Title(UiKit.Txt(_bannerArt[1], "VS", "VS", 30, Color.white, TextAnchor.MiddleCenter),
            new Color(0.62f, 0.04f, 0.10f), new Color(1f, 0.85f, 0.25f));
        _vsText = vs.rectTransform;
        _vsText.sizeDelta = new Vector2(60f, 40f);
    }

    private RectTransform Emblem(RectTransform parent, string name)
    {
        var sp = Resources.Load<Sprite>("Presentation/" + name);
        var img = UiKit.Img(parent, name, sp, Color.white);
        img.preserveAspect = true;
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        return img.rectTransform;
    }

    private RectTransform MakeBanner(string name, Color a, Color b, Color glow, int seed, string title, Color titleInner,
        string cta, System.Action act, out RectTransform art, out Text sub)
    {
        var img = UiKit.Img(_home, name, HomeSkin.Banner(a, b, glow, seed), Color.white, true);
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(1f, 0f);
        r.pivot = new Vector2(1f, 0f);
        var btn = img.gameObject.AddComponent<Button>();
        var cb = btn.colors; cb.pressedColor = new Color(0.85f, 0.85f, 0.9f); btn.colors = cb;
        btn.onClick.AddListener(() => { GameAudio.Play(Sfx.Click, 0.6f, 1.2f); act(); });

        art = UiKit.Rect(r, "Art");
        art.anchorMin = new Vector2(0.52f, 0f); art.anchorMax = new Vector2(1f, 1f);
        art.offsetMin = new Vector2(0f, 6f); art.offsetMax = new Vector2(-10f, -6f);

        var tt = HomeSkin.Title(UiKit.Txt(r, "Title", title, 30, Color.white, TextAnchor.UpperLeft), titleInner, new Color(1f, 0.92f, 0.45f));
        UiKit.Place(tt.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -14f), new Vector2(300f, 40f));
        sub = HomeSkin.NavText(UiKit.Txt(r, "Sub", "", 19, Color.white, TextAnchor.MiddleLeft));
        UiKit.Place(sub.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 4f), new Vector2(230f, 28f));

        var c = UiKit.Img(r, "Cta", HomeSkin.Cta, Color.white);
        UiKit.Place(c.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 14f), new Vector2(150f, 42f));
        var ct = HomeSkin.NavText(UiKit.Txt(c.rectTransform, "Label", cta, 22, Color.white, TextAnchor.MiddleCenter));
        UiKit.Fill(ct.rectTransform);
        return r;
    }

    // ---- 下の帯(メニュー + 試合ボタン) ----
    private void BuildHomeNav()
    {
        // 帯は画面とは別の層に置き、ホーム以外のメニュー画面でも出す(Build で画面より手前へ)
        _navLayer = UiKit.Fill(UiKit.Rect(_root, "NavLayer"));
        var nav = UiKit.Img(_navLayer, "Nav", HomeSkin.Bar, Color.white).rectTransform;
        nav.anchorMin = Vector2.zero; nav.anchorMax = new Vector2(1f, 0f); nav.pivot = new Vector2(0.5f, 0f);
        nav.offsetMin = new Vector2(Edge, 8f); nav.offsetMax = new Vector2(-Edge, 8f + NavH);

        string[] labels = { "ホーム", "編成", "カード", "ガチャ", "オンライン" };
        string[] icons = { "icon_nav_home", "icon_nav_team", "icon_nav_cards", "icon_nav_gacha", "icon_nav_online" };
        System.Action[] acts = { ShowHome, ShowLineup, ShowCards, ShowGacha, ShowOnline };
        for (int i = 0; i < labels.Length; i++)
        {
            var hit = UiKit.Img(nav, "Tab" + i, SpriteFactory.White, new Color(1f, 1f, 1f, 0f), true);
            var r = hit.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            var on = UiKit.Img(r, "On", HomeSkin.TabOn, Color.white);
            UiKit.Fill(on.rectTransform, 2f);
            on.gameObject.SetActive(i == 0);
            _tabOn[i] = on.gameObject;
            var ic = Icon(r, icons[i], new Vector2(0.5f, 1f), new Vector2(0f, -26f), 46f);
            if (ic != null) ic.raycastTarget = false;
            var t = HomeSkin.NavText(UiKit.Txt(r, "Label", labels[i], 18, Color.white, TextAnchor.MiddleCenter));
            UiKit.Place(t.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(140f, 24f));
            var act = acts[i];
            int idx = i;
            var b = hit.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            var rr = r;
            b.onClick.AddListener(() =>
            {
                if (_busy || idx == _navIdx) return;   // ガチャの演出中・今いる画面のタブは何もしない
                GameAudio.Play(Sfx.Click, 0.6f, 1.2f); StartCoroutine(PopIn(rr, 0.18f)); act();
            });
            _tabs[i] = r;
        }
        // ガチャを何回引けるか(赤い丸)
        var badge = UiKit.Img(_tabs[3], "Badge", HomeSkin.Badge, Color.white);
        _gachaBadge = badge.rectTransform;
        UiKit.Place(_gachaBadge, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(28f, -10f), new Vector2(32f, 32f));
        _gachaBadgeText = HomeSkin.NavText(UiKit.Txt(_gachaBadge, "N", "", 17, Color.white, TextAnchor.MiddleCenter), 1.6f);
        UiKit.Fill(_gachaBadgeText.rectTransform);

        // 試合ボタン(帯から少しはみ出す)
        var bb = UiKit.Img(_navLayer, "Battle", HomeSkin.Orange, Color.white, true);
        _battleBtn = bb.rectTransform;
        UiKit.Place(_battleBtn, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f + NavH * 0.5f + 4f), new Vector2(BattleW, 92f));
        var btn = bb.gameObject.AddComponent<Button>();
        var cb = btn.colors; cb.pressedColor = new Color(0.85f, 0.75f, 0.65f); btn.colors = cb;
        btn.onClick.AddListener(() => { if (_busy) return; GameAudio.Play(Sfx.Click, 0.6f, 1.2f); StartGame(); });
        var mask = UiKit.Rect(_battleBtn, "ShineMask");
        UiKit.Fill(mask, 7f);
        mask.gameObject.AddComponent<RectMask2D>();
        var sh = UiKit.Img(mask, "Shine", UiKit.Shine, new Color(1f, 1f, 1f, 0.9f));
        _battleShine = sh.rectTransform;
        _battleShine.anchorMin = _battleShine.anchorMax = new Vector2(0.5f, 0.5f);
        _battleShine.sizeDelta = new Vector2(70f, 160f);
        _battleShine.localRotation = Quaternion.Euler(0f, 0f, -22f);
        Icon(_battleBtn, "icon_nav_ball", new Vector2(0f, 0.5f), new Vector2(52f, 0f), 62f);
        _playLabel = HomeSkin.Title(UiKit.Txt(_battleBtn, "Label", "試合", 42, Color.white, TextAnchor.MiddleCenter),
            new Color(0.50f, 0.12f, 0f), new Color(1f, 0.88f, 0.30f), 5f);
        UiKit.Place(_playLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, 2f), new Vector2(200f, 60f));
    }

    // 下の帯: ホームとメニュー画面だけに出し、今いる画面のタブを水色にする
    private void RefreshNav(RectTransform screen)
    {
        if (_navLayer == null) return;
        int idx = screen == null ? -1
            : screen == _home ? 0 : screen == _lineup ? 1 : screen == _cards ? 2 : screen == _gacha ? 3 : screen == _onlineScr ? 4 : -1;
        _navIdx = idx;
        _navLayer.gameObject.SetActive(idx >= 0);
        for (int i = 0; i < _tabOn.Length; i++)
            if (_tabOn[i] != null) _tabOn[i].SetActive(i == idx);
        if (idx >= 0) RefreshNavBadge();
    }

    // ガチャを何回引けるか(赤い丸)
    private void RefreshNavBadge()
    {
        if (_gachaBadge == null) return;
        int pulls = Collection.Coins / Collection.PullCoinCost + Collection.Points / Collection.ScoutCost;
        _gachaBadge.gameObject.SetActive(pulls > 0);
        _gachaBadgeText.text = pulls > 99 ? "99+" : pulls.ToString();
        _gachaBadge.sizeDelta = new Vector2(pulls > 9 ? 44f : 32f, 32f);
    }

    // 所持数・名前・バッジを今の値にする
    private void RefreshHome()
    {
        _homeCoins.text = Collection.Coins.ToString("N0");
        _homePoints.text = Collection.Points.ToString("N0");
        _homeOwned.text = Collection.OwnedKinds() + "/" + CharaCatalog.All.Count;
        _homeName.text = string.IsNullOrEmpty(PlayerName) ? "ななし" : PlayerName;
        RefreshNavBadge();
        string code = OnlineService.MyCode;
        _onlineSub.text = string.IsNullOrEmpty(code) ? "" : "マイコード " + code;
        _playLabel.text = _gameStarted ? "試合に戻る" : "試合";
        _playLabel.fontSize = _gameStarted ? 34 : 42;
    }

    private void LayoutHome(float t)
    {
        if (_home == null || !_home.gameObject.activeSelf) return;
        var size = _root.rect.size;
        float top = size.y - (8f + BarH + 12f);
        float bottom = 8f + NavH + 14f;
        float availH = top - bottom;

        // バナー: 右に縦2枚
        float gap = 12f;
        float bh = (availH - gap) * 0.5f;
        float bw = Mathf.Min(bh * 2.25f, size.x * 0.44f);
        for (int i = 0; i < 2; i++)
        {
            _banners[i].sizeDelta = new Vector2(bw, bh);
            _banners[i].anchoredPosition = new Vector2(-Edge - 6f, bottom + (1 - i) * (bh + gap));
        }
        // VS の並び
        float artW = bw * 0.46f - 10f, artH = bh * 0.70f - 12f;
        float em = Mathf.Min(artW * 0.38f, artH * 0.9f);
        _vsMe.sizeDelta = _vsRival.sizeDelta = new Vector2(em, em);
        _vsMe.anchoredPosition = new Vector2(-artW * 0.5f + em * 0.5f, 0f);
        _vsRival.anchoredPosition = new Vector2(artW * 0.5f - em * 0.5f, 0f);
        _vsText.anchoredPosition = Vector2.zero;

        // 上の帯: 狭い画面では名前を出さない
        _homeNamePill.gameObject.SetActive(size.x - 2f * Edge >= 980f);

        LayoutHomeChara(size, availH, bottom, bw, t);
    }

    private void LayoutNav(float t)
    {
        if (_navLayer == null || !_navLayer.gameObject.activeSelf) return;
        var size = _root.rect.size;
        // 下の帯: 左に3つ、右に2つ
        float navW = size.x - 2f * Edge;
        float side = (navW - BattleW) * 0.5f - 12f;
        float tw = side / 3f;
        for (int i = 0; i < 5; i++)
        {
            float x = i < 3
                ? -navW * 0.5f + 8f + tw * (i + 0.5f)
                : BattleW * 0.5f + 4f + side * ((i - 3) + 0.5f) / 2f;
            _tabs[i].anchoredPosition = new Vector2(x, 0f);
            _tabs[i].sizeDelta = new Vector2(Mathf.Min(tw, 150f) - 6f, NavH - 8f);
        }

        // 試合ボタン: ゆっくり脈打ち、光が横切る
        float pulse = 1f + 0.025f * Mathf.Sin(t * 4f);
        _battleBtn.localScale = new Vector3(pulse, pulse, 1f);
        float u = Mathf.Repeat(t * 0.55f, 1.8f);
        _battleShine.anchoredPosition = new Vector2(Mathf.Lerp(-BattleW * 0.75f, BattleW * 0.75f, Mathf.Clamp01(u)), 0f);
    }

    private void LayoutHomeChara(Vector2 size, float availH, float bottom, float bw, float t)
    {
        // キャラ: 左の空いたところに、帯と重ならない大きさで
        float freeL = Edge + 6f, freeR = size.x - Edge - 6f - bw - 16f;
        float freeW = Mathf.Max(80f, freeR - freeL);
        float frac = availH / size.y;
        float w = LayoutChara(_homeChara, size.y, frac, freeL, bottom);
        if (w > freeW && w > 0f) { frac *= freeW / w; w = LayoutChara(_homeChara, size.y, frac, freeL, bottom); }
        float cx = freeL + (freeW - w) * 0.5f;
        LayoutChara(_homeChara, size.y, frac, cx, bottom);
        _homeShadow.sizeDelta = new Vector2(w * 1.1f, Mathf.Max(18f, w * 0.16f));
        _homeShadow.anchoredPosition = new Vector2(cx + w * 0.5f, bottom + 2f);
        // 待機の動き: 呼吸(縦に少し伸び縮み)と、ゆっくりした左右の揺れ
        if (_homeChara != null)
        {
            float br = Mathf.Sin(t * 2.2f);
            _homeChara.Rt.localScale = new Vector3(1f - 0.006f * br, 1f + 0.016f * br, 1f);
            _homeChara.Rt.localRotation = Quaternion.Euler(0f, 0f, 0.7f * Mathf.Sin(t * 1.15f));
            _homeShadow.localScale = new Vector3(1f + 0.03f * Mathf.Sin(t * 1.15f), 1f, 1f);
        }
    }
}
