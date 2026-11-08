using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// GachaShow.cs (AppShell の続き)
// ガチャの排出演出。光る球 → 閃光と放射光 → カードが裏返って登場。
// レア度で演出の格が上がる: ★1-2 白 / ★3 青 / ★4 金(紙吹雪・ファンファーレ) /
// ★5 赤金(確定演出・花火・揺れ) / SECRET 虹色(暗転・長めの確定演出・大量の花火)。
// タップで次へ。「SKIP」で結果一覧へ飛ぶ。
// ============================================================
public partial class AppShell
{
    private bool _busy, _tapped, _skipAll;
    private Image _dim;

    public void Pull(int times)
    {
        if (_busy) return;
        List<PullResult> results;
        if (!Gacha.TryPullCoins(times, out results))
        {
            GameAudio.Play(Sfx.Boing, 0.5f, 1.2f);
            return;
        }
        RefreshGacha();
        StartCoroutine(PullRoutineFixed(results));
    }

    private void Scout()
    {
        if (_busy) return;
        List<PullResult> results;
        if (!Gacha.TryScout(out results))
        {
            GameAudio.Play(Sfx.Boing, 0.5f, 1.2f);
            return;
        }
        RefreshGacha();
        StartCoroutine(PullRoutineFixed(results));
    }

    // 検証用: 指定のキャラで演出だけ見る(所持は増やさない)
    public void DebugReveal(string id)
    {
        var d = CharaCatalog.Get(id);
        if (d == null || _busy) return;
        var r = new PullResult { Chara = d, Add = new Collection.AddResult { Before = 0, After = 1, IsNew = true } };
        StartCoroutine(PullRoutineFixed(new List<PullResult> { r }));
    }

    private IEnumerator PullRoutineFixed(List<PullResult> results)
    {
        _busy = true;
        // ダイヤルをぐるっと回して、マシンを揺らしてから演出へ
        if (_gachaDial != null && _gacha.gameObject.activeSelf)
        {
            _dialSpin = 1400f;
            GameAudio.Play(Sfx.Whoosh, 0.6f, 0.7f);
            float t = 0f;
            while (t < 0.55f && !_tapped) { t += Time.unscaledDeltaTime; yield return null; }
            _tapped = false;
        }
        BgmPlayer.Jingle("bgm-gacha-pull", 3.5f);
        _skipAll = false;
        ClearChildren(_show);
        _show.gameObject.SetActive(true);
        _fx.transform.SetAsLastSibling();

        // 画面全体のタップ受け
        var dim = UiKit.Img(_show, "Dim", SpriteFactory.White, new Color(0f, 0f, 0f, 0.94f), true);
        UiKit.Fill(dim.rectTransform);
        _dim = dim;
        var tap = dim.gameObject.AddComponent<Button>();
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(() => _tapped = true);
        var stage = UiKit.Fill(UiKit.Rect(_show, "Stage"));
        GameObject skipGo = null;
        if (results.Count > 1)
        {
            var skip = UiKit.Btn(_show, "Skip", "SKIP", new Vector2(110f, 44f), false, () => { _skipAll = true; _tapped = true; }, 20);
            UiKit.Place(skip.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -12f), new Vector2(110f, 44f));
            skipGo = skip.gameObject;
        }

        for (int i = 0; i < results.Count && !_skipAll; i++)
            yield return Reveal(stage, results[i], results.Count > 1, i, results.Count);

        ClearChildren(stage);
        if (skipGo != null) skipGo.SetActive(false);
        _tapped = false;
        if (results.Count > 1) yield return ResultsGrid(stage, results);

