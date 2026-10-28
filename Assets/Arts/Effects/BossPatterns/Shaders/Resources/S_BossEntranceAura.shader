Shader "Cheon/Effects/BossEntranceAura"
{
    Properties
    {
        _Ember("Ember Shape", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+15" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Ember;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            float Hash(float2 coordinate)
            {
                return frac(sin(dot(coordinate, float2(127.1, 311.7))) * 43758.5453);
            }
            float Noise(float2 coordinate)
            {
                float2 cell = floor(coordinate);
                float2 blend = frac(coordinate);
                blend = blend * blend * (3 - 2 * blend);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), blend.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), blend.x), blend.y);
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv; output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                // 위로 흐르는 잡음으로 가장자리와 끝을 흔들어, 연기보다 가늘고 찢어진 불꽃을 만든다.
                float2 flow = float2(uv.x * 5, uv.y * 4 - _Time.y * 2.8);
                float coarse = Noise(flow);
                float fine = Noise(flow * 2.1 + 7.3);
                float bend = (Noise(float2(uv.y * 3 - _Time.y * 1.7, 5.1)) - .5) * uv.y * .5;
                float width = lerp(.43, .035, pow(abs(uv.y), .85)) * lerp(.65, 1.2, coarse);
                float edge = abs(uv.x - .5 + bend);
                float flame = 1 - smoothstep(width * .35, width, edge);
                flame *= smoothstep(0, .16, uv.y) * (1 - smoothstep(.68, 1, uv.y));
                flame *= lerp(.4, 1, coarse * .65 + fine * .35);
                float ember = pow(saturate(1 - dot((uv - .5) * 2, (uv - .5) * 2)), 2);
                float alpha = lerp(flame, ember, _Ember) * input.color.a;
                half glow = lerp(.7, 1.35, saturate((coarse - .45) * 2)) * (1 - uv.y * .35);
                return half4(input.color.rgb * lerp(glow, 1.25, _Ember), alpha);
            }
            ENDHLSL
        }
    }
}
