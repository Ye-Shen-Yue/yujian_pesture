Shader "YuJian/SwordGlow"
{
    // 剑体自发光着色器 - 支持颜色参数控制（青/赤/玄/白四色）
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (0.8, 0.8, 0.9, 1)
        _GlowColor ("Glow Color", Color) = (0.3, 0.7, 1, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1.5
        _FresnelPower ("Fresnel Power", Range(0.1, 5)) = 2.0
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _GlowColor;
        float _GlowIntensity;
        float _FresnelPower;
        float _PulseSpeed;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
            float3 worldNormal;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = tex.rgb;
            o.Metallic = 0.8;
            o.Smoothness = 0.9;

            // Fresnel边缘发光
            float fresnel = pow(1.0 - saturate(dot(IN.viewDir, IN.worldNormal)), _FresnelPower);

            // 脉动效果
            float pulse = 0.8 + 0.2 * sin(_Time.y * _PulseSpeed);

            // 自发光 = Fresnel边缘光 + 基础发光
            o.Emission = _GlowColor.rgb * _GlowIntensity * (fresnel * 0.7 + 0.3) * pulse;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
