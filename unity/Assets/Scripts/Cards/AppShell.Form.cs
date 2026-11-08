using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// AppShell.Form.cs
// 編成 →「フォーム」: 選んだ選手のフォームを作る。左でその選手が実際に動き、右の11段階で変える。
//   投手は投げるフォーム、野手は打つフォーム。変えるたびに保存し、試合でもそのとおりに体で動く。
//   「もとに戻す」で保存を消す(試合では従来の決めポーズの絵に戻る)。
// ============================================================
public partial class AppShell
{
    private RectTransform _formScr, _formRows;
    private Text _formTitle;
    private RawImage _formView;
    private Button _formReset;
    private FormPreview _preview;
    private CharaDef _formDef;
    private CharaForm _form;
    private bool _formPitcher;

    private class FormItem
    {
        public string Label, Low, High;
        public System.Func<CharaForm, int> Get;
        public System.Action<CharaForm, int> Set;
    }

    private static readonly FormItem[] BatItems =
    {
        new FormItem { Label = "バットの構え", Low = "寝かせる", High = "立てる", Get = f => f.BatHold, Set = (f, v) => f.BatHold = v },
        new FormItem { Label = "腰の落とし", Low = "高い", High = "低い", Get = f => f.Crouch, Set = (f, v) => f.Crouch = v },
        new FormItem { Label = "足の上げ", Low = "すり足", High = "一本足", Get = f => f.LegKick, Set = (f, v) => f.LegKick = v },
        new FormItem { Label = "振りの軌道", Low = "ダウン", High = "アッパー", Get = f => f.SwingPlane, Set = (f, v) => f.SwingPlane = v },
        new FormItem { Label = "振りの大きさ", Low = "小さく", High = "大きく", Get = f => f.SwingSize, Set = (f, v) => f.SwingSize = v },
        new FormItem { Label = "ぐにゃ度", Low = "しゃきっ", High = "ぐにゃぐにゃ", Get = f => f.Gunya, Set = (f, v) => f.Gunya = v },
    };

    private static readonly FormItem[] PitchItems =
    {
        new FormItem { Label = "腕の角度", Low = "アンダー", High = "オーバー", Get = f => f.ArmSlot, Set = (f, v) => f.ArmSlot = v },
        new FormItem { Label = "足の上げ", Low = "低い", High = "高い", Get = f => f.PLegKick, Set = (f, v) => f.PLegKick = v },
        new FormItem { Label = "前のめり", Low = "立つ", High = "倒れこむ", Get = f => f.Lean, Set = (f, v) => f.Lean = v },
        new FormItem { Label = "腕の振り", Low = "小さく", High = "大きく", Get = f => f.ArmSwing, Set = (f, v) => f.ArmSwing = v },
        new FormItem { Label = "ぐにゃ度", Low = "しゃきっ", High = "ぐにゃぐにゃ", Get = f => f.Gunya, Set = (f, v) => f.Gunya = v },
    };

    private readonly List<Image[]> _formPips = new List<Image[]>();
    private FormItem[] _formItems;

    private static readonly Color PipOn = new Color(1f, 0.78f, 0.18f, 1f);
    private static readonly Color PipOff = new Color(1f, 1f, 1f, 0.14f);

    private void BuildForm()
    {
        _formScr = MakeScreen("Form", 0.42f);
        UseMenuBackdrop(_formScr);
        TopBar(_formScr, "フォーム", out _formTitle, ShowLineupKeep);
        _formReset = UiKit.Btn(_formScr, "Reset", "もとに戻す", new Vector2(170f, 42f), false, OnFormResetTap, 20);
        UiKit.Place(_formReset.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -11f), new Vector2(170f, 42f));

        // 左: 動いて見せる選手
        var frame = UiKit.Img(_formScr, "ViewFrame", HomeSkin.Pill, Color.white);
        var fr = frame.rectTransform;
        fr.anchorMin = new Vector2(0f, 0f); fr.anchorMax = new Vector2(0f, 1f); fr.pivot = new Vector2(0f, 0.5f);
        fr.offsetMin = new Vector2(14f, NavReserve); fr.offsetMax = new Vector2(14f + 280f, -70f);
        var view = new GameObject("View", typeof(RectTransform));
        view.transform.SetParent(frame.transform, false);
        _formView = view.AddComponent<RawImage>();
        _formView.raycastTarget = false;
        UiKit.Fill(_formView.rectTransform, 6f);
        var fit = view.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = 480f / 560f;

        // 右: 項目ごとの11段階
        _formRows = UiKit.Rect(_formScr, "Rows");
        _formRows.anchorMin = new Vector2(0f, 0f); _formRows.anchorMax = new Vector2(1f, 1f);
        _formRows.offsetMin = new Vector2(14f + 280f + 18f, NavReserve); _formRows.offsetMax = new Vector2(-16f, -70f);
        var vl = _formRows.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 4f; vl.childControlHeight = true; vl.childControlWidth = true;
        vl.childForceExpandHeight = false; vl.childForceExpandWidth = true;
        vl.childAlignment = TextAnchor.MiddleCenter;

