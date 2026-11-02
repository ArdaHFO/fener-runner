using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FenerRunner.Editor
{
    public static class VersionFifteenAssets
    {
        [MenuItem("Fener Runner/00 - Apply chick and chase assets v15 (no APK)")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            // Update only requested art. Preserve the tuned scene, character rigs,
            // cloud project, course settings and all earlier authored content.
            SetupVerification.OpenSceneForReview();
            TokenBuilder.Build();ChaseSUVBuilder.Build();ChickAppIconV15.Build();
            foreach(var icon in UnityEngine.Object.FindObjectsByType<UIIcon>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(icon.kind==IconKind.Coins||(icon.kind==IconKind.Star&&icon.transform.parent.name=="Token counter"))
                {icon.kind=IconKind.Chick;icon.SetVerticesDirty();EditorUtility.SetDirty(icon);}
            PlayerSettings.bundleVersion="0.15.0";PlayerSettings.Android.bundleVersionCode=15;
            PlayerSettings.Android.forceInternetPermission=true;
            PlayerSettings.SplashScreen.show=false;PlayerSettings.SplashScreen.showUnityLogo=false;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();MultiplayerSetupV15.Verify();SetupVerification.Verify();
            Debug.Log("V15 chick currency, chase SUV and chick app icon prepared. No APK built.");
        }
        public static void PrepareAndExit(){Prepare();EditorApplication.Exit(0);}
    }
}
