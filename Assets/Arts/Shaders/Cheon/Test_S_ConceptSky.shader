Shader "Cheon/Test/ConceptSky"
{
    Properties
    {
        _ZenithColor("하늘",Color)=(.25,.57,.85,1)
        _HorizonColor("수평선",Color)=(.72,.86,.92,1)
        _CloudColor("구름",Color)=(1,.94,.80,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            half4 _ZenithColor,_HorizonColor,_CloudColor;
            struct Varyings { float4 positionCS:SV_POSITION;float3 ray:TEXCOORD0; };
            Varyings Vert(float4 vertex:POSITION)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(vertex.xyz);
                o.positionCS.z=UNITY_RAW_FAR_CLIP_VALUE*o.positionCS.w;
                o.ray=vertex.xyz;return o;
            }
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);}
            half4 Frag(Varyings i):SV_Target
            {
                float3 d=normalize(i.ray);float h=saturate(d.y*.8+.24);
                half3 color=lerp(_HorizonColor.rgb,_ZenithColor.rgb,pow(h,.65));
                float2 p=d.xz/(abs(d.y)+.35)*3.2;
                float n=Noise(p)*.58+Noise(p*2.02)*.28+Noise(p*4.1)*.14;
                float cloud=smoothstep(.47,.72,n)*smoothstep(-.6,.05,d.y);
                color=lerp(color,_CloudColor.rgb,cloud*.88);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
