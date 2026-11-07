using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// AppShell.Lineup.cs
// 編成: 左に打順(1〜9番)、右に選べる選手。
//   打順の行を押して選び、右の選手を押すとその打順に入る(すでに打線にいる選手なら入れ替わる)。
//   打順の行を2つ続けて押すと、打順を入れ替える。変えるたびに保存する。
// ============================================================
public partial class AppShell
{
    private RectTransform _lineup, _lineupRows, _lineupPool;
    private readonly List<CharaDef> _edit = new List<CharaDef>();
    private int _selRow = -1;
    private readonly List<Image> _rowBg = new List<Image>();
    private readonly List<Image> _rowHead = new List<Image>();
    private readonly List<Text> _rowName = new List<Text>(), _rowPos = new List<Text>();
    private readonly List<KeyValuePair<CharaDef, Image>> _poolCells = new List<KeyValuePair<CharaDef, Image>>();
    private readonly List<Text> _poolLabels = new List<Text>();
    private Text _lineupTitle;

    private static readonly Color RowColor = new Color(0.10f, 0.05f, 0.08f, 0.88f);
    private static readonly Color RowSel = new Color(0.62f, 0.42f, 0.06f, 0.95f);

    private void BuildLineup()
    {
        _lineup = MakeScreen("Lineup", 0.42f);
        TopBar(_lineup, "編成", out _lineupTitle);

        _lineupRows = UiKit.Rect(_lineup, "Rows");
        _lineupRows.anchorMin = new Vector2(0f, 0f); _lineupRows.anchorMax = new Vector2(0f, 1f);
        _lineupRows.pivot = new Vector2(0f, 1f);
        _lineupRows.offsetMin = new Vector2(16f, 10f); _lineupRows.offsetMax = new Vector2(16f + 330f, -72f);
        var vl = _lineupRows.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 4f; vl.childControlHeight = true; vl.childControlWidth = true; vl.childForceExpandHeight = true;
        for (int i = 0; i < 9; i++)
        {
            int idx = i;
            var row = UiKit.Img(_lineupRows, "Row" + i, SpriteFactory.White, RowColor, true);
            var b = row.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => OnRow(idx));
            var num = UiKit.Txt(row.transform, "No", (i + 1).ToString(), 22, UiKit.GoldLight, TextAnchor.MiddleCenter);
            num.rectTransform.anchorMin = new Vector2(0f, 0f); num.rectTransform.anchorMax = new Vector2(0f, 1f);
            num.rectTransform.pivot = new Vector2(0f, 0.5f);
            num.rectTransform.offsetMin = new Vector2(4f, 0f); num.rectTransform.offsetMax = new Vector2(34f, 0f);
            var head = UiKit.Img(row.transform, "Head", null, Color.white);
            head.preserveAspect = true; head.raycastTarget = false;
            head.rectTransform.anchorMin = new Vector2(0f, 0.5f); head.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            head.rectTransform.pivot = new Vector2(0f, 0.5f);
            head.rectTransform.anchoredPosition = new Vector2(38f, 0f);
            head.rectTransform.sizeDelta = new Vector2(40f, 40f);
            var name = UiKit.Txt(row.transform, "Name", "", 19, Color.white, TextAnchor.MiddleLeft);
            name.rectTransform.anchorMin = new Vector2(0f, 0f); name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.offsetMin = new Vector2(86f, 0f); name.rectTransform.offsetMax = new Vector2(-70f, 0f);
            var pos = UiKit.Txt(row.transform, "Pos", "", 15, UiKit.GoldLight, TextAnchor.MiddleRight);
            pos.rectTransform.anchorMin = new Vector2(1f, 0f); pos.rectTransform.anchorMax = new Vector2(1f, 1f);
            pos.rectTransform.pivot = new Vector2(1f, 0.5f);
            pos.rectTransform.offsetMin = new Vector2(-70f, 0f); pos.rectTransform.offsetMax = new Vector2(-8f, 0f);
            _rowBg.Add(row); _rowHead.Add(head); _rowName.Add(name); _rowPos.Add(pos);
        }

