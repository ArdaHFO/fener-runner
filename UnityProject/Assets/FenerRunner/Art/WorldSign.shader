Shader "FenerRunner/WorldSign"
{
    Properties { _MainTex("Font atlas",2D)="white"{} }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            struct Attributes {float4 position:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct Varyings {float4 position:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;half fog:TEXCOORD1;};
            Varyings Vert(Attributes i){Varyings o;o.position=TransformObjectToHClip(i.position.xyz);o.uv=i.uv;o.color=i.color;o.fog=ComputeFogFactor(o.position.z);return o;}
            half4 Frag(Varyings i):SV_Target{return half4(MixFog(i.color.rgb,i.fog),SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a*i.color.a);}
            ENDHLSL
        }
    }
}
