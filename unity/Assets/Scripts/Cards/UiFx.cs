using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UiFx.cs
// UIの上で動く粒(火花・紙吹雪・花火)。時間停止中も動くよう実時間で更新する。
// ============================================================
public class UiFx : MonoBehaviour
{
    private class P
    {
        public RectTransform R; public Image I;
        public Vector2 Pos, Vel; public float Life, Max, Size, Grav, Drag;
        public Color C; public bool Rocket; public Color BurstColor; public int BurstN;
        public bool Spin;
    }

    private readonly List<P> _live = new List<P>();
    private readonly Stack<P> _pool = new Stack<P>();
    private RectTransform _layer;
    private readonly Color[] _burstColors = new Color[3];

    public static UiFx Create(RectTransform parent)
    {
        var r = UiKit.Fill(UiKit.Rect(parent, "Fx"));
        var fx = r.gameObject.AddComponent<UiFx>();
        fx._layer = r;
        return fx;
    }

    private P Get()
    {
        P p = _pool.Count > 0 ? _pool.Pop() : null;
        if (p == null)
        {
            p = new P();
            p.I = UiKit.Img(_layer, "p", StadiumArt.Glow, Color.white);
            p.R = p.I.rectTransform;
            p.R.anchorMin = p.R.anchorMax = new Vector2(0.5f, 0.5f);
        }
        p.I.gameObject.SetActive(true);
        p.I.sprite = StadiumArt.Glow;
        p.Rocket = false; p.Spin = false;
        _live.Add(p);
        return p;
    }

    public void Clear()
    {
        foreach (var p in _live) { p.I.gameObject.SetActive(false); _pool.Push(p); }
        _live.Clear();
    }

    // 中心から放射状に飛び散る火花
    public void Burst(Vector2 pos, int n, Color[] colors, float speed, float life, float size, float grav = 260f)
    {
        for (int i = 0; i < n; i++)
        {
            var p = Get();
            float a = Random.value * Mathf.PI * 2f;
            float s = speed * Random.Range(0.35f, 1f);
            p.Pos = pos;
            p.Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s;
            p.Max = p.Life = life * Random.Range(0.6f, 1.1f);
            p.Size = size * Random.Range(0.6f, 1.3f);
            p.Grav = grav;
            p.Drag = 1.6f;
            p.C = colors[Random.Range(0, colors.Length)];
        }
    }

    // 打ち上げ花火(上がってから輪になって開く)
    public void Firework(Vector2 from, float height, Color c)
    {
        var p = Get();
        p.Pos = from;
        p.Vel = new Vector2(Random.Range(-40f, 40f), Mathf.Sqrt(2f * 420f * height));
        p.Max = p.Life = 3f;
        p.Size = 14f;
        p.Grav = 420f;
        p.Drag = 0f;
        p.C = Color.Lerp(c, Color.white, 0.5f);
        p.Rocket = true;
        p.BurstColor = c;
        p.BurstN = 46;
    }

    // 紙吹雪(上から舞い落ちる四角)
    public void Confetti(float width, float top, int n, Color[] colors)
    {
        for (int i = 0; i < n; i++)
        {
            var p = Get();
            p.I.sprite = SpriteFactory.White;
            p.Pos = new Vector2(Random.Range(-width * 0.5f, width * 0.5f), top + Random.Range(0f, 200f));
            p.Vel = new Vector2(Random.Range(-60f, 60f), Random.Range(-160f, -60f));
            p.Max = p.Life = Random.Range(2.5f, 4f);
            p.Size = Random.Range(7f, 13f);
            p.Grav = 40f;
            p.Drag = 0.3f;
            p.C = colors[Random.Range(0, colors.Length)];
            p.Spin = true;
        }
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            var p = _live[i];
            p.Life -= dt;
            if (p.Rocket && (p.Vel.y <= 30f || p.Life <= 0f))
            {
                Vector2 at = p.Pos;
                Color bc = p.BurstColor;
                p.Life = 0f;
                _burstColors[0] = bc;
                _burstColors[1] = Color.Lerp(bc, Color.white, 0.6f);
                _burstColors[2] = UiKit.GoldLight;
                Burst(at, p.BurstN, _burstColors, 380f, 1.3f, 12f, 120f);
                GameAudio.Play(Sfx.HitGood, 0.25f, Random.Range(0.5f, 0.7f));
            }
            if (p.Life <= 0f)
            {
                p.I.gameObject.SetActive(false);
                _pool.Push(p);
                _live.RemoveAt(i);
                continue;
            }
            p.Vel.y -= p.Grav * dt;
            p.Vel *= Mathf.Max(0f, 1f - p.Drag * dt);
            p.Pos += p.Vel * dt;
            float u = p.Life / p.Max;
            p.R.anchoredPosition = p.Pos;
            float sz = p.Size * (p.Rocket ? 1f : Mathf.Lerp(0.3f, 1f, u));
            p.R.sizeDelta = new Vector2(sz, p.Spin ? sz * 0.6f : sz);
            if (p.Spin) p.R.localRotation = Quaternion.Euler(0f, 0f, p.Life * 400f);
            var c = p.C; c.a = p.Rocket ? 1f : Mathf.Clamp01(u * 1.4f);
            p.I.color = c;
        }
    }
}
