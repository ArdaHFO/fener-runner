using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner.Editor
{
    // No player build in this workflow. Each stage enters the existing scene in
    // native Play Mode and must produce a fresh report before the next can start.
    public static class VersionThirteenValidation
    {
        const string Key="FenerRunner.V13Suite";
        static readonly string[] Reports={"slide_response","speed_transition","jump_presentation","presentation_state","run_flow","tempo_motion","ui_audio","tempo_course","contacts","collisions","coins","action_motion","variety","power_remix","audio","presentation","run_motion","jump","fatal_collisions","course","obstacles","character_look_showcase"};
        [Serializable] sealed class Result{public bool passed;public string[] checks,errors;}
        [Serializable] sealed class Stage{public string report,utc;public bool passed;public int checks;}
        [Serializable] sealed class Summary
        {
            public bool passed;public string utc;public int totalChecks;public Stage[] reports;
            public string scope="Fresh native Unity Play Mode regression and finite collision/collection matrix. No APK/player build or multiplayer implementation. Not physical-phone performance or exhaustive input verification.";
        }
        [MenuItem("Fener Runner/00 - Verify v13 without building APK")]
        public static void Review()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first");
            foreach(string old in new[]{"V12Suite","V11Suite","V9Suite","V8Suite","SurfaceRegression"})SessionState.SetInt("FenerRunner."+old,0);
            SetupVerification.OpenSceneForReview();PortraitReview.Open();
            SessionState.SetString(Key+"RunStart",DateTime.UtcNow.ToString("O"));StartAt(1);
        }
        public static void ReviewAndExit(){SessionState.SetBool(Key+"Exit",true);Review();}
        static void StartAt(int stage)
        {
            SessionState.SetInt("FenerRunner.ReviewVersion",13);SessionState.SetInt(Key,stage);
            SessionState.SetString(Key+"StageStart",DateTime.UtcNow.ToString("O"));
            Debug.Log("V13 starting stage "+stage+" / "+Reports[stage-1]);
            EditorApplication.delayCall+=()=>Launch(stage);
        }
        static void Launch(int stage)
        {
            switch(stage)
            {
                case 1:SlideResponseChecksV13.StartChecks();break;
                case 2:SpeedTransitionChecksV13.StartChecks();break;
                case 3:JumpPresentationChecksV13.StartChecks();break;
                case 4:PresentationStateChecksV13.StartChecks();break;
                case 5:RunFlowChecksV12.StartChecks();break;
                case 6:TempoMotionChecksV12.StartChecks();break;
                case 7:UIAudioRegressionCheck.StartChecks();break;
                case 8:TempoCourseChecksV12.StartChecks();break;
                case 9:ContactMatrixChecksV11.StartChecks();break;
                case 10:CollisionChecksV8.StartChecks();break;
                case 11:CoinChecksV7.StartChecks();break;
                case 12:ActionMotionChecksV11.StartChecks();break;
                case 13:PowerVarietyChecksV7.StartChecks();break;
                case 14:PowerRemixChecksV11.StartChecks();break;
                case 15:AudioChecksV11.StartChecks();break;
                case 16:PresentationChecksV9.StartChecks();break;
                case 17:RunMotionChecksV8.StartChecks();break;
                case 18:JumpChecksV7.StartChecks();break;
                case 19:CollisionChecksV6.StartChecks();break;
                case 20:CourseChecksV8.StartChecks();break;
                case 21:ObstacleChecks.StartChecks();break;
                case 22:CharacterLookReview.StartChecks("showcase");break;
            }
        }
        static string FileFor(int i)=>"../Validation/unity_"+Reports[i]+"_v13.json";
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            int stage=SessionState.GetInt(Key,0);if(state!=PlayModeStateChange.EnteredEditMode||stage==0)return;
            string file=FileFor(stage-1);DateTime start=DateTime.Parse(SessionState.GetString(Key+"StageStart",""),null,System.Globalization.DateTimeStyles.RoundtripKind);
            bool passed=File.Exists(file)&&File.GetLastWriteTimeUtc(file)>=start&&JsonUtility.FromJson<Result>(File.ReadAllText(file)).passed;
            if(!passed)
            {
                SessionState.SetInt(Key,0);Debug.LogError("V13 stopped at stage "+stage+": "+file);
                if(SessionState.GetBool(Key+"Exit",false))EditorApplication.delayCall+=()=>EditorApplication.Exit(1);
                return;
            }
            if(stage<Reports.Length){StartAt(stage+1);return;}
            SessionState.SetInt(Key,0);var summary=new Summary{passed=true,utc=DateTime.UtcNow.ToString("O"),reports=new Stage[Reports.Length]};
            DateTime runStart=DateTime.Parse(SessionState.GetString(Key+"RunStart",""),null,System.Globalization.DateTimeStyles.RoundtripKind);
            for(int i=0;i<Reports.Length;i++)
            {
                string path=FileFor(i);var result=JsonUtility.FromJson<Result>(File.ReadAllText(path));
                var entry=new Stage{report=Path.GetFileName(path),utc=File.GetLastWriteTimeUtc(path).ToString("O"),passed=result.passed&&File.GetLastWriteTimeUtc(path)>=runStart,checks=result.checks==null?0:result.checks.Length};
                summary.reports[i]=entry;summary.passed&=entry.passed;summary.totalChecks+=entry.checks;
            }
            File.WriteAllText("../Validation/unity_suite_v13.json",JsonUtility.ToJson(summary,true));
            Debug.Log("V13 suite "+(summary.passed?"PASSED":"INCOMPLETE")+" with "+summary.totalChecks+" checks");
            if(SessionState.GetBool(Key+"Exit",false))EditorApplication.delayCall+=()=>EditorApplication.Exit(summary.passed?0:1);
        }
    }
}
