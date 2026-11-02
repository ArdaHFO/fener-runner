using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FenerRunner.Editor
{
    public static class VersionTwentyAssets
    {
        [MenuItem("Fener Runner/00 - Apply workshop, garage and hen v20 (no APK)")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            EditorSceneManager.OpenScene(RunnerSceneBuilder.ScenePath);
            ChaseSUVBuilder.BuildAll();HenBonusBuilderV20.Build();
            var game=UnityEngine.Object.FindFirstObjectByType<RunnerGame>();
            var old=UnityEngine.Object.FindFirstObjectByType<RunnerUI>();
            if(game==null||old==null)throw new InvalidOperationException("Authored runner scene is required.");
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach(var events in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(events.gameObject);
            RunnerUI view=RunnerInterfaceBuilder.Build(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            var serialized=new SerializedObject(game);serialized.FindProperty("ui").objectReferenceValue=view;serialized.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.bundleVersion="0.20.0";PlayerSettings.Android.bundleVersionCode=20;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),RunnerSceneBuilder.ScenePath);
            AssetDatabase.SaveAssets();SetupVerification.Verify();PortraitReview.Open();
            Debug.Log("V20 workshop, three vehicles, pause home button and hen assets prepared. No player build.");
        }
        public static void PrepareAndExit(){Prepare();EditorApplication.Exit(0);}
        public static void PrepareAndReview(){Prepare();VersionTwentyValidation.ReviewAndExit();}
    }
}
