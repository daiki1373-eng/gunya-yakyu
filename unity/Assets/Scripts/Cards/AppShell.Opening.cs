using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// AppShell.Opening.cs
// 起動時のタイトル演出(約3秒、タップで最後の形へ飛ばす)
//   暗闇に光が差す → 「ぐにゃぐにゃ」が落ちてきてつぶれる → 「ベースボール」が叩きつけられる(閃光・揺れ・花火・曲)
//   → カードが下から1枚ずつ飛び出してくるりと返る → 「つづきから」が弾んで出る
// 演出の後も、ロゴはぐにゃぐにゃ揺れ、カードはゆっくり浮き沈みし、ボタンは軽く脈打つ。
// ============================================================
public partial class AppShell
{
    private readonly List<RectTransform> _titleCards = new List<RectTransform>();
    private Vector2[] _titleCardHome;
    private JellyText _logo1, _logo2;
    private RectTransform _titleCont;
    private bool _titleIntroDone, _titleIntroRunning, _titleSkip;

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
        var logo1 = (RectTransform)_logo1.transform;
        var logo2 = (RectTransform)_logo2.transform;
        Vector2 l1 = logo1.anchoredPosition, l2 = logo2.anchoredPosition;
        int n = _titleCards.Count;
        _titleCardHome = new Vector2[n];
        var cardRot = new Quaternion[n];
        for (int i = 0; i < n; i++)
        {
            _titleCardHome[i] = _titleCards[i].anchoredPosition;
            cardRot[i] = _titleCards[i].localRotation;
            _titleCards[i].localScale = Vector3.zero;
        }
        var raysImg = _titleRays.GetComponent<Image>();
        Color raysC = raysImg.color;

        // 幕(黒)。ここをタップすると最後の形へ飛ばす
        var curtain = UiKit.Img(_title, "Curtain", SpriteFactory.White, Color.black, true);
        UiKit.Fill(curtain.rectTransform);
        var tap = curtain.gameObject.AddComponent<Button>();
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(() => _titleSkip = true);
        var flash = UiKit.Img(_title, "Flash", SpriteFactory.White, new Color(1f, 1f, 1f, 0f));
        UiKit.Fill(flash.rectTransform);
        logo1.localScale = Vector3.zero; logo2.localScale = Vector3.zero; _titleCont.localScale = Vector3.zero;
        raysImg.color = new Color(raysC.r, raysC.g, raysC.b, 0f);
        // ロゴとボタンは幕より手前(幕は背景・カードだけを暗くする)
        curtain.transform.SetSiblingIndex(_titleRays.GetSiblingIndex() + 1);
        logo1.SetAsLastSibling(); logo2.SetAsLastSibling(); _titleCont.SetAsLastSibling();
        flash.transform.SetAsLastSibling();
        _namePanel.SetAsLastSibling();   // 名前入力はいつも一番手前

        // 1) 暗闇に光が差す
        GameAudio.Play(Sfx.Whoosh, 0.5f, 0.6f);
        yield return TitleStep(0.55f, u =>
        {
            raysImg.color = new Color(raysC.r, raysC.g, raysC.b, raysC.a * u);
            _titleRays.localScale = Vector3.one * Mathf.Lerp(0.25f, 1f, EaseOut(u));
            curtain.color = new Color(0f, 0f, 0f, Mathf.Lerp(1f, 0.55f, u));
        });

        // 2) 「ぐにゃぐにゃ」が上から落ちてきて、着地でぐにゃっとつぶれる
        GameAudio.Play(Sfx.Whoosh, 0.7f, 1.15f);
        yield return TitleStep(0.32f, u =>
        {
            logo1.localScale = Vector3.one;
            logo1.anchoredPosition = l1 + new Vector2(0f, (1f - u * u) * 420f);
        });
        if (!_titleSkip) { GameAudio.Play(Sfx.Boing, 0.9f, 0.8f); _logo1.Boost = 1f; }
        yield return TitleStep(0.3f, u =>
        {
            float sq = Mathf.Sin(u * Mathf.PI) * (1f - u) * 0.6f;
            logo1.localScale = new Vector3(1f + sq, 1f - sq * 0.8f, 1f);
        });

