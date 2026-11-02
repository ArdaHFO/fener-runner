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
    public sealed class SlideRecoveryChecksV17:MonoBehaviour
    {
        const string Flag="FenerRunner.SlideRecoveryV17";
        [Serializable] sealed class Sample
        {public int hz;public float elapsed,capsuleHeight,visibleHeight,recoveryProgress;public bool recovering;}
        [Serializable] sealed class Report
        {
            public bool passed;public string utc;public string[] checks,errors;public Sample[] samples;
            public float maximumCapsuleExcess,headerBottom,minimumRunHeight,maximumRunHeight,minimumSlideHeight,baselineCapsuleExcess,maximumExtraRecoveryPadding;
            public string scope="Native authored SkinnedMeshRenderer BakeMesh through natural slide-to-run blend at15/30/60/120Hz, real gate overlap controls at expiry and after recovery, bounded capsule recovery, immediate jump/flight/new-slide cancellation, double slide, pause/reset. Native Editor frames, not physical-phone or all-input proof.";
        }
        readonly List<string> checks=new List<string>(),errors=new List<string>();readonly List<Sample> samples=new List<Sample>();Report report=new Report();
        RunnerGame game;RunnerMotor motor;RunnerAnimation motion;Animator animator;SkinnedMeshRenderer skin;Mesh baked;RunWallet wallet;
        GameObject gateReference;Mesh gateMesh;
        bool prepared,background,oldAnimatorEnabled;float oldScale,oldCapture;int oldRate;
        [MenuItem("Fener Runner/109 - Verify natural slide recovery v17")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod]static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Natural slide recovery checks v17").AddComponent<SlideRecoveryChecksV17>();}}
        IEnumerator Start()
        {
            background=Application.runInBackground;oldScale=Time.timeScale;oldCapture=Time.captureDeltaTime;oldRate=Application.targetFrameRate;prepared=true;
            Application.runInBackground=true;Application.targetFrameRate=60;Time.captureDeltaTime=1f/60;Application.logMessageReceived+=Log;
            var run=Run();while(true){bool more;object value=null;try{more=run.MoveNext();if(more)value=run.Current;}catch(Exception e){errors.Add(e.ToString());break;}if(!more)break;yield return value;}
            if(game!=null){game.enabled=false;game.ReturnToMenu();}
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.checks=checks.ToArray();report.errors=errors.ToArray();report.samples=samples.ToArray();
            File.WriteAllText(Evidence("unity_slide_recovery_v17.json"),JsonUtility.ToJson(report,true));Application.logMessageReceived-=Log;
            Debug.Log("Slide recovery v17 "+(report.passed?"PASSED":"FAILED")+": "+checks.Count+" checks; "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Run()
        {
            game=FindFirstObjectByType<RunnerGame>();if(game==null)throw new InvalidOperationException("Actual Fener Runner scene required.");
            game.AutomatedTest=true;game.enabled=false;wallet=game.UseWalletForTest("FenerRunner.Test.RecoveryV17."+DateTime.UtcNow.Ticks+".");
            motor=game.Motor;motion=game.Animation;animator=motion.Animator;oldAnimatorEnabled=animator.enabled;animator.enabled=false;
            skin=animator.GetComponentInChildren<SkinnedMeshRenderer>();baked=new Mesh();
            Check(Mathf.Abs(motor.SlideRecoverySeconds-Mathf.Min(game.Settings.blendSeconds,.09f))<.0001f,"Natural collision recovery duration matches the existing short Run crossfade");
            // The authored running stride has vertical body motion, and its
            // established2.05m collision envelope is deliberately stable. Measure
            // that existing tolerance rather than pretending every pose is2.05m.
            Reset();report.minimumRunHeight=report.minimumSlideHeight=float.PositiveInfinity;
            for(int frame=0;frame<120;frame++)
            {
                Step(1f/60);yield return new WaitForEndOfFrame();float height=VisibleHeight();
                report.minimumRunHeight=Mathf.Min(report.minimumRunHeight,height);report.maximumRunHeight=Mathf.Max(report.maximumRunHeight,height);
                report.baselineCapsuleExcess=Mathf.Max(report.baselineCapsuleExcess,motor.Capsule.height-height);
            }
            Reset();motor.TrySlide();
            for(int frame=0;frame<50;frame++)
            {
                Step(1f/60);yield return new WaitForEndOfFrame();if(frame<6)continue;float height=VisibleHeight();
                report.minimumSlideHeight=Mathf.Min(report.minimumSlideHeight,height);report.baselineCapsuleExcess=Mathf.Max(report.baselineCapsuleExcess,motor.Capsule.height-height);
            }
            Check(report.minimumRunHeight>1.6f&&report.maximumRunHeight>report.minimumRunHeight&&report.minimumSlideHeight<1.5f,"Native full-stride and held-slide geometry establish an independent collision-envelope baseline");
            foreach(int hz in new[]{15,30,60,120})
            {
                Reset();motor.TrySlide();float elapsed=0,previous=1.5f;bool monotonic=true,bounded=true,observedRecovery=false,finished=false;
                while(elapsed<game.Settings.slideSeconds+.2f)
                {
                    float dt=1f/hz;Step(dt);elapsed+=dt;yield return new WaitForEndOfFrame();
                    if(elapsed>=game.Settings.slideSeconds-.0001f)
                    {
                        float visible=VisibleHeight(),height=motor.Capsule.height;
                        samples.Add(new Sample{hz=hz,elapsed=elapsed-game.Settings.slideSeconds,capsuleHeight=height,visibleHeight=visible,recoveryProgress=motor.SlideRecoveryProgress,recovering=motor.SlideRecovering});
                        monotonic&=height>=previous-.001f;previous=height;bounded&=height>=1.499f&&height<=2.051f;
                        observedRecovery|=motor.SlideRecovering;finished|=!motor.SlideRecovering&&height>2.04f;
                        report.maximumCapsuleExcess=Mathf.Max(report.maximumCapsuleExcess,height-visible);
                        report.maximumExtraRecoveryPadding=Mathf.Max(report.maximumExtraRecoveryPadding,height-visible-report.baselineCapsuleExcess);
                        Check(height<=visible+report.baselineCapsuleExcess+.012f,"Recovery adds no invisible envelope beyond measured native standing/slide tolerance / "+hz+"Hz t="+(elapsed-game.Settings.slideSeconds).ToString("F3"));
                    }
                }
                Check(monotonic&&bounded&&observedRecovery&&finished,"Natural expiry rises monotonically through a bounded short recovery then restores full collision height / "+hz+"Hz");
            }
            // This reproduces the former invisible-header hit: visible low mesh,
            // expired motor timer, and a real header over the same world position.
            Reset();var gate=game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder,motor.transform.position.z);CreateReference(gate);
            Check(gateReference.GetComponent<MeshCollider>().Raycast(new Ray(motor.transform.position+Vector3.up*.02f,Vector3.up),out RaycastHit underside,5),"Independent native final-mesh ray finds the actual overhead body surface");
            report.headerBottom=underside.point.y-motor.transform.position.y;
            game.Obstacles.SetActive(false);motor.TrySlide();
            for(int frame=0;frame<50;frame++){Step(1f/60);yield return new WaitForEndOfFrame();}
            Step(game.Settings.slideSeconds-.005f-50f/60);yield return new WaitForEndOfFrame();
            game.Obstacles.Enabled=true;gate.gameObject.SetActive(true);Physics.SyncTransforms();
            bool hitAtExpiry=Step(.01f);yield return new WaitForEndOfFrame();float earlyMesh=VisibleHeight();
            Check(!motor.Sliding&&motor.SlideRecovering&&earlyMesh<report.headerBottom&&motor.Capsule.height<report.headerBottom&&!hitAtExpiry,"Real final-mesh gate stays clear just after expiry while the rendered chick remains beneath it");
            bool hitAt30=Step(.025f);yield return new WaitForEndOfFrame();
            Check(motor.SlideRecovering&&VisibleHeight()<report.headerBottom&&!hitAt30,"A real gate still cannot hit the visibly low head30ms into recovery");
            bool hitAt50=Step(.02f);yield return new WaitForEndOfFrame();
            Check(motor.SlideRecovering&&VisibleHeight()<report.headerBottom&&motor.Capsule.height<=VisibleHeight()+.03f&&!hitAt50,"At50ms the recovered capsule follows measured head rise and clears the real surface that the former SmoothStep envelope entered");
            ScreenCapture.CaptureScreenshot(Evidence("v17-slide-recovery-clear.png"));yield return new WaitForEndOfFrame();
            bool hitUpright=Step(.13f);yield return new WaitForEndOfFrame();
            Check(!motor.SlideRecovering&&motor.Capsule.height>2&&VisibleHeight()>report.headerBottom&&hitUpright,"The same gate becomes a lethal contact when the actual recovery reaches standing height");
            gateReference.SetActive(false);

            Reset();motor.TrySlide();Step(game.Settings.slideSeconds+.025f);Check(motor.SlideRecovering,"A natural slide has an observable brief recovery state");
            float heightBefore=motor.Capsule.height,progress=motor.SlideRecoveryProgress;game.Pause();game.AdvanceForTest(.2f);yield return new WaitForSecondsRealtime(.08f);
            Check(motor.Capsule.height==heightBefore&&motor.SlideRecoveryProgress==progress,"Pause freezes the partial collision recovery instead of standing up in the background");game.Resume();Step(.01f);
            Check(motor.Capsule.height>heightBefore&&motor.SlideRecovering,"Explicit resume continues the same recovery");
            Check(motor.TrySlide()&&!motor.SlideRecovering&&motor.Capsule.height<1.501f,"A new down swipe cancels recovery and immediately returns to the low capsule");
            Check(motor.TrySlide()&&motor.SlideQueued,"The renewed low slide can still queue its second cycle");
            Step(game.Settings.slideSeconds+.02f);Check(motor.Sliding&&!motor.SlideRecovering&&motor.Capsule.height<1.501f,"Queued slides do not rise or enter recovery at their cycle boundary");
            Reset();motor.TrySlide();Step(game.Settings.slideSeconds+.02f);Check(motor.TryJump()&&!motor.SlideRecovering&&motor.Capsule.height>2.04f,"Jump from recovery restores the upright jumping capsule immediately");
            Reset();motor.TrySlide();Step(game.Settings.slideSeconds+.02f);motor.SetFlight(true);Check(!motor.SlideRecovering&&motor.Capsule.height>2.04f&&motor.Flying,"Powered flight cancels collision recovery immediately");
            Reset();motor.TrySlide();Step(game.Settings.slideSeconds+.02f);motor.ResetRun();Check(!motor.SlideRecovering&&!motor.Sliding&&motor.Capsule.height>2.04f,"Restart clears recovery and restores the normal standing body");
            RunnerSettings original=game.Settings,zero=Instantiate(original);
            try
            {zero.blendSeconds=0;motor.Configure(zero);motor.ResetRun();motor.TrySlide();motor.Tick(zero.slideSeconds+.001f);Check(!motor.SlideRecovering&&motor.Capsule.height>2.04f,"A zero-duration blend cannot leave a permanent crouched recovery");}
            finally{motor.Configure(original);Destroy(zero);}
            Check(File.Exists(Evidence("v17-slide-recovery-clear.png")),"Native header-clear recovery screenshot was written");
        }
        void Reset(){game.enabled=false;game.Tokens.Enabled=false;game.Powerups.Enabled=false;game.Play();game.Obstacles.SetActive(false);game.Powerups.IsolatePickupsForTest();motor.Speed=0;motion.TurnAllowed=false;animator.enabled=false;}
        bool Step(float dt)
        {
            bool hit=false;float remaining=dt;
            while(remaining>.000001f){float part=Mathf.Min(remaining,1f/60);game.Obstacles.BeginMove(motor);motor.Tick(part);hit|=game.Obstacles.Check(motor)!=null;remaining-=part;}
            motion.Tick(dt);animator.Update(dt);return hit;
        }
        float VisibleHeight(){skin.BakeMesh(baked);float top=float.NegativeInfinity;foreach(var p in baked.vertices)top=Mathf.Max(top,skin.transform.TransformPoint(p).y);return top-motor.transform.position.y;}
        void CreateReference(RunnerObstacle obstacle)
        {
            gateReference=new GameObject("Independent recovery gate surface");gateReference.layer=31;
            gateMesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};var vertices=new List<Vector3>();var indices=new List<int>();
            // Rebuild from the live rendered geometry, independently of hitMesh,
            // so an erroneous bake transform cannot agree with its own oracle.
            foreach(var filter in obstacle.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                int offset=vertices.Count;Matrix4x4 toWorld=filter.transform.localToWorldMatrix;
                foreach(var point in filter.sharedMesh.vertices)vertices.Add(toWorld.MultiplyPoint3x4(point));
                foreach(int index in filter.sharedMesh.triangles)indices.Add(offset+index);
            }
            if(vertices.Count==0)throw new InvalidOperationException("Actual final gate rendering geometry is missing.");
            gateMesh.SetVertices(vertices);gateMesh.SetTriangles(indices,0);gateMesh.RecalculateBounds();
            var collider=gateReference.AddComponent<MeshCollider>();collider.sharedMesh=gateMesh;Physics.IgnoreCollision(collider,motor.Capsule,true);Physics.SyncTransforms();
        }
        static string Evidence(string file){string path=ReviewEvidence.FileFor(17,"../Validation/"+file);Directory.CreateDirectory(Path.GetDirectoryName(path));return path;}
        void Check(bool value,string label){if(value)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<70)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;if(!prepared)return;if(animator!=null)animator.enabled=oldAnimatorEnabled;if(wallet!=null)wallet.DeleteIsolatedTestData();if(baked!=null)Destroy(baked);if(gateReference!=null)Destroy(gateReference);if(gateMesh!=null)Destroy(gateMesh);Application.runInBackground=background;Application.targetFrameRate=oldRate;Time.captureDeltaTime=oldCapture;Time.timeScale=oldScale;}
    }
}
#endif
