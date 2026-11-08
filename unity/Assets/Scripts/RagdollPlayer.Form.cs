using UnityEngine;

// ============================================================
// RagdollPlayer.Form.cs
// 選手ごとのフォーム(CharaForm)どおりに体を動かす。
//   打者: 構え(バットの角度・腰の落とし) → 投球に合わせて足を上げる → スイング(軌道・大きさ)
//   投手: セット → 足を上げて腕をトップへ(腕の角度) → リリースで腕を振り切る(前のめり・腕の振り)
//   ぐにゃ度: 関節の固さ・可動域・起き上がる力・崩れ方の大きさ。
// 角度は体の面の中の回転(0=まっすぐ下/上)。元絵では前の腕(投げる腕・バットを持つ腕)が画面の外側 -Facing、
// 後ろの腕(グラブの腕)が +Facing の側にあるので、「外へ開く」は前の腕で -s、後ろの腕で +s。見た目だけで判定には使わない。
// ManualStep=true のときは Step() で進める(フォーム作成のプレビュー。別の物理シーンで動かす)。
// ============================================================
public partial class RagdollPlayer
{
    public CharaForm Form { get; private set; }
    public bool ManualStep;
    private float _clock;
    private bool _isolated;
    private float Now { get { return ManualStep ? _clock : Time.time; } }

    private enum FormPhase { Stance, Swing, Windup, Release, Free }
    private FormPhase _fPhase = FormPhase.Free;
    private float _fT;

    // 手で1歩進める(物理シーンの Simulate の前に呼ぶ)
    public void Step(float dt)
    {
        _clock += dt;
        Tick(dt);
    }

    // 別の物理シーンへ移す前に呼ぶ: ほかの選手との衝突の登録から外し、手で進める
    public void Isolate()
    {
        _isolated = true;
        ManualStep = true;
        ActivePlayers.Remove(this);
        foreach (var rb in _allParts) rb.interpolation = RigidbodyInterpolation2D.None;
    }

    private float OwnGunyaScale { get { return Form != null ? Form.GunyaScale : 1f; } }
    private float BalanceScale { get { return Form != null ? Mathf.Lerp(1.6f, 0.45f, CharaForm.U(Form.Gunya)) : 1f; } }
    private bool FormHoldsBody { get { return Form != null && _fPhase != FormPhase.Free; } }
    private float Stiff { get { return Mathf.Lerp(1.6f, 0.3f, CharaForm.U(Form.Gunya)); } }

    // フォームを使う(null で従来どおり)。決めポーズの絵はやめて体で動く
    public void SetForm(CharaForm f)
    {
        float before = Form != null ? JointExtra(Form) : 0f;
        Form = f;
        if (f != null && _poseCard != null) { _poseCard.Remove(); _poseCard = null; }
        // ぐにゃ度で関節の可動域を狭める/広げる(今の可動域からの差分)
        float delta = (f != null ? JointExtra(f) : 0f) - before;
        if (delta != 0f)
        {
            for (int i = 0; i < _joints.Count; i++)
            {
                var j = _joints[i].Joint;
                if (j == null) continue;
                var lim = j.limits;
                lim.min = Mathf.Clamp(lim.min - delta, -180f, 0f);
                lim.max = Mathf.Clamp(lim.max + delta, 0f, 180f);
                j.limits = lim;
            }
        }
        OnFormReset();
    }

    private static float JointExtra(CharaForm f) { return Mathf.Lerp(-30f, 30f, CharaForm.U(f.Gunya)); }

    private void OnFormReset()
    {
        if (Form == null) return;
        _fPhase = FormPhase.Stance;
        _fT = 0f;
    }

