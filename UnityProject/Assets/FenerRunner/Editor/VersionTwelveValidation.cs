using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner.Editor
{
    public static class VersionTwelveValidation
    {
        const string Key="FenerRunner.V12Suite";
        static readonly string[] Reports={"run_flow","tempo_motion","ui_audio","tempo_course","contacts","collisions","coins","action_motion","variety","power_remix","audio","presentation","run_motion","jump","fatal_collisions","course","obstacles","character_look_showcase"};
        [Serializable] sealed class Result {public bool passed;public string[] checks,errors;}
        [Serializable] sealed class Stage {public string report,utc;public bool passed;public int checks;}
        [Serializable] sealed class Summary {public bool passed;public string utc;public int totalChecks;public Stage[] reports;public string scope="Fresh native Unity Play Mode reports. A finite contact matrix and targeted scenario assertions, not exhaustive proof over every possible gesture or device.";}
        [MenuItem("Fener Runner/00 - Rebuild and verify v12")]
        public static void BuildAndRun(){SessionState.SetInt(Key,0);RunnerSceneBuilder.Build();Review();}
        [MenuItem("Fener Runner/01 - Verify v12 without rebuilding")]
        public static void Review(){SetupVerification.OpenSceneForReview();PortraitReview.Open();SessionState.SetString(Key+"RunStart",DateTime.UtcNow.ToString("O"));StartAt(1);}
        [MenuItem("Fener Runner/02 - Continue failed v12 stage")]
        public static void Continue(){StartAt(Mathf.Clamp(SessionState.GetInt(Key+"Last",1),1,Reports.Length));}
        [MenuItem("Fener Runner/03 - Capture v12 character gallery")]
        public static void Capture(){SessionState.SetInt("FenerRunner.ReviewVersion",12);CharacterLookReview.StartChecks("showcase");}
        public static void StartAt(int stage)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first");
            SessionState.SetInt("FenerRunner.V11Suite",0);SessionState.SetInt("FenerRunner.V9Suite",0);SessionState.SetInt("FenerRunner.V8Suite",0);SessionState.SetInt("FenerRunner.SurfaceRegression",0);
            SessionState.SetInt("FenerRunner.ReviewVersion",12);SessionState.SetInt(Key,stage);SessionState.SetInt(Key+"Last",stage);
            SessionState.SetString(Key+"StageStart",DateTime.UtcNow.ToString("O"));EditorApplication.delayCall+=()=>Launch(stage);
        }
        static void Launch(int stage)
        {
            switch(stage)
            {
                case 1:RunFlowChecksV12.StartChecks();break;
                case 2:TempoMotionChecksV12.StartChecks();break;
                case 3:UIAudioRegressionCheck.StartChecks();break;
                case 4:TempoCourseChecksV12.StartChecks();break;
                case 5:ContactMatrixChecksV11.StartChecks();break;
                case 6:CollisionChecksV8.StartChecks();break;
                case 7:CoinChecksV7.StartChecks();break;
                case 8:ActionMotionChecksV11.StartChecks();break;
                case 9:PowerVarietyChecksV7.StartChecks();break;
                case 10:PowerRemixChecksV11.StartChecks();break;
                case 11:AudioChecksV11.StartChecks();break;
                case 12:PresentationChecksV9.StartChecks();break;
                case 13:RunMotionChecksV8.StartChecks();break;
                case 14:JumpChecksV7.StartChecks();break;
                case 15:CollisionChecksV6.StartChecks();break;
                case 16:CourseChecksV8.StartChecks();break;
                case 17:ObstacleChecks.StartChecks();break;
                case 18:CharacterLookReview.StartChecks("showcase");break;
            }
        }
        static string FileFor(int index)=>"../Validation/unity_"+Reports[index]+"_v12.json";
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            int stage=SessionState.GetInt(Key,0);if(state!=PlayModeStateChange.EnteredEditMode||stage==0)return;
            string file=FileFor(stage-1);DateTime began=DateTime.Parse(SessionState.GetString(Key+"StageStart",""),null,System.Globalization.DateTimeStyles.RoundtripKind);
            if(!File.Exists(file)||File.GetLastWriteTimeUtc(file)<began||!JsonUtility.FromJson<Result>(File.ReadAllText(file)).passed)
            {SessionState.SetInt(Key,0);Debug.LogError("V12 stopped at stage "+stage+": "+file);return;}
            if(stage<Reports.Length){StartAt(stage+1);return;}
            SessionState.SetInt(Key,0);var summary=new Summary{passed=true,utc=DateTime.UtcNow.ToString("O"),reports=new Stage[Reports.Length]};
            DateTime runStart=DateTime.Parse(SessionState.GetString(Key+"RunStart",SessionState.GetString(Key+"StageStart","")),null,System.Globalization.DateTimeStyles.RoundtripKind);
            for(int i=0;i<Reports.Length;i++)
            {
                string path=FileFor(i);var result=JsonUtility.FromJson<Result>(File.ReadAllText(path));
                var entry=new Stage{report=Path.GetFileName(path),utc=File.GetLastWriteTimeUtc(path).ToString("O"),passed=result.passed&&File.GetLastWriteTimeUtc(path)>=runStart,checks=result.checks==null?0:result.checks.Length};
                summary.reports[i]=entry;summary.passed&=entry.passed;summary.totalChecks+=entry.checks;
            }
            File.WriteAllText("../Validation/unity_suite_v12.json",JsonUtility.ToJson(summary,true));Debug.Log("V12 suite "+(summary.passed?"PASSED":"INCOMPLETE")+" with "+summary.totalChecks+" conditions");
        }
    }
}
