Shader "Cheon/Tests/FanAttack/Sweep"
{
    Properties
    {
        _BaseMap ("Wind grain", 2D) = "white" {}
        _Color ("Color", Color) = (0.5,1,0.85,0.8)
        _Head ("Sweep progress", Range(0,1)) = 1
        _Tail ("Visible tail", Range(0,1)) = 1
        _Opacity ("Opacity", Range(0,1)) = 1
        _Intensity ("Intensity", Range(0,5)) = 1.5
        _FlowSpeed ("Flow speed", Float) = 1.5
        _WindMode ("Radial wind mode", Float) = 0
        _RibbonShape ("Shaped sweep surface", Float) = 0
        _SurfaceFill ("Sweep surface fill", Range(0,1)) = 0
        _TipSerration ("Pointed sweep tips", Range(0,.15)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+15" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "FanWind"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Head, _Tail, _Opacity, _Intensity, _FlowSpeed, _WindMode;
            float _RibbonShape, _SurfaceFill, _TipSerration;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float front = 1 - smoothstep(_Head - .025, _Head + .025, uv.x);
                float tail = smoothstep(_Head - _Tail, _Head - _Tail + .15, uv.x);
                float ends = smoothstep(0, .035, uv.x) * (1 - smoothstep(.965, 1, uv.x));
                float sides = smoothstep(0, .12, uv.y) * (1 - smoothstep(.93, 1, uv.y));
                if (_RibbonShape > .5)
                {
                    // 폭 방향으로 끝 길이를 달리해 양끝과 진행 중 선단에 가는 바람 이빨을 만든다.
                    float teeth = pow(saturate(1 - abs(frac(uv.y * 5 + .13) * 2 - 1)), 3.2);
                    float uneven = .78 + .22 * sin(uv.y * 37 + 1.7);
                    float cut = _TipSerration * (1 - teeth) * uneven;
                    front = 1 - smoothstep(_Head - cut - .006, _Head - cut + .006, uv.x);
                    ends = smoothstep(cut * .45, cut * .45 + .012, uv.x)
                        * (1 - smoothstep(1 - cut - .008, 1 - cut + .002, uv.x));
                    sides = smoothstep(0, .06, uv.y) * (1 - smoothstep(.97, 1, uv.y));
                }
                // 궤적의 결도 공격 진행률을 따라가므로 정지/배속/방향과 어긋나지 않는다.
                float2 flowingUV = float2(uv.x * 1.6 - _Head * _FlowSpeed, uv.y);
                half grain = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, flowingUV).r;
                half fine = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, flowingUV * float2(.71, 1.8) + .29).g;
                float outerRim = exp(-pow((uv.y - .89) * 52, 2));
                float innerRim = exp(-pow((uv.y - .57) * 80, 2)) * .28;
                float bands = lerp(outerRim + innerRim, .12, _WindMode);
                float body = saturate((grain - .26) * 1.7) * (.23 + fine * .6) + bands;
                if (_RibbonShape > .5)
                    body = _SurfaceFill + saturate((grain - .18) * 1.5) * (.2 + fine * .32) + bands * .32;
                float alpha = saturate(body * sides * ends * front * tail * _Color.a * _Opacity);
                float edgeLight = pow(saturate(grain), 3) + bands;
                half3 color = lerp(_Color.rgb, half3(.92, 1, .94), saturate(edgeLight) * lerp(.6, .22, _RibbonShape));
                return half4(color * _Intensity, alpha);
            }
            ENDHLSL
        }
    }
}
