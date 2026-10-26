Shader "Cheon/Test/ConceptSurface"
{
    Properties
    {
        _BaseColor("기본 색", Color) = (1,1,1,1)
        _AccentColor("표면 보조 색", Color) = (.3,.4,.25,1)
        _SurfaceType("표면 종류", Float) = 0
        _DetailScale("표면 무늬 크기", Float) = 1
        _VertexTint("메시 색 사용", Range(0,1)) = 0
        _Glow("발광", Range(0,4)) = 0
        _Decor("단청 장식", Range(0,1)) = 0
        [HideInInspector] _BaseMap("Base Map", 2D) = "white" {}
        [HideInInspector] _Cutoff("Cutoff", Float) = .5
        [HideInInspector] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _AccentColor;
            float _SurfaceType, _DetailScale, _VertexTint, _Glow, _Decor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 positionOS : TEXCOORD2; float fog : TEXCOORD3; half4 color : COLOR; };
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            float Fbm(float2 p) { return Noise(p)*.57+Noise(p*2.13+7)*.28+Noise(p*4.31)*.15; }
            float2 SurfaceUV(float3 p,float3 n)
            {
                float3 a=abs(n);
                return a.y>max(a.x,a.z) ? p.xz : a.x>a.z ? p.zy : p.xy;
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS=p.positionCS;output.positionWS=p.positionWS;output.positionOS=input.positionOS.xyz;
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.color=input.color;
                output.fog=ComputeFogFactor(p.positionCS.z);return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal=normalize(input.normalWS), p=input.positionWS;
                float2 uv=SurfaceUV(p,normal)*_DetailScale;
                half3 base=_BaseColor.rgb*lerp(half3(1,1,1),input.color.rgb,_VertexTint);
                float grain=Fbm(uv*2.7), detail=Noise(uv*18);
                half3 emission=base*_Glow;
                if(_SurfaceType>.5 && _SurfaceType<1.5)
                {
                    // 이끼의 밝은 덩어리와 작은 풀 무늬를 섞어 넓은 바닥의 단색 느낌을 줄인다.
                    float patches=Fbm(p.xz*.38);
                    base*=.78+patches*.43+detail*.08;
                    base=lerp(base,_AccentColor.rgb,smoothstep(.56,.82,patches)*.45);
                    float speck=pow(Noise(p.xz*31),18)*.18;base+=half3(.8,.78,.32)*speck;
                }
                else if(_SurfaceType>1.5 && _SurfaceType<2.5)
                {
                    float strata=sin(p.y*7.5+Noise(uv*.8)*5)*.03;
                    base*=.84+grain*.25+strata;
                    float moss=smoothstep(.51,.76,grain)*smoothstep(.1,.8,normal.y);
                    base=lerp(base,_AccentColor.rgb,moss*.62);
                    float fissure=1-smoothstep(.025,.06,abs(Noise(uv*3)-.51));base*=1-fissure*.08;
                }
                else if(_SurfaceType>2.5 && _SurfaceType<3.5)
                {
                    float rings=sin(uv.x*33+sin(uv.y*2)*3+Noise(uv*2)*5);
                    base*=.92+rings*.045+grain*.12;
                    float band=smoothstep(.38,.44,abs(input.positionOS.y))*_Decor;
                    float pattern=step(.5,frac((input.positionOS.x+input.positionOS.z)*11));
                    base=lerp(base,lerp(_AccentColor.rgb,half3(.94,.64,.17),pattern),band*.9);
                }
                else if(_SurfaceType>3.5 && _SurfaceType<4.5)
                {
                    float rib=pow(saturate(cos(uv.x*24)),6);
                    float courses=smoothstep(.90,.98,frac(uv.y*4));
                    base*=.85+rib*.35-courses*.14+grain*.1;
                }
                else if(_SurfaceType>4.5 && _SurfaceType<5.5)
                {
                    // 조금씩 어긋난 석판을 월드 좌표로 그려 기존 바닥의 형태를 유지한다.
                    float2 q=uv*float2(.75,1.02);q.x+=floor(q.y)*.47;
                    float2 cell=floor(q),f=frac(q);
                    float edge=min(min(f.x,1-f.x),min(f.y,1-f.y));
                    float seam=1-smoothstep(.018,.045,edge+(Noise(uv*5)-.5)*.018);
                    base*=.86+Hash(cell)*.19+detail*.055;
                    base=lerp(base,_AccentColor.rgb,seam*.72);
                    base+=smoothstep(.045,.07,edge)*(1-smoothstep(.07,.10,edge))*.08;
                }
                else if(_SurfaceType>5.5 && _SurfaceType<6.5)
                {
                    base*=.88+grain*.19;
                    float2 rune=float2(input.positionOS.x,input.positionOS.y-.61);
                    float circle=1-smoothstep(.018,.034,abs(length(rune)-.20));
                    float stem=(1-smoothstep(.013,.025,abs(rune.x)))*step(abs(rune.y),.34);
                    float arms=(1-smoothstep(.014,.025,abs(rune.y+rune.x*.43)))*step(abs(rune.x),.32);
                    float glyph=max(circle,max(stem,arms))*step(input.positionOS.z,-.2);
                    base=lerp(base,_AccentColor.rgb,glyph*.5);emission+=_AccentColor.rgb*glyph*1.8;
                }
                else if(_SurfaceType>6.5 && _SurfaceType<7.5)
                {
                    float2 flow=uv+float2(_Time.y*.10,-_Time.y*.35);
                    float waves=sin(flow.x*3.1+sin(flow.y*3)*.6)*sin(flow.y*4.2);
                    float caustic=pow(saturate(1-abs(waves)),14);
                    base=lerp(base,_AccentColor.rgb,caustic*.35);
                    if(abs(normal.y)<.65)
                    {
                        float streak=pow(saturate(sin(uv.x*8+sin(uv.y*2-_Time.y*4)*.35)),8);
                        base=lerp(base,_AccentColor.rgb,streak*.55);
                    }
                    emission+=base*.18;
                }
                else if(_SurfaceType>7.5 && _SurfaceType<8.5)
                {
                    base*=.88+grain*.18;
                }
                Light light=GetMainLight(TransformWorldToShadowCoord(p));
                float diffuse=saturate(dot(normal,light.direction));
                float shading=.35+smoothstep(-.12,.9,diffuse)*.65;
                float shadow=lerp(.57,1,light.shadowAttenuation);
                half3 ambient=SampleSH(normal)*.45+half3(.15,.19,.22);
                half3 color=base*(ambient+light.color*shading*shadow*.68);
                float rim=pow(1-saturate(dot(normal,GetWorldSpaceNormalizeViewDir(p))),4);
                color+=base*rim*.06+emission;
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