        _preview = FormPreview.Create(transform);
    }

    // 編成の今の選手のフォームを開く
    private void OpenForm()
    {
        var d = RowDef(_selRow);
        if (d == null) return;
        _formDef = d;
        _formPitcher = _selRow == 0;
        _form = CharaForm.Load(d.Id) ?? new CharaForm();
        Only(_formScr);
        _formTitle.text = "フォーム   " + d.Name;
        BuildFormRows(_formPitcher ? PitchItems : BatItems);
        _preview.Show(d, _form, _formPitcher);
        _formView.texture = _preview.Texture;
        RefreshForm();
    }

    private void ShowLineupKeep()
    {
        // フォームから戻るときは、選んでいた行のまま
        int keep = _selRow;
        ShowLineup();
        _selRow = keep;
        RefreshLineup();
        RebuildPool();
    }

    private void BuildFormRows(FormItem[] items)
    {
        ClearChildren(_formRows);
        _formPips.Clear();
        _formItems = items;
        for (int i = 0; i < items.Length; i++)
        {
            int idx = i;
            var row = UiKit.Img(_formRows, "Row" + i, SpriteFactory.White, RowColor);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 44f;
            var lab = UiKit.Txt(row.transform, "Label", items[i].Label, 19, UiKit.GoldLight, TextAnchor.MiddleLeft);
            Anchor(lab.rectTransform, 12f, 132f);
            var lo = UiKit.Txt(row.transform, "Low", items[i].Low, 14, new Color(0.8f, 0.85f, 1f), TextAnchor.MiddleRight);
            Anchor(lo.rectTransform, 144f, 82f);
            var minus = UiKit.Btn(row.transform, "Minus", "◀", new Vector2(34f, 32f), false, () => StepForm(idx, -1), 15);
            UiKit.Place(minus.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(232f, 0f), new Vector2(34f, 32f));
            var pips = new Image[CharaForm.Max + 1];
            for (int k = 0; k <= CharaForm.Max; k++)
            {
                int v = k;
                var pip = UiKit.Img(row.transform, "Pip" + k, SpriteFactory.White, PipOff, true);
                UiKit.Place(pip.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(272f + k * 15f, 0f), new Vector2(12f, 24f));
                var pb = pip.gameObject.AddComponent<Button>();
                pb.transition = Selectable.Transition.None;
                pb.onClick.AddListener(() => SetFormValue(idx, v));
                pips[k] = pip;
            }
            var plus = UiKit.Btn(row.transform, "Plus", "▶", new Vector2(34f, 32f), false, () => StepForm(idx, 1), 15);
            UiKit.Place(plus.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(272f + 11f * 15f + 4f, 0f), new Vector2(34f, 32f));
            var hi = UiKit.Txt(row.transform, "High", items[i].High, 14, new Color(0.8f, 0.85f, 1f), TextAnchor.MiddleLeft);
            Anchor(hi.rectTransform, 272f + 11f * 15f + 44f, 110f);
            _formPips.Add(pips);
        }
    }

    private void StepForm(int idx, int dir)
    {
        SetFormValue(idx, Mathf.Clamp(_formItems[idx].Get(_form) + dir, 0, CharaForm.Max));
    }

    private void SetFormValue(int idx, int v)
    {
        if (_form == null || _formDef == null) return;
        if (_formItems[idx].Get(_form) == v) return;
        GameAudio.Play(Sfx.Click, 0.4f, 1.0f + v * 0.05f);
        _formItems[idx].Set(_form, v);
        CharaForm.Save(_formDef.Id, _form);
        _preview.Apply(_form);
        RefreshForm();
    }

    private void OnFormResetTap()
    {
        if (_formDef == null) return;
        CharaForm.Clear(_formDef.Id);
        _form = new CharaForm();
        _preview.Apply(_form);
        RefreshForm();
    }

    private void RefreshForm()
    {
        for (int i = 0; i < _formPips.Count; i++)
        {
            int v = _formItems[i].Get(_form);
            var pips = _formPips[i];
            for (int k = 0; k < pips.Length; k++)
            {
                pips[k].color = k <= v ? PipOn : PipOff;
                pips[k].rectTransform.sizeDelta = new Vector2(12f, k == v ? 32f : 24f);
            }
        }
        _formReset.interactable = _formDef != null && CharaForm.Has(_formDef.Id);
        _formReset.GetComponent<Image>().color = _formReset.interactable ? Color.white : new Color(1f, 1f, 1f, 0.4f);
    }
}