    // 角度をねらう(PD)。強さはぐにゃ度で弱くなる
    private void Aim(Rigidbody2D rb, float target, float k, float d)
    {
        if (rb == null) return;
        float st = Stiff;
        float err = Mathf.DeltaAngle(rb.rotation, target) * Mathf.Deg2Rad;
        float av = rb.angularVelocity * Mathf.Deg2Rad;
        rb.AddTorque(rb.inertia * (k * st * err - d * Mathf.Sqrt(st) * av));
    }

    // 腰を支える(下がりすぎないように持ち上げるだけ)
    private void HoldPelvis(float y)
    {
        if (Pelvis == null) return;
        if (_massTotal < 0f) { _massTotal = 0f; foreach (var rb in _allParts) _massTotal += rb.mass; }
        float dy = y - Pelvis.position.y;
        if (dy > 0f)
        {
            // ぐにゃ度が高いほど支えが弱く、崩れやすい
            float a = Mathf.Clamp(60f * dy - 8f * Pelvis.velocity.y, 0f, 40f) * Mathf.Lerp(1.6f, 0.35f, CharaForm.U(Form.Gunya));
            Pelvis.AddForce(new Vector2(0f, _massTotal * a));
        }
    }

    private static float Bump(float t, float a, float b, float c, float d)
    {
        // a→b で 0→1、c まで 1、c→d で 1→0
        if (t <= a || t >= d) return 0f;
        if (t < b) return Mathf.SmoothStep(0f, 1f, (t - a) / Mathf.Max(0.001f, b - a));
        if (t <= c) return 1f;
        return Mathf.SmoothStep(1f, 0f, (t - c) / Mathf.Max(0.001f, d - c));
    }

    // 投球に合わせて足を上げる時刻(ResetPose からの秒)。投手の始動 + 投球モーションに合わせる
    public static float KickStart { get { return BattingConfig.PitchReadySeconds + BattingConfig.WindupSeconds * 0.5f; } }
    public static float KickPeak { get { return BattingConfig.PitchReadySeconds + BattingConfig.WindupSeconds + 0.1f; } }

    private void TickForm(float dt)
    {
        if (Form == null) return;
        _fT += dt;
        var F = Form;
        float s = Facing;
        switch (_fPhase)
        {
            case FormPhase.Stance:
                if (Bat != null) BatStance(s, F, Bump(_fT, KickStart, KickPeak, KickPeak + 0.25f, KickPeak + 0.6f));
                else PitchSet(s, F);
                break;
            case FormPhase.Swing:
                SwingFinish(s, F);
                if (_fT > 0.9f) _fPhase = FormPhase.Free;
                break;
            case FormPhase.Windup:
                Windup(s, F, _fT / BattingConfig.WindupSeconds);
                if (_fT >= BattingConfig.WindupSeconds) Release(s, F);
                break;
            case FormPhase.Release:
                ReleaseFinish(s, F);
                if (_fT > 0.6f) _fPhase = FormPhase.Free;
                break;
        }
    }

    // ---- 打者 ----
    private float Spread(CharaForm F) { return Mathf.Lerp(4f, 30f, CharaForm.U(F.Crouch)); }

    private void BatStance(float s, CharaForm F, float kick)
    {
        float a = Spread(F);
        float kickUp = Mathf.Lerp(15f, 105f, CharaForm.U(F.LegKick)) * kick;
        Aim(Pelvis, 0f, 500f, 45f);
        Aim(LegFrontUpper, s * (a + kickUp), 500f, 45f);
        Aim(LegFrontFore, s * (a * 0.3f + kickUp * 0.15f), 400f, 40f);
        Aim(LegBackUpper, -s * a, 500f, 45f);
        Aim(LegBackFore, -s * a * 0.3f, 400f, 40f);
        Aim(Chest, -s * 8f * kick, 500f, 45f);
        Aim(Head, 0f, 300f, 30f);
        Aim(ArmFrontUpper, -s * 115f, 900f, 60f);
        Aim(ArmFrontFore, -s * 165f, 900f, 60f);
        Aim(ArmBackUpper, -s * 50f, 900f, 60f);
        Aim(ArmBackFore, -s * 150f, 900f, 60f);
        Aim(Bat, s * Mathf.Lerp(85f, 0f, CharaForm.U(F.BatHold)), 600f, 40f);
        HoldPelvis(_pelvisHomeInit.y - Mathf.Lerp(0f, 0.30f, CharaForm.U(F.Crouch)) + 0.10f * kick * CharaForm.U(F.LegKick));
    }

