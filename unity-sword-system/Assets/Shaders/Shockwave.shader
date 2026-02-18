Shader "YuJian/Shockwave"
{
    // 径向冲击波扭曲着色器 - 破阵时的环形扩展效果
    Properties
    {
        _MainTex ("Screen Texture", 2D) = "white" {}
        _Center ("Wave Center", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Current Radius", Range(0, 2)) = 0.0
        _Width ("Wave Width", Range(0.01, 0.5)) = 0.1
        _Distortion ("Distortion Amount", Range(0, 0.2)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

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
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _Center;
            float _Radius;
            float _Width;
            float _Distortion;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 center = _Center.xy;
                float2 dir = i.uv - center;
                float dist = length(dir);

                // 冲击波环形区域
                float diff = abs(dist - _Radius);
                float waveFactor = 1.0 - smoothstep(0, _Width, diff);

                // UV扭曲
                float2 distortedUV = i.uv;
                if (dist > 0.001)
                {
                    float2 normalDir = dir / dist;
                    distortedUV += normalDir * waveFactor * _Distortion;
                }

                fixed4 col = tex2D(_MainTex, distortedUV);

                // 冲击波边缘高光
                col.rgb += waveFactor * float3(0.3, 0.5, 1.0) * 0.5;

                return col;
            }
            ENDCG
        }
    }
}
