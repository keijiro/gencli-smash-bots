Shader "SmashBots/Effect"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [HDR] _Color("Color", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
    }

HLSLINCLUDE

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
float4 _Color;
CBUFFER_END

void VertEffect(float4 position : POSITION,
                float2 texCoord : TEXCOORD0,
                float4 color : COLOR,
                out float4 outPosition : SV_Position,
                out float2 outTexCoord : TEXCOORD0,
                out float4 outColor : COLOR)
{
    outPosition = TransformObjectToHClip(position.xyz);
    outTexCoord = TRANSFORM_TEX(texCoord, _MainTex);
    outColor = color * _Color;
}

float4 FragEffect(float4 position : SV_Position,
                  float2 texCoord : TEXCOORD0,
                  float4 color : COLOR) : SV_Target
{
    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, texCoord) * color;
}

ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "EffectPass"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex VertEffect
            #pragma fragment FragEffect
            ENDHLSL
        }
    }
}
