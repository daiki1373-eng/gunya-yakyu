// 画面全体の仕上げ(ブルーム・色調・ビネット)。PostFX.cs から使う。
Shader "Hidden/GunyaPost"
{
    Properties { _MainTex ("", 2D) = "white" {} }

    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex; float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float4 _Threshold;     // x=しきい値 y=ひざ z=上限
    float _Intensity, _Exposure, _Contrast, _Saturation, _Vignette, _Shoulder;
    float4 _Lift, _Gain, _VigColor;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

    half3 Box4(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb
                + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25h;
    }

    half4 fragPrefilter(v2f i) : SV_Target
    {
        half3 c = Box4(i.uv, 1.0);
        half br = max(c.r, max(c.g, c.b));
        half soft = clamp(br - _Threshold.x + _Threshold.y, 0, 2 * _Threshold.y);
        soft = soft * soft / (4 * _Threshold.y + 1e-4);
        half k = max(soft, br - _Threshold.x) / max(br, 1e-4);
        return half4(min(c * k, _Threshold.zzz), 1);
    }
    half4 fragDown(v2f i) : SV_Target { return half4(Box4(i.uv, 1.0), 1); }
    half4 fragUp(v2f i) : SV_Target { return half4(Box4(i.uv, 0.5), 1); }

    half3 Shoulder(half3 x)
    {
        // 明るいところだけなめらかに丸める(白飛びを防ぐ)
        half s = _Shoulder;
        half3 over = max(x - s, 0);
        half r = 1 - s;
        return min(x, s) + r * (1 - exp(-over / r));
    }

    half4 fragFinal(v2f i) : SV_Target
    {
        half4 src = tex2D(_MainTex, i.uv);
        half3 c = src.rgb + tex2D(_BloomTex, i.uv).rgb * _Intensity;
        c *= _Exposure;
        c = Shoulder(c);
        // 色調はガンマ空間で
        #if !defined(UNITY_COLORSPACE_GAMMA)
        c = LinearToGammaSpace(max(c, 0));
        #endif
        c = c * _Gain.rgb + _Lift.rgb * (1 - c);
        c = (c - 0.5) * _Contrast + 0.5;
        half l = dot(c, half3(0.299, 0.587, 0.114));
        c = lerp(l.xxx, c, _Saturation);
        float2 d = i.uv - 0.5; d.x *= _ScreenParams.x / _ScreenParams.y;
        half v = saturate(1 - _Vignette * dot(d, d) * 1.6);
        c = lerp(_VigColor.rgb, c, v);
        c = saturate(c);
        #if !defined(UNITY_COLORSPACE_GAMMA)
        c = GammaToLinearSpace(c);
        #endif
        return half4(c, src.a);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDown
            ENDCG }
        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragUp
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragFinal
            #pragma multi_compile __ UNITY_COLORSPACE_GAMMA
            ENDCG }
    }
}
