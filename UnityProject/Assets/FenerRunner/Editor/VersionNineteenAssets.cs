using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FenerRunner.Editor
{
    public static class VersionNineteenAssets
    {
        [MenuItem("Fener Runner/00 - Apply human supporters and varied throws v19 (no APK)")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            SetupVerification.OpenSceneForReview();
            CrowdPropBuilderV19.Build();
            RefreshSupporters();
            PlayerSettings.bundleVersion="0.19.0";PlayerSettings.Android.bundleVersionCode=19;
            AssetDatabase.SaveAssets();
            // Re-open the authored scene to resolve updated prefab contents, without
            // recreating player rigs, layout, course settings, audio or lighting.
            EditorSceneManager.OpenScene(RunnerSceneBuilder.ScenePath);
            MultiplayerSetupV19.Verify();SetupVerification.Verify();
            Debug.Log("V19 human supporters, individual motion and varied stadium throws prepared. No APK built.");
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
                // These four labels shared the exact plane of the yellow rail,
                // causing glyph/rail depth flicker at changing world origins.
                float edge=settings.laneSpacing*1.5f+.7f;
                foreach(var label in contents.GetComponentsInChildren<TextMesh>(true))
                {
                    Vector3 p=label.transform.localPosition;
                    if(Mathf.Abs(Mathf.Abs(p.x)-(edge+2.28f))<.005f)
                    {p.x=Mathf.Sign(p.x)*(edge+2.25f);label.transform.localPosition=p;}
                }
                PrefabUtility.SaveAsPrefabAsset(contents,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(contents);}
        }
        public static void PrepareAndExit(){Prepare();EditorApplication.Exit(0);}
        public static void PrepareAndReviewCrowd(){Prepare();VersionNineteenValidation.ReviewNewFeaturesAndExit();}
        public static void PrepareAndReviewAll(){Prepare();VersionNineteenValidation.ReviewAndExit();}
        public static void PrepareCrowdAndExit()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            SetupVerification.OpenSceneForReview();RefreshSupporters();AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(RunnerSceneBuilder.ScenePath);SetupVerification.Verify();EditorApplication.Exit(0);
        }
    }
}
