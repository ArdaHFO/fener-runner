#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    // Editor-only evidence: production scene, camera, HUD, animation and collision
    // paths. The three style timers are queued early so a short clip shows all of them.
    public sealed class ActionMotionCaptureV11 : MonoBehaviour
    {
        const int Fps=24,Frames=288;
        const string Flag="FenerRunner.ActionMotionCaptureV11";
        [Serializable] sealed class Report
        {
            public bool passed,audioCaptured,preferencesPreserved,actualFatalContact,completedDeath,resultVisible;
            public string utc,unityVersion,frameDirectory;
            public int expectedFrameCount=Frames,actualFrameCount,frameRate=Fps,width,height,deathFrame=-1,resultFrame=-1;
            public int[] stylePeakFrames={-1,-1,-1};
            public float[] stylePeakWeights=new float[3];
            public double[] framePresentationSeconds=new double[Frames];
            public double presentationSeconds,deathPresentationSeconds;
            public string[] errors;
            public string scope="Actual Unity portrait Game-view screenshots with the production camera and HUD. Three real upper-body styles are queued early on an empty inspection stretch with token renderers hidden, followed by one actual black-bird collision, the unscaled fall and delayed result panel. Running frames use the fixed 24 Hz simulation timestep; after impact frame durations use measured real intervals to preserve unscaled death/camera timing. This is not measured phone FPS or physical touchscreen input. ScreenCapture does not capture audio.";
        }
        readonly List<string> errors=new List<string>();
        readonly Report report=new Report();
        RunnerGame game;RunWallet wallet;
        int oldCaptureRate,oldWallet,oldScore,deathStill=-1;float oldScale,oldBest;
        bool oldBackground,completed;string audioPreferencesBefore;

        [MenuItem("Fener Runner/04 - Capture v11 run styles and collision")]
        public static void StartCapture()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before capture.");
            if(!EditorApplication.ExecuteMenuItem("Fener Runner/5 - Open portrait Game view"))throw new InvalidOperationException("Portrait Game view command is missing.");
            SessionState.SetBool(Flag,true);EditorApplication.delayCall+=EditorApplication.EnterPlaymode;
        }
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Flag,false))return;
            SessionState.SetBool(Flag,false);new GameObject("V11 action motion capture").AddComponent<ActionMotionCaptureV11>();
        }
        IEnumerator Start()
        {
            oldCaptureRate=Time.captureFramerate;oldScale=Time.timeScale;oldBackground=Application.runInBackground;
            oldWallet=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0);oldScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);oldBest=PlayerPrefs.GetFloat("FenerRunner.Best",0);
            audioPreferencesBefore=AudioPreferences();Application.runInBackground=true;Application.logMessageReceived+=Log;
            report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;
            report.frameDirectory=Path.GetFullPath(Path.Combine(Application.dataPath, "../../Captures/v11-action-motion-frames"))+"/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(report.frameDirectory);Directory.CreateDirectory("../Validation");
            game=FindFirstObjectByType<RunnerGame>();
            if(game==null){errors.Add("RunnerGame is missing.");Finish();yield break;}
            game.AutomatedTest=true;wallet=game.UseWalletForTest("FenerRunner.Test.ActionCaptureV11."+DateTime.UtcNow.Ticks+".");
            yield return null;report.width=Screen.width;report.height=Screen.height;
            if(Screen.width!=1080||Screen.height!=1920){errors.Add("Expected 1080x1920 Game view.");Finish();yield break;}
            Time.captureFramerate=Fps;game.Play();game.Obstacles.SetActive(false);game.Tokens.Enabled=false;game.Powerups.Enabled=false;game.Animation.TurnAllowed=false;
            foreach(var renderer in game.Tokens.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            int startingDeathCount=game.Animation.DeathCount;
            double previousFrameTime=0;bool previousFinished=false;
            for(int f=0;f<Frames;f++)
            {
                if(f==12)game.Animation.QueueRunStyleForTest(0);
                if(f==78)game.Animation.QueueRunStyleForTest(1);
                if(f==144)game.Animation.QueueRunStyleForTest(2);
                if(f==198)game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+18f,game.Motor.Lane);
                yield return new WaitForEndOfFrame();
                double frameTime=Time.realtimeSinceStartupAsDouble;bool finished=game.State==RunState.Finished;
                // Duration belongs to the preceding screenshot: this interval is
                // how long that rendered pose remains on screen before this one.
                // PNG IO slows unscaled playback without slowing captured run time.
                if(f>0)report.framePresentationSeconds[f-1]=previousFinished||finished?Math.Max(.0001,frameTime-previousFrameTime):1.0/Fps;
                previousFrameTime=frameTime;previousFinished=finished;
                int style=(int)game.Animation.CurrentRunStyle-1;
                if(style>=0&&style<3)
                {
                    report.stylePeakWeights[style]=Mathf.Max(report.stylePeakWeights[style],game.Animation.RunStyleWeight);
                    if(report.stylePeakFrames[style]<0&&game.Animation.RunStyleWeight>=.92f)report.stylePeakFrames[style]=f;
                }
                if(game.State==RunState.Finished&&report.deathFrame<0)
                {report.deathFrame=f;report.actualFatalContact=f>=198&&game.Animation.DeathCount==startingDeathCount+1;}
                if(game.Animation.DeathProgress>=.7f&&deathStill<0)deathStill=f;
                if(game.Interface.resultPanel.activeSelf&&report.resultFrame<0)report.resultFrame=f;
                ScreenCapture.CaptureScreenshot(Path.Combine(report.frameDirectory,"frame_"+f.ToString("D4")+".png"));
                yield return null;
            }
            report.framePresentationSeconds[Frames-1]=previousFinished?Math.Max(.0001,Time.realtimeSinceStartupAsDouble-previousFrameTime):1.0/Fps;
            for(int i=0;i<Frames;i++)report.presentationSeconds+=report.framePresentationSeconds[i];
            if(report.deathFrame>=0&&report.resultFrame>report.deathFrame)
                for(int i=report.deathFrame;i<report.resultFrame;i++)report.deathPresentationSeconds+=report.framePresentationSeconds[i];
            report.completedDeath=game.Animation.DeathComplete;report.resultVisible=game.Interface.resultPanel.activeSelf;
            Time.captureFramerate=oldCaptureRate;
            float deadline=Time.realtimeSinceStartup+8;
            while(Count()<Frames&&Time.realtimeSinceStartup<deadline)yield return null;
            for(int i=0;i<3;i++)
            {
                if(report.stylePeakWeights[i]<.99f||report.stylePeakFrames[i]<0)errors.Add("Style "+(i+1)+" never reached full visible blend.");
                else CopyStill(report.stylePeakFrames[i],new[]{"compact","open-arms","glance"}[i]);
            }
            if(!report.actualFatalContact||!report.completedDeath||!report.resultVisible||report.resultFrame<=report.deathFrame)errors.Add("The actual collision, fall and delayed result sequence did not complete.");
            if(deathStill>=0)CopyStill(deathStill,"collision-fall");
            if(report.resultFrame>=0)CopyStill(Mathf.Min(Frames-1,report.resultFrame+12),"result");
            report.preferencesPreserved=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0)==oldWallet&&PlayerPrefs.GetInt("FenerRunner.BestScore",0)==oldScore&&PlayerPrefs.GetFloat("FenerRunner.Best",0)==oldBest&&AudioPreferences()==audioPreferencesBefore;
            if(!report.preferencesPreserved)errors.Add("Real wallet, records or audio preferences changed.");
            Finish();
        }
        static string AudioPreferences()
        {
            string[] keys={"FenerRunner.Muted","FenerRunner.Audio.Master","FenerRunner.Audio.Music","FenerRunner.Audio.Voices","FenerRunner.Audio.Effects"};
            var parts=new string[keys.Length];
            for(int i=0;i<keys.Length;i++)parts[i]=keys[i]+":"+PlayerPrefs.HasKey(keys[i])+":"+(i==0?PlayerPrefs.GetInt(keys[i],-999).ToString():PlayerPrefs.GetFloat(keys[i],-999).ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            return string.Join("|",parts);
        }
        int Count()=>Directory.Exists(report.frameDirectory)?Directory.GetFiles(report.frameDirectory,"frame_*.png").Length:0;
        void CopyStill(int frame,string name)
        {
            string path=Path.Combine(report.frameDirectory,"frame_"+frame.ToString("D4")+".png");
            if(File.Exists(path))File.Copy(path,Path.GetFullPath("../Validation/v11-"+name+".png"),true);
            else errors.Add("Missing screenshot "+frame);
        }
        void Finish()
        {
            report.actualFrameCount=Count();if(report.actualFrameCount!=Frames)errors.Add("Expected 288 frames, got "+report.actualFrameCount);
            report.errors=errors.ToArray();report.passed=errors.Count==0;completed=true;
            File.WriteAllText(Path.GetFullPath("../Validation/unity_action_capture_v11.json"),JsonUtility.ToJson(report,true));
            Debug.Log("V11 action capture "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void Log(string text,string trace,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(text);}
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;Time.captureFramerate=oldCaptureRate;Time.timeScale=oldScale;Application.runInBackground=oldBackground;
            if(wallet!=null)wallet.DeleteIsolatedTestData();
            if(!completed)Debug.LogWarning("V11 action capture interrupted; no passing evidence was created.");
        }
    }
}
#endif

