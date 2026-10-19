Shader "Cheon/Test/ReferenceSurface"
{
    Properties
    {
        _BaseColor("색", Color) = (1,1,1,1)
        _BaseMap("색 텍스처", 2D) = "white" {}
        _Mode("표면: 0 일반, 1 수면, 2 낙수", Float) = 0
        _Emission("발광", Range(0,2)) = 0
        _FlowSpeed("흐름 속도", Float) = 0.18
        _Cull("면 컬링", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor; float4 _BaseMap_ST;
        float _Mode, _Emission, _FlowSpeed, _Cull;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half4 color:COLOR; half fog:TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
        Varyings Vert(Attributes input)
        {
            Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs position=GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS=position.positionCS; output.positionWS=position.positionWS;
            output.normalWS=TransformObjectToWorldNormal(input.normalOS); output.uv=TRANSFORM_TEX(input.uv,_BaseMap);
            output.color=input.color; output.fog=ComputeFogFactor(position.positionCS.z); return output;
        }
        ENDHLSL
        Pass
        {
            Name "Forward" Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*_BaseColor.rgb*input.color.rgb;
                if (_Mode>0.5)
                {
                    float2 uv=input.uv; float time=_Time.y*_FlowSpeed;
                    float waves=sin(length(uv-.5)*65-time*2+sin(uv.x*12)*.4);
                    float ribbons=smoothstep(.93,.99,sin(uv.x*47+sin(uv.y*7-time*4)*.7));
                    float foam=_Mode>1.5?ribbons*.65:smoothstep(.97,.999,waves)*.2;
                    albedo=lerp(albedo,half3(.85,.98,.96),foam);
                }
                half3 normal=normalize(input.normalWS);
                Light light=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 ambient=SampleSH(normal);
                half diffuse=saturate(dot(normal,light.direction));
                half3 illumination=ambient+light.color*diffuse*lerp(.35,1,light.shadowAttenuation);
                half3 color=albedo*illumination+albedo*_Emission;
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            float3 _LightDirection;
            float4 ShadowVert(Attributes input):SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS=TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS=TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS=TransformWorldToHClip(ApplyShadowBias(positionWS,normalWS,_LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z=min(positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z=max(positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }
            half4 ShadowFrag():SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            half4 DepthFrag(Varyings input):SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
    }
}
