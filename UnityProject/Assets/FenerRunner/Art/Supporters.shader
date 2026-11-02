Shader "FenerRunner/Supporters"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        float _StadiumTime,_CrowdEnergy,_CrowdThrowAge,_CrowdWorldOffset,_CrowdReactionSeed;
        float4 _CrowdThrowOrigin,_CrowdReactionOrigin,_CrowdReactionData,_CrowdPlayer;
        struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; float4 probe:TEXCOORD0; float4 motion:TEXCOORD1; float4 anchor:TEXCOORD2; float4 persona:TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct V { float4 positionCS:SV_POSITION; half3 color:TEXCOORD0; half3 normal:TEXCOORD1; float fog:TEXCOORD2; };
        float3 RotateX(float3 p,float a){float s,c;sincos(a,s,c);return float3(p.x,p.y*c-p.z*s,p.y*s+p.z*c);}
        float3 RotateY(float3 p,float a){float s,c;sincos(a,s,c);return float3(p.x*c+p.z*s,p.y,-p.x*s+p.z*c);}
        float3 RotateZ(float3 p,float a){float s,c;sincos(a,s,c);return float3(p.x*c-p.y*s,p.x*s+p.y*c,p.z);}
        float Hash(float key){return frac(sin(key*17.137+3.713)*43758.5453);}
        float Pulse(float age,float start,float end)
        {return smoothstep(start,start+.24,age)*(1-smoothstep(end-.36,end,age));}
        float3 RestHand(float arm,float width,float t,float key)
        {
            float h=Hash(key+2.13),breath=sin(t*(1.1+Hash(key+1.91)*.35)+key);
            float3 hand=float3(arm*(.245*width+.004*breath),.705+.004*breath,.055);
            if(h>.40&&h<.75)hand=float3(arm*.14*width,.99+.006*breath,.235);
            else if(h>=.75&&arm*(Hash(key+9.51)>.5?1:-1)>0)hand=float3(arm*.24*width,1.00,.11);
            return hand;
        }
        float3 ClapHand(float arm,float t,float key,float height)
        {
            float open=.5+.5*sin(t*(4.8+Hash(key+7.7)*1.6)+key*3);
            return float3(arm*(.035+.145*open),height+.013*sin(t*2.1+key),.34);
        }
        float3 Hand(float arm,float style,float t,float key,float width,float activity,float reaction,float choice,float kind,float reactionAge,float throwing)
        {
            float slow=sin(t*(.75+Hash(key+1.2)*.55)+key),beat=sin(t*(2.2+Hash(key+4.8))+key*2);
            float dominant=Hash(key+9.51)>.5?1:-1;
            float3 rest=RestHand(arm,width,t,key),hand=rest,active=rest;
            if(style<.5)active=ClapHand(arm,t,key,1.17);
            else if(style<1.5)active=arm*dominant>0?float3(arm*(.32+.035*slow)*width,1.66+.025*beat,.16):rest;
            else if(style<2.5)
            {
                // Most scarves stay at chest height. A small independent
                // subset lifts briefly, rather than a permanent overhead pose.
                rest=float3(arm*.39,1.08,.24);
                active=float3(arm*.40,Hash(key+5.73)>.84?1.79:1.38,.21);
            }
            else if(style<3.5)active=arm*dominant>0?float3(arm*(.25+.055*slow),1.15+.025*beat,.34):rest;
            else if(style<4.5)active=arm*dominant>0?float3(arm*.29,1.44+.06*beat,.20):rest;
            else active=ClapHand(arm,t,key+4.7,1.06);
            hand=lerp(rest,active,activity);
            if(reaction>0)
            {
                float3 response=rest;
                // Participation and response class are private to this person
                // and event. Even a power cannot command every pair of arms.
                float responseClass=choice;
                if(kind<.5) responseClass=choice<.55?.60:.12;
                else if(kind<1.5)responseClass=choice<.74?.12:choice<.96?.60:.83;
                else if(kind<2.5)responseClass=choice<.64?.83:.12;
                else if(kind<4.5)responseClass=choice<.76?.12:choice<.94?.60:.83;
                else if(kind<5.5)responseClass=choice;
                else if(kind<6.5)responseClass=choice<.48?.60:choice<.92?.12:.98;
                if(responseClass<.45)response=ClapHand(arm,t,key+2.1,1.23);
                else if(responseClass<.76)response=arm*dominant>0?float3(arm*(.31+.025*slow)*width,1.75+.018*beat,.18):rest;
                else if(responseClass<.93)response=arm*dominant>0?float3(arm*.18,1.18,.41):rest;
                else response=float3(arm*.36*width,1.79,.17);
                if(style>1.5&&style<2.5)
                    response=float3(arm*.40,responseClass>.93?1.79:1.31,.23);
                hand=lerp(hand,response,reaction);
            }
            if(arm>0&&throwing>0)
            {
                float release=smoothstep(.16,.35,_CrowdThrowAge),follow=smoothstep(.35,.68,_CrowdThrowAge);
                float3 windup=float3(.31*width,1.70,-.17),launch=float3(.24*width,1.83,.20),finish=float3(.20*width,1.27,.48);
                hand=lerp(hand,lerp(lerp(windup,launch,release),finish,follow),throwing);
            }
            return hand;
        }
        void Joint(float arm,float width,float3 target,out float3 shoulder,out float3 elbow,out float3 hand)
        {
            shoulder=float3(arm*.22*width,1.28,0);
            float3 toward=target-shoulder;float distance=clamp(length(toward),.08,.596);
            float3 direction=normalize(toward+float3(0,.00001,0));hand=shoulder+direction*distance;
            float along=distance*.5,bend=sqrt(max(0,.30*.30-along*along));
            float3 outside=float3(arm,0,0);outside-=direction*dot(direction,outside);
            outside=normalize(outside+float3(0,0,.0001));
            elbow=shoulder+direction*along+outside*bend;
        }
        void Bone(inout float3 p,inout float3 n,float3 rest,float3 from,float3 to)
        {
            float3 axis=normalize(to-from),right=cross(float3(0,0,1),axis);
            if(dot(right,right)<.001)right=float3(1,0,0);else right=normalize(right);
            float3 front=cross(axis,right),d=p-rest;
            p=from+right*d.x-axis*d.y+front*d.z;
            n=right*n.x-axis*n.y+front*n.z;
        }
        void Pose(A v,out float3 p,out float3 n,out float shade)
        {
            p=v.positionOS.xyz;n=v.normalOS;shade=1;
            float part=v.motion.y,phase=v.motion.x,t=_StadiumTime,energy=saturate(_CrowdEnergy);
            if(part>7.5)
            {
                if(part<8.5)
                {
                    float u=v.motion.w,wind=t*3.5+phase+u*6;
                    p.z+=u*(sin(wind)*.23+sin(wind*1.73)*.065)*(1+energy*.25);
                    p.y+=u*sin(wind*.78)*.065-u*u*.035;
                    n=normalize(float3(-v.motion.z*.55*cos(wind)*u,0,-1));
                }
                return;
            }
            float side=v.anchor.x<0?-1:1,height=max(.1,v.anchor.w),arm=v.motion.z,style=v.motion.w,width=max(.7,v.persona.x);
            float3 d=(p-v.anchor.xyz)/height;
            p=float3(d.z*side,d.y,-d.x*side);n=float3(n.z*side,n.y,-n.x*side);
            float3 worldAnchor=TransformObjectToWorld(v.anchor.xyz);
            float absoluteZ=worldAnchor.z+_CrowdWorldOffset;
            // Quantize the segment identity before hashing. Sub-millimetre
            // floating-origin rounding cannot reshuffle a person's behaviour.
            float segment=fmod(floor((absoluteZ+.01)/40),512);
            float key=phase*19.17+v.persona.w*.713+floor(v.anchor.y*100+.5)*.019+segment*.371;
            float pace=8.5+Hash(key+1.3)*9,cycle=frac(t/pace+Hash(key+3.1));
            float end=.23+Hash(key+6.7)*.10;
            float activity=smoothstep(0,.045,cycle)*(1-smoothstep(end-.09,end,cycle));
            float eventKey=key+_CrowdReactionSeed*.41;
            float choice=Hash(eventKey+5.9),eligible=Hash(eventKey+2.7)<.64?1:0;
            float radius=max(.1,_CrowdReactionData.w),separation=abs(worldAnchor.z-_CrowdReactionOrigin.z);
            float delay=.08+Hash(eventKey+4.9)*.72+separation*.012;
            float reactionAge=_CrowdReactionData.x-delay;
            float selectedSide=abs(_CrowdReactionOrigin.w)<.5?1:(_CrowdReactionOrigin.w*side>0?1:0);
            float reaction=selectedSide*eligible*(1-smoothstep(radius*.25,radius,separation))*saturate(_CrowdReactionData.z);
            reaction*=Pulse(reactionAge,0,2.48)*(1-smoothstep(3.32,3.6,_CrowdReactionData.x));
            reaction*=(_CrowdReactionData.x>=0&&_CrowdReactionData.x<3.6)?1:0;
            float kind=_CrowdReactionData.y,age=_CrowdThrowAge;
            float localThrow=(abs(worldAnchor.x-_CrowdThrowOrigin.x)<.63&&abs(worldAnchor.z-_CrowdThrowOrigin.z)<.55&&age>=0&&age<1.2)?1:0;
            float throwing=localThrow*smoothstep(0,.12,age)*(1-smoothstep(.68,1.2,age));
            if(part>=2.5&&part<5.5)
            {
                float3 shoulder,elbow,hand;Joint(arm,width,Hand(arm,style,t,key,width,activity,reaction,choice,kind,reactionAge,throwing),shoulder,elbow,hand);
                if(part<3.5)Bone(p,n,shoulder,shoulder,elbow);
                else Bone(p,n,shoulder+float3(0,-.30,0),elbow,hand);
            }
            else if(part>5.5&&part<6.5)
            {
                float3 s,e,left,right;
                Joint(-1,width,Hand(-1,style,t,key,width,activity,reaction,choice,kind,reactionAge,0),s,e,left);
                Joint(1,width,Hand(1,style,t,key,width,activity,reaction,choice,kind,reactionAge,throwing),s,e,right);
                float u=saturate((p.x+.44)/.88),edge=abs(u-.5)*2;
                p=lerp(left,right,u)+float3(0,p.y-1.895-.055*(1-edge*edge),.013*sin(t*2.1+u*7+key)*(1-edge));
                n=float3(0,0,1);
            }
            float breathe=sin(t*(1.08+Hash(key+1.1)*.32)+key);
            float lean=.011*sin(t*(.57+Hash(key+7.8)*.18)+key)+throwing*.035;
            if(kind>1.5&&kind<2.5)lean-=reaction*.075;
            float roll=.014*sin(t*(.43+Hash(key+8.2)*.18)+key*1.3),yaw=.018*sin(t*.53+key*2.1);
            float weight=part<.5?smoothstep(.095,.77,p.y):part>6.5?0:1;
            if(part>1.5&&part<2.5)
            {
                float3 toPlayer=_CrowdPlayer.xyz-worldAnchor;
                float attention=(_CrowdPlayer.w>.5?1:0)*(1-smoothstep(8,27,abs(toPlayer.z)));
                float gaze=clamp(toPlayer.z*side*(.031+Hash(key+8.7)*.012),-.42,.42)*attention+.035*sin(t*.48+key);
                float nod=sin(t*(1.4+Hash(key+5.3)) + key)*(.010+reaction*.027);
                p=float3(0,1.405,0)+RotateY(RotateX(p-float3(0,1.405,0),nod),gaze);
                n=RotateY(RotateX(n,nod),gaze);
                shade=lerp(.87,1,saturate((p.y-1.41)/.18));
            }
            else if(part>0.5&&part<1.5)shade=lerp(.91,1,saturate((p.y-.77)/.4));
            p=float3(0,.77,0)+RotateY(RotateZ(RotateX(p-float3(0,.77,0),lean*weight),roll*weight),yaw*weight);
            n=RotateY(RotateZ(RotateX(n,lean*weight),roll*weight),yaw*weight);
            // A small hip transfer compresses knees while soles stay planted.
            p.x+=.012*sin(t*.61+key)*weight;
            p.y-=.008*(.5+.5*breathe)*weight;
            p.z+=.003*breathe*weight;
            p=v.anchor.xyz+float3(-side*p.z,p.y,side*p.x)*height;
            n=float3(-side*n.z,n.y,side*n.x);
        }
        V Vert(A v)
        {
            UNITY_SETUP_INSTANCE_ID(v);
            V o;float3 p,n;float shade;Pose(v,p,n,shade);
            o.positionCS=TransformObjectToHClip(p);o.normal=TransformObjectToWorldNormal(n);
            o.color=v.color.rgb*shade;o.fog=ComputeFogFactor(o.positionCS.z);return o;
        }
        half4 Frag(V i):SV_Target
        {
            half3 n=normalize(i.normal);Light light=GetMainLight();half diffuse=saturate(dot(n,light.direction));
            half3 ambient=max(half3(.19,.20,.22),SampleSH(n)*.72);
            half3 colour=i.color*(ambient+light.color*diffuse*.67);
            return half4(MixFog(colour,i.fog),1);
        }
        // The explicitly selected probe pass is used only by Editor regression
        // checks. It measures the production GPU pose, not a CPU reimplementation.
        struct ProbeV {float4 positionCS:SV_POSITION;float4 result:TEXCOORD0;};
        ProbeV ProbeVert(A v)
        {
            UNITY_SETUP_INSTANCE_ID(v);
            float3 p,n;float shade;Pose(v,p,n,shade);
            float side=v.anchor.x<0?-1:1;float3 d=(p-v.anchor.xyz)/max(.1,v.anchor.w);
            ProbeV o;o.positionCS=float4(v.probe.xy,0,1);o.result=float4(d.z*side,d.y,-d.x*side,1);return o;
        }
        float4 ProbeFrag(ProbeV i):SV_Target{return i.result;}
        ENDHLSL
        Pass
        {
            Name "SupportersForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "CrowdPoseProbe"
            Tags { "LightMode"="CrowdPoseProbe" }
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ProbeVert
            #pragma fragment ProbeFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
