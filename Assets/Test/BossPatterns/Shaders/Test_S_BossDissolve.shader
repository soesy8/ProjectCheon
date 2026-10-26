Shader "Cheon/Tests/BossNoiseDissolve"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _DeathColor("Dark Purple", Color) = (.13,.025,.21,1)
        _EdgeColor("Powder Edge", Color) = (.32,.08,.44,1)
        _TintProgress("Tint Progress", Range(0,1)) = 0
        _DissolveProgress("Dissolve Progress", Range(0,1)) = 0
        _NoiseScale("Noise Scale", Float) = 3
        _EdgeWidth("Edge Width", Range(.01,.5)) = .12
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "NoiseDissolve"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _DeathColor, _EdgeColor;
                float _TintProgress, _DissolveProgress, _NoiseScale, _EdgeWidth;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; half fog : TEXCOORD3; };
            float Hash(float3 coordinates)
            {
                coordinates = frac(coordinates * .1031);
                coordinates += dot(coordinates, coordinates.yzx + 33.33);
                return frac((coordinates.x + coordinates.y) * coordinates.z);
            }
            float Noise(float3 coordinates)
            {
                float3 cell = floor(coordinates), f = frac(coordinates);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash(cell), Hash(cell + float3(1,0,0)), f.x),
                                 lerp(Hash(cell + float3(0,1,0)), Hash(cell + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash(cell + float3(0,0,1)), Hash(cell + float3(1,0,1)), f.x),
                                 lerp(Hash(cell + float3(0,1,1)), Hash(cell + float3(1,1,1)), f.x), f.y), f.z);
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 samplePoint = input.positionWS * _NoiseScale;
                float noise = Noise(samplePoint) * .7 + Noise(samplePoint * 2.07 + 9.3) * .3;
                // 색이 노이즈를 따라 먼저 번지고, 그 뒤 같은 무늬가 부스러지며 투명해진다.
                float tint = smoothstep(noise - .12, noise + .12, lerp(-.2, 1.2, _TintProgress));
                float remaining = noise - lerp(-.15, 1.15, _DissolveProgress);
                clip(remaining);
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                Light light = GetMainLight();
                half3 normal = normalize(input.normalWS);
                half3 illumination = SampleSH(normal) + light.color * saturate(dot(normal, light.direction));
                illumination = max(illumination, half3(.22,.22,.22));
                half3 color = lerp(base.rgb * illumination, _DeathColor.rgb * (.65 + .35 * noise), tint);
                float edge = (1 - smoothstep(0, _EdgeWidth, remaining)) * step(.001, _DissolveProgress);
                color = lerp(color, _EdgeColor.rgb, edge * .65);
                half alpha = base.a * (1 - _DissolveProgress) * smoothstep(0, _EdgeWidth, remaining);
                return half4(MixFog(color, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
