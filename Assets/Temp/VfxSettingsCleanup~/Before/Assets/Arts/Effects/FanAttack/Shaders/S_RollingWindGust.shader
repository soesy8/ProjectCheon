Shader "Cheon/Effects/FanAttack/RollingWindGust"
{
    Properties
    {
        _Color ("Wind tint", Color) = (0.12,0.78,0.73,0.9)
        _EdgeColor ("Mint edge", Color) = (0.76,1,0.95,1)
        _ShadowColor ("Ribbon shadow", Color) = (0.025,0.33,0.40,1)
        _Head ("Travel progress", Range(0,1)) = 1
        _Tail ("Wave length", Range(0,1)) = 0.6
        _Opacity ("Opacity", Range(0,1)) = 1
        _Intensity ("Intensity", Range(0,5)) = 1.2
        _BranchCount ("Wind branches", Range(3,5)) = 3
        _RibbonWidth ("Ribbon thickness", Range(0.6,1.5)) = 1
        _CurlSize ("Rolled tip size", Range(0.7,1.4)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+14" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "S_CloudWindShapes.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color, _EdgeColor, _ShadowColor;
            float _Head, _Tail, _Opacity, _Intensity, _BranchCount, _RibbonWidth, _CurlSize;
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

            float RollingRibbonDistance(float2 p, float2 center, float size, float phase)
            {
                float radius = 0.175 * size * _CurlSize;
                float2 curlStart = center + float2(cos(-2.55), sin(-2.55) * 0.78) * radius;
                float tailStart = curlStart.x - 1.48;
                float progress = saturate((p.x - tailStart) / (curlStart.x - tailStart));
                float tailCenter = lerp(center.y, curlStart.y, smoothstep(0, 1, progress));
                // 진행률을 따라 꼬리가 함께 휘어 정지된 문양이 미끄러지는 느낌을 줄인다.
                tailCenter += sin(progress * PI) * sin(progress * 3.6 + _Head * 3.6 + phase * 0.12) * 0.105;
                float width = 0.048 * pow(progress, 0.75) * _RibbonWidth * size;
                float shape = max(abs(p.y - tailCenter) - width, max(tailStart - p.x, p.x - curlStart.x));

                // 폭이 줄어드는 한 줄의 띠가 끝에서 돌아 안쪽으로 감긴다.
                // 구름 덩어리 없이 내부의 빈 공간이 그대로 보이는 모양이다.
                [branch]
                if (length(p - center) < radius + 0.09)
                {
                    float2 previous = curlStart;
                    [unroll]
                    for (int index = 1; index <= 28; index++)
                    {
                        float curlProgress = index / 28.0;
                        float angle = -2.55 - curlProgress * 7.35;
                        float curlRadius = radius * lerp(1, 0.06, curlProgress);
                        float2 next = center + float2(cos(angle), sin(angle) * 0.78) * curlRadius;
                        float stroke = lerp(0.048, 0.004, curlProgress) * size * _RibbonWidth;
                        shape = min(shape, CloudWindSegmentDistance(p, previous, next) - stroke);
                        previous = next;
                    }
                }
                return shape;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 p = float2((uv.x - _Head) * 2.6, (uv.y - 0.5) * 1.4);
                float antialias = max(length(fwidth(p)) * 0.8, 0.0015);
                float count = clamp(floor(_BranchCount + 0.5), 3, 5);
                float spacing = lerp(0.40, 0.215, (count - 3) * 0.5);
                float size = 1.10 - (count - 3) * 0.14;
                float shape = 10;
                [unroll]
                for (int branch = 0; branch < 5; branch++)
                {
                    if (branch >= count) continue;
                    float side = branch - (count - 1) * 0.5;
                    float2 center = float2(-0.24 - abs(side) * 0.15, side * spacing);
                    shape = min(shape, RollingRibbonDistance(p, center, size, branch * 1.4));
                }
                float coverage = 1 - smoothstep(-antialias, antialias, shape);
                float rim = (1 - smoothstep(0.007, 0.014 + antialias, -shape)) * coverage;
                float front = 1 - smoothstep(_Head - 0.004, _Head + 0.002, uv.x);
                float back = smoothstep(_Head - _Tail, _Head - _Tail + 0.13, uv.x);
                float ends = smoothstep(0, 0.035, uv.x) * (1 - smoothstep(0.993, 1, uv.x));
                float sides = smoothstep(0, 0.06, uv.y) * (1 - smoothstep(0.94, 1, uv.y));
                float bodyFade = smoothstep(0.1, 0.75, _Opacity);
                half3 body = lerp(_ShadowColor.rgb, _Color.rgb, 0.62);
                half3 color = lerp(body, _EdgeColor.rgb, rim * 0.88);
                float alpha = coverage * lerp(0.54 * bodyFade, 0.91, rim) * _Color.a * _Opacity;
                alpha *= front * back * ends * sides;
                clip(alpha - 0.002);
                return half4(color * _Intensity, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