        // 3) 「ベースボール」が叩きつけられる: 閃光・画面の揺れ・花火・曲の始まり
        yield return TitleStep(0.2f, u => { logo2.localScale = Vector3.one * Mathf.Lerp(3f, 1f, u * u); });
        if (!_titleSkip)
        {
            GameAudio.Play(Sfx.HitPerfect, 1f, 0.75f);
            GameAudio.Play(Sfx.BigCheer, 0.6f);
            BgmPlayer.PlayBase(BgmPlayer.Home);
            _logo2.Boost = 0.8f;
            for (int k = 0; k < 4; k++)
                _fx.Firework(new Vector2(UnityEngine.Random.Range(-420f, 420f), -300f), UnityEngine.Random.Range(330f, 470f),
                    k % 2 == 0 ? UiKit.Gold : new Color(1f, 0.3f, 0.2f));
        }
        Vector2 home = _title.anchoredPosition;
        yield return TitleStep(0.3f, u =>
        {
            flash.color = new Color(1f, 1f, 1f, 0.75f * (1f - u));
            curtain.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.55f, 0f, u));
            float amp = (1f - u) * 14f;
            _title.anchoredPosition = home + new Vector2(Mathf.Sin(u * 70f) * amp, Mathf.Cos(u * 53f) * amp);
        });
        _title.anchoredPosition = home;

        // 4) カードが外側から順に下から飛び出し、くるりと返って扇に収まる
        int[] order = { 0, 4, 1, 3, 2 };
        foreach (int i in order)
        {
            if (i >= n) continue;
            StartCoroutine(TitleCardIn(_titleCards[i], _titleCardHome[i], cardRot[i]));
            if (!_titleSkip) GameAudio.Play(Sfx.Whoosh, 0.35f, 1.3f + i * 0.05f);
            yield return TitleWait(0.11f);
        }
        yield return TitleWait(0.35f);

        // 5) ボタンが弾んで出る
        if (!_titleSkip) GameAudio.Play(Sfx.Click, 0.6f, 1.4f);
        yield return TitleStep(0.3f, u => { _titleCont.localScale = Vector3.one * BackOut(u); });

        // 仕上げ(飛ばしたときもここで最後の形にそろえる)
        if (_titleSkip) BgmPlayer.PlayBase(BgmPlayer.Home);
        logo1.anchoredPosition = l1; logo2.anchoredPosition = l2;
        logo1.localScale = logo2.localScale = Vector3.one;
        _titleCont.localScale = Vector3.one;
        raysImg.color = raysC;
        _titleRays.localScale = Vector3.one;
        _title.anchoredPosition = home;
        for (int i = 0; i < n; i++)
        {
            _titleCards[i].anchoredPosition = _titleCardHome[i];
            _titleCards[i].localRotation = cardRot[i];
            _titleCards[i].localScale = Vector3.one;
        }
        Destroy(curtain.gameObject);
        Destroy(flash.gameObject);
        _titleIntroRunning = false;
    }

    private IEnumerator TitleCardIn(RectTransform card, Vector2 to, Quaternion rot)
    {
        Vector2 from = to + new Vector2(0f, -520f);
        float t = 0f, dur = 0.42f;
        while (t < dur && !_titleSkip)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float u = Mathf.Clamp01(t / dur);
            float e = BackOut(u);
            card.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
            // 横に裏返った状態から開く
            float flip = Mathf.Clamp01(u * 1.6f);
            card.localScale = new Vector3(Mathf.Abs(Mathf.Cos((1f - flip) * Mathf.PI * 0.5f)), 1f, 1f);
            card.localRotation = rot * Quaternion.Euler(0f, 0f, (1f - e) * 25f);
            yield return null;
        }
        card.anchoredPosition = to;
        card.localScale = Vector3.one;
        card.localRotation = rot;
    }

    // 演出の後: カードがゆっくり浮き沈みし、ボタンが軽く脈打つ
    private void TitleIdle(float t)
    {
        if (_title == null || !_title.gameObject.activeSelf || _titleIntroRunning || _titleCardHome == null) return;
        for (int i = 0; i < _titleCards.Count && i < _titleCardHome.Length; i++)
            _titleCards[i].anchoredPosition = _titleCardHome[i] + new Vector2(0f, Mathf.Sin(t * 1.4f + i * 1.3f) * 5f);
        if (_titleCont != null) _titleCont.localScale = Vector3.one * (1f + 0.035f * Mathf.Sin(t * 3.2f));
    }
}
