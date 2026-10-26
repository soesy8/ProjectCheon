Shader "Cheon/Tests/FanAttack/CloudWindSweep"
{
    Properties
    {
        _Color ("Wind tint", Color) = (0.12,0.78,0.73,0.95)
        _EdgeColor ("Mint outline", Color) = (0.68,1,0.92,1)
        _ShadowColor ("Deep teal", Color) = (0.025,0.36,0.42,1)
        _Head ("Sweep progress", Range(0,1)) = 1
        _Tail ("Visible tail", Range(0,1)) = 1
        _Opacity ("Opacity", Range(0,1)) = 1
        _Intensity ("Intensity", Range(0,5)) = 1.2
        _RibbonWidth ("Ribbon width", Range(0.5,1.5)) = 1
        _CloudScale ("Cloud curl size", Range(0.6,1.4)) = 1
        _OutlineWidth ("Outline width", Range(0.003,0.03)) = 0.012
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+16" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "CloudWind"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color, _EdgeColor, _ShadowColor;
            float _Head, _Tail, _Opacity, _Intensity;
            float _RibbonWidth, _CloudScale, _OutlineWidth;
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

            float SegmentDistance(float2 p, float2 a, float2 b)
            {
                float2 direction = b - a;
                float progress = saturate(dot(p - a, direction) / max(dot(direction, direction), 0.000001));
                return length(p - a - direction * progress);
            }

            // 구름 몸체의 둥근 돌기와 내부 소용돌이를 같은 좌표에 배치한다.
            // 넓은 면을 전부 채우지 않아 띠 사이로 배경과 캐릭터가 보인다.
            float CloudDistance(float2 p)
            {
                float shape = length(p - float2(-0.055, 0.025)) - 0.105;
                shape = min(shape, length(p - float2(0.015, 0.095)) - 0.105);
                shape = min(shape, length(p - float2(0.105, 0.065)) - 0.082);
                shape = min(shape, length(p - float2(0.105, -0.035)) - 0.078);
                shape = min(shape, length(p - float2(0.015, -0.065)) - 0.096);
                shape = min(shape, length(p - float2(-0.095, -0.055)) - 0.06);
                return shape;
            }

            float SpiralDistance(float2 p)
            {
                float distanceToSpiral = 10;
                float2 previous = float2(-0.165, -0.105);
                // 마지막 점이 구름 꼬리로 빠져나오는 한 바퀴 반의 선이다.
                [unroll]
                for (int index = 0; index <= 20; index++)
                {
                    float progress = index / 20.0;
                    float angle = -2.55 - progress * 8.15;
                    float radius = lerp(0.155, 0.008, progress);
                    float2 next = float2(cos(angle), sin(angle)) * radius;
                    distanceToSpiral = min(distanceToSpiral, SegmentDistance(p, previous, next));
                    previous = next;
                }
                return distanceToSpiral;
            }

            float RibbonDistance(float2 uv, float center, float width, float phase, float start, float end)
            {
                float progress = saturate((uv.x - start) / (end - start));
                float taper = pow(saturate(sin(progress * PI)), 0.65);
                float wave = sin(uv.x * 6.2 + phase) * 0.04 + sin(uv.x * 13 + phase) * 0.009;
                float distanceToBand = abs(uv.y - center - wave) - width * taper * _RibbonWidth;
                // 양끝의 폭이 0이 되는 뾰족한 띠를 만들며, 끝 밖의 가로선은 잘라낸다.
                return max(distanceToBand, max(start - uv.x, uv.x - end) * 3.6);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float antialias = max(fwidth(uv.y) * 1.1, 0.0012);
                float outline = _OutlineWidth;
                float ribbon = RibbonDistance(uv, 0.80, 0.080, 0.4, 0.015, 0.985);
                ribbon = min(ribbon, RibbonDistance(uv, 0.49, 0.095, 2.6, 0.10, 0.96));
                ribbon = min(ribbon, RibbonDistance(uv, 0.20, 0.045, 4.0, 0.25, 0.91));

                float2 p = float2(uv.x * 3.6, uv.y);
                float2 firstCloud = (p - float2(0.91, 0.765)) / _CloudScale;
                float2 secondCloud = (p - float2(1.93, 0.50)) / (_CloudScale * 0.88);
                secondCloud.x = -secondCloud.x;
                float2 thirdCloud = (p - float2(2.68, 0.25)) / (_CloudScale * 0.55);
                float cloud = min(CloudDistance(firstCloud) * _CloudScale,
                    CloudDistance(secondCloud) * _CloudScale * 0.88);
                cloud = min(cloud, CloudDistance(thirdCloud) * _CloudScale * 0.55);

                float shape = min(ribbon, cloud);
                float coverage = 1 - smoothstep(-antialias, antialias, shape);
                float border = (1 - smoothstep(outline - antialias, outline + antialias, -shape)) * coverage;
                float spiral = min(SpiralDistance(firstCloud) * _CloudScale,
                    SpiralDistance(secondCloud) * _CloudScale * 0.88);
                spiral = min(spiral, SpiralDistance(thirdCloud) * _CloudScale * 0.55);
                float innerLine = (1 - smoothstep(outline * 0.65, outline * 0.65 + antialias, spiral))
                    * (1 - smoothstep(-0.008, 0.003, cloud));

                // 가는 잔선은 세 개만 남겨 큰 띠와 구름 문양의 가독성을 유지한다.
                float streak = min(RibbonDistance(uv, 0.945, 0.005, 0.7, 0.28, 0.76),
                    RibbonDistance(uv, 0.635, 0.004, 2.9, 0.05, 0.40));
                streak = min(streak, RibbonDistance(uv, 0.075, 0.004, 4.2, 0.54, 0.94));
                float thinLines = (1 - smoothstep(-antialias, antialias, streak)) * 0.62;

                float front = 1 - smoothstep(_Head - 0.016, _Head + 0.008, uv.x);
                float tail = smoothstep(_Head - _Tail - 0.035, _Head - _Tail + 0.025, uv.x);
                float endTaper = smoothstep(0, 0.018, uv.x) * (1 - smoothstep(0.985, 1, uv.x));
                float rim = saturate(border * 0.95 + innerLine);
                float shade = smoothstep(0.10, 0.92, uv.y);
                half3 body = lerp(_ShadowColor.rgb, _Color.rgb, 0.48 + shade * 0.52);
                half3 color = lerp(body, _EdgeColor.rgb, rim);
                color = lerp(color, _EdgeColor.rgb, thinLines);
                // 소멸할 때 면이 먼저 빠지고 소용돌이 윤곽이 잠시 더 남는다.
                float bodyFade = smoothstep(0.15, 0.8, _Opacity);
                float alpha = max(coverage * lerp(0.56 * bodyFade, 0.96, rim), thinLines);
                alpha *= front * tail * endTaper * _Color.a * _Opacity;
                clip(alpha - 0.002);
                return half4(color * _Intensity, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
