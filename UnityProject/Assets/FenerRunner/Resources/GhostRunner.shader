Shader "FenerRunner/GhostRunner"
{
    Properties
    {
        [MainTexture] _BaseMap("Original character surface",2D)="white"{}
        [MainColor] _BaseColor("Original character color",Color)=(1,1,1,1)
        _GhostTint("Blue glow",Color)=(.22,.52,1,1)
        _RimColor("Gold edge",Color)=(1,.79,.16,1)
        _Opacity("Ghost opacity",Range(0,1))=.48
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Name "GhostForward" Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On // Hide far-side facial/material islands behind the visible ghost surface.
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor,_GhostTint,_RimColor;
                half _Opacity;
            CBUFFER_END
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;half fog:TEXCOORD3;};
            Varyings Vertex(Attributes input)
            {
                Varyings output;VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS=p.positionCS;output.positionWS=p.positionWS;
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=TRANSFORM_TEX(input.uv,_BaseMap);output.fog=ComputeFogFactor(p.positionCS.z);return output;
            }
            half4 Fragment(Varyings input):SV_Target
            {
                half3 normal=normalize(input.normalWS),view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                half rim=pow(1-saturate(dot(normal,view)),3);
                half3 original=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*_BaseColor.rgb;
                Light light=GetMainLight();half shade=.58+.42*saturate(dot(normal,light.direction));
                half3 color=original*shade+_GhostTint.rgb*.10+_RimColor.rgb*rim*.42;
                color=MixFog(color,input.fog);
                return half4(color,_Opacity*(.72+.28*rim));
            }
            ENDHLSL
        }
    }
}
