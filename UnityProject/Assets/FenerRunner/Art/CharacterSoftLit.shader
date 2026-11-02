Shader "FenerRunner/CharacterSoftLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Original clothing", 2D) = "white" {}
        [MainColor] _BaseColor("Original palette", Color) = (1,1,1,1)
        [Normal] _BumpMap("Surface detail", 2D) = "bump" {}
        _BumpScale("Normal strength", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = .25
        _Metallic("Metallic", Range(0,1)) = 0
        _SoftWrap("Soft diffuse", Range(0,.4)) = .18
        _Sheen("Fibre sheen", Range(0,.3)) = .08
        _Grain("Micro texture", Range(0,.1)) = .025
        _OcclusionStrength("Contact shading", Range(0,1)) = .6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale, _Smoothness, _Metallic, _SoftWrap, _Sheen, _Grain, _OcclusionStrength;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        struct Attributes
        {
            float4 positionOS:POSITION;
            float3 normalOS:NORMAL;
            float4 tangentOS:TANGENT;
            float2 uv:TEXCOORD0;
            half4 color:COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            half3 normalWS:TEXCOORD1;
            half4 tangentWS:TEXCOORD2;
            float2 uv:TEXCOORD3;
            half2 aoFog:TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings ForwardVertex(Attributes input)
        {
            Varyings o=(Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs n=GetVertexNormalInputs(input.normalOS,input.tangentOS);
            o.positionCS=p.positionCS; o.positionWS=p.positionWS;
            o.normalWS=n.normalWS;
            o.tangentWS=half4(n.tangentWS,input.tangentOS.w*GetOddNegativeScale());
            o.uv=TRANSFORM_TEX(input.uv,_BaseMap);
            o.aoFog=half2(input.color.r,ComputeFogFactor(p.positionCS.z));
            return o;
        }
        half4 ForwardFragment(Varyings input):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half3 n=normalize(input.normalWS);
            #if defined(_NORMALMAP)
                half3 nt=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,input.uv),_BumpScale);
                half3 t=normalize(input.tangentWS.xyz);
                n=normalize(t*nt.x+cross(n,t)*input.tangentWS.w*nt.y+n*nt.z);
            #endif
            // Filter fine detail away when it becomes smaller than a pixel.
            float2 grainUV=input.uv*420;
            half visible=1-saturate(max(fwidth(grainUV.x),fwidth(grainUV.y)));
            half grain=(frac(sin(dot(floor(grainUV),float2(127.1,311.7)))*43758.5453)-.5)*_Grain*visible;
            half ao=lerp(1,input.aoFog.x,_OcclusionStrength);
            SurfaceData surface=(SurfaceData)0;
            surface.albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*_BaseColor.rgb*(1+grain);
            surface.alpha=1; surface.metallic=_Metallic; surface.smoothness=saturate(_Smoothness+grain);
            surface.normalTS=half3(0,0,1); surface.occlusion=ao;
            InputData data=(InputData)0;
            data.positionWS=input.positionWS; data.normalWS=n;
            data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
            data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
            data.bakedGI=SampleSH(n); data.shadowMask=half4(1,1,1,1);
            data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
            Light key=GetMainLight(data.shadowCoord);
            half ndl=dot(n,key.direction);
            half wrapped=saturate((ndl+_SoftWrap)/(1+_SoftWrap))-saturate(ndl);
            half grazing=pow(1-saturate(dot(n,data.viewDirectionWS)),4);
            // Restrained light wrap and fibre sheen, both tied to actual scene lighting.
            surface.emission=surface.albedo*ao*(key.color*max(0,wrapped)*key.shadowAttenuation*.5
                +(data.bakedGI+key.color*saturate(ndl)*.18)*grazing*_Sheen);
            half4 color=UniversalFragmentPBR(data,surface);
            color.rgb=MixFog(color.rgb*lerp(1,ao,.23),input.aoFog.y);
            return half4(color.rgb,1);
        }
        float3 _LightDirection, _LightPosition;
        struct DepthVaryings {float4 positionCS:SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID};
        DepthVaryings ShadowVertex(Attributes input)
        {
            DepthVaryings o; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,o);
            float3 p=TransformObjectToWorld(input.positionOS.xyz), n=TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirection=normalize(_LightPosition-p);
            #else
                float3 lightDirection=_LightDirection;
            #endif
            o.positionCS=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p,n,lightDirection)));return o;
        }
        DepthVaryings DepthVertex(Attributes input)
        {DepthVaryings o;UNITY_SETUP_INSTANCE_ID(input);UNITY_TRANSFER_INSTANCE_ID(input,o);o.positionCS=TransformObjectToHClip(input.positionOS.xyz);return o;}
        half4 DepthFragment(DepthVaryings input):SV_Target {return 0;}
        ENDHLSL
        Pass
        {
            Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ForwardVertex
            #pragma fragment ForwardFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
