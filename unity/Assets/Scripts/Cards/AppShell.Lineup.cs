using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// AppShell.Lineup.cs
// 編成: 左に投手と打順(1〜9番、それぞれの守備位置つき)、右にその守備位置の選手だけ。
//   行を押すと、その位置に入れられる選手だけが右に並ぶ。右の選手を押すとその位置に入る
//   (同じ守備の別の枠にいる選手なら入れ替わる)。打順は行の ▲▼ で入れ替える。変えるたびに保存する。
// ============================================================
public partial class AppShell
{
    private RectTransform _lineup, _lineupRows, _lineupPool;
    private Text _lineupTitle, _poolTitle;
    private TeamSetup.Lineup _edit;
    private int _selRow = 1;   // 0=投手、1〜9=打順
    private readonly List<Image> _rowBg = new List<Image>(), _rowHead = new List<Image>();
    private readonly List<Text> _rowNo = new List<Text>(), _rowPos = new List<Text>(), _rowName = new List<Text>();
    private readonly List<KeyValuePair<CharaDef, Image>> _poolCells = new List<KeyValuePair<CharaDef, Image>>();
    private readonly List<Text> _poolLabels = new List<Text>();

    private static readonly Color RowColor = new Color(0.04f, 0.06f, 0.15f, 0.88f);
    private static readonly Color RowSel = new Color(0.62f, 0.42f, 0.06f, 0.95f);
    private static readonly Color PosChip = new Color(0.70f, 0.08f, 0.10f, 1f);

