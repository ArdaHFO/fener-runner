using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FenerRunner.Editor
{
    public static class VersionFourValidation
    {
        [Serializable] class IdentityReport
        {
            public bool passed;public string utc;
            public float blackHeight,blackWidth,blackDepth;
            public int coachAccessories,runCurves;
            public string scope="Unity imported and generated mascot geometry/material validation. Stylized interpretations of the requested people, not photoreal likenesses.";
        }
        public static void BuildAndVerify()
        {
            RunnerSceneBuilder.Build();CoursePatternVerification.Run();VerifyIdentity();
        }
        [MenuItem("Fener Runner/12 - Run complete v4 Editor review")]
        public static void ReviewAll()
        {
            SetupVerification.OpenSceneForReview();PortraitReview.Open();
            SessionState.SetInt("FenerRunner.V4Suite",1);
            EditorApplication.delayCall+=ObstacleChecks.StartChecks;
        }
        public static void ReviewVisualAndMotion()
        {
            SetupVerification.OpenSceneForReview();PortraitReview.Open();
            SessionState.SetInt("FenerRunner.V4Suite",10);
            EditorApplication.delayCall+=ObstacleChecks.CaptureOnly;
        }
        [InitializeOnLoadMethod] static void RegisterSuite()
        {EditorApplication.playModeStateChanged-=SuiteChanged;EditorApplication.playModeStateChanged+=SuiteChanged;}
        static void SuiteChanged(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            int stage=SessionState.GetInt("FenerRunner.V4Suite",0);
            if(stage==1){SessionState.SetInt("FenerRunner.V4Suite",2);EditorApplication.delayCall+=MobileChecks.StartChecks;}
            else if(stage==2){SessionState.SetInt("FenerRunner.V4Suite",3);EditorApplication.delayCall+=SetupVerification.PlayChecks;}
            else if(stage==3){SessionState.SetInt("FenerRunner.V4Suite",0);Debug.Log("Complete v4 Editor review finished. Inspect all three JSON reports.");}
            else if(stage==10){SessionState.SetInt("FenerRunner.V4Suite",11);EditorApplication.delayCall+=MotionCapture.StartCapture;}
            else if(stage==11){SessionState.SetInt("FenerRunner.V4Suite",0);Debug.Log("Final visual and motion recording finished.");}
        }
        public static void VerifyIdentity()
        {
            string root=RunnerSceneBuilder.Generated;
            var player=AssetDatabase.LoadAssetAtPath<GameObject>(root+"/Prefabs/KartalPlayer.prefab");
            var skin=player.GetComponentInChildren<SkinnedMeshRenderer>();
            var knit=skin.sharedMaterials.Single(m=>m.name.Contains("Cream_CableKnit"));
            Color c=knit.GetColor("_BaseColor");
            if(c.r<.95f||c.g<.95f||c.b<.9f)throw new Exception("Coach sweater must be white");
            if(player.GetComponentsInChildren<MeshRenderer>().Length<2)throw new Exception("Coach identity details missing");
            var black=AssetDatabase.LoadAssetAtPath<GameObject>(root+"/Prefabs/Engel_Atlama_SiyahKus_v01.prefab");
            var collider=black.GetComponentInChildren<BoxCollider>();
            if(collider.size.y<1.9f||collider.size.x>2.0f)throw new Exception("Striker must be taller with a readable lane footprint");
            var animator=player.GetComponentInChildren<Animator>();
            foreach(var r in player.GetComponentsInChildren<Renderer>())
                if(r.sharedMaterials.Any(m=>m==null||m.shader==null))throw new Exception("Missing mascot material");
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(root+"/Animations/Run.anim");
            var report=new IdentityReport{passed=true,utc=DateTime.UtcNow.ToString("O"),blackHeight=collider.size.y,
                blackWidth=collider.size.x,blackDepth=collider.size.z,coachAccessories=player.GetComponentsInChildren<MeshRenderer>().Length,
                runCurves=AnimationUtility.GetCurveBindings(clip).Length};
            File.WriteAllText("../Validation/unity_identity_v4.json",JsonUtility.ToJson(report,true));
        }
    }
}
