Shader "Cheon/SimpleToon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Tint", Color) = (0.35, 0.42, 0.58, 1)
        _MidTone ("Midtone Brightness", Range(0, 1)) = 0.72
        _ShadowThreshold ("Shadow Threshold", Range(0, 0.5)) = 0.2
        _HighlightThreshold ("Highlight Threshold", Range(0.5, 1)) = 0.65
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.15
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap ("Emission Texture", 2D) = "white" {}
        _EmissionStrength ("Base Color Emission", Range(0, 4)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
        [Toggle] _AlphaClip ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Opacity ("Opacity", Range(0, 1)) = 1
        [HideInInspector] _Surface ("Surface", Float) = 0
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 0
        [HideInInspector] _ZWrite ("Depth Write", Float) = 1
        [HideInInspector] _VertexTint ("Vertex Color", Float) = 0
        [HideInInspector] _ReceiveShadows ("Receive Shadows", Float) = 1
        [HideInInspector] _EffectMode ("Effect Shape", Float) = 0
        [HideInInspector] _EffectSpeed ("Effect Speed", Float) = 0.25
        [HideInInspector] _WaterMode ("Water Flow", Float) = 0
        [HideInInspector] _FlowSpeed ("Flow Speed", Float) = 0.18
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap);
        SAMPLER(sampler_EmissionMap);

        // 모든 패스에서 동일한 상수 버퍼를 사용한다.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _EmissionMap_ST;
            half4 _BaseColor;
            half4 _ShadowColor;
            half4 _EmissionColor;
            half _MidTone;
            half _ShadowThreshold;
            half _HighlightThreshold;
            half _AmbientStrength;
            half _EmissionStrength;
            half _Cull;
            half _AlphaClip;
            half _Cutoff;
            half _Opacity;
            half _Surface;
            half _SrcBlend;
            half _DstBlend;
            half _ZWrite;
            half _VertexTint;
            half _ReceiveShadows;
            half _EffectMode;
            half _EffectSpeed;
            half _WaterMode;
            half _FlowSpeed;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            half4 color : COLOR;
            half fogFactor : TEXCOORD3;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings ToonVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = position.positionCS;
            output.positionWS = position.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = input.uv;
            output.color = input.color;
            output.fogFactor = ComputeFogFactor(position.positionCS.z);
            return output;
        }

        void ClipSurface(float2 uv)
        {
            if (_AlphaClip > 0.5)
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(uv, _BaseMap)).a;
                clip(alpha * _BaseColor.a - _Cutoff);
            }
        }

        half4 SampleSurface(Varyings input)
        {
            half4 surface = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(input.uv, _BaseMap)) * _BaseColor;
            surface *= lerp(half4(1, 1, 1, 1), input.color, _VertexTint);

            // 투명 효과의 기존 불꽃과 안개 모양을 유지한다.
            float2 uv = input.uv;
            float time = _Time.y * _EffectSpeed;
            if (_EffectMode > 1.5)
            {
                float2 offset = (uv - 0.5) * 2;
                float billow = sin(offset.x * 12 + time) * sin(offset.y * 9 - time * 0.5) * 0.12
                    + sin(offset.x * 27 + offset.y * 17) * 0.04;
                surface.a *= smoothstep(0, 0.45, 1 - dot(offset, offset) + billow) * 0.8;
            }
            else if (_EffectMode > 0.5)
            {
                float offset = (uv.x - 0.5) * 2 + sin(uv.y * 10 - time * 4) * uv.y * 0.2;
                float width = lerp(0.52, 0.025, pow(saturate(uv.y), 0.8));
                float shape = 1 - smoothstep(width - 0.08, width + 0.08, abs(offset));
                shape *= smoothstep(0, 0.12, uv.y) * (1 - smoothstep(0.88, 1, uv.y));
                surface.rgb = lerp(surface.rgb, half3(0.83, 1, 1), saturate((1 - abs(offset) / max(width, 0.01)) * uv.y));
                surface.a *= shape;
            }

            // 수면과 폭포의 흐르는 무늬도 툰 명암 아래에서 유지한다.
            if (_WaterMode > 0.5)
            {
                float flowTime = _Time.y * _FlowSpeed;
                float waves = sin(length(uv - 0.5) * 65 - flowTime * 2 + sin(uv.x * 12) * 0.4);
                float ribbons = smoothstep(0.93, 0.99, sin(uv.x * 47 + sin(uv.y * 7 - flowTime * 4) * 0.7));
                float foam = _WaterMode > 1.5 ? ribbons * 0.65 : smoothstep(0.97, 0.999, waves) * 0.2;
                surface.rgb = lerp(surface.rgb, half3(0.85, 0.98, 0.96), foam);
            }
            surface.a *= _Opacity;
            return surface;
        }
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            half4 ToonFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipSurface(input.uv);
                half4 surface = SampleSurface(input);
                half3 normalWS = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    shadowCoord = float4(GetNormalizedScreenSpaceUV(input.positionCS), 0, 1);
                #endif
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = lerp(1, mainLight.shadowAttenuation, _ReceiveShadows);
                half lightAmount = saturate(dot(normalWS, mainLight.direction)) * shadow;

                // 빛의 세기를 그림자·중간톤·밝은 면의 세 단계로 나눈다.
                half3 bandColor = lerp(_ShadowColor.rgb, _MidTone.xxx, step(_ShadowThreshold, lightAmount));
                bandColor = lerp(bandColor, half3(1, 1, 1), step(_HighlightThreshold, lightAmount));
                half3 ambient = max(SampleSH(normalWS), half3(0, 0, 0)) * _AmbientStrength;
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, TRANSFORM_TEX(input.uv, _EmissionMap)).rgb * _EmissionColor.rgb;
                half3 color = surface.rgb * (bandColor * mainLight.color * mainLight.distanceAttenuation + ambient + _EmissionStrength) + emission;
                return half4(MixFog(color, input.fogFactor), lerp(1, surface.a, _Surface));
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;

            Varyings ShadowVertex(Attributes input)
            {
                Varyings output = ToonVertex(input);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - output.positionWS);
                #else
                    float3 direction = _LightDirection;
                #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, direction)));
                return output;
            }
            half4 ShadowFragment(Varyings input) : SV_Target
            {
                ClipSurface(input.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalsFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipSurface(input.uv);
                float3 normalWS = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = PackNormalOctQuadEncode(normalWS);
                    return half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)), 0);
                #else
                    return half4(normalWS, 0);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonVertex
            #pragma fragment DepthFragment
            half4 DepthFragment(Varyings input) : SV_Target
            {
                ClipSurface(input.uv);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
