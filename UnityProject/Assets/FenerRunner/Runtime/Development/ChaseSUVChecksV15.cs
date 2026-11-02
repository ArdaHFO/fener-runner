#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FenerRunner
{
    public sealed class ChaseSUVChecksV15 : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public bool passed,preferencesPreserved;
            public string utc,unityVersion;
            public string[] checks,errors;
            public int renderedTriangles,sharedMeshes,renderers,materialSlots,simulatedFrames,episodes;
            public float minimumRearClearance,visibleFraction,roofViewportX,roofViewportY,hoodViewportY;
            public Composition[] compositions;
            public int previewForegroundPixels,previewLetteringPixels;
            public string scope="Native Unity scene and procedural mesh checks, manual deterministic chase sampling, pause/reset/death/origin and camera-visible rendered screenshots. No APK, physical-device performance or two-phone test.";
        }
        [Serializable] private sealed class Composition
        {
            public int lane,width,height,playerPixels,visiblePlayerPixels,lowerPlayerPixels,visibleLowerPlayerPixels,vehiclePixels;
            public float playerRetention,lowerPlayerRetention,vehicleCoverage;
            public string image;
        }
        private const string Flag="FenerRunner.ChaseSUVV15";
        private readonly Report report=new Report();
        private readonly List<string> checks=new List<string>(),errors=new List<string>();
        private RunnerGame game;
        private ChaseSUV chase;
        private RunWallet wallet;
        private bool oldBackground;
        private float oldScale,oldBest;
        private int oldCapture,oldCoins,oldScore;

        [MenuItem("Fener Runner/98 - Verify animated chase SUV")]
        public static void StartChecks()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before chase SUV checks.");
            SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] private static void Register()
        {EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        private static void Changed(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false))
            {SessionState.SetBool(Flag,false);new GameObject("V15 chase SUV checks").AddComponent<ChaseSUVChecksV15>();}
        }
        private IEnumerator Start()
        {
            oldBackground=Application.runInBackground;oldCapture=Time.captureFramerate;oldScale=Time.timeScale;
            oldCoins=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0);oldScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);oldBest=PlayerPrefs.GetFloat("FenerRunner.Best",0);
            Application.runInBackground=true;Time.captureFramerate=60;Application.logMessageReceived+=Log;
            game=FindFirstObjectByType<RunnerGame>();
            if(game==null){Check(false,"Scene contains RunnerGame");Finish();yield break;}
            game.AutomatedTest=true;
            wallet=game.UseWalletForTest("FenerRunner.Test.ChaseSUVV15."+DateTime.UtcNow.Ticks+".");
            chase=game.GetComponent<ChaseSUV>();
            Check(chase!=null&&chase.VisualRoot!=null,"Game initializes exactly one retained SUV from its baked Resources prefab");
            if(chase==null||chase.VisualRoot==null){Finish();yield break;}
            Check(!chase.IsVisible,"Menu starts without an on-screen pursuer");
            Transform visual=chase.VisualRoot;
            var components=visual.GetComponentsInChildren<Component>(true);
            Check(components.All(c=>c is Transform||c is MeshFilter||c is MeshRenderer),"SUV hierarchy contains only rendering components, no collider, audio, gameplay, physics or animation event sources");
            var filters=visual.GetComponentsInChildren<MeshFilter>(true);var renderers=visual.GetComponentsInChildren<MeshRenderer>(true);
            report.renderers=renderers.Length;report.materialSlots=renderers.Sum(r=>r.sharedMaterials.Length);
            report.renderedTriangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3);report.sharedMeshes=filters.Select(f=>f.sharedMesh).Distinct().Count();
            Check(report.sharedMeshes==3&&report.renderers==6&&report.renderedTriangles<40000&&report.materialSlots<=23,"Car uses three shared meshes, six renderers and a bounded mobile rendering budget under40k triangles/23 material slots including the shared lettering atlas");
            Check(renderers.All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader.isSupported)),"Native URP shaders and every vehicle material are supported and assigned");
            var wheels=visual.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="Tire and twin spoke alloy").ToArray();
            Check(wheels.Length==4&&wheels.All(w=>w.sharedMesh==wheels[0].sharedMesh),"All four detailed tires/alloy wheels instance the same retained mesh");
            Check(filters.All(f=>f.sharedMesh.vertices.All(p=>Finite(p))&&f.sharedMesh.normals.All(n=>Finite(n)&&n.sqrMagnitude>.5f)),"All authored positions and normals are finite and nondegenerate");
            var coach=visual.Find("Suspension/Coachwork").GetComponent<MeshFilter>();
            Check(coach.sharedMesh.bounds.size.z>4.7f&&coach.sharedMesh.bounds.size.y>1.5f&&coach.sharedMesh.bounds.size.x>2.3f,"Full 3D coachwork has luxury-SUV proportions with physical mirrors, hood, roof and lettering plate");
            Material[] bodyMaterials=coach.GetComponent<MeshRenderer>().sharedMaterials;
            Color paint=bodyMaterials[0].GetColor("_BaseColor");
            Check(paint.maxColorComponent<.025f&&Mathf.Abs(paint.r-paint.b)<.001f&&Mathf.Abs(paint.r-paint.g)<.001f,"Primary coachwork paint is neutral deep black, without the previous blue tint");
            int letteringIndex=Array.FindIndex(bodyMaterials,IsPresidentLettering);
            Check(letteringIndex>=0&&coach.sharedMesh.GetIndexCount(letteringIndex)==18&&bodyMaterials[letteringIndex].mainTexture!=null&&bodyMaterials[letteringIndex].mainTexture.width==1024,"Roof, hood and rear inscriptions share exactly three baked quads and one 1024-pixel Turkish lettering atlas");
            Check(bodyMaterials.All(m=>m.name.IndexOf("lightning",StringComparison.OrdinalIgnoreCase)<0)&&coach.sharedMesh.subMeshCount==10,"Baked coachwork materials have no remaining lightning emblem and include the replacement inscription");
            IsolateRun();
            int transformCount=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            int coins=game.Coins,score=game.Score,jumps=game.Motor.JumpCount;double distance=game.Distance;Vector3 player=game.Motor.transform.position;
            UnityEngine.Random.State random=UnityEngine.Random.state;
            string randomBefore=JsonUtility.ToJson(random);
            bool finite=true,clear=true,tracks=true;
            float clearance=float.PositiveInfinity;int visibleFrames=0;
            for(int i=0;i<60*180;i++)
            {
                chase.Tick(1f/60);report.simulatedFrames++;
                if(!chase.IsVisible)continue;
                visibleFrames++;
                Bounds bounds=renderers[0].bounds;for(int n=1;n<renderers.Length;n++)bounds.Encapsulate(renderers[n].bounds);
                float gap=game.Motor.transform.position.z-bounds.max.z;
                clearance=Mathf.Min(clearance,gap);clear&=gap>.85f;
                finite&=Finite(visual.position)&&Finite(visual.eulerAngles);
                tracks&=Mathf.Abs(visual.position.z-game.Motor.transform.position.z+chase.ApproachGap)<.0001f;
            }
            report.minimumRearClearance=clearance;report.visibleFraction=visibleFrames/(float)report.simulatedFrames;report.episodes=chase.EpisodeCount;
            Check(finite&&clear&&tracks,"Three simulated minutes keep finite transforms and every bumper/wheel strictly behind the runner through approach and retreat");
            Check(report.episodes>=7&&report.visibleFraction>.18f&&report.visibleFraction<.40f,"Pursuit repeatedly enters and retreats, remaining absent for most of a run");
            Check(visual==chase.VisualRoot&&transformCount==FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,"Repeated pursuit episodes reuse one fixed hierarchy without object growth");
            Check(coins==game.Coins&&score==game.Score&&distance==game.Distance&&jumps==game.Motor.JumpCount&&player==game.Motor.transform.position,"Chase sampling never changes local movement, distance, score, jumps or coins");
            Check(randomBefore==JsonUtility.ToJson(UnityEngine.Random.state),"Chase scheduling leaves the global random sequence unchanged");

            chase.ResetPresentation();AdvanceToClose();
            Transform wheel=visual.Find("WheelFLSteer/WheelFLSpin");Transform body=visual.Find("Suspension");
            Quaternion wheelBefore=wheel.localRotation;Vector3 suspensionBefore=body.localPosition;
            chase.Tick(.05f);
            Check(Quaternion.Angle(wheelBefore,wheel.localRotation)>.1f&&Vector3.Distance(suspensionBefore,body.localPosition)>.00001f,"Running updates wheel roll and restrained body suspension independently");
            game.Motor.transform.position+=Vector3.right*2.8f;
            for(int i=0;i<12;i++)chase.Tick(1f/60);
            Check(Quaternion.Angle(Quaternion.identity,visual.Find("WheelFLSteer").localRotation)>1f&&Mathf.Abs(visual.position.x)>0f,"Lane pursuit steers the front wheels and follows laterally with lag");
            float time=chase.PresentationTime;Vector3 position=visual.position;Quaternion wheelRotation=wheel.localRotation;Vector3 suspension=body.localPosition;
            game.Pause();for(int i=0;i<120;i++)chase.Tick(1f/60);
            Check(chase.PresentationTime==time&&visual.position==position&&wheel.localRotation==wheelRotation&&body.localPosition==suspension,"Pause freezes chase clock, vehicle, wheel roll and suspension exactly");
            game.Resume();chase.Tick(float.NaN);chase.Tick(float.PositiveInfinity);chase.Tick(-1);chase.Tick(0);
            Check(chase.PresentationTime==time&&visual.position==position,"Invalid and non-positive deltas cannot corrupt visual transforms");
            chase.enabled=false;chase.Tick(1f/60);
            Check(!chase.IsVisible&&chase.PresentationTime==time,"Disabling the presentation hides its hierarchy and rejects manually dispatched ticks");
            chase.enabled=true;chase.Tick(1f/60);Check(chase.IsVisible,"Re-enabling during an active episode restores its retained hierarchy");
            game.Motor.ShiftOrigin(1024f);chase.Tick(1f/60);
            Check(Mathf.Abs(visual.position.z-game.Motor.transform.position.z+chase.ApproachGap)<.001f&&chase.EpisodeCount==1,"Floating-origin rebase preserves chase episode and exact runner-relative placement");
            // Repeat with several frame intervals, catching timestamp assumptions without changing runner gameplay.
            foreach(int rate in new[]{24,60,144})
            {
                chase.ResetPresentation();int count=0;
                for(int frame=0;frame<rate*32;frame++){chase.Tick(1f/rate);if(chase.IsVisible)count++;}
                Check(chase.EpisodeCount>=2&&count>rate*5&&count<rate*16,"Intermittent chase remains bounded at"+rate+" Hz");
            }

            IsolateRun();AdvanceToClose();
            for(int i=0;i<90;i++)yield return new WaitForEndOfFrame();
            Camera camera=Camera.main;
            Vector3 roof=camera.WorldToViewportPoint(visual.TransformPoint(new Vector3(0,2.18f,-.58f)));
            Vector3 hood=camera.WorldToViewportPoint(visual.TransformPoint(new Vector3(0,1.50f,1.56f)));
            report.roofViewportX=roof.x;report.roofViewportY=roof.y;report.hoodViewportY=hood.y;
            Check((OnScreen(roof)||OnScreen(hood))&&camera.aspect<.8f,"Actual portrait game camera sees the roof or hood inscription during a close approach");
            ScreenCapture.CaptureScreenshot(Evidence("v15-chase-visible.png"));yield return new WaitForEndOfFrame();yield return null;
            Check(File.Exists(Evidence("v15-chase-visible.png")),"Native game camera captured the visible SUV in the real stadium scene");
            for(int i=0;i<240;i++)chase.Tick(1f/60);
            Check(!chase.IsVisible&&chase.Presence01==0,"Complete retreat removes the SUV from view without destroying its pool");
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Evidence("v15-chase-retreat.png"));yield return new WaitForEndOfFrame();yield return null;
            Check(File.Exists(Evidence("v15-chase-retreat.png")),"Native game camera captured the uncluttered scene after retreat");
            yield return CaptureCompositionMatrix();
            yield return CaptureVehicleCloseup();
            game.ReturnToMenu();chase.Tick(1f/60);Check(!chase.IsVisible&&chase.Presence01==0,"Returning to menu hides the vehicle immediately");
            IsolateRun();AdvanceToClose();
            game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+3f,0);game.AdvanceForTest(.4f);chase.Tick(1f/60);
            Check(game.State==RunState.Finished&&!chase.IsVisible&&chase.Presence01==0,"Actual fatal contact hides pursuer immediately so the collision animation remains clear");
            game.Restart();game.enabled=false;Check(!chase.IsVisible&&chase.EpisodeCount==0&&chase.PresentationTime==0,"Restart reuses the visual and resets the full first-entrance delay");
            game.ReturnToMenu();
            report.preferencesPreserved=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0)==oldCoins&&PlayerPrefs.GetInt("FenerRunner.BestScore",0)==oldScore&&PlayerPrefs.GetFloat("FenerRunner.Best",0)==oldBest;
            Check(report.preferencesPreserved,"Real wallet and best records are unchanged");Finish();
        }
        private void IsolateRun()
        {
            game.Restart();game.enabled=false;game.Obstacles.SetActive(false);game.Tokens.Enabled=false;
            game.Powerups.Enabled=false;game.Powerups.IsolatePickupsForTest();game.Animation.TurnAllowed=false;
            foreach(var renderer in game.Tokens.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            chase.ResetPresentation();
        }
        private void AdvanceToClose(){for(int i=0;i<420;i++)chase.Tick(1f/60);}

        private IEnumerator CaptureCompositionMatrix()
        {
            Camera main=Camera.main;FollowCamera follow=main.GetComponent<FollowCamera>();
            RenderTexture previousTarget=main.targetTexture;float previousAspect=main.aspect;
            var compositions=new List<Composition>();
            // Raster masks measure actual visibility, not just a transform falling inside the camera frustum.
            // White = the runner, magenta = the SUV. Depth testing is the normal native pipeline depth test.
            var white=new Material(Shader.Find("Universal Render Pipeline/Unlit"));white.SetColor("_BaseColor",Color.white);
            var magenta=new Material(Shader.Find("Universal Render Pipeline/Unlit"));magenta.SetColor("_BaseColor",Color.magenta);
            var cameraHost=new GameObject("Native chase composition mask camera",typeof(Camera),typeof(UniversalAdditionalCameraData));
            Camera maskCamera=cameraHost.GetComponent<Camera>();maskCamera.enabled=false;
            var playerTransforms=game.Motor.GetComponentsInChildren<Transform>(true);
            var vehicleTransforms=chase.VisualRoot.GetComponentsInChildren<Transform>(true);
            int[] playerLayers=playerTransforms.Select(t=>t.gameObject.layer).ToArray();
            int[] vehicleLayers=vehicleTransforms.Select(t=>t.gameObject.layer).ToArray();
            var playerRenderers=game.Motor.GetComponentsInChildren<Renderer>(true);
            var vehicleRenderers=chase.VisualRoot.GetComponentsInChildren<Renderer>(true);
            var allRenderers=playerRenderers.Concat(vehicleRenderers).ToArray();
            Material[][] oldMaterials=allRenderers.Select(r=>r.sharedMaterials).ToArray();
            bool[] vehicleEnabled=vehicleRenderers.Select(r=>r.enabled).ToArray();
            Animator animator=game.Animation.Animator;bool animatorEnabled=animator.enabled;
            RenderTexture target=null;Texture2D pixels=null;
            try
            {
                foreach(int height in new[]{1920,2340})
                    foreach(int lane in new[]{-1,0,1})
                    {
                        const int width=1080;
                        target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=1};target.Create();
                        main.targetTexture=target;main.ResetAspect();
                        IsolateRun();game.Motor.transform.position=new Vector3(lane*game.Settings.laneSpacing,.02f,8f);
                        follow.Snap();chase.ResetPresentation();AdvanceToClose();
                        // Hold the real pursuit at peak while the production camera reaches its eased framing.
                        for(int i=0;i<90;i++)yield return new WaitForEndOfFrame();
                        animator.enabled=false;
                        pixels=ReadTarget(target);
                        string image="v15-chase-lane"+(lane<0?"left":lane>0?"right":"center")+"-"+width+"x"+height+".png";
                        File.WriteAllBytes(Evidence(image),pixels.EncodeToPNG());Destroy(pixels);pixels=null;

                        for(int i=0;i<playerTransforms.Length;i++)playerTransforms[i].gameObject.layer=30;
                        for(int i=0;i<vehicleTransforms.Length;i++)vehicleTransforms[i].gameObject.layer=31;
                        for(int i=0;i<allRenderers.Length;i++)
                            allRenderers[i].sharedMaterials=Enumerable.Repeat(i<playerRenderers.Length?white:magenta,oldMaterials[i].Length).ToArray();
                        maskCamera.CopyFrom(main);maskCamera.transform.SetPositionAndRotation(main.transform.position,main.transform.rotation);
                        maskCamera.clearFlags=CameraClearFlags.SolidColor;maskCamera.backgroundColor=Color.black;maskCamera.cullingMask=(1<<30)|(1<<31);
                        maskCamera.allowHDR=false;maskCamera.allowMSAA=false;maskCamera.depth=100;maskCamera.targetTexture=target;maskCamera.enabled=true;
                        cameraHost.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
                        main.targetTexture=previousTarget;main.enabled=false;
                        // The baseline is the exact same pose and lens without the vehicle, so feet/wing motion cannot fake occlusion.
                        foreach(Renderer renderer in vehicleRenderers)renderer.enabled=false;
                        yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                        pixels=ReadTarget(target);Color32[] baseline=pixels.GetPixels32();Destroy(pixels);pixels=null;
                        for(int i=0;i<vehicleRenderers.Length;i++)vehicleRenderers[i].enabled=vehicleEnabled[i];
                        yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                        pixels=ReadTarget(target);Color32[] composite=pixels.GetPixels32();
                        File.WriteAllBytes(Evidence(image.Replace("v15-chase-","v15-chase-mask-")),pixels.EncodeToPNG());Destroy(pixels);pixels=null;
                        var sample=new Composition{lane=lane,width=width,height=height,image=Path.GetFileName(Evidence(image))};
                        int minY=height,maxY=0;
                        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                            if(IsWhite(baseline[y*width+x])){minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
                        int lowerLimit=minY+Mathf.RoundToInt((maxY-minY)*.45f);
                        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                        {
                            int index=y*width+x;
                            if(IsWhite(baseline[index]))
                            {
                                sample.playerPixels++;if(IsWhite(composite[index]))sample.visiblePlayerPixels++;
                                if(y<=lowerLimit){sample.lowerPlayerPixels++;if(IsWhite(composite[index]))sample.visibleLowerPlayerPixels++;}
                            }
                            if(IsMagenta(composite[index]))sample.vehiclePixels++;
                        }
                        sample.playerRetention=sample.visiblePlayerPixels/(float)Mathf.Max(1,sample.playerPixels);
                        sample.lowerPlayerRetention=sample.visibleLowerPlayerPixels/(float)Mathf.Max(1,sample.lowerPlayerPixels);
                        sample.vehicleCoverage=sample.vehiclePixels/(float)(width*height);compositions.Add(sample);
                        string label=" lane"+lane+" "+width+"x"+height;
                        Check(sample.playerPixels>4000&&sample.lowerPlayerPixels>500,"Native player mask contains a real full-body silhouette"+label);
                        Check(sample.playerRetention>.985f&&sample.lowerPlayerRetention>.98f,"SUV leaves at least98.5% of runner and98% of lower body unobstructed"+label);
                        Check(sample.vehicleCoverage>.018f&&sample.vehicleCoverage<.19f,"SUV occupies a visible but bounded1.8–19% of portrait pixels"+label);
                        maskCamera.enabled=false;maskCamera.targetTexture=null;main.enabled=true;
                        for(int i=0;i<allRenderers.Length;i++)allRenderers[i].sharedMaterials=oldMaterials[i];
                        for(int i=0;i<playerTransforms.Length;i++)playerTransforms[i].gameObject.layer=playerLayers[i];
                        for(int i=0;i<vehicleTransforms.Length;i++)vehicleTransforms[i].gameObject.layer=vehicleLayers[i];
                        target.Release();Destroy(target);target=null;animator.enabled=animatorEnabled;
                    }
                report.compositions=compositions.ToArray();
            }
            finally
            {
                main.targetTexture=previousTarget;main.aspect=previousAspect;main.enabled=true;animator.enabled=animatorEnabled;
                for(int i=0;i<allRenderers.Length;i++)if(allRenderers[i]!=null)allRenderers[i].sharedMaterials=oldMaterials[i];
                for(int i=0;i<playerTransforms.Length;i++)if(playerTransforms[i]!=null)playerTransforms[i].gameObject.layer=playerLayers[i];
                for(int i=0;i<vehicleTransforms.Length;i++)if(vehicleTransforms[i]!=null)vehicleTransforms[i].gameObject.layer=vehicleLayers[i];
                for(int i=0;i<vehicleRenderers.Length;i++)if(vehicleRenderers[i]!=null)vehicleRenderers[i].enabled=vehicleEnabled[i];
                maskCamera.enabled=false;maskCamera.targetTexture=null;Destroy(cameraHost);Destroy(white);Destroy(magenta);
                if(target!=null){target.Release();Destroy(target);}if(pixels!=null)Destroy(pixels);
            }
        }

        private IEnumerator CaptureVehicleCloseup()
        {
            const int width=1080,height=810,mask=1<<31;
            Vector3 origin=new Vector3(5000,50,5000);
            GameObject source=Resources.Load<GameObject>(ChaseSUV.ResourceName),visual=null,cameraHost=null,keyHost=null,fillHost=null;
            Texture2D pixels=null,withoutLettering=null;RenderTexture target=null;Material hiddenLettering=null;
            Light[] lights=FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            int[] oldMasks=lights.Select(light=>light.cullingMask).ToArray();
            try
            {
                for(int i=0;i<lights.Length;i++)lights[i].cullingMask&=~mask;
                visual=Instantiate(source);visual.transform.position=origin;
                foreach(Transform item in visual.GetComponentsInChildren<Transform>(true))item.gameObject.layer=31;
                cameraHost=new GameObject("Native SUV three-quarter camera",typeof(Camera),typeof(UniversalAdditionalCameraData));
                Camera camera=cameraHost.GetComponent<Camera>();camera.fieldOfView=36;camera.aspect=width/(float)height;
                camera.nearClipPlane=.05f;camera.farClipPlane=30;camera.cullingMask=mask;camera.depth=100;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.05f,.105f,1);camera.allowHDR=false;camera.allowMSAA=true;
                camera.transform.position=origin+new Vector3(6.3f,4.2f,7.4f);camera.transform.LookAt(origin+new Vector3(0,1.05f,0));
                cameraHost.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
                target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=4};target.Create();camera.targetTexture=target;
                keyHost=new GameObject("Native SUV closeup key",typeof(Light));Light key=keyHost.GetComponent<Light>();
                key.type=LightType.Directional;key.intensity=1.7f;key.color=new Color(1,.94f,.82f);key.cullingMask=mask;key.shadows=LightShadows.None;key.transform.rotation=Quaternion.Euler(35,-35,0);
                fillHost=new GameObject("Native SUV closeup fill",typeof(Light));Light fill=fillHost.GetComponent<Light>();
                fill.type=LightType.Directional;fill.intensity=.8f;fill.color=new Color(.68f,.82f,1f);fill.cullingMask=mask;fill.shadows=LightShadows.None;fill.transform.rotation=Quaternion.Euler(20,145,0);
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                pixels=ReadTarget(target);Color32[] image=pixels.GetPixels32();Color32 background=image[0];
                foreach(Color32 pixel in image)
                {
                    if(Mathf.Abs(pixel.r-background.r)+Mathf.Abs(pixel.g-background.g)+Mathf.Abs(pixel.b-background.b)>40)report.previewForegroundPixels++;
                }
                File.WriteAllBytes(Evidence("v15-chase-suv-three-quarter.png"),pixels.EncodeToPNG());
                Check(report.previewForegroundPixels>60000&&report.previewForegroundPixels<width*height*.75f,"Native front-three-quarter render contains the real complete SUV silhouette, not a blank target");
                MeshRenderer coach=visual.transform.Find("Suspension/Coachwork").GetComponent<MeshRenderer>();
                Material[] original=coach.sharedMaterials,blank=(Material[])original.Clone();
                int textIndex=Array.FindIndex(original,IsPresidentLettering);
                if(textIndex>=0)
                {
                    hiddenLettering=new Material(original[textIndex]);hiddenLettering.SetColor("_BaseColor",Color.clear);
                    blank[textIndex]=hiddenLettering;coach.sharedMaterials=blank;
                    yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                    withoutLettering=ReadTarget(target);Color32[] baseline=withoutLettering.GetPixels32();
                    for(int i=0;i<image.Length;i++)
                    {
                        Color32 ink=image[i],bare=baseline[i];
                        if(ink.r>175&&ink.g>170&&ink.b>150&&Mathf.Abs(ink.r-bare.r)+Mathf.Abs(ink.g-bare.g)+Mathf.Abs(ink.b-bare.b)>90)report.previewLetteringPixels++;
                    }
                    coach.sharedMaterials=original;
                }
                Check(report.previewLetteringPixels>500,"Paired native renders isolate over 500 visible cream inscription pixels on the actual SUV, excluding lights, reflections and blank quads");
            }
            finally
            {
                for(int i=0;i<lights.Length;i++)if(lights[i]!=null)lights[i].cullingMask=oldMasks[i];
                if(cameraHost!=null){cameraHost.GetComponent<Camera>().enabled=false;cameraHost.GetComponent<Camera>().targetTexture=null;Destroy(cameraHost);}
                if(visual!=null)Destroy(visual);if(keyHost!=null)Destroy(keyHost);if(fillHost!=null)Destroy(fillHost);
                if(target!=null){target.Release();Destroy(target);}if(pixels!=null)Destroy(pixels);if(withoutLettering!=null)Destroy(withoutLettering);if(hiddenLettering!=null)Destroy(hiddenLettering);
            }
        }
        private static Texture2D ReadTarget(RenderTexture target)
        {
            RenderTexture previous=RenderTexture.active;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false,false);
            try{RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);pixels.Apply(false,false);return pixels;}
            finally{RenderTexture.active=previous;}
        }
        private static bool IsWhite(Color32 pixel)=>pixel.r>200&&pixel.g>200&&pixel.b>200;
        private static bool IsMagenta(Color32 pixel)=>pixel.r>180&&pixel.g<80&&pixel.b>180;
        // Unity uses the asset filename for a newly saved material's display name.
        // Identify the real imported atlas instead of relying on that mutable label.
        private static bool IsPresidentLettering(Material material)=>material!=null&&material.HasProperty("_BaseMap")&&
            AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap"))=="Assets/FenerRunner/Art/SUVPresidentLettering.png";
        private static bool OnScreen(Vector3 p)=>p.z>.1f&&p.x>.01f&&p.x<.99f&&p.y>.01f&&p.y<.99f;
        private static bool Finite(Vector3 p)=>!(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z));
        private static string Evidence(string file)
        {string path=ReviewEvidence.FileFor(15,"../Validation/"+file);Directory.CreateDirectory(Path.GetDirectoryName(path));return path;}
        private void Check(bool passed,string message){if(passed)checks.Add(message);else errors.Add(message);}
        private void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        private void Finish()
        {
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.checks=checks.ToArray();report.errors=errors.ToArray();
            File.WriteAllText(Evidence("unity_chase_suv_v15.json"),JsonUtility.ToJson(report,true));Application.logMessageReceived-=Log;
            Debug.Log("Chase SUV v"+ReviewEvidence.Version+" "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        private void OnDestroy()
        {Application.logMessageReceived-=Log;Application.runInBackground=oldBackground;Time.captureFramerate=oldCapture;Time.timeScale=oldScale;if(wallet!=null)wallet.DeleteIsolatedTestData();}
    }
}
#endif
