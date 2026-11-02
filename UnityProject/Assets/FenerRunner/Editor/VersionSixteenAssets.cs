using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FenerRunner.Editor
{
    public static class VersionSixteenAssets
    {
        [MenuItem("Fener Runner/00 - Apply double actions and black SUV v16 (no APK)")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            SetupVerification.OpenSceneForReview();
            ChaseSUVBuilder.Build();
            // Keep the authored scene and layout. Only the existing brief control
            // reminder is updated to advertise the new two-swipe actions.
            foreach(var label in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(label.transform.parent==null||label.transform.parent.name!="Brief swipe reminder")continue;
                if(label.text=="ZIPLA")label.text="ZIPLA ×2";
                else if(label.text=="KAY")label.text="KAY ×2";
                EditorUtility.SetDirty(label);
            }
            PlayerSettings.bundleVersion="0.16.0";PlayerSettings.Android.bundleVersionCode=16;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();MultiplayerSetupV16.Verify();SetupVerification.Verify();
            Debug.Log("V16 black president SUV and double-action hints prepared. No APK built.");
        }
        public static void PrepareAndExit(){Prepare();EditorApplication.Exit(0);}
    }
}
