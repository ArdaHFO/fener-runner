#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    // Evidence capture only: ordinary production progression and naturally collected
    // rewards. No collision exemption, early style fixture or artificial HUD state.
    public sealed class TempoCaptureV12 : MonoBehaviour
    {
        const int Fps=24,RunFrames=480,TailFrames=60;
        const string Flag="FenerRunner.TempoCaptureV12";
        [Serializable] sealed class Report
        {
            public bool passed,audioCaptured,sourcePreferencesUnchanged,normalRunCompleted;
            public bool isolatedWallet=true,fatalTailRequested,actualFatalContact,completedDeath,resultVisible;
            public string utc,unityVersion,frameDirectory,stillPath,tailReason;
            public int seed=1907,frameRate=Fps,width,height,expectedFrameCount,actualFrameCount;
            public int runFrames=RunFrames,deathFrame=-1,resultFrame=-1,selectedStillFrame=-1;
            public int coins,rawPickups,streak,bestStreak,goals,bonusCoins,passedRows,cleanRows;
            public int jumps,slides,laneChanges;
            public float minimumSpeed=float.MaxValue,maximumSpeed;
            public float stillSpeed;
            public int stillStreak,normalRunStreak,normalRunCoins,normalRunGoals;
            public double stillDistance,normalRunDistance;
            public bool stillGrounded;
            public double distance,simulationSeconds,presentationSeconds,deathPresentationSeconds;
            public double[] framePresentationSeconds;
            public string[] naturalPowerPickups,errors;
            public string scope="Actual Unity1080x1920 portrait Game-view ScreenCapture frames. First480 frames use24Hz simulation, production speed progression, generated seed1907, ordinary coins and naturally collected powers. Real lane/jump/slide commands follow the course; no forced grants, invulnerability, empty-course fixture or fake HUD. If no flight/shield protection is active at the end, one real blocker is explicitly arranged for an optional60-frame fatal-contact/death/result tail. Post-impact frame presentation durations use measured real intervals to preserve unscaled animation timing. A temporary zero-upgrade wallet isolates rewards. No audio capture, measured device FPS or physical touchscreen validation.";
        }
        readonly Report report=new Report();
        readonly List<string> errors=new List<string>(),naturalPowers=new List<string>();
        readonly List<double> durations=new List<double>();
        RunnerGame game;RunWallet wallet;
        int oldCaptureRate,oldTargetRate,actioned=-1,startingDeathCount;
        float oldScale;
        bool oldBackground,prepared,completed,walletDeleted;
        string preferencesBefore;
        double previousFrameTime,simulationSeconds;
        bool previousFinished;

        [MenuItem("Fener Runner/07 - Capture v12 normal gameplay")]
        public static void StartCapture()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before capture.");
            if(!EditorApplication.ExecuteMenuItem("Fener Runner/5 - Open portrait Game view"))throw new InvalidOperationException("Portrait Game view command is missing.");
            SessionState.SetBool(Flag,true);EditorApplication.delayCall+=EditorApplication.EnterPlaymode;
        }
        [InitializeOnLoadMethod] static void Register()
        {EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Flag,false))return;
            SessionState.SetBool(Flag,false);new GameObject("V12 normal gameplay capture").AddComponent<TempoCaptureV12>();
        }
        IEnumerator Start()
        {
            oldCaptureRate=Time.captureFramerate;oldTargetRate=Application.targetFrameRate;oldScale=Time.timeScale;oldBackground=Application.runInBackground;
            preferencesBefore=PreferenceSnapshot();Application.runInBackground=true;Application.targetFrameRate=Fps;prepared=true;
            Application.logMessageReceived+=Log;
            report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;
            report.frameDirectory=Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "../../Captures/v12-gameplay-frames")),DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(report.frameDirectory);Directory.CreateDirectory("../Validation");
            report.stillPath=Path.GetFullPath("../Validation/v12-gameplay.png");
            game=FindFirstObjectByType<RunnerGame>();
            if(game==null){errors.Add("RunnerGame is missing.");Finish();yield break;}
            game.AutomatedTest=true;
            wallet=game.UseWalletForTest("FenerRunner.Test.TempoCaptureV12."+DateTime.UtcNow.Ticks+".");
            game.Powerups.Collected+=PowerCollected;
            yield return null;
            report.width=Screen.width;report.height=Screen.height;
            if(Screen.width!=1080||Screen.height!=1920){errors.Add("Expected1080x1920 portrait Game view.");Finish();yield break;}
            if(Mathf.Abs(game.Settings.forwardSpeed-18)>.001f||Mathf.Abs(game.Settings.maxSpeed-34)>.001f)
            {errors.Add("Capture requires the normal18→34m/s production settings.");Finish();yield break;}

            Time.captureFramerate=Fps;game.Obstacles.UseSeedForTest(report.seed);
            game.Obstacles.Enabled=true;game.Tokens.Enabled=true;game.Powerups.Enabled=true;game.Play();
            startingDeathCount=game.Animation.DeathCount;
            report.expectedFrameCount=RunFrames;
            int frame=0;
            for(;frame<RunFrames;frame++)
            {
                if(game.State==RunState.Running)IssueRouteCommands();
                yield return new WaitForEndOfFrame();
                CaptureFrame(frame,true);
                yield return null;
            }
            report.normalRunCompleted=game.State==RunState.Running&&report.deathFrame<0;
            report.normalRunDistance=game.Distance;report.normalRunStreak=game.Flow.Streak;report.normalRunCoins=game.Coins;report.normalRunGoals=game.Flow.CompletedChallenges;
            if(!report.normalRunCompleted)errors.Add("The normal generated route ended before480 gameplay frames completed.");

            // Preserve natural powers. A death tail is optional because a legitimately
            // earned shield or flight must not be removed to manufacture a collision.
            bool canShowFatal=report.normalRunCompleted&&game.Motor.Grounded&&!game.Motor.Flying&&!game.Powerups.FlightActive&&
                !game.Powerups.ShieldActive&&!game.Powerups.IsProtected;
            report.fatalTailRequested=canShowFatal;
            report.tailReason=canShowFatal?"One real black obstacle18m ahead after the normal20-second run.":
                report.normalRunCompleted?"Omitted to preserve natural protection or the current airborne action.":"Omitted because normal gameplay ended early.";
            if(canShowFatal)
            {
                game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+18,game.Motor.Lane);
                report.expectedFrameCount+=TailFrames;
                for(int tail=0;tail<TailFrames;tail++,frame++)
                {
                    yield return new WaitForEndOfFrame();
                    CaptureFrame(frame,false);
                    yield return null;
                }
                report.actualFatalContact=report.deathFrame>=RunFrames&&game.Animation.DeathCount==startingDeathCount+1&&game.LastHit.Length>0;
                report.completedDeath=game.Animation.DeathComplete;report.resultVisible=game.Interface.resultPanel.activeSelf;
                if(!report.actualFatalContact||!report.completedDeath||!report.resultVisible||report.resultFrame<=report.deathFrame)
                    errors.Add("The optional actual collision, unscaled death and delayed result tail did not complete.");
            }
            if(durations.Count>0)durations[durations.Count-1]=previousFinished?Math.Max(.0001,Time.realtimeSinceStartupAsDouble-previousFrameTime):1.0/Fps;
            report.framePresentationSeconds=durations.ToArray();
            foreach(double duration in durations)report.presentationSeconds+=duration;
            if(report.deathFrame>=0&&report.resultFrame>report.deathFrame)
                for(int i=report.deathFrame;i<report.resultFrame;i++)report.deathPresentationSeconds+=durations[i];
            report.simulationSeconds=simulationSeconds;
            RecordFinalState();
            Time.captureFramerate=oldCaptureRate;
            float deadline=Time.realtimeSinceStartup+8;
            while(Count()<report.expectedFrameCount&&Time.realtimeSinceStartup<deadline)yield return null;
            if(report.selectedStillFrame>=0)
            {
                string source=FramePath(report.selectedStillFrame);
                if(File.Exists(source))File.Copy(source,report.stillPath,true);else errors.Add("Selected gameplay still is missing.");
            }
            else errors.Add("No running frame reached streak8 or350m for the gameplay still.");
            if(report.minimumSpeed<12||report.maximumSpeed>RunTempo.HardCap+.001f||report.maximumSpeed<24)
                errors.Add("Captured speed range does not show normal safe progression.");
            SettleAndRemoveTestWallet();
            report.sourcePreferencesUnchanged=PreferenceSnapshot()==preferencesBefore;
            if(!report.sourcePreferencesUnchanged)errors.Add("Real wallet, upgrades, records or audio preferences changed.");
            Finish();
        }
        void IssueRouteCommands()
        {
            if(!game.Obstacles.TryGetNextRoute(game.Motor.transform.position.z,out CourseRoute route))return;
            float ahead=route.Z-game.Motor.transform.position.z;
            if(ahead<=CoursePatternGenerator.LaneChangeLead&&game.Motor.Lane!=route.Lane)
            {game.HandleCommand(game.Motor.Lane<route.Lane?RunnerCommand.Right:RunnerCommand.Left);report.laneChanges++;}
            float lead=route.Action==RouteAction.Jump?game.Settings.JumpRiseSeconds:.42f;
            if(route.Sequence==actioned||ahead<=0||ahead>game.CurrentSpeed*lead||game.Motor.Lane!=route.Lane)return;
            if(route.Action==RouteAction.Jump){game.HandleCommand(RunnerCommand.Jump);report.jumps++;}
            if(route.Action==RouteAction.Slide){game.HandleCommand(RunnerCommand.Slide);report.slides++;}
            actioned=route.Sequence;
        }
        void CaptureFrame(int frame,bool normalRun)
        {
            double now=Time.realtimeSinceStartupAsDouble;bool finished=game.State==RunState.Finished;
            if(durations.Count>0)durations[durations.Count-1]=previousFinished||finished?Math.Max(.0001,now-previousFrameTime):1.0/Fps;
            durations.Add(1.0/Fps);previousFrameTime=now;previousFinished=finished;
            if(game.State==RunState.Running)
            {
                simulationSeconds+=1.0/Fps;
                report.minimumSpeed=Mathf.Min(report.minimumSpeed,game.CurrentSpeed);report.maximumSpeed=Mathf.Max(report.maximumSpeed,game.CurrentSpeed);
                // Prefer an unobscured actual running pose after the clean-run bonus
                // appears. The350m fallback also covers naturally protected runs.
                if(normalRun&&(game.Flow.Streak>=8||game.Distance>=350)&&
                    (report.selectedStillFrame<0||(game.Motor.Grounded&&!game.Motor.Sliding&&!game.Motor.ChangingLane)))
                {
                    report.selectedStillFrame=frame;report.stillSpeed=game.CurrentSpeed;report.stillStreak=game.Flow.Streak;
                    report.stillDistance=game.Distance;report.stillGrounded=game.Motor.Grounded;
                }
            }
            if(finished&&report.deathFrame<0)report.deathFrame=frame;
            if(game.Interface.resultPanel.activeSelf&&report.resultFrame<0)report.resultFrame=frame;
            ScreenCapture.CaptureScreenshot(FramePath(frame));
        }
        void RecordFinalState()
        {
            report.distance=game.Distance;report.coins=game.Coins;report.rawPickups=game.Tokens.TotalCollected;
            report.streak=game.Flow.Streak;report.bestStreak=game.Flow.BestStreak;report.goals=game.Flow.CompletedChallenges;
            report.bonusCoins=game.Flow.BonusCoins;report.passedRows=game.Obstacles.Passed;report.cleanRows=game.Obstacles.CleanPassed;
            report.naturalPowerPickups=naturalPowers.ToArray();
        }
        void PowerCollected(PowerupKind kind){naturalPowers.Add(kind+" at "+game.Distance.ToString("F1",CultureInfo.InvariantCulture)+"m");}
        string FramePath(int frame)=>Path.Combine(report.frameDirectory,"frame_"+frame.ToString("D4")+".png");
        int Count()=>Directory.Exists(report.frameDirectory)?Directory.GetFiles(report.frameDirectory,"frame_*.png").Length:0;
        void SettleAndRemoveTestWallet()
        {
            if(wallet==null||walletDeleted)return;
            if(game!=null){game.enabled=false;game.ReturnToMenu();}
            wallet.DeleteIsolatedTestData();walletDeleted=true;
        }
        void Finish()
        {
            SettleAndRemoveTestWallet();
            report.actualFrameCount=Count();
            if(report.actualFrameCount!=report.expectedFrameCount)errors.Add("Expected "+report.expectedFrameCount+" screenshots, got "+report.actualFrameCount);
            report.errors=errors.ToArray();report.passed=errors.Count==0;completed=true;
            File.WriteAllText(Path.GetFullPath("../Validation/unity_gameplay_capture_v12.json"),JsonUtility.ToJson(report,true));
            Debug.Log("V12 gameplay capture "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        static string PreferenceSnapshot()
        {
            var values=new List<string>();string prefix=RunWallet.DefaultPrefix;
            foreach(string key in new[]{prefix+"Coins",prefix+"Upgrade.0",prefix+"Upgrade.1",prefix+"Upgrade.2","FenerRunner.BestScore","FenerRunner.Muted"})
                values.Add(key+"="+PlayerPrefs.HasKey(key)+"/"+PlayerPrefs.GetInt(key,int.MinValue));
            foreach(string key in new[]{"FenerRunner.Best","FenerRunner.Audio.Master","FenerRunner.Audio.Music","FenerRunner.Audio.Voices","FenerRunner.Audio.Effects"})
                values.Add(key+"="+PlayerPrefs.HasKey(key)+"/"+PlayerPrefs.GetFloat(key,-999).ToString("R",CultureInfo.InvariantCulture));
            return string.Join("|",values);
        }
        void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;
            if(game!=null&&game.Powerups!=null)game.Powerups.Collected-=PowerCollected;
            SettleAndRemoveTestWallet();
            if(prepared){Time.captureFramerate=oldCaptureRate;Time.timeScale=oldScale;Application.targetFrameRate=oldTargetRate;Application.runInBackground=oldBackground;}
            if(!completed)Debug.LogWarning("V12 gameplay capture interrupted; no passing evidence was created.");
        }
    }
}
#endif
