using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace FenerRunner.Editor
{
    public static class VersionFiveValidation
    {
        [Serializable] class IdentityReport
        {
            public bool passed;public string utc;
            public bool originalFeatheredMesh,whiteSweater,noHeadAccessories,allSevenVoices;
            public int animatorStates;
        }
        public static void BuildAndVerify(){RunnerSceneBuilder.Build();CoursePatternVerification.Run();VerifyIdentity();SprintValidation.Verify();}
        [MenuItem("Fener Runner/14 - Run complete v5 review")]
        public static void ReviewAll()
        {
            SetupVerification.OpenSceneForReview();PortraitReview.Open();SessionState.SetInt("FenerRunner.V5Suite",1);
            EditorApplication.delayCall+=ObstacleChecks.StartChecks;
        }
        public static void ReviewPowers()
        {SetupVerification.OpenSceneForReview();PortraitReview.Open();EditorApplication.delayCall+=PowerupChecks.StartChecks;}
        public static void ReviewFinal()
        {SessionState.SetInt("FenerRunner.V5Suite",12);EditorApplication.delayCall+=VisualFinishChecks.StartChecks;}
        public static void ReviewVisualAndMotion()
        {SetupVerification.OpenSceneForReview();PortraitReview.Open();SessionState.SetInt("FenerRunner.V5Suite",10);EditorApplication.delayCall+=ObstacleChecks.CaptureOnly;}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            int stage=SessionState.GetInt("FenerRunner.V5Suite",0);
            if(stage==1){SessionState.SetInt("FenerRunner.V5Suite",2);EditorApplication.delayCall+=MobileChecks.StartChecks;}
            else if(stage==2){SessionState.SetInt("FenerRunner.V5Suite",3);EditorApplication.delayCall+=SetupVerification.PlayChecks;}
            else if(stage==3){SessionState.SetInt("FenerRunner.V5Suite",4);EditorApplication.delayCall+=PowerupChecks.StartChecks;}
            else if(stage==4){SessionState.SetInt("FenerRunner.V5Suite",5);EditorApplication.delayCall+=DifficultyChecks.StartChecks;}
            else if(stage==5){SessionState.SetInt("FenerRunner.V5Suite",6);EditorApplication.delayCall+=MotionCapture.StartCapture;}
            else if(stage==6||stage==11){SessionState.SetInt("FenerRunner.V5Suite",0);Debug.Log("Complete v5 Editor review finished.");}
            else if(stage==10){SessionState.SetInt("FenerRunner.V5Suite",11);EditorApplication.delayCall+=MotionCapture.StartCapture;}
            else if(stage==12){SessionState.SetInt("FenerRunner.V5Suite",11);EditorApplication.delayCall+=MotionCapture.StartCapture;}
        }
        public static void VerifyIdentity()
        {
            var player=AssetDatabase.LoadAssetAtPath<GameObject>(RunnerSceneBuilder.Generated+"/Prefabs/KartalPlayer.prefab");
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(KartalImport.ModelPath);
            var skin=player.GetComponentInChildren<SkinnedMeshRenderer>();
            Color knit=skin.sharedMaterials.Single(m=>m.name.Contains("Cream_CableKnit")).GetColor("_BaseColor");
            var report=new IdentityReport{utc=DateTime.UtcNow.ToString("O"),originalFeatheredMesh=skin.sharedMesh==original.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh,
                noHeadAccessories=player.GetComponentsInChildren<MeshRenderer>().Length==0,whiteSweater=knit.r>.95f&&knit.g>.95f&&knit.b>.9f,
                allSevenVoices=UnityEngine.Object.FindFirstObjectByType<StadiumSound>().HasUserVoices,
                animatorStates=player.GetComponentInChildren<Animator>().runtimeAnimatorController.animationClips.Length};
            report.passed=report.originalFeatheredMesh&&report.noHeadAccessories&&report.whiteSweater&&report.allSevenVoices&&report.animatorStates==8;
            File.WriteAllText("../Validation/unity_identity_v5.json",JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidOperationException("v5 reference character/audio verification failed");
        }
    }
}
