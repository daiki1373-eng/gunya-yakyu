using UnityEngine;
using UnityEngine.UI;

// ============================================================
// AppShell.KeyVisual.cs
// タイトルとホームの見た目: 背景は試合の球場(3D)を夜空寄りに映し、カメラがゆっくり動く。
// キャラは決めポーズの絵を左右の列に1人ずつ置き、ロゴ・ボタンは真ん中(ホームは右)の列に置く。
// どの絵も互いに重ねない(列の幅は画面の幅から毎フレーム計算する)。
// ============================================================
public partial class AppShell
{
    // 決めポーズの絵の中で体が占める範囲(画像に対する割合: 左・上・右・下)。prep_poses.py の出力から測った値
    private struct PoseBox { public float L, T, R, B; public PoseBox(float l, float t, float r, float b) { L = l; T = t; R = r; B = b; } }
    private static PoseBox BoxOf(string name)
    {
        switch (name)
        {
            case "heroine_swing_ready": return new PoseBox(0.211f, 0.098f, 0.789f, 0.918f);
            case "robot-gold_pitch_release": return new PoseBox(0.182f, 0.098f, 0.816f, 0.918f);
            case "ojisan_swing_ready": return new PoseBox(0.092f, 0.078f, 0.908f, 0.944f);
            default: return new PoseBox(0.1f, 0.1f, 0.9f, 0.92f);
        }
    }

    private class KvChara { public RectTransform Rt; public PoseBox Box; public bool Left; public float Aspect; }

    private BattingStage _stageRef;
    private float _kvT0, _nextBurst;
    private static Sprite _shadeSprite;