        _show.gameObject.SetActive(false);
        _fx.Clear();
        RefreshCards();
        RefreshGacha();
        _busy = false;
    }

    // u: 0→1 を dur 秒で回す。タップで最後まで飛ばせる(skippable のとき)
    private IEnumerator Anim(float dur, Action<float> f, bool skippable = true)
    {
        float t = 0f;
        while (t < dur)
        {
            if (skippable && _tapped) { _tapped = false; break; }
            t += Time.unscaledDeltaTime;
            f(Mathf.Clamp01(t / dur));
            yield return null;
        }
        f(1f);
    }

    private IEnumerator WaitTap(float autoAfter)
    {
        _tapped = false;
        float t = 0f;
        while (!_tapped && (autoAfter <= 0f || t < autoAfter))
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        _tapped = false;
    }

    private IEnumerator Reveal(RectTransform stage, PullResult r, bool multi, int index, int total)
    {
        ClearChildren(stage);
        _tapped = false;
        var d = r.Chara;
        Rarity rr = d.Rarity;
        bool special = rr >= Rarity.R5;
        bool rich = rr >= Rarity.R4;
        Color tint = RevealColor(rr);

        var shake = UiKit.Fill(UiKit.Rect(stage, "Shake"));
        var bgRays = UiKit.Img(shake, "Rays", UiKit.Rays, new Color(tint.r, tint.g, tint.b, 0f));
        UiKit.Place(bgRays.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 1300f));
        var counter = UiKit.Txt(stage, "Counter", multi ? (index + 1) + " / " + total : "", 20, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleLeft);
        UiKit.Place(counter.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -14f), new Vector2(200f, 30f));

        // ---- 1) 光る球(ボール) ----
        var glow = UiKit.Img(shake, "Glow", StadiumArt.Glow, new Color(1f, 1f, 1f, 0.9f));
        UiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 260f));
        var ball = UiKit.Img(shake, "Ball", SpriteFactory.Circle, Color.white);
        UiKit.Place(ball.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
        // 低レアは白、上がるほど早い段階で色が付く(期待感)
        Color pre = rr >= Rarity.R4 ? UiKit.Gold : (rr == Rarity.R3 ? tint : Color.white);
        GameAudio.Play(Sfx.Whoosh, 0.6f, 0.8f);
        if (!multi || rich || index == 0) BgmPlayer.Jingle("bgm-gacha-suspense", 4f);
        float charge = multi && !rich ? 0.35f : 0.7f;
        yield return Anim(charge, u =>
        {
            float s = Mathf.Lerp(0.3f, 1f, u) * (1f + 0.08f * Mathf.Sin(u * 30f));
            ball.rectTransform.localScale = Vector3.one * s;
            glow.rectTransform.localScale = Vector3.one * s * (1f + 0.2f * Mathf.Sin(u * 18f));
            glow.color = Color.Lerp(Color.white, pre, u);
            ball.color = Color.Lerp(Color.white, Color.Lerp(pre, Color.white, 0.5f), u);
        });

        // ---- 2) 確定演出(★5 / SECRET) ----
        if (special)
        {
            bool secret = rr == Rarity.Secret;
            GameAudio.Play(Sfx.Thud, 1f, 0.7f);
            yield return Anim(secret ? 1.4f : 1.0f, u =>
            {
                float amp = 14f * u;
                shake.anchoredPosition = new Vector2(UnityEngine.Random.Range(-amp, amp), UnityEngine.Random.Range(-amp, amp));
                Color c = secret ? Color.HSVToRGB(Mathf.Repeat(u * 2.5f, 1f), 0.7f, 1f) : Color.Lerp(UiKit.Gold, tint, u);
                glow.color = c;
                ball.color = Color.Lerp(Color.white, c, 0.6f);
                float s = 1f + 0.5f * u;
                glow.rectTransform.localScale = Vector3.one * s * (1f + 0.25f * Mathf.Sin(u * 40f));
                _dim.color = new Color(secret ? 0.05f * u : 0.25f * u, 0f, secret ? 0.08f * u : 0f, 0.9f);
            }, false);
            shake.anchoredPosition = Vector2.zero;
            // 大きな文字(確定)
            var stamp = UiKit.Txt(stage, "Stamp", secret ? "SECRET" : "★5", secret ? 110 : 130,
                secret ? new Color(1f, 0.75f, 1f) : UiKit.GoldLight, TextAnchor.MiddleCenter);
            UiKit.Place(stamp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 160f));
            UiKit.Style(stamp, TelopFx.Kind.Big);
            GameAudio.Play(Sfx.HitPerfect, 1f, 0.8f);
            BgmPlayer.Jingle(secret ? "bgm-rarity-secret" : "bgm-rarity-5");
            GameAudio.Play(Sfx.BigCheer, 0.9f);
            for (int k = 0; k < (secret ? 8 : 5); k++)
                _fx.Firework(new Vector2(UnityEngine.Random.Range(-420f, 420f), -300f), UnityEngine.Random.Range(300f, 480f),
                    secret ? Color.HSVToRGB(UnityEngine.Random.value, 0.6f, 1f) : (k % 2 == 0 ? UiKit.Gold : tint));
            yield return Anim(0.9f, u =>
            {
                float s = Mathf.Lerp(3f, 1f, Mathf.Clamp01(u * 3f));
                stamp.rectTransform.localScale = Vector3.one * s;
                var c = stamp.color; c.a = u < 0.75f ? 1f : Mathf.Lerp(1f, 0f, (u - 0.75f) / 0.25f);
                stamp.color = c;
            }, false);
            Destroy(stamp.gameObject);
        }

        // ---- 3) 閃光 → カード登場 ----
        var flash = UiKit.Img(stage, "Flash", SpriteFactory.White, Color.white);
        UiKit.Fill(flash.rectTransform);
        Destroy(glow.gameObject);
        Destroy(ball.gameObject);
        _fx.Burst(Vector2.zero, rich ? 90 : 40, new[] { tint, Color.white, UiKit.GoldLight }, rich ? 700f : 480f, 1.2f, rich ? 18f : 12f, 160f);
        // 排出の曲: SECRET > ★5 > ★4 > 通常(★5以上は確定演出で鳴らし済み)
        if (rr == Rarity.R4) BgmPlayer.Jingle("bgm-rarity-4");
        else if (!special)
        {
            GameAudio.Play(rr == Rarity.R3 ? Sfx.HitGood : Sfx.HitNormal, 0.7f, 1.2f);
            if (!multi || index == 0) BgmPlayer.Jingle("bgm-rarity-low", 6f);
        }
        if (rich) _fx.Confetti(1000f, 300f, special ? 120 : 60, new[] { UiKit.Gold, UiKit.Red, Color.white, UiKit.GoldLight });

        Vector2 size = new Vector2(280f, 420f);
        var card = CardWidget.Build(stage, d, size, true, true, r.Add.After);
        card.Root.anchorMin = card.Root.anchorMax = new Vector2(0.5f, 0.5f);
        card.Root.anchoredPosition = new Vector2(0f, 20f);
        card.Frame.raycastTarget = false;
        flash.transform.SetAsLastSibling();
        var name = UiKit.Style(UiKit.Txt(stage, "Name", d.Name, 32, Color.white, TextAnchor.MiddleCenter), TelopFx.Kind.Heading);
        UiKit.Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -222f), new Vector2(700f, 44f));
        UiKit.FitName(name, d.Name, 680f, 0f, 32, 24);
        string sub = r.Add.Capped ? "MAX" : (r.Add.IsNew ? "" : "重ね " + r.Add.After + " / " + CharaCatalog.StackMax(d));
        var subT = UiKit.Txt(stage, "Sub", sub, 20, UiKit.GoldLight, TextAnchor.MiddleCenter);
        UiKit.Place(subT.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -252f), new Vector2(500f, 28f));
        if (r.Add.IsNew)
        {
            var nb = UiKit.Img(card.Root, "New", UiKit.ButtonRed, Color.white);
            UiKit.Place(nb.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-18f, -14f), new Vector2(84f, 36f));
            nb.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            var nt = UiKit.Txt(nb.transform, "T", "NEW", 20, Color.white, TextAnchor.MiddleCenter);
            UiKit.Fill(nt.rectTransform);
        }

        yield return Anim(0.45f, u =>
        {
            float a = Mathf.Clamp01(1f - u * 1.6f);
            flash.color = new Color(1f, 1f, 1f, a);
            bgRays.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(0f, rich ? 0.9f : 0.55f, u));
            // 裏返るように横から開いて、少し弾む
            float sx = Mathf.Clamp01(u * 1.8f);
            float bounce = 1f + 0.12f * Mathf.Sin(Mathf.Clamp01(u * 1.3f) * Mathf.PI);
            card.Root.localScale = new Vector3(sx * bounce, bounce, 1f);
            name.color = new Color(1f, 1f, 1f, u);
        });
        flash.color = new Color(1f, 1f, 1f, 0f);

        // ---- 4) 待ち(放射光を回し続ける。タップで次へ) ----
        _tapped = false;
        float wait = multi && !rich ? 0.9f : 0f;
        float t = 0f;
        float nextFw = 0.6f;
        while (!_tapped && (wait <= 0f || t < wait))
        {
            t += Time.unscaledDeltaTime;
            bgRays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -t * 14f);
            if (rr == Rarity.Secret)
            {
                // SECRET は「ネタ枠」: 規格外に豪華なのに、カード自体がぐにゃぐにゃ揺れる
                float w = Mathf.Sin(t * 5.5f) * 9f * Mathf.Max(0.25f, 1f - t * 0.15f);
                card.Root.localRotation = Quaternion.Euler(0f, 0f, w);
                float sq = 1f + Mathf.Sin(t * 11f) * 0.06f;
                card.Root.localScale = new Vector3(2f - sq, sq, 1f);
                if (Mathf.Repeat(t, 1.8f) < Time.unscaledDeltaTime) GameAudio.Play(Sfx.Boing, 0.6f, UnityEngine.Random.Range(0.8f, 1.3f));
            }
            if (card.Shine != null)
            {
                float u = Mathf.Repeat(t * 0.6f, 1.4f);
                card.Shine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-size.x, size.x, u), 0f);
            }
            if (special && t > nextFw)
            {
                nextFw = t + 0.7f;
                _fx.Firework(new Vector2(UnityEngine.Random.Range(-420f, 420f), -300f), UnityEngine.Random.Range(300f, 460f),
                    rr == Rarity.Secret ? Color.HSVToRGB(UnityEngine.Random.value, 0.6f, 1f) : UiKit.Gold);
            }
            yield return null;
        }
        _tapped = false;
    }

    private IEnumerator ResultsGrid(RectTransform stage, List<PullResult> results)
    {
        var rays = UiKit.Img(stage, "Rays", UiKit.Rays, new Color(1f, 0.8f, 0.35f, 0.35f));
        UiKit.Place(rays.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 1300f));
        Vector2 cs = new Vector2(120f, 180f);
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            int col = i % 5, row = i / 5;
            var p = CardWidget.Build(stage, r.Chara, cs, false, true, r.Add.After);
            p.Root.anchorMin = p.Root.anchorMax = new Vector2(0.5f, 0.5f);
            p.Root.anchoredPosition = new Vector2((col - 2) * (cs.x + 16f), row == 0 ? 112f : -84f);
            p.Frame.raycastTarget = false;
            if (r.Add.IsNew)
            {
                var nb = UiKit.Img(p.Root, "New", UiKit.ButtonRed, Color.white);
                UiKit.Place(nb.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-10f, -8f), new Vector2(54f, 24f));
                var nt = UiKit.Txt(nb.transform, "T", "NEW", 13, Color.white, TextAnchor.MiddleCenter);
                UiKit.Fill(nt.rectTransform);
            }
            StartCoroutine(PopIn(p.Root, 0.2f));
            if (r.Chara.Rarity >= Rarity.R4)
                _fx.Burst(p.Root.anchoredPosition, 24, new[] { UiKit.Gold, UiKit.GoldLight, Color.white }, 260f, 0.8f, 10f, 60f);
        }
        var ok = UiKit.Btn(stage, "OK", "OK", new Vector2(220f, 56f), true, () => _tapped = true, 26);
        UiKit.Place(ok.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(220f, 56f));
        _tapped = false;
        float t = 0f;
        while (!_tapped)
        {
            t += Time.unscaledDeltaTime;
            rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 6f);
            yield return null;
        }
        _tapped = false;
    }
}
