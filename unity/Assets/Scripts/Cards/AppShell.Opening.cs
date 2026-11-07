using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// AppShell.Opening.cs
// 起動時のタイトル演出(約3秒、タップで最後の形へ飛ばす)
//   暗闇の球場に照明が3段階で点く(カメラは低い位置から夜空へ持ち上がる)
//   → 左から打者、右から投手が滑り込む → ロゴが叩きつけられてぐにゃっとつぶれる(花火・曲)
//   → 「つづきから」が弾んで出る
// 演出の後は、ロゴがゆっくり弾み、キャラが呼吸し、ボタンが軽く脈打つ。絵は互いに重ねない。
// ============================================================
public partial class AppShell
{
    private RectTransform _titleCont;
    private bool _titleIntroDone, _titleIntroRunning, _titleSkip;
    private float _titleSlideL, _titleSlideR, _logoSquash = 0f, _logoIn = 1f;

    private IEnumerator TitleStep(float dur, Action<float> f)
    {
        float t = 0f;
        while (t < dur && !_titleSkip)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            f(Mathf.Clamp01(t / dur));
            yield return null;
        }
        if (!_titleSkip) f(1f);
    }

    private IEnumerator TitleWait(float dur)
    {
        float t = 0f;
        while (t < dur && !_titleSkip) { t += Time.unscaledDeltaTime; yield return null; }
    }

    private static float EaseOut(float u) { return 1f - (1f - u) * (1f - u) * (1f - u); }
    private static float BackOut(float u)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(u - 1f, 3f) + c1 * Mathf.Pow(u - 1f, 2f);
    }

    private IEnumerator TitleIntro()
    {
        _titleIntroDone = true;
        _titleIntroRunning = true;
        _titleSkip = false;

        // 幕(黒)。ここをタップすると最後の形へ飛ばす
        var curtain = UiKit.Img(_title, "Curtain", SpriteFactory.White, Color.black, true);
        UiKit.Fill(curtain.rectTransform);
        curtain.transform.SetSiblingIndex(1);   // 背景の幕のすぐ上(キャラ・ロゴ・ボタンより奥)
        var tap = curtain.gameObject.AddComponent<Button>();
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(() => _titleSkip = true);
        _namePanel.SetAsLastSibling();

        _titleSlideL = _titleSlideR = 1f;
        _logoIn = 0f; _logoSquash = 0f;
        _titleCont.localScale = Vector3.zero;

        // 1) 照明が3段階で点く
        yield return TitleWait(0.35f);
        float[] lv = { 0.72f, 0.45f, 0.22f };
        for (int i = 0; i < lv.Length && !_titleSkip; i++)
        {
            GameAudio.Play(Sfx.Thud, 0.8f, 0.7f + i * 0.08f);
            float from = curtain.color.a, to = lv[i];
            yield return TitleStep(0.08f, u => curtain.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, u)));
            yield return TitleWait(0.2f);
        }

        // 2) 打者が左から、投手が右から滑り込む
        GameAudio.Play(Sfx.Whoosh, 0.7f, 1.0f);
        StartCoroutine(TitleStep(0.42f, u => _titleSlideL = 1f - BackOut(u)));
        yield return TitleWait(0.12f);
        GameAudio.Play(Sfx.Whoosh, 0.7f, 1.15f);
        yield return TitleStep(0.42f, u => _titleSlideR = 1f - BackOut(u));

        // 3) ロゴが叩きつけられ、着地でぐにゃっとつぶれる。花火と曲
        GameAudio.Play(Sfx.Whoosh, 0.6f, 1.3f);
        yield return TitleStep(0.18f, u => { _logoIn = u; });
        if (!_titleSkip)
        {
            GameAudio.Play(Sfx.HitPerfect, 1f, 0.75f);
            GameAudio.Play(Sfx.BigCheer, 0.6f);
            BgmPlayer.PlayBase(BgmPlayer.Home);
            BgmPlayer.Jingle("bgm-title");
            if (_stageRef != null) _stageRef.Stadium.LaunchFireworks(6);
        }
        Vector2 home = _title.anchoredPosition;
        yield return TitleStep(0.45f, u =>
        {
            _logoSquash = Mathf.Sin(u * Mathf.PI * 2.2f) * (1f - u);
            curtain.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.22f, 0f, u));
            float amp = (1f - u) * 10f;
            _title.anchoredPosition = home + new Vector2(Mathf.Sin(u * 70f) * amp, Mathf.Cos(u * 53f) * amp);
        });
        _title.anchoredPosition = home;

        // 4) ボタンが弾んで出る
        if (!_titleSkip) GameAudio.Play(Sfx.Click, 0.6f, 1.4f);
        yield return TitleStep(0.3f, u => { _titleCont.localScale = Vector3.one * BackOut(u); });

        // 仕上げ(飛ばしたときもここで最後の形にそろえる)
        if (_titleSkip) BgmPlayer.PlayBase(BgmPlayer.Home);
        _titleSlideL = _titleSlideR = 0f;
        _logoIn = 1f; _logoSquash = 0f;
        _titleCont.localScale = Vector3.one;
        _title.anchoredPosition = home;
        Destroy(curtain.gameObject);
        _titleIntroRunning = false;
    }

    // 毎フレームの配置: 左右のキャラの体の幅から、真ん中の列(ロゴ)の幅を決める
    private void TitleIdle(float t)
    {
        if (_title == null || !_title.gameObject.activeSelf) return;
        var size = _root.rect.size;
        const float margin = 24f, gap = 28f, feet = 12f;
        float bodyFrac = 0.82f;
        float wl = LayoutChara(_titleL, size.y, bodyFrac, margin, feet);
        float wr = LayoutChara(_titleR, size.y, bodyFrac, margin, feet);
        float side = Mathf.Max(wl, wr);
        // 真ん中の列は最低 400 を確保する(狭い画面ではキャラを小さくする)
        float mid = size.x - 2f * (margin + side + gap);
        if (mid < 400f && side > 0f)
        {
            bodyFrac *= Mathf.Max(0.4f, (size.x - 400f - 2f * (margin + gap)) / (2f * side));
            wl = LayoutChara(_titleL, size.y, bodyFrac, margin, feet);
            wr = LayoutChara(_titleR, size.y, bodyFrac, margin, feet);
            side = Mathf.Max(wl, wr);
            mid = size.x - 2f * (margin + side + gap);
        }
        // 滑り込み(画面の外から)と呼吸
        if (_titleL != null)
        {
            _titleL.Rt.anchoredPosition += new Vector2(-_titleSlideL * (side + margin + 40f), 0f);
            _titleL.Rt.localScale = new Vector3(1f, 1f + 0.01f * Mathf.Sin(t * 2.0f), 1f);
        }
        if (_titleR != null)
        {
            _titleR.Rt.anchoredPosition += new Vector2(_titleSlideR * (side + margin + 40f), 0f);
            _titleR.Rt.localScale = new Vector3(1f, 1f + 0.01f * Mathf.Sin(t * 2.0f + 1.7f), 1f);
        }
        // ロゴ: 真ん中の列の幅いっぱい(最大 560)。上から落ちてきて、つぶれて戻る。演出の後はゆっくり弾む
        if (_titleLogo != null)
        {
            var sp = _titleLogo.GetComponent<Image>().sprite;
            float lw = Mathf.Min(560f, mid);
            float lh = sp != null ? lw * sp.rect.height / sp.rect.width : lw * 0.38f;
            _titleLogo.sizeDelta = new Vector2(lw, lh);
            float y = -size.y * 0.08f - lh / 2f;
            float drop = (1f - _logoIn) * (size.y * 0.6f);
            float bob = _titleIntroRunning ? 0f : Mathf.Sin(t * 1.6f) * 4f;
            _titleLogo.anchoredPosition = new Vector2(0f, y + drop + bob);
            float sq = _logoSquash * 0.22f + (_titleIntroRunning ? 0f : Mathf.Sin(t * 3.2f) * 0.012f);
            _titleLogo.localScale = new Vector3(1f + sq, 1f - sq, 1f);
            _titleLogo.gameObject.SetActive(_logoIn > 0f);
        }
        if (_titleCont != null && !_titleIntroRunning) _titleCont.localScale = Vector3.one * (1f + 0.035f * Mathf.Sin(t * 3.2f));
    }
}
