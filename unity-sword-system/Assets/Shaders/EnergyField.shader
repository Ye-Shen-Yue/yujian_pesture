Shader "YuJian/EnergyField"
{
    // 阵法能量场着色器 - Fresnel + 噪声动态效果
    Properties
    {
        _Color ("Field Color", Color) = (0.2, 0.6, 1, 0.3)
        _FresnelPower ("Fresnel Power", Range(0.5, 5)) = 2.5
        _NoiseScale ("Noise Scale", Range(0.1, 10)) = 3.0
        _NoiseSpeed ("Noise Speed", Range(0, 3)) = 0.5
        _Intensity ("Intensity", Range(0, 3)) = 1.0
        _NoiseTex ("Noise Texture", 2D) = "white" {}
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
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
            };

            fixed4 _Color;
            float _FresnelPower;
            float _NoiseScale;
            float _NoiseSpeed;
            float _Intensity;
            sampler2D _NoiseTex;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Fresnel效果
                float fresnel = pow(1.0 - saturate(dot(i.viewDir, i.worldNormal)), _FresnelPower);

                // 动态噪声
                float2 noiseUV = i.worldPos.xz * _NoiseScale + _Time.y * _NoiseSpeed;
                float noise = tex2D(_NoiseTex, noiseUV).r;

                // 组合
                float alpha = fresnel * noise * _Intensity;
                fixed4 col = _Color;
                col.a *= alpha;

                return col;
            }
            ENDCG
        }
    }
}
