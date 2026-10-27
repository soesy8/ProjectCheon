Shader "Cheon/Effects/FanAttack/CloudWindGust"
{
    Properties
    {
        _Color ("Wind tint", Color) = (0.12,0.78,0.73,0.9)
        _EdgeColor ("Mint outline", Color) = (0.68,1,0.92,1)
        _ShadowColor ("Deep teal", Color) = (0.025,0.36,0.42,1)
        _Head ("Travel progress", Range(0,1)) = 1
        _Tail ("Wave length", Range(0,1)) = 0.6
        _Opacity ("Opacity", Range(0,1)) = 1
        _Intensity ("Intensity", Range(0,5)) = 1.2
        _CloudScale ("Cloud head size", Range(0.6,1.3)) = 1
        _OutlineWidth ("Outline width", Range(0.004,0.025)) = 0.012
        _BranchCount ("Wind branches", Range(3,5)) = 3
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
            float _Head, _Tail, _Opacity, _Intensity, _CloudScale, _OutlineWidth, _BranchCount;
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
                // 구름 머리와 꼬리를 선단에 묶어 두어 실제 타격 바람과 함께 전진시킨다.
                float2 p = float2((uv.x - _Head) * 2.6, (uv.y - 0.5) * 1.4);
                float antialias = max(length(fwidth(p)) * 0.8, 0.0015);
                float outline = _OutlineWidth;
                float2 first = (p - float2(-0.21, 0)) / _CloudScale;
                float2 upper = (p - float2(-0.43, 0.34)) / (_CloudScale * 0.85);
                float2 lower = (p - float2(-0.52, -0.32)) / (_CloudScale * 0.72);
                lower.y = -lower.y;
                float cloud = min(CloudWindBodyDistance(first) * _CloudScale,
                    CloudWindBodyDistance(upper) * _CloudScale * 0.85);
                cloud = min(cloud, CloudWindBodyDistance(lower) * _CloudScale * 0.72);
                float ribbons = CloudWindTailDistance(p, 0, 0.09, 0.6, -1.66, -0.18);
                ribbons = min(ribbons, CloudWindTailDistance(p, 0.34, 0.065, 2.0, -1.27, -0.38));
                ribbons = min(ribbons, CloudWindTailDistance(p, -0.32, 0.06, 4.2, -1.49, -0.48));
                float spiral = min(CloudWindSpiralDistance(first) * _CloudScale,
                    CloudWindSpiralDistance(upper) * _CloudScale * 0.85);
                spiral = min(spiral, CloudWindSpiralDistance(lower) * _CloudScale * 0.72);
                if (_BranchCount > 3.5)
                {
                    cloud = ribbons = spiral = 10;
                    float branchCount = clamp(floor(_BranchCount + 0.5), 3, 5);
                    [unroll]
                    for (int branch = 0; branch < 5; branch++)
                    {
                        if (branch >= branchCount) continue;
                        float side = branch - (branchCount - 1) * 0.5;
                        float lane = side * (0.9 / (branchCount - 1));
                        float headX = -0.21 - abs(side) * 0.18;
                        float scale = _CloudScale * (0.76 - abs(side) * 0.06);
                        float2 cloudPoint = (p - float2(headX, lane)) / scale;
                        if (side < 0) cloudPoint.y = -cloudPoint.y;
                        cloud = min(cloud, CloudWindBodyDistance(cloudPoint) * scale);
                        spiral = min(spiral, CloudWindSpiralDistance(cloudPoint) * scale);
                        ribbons = min(ribbons, CloudWindTailDistance(p, lane,
                            0.066 - abs(side) * 0.009, branch + 0.6,
                            -1.70 + abs(side) * 0.08, headX + 0.03));
                    }
                }
                float shape = min(cloud, ribbons);
                float coverage = 1 - smoothstep(-antialias, antialias, shape);
                float border = (1 - smoothstep(outline - antialias, outline + antialias, -shape)) * coverage;
                float innerLine = (1 - smoothstep(outline * 0.65, outline * 0.65 + antialias, spiral))
                    * (1 - smoothstep(-0.008, 0.003, cloud));
                float rim = saturate(border + innerLine);
                float streak = min(CloudWindTailDistance(p, 0.57, 0.006, 0.2, -1.1, -0.40),
                    CloudWindTailDistance(p, -0.55, 0.006, 3.0, -1.38, -0.62));
                float thinLines = (1 - smoothstep(-antialias, antialias, streak)) * 0.62;
                float front = 1 - smoothstep(_Head - 0.005, _Head + 0.002, uv.x);
                float back = smoothstep(_Head - _Tail, _Head - _Tail + 0.10, uv.x);
                float ends = smoothstep(0, 0.035, uv.x) * (1 - smoothstep(0.993, 1, uv.x));
                float sides = smoothstep(0, 0.07, uv.y) * (1 - smoothstep(0.93, 1, uv.y));
                float bodyFade = smoothstep(0.14, 0.80, _Opacity);
                half3 body = lerp(_ShadowColor.rgb, _Color.rgb, 0.72);
                half3 color = lerp(body, _EdgeColor.rgb, saturate(rim + thinLines));
                float alpha = max(coverage * lerp(0.57 * bodyFade, 0.96, rim), thinLines);
                alpha *= front * back * ends * sides * _Opacity * _Color.a;
                clip(alpha - 0.002);
                return half4(color * _Intensity, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