    // 下と上を暗くする縦のグラデーション(背景の球場の上に重ねる幕。絵ではなく色の幕)
    private static Sprite Shade
    {
        get
        {
            if (_shadeSprite != null) return _shadeSprite;
            const int h = 128;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                float t = y / (h - 1f);                 // 0 = 下, 1 = 上
                float a = 0.30f + 0.62f * Mathf.Pow(Mathf.Clamp01((0.55f - t) / 0.55f), 1.3f) + 0.25f * Mathf.Pow(Mathf.Clamp01((t - 0.75f) / 0.25f), 2f);
                tex.SetPixel(0, y, new Color(0.02f, 0.03f, 0.10f, Mathf.Clamp01(a)));
            }
            tex.Apply();
            _shadeSprite = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
            return _shadeSprite;
        }
    }

    // 画面の背景(Bg)を、球場が透けて見える幕にする
    private static void UseStadiumBackdrop(RectTransform screen)
    {
        var bg = screen.Find("Bg");
        if (bg == null) return;
        var img = bg.GetComponent<Image>();
        img.sprite = Shade;
        img.type = Image.Type.Simple;
        img.color = Color.white;
    }

    // メニュー画面(カード・ガチャ・編成・オンライン): 球場の背景の上に、読みやすいよう紺の幕を重ねる
    private static void UseMenuBackdrop(RectTransform screen, float dim = 0.6f)
    {
        UseStadiumBackdrop(screen);
        var d = UiKit.Img(screen, "Dim", SpriteFactory.White, new Color(0.02f, 0.03f, 0.09f, dim));
        d.raycastTarget = false;
        UiKit.Fill(d.rectTransform);
        d.transform.SetSiblingIndex(1);
    }

    private KvChara AddChara(RectTransform parent, string poseName, bool left)
    {
        var sp = Resources.Load<Sprite>("Chara/Pose/" + poseName);
        if (sp == null) return null;
        var img = UiKit.Img(parent, "Chara_" + poseName, sp, Color.white);
        img.raycastTarget = false;
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(left ? 0f : 1f, 0f);
        r.pivot = new Vector2(left ? 0f : 1f, 0f);
        return new KvChara { Rt = r, Box = BoxOf(poseName), Left = left, Aspect = sp.rect.width / sp.rect.height };
    }

    // キャラの体の高さ(画面の高さに対する割合)と横の余白からキャラを置き、体が占める横幅(キャンバス単位)を返す
    private static float LayoutChara(KvChara c, float canvasH, float bodyFrac, float margin, float feet)
    {
        if (c == null) return 0f;
        float body = canvasH * bodyFrac;
        float imgH = body / (c.Box.B - c.Box.T);
        float imgW = imgH * c.Aspect;
        c.Rt.sizeDelta = new Vector2(imgW, imgH);
        float x = c.Left ? margin - c.Box.L * imgW : -(margin - (1f - c.Box.R) * imgW);
        c.Rt.anchoredPosition = new Vector2(x, feet - (1f - c.Box.B) * imgH);
        return (c.Box.R - c.Box.L) * imgW;
    }

    // ---- 背景の球場のカメラ(試合が始まる前のタイトル・ホームだけ動かす) ----
    private void TickBackdrop(float t)
    {
        bool titleOn = _title != null && _title.gameObject.activeSelf;
        bool homeOn = (_home != null && _home.gameObject.activeSelf) || MenuScreenOn();
        bool pre = !_gameStarted && (titleOn || homeOn);
        if (BattingUI.RootCanvas != null) BattingUI.RootCanvas.enabled = !pre;
        if (!pre) return;
        if (!_fieldersPreviewed && AtBatController.Current != null) { _fieldersPreviewed = true; AtBatController.Current.PreviewFielders(); }
        if (_stageRef == null) _stageRef = FindObjectOfType<BattingStage>();
        if (_stageRef == null) return;
        float a = (t - _kvT0) * 0.05f;
        Vector3 pos, look;
        if (titleOn)
        {
            // 起動直後は低い位置から夜空へ持ち上がる(クレーン)
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - _kvT0) / 2.6f));
            pos = new Vector3(Mathf.Sin(a) * 6f, Mathf.Lerp(1.0f, 2.0f, k), Mathf.Lerp(-3f, -9f, k));
            look = new Vector3(Mathf.Sin(a + 0.6f) * 18f, Mathf.Lerp(14f, 86f, k), 130f);   // 夜空と照明塔(電光掲示板は画面の外)
        }
        else if (MenuScreenOn())
        {
            // カード・ガチャ・編成・オンラインは、文字が読みやすいよう照明と夜空を見上げる
            pos = new Vector3(14f + Mathf.Sin(a) * 3f, 2.2f, 2f);
            look = new Vector3(-30f + Mathf.Sin(a * 0.7f) * 10f, 74f, 110f);
        }
        else
        {
            // ホームは本塁の後ろの高いところから、照明に照らされた球場全体を見る
            pos = new Vector3(Mathf.Sin(a) * 4f, 7f, -20f);
            look = new Vector3(Mathf.Sin(a * 0.7f) * 10f, 15f, 95f);
        }
        _stageRef.SetShot(pos, look, 0.9f, false);
        if (t > _nextBurst && !_titleIntroRunning)
        {
            _nextBurst = t + UnityEngine.Random.Range(5f, 8f);
            _stageRef.Stadium.LaunchFireworks(2);
        }
    }

    private bool _fieldersPreviewed;

    private bool MenuScreenOn()
    {
        return (_cards != null && _cards.gameObject.activeSelf) || (_gacha != null && _gacha.gameObject.activeSelf)
            || (_lineup != null && _lineup.gameObject.activeSelf) || (_onlineScr != null && _onlineScr.gameObject.activeSelf);
    }

    private void SnapBackdrop()
    {
        _kvT0 = Time.unscaledTime;
        _nextBurst = _kvT0 + 4f;
        if (_stageRef == null) _stageRef = FindObjectOfType<BattingStage>();
        if (_stageRef != null && !_gameStarted)
            _stageRef.SetShot(new Vector3(0f, 1.0f, -3f), new Vector3(0f, 14f, 130f), 0.9f, true);
    }

    // 画面全体を黒で覆い、少し待ってから明ける(試合の始まり用)
    private void CoverFade()
    {
        var cover = UiKit.Img(_root, "Cover", SpriteFactory.White, Color.black, true);
        UiKit.Fill(cover.rectTransform);
        cover.transform.SetAsLastSibling();
        StartCoroutine(CoverRoutine(cover));
    }

    private System.Collections.IEnumerator CoverRoutine(Image cover)
    {
        float t = 0f;
        while (t < 0.3f) { t += Time.unscaledDeltaTime; yield return null; }
        t = 0f;
        while (t < 0.45f)
        {
            t += Time.unscaledDeltaTime;
            cover.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(t / 0.45f));
            yield return null;
        }
        Destroy(cover.gameObject);
    }
}
