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
    public sealed class TempoMotionChecksV12:MonoBehaviour
    {
        [Serializable] sealed class SpeedSample
        {public float speed,cadence,fov,maximumForwardOffsetError,maximumFovStep,lowerPositionError,lowerRotationError;}
        [Serializable] sealed class Report
        {
            public bool passed,preferencesPreserved;public string utc,unityVersion;public string[] checks,errors;public SpeedSample[] speeds;
            public float combinedJumpApex;public int preservedPowerRefreshes;
            public string scope="Actual Unity Editor motor, Animator and camera at18/26/34/42m/s, matched-phase lower-bone comparison, airborne power combinations, buffered jump, fast fall, pause and real overlapping-obstacle death. Isolated test wallet. These finite native regressions are not phone FPS, touchscreen or exhaustive input-sequence proof.";
        }
        const string Flag="FenerRunner.TempoMotionV12";
        readonly List<string> checks=new List<string>(),errors=new List<string>();readonly List<SpeedSample> samples=new List<SpeedSample>();
        readonly Report report=new Report();RunnerGame game;RunnerAnimation motion;RunnerMotor motor;RunnerPowerups powers;FollowCamera follow;Camera lens;RunWallet wallet;
        GameObject reference;Transform[] bones,referenceBones;AnimationClip run;
        bool oldBackground;int oldRate,oldWallet,oldScore;float oldScale,oldBest;
        const float Step=1f/60;

        [MenuItem("Fener Runner/73 - Verify high speed motion and power refresh")]
        public static void StartChecks()
        {if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before tempo motion checks.");SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("V12 tempo and motion checks").AddComponent<TempoMotionChecksV12>();}}
        IEnumerator Start()
        {
            oldBackground=Application.runInBackground;oldRate=Time.captureFramerate;oldScale=Time.timeScale;Application.runInBackground=true;Time.captureFramerate=60;
            oldWallet=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0);oldScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);oldBest=PlayerPrefs.GetFloat("FenerRunner.Best",0);
            Application.logMessageReceived+=Log;game=FindFirstObjectByType<RunnerGame>();
            if(game==null){Check(false,"RunnerGame exists");Finish();yield break;}
            game.AutomatedTest=true;wallet=game.UseWalletForTest("FenerRunner.Test.TempoMotionV12."+DateTime.UtcNow.Ticks+".");
            motion=game.Animation;motor=game.Motor;powers=game.Powerups;lens=Camera.main;follow=lens.GetComponent<FollowCamera>();
            run=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/FenerRunner/Generated/Animations/Run.anim");
            Reset();reference=Instantiate(motion.Animator.gameObject,motion.Animator.transform.parent);reference.name="V12 invisible matching Run reference";
            reference.GetComponent<Animator>().enabled=false;foreach(var r in reference.GetComponentsInChildren<Renderer>())r.enabled=false;
            bones=motion.Animator.GetComponentInChildren<SkinnedMeshRenderer>().bones;referenceBones=reference.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            foreach(float speed in new[]{18f,26f,34f,RunTempo.HardCap})yield return CheckSpeed(speed);
            Check(samples.Zip(samples.Skip(1),(a,b)=>b.cadence>a.cadence&&b.fov>a.fov).All(v=>v),"Cadence and field of view progress monotonically through the new speed range");
            Check(samples.Last().cadence>1.7f&&samples.Last().cadence<=1.8001f&&Mathf.Abs(samples.Last().fov-68)<.15f,"42m/s reaches the bounded sprint cadence and comfortable68-degree field of view");
            game.Pause();yield return new WaitForEndOfFrame();Vector3 pausedPosition=lens.transform.position;Quaternion pausedRotation=lens.transform.rotation;float pausedFov=lens.fieldOfView;
            yield return new WaitForSecondsRealtime(.2f);
            Check(lens.transform.position==pausedPosition&&Quaternion.Angle(lens.transform.rotation,pausedRotation)<.001f&&lens.fieldOfView==pausedFov,"Running pause freezes camera framing and field of view");game.Resume();
            yield return CheckAirCombinations();CheckPowerRefreshes();yield return CheckFlightExit();yield return CheckDeathLifecycle();
            game.ReturnToMenu();
            report.preferencesPreserved=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0)==oldWallet&&PlayerPrefs.GetInt("FenerRunner.BestScore",0)==oldScore&&PlayerPrefs.GetFloat("FenerRunner.Best",0)==oldBest;
            Check(report.preferencesPreserved,"Real wallet and best records remain unchanged");Finish();
        }
        void Reset()
        {game.Restart();game.enabled=false;game.Obstacles.SetActive(false);game.Tokens.Enabled=false;powers.Enabled=true;powers.IsolatePickupsForTest();motion.TurnAllowed=false;motor.Speed=RunTempo.HardCap;}
        void Move(float dt=Step)
        {motor.Tick(dt);game.Course.Tick(motor.transform.position.z);motion.Tick(dt);}
        IEnumerator CheckSpeed(float speed)
        {
            motor.Speed=speed;var sample=new SpeedSample{speed=speed};samples.Add(sample);float previous=lens.fieldOfView;
            string[] lower={"root","pelvis","thigh.L","shin.L","foot.L","thigh.R","shin.R","foot.R"};
            for(int f=0;f<100;f++)
            {
                Move();yield return new WaitForEndOfFrame();
                sample.maximumForwardOffsetError=Mathf.Max(sample.maximumForwardOffsetError,Mathf.Abs(lens.transform.position.z-motor.transform.position.z-game.Settings.cameraOffset.z));
                sample.maximumFovStep=Mathf.Max(sample.maximumFovStep,Mathf.Abs(lens.fieldOfView-previous));previous=lens.fieldOfView;
                var state=motion.Animator.GetCurrentAnimatorStateInfo(0);run.SampleAnimation(reference,Mathf.Repeat(state.normalizedTime,1)*run.length);
                foreach(string name in lower)
                {
                    Transform a=bones.Single(b=>b.name==name),b=referenceBones.Single(v=>v.name==name);
                    sample.lowerPositionError=Mathf.Max(sample.lowerPositionError,Vector3.Distance(a.localPosition,b.localPosition));
                    sample.lowerRotationError=Mathf.Max(sample.lowerRotationError,Quaternion.Angle(a.localRotation,b.localRotation));
                }
            }
            sample.cadence=motion.RunCadence;sample.fov=lens.fieldOfView;
            Check(sample.maximumForwardOffsetError<.003f,speed+"m/s preserves exact configured forward camera distance");
            Check(sample.maximumFovStep<.6f&&sample.fov>=57.99f&&sample.fov<=68.01f,speed+"m/s changes camera field of view smoothly within58–68 degrees");
            Check(sample.lowerPositionError<.002f&&sample.lowerRotationError<.15f,speed+"m/s preserves authored pelvis, knee and foot contact curves at the same clip phase");
        }
        IEnumerator CheckAirCombinations()
        {
            Reset();powers.Activate(PowerupKind.SuperJump);powers.Activate(PowerupKind.AirStep);motor.JumpHeightMultiplier=powers.JumpMultiplier;motor.AirStepEnabled=powers.AirStepActive;
            Check(motor.TryJump(),"Combined SuperJump and AirStep begins an actual jump");
            for(int i=0;i<18;i++){Move();yield return new WaitForEndOfFrame();}
            bool second=motor.TryJump(),third=motor.TryJump();
            Check(second&&!third&&motor.JumpCount==2&&motor.AirStepUsed,"AirStep adds exactly one airborne impulse during SuperJump at42m/s");
            for(int i=0;i<120&&!motor.Grounded;i++){Move();report.combinedJumpApex=Mathf.Max(report.combinedJumpApex,motor.transform.position.y);yield return new WaitForEndOfFrame();}
            Check(motor.Grounded&&!motor.AirStepUsed&&report.combinedJumpApex>4&&report.combinedJumpApex<=5.04f&&Mathf.Abs(motor.Capsule.height-2.05f)<.001f,"Combined jump lands with bounded height and restored standing collider");
            Reset();motor.TryJump();for(int i=0;i<10;i++){Move();yield return null;}
            Check(motor.TrySlide()&&!motor.TrySlide()&&motor.FastFalling,"A downward command at42m/s accepts one fast fall and rejects a duplicate");
            for(int i=0;i<90&&!motor.Grounded;i++){Move();yield return null;}
            Check(motor.Grounded&&motor.Sliding&&!motor.FastFalling&&motor.Capsule.height<=1.501f,"Fast fall enters the existing low landing slide at42m/s");
            Reset();motor.TryJump();Move();motor.TryJump();
            for(int i=0;i<90&&!(motor.VerticalVelocity<0&&motor.transform.position.y<.45f);i++){Move();yield return null;}
            bool buffered=motor.TryJump();int count=motor.JumpCount;
            for(int i=0;i<12&&motor.JumpCount==count;i++){Move();yield return null;}
            Check(!buffered&&motor.JumpCount==count+1&&!motor.Grounded,"After spending the air jump, an early upward swipe during descent triggers once on actual ground contact at42m/s");
        }
        void CheckPowerRefreshes()
        {
            foreach(PowerupKind kind in Enum.GetValues(typeof(PowerupKind)))
            {
                if(kind==PowerupKind.Encore)continue;
                Reset();powers.Activate(kind);powers.Activate(PowerupKind.Encore);powers.TickTimers(.1f);float remaining=powers.Remaining(kind);powers.Activate(kind);
                bool ok=powers.Remaining(kind)>=remaining-.00001f;if(ok)report.preservedPowerRefreshes++;
                Check(ok,"Refreshing Encore-extended "+kind+" never shortens its remaining effect");
            }
            Reset();powers.Activate(PowerupKind.Rhythm);powers.RewardCleanPasses(2);powers.Activate(PowerupKind.Encore);powers.Activate(PowerupKind.Rhythm);
            Check(powers.RhythmChain==2&&powers.RewardCleanPasses(1)==150,"Refreshing active Rhythm preserves the clean-pass chain");
            Reset();int volleys=0;Action<int> heard=n=>volleys++;powers.CoinRainVolley+=heard;
            powers.Activate(PowerupKind.CoinRain);powers.TickTimers(.9f);powers.Activate(PowerupKind.CoinRain);powers.TickTimers(.11f);powers.CoinRainVolley-=heard;
            Check(volleys==1,"Refreshing CoinRain preserves the imminent next volley rather than delaying it");
        }
        IEnumerator CheckFlightExit()
        {
            Reset();powers.Activate(PowerupKind.Flight);powers.Activate(PowerupKind.Shield);
            for(int i=0;i<75;i++){Move();yield return null;}
            powers.TickTimers(game.PowerupDuration(PowerupKind.Flight)+.01f);
            bool protectedDescent=true;for(int i=0;i<140&&!motor.Grounded;i++)
            {protectedDescent&=powers.IsProtected&&powers.TryAbsorbHit()&&powers.ShieldActive;Move();powers.TickTimers(Step);yield return null;}
            Check(protectedDescent&&motor.Grounded&&!motor.Flying&&powers.LandingGraceRemaining>0,"Flight expiry descends safely at42m/s and grants the existing explicit landing grace");
            Check(powers.ShieldActive,"Controlled flight descent does not consume an independently active shield");
        }
        IEnumerator CheckDeathLifecycle()
        {
            Reset();powers.Activate(PowerupKind.Shield);powers.Activate(PowerupKind.Magnet);powers.Activate(PowerupKind.SuperJump);powers.TickTimers(.01f);
            float z=motor.transform.position.z+2;game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,z);game.Obstacles.AddTestObstacle(ObstacleKind.SlideUnder,z+.05f);game.enabled=true;
            float deadline=Time.realtimeSinceStartup+2;while(game.State==RunState.Running&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return new WaitForEndOfFrame();
            Check(game.State==RunState.Finished&&!powers.ShieldActive&&motion.DeathPlaying,"Two actual overlapping obstacles consume the shield then begin one fatal presentation");
            // Effect roots are exactly the objects managed by RunnerPowerups.
            var fields=typeof(RunnerPowerups).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            bool hidden=true;foreach(var field in fields)
            {
                if(field.Name=="flightVisual"||field.Name=="shieldVisual")hidden&=!((GameObject)field.GetValue(powers)).activeSelf;
                if(field.Name=="abilityVisuals")foreach(var effect in (GameObject[])field.GetValue(powers))hidden&=!effect.activeSelf;
            }
            Check(hidden,"Fatal presentation hides consumed shield and all remaining power effects on the frozen root");
            motion.SetPaused(true);yield return new WaitForEndOfFrame();Vector3 position=lens.transform.position;Quaternion rotation=lens.transform.rotation;float progress=motion.DeathProgress,fov=lens.fieldOfView;
            yield return new WaitForSecondsRealtime(.2f);
            Check(motion.DeathPresentationPaused&&progress==motion.DeathProgress&&lens.transform.position==position&&Quaternion.Angle(rotation,lens.transform.rotation)<.001f&&lens.fieldOfView==fov,"Death pause freezes both unscaled character presentation and camera");
            motion.SetPaused(false);yield return new WaitForSecondsRealtime(.12f);
            Check(!motion.DeathPresentationPaused&&motion.DeathProgress>progress,"Death presentation resumes from its paused point");
            Reset();yield return new WaitForSecondsRealtime(1.2f);
            Check(game.State==RunState.Running&&!motion.DeathComplete&&!motion.DeathPresentationPaused&&!game.Interface.resultPanel.activeSelf,"Retry cancels paused/resumed death and its pending result");
            for(int i=0;i<4;i++){game.ReturnToMenu();game.Restart();}
            Check(game.State==RunState.Running&&motion.CurrentClip=="Run"&&motor.Grounded&&!motor.Flying&&!motor.Sliding&&powers.ActiveCount==0,"Repeated menu and retry cycles clear power, flight, slide and death state");
        }
        void Check(bool ok,string label){if(ok)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType kind){if(kind==LogType.Exception||kind==LogType.Error||kind==LogType.Assert)errors.Add(message);}
        void Finish()
        {
            report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.passed=errors.Count==0;report.checks=checks.ToArray();report.errors=errors.ToArray();report.speeds=samples.ToArray();
            Directory.CreateDirectory("../Validation");File.WriteAllText(ReviewEvidence.FileFor(12,"../Validation/unity_tempo_motion_v12.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Tempo motion v12 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void OnDestroy()
        {Application.logMessageReceived-=Log;Application.runInBackground=oldBackground;Time.captureFramerate=oldRate;Time.timeScale=oldScale;if(reference!=null)Destroy(reference);if(wallet!=null)wallet.DeleteIsolatedTestData();}
    }
}
#endif
