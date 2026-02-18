Shader "YuJian/TrailDistortion"
{
    // 拖尾热扭曲效果 - 剑体高速移动时的空气扭曲
    Properties
    {
        _MainTex ("Trail Texture", 2D) = "white" {}
        _Color ("Trail Color", Color) = (0.5, 0.8, 1, 0.7)
        _DistortionStrength ("Distortion Strength", Range(0, 0.1)) = 0.02
        _ScrollSpeed ("Scroll Speed", Range(0, 5)) = 2.0
        _NoiseTex ("Noise Texture", 2D) = "bump" {}
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _NoiseTex;
            fixed4 _Color;
            float _DistortionStrength;
            float _ScrollSpeed;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 滚动噪声UV
                float2 noiseUV = i.uv + float2(_Time.y * _ScrollSpeed, 0);
                float2 noise = tex2D(_NoiseTex, noiseUV).rg * 2.0 - 1.0;

                // 扭曲采样UV
                float2 distortedUV = i.uv + noise * _DistortionStrength;

                fixed4 tex = tex2D(_MainTex, distortedUV);
                fixed4 col = tex * _Color * i.color;

                // 沿拖尾方向淡出
                col.a *= (1.0 - i.uv.x) * i.color.a;

                return col;
            }
            ENDCG
        }
    }
}
