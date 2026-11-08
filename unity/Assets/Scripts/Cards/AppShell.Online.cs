using UnityEngine;
using UnityEngine.UI;

// ============================================================
// AppShell.Online.cs
// オンライン対戦: 自分の番号(自分の編成)を表示し、相手の番号を入れると、相手の編成と1試合戦う。
// ============================================================
public partial class AppShell
{
    private RectTransform _onlineScr;
    private Text _myCode, _onlineStatus;
    private InputField _codeInput;
    private Button _vsButton;
    private bool _onlineBusy;

    private void BuildOnline()
    {
        _onlineScr = MakeScreen("Online", 0.42f);
        UseMenuBackdrop(_onlineScr);
        Text title;
        TopBar(_onlineScr, "オンライン対戦", out title);
        var col = UiKit.Rect(_onlineScr, "Column");
        // 上の帯と下の帯のあいだの真ん中に置く(上は 64、下は NavReserve 空ける)
        UiKit.Place(col, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, (NavReserve - 64f) * 0.5f), new Vector2(460f, 290f));

        var l1 = UiKit.Txt(col, "MyLabel", "あなたの番号", 20, UiKit.GoldLight, TextAnchor.MiddleCenter);
        UiKit.Place(l1.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(400f, 26f));
        _myCode = UiKit.Txt(col, "MyCode", "------", 56, Color.white, TextAnchor.MiddleCenter);
        UiKit.Place(_myCode.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(440f, 66f));
        UiKit.Style(_myCode, TelopFx.Kind.Heading);

        var l2 = UiKit.Txt(col, "OppLabel", "相手の番号", 20, UiKit.GoldLight, TextAnchor.MiddleCenter);
        UiKit.Place(l2.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(400f, 26f));
        // 入力欄は上の帯・所持数の札と同じ紺の丸い札(白い四角だと浮くため)
        var field = UiKit.Img(col, "Field", HomeSkin.Pill, Color.white, true);
        UiKit.Place(field.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(300f, 56f));
        var txt = HomeSkin.NavText(UiKit.Txt(field.transform, "Text", "", 36, Color.white, TextAnchor.MiddleCenter, true, false));
        UiKit.Fill(txt.rectTransform, 6f);
        txt.supportRichText = false;
        var ph = UiKit.Txt(field.transform, "Placeholder", "000000", 36, new Color(0.55f, 0.62f, 0.95f, 0.45f), TextAnchor.MiddleCenter, false, false);
        UiKit.Fill(ph.rectTransform, 6f);
        _codeInput = field.gameObject.AddComponent<InputField>();
        _codeInput.textComponent = txt;
        _codeInput.placeholder = ph;
        _codeInput.characterLimit = 6;
        _codeInput.contentType = InputField.ContentType.IntegerNumber;
        _codeInput.customCaretColor = true;
        _codeInput.caretColor = Color.white;
        _codeInput.selectionColor = new Color(0.38f, 0.82f, 1f, 0.5f);
        if (WebPrompt.Available)
        {
            // ブラウザ版は入力欄を押すと標準の入力ダイアログを出す
            _codeInput.readOnly = true;
            var fb = field.gameObject.AddComponent<Button>();
            fb.transition = Selectable.Transition.None;
            fb.onClick.AddListener(() =>
            {
                string r = WebPrompt.Ask("相手の番号（6けた）", _codeInput.text);
                if (r == null) return;
                var sb = new System.Text.StringBuilder();
                foreach (char c in r) if (c >= '0' && c <= '9' && sb.Length < 6) sb.Append(c);
                else if (c >= '０' && c <= '９' && sb.Length < 6) sb.Append((char)('0' + (c - '０')));
                _codeInput.text = sb.ToString();
            });
        }

        _vsButton = UiKit.Btn(col, "Vs", "対戦する", new Vector2(300f, 60f), true, OnOnlineVs, 28);
        UiKit.Place(_vsButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -192f), new Vector2(300f, 60f));
        _onlineStatus = UiKit.Txt(col, "Status", "", 20, UiKit.GoldLight, TextAnchor.MiddleCenter);
        UiKit.Place(_onlineStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -258f), new Vector2(440f, 30f));
    }

    public void ShowOnline()
    {
        Only(_onlineScr);
        BgmPlayer.PlayBase(BgmPlayer.Team);
        var svc = OnlineService.I;
        string mine = OnlineService.MyCode;
        _myCode.text = string.IsNullOrEmpty(mine) ? "------" : mine;
        _onlineStatus.text = "";
        if (svc == null || !svc.Configured)
        {
            _onlineStatus.text = "サーバー未設定";
            _vsButton.interactable = false;
            return;
        }
        _vsButton.interactable = true;
        // 開くたびに、今の編成を自分の番号に保存する
        _onlineStatus.text = "通信中…";
        string nm = string.IsNullOrEmpty(PlayerName) ? "ななし" : PlayerName;
        StartCoroutine(svc.Publish(CharaRoster.UserTeam(), nm,
            code => { _myCode.text = code; if (!_onlineBusy) _onlineStatus.text = ""; },
            err => { if (!_onlineBusy) _onlineStatus.text = err; }));
    }

    private void OnOnlineVs()
    {
        var svc = OnlineService.I;
        if (svc == null || !svc.Configured || _onlineBusy) return;
        string code = (_codeInput.text ?? "").Trim();
        if (code.Length != 6) { _onlineStatus.text = "6けたの番号"; return; }
        if (code == OnlineService.MyCode) { _onlineStatus.text = "自分の番号です"; return; }
        _onlineBusy = true;
        _onlineStatus.text = "通信中…";
        StartCoroutine(svc.Fetch(code, doc =>
        {
            _onlineBusy = false;
            var team = OnlineService.ToTeam(doc);
            if (team == null) { _onlineStatus.text = "読めません"; return; }
            AtBatController.OnlineOpp = team;
            AtBatController.OnlineOppName = (string.IsNullOrEmpty(doc.name) ? "相手" : doc.name) + "のチーム";
            int mark; int.TryParse(code, out mark);
            AtBatController.OnlineOppMark = mark;
            bool restart = _gameStarted;
            if (restart) CoverFade();
            StartGame();
            if (restart && AtBatController.Current != null) AtBatController.Current.RestartMatch();
        }, err => { _onlineBusy = false; _onlineStatus.text = err; }));
    }
}
