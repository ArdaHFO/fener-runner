using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace FenerRunner.Editor
{
    public static class SetupVerification
    {
        [Serializable] private sealed class Report
        {
            public string unityVersion;
            public string utc;
            public bool passed;
            public int rootObjectCount, generatedAssetCount, animatorStateCount;
            public string[] clips;
            public string scope = "Editor asset and scene wiring checks. Visual quality, touch input, Play Mode and device performance are separate checks.";
        }
        [MenuItem("Fener Runner/2 - Verify imported assets and scene")]
        public static void Verify()
        {
            var importer = AssetImporter.GetAtPath(KartalImport.ModelPath) as ModelImporter;
            Require(importer != null && importer.animationType == ModelImporterAnimationType.Generic, "Generic rig");
            Require(importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel && importer.motionNodeName == "root", "Avatar and root node");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(KartalImport.ModelPath);
            Require(model.GetComponentsInChildren<Transform>(true).Any(t => t.name == "root"), "root bone");
            var avatar = AssetDatabase.LoadAllAssetsAtPath(KartalImport.ModelPath).OfType<Avatar>().SingleOrDefault();
            Require(avatar != null && avatar.isValid, "Valid imported Avatar");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(RunnerSceneBuilder.Generated + "/Animations/Kartal.controller");
            Require(controller != null, "Animator controller");
            string[] required=KartalImport.RequiredClips.Concat(new[]{"Flight","MenuWave","MenuPride","MenuReady","CollisionFall"}).ToArray();
            foreach (string name in required)
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RunnerSceneBuilder.Generated + "/Animations/" + name + ".anim");
                Require(clip != null && clip.length > 0f, "Clip " + name);
                Require(AnimationUtility.GetAnimationClipSettings(clip).loopTime == (name == "Run" || name == "Idle" || name == "Flight"), "Clip loop " + name);
                Require(controller.layers[0].stateMachine.states.Any(s => s.state.name == name && s.state.motion == clip), "Animator state " + name);
            }
            foreach (string kind in new[] { "basecolor", "normal" })
            {
                var texture = AssetImporter.GetAtPath("Assets/FenerRunner/Art/Kartal/textures/kartal_knit_" + kind + ".png") as TextureImporter;
                Require(texture != null && texture.sRGBTexture == (kind == "basecolor"), "Texture color space " + kind);
                Require(kind != "normal" || texture.textureType == TextureImporterType.NormalMap, "Normal import type");
            }
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(RunnerSceneBuilder.Generated + "/Prefabs/KartalPlayer.prefab");
            Require(player != null && player.GetComponent<CharacterController>() != null, "Player physics root");
            Animator animator = player.GetComponentInChildren<Animator>();
            Require(animator != null && animator.transform != player.transform && !animator.applyRootMotion, "Separate visual root, root motion off");
            var skin = player.GetComponentInChildren<SkinnedMeshRenderer>();
            Require(skin != null && skin.sharedMaterials.Length == 15, "Original 15 character materials");
            foreach (Material mat in skin.sharedMaterials)
                Require(mat != null && mat.shader != null && mat.shader.name == "FenerRunner/CharacterSoftLit" && mat.shader.isSupported, "Finished URP character material");
            Material knit = skin.sharedMaterials.Single(m => m.name.Contains("Cream_CableKnit"));
            Require(knit.GetTexture("_BumpMap") != null && knit.GetColor("_BaseColor").r>.95f && knit.GetColor("_BaseColor").b>.9f, "White knit material and source normal connected");
            var segment = AssetDatabase.LoadAssetAtPath<GameObject>(RunnerSceneBuilder.Generated + "/Prefabs/StadiumSegment.prefab");
            Collider[] colliders = segment.GetComponentsInChildren<Collider>();
            Require(colliders.Length == 1 && colliders[0] is BoxCollider, "Only ground collider in stadium segment");
            var ground = (BoxCollider)colliders[0];
            Require(Mathf.Abs(ground.center.y + ground.size.y * .5f) < .001f, "Ground surface y=0");
            Require(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset, "URP assigned");
            foreach(var item in new[]{("Engel_Atlama_SiyahKus_v01",8),("Engel_Kayma_KivircikKus_v01",3)})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(RunnerSceneBuilder.Generated+"/Prefabs/"+item.Item1+".prefab");
                Require(prefab!=null&&prefab.GetComponent<RunnerObstacle>()!=null,"Original obstacle prefab "+item.Item1);
                var boxes=prefab.GetComponentsInChildren<BoxCollider>();
                Require(boxes.Length==item.Item2&&boxes.All(b=>b.isTrigger&&b.gameObject.layer==8),"Obstacle trigger layout "+item.Item1);
                Require(prefab.GetComponentInChildren<Animator>()==null,"Static obstacle has no Animator");
                foreach(var material in prefab.GetComponentInChildren<MeshRenderer>().sharedMaterials)
                    Require(material.shader.name=="FenerRunner/CharacterSoftLit"&&material.shader.isSupported,"Finished obstacle URP material");
            }
            Require(segment.transform.Find("Animated supporters and flags")!=null,"Animated supporters mesh present");
            var scene = EditorSceneManager.GetActiveScene();
            Require(scene.path == RunnerSceneBuilder.ScenePath, "Prototype scene open");
            var objects = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            Require(objects.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "No missing scripts");
            foreach (var component in objects.SelectMany(t => t.GetComponents<MonoBehaviour>())
                         .Where(c => c != null && c.GetType().Namespace == "FenerRunner"))
            {
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.NextVisible(true))
                    // Unity UI uses its default material when this optional inherited field is null.
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.propertyPath != "m_Material")
                        Require(property.objectReferenceValue != null, component.GetType().Name + "." + property.propertyPath + " reference");
            }
            var game = Object.FindObjectsByType<RunnerGame>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Require(game.Length == 1, "Single game flow owner");
            var report = new Report { unityVersion = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"), passed = true,
                clips = required, rootObjectCount = scene.rootCount,
                generatedAssetCount = AssetDatabase.FindAssets("", new[] { RunnerSceneBuilder.Generated }).Length,
                animatorStateCount = controller.layers[0].stateMachine.states.Length };
            Directory.CreateDirectory("../Validation");
            File.WriteAllText("../Validation/unity_setup.json", JsonUtility.ToJson(report, true));
            Debug.Log("Fener Runner asset/scene verification passed. Report: ../Validation/unity_setup.json");
        }
        private static void Require(bool condition, string label)
        { if (!condition) throw new InvalidOperationException("Fener Runner setup check failed: " + label); }

        [Serializable] private sealed class PackageIssue
        {
            public string name, version;
            public string signatureStatus, signatureReason, signatureErrorCode;
            public string[] errors;
        }
        [Serializable] private sealed class PackageReport
        {
            public string unityVersion, utc;
            public PackageIssue[] issues;
        }
        public static void ReviewObstacles()
        {
            OpenSceneForReview();PortraitReview.Open();EditorApplication.delayCall+=ObstacleChecks.StartChecks;
        }
        public static void ReviewMobile(){RunnerSceneBuilder.Build();PortraitReview.Open();EditorApplication.delayCall+=MobileChecks.StartChecks;}
        public static void OpenSceneForReview()
        {
            EditorSceneManager.OpenScene(RunnerSceneBuilder.ScenePath);
            Verify();
            ReportPackages();
        }
        [MenuItem("Fener Runner/6 - Report package validation")]
        public static void ReportPackages()
        {
            var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            var report = new PackageReport
            {
                unityVersion = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"),
                issues = packages.Where(p => p.source.ToString() == "Registry" || (p.errors != null && p.errors.Length > 0))
                    .Select(p => new PackageIssue { name = p.name, version = p.version,
                        signatureStatus = SignatureField(p, "status"), signatureReason = SignatureField(p, "reason"),
                        signatureErrorCode = SignatureField(p, "errorCode"),
                        errors = p.errors == null ? new string[0] : p.errors.Select(e => e.errorCode + ": " + e.message).ToArray() }).ToArray()
            };
            File.WriteAllText("../Validation/unity_packages.json", JsonUtility.ToJson(report, true));
        }
        private static string SignatureField(UnityEditor.PackageManager.PackageInfo package, string field)
        {
            var property = typeof(UnityEditor.PackageManager.PackageInfo).GetProperty("signature",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            object signature = property == null ? null : property.GetValue(package);
            return signature?.GetType().GetProperty(field)?.GetValue(signature)?.ToString() ?? "Unavailable";
        }

        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }
        [MenuItem("Fener Runner/3 - Run movement and course checks")]
        public static void PlayChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (EditorSceneManager.GetActiveScene().path != RunnerSceneBuilder.ScenePath)
                EditorSceneManager.OpenScene(RunnerSceneBuilder.ScenePath);
            Verify();
            SessionState.SetBool("FenerRunner.PlayChecks", true);
            EditorApplication.EnterPlaymode();
        }
        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("FenerRunner.PlayChecks", false)) return;
            SessionState.SetBool("FenerRunner.PlayChecks", false);
            new GameObject("Prototype checks").AddComponent<PrototypeSmokeCheck>();
        }
    }
}
