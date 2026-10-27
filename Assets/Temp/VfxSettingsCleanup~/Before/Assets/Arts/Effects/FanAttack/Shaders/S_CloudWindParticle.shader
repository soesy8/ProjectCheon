Shader "Cheon/Effects/FanAttack/CloudWindParticle"
{
    Properties
    {
        _EdgeColor ("Mint outline", Color) = (0.68,1,0.92,1)
        _Intensity ("Intensity", Range(0,4)) = 1.15
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+20" "RenderPipeline"="UniversalPipeline" }
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
            float4 _EdgeColor;
            float _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * float2(0.88, 0.65);
                float2 cloudPoint = p - float2(0.13, 0);
                float cloud = CloudWindBodyDistance(cloudPoint);
                float tail = CloudWindTailDistance(p, -0.05, 0.035, 0.4, -0.40, 0.06);
                float shape = min(cloud, tail);
                float antialias = max(length(fwidth(p)), 0.0015);
                float coverage = 1 - smoothstep(-antialias, antialias, shape);
                float outline = (1 - smoothstep(0.008, 0.014 + antialias, -shape)) * coverage;
                float innerLine = (1 - smoothstep(0.006, 0.009 + antialias, CloudWindSpiralDistance(cloudPoint)))
                    * (1 - smoothstep(-0.008, 0.003, cloud));
                float rim = saturate(outline + innerLine);
                half3 color = lerp(input.color.rgb, _EdgeColor.rgb, rim);
                float alpha = coverage * lerp(0.38, 0.8, rim) * input.color.a;
                clip(alpha - 0.002);
                return half4(color * _Intensity, alpha);
            }
            ENDHLSL
        }
    }
}
