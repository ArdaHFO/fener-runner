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
    public sealed class JumpPresentationChecksV13:MonoBehaviour
    {
        [Serializable] sealed class Trajectory
        {public string action;public float speed,predictedX,predictedZ,actualX,actualZ,errorX,errorZ,airSeconds;public bool markerSeen,landed;}
        [Serializable] sealed class Report
        {
            public bool passed,preferencesPreserved;public string utc,unityVersion;public string[] checks,errors;
            public Trajectory[] trajectories;public float maximumSlideMeshHeight,slideHeaderHeight,maximumSlidePhaseStep,lowerBonePositionError,lowerBoneRotationError;
            public float maximumJumpBalance,maximumLandingRecovery;public string scope="Native Editor motor trajectories and actual Animator/mesh poses at18 and42m/s. Includes normal/super/AirStep/fast-fall/late-lane landings, continued low slides, pause/menu/death visibility and real scene screenshots. Projection assumes current forward speed and a flat course; it does not guarantee an obstacle-free landing. Not device performance or touchscreen validation.";
        }
        const string Flag="FenerRunner.JumpPresentationV13";
        const float Step=1f/60;
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        readonly List<Trajectory> paths=new List<Trajectory>();readonly Report report=new Report();
        RunnerGame game;RunnerMotor motor;RunnerAnimation motion;FollowCamera follow;RunWallet wallet;
        SkinnedMeshRenderer skin;Mesh baked;GameObject reference;Transform[] lower,referenceLower;AnimationClip jump;
        bool oldBackground;int oldRate,oldCoins,oldScore;float oldScale,oldBest;
        [MenuItem("Fener Runner/88 - Verify jump and slide presentation")]
        public static void StartChecks()
        {if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before jump presentation checks.");SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("V13 jump presentation checks").AddComponent<JumpPresentationChecksV13>();}}
        IEnumerator Start()
        {
            oldBackground=Application.runInBackground;oldRate=Time.captureFramerate;oldScale=Time.timeScale;
            Application.runInBackground=true;Time.captureFramerate=60;Application.logMessageReceived+=Log;
            oldCoins=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0);oldScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);oldBest=PlayerPrefs.GetFloat("FenerRunner.Best",0);
            game=FindFirstObjectByType<RunnerGame>();if(game==null){Check(false,"Scene has RunnerGame");Finish();yield break;}
            game.AutomatedTest=true;wallet=game.UseWalletForTest("FenerRunner.Test.JumpPresentationV13."+DateTime.UtcNow.Ticks+".");
            motor=game.Motor;motion=game.Animation;follow=Camera.main.GetComponent<FollowCamera>();Reset(18);
            skin=motion.Animator.GetComponentInChildren<SkinnedMeshRenderer>();baked=new Mesh();
            reference=Instantiate(motion.Animator.gameObject,motion.Animator.transform.parent);reference.name="Invisible matching jump reference";
            reference.GetComponent<Animator>().enabled=false;foreach(var r in reference.GetComponentsInChildren<Renderer>())r.enabled=false;
            string[] names={"root","pelvis","thigh.L","shin.L","foot.L","thigh.R","shin.R","foot.R"};
            lower=names.Select(n=>skin.bones.Single(b=>b.name==n)).ToArray();
            var referenceBones=reference.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            referenceLower=names.Select(n=>referenceBones.Single(b=>b.name==n)).ToArray();
            jump=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/FenerRunner/Generated/Animations/Jump.anim");
            foreach(float speed in new[]{18f,RunTempo.HardCap})foreach(string action in new[]{"normal","super","airStep","fastFall","lateLane"})yield return CheckTrajectory(speed,action);
            Check(report.lowerBonePositionError<.002f&&report.lowerBoneRotationError<.15f,"Normal-jump balance preserves supplied pelvis, leg and foot curves at matching Animator phase");
            Check(report.maximumJumpBalance>.7f,"Normal-jump wings visibly balance through the airborne arc");
            Check(report.maximumLandingRecovery>.5f,"Actual ground contact produces bounded upper-body landing recovery");
            yield return CheckSlides();yield return CheckLifecycle();
            game.ReturnToMenu();report.preferencesPreserved=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0)==oldCoins&&PlayerPrefs.GetInt("FenerRunner.BestScore",0)==oldScore&&PlayerPrefs.GetFloat("FenerRunner.Best",0)==oldBest;
            Check(report.preferencesPreserved,"Production wallet and personal records remain unchanged");Finish();
        }
        void Reset(float speed)
        {
            game.Restart();game.enabled=false;motor.Speed=speed;motion.TurnAllowed=false;game.Obstacles.SetActive(false);game.Tokens.Enabled=false;
            foreach(var renderer in game.Tokens.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            game.Powerups.Enabled=false;game.Powerups.IsolatePickupsForTest();
        }
        void Move(float dt=Step)
        {
            motor.Tick(dt);game.Course.Tick(motor.transform.position.z);
            Vector3 root=motor.transform.position;float height=motor.Capsule.height;motion.Tick(dt);
            if(motor.transform.position!=root||motor.Capsule.height!=height)Check(false,"Animation never changes collision root or capsule");
        }
        IEnumerator CheckTrajectory(float speed,string action)
        {
            Reset(speed);if(action=="super")motor.JumpHeightMultiplier=1.5f;if(action=="airStep")motor.AirStepEnabled=true;
            var sample=new Trajectory{speed=speed,action=action};paths.Add(sample);
            Check(motor.TryJump(),speed+" "+action+" starts an actual jump");bool acted=false,sampled=false,captured=false;
            for(int frame=0;frame<150&&!motor.Grounded;frame++)
            {
                float dt=Step;
                if(follow.RemainingAirSeconds>0&&follow.RemainingAirSeconds<Step)dt=Mathf.Clamp(follow.RemainingAirSeconds+.0001f,.001f,Step);
                Move(dt);yield return new WaitForEndOfFrame();
                sample.markerSeen|=follow.LandingGuideVisible;
                report.maximumJumpBalance=Mathf.Max(report.maximumJumpBalance,motion.JumpBalanceWeight);
                if(action=="airStep"&&frame==10){acted=motor.TryJump();Check(acted,"AirStep impulse accepted before projection sample");continue;}
                if(action=="fastFall"&&frame==12){acted=motor.TrySlide();Check(acted,"Fast-fall command accepted before projection sample");continue;}
                if(action=="lateLane"&&!acted&&motor.VerticalVelocity<0&&follow.RemainingAirSeconds<.11f&&!motor.Grounded){motor.ChangeLane(1);acted=true;continue;}
                bool ready=(action=="lateLane"?acted:action=="fastFall"?frame>12:frame>=18)&&!motor.Grounded;
                if(ready&&!sampled){sampled=true;sample.predictedX=follow.PredictedLandingPosition.x;sample.predictedZ=follow.PredictedLandingPosition.z;sample.airSeconds=follow.RemainingAirSeconds;}
                if(action=="normal"&&!motion.Animator.IsInTransition(0)&&motion.Animator.GetCurrentAnimatorStateInfo(0).IsName("Jump"))
                {
                    var state=motion.Animator.GetCurrentAnimatorStateInfo(0);jump.SampleAnimation(reference,Mathf.Clamp01(state.normalizedTime)*jump.length);
                    for(int i=0;i<lower.Length;i++){report.lowerBonePositionError=Mathf.Max(report.lowerBonePositionError,Vector3.Distance(lower[i].localPosition,referenceLower[i].localPosition));report.lowerBoneRotationError=Mathf.Max(report.lowerBoneRotationError,Quaternion.Angle(lower[i].localRotation,referenceLower[i].localRotation));}
                }
                if(!captured&&speed==18&&action=="normal"&&frame>=19)
                {ScreenCapture.CaptureScreenshot(Evidence("v13-jump-guide.png"));captured=true;}
            }
            sample.landed=motor.Grounded;sample.actualX=motor.transform.position.x;sample.actualZ=motor.transform.position.z;
            sample.errorX=Mathf.Abs(sample.actualX-sample.predictedX);sample.errorZ=Mathf.Abs(sample.actualZ-sample.predictedZ);
            Check(sampled&&sample.landed&&sample.markerSeen,speed+" "+action+" projects a visible airborne guide and reaches real ground");
            Check(sample.errorZ<=speed*Step+.08f&&sample.errorX<.22f,speed+" "+action+" landing matches prediction within one motor step and contact skin tolerance");
            Check(!follow.LandingGuideVisible,speed+" "+action+" hides the guide on contact");
            for(int i=0;i<19;i++){Move();yield return new WaitForEndOfFrame();report.maximumLandingRecovery=Mathf.Max(report.maximumLandingRecovery,motion.LandingReactionWeight);}
            Check(motion.LandingReactionWeight<.001f,speed+" "+action+" landing recovery closes without a persistent pose offset");
        }
        IEnumerator CheckSlides()
        {
            Reset(18);var gate=game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder,motor.transform.position.z+200);
            report.slideHeaderHeight=gate.CollisionBoxes.Single(c=>c.name=="TorsoHeader").bounds.min.y;
            Check(motor.TrySlide(),"Grounded slide is accepted");int count=motor.SlideCount;float previous=0;
            for(int i=0;i<180;i++)
            {
                if(i==31||i==62){Check(motor.TrySlide(),"Repeated downward swipe refreshes an active slide");count++;}
                Move();yield return new WaitForEndOfFrame();
                if(i>=7&&motor.Sliding)
                {
                    skin.BakeMesh(baked);foreach(var p in baked.vertices)report.maximumSlideMeshHeight=Mathf.Max(report.maximumSlideMeshHeight,skin.transform.TransformPoint(p).y);
                    if(previous>0)report.maximumSlidePhaseStep=Mathf.Max(report.maximumSlidePhaseStep,Mathf.Abs(motion.SlidePosePhase-previous));previous=motion.SlidePosePhase;
                    if(motion.SlidePosePhase<.319f||motion.SlidePosePhase>.501f)Check(false,"Slide stays within imported low-pose plateau");
                }
                if(i==74)ScreenCapture.CaptureScreenshot(Evidence("v13-chained-slide.png"));
            }
            Check(motor.SlideCount==count,"Each accepted chain creates one slide request with no duplicate count");
            Check(report.maximumSlideMeshHeight<report.slideHeaderHeight,"Actual baked slide mesh remains below the header for the entire held and refreshed low pose");
            Check(report.maximumSlidePhaseStep<.02f,"Chained slide pose advances smoothly without snapping upright");
            Check(!motor.Sliding&&motion.CurrentClip=="Run","Final expiry returns the motor and animation to running");
            Check(File.Exists(Evidence("v13-jump-guide.png"))&&File.Exists(Evidence("v13-chained-slide.png")),"Native jump-guide and refreshed-slide screenshots were written");
        }
        IEnumerator CheckLifecycle()
        {
            Reset(18);motor.TryJump();for(int i=0;i<12;i++){Move();yield return new WaitForEndOfFrame();}
            Check(follow.LandingGuideVisible,"Guide is visible before pausing an airborne run");
            game.Pause();yield return new WaitForEndOfFrame();Vector3 cameraPosition=follow.transform.position;float phase=motion.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Check(!follow.LandingGuideVisible,"Pause hides the landing guide");yield return new WaitForSecondsRealtime(.12f);
            Check(follow.transform.position==cameraPosition&&Mathf.Abs(motion.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime-phase)<.001f,"Pause freezes camera and airborne Animator phase");
            game.Resume();Move();yield return new WaitForEndOfFrame();Check(follow.LandingGuideVisible,"Resume restores the projected guide from current physics");
            motor.SetFlight(true);Move();yield return new WaitForEndOfFrame();Check(!follow.LandingGuideVisible,"Sustained flight has no misleading normal-jump landing marker");
            Reset(18);motor.TryJump();for(int i=0;i<10;i++){Move();yield return new WaitForEndOfFrame();}
            motion.BeginDeath(ObstacleKind.JumpOver);follow.BeginDeathPresentation(motion);Time.timeScale=0;yield return new WaitForEndOfFrame();
            Check(!follow.LandingGuideVisible,"Collision presentation hides the landing marker immediately");
            Reset(18);yield return new WaitForEndOfFrame();Check(!follow.LandingGuideVisible&&motion.CurrentClip=="Run"&&motion.LandingReactionWeight==0,"Retry clears prediction and residual landing recovery");
            game.ReturnToMenu();yield return new WaitForEndOfFrame();Check(!follow.LandingGuideVisible,"Menu keeps the landing guide hidden");
            Check(FindObjectsByType<LineRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(r=>r.name=="Projected jump landing")==1,"All runs reuse exactly one cached landing guide");
        }
        static string Evidence(string file){string path=ReviewEvidence.FileFor(13,"../Validation/"+file);Directory.CreateDirectory(Path.GetDirectoryName(path));return path;}
        void Check(bool condition,string message){if(condition)checks.Add(message);else errors.Add(message);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        void Finish()
        {
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.checks=checks.ToArray();report.errors=errors.ToArray();report.trajectories=paths.ToArray();
            File.WriteAllText(Evidence("unity_jump_presentation_v13.json"),JsonUtility.ToJson(report,true));Application.logMessageReceived-=Log;
            Debug.Log("Jump presentation v13 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;Application.runInBackground=oldBackground;Time.captureFramerate=oldRate;Time.timeScale=oldScale;
            if(reference!=null)Destroy(reference);if(baked!=null)Destroy(baked);if(wallet!=null)wallet.DeleteIsolatedTestData();
        }
    }
}
#endif
