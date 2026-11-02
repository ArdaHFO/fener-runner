#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class DoubleActionsChecksV16:MonoBehaviour
    {
        const string Flag="FenerRunner.DoubleActionsV16";
        [Serializable] sealed class Report
        {
            public bool passed;public string utc,unityVersion;public string[] checks,errors;
            public float maximumApex,maximumSlidePoseStep,maximumSlideChainReaction;
            public string scope="Actual scene CharacterController, Animator, mobile gesture recognizer, swept obstacle contacts and production death path. Permanent double jump and a bounded queued second slide at 15/30/60/120Hz and 18/42m/s, power combinations, reset and pause. Native Editor simulation, not physical touchscreen or device performance validation.";
        }
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        Report report=new Report();RunnerGame game;RunnerMotor motor;RunnerAnimation motion;RunWallet wallet;
        bool prepared,background;float oldScale,oldCapture;int oldRate;
        [MenuItem("Fener Runner/105 - Verify double actions v16")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Double action checks v16").AddComponent<DoubleActionsChecksV16>();}}
        IEnumerator Start()
        {
            background=Application.runInBackground;oldScale=Time.timeScale;oldCapture=Time.captureDeltaTime;oldRate=Application.targetFrameRate;prepared=true;
            Application.runInBackground=true;Application.targetFrameRate=60;Time.captureDeltaTime=1f/60;Application.logMessageReceived+=Log;
            var routine=Run();
            while(true)
            {
                bool more;object wait=null;
                try{more=routine.MoveNext();if(more)wait=routine.Current;}catch(Exception e){errors.Add(e.ToString());break;}
                if(!more)break;yield return wait;
            }
            if(game!=null){game.enabled=false;game.ReturnToMenu();}
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.checks=checks.ToArray();report.errors=errors.ToArray();
            File.WriteAllText(Evidence("unity_double_actions_v16.json"),JsonUtility.ToJson(report,true));Application.logMessageReceived-=Log;
            Debug.Log("Double actions v16 "+(report.passed?"PASSED":"FAILED")+": "+checks.Count+" checks; "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Run()
        {
            game=FindFirstObjectByType<RunnerGame>();if(game==null)throw new InvalidOperationException("Actual game scene is required.");
            game.AutomatedTest=true;game.enabled=false;motor=game.Motor;motion=game.Animation;
            wallet=game.UseWalletForTest("FenerRunner.Test.DoubleActionsV16."+DateTime.UtcNow.Ticks+".");
            foreach(int hz in new[]{15,30,60,120})
            foreach(float timing in new[]{0f,.12f,.30f,.57f})
            {
                Reset(0);Check(motor.TryJump(),"First jump accepted / "+hz+"Hz "+timing);
                Move(timing,hz);float before=motor.VerticalVelocity;bool second=motor.TryJump();
                Check(second&&motor.JumpCount==2&&motor.AirStepUsed&&motor.VerticalVelocity>before,"Ordinary air jump adds a real upward impulse / "+hz+"Hz "+timing);
                float secondVelocity=motor.VerticalVelocity;Check(!motor.TryJump()&&motor.JumpCount==2&&motor.VerticalVelocity==secondVelocity,"Third same-frame input cannot add another impulse / "+hz+"Hz "+timing);
                float peak=motor.transform.position.y,elapsed=0;
                while(!motor.Grounded&&elapsed<2){Move(1f/hz,hz);peak=Mathf.Max(peak,motor.transform.position.y);elapsed+=1f/hz;}
                report.maximumApex=Mathf.Max(report.maximumApex,peak);
                Check(motor.Grounded&&!motor.AirStepUsed&&peak<=5.05f,"Double jump lands and resets its single charge within bounded height / "+hz+"Hz "+timing);
                Check(motor.TryJump()&&motor.TryJump()&&motor.JumpCount==4,"The next landing restores exactly one new air jump / "+hz+"Hz "+timing);
                yield return null;
            }
            // The pickup strengthens the permanent ability without granting a third charge.
            Reset(0);motor.TryJump();Move(.18f,120);motor.TryJump();float ordinaryImpulse=motor.VerticalVelocity;
            Reset(0);motor.AirStepEnabled=true;motor.TryJump();Move(.18f,120);motor.TryJump();float poweredImpulse=motor.VerticalVelocity;
            Check(poweredImpulse>ordinaryImpulse+1,"AirStep still adds a meaningfully stronger second impulse than ordinary double jump");
            motor.AirStepEnabled=false;float inFlight=motor.VerticalVelocity;
            Check(!motor.TryJump()&&motor.VerticalVelocity==inFlight,"Power expiry cannot refund a spent air charge or change velocity");
            motor.AirStepEnabled=true;Check(!motor.TryJump(),"Collecting AirStep after using the air jump cannot create a third jump");
            Reset(0);motor.JumpHeightMultiplier=1.5f;motor.AirStepEnabled=true;motor.TryJump();motor.TryJump();float superPeak=0;
            for(int i=0;i<240&&!motor.Grounded;i++){Move(1f/120,120);superPeak=Mathf.Max(superPeak,motor.transform.position.y);}
            Check(superPeak<=5.05f&&motor.Grounded,"SuperJump plus strongest immediate air impulse respects the five-metre cap and lands");
            Reset(0);motor.TryJump();Move(.12f,120);motor.TryJump();Check(motor.TrySlide()&&motor.FastFalling&&!motor.TryJump(),"Down swipe cancels the second jump and cannot be undone by an upward swipe");
            for(int i=0;i<240&&!motor.Grounded;i++)Move(1f/120,120);
            Check(motor.Grounded&&motor.Sliding&&!motor.AirStepUsed,"A cancelled double jump lands low and replenishes the next air charge");
            Reset(0);motor.TryJump();motor.TryJump();motor.SetFlight(true);
            Check(!motor.TryJump()&&!motor.TrySlide()&&!motor.SlideQueued,"Powered flight owns height and rejects double-action input");
            motor.SetFlight(false);for(int i=0;i<360&&motor.Flying;i++)Move(1f/120,120);
            Check(motor.Grounded&&!motor.Flying&&!motor.AirStepUsed&&motor.TryJump()&&motor.TryJump(),"Finishing flight restores normal double-jump control on real ground contact");

            foreach(int hz in new[]{15,30,60,120})
            foreach(float speed in new[]{18f,42f})
            {
                string label=hz+"Hz "+speed+"m/s";Reset(speed);float duration=game.Settings.slideSeconds;
                Check(motor.TrySlide(),"First low slide starts immediately / "+label);Move(.001f,hz);
                Check(motor.TrySlide()&&motor.SlideQueued&&motor.SlideCount==2,"A second 1ms-later swipe reserves a full next slide / "+label);
                Check(!motor.TrySlide()&&motor.SlideCount==2,"An immediate third swipe cannot stockpile unlimited slides / "+label);
                bool alwaysLow=true;float elapsed=.001f;
                while(elapsed<duration*2-.015f){float dt=Mathf.Min(1f/hz,duration*2-.015f-elapsed);Move(dt,hz);elapsed+=dt;alwaysLow&=motor.Sliding&&motor.Capsule.height<1.501f;}
                Check(alwaysLow&&motor.SlideCycleCount==2&&!motor.SlideQueued,"Two complete low cycles have no standing collision frame / "+label);
                Move(.15f,hz);Check(!motor.Sliding&&!motor.SlideRecovering&&motor.Capsule.height>2,"Both slides expire and restore the standing capsule after the visual recovery / "+label);
                Reset(speed);motor.TrySlide();motor.TrySlide();Move(duration+.02f,hz);
                Check(motor.TrySlide()&&motor.SlideCount==3,"Once the second cycle starts, a fresh swipe can chain another / "+label);
                Reset(speed);ArrangeGates(speed);motor.TrySlide();bool hit=Move(.001f,hz);motor.TrySlide();hit|=Move(1.48f,hz);
                Check(!hit,"An early double slide clears two actual consecutive gate colliders / "+label);
                Reset(speed);ArrangeGates(speed);motor.TrySlide();Check(Move(1.48f,hz),"A single slide still hits the second gate after its stance expires / "+label);
                Reset(speed);game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,motor.transform.position.z+speed*.65f);motor.TryJump();Move(.30f,hz);motor.TryJump();
                Check(!Move(.75f,hz),"A second upward swipe clears a late real floor obstacle / "+label);
                Reset(speed);game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,motor.transform.position.z+speed*.65f);motor.TryJump();
                Check(Move(1.05f,hz),"The same late floor obstacle remains a contact without the second upward swipe / "+label);
                yield return null;
            }
            Reset(0);motor.TrySlide();motor.TrySlide();Check(motor.TryJump()&&!motor.Sliding&&!motor.SlideQueued&&motor.Capsule.height>2,"Jump cancels both the active and queued slide");
            Reset(0);motor.TrySlide();motor.TrySlide();motor.SetFlight(true);Check(!motor.Sliding&&!motor.SlideQueued,"Flight cancels the queued slide without a hidden landing replay");
            Reset(0);motor.TrySlide();motor.TrySlide();motor.ResetRun();Check(!motor.Sliding&&!motor.SlideQueued&&motor.SlideCycleCount==0&&motor.SlideCount==0,"Reset clears all slide cycles and input reservations");
            Reset(0);motor.TrySlide();motor.TrySlide();float remaining=motor.SlideRemaining;Vector3 position=motor.transform.position;int count=motor.SlideCount;
            game.Pause();game.HandleCommand(RunnerCommand.Jump);game.HandleCommand(RunnerCommand.Slide);game.enabled=true;yield return new WaitForSecondsRealtime(.15f);game.enabled=false;
            Check(motor.SlideRemaining==remaining&&motor.transform.position==position&&motor.SlideCount==count&&motor.SlideQueued,"Pause freezes a queued slide and ignores new gameplay commands");
            game.Resume();Move(.1f,60);Check(motor.SlideRemaining<remaining&&motor.SlideQueued,"Resume continues the saved queue without restarting it");
            Reset(18);motor.TrySlide();motor.TrySlide();game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,motor.transform.position.z);game.AdvanceForTest(1f/60);
            int deaths=motion.DeathCount;Check(game.State==RunState.Finished&&motion.DeathPlaying&&!motor.SlideQueued,"A real solid contact kills a double-slide run and clears the pending cycle");
            game.HandleCommand(RunnerCommand.Jump);game.HandleCommand(RunnerCommand.Slide);game.AdvanceForTest(.2f);
            Check(game.State==RunState.Finished&&motion.DeathCount==deaths&&!motor.SlideQueued,"Double actions cannot escape death or restart its animation");
            Reset(18);motor.TryJump();Move(.05f,60);motor.TryJump();game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder,motor.transform.position.z);game.AdvanceForTest(1f/60);
            Check(game.State==RunState.Finished,"An airborne second jump still dies on an intersecting real gate header");

            Reset(0);var input=FindFirstObjectByType<RunnerInput>();Swipe(input,Vector2.up);Swipe(input,Vector2.up);Swipe(input,Vector2.up);
            Check(motor.JumpCount==2&&motor.AirStepUsed,"Three real recognizer gestures deliver exactly two jumps");
            Reset(0);Swipe(input,Vector2.down);Swipe(input,Vector2.down);Swipe(input,Vector2.down);
            Check(motor.SlideCount==2&&motor.SlideQueued,"Three real downward gestures create exactly the active and queued slide");
            // Actual Animator poses, including a distinct low push on the second cycle.
            Reset(18);motor.TrySlide();float lastPhase=0;
            for(int frame=0;frame<125;frame++)
            {
                if(frame==8)motor.TrySlide();Move(1f/60,60);motion.Tick(1f/60);motion.Animator.Update(1f/60);yield return new WaitForEndOfFrame();
                if(motor.Sliding&&frame>5)
                {
                    Check(motion.SlidePosePhase>=.319f&&motion.SlidePosePhase<=.501f,"Chained authored pose remains low / frame "+frame);
                    if(lastPhase>0)report.maximumSlidePoseStep=Mathf.Max(report.maximumSlidePoseStep,Mathf.Abs(motion.SlidePosePhase-lastPhase));lastPhase=motion.SlidePosePhase;
                    report.maximumSlideChainReaction=Mathf.Max(report.maximumSlideChainReaction,motion.SlideChainReactionWeight);
                }
                if(frame==65)ScreenCapture.CaptureScreenshot(Evidence("v16-double-slide.png"));
            }
            Check(report.maximumSlidePoseStep<.02f&&report.maximumSlideChainReaction>.8f,"The second slide has a visible renewed push and smooth low-pose phase");
            Check(!motor.Sliding&&motion.CurrentClip=="Run","Chained slide animation settles back into running after both complete cycles");
            Reset(18);motor.TryJump();Move(.2f,60);motor.TryJump();
            for(int frame=0;frame<16;frame++){Move(1f/60,60);motion.Tick(1f/60);motion.Animator.Update(1f/60);yield return new WaitForEndOfFrame();}
            Check(motion.CurrentClip=="Jump"&&!motor.Grounded,"The second jump restarts actual airborne animation");
            ScreenCapture.CaptureScreenshot(Evidence("v16-double-jump.png"));yield return new WaitForEndOfFrame();
            Check(File.Exists(Evidence("v16-double-slide.png"))&&File.Exists(Evidence("v16-double-jump.png")),"Native scene double-action screenshots were written");
        }
        void Reset(float speed)
        {game.enabled=false;game.Tokens.Enabled=false;game.Powerups.Enabled=false;game.Play();game.Obstacles.SetActive(false);motor.Speed=speed;}
        void ArrangeGates(float speed)
        {float z=motor.transform.position.z;game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder,z+speed*.35f);game.Obstacles.AddTestObstacle(ObstacleKind.SlideUnder,z+speed*1.15f);}
        bool Move(float duration,int hz)
        {
            bool hit=false;float elapsed=0;
            while(elapsed<duration-.000001f)
            {
                float frame=Mathf.Min(1f/hz,duration-elapsed);
                while(frame>.000001f){float dt=Mathf.Min(frame,1f/60);game.Obstacles.BeginMove(motor);motor.Tick(dt);hit|=game.Obstacles.Check(motor)!=null;elapsed+=dt;frame-=dt;}
            }
            return hit;
        }
        static void Swipe(RunnerInput input,Vector2 direction)
        {Vector2 from=new Vector2(Screen.width*.5f,Screen.height*.55f),to=from+direction*Mathf.Min(Screen.width,Screen.height)*.20f;input.TestBegin(from);input.TestMove(to);input.TestEnd(to);}
        static string Evidence(string file){string path=ReviewEvidence.FileFor(16,"../Validation/"+file);Directory.CreateDirectory(Path.GetDirectoryName(path));return path;}
        void Check(bool valid,string message){if(valid)checks.Add(message);else errors.Add(message);}
        void Log(string message,string trace,LogType type){if((type==LogType.Error||type==LogType.Assert||type==LogType.Exception)&&errors.Count<80)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;if(!prepared)return;if(wallet!=null)wallet.DeleteIsolatedTestData();Application.runInBackground=background;Application.targetFrameRate=oldRate;Time.captureDeltaTime=oldCapture;Time.timeScale=oldScale;}
    }
}
#endif
