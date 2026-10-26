Shader "Cheon/Test/ReferenceEffect"
{
    Properties
    {
        _BaseMap("문양", 2D)="white" {}
        [HDR] _BaseColor("색", Color)=(1,1,1,1)
        _Mode("효과: 0 문양, 1 영혼불, 2 안개", Float)=0
        _Opacity("투명도", Range(0,1))=1
        _Speed("움직임", Float)=0.3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST,_BaseColor; float _Mode,_Opacity,_Speed;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz); output.uv=input.uv;
                output.fog=ComputeFogFactor(output.positionCS.z); return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 p=input.uv; float time=_Time.y*_Speed;
                half4 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p)*_BaseColor;
                if (_Mode>1.5)
                {
                    float2 q=(p-.5)*2;
                    float edge=1-dot(q,q);
                    float billow=sin(q.x*12+time)*sin(q.y*9-time*.5)*.12+sin(q.x*27+q.y*17)*.04;
                    color.a*=smoothstep(0,.45,edge+billow)*.8;
                }
                else if (_Mode>.5)
                {
                    float x=(p.x-.5)*2 + sin(p.y*10-time*4)*p.y*.2;
                    float width=lerp(.52,.025,pow(p.y,.8));
                    float shape=smoothstep(width+.08,width-.08,abs(x));
                    shape*=smoothstep(0,.12,p.y)*smoothstep(1,.88,p.y);
                    color.rgb=lerp(_BaseColor.rgb,half3(.83,1,1),saturate((1-abs(x)/max(width,.01))*p.y));
                    color.a*=shape;
                }
                color.a*=_Opacity; color.rgb=MixFog(color.rgb,input.fog); return color;
            }
            ENDHLSL
        }
    }
}
