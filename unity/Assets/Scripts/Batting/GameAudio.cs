using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// GameAudio.cs
// 効果音をコードで合成して鳴らす(外部の音素材は使わない)。
// 打球音はランク別: Perfect=鋭い快音 / Good / Normal / Weak=鈍い音。
// 歓声はノイズを帯域とうねりで加工したもの。数値はすべて自前の仮置き。
// ============================================================
public enum Sfx { HitWeak, HitNormal, HitGood, HitPerfect, Whiff, Catch, Cheer, BigCheer, Boing, Thud, Whoosh, Click }

public static class GameAudio
{
    private const int Rate = 22050;
    private static AudioSource[] _srcs;
    private static int _next;
    private static readonly Dictionary<Sfx, AudioClip> Clips = new Dictionary<Sfx, AudioClip>();
    public static bool Muted = false;
    public static float MasterVolume = 0.8f;

    public static void Init(GameObject host)
    {
        if (_srcs != null && _srcs[0] != null) return;
        _srcs = new AudioSource[4];
        for (int i = 0; i < _srcs.Length; i++)
        {
            var s = host.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            _srcs[i] = s;
        }
        if (UnityEngine.Object.FindObjectOfType<AudioListener>() == null && Camera.main != null)
            Camera.main.gameObject.AddComponent<AudioListener>();
    }

    public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
    {
        if (Muted || _srcs == null || _srcs[0] == null) return;
        var clip = Get(sfx);
        var s = _srcs[_next];
        _next = (_next + 1) % _srcs.Length;
        s.pitch = pitch;
        s.PlayOneShot(clip, volume * MasterVolume);
    }

    public static void PlayHit(HitRank r)
    {
        switch (r)
        {
            case HitRank.Perfect: Play(Sfx.HitPerfect, 1.0f, UnityEngine.Random.Range(0.98f, 1.04f)); break;
            case HitRank.Good: Play(Sfx.HitGood, 0.9f, UnityEngine.Random.Range(0.96f, 1.04f)); break;
            case HitRank.Normal: Play(Sfx.HitNormal, 0.8f, UnityEngine.Random.Range(0.95f, 1.05f)); break;
            default: Play(Sfx.HitWeak, 0.7f, UnityEngine.Random.Range(0.94f, 1.06f)); break;
        }
    }

    // 対応端末だけ短く震わせる(任意機能)
    public static void Haptic()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    private static AudioClip Get(Sfx sfx)
    {
        AudioClip c;
        if (Clips.TryGetValue(sfx, out c) && c != null) return c;
        c = Build(sfx);
        Clips[sfx] = c;
        return c;
    }

    private static float Tau(float f, float t) { return Mathf.Sin(2f * Mathf.PI * f * t); }

