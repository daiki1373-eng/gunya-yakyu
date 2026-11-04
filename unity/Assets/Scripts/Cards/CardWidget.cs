using UnityEngine;
using UnityEngine.UI;

// ============================================================
// CardWidget.cs
// カード1枚の見た目(枠・絵・レア度・名前・重ね数)。一覧・ガチャ結果・拡大表示で共通。
// ============================================================
public static class CardWidget
{
    public class Parts
    {
        public RectTransform Root;
        public Image Frame, Art, Shine, StackFrame;
        public Text RarityText, NameText, CountText;
    }

    // size: カード全体の大きさ(2:3推奨)。full=true なら拡大用の画像を使う。
    public static Parts Build(Transform parent, CharaDef d, Vector2 size, bool full, bool owned, int count)
    {
        var p = new Parts();
        var frame = UiKit.Img(parent, "Card_" + d.Id, UiKit.Frame, CharaCatalog.FrameColor(d.Rarity), true);
        p.Frame = frame;
        p.Root = frame.rectTransform;
        p.Root.sizeDelta = size;
        float inset = Mathf.Max(6f, size.x * 0.055f);

        // 絵(枠の内側に収め、はみ出しは切る)
        // (回転させても切り抜きがずれないよう、ステンシルのMaskを使う)
        var maskImg = UiKit.Img(p.Root, "Mask", SpriteFactory.White, new Color(0.08f, 0.03f, 0.03f));
        var mask = UiKit.Fill(maskImg.rectTransform, inset);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        Sprite sp = full ? CharaCatalog.Full(d.Id) : CharaCatalog.Thumb(d.Id);
        var art = UiKit.Img(mask, "Art", sp, owned ? Color.white : new Color(0.22f, 0.22f, 0.26f));
        p.Art = art;
        var ar = art.rectTransform;
        ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0.5f);
        Vector2 inner = size - Vector2.one * inset * 2f;
        if (sp != null)
        {
            // 覆うように拡大(縦長の絵は上寄せ)
            float a = sp.rect.width / sp.rect.height;
            float ia = inner.x / inner.y;
            Vector2 sz = a > ia ? new Vector2(inner.y * a, inner.y) : new Vector2(inner.x, inner.x / a);
            ar.sizeDelta = sz;
            ar.anchoredPosition = new Vector2(0f, a > ia ? 0f : -(sz.y - inner.y) * 0.15f);
        }
        else ar.sizeDelta = inner;

        // きらめき(★4以上。動きは呼び出し側で)
        {
            p.Shine = UiKit.Img(mask, "Shine", UiKit.Shine, new Color(1f, 0.95f, 0.75f, owned ? 1f : 0f));
            var sr = p.Shine.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
            sr.sizeDelta = new Vector2(inner.x * 0.5f, inner.y * 1.6f);
            sr.localRotation = Quaternion.Euler(0f, 0f, -25f);
            sr.anchoredPosition = new Vector2(-inner.x * 1.2f, 0f);
        }

        // 重ねの豪華化: 半分以上で金の縁、上限で金の縁+きらめき
        p.StackFrame = UiKit.Img(p.Root, "StackFrame", UiKit.Frame, new Color(1f, 0.85f, 0.3f, 0f));
        UiKit.Fill(p.StackFrame.rectTransform, -3f);

        float fs = size.y / 156f;   // 一覧サイズを基準にした文字の倍率
        // レア度(左上)
        var top = UiKit.Img(p.Root, "RarityPlate", UiKit.Plate, Color.white);
        UiKit.Place(top.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(inset, -inset),
            new Vector2(inner.x, 20f * fs));
        p.RarityText = UiKit.Txt(top.transform, "Rarity", CharaCatalog.Label(d.Rarity), Mathf.RoundToInt(14 * fs),
            d.Rarity == Rarity.Secret ? new Color(1f, 0.6f, 0.95f) : UiKit.GoldLight, TextAnchor.MiddleLeft);
        UiKit.Fill(p.RarityText.rectTransform);
        p.RarityText.rectTransform.offsetMin = new Vector2(4f * fs, 0f);

        // 名前と重ね数(下)
        var bottom = UiKit.Img(p.Root, "NamePlate", UiKit.Plate, Color.white);
        UiKit.Place(bottom.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(inset, inset),
            new Vector2(inner.x, 24f * fs));
        p.NameText = UiKit.Txt(bottom.transform, "Name", d.Name, Mathf.RoundToInt(12 * fs), Color.white, TextAnchor.MiddleLeft);
        UiKit.Fill(p.NameText.rectTransform);
        p.NameText.rectTransform.offsetMin = new Vector2(4f * fs, 0f);
        p.NameText.horizontalOverflow = HorizontalWrapMode.Wrap;
        p.NameText.resizeTextForBestFit = true;
        p.NameText.resizeTextMinSize = Mathf.RoundToInt(9 * fs);
        p.NameText.resizeTextMaxSize = Mathf.RoundToInt(12 * fs);
        p.NameText.rectTransform.offsetMax = new Vector2(-24f * fs, 0f);
        p.CountText = UiKit.Txt(bottom.transform, "Count", "", Mathf.RoundToInt(12 * fs), UiKit.GoldLight, TextAnchor.MiddleRight);
        UiKit.Fill(p.CountText.rectTransform);
        p.CountText.rectTransform.offsetMax = new Vector2(-4f * fs, 0f);
        SetCount(p, d, owned, count);
        return p;
    }

    public static void SetCount(Parts p, CharaDef d, bool owned, int count)
    {
        p.Art.color = owned ? Color.white : new Color(0.22f, 0.22f, 0.26f);
        int max = CharaCatalog.StackMax(d.Rarity);
        bool isMax = owned && count >= max;
        bool shine = owned && (d.Rarity >= Rarity.R4 || isMax);
        if (p.Shine != null) p.Shine.color = new Color(1f, 0.95f, 0.75f, shine ? 1f : 0f);
        float lv = owned ? Mathf.Clamp01((count - 1f) / Mathf.Max(1f, max - 1f)) : 0f;
        if (p.StackFrame != null) p.StackFrame.color = new Color(1f, 0.85f, 0.3f, isMax ? 1f : (lv >= 0.5f ? 0.65f : 0f));
        p.CountText.text = !owned ? "" : (count >= max ? "MAX" : "×" + count);
    }
}
