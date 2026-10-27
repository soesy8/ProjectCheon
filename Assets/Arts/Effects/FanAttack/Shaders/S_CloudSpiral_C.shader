Shader "Cheon/Effects/FanAttack/CloudSpiralC"
{
    Properties
    {
        [MainTexture] _BaseMap ("구름 원화", 2D) = "white" {}
        [MainColor] _BaseColor ("색상 / 투명도", Color) = (1, 1, 1, 1)
        [ToggleUI] _UseRadialOpacity ("안쪽 / 테두리 투명도 사용", Float) = 0
        [NoScaleOffset] _OpacityZoneMap ("안쪽 비율 / 외곽 거리 마스크", 2D) = "black" {}
        _BaseTransparency ("기본 투명도", Range(0, 1)) = 0
        _InnerTransparency ("안쪽 투명도", Range(0, 1)) = .25
        _OuterTransparency ("테두리 투명도", Range(0, 1)) = .25
        _InnerZoneRatio ("안쪽 적용 비율", Range(0, 1)) = .5
        _OuterEdgePixels ("테두리 폭 (원화 픽셀)", Range(0, 32)) = 8
        _InnerBlendWidth ("안쪽 경계 혼합 폭", Range(.001, .5)) = .18
        _EdgeBlendPixels ("테두리 경계 혼합 폭 (픽셀)", Range(.1, 12)) = 3
        _InnerRoughness ("안쪽 경계 러프 강도", Range(0, .25)) = .06
        _EdgeRoughPixels ("테두리 경계 러프 강도 (픽셀)", Range(0, 8)) = 2
        [HideInInspector] _SweepMask ("궤적 구간별 잔상", 2D) = "white" {}
        [HideInInspector] _UseSweepMask ("궤적 마스크 사용", Float) = 0
        [HideInInspector] _SweepReverse ("궤적 진행 반전", Float) = 0
        [HideInInspector] _SweepHead ("현재 휘두른 위치", Float) = 1
        [HideInInspector] _FitSweepArc ("수평 궤적 공격 범위 맞춤", Float) = 0
        [HideInInspector] _SweepArcAngle ("공격 범위 각도", Float) = 235
        [HideInInspector] _UseWindMask ("거리 방향 바람 궤적 사용", Float) = 0
        [HideInInspector] _WindMask ("바람 구간별 잔상", 2D) = "white" {}
        [HideInInspector] _WindHead ("현재 바람의 도달 거리", Float) = 0
        [HideInInspector] _WindTextureRect ("바람 원화의 실제 영역", Vector) = (0, 0, 1, 1)
        _WindRevealSoftness ("바람 생성 경계 혼합 폭", Range(.005, .15)) = .025
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_SweepMask);
            SAMPLER(sampler_SweepMask);
            TEXTURE2D(_OpacityZoneMap);
            SAMPLER(sampler_OpacityZoneMap);
            TEXTURE2D(_WindMask);
            SAMPLER(sampler_WindMask);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _UseRadialOpacity;
                float _BaseTransparency;
                float _InnerTransparency;
                float _OuterTransparency;
                float _InnerZoneRatio;
                float _OuterEdgePixels;
                float _InnerBlendWidth;
                float _EdgeBlendPixels;
                float _InnerRoughness;
                float _EdgeRoughPixels;
                float _UseSweepMask;
                float _SweepReverse;
                float _SweepHead;
                float _FitSweepArc;
                float _SweepArcAngle;
                float _UseWindMask;
                float _WindHead;
                float4 _WindTextureRect;
                float _WindRevealSoftness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            float OpacityNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 blend = frac(position);
                blend = blend * blend * (3 - 2 * blend);
                float4 corners = float4(dot(cell, float2(127.1, 311.7)),
                    dot(cell + float2(1, 0), float2(127.1, 311.7)),
                    dot(cell + float2(0, 1), float2(127.1, 311.7)),
                    dot(cell + 1, float2(127.1, 311.7)));
                corners = frac(sin(corners) * 43758.5453);
                return lerp(lerp(corners.x, corners.y, blend.x),
                    lerp(corners.z, corners.w, blend.x), blend.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 textureUV = input.uv;
                float arcOpacity = 1;
                if (_FitSweepArc > .5)
                {
                    // 원화의 약 235도 호를 정면 중심의 실제 공격 각도에 맞춘다.
                    // 같은 진행률을 잔상 마스크에도 사용하므로 표시 위치와 판정 진행이 일치한다.
                    float2 centered = input.uv - .5;
                    float radius = length(centered);
                    float along = .5 - atan2(centered.y, centered.x) / radians(max(1, _SweepArcAngle));
                    float sourceAngle = 1.309 - saturate(along) * 4.102;
                    float sourceSin, sourceCos;
                    sincos(sourceAngle, sourceSin, sourceCos);
                    textureUV = .5 + radius * float2(sourceCos, sourceSin);
                    // 범위 밖의 꼬리와 미세한 원화 잔여 픽셀을 제거하고 끝은 부드럽게 닫는다.
                    arcOpacity = smoothstep(0, .01, min(along, 1 - along));
                    arcOpacity *= 1 - smoothstep(.495, .5, radius);
                }
                // PNG의 빈 여백을 제외한 원화 전체를 실제 공격 부채꼴 안에 맞춘다.
                if (_UseWindMask > .5)
                    textureUV = lerp(_WindTextureRect.xy, _WindTextureRect.zw, input.uv);
                // 먼 바람도 근접 궤적과 같은 색으로 읽히게 조명·안개 혼합을 생략한다.
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, textureUV) * _BaseColor;
                color.a *= arcOpacity;
                if (_UseRadialOpacity > .5)
                {
                    // 원화의 실제 윤곽을 기준으로 두께의 안쪽 절반과 외곽 8픽셀을 구분한다.
                    // 마스크 R은 안쪽→바깥쪽 비율, G는 윤곽까지의 거리를 32픽셀 단위로 저장한다.
                    float2 zones = SAMPLE_TEXTURE2D(_OpacityZoneMap, sampler_OpacityZoneMap, textureUV).rg;
                    float rough = OpacityNoise(textureUV * 23) * .65
                        + OpacityNoise(textureUV * 57 + 7.3) * .25
                        + OpacityNoise(textureUV * 131 + 19.1) * .1;
                    rough = rough * 2 - 1;
                    float innerBoundary = _InnerZoneRatio + rough * _InnerRoughness;
                    float innerHalfWidth = max(.0005, _InnerBlendWidth * .5);
                    float innerWeight = 1 - smoothstep(innerBoundary - innerHalfWidth,
                        innerBoundary + innerHalfWidth, zones.r);
                    float edgeBoundary = _OuterEdgePixels + rough * _EdgeRoughPixels;
                    float edgeHalfWidth = max(.05, _EdgeBlendPixels * .5);
                    float edgeWeight = 1 - smoothstep(edgeBoundary - edgeHalfWidth,
                        edgeBoundary + edgeHalfWidth, zones.g * 32);
                    // 25% 투명도 영역이 겹쳐도 중복 곱셈으로 더 흐려지지 않게 합성한다.
                    float transparency = lerp(_BaseTransparency, _InnerTransparency, innerWeight);
                    transparency = lerp(transparency, _OuterTransparency, edgeWeight);
                    color.a *= 1 - transparency;
                }
                if (_UseSweepMask > .5)
                {
                    // UV는 고정하고, 원화의 위쪽 끝부터 아래 꼬리까지 지나간 구간만 순서대로 표시한다.
                    float angle = atan2(textureUV.y - .5, textureUV.x - .5);
                    float along = saturate((1.309 - angle) / 4.102);
                    along = lerp(along, 1 - along, _SweepReverse);
                    color.a *= SAMPLE_TEXTURE2D(_SweepMask, sampler_SweepMask, float2(along, .5)).r;
                    color.a *= smoothstep(0, .025, _SweepHead - along);
                }
                if (_UseWindMask > .5)
                {
                    // UV.x는 실제 판정과 같은 시작 반경→최대 반경 비율이다. 원화 자체는 이동하지 않는다.
                    float along = input.uv.x;
                    color.a *= SAMPLE_TEXTURE2D(_WindMask, sampler_WindMask, float2(along, .5)).r;
                    color.a *= smoothstep(0, max(.005, _WindRevealSoftness), _WindHead - along);
                }
                return color;
            }
            ENDHLSL
        }
    }
}
