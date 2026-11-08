using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AtBatController
{
    // ------------------------------------------------------------
    // 入力とカーソル
    // ------------------------------------------------------------
    private void Update()
    {
        if (!AutoPlay && !InputBlocked) HandleInput();

        if (!_cursorLocked)
        {
            float spd = _gs.UserBatting ? BattingConfig.CursorSpeed : 6.0f;
            _cursorPos = Vector2.MoveTowards(_cursorPos, _cursorTarget, spd * Time.deltaTime);
        }
        if (_cursor != null) _cursor.SetPos(_cursorPos);
        if (_stage != null && !_gs.UserBatting) _stage.SetReticle(_cursorPos, _reticleOn, _reticleCol);
        if (_ui != null) _ui.Tick();
    }

    private Vector2 ClampCursor(Vector2 p)
    {
        if (!_gs.UserBatting)
            return new Vector2(Mathf.Clamp(p.x, -1.1f, 1.1f), Mathf.Clamp(p.y, 0.25f, 2.0f));
        float mx = BattingConfig.ZoneHalfW + BattingConfig.CursorMargin;
        float yMin = BattingConfig.ZoneBottom - BattingConfig.CursorMargin;
        float yMax = BattingConfig.ZoneTop + BattingConfig.CursorMargin;
        return new Vector2(Mathf.Clamp(p.x, -mx, mx), Mathf.Clamp(p.y, yMin, yMax));
    }

    // 操作(シンプル版)
    //   打つ: 画面に触れている間はミートの円が指を追い(指で隠れないよう少し上)、指を離した瞬間にスイング
    //   投げる: 投げたい場所に触れた瞬間、その場所へ投げる(長押し・メーターなし)。球種は画面のボタンで選ぶ
    //   盗塁・球種のボタンに触れた指は、離してもスイングしない
    private bool _pointerArmed;

    private void HandleInput()
    {
        if (_presentationInput)
        {
            _pointerArmed = false;
            bool tap = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return);
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).phase == TouchPhase.Began) tap = true;
            if (tap) _skipQueued = true;
            return;
        }
        bool swing = false, havePointer = false, pitchTap = false;
        Vector2 screen = Vector2.zero, tapScreen = Vector2.zero;

        if (Input.touchCount > 0)
        {
            Touch t0 = Input.GetTouch(0);
            if (t0.phase == TouchPhase.Began)
            {
                int tb = _ui.PitchButtonAt(t0.position);
                if (_ui.OverSteal(t0.position)) { ToggleSteal(); _pointerArmed = false; }
                else if (tb >= 0) { SelectPitchKind(tb); _pointerArmed = false; }
                else
                {
                    _pointerArmed = true;
                    if (!_gs.UserBatting) { pitchTap = true; tapScreen = t0.position; }
                }
            }
            if (_pointerArmed && _gs.UserBatting)
            {
                screen = t0.position + new Vector2(0f, Screen.height * 0.08f);
                havePointer = true;
                if (t0.phase == TouchPhase.Ended) swing = true;      // 離したら振る
            }
            if (t0.phase == TouchPhase.Ended || t0.phase == TouchPhase.Canceled) _pointerArmed = false;
        }
        else
        {
            Vector3 mp = Input.mousePosition;
            if ((mp - _lastMouse).sqrMagnitude > 4f) _keyboardMode = false;
            _lastMouse = mp;
            int mb = _ui.PitchButtonAt(mp);
            bool overUi = _ui.OverSteal(mp) || mb >= 0;
            // マウスは押していなくても円が追う(机の上での確認用)
            if (!_keyboardMode && Input.mousePresent && !overUi) { screen = mp; havePointer = true; }
            if (Input.GetMouseButtonDown(0))
            {
                if (_ui.OverSteal(mp)) { ToggleSteal(); _pointerArmed = false; }
                else if (mb >= 0) { SelectPitchKind(mb); _pointerArmed = false; }
                else
                {
                    _pointerArmed = true;
                    if (!_gs.UserBatting) { pitchTap = true; tapScreen = mp; }
                }
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (_pointerArmed && _gs.UserBatting) swing = true;
                _pointerArmed = false;
            }
        }

        // キーボード
        float kx = 0f, ky = 0f;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) kx -= 1f;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) kx += 1f;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) ky -= 1f;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) ky += 1f;
        if (kx != 0f || ky != 0f)
        {
            _keyboardMode = true;
            havePointer = false;
            _cursorTarget = ClampCursor(_cursorPos + new Vector2(kx, ky) * 0.6f);
        }
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))
            swing = true;
        for (int k = 0; k < 5; k++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + k)) SelectPitchKind(k);
        }
        if (Input.GetKeyDown(KeyCode.X)) ToggleSteal();
        if (Input.GetKeyDown(KeyCode.M)) GameAudio.Muted = !GameAudio.Muted;

        Vector2 w;
        if (havePointer && ScreenToZone(screen, out w)) _cursorTarget = ClampCursor(w);
        // 投げる: 触れた場所へ狙いを置いて、すぐ投げる
        if (pitchTap && ScreenToZone(tapScreen, out w))
        {
            _cursorTarget = ClampCursor(w);
            if (!_cursorLocked) _cursorPos = _cursorTarget;
            swing = true;
        }
        if (swing) _skipQueued = true;   // 登場演出のスキップ用
        if (swing && (_gs.UserBatting || _defTapOk)) _swingQueued = true;
    }

    // 画面の点 → ストライクゾーンの面(z=0)の上の点
    private bool ScreenToZone(Vector2 screen, out Vector2 w)
    {
        w = Vector2.zero;
        Ray ray = _stage.Cam.ScreenPointToRay(screen);
        if (Mathf.Abs(ray.direction.z) < 1e-4f) return false;
        float t = -ray.origin.z / ray.direction.z;
        Vector3 p = ray.origin + ray.direction * t;
        w = new Vector2(p.x, p.y);
        return true;
    }

    private void SelectPitchKind(int k)
    {
        if (_gs.UserBatting || k < 0 || k > 4 || !_avail[k]) return;
        _selKind = k;
        _ui.SelectPitch(k);
    }

    private bool ConsumeSwing()
    {
        if (!_swingQueued) return false;
        _swingQueued = false;
        return true;
    }

}
