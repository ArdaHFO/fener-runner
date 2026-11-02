#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class RunMotionChecksV8:MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed,preferencesPreserved;
            public string utc,unityVersion;
            public string[] checks,errors,images;
            public float lowestSole,highestLowerSole,leftRecovery,rightRecovery,flightFraction;
            public float leftSupportResidual,rightSupportResidual,leftSupportTravel,rightSupportTravel,supportHeightRange;
            public float minimumElbow,maximumElbow,hipYawRange,shoulderYawRange,counterrotationCorrelation;
            public float loopPositionError,loopRotationError,baseCadence,fastCadence,baseLean,fastLean,maximumLeanFrameStep;
            public float testedBaseSpeed,testedFastSpeed;
            public string scope="Actual Unity skinned rig geometry, real runtime speed response through the shared42m/s hard cap, and front/side pose strips rendered from the game scene. Support residual measures local sole travel against a straight backward track. The18–42m/s arcade travel speed exceeds this short-legged character's anatomical stride speed, so this does not claim zero world-space skating or human motion-capture fidelity.";
        }
        const string Flag="FenerRunner.RunMotionV8";
        const string Animations="Assets/FenerRunner/Generated/Animations/";
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        Report report=new Report();RunnerGame game;RunnerAnimation motion;RunnerFeedback feedback;
        SkinnedMeshRenderer skin;Transform[] bones;Mesh baked;RunWallet testWallet;
        bool background,ready;int oldCoins,oldScore;float oldBest;
        AnimationClip run;Camera viewCamera;FollowCamera follow;RenderTexture previousTarget;
        Vector3 cameraPosition;Quaternion cameraRotation;bool oldFollow,oldFeedback,oldAnimator;float cameraFov;

        [MenuItem("Fener Runner/36 - Verify sprint motion and capture pose strips")]
        public static void StartChecks(){if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before motion checks.");SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Sprint motion v8 checks").AddComponent<RunMotionChecksV8>();}}
        IEnumerator Start()
        {
            background=Application.runInBackground;Application.runInBackground=true;Application.logMessageReceived+=Log;
            oldCoins=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0);oldScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);oldBest=PlayerPrefs.GetFloat("FenerRunner.Best",0);
            game=FindFirstObjectByType<RunnerGame>();if(game==null){Check(false,"RunnerGame exists");Finish();yield break;}
            game.AutomatedTest=true;testWallet=game.UseWalletForTest("FenerRunner.Test.MotionV8."+DateTime.UtcNow.Ticks+".");
            game.Play();game.enabled=false;game.Obstacles.SetActive(false);game.Tokens.Enabled=false;game.Powerups.Enabled=false;
            motion=game.Animation;motion.TurnAllowed=false;feedback=game.GetComponent<RunnerFeedback>();
            oldFeedback=feedback.enabled;feedback.enabled=false;feedback.ResetFeedback();
            oldAnimator=motion.Animator.enabled;motion.Animator.enabled=false;
            skin=motion.Animator.GetComponentInChildren<SkinnedMeshRenderer>();bones=skin.bones;baked=new Mesh();
            run=AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations+"Run.anim");
            viewCamera=Camera.main;follow=viewCamera.GetComponent<FollowCamera>();oldFollow=follow.enabled;follow.enabled=false;
            previousTarget=viewCamera.targetTexture;cameraPosition=viewCamera.transform.position;cameraRotation=viewCamera.transform.rotation;cameraFov=viewCamera.fieldOfView;ready=true;
            MeasureGeometry();CheckUntouchedActionClips();
            yield return CaptureStrip("v8-run-side-sequence",Vector3.right);
            yield return CaptureStrip("v8-run-front-sequence",Vector3.forward);
            report.images=new[]{Path.GetFileName(ReviewEvidence.FileFor(8,"../Validation/v8-run-side-sequence.png")),Path.GetFileName(ReviewEvidence.FileFor(8,"../Validation/v8-run-front-sequence.png"))};
            viewCamera.targetTexture=previousTarget;viewCamera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);viewCamera.fieldOfView=cameraFov;
            motion.Animator.enabled=true;motion.ResetRun(true);game.Motor.Speed=game.Settings.forwardSpeed;report.testedBaseSpeed=game.Motor.Speed;
            float until=Time.realtimeSinceStartup+.5f;
            while(Time.realtimeSinceStartup<until){motion.Tick(Time.deltaTime);yield return null;}
            report.baseCadence=motion.RunCadence;report.baseLean=motion.AppliedSprintLeanDegrees;
            game.Motor.Speed=RunTempo.HardCap;report.testedFastSpeed=game.Motor.Speed;until=Time.realtimeSinceStartup+1f;float previous=motion.AppliedSprintLeanDegrees;
            while(Time.realtimeSinceStartup<until)
            {motion.Tick(Time.deltaTime);yield return new WaitForEndOfFrame();report.maximumLeanFrameStep=Mathf.Max(report.maximumLeanFrameStep,Mathf.Abs(motion.AppliedSprintLeanDegrees-previous));previous=motion.AppliedSprintLeanDegrees;}
            report.fastCadence=motion.RunCadence;report.fastLean=motion.AppliedSprintLeanDegrees;
            Check(report.fastCadence>report.baseCadence+.3f&&report.fastCadence<=1.81f,"Cadence responds through the42m/s cap without exceeding1.8-times clip playback");
            Check(report.fastLean>report.baseLean+3f&&report.fastLean<=4.3f&&report.maximumLeanFrameStep<1.3f,"Sprint lean increases smoothly with speed through the upper body only");
            game.Motor.TryJump();game.Motor.Tick(1f/60);motion.Tick(1f/60);yield return new WaitForEndOfFrame();
            Check(!game.Motor.Grounded&&motion.AppliedSprintLeanDegrees==0&&motion.CurrentClip=="Jump","Extra sprint lean is absent from the jump pose and its clearance");
            for(int i=0;i<90&&!game.Motor.Grounded;i++){game.Motor.Tick(1f/120);motion.Tick(1f/120);}
            motion.Tick(1f/60);yield return new WaitForEndOfFrame();
            Check(game.Motor.Grounded&&motion.CurrentClip=="Run","Landing returns directly to the Run contact phase");
            yield return new WaitForSecondsRealtime(.15f);game.Pause();yield return new WaitForEndOfFrame();
            Quaternion paused=Bone("chest").rotation;yield return new WaitForSecondsRealtime(.15f);
            Check(Quaternion.Angle(paused,Bone("chest").rotation)<.01f,"Pause freezes the final animated pose without accumulating additive lean");
            game.Resume();game.ReturnToMenu();
            report.preferencesPreserved=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0)==oldCoins&&PlayerPrefs.GetInt("FenerRunner.BestScore",0)==oldScore&&PlayerPrefs.GetFloat("FenerRunner.Best",0)==oldBest;
            Check(report.preferencesPreserved,"Real wallet and best-score preferences remain unchanged");Finish();
        }
        Transform Bone(string name)=>bones.Single(b=>b.name==name);
        void MeasureGeometry()
        {
            const int count=96;var left=new List<int>();var right=new List<int>();
            int leftIndex=Array.FindIndex(bones,b=>b.name=="foot.L"),rightIndex=Array.FindIndex(bones,b=>b.name=="foot.R");
            BoneWeight[] weights=skin.sharedMesh.boneWeights;
            for(int i=0;i<weights.Length;i++)
            {
                var w=weights[i];int bone=w.boneIndex0;float value=w.weight0;
                if(w.weight1>value){bone=w.boneIndex1;value=w.weight1;}if(w.weight2>value){bone=w.boneIndex2;value=w.weight2;}if(w.weight3>value)bone=w.boneIndex3;
                if(bone==leftIndex)left.Add(i);if(bone==rightIndex)right.Add(i);
            }
            Check(left.Count>20&&right.Count>20,"Actual skinned sole vertices are available for both feet");
            var leftFloor=new float[count];var rightFloor=new float[count];var leftContact=new Vector3[count];var rightContact=new Vector3[count];
            var hips=new float[count];var shoulders=new float[count];report.minimumElbow=180;
            for(int i=0;i<count;i++)
            {
                run.SampleAnimation(motion.Animator.gameObject,run.length*i/count);skin.BakeMesh(baked);var vertices=baked.vertices;
                leftContact[i]=Contact(left,vertices,out leftFloor[i]);rightContact[i]=Contact(right,vertices,out rightFloor[i]);
                Vector3 hip=Bone("thigh.L").position-Bone("thigh.R").position,shoulder=Bone("upper_arm.L").position-Bone("upper_arm.R").position;
                hips[i]=Mathf.Atan2(hip.z,hip.x)*Mathf.Rad2Deg;shoulders[i]=Mathf.Atan2(shoulder.z,shoulder.x)*Mathf.Rad2Deg;
                if(i>0){hips[i]=hips[0]+Mathf.DeltaAngle(hips[0],hips[i]);shoulders[i]=shoulders[0]+Mathf.DeltaAngle(shoulders[0],shoulders[i]);}
                foreach(string side in new[]{"L","R"})
                {
                    Vector3 elbow=Bone("forearm."+side).position;
                    float angle=Vector3.Angle(Bone("upper_arm."+side).position-elbow,Bone("hand."+side).position-elbow);
                    report.minimumElbow=Mathf.Min(report.minimumElbow,angle);report.maximumElbow=Mathf.Max(report.maximumElbow,angle);
                }
            }
            report.lowestSole=Mathf.Min(leftFloor.Min(),rightFloor.Min());report.highestLowerSole=Enumerable.Range(0,count).Max(i=>Mathf.Min(leftFloor[i],rightFloor[i]));
            report.leftRecovery=leftFloor.Max()-leftFloor.Min();report.rightRecovery=rightFloor.Max()-rightFloor.Min();
            report.flightFraction=Enumerable.Range(0,count).Count(i=>leftFloor[i]>leftFloor.Min()+.022f&&rightFloor[i]>rightFloor.Min()+.022f)/(float)count;
            report.leftSupportResidual=FitSupport(leftContact,0,out report.leftSupportTravel);
            report.rightSupportResidual=FitSupport(rightContact,48,out report.rightSupportTravel);
            var supportHeights=Enumerable.Range(0,count).Where(i=>(i%48)/(float)count<.30f&&(i%48)/(float)count>.04f).Select(i=>i<48?leftFloor[i]:rightFloor[i]).ToArray();
            report.supportHeightRange=supportHeights.Max()-supportHeights.Min();
            report.hipYawRange=hips.Max()-hips.Min();report.shoulderYawRange=shoulders.Max()-shoulders.Min();report.counterrotationCorrelation=Correlation(hips,shoulders);
            Check(report.lowestSole>-.025f&&report.highestLowerSole<.18f&&report.supportHeightRange<.018f,"Support soles stay close to a consistent ground plane without sinking or hovering");
            Check(report.flightFraction>.10f&&report.flightFraction<.48f&&report.leftRecovery>.18f&&report.rightRecovery>.18f,"Both legs have a clear recovery and bounded flight phase");
            Check(report.leftSupportResidual<.015f&&report.rightSupportResidual<.015f&&report.leftSupportTravel>.25f&&report.rightSupportTravel>.25f,"Both support contacts follow steady backward local tracks within 1.5 cm");
            Check(report.minimumElbow>70&&report.maximumElbow<120,"Arms maintain athletic elbow bends rather than the previous near-straight wings");
            Check(report.hipYawRange>5&&report.shoulderYawRange>5&&report.counterrotationCorrelation<-.7f,"Hips and shoulders visibly counterrotate instead of turning as one rigid body");
            run.SampleAnimation(motion.Animator.gameObject,0);var positions=bones.Select(b=>b.localPosition).ToArray();var rotations=bones.Select(b=>b.localRotation).ToArray();
            run.SampleAnimation(motion.Animator.gameObject,run.length);
            for(int i=0;i<bones.Length;i++){report.loopPositionError=Mathf.Max(report.loopPositionError,Vector3.Distance(positions[i],bones[i].localPosition));report.loopRotationError=Mathf.Max(report.loopRotationError,Quaternion.Angle(rotations[i],bones[i].localRotation));}
            Check(report.loopPositionError<.001f&&report.loopRotationError<.1f,"All sampled bones close the sprint loop without a position or rotation jump");
        }
        Vector3 Contact(List<int> indices,Vector3[] vertices,out float floor)
        {
            floor=float.PositiveInfinity;foreach(int i in indices)floor=Mathf.Min(floor,skin.transform.TransformPoint(vertices[i]).y);
            Vector3 weighted=Vector3.zero;float total=0;
            foreach(int i in indices){Vector3 p=skin.transform.TransformPoint(vertices[i]);float w=Mathf.Exp(-(p.y-floor)/.012f);weighted+=p*w;total+=w;}
            return weighted/total;
        }
        static float FitSupport(Vector3[] contacts,int offset,out float travel)
        {
            // Independent least-squares fit to the observed support samples.
            var points=Enumerable.Range(3,28).Select(i=>contacts[(i+offset)%contacts.Length]).ToArray();
            float meanX=points.Average(p=>p.x),meanZ=points.Average(p=>p.z),meanT=(points.Length-1)*.5f;
            double numerator=0,denominator=0;
            for(int i=0;i<points.Length;i++){numerator+=(i-meanT)*(points[i].z-meanZ);denominator+=(i-meanT)*(i-meanT);}
            float slope=(float)(numerator/denominator),maximum=0;
            for(int i=0;i<points.Length;i++)maximum=Mathf.Max(maximum,new Vector2(points[i].x-meanX,points[i].z-meanZ-slope*(i-meanT)).magnitude);
            // Report the fitted full 34%-cycle support travel. The residual fit
            // excludes touchdown/toe-off boundaries but retains their time scale.
            travel=-slope*(.34f*contacts.Length);return maximum;
        }
        static float Correlation(float[] a,float[] b)
        {
            float ma=a.Average(),mb=b.Average();double ab=0,aa=0,bb=0;
            for(int i=0;i<a.Length;i++){double x=a[i]-ma,y=b[i]-mb;ab+=x*y;aa+=x*x;bb+=y*y;}return (float)(ab/Math.Sqrt(aa*bb));
        }
        void CheckUntouchedActionClips()
        {
            var imported=AssetDatabase.LoadAllAssetsAtPath("Assets/FenerRunner/Art/Kartal/Kartal_Runner_v01.fbx").OfType<AnimationClip>().ToArray();
            foreach(string name in new[]{"Jump","JumpFlip","Slide"})
            {
                var source=imported.Single(c=>!c.name.StartsWith("__preview__")&&c.name.Split('|').Last().Trim()==name);
                var generated=AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations+name+".anim");bool equal=Mathf.Abs(source.length-generated.length)<.0001f;
                foreach(var binding in AnimationUtility.GetCurveBindings(source))
                {
                    var original=AnimationUtility.GetEditorCurve(source,binding);var actual=AnimationUtility.GetEditorCurve(generated,binding);
                    if(actual==null){equal=false;continue;}
                    for(int i=0;i<=24;i++)if(Mathf.Abs(original.Evaluate(source.length*i/24)-actual.Evaluate(generated.length*i/24))>.00002f)equal=false;
                }
                Check(equal,name+" retains the supplied motion curves and clearance geometry");
            }
        }
        IEnumerator CaptureStrip(string name,Vector3 view)
        {
            const int width=384,height=640,count=6;var target=new RenderTexture(width,height,24);target.Create();
            var frame=new Texture2D(width,height,TextureFormat.RGB24,false);var strip=new Texture2D(width*count,height,TextureFormat.RGB24,false);
            viewCamera.targetTexture=target;viewCamera.fieldOfView=29;
            Vector3 centre=game.Motor.transform.position+Vector3.up*.95f;
            viewCamera.transform.position=centre+view*4.6f+Vector3.up*.22f;viewCamera.transform.LookAt(centre);
            for(int i=0;i<count;i++)
            {
                run.SampleAnimation(motion.Animator.gameObject,run.length*i/count);
                yield return new WaitForEndOfFrame();
                var old=RenderTexture.active;RenderTexture.active=target;frame.ReadPixels(new Rect(0,0,width,height),0,0);frame.Apply();RenderTexture.active=old;
                strip.SetPixels(i*width,0,width,height,frame.GetPixels());
            }
            strip.Apply();Directory.CreateDirectory("../Validation");File.WriteAllBytes(ReviewEvidence.FileFor(8,"../Validation/"+name+".png"),strip.EncodeToPNG());
            viewCamera.targetTexture=previousTarget;target.Release();Destroy(target);Destroy(frame);Destroy(strip);
            Check(File.Exists(ReviewEvidence.FileFor(8,"../Validation/"+name+".png")),name+" captures six actual-scene rig poses");
        }
        void Check(bool ok,string label){if(ok)checks.Add(label);else errors.Add(label);}
        void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text);}
        void Finish()
        {
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.checks=checks.ToArray();report.errors=errors.ToArray();
            Directory.CreateDirectory("../Validation");File.WriteAllText(ReviewEvidence.FileFor(8,"../Validation/unity_run_motion_v8.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Run motion v8 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;Application.runInBackground=background;
            if(ready)
            {
                if(viewCamera!=null){viewCamera.targetTexture=previousTarget;viewCamera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);viewCamera.fieldOfView=cameraFov;}
                if(follow!=null)follow.enabled=oldFollow;if(feedback!=null)feedback.enabled=oldFeedback;if(motion!=null)motion.Animator.enabled=oldAnimator;
            }
            if(baked!=null)Destroy(baked);if(testWallet!=null)testWallet.DeleteIsolatedTestData();
        }
    }
}
#endif
