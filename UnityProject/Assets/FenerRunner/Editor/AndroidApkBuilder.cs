using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FenerRunner.Editor
{
    // Local-install release configuration, signed with Unity's default debug key.
    // This does not publish to a store or replace the source/generated game scene.
    public static class AndroidApkBuilder
    {
        const string AppId="com.fenerrunner.prototype",Version="0.20.0";
        const int VersionCode=20;
        const string Pending="FenerRunner.AndroidApkBuild.Pending";
        static int removedPreviewRoots,checkedSceneBehaviours;
        static bool packaging;
        static string ProjectRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        [Serializable] sealed class ApkReport
        {
            public bool passed,development,arm64NativeLibrary,manifestPresent,dexPresent;
            public string startedUtc,finishedUtc,unityVersion,outputPath,applicationIdentifier=AppId,version=Version;
            public int versionCode=VersionCode,minimumApi=26,editorGuardedSources,removedEditorPreviewRoots,checkedSceneComponents,errors,warnings;
            public string architecture="ARM64",scriptingBackend="IL2CPP",compilerConfiguration="Release",graphicsApi="OpenGLES3",textureCompression="ETC2";
            public string signing="Unity default debug keystore for local installation; not store signing";
            public string result,sha256;public long bytes;public double buildSeconds;public string[] messages;
            public string scope="Native Unity Android APK build and ZIP/native-library structure checks. Non-development build, Editor harnesses preprocessor-excluded, preview scenery removed only from the build-copy scene. This report is not a device installation, touchscreen, speaker, FPS, thermal or app-store validation.";
        }

        [MenuItem("Fener Runner/07 - Build Android APK (ARM64)")]
        public static void BuildFromMenu()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before building an APK.");
            RequireAndroidSupport();
            if(EditorUserBuildSettings.activeBuildTarget==BuildTarget.Android){Build();return;}
            SessionState.SetBool(Pending,true);
            if(!EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Android,BuildTarget.Android))
            {SessionState.SetBool(Pending,false);throw new BuildFailedException("Switching to the installed Android target failed.");}
            EditorApplication.delayCall+=ResumeMenuBuild;
        }
        [InitializeOnLoadMethod] static void RestorePendingBuild(){EditorApplication.delayCall+=ResumeMenuBuild;}
        static void ResumeMenuBuild()
        {
            if(!SessionState.GetBool(Pending,false))return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=ResumeMenuBuild;return;}
            SessionState.SetBool(Pending,false);Build();
        }

        // Batch entry:
        // -batchmode -quit -buildTarget Android
        // -executeMethod FenerRunner.Editor.AndroidApkBuilder.Build
        // Optional: -apkOutput C:/absolute/path/FenerRunner.apk
        public static void Build()
        {
            var evidence=new ApkReport{startedUtc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion};
            var messages=new List<string>();string reportPath=Path.GetFullPath(Path.Combine(ProjectRoot,"../Validation/android_apk_build_v20.json"));
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)throw new BuildFailedException("Stop Play before building an APK.");
                RequireAndroidSupport();
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)
                    throw new BuildFailedException("Batch builds require -buildTarget Android so scripts compile for Android before this entry runs. The menu command can switch target first.");
                if(!File.Exists(Path.Combine(ProjectRoot,RunnerSceneBuilder.ScenePath)))throw new BuildFailedException("The generated game scene is missing. Rebuild and validate it before packaging.");
                evidence.editorGuardedSources=CheckEditorGuards();CheckLegacyInput();
                evidence.outputPath=OutputPath();Directory.CreateDirectory(Path.GetDirectoryName(evidence.outputPath));
                Configure();removedPreviewRoots=checkedSceneBehaviours=0;AssetDatabase.SaveAssets();
                packaging=true;
                BuildReport build=BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes=new[]{RunnerSceneBuilder.ScenePath},target=BuildTarget.Android,
                    locationPathName=evidence.outputPath,options=BuildOptions.None
                });
                evidence.result=build.summary.result.ToString();evidence.errors=build.summary.totalErrors;evidence.warnings=build.summary.totalWarnings;
                evidence.development=(build.summary.options&BuildOptions.Development)!=0;evidence.buildSeconds=build.summary.totalTime.TotalSeconds;
                evidence.removedEditorPreviewRoots=removedPreviewRoots;evidence.checkedSceneComponents=checkedSceneBehaviours;
                foreach(var step in build.steps)foreach(var entry in step.messages)
                    if(entry.type==LogType.Error||entry.type==LogType.Exception||entry.type==LogType.Warning)messages.Add(entry.type+": "+entry.content);
                if(build.summary.result!=BuildResult.Succeeded||evidence.development)
                    throw new BuildFailedException("The non-development Android build failed. See the build report and Editor log.");
                using(var stream=File.OpenRead(evidence.outputPath))
                using(var archive=new ZipArchive(stream,ZipArchiveMode.Read))
                {
                    evidence.manifestPresent=archive.GetEntry("AndroidManifest.xml")!=null;
                    evidence.dexPresent=archive.GetEntry("classes.dex")!=null;
                    evidence.arm64NativeLibrary=archive.GetEntry("lib/arm64-v8a/libil2cpp.so")!=null;
                    if(!evidence.manifestPresent||!evidence.dexPresent||!evidence.arm64NativeLibrary)
                        throw new BuildFailedException("APK lacks its Android manifest, Java entry point or ARM64 IL2CPP native library.");
                }
                evidence.bytes=new FileInfo(evidence.outputPath).Length;
                using(var sha=SHA256.Create())using(var stream=File.OpenRead(evidence.outputPath))
                    evidence.sha256=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                evidence.passed=true;Debug.Log("Android APK built: "+evidence.outputPath+" (ARM64, non-development, local debug signing)");
            }
            catch(Exception error)
            {messages.Add(error.ToString());evidence.passed=false;if(string.IsNullOrEmpty(evidence.result))evidence.result="Failed preflight or packaging";throw;}
            finally
            {
                packaging=false;
                evidence.finishedUtc=DateTime.UtcNow.ToString("O");evidence.messages=messages.ToArray();
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));File.WriteAllText(reportPath,JsonUtility.ToJson(evidence,true));
            }
        }
        static void RequireAndroidSupport()
        {
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))
                throw new BuildFailedException("Install this Unity Editor's Android Build Support, Android SDK/NDK Tools and OpenJDK modules first.");
        }
        static string OutputPath()
        {
            string output="../Builds/Android/FenerRunner-v0.20-arm64.apk";string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length;i++)if(args[i]=="-apkOutput")
            {if(i+1>=args.Length||args[i+1].StartsWith("-",StringComparison.Ordinal))throw new BuildFailedException("-apkOutput requires an APK path.");output=args[i+1];break;}
            output=Path.GetFullPath(Path.IsPathRooted(output)?output:Path.Combine(ProjectRoot,output));
            if(!string.Equals(Path.GetExtension(output),".apk",StringComparison.OrdinalIgnoreCase))throw new BuildFailedException("APK output must use the .apk extension.");
            return output;
        }
        static void Configure()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,AppId);PlayerSettings.bundleVersion=Version;
            PlayerSettings.Android.bundleVersionCode=VersionCode;PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;PlayerSettings.Android.buildApkPerCpuArchitecture=false;
            PlayerSettings.Android.splitApplicationBinary=false;PlayerSettings.Android.useCustomKeystore=false;
            PlayerSettings.Android.forceInternetPermission=true;PlayerSettings.Android.forceSDCardPermission=false;
            PlayerSettings.SplashScreen.show=false;PlayerSettings.SplashScreen.showUnityLogo=false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android,Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android,ManagedStrippingLevel.Low);PlayerSettings.stripEngineCode=true;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;PlayerSettings.allowedAutorotateToPortrait=true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;PlayerSettings.allowedAutorotateToLandscapeLeft=false;PlayerSettings.allowedAutorotateToLandscapeRight=false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.OpenGLES3});
            EditorUserBuildSettings.androidBuildSubtarget=MobileTextureSubtarget.ETC2;
            EditorUserBuildSettings.buildAppBundle=false;EditorUserBuildSettings.exportAsGoogleAndroidProject=false;
            EditorUserBuildSettings.development=false;EditorUserBuildSettings.allowDebugging=false;EditorUserBuildSettings.connectProfiler=false;EditorUserBuildSettings.buildWithDeepProfilingSupport=false;
        }
        static int CheckEditorGuards()
        {
            string[] paths=Directory.GetFiles(Path.Combine(ProjectRoot,"Assets/FenerRunner/Runtime/Development"),"*.cs",SearchOption.AllDirectories);
            foreach(string path in paths)
            {
                string source=File.ReadAllText(path).Trim();
                if(!source.StartsWith("#if UNITY_EDITOR",StringComparison.Ordinal)||!source.EndsWith("#endif",StringComparison.Ordinal))
                    throw new BuildFailedException("Development source is not enclosed in UNITY_EDITOR: "+path);
            }
            return paths.Length;
        }
        static void CheckLegacyInput()
        {
            string projectSettings=File.ReadAllText(Path.Combine(ProjectRoot,"ProjectSettings/ProjectSettings.asset"));
            if(!Regex.IsMatch(projectSettings,@"(?m)^\s*activeInputHandler:\s*[02]\s*$"))
                throw new BuildFailedException("This swipe controller needs the legacy Input Manager enabled (Active Input Handling: Input Manager or Both).");
        }
        internal static void PrepareBuildScene(Scene scene,BuildReport report)
        {
            if(!packaging||report==null||report.summary.platform!=BuildTarget.Android)return;
            if(scene.path!=RunnerSceneBuilder.ScenePath)throw new BuildFailedException("Unexpected Android build scene: "+scene.path);
            foreach(var root in scene.GetRootGameObjects())
                foreach(var preview in root.GetComponentsInChildren<EditorStadiumPreview>(true))
                {UnityEngine.Object.DestroyImmediate(preview.gameObject);removedPreviewRoots++;}
            foreach(var root in scene.GetRootGameObjects())foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(behaviour==null)throw new BuildFailedException("Missing script in the Android build-copy scene.");
                string path=AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(behaviour)).Replace('\\','/');
                if(path.Contains("/Runtime/Development/")||path.Contains("/Editor/"))
                    throw new BuildFailedException("Editor/test component cannot ship in the Android scene: "+path);
                checkedSceneBehaviours++;
            }
        }
    }
    // Unity provides a temporary scene copy to this callback. The authored scene
    // and its useful Editor preview remain unchanged on disk.
    public sealed class AndroidApkBuildSceneProcessor:IProcessSceneWithReport
    {
        public int callbackOrder=>0;
        public void OnProcessScene(Scene scene,BuildReport report)=>AndroidApkBuilder.PrepareBuildScene(scene,report);
    }
}
