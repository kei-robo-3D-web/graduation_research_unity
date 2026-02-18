Shader "Custom/ToonShader"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _Shade ("Shade Color", Color) = (0.4,0.4,0.4,1)
        _RampThreshold ("Ramp Threshold", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf ToonRamp

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _Shade;
        float _RampThreshold;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = tex.rgb;
            o.Alpha = tex.a;
        }

        half4 LightingToonRamp(SurfaceOutput s, half3 lightDir, half atten)
        {
            half NdotL = dot(s.Normal, lightDir);
            half3 col = NdotL > _RampThreshold ? s.Albedo : s.Albedo * _Shade.rgb;
            return half4(col * _LightColor0.rgb * atten, s.Alpha);
        }

        ENDCG
    }
    FallBack "Diffuse"
}
