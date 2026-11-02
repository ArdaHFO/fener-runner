using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FenerRunner.Editor
{
    /// <summary>
    /// Re-bakes the generated Run copy for the supplied Generic skeleton.
    /// The imported FBX and every non-Run clip remain untouched.
    /// </summary>
    internal static class RunMotionBuilder
    {
        const string Pelvis = "Kartal_Rig/root/pelvis";
        const int Samples = 48; // 0.8 seconds at 60 Hz, including a closed endpoint.
        const float Duration = .8f;

        public static AnimationClip Build(AnimationClip generatedCopy)
        {
            if (generatedCopy == null) throw new ArgumentNullException(nameof(generatedCopy));
            foreach (string side in new[] { "L", "R" })
            {
                string thigh = Pelvis + "/thigh." + side;
                string shin = thigh + "/shin." + side;
                string foot = shin + "/foot." + side;
                BakeLeg(generatedCopy, thigh, side, 0);
                BakeLeg(generatedCopy, shin, side, 1);
                BakeLeg(generatedCopy, foot, side, 2);
            }

            var heightBinding = EditorCurveBinding.FloatCurve(Pelvis, typeof(Transform), "m_LocalPosition.y");
            AnimationCurve sourceHeight = AnimationUtility.GetEditorCurve(generatedCopy, heightBinding);
            if (sourceHeight == null) throw new InvalidOperationException("Run pelvis translation is missing.");
            float restHeight = sourceHeight.Evaluate(0) - OriginalBob(0);
            AnimationUtility.SetEditorCurve(generatedCopy, heightBinding, PeriodicCurve(t => restHeight + Bob(t)));

            AnimationClip idle = Imported("Idle"), slide = Imported("Slide");
            Stabilize(generatedCopy, Pelvis, .8f);
            PitchOffset(generatedCopy, slide, Pelvis + "/spine", .93f, .14f, .75f);
            PitchOffset(generatedCopy, slide, Pelvis + "/spine/chest", .08f, .035f, .8f);
            PitchOffset(generatedCopy, slide, Pelvis + "/spine/chest/neck", -.30f, -.08f, .3f);
            PitchOffset(generatedCopy, slide, Pelvis + "/spine/chest/neck/head", -.37f, -.095f, .3f);
            BakeSprintUpperBody(generatedCopy);

            generatedCopy.frameRate = 60;
            generatedCopy.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(generatedCopy);
            settings.loopTime = true;
            settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(generatedCopy, settings);
            return generatedCopy;
        }

        // The eighth generated state uses the same supplied rig. It is not an
        // imported animation dependency and has no physics/root displacement.
        public static AnimationClip BuildFlight(AnimationClip importedIdle)
        {
            var clip = new AnimationClip { name = "Flight", frameRate = 60 };
            foreach (var binding in AnimationUtility.GetCurveBindings(importedIdle))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(importedIdle, binding);
                float value = curve.Evaluate(0);
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, Duration, value));
            }
            AnimationClip run = Imported("Run"), slide = Imported("Slide");
            BakeArms(clip, run, importedIdle, true);
            PitchOffset(clip, slide, Pelvis + "/spine", .93f, .10f, 0);
            PitchOffset(clip, slide, Pelvis + "/spine/chest/neck", -.30f, -.05f, 0);
            PitchOffset(clip, slide, Pelvis + "/spine/chest/neck/head", -.37f, -.055f, 0);
            foreach (string side in new[] { "L", "R" })
            {
                string thigh = Pelvis + "/thigh." + side;
                string[] paths = { thigh, thigh + "/shin." + side, thigh + "/shin." + side + "/foot." + side };
                for (int joint = 0; joint < 3; joint++)
                {
                    int j = joint;
                    var original = ReadQuaternion(run, paths[joint]);
                    float offset = side == "R" ? .5f : 0;
                    float firstAngle = LegAngles(offset, false)[joint];
                    var calibration = CalibratePitch(original, t => LegAngles(Mathf.Repeat(t + offset, 1), false)[j]);
                    WriteQuaternion(clip, paths[joint], t => calibration.rotation * Quaternion.AngleAxis(
                        ((j == 0 ? -.72f : j == 1 ? 1.28f : -.45f) + .035f * Mathf.Sin(t * Mathf.PI * 2 + offset * Mathf.PI * 2) - firstAngle) * Mathf.Rad2Deg,
                        calibration.axis));
                }
            }
            var height = EditorCurveBinding.FloatCurve(Pelvis, typeof(Transform), "m_LocalPosition.y");
            float rest = AnimationUtility.GetEditorCurve(importedIdle, height).Evaluate(0);
            AnimationUtility.SetEditorCurve(clip, height, PeriodicCurve(t => rest - .03f + .018f * Mathf.Sin(t * Mathf.PI * 2)));
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        public static AnimationClip[] BuildMenuClips(AnimationClip idle)
        {
            return new[]{BuildMenuClip(idle,"MenuWave",2.4f,0),BuildMenuClip(idle,"MenuPride",2.2f,1),BuildMenuClip(idle,"MenuReady",1.6f,2)};
        }

        public static AnimationClip BuildDeathClip(AnimationClip idle)
        {
            const float seconds=1.05f;const int frames=63;
            var clip=new AnimationClip{name="CollisionFall",frameRate=60};
            foreach(var binding in AnimationUtility.GetCurveBindings(idle))
                AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0,seconds,AnimationUtility.GetEditorCurve(idle,binding).Evaluate(0)));
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(KartalImport.ModelPath));
            model.hideFlags=HideFlags.HideAndDontSave;var baked=new Mesh();
            try
            {
                var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();Transform Bone(string name)=>skin.bones.Single(b=>b.name==name);
                idle.SampleAnimation(model,0);Vector3 up=model.transform.up;
                Vector3 forward=Vector3.ProjectOnPlane(Bone("jaw").position-Bone("head").position,up).normalized,right=Vector3.Cross(up,forward).normalized;
                float Lowest(){skin.BakeMesh(baked);float floor=float.PositiveInfinity;foreach(Vector3 v in baked.vertices)floor=Mathf.Min(floor,skin.transform.TransformPoint(v).y);return floor;}
                float restFloor=Lowest();Transform pelvis=Bone("pelvis");var positions=new Vector3[frames+1];
                var rotations=new Quaternion[skin.bones.Length][];for(int j=0;j<rotations.Length;j++)rotations[j]=new Quaternion[frames+1];
                for(int i=0;i<=frames;i++)
                {
                    idle.SampleAnimation(model,0);float t=(float)i/frames;
                    float fall=Smoother(Mathf.Clamp01((t-.12f)/.88f));
                    float recoil=Mathf.Sin(Mathf.PI*Mathf.Clamp01(t/.27f));
                    pelvis.rotation=Quaternion.AngleAxis(-87*fall+6*recoil,right)*Quaternion.AngleAxis(10*fall,forward)*pelvis.rotation;
                    pelvis.position-=up*(.30f*fall+.025f*recoil)+forward*(.32f*fall);
                    Bone("spine").rotation=Quaternion.AngleAxis(15*fall-9*recoil,right)*Bone("spine").rotation;
                    Bone("chest").rotation=Quaternion.AngleAxis(5*fall,right)*Bone("chest").rotation;
                    Bone("neck").rotation=Quaternion.AngleAxis(8*fall+4*recoil,right)*Bone("neck").rotation;
                    foreach(string side in new[]{"L","R"})
                    {
                        bool left=side=="L";
                        Bone("thigh."+side).rotation=Quaternion.AngleAxis((left?-40:-62)*fall,right)*Bone("thigh."+side).rotation;
                        Bone("shin."+side).rotation=Quaternion.AngleAxis((left?95:110)*fall,right)*Bone("shin."+side).rotation;
                        Bone("foot."+side).rotation=Quaternion.AngleAxis((left?-25:-18)*fall,right)*Bone("foot."+side).rotation;
                        Transform upper=Bone("upper_arm."+side),fore=Bone("forearm."+side),hand=Bone("hand."+side);
                        float sign=Mathf.Sign(Vector3.Dot(upper.position-Bone("chest").position,right));Vector3 outward=right*sign;
                        Vector3 target=Bone("chest").position+outward*.44f+forward*.10f+up*(.2f*recoil-.35f*fall);
                        Quaternion upperRest=upper.localRotation,foreRest=fore.localRotation;
                        SolveArm(upper,fore,hand,target,outward-up*.35f);
                        Quaternion upperPose=upper.localRotation,forePose=fore.localRotation;float brace=Smoother(Mathf.Clamp01(t/.18f));
                        upper.localRotation=Quaternion.Slerp(upperRest,upperPose,brace);fore.localRotation=Quaternion.Slerp(foreRest,forePose,brace);
                    }
                    // Fit the entire fallen silhouette to the same floor as Idle.
                    // The runtime separately descends from any frozen airborne height.
                    pelvis.position+=up*(restFloor-Lowest());positions[i]=pelvis.localPosition;
                    for(int j=0;j<rotations.Length;j++)rotations[j][i]=skin.bones[j].localRotation;
                }
                for(int j=0;j<rotations.Length;j++)WriteGestureRotation(clip,AnimationUtility.CalculateTransformPath(skin.bones[j],model.transform),rotations[j],seconds);
                const string channels="xyz";float dt=seconds/frames;
                for(int axis=0;axis<3;axis++)
                {
                    var keys=new Keyframe[frames+1];
                    for(int i=0;i<=frames;i++){float slope=i==0||i==frames?0:(positions[i+1][axis]-positions[i-1][axis])/(2*dt);keys[i]=new Keyframe(i*dt,positions[i][axis],slope,slope);}
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(Pelvis,typeof(Transform),"m_LocalPosition."+channels[axis]),new AnimationCurve(keys));
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(model);}
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=settings.loopBlend=false;AnimationUtility.SetAnimationClipSettings(clip,settings);return clip;
        }

        static AnimationClip BuildMenuClip(AnimationClip idle,string name,float seconds,int gesture)
        {
            var clip=new AnimationClip{name=name,frameRate=60};
            foreach(var binding in AnimationUtility.GetCurveBindings(idle))
                AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0,seconds,AnimationUtility.GetEditorCurve(idle,binding).Evaluate(0)));
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(KartalImport.ModelPath));
            model.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();Transform Bone(string value)=>skin.bones.Single(b=>b.name==value);
                idle.SampleAnimation(model,0);Vector3 up=model.transform.up;
                Vector3 forward=Vector3.ProjectOnPlane(Bone("jaw").position-Bone("head").position,up).normalized,right=Vector3.Cross(up,forward).normalized;
                string[] names={"spine","chest","neck","head","jaw","upper_arm.L","forearm.L","hand.L","upper_arm.R","forearm.R","hand.R"};
                int frames=Mathf.CeilToInt(seconds*60);var rotations=new Quaternion[names.Length][];
                for(int j=0;j<names.Length;j++)rotations[j]=new Quaternion[frames+1];
                for(int i=0;i<=frames;i++)
                {
                    idle.SampleAnimation(model,0);float t=(float)i/frames,weight=GestureEnvelope(t);
                    float action=Mathf.Clamp01((t-.23f)/.5f),beat=Mathf.Sin(action*Mathf.PI*4);
                    if(gesture==0)
                    {
                        Bone("chest").rotation=Quaternion.AngleAxis(-5*weight,up)*Bone("chest").rotation;
                        Bone("head").rotation=Quaternion.AngleAxis(4*weight,forward)*Quaternion.AngleAxis(-3*weight,right)*Bone("head").rotation;
                    }
                    else if(gesture==1)
                    {
                        Bone("chest").rotation=Quaternion.AngleAxis(-3*weight,right)*Bone("chest").rotation;
                        Bone("head").rotation=Quaternion.AngleAxis(-4*weight,right)*Bone("head").rotation;
                    }
                    else
                    {
                        float dip=weight*(4+2*Mathf.Sin(action*Mathf.PI*2));
                        Bone("spine").rotation=Quaternion.AngleAxis(dip,right)*Bone("spine").rotation;
                        Bone("head").rotation=Quaternion.AngleAxis(-dip*.55f,right)*Bone("head").rotation;
                    }
                    foreach(string side in new[]{"L","R"})
                    {
                        Transform upper=Bone("upper_arm."+side),fore=Bone("forearm."+side),hand=Bone("hand."+side);
                        float sign=Mathf.Sign(Vector3.Dot(upper.position-Bone("chest").position,right));Vector3 outward=right*sign;
                        if(gesture==0&&side=="L")continue;
                        Vector3 target;
                        if(gesture==0)target=upper.position+up*.41f+outward*(.17f+.065f*beat)+forward*.14f;
                        else if(gesture==1)target=Bone("chest").position+up*.015f+outward*.13f+forward*(.37f-.015f*Mathf.Cos(action*Mathf.PI*4));
                        else target=Bone("chest").position-up*.13f+outward*.30f+forward*(.30f+.025f*beat);
                        Quaternion upperRest=upper.localRotation,foreRest=fore.localRotation;
                        SolveArm(upper,fore,hand,target,outward-up*.5f);
                        Quaternion upperPose=upper.localRotation,forePose=fore.localRotation;
                        upper.localRotation=Quaternion.Slerp(upperRest,upperPose,weight);fore.localRotation=Quaternion.Slerp(foreRest,forePose,weight);
                        if(gesture==0)hand.rotation=Quaternion.AngleAxis(14*beat*weight,forward)*hand.rotation;
                    }
                    // A restrained beak movement accompanies the proud smile.
                    if(gesture==1)Bone("jaw").rotation=Quaternion.AngleAxis(-5*weight,right)*Bone("jaw").rotation;
                    for(int j=0;j<names.Length;j++)rotations[j][i]=Bone(names[j]).localRotation;
                }
                for(int j=0;j<names.Length;j++)
                {
                    string path=AnimationUtility.CalculateTransformPath(Bone(names[j]),model.transform);
                    WriteGestureRotation(clip,path,rotations[j],seconds);
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(model);}
            clip.EnsureQuaternionContinuity();var clipSettings=AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime=clipSettings.loopBlend=false;AnimationUtility.SetAnimationClipSettings(clip,clipSettings);return clip;
        }

        static float GestureEnvelope(float t)=>Smoother(Mathf.Clamp01(t/.23f))*(1-Smoother(Mathf.Clamp01((t-.73f)/.27f)));
        static float Smoother(float t)=>t*t*t*(t*(t*6-15)+10);
        static void SolveArm(Transform upper,Transform fore,Transform hand,Vector3 target,Vector3 pole)
        {
            Vector3 start=upper.position,delta=target-start;float upperLength=Vector3.Distance(start,fore.position),lowerLength=Vector3.Distance(fore.position,hand.position);
            float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(upperLength-lowerLength)+.015f,upperLength+lowerLength-.015f);
            Vector3 direction=delta.normalized,bend=Vector3.ProjectOnPlane(pole,direction).normalized;
            float cosine=Mathf.Clamp((upperLength*upperLength+distance*distance-lowerLength*lowerLength)/(2*upperLength*distance),-1,1);
            Vector3 elbowDirection=direction*cosine+bend*Mathf.Sqrt(1-cosine*cosine);
            upper.rotation=Quaternion.FromToRotation(fore.position-start,elbowDirection)*upper.rotation;
            fore.rotation=Quaternion.FromToRotation(hand.position-fore.position,start+direction*distance-fore.position)*fore.rotation;
        }
        static void WriteGestureRotation(AnimationClip clip,string path,Quaternion[] values,float seconds)
        {
            for(int i=1;i<values.Length;i++)if(Quaternion.Dot(values[i-1],values[i])<0)values[i]=Negate(values[i]);
            const string channels="xyzw";int frames=values.Length-1;float dt=seconds/frames;
            for(int component=0;component<4;component++)
            {
                var keys=new Keyframe[values.Length];
                for(int i=0;i<values.Length;i++)
                {float slope=i==0||i==frames?0:(values[i+1][component]-values[i-1][component])/(2*dt);keys[i]=new Keyframe(i*dt,values[i][component],slope,slope);}
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+channels[component]),new AnimationCurve(keys));
            }
        }

        /// <summary>
        /// Fit the authored support/flight timing to the actual weighted foot mesh.
        /// Call after the player prefab has been aligned to the real Idle soles.
        /// Only the visual pelvis Y curve changes; the physics root is untouched.
        /// </summary>
        public static void GroundSprint(GameObject playerPrefab,AnimationClip run)
        {
            const int steps=96;
            var instance=UnityEngine.Object.Instantiate(playerPrefab);
            instance.hideFlags=HideFlags.HideAndDontSave;
            instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var baked=new Mesh();
            try
            {
                var skin=instance.GetComponentInChildren<SkinnedMeshRenderer>();
                var animator=instance.GetComponentInChildren<Animator>();
                Transform pelvis=skin.bones.Single(b=>b.name=="pelvis");
                int leftBone=Array.FindIndex(skin.bones,b=>b.name=="foot.L");
                int rightBone=Array.FindIndex(skin.bones,b=>b.name=="foot.R");
                var weights=skin.sharedMesh.boneWeights;
                var left=new System.Collections.Generic.List<int>();var right=new System.Collections.Generic.List<int>();
                for(int i=0;i<weights.Length;i++)
                {
                    BoneWeight w=weights[i];int bone=w.boneIndex0;float strongest=w.weight0;
                    if(w.weight1>strongest){bone=w.boneIndex1;strongest=w.weight1;}
                    if(w.weight2>strongest){bone=w.boneIndex2;strongest=w.weight2;}
                    if(w.weight3>strongest)bone=w.boneIndex3;
                    if(bone==leftBone)left.Add(i);if(bone==rightBone)right.Add(i);
                }
                if(left.Count==0||right.Count==0)throw new InvalidOperationException("Sprint grounding needs weighted foot vertices.");
                var positions=new Vector3[steps];var leftContacts=new Vector3[steps];var rightContacts=new Vector3[steps];
                var floors=new float[steps];var lifts=new float[steps];
                for(int i=0;i<steps;i++)
                {
                    float phase=(float)i/steps;
                    run.SampleAnimation(animator.gameObject,phase*Duration);
                    positions[i]=pelvis.localPosition;skin.BakeMesh(baked);var vertices=baked.vertices;
                    leftContacts[i]=SoleContact(left,vertices,skin.transform,out float leftY);
                    rightContacts[i]=SoleContact(right,vertices,skin.transform,out float rightY);
                    float half=Mathf.Repeat(phase,.5f);bool support=half<.34f;
                    floors[i]=support?(phase<.5f?leftY:rightY):Mathf.Min(leftY,rightY);
                    if(!support){float u=(half-.34f)/.16f;lifts[i]=.08f*Mathf.Pow(Mathf.Sin(u*Mathf.PI),2);}
                }
                // Fit the soft sole contact centroid to a straight backward track
                // during support, including ankle roll and pelvis counterrotation.
                // Airborne intervals connect both supports with C1 Hermite curves.
                float maximumCorrection=0,maximumHorizontalCorrection=0;
                for(int i=0;i<steps;i++)
                {
                    float phase=(float)i/steps;
                    Vector3 correction=HorizontalSupportCorrection(phase,leftContacts,rightContacts);
                    maximumHorizontalCorrection=Mathf.Max(maximumHorizontalCorrection,new Vector2(correction.x,correction.z).magnitude);
                    correction.y=.025f+lifts[i]-floors[i];
                    positions[i]+=pelvis.parent.InverseTransformVector(correction);
                    maximumCorrection=Mathf.Max(maximumCorrection,correction.magnitude);
                }
                if(maximumHorizontalCorrection>.14f)throw new InvalidOperationException("Unexpectedly large sprint contact correction: "+maximumHorizontalCorrection);
                const string channels="xyz";
                for(int axis=0;axis<3;axis++)
                {
                    var keys=new Keyframe[steps+1];
                    for(int i=0;i<=steps;i++)
                    {
                        int index=i%steps,before=(index+steps-1)%steps,after=(index+1)%steps;
                        float tangent=(positions[after][axis]-positions[before][axis])/(2*Duration/steps);
                        keys[i]=new Keyframe(i*Duration/steps,positions[index][axis],tangent,tangent);
                    }
                    AnimationUtility.SetEditorCurve(run,EditorCurveBinding.FloatCurve(Pelvis,typeof(Transform),"m_LocalPosition."+channels[axis]),new AnimationCurve(keys));
                }
                EditorUtility.SetDirty(run);
                Debug.Log("Sprint support fitted: maximum visual adjustment="+maximumCorrection.ToString("F4")+" m; horizontal="+maximumHorizontalCorrection.ToString("F4")+" m.");
            }
            finally{UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(instance);}
        }

        static Vector3 SoleContact(System.Collections.Generic.List<int> indices,Vector3[] vertices,Transform space,out float floor)
        {
            floor=float.PositiveInfinity;
            foreach(int index in indices)floor=Mathf.Min(floor,space.TransformPoint(vertices[index]).y);
            Vector3 weighted=Vector3.zero;float total=0;
            foreach(int index in indices)
            {
                Vector3 p=space.TransformPoint(vertices[index]);float weight=Mathf.Exp(-(p.y-floor)/.012f);
                weighted+=p*weight;total+=weight;
            }
            return weighted/total;
        }
        static Vector3 AtPhase(Vector3[] samples,float phase)
        {
            float position=Mathf.Repeat(phase,1)*samples.Length;int index=Mathf.FloorToInt(position);
            return Vector3.LerpUnclamped(samples[index%samples.Length],samples[(index+1)%samples.Length],position-index);
        }
        static Vector3 SupportCorrection(float phase,float start,Vector3[] contacts)
        {
            Vector3 from=AtPhase(contacts,start),to=AtPhase(contacts,start+.34f),actual=AtPhase(contacts,phase);
            return new Vector3((from.x+to.x)*.5f-actual.x,0,Mathf.LerpUnclamped(from.z,to.z,(phase-start)/.34f)-actual.z);
        }
        static Vector3 HorizontalSupportCorrection(float phase,Vector3[] left,Vector3[] right)
        {
            float start=phase<.5f?0:.5f,half=phase-start;
            Vector3[] current=phase<.5f?left:right,next=phase<.5f?right:left;
            if(half<.34f)return SupportCorrection(phase,start,current);
            const float epsilon=1f/96f;
            float end=start+.34f,nextStart=start+.5f;
            Vector3 from=SupportCorrection(end,start,current),to=SupportCorrection(nextStart,nextStart,next);
            Vector3 fromSlope=(from-SupportCorrection(end-epsilon,start,current))/epsilon;
            Vector3 toSlope=(SupportCorrection(nextStart+epsilon,nextStart,next)-to)/epsilon;
            float t=(half-.34f)/.16f;
            return new Vector3(Hermite(from.x,to.x,fromSlope.x*.16f,toSlope.x*.16f,t),0,
                Hermite(from.z,to.z,fromSlope.z*.16f,toSlope.z*.16f,t));
        }

        static void BakeSprintUpperBody(AnimationClip clip)
        {
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(KartalImport.ModelPath));
            model.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();
                Transform Bone(string name)=>skin.bones.Single(b=>b.name==name);
                clip.SampleAnimation(model,0);
                Vector3 up=model.transform.up;
                Vector3 forward=Vector3.ProjectOnPlane(Bone("jaw").position-Bone("head").position,up).normalized;
                Vector3 right=Vector3.Cross(up,forward).normalized;
                Quaternion pelvisReference=Bone("pelvis").rotation,chestReference=Bone("chest").rotation,neckReference=Bone("neck").rotation;
                string[] names={"pelvis","chest","neck","upper_arm.L","forearm.L","upper_arm.R","forearm.R"};
                var rotations=new Quaternion[names.Length][];
                for(int j=0;j<names.Length;j++)rotations[j]=new Quaternion[Samples+1];
                for(int i=0;i<=Samples;i++)
                {
                    float phase=i==Samples?0:(float)i/Samples;
                    clip.SampleAnimation(model,phase*Duration);
                    float twist=Mathf.Sin(phase*Mathf.PI*2);
                    float balance=Mathf.Cos(phase*Mathf.PI*2);
                    Bone("pelvis").rotation=Quaternion.AngleAxis(3.8f*twist,up)*Quaternion.AngleAxis(.75f*balance,forward)*pelvisReference;
                    Bone("chest").rotation=Quaternion.AngleAxis(-4.5f*twist,up)*Quaternion.AngleAxis(-.4f*balance,forward)*chestReference;
                    Bone("neck").rotation=Quaternion.AngleAxis(.5f*twist,up)*neckReference;
                    foreach(string side in new[]{"L","R"})
                    {
                        Transform upper=Bone("upper_arm."+side),fore=Bone("forearm."+side),hand=Bone("hand."+side);
                        float sideSign=Mathf.Sign(Vector3.Dot(upper.position-Bone("chest").position,right));
                        float beat=(phase+(side=="R"?.5f:0)-.02f)*Mathf.PI*2;
                        Vector3 upperDirection=(-up-forward*(.62f*Mathf.Cos(beat))+right*(sideSign*.18f)).normalized;
                        upper.rotation=Quaternion.FromToRotation(fore.position-upper.position,upperDirection)*upper.rotation;
                        Vector3 foreDirection=Quaternion.AngleAxis(-82f-7f*Mathf.Cos(beat+.3f),right)*upperDirection;
                        fore.rotation=Quaternion.FromToRotation(hand.position-fore.position,foreDirection)*fore.rotation;
                    }
                    for(int j=0;j<names.Length;j++)rotations[j][i]=Bone(names[j]).localRotation;
                }
                for(int j=0;j<names.Length;j++)
                {
                    int joint=j;string path=AnimationUtility.CalculateTransformPath(Bone(names[j]),model.transform);
                    WriteQuaternion(clip,path,t=>rotations[joint][Mathf.RoundToInt(t*Samples)]);
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(model);}
        }

        static AnimationClip Imported(string name) => AssetDatabase.LoadAllAssetsAtPath(KartalImport.ModelPath)
            .OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__") && KartalImport.ShortName(c.name) == name);

        static void BakeLeg(AnimationClip clip, string path, string side, int joint)
        {
            var curves = ReadQuaternion(clip, path);
            float offset = side == "R" ? .5f : 0f;
            float angle0 = LegAngles(offset, false)[joint];
            Quaternion q0 = Read(curves, 0);
            float largestChange = 0;
            Quaternion calibration = q0;
            for (int i = 1; i < 24; i++)
            {
                float t = i / 24f;
                float change = LegAngles(Mathf.Repeat(t + offset, 1), false)[joint] - angle0;
                if (Mathf.Abs(change) <= Mathf.Abs(largestChange)) continue;
                largestChange = change;
                calibration = Read(curves, t * Duration);
            }
            if (Mathf.Abs(largestChange) < .03f)
                throw new InvalidOperationException("Cannot calibrate supplied Run joint: " + path);
            if (Quaternion.Dot(q0, calibration) < 0) calibration = Negate(calibration);
            Quaternion delta = Quaternion.Inverse(q0) * calibration;
            delta.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180) { degrees = 360 - degrees; axis = -axis; }
            if (largestChange < 0) axis = -axis;

            // The supplied leg rig is a sagittal two-link system. Derive its axis
            // from actual imported quaternion keys instead of assuming FBX axes.
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                float oldAngle = LegAngles(Mathf.Repeat(t + offset, 1), false)[joint];
                Quaternion expected = q0 * Quaternion.AngleAxis((oldAngle - angle0) * Mathf.Rad2Deg, axis);
                if (Quaternion.Angle(expected, Read(curves, t * Duration)) > 3f)
                    throw new InvalidOperationException("Run joint axis no longer matches supplied rig: " + path);
            }
            WriteQuaternion(clip, path, t => q0 * Quaternion.AngleAxis(
                (LegAngles(Mathf.Repeat(t + offset, 1), true)[joint] - angle0) * Mathf.Rad2Deg, axis));
        }

        static Vector3 LegAngles(float phase, bool improved)
        {
            phase = Mathf.Repeat(phase, 1);
            float y, z, pitch;
            float body = improved ? Bob(phase) : OriginalBob(phase);
            if (improved)
            {
                const float contact = .34f;
                const float stride = .44f;
                if (phase < contact)
                {
                    float u = phase / contact;
                    float push = Smooth(Mathf.InverseLerp(.60f, 1f, u));
                    y = -.22f + stride * u;
                    z = .12f + .06f * push;
                    pitch = -.055f * (1 - Smooth(Mathf.InverseLerp(0, .18f, u))) + .46f * push;
                }
                else
                {
                    float u = (phase - contact) / (1 - contact);
                    float tangent = stride / contact * (1 - contact);
                    // Clear heel recovery, then knee drive and backward-moving
                    // touchdown. Both feet leave support for 32% of a stride.
                    y = Track(u, new[]{0f,.20f,.65f,.90f,1f}, new[]{.22f,.285f,-.15f,-.25f,-.22f}, new[]{tangent,0f,-1.1f,0f,tangent});
                    z = Track(u, new[]{0f,.22f,.62f,.90f,1f}, new[]{.18f,.40f,.32f,.15f,.12f}, new[]{0f,0f,-.35f,-.5f,0f});
                    pitch = Track(u, new[]{0f,.22f,.62f,1f}, new[]{.46f,.10f,-.30f,-.055f}, new[]{0f,-.6f,0f,0f});
                }
            }
            else if (phase < .55f)
            {
                y = -.13f + .26f * phase / .55f;
                z = .12f;
                pitch = 0;
            }
            else
            {
                float u = (phase - .55f) / .45f;
                y = .13f - .26f * u * u * (3 - 2 * u);
                z = .12f + .115f * Mathf.Sin(Mathf.PI * u);
                pitch = -.24f * Mathf.Sin(Mathf.PI * u);
            }

            float upper = Mathf.Sqrt(.015f * .015f + .28f * .28f);
            float lower = Mathf.Sqrt(.015f * .015f + .18f * .18f);
            float dz = z - (.58f + body);
            float distance = Mathf.Clamp(Mathf.Sqrt(y * y + dz * dz), Mathf.Abs(upper - lower) + .002f, upper + lower - .001f);
            float aim = Mathf.Atan2(y, -dz);
            float alpha = Mathf.Acos(Mathf.Clamp((upper * upper + distance * distance - lower * lower) / (2 * upper * distance), -1, 1));
            float bend = Mathf.Acos(Mathf.Clamp((distance * distance - upper * upper - lower * lower) / (2 * upper * lower), -1, 1));
            float thighRest = Mathf.Atan2(-.015f, .28f);
            float shinRest = Mathf.Atan2(.015f, .18f);
            float thigh = aim - alpha - thighRest;
            float shin = bend - (shinRest - thighRest);
            return new Vector3(thigh, shin, pitch - thigh - shin);
        }

        static float OriginalBob(float t) => -.045f + .028f * (.5f - .5f * Mathf.Cos(t * Mathf.PI * 4));
        static float Bob(float t) => Track(Mathf.Repeat(t * 2, 1),
            new[]{0f,.22f,.68f,.84f,1f}, new[]{-.055f,-.080f,-.050f,.010f,-.055f}, new[]{-.28f,0f,.35f,0f,-.28f});
        static float Smooth(float t) => t*t*(3-2*t);
        static float Track(float t, float[] time, float[] value, float[] derivative)
        {
            for(int i=0;i<time.Length-1;i++)
                if(t<=time[i+1])
                {
                    float span=time[i+1]-time[i];
                    return Hermite(value[i],value[i+1],derivative[i]*span,derivative[i+1]*span,(t-time[i])/span);
                }
            return value[value.Length-1];
        }
        static float Hermite(float from, float to, float startSlope, float endSlope, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return (2 * t3 - 3 * t2 + 1) * from + (t3 - 2 * t2 + t) * startSlope
                + (-2 * t3 + 3 * t2) * to + (t3 - t2) * endSlope;
        }

        static void Stabilize(AnimationClip clip, string path, float scale)
        {
            var original = ReadQuaternion(clip, path);
            Quaternion origin = Read(original, 0);
            WriteQuaternion(clip, path, t => Quaternion.SlerpUnclamped(origin, Read(original, t * Duration), scale));
        }

        static void PitchOffset(AnimationClip clip, AnimationClip slide, string path, float slidePeak, float offset, float sway)
        {
            var reference=ReadQuaternion(slide,path);
            Vector3 axis=ShortAxis(Quaternion.Inverse(Read(reference,0))*Read(reference,slide.length*.5f));
            if(slidePeak<0)axis=-axis;
            var original=ReadQuaternion(clip,path);
            Quaternion origin=Read(original,0);
            WriteQuaternion(clip,path,t=>Quaternion.SlerpUnclamped(origin,Read(original,t*Duration),sway)*Quaternion.AngleAxis(offset*Mathf.Rad2Deg,axis));
        }

        static (Quaternion rotation,Vector3 axis) CalibratePitch(AnimationCurve[] curves,Func<float,float> angle)
        {
            Quaternion first=Read(curves,0), furthest=first;float delta=0,angle0=angle(0);
            for(int i=1;i<24;i++)
            {
                float t=i/24f,change=angle(t)-angle0;
                if(Mathf.Abs(change)<=Mathf.Abs(delta))continue;
                delta=change;furthest=Read(curves,t*Duration);
            }
            if(Mathf.Abs(delta)<.025f)throw new InvalidOperationException("Cannot calibrate motion axis.");
            Vector3 axis=ShortAxis(Quaternion.Inverse(first)*furthest);
            return(first,delta<0?-axis:axis);
        }

        sealed class ArmBasis
        {
            public Quaternion rotation,abstractOrigin;
            public Vector3 x,y,z;
            public Quaternion Pose(Vector3 euler)
            {
                Quaternion change=Quaternion.Inverse(abstractOrigin)*Euler(euler);
                if(change.w<0)change=Negate(change);
                change.ToAngleAxis(out float angle,out Vector3 direction);
                if(angle<.00001f)return rotation;
                Vector3 mapped=(x*direction.x+y*direction.y+z*direction.z).normalized;
                return rotation*Quaternion.AngleAxis(angle,mapped);
            }
        }

        static void BakeArms(AnimationClip target,AnimationClip originalRun,AnimationClip idle,bool flying)
        {
            foreach(string side in new[]{"L","R"})
            {
                int sign=side=="L"?1:-1;
                for(int joint=0;joint<2;joint++)
                {
                    bool forearm=joint==1;
                    string path=Pelvis+"/spine/chest/upper_arm."+side+(forearm?"/forearm."+side:"");
                    ArmBasis basis=CalibrateArm(originalRun,idle,path,sign,forearm);
                    WriteQuaternion(target,path,t=>
                    {
                        float phase=t+(sign<0?.5f:0), swing=Mathf.Cos(phase*Mathf.PI*2);
                        if(flying)
                        {
                            float flap=Mathf.Sin(t*Mathf.PI*2);
                            return basis.Pose(forearm?new Vector3(-.30f+.12f*flap,sign*.02f,0):
                                new Vector3(-.08f+.12f*flap,sign*(-.70f+.22f*flap),sign*.03f));
                        }
                        return basis.Pose(forearm?new Vector3(-.90f-.23f*(swing+1)*.5f,sign*.02f,0):
                            new Vector3(-.12f+.68f*swing,sign*.30f,sign*.025f));
                    });
                }
            }
        }

        static ArmBasis CalibrateArm(AnimationClip run,AnimationClip idle,string path,int sign,bool forearm)
        {
            var curves=ReadQuaternion(run,path);
            Func<float,Vector3> pose=t=>OriginalArm(t,sign,forearm);
            var pitch=CalibratePitch(curves,t=>pose(t).x);
            Quaternion abstract0=Euler(pose(0));
            Vector3 idlePose=forearm?new Vector3(-.11f,.04f*sign,0):new Vector3(-.04f,.47f*sign,0);
            Vector3 from=ShortAxis(Quaternion.Inverse(abstract0)*Euler(idlePose));
            Vector3 to=ShortAxis(Quaternion.Inverse(pitch.rotation)*Read(ReadQuaternion(idle,path),0));
            Vector3 a=(from-Vector3.right*from.x).normalized;
            Vector3 b=(to-pitch.axis*Vector3.Dot(to,pitch.axis)).normalized;
            if(a.sqrMagnitude<.9f||b.sqrMagnitude<.9f)throw new InvalidOperationException("Cannot calibrate arm roll: "+path);
            Vector3 c=Vector3.Cross(Vector3.right,a),d=Vector3.Cross(pitch.axis,b);
            var basis=new ArmBasis{rotation=pitch.rotation,abstractOrigin=abstract0,x=pitch.axis,
                y=b*a.y+d*c.y,z=b*a.z+d*c.z};
            for(int i=0;i<=24;i++)
                if(Quaternion.Angle(basis.Pose(pose(i/24f)),Read(curves,i*Duration/24f))>3f)
                    throw new InvalidOperationException("Arm basis differs from supplied rig: "+path);
            return basis;
        }

        static Vector3 OriginalArm(float phase,int sign,bool forearm)
        {
            float swing=Mathf.Sin((phase+(sign<0?.5f:0))*Mathf.PI*2);
            return forearm?new Vector3(-.35f-.14f*Mathf.Max(0,-swing),.02f*sign,0):new Vector3(.46f*swing-.05f,.39f*sign,.025f*sign);
        }
        static Quaternion Euler(Vector3 radians)=>Quaternion.AngleAxis(radians.z*Mathf.Rad2Deg,Vector3.forward)*
            Quaternion.AngleAxis(radians.y*Mathf.Rad2Deg,Vector3.up)*Quaternion.AngleAxis(radians.x*Mathf.Rad2Deg,Vector3.right);
        static Vector3 ShortAxis(Quaternion q)
        {
            q=q.normalized;if(q.w<0)q=Negate(q);
            q.ToAngleAxis(out float degrees,out Vector3 axis);
            if(degrees<.00001f)throw new InvalidOperationException("Rotation is too small to define a reliable axis.");
            return axis.normalized;
        }

        static AnimationCurve[] ReadQuaternion(AnimationClip clip, string path)
        {
            var curves = new AnimationCurve[4];
            const string channels = "xyzw";
            for (int i = 0; i < curves.Length; i++)
            {
                curves[i] = AnimationUtility.GetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + channels[i]));
                if (curves[i] == null) throw new InvalidOperationException("Missing Run rotation: " + path + "/" + channels[i]);
            }
            return curves;
        }

        static Quaternion Read(AnimationCurve[] curves, float seconds) => new Quaternion(
            curves[0].Evaluate(seconds), curves[1].Evaluate(seconds), curves[2].Evaluate(seconds), curves[3].Evaluate(seconds)).normalized;
        static Quaternion Negate(Quaternion q) => new Quaternion(-q.x, -q.y, -q.z, -q.w);

        static void WriteQuaternion(AnimationClip clip, string path, Func<float, Quaternion> sample)
        {
            var values = new Quaternion[Samples + 1];
            for (int i = 0; i <= Samples; i++)
            {
                Quaternion q = sample(i == Samples ? 0 : (float)i / Samples).normalized;
                if (i > 0 && Quaternion.Dot(values[i - 1], q) < 0) q = Negate(q);
                values[i] = q;
            }
            const string channels = "xyzw";
            for (int component = 0; component < 4; component++)
            {
                int index = component;
                var keys = new Keyframe[Samples + 1];
                for (int i = 0; i <= Samples; i++)
                {
                    int previous = i == 0 || i == Samples ? Samples - 1 : i - 1;
                    int next = i == Samples ? 1 : i + 1;
                    float slope = (values[next][index] - values[previous][index]) / (2 * Duration / Samples);
                    keys[i] = new Keyframe(i * Duration / Samples, values[i][index], slope, slope);
                }
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + channels[component]), new AnimationCurve(keys));
            }
        }

        static AnimationCurve PeriodicCurve(Func<float, float> sample)
        {
            var keys = new Keyframe[Samples + 1];
            float step = 1f / Samples;
            for (int i = 0; i <= Samples; i++)
            {
                float t = i == Samples ? 0 : i * step;
                float slope = (sample(Mathf.Repeat(t + step, 1)) - sample(Mathf.Repeat(t - step, 1))) / (2 * step * Duration);
                keys[i] = new Keyframe(i * Duration / Samples, sample(t), slope, slope);
            }
            return new AnimationCurve(keys);
        }
    }
}
