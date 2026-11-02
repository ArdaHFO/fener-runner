Shader "FenerRunner/EveningSky"
{
    Properties
    {
        _ZenithColor ("Evening blue", Color) = (0.1, 0.18, 0.32, 1)
        _HorizonColor ("Warm horizon", Color) = (0.86, 0.49, 0.32, 1)
        _GroundColor ("Below horizon", Color) = (0.16, 0.21, 0.23, 1)
        _CloudCover ("Cloud cover", Range(0,1)) = 0.15
        _Night ("Night sky", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half _CloudCover;
                half _Night;
            CBUFFER_END
            float _StadiumTime;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float elevation = normalize(input.direction).y;
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, smoothstep(0.0, 0.32, elevation));
                float3 dir=normalize(input.direction);
                float2 uv=dir.xz/(max(.13,dir.y)+.3);
                float drift=_StadiumTime*.008;
                float clouds=sin(uv.x*4+drift)*.27+sin(uv.y*5-drift*.7)*.26+sin(uv.x*9+uv.y*6+drift)*.14+sin(uv.x*17-uv.y*11)*.08+.42;
                float coverage=smoothstep(.62-_CloudCover*.32,.86-_CloudCover*.27,clouds)*smoothstep(0,.20,elevation)*_CloudCover;
                sky=lerp(sky,lerp(half3(.83,.87,.91),half3(.32,.39,.49),_CloudCover),coverage*.62);
                float2 cell=floor(uv*145);
                float star=step(.9965,frac(sin(dot(cell,float2(12.9898,78.233)))*43758.5453));
                sky+=star*_Night*(1-coverage)*smoothstep(.08,.4,elevation)*.5;
                half3 ground = lerp(_GroundColor.rgb, _HorizonColor.rgb, smoothstep(-0.18, 0.0, elevation));
                return half4(elevation >= 0.0 ? sky : ground, 1.0);
            }
            ENDHLSL
        }
    }
}
