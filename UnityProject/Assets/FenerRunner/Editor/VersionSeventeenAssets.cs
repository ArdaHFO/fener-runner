using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FenerRunner.Editor
{
    public static class VersionSeventeenAssets
    {
        [MenuItem("Fener Runner/00 - Apply precise contacts and lively stands v17 (no APK)")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            SetupVerification.OpenSceneForReview();
            ObstacleBuilder.Build();
            CrowdBottleBuilder.Build();
            RefreshSupporters();
            PlayerSettings.bundleVersion="0.17.0";PlayerSettings.Android.bundleVersionCode=17;
            AssetDatabase.SaveAssets();
            // Re-open the authored scene to resolve updated prefab contents, without
            // recreating player rigs, layout, course settings, audio or lighting.
            EditorSceneManager.OpenScene(RunnerSceneBuilder.ScenePath);
            MultiplayerSetupV17.Verify();SetupVerification.Verify();
            Debug.Log("V17 precise obstacle contacts, articulated supporters and pooled bottle throws prepared. No APK built.");
        }
        static void RefreshSupporters()
        {
            const string path=RunnerSceneBuilder.Generated+"/Prefabs/StadiumSegment.prefab";
            var settings=AssetDatabase.LoadAssetAtPath<RunnerSettings>(RunnerSceneBuilder.Generated+"/RunnerSettings.asset");
            GameObject contents=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(string name in new[]{"Animated supporters and flags","Distant supporters"})
                {
                    Transform child=contents.transform.Find(name);
                    if(child==null)throw new InvalidOperationException("Missing authored crowd child "+name);
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                var lod=contents.GetComponent<LODGroup>();if(lod!=null)UnityEngine.Object.DestroyImmediate(lod);
                SupporterBuilder.Build(contents.transform,settings);
                PrefabUtility.SaveAsPrefabAsset(contents,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(contents);}
        }
        public static void PrepareAndExit(){Prepare();EditorApplication.Exit(0);}
        public static void PrepareCrowdAndExit()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            SetupVerification.OpenSceneForReview();RefreshSupporters();AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(RunnerSceneBuilder.ScenePath);SetupVerification.Verify();EditorApplication.Exit(0);
        }
    }
}