    private void FormSwing()
    {
        _fPhase = FormPhase.Swing;
        _fT = 0f;
        _poseHoldTimer = 0f;
        if (_expr == Expr.Normal) SetExpression(Expr.Focus, 0.5f);
        var F = Form;
        float s = Facing;
        float size = Mathf.Lerp(0.6f, 1.5f, CharaForm.U(F.SwingSize));
        float g = Mathf.Sqrt(F.GunyaScale) * _stackSpin;
        if (Bat != null) Bat.angularVelocity = -1500f * s * size * g;
        if (ArmFrontFore != null) ArmFrontFore.angularVelocity = 1100f * s * size * g;
        if (ArmFrontUpper != null) ArmFrontUpper.angularVelocity = 700f * s * size * g;
        if (ArmBackUpper != null) ArmBackUpper.angularVelocity = 500f * s * size * g;
        if (Chest != null) Chest.angularVelocity = -150f * s * size * g;
    }

    private void SwingFinish(float s, CharaForm F)
    {
        float plane = CharaForm.U(F.SwingPlane);
        float size = CharaForm.U(F.SwingSize);
        float a = Spread(F);
        // 振り抜いた形へ(ねらいは弱め: 勢いでぐにゃっと行き過ぎる)
        Aim(Bat, -s * Mathf.Lerp(150f, 40f, plane), 250f, 12f);
        Aim(ArmFrontUpper, s * Mathf.Lerp(55f, 140f, plane), 300f, 18f);
        Aim(ArmFrontFore, s * Mathf.Lerp(70f, 165f, plane), 300f, 18f);
        Aim(ArmBackUpper, s * Mathf.Lerp(40f, 120f, plane), 300f, 18f);
        Aim(ArmBackFore, s * Mathf.Lerp(60f, 150f, plane), 300f, 18f);
        Aim(Chest, s * Mathf.Lerp(-5f, -25f, size) * (plane < 0.5f ? 1f : -0.6f), 260f, 22f);
        Aim(Pelvis, 0f, 300f, 30f);
        Aim(Head, 0f, 160f, 16f);
        Aim(LegFrontUpper, s * a, 400f, 40f);
        Aim(LegFrontFore, s * a * 0.3f, 300f, 30f);
        Aim(LegBackUpper, -s * a, 400f, 40f);
        Aim(LegBackFore, -s * a * 0.3f, 300f, 30f);
        HoldPelvis(_pelvisHomeInit.y - Mathf.Lerp(0f, 0.30f, CharaForm.U(F.Crouch)));
    }

    // ---- 投手 ----
    private static float Slot(CharaForm F) { return Mathf.Lerp(35f, 175f, CharaForm.U(F.ArmSlot)); }

    private void PitchSet(float s, CharaForm F)
    {
        Aim(Pelvis, 0f, 500f, 45f);
        Aim(LegFrontUpper, s * 6f, 500f, 45f);
        Aim(LegFrontFore, s * 2f, 400f, 40f);
        Aim(LegBackUpper, -s * 6f, 500f, 45f);
        Aim(LegBackFore, -s * 2f, 400f, 40f);
        Aim(Chest, 0f, 500f, 45f);
        Aim(Head, 0f, 300f, 30f);
        // セット: 両手を胸の前で合わせる
        Aim(ArmFrontUpper, s * 30f, 900f, 60f);
        Aim(ArmFrontFore, s * 125f, 900f, 60f);
        Aim(ArmBackUpper, -s * 30f, 900f, 60f);
        Aim(ArmBackFore, -s * 125f, 900f, 60f);
        HoldPelvis(_pelvisHomeInit.y);
    }

