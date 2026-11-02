using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner.Editor
{
    public static class VersionNineValidation
    {
        const string Key="FenerRunner.V9Suite";
        static readonly string[] Reports={"presentation","character_motion","collisions","fatal_collisions","run_motion",
            "jump","coins","mobile","obstacles","presentation_capture"};
        [Serializable] sealed class Result{public bool passed;public string[] checks,errors;}
        [Serializable] sealed class Stage{public string report,utc;public bool passed;public int checks;}
        [Serializable] sealed class Summary{public bool passed;public string utc;public int reportCount,totalChecks;public Stage[] reports;}

        [MenuItem("Fener Runner/40 - Rebuild and verify v9")]
        public static void RebuildAndReview(){RunnerSceneBuilder.Build();ReviewAll();}
        [MenuItem("Fener Runner/41 - Verify v9 without rebuilding")]
        public static void ReviewAll(){SetupVerification.OpenSceneForReview();PortraitReview.Open();StartAt(1);}
        [MenuItem("Fener Runner/42 - Resume v9 gameplay regression")]
        public static void ResumeRegression(){StartAt(3);}
        [MenuItem("Fener Runner/44 - Capture v9 presentation only")]
        public static void CaptureOnly(){PortraitReview.Open();StartAt(Reports.Length);}
        public static void StartAt(int stage)
        {
            if(stage<1||stage>Reports.Length)throw new ArgumentOutOfRangeException(nameof(stage));
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before starting the review.");
            SessionState.SetInt("FenerRunner.V8Suite",0);
            SessionState.SetInt("FenerRunner.ReviewVersion",9);
            SessionState.SetInt(Key,stage);SessionState.SetString(Key+"Started",DateTime.UtcNow.ToString("O"));
            EditorApplication.delayCall+=()=>Launch(stage);
        }
        [InitializeOnLoadMethod] static void Register()
        {EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Launch(int stage)
        {
            switch(stage)
            {
                case 1:PresentationChecksV9.StartChecks();break;
                case 2:CharacterMotionChecksV9.StartChecks();break;
                case 3:CollisionChecksV8.StartChecks();break;
                case 4:CollisionChecksV6.StartChecks();break;
                case 5:RunMotionChecksV8.StartChecks();break;
                case 6:JumpChecksV7.StartChecks();break;
                case 7:CoinChecksV7.StartChecks();break;
                case 8:MobileChecks.StartChecks();break;
                case 9:ObstacleChecks.StartChecks();break;
                case 10:PresentationCaptureV9.StartCapture();break;
            }
        }
        static string ReportPath(int index)=>"../Validation/unity_"+Reports[index]+"_v9.json";
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            int stage=SessionState.GetInt(Key,0);if(stage<=0||stage>Reports.Length)return;
            string file=ReportPath(stage-1);
            DateTime began=DateTime.Parse(SessionState.GetString(Key+"Started",""),null,System.Globalization.DateTimeStyles.RoundtripKind);
            bool fresh=File.Exists(file)&&File.GetLastWriteTimeUtc(file)>=began;
            if(!fresh||!JsonUtility.FromJson<Result>(File.ReadAllText(file)).passed)
            {SessionState.SetInt(Key,0);Debug.LogError("V9 review stopped at "+file+". Inspect the report before resuming.");return;}
            if(stage==Reports.Length)
            {SessionState.SetInt(Key,0);WriteSummary();Debug.Log("V9 presentation and regression reviews completed.");return;}
            StartAt(stage+1);
        }
        static void WriteSummary()
        {
            var summary=new Summary{passed=true,utc=DateTime.UtcNow.ToString("O"),reportCount=Reports.Length,reports=new Stage[Reports.Length]};
            for(int i=0;i<Reports.Length;i++)
            {
                string path=ReportPath(i);var stage=new Stage{report=Path.GetFileName(path)};summary.reports[i]=stage;
                if(!File.Exists(path)){summary.passed=false;continue;}
                var data=JsonUtility.FromJson<Result>(File.ReadAllText(path));stage.passed=data.passed;
                stage.utc=File.GetLastWriteTimeUtc(path).ToString("O");stage.checks=data.checks==null?0:data.checks.Length;
                summary.totalChecks+=stage.checks;summary.passed&=stage.passed;
            }
            Directory.CreateDirectory("../Validation");File.WriteAllText("../Validation/unity_suite_v9.json",JsonUtility.ToJson(summary,true));
        }
    }
}
