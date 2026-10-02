Shader "SmashBots/PlanarReflection"
{
    Properties
    {
        _Strength("Strength", Range(0, 1)) = 0.4
        _Fresnel("Fresnel Power", Range(0, 8)) = 3
        _Blur("Blur", Range(0, 4)) = 1.5
    }

HLSLINCLUDE

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_PlanarReflectionTex);
SAMPLER(sampler_PlanarReflectionTex);
float4 _PlanarReflectionTex_TexelSize;

CBUFFER_START(UnityPerMaterial)
float _Strength;
float _Fresnel;
float _Blur;
CBUFFER_END

void VertReflection(float4 position : POSITION,
                    out float4 outPosition : SV_Position,
                    out float4 outScreen : TEXCOORD0,
                    out float3 outWorld : TEXCOORD1)
{
    outWorld = TransformObjectToWorld(position.xyz);
    outPosition = TransformWorldToHClip(outWorld);
    outScreen = ComputeScreenPos(outPosition);
}

float3 SampleReflection(float2 uv)
{
    return SAMPLE_TEXTURE2D(_PlanarReflectionTex, sampler_PlanarReflectionTex, uv).rgb;
}

float4 FragReflection(float4 position : SV_Position,
                      float4 screen : TEXCOORD0,
                      float3 world : TEXCOORD1) : SV_Target
{
    // The mirror camera renders a horizontally flipped image
    float2 uv = screen.xy / screen.w;
    uv.x = 1 - uv.x;

    // 12-tap disc blur, clamped to keep thin HDR lights from sparkling
    const float2 taps[12] =
    {
        float2( 1.00,  0.00), float2( 0.50,  0.87), float2(-0.50,  0.87),
        float2(-1.00,  0.00), float2(-0.50, -0.87), float2( 0.50, -0.87),
        float2( 0.43,  0.25), float2( 0.00,  0.50), float2(-0.43,  0.25),
        float2(-0.43, -0.25), float2( 0.00, -0.50), float2( 0.43, -0.25)
    };
    float2 d = _PlanarReflectionTex_TexelSize.xy * _Blur;
    float3 c = 0;
    for (int i = 0; i < 12; i++) c += min(SampleReflection(uv + taps[i] * d), 2.0);
    c /= 12;

    float3 v = normalize(GetCameraPositionWS() - world);
    float fresnel = pow(1 - saturate(v.y), _Fresnel);
    return float4(c, _Strength * lerp(0.15, 1, fresnel));
}

ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-100" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ReflectionPass"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex VertReflection
            #pragma fragment FragReflection
            ENDHLSL
        }
    }
}