    private static AudioClip Build(Sfx sfx)
    {
        float seconds;
        Func<float, float, float> gen;   // (t, noise[-1..1]) -> sample
        var lp = new float[1];           // ローパス状態
        var ph = new float[1];           // 位相

        switch (sfx)
        {
            case Sfx.HitPerfect:
                seconds = 0.30f;
                gen = (t, n) => 0.95f * n * Mathf.Exp(-t * 320f) + 0.45f * Tau(1180f, t) * Mathf.Exp(-t * 26f)
                    + 0.22f * Tau(2400f, t) * Mathf.Exp(-t * 42f) + 0.5f * Tau(230f, t) * Mathf.Exp(-t * 15f);
                break;
            case Sfx.HitGood:
                seconds = 0.24f;
                gen = (t, n) => 0.7f * n * Mathf.Exp(-t * 260f) + 0.45f * Tau(780f, t) * Mathf.Exp(-t * 30f)
                    + 0.45f * Tau(190f, t) * Mathf.Exp(-t * 18f);
                break;
            case Sfx.HitNormal:
                seconds = 0.20f;
                gen = (t, n) => 0.5f * n * Mathf.Exp(-t * 220f) + 0.4f * Tau(520f, t) * Mathf.Exp(-t * 34f)
                    + 0.4f * Tau(170f, t) * Mathf.Exp(-t * 22f);
                break;
            case Sfx.HitWeak:
                seconds = 0.16f;
                gen = (t, n) => 0.3f * n * Mathf.Exp(-t * 180f) + 0.45f * Tau(340f, t) * Mathf.Exp(-t * 40f)
                    + 0.3f * Tau(120f, t) * Mathf.Exp(-t * 30f);
                break;
            case Sfx.Whiff:
                seconds = 0.26f;
                gen = (t, n) =>
                {
                    float a = 0.04f + 0.5f * (t / 0.26f);
                    lp[0] += a * (n - lp[0]);
                    float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.26f));
                    return lp[0] * env * env * 1.6f;
                };
                break;
            case Sfx.Whoosh:
                seconds = 0.30f;
                gen = (t, n) =>
                {
                    lp[0] += 0.12f * (n - lp[0]);
                    float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.30f));
                    return lp[0] * env * env * 0.9f;
                };
                break;
            case Sfx.Catch:
                seconds = 0.16f;
                gen = (t, n) => 0.7f * Tau(190f - 420f * t, t) * Mathf.Exp(-t * 38f) + 0.35f * n * Mathf.Exp(-t * 120f);
                break;
            case Sfx.Thud:
                seconds = 0.34f;
                gen = (t, n) =>
                {
                    lp[0] += 0.08f * (n - lp[0]);
                    return 0.8f * Tau(68f, t) * Mathf.Exp(-t * 13f) + 1.3f * lp[0] * Mathf.Exp(-t * 22f);
                };
                break;
            case Sfx.Boing:
                seconds = 0.75f;
                gen = (t, n) =>
                {
                    float f = 110f + 330f * Mathf.Exp(-t * 2.6f) + 22f * Mathf.Sin(2f * Mathf.PI * 9f * t);
                    ph[0] += 2f * Mathf.PI * f / Rate;
                    return 0.6f * Mathf.Sin(ph[0]) * Mathf.Exp(-t * 3.4f) + 0.15f * Mathf.Sin(ph[0] * 2f) * Mathf.Exp(-t * 5f);
                };
                break;
            case Sfx.Cheer:
            case Sfx.BigCheer:
            {
                bool big = sfx == Sfx.BigCheer;
                seconds = big ? 2.8f : 1.7f;
                float dur = seconds;
                gen = (t, n) =>
                {
                    lp[0] += 0.16f * (n - lp[0]);
                    float up = Mathf.Clamp01(t / 0.35f);
                    float down = Mathf.Clamp01((dur - t) / (big ? 1.2f : 0.7f));
                    float wave = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 5.5f * t);
                    return lp[0] * 1.9f * up * down * wave * (big ? 1.1f : 0.8f);
                };
                break;
            }
            default:
                seconds = 0.05f;
                gen = (t, n) => 0.5f * Tau(1300f, t) * Mathf.Exp(-t * 90f);
                break;
        }

        int count = Mathf.Max(1, (int)(Rate * seconds));
        var data = new float[count];
        var rnd = new System.Random(1000 + (int)sfx * 17);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Rate;
            float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float v = gen(t, n);
            // クリックノイズ防止の短いフェードアウト
            float tail = Mathf.Clamp01((count - i) / (Rate * 0.01f));
            data[i] = Mathf.Clamp(v, -1f, 1f) * tail;
        }
        var clip = AudioClip.Create("sfx_" + sfx, count, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

// ============================================================
// Commentary.cs(同居) 実況の煽り文句。短い一言だけ。文言はすべて自作。
// ============================================================
public static class Commentary
{
    private static string Pick(string[] a) { return a[UnityEngine.Random.Range(0, a.Length)]; }

    private static readonly string[] Collision = { "ごっつんこ！", "おしくらまんじゅう！", "お見合いから大激突！" };
    private static readonly string[] Fence = { "壁に抱きついた！", "フェンスと一体化！", "ぐにゃ〜っと崩れ落ちる！" };
    private static readonly string[] Slip = { "足がぐにゃ〜！", "芝生に溶けた！", "ずるっと転倒！" };
    private static readonly string[] BadThrow = { "どこへ投げた！？", "ボールが迷子に！", "送球、旅に出る！" };
    private static readonly string[] HomeRun = { "ぐにゃっと場外へ！", "ボールが空に溶けた！", "特大アーチ！" };
    private static readonly string[] Perfect = { "ど真ん中の快音！", "芯でとらえた！", "バットが歌った！" };
    private static readonly string[] Hbp = { "ぐにゃっと吹っ飛ぶ！", "ごめんなさい！", "ふわっと宙に舞う！" };
    private static readonly string[] Whiff = { "バットがぐにゃり空振り！", "空気を切り裂いた！", "ブンッ！" };
    private static readonly string[] Dp = { "ダブルプレー！", "あっという間に二つ！" };

    public static string ForQuirk(QuirkKind k)
    {
        switch (k)
        {
            case QuirkKind.Collision: return Pick(Collision);
            case QuirkKind.FenceCrash: return Pick(Fence);
            case QuirkKind.Slip: return Pick(Slip);
            case QuirkKind.BadThrow: return Pick(BadThrow);
            default: return "";
        }
    }
    public static string ForHomeRun() { return Pick(HomeRun); }
    public static string ForPerfect() { return Pick(Perfect); }
    public static string ForHitByPitch() { return Pick(Hbp); }
    public static string ForWhiffK() { return Pick(Whiff); }
    public static string ForDoublePlay() { return Pick(Dp); }
}
