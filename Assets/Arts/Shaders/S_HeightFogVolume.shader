Shader "Cheon/VFX/Height Fog Volume"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.76, 0.83, 0.85, 1)
        _Density ("Density", Range(0, 0.3)) = 0.065
        _HeightFade ("Top Fade Height", Float) = 10
        _EdgeFade ("Edge Fade Distance", Float) = 180
        _MaxOpacity ("Maximum Opacity", Range(0, 1)) = 0.96
        _NoiseScale ("Noise Scale", Float) = 0.035
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.65
        _WindVelocity ("Wind XZ", Vector) = (0.7, 0.3, 0, 0)
        [HideInInspector] _VolumeMin ("Volume Minimum", Vector) = (-250, -86, -290, 0)
        [HideInInspector] _VolumeMax ("Volume Maximum", Vector) = (250, 24, 290, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "HeightFogVolume"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            // 상자의 뒷면을 그려 카메라가 안개 내부에 들어와도 계속 표시한다.
            Cull Front

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float4 _VolumeMin;
                float4 _VolumeMax;
                float4 _WindVelocity;
                float _Density;
                float _HeightFade;
                float _EdgeFade;
                float _MaxOpacity;
                float _NoiseScale;
                float _NoiseStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float Hash(float2 position)
            {
                float3 value = frac(float3(position.xyx) * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float SmoothNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 fraction = frac(position);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);
                return lerp(
                    lerp(Hash(cell), Hash(cell + float2(1, 0)), fraction.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), fraction.x),
                    fraction.y);
            }

            float SampleFog(float3 position)
            {
                float2 noisePosition = (position.xz - _WindVelocity.xy * _Time.y) * _NoiseScale;
                noisePosition += position.y * float2(0.018, -0.012);
                float noise = SmoothNoise(noisePosition) * 0.7 + SmoothNoise(noisePosition * 2.07 + 13.5) * 0.3;
                // 흐르는 무늬에 따라 안개 윗면이 흔들리되 지정한 상한 높이를 넘지 않는다.
                float topOffset = (1.0 - noise) * _HeightFade * 0.5 * _NoiseStrength;
                float heightMask = smoothstep(0.0, _HeightFade, _VolumeMax.y - position.y - topOffset);
                float bottomMask = smoothstep(0.0, _HeightFade, position.y - _VolumeMin.y);
                float2 edgeDistance = min(position.xz - _VolumeMin.xz, _VolumeMax.xz - position.xz);
                float edgeMask = smoothstep(0.0, _EdgeFade, min(edgeDistance.x, edgeDistance.y));
                float variation = lerp(1.0, lerp(0.25, 1.75, noise), _NoiseStrength);
                return _Density * heightMask * bottomMask * edgeMask * variation;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                #if !UNITY_REVERSED_Z
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                float3 scenePosition = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                float3 rayOrigin = GetCameraPositionWS();
                float3 rayDirection = normalize(input.positionWS - rayOrigin);

                // Scene 뷰의 직교 카메라에서도 같은 월드 높이 기준을 유지한다.
                if (unity_OrthoParams.w > 0.5)
                {
                    #if UNITY_REVERSED_Z
                        float nearDepth = 1.0;
                    #else
                        float nearDepth = UNITY_NEAR_CLIP_VALUE;
                    #endif
                    rayOrigin = ComputeWorldSpacePosition(screenUV, nearDepth, UNITY_MATRIX_I_VP);
                    rayDirection = -UNITY_MATRIX_V[2].xyz;
                }

                float3 safeDirection = (step(0.0, rayDirection) * 2.0 - 1.0) * max(abs(rayDirection), 0.00001);
                float3 firstHit = (_VolumeMin.xyz - rayOrigin) / safeDirection;
                float3 secondHit = (_VolumeMax.xyz - rayOrigin) / safeDirection;
                float3 nearHits = min(firstHit, secondHit);
                float3 farHits = max(firstHit, secondHit);
                float entry = max(0.0, max(nearHits.x, max(nearHits.y, nearHits.z)));
                float exit = min(farHits.x, min(farHits.y, farHits.z));

                // 지형의 깊이까지만 안개를 적분하므로 높은 정상이나 앞쪽 물체를 덮지 않는다.
                float surfaceDistance = max(0.0, dot(scenePosition - rayOrigin, rayDirection));
                exit = min(exit, surfaceDistance);
                clip(exit - entry - 0.0001);

                const int SampleCount = 24;
                float stepLength = (exit - entry) / SampleCount;
                float opticalDepth = 0.0;
                [loop]
                for (int sampleIndex = 0; sampleIndex < SampleCount; sampleIndex++)
                {
                    float distance = entry + (sampleIndex + 0.5) * stepLength;
                    opticalDepth += SampleFog(rayOrigin + rayDirection * distance) * stepLength;
                }

                float opacity = min(1.0 - exp(-opticalDepth), _MaxOpacity);
                return half4(_FogColor.rgb, opacity);
            }
            ENDHLSL
        }
    }
}
