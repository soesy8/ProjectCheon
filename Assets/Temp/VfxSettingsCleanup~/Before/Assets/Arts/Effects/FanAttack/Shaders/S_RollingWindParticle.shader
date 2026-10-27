Shader "Cheon/Effects/FanAttack/RollingWindParticle"
{
    Properties
    {
        _EdgeColor ("Mint wisp", Color) = (0.70,1,0.94,1)
        _Intensity ("Intensity", Range(0,4)) = 1.1
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
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
                float2 uv = input.uv;
                float taper = pow(saturate(sin(uv.x * PI)), 0.8);
                float center = 0.42 + sin(uv.x * PI) * 0.16;
                float shape = abs(uv.y - center) - taper * 0.028;
                float antialias = max(fwidth(uv.y), 0.003);
                float coverage = 1 - smoothstep(-antialias, antialias, shape);
                float alpha = coverage * taper * input.color.a * 0.55;
                clip(alpha - 0.002);
                return half4(lerp(input.color.rgb, _EdgeColor.rgb, 0.72) * _Intensity, alpha);
            }
            ENDHLSL
        }
    }
}
