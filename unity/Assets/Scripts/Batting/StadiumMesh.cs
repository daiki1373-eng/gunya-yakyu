using System;
using System.Collections.Generic;
using UnityEngine;

// 球場の曲面をなめらかな1枚の網目で作る(細かい板を並べたときの段差・ギザギザをなくす)。
// 地面の弧(外野の土・暗がり)と、曲がった壁(フェンス)。色は頂点色で上下・内外にグラデーションできる。
public static class StadiumMesh
{
    private static readonly Dictionary<Texture, Material> _mats = new Dictionary<Texture, Material>();

    public static Material Mat(Texture tex)
    {
        if (tex == null) tex = Texture2D.whiteTexture;
        Material m;
        if (_mats.TryGetValue(tex, out m) && m != null) return m;
        m = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
        _mats[tex] = m;
        return m;
    }

    private static MeshRenderer Make(Transform parent, string name, Mesh mesh, Texture tex, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = Mat(tex);
        mr.sortingOrder = order;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return mr;
    }

    private static Vector3 Dir(float phi)
    {
        float r = phi * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
    }

    // 地面の弧。rIn/rOut は角度(度)→半径。colIn/colOut は内側/外側の頂点色。tile は模様の繰り返し(m)
    public static MeshRenderer GroundArc(Transform parent, string name, Texture tex, int order,
        float phiFrom, float phiTo, float step, Func<float, float> rIn, Func<float, float> rOut,
        Color colIn, Color colOut, float tile, float y = 0f)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tri = new List<int>();
        int n = Mathf.Max(2, Mathf.CeilToInt((phiTo - phiFrom) / step) + 1);
        for (int i = 0; i < n; i++)
        {
            float phi = Mathf.Lerp(phiFrom, phiTo, i / (float)(n - 1));
            Vector3 d = Dir(phi);
            Vector3 a = d * rIn(phi) + Vector3.up * y, b = d * rOut(phi) + Vector3.up * y;
            v.Add(a); v.Add(b);
            uv.Add(new Vector2(a.x / tile, a.z / tile)); uv.Add(new Vector2(b.x / tile, b.z / tile));
            col.Add(colIn); col.Add(colOut);
            if (i > 0)
            {
                int k = (i - 1) * 2;
                tri.Add(k); tri.Add(k + 2); tri.Add(k + 1);
                tri.Add(k + 1); tri.Add(k + 2); tri.Add(k + 3);
            }
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        return Make(parent, name, mesh, tex, order);
    }

    // 円すい状の帯(スタンドの傾斜面・ひさしの裏など)。内側の点 (rA(phi), yA) と外側の点 (rB(phi), yB) を結ぶ。
    // 横の模様は弧の長さ/uTile、縦は斜面の長さ/vTile で繰り返す。colA/colB は手前/奥の頂点色
    public static MeshRenderer ConeBand(Transform parent, string name, Texture tex, int order,
        float phiFrom, float phiTo, float step, Func<float, float> rA, float yA, Func<float, float> rB, float yB,
        Color colA, Color colB, float uTile, float vTile, float uOffset = 0f, int vSeg = 1)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tri = new List<int>();
        int n = Mathf.Max(2, Mathf.CeilToInt((phiTo - phiFrom) / step) + 1);
        vSeg = Mathf.Max(1, vSeg);
        int rowLen = vSeg + 1;
        float u = uOffset; Vector3 prev = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            float phi = Mathf.Lerp(phiFrom, phiTo, i / (float)(n - 1));
            Vector3 d = Dir(phi);
            Vector3 a = d * rA(phi) + Vector3.up * yA, b = d * rB(phi) + Vector3.up * yB;
            Vector3 mid = (a + b) * 0.5f;
            if (i > 0) u += (mid - prev).magnitude / uTile;
            prev = mid;
            float L = (b - a).magnitude;
            for (int k = 0; k <= vSeg; k++)
            {
                float t = k / (float)vSeg;
                v.Add(Vector3.Lerp(a, b, t));
                uv.Add(new Vector2(u, L * t / vTile));
                col.Add(Color.Lerp(colA, colB, t));
            }
            if (i > 0)
            {
                int c0 = (i - 1) * rowLen, c1 = i * rowLen;
                for (int k = 0; k < vSeg; k++)
                {
                    tri.Add(c0 + k); tri.Add(c0 + k + 1); tri.Add(c1 + k);
                    tri.Add(c0 + k + 1); tri.Add(c1 + k + 1); tri.Add(c1 + k);
                }
            }
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        return Make(parent, name, mesh, tex, order);
    }

    // 曲がった壁。下端 y0・上端 y1、colBottom/colTop は上下の頂点色。uTile は横の繰り返し(m)
    public static MeshRenderer CurvedWall(Transform parent, string name, Texture tex, int order,
        float phiFrom, float phiTo, float step, Func<float, float> r, float y0, float y1,
        Color colBottom, Color colTop, float uTile, float inset = 0f)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tri = new List<int>();
        int n = Mathf.Max(2, Mathf.CeilToInt((phiTo - phiFrom) / step) + 1);
        float u = 0f; Vector3 prev = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            float phi = Mathf.Lerp(phiFrom, phiTo, i / (float)(n - 1));
            Vector3 p = Dir(phi) * (r(phi) - inset);
            if (i > 0) u += (p - prev).magnitude / uTile;
            prev = p;
            v.Add(p + Vector3.up * y0); v.Add(p + Vector3.up * y1);
            uv.Add(new Vector2(u, 0f)); uv.Add(new Vector2(u, 1f));
            col.Add(colBottom); col.Add(colTop);
            if (i > 0)
            {
                int k = (i - 1) * 2;
                tri.Add(k); tri.Add(k + 1); tri.Add(k + 2);
                tri.Add(k + 1); tri.Add(k + 3); tri.Add(k + 2);
            }
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        return Make(parent, name, mesh, tex, order);
    }
}
