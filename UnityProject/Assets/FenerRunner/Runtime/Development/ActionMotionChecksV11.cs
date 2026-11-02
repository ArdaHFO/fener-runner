#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class ActionMotionChecksV11:MonoBehaviour
    {
        [Serializable] sealed class DeathCase
        {
            public string name;public bool actualContact,rootFrozen,capsuleUnchanged,completed;
            public float initialRootHeight,minimumMeshY,finalMeshY,finalMeshHeight,headTravel,activeSeconds;
        }
        [Serializable] sealed class StyleCase
        {
            public string name;public float maximumWeight,maximumWeightStep,handDifference,headDifference,lowerPositionError,lowerAngleError;
        }
        [Serializable] sealed class Report
        {
            public bool passed,preferencesPreserved;public string utc,unityVersion;public string[] checks,errors;
            public DeathCase[] deaths;public StyleCase[] styles;public float naturalVariantDelay,observedVariantDelay,entryContactPhase;
            public string scope="Actual Unity fatal contact from ground, low jump, slide and lane change; an explicitly forced Finish presentation at flight height; unscaled death descent and cancellation; actual grounded Run variants against a matching base-clip phase. Physics/capsule/root remain stopped during death. No claim that protected flight normally collides or that the arcade travel speed matches human stride speed.";
        }
        const string Flag="FenerRunner.ActionMotionV11";
        readonly List<string> checks=new List<string>(),errors=new List<string>();readonly List<DeathCase> deaths=new List<DeathCase>();readonly List<StyleCase> styles=new List<StyleCase>();
        RunnerGame game;RunnerAnimation motion;SkinnedMeshRenderer skin;Transform[] bones;Mesh baked;RunWallet wallet;
        GameObject reference;Transform[] referenceBones;AnimationClip run;Report report=new Report();
        bool background,ready;float oldScale;int oldWallet,oldScore;float oldBest;
        Camera viewCamera;RenderTexture oldTarget,target;Texture2D frame,deathSheet,styleSheet;
        const int Width=256,Height=384;

        [MenuItem("Fener Runner/63 - Verify death and varied run presentation")]
        public static void StartChecks(){if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before action motion checks.");SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Action motion v11 checks").AddComponent<ActionMotionChecksV11>();}}
        IEnumerator Start()
        {
            background=Application.runInBackground;oldScale=Time.timeScale;Application.runInBackground=true;Application.logMessageReceived+=Log;
            oldWallet=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0);oldScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);oldBest=PlayerPrefs.GetFloat("FenerRunner.Best",0);
            game=FindFirstObjectByType<RunnerGame>();if(game==null){Check(false,"RunnerGame exists");Finish();yield break;}
            game.AutomatedTest=true;wallet=game.UseWalletForTest("FenerRunner.Test.ActionV11."+DateTime.UtcNow.Ticks+".");
            motion=game.Animation;skin=motion.Animator.GetComponentInChildren<SkinnedMeshRenderer>();bones=skin.bones;baked=new Mesh();ready=true;
            run=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/FenerRunner/Generated/Animations/Run.anim");
            viewCamera=Camera.main;oldTarget=viewCamera.targetTexture;target=new RenderTexture(Width,Height,24);target.Create();
            frame=new Texture2D(Width,Height,TextureFormat.RGB24,false);deathSheet=new Texture2D(Width*4,Height*5,TextureFormat.RGB24,false);styleSheet=new Texture2D(Width*3,Height,TextureFormat.RGB24,false);
            viewCamera.targetTexture=target;
            for(int i=0;i<5;i++)yield return CheckDeath(i);
            deathSheet.Apply();File.WriteAllBytes(ReviewEvidence.FileFor(11,"../Validation/v11-death-poses.png"),deathSheet.EncodeToPNG());
            yield return CheckCancellation();
            ResetRun();reference=Instantiate(motion.Animator.gameObject,motion.Animator.transform.parent);reference.name="Invisible base Run reference";
            reference.GetComponent<Animator>().enabled=false;foreach(var renderer in reference.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            referenceBones=reference.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            report.naturalVariantDelay=motion.RunStyleCountdown;Check(report.naturalVariantDelay>=8&&report.naturalVariantDelay<=14,"A fresh run schedules its natural style change in 8â€“14 active seconds");
            float started=Time.time,deadline=Time.realtimeSinceStartup+17;
            while(motion.CurrentRunStyle==RunStyle.None&&Time.realtimeSinceStartup<deadline)yield return null;
            report.observedVariantDelay=Time.time-started;report.entryContactPhase=Mathf.Repeat(motion.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime,.5f);
            Check(motion.CurrentRunStyle==RunStyle.CompactDrive&&report.observedVariantDelay>=report.naturalVariantDelay-.15f&&report.observedVariantDelay<=report.naturalVariantDelay+1,
                "The first natural style actually enters after its active-run interval");
            Check(report.entryContactPhase<.22f,"A new style enters near a grounded Run contact phase");
            yield return CheckStyle(0);
            for(int i=1;i<3;i++){ResetRun();motion.QueueRunStyleForTest(i);yield return CheckStyle(i);}
            styleSheet.Apply();File.WriteAllBytes(ReviewEvidence.FileFor(11,"../Validation/v11-run-styles.png"),styleSheet.EncodeToPNG());
            ResetRun();motion.QueueRunStyleForTest(2);deadline=Time.realtimeSinceStartup+2;
            while(motion.RunStyleWeight<.7f&&Time.realtimeSinceStartup<deadline)yield return null;
            game.Pause();yield return new WaitForEndOfFrame();float weight=motion.RunStyleWeight,clock=motion.RunStyleCountdown;Quaternion pose=Bone("head").rotation;
            yield return new WaitForSecondsRealtime(.2f);
            Check(Mathf.Abs(weight-motion.RunStyleWeight)<.001f&&Mathf.Abs(clock-motion.RunStyleCountdown)<.001f&&Quaternion.Angle(pose,Bone("head").rotation)<.01f,"Pause freezes variant blend, schedule and actual bone pose");
            game.Resume();game.Motor.TryJump();yield return new WaitForEndOfFrame();
            Check(motion.CurrentRunStyle==RunStyle.None&&motion.RunStyleWeight==0&&motion.CurrentClip=="Jump","Jump immediately restores the original airborne pose without a style overlay");
            game.ReturnToMenu();
            report.preferencesPreserved=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0)==oldWallet&&PlayerPrefs.GetInt("FenerRunner.BestScore",0)==oldScore&&PlayerPrefs.GetFloat("FenerRunner.Best",0)==oldBest;
            Check(report.preferencesPreserved,"Real wallet and best records are unchanged");Finish();
        }
        void ResetRun()
        {game.enabled=true;game.Restart();game.Obstacles.SetActive(false);game.Tokens.Enabled=false;game.Powerups.Enabled=false;motion.TurnAllowed=false;}
        IEnumerator CheckDeath(int index)
        {
            ResetRun();game.enabled=false;
            string[] names={"Ground contact","Low jump contact","Slide contact","Lane-change contact","Forced flight-height presentation"};
            if(index==1){game.Motor.TryJump();game.Motor.Tick(.11f);motion.Tick(.11f);motion.Animator.Update(.11f);}
            if(index==2){game.Motor.TrySlide();game.Motor.Tick(.18f);motion.Tick(.18f);motion.Animator.Update(.18f);}
            if(index==3){game.Motor.ChangeLane(1);game.Motor.Tick(.025f);motion.Tick(.025f);motion.Animator.Update(.025f);}
            if(index==4)
            {
                game.Motor.Speed=0;game.Motor.SetFlight(true);
                for(int i=0;i<100;i++){game.Motor.Tick(1f/60);motion.Tick(1f/60);motion.Animator.Update(1f/60);}
            }
            int count=motion.DeathCount;RunnerObstacle obstacle=game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+.6f,0);
            if(index==4)typeof(RunnerGame).GetMethod("Finish",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{obstacle});
            else game.enabled=true;
            float deadline=Time.realtimeSinceStartup+1;
            while(game.State!=RunState.Finished&&Time.realtimeSinceStartup<deadline)yield return null;
            var result=new DeathCase{name=names[index],actualContact=index!=4,initialRootHeight=game.Motor.transform.position.y,minimumMeshY=float.PositiveInfinity};deaths.Add(result);
            Check(game.State==RunState.Finished&&motion.DeathPlaying&&motion.DeathCount==count+1,names[index]+" starts exactly one visible death presentation");
            Check(Time.timeScale==0&&!game.Interface.resultPanel.activeSelf,names[index]+" stops gameplay before showing the delayed result panel");
            Check(!motion.BeginDeath(ObstacleKind.SlideUnder)&&!motion.CelebrateReward()&&motion.DeathCount==count+1,names[index]+" rejects duplicate death and stale rewards");
            Vector3 root=game.Motor.transform.position;float capsuleHeight=game.Motor.Capsule.height,capsuleRadius=game.Motor.Capsule.radius;Vector3 capsuleCentre=game.Motor.Capsule.center,firstHead=Bone("head").position;
            result.rootFrozen=result.capsuleUnchanged=true;float begin=Time.realtimeSinceStartup;int capture=0;float[] milestones={.10f,.35f,.65f,1f};
            deadline=Time.realtimeSinceStartup+1.8f;
            while(!motion.DeathComplete&&Time.realtimeSinceStartup<deadline)
            {
                yield return new WaitForEndOfFrame();result.rootFrozen&=game.Motor.transform.position==root;
                result.capsuleUnchanged&=game.Motor.Capsule.height==capsuleHeight&&game.Motor.Capsule.radius==capsuleRadius&&game.Motor.Capsule.center==capsuleCentre;
                result.headTravel=Mathf.Max(result.headTravel,Vector3.Distance(firstHead,Bone("head").position));
                if(capture<4&&motion.DeathProgress>=milestones[capture])
                {Bounds bounds=MeshBounds();result.minimumMeshY=Mathf.Min(result.minimumMeshY,bounds.min.y);CopyFrame(deathSheet,capture++,4-index);}
            }
            yield return new WaitForEndOfFrame();while(capture<4)CopyFrame(deathSheet,capture++,4-index);
            Bounds final=MeshBounds();result.finalMeshY=final.min.y;result.finalMeshHeight=final.size.y;result.minimumMeshY=Mathf.Min(result.minimumMeshY,final.min.y);result.activeSeconds=Time.realtimeSinceStartup-begin;result.completed=motion.DeathComplete;
            Check(result.completed&&result.activeSeconds>=.85f&&result.activeSeconds<1.4f,names[index]+" completes its unscaled stumble in a bounded second");
            Check(result.rootFrozen&&result.capsuleUnchanged,names[index]+" keeps the stopped physics root and collider unchanged");
            Check(result.headTravel>.35f&&result.finalMeshHeight<1.65f,names[index]+" visibly falls into a distinct low resting pose");
            Check(result.minimumMeshY>-.055f&&result.finalMeshY>-.055f&&result.finalMeshY<.13f,names[index]+" finishes on the ground without sinking or remaining in the air");
            Check(motion.CurrentClip=="CollisionFall"&&motion.Animator.speed==0,names[index]+" holds its completed pose without looping");
        }
        IEnumerator CheckCancellation()
        {
            ResetRun();game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+.6f,0);float deadline=Time.realtimeSinceStartup+1;
            while(!motion.DeathPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return new WaitForSecondsRealtime(.2f);motion.SetPaused(true);yield return new WaitForEndOfFrame();
            float progress=motion.DeathProgress;Quaternion pose=Bone("head").rotation;yield return new WaitForSecondsRealtime(.2f);
            Check(Mathf.Abs(motion.DeathProgress-progress)<.001f&&Quaternion.Angle(pose,Bone("head").rotation)<.01f,"An explicit presentation pause freezes death time and pose");
            motion.SetPaused(false);yield return new WaitForSecondsRealtime(.1f);Check(motion.DeathProgress>progress,"Death resumes from its paused pose");
            ResetRun();Vector3 restored=motion.Animator.transform.localPosition;yield return new WaitForSecondsRealtime(1.2f);
            Check(game.State==RunState.Running&&!motion.DeathPlaying&&!motion.DeathComplete&&motion.CurrentClip=="Run"&&!game.Interface.resultPanel.activeSelf&&motion.Animator.transform.localPosition==restored,
                "Retry cancels the fall and any delayed result while restoring the model transform");
            game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+.6f,game.Motor.Lane);deadline=Time.realtimeSinceStartup+1;
            while(!motion.DeathPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            game.ReturnToMenu();yield return new WaitForSecondsRealtime(1.2f);
            Check(game.State==RunState.Menu&&!motion.DeathComplete&&!game.Interface.resultPanel.activeSelf&&motion.Animator.updateMode==AnimatorUpdateMode.UnscaledTime,
                "Returning to the menu cancels the old result and restores live menu gestures");
        }
        IEnumerator CheckStyle(int index)
        {
            var result=new StyleCase{name=((RunStyle)(index+1)).ToString()};styles.Add(result);
            float deadline=Time.realtimeSinceStartup+3.5f,previous=motion.RunStyleWeight;bool captured=false,started=motion.CurrentRunStyle!=RunStyle.None,baseClip=true;
            string[] lower={"root","pelvis","thigh.L","shin.L","foot.L","thigh.R","shin.R","foot.R"};
            while(Time.realtimeSinceStartup<deadline)
            {
                yield return new WaitForEndOfFrame();started|=motion.CurrentRunStyle!=RunStyle.None;
                result.maximumWeight=Mathf.Max(result.maximumWeight,motion.RunStyleWeight);result.maximumWeightStep=Mathf.Max(result.maximumWeightStep,Mathf.Abs(previous-motion.RunStyleWeight));previous=motion.RunStyleWeight;
                var state=motion.Animator.GetCurrentAnimatorStateInfo(0);run.SampleAnimation(reference,Mathf.Repeat(state.normalizedTime,1)*run.length);
                baseClip&=motion.CurrentClip=="Run"&&Mathf.Abs(motion.Animator.GetFloat("MotionRate")-motion.RunCadence)<.0001f;
                foreach(string name in lower)
                {
                    Transform a=Bone(name),b=referenceBones.Single(v=>v.name==name);result.lowerPositionError=Mathf.Max(result.lowerPositionError,Vector3.Distance(a.localPosition,b.localPosition));
                    result.lowerAngleError=Mathf.Max(result.lowerAngleError,Quaternion.Angle(a.localRotation,b.localRotation));
                }
                result.handDifference=Mathf.Max(result.handDifference,Vector3.Distance(Bone("hand.R").position,referenceBones.Single(b=>b.name=="hand.R").position));
                result.headDifference=Mathf.Max(result.headDifference,Quaternion.Angle(Bone("head").rotation,referenceBones.Single(b=>b.name=="head").rotation));
                if(!captured&&motion.RunStyleWeight>.9f){CopyFrame(styleSheet,index,0);captured=true;}
                if(started&&motion.CurrentRunStyle==RunStyle.None&&motion.RunStyleWeight==0)break;
            }
            Check(started&&captured&&result.maximumWeight>.9f&&motion.CurrentRunStyle==RunStyle.None&&motion.RunStyleWeight==0,result.name+" smoothly enters and fully returns to the base stride");
            Check(result.maximumWeightStep<.4f&&baseClip,result.name+" preserves base Run playback and a bounded blend without a new jump");
            Check(result.lowerPositionError<.002f&&result.lowerAngleError<.15f,result.name+" leaves every lower-body bone on the same authored contact phase");
            Check(index==2?result.headDifference>12:result.handDifference>.08f,result.name+" produces a distinct visible upper-body movement");
        }
        Transform Bone(string name)=>bones.Single(b=>b.name==name);
        Bounds MeshBounds()
        {skin.BakeMesh(baked);var vertices=baked.vertices;var bounds=new Bounds(skin.transform.TransformPoint(vertices[0]),Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(skin.transform.TransformPoint(v));return bounds;}
        void CopyFrame(Texture2D sheet,int column,int row)
        {var old=RenderTexture.active;RenderTexture.active=target;frame.ReadPixels(new Rect(0,0,Width,Height),0,0);frame.Apply();RenderTexture.active=old;sheet.SetPixels(column*Width,row*Height,Width,Height,frame.GetPixels());}
        void Check(bool ok,string label){if(ok)checks.Add(label);else errors.Add(label);}
        void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text);}
        void Finish()
        {
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.checks=checks.ToArray();report.errors=errors.ToArray();report.deaths=deaths.ToArray();report.styles=styles.ToArray();
            Directory.CreateDirectory("../Validation");File.WriteAllText(ReviewEvidence.FileFor(11,"../Validation/unity_action_motion_v11.json"),JsonUtility.ToJson(report,true));Application.logMessageReceived-=Log;
            Debug.Log("Action motion v11 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;Application.runInBackground=background;Time.timeScale=oldScale;
            if(viewCamera!=null)viewCamera.targetTexture=oldTarget;if(target!=null){target.Release();Destroy(target);}if(frame!=null)Destroy(frame);if(deathSheet!=null)Destroy(deathSheet);if(styleSheet!=null)Destroy(styleSheet);
            if(baked!=null)Destroy(baked);if(reference!=null)Destroy(reference);if(ready&&wallet!=null)wallet.DeleteIsolatedTestData();
        }
    }
}
#endif
