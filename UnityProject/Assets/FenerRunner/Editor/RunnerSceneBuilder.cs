using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace FenerRunner.Editor
{
    public static class RunnerSceneBuilder
    {
        public const string Generated = "Assets/FenerRunner/Generated";
        public const string ScenePath = Generated + "/Scenes/FenerRunner.unity";
        private const string TexturePath = "Assets/FenerRunner/Art/Kartal/textures/";
        private static readonly string[] Folders = { "Materials", "Meshes", "Prefabs", "Animations", "Scenes", "Rendering" };

        [InitializeOnLoadMethod]
        private static void OnLoad() { EditorApplication.delayCall += FirstOpen; }
        private static void FirstOpen()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += FirstOpen; return; }
            if (!File.Exists(ScenePath)) Build();
        }

        [MenuItem("Fener Runner/1 - Build or rebuild prototype")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before rebuilding.");
            if (!Application.isBatchMode && EditorSceneManager.GetActiveScene().path != ScenePath &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Generated);
            CharacterSurfaceBuilder.BeginBuild();
            foreach (string folder in Folders) Directory.CreateDirectory(Generated + "/" + folder);
            AssetDatabase.Refresh();
            ConfigurePlayer();
            ConfigurePipeline();
            // Force the provided model through our import policy, including on a repeated setup.
            AssetDatabase.ImportAsset(KartalImport.ModelPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(TexturePath + "kartal_knit_basecolor.png", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(TexturePath + "kartal_knit_normal.png", ImportAssetOptions.ForceUpdate);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(KartalImport.ModelPath);
            if (model == null) throw new FileNotFoundException("The supplied Kartal FBX is required.", KartalImport.ModelPath);
            AnimationClip[] clips = PrepareClips();
            AnimatorController controller = PrepareController(clips);
            var settings = AssetDatabase.LoadAssetAtPath<RunnerSettings>(Generated + "/RunnerSettings.asset");
            if (settings == null)
            { settings = ScriptableObject.CreateInstance<RunnerSettings>(); AssetDatabase.CreateAsset(settings, Generated + "/RunnerSettings.asset"); }
            settings.forwardSpeed = 18f; settings.maxSpeed = 34f; settings.speedRampDistance = 1800f;
            EditorUtility.SetDirty(settings);
            // These scene dimensions match the single settings asset, including after tuning and rebuilding.
            if (Mathf.Abs(settings.chunkLength - 40f) > .001f)
                throw new InvalidOperationException("The stadium art uses 40 m modules. Keep chunkLength at 40.");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            RunnerObstacle[] obstaclePrefabs = ObstacleBuilder.Build();
            GameObject tokenPrefab = TokenBuilder.Build();
            ChaseSUVBuilder.Build();
            CrowdBottleBuilder.Build();
            AudioClip crowdRecording = AudioBuilder.Build();
            GameObject segment = StadiumMeshBuilder.Build(settings, EnvironmentMaterials(), font);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLighting();
            GameObject playerPrefab = BuildPlayer(model, settings, controller, clips);
            RunMotionBuilder.GroundSprint(playerPrefab,clips.Single(c=>c.name=="Run"));
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            var motor = player.GetComponent<RunnerMotor>();
            var animation = player.GetComponent<RunnerAnimation>();
            var course = new GameObject("Endless stadium", typeof(EndlessCourse)).GetComponent<EndlessCourse>();
            course.Configure(settings, segment);
            // A fixed pool is also visible before Play, so the first opened scene already shows the stadium.
            // The preview is removed on Awake; the runtime pool belongs solely to EndlessCourse.
            var preview = new GameObject("Editor stadium preview", typeof(EditorStadiumPreview));
            for (int i = 0; i < settings.chunkCount; i++)
            {
                var part = (GameObject)PrefabUtility.InstantiatePrefab(segment, preview.transform);
                part.transform.localPosition = Vector3.forward * ((i - 2) * settings.chunkLength);
            }
            player.transform.position = new Vector3(0f, .02f, 8f);
            var cameraObject = new GameObject("Follow Camera", typeof(Camera), typeof(AudioListener), typeof(FollowCamera));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 58f; camera.nearClipPlane = .1f; camera.farClipPlane = 215f;
            camera.clearFlags = CameraClearFlags.Skybox;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            var rig = cameraObject.GetComponent<FollowCamera>(); rig.Configure(settings, player.transform); rig.Snap();
            RunnerUI ui = RunnerInterfaceBuilder.Build(font);
            var input = new GameObject("Input", typeof(RunnerInput)).GetComponent<RunnerInput>(); input.Configure(settings);
            var game = new GameObject("Game", typeof(RunnerGame),typeof(StadiumSound),typeof(RunnerFeedback),typeof(StadiumWeather),typeof(RunnerPowerups)).GetComponent<RunnerGame>();
            game.GetComponent<StadiumSound>().Configure(crowdRecording,AudioBuilder.BuildVoices(),AudioBuilder.BuildMenuMusic());
            game.GetComponent<RunnerPowerups>().Configure(settings,PowerupBuilder.BuildPickups(),PowerupBuilder.BuildFlightVisual(),PowerupBuilder.BuildShieldVisual(),PowerupBuilder.BuildAbilityVisuals());
            game.GetComponent<RunnerFeedback>().Configure(TokenBuilder.SparkleMaterial());
            var rain=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")){name="Rain",enableInstancing=true};
            rain.SetFloat("_Surface",1);rain.SetFloat("_Blend",0);rain.SetFloat("_ZWrite",0);
            rain.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);rain.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            rain.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");rain.renderQueue=3000;rain.SetColor("_BaseColor",new Color(.8f,.9f,1,.4f));
            game.GetComponent<StadiumWeather>().Configure(RenderSettings.sun,RenderSettings.skybox,Upsert(rain,Generated+"/Materials/Rain.mat"));
            var obstacles=new GameObject("Obstacle pool",typeof(ObstacleCourse)).GetComponent<ObstacleCourse>();
            obstacles.Configure(settings,obstaclePrefabs[0],obstaclePrefabs[1]);
            var tokens=new GameObject("Reward pool",typeof(TokenCourse)).GetComponent<TokenCourse>();
            tokens.Configure(settings,tokenPrefab);
            game.Configure(settings, motor, animation, input, rig, course, ui,obstacles,tokens);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            SetupVerification.Verify();
            Selection.activeGameObject = player;
            Debug.Log("Fener Runner scene is ready. Choose 9:16 in Game view, then press Play and OYNA.");
        }

        internal static T Upsert<T>(T source, string path) where T : Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(source, path); return source; }
            // Serialized replacement alone can retain an already uploaded index buffer.
            // Invalidate it before topology changes shift the later material ranges.
            if(existing is Mesh oldMesh)oldMesh.Clear(false);
            EditorUtility.CopySerialized(source, existing);
            if(existing is Mesh updatedMesh){updatedMesh.MarkModified();updatedMesh.UploadMeshData(false);}
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(source);
            return existing;
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Fener Runner Prototype";
            PlayerSettings.productName = "Fener Runner";
            PlayerSettings.defaultScreenWidth = 540; PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.fenerrunner.prototype");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.fenerrunner.prototype");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        }

        private static void ConfigurePipeline()
        {
            const string rendererPath = Generated + "/Rendering/FenerRenderer.asset";
            const string pipelinePath = Generated + "/Rendering/FenerURP.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            { renderer = ScriptableObject.CreateInstance<UniversalRendererData>(); renderer.name = "FenerRenderer"; AssetDatabase.CreateAsset(renderer, rendererPath); }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null) { pipeline = UniversalRenderPipelineAsset.Create(renderer); AssetDatabase.CreateAsset(pipeline, pipelinePath); }
            pipeline.renderScale = 1f;
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = false;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.shadowDistance = 45f;
            pipeline.shadowCascadeCount = 2;
            CharacterSurfaceBuilder.EnableSoftShadows(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
        }

        private static AnimationClip[] PrepareClips()
        {
            var imported = AssetDatabase.LoadAllAssetsAtPath(KartalImport.ModelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            float[] expectedSeconds = { 2f, .8f, .9f, .8f, 1f, 1f, 1.5f };
            var result = new AnimationClip[KartalImport.RequiredClips.Length+5];
            for (int i = 0; i < KartalImport.RequiredClips.Length; i++)
            {
                string name = KartalImport.RequiredClips[i];
                AnimationClip source = imported.SingleOrDefault(c => KartalImport.ShortName(c.name) == name);
                if (source == null) throw new InvalidOperationException("FBX clip not found: " + name + ". Found: " + string.Join(", ", imported.Select(c => c.name)));
                if (Mathf.Abs(source.length - expectedSeconds[i]) > .08f)
                    throw new InvalidOperationException("Unexpected duration for " + name + ": " + source.length);
                var copy = new AnimationClip(); EditorUtility.CopySerialized(source, copy); copy.name = name;
                if(name=="Run")RunMotionBuilder.Build(copy);
                var clipSettings = AnimationUtility.GetAnimationClipSettings(copy);
                clipSettings.loopTime = name == "Idle" || name == "Run";
                clipSettings.loopBlend = clipSettings.loopTime;
                AnimationUtility.SetAnimationClipSettings(copy, clipSettings);
                result[i] = Upsert(copy, Generated + "/Animations/" + name + ".anim");
            }
            var flight=RunMotionBuilder.BuildFlight(result[0]);
            result[KartalImport.RequiredClips.Length]=Upsert(flight,Generated+"/Animations/Flight.anim");
            var menuClips=RunMotionBuilder.BuildMenuClips(result[0]);
            for(int i=0;i<menuClips.Length;i++)result[KartalImport.RequiredClips.Length+1+i]=Upsert(menuClips[i],Generated+"/Animations/"+menuClips[i].name+".anim");
            result[result.Length-1]=Upsert(RunMotionBuilder.BuildDeathClip(result[0]),Generated+"/Animations/CollisionFall.anim");
            return result;
        }

        private static AnimatorController PrepareController(AnimationClip[] clips)
        {
            string path = Generated + "/Animations/Kartal.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            if (!controller.parameters.Any(p => p.name == "MotionRate")) controller.AddParameter("MotionRate", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            foreach (AnimationClip clip in clips)
            {
                AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == clip.name);
                if (state == null) state = machine.AddState(clip.name);
                state.motion = clip; state.speed = 1f; state.speedParameter = "MotionRate"; state.speedParameterActive = true;
                if (clip.name == "Idle") machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static GameObject BuildPlayer(GameObject model, RunnerSettings settings, AnimatorController controller, AnimationClip[] clips)
        {
            var root = new GameObject("Kartal Player", typeof(CharacterController), typeof(RunnerMotor), typeof(RunnerAnimation));
            var capsule = root.GetComponent<CharacterController>();
            capsule.height = 2.05f; capsule.radius = .38f; capsule.center = new Vector3(0f, 1.025f, 0f);
            capsule.skinWidth = .025f; capsule.stepOffset = .1f; capsule.minMoveDistance = 0f;
            var accents = new GameObject("Motion accents"); accents.transform.SetParent(root.transform, false);
            var visual = new GameObject("Visual Only"); visual.transform.SetParent(accents.transform, false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, visual.transform);
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length != 1) throw new InvalidOperationException("Expected the supplied single skinned Kartal mesh.");
            var renderer = renderers[0];
            // Derive forward from the actual beak submesh, rather than guessing FBX axis conventions.
            int beak = Array.FindIndex(renderer.sharedMaterials, m => m != null && m.name.Contains("Orange_Beak"));
            if (beak < 0) throw new InvalidOperationException("Kartal beak material is missing.");
            Mesh mesh = renderer.sharedMesh;
            Vector3[] vertices = mesh.vertices; int[] triangles = mesh.GetTriangles(beak);
            Vector3 centre = Vector3.zero;
            foreach (int index in triangles) centre += renderer.transform.TransformPoint(vertices[index]);
            centre /= triangles.Length;
            Vector3 heading = centre - renderer.bounds.center; heading.y = 0f;
            visual.transform.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg, 0f);
            float height = renderer.bounds.size.y;
            if (height < .001f) throw new InvalidOperationException("Kartal imported bounds are empty.");
            visual.transform.localScale = Vector3.one * (2.04f / height);
            visual.transform.localPosition = Vector3.up * (.025f - renderer.bounds.min.y);
            Material[] remapped = renderer.sharedMaterials.Select(CharacterMaterial).ToArray();
            renderer.sharedMaterials = remapped;
            CharacterIdentityBuilder.Coach(root,renderer);
            renderer.updateWhenOffscreen = true;
            // Generous bounds cover the actual full flip without animation culling.
            renderer.localBounds = new Bounds(renderer.localBounds.center, renderer.localBounds.size * 3f);
            Animator animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(KartalImport.ModelPath).OfType<Avatar>().Single();
            if (!animator.avatar.isValid) throw new InvalidOperationException("Kartal Generic avatar is invalid.");
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            // Imported renderer bounds contain animation padding. Ground actual soles instead.
            clips.Single(c=>c.name=="Idle").SampleAnimation(instance,0f);
            var groundingMesh=new Mesh();renderer.BakeMesh(groundingMesh);
            float lowest=groundingMesh.vertices.Min(v=>renderer.transform.TransformPoint(v).y);
            visual.transform.localPosition-=Vector3.up*(lowest-.025f);
            Object.DestroyImmediate(groundingMesh);
            var motor = root.GetComponent<RunnerMotor>(); motor.Configure(settings);
            root.GetComponent<RunnerAnimation>().Configure(settings, motor, animator, clips);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Generated + "/Prefabs/KartalPlayer.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material CharacterMaterial(Material source)
        {
            if (source == null) throw new InvalidOperationException("Missing character material.");
            var palette = new Dictionary<string, string>
            {
                { "Golden_Feathers", "F3CD57" }, { "Feather_Highlights", "F7DC78" }, { "Feather_Warm", "E4B644" },
                { "Orange_Beak", "E99A32" }, { "Lower_Beak", "D88528" }, { "Orange_Feet", "CA792E" },
                { "Toe_Tips", "DEAD68" }, { "Mouth_Interior", "52291F" }, { "Strong_Brows", "B48635" },
                { "Eye_Ivory", "FFF5DB" }, { "Eye_Amber", "98582C" }, { "Eye_Pupils", "251B16" },
                { "Eye_Catchlight", "FFFFFF" }, { "Sweater_Ribbing", "F4F3EE" }, { "Cream_CableKnit", "FFFEFA" }
            };
            string key = palette.Keys.FirstOrDefault(source.name.Contains);
            if (key == null) throw new InvalidOperationException("Unknown supplied character material: " + source.name);
            Material mat = Lit(source.name, palette[key], .15f);
            if (key == "Cream_CableKnit")
            {
                // URP's default white albedo keeps the sweater neutral; retain the authored knit normal.
                mat.SetTexture("_BaseMap", null);
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + "kartal_knit_normal.png"));
                mat.SetFloat("_BumpScale", .65f); mat.EnableKeyword("_NORMALMAP");
            }
            return Upsert(mat, Generated + "/Materials/" + source.name + ".mat");
        }

        private static Material Lit(string name, string hex, float smoothness = .1f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable. Resolve the URP package first.");
            var mat = new Material(shader) { name = name, enableInstancing = true };
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", smoothness); mat.SetFloat("_Metallic", 0f);
            return mat;
        }
        private static Dictionary<string, Material> EnvironmentMaterials()
        {
            var colors = new Dictionary<string, string> { { "Navy", "071B45" }, { "Yellow", "F6C900" },
                { "Turf", "27764C" }, { "PitchStripe", "318556" }, { "Track", "243447" },
                { "Ivory", "F1EACF" }, { "Steel", "8192A4" }, { "Lamp", "FFF1CF" } };
            var result = new Dictionary<string, Material>();
            foreach (var pair in colors)
            {
                Material mat = Lit(pair.Key, pair.Value);
                if (pair.Key == "Lamp")
                { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", new Color(1.7f, 1.4f, .9f)); }
                result.Add(pair.Key, Upsert(mat, Generated + "/Materials/" + pair.Key + ".mat"));
            }
            return result;
        }
        private static void CreateLighting()
        {
            var sun = new GameObject("Warm evening sun", typeof(Light));
            sun.transform.rotation = Quaternion.Euler(32f, -32f, 0f);
            var light = sun.GetComponent<Light>(); light.type = LightType.Directional;
            light.color = new Color(1f, .82f, .64f); light.intensity = 1.2f; light.shadows = LightShadows.Soft;
            light.shadowBias = .04f; light.shadowNormalBias = .25f;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.48f, .58f, .8f);
            RenderSettings.ambientEquatorColor = new Color(.42f, .46f, .55f);
            RenderSettings.ambientGroundColor = new Color(.21f, .3f, .25f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.44f, .32f, .35f);
            RenderSettings.fogStartDistance = 100f; RenderSettings.fogEndDistance = 205f;
            Shader skyShader = Shader.Find("FenerRunner/EveningSky");
            if (skyShader == null) throw new InvalidOperationException("Fener Runner evening sky shader is missing.");
            var sky = new Material(skyShader) { name = "EveningSky" };
            sky.SetColor("_ZenithColor", new Color(.1f, .18f, .32f));
            sky.SetColor("_HorizonColor", new Color(.86f, .49f, .32f));
            sky.SetColor("_GroundColor", new Color(.16f, .21f, .23f));
            RenderSettings.skybox = Upsert(sky, Generated + "/Rendering/EveningSky.mat");
        }

        [MenuItem("Fener Runner/4 - Build local development APK")]
        public static void BuildAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Install Android Build Support, SDK/NDK Tools and OpenJDK for Unity 6.3 in Unity Hub.");
            if (!File.Exists(ScenePath)) Build();
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            string directory = Path.GetFullPath("../Builds/Android"); Directory.CreateDirectory(directory);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, target = BuildTarget.Android,
                locationPathName = Path.Combine(directory, "FenerRunner-development.apk"), options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("APK build failed. See the Unity Console.");
            Debug.Log("Development APK: " + report.summary.outputPath);
        }
    }
}
