using System.Collections.Generic;
using UnityEngine;

// ============================================================
// BgmPlayer.cs
// BGM(Resources/Audio/BGM の35曲)を鳴らす。
// - 常駐曲(ホーム・試合・ガチャ画面・図鑑など)はループ。切り替えはクロスフェード
// - 場面のジングル(ホームラン・三振・排出など)は常駐曲を小さくして上に重ねる
// - ジングルは優先度つき。鳴っている曲より低いものは、鳴り始めから2.5秒たつまで割り込めない
//   (排出は SECRET > ★5 > ★4 > 通常、サヨナラ・優勝は勝利曲より上)
// 曲の対応表は BRIEF_music30_addendum.md が正本。
// ============================================================
public static class BgmPlayer
{
    // 常駐曲
    public const string Home = "bgm-home", Match = "bgm-match", GachaScreen = "bgm-gacha", Team = "bgm-team-screen",
        Shop = "bgm-shop", Event = "bgm-event", Training = "bgm-training", Tournament = "bgm-tournament-entry",
        Champion = "bgm-champion";

    private static readonly Dictionary<string, int> Priority = new Dictionary<string, int>
    {
        { "bgm-rarity-secret", 100 }, { "bgm-entrance-secret", 95 }, { "bgm-rarity-5", 90 }, { "bgm-entrance-5", 85 },
        { "bgm-sayonara", 88 }, { "bgm-win", 80 }, { "bgm-lose", 80 }, { "bgm-rarity-4", 70 },
        { "bgm-comeback", 64 }, { "bgm-homerun", 62 }, { "bgm-hit-by-pitch", 58 }, { "bgm-rare-play", 56 },
        { "bgm-doubleplay", 54 }, { "bgm-error", 54 }, { "bgm-fineplay", 54 }, { "bgm-pitcher-hit", 52 },
        { "bgm-playball", 50 }, { "bgm-steal-ok", 48 }, { "bgm-steal-out", 48 }, { "bgm-bases-loaded", 46 },
        { "bgm-chance", 42 }, { "bgm-pinch", 42 }, { "bgm-strikeout", 36 }, { "bgm-gacha-suspense", 30 },
        { "bgm-rarity-low", 26 }, { "bgm-gacha-pull", 20 }, { "bgm-title", 60 }, { "bgm-versus", 45 },
    };

    public static readonly List<string> Log = new List<string>();   // 検証用: 鳴らしたジングルの記録
    public static float BaseVolume = 0.42f, JingleVolume = 0.85f, DuckVolume = 0.12f;

    private class Runner : MonoBehaviour
    {
        private void Update() { Tick(Time.unscaledDeltaTime); }
    }

    private static AudioSource _a, _b, _jingle;   // _a/_b はクロスフェード用
    private static AudioSource _cur;
    private static string _curName;
    private static string _jingleName;
    private static int _jinglePrio;
    private static float _jingleStart, _jingleEnd, _fadeOut;
    private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

    private static void Ensure()
    {
        if (_a != null) return;
        var go = new GameObject("BgmPlayer");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Runner>();
        _a = Make(go, true); _b = Make(go, true); _jingle = Make(go, false);
    }

    private static AudioSource Make(GameObject go, bool loop)
    {
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
        s.volume = 0f;
        s.ignoreListenerPause = true;
        return s;
    }

    private static AudioClip Clip(string name)
    {
        AudioClip c;
        if (Cache.TryGetValue(name, out c) && c != null) return c;
        c = Resources.Load<AudioClip>("Audio/BGM/" + name);
        if (c == null) Debug.LogWarning("BGMがありません: " + name);
        Cache[name] = c;
        return c;
    }

    public static string Current { get { return _curName; } }

    // 常駐曲を切り替える(同じ曲なら何もしない)
    public static void PlayBase(string name)
    {
        Ensure();
        if (_curName == name) return;
        var clip = Clip(name);
        if (clip == null) return;
        var next = _cur == _a ? _b : _a;
        next.clip = clip;
        next.volume = 0f;
        next.Play();
        _cur = next;
        _curName = name;
    }

    public static void StopBase()
    {
        Ensure();
        _curName = null;
        _cur = null;
    }

    // ジングルを鳴らす。fadeAfter 秒で切り上げる(0なら曲の終わりまで)
    public static bool Jingle(string name, float fadeAfter = 0f)
    {
        Ensure();
        int prio;
        if (!Priority.TryGetValue(name, out prio)) prio = 40;
        float now = Time.unscaledTime;
        bool busy = _jingle.isPlaying && now < _jingleEnd;
        if (busy && prio < _jinglePrio && now - _jingleStart < 2.5f) return false;
        var clip = Clip(name);
        if (clip == null) return false;
        if (Log.Count < 400) Log.Add(name);
        _jingle.Stop();
        _jingle.clip = clip;
        _jingle.volume = JingleVolume;
        _jingle.Play();
        _jingleName = name;
        _jinglePrio = prio;
        _jingleStart = now;
        float len = clip.length;
        _jingleEnd = now + (fadeAfter > 0f ? Mathf.Min(fadeAfter, len) : len);
        _fadeOut = 0f;
        return true;
    }

    public static void StopJingle(float fade = 0.4f)
    {
        if (_jingle == null || !_jingle.isPlaying) return;
        _jingleEnd = Mathf.Min(_jingleEnd, Time.unscaledTime);
        _fadeOut = fade;
    }

    public static bool JinglePlaying { get { return _jingle != null && _jingle.isPlaying && Time.unscaledTime < _jingleEnd; } }

    private static void Tick(float dt)
    {
        bool mute = GameAudio.Muted;
        bool jingleOn = _jingle != null && _jingle.isPlaying;
        // ジングルの切り上げ(フェードアウト)
        if (jingleOn && Time.unscaledTime >= _jingleEnd)
        {
            _jingle.volume = Mathf.MoveTowards(_jingle.volume, 0f, dt / Mathf.Max(0.05f, _fadeOut > 0f ? _fadeOut : 0.8f));
            if (_jingle.volume <= 0.001f) { _jingle.Stop(); jingleOn = false; }
        }
        float target = mute ? 0f : (jingleOn ? DuckVolume : BaseVolume) * GameAudio.MasterVolume;
        foreach (var s in new[] { _a, _b })
        {
            if (s == null) continue;
            float t = s == _cur ? target : 0f;
            s.volume = Mathf.MoveTowards(s.volume, t, dt * (s == _cur ? 0.6f : 0.9f));
            if (s != _cur && s.volume <= 0.001f && s.isPlaying) s.Stop();
        }
        if (_jingle != null) _jingle.mute = mute;
    }
}
