Shader "Cheon/Effects/FanAttack/Gust"
{
    Properties
    {
        _BaseMap ("Gust noise", 2D) = "white" {}
        _Color ("Gust color", Color) = (.55,.9,.8,.5)
        _Head ("Travel", Range(0,1)) = 1
        _Tail ("Wave length", Range(0,1)) = .6
        _Opacity ("Opacity", Range(0,1)) = 1
        _Intensity ("Intensity", Range(0,5)) = 1
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Head, _Tail, _Opacity, _Intensity;
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
                float front = 1 - smoothstep(_Head - .08, _Head + .02, uv.x);
                float back = smoothstep(_Head - _Tail, _Head - _Tail + .25, uv.x);
                float sides = pow(saturate(sin(uv.y * PI)), .65);
                float ends = smoothstep(0, .1, uv.x) * (1 - smoothstep(.94, 1, uv.x));
                float2 flowing = float2(uv.x * 2.2 - _Head * 2.4, uv.y * 1.1);
                half2 noise = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, flowing).rg;
                float curl = sin(uv.x * 13 - _Head * 11 + uv.y * 8) * .07;
                float lane = abs(frac((uv.y + curl) * 4.5) - .5);
                float wisps = exp(-lane * lane * 95) * smoothstep(.3, .7, noise.r);
                float tufts = smoothstep(.46, .8, noise.g) * .38;
                float alpha = (wisps * .6 + tufts) * front * back * sides * ends * _Opacity * _Color.a;
                half3 color = lerp(_Color.rgb, half3(.87, .97, .94), noise.r * .35);
                return half4(color * _Intensity, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
