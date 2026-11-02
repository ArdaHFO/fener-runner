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
    public sealed class CourseChecksV8:MonoBehaviour
    {
        const string SessionKey="FenerRunner.CourseChecksV8";
        static readonly FieldInfo SettingsField=typeof(RunnerGame).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic);
        [Serializable] sealed class RouteRun
        {
            public int seed,commandHz,physicsSteps,jumps,slides,laneChanges,passedRows,originShifts;
            public float speed;
            public double distance;
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
            public string scope="Nine deterministic real Unity CharacterController/collider routes at18/24/30m/s and15/30/60Hz command sampling, each2500m, with production1/60s physics substeps. The advertised lane is used, but action cues are checked against the active obstacle instances and actual collisions decide survival. One additional live Game.Update route verifies integration. Powers and rewards disabled. Source settings and production records/wallet preserved. Not physical-phone performance evidence.";
        }
        RunnerGame game;RunnerSettings originalSettings,testSettings;string originalSettingsJson;
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        readonly List<RouteRun> runs=new List<RouteRun>();readonly HashSet<string> labels=new HashSet<string>();
        readonly Report report=new Report();
        bool prepared,previousBackground,hadBest,hadScore;
        float previousBest,previousCaptureDelta,began;int previousScore,previousTargetRate;

        [MenuItem("Fener Runner/27 - Verify varied physical course routes")]
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
            SessionState.SetBool(SessionKey,false);new GameObject("Varied course checks v0.8").AddComponent<CourseChecksV8>();
        }
        IEnumerator Start()
        {
            began=Time.realtimeSinceStartup;game=FindFirstObjectByType<RunnerGame>();
            previousBackground=Application.runInBackground;previousTargetRate=Application.targetFrameRate;previousCaptureDelta=Time.captureDeltaTime;
            hadBest=PlayerPrefs.HasKey("FenerRunner.Best");hadScore=PlayerPrefs.HasKey("FenerRunner.BestScore");
            previousBest=PlayerPrefs.GetFloat("FenerRunner.Best",0);previousScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);
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
            report.checks=checks.ToArray();report.errors=errors.ToArray();report.runs=runs.ToArray();report.passed=errors.Count==0;
            report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.wallSeconds=Time.realtimeSinceStartup-began;
            Directory.CreateDirectory("../Validation");File.WriteAllText(ReviewEvidence.FileFor(8,"../Validation/unity_course_v8.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Course v8 checks "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));
            EditorApplication.isPlaying=false;
        }
        void ResetRun(int seed,float speed,bool live)
        {
            game.enabled=false;testSettings.forwardSpeed=testSettings.maxSpeed=speed;
            game.Powerups.Enabled=false;game.Tokens.Enabled=false;game.Obstacles.Enabled=true;game.Obstacles.UseSeedForTest(seed);
            game.Play();game.Motor.Speed=speed;game.enabled=live;
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
            if(game==null||game.Powerups==null||SettingsField==null)throw new InvalidOperationException("Built v0.8 scene required.");
            game.AutomatedTest=true;originalSettings=game.Settings;originalSettingsJson=JsonUtility.ToJson(originalSettings);
            testSettings=Instantiate(originalSettings);testSettings.name="Physical course check settings (runtime clone)";
            SettingsField.SetValue(game,testSettings);game.Motor.Configure(testSettings);
            Check(Mathf.Abs(testSettings.jumpHeight-2.65f)<.001f&&Mathf.Abs(testSettings.jumpAirSeconds-.72f)<.001f&&
                Mathf.Abs(testSettings.jumpAscentFraction-.56f)<.001f&&Mathf.Abs(testSettings.slideSeconds-.85f)<.001f&&
                Mathf.Abs(testSettings.laneChangeSeconds-.14f)<.001f&&Mathf.Abs(testSettings.laneSpacing-3.3f)<.001f,
                "Actual motor settings match the constructive audit timing envelope");
            report.normalPool=game.Obstacles.PoolCount;
            int[] seeds={1907,5521,27011,104729,777777,90210,42,314159,8881};
            float[] speeds={18,24,30};int[] frequencies={15,30,60};int caseIndex=0;
            foreach(float speed in speeds)foreach(int hz in frequencies)
            {
                var run=new RouteRun{seed=seeds[caseIndex++],speed=speed,commandHz=hz};
                ResetRun(run.seed,speed,false);int actioned=-1,frames=0;
                while(run.distance<2500&&run.hit.Length==0&&frames<12000)
                {
                    if(!IssueCommands(run,ref actioned)){if(run.hit.Length==0)run.hit="No readable upcoming row";break;}
                    float remaining=1f/hz;
                    while(remaining>.000001f)
                    {
                        float dt=Mathf.Min(remaining,1f/60f);
                        game.Obstacles.BeginMove(game.Motor);run.distance+=game.Motor.Tick(dt);run.physicsSteps++;
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
                run.passedRows=game.Obstacles.Passed;run.survived=run.hit.Length==0&&run.distance>=2500;runs.Add(run);
                Check(run.survived&&run.physicalCuesMatch&&run.jumps>0&&run.slides>0&&run.laneChanges>0&&run.passedRows>=45&&run.originShifts>=2,
                    "Seed"+run.seed+" real-collider route survives2500m at"+speed+"m/s and"+hz+"Hz"+
                    (run.hit.Length>0?" ("+run.hit+")":""));
            }
            var live=new RouteRun{seed=1907,speed=30,commandHz=30,liveGameUpdate=true};
            ResetRun(live.seed,live.speed,true);int liveAction=-1,liveFrames=0;float oldZ=game.Motor.transform.position.z;
            while(game.State==RunState.Running&&game.Distance<350&&liveFrames++<600)
            {
                if(!IssueCommands(live,ref liveAction))break;
                float z=game.Motor.transform.position.z;if(z<oldZ-100)live.originShifts++;oldZ=z;
                yield return null;
            }
            live.distance=game.Distance;live.passedRows=game.Obstacles.Passed;
            live.survived=game.State==RunState.Running&&game.Distance>=350&&live.physicalCuesMatch;
            live.hit=game.LastHit;runs.Add(live);
            Check(live.survived&&live.jumps>=2&&live.slides>=2&&live.laneChanges>0,
                "Live Game.Update follows the generated course beyond350m with real jump/slide/lane commands");
            report.finalPool=game.Obstacles.PoolCount;report.distinctObservedLabels=labels.Count;
            Check(report.finalPool==report.normalPool&&report.finalPool==ObstacleCourse.ObstacleCapacity,
                "All nine long routes and live integration retain the fixed obstacle pool");
            Check(!game.Powerups.Enabled&&!game.Tokens.Enabled&&!game.Powerups.IsProtected&&!game.Motor.Flying&&game.Coins==0,
                "Route survival uses no flight, shield, reward fever or powerup assistance");
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
            if(hadBest)PlayerPrefs.SetFloat("FenerRunner.Best",previousBest);else PlayerPrefs.DeleteKey("FenerRunner.Best");
            if(hadScore)PlayerPrefs.SetInt("FenerRunner.BestScore",previousScore);else PlayerPrefs.DeleteKey("FenerRunner.BestScore");
            PlayerPrefs.Save();
        }
    }
}
#endif

