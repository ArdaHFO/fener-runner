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
    public sealed class SpeedTransitionChecksV13 : MonoBehaviour
    {
        const string Flag="FenerRunner.SpeedTransitionsV13";
        [Serializable] sealed class Curve
        {public int hz;public float baseSpeed,finalSpeed,maximumPositiveFrameStep;public double distance;}
        [Serializable] sealed class Report
        {
            public bool passed,preferencesPreserved;public string utc,unityVersion;public string[] checks,errors;public Curve[] curves;
            public string scope="Finite native Unity tests of the production movement routine at 15/30/60/120 Hz, focus expiry/recovery, refresh/Encore, pause/retry, hard cap, real swept medallions and actual ordered shield/obstacle contacts. One live Game.Update pause/resume case. Coin/score/magnet/lane-sweep benefits begin on the next <=1/60 s substep; shield protection is ordered at actual contact time. No physical-device or exhaustive-input claim.";
        }
        static readonly FieldInfo SettingsField=typeof(RunnerGame).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic);
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        readonly List<Curve> curves=new List<Curve>();readonly Report report=new Report();
        RunnerGame game;RunnerPowerups powers;RunnerMotor motor;RunnerSettings originalSettings,testSettings;RunWallet wallet;
        bool oldBackground,prepared;float oldCapture,oldScale;int oldRate;string beforePreferences;

        [MenuItem("Fener Runner/84 - Verify focus recovery and ordered power contacts v13")]
        public static void StartChecks()
        {if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("V13 speed and contact checks").AddComponent<SpeedTransitionChecksV13>();}}
        IEnumerator Start()
        {
            oldBackground=Application.runInBackground;oldCapture=Time.captureDeltaTime;oldScale=Time.timeScale;oldRate=Application.targetFrameRate;
            Application.runInBackground=true;Application.targetFrameRate=120;Time.captureDeltaTime=1f/60f;prepared=true;
            Application.logMessageReceived+=Log;game=FindFirstObjectByType<RunnerGame>();
            if(game==null){Check(false,"RunnerGame exists");Finish();yield break;}
            game.AutomatedTest=true;game.enabled=false;beforePreferences=Preferences();
            wallet=game.UseWalletForTest("FenerRunner.Test.SpeedV13."+DateTime.UtcNow.Ticks+".");
            powers=game.Powerups;motor=game.Motor;originalSettings=game.Settings;testSettings=Instantiate(originalSettings);
            SettingsField.SetValue(game,testSettings);motor.Configure(testSettings);
            IEnumerator work=Run();
            while(true)
            {
                bool more;object current=null;
                try{more=work.MoveNext();if(more)current=work.Current;}catch(Exception error){errors.Add(error.ToString());break;}
                if(!more)break;yield return current;
            }
            game.enabled=false;game.ReturnToMenu();report.preferencesPreserved=Preferences()==beforePreferences;
            Check(report.preferencesPreserved,"Real wallet, upgrade levels and records remain unchanged");Finish();
        }
        void Begin(float speed=34f,bool coins=false)
        {
            game.enabled=false;testSettings.forwardSpeed=testSettings.maxSpeed=speed;
            game.Obstacles.Enabled=true;game.Tokens.Enabled=coins;powers.Enabled=true;game.Restart();
            powers.IsolatePickupsForTest();game.Obstacles.SetActive(false);
            if(coins)for(int i=0;i<game.Tokens.PoolCount;i++)game.Tokens.PlaceForTest(i,new Vector3(20,1,1000+i*4));
        }
        IEnumerator Run()
        {
            CheckEnvelope();CheckRateMatrix();CheckCombinations();CheckSweptPickups();CheckOrderedContacts();CheckCoinSnapshots();CheckExpiryAndFlightBoundaries();
            Begin();powers.Activate(PowerupKind.SlowMotion);powers.TickTimers(game.PowerupDuration(PowerupKind.SlowMotion)+.25f);
            game.AdvanceForTest(.01f);game.enabled=true;game.Pause();
            float factor=powers.SpeedFactor,remaining=powers.SpeedRecoveryRemaining;double distance=game.Distance;
            yield return new WaitForSecondsRealtime(.15f);
            Check(game.State==RunState.Paused&&factor==powers.SpeedFactor&&remaining==powers.SpeedRecoveryRemaining&&distance==game.Distance,
                "Live Game.Update pause freezes recovery, speed and distance");
            game.Resume();yield return null;yield return null;game.enabled=false;
            Check(game.State==RunState.Running&&powers.SpeedFactor>factor&&powers.SpeedFactor<1&&game.Distance>distance,
                "Live resume continues recovery from its paused point");
            game.Restart();Check(powers.SpeedFactor==1&&!powers.SlowMotionRecovering&&game.CurrentSpeed==testSettings.forwardSpeed,
                "Retry clears the recovery envelope and restores configured initial speed");
            powers.Activate(PowerupKind.SlowMotion);powers.TickTimers(5.2f);game.ReturnToMenu();game.Play();
            Check(powers.ActiveCount==0&&powers.SpeedFactor==1&&!powers.SlowMotionRecovering,"Menu and new run cannot inherit a slowdown tail");
        }
        void CheckEnvelope()
        {
            Begin();powers.Activate(PowerupKind.SlowMotion);powers.TickTimers(5f);
            Check(powers.SlowMotionRemaining==0&&powers.SlowMotionRecovering&&Mathf.Abs(powers.SpeedFactor-.68f)<.00001f,
                "Exact focus expiry begins recovery at the existing slow speed without a jump");
            powers.TickTimers(RunnerPowerups.FocusRecoverySeconds*.5f);
            Check(Mathf.Abs(powers.SpeedFactor-.84f)<.0001f,"Halfway through recovery reaches the bounded midpoint");
            powers.TickTimers(RunnerPowerups.FocusRecoverySeconds*.5f+.00001f);
            Check(powers.SpeedFactor==1&&!powers.SlowMotionRecovering,"Recovery reaches exact normal speed and finishes");
            Begin();powers.Activate(PowerupKind.SlowMotion);powers.TickTimers(5.25f);float crossed=powers.SpeedFactor;
            Begin();powers.Activate(PowerupKind.SlowMotion);powers.TickTimers(4.75f);powers.TickTimers(.5f);
            Check(Mathf.Abs(crossed-powers.SpeedFactor)<.00001f,"A step crossing expiry advances only its post-expiry fraction");
            powers.Activate(PowerupKind.SlowMotion);powers.Activate(PowerupKind.Encore);powers.TickTimers(5.2f);
            Check(powers.SlowMotionRemaining>1.79f&&powers.SpeedFactor==.68f&&!powers.SlowMotionRecovering,
                "Recollection cancels recovery and Encore extends the active slowdown");
            float held=powers.SlowMotionRemaining;powers.Activate(PowerupKind.SlowMotion);
            Check(powers.SlowMotionRemaining>=held,"Refreshing focus never shortens its remaining effect");
            powers.TickTimers(powers.SlowMotionRemaining+.2f);powers.Enabled=false;
            Check(powers.SpeedFactor==1&&powers.SpeedRecoveryRemaining==0&&powers.ActiveCount==0,"Disabling powers explicitly clears recovery state");
        }
        void CheckRateMatrix()
        {
            foreach(float speed in new[]{18f,34f,RunTempo.HardCap})
            {
                double min=double.MaxValue,max=double.MinValue;
                foreach(int hz in new[]{15,30,60,120})
                {
                    Begin(speed);powers.Activate(PowerupKind.SlowMotion);float previous=speed*.68f;bool bounded=true;
                    var curve=new Curve{hz=hz,baseSpeed=speed};
                    for(int frame=0;frame<Mathf.RoundToInt(6.6f*hz);frame++)
                    {
                        game.AdvanceForTest(1f/hz);float current=game.CurrentSpeed;
                        curve.maximumPositiveFrameStep=Mathf.Max(curve.maximumPositiveFrameStep,current-previous);
                        bounded&=current>=speed*.68f-.001f&&current<=speed+.001f&&current>=previous-.001f;
                        previous=current;
                    }
                    curve.finalSpeed=game.CurrentSpeed;curve.distance=game.Distance;curves.Add(curve);
                    min=Math.Min(min,curve.distance);max=Math.Max(max,curve.distance);
                    float maximumStep=speed*.32f*1.5f/RunnerPowerups.FocusRecoverySeconds/hz+.015f;
                    Check(bounded&&curve.maximumPositiveFrameStep<=maximumStep&&Mathf.Abs(curve.finalSpeed-speed)<.001f,
                        speed+" m/s at "+hz+" Hz returns smoothly and monotonically without overshoot");
                }
                Check(max-min<.13,"Actual travelled distance during focus/recovery agrees within 13 cm across frame rates at "+speed+" m/s");
            }
        }
        void CheckCombinations()
        {
            Begin(34);powers.Activate(PowerupKind.Dash);powers.Activate(PowerupKind.FeverRush);powers.Activate(PowerupKind.SlowMotion);
            for(int i=0;i<8;i++)game.Flow.CleanPass();game.AdvanceForTest(.1f);
            Check(Mathf.Abs(game.CurrentSpeed-42*.68f)<.001f,"Focus applies to the combined dash, fever and flow speed");
            bool safe=true;for(int i=0;i<420;i++){game.AdvanceForTest(1f/60);safe&=game.CurrentSpeed<=RunTempo.HardCap&&game.CurrentSpeed>0;}
            Check(safe&&!powers.SlowMotionRecovering,"Combined boost expiry and recovery always preserve the hard speed cap");
        }
        void CheckSweptPickups()
        {
            foreach(string motion in new[]{"straight","slide","jump","diagonal","reversal","flight","adjacent lane","above","origin shift"})
            {
                Begin(42);Vector3 centre;
                if(motion=="slide")motor.TrySlide();
                if(motion=="jump"){motor.TryJump();motor.Tick(testSettings.JumpRiseSeconds);}
                if(motion=="flight"){motor.SetFlight(true);for(int i=0;i<60;i++)motor.Tick(1f/60);}
                if(motion=="diagonal")motor.ChangeLane(1);
                if(motion=="reversal"){motor.ChangeLane(1);motor.Tick(.07f);motor.ChangeLane(-1);}
                centre=motor.transform.position+new Vector3(0,1.3f,3);
                if(motion=="diagonal")centre.x=testSettings.laneSpacing*.5f;
                if(motion=="reversal")centre.x=motor.transform.position.x*.5f;
                if(motion=="adjacent lane")centre.x=testSettings.laneSpacing;
                if(motion=="above")centre.y=7;
                powers.PlaceForTest(PowerupKind.Magnet,centre);int count=0;Action<PowerupKind> heard=k=>count++;powers.Collected+=heard;
                try
                {
                    powers.BeginMove();
                    if(motion=="origin shift"){powers.ShiftOrigin(500);motor.ShiftOrigin(500);}
                    motor.Tick(6f/42);powers.TickPickups(6f/42);powers.TickPickups(0);
                }
                finally{powers.Collected-=heard;}
                bool expected=motion!="adjacent lane"&&motion!="above";
                Check(count==(expected?1:0)&&powers.MagnetActive==expected,
                    "Six metre swept "+motion+" medallion contact has correct collection and no duplicate event");
            }
        }
        RunnerObstacle ObstacleAtEntry(float distance,bool extra=false,ObstacleKind kind=ObstacleKind.JumpOver)
        {
            float z=motor.transform.position.z;
            RunnerObstacle obstacle=extra?game.Obstacles.AddTestObstacle(kind,z+4):game.Obstacles.ArrangeTest(kind,z+4);
            float front=float.PositiveInfinity;foreach(var box in obstacle.CollisionBoxes)if(box.enabled)front=Mathf.Min(front,box.bounds.min.z);
            obstacle.transform.position+=Vector3.forward*(z+motor.Capsule.radius+distance-front);Physics.SyncTransforms();return obstacle;
        }
        void ShieldAtEntry(float distance)
        {powers.PlaceForTest(PowerupKind.Shield,new Vector3(0,1.3f,motor.transform.position.z+motor.Capsule.radius+.72f+distance));}
        void CheckOrderedContacts()
        {
            foreach(int hz in new[]{15,30,60,120})
            {
                float travel=42f/Mathf.Max(60,hz);
                foreach(bool before in new[]{true,false})
                {
                    Begin(42);ObstacleAtEntry(travel*.55f);ShieldAtEntry(travel*(before?.15f:.9f));
                    int count=0;Action<PowerupKind> heard=k=>count++;powers.Collected+=heard;
                    try{game.AdvanceForTest(1f/hz);}finally{powers.Collected-=heard;}
                    Check(before?game.State==RunState.Running&&count==1&&!powers.ShieldActive&&powers.HitGraceRemaining>0:
                        game.State==RunState.Finished&&count==0&&!powers.ShieldActive,
                        hz+" Hz shield "+(before?"before":"after")+" the actual obstacle obeys contact order");
                }
            }
            Begin(42);ObstacleAtEntry(.25f);ObstacleAtEntry(.5f,true);ShieldAtEntry(.05f);game.AdvanceForTest(1f/60);
            Check(game.State==RunState.Finished&&!powers.ShieldActive&&game.Obstacles.ContactCount==2,
                "One swept shield absorbs the first of two distinct obstacles but cannot hide the second");
            Begin(42);ObstacleAtEntry(.2f);powers.Activate(PowerupKind.Shield);powers.TickTimers(powers.ShieldRemaining-.02f);game.AdvanceForTest(1f/15);
            Check(game.State==RunState.Running&&!powers.ShieldActive&&powers.HitGraceRemaining>0,
                "A shield alive at the first physics substep is not expired by the entire 15 Hz frame in advance");
            Begin(42,true);ObstacleAtEntry(.35f);ShieldAtEntry(.6f);
            float radius=motor.Capsule.radius+game.Tokens.PickupRadius,z=motor.transform.position.z;
            game.Tokens.PlaceForTest(0,new Vector3(0,1.1f,z+radius+.1f));game.Tokens.PlaceForTest(1,new Vector3(0,1.1f,z+radius+.6f));
            game.AdvanceForTest(1f/60);
            Check(game.State==RunState.Finished&&game.Coins==1&&game.Tokens.CollectedForTest(0)&&!game.Tokens.CollectedForTest(1)&&!powers.ShieldActive,
                "Fatal cutoff credits the earlier coin and rejects both the later coin and later shield");
        }
        void CheckCoinSnapshots()
        {
            Begin(42,true);float z=motor.transform.position.z;
            powers.PlaceForTest(PowerupKind.DoubleCoins,new Vector3(0,1.3f,z+motor.Capsule.radius+.72f+.05f));
            game.Tokens.PlaceForTest(0,new Vector3(0,1.1f,z+motor.Capsule.radius+game.Tokens.PickupRadius+.4f));
            game.AdvanceForTest(1f/60);
            Check(game.Coins==1&&powers.CoinMultiplier==2,"New DoubleCoins does not retroactively multiply its collection substep");
            game.Tokens.PlaceForTest(1,motor.transform.position+Vector3.up*1.1f);game.AdvanceForTest(1f/60);
            Check(game.Coins==3,"DoubleCoins pays double from the next bounded substep");
            Begin(42,true);z=motor.transform.position.z;
            powers.PlaceForTest(PowerupKind.LaneSweep,new Vector3(0,1.3f,z+motor.Capsule.radius+.72f+.05f));
            game.Tokens.PlaceForTest(0,new Vector3(testSettings.laneSpacing,1.1f,z+.4f));game.AdvanceForTest(1f/60);
            Check(game.Coins==0&&powers.LaneSweepActive,"New lane sweep does not pull a neighbouring coin retroactively");
            game.AdvanceForTest(1f/60);Check(game.Coins==1,"Lane sweep starts collecting on the next bounded substep");
        }
        void CheckExpiryAndFlightBoundaries()
        {
            foreach(float contactSeconds in new[]{.002f,.010f})
            {
                Begin(42);ObstacleAtEntry(42*contactSeconds);
                powers.Activate(PowerupKind.Shield);powers.TickTimers(powers.ShieldRemaining-.005f);
                game.AdvanceForTest(1f/60);
                bool beforeExpiry=contactSeconds<.005f;
                Check(beforeExpiry?game.State==RunState.Running&&!powers.ShieldActive&&powers.HitGraceRemaining>0:
                    game.State==RunState.Finished&&!powers.ShieldActive&&powers.HitGraceRemaining==0,
                    "Shield with 5 ms remaining "+(beforeExpiry?"protects a 2 ms":"rejects a 10 ms")+" actual obstacle contact in the same physics step");
            }
            Begin(42);ObstacleAtEntry(42*.0006f);ObstacleAtEntry(42*.0028f,true);ObstacleAtEntry(42*.0042f,true);
            powers.Activate(PowerupKind.Shield);powers.TickTimers(powers.ShieldRemaining-.005f);ShieldAtEntry(42*.0018f);
            int picked=0;Action<PowerupKind> heard=k=>picked++;powers.Collected+=heard;
            try{game.AdvanceForTest(1f/60);}finally{powers.Collected-=heard;}
            Check(game.State==RunState.Finished&&picked==1&&game.Obstacles.ContactCount==3&&!powers.ShieldActive,
                "Historical shield then a new swept shield each protect once; the third distinct contact is fatal before the old expiry");

            Begin(42,true);motor.TrySlide();float z=motor.transform.position.z;
            powers.PlaceForTest(PowerupKind.Flight,new Vector3(0,1.3f,z+motor.Capsule.radius+.72f+.05f));
            game.Tokens.PlaceForTest(0,new Vector3(0,2.22f,z+.6f));
            game.AdvanceForTest(1f/60);
            Check(powers.FlightActive&&!motor.Flying&&motor.Sliding&&Mathf.Abs(motor.Capsule.height-1.5f)<.001f&&game.Coins==0,
                "Flight collected during a slide preserves the low contact capsule and cannot retroactively collect a high coin");
            game.AdvanceForTest(1f/60);
            Check(motor.Flying&&motor.FlightRequested&&motor.Capsule.height>2&&game.Coins==1&&!powers.IsProtected,
                "The next substep begins real flight with a matching standing capsule and collects the now reachable coin without immunity");

            Begin(42);motor.TrySlide();z=motor.transform.position.z;
            ObstacleAtEntry(.35f,false,ObstacleKind.SlideUnder);
            powers.PlaceForTest(PowerupKind.Flight,new Vector3(0,1.3f,z+motor.Capsule.radius+.72f+.05f));
            game.AdvanceForTest(1f/60);
            Check(game.State==RunState.Running&&powers.FlightActive&&motor.Sliding&&!motor.Flying,
                "A sliding flight pickup keeps the actual low capsule through its same-step overhead gate query");
            game.AdvanceForTest(1f/60);
            Check(game.State==RunState.Finished&&!powers.IsProtected,
                "Standing flight ascent into an overhead gate remains a real fatal collision on the next substep");
            Begin(42);motor.TrySlide();powers.Activate(PowerupKind.Flight);
            Check(motor.Flying&&motor.FlightRequested&&!motor.Sliding&&motor.Capsule.height>2,
                "Direct Activate Flight retains its existing immediate motor transition API");
        }
        static string Preferences()
        {
            string value=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0)+"|"+PlayerPrefs.GetInt("FenerRunner.BestScore",0)+"|"+PlayerPrefs.GetFloat("FenerRunner.Best",0).ToString("R");
            for(int i=0;i<3;i++)value+="|"+PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Upgrade."+i,0);return value;
        }
        void Check(bool ok,string label){checks.Add((ok?"PASS ":"FAIL ")+label);if(!ok)errors.Add(label);}
        void Log(string message,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(message);}
        void Finish()
        {
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;
            report.checks=checks.ToArray();report.errors=errors.ToArray();report.curves=curves.ToArray();Directory.CreateDirectory("../Validation");
            File.WriteAllText(ReviewEvidence.FileFor(13,"../Validation/unity_speed_transition_v13.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("V13 speed/contact "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;if(prepared){Application.runInBackground=oldBackground;Application.targetFrameRate=oldRate;Time.captureDeltaTime=oldCapture;Time.timeScale=oldScale;}
            if(game!=null&&originalSettings!=null){SettingsField.SetValue(game,originalSettings);motor.Configure(originalSettings);}
            if(testSettings!=null)Destroy(testSettings);if(wallet!=null)wallet.DeleteIsolatedTestData();
        }
    }
}
#endif
