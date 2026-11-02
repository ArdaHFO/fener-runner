using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace FenerRunner.Editor
{
    public static class VersionSixValidation
    {
        const string Key="FenerRunner.V6Suite";
        public static void Build(){RunnerSceneBuilder.Build();SetupVerification.Verify();}
        [MenuItem("Fener Runner/20 - Run v6 collision audio and ability review")]
        public static void ReviewAll(){SetupVerification.OpenSceneForReview();PortraitReview.Open();SessionState.SetInt(Key,1);EditorApplication.delayCall+=CollisionChecksV6.StartChecks;}
        public static void ReviewAbilities(){SetupVerification.OpenSceneForReview();PortraitReview.Open();EditorApplication.delayCall+=AbilityChecksV6.StartChecks;}
        public static void ReviewAudio(){SetupVerification.OpenSceneForReview();PortraitReview.Open();EditorApplication.delayCall+=AudioChecksV6.StartChecks;}
        public static void ReviewCollision(){SetupVerification.OpenSceneForReview();PortraitReview.Open();EditorApplication.delayCall+=CollisionChecksV6.StartChecks;}
        public static void ReviewFinal(){SetupVerification.OpenSceneForReview();PortraitReview.Open();SessionState.SetInt(Key,7);EditorApplication.delayCall+=VisualFinishChecks.StartChecks;}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            int stage=SessionState.GetInt(Key,0);if(stage<=0)return;
            SessionState.SetInt(Key,stage+1);
            if(stage==1)EditorApplication.delayCall+=AudioChecksV6.StartChecks;
            else if(stage==2)EditorApplication.delayCall+=AbilityChecksV6.StartChecks;
            else if(stage==3)EditorApplication.delayCall+=PowerupChecks.StartChecks;
            else if(stage==4)EditorApplication.delayCall+=ObstacleChecks.StartChecks;
            else if(stage==5)EditorApplication.delayCall+=MobileChecks.StartChecks;
            else if(stage==6)EditorApplication.delayCall+=VisualFinishChecks.StartChecks;
            else if(stage==7)EditorApplication.delayCall+=MotionCapture.StartCapture;
            else {SessionState.SetInt(Key,0);Debug.Log("Complete v6 review finished.");}
        }
    }
}
