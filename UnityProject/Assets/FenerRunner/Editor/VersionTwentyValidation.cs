using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner.Editor
{
    // Fresh native reports are required per stage. Prior version evidence stays separate.
    public static class VersionTwentyValidation
    {
        const string Key="FenerRunner.V20Suite";
        static readonly string[] Reports={"pause_workshop","market_garage","collectibles","friends","friends_cloud","ghost_protocol","multiplayer_session","multiplayer_relay","multiplayer_ui","multiplayer_gameplay","contacts","coins","chick_tokens","collisions","hitboxes","double_actions","slide_recovery","speed_transition","power_remix","variety","run_flow","ui_audio","lifecycle","audio_lifecycle","course","crowd_props","obstacles"};
        [Serializable] sealed class Result{public bool passed;public string[] checks,errors;}
        [Serializable] sealed class Stage{public string report,utc;public bool passed;public int checks;}
        [Serializable] sealed class Summary
        {
            public bool passed;public string utc;public int totalChecks;public Stage[] reports;
            public string scope="27 native Unity Play Mode stages for pause/home, all shop tabs, economy/vehicles, collection/hen, cloud friends plus Relay/multiplayer, contact/hitbox, movement, powers, audio/lifecycle, crowd props and route gameplay. Finite regression, no APK and no physical phone test.";
        }
        [MenuItem("Fener Runner/00 - Verify workshop and friends v20 without APK")]
        public static void Review(){Begin(false,false);}
        public static void ReviewAndExit(){Begin(true,false);}
        public static void ReviewNewFeaturesAndExit(){Begin(true,true);}
        public static void ReviewCloudAndExit(){Begin(true,false,5);}
        public static void ReviewRemainingAndExit(){Begin(true,false,4,true);}
        [MenuItem("Fener Runner/00 - Continue v20 checks from cloud (no APK)")]
        public static void ReviewCloudRemainingAndExit(){Begin(true,false,5,true);}
        // Native startup dialogs can block Editor callbacks while realtime task
        // timeouts advance. Start only after the UI operator releases this gate.
        public static void ReviewCloudRemainingWhenReleased()
        {
            EditorApplication.update-=AwaitStartSignal;EditorApplication.update+=AwaitStartSignal;
            Debug.Log("V20 waiting for startup UI release before any cloud test timer starts");
        }
        static void AwaitStartSignal()
        {
            const string signal="../Validation/v20_test_start.signal";
            if(!File.Exists(signal))return;
            EditorApplication.update-=AwaitStartSignal;Begin(true,false,5,true);
        }
        static void Begin(bool exit,bool focused,int first=1,bool remaining=false)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            foreach(string old in new[]{"V12Suite","V11Suite","V9Suite","V8Suite","SurfaceRegression","V13Suite","V14Suite","V15Suite","V16Suite","V17Suite","V18Suite","V19Suite"})SessionState.SetInt("FenerRunner."+old,0);
            SetupVerification.OpenSceneForReview();PortraitReview.Open();
            SessionState.SetBool(Key+"Exit",exit);SessionState.SetBool(Key+"Focused",focused);
            SessionState.SetBool(Key+"Remaining",remaining);
            SessionState.SetInt(Key+"First",first);SessionState.SetString(Key+"RunStart",DateTime.UtcNow.ToString("O"));StartAt(first);
        }
        static void StartAt(int stage)
        {
            SessionState.SetInt("FenerRunner.ReviewVersion",20);SessionState.SetInt(Key,stage);
            SessionState.SetInt(Key+"StageErrors",0);SessionState.SetString(Key+"LastError","");
            SessionState.SetString(Key+"StageStart",DateTime.UtcNow.ToString("O"));
            Debug.Log("V20 starting stage "+stage+" / "+Reports[stage-1]);EditorApplication.delayCall+=()=>Launch(stage);
        }
        static void Launch(int stage)
        {
            switch(stage)
            {
                case 1:PauseWorkshopChecksV20.StartChecks();break;
                case 2:MarketGarageChecksV20.StartChecks();break;
                case 3:CollectibleChecksV20.StartChecks();break;
                case 4:FriendsChecksV20.StartChecks();break;
                case 5:FriendsCloudChecksV20.StartChecks();break;
                case 6:GhostProtocolChecksV14.StartChecks();break;
                case 7:MultiplayerSessionChecksV14.StartChecks();break;
                case 8:MultiplayerSessionChecksV14.StartRelayChecks();break;
                case 9:MultiplayerUIChecks.StartChecks();break;
                case 10:MultiplayerGameplayChecksV14.StartChecks();break;
                case 11:ContactMatrixChecksV11.StartChecks();break;
                case 12:CoinChecksV7.StartChecks();break;
                case 13:ChickTokenChecksV15.StartChecks();break;
                case 14:CollisionChecksV8.StartChecks();break;
                case 15:HitboxChecksV17.StartChecks();break;
                case 16:DoubleActionsChecksV16.StartChecks();break;
                case 17:SlideRecoveryChecksV17.StartChecks();break;
                case 18:SpeedTransitionChecksV13.StartChecks();break;
                case 19:PowerRemixChecksV11.StartChecks();break;
                case 20:PowerVarietyChecksV7.StartChecks();break;
                case 21:RunFlowChecksV12.StartChecks();break;
                case 22:UIAudioRegressionCheck.StartChecks();break;
                case 23:LifecycleChecksV15.StartChecks();break;
                case 24:AudioLifecycleChecksV18.StartChecks();break;
                case 25:CourseChecksV8.StartChecks();break;
                case 26:CrowdPropsChecksV19.StartChecks();break;
                case 27:ObstacleChecks.StartChecks();break;
            }
        }
        static string FileFor(int i)=>"../Validation/unity_"+Reports[i]+"_v20.json";
        [InitializeOnLoadMethod]static void Register()
        {
            EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;
            Application.logMessageReceived-=RuntimeLog;Application.logMessageReceived+=RuntimeLog;
        }
        static void RuntimeLog(string message,string trace,LogType type)
        {
            if(SessionState.GetInt(Key,0)==0||(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert))return;
            SessionState.SetInt(Key+"StageErrors",SessionState.GetInt(Key+"StageErrors",0)+1);SessionState.SetString(Key+"LastError",message);
        }
        static void Changed(PlayModeStateChange state)
        {
            int stage=SessionState.GetInt(Key,0);if(state!=PlayModeStateChange.EnteredEditMode||stage==0)return;
            string file=FileFor(stage-1);DateTime start=DateTime.Parse(SessionState.GetString(Key+"StageStart",""),null,System.Globalization.DateTimeStyles.RoundtripKind);
            bool passed=File.Exists(file)&&File.GetLastWriteTimeUtc(file)>=start&&JsonUtility.FromJson<Result>(File.ReadAllText(file)).passed&&SessionState.GetInt(Key+"StageErrors",0)==0;
            if(!passed)
            {
                SessionState.SetInt(Key,0);Debug.LogError("V20 stopped at stage "+stage+": "+file+"; runtime error: "+SessionState.GetString(Key+"LastError",""));
                if(SessionState.GetBool(Key+"Exit",false))EditorApplication.delayCall+=()=>EditorApplication.Exit(1);return;
            }
            if(SessionState.GetBool(Key+"Focused",false)&&stage==3)
            {SessionState.SetInt(Key,0);Debug.Log("V20 new feature checks PASSED");EditorApplication.delayCall+=()=>EditorApplication.Exit(0);return;}
            if(SessionState.GetInt(Key+"First",1)>1&&!SessionState.GetBool(Key+"Remaining",false)){SessionState.SetInt(Key,0);EditorApplication.delayCall+=()=>EditorApplication.Exit(0);return;}
            if(stage<Reports.Length){StartAt(stage+1);return;}
            SessionState.SetInt(Key,0);int first=SessionState.GetInt(Key+"First",1)-1;
            var summary=new Summary{passed=true,utc=DateTime.UtcNow.ToString("O"),reports=new Stage[Reports.Length-first]};
            if(first>0)summary.scope="Native Unity Play Mode continuation stages "+(first+1)+" through "+Reports.Length+". Earlier stage evidence is separate. No APK or physical phone test.";
            DateTime runStart=DateTime.Parse(SessionState.GetString(Key+"RunStart",""),null,System.Globalization.DateTimeStyles.RoundtripKind);
            for(int i=first;i<Reports.Length;i++)
            {
                string path=FileFor(i);var result=JsonUtility.FromJson<Result>(File.ReadAllText(path));
                var entry=new Stage{report=Path.GetFileName(path),utc=File.GetLastWriteTimeUtc(path).ToString("O"),passed=result.passed&&File.GetLastWriteTimeUtc(path)>=runStart,checks=result.checks==null?0:result.checks.Length};
                summary.reports[i-first]=entry;summary.passed&=entry.passed;summary.totalChecks+=entry.checks;
            }
            File.WriteAllText(first>0?"../Validation/unity_continuation_v20.json":"../Validation/unity_suite_v20.json",JsonUtility.ToJson(summary,true));
            Debug.Log("V20 suite "+(summary.passed?"PASSED":"INCOMPLETE")+" with "+summary.totalChecks+" checks");
            if(SessionState.GetBool(Key+"Exit",false))EditorApplication.delayCall+=()=>EditorApplication.Exit(summary.passed?0:1);
        }
    }
}
