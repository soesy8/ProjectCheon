Shader "Cheon/Tests/FanAttack/CloudSpiralC"
{
    Properties
    {
        [MainTexture] _BaseMap ("구름 원화", 2D) = "white" {}
        [MainColor] _BaseColor ("색상 / 투명도", Color) = (1, 1, 1, 1)
        [HideInInspector] _SweepMask ("궤적 구간별 잔상", 2D) = "white" {}
        [HideInInspector] _UseSweepMask ("궤적 마스크 사용", Float) = 0
        [HideInInspector] _SweepReverse ("궤적 진행 반전", Float) = 0
        [HideInInspector] _SweepHead ("현재 휘두른 위치", Float) = 1
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
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _UseSweepMask;
                float _SweepReverse;
                float _SweepHead;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 먼 바람도 근접 궤적과 같은 색으로 읽히게 조명·안개 혼합을 생략한다.
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                if (_UseSweepMask > .5)
                {
                    // UV는 고정하고, 원화의 위쪽 끝부터 아래 꼬리까지 지나간 구간만 순서대로 표시한다.
                    float angle = atan2(input.uv.y - .5, input.uv.x - .5);
                    float along = saturate((1.309 - angle) / 4.102);
                    along = lerp(along, 1 - along, _SweepReverse);
                    color.a *= SAMPLE_TEXTURE2D(_SweepMask, sampler_SweepMask, float2(along, .5)).r;
                    color.a *= smoothstep(0, .025, _SweepHead - along);
                }
                return color;
            }
            ENDHLSL
        }
    }
}