    private void BuildLineup()
    {
        _lineup = MakeScreen("Lineup", 0.42f);
        UseMenuBackdrop(_lineup);
        TopBar(_lineup, "編成", out _lineupTitle);

        _lineupRows = UiKit.Rect(_lineup, "Rows");
        _lineupRows.anchorMin = new Vector2(0f, 0f); _lineupRows.anchorMax = new Vector2(0f, 1f);
        _lineupRows.pivot = new Vector2(0f, 1f);
        _lineupRows.offsetMin = new Vector2(14f, NavReserve); _lineupRows.offsetMax = new Vector2(14f + 360f, -70f);
        var vl = _lineupRows.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 2f; vl.childControlHeight = true; vl.childControlWidth = true; vl.childForceExpandHeight = true;
        for (int i = 0; i < 10; i++) BuildRow(i);

        // 右: 見出し(どの位置の選手か)と、選手の一覧(スクロール)
        _poolTitle = UiKit.Txt(_lineup, "PoolTitle", "", 20, UiKit.GoldLight, TextAnchor.MiddleLeft);
        var pt = _poolTitle.rectTransform;
        pt.anchorMin = new Vector2(0f, 1f); pt.anchorMax = new Vector2(1f, 1f); pt.pivot = new Vector2(0f, 1f);
        pt.offsetMin = new Vector2(14f + 360f + 18f, -100f); pt.offsetMax = new Vector2(-16f - 130f, -70f);
        // 選んでいる選手のフォームを作る
        var fb = UiKit.Btn(_lineup, "FormButton", "フォーム", new Vector2(124f, 34f), true, OpenForm, 18);
        UiKit.Place(fb.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -68f), new Vector2(124f, 34f));
        var view = UiKit.Rect(_lineup, "Pool");
        view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one;
        view.offsetMin = new Vector2(14f + 360f + 16f, NavReserve); view.offsetMax = new Vector2(-16f, -104f);
        view.gameObject.AddComponent<RectMask2D>();
        var hit = view.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        _lineupPool = UiKit.Rect(view, "Content");
        _lineupPool.anchorMin = new Vector2(0f, 1f); _lineupPool.anchorMax = new Vector2(1f, 1f);
        _lineupPool.pivot = new Vector2(0.5f, 1f);
        _lineupPool.anchoredPosition = Vector2.zero; _lineupPool.sizeDelta = Vector2.zero;
        var grid = _lineupPool.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(92f, 120f);
        grid.spacing = new Vector2(8f, 8f);
        grid.padding = new RectOffset(4, 4, 4, 12);
        var fit = _lineupPool.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = view.gameObject.AddComponent<ScrollRect>();
        sr.content = _lineupPool; sr.viewport = view; sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Elastic; sr.scrollSensitivity = 30f;
    }

    private void BuildRow(int i)
    {
        int idx = i;
        var row = UiKit.Img(_lineupRows, "Row" + i, SpriteFactory.White, RowColor, true);
        var b = row.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => OnRow(idx));
        // 打順(投手の行は空)
        var no = UiKit.Txt(row.transform, "No", "", 20, UiKit.GoldLight, TextAnchor.MiddleCenter);
        Anchor(no.rectTransform, 2f, 26f);
        // 守備位置(赤い札に1文字)
        var chip = UiKit.Img(row.transform, "Pos", SpriteFactory.White, PosChip);
        chip.raycastTarget = false;
        var cr = chip.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(0f, 0.5f); cr.pivot = new Vector2(0f, 0.5f);
        cr.anchoredPosition = new Vector2(30f, 0f); cr.sizeDelta = new Vector2(28f, 24f);
        var pos = UiKit.Txt(chip.transform, "T", "", 18, Color.white, TextAnchor.MiddleCenter);
        UiKit.Fill(pos.rectTransform);
        var head = UiKit.Img(row.transform, "Head", null, Color.white);
        head.preserveAspect = true; head.raycastTarget = false;
        var hr = head.rectTransform;
        hr.anchorMin = hr.anchorMax = new Vector2(0f, 0.5f); hr.pivot = new Vector2(0f, 0.5f);
        hr.anchoredPosition = new Vector2(66f, 0f); hr.sizeDelta = new Vector2(30f, 30f);
        var name = UiKit.Txt(row.transform, "Name", "", 18, Color.white, TextAnchor.MiddleLeft);
        name.rectTransform.anchorMin = new Vector2(0f, 0f); name.rectTransform.anchorMax = new Vector2(1f, 1f);
        name.rectTransform.offsetMin = new Vector2(102f, 0f); name.rectTransform.offsetMax = new Vector2(-66f, 0f);
        if (i > 0)
        {
            // 打順の入れ替え(▲▼)
            var up = UiKit.Btn(row.transform, "Up", "▲", new Vector2(28f, 24f), false, () => MoveOrder(idx, -1), 14);
            UiKit.Place(up.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-34f, 0f), new Vector2(28f, 24f));
            var dn = UiKit.Btn(row.transform, "Down", "▼", new Vector2(28f, 24f), false, () => MoveOrder(idx, 1), 14);
            UiKit.Place(dn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(28f, 24f));
        }
        _rowBg.Add(row); _rowNo.Add(no); _rowPos.Add(pos); _rowHead.Add(head); _rowName.Add(name);
    }

    private static void Anchor(RectTransform r, float left, float width)
    {
        r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 0.5f);
        r.offsetMin = new Vector2(left, 0f); r.offsetMax = new Vector2(left + width, 0f);
    }

    public void ShowLineup()
    {
        Only(_lineup);
        BgmPlayer.PlayBase(BgmPlayer.Team);
        _edit = TeamSetup.Load();
        _selRow = 1;
        RefreshLineup();
        RebuildPool();
    }

    private Slot SelSlot { get { return _selRow == 0 ? Slot.P : _edit.Order[_selRow - 1].S; } }
    private CharaDef RowDef(int i) { return i == 0 ? _edit.Pitcher : _edit.Order[i - 1].D; }

    private void RebuildPool()
    {
        ClearChildren(_lineupPool);
        _poolCells.Clear(); _poolLabels.Clear();
        Slot s = SelSlot;
        _poolTitle.text = TeamSetup.SlotNames[(int)s] + "（" + TeamSetup.PositionOf(s) + "）";
        foreach (var d in TeamSetup.Candidates(s))
        {
            var def = d;
            var cell = UiKit.Img(_lineupPool, "C_" + d.Id, SpriteFactory.White, new Color(0f, 0f, 0f, 0f), true);
            var b = cell.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => OnPool(def));
            var th = UiKit.Img(cell.transform, "Thumb", CharaCatalog.Thumb(d.Id) ?? CharaCatalog.Head(d.Id), Color.white);
            th.preserveAspect = true; th.raycastTarget = false;
            th.rectTransform.anchorMin = new Vector2(0f, 1f); th.rectTransform.anchorMax = new Vector2(1f, 1f);
            th.rectTransform.pivot = new Vector2(0.5f, 1f);
            th.rectTransform.anchoredPosition = Vector2.zero; th.rectTransform.sizeDelta = new Vector2(0f, 88f);
            var lb = UiKit.Txt(cell.transform, "Label", "", 13, Color.white, TextAnchor.MiddleCenter);
            lb.rectTransform.anchorMin = new Vector2(0f, 0f); lb.rectTransform.anchorMax = new Vector2(1f, 0f);
            lb.rectTransform.pivot = new Vector2(0.5f, 0f);
            lb.rectTransform.anchoredPosition = Vector2.zero; lb.rectTransform.sizeDelta = new Vector2(0f, 30f);
            _poolCells.Add(new KeyValuePair<CharaDef, Image>(d, th));
            _poolLabels.Add(lb);
        }
        RefreshPoolMarks();
    }

    private void RefreshLineup()
    {
        for (int i = 0; i < 10; i++)
        {
            var d = RowDef(i);
            Slot s = i == 0 ? Slot.P : _edit.Order[i - 1].S;
            _rowBg[i].color = i == _selRow ? RowSel : RowColor;
            _rowNo[i].text = i == 0 ? "" : i.ToString();
            _rowPos[i].text = TeamSetup.SlotShort[(int)s];
            _rowHead[i].sprite = d != null ? CharaCatalog.Head(d.Id) : null;
            _rowHead[i].enabled = _rowHead[i].sprite != null;
            if (d != null) UiKit.FitName(_rowName[i], d.Name, 170f, 36f, 18, 11); else _rowName[i].text = "";
        }
        RefreshPoolMarks();
    }

    // 右の一覧: 今の編成にいる選手は暗くして、何番(投手)かを出す
    private void RefreshPoolMarks()
    {
        for (int k = 0; k < _poolCells.Count; k++)
        {
            var d = _poolCells[k].Key;
            string where = null;
            if (_edit.Pitcher == d) where = "投手";
            for (int j = 0; j < _edit.Order.Count; j++) if (_edit.Order[j].D == d) where = (j + 1) + "番 " + TeamSetup.SlotNames[(int)_edit.Order[j].S];
            _poolCells[k].Value.color = where != null ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
            _poolLabels[k].text = where ?? d.Name;
            _poolLabels[k].color = where != null ? UiKit.GoldLight : Color.white;
        }
    }

    private void OnRow(int i)
    {
        GameAudio.Play(Sfx.Click, 0.5f, 1.3f);
        bool slotChanged = _selRow != i;
        _selRow = i;
        RefreshLineup();
        if (slotChanged) RebuildPool();
    }

    private void OnPool(CharaDef d)
    {
        GameAudio.Play(Sfx.Click, 0.5f, 1.4f);
        if (_selRow == 0) _edit.Pitcher = d;
        else
        {
            var e = _edit.Order[_selRow - 1];
            int j = _edit.Order.FindIndex(x => x.D == d);
            if (j >= 0) { var tmp = _edit.Order[j].D; _edit.Order[j].D = e.D; e.D = tmp; }   // 同じ守備の別の枠の選手と入れ替え
            else e.D = d;
        }
        TeamSetup.Save(_edit);
        RefreshLineup();
    }

    // 打順を上下に1つ動かす(守備位置は選手について動く)
    private void MoveOrder(int row, int dir)
    {
        int a = row - 1, b = a + dir;
        if (b < 0 || b >= _edit.Order.Count) return;
        var tmp = _edit.Order[a]; _edit.Order[a] = _edit.Order[b]; _edit.Order[b] = tmp;
        if (_selRow == row) _selRow = b + 1;
        TeamSetup.Save(_edit);
        RefreshLineup();
    }
}