    private void FormPitch()
    {
        _fPhase = FormPhase.Windup;
        _fT = 0f;
        _poseHoldTimer = 0f;
        SetExpression(Expr.Focus, 0.7f);
    }

    private void Windup(float s, CharaForm F, float u)
    {
        float kick = Bump(u, 0.05f, 0.5f, 0.75f, 1.0f);
        float lift = Mathf.Lerp(30f, 115f, CharaForm.U(F.PLegKick)) * kick;
        float c = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.9f, u));
        float slot = Slot(F);
        Aim(Pelvis, 0f, 500f, 45f);
        Aim(LegFrontUpper, s * (6f + lift), 500f, 45f);
        Aim(LegFrontFore, s * (2f + lift * 0.1f), 400f, 40f);
        Aim(LegBackUpper, -s * 6f, 600f, 50f);
        Aim(LegBackFore, -s * 2f, 600f, 50f);
        Aim(Chest, s * 10f * kick, 500f, 45f);
        Aim(Head, 0f, 300f, 30f);
        // 投げる腕(前の腕): セット → トップ(腕の角度の外側)へ
        Aim(ArmFrontUpper, Mathf.Lerp(s * 30f, -s * (slot + 15f), c), 900f, 60f);
        Aim(ArmFrontFore, Mathf.Lerp(s * 125f, -s * (slot + 45f), c), 900f, 60f);
        // グラブの腕は外へ伸ばす
        Aim(ArmBackUpper, Mathf.Lerp(-s * 30f, s * 95f, c), 800f, 55f);
        Aim(ArmBackFore, Mathf.Lerp(-s * 125f, s * 100f, c), 800f, 55f);
        HoldPelvis(_pelvisHomeInit.y + 0.05f * kick);
    }

    private void Release(float s, CharaForm F)
    {
        _fPhase = FormPhase.Release;
        _fT = 0f;
        float arm = Mathf.Lerp(0.6f, 1.5f, CharaForm.U(F.ArmSwing));
        float g = Mathf.Sqrt(F.GunyaScale) * _stackSpin;
        // 腕を外の上から、体の前を通って反対の腰へ振り下ろす
        if (ArmFrontUpper != null) ArmFrontUpper.angularVelocity = s * 1000f * arm * g;
        if (ArmFrontFore != null) ArmFrontFore.angularVelocity = s * 1500f * arm * g;
        if (Chest != null) Chest.angularVelocity = -s * 70f * arm * g;
    }

    private void ReleaseFinish(float s, CharaForm F)
    {
        float lean = CharaForm.U(F.Lean);
        float lift = CharaForm.U(F.PLegKick);
        Aim(ArmFrontUpper, s * 40f, 200f, 12f);
        Aim(ArmFrontFore, s * 75f, 200f, 12f);
        Aim(ArmBackUpper, -s * 30f, 400f, 30f);
        Aim(ArmBackFore, -s * 110f, 400f, 30f);
        Aim(Pelvis, -s * 8f * lean, 500f, 45f);
        Aim(Chest, -s * Mathf.Lerp(5f, 40f, lean), 400f, 36f);
        Aim(Head, -s * Mathf.Lerp(0f, 20f, lean), 250f, 24f);
        Aim(LegFrontUpper, s * Mathf.Lerp(12f, 35f, lift), 600f, 50f);
        Aim(LegFrontFore, s * 4f, 500f, 45f);
        Aim(LegBackUpper, -s * Mathf.Lerp(10f, 45f, lean), 500f, 45f);
        Aim(LegBackFore, -s * Mathf.Lerp(10f, 70f, lean), 500f, 45f);
        HoldPelvis(_pelvisHomeInit.y - 0.04f - 0.12f * lean);
    }
}
