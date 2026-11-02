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
    public sealed class TempoCourseChecksV12:MonoBehaviour
    {
        const string SessionKey="FenerRunner.TempoCourseChecksV12";
        static readonly FieldInfo SettingsField=typeof(RunnerGame).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic);
        [Serializable] sealed class RouteRun
        {
            public int seed,commandHz,physicsSteps,jumps,slides,laneChanges,passedRows,originShifts;
            public float speed;
            public float minimumSpeed=float.MaxValue,maximumSpeed;
            public double distance,metresAtHardCap;
            public string profile;
            public bool liveGameUpdate,survived,physicalCuesMatch=true;
            public string hit="";
        }
        [Serializable] sealed class Report
        {
            public bool passed,sourceSettingsUnchanged;
            public string utc,unityVersion;
            public string[] checks,errors;
            public RouteRun[] runs;
            public int normalPool,finalPool,distinctObservedLabels;
            public float wallSeconds;
            public string scope="Nine deterministic native CharacterController/collider routes, each4200m, at15/30/60Hz command sampling with production1/60s maximum physics steps. Profiles are baseline18→34, maximum reachable boosted26→42, and baseline until1800m then sustained42. Cue lanes/actions are cross-checked against actual active prefabs and real contacts decide survival. An additional live Game.Update progression route crosses the220m and800m acceleration milestones. Protected-contact clean-row accounting is tested on a real generated row. Source settings and production records/wallet preserved without preference writes. Finite Unity Editor coverage, not physical-phone validation.";
        }
        RunnerGame game;RunnerSettings originalSettings,testSettings;string originalSettingsJson;
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        readonly List<RouteRun> runs=new List<RouteRun>();readonly HashSet<string> labels=new HashSet<string>();
        readonly Report report=new Report();
        bool prepared,previousBackground;
        float previousCaptureDelta,began;int previousTargetRate;
        string prefsBefore;

        [MenuItem("Fener Runner/56 - Verify v12 progressive and maximum-speed routes")]
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
            SessionState.SetBool(SessionKey,false);new GameObject("Varied course checks v0.12").AddComponent<TempoCourseChecksV12>();
        }
        IEnumerator Start()
        {
            began=Time.realtimeSinceStartup;game=FindFirstObjectByType<RunnerGame>();
            previousBackground=Application.runInBackground;previousTargetRate=Application.targetFrameRate;previousCaptureDelta=Time.captureDeltaTime;
            game.AutomatedTest=true;prefsBefore=PreferenceSnapshot();
            Application.runInBackground=true;Application.targetFrameRate=120;Time.captureDeltaTime=1f/30f;
            prepared=true;Application.logMessageReceived+=Log;
            var routine=RunScenarios();
            while(true)
            {
                bool more;object wait=null;
                try{more=routine.MoveNext();if(more)wait=routine.Current;}
                catch(Exception exception){errors.Add(exception.ToString());break;}
                if(!more)break;yield return wait;
            }
            if(originalSettings!=null)
            {report.sourceSettingsUnchanged=JsonUtility.ToJson(originalSettings)==originalSettingsJson;Check(report.sourceSettingsUnchanged,"Original settings asset remains unchanged");}
            Check(PreferenceSnapshot()==prefsBefore,"Earned wallet, upgrades and records remain unchanged without preference writes");
            report.checks=checks.ToArray();report.errors=errors.ToArray();report.runs=runs.ToArray();report.passed=errors.Count==0;
            report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.wallSeconds=Time.realtimeSinceStartup-began;
            Directory.CreateDirectory("../Validation");File.WriteAllText(ReviewEvidence.FileFor(12,"../Validation/unity_tempo_course_v12.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Course v12 checks "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));
            EditorApplication.isPlaying=false;
        }
        void ResetRun(int seed,bool live)
        {
            game.enabled=false;
            game.Powerups.Enabled=false;game.Tokens.Enabled=false;game.Obstacles.Enabled=true;game.Obstacles.UseSeedForTest(seed);
            game.Play();game.Motor.Speed=RunTempo.BaseSpeedAtDistance(0);game.enabled=live;
        }
        static float SpeedFor(RouteRun run)
        {
            if(run.profile=="boosted envelope")return RunTempo.CeilingAtDistance(run.distance);
            if(run.profile=="sustained late maximum"&&run.distance>=1800)return RunTempo.HardCap;
            return RunTempo.BaseSpeedAtDistance(run.distance);
        }
        bool IssueCommands(RouteRun run,ref int actioned)
        {
            float z=game.Motor.transform.position.z;
            if(!game.Obstacles.TryGetNextRoute(z,out CourseRoute route))return false;
            labels.Add(route.Pattern);
            RouteAction actual=RouteAction.None;
            foreach(var item in game.Obstacles.Items)
            {
                if(!item.gameObject.activeInHierarchy||Mathf.Abs(item.transform.position.z-route.Z)>.02f||
                    Mathf.Abs(item.transform.position.x-route.Lane*game.Settings.laneSpacing)>.02f)continue;
                actual=item.kind==ObstacleKind.JumpOver?RouteAction.Jump:RouteAction.Slide;break;
            }
            if(actual!=route.Action){run.physicalCuesMatch=false;run.hit="Route cue differs from its active prefab";return false;}
            float ahead=route.Z-z;
            if(ahead<=CoursePatternGenerator.LaneChangeLead&&game.Motor.Lane!=route.Lane)
            {
                game.HandleCommand(game.Motor.Lane<route.Lane?RunnerCommand.Right:RunnerCommand.Left);run.laneChanges++;
            }
            float lead=route.Action==RouteAction.Jump?game.Settings.JumpRiseSeconds:.42f;
            if(route.Sequence!=actioned&&ahead>0&&ahead<=run.speed*lead&&game.Motor.Lane==route.Lane)
            {
                if(actual==RouteAction.Jump){game.HandleCommand(RunnerCommand.Jump);run.jumps++;}
                if(actual==RouteAction.Slide){game.HandleCommand(RunnerCommand.Slide);run.slides++;}
                actioned=route.Sequence;
            }
            return true;
        }
        IEnumerator RunScenarios()
        {
            if(game==null||game.Powerups==null||SettingsField==null)throw new InvalidOperationException("Built v0.12 scene required.");
            game.AutomatedTest=true;originalSettings=game.Settings;originalSettingsJson=JsonUtility.ToJson(originalSettings);
            testSettings=Instantiate(originalSettings);testSettings.name="Physical course check settings (runtime clone)";
            SettingsField.SetValue(game,testSettings);game.Motor.Configure(testSettings);
            Check(Mathf.Abs(testSettings.jumpHeight-2.65f)<.001f&&Mathf.Abs(testSettings.jumpAirSeconds-.72f)<.001f&&
                Mathf.Abs(testSettings.jumpAscentFraction-.56f)<.001f&&Mathf.Abs(testSettings.slideSeconds-.85f)<.001f&&
                Mathf.Abs(testSettings.laneChangeSeconds-.14f)<.001f&&Mathf.Abs(testSettings.laneSpacing-3.3f)<.001f,
                "Actual motor settings match the constructive audit timing envelope");
            report.normalPool=game.Obstacles.PoolCount;
            Check(RunTempo.BaseSpeedAtDistance(0)==18&&RunTempo.BaseSpeedAtDistance(220)==24&&RunTempo.BaseSpeedAtDistance(800)==30&&RunTempo.BaseSpeedAtDistance(1800)==34&&RunTempo.HardCap==42,
                "Shared speed contract reaches every requested milestone and the42m/s hard cap");
            int[] seeds={1907,5521,27011,104729,777777,90210,42,314159,8881};
            string[] profiles={"baseline progression","boosted envelope","sustained late maximum"};int[] frequencies={15,30,60};int caseIndex=0;
            foreach(string profile in profiles)foreach(int hz in frequencies)
            {
                var run=new RouteRun{seed=seeds[caseIndex++],profile=profile,commandHz=hz};
                ResetRun(run.seed,false);int actioned=-1,frames=0;
                while(run.distance<4200&&run.hit.Length==0&&frames<24000)
                {
                    run.speed=SpeedFor(run);game.Motor.Speed=run.speed;
                    run.minimumSpeed=Mathf.Min(run.minimumSpeed,run.speed);run.maximumSpeed=Mathf.Max(run.maximumSpeed,run.speed);
                    if(!IssueCommands(run,ref actioned)){if(run.hit.Length==0)run.hit="No readable upcoming row";break;}
                    float remaining=1f/hz;
                    while(remaining>.000001f)
                    {
                        float dt=Mathf.Min(remaining,1f/60f);
                        game.Obstacles.BeginMove(game.Motor);float moved=game.Motor.Tick(dt);run.distance+=moved;run.physicsSteps++;
                        if(run.speed>=RunTempo.HardCap-.001f)run.metresAtHardCap+=moved;
                        game.Course.Tick(game.Motor.transform.position.z);
                        RunnerObstacle hit=game.Obstacles.Check(game.Motor);
                        if(hit!=null){run.hit=hit.kind+" at localZ"+game.Motor.transform.position.z.ToString("F3");break;}
                        remaining-=dt;
                    }
                    if(game.Motor.transform.position.z>=game.Settings.originShiftDistance)
                    {
                        float shift=Mathf.Floor(game.Motor.transform.position.z/game.Settings.chunkLength)*game.Settings.chunkLength;
                        game.Course.ShiftOrigin(shift);game.Obstacles.ShiftOrigin(shift);game.Tokens.ShiftOrigin(shift);
                        game.Powerups.ShiftOrigin(shift);game.Motor.ShiftOrigin(shift);run.originShifts++;
                    }
                    if(++frames%500==0)yield return null;
                }
                run.passedRows=game.Obstacles.Passed;run.survived=run.hit.Length==0&&run.distance>=4200;runs.Add(run);
                Check(run.survived&&run.physicalCuesMatch&&run.jumps>0&&run.slides>0&&run.laneChanges>0&&run.passedRows>=65&&run.originShifts>=4,
                    "Seed "+run.seed+" real-collider route survives4200m with "+profile+" at "+hz+"Hz"+
                    (run.hit.Length>0?" ("+run.hit+")":""));
                Check(run.maximumSpeed>=(profile=="baseline progression"?34:42)&&
                    (profile=="baseline progression"||run.metresAtHardCap>2300),
                    "Speed profile actually reaches its maximum and boosted runs retain over2300m at42m/s / "+profile+" / "+hz);
            }
            var live=new RouteRun{seed=1907,profile="live progression with normal clean-pass bonuses",speed=18,commandHz=30,liveGameUpdate=true};
            ResetRun(live.seed,true);int liveAction=-1,liveFrames=0;float oldZ=game.Motor.transform.position.z;
            while(game.State==RunState.Running&&game.Distance<850&&liveFrames++<2400)
            {
                live.speed=game.CurrentSpeed;live.minimumSpeed=Mathf.Min(live.minimumSpeed,live.speed);live.maximumSpeed=Mathf.Max(live.maximumSpeed,live.speed);
                if(!IssueCommands(live,ref liveAction))break;
                float z=game.Motor.transform.position.z;if(z<oldZ-100)live.originShifts++;oldZ=z;
                yield return null;
            }
            live.distance=game.Distance;live.passedRows=game.Obstacles.Passed;
            live.survived=game.State==RunState.Running&&game.Distance>=850&&live.physicalCuesMatch;
            live.hit=game.LastHit;runs.Add(live);
            Check(live.survived&&live.jumps>=2&&live.slides>=2&&live.laneChanges>0&&live.maximumSpeed>=30,
                "Live Game.Update follows the generated course beyond850m and reaches30m/s with real jump/slide/lane commands");
            report.finalPool=game.Obstacles.PoolCount;report.distinctObservedLabels=labels.Count;
            Check(report.finalPool==report.normalPool&&report.finalPool==ObstacleCourse.ObstacleCapacity,
                "All nine long routes and live integration retain the fixed obstacle pool");
            Check(!game.Powerups.Enabled&&!game.Tokens.Enabled&&!game.Powerups.IsProtected&&!game.Motor.Flying&&game.Coins==0,
                "Route survival uses no flight, shield, reward fever or powerup assistance");
            game.enabled=false;
            ResetRun(1907,false);game.Powerups.Enabled=true;game.Powerups.IsolatePickupsForTest();game.Powerups.Activate(PowerupKind.Shield);
            game.Motor.Speed=RunTempo.HardCap;
            bool absorbed=false,unexpected=false;
            while(game.Motor.transform.position.z<80)
            {
                game.Obstacles.BeginMove(game.Motor);game.Motor.Tick(1f/60f);game.Obstacles.Check(game.Motor);
                for(int contact=0;contact<game.Obstacles.ContactCount;contact++)
                {bool protectedHit=game.Powerups.TryAbsorbHit(game.Obstacles.ContactAt(contact));absorbed|=protectedHit;unexpected|=!protectedHit;}
                game.Powerups.TickTimers(1f/60f);
            }
            Check(absorbed&&!unexpected&&game.Obstacles.Passed==1&&game.Obstacles.CleanPassed==0,
                "A shield-protected hit through the real first row at42m/s counts as passed but never clean");
            game.ReturnToMenu();
        }
        void Check(bool condition,string label){if(condition)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type)
        {if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<40)errors.Add(message);}
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;Time.timeScale=1;
            if(!prepared)return;
            Application.runInBackground=previousBackground;Application.targetFrameRate=previousTargetRate;Time.captureDeltaTime=previousCaptureDelta;
            if(game!=null&&originalSettings!=null){SettingsField.SetValue(game,originalSettings);game.Motor.Configure(originalSettings);game.enabled=true;}
            if(testSettings!=null)Destroy(testSettings);
        }
        static string PreferenceSnapshot()
        {
            var values=new List<string>();string prefix=RunWallet.DefaultPrefix;
            foreach(string key in new[]{prefix+"Coins",prefix+"Upgrade.0",prefix+"Upgrade.1",prefix+"Upgrade.2","FenerRunner.BestScore"})
                values.Add(key+"="+PlayerPrefs.HasKey(key)+"/"+PlayerPrefs.GetInt(key,int.MinValue));
            values.Add("FenerRunner.Best="+PlayerPrefs.HasKey("FenerRunner.Best")+"/"+PlayerPrefs.GetFloat("FenerRunner.Best",-1).ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            return string.Join("|",values);
        }
    }
}
#endif