        // 右: 選べる選手(スクロール)
        var view = UiKit.Rect(_lineup, "Pool");
        view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one;
        view.offsetMin = new Vector2(16f + 330f + 16f, 8f); view.offsetMax = new Vector2(-16f, -70f);
        view.gameObject.AddComponent<RectMask2D>();
        var hit = view.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        _lineupPool = UiKit.Rect(view, "Content");
        _lineupPool.anchorMin = new Vector2(0f, 1f); _lineupPool.anchorMax = new Vector2(1f, 1f);
        _lineupPool.pivot = new Vector2(0.5f, 1f);
        _lineupPool.anchoredPosition = Vector2.zero; _lineupPool.sizeDelta = Vector2.zero;
        var grid = _lineupPool.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(92f, 118f);
        grid.spacing = new Vector2(8f, 8f);
        grid.padding = new RectOffset(4, 4, 4, 12);
        var fit = _lineupPool.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = view.gameObject.AddComponent<ScrollRect>();
        sr.content = _lineupPool; sr.viewport = view; sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Elastic; sr.scrollSensitivity = 30f;
    }

    public void ShowLineup()
    {
        Only(_lineup);
        BgmPlayer.PlayBase(BgmPlayer.Team);
        _edit.Clear();
        _edit.AddRange(CharaRoster.UserTeam().Lineup);
        _selRow = -1;
        RebuildPool();
        RefreshLineup();
    }

    private void RebuildPool()
    {
        ClearChildren(_lineupPool);
        _poolCells.Clear(); _poolLabels.Clear();
        foreach (var d in TeamSetup.Candidates())
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
            lb.rectTransform.anchoredPosition = Vector2.zero; lb.rectTransform.sizeDelta = new Vector2(0f, 28f);
            _poolCells.Add(new KeyValuePair<CharaDef, Image>(d, th));
            _poolLabels.Add(lb);
        }
    }

    private void RefreshLineup()
    {
        for (int i = 0; i < 9; i++)
        {
            var d = i < _edit.Count ? _edit[i] : null;
            _rowBg[i].color = i == _selRow ? RowSel : RowColor;
            _rowHead[i].sprite = d != null ? CharaCatalog.Head(d.Id) : null;
            _rowHead[i].enabled = _rowHead[i].sprite != null;
            if (d != null) UiKit.FitName(_rowName[i], d.Name, 190f, 40f, 19, 12); else _rowName[i].text = "";
            _rowPos[i].text = d != null ? d.Position : "";
        }
        for (int k = 0; k < _poolCells.Count; k++)
        {
            var d = _poolCells[k].Key;
            int at = _edit.IndexOf(d);
            _poolCells[k].Value.color = at >= 0 ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
            _poolLabels[k].text = at >= 0 ? (at + 1) + "番" : d.Name;
            _poolLabels[k].color = at >= 0 ? UiKit.GoldLight : Color.white;
        }
    }

    private void OnRow(int i)
    {
        GameAudio.Play(Sfx.Click, 0.5f, 1.3f);
        if (_selRow < 0) _selRow = i;
        else if (_selRow == i) _selRow = -1;
        else
        {
            var tmp = _edit[_selRow]; _edit[_selRow] = _edit[i]; _edit[i] = tmp;
            _selRow = -1;
            TeamSetup.Save(_edit);
        }
        RefreshLineup();
    }

    private void OnPool(CharaDef d)
    {
        GameAudio.Play(Sfx.Click, 0.5f, 1.4f);
        int at = _edit.IndexOf(d);
        if (_selRow < 0) { _selRow = at; RefreshLineup(); return; }   // 打線にいる選手なら、その行を選ぶ
        if (at >= 0) { var tmp = _edit[_selRow]; _edit[_selRow] = _edit[at]; _edit[at] = tmp; }
        else _edit[_selRow] = d;
        _selRow = -1;
        TeamSetup.Save(_edit);
        RefreshLineup();
    }
}
