#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace FenerRunner
{
    // Actual Game-view evidence. The temporary purchase wallet never touches the
    // player's currency, progress or records and this harness is Editor-only.
    public sealed class PresentationCaptureV9:MonoBehaviour
    {
        const int Fps=24,Frames=672;
        const string Flag="FenerRunner.PresentationCaptureV9";
        [Serializable] sealed class Report
        {
            public bool passed,audioCaptured;
            public string utc,frameDirectory,unityVersion;
            public int expectedFrameCount=Frames,actualFrameCount,frameRate=Fps,width,height,purchaseCount,jumpCommands,slideCommands;
            public string[] errors;
            public string scope="Actual Unity Game-view frames, 28-second presentation with scripted menu interaction, isolated test-wallet purchases and live gameplay. No audio capture, physical phone or measured device FPS.";
        }
        Report report=new Report();RunnerGame game;RunWallet testWallet;
        readonly List<string> errors=new List<string>();
        int oldCaptureRate,actioned=-1;bool oldBackground,completed;
        [MenuItem("Fener Runner/43 - Capture v9 animated presentation")]
        public static void StartCapture()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before capture.");
            EditorApplication.ExecuteMenuItem("Fener Runner/5 - Open portrait Game view");
            SessionState.SetBool(Flag,true);EditorApplication.delayCall+=EditorApplication.EnterPlaymode;
        }
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("V9 presentation recording").AddComponent<PresentationCaptureV9>();}}
        IEnumerator Start()
        {
            oldCaptureRate=Time.captureFramerate;oldBackground=Application.runInBackground;Application.runInBackground=true;Application.logMessageReceived+=Log;
            game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;
            testWallet=game.UseWalletForTest("FenerRunner.Test.PresentationCaptureV9."+DateTime.UtcNow.Ticks+".");testWallet.Credit(500);
            game.ReturnToMenu();
            report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;
            // Evidence scratch space for this workstation; the shipping game has
            // no dependency on this Editor-only capture destination.
            report.frameDirectory=Path.GetFullPath(Path.Combine(Application.dataPath, "../../Captures/v9-presentation-frames"))+"/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(report.frameDirectory);
            yield return null;report.width=Screen.width;report.height=Screen.height;
            if(Screen.width!=1080||Screen.height!=1920){errors.Add("Expected portrait 1080x1920.");Finish();yield break;}
            Time.captureFramerate=Fps;
            for(int f=0;f<Frames;f++)
            {
                if(f==56)game.Interface.heroGesture.onClick.Invoke();
                if(f==144)game.Interface.OpenShop();
                if(f==186){game.Interface.upgradeButtons[0].onClick.Invoke();report.purchaseCount++;}
                if(f==238){game.Interface.upgradeButtons[1].onClick.Invoke();report.purchaseCount++;}
                if(f==288)game.Interface.CloseShop();
                if(f==300)game.Interface.heroGesture.onClick.Invoke();
                if(f==360){game.Obstacles.UseSeedForTest(1907);game.Play();}
                if(f>=360)
                {
                    if(game.State!=RunState.Running){errors.Add("Gameplay ended unexpectedly at frame "+f);break;}
                    Drive();
                }
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(report.frameDirectory,"frame_"+f.ToString("D4")+".png"));
                yield return null;
            }
            Time.captureFramerate=oldCaptureRate;
            float until=Time.realtimeSinceStartup+8;
            while(Count()<Frames&&Time.realtimeSinceStartup<until)yield return null;
            // Unity accepts one asynchronous screenshot request per rendered frame.
            // Still evidence reuses those exact PNGs after the sequence is written.
            CopyStill(120,"v9-menu-final");CopyStill(175,"v9-shop-final");CopyStill(193,"v9-shop-purchase");
            if(testWallet.Level(PowerupKind.Dash)!=1||testWallet.Level(PowerupKind.Flight)!=1)errors.Add("Capture purchase sequence did not complete.");
            Finish();
        }
        void Drive()
        {
            if(!game.Obstacles.TryGetNextRoute(game.Motor.transform.position.z,out CourseRoute route))return;
            float ahead=route.Z-game.Motor.transform.position.z;
            if(ahead<=21&&game.Motor.Lane!=route.Lane)game.HandleCommand(game.Motor.Lane<route.Lane?RunnerCommand.Right:RunnerCommand.Left);
            if(route.Sequence==actioned||ahead>game.CurrentSpeed*(route.Action==RouteAction.Jump?game.Settings.JumpRiseSeconds:.42f)||ahead<=0||game.Motor.Lane!=route.Lane)return;
            if(route.Action==RouteAction.Jump){game.HandleCommand(RunnerCommand.Jump);report.jumpCommands++;}
            if(route.Action==RouteAction.Slide){game.HandleCommand(RunnerCommand.Slide);report.slideCommands++;}
            actioned=route.Sequence;
        }
        int Count(){return Directory.Exists(report.frameDirectory)?Directory.GetFiles(report.frameDirectory,"frame_*.png").Length:0;}
        void CopyStill(int frame,string name)
        {
            string source=Path.Combine(report.frameDirectory,"frame_"+frame.ToString("D4")+".png");
            if(File.Exists(source))File.Copy(source,Path.GetFullPath("../Validation/"+name+".png"),true);
            else errors.Add("Missing still-evidence frame "+frame);
        }
        void Finish()
        {
            report.actualFrameCount=Count();if(report.actualFrameCount!=Frames)errors.Add("Expected "+Frames+" frames, got "+report.actualFrameCount);
            report.errors=errors.ToArray();report.passed=errors.Count==0;completed=true;
            File.WriteAllText(Path.GetFullPath("../Validation/unity_presentation_capture_v9.json"),JsonUtility.ToJson(report,true));
            Debug.Log("Presentation capture v9 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void Log(string message,string trace,LogType kind){if(kind==LogType.Error||kind==LogType.Assert||kind==LogType.Exception)errors.Add(message);}
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;Time.captureFramerate=oldCaptureRate;Application.runInBackground=oldBackground;
            if(testWallet!=null)testWallet.DeleteIsolatedTestData();
            if(!completed)Debug.LogWarning("V9 presentation capture interrupted; no passing evidence created.");
        }
    }
}
#endif
