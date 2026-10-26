Shader "Cheon/Tests/AttackRange"
{
    Properties
    {
        _Opacity ("Opacity", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
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
                half _Opacity;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.color.a *= _Opacity;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
}
