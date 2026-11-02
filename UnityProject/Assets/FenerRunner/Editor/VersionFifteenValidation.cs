using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner.Editor
{
    // No player build in this workflow. Each stage enters the existing scene in
    // native Play Mode and must produce a fresh report before the next can start.
    public static class VersionFifteenValidation
    {
        const string Key="FenerRunner.V15Suite";
        static readonly string[] Reports={"ghost_protocol","multiplayer_session","multiplayer_relay","ghost_visual","multiplayer_ui","multiplayer_gameplay","slide_response","speed_transition","jump_presentation","presentation_state","run_flow","tempo_motion","ui_audio","tempo_course","contacts","collisions","coins","action_motion","variety","power_remix","audio","presentation","run_motion","jump","fatal_collisions","course","obstacles","character_look_showcase","chick_tokens","chase_suv","lifecycle"};
        [Serializable] sealed class Result{public bool passed;public string[] checks,errors;}
        [Serializable] sealed class Stage{public string report,utc;public bool passed;public int checks;}
        [Serializable] sealed class Summary
        {
            public bool passed;public string utc;public int totalChecks;public Stage[] reports;
            public string scope="Fresh native Unity Play Mode regression and finite collision/collection matrix. Includes real two-peer loopback and Relay checks. No APK/player build. Not physical-phone performance or exhaustive input verification.";
        }
        [MenuItem("Fener Runner/00 - Verify v15 without building APK")]
        public static void Review()
        {
            SessionState.SetBool(Key+"Focused",false);SessionState.SetInt(Key+"FirstStage",1);
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first");
            foreach(string old in new[]{"V12Suite","V11Suite","V9Suite","V8Suite","SurfaceRegression","V13Suite","V14Suite"})SessionState.SetInt("FenerRunner."+old,0);
            MultiplayerSetupV15.Verify();EditorPackageHealthV15.Record();SetupVerification.OpenSceneForReview();PortraitReview.Open();
            SessionState.SetString(Key+"RunStart",DateTime.UtcNow.ToString("O"));StartAt(1);
        }
        public static void ReviewAndExit(){SessionState.SetBool(Key+"Exit",true);Review();}
        public static void ReviewGameplayAndExit()
        {
            MultiplayerSetupV15.Verify();EditorPackageHealthV15.Record();SetupVerification.OpenSceneForReview();PortraitReview.Open();
            SessionState.SetBool(Key+"Exit",true);SessionState.SetBool(Key+"Focused",true);
            SessionState.SetString(Key+"RunStart",DateTime.UtcNow.ToString("O"));StartAt(6);
        }
        public static void ReviewArtAndExit()
        {
            MultiplayerSetupV15.Verify();EditorPackageHealthV15.Record();SetupVerification.OpenSceneForReview();PortraitReview.Open();
            SessionState.SetBool(Key+"Exit",true);SessionState.SetBool(Key+"Focused",false);
            SessionState.SetString(Key+"RunStart",DateTime.UtcNow.ToString("O"));
            SessionState.SetInt(Key+"FirstStage",29);StartAt(29);
        }
        public static void ReviewChickAndExit()
        {
            MultiplayerSetupV15.Verify();EditorPackageHealthV15.Record();SetupVerification.OpenSceneForReview();PortraitReview.Open();
            SessionState.SetBool(Key+"Exit",true);SessionState.SetBool(Key+"Focused",true);
            SessionState.SetString(Key+"RunStart",DateTime.UtcNow.ToString("O"));StartAt(29);
        }
        public static void ReviewChaseAndExit()
        {
            MultiplayerSetupV15.Verify();EditorPackageHealthV15.Record();SetupVerification.OpenSceneForReview();PortraitReview.Open();
            SessionState.SetBool(Key+"Exit",true);SessionState.SetBool(Key+"Focused",false);
            SessionState.SetString(Key+"RunStart",DateTime.UtcNow.ToString("O"));
            SessionState.SetInt(Key+"FirstStage",30);StartAt(30);
        }
        static void StartAt(int stage)
        {
            SessionState.SetInt("FenerRunner.ReviewVersion",15);SessionState.SetInt(Key,stage);
            SessionState.SetInt(Key+"StageErrors",0);
            SessionState.SetString(Key+"LastError","");
            SessionState.SetString(Key+"StageStart",DateTime.UtcNow.ToString("O"));
            Debug.Log("V15 starting stage "+stage+" / "+Reports[stage-1]);
            EditorApplication.delayCall+=()=>Launch(stage);
        }
        static void Launch(int stage)
        {
            switch(stage)
            {
                case 1:GhostProtocolChecksV14.StartChecks();break;
                case 2:MultiplayerSessionChecksV14.StartChecks();break;
                case 3:MultiplayerSessionChecksV14.StartRelayChecks();break;
                case 4:GhostVisualChecksV14.StartChecks();break;
                case 5:MultiplayerUIChecks.StartChecks();break;
                case 6:MultiplayerGameplayChecksV14.StartChecks();break;
                case 7:SlideResponseChecksV13.StartChecks();break;
                case 8:SpeedTransitionChecksV13.StartChecks();break;
                case 9:JumpPresentationChecksV13.StartChecks();break;
                case 10:PresentationStateChecksV13.StartChecks();break;
                case 11:RunFlowChecksV12.StartChecks();break;
                case 12:TempoMotionChecksV12.StartChecks();break;
                case 13:UIAudioRegressionCheck.StartChecks();break;
                case 14:TempoCourseChecksV12.StartChecks();break;
                case 15:ContactMatrixChecksV11.StartChecks();break;
                case 16:CollisionChecksV8.StartChecks();break;
                case 17:CoinChecksV7.StartChecks();break;
                case 18:ActionMotionChecksV11.StartChecks();break;
                case 19:PowerVarietyChecksV7.StartChecks();break;
                case 20:PowerRemixChecksV11.StartChecks();break;
                case 21:AudioChecksV11.StartChecks();break;
                case 22:PresentationChecksV9.StartChecks();break;
                case 23:RunMotionChecksV8.StartChecks();break;
                case 24:JumpChecksV7.StartChecks();break;
                case 25:CollisionChecksV6.StartChecks();break;
                case 26:CourseChecksV8.StartChecks();break;
                case 27:ObstacleChecks.StartChecks();break;
                case 28:CharacterLookReview.StartChecks("showcase");break;
                case 29:ChickTokenChecksV15.StartChecks();break;
                case 30:ChaseSUVChecksV15.StartChecks();break;
                case 31:LifecycleChecksV15.StartChecks();break;
            }
        }
        static string FileFor(int i)=>"../Validation/unity_"+Reports[i]+"_v15.json";
        [InitializeOnLoadMethod] static void Register()
        {
            EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;
            Application.logMessageReceived-=RuntimeLog;Application.logMessageReceived+=RuntimeLog;
        }
        static void RuntimeLog(string message,string trace,LogType type)
        {
            if(SessionState.GetInt(Key,0)==0||(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert))return;
            SessionState.SetInt(Key+"StageErrors",SessionState.GetInt(Key+"StageErrors",0)+1);
            SessionState.SetString(Key+"LastError",message);
        }
        static void Changed(PlayModeStateChange state)
        {
            int stage=SessionState.GetInt(Key,0);if(state!=PlayModeStateChange.EnteredEditMode||stage==0)return;
            string file=FileFor(stage-1);DateTime start=DateTime.Parse(SessionState.GetString(Key+"StageStart",""),null,System.Globalization.DateTimeStyles.RoundtripKind);
            bool passed=File.Exists(file)&&File.GetLastWriteTimeUtc(file)>=start&&JsonUtility.FromJson<Result>(File.ReadAllText(file)).passed&&SessionState.GetInt(Key+"StageErrors",0)==0;
            if(!passed)
            {
                SessionState.SetInt(Key,0);Debug.LogError("V15 stopped at stage "+stage+": "+file+"; startup/runtime error: "+SessionState.GetString(Key+"LastError",""));
                if(SessionState.GetBool(Key+"Exit",false))EditorApplication.delayCall+=()=>EditorApplication.Exit(1);
                return;
            }
            if(SessionState.GetBool(Key+"Focused",false))
            {SessionState.SetInt(Key,0);SessionState.SetBool(Key+"Focused",false);Debug.Log("V15 focused scene gameplay PASSED");EditorApplication.delayCall+=()=>EditorApplication.Exit(0);return;}
            if(stage<Reports.Length){StartAt(stage+1);return;}
            if(SessionState.GetInt(Key+"FirstStage",1)>1)
            {SessionState.SetInt(Key,0);Debug.Log("V15 focused art checks PASSED");EditorApplication.delayCall+=()=>EditorApplication.Exit(0);return;}
            SessionState.SetInt(Key,0);var summary=new Summary{passed=true,utc=DateTime.UtcNow.ToString("O"),reports=new Stage[Reports.Length]};
            DateTime runStart=DateTime.Parse(SessionState.GetString(Key+"RunStart",""),null,System.Globalization.DateTimeStyles.RoundtripKind);
            for(int i=0;i<Reports.Length;i++)
            {
                string path=FileFor(i);var result=JsonUtility.FromJson<Result>(File.ReadAllText(path));
                var entry=new Stage{report=Path.GetFileName(path),utc=File.GetLastWriteTimeUtc(path).ToString("O"),passed=result.passed&&File.GetLastWriteTimeUtc(path)>=runStart,checks=result.checks==null?0:result.checks.Length};
                summary.reports[i]=entry;summary.passed&=entry.passed;summary.totalChecks+=entry.checks;
            }
            File.WriteAllText("../Validation/unity_suite_v15.json",JsonUtility.ToJson(summary,true));
            Debug.Log("V15 suite "+(summary.passed?"PASSED":"INCOMPLETE")+" with "+summary.totalChecks+" checks");
            if(SessionState.GetBool(Key+"Exit",false))EditorApplication.delayCall+=()=>EditorApplication.Exit(summary.passed?0:1);
        }
    }
}
