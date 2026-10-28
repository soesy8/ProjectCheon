Shader "Cheon/VFX/StairMistCloud"
{
    Properties
    {
        [PerRendererData] _MainTex ("Cloud Texture", 2D) = "white" {}
        _ClearRadius ("Clear Center Radius", Vector) = (0.24, 0.28, 0, 0)
        _ClearFeather ("Clear Center Feather", Range(0.05, 1)) = 0.65
        _BlurRadius ("Cloud Blur Texels", Range(0, 8)) = 3
        _NoiseStrength ("Patchy Alpha Strength", Range(0, 1)) = 0.9
        _NoiseScale ("Patchy Alpha Scale", Float) = 5.2
        [HideInInspector] _NoiseTime ("Patchy Alpha Time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
                fixed4 color : COLOR;
            };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _ClearRadius;
            float _ClearFeather;
            float _BlurRadius;
            float _NoiseStrength;
            float _NoiseScale;
            float _NoiseTime;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.screenPosition = ComputeScreenPos(output.position);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 SampleCloud(float2 uv)
            {
                half4 cloud = tex2D(_MainTex, uv);
                // 투명 픽셀의 RGB 때문에 흐린 가장자리가 어두워지지 않도록 먼저 알파를 곱한다.
                cloud.rgb *= cloud.a;
                return cloud;
            }

            float NoiseHash(float2 cell)
            {
                cell = frac(cell * float2(123.34, 456.21));
                cell += dot(cell, cell + 45.32);
                return frac(cell.x * cell.y);
            }

            float SmoothNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 blend = frac(position);
                blend = blend * blend * (3.0 - 2.0 * blend);
                return lerp(lerp(NoiseHash(cell), NoiseHash(cell + float2(1, 0)), blend.x),
                    lerp(NoiseHash(cell + float2(0, 1)), NoiseHash(cell + float2(1, 1)), blend.x), blend.y);
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPosition.xy / input.screenPosition.w;
                float centerDistance = length((screenUV - 0.5) / max(_ClearRadius.xy, 0.01));
                float edgeMask = smoothstep(1.0, 1.0 + _ClearFeather, centerDistance);
                float2 blur = _MainTex_TexelSize.xy * _BlurRadius;
                half4 cloud = SampleCloud(input.uv) * 0.4;
                cloud += SampleCloud(input.uv + float2(blur.x, 0)) * 0.15;
                cloud += SampleCloud(input.uv - float2(blur.x, 0)) * 0.15;
                cloud += SampleCloud(input.uv + float2(0, blur.y)) * 0.15;
                cloud += SampleCloud(input.uv - float2(0, blur.y)) * 0.15;
                cloud.rgb /= max(cloud.a, 0.0001);
                // 크기가 다른 노이즈를 겹쳐 구름 안쪽도 군데군데 얇아지게 한다.
                // 구름별 UV와 화면 위치를 함께 사용해 겹친 이미지들의 투명 무늬가 겹치지 않게 한다.
                float2 noiseUV = input.uv * float2(2.0, 1.0) * _NoiseScale
                    + screenUV * float2(1.9, 1.3) + float2(_NoiseTime, -_NoiseTime * 0.6);
                float noise = SmoothNoise(noiseUV) * 0.57;
                noise += SmoothNoise(noiseUV * 2.17 + float2(7.3, 13.1)) * 0.29;
                noise += SmoothNoise(noiseUV * 4.13 - float2(4.9, 2.5)) * 0.14;
                float patchyAlpha = lerp(1.0, smoothstep(0.28, 0.7, noise), _NoiseStrength);
                return half4(cloud.rgb * input.color.rgb, cloud.a * input.color.a * edgeMask * patchyAlpha);
            }
            ENDCG
        }
    }
}
