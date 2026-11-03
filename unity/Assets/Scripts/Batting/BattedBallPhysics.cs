using System.Collections.Generic;
using UnityEngine;

// ============================================================
// BattedBallPhysics.cs
// 打球を「初速・打出角・方向・スピン」から物理で飛ばす。
// 重力 + 空気抵抗(速度の2乗) + マグヌス揚力(バックスピン) で積分し、
// バウンド・転がり・フェンス反射まで含めた軌道を先に全部計算して保持する。
// (再生側はこの軌道を時刻で読むだけ。結果判定も同じ軌道を使うので食い違わない)
//
// 座標系は BattingConfig と同じ。ここは純粋計算でシーンに依存しない。
// ============================================================
public class BattedBallTrajectory
{
    public const float Dt = 1f / 120f;

    public readonly List<Vector3> Points = new List<Vector3>();
    public float LaunchDeg;
    public int LandIndex = -1;          // 初回接地のステップ
    public Vector3 LandPos;
    public float HangTime;              // 初回接地までの滞空時間
    public float Apex;                  // 最高到達点
    public bool Foul;                   // 初回接地(または柵越え)がファウルゾーン
    public bool HomeRun;                // フェア側で柵を越えた
    public int HomeRunIndex = -1;
    public bool HitWall;
    public int WallIndex = -1;

    public float Duration { get { return (Points.Count - 1) * Dt; } }

    public float LandDistance
    {
        get { return new Vector2(LandPos.x, LandPos.z).magnitude; }
    }

    public Vector3 PosAt(float t)
    {
        if (Points.Count == 0) return Vector3.zero;
        float f = Mathf.Clamp(t / Dt, 0f, Points.Count - 1);
        int i = Mathf.Min((int)f, Points.Count - 1);
        int j = Mathf.Min(i + 1, Points.Count - 1);
        return Vector3.Lerp(Points[i], Points[j], f - i);
    }
}

public static class BattedBallPhysics
{
    private const float Gravity = 9.81f;
    private const float DragK = 0.0061f;   // 0.5*ρ*Cd*A/m
    private const float LiftK = 0.0035f;   // 揚力係数(2000rpm時)
    private const float MaxTime = 12f;

    public static BattedBallTrajectory Simulate(Vector3 start, float exitSpeed, float launchDeg,
        float sprayDeg, float backspinRpm, float tiltDeg)
    {
        var tr = new BattedBallTrajectory();
        tr.LaunchDeg = launchDeg;

        float sp = sprayDeg * Mathf.Deg2Rad;
        float la = launchDeg * Mathf.Deg2Rad;
        Vector3 dirH = new Vector3(Mathf.Sin(sp), 0f, Mathf.Cos(sp));
        Vector3 v = (dirH * Mathf.Cos(la) + Vector3.up * Mathf.Sin(la)) * exitSpeed;
        Vector3 p = start;
        float dt = BattedBallTrajectory.Dt;
        float tilt = tiltDeg * Mathf.Deg2Rad;
        bool landed = false;
        bool crossed = false;
        float t = 0f;
        tr.Points.Add(p);

        int maxSteps = (int)(MaxTime / dt);
        for (int step = 0; step < maxSteps; step++)
        {
            float speed = v.magnitude;
            Vector3 a = Vector3.down * Gravity - v * (speed * DragK);
            if (!landed && speed > 0.1f)
            {
                Vector3 vh = v / speed;
                Vector3 upPerp = Vector3.up - Vector3.Dot(Vector3.up, vh) * vh;
                upPerp = upPerp.sqrMagnitude > 1e-6f ? upPerp.normalized : Vector3.up;
                Vector3 side = Vector3.Cross(Vector3.up, vh);
                side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.right;
                Vector3 lift = upPerp * Mathf.Cos(tilt) + side * Mathf.Sin(tilt);
                a += lift * (LiftK * speed * speed * (backspinRpm / 2000f));
            }
            v += a * dt;
            p += v * dt;
            t += dt;

            // フェンス(フェア側の円弧)
            if (!crossed && p.z > 0f)
            {
                float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                float phi = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
                float rf = BattingConfig.FenceRadius(phi);
                if (r >= rf)
                {
                    if (p.y >= BattingConfig.FenceHeight)
                    {
                        crossed = true;
                        bool fairAtFence = Mathf.Abs(p.x) <= p.z;
                        if (fairAtFence)
                        {
                            tr.HomeRun = true;
                            tr.HomeRunIndex = tr.Points.Count;
                        }
                        else
                        {
                            tr.Foul = true;
                        }
                        if (!landed)
                        {
                            landed = true; // 柵の外へ出たので以降は落下だけ追う
                            tr.LandIndex = tr.Points.Count;
                            tr.LandPos = p;
                            tr.HangTime = t;
                        }
                    }
                    else if (Vector3.Dot(v, new Vector3(p.x, 0f, p.z)) > 0f)
                    {
                        Vector3 n = new Vector3(p.x, 0f, p.z).normalized;
                        float vr = Vector3.Dot(v, n);
                        v -= (1f + 0.35f) * vr * n;
                        p.x = n.x * (rf - 0.05f);
                        p.z = n.z * (rf - 0.05f);
                        if (!tr.HitWall) { tr.HitWall = true; tr.WallIndex = tr.Points.Count; }
                    }
                }
            }

            // 地面
            if (p.y <= 0f)
            {
                if (!landed)
                {
                    landed = true;
                    tr.LandIndex = tr.Points.Count;
                    tr.LandPos = p;
                    tr.LandPos.y = 0f;
                    tr.HangTime = t;
                    if (!tr.HomeRun) tr.Foul = p.z < 0f || Mathf.Abs(p.x) > p.z;
                }
                p.y = 0f;
                if (crossed)
                {
                    // 観客席の中: ここで止める
                    tr.Points.Add(p);
                    break;
                }
                if (Mathf.Abs(v.y) > 1.2f)
                {
                    v.y = -v.y * 0.42f;
                    v.x *= 0.82f;
                    v.z *= 0.82f;
                }
                else
                {
                    v.y = 0f;
                }
            }

            if (p.y > tr.Apex) tr.Apex = p.y;

            // 転がり: 一定の減速
            if (p.y <= 0.001f && v.y <= 0.001f)
            {
                float hs = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                float nhs = Mathf.Max(0f, hs - 3.2f * dt);
                if (hs > 1e-4f) { v.x *= nhs / hs; v.z *= nhs / hs; }
                if (nhs < 0.12f)
                {
                    tr.Points.Add(p);
                    break;
                }
            }

            tr.Points.Add(p);
        }

        if (tr.LandIndex < 0)
        {
            tr.LandIndex = tr.Points.Count - 1;
            tr.LandPos = tr.Points[tr.Points.Count - 1];
            tr.HangTime = t;
        }
        return tr;
    }
}
