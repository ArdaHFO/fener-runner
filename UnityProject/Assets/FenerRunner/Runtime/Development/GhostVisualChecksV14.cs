#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class GhostVisualChecksV14:MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed,preferencesPreserved;public string utc,unityVersion;public string[] checks,errors;
            public int replicas,components,materials,sourceMeshVertices;public float standingHeight,slideHeight,runFootTravel;public float localForwardZ,ghostForwardZ,headingAgreement;
            public string scope="Actual Unity scene visual replica and Animator/mesh sampling from synthetic remote snapshots. Verifies no ghost physics/audio/gameplay components, no local-state mutation, bounded rendering, pooling, origin shift, phase mapping and native screenshot. This is not a two-phone transport or device performance test.";
        }
        const string Flag="FenerRunner.GhostVisualV14";
        readonly Report report=new Report();readonly List<string> checks=new List<string>(),errors=new List<string>();
        RunnerGame game;GhostRunnerView view;GameObject viewHost;RunWallet wallet;Mesh baked;
        bool oldBackground;int oldRate,oldCoins,oldScore;float oldScale,oldBest;
        [MenuItem("Fener Runner/96 - Verify remote ghost visual")]
        public static void StartChecks()
        {if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before ghost visual checks.");SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("V14 ghost visual checks").AddComponent<GhostVisualChecksV14>();}}
        IEnumerator Start()
        {
            oldBackground=Application.runInBackground;oldRate=Time.captureFramerate;oldScale=Time.timeScale;
            oldCoins=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0);oldScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);oldBest=PlayerPrefs.GetFloat("FenerRunner.Best",0);
            Application.runInBackground=true;Time.captureFramerate=60;Application.logMessageReceived+=Log;
            game=FindFirstObjectByType<RunnerGame>();if(game==null){Check(false,"Scene has RunnerGame");Finish();yield break;}
            game.AutomatedTest=true;wallet=game.UseWalletForTest("FenerRunner.Test.GhostVisualV14."+DateTime.UtcNow.Ticks+".");
            game.Restart();game.enabled=false;game.Obstacles.SetActive(false);game.Tokens.Enabled=false;game.Powerups.Enabled=false;game.Powerups.IsolatePickupsForTest();game.Animation.TurnAllowed=false;
            foreach(var renderer in game.Tokens.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            game.Pause();yield return new WaitForEndOfFrame();
            viewHost=new GameObject("Isolated remote ghost visual");view=viewHost.AddComponent<GhostRunnerView>();
            int before=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            view.Initialize(game);view.Hide();
            Check(!view.IsAllocated&&before==FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,"Initialize and offline Hide create no replica hierarchy");
            var originalSkin=game.Animation.Animator.GetComponentInChildren<SkinnedMeshRenderer>();var originalMaterials=originalSkin.sharedMaterials;
            var originalColors=originalMaterials.Select(m=>m.GetColor("_BaseColor")).ToArray();
            Vector3 playerPosition=game.Motor.transform.position;float capsuleHeight=game.Motor.Capsule.height;int score=game.Score,coins=game.Coins,jumps=game.Motor.JumpCount;
            double distance=game.Distance;float localPhase=game.Animation.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            var sample=Sample(GhostPose.Run,.05f);view.Show(sample,.2f);
            Check(view.IsVisible&&view.AllocationCount==1,"First drawable peer creates exactly one visible replica");
            report.components=view.VisualRoot.GetComponentsInChildren<Component>(true).Length;report.materials=view.MaterialCount;
            bool safe=view.VisualRoot.GetComponentsInChildren<Component>(true).All(c=>c is Transform||c is Animator||c is SkinnedMeshRenderer||c is MeshFilter||c is MeshRenderer);
            Check(safe,"Ghost hierarchy contains only transforms, renderers, mesh filters and one Animator");
            Check(view.VisualRoot.GetComponentsInChildren<Animator>(true).Length==1&&!view.VisualAnimator.applyRootMotion&&!view.VisualAnimator.fireEvents&&view.VisualAnimator.speed==0,"One explicitly sampled Animator has root motion and animation events disabled");
            var skin=view.VisualRoot.GetComponentInChildren<SkinnedMeshRenderer>();baked=new Mesh();report.sourceMeshVertices=skin.sharedMesh.vertexCount;
            Check(skin.sharedMesh==originalSkin.sharedMesh&&skin.bones.All(b=>b!=null&&b.IsChildOf(view.VisualRoot)),"Shared source mesh uses exclusively cloned ghost bones");
            Check(skin.sharedMaterials.All(m=>!originalMaterials.Contains(m)&&m.shader.name=="FenerRunner/GhostRunner"),"Ghost materials are separate retained translucent materials");
            Vector3 localHeading=Vector3.ProjectOnPlane(originalSkin.bones.Single(b=>b.name=="jaw").position-originalSkin.bones.Single(b=>b.name=="head").position,Vector3.up).normalized;
            Vector3 ghostHeading=Vector3.ProjectOnPlane(skin.bones.Single(b=>b.name=="jaw").position-skin.bones.Single(b=>b.name=="head").position,Vector3.up).normalized;
            report.localForwardZ=localHeading.z;report.ghostForwardZ=ghostHeading.z;report.headingAgreement=Vector3.Dot(localHeading,ghostHeading);
            Check(report.localForwardZ>.95f&&report.ghostForwardZ>.95f&&report.headingAgreement>.98f,"Local and remote actual jaw/head vectors both face down the course with matching orientation");
            var opacity=new MaterialPropertyBlock();skin.GetPropertyBlock(opacity);
            Check(opacity.GetFloat("_Opacity")>0&&opacity.GetFloat("_Opacity")<1&&skin.sharedMaterial.shader.isSupported,"Native renderer receives bounded translucent opacity with a supported shader");
            report.standingHeight=Height(skin);
            Transform foot=skin.bones.Single(b=>b.name=="foot.L");Vector3 footA=view.VisualRoot.InverseTransformPoint(foot.position);
            sample.Phase=.55f;view.Show(sample,.016f);report.runFootTravel=Vector3.Distance(footA,view.VisualRoot.InverseTransformPoint(foot.position));
            Check(report.runFootTravel>.1f,"Remote normalized Run phase changes the actual foot trajectory");
            CheckPose(GhostPose.Jump,.5f,2.65f,"Jump");CheckPose(GhostPose.Slide,.4f,0,"Slide");report.slideHeight=Height(skin);
            Check(report.slideHeight<report.standingHeight-.25f,"Remote slide uses the actual low character silhouette");
            CheckPose(GhostPose.Flight,.25f,4.8f,"Flight");CheckPose(GhostPose.Dead,.8f,3,"CollisionFall");
            float fallenHeight=view.VisualRoot.position.y;float deadPhase=view.VisualAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            sample=Sample(GhostPose.Paused,.99f);sample.Height=3;view.Show(sample,.016f);
            Check(Mathf.Abs(view.VisualRoot.position.y-fallenHeight)<.0001f&&Mathf.Abs(view.VisualAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime-deadPhase)<.0001f,"Paused ghost holds its prior collision pose and visual descent");
            sample=Sample(GhostPose.Run,.25f);sample.RunId=2;view.Show(sample,.016f);
            Check(view.CurrentClip=="Run"&&Mathf.Abs(view.VisualRoot.position.y)<.0001f,"New race resets the prior ghost death pose");
            for(uint i=0;i<300;i++){sample.Sequence=i;sample.Phase=i*.017f;view.Show(sample,.016f);}
            Check(view.AllocationCount==1&&view.MaterialCount==report.materials&&view.VisualRoot.GetComponentsInChildren<Component>(true).Length==report.components,"Repeated snapshots retain one hierarchy and a fixed material count");
            Check(game.Motor.transform.position==playerPosition&&game.Motor.Capsule.height==capsuleHeight&&game.Score==score&&game.Coins==coins&&game.Distance==distance&&game.Motor.JumpCount==jumps,"Remote visual updates do not change local physics, distance, coins or score");
            Check(Mathf.Abs(game.Animation.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime-localPhase)<.0001f,"Remote phase sampling never advances the paused local Animator");
            Check(originalMaterials.Select((m,i)=>m.GetColor("_BaseColor")==originalColors[i]&&m.shader.name!="FenerRunner/GhostRunner").All(v=>v),"Original character material colors and shaders are unchanged");
            float beforeShift=view.VisualRoot.position.z;game.Motor.ShiftOrigin(400);view.Show(sample,.016f);
            Check(Mathf.Abs(view.VisualRoot.position.z-(beforeShift-400))<.001f,"Total distance mapping follows local origin shifts without remote discontinuity");
            sample.Distance=game.Distance+111;view.Show(sample,.016f);Check(!view.IsVisible,"Far-ahead ghost is culled beyond110 metres");
            sample.Distance=game.Distance-16;view.Show(sample,.016f);Check(!view.IsVisible,"Far-behind ghost is culled beyond15 metres");
            sample=Sample(GhostPose.Run,.2f);sample.Distance=double.NaN;view.Show(sample,.016f);Check(!view.IsVisible,"Invalid non-finite position cannot enter the transform hierarchy");
            sample=Sample(GhostPose.Run,.2f);view.Show(sample,.016f);view.Hide();Check(!view.IsVisible&&view.IsAllocated,"Hide disables rendering and retains the one-time pool");
            game.Restart();game.enabled=false;game.Obstacles.SetActive(false);game.Tokens.Enabled=false;game.Powerups.Enabled=false;game.Powerups.IsolatePickupsForTest();
            foreach(var renderer in game.Tokens.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            sample=Sample(GhostPose.Run,.18f);sample.LaneX=game.Settings.laneSpacing;sample.Distance=game.Distance+8;
            for(int i=0;i<18;i++){view.Show(sample,1f/60);yield return new WaitForEndOfFrame();}
            ScreenCapture.CaptureScreenshot(Evidence("v14-remote-ghost.png"));yield return new WaitForEndOfFrame();yield return null;
            Check(File.Exists(Evidence("v14-remote-ghost.png")),"Native game camera captures the real translucent ghost beside the local character");
            var ghostMaterials=view.VisualRoot.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct().ToArray();Transform replica=view.VisualRoot;report.replicas=view.AllocationCount;
            Destroy(viewHost);yield return null;
            Check(replica==null&&ghostMaterials.All(m=>m==null)&&originalSkin.sharedMesh!=null,"Disposal releases only owned ghost hierarchy/materials and preserves the source mesh");
            game.ReturnToMenu();report.preferencesPreserved=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0)==oldCoins&&PlayerPrefs.GetInt("FenerRunner.BestScore",0)==oldScore&&PlayerPrefs.GetFloat("FenerRunner.Best",0)==oldBest;
            Check(report.preferencesPreserved,"Real wallet and best records remain unchanged");Finish();
        }
        GhostSnapshot Sample(GhostPose pose,float phase)=>new GhostSnapshot{RunId=1,Sequence=1,Time=0,Distance=game.Distance+5,LaneX=0,Height=0,Speed=18,Phase=phase,Pose=pose};
        void CheckPose(GhostPose pose,float phase,float height,string clip)
        {
            var sample=Sample(pose,phase);sample.Height=height;view.Show(sample,.016f);
            Check(view.CurrentClip==clip&&Mathf.Abs(view.VisualAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime-phase)<.001f,pose+" samples the correct remote clip phase");
            float expectedHeight=pose==GhostPose.Dead?height*(1-phase*phase*(3-2*phase)):height;
            Check(Mathf.Abs(view.VisualRoot.position.y-expectedHeight)<.001f&&Mathf.Abs(view.VisualRoot.position.z-game.Motor.transform.position.z-5)<.001f,pose+" maps its remote height and relative race distance");
        }
        float Height(SkinnedMeshRenderer renderer)
        {renderer.BakeMesh(baked);float low=float.PositiveInfinity,high=float.NegativeInfinity;foreach(var p in baked.vertices){float y=renderer.transform.TransformPoint(p).y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);}return high-low;}
        static string Evidence(string file){string path=ReviewEvidence.FileFor(14,"../Validation/"+file);Directory.CreateDirectory(Path.GetDirectoryName(path));return path;}
        void Check(bool ok,string message){if(ok)checks.Add(message);else errors.Add(message);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        void Finish()
        {
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.checks=checks.ToArray();report.errors=errors.ToArray();
            File.WriteAllText(Evidence("unity_ghost_visual_v14.json"),JsonUtility.ToJson(report,true));Application.logMessageReceived-=Log;
            Debug.Log("Ghost visual v14 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void OnDestroy()
        {Application.logMessageReceived-=Log;Application.runInBackground=oldBackground;Time.captureFramerate=oldRate;Time.timeScale=oldScale;if(viewHost!=null)Destroy(viewHost);if(baked!=null)Destroy(baked);if(wallet!=null)wallet.DeleteIsolatedTestData();}
    }
}
#endif
