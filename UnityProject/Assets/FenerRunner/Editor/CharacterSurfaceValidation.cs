using System;
using System.IO;
using UnityEditor;
namespace FenerRunner.Editor
{
    public static class CharacterSurfaceValidation
    {
        const string Flag="FenerRunner.SurfaceRegression";
        static readonly string[] Reports={"run_motion","collisions","obstacles"};
        [Serializable] class Result {public bool passed;public string[] checks;}
        [MenuItem("Fener Runner/50 - Verify finished character gameplay")]
        public static void Begin()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first");
            SessionState.SetInt("FenerRunner.V9Suite",0);SessionState.SetInt("FenerRunner.V8Suite",0);
            SessionState.SetInt("FenerRunner.ReviewVersion",10);StartStage(1);
        }
        [MenuItem("Fener Runner/51 - Capture finished character showcase")]
        public static void Showcase(){CharacterLookReview.StartChecks("showcase");}
        static void StartStage(int stage)
        {
            SessionState.SetInt(Flag,stage);SessionState.SetString(Flag+"Started",DateTime.UtcNow.ToString("O"));
            EditorApplication.delayCall+=()=>{if(stage==1)RunMotionChecksV8.StartChecks();else if(stage==2)CollisionChecksV8.StartChecks();else ObstacleChecks.StartChecks();};
        }
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            int stage=SessionState.GetInt(Flag,0);if(state!=PlayModeStateChange.EnteredEditMode||stage==0)return;
            string file="../Validation/unity_"+Reports[stage-1]+"_v10.json";
            bool passed=File.Exists(file)&&File.GetLastWriteTimeUtc(file)>=DateTime.Parse(SessionState.GetString(Flag+"Started",""),null,System.Globalization.DateTimeStyles.RoundtripKind)&&UnityEngine.JsonUtility.FromJson<Result>(File.ReadAllText(file)).passed;
            if(!passed){SessionState.SetInt(Flag,0);UnityEngine.Debug.LogError("Character regression stopped: "+file);return;}
            if(stage==Reports.Length){SessionState.SetInt(Flag,0);UnityEngine.Debug.Log("Finished character gameplay regression PASSED");return;}
            StartStage(stage+1);
        }
    }
}
