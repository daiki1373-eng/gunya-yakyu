// Aポーズの体(RigSkin)用。部品(胴・腕・脚)ごとに、その部品の画素だけを描く。
// 頂点の uv2.x = 部品の番号、_LabelTex(アルファ) = 画素ごとの部品の番号。一致しない画素は透明にする。
// 部品の境目のマスは両方の部品で重ねて描くので、境目は網目の階段ではなく画素の線で切れる。
Shader "Gunya/RigParts"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LabelTex ("Label", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            sampler2D _LabelTex;
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float grp : TEXCOORD1; fixed4 color : COLOR; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.grp = v.uv2.x;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                float lab = tex2D(_LabelTex, i.uv).a;
                c.a *= step(abs(lab - i.grp), 0.06);
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
