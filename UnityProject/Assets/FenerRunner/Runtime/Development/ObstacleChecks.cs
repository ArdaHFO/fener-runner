#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace FenerRunner
{
    public sealed class ObstacleChecks:MonoBehaviour
    {
        [Serializable] class Report
        {
            public bool passed;public string utc,unityVersion;
            public string[] checks,errors; public float runSeconds,normalClearance,flipClearance,slideClearance;
            public int initialObjects,finalObjects,obstaclePool,stadiumPool,seed,scriptedJumps,scriptedSlides;
            public float maximumRunCadence,runLoopQuaternionError,runLowestMeshY,runHighestSoleY;
            public string scope="Real Unity Play Mode, physical capsule queries, actual skinned-mesh clearance samples, deterministic route-following controls and fast-fall input. Not a device or physical touchscreen test.";
        }
        bool captureOnly;
        [MenuItem("Fener Runner/8 - Capture obstacle review")]
        public static void CaptureOnly(){SessionState.SetBool("FenerRunner.CaptureOnly",true);StartChecks();}
        RunnerGame game;Report report=new Report();readonly List<string> checks=new List<string>(),errors=new List<string>();
        bool previousBackground;float previousBest;int previousBestScore;Mesh baked;
        [MenuItem("Fener Runner/7 - Verify obstacle gameplay")]
        public static void StartChecks()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
            SessionState.SetBool("FenerRunner.ObstacleChecks",true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("FenerRunner.ObstacleChecks",false))return;
            SessionState.SetBool("FenerRunner.ObstacleChecks",false);new GameObject("Obstacle integration checks").AddComponent<ObstacleChecks>();
        }
        IEnumerator Start()
        {
            captureOnly=SessionState.GetBool("FenerRunner.CaptureOnly",false);SessionState.SetBool("FenerRunner.CaptureOnly",false);
            game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;game.Tokens.Enabled=false;game.Powerups.Enabled=false;
            previousBackground=Application.runInBackground;Application.runInBackground=true;
            previousBest=PlayerPrefs.GetFloat("FenerRunner.Best",0);previousBestScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);baked=new Mesh();
            Application.logMessageReceived+=Log;
            yield return null;
            Directory.CreateDirectory("../Validation");
            yield return Capture("v7-menu");
            Check(game.State==RunState.Menu,"Menu and updated character models loaded");
            VerifyRunClip();
            game.Play();game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+3);
            yield return Wait(.6f);
            Check(game.State==RunState.Finished,"Running into black bird ends the run");
            double distance=game.Distance;yield return Wait(.2f);
            Check(game.Distance==distance,"Collision freezes distance");
            yield return Capture("v7-result");
            game.Interface.retry.onClick.Invoke();
            Check(game.State==RunState.Running&&game.Distance==0,"Retry button resets run");
            game.Play();game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder,game.Motor.transform.position.z+3);
            yield return Wait(.6f);Check(game.State==RunState.Finished,"Standing under curly bird hits header");
            var sound=game.GetComponent<StadiumSound>();
            Check(sound.HasUserVoices&&sound.LastVoiceIndex==0&&sound.VoiceSource.clip.name=="ses1"&&sound.VoiceSource.isPlaying,"Fatal curly collision plays the supplied ses1 while gameplay time is frozen");
            yield return Jump(false);yield return Jump(true);
            game.Play();var slide=game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder,game.Motor.transform.position.z+game.Settings.forwardSpeed*.42f);
            game.HandleCommand(RunnerCommand.Slide);
            float begin=Time.realtimeSinceStartup;float clearance=100;bool slideCaptured=false;
            while(Time.realtimeSinceStartup-begin<1.12f)
            {
                yield return new WaitForEndOfFrame();Bounds b=PlayerBounds();Bounds obstacle=slide.GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="TorsoHeader").bounds;
                if(b.max.z>obstacle.min.z&&b.min.z<obstacle.max.z)clearance=Mathf.Min(clearance,obstacle.min.y-b.max.y);
                if(!slideCaptured&&Time.realtimeSinceStartup-begin>.45f){ScreenCapture.CaptureScreenshot(ReviewEvidence.File("../Validation/v7-slide.png"));slideCaptured=true;}
            }
            report.slideClearance=clearance;
            Check(game.State==RunState.Running,"Timed slide passes open corridor");
            Check(clearance>0&&clearance<100,"Actual Slide mesh stays below header throughout crossing");
            game.Play();game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder,game.Motor.transform.position.z+game.Settings.forwardSpeed*1.35f);
            game.HandleCommand(RunnerCommand.Slide);yield return Wait(1.6f);
            Check(game.State==RunState.Finished,"Early slide rises before gate and collides");
            game.Play();game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+6);
            game.HandleCommand(RunnerCommand.Left);yield return Wait(.9f);
            Check(game.State==RunState.Running,"Adjacent lane safely avoids obstacle");
            game.Pause();float stadiumTime=Shader.GetGlobalFloat("_StadiumTime");distance=game.Distance;
            yield return Wait(.3f);Check(game.State==RunState.Paused&&game.Distance==distance&&Shader.GetGlobalFloat("_StadiumTime")==stadiumTime,"Pause freezes gameplay and supporter motion");game.Resume();
            Check(game.State==RunState.Running,"Resume restores run");
            yield return VerifyFastFall();
            game.Obstacles.SetActive(true);game.Obstacles.UseSeedForTest(1907);game.Play();yield return null;
            report.initialObjects=Count();report.obstaclePool=game.Obstacles.PoolCount;report.stadiumPool=game.Course.ActiveChunkCount;
            report.seed=game.Obstacles.Seed;
            begin=Time.realtimeSinceStartup;float oldZ=game.Motor.transform.position.z;bool shifted=false,bounded=true,active=true;double oldDistance=game.Distance;bool monotonic=true;
            bool screenshot=false;int actionedSequence=-1;float nextCountCheck=0;
            while(Time.realtimeSinceStartup-begin<(captureOnly?4f:100f))
            {
                float z=game.Motor.transform.position.z;
                if(game.State==RunState.Running&&game.Obstacles.TryGetNextRoute(z,out CourseRoute route))
                {
                    float ahead=route.Z-z;
                    if(ahead<=CoursePatternGenerator.LaneChangeLead&&game.Motor.Lane!=route.Lane)
                        game.HandleCommand(game.Motor.Lane<route.Lane?RunnerCommand.Right:RunnerCommand.Left);
                    if(route.Sequence!=actionedSequence&&ahead<=game.CurrentSpeed*(route.Action==RouteAction.Jump?game.Settings.JumpRiseSeconds:.42f)&&ahead>0&&game.Motor.Lane==route.Lane)
                    {
                        if(route.Action==RouteAction.Jump){game.HandleCommand(RunnerCommand.Jump);report.scriptedJumps++;}
                        if(route.Action==RouteAction.Slide){game.HandleCommand(RunnerCommand.Slide);report.scriptedSlides++;}
                        actionedSequence=route.Sequence;
                    }
                }
                shifted|=z<oldZ-100;oldZ=z;monotonic&=game.Distance>=oldDistance;oldDistance=game.Distance;
                if(Time.realtimeSinceStartup>=nextCountCheck){bounded&=Count()==report.initialObjects&&game.Obstacles.PoolCount==ObstacleCourse.ObstacleCapacity;nextCountCheck=Time.realtimeSinceStartup+.25f;}
                active&=game.State==RunState.Running;
                report.maximumRunCadence=Mathf.Max(report.maximumRunCadence,game.Animation.RunCadence);
                if(!screenshot&&Time.realtimeSinceStartup-begin>2.2f){yield return Capture("v7-running");screenshot=true;}
                if(game.State!=RunState.Running)break;
                yield return null;
            }
            report.runSeconds=Time.realtimeSinceStartup-begin;report.finalObjects=Count();
            if(!captureOnly){
            Check(active&&report.runSeconds>=100,"100-second patterned run remains active with route-following lane/jump/slide controls");
            Check(bounded,"Stadium, crowd and obstacle object counts remain bounded");
            Check(shifted&&monotonic,"Floating origin shifts obstacle pool and preserves distance");
            Check(game.Obstacles.Passed>20,"More than twenty obstacle rows passed");
            Check(report.scriptedJumps>0&&report.scriptedSlides>0,"Procedural run includes required jump and slide phrases");
            Check(report.maximumRunCadence>=1.3f&&report.maximumRunCadence<=1.8f,"Sprint cadence increases smoothly within the authored range");
            }
            report.passed=errors.Count==0;report.checks=checks.ToArray();report.errors=errors.ToArray();report.unityVersion=Application.unityVersion;report.utc=DateTime.UtcNow.ToString("O");
            File.WriteAllText(ReviewEvidence.File(captureOnly?"../Validation/unity_visual_review_v7.json":"../Validation/unity_obstacles_v7.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;
            Debug.Log("Obstacle gameplay checks "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));
            EditorApplication.isPlaying=false;
        }
        IEnumerator Jump(bool flip)
        {
            game.Play();var obstacle=game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+game.Settings.forwardSpeed*game.Settings.JumpRiseSeconds);
            game.HandleCommand(flip?RunnerCommand.DebugFlip:RunnerCommand.Jump);
            float begin=Time.realtimeSinceStartup,clearance=100;bool captured=false;
            var boxes=obstacle.GetComponentsInChildren<BoxCollider>(); Bounds box=boxes[0].bounds; foreach(var part in boxes) box.Encapsulate(part.bounds);
            while(Time.realtimeSinceStartup-begin<1.2f)
            {
                yield return new WaitForEndOfFrame();var b=PlayerBounds();
                if(b.max.z>box.min.z&&b.min.z<box.max.z)clearance=Mathf.Min(clearance,b.min.y-box.max.y);
                if(!captured&&Time.realtimeSinceStartup-begin>game.Settings.JumpRiseSeconds){ScreenCapture.CaptureScreenshot(ReviewEvidence.File("../Validation/v7-"+(flip?"flip":"jump")+".png"));captured=true;}
            }
            if(flip)report.flipClearance=clearance;else report.normalClearance=clearance;
            Check(game.State==RunState.Running&&game.Motor.Grounded,(flip?"Flip":"Normal jump")+" clears physical obstacle and lands");
            Check(clearance>0&&clearance<100,(flip?"Flip":"Normal jump")+" actual mesh clears obstacle for full longitudinal overlap");
        }

        IEnumerator VerifyFastFall()
        {
            game.Play();game.Obstacles.SetActive(false);
            game.HandleCommand(RunnerCommand.Jump);
            yield return Wait(.18f);
            Check(!game.Motor.Grounded&&game.Motor.VerticalVelocity>0,"Fast-fall test begins during rising jump");
            game.HandleCommand(RunnerCommand.Slide);
            Check(game.Motor.FastFalling&&game.Motor.VerticalVelocity<=-14f&&game.Motor.Capsule.height<=1.51f,"Down command cancels ascent and tucks physical capsule");
            Check(!game.Motor.TrySlide()&&game.Motor.JumpCount==1,"Repeated downward command adds no second dive or jump");
            Vector3 frozen=game.Motor.transform.position;
            game.Pause();yield return Wait(.12f);
            Check(game.Motor.FastFalling&&game.Motor.transform.position==frozen,"Pause preserves an airborne fast-fall without advancing it");
            game.Resume();
            float start=Time.realtimeSinceStartup;
            while(!game.Motor.Grounded&&Time.realtimeSinceStartup-start<.7f)yield return null;
            Check(game.Motor.Grounded&&game.Motor.Sliding&&!game.Motor.FastFalling,"Dive lands into queued grounded slide");
            yield return Wait(.12f);
            Check(game.Animation.CurrentClip=="Slide"&&game.Motor.Capsule.height<=1.51f,"Landing slide retains tucked mesh animation and capsule");
            yield return Wait(game.Settings.slideSeconds+.2f);
            Check(!game.Motor.Sliding&&Mathf.Abs(game.Motor.Capsule.height-2.05f)<.001f,"Fast-fall slide restores standing capsule");
            game.HandleCommand(RunnerCommand.Jump);yield return Wait(.12f);game.HandleCommand(RunnerCommand.Slide);
            game.Play();
            Check(!game.Motor.FastFalling&&!game.Motor.Sliding&&game.Motor.Grounded,"Restart clears airborne dive and queued slide state");
        }

        void VerifyRunClip()
        {
            AnimationClip clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/FenerRunner/Generated/Animations/Run.anim");
            if(clip==null){Check(false,"Generated Run clip exists");return;}
            Check(Mathf.Abs(clip.frameRate-60)<.01f&&Mathf.Abs(clip.length-.8f)<.01f,"Run is a 60 Hz authored in-place cycle");
            string[] legs={"Kartal_Rig/root/pelvis/thigh.L","Kartal_Rig/root/pelvis/thigh.R"};
            bool dense=true;float maxError=0;
            foreach(string thigh in legs)
            {
                string side=thigh.Substring(thigh.Length-1);
                string[] paths={thigh,thigh+"/shin."+side,thigh+"/shin."+side+"/foot."+side};
                foreach(string path in paths)
                {
                    var start=new Quaternion();var end=new Quaternion();
                    for(int component=0;component<4;component++)
                    {
                        string property="m_LocalRotation."+"xyzw"[component];
                        AnimationCurve curve=AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property));
                        if(curve==null){dense=false;continue;}
                        dense&=curve.length==49;
                        start[component]=curve.Evaluate(0);end[component]=curve.Evaluate(clip.length);
                    }
                    maxError=Mathf.Max(maxError,Quaternion.Angle(start.normalized,end.normalized));
                }
            }
            report.runLoopQuaternionError=maxError;
            Check(dense&&maxError<.1f,"Both legs use dense closed quaternion loops without endpoint pop");
            float lowest=100,highest=-100;
            for(int frame=0;frame<48;frame++)
            {
                clip.SampleAnimation(game.Animation.Animator.gameObject,clip.length*frame/48f);
                float sole=PlayerBounds().min.y;
                lowest=Mathf.Min(lowest,sole);highest=Mathf.Max(highest,sole);
            }
            report.runLowestMeshY=lowest;report.runHighestSoleY=highest;
            Check(lowest>-.065f&&highest<.22f,"Baked sprint soles retain ground contact and a bounded flight phase");
            game.Animation.ResetRun(false);
        }
        Bounds PlayerBounds()
        {
            var skin=game.GetComponentInChildren<SkinnedMeshRenderer>();
            if(skin==null)skin=game.Motor.GetComponentInChildren<SkinnedMeshRenderer>();
            skin.BakeMesh(baked);var vertices=baked.vertices;Bounds b=new Bounds(skin.transform.TransformPoint(vertices[0]),Vector3.zero);
            foreach(var v in vertices)b.Encapsulate(skin.transform.TransformPoint(v));
            // Include head-bound hair/moustache accessories, but not reward particles.
            foreach(var accessory in game.Motor.GetComponentsInChildren<MeshRenderer>())
                if(accessory.enabled&&accessory.gameObject.activeInHierarchy)b.Encapsulate(accessory.bounds);
            return b;
        }
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(ReviewEvidence.File("../Validation/"+name+".png"));yield return null;}
        static WaitForSecondsRealtime Wait(float seconds)=>new WaitForSecondsRealtime(seconds);
        static int Count()=>FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
        void Check(bool ok,string label){if(ok)checks.Add(label);else errors.Add(label);}
        void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text);}
        void OnDestroy(){Application.logMessageReceived-=Log;Application.runInBackground=previousBackground;PlayerPrefs.SetFloat("FenerRunner.Best",previousBest);PlayerPrefs.SetInt("FenerRunner.BestScore",previousBestScore);PlayerPrefs.Save();if(baked!=null)Destroy(baked);}
    }
}
#endif
