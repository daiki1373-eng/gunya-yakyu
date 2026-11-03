using System.Collections;
using UnityEngine;

// ============================================================
// MeetCursor.cs
// バット型のミートカーソル。ゾーン面(z=0)に重ねて描く。
//  - 外側の楕円 = 当たる範囲の縁(Weak)。横長なのでバットの形に見える
//  - 中心の明るい楕円 = 芯(Perfect)
//  - 左右どちらかに伸びる細い棒と丸 = グリップ(打者側)
// 大きさはミート能力で変わる。判定は HitJudge の楕円と完全に同じ形。
// ============================================================
public class MeetCursor : MonoBehaviour
{
    private const int Order = 10;

    private SpriteRenderer _body, _outline, _core, _grip, _knob;
    private Color _baseColor = new Color(1f, 0.93f, 0.45f);
    private float _halfW = 0.25f, _halfH = 0.15f;
    private int _batSide = -1;
    private Coroutine _flash;

    public static MeetCursor Create(Transform parent)
    {
        var go = new GameObject("MeetCursor");
        go.transform.SetParent(parent, false);
        var c = go.AddComponent<MeetCursor>();
        c.Build();
        return c;
    }

    private void Build()
    {
        Sprite circle = SpriteFactory.Circle;
        Sprite white = SpriteFactory.White;
        _body = BattingSprites.Make("Body", transform, circle, Color.white, Order,
            Vector3.zero, Vector3.zero, Vector3.one);
        _outline = BattingSprites.Make("Outline", transform, BattingSprites.Ring, Color.white, Order + 1,
            Vector3.zero, Vector3.zero, Vector3.one);
        _core = BattingSprites.Make("Core", transform, circle, Color.white, Order + 2,
            Vector3.zero, Vector3.zero, Vector3.one);
        _grip = BattingSprites.Make("Grip", transform, white, Color.white, Order, Vector3.zero, Vector3.zero, Vector3.one);
        _knob = BattingSprites.Make("Knob", transform, circle, Color.white, Order, Vector3.zero, Vector3.zero, Vector3.one);
        ApplyShape();
        ApplyColor(_baseColor);
    }

    public void Configure(float halfW, float halfH, int batSide)
    {
        _halfW = halfW;
        _halfH = halfH;
        _batSide = batSide;
        ApplyShape();
    }

    public void SetPos(Vector2 p)
    {
        transform.position = new Vector3(p.x, p.y, -0.05f);
    }

    public void SetVisible(bool v)
    {
        gameObject.SetActive(v);
    }

    private void ApplyShape()
    {
        float w = _halfW * 2f, h = _halfH * 2f;
        _body.transform.localScale = new Vector3(w, h, 1f);
        _outline.transform.localScale = new Vector3(w, h, 1f);
        float cw = w * BattingConfig.EllipsePerfect, ch = h * BattingConfig.EllipsePerfect;
        _core.transform.localScale = new Vector3(cw, ch, 1f);

        // グリップは打者側(右打者=左側)へ伸ばす
        float gripLen = _halfW * 0.9f;
        float gripThick = Mathf.Max(0.035f, _halfH * 0.22f);
        float gx = _batSide * (_halfW + gripLen * 0.5f - 0.01f);
        _grip.transform.localPosition = new Vector3(gx, 0f, 0f);
        _grip.transform.localScale = new Vector3(gripLen, gripThick, 1f);
        float kx = _batSide * (_halfW + gripLen);
        float kd = gripThick * 2.2f;
        _knob.transform.localPosition = new Vector3(kx, 0f, 0f);
        _knob.transform.localScale = new Vector3(kd, kd, 1f);
    }

    private void ApplyColor(Color c)
    {
        _body.color = new Color(c.r, c.g, c.b, 0.22f);
        _outline.color = new Color(c.r, c.g, c.b, 0.95f);
        _core.color = new Color(1f, 0.82f, 0.2f, 0.80f);
        _grip.color = new Color(c.r, c.g, c.b, 0.75f);
        _knob.color = new Color(c.r, c.g, c.b, 0.85f);
    }

    // スイング直後に判定ランクの色で光らせる
    public void FlashColor(Color c, float seconds)
    {
        if (_flash != null) StopCoroutine(_flash);
        _flash = StartCoroutine(FlashRoutine(c, seconds));
    }

    private IEnumerator FlashRoutine(Color c, float seconds)
    {
        ApplyColor(c);
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
        ApplyColor(_baseColor);
        _flash = null;
    }

    public void ResetColor()
    {
        if (_flash != null) { StopCoroutine(_flash); _flash = null; }
        ApplyColor(_baseColor);
    }
}
