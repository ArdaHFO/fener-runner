using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace FenerRunner.Editor
{
    public static class VersionSevenValidation
    {
        const string Key="FenerRunner.V7Suite";
        static readonly string[] Reports={"audio","jump","coins","variety","fatal_collisions","abilities","powerups","obstacles","mobile","final_visual","motion_capture"};
        [Serializable] class Result {public bool passed;}
        public static void Build(){RunnerSceneBuilder.Build();SetupVerification.Verify();}
        [MenuItem("Fener Runner/27 - Rebuild and verify v7")]
        public static void RebuildAndReview(){Build();ReviewAll();}
        [MenuItem("Fener Runner/28 - Verify v7 without rebuilding")]
        public static void ReviewAll(){AudioEnvironmentV7.EnableAudio();SetupVerification.OpenSceneForReview();PortraitReview.Open();StartAt(1);}
        public static void StartAt(int stage){SessionState.SetInt("FenerRunner.ReviewVersion",7);SessionState.SetInt(Key,stage);EditorApplication.delayCall+=()=>Launch(stage);}
        public static void ResumeFromJump(){StartAt(2);}
        public static void ResumeFromVariety(){StartAt(4);}
        public static void ResumeFromRegression(){StartAt(5);}
        [MenuItem("Fener Runner/29 - Resume v7 economy and course review")]
        public static void ResumeFromEconomy(){StartAt(7);}
        [MenuItem("Fener Runner/30 - Resume v7 course and visual review")]
        public static void ResumeFromCourse(){StartAt(8);}
        public static void ResumeFromCapture(){StartAt(11);}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Launch(int stage)
        {
            switch(stage){case 1:AudioChecksV7.StartChecks();break;case 2:JumpChecksV7.StartChecks();break;case 3:CoinChecksV7.StartChecks();break;case 4:PowerVarietyChecksV7.StartChecks();break;case 5:CollisionChecksV6.StartChecks();break;case 6:AbilityChecksV6.StartChecks();break;case 7:PowerupChecks.StartChecks();break;case 8:ObstacleChecks.StartChecks();break;case 9:MobileChecks.StartChecks();break;case 10:VisualFinishChecks.StartChecks();break;case 11:MotionCapture.StartCapture();break;}
        }
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            int stage=SessionState.GetInt(Key,0);if(stage<=0||stage>Reports.Length)return;
            string file="../Validation/unity_"+Reports[stage-1]+"_v7.json";
            if(!File.Exists(file)||!JsonUtility.FromJson<Result>(File.ReadAllText(file)).passed)
            {SessionState.SetInt(Key,0);Debug.LogError("V7 review stopped at "+file+". Inspect failure before continuing.");return;}
            if(stage==Reports.Length){SessionState.SetInt(Key,0);Debug.Log("All v7 reviews completed.");return;}
            StartAt(stage+1);
        }
    }
}
