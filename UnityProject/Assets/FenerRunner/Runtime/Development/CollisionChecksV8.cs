#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class CollisionChecksV8 : MonoBehaviour
    {
        [Serializable] sealed class JumpCase
        {
            public int hz;
            public float speed, launchDistance, visibleClearanceAtFormerHit;
            public bool formerBoxHit, contourHit, landed;
        }
        [Serializable] sealed class Report
        {
            public bool passed;
            public string utc, unityVersion;
            public string[] checks, errors;
            public JumpCase[] jumps;
            public int blackSlices, oracleCases;
            public float blackHeight, wallSeconds;
            public string scope="Unity Editor actual CharacterController and generated obstacle geometry. Former enclosing-box false contacts, late jumps with baked visible-mesh clearance, variable-height capsule sweeps, and real fatal Game updates at simulated 15/30/60 Hz. AutomatedTest suppresses user record and wallet writes. Not physical-device validation.";
        }
        const string SessionKey="FenerRunner.CollisionChecksV8";
        static readonly FieldInfo SettingsField=typeof(RunnerGame).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly MethodInfo SweepMethod=typeof(ObstacleCourse).GetMethod("SweepBox",BindingFlags.Instance|BindingFlags.NonPublic);
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        readonly List<JumpCase> jumps=new List<JumpCase>();
        readonly Collider[] scratch=new Collider[16];
        readonly Report report=new Report();
        RunnerGame game;RunnerSettings originalSettings,testSettings;
        SkinnedMeshRenderer skin;Mesh baked;BoxCollider formerBox;
        bool previousBackground,prepared;float previousCaptureDelta,started;int previousTargetRate;
        int walletBefore;readonly int[] levelsBefore=new int[3];

        [MenuItem("Fener Runner/26 - Verify collision fairness and real contact")]
        public static void StartChecks()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            SessionState.SetBool(SessionKey,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Register()
        {EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(SessionKey,false))return;
            SessionState.SetBool(SessionKey,false);new GameObject("Collision fairness checks v8").AddComponent<CollisionChecksV8>();
        }
        IEnumerator Start()
        {
            started=Time.realtimeSinceStartup;game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;game.enabled=false;
            previousBackground=Application.runInBackground;previousTargetRate=Application.targetFrameRate;previousCaptureDelta=Time.captureDeltaTime;
            Application.runInBackground=true;prepared=true;Application.logMessageReceived+=Log;
            originalSettings=game.Settings;testSettings=Instantiate(originalSettings);testSettings.name="Collision fairness settings clone";
            SettingsField.SetValue(game,testSettings);game.Motor.Configure(testSettings);
            walletBefore=game.WalletBalance;for(int i=0;i<3;i++)levelsBefore[i]=game.UpgradeLevel((PowerupKind)i);
            skin=game.Motor.GetComponentInChildren<SkinnedMeshRenderer>();baked=new Mesh();
            var old=new GameObject("Former oversized envelope (test comparison only)",typeof(BoxCollider));old.layer=31;
            formerBox=old.GetComponent<BoxCollider>();formerBox.isTrigger=true;formerBox.center=new Vector3(0,.96075f,0);formerBox.size=new Vector3(1.781f,1.9215f,1.202f);formerBox.enabled=false;
            var routine=Run();
            while(true)
            {
                bool more;object wait=null;
                try{more=routine.MoveNext();if(more)wait=routine.Current;}
                catch(Exception exception){errors.Add(exception.ToString());break;}
                if(!more)break;yield return wait;
            }
            var wallet=new RunWallet();bool unchanged=wallet.Balance==walletBefore;
            for(int i=0;i<3;i++)unchanged&=wallet.Level((PowerupKind)i)==levelsBefore[i];
            Check(unchanged,"Collision scenarios preserve earned wallet and upgrades");
            report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;
            report.wallSeconds=Time.realtimeSinceStartup-started;report.jumps=jumps.ToArray();report.checks=checks.ToArray();report.errors=errors.ToArray();report.passed=errors.Count==0;
            Directory.CreateDirectory("../Validation");File.WriteAllText(ReviewEvidence.FileFor(8,"../Validation/unity_collisions_v8.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Collision fairness v8 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));
            EditorApplication.isPlaying=false;
        }
        IEnumerator Run()
        {
            Reset(false,18);RunnerObstacle black=game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,14);
            Bounds aggregate=ObstacleBounds(black);report.blackHeight=aggregate.size.y;report.blackSlices=black.GetComponentsInChildren<BoxCollider>().Length;
            Check(report.blackSlices==8&&Mathf.Abs(aggregate.size.y-1.9215f)<.002f,"Eight contour slices preserve the actual black model's full 1.9215 metre height");
            Check(aggregate.size.x>1.77f&&aggregate.size.z>1.19f,"The fix preserves full shoulder width and model depth instead of shrinking the whole obstacle");
            MatchFormer(black);
            foreach(int side in new[]{-1,1})
            {
                Teleport(new Vector3(side*1.08f,1.72f,14));Physics.SyncTransforms();game.Obstacles.BeginMove(game.Motor);
                Check(FormerOverlap()&&game.Obstacles.Check(game.Motor)==null,"The measured empty upper corner on side "+side+" is safe although the former enclosing box reports contact");
            }
            Teleport(new Vector3(0,1.30f,14));Physics.SyncTransforms();
            Check(game.Obstacles.Check(game.Motor)==black,"Actual contact with the central head remains a collision");
            formerBox.enabled=false;

            // These late launch distances lie in the measured gap between the old
            // full-width head envelope and the final visible head contour.
            foreach(int hz in new[]{15,30,60})
                foreach(float speed in new[]{18f,30f})
                {
                    float launch=speed<20?3.95f:6.18f;
                    Reset(false,speed);float startZ=game.Motor.transform.position.z;
                    black=game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,startZ+launch);MatchFormer(black);
                    var result=new JumpCase{hz=hz,speed=speed,launchDistance=launch,visibleClearanceAtFormerHit=-999};
                    game.Motor.TryJump();float elapsed=0;bool captured=false;
                    while(elapsed<.9f)
                    {
                        float frameRemaining=1f/hz;
                        while(frameRemaining>.000001f)
                        {
                            float step=Mathf.Min(frameRemaining,1f/60f);game.Obstacles.BeginMove(game.Motor);game.Motor.Tick(step);
                            result.contourHit|=game.Obstacles.Check(game.Motor)!=null;
                            game.Animation.Tick(step);game.Animation.Animator.Update(step);
                            if(FormerOverlap())
                            {
                                result.formerBoxHit=true;
                                float clearance=VisibleBounds().min.y-ObstacleBounds(black).max.y;
                                result.visibleClearanceAtFormerHit=Mathf.Max(result.visibleClearanceAtFormerHit,clearance);
                                if(!captured&&hz==60&&speed==18&&clearance>0)
                                {
                                    captured=true;game.Pause();game.Interface.Show(RunState.Running);
                                    FindFirstObjectByType<FollowCamera>().Snap();yield return new WaitForEndOfFrame();
                                    ScreenCapture.CaptureScreenshot(ReviewEvidence.FileFor(8,"../Validation/v8-late-jump.png"));yield return null;game.Resume();
                                }
                            }
                            frameRemaining-=step;elapsed+=step;
                        }
                    }
                    result.landed=game.Motor.Grounded;jumps.Add(result);
                    Check(result.formerBoxHit&&!result.contourHit&&result.landed,"A real late jump at "+speed+" m/s and "+hz+" Hz clears the model while reproducing the former-box false collision");
                    Check(result.visibleClearanceAtFormerHit>0,"The baked visible bird has positive vertical clearance at a former false hit at "+speed+" m/s and "+hz+" Hz");
                    formerBox.enabled=false;
                }

            Reset(false,30);var gate=game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder,game.Motor.transform.position.z+4.7f);
            game.Obstacles.BeginMove(game.Motor);Vector3 start=game.Motor.transform.position;
            game.Motor.TrySlide();game.Motor.Tick(.17f);
            Check(game.Motor.Capsule.height<1.51f&&game.Obstacles.Check(game.Motor)==null,"A real shrinking slide clears a gate without dragging standing shoulders through the whole move");
            Bounds header=FindHeader(gate).bounds;
            bool oldTall=Sweep(header,start+Vector3.up*.38f,start+Vector3.up*1.67f,game.Motor.transform.position+Vector3.up*.38f,game.Motor.transform.position+Vector3.up*1.67f,.38f,.38f);
            Check(oldTall,"The shrinking-slide regression actually intersects the former taller sweep envelope");

            // A dense independent distance oracle covers variable radius as well
            // as both changing capsule ends, without reproducing the solver.
            Check(SweepMethod!=null,"The continuous variable-capsule solver is available");
            if(SweepMethod!=null)
            {
                for(int i=0;i<120;i++)
                {
                    bool shrinking=i%2==0;float z=2.8f+(i%10)*.14f,x=((i/10)%3-1)*.4f;
                    Bounds box=new Bounds(new Vector3(0,2.6f,z),new Vector3(2.82f,1.88f,.3f));
                    Vector3 b0=new Vector3(x,.38f,0),b1=new Vector3(x,.38f,5.1f);
                    Vector3 t0=new Vector3(x,shrinking?1.67f:1.12f,0),t1=new Vector3(x,shrinking?1.12f:1.67f,5.1f);
                    float r0=.38f,r1=i<60?.38f:.30f;
                    bool actual=Sweep(box,b0,t0,b1,t1,r0,r1);float minimum=float.PositiveInfinity;
                    for(int sample=0;sample<=5000;sample++)
                    {
                        float u=sample/5000f;Vector3 b=Vector3.Lerp(b0,b1,u),t=Vector3.Lerp(t0,t1,u);float r=Mathf.Lerp(r0,r1,u);
                        float dx=Mathf.Max(box.min.x-b.x,b.x-box.max.x,0),dz=Mathf.Max(box.min.z-b.z,b.z-box.max.z,0);
                        float dy=Mathf.Max(box.min.y-t.y,b.y-box.max.y,0);minimum=Mathf.Min(minimum,dx*dx+dy*dy+dz*dz-r*r);
                    }
                    if(Mathf.Abs(minimum)>.00001f)Check(actual==(minimum<0),"Continuous changing-capsule result agrees with the independent dense geometry oracle "+i);
                    report.oracleCases++;
                }
                Bounds tooEarly=new Bounds(new Vector3(0,2.6f,3.7f),new Vector3(2.82f,1.88f,.3f));
                Check(Sweep(tooEarly,new Vector3(0,.38f,0),new Vector3(0,1.12f,0),new Vector3(0,.38f,5.1f),new Vector3(0,1.67f,5.1f),.38f,.38f),"Standing up too early while crossing a header still hits the real changing capsule");
            }

            foreach(int hz in new[]{15,30,60})
            {
                Time.captureDeltaTime=1f/hz;Application.targetFrameRate=hz;
                foreach(ObstacleKind kind in new[]{ObstacleKind.JumpOver,ObstacleKind.SlideUnder})
                {
                    Reset(true,30);game.Obstacles.ArrangeTest(kind,game.Motor.transform.position.z+4);
                    float deadline=Time.realtimeSinceStartup+2;
                    while(game.State==RunState.Running&&Time.realtimeSinceStartup<deadline)yield return null;
                    Check(game.State==RunState.Finished&&game.LastHit.Length>0,"True central "+kind+" contact remains fatal through Game.Update at "+hz+" Hz");
                }
                Reset(true,30);game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+2.5f);game.HandleCommand(RunnerCommand.Jump);
                float lateDeadline=Time.realtimeSinceStartup+2;
                while(game.State==RunState.Running&&Time.realtimeSinceStartup<lateDeadline)yield return null;
                Check(game.State==RunState.Finished&&!game.Powerups.IsProtected,"A genuinely too-late jump still hits the black body at "+hz+" Hz");
            }
            game.enabled=false;game.ReturnToMenu();
        }
        void Reset(bool live,float speed)
        {
            game.enabled=false;formerBox.enabled=false;testSettings.forwardSpeed=testSettings.maxSpeed=speed;
            game.Powerups.Enabled=false;game.Tokens.Enabled=false;game.Play();game.Animation.TurnAllowed=false;
            game.Obstacles.SetActive(false);game.Motor.Speed=speed;game.enabled=live;
        }
        void Teleport(Vector3 position){game.Motor.Capsule.enabled=false;game.Motor.transform.position=position;game.Motor.Capsule.enabled=true;}
        void MatchFormer(RunnerObstacle obstacle)
        {formerBox.transform.SetPositionAndRotation(obstacle.transform.position,obstacle.transform.rotation);formerBox.enabled=true;Physics.SyncTransforms();}
        bool FormerOverlap()
        {
            CharacterController c=game.Motor.Capsule;Vector3 centre=game.Motor.transform.TransformPoint(c.center);
            Vector3 offset=Vector3.up*(c.height*.5f-c.radius);
            int count=Physics.OverlapCapsuleNonAlloc(centre-offset,centre+offset,c.radius,scratch,1<<31,QueryTriggerInteraction.Collide);
            for(int i=0;i<count;i++)if(scratch[i]==formerBox)return true;return false;
        }
        static Bounds ObstacleBounds(RunnerObstacle obstacle)
        {
            BoxCollider[] boxes=obstacle.GetComponentsInChildren<BoxCollider>();Bounds result=boxes[0].bounds;
            for(int i=1;i<boxes.Length;i++)result.Encapsulate(boxes[i].bounds);return result;
        }
        static BoxCollider FindHeader(RunnerObstacle obstacle)
        {foreach(BoxCollider box in obstacle.GetComponentsInChildren<BoxCollider>())if(box.name=="TorsoHeader")return box;throw new InvalidOperationException("Missing gate header.");}
        Bounds VisibleBounds()
        {
            skin.BakeMesh(baked);Vector3[] points=baked.vertices;Bounds bounds=new Bounds(skin.transform.TransformPoint(points[0]),Vector3.zero);
            foreach(Vector3 p in points)bounds.Encapsulate(skin.transform.TransformPoint(p));return bounds;
        }
        bool Sweep(Bounds box,Vector3 b0,Vector3 t0,Vector3 b1,Vector3 t1,float r0,float r1)
        {
            if(SweepMethod==null)throw new MissingMethodException("ObstacleCourse.SweepBox");
            object[] args={box,b0,t0,b1,t1,r0,r1,0f};return (bool)SweepMethod.Invoke(game.Obstacles,args);
        }
        void Check(bool condition,string label){if(condition)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;if(!prepared)return;Time.timeScale=1;
            Application.runInBackground=previousBackground;Application.targetFrameRate=previousTargetRate;Time.captureDeltaTime=previousCaptureDelta;
            if(game!=null&&originalSettings!=null){SettingsField.SetValue(game,originalSettings);game.Motor.Configure(originalSettings);game.enabled=true;}
            if(formerBox!=null)Destroy(formerBox.gameObject);if(baked!=null)Destroy(baked);if(testSettings!=null)Destroy(testSettings);
        }
    }
}
#endif
