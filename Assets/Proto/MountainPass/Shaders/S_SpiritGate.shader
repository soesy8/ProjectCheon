Shader "Cheon/Prototype/SpiritGate"
{
    Properties
    {
        _BaseColor ("안개 색", Color) = (0.12, 0.65, 0.95, 0.3)
        _EdgeColor ("경계 빛", Color) = (0.4, 0.9, 1, 1)
        _Intensity ("빛 강도", Range(0, 3)) = 1.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                float _Intensity;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y;
                float noise = Noise(float2(uv.x * 8 + sin(uv.y * 9 + time) * 0.4, uv.y * 12 - time * 0.65));
                float fine = Noise(float2(uv.x * 20 + time * 0.12, uv.y * 18 - time * 0.9));
                float edge = pow(abs(uv.x * 2 - 1), 10);
                float wisps = pow(saturate(noise * 0.65 + fine * 0.35), 2.4);
                float flow = 0.5 + 0.5 * sin(uv.x * 29 + noise * 6 - time * 1.5 + uv.y * 5);
                float fade = smoothstep(0, 0.07, uv.y) * (1 - smoothstep(0.94, 1, uv.y));
                float alpha = saturate((0.08 + wisps * 0.48 + edge * 0.5) * fade);
                float3 color = lerp(_BaseColor.rgb, _EdgeColor.rgb, saturate(edge + flow * wisps)) * _Intensity;
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
