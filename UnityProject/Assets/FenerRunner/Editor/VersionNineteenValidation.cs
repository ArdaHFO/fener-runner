using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner.Editor
{
    // Focused regression for this presentation change. The completed v18 full
    // suite remains archived; this does not claim a new full39-stage run.
    public static class VersionNineteenValidation
    {
        const string Key="FenerRunner.V19Suite";
        static readonly string[] Reports={"crowd_human","crowd_natural_motion","crowd_props","crowd_motion","crowd_throws","crowd_reactions","crowd_look","contacts","lifecycle","obstacles"};
        [Serializable] sealed class Result{public bool passed;public string[] checks,errors;}
        [Serializable] sealed class Stage{public string report,utc;public bool passed;public int checks;}
        [Serializable] sealed class Summary
        {
            public bool passed;public string utc;public int totalChecks;public Stage[] reports;
            public string scope="Ten focused native Unity Play Mode stages for adult crowd appearance, GPU motion independence, frequent varied props, existing crowd regressions, contact matrix, application lifecycle and100-second route-following gameplay. No player/APK build or physical-phone performance test. The v18 full39-stage suite is retained separately.";
        }
        [MenuItem("Fener Runner/00 - Verify human crowd v19 without APK")]
        public static void Review(){Begin(false,false);}
        public static void ReviewAndExit(){Begin(true,false);}
        public static void ReviewNewFeaturesAndExit(){Begin(true,true);}
        static void Begin(bool exit,bool focused)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            foreach(string old in new[]{"V12Suite","V11Suite","V9Suite","V8Suite","SurfaceRegression","V13Suite","V14Suite","V15Suite","V16Suite","V17Suite","V18Suite"})SessionState.SetInt("FenerRunner."+old,0);
            MultiplayerSetupV19.Verify();EditorPackageHealthV19.Record();SetupVerification.OpenSceneForReview();PortraitReview.Open();
            SessionState.SetBool(Key+"Exit",exit);SessionState.SetBool(Key+"Focused",focused);
            SessionState.SetString(Key+"RunStart",DateTime.UtcNow.ToString("O"));StartAt(1);
        }
        static void StartAt(int stage)
        {
            SessionState.SetInt("FenerRunner.ReviewVersion",19);SessionState.SetInt(Key,stage);
            SessionState.SetInt(Key+"StageErrors",0);SessionState.SetString(Key+"LastError","");
            SessionState.SetString(Key+"StageStart",DateTime.UtcNow.ToString("O"));
            Debug.Log("V19 starting stage "+stage+" / "+Reports[stage-1]);EditorApplication.delayCall+=()=>Launch(stage);
        }
        static void Launch(int stage)
        {
            switch(stage)
            {
                case 1:CrowdHumanChecksV19.StartChecks();break;
                case 2:CrowdNaturalMotionChecksV19.StartChecks();break;
                case 3:CrowdPropsChecksV19.StartChecks();break;
                case 4:CrowdMotionChecksV17.StartChecks();break;
                case 5:CrowdThrowsChecksV17.StartChecks();break;
                case 6:CrowdReactionsChecksV18.StartChecks();break;
                case 7:CrowdLookChecksV18.StartChecks();break;
                case 8:ContactMatrixChecksV11.StartChecks();break;
                case 9:LifecycleChecksV15.StartChecks();break;
                case 10:ObstacleChecks.StartChecks();break;
            }
        }
        static string FileFor(int i)=>"../Validation/unity_"+Reports[i]+"_v19.json";
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
                SessionState.SetInt(Key,0);Debug.LogError("V19 stopped at stage "+stage+": "+file+"; runtime error: "+SessionState.GetString(Key+"LastError",""));
                if(SessionState.GetBool(Key+"Exit",false))EditorApplication.delayCall+=()=>EditorApplication.Exit(1);return;
            }
            if(SessionState.GetBool(Key+"Focused",false)&&stage==3)
            {SessionState.SetInt(Key,0);Debug.Log("V19 new crowd checks PASSED");EditorApplication.delayCall+=()=>EditorApplication.Exit(0);return;}
            if(stage<Reports.Length){StartAt(stage+1);return;}
            SessionState.SetInt(Key,0);var summary=new Summary{passed=true,utc=DateTime.UtcNow.ToString("O"),reports=new Stage[Reports.Length]};
            DateTime runStart=DateTime.Parse(SessionState.GetString(Key+"RunStart",""),null,System.Globalization.DateTimeStyles.RoundtripKind);
            for(int i=0;i<Reports.Length;i++)
            {
                string path=FileFor(i);var result=JsonUtility.FromJson<Result>(File.ReadAllText(path));
                var entry=new Stage{report=Path.GetFileName(path),utc=File.GetLastWriteTimeUtc(path).ToString("O"),passed=result.passed&&File.GetLastWriteTimeUtc(path)>=runStart,checks=result.checks==null?0:result.checks.Length};
                summary.reports[i]=entry;summary.passed&=entry.passed;summary.totalChecks+=entry.checks;
            }
            File.WriteAllText("../Validation/unity_suite_v19.json",JsonUtility.ToJson(summary,true));
            Debug.Log("V19 suite "+(summary.passed?"PASSED":"INCOMPLETE")+" with "+summary.totalChecks+" checks");
            if(SessionState.GetBool(Key+"Exit",false))EditorApplication.delayCall+=()=>EditorApplication.Exit(summary.passed?0:1);
        }
    }
}
