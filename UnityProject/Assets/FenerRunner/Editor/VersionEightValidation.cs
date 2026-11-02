using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace FenerRunner.Editor
{
    public static class VersionEightValidation
    {
        const string Key="FenerRunner.V8Suite";
        static readonly string[] Reports={"collisions","fatal_collisions","course","run_motion","jump","coins","obstacles","mobile","motion_capture"};
        [Serializable] class Result {public bool passed;}
        [MenuItem("Fener Runner/31 - Rebuild and verify v8")]
        public static void RebuildAndReview(){RunnerSceneBuilder.Build();ReviewAll();}
        [MenuItem("Fener Runner/32 - Verify v8 without rebuilding")]
        public static void ReviewAll(){SetupVerification.OpenSceneForReview();PortraitReview.Open();StartAt(1);}
        [MenuItem("Fener Runner/33 - Resume v8 course and motion")]
        public static void ResumeCourse(){StartAt(3);}
        [MenuItem("Fener Runner/34 - Resume v8 gameplay regression")]
        public static void ResumeRegression(){StartAt(5);}
        [MenuItem("Fener Runner/35 - Resume v8 visual and capture")]
        public static void ResumeVisual(){StartAt(7);}
        public static void StartAt(int stage)
        {
            SessionState.SetInt("FenerRunner.ReviewVersion",8);
            SessionState.SetInt(Key,stage);
            SessionState.SetString(Key+"Started",DateTime.UtcNow.ToString("O"));
            EditorApplication.delayCall+=()=>Launch(stage);
        }
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Launch(int stage)
        {
            switch(stage){case 1:CollisionChecksV8.StartChecks();break;case 2:CollisionChecksV6.StartChecks();break;case 3:CourseChecksV8.StartChecks();break;case 4:RunMotionChecksV8.StartChecks();break;case 5:JumpChecksV7.StartChecks();break;case 6:CoinChecksV7.StartChecks();break;case 7:ObstacleChecks.StartChecks();break;case 8:MobileChecks.StartChecks();break;case 9:MotionCapture.StartCapture();break;}
        }
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            int stage=SessionState.GetInt(Key,0);if(stage<=0||stage>Reports.Length)return;
            string file="../Validation/unity_"+Reports[stage-1]+"_v8.json";
            bool fresh=File.Exists(file)&&File.GetLastWriteTimeUtc(file)>=DateTime.Parse(SessionState.GetString(Key+"Started",""),null,System.Globalization.DateTimeStyles.RoundtripKind);
            if(!fresh||!JsonUtility.FromJson<Result>(File.ReadAllText(file)).passed)
            {SessionState.SetInt(Key,0);Debug.LogError("V8 review stopped at "+file+". Inspect failure before continuing.");return;}
            if(stage==Reports.Length){SessionState.SetInt(Key,0);SessionState.SetInt("FenerRunner.ReviewVersion",7);Debug.Log("All v8 reviews completed.");return;}
            StartAt(stage+1);
        }
    }
}
