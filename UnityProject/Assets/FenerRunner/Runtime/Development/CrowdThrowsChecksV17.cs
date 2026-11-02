#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FenerRunner
{
    public sealed class CrowdThrowsChecksV17:MonoBehaviour
    {
        const string Flag="FenerRunner.CrowdThrowsV17";
        [Serializable] sealed class VisibilityCase
        {
            public int side,visiblePixels,baselineNoisePixels;public float speed,flightProgress,projectedWidth,projectedHeight,landingLead;
            public string capture;public bool passed;
        }
        [Serializable] sealed class Report
        {
            public bool passed;public string utc;public string[] checks,errors;
            public int triangles,pool,throws,leftThrows,rightThrows,maximumActive,previewPixels;public float maximumHeight;
            public VisibilityCase[] mobileVisibility;
            public string scope="Native actual-scene prop pool and hand-cue timing, ballistic flight/bounce/fade, both stands, real fatal contact, pause/menu/restart/disable/origin shift, finite input and shared mesh/material validation. Purely visual bottles with no gameplay colliders. Native prefab and scene renders, not physical phone performance.";
        }
        readonly List<string> checks=new List<string>(),errors=new List<string>();Report report=new Report();
        RunnerGame game;CrowdThrows throws;RunWallet wallet;
        bool prepared,background;float oldScale,oldCapture;int oldRate;
        [MenuItem("Fener Runner/108 - Verify crowd bottle throws v17")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod]static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Crowd throws checks v17").AddComponent<CrowdThrowsChecksV17>();}}
        IEnumerator Start()
        {
            background=Application.runInBackground;oldScale=Time.timeScale;oldCapture=Time.captureDeltaTime;oldRate=Application.targetFrameRate;prepared=true;
            Application.runInBackground=true;Application.targetFrameRate=60;Time.captureDeltaTime=1f/60;Application.logMessageReceived+=Log;
            var run=Run();while(true){bool more;object value=null;try{more=run.MoveNext();if(more)value=run.Current;}catch(Exception e){errors.Add(e.ToString());break;}if(!more)break;yield return value;}
            if(game!=null){game.enabled=false;game.ReturnToMenu();}
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.checks=checks.ToArray();report.errors=errors.ToArray();
            File.WriteAllText(Evidence("unity_crowd_throws_v17.json"),JsonUtility.ToJson(report,true));Application.logMessageReceived-=Log;
            Debug.Log("Crowd throws v17 "+(report.passed?"PASSED":"FAILED")+": "+checks.Count+" checks; "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Run()
        {
            game=FindFirstObjectByType<RunnerGame>();if(game==null)throw new InvalidOperationException("Actual Fener Runner scene required.");
            game.AutomatedTest=true;game.enabled=false;wallet=game.UseWalletForTest("FenerRunner.Test.ThrowsV17."+DateTime.UtcNow.Ticks+".");throws=game.CrowdThrows;
            Check(throws!=null&&throws.PoolCount==CrowdThrows.Capacity,"Game creates exactly eight retained visual bottles");if(throws==null)yield break;
            report.pool=throws.PoolCount;
            var filters=throws.VisualRoot.GetComponentsInChildren<MeshFilter>(true);var renderers=throws.VisualRoot.GetComponentsInChildren<Renderer>(true);
            Check(filters.Length==8&&filters.Select(f=>f.sharedMesh).Distinct().Count()==1&&renderers.Select(r=>r.sharedMaterial).Distinct().Count()==1,"All eight bottles share one baked mesh and one retained material");
            report.triangles=filters[0].sharedMesh.triangles.Length/3;
            Check(report.triangles<1400&&filters[0].sharedMesh.bounds.size.y>.4f&&filters[0].sharedMesh.bounds.size.x<.19f,"PET mesh has a full bottle silhouette within a mobile budget under1400 triangles");
            Check(throws.VisualRoot.GetComponentsInChildren<Component>(true).All(c=>c is Transform||c is MeshFilter||c is MeshRenderer),"Prop hierarchy cannot collide, collect, play sound or alter gameplay");
            game.ReturnToMenu();throws.Tick(1);Check(throws.ActiveCount==0&&Shader.GetGlobalFloat("_CrowdThrowAge")<0,"Menu hides every prop and disables the throw gesture");
            Reset();int objects=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            int score=game.Score,coins=game.Coins;double distance=game.Distance;Vector3 player=game.Motor.transform.position;
            string random=JsonUtility.ToJson(UnityEngine.Random.state);
            for(int i=0;i<60*180;i++){throws.Tick(1f/60);report.maximumActive=Mathf.Max(report.maximumActive,throws.ActiveCount);}
            report.throws=throws.ThrowCount;report.leftThrows=throws.LeftThrowCount;report.rightThrows=throws.RightThrowCount;
            Check(throws.ThrowCount>=45&&throws.ThrowCount<=75&&throws.LeftThrowCount>=18&&throws.RightThrowCount>=18,"Three simulated minutes produce frequent but spaced stadium throws from both stands");
            Check(report.maximumActive<=3&&throws.PoolCount==8&&FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==objects,"Long presentation sampling reuses the bounded pool with at most three visible props and no new objects");
            Check(game.Score==score&&game.Coins==coins&&game.Distance==distance&&game.Motor.transform.position==player&&JsonUtility.ToJson(UnityEngine.Random.state)==random,"Bottles never move the runner, alter rewards or consume gameplay randomness");

            foreach(int hz in new[]{15,30,60,120})
            {
                Reset();Check(throws.StartThrowForTest(-1),"A real left-front-row throw can be scheduled / "+hz+"Hz");
                Check(throws.PendingThrow&&throws.ThrowCount==0&&throws.LastThrowAge==0&&throws.LastThrowOrigin.x< -8,"Windup is shown at an actual supporter before bottle release / "+hz+"Hz");
                throws.Tick(.34f);Check(throws.ActiveCount==0,"No projectile is released during the windup / "+hz+"Hz");throws.Tick(.02f);
                Check(throws.ActiveCount==1&&throws.ThrowCount==1&&throws.LeftThrowCount==1,"Bottle releases once after350ms / "+hz+"Hz");
                float peak=0;bool finite=true,aboveFloor=true,sawBounce=false;float duration=throws.FlightDurationAt(0);
                while(throws.AgeAt(0)<duration+.65f)
                {
                    throws.Tick(1f/hz);Vector3 p=throws.BottleAt(0).position;float age=throws.AgeAt(0);
                    peak=Mathf.Max(peak,p.y);finite&=Finite(p);aboveFloor&=p.y>=.094f;
                    if(age>duration+.03f&&age<duration+.38f)sawBounce|=p.y>.13f;
                }
                report.maximumHeight=Mathf.Max(report.maximumHeight,peak);
                Check(finite&&aboveFloor&&peak>2.3f&&peak<4&&sawBounce,"Gravity-shaped arc clears the stand and bounces above the track without ground penetration / "+hz+"Hz");
                Check(Mathf.Abs(throws.LastLanding.x)<=game.Settings.laneSpacing&&throws.LastLanding.z>game.Motor.transform.position.z+20,"Landing is confined to the track ahead, with no camera-face projectile / "+hz+"Hz");
                throws.Tick(duration+1.95f-throws.AgeAt(0));Check(throws.OpacityAt(0)>0&&throws.OpacityAt(0)<1,"The resting bottle fades before recycling / "+hz+"Hz");
                throws.Tick(.6f);Check(throws.ActiveCount==0,"A completed bottle returns to its existing pool slot / "+hz+"Hz");
            }
            Reset();throws.StartThrowForTest(1);throws.Tick(.60f);Vector3 before=throws.BottleAt(0).position;Quaternion turn=throws.BottleAt(0).rotation;float clock=throws.PresentationTime,cue=throws.LastThrowAge;
            game.Pause();throws.Tick(5);Check(throws.BottleAt(0).position==before&&throws.BottleAt(0).rotation==turn&&throws.PresentationTime==clock&&throws.LastThrowAge==cue,"Pause freezes the bottle trajectory, spin and matching crowd gesture");
            game.Resume();throws.Tick(.03f);Check(throws.BottleAt(0).position!=before,"Resume continues the same throw instead of restarting it");
            before=throws.BottleAt(0).position;Vector3 origin=throws.LastThrowOrigin,landing=throws.LastLanding;float ageBefore=throws.AgeAt(0);
            game.Course.ShiftOrigin(1024);game.Motor.ShiftOrigin(1024);throws.ShiftOrigin(1024);
            Check((throws.BottleAt(0).position-(before-Vector3.forward*1024)).sqrMagnitude<.000001f&&Mathf.Abs(throws.LastThrowOrigin.z-origin.z+1024)<.001f&&Mathf.Abs(throws.LastLanding.z-landing.z+1024)<.001f&&throws.AgeAt(0)==ageBefore,"Origin rebasing moves the live prop, source and target together without changing flight age");
            Check(Mathf.Abs(Shader.GetGlobalVector("_CrowdThrowOrigin").z-throws.LastThrowOrigin.z)<.001f,"The visible thrower cue follows the same origin rebase");
            throws.Tick(.01f);Check(Finite(throws.BottleAt(0).position)&&Mathf.Abs(throws.BottleAt(0).position.z-(before.z-1024))<1,"The next flight step stays continuous after rebasing");
            Reset();throws.StartThrowForTest(1);throws.Tick(.6f);throws.enabled=false;throws.Tick(.5f);Check(throws.ActiveCount==0&&throws.LastThrowAge<0,"Disabled presentation cannot reactivate hidden bottles through a manual game tick");throws.enabled=true;
            Reset();throws.StartThrowForTest(1);throws.Tick(.6f);clock=throws.PresentationTime;before=throws.BottleAt(0).position;
            foreach(float dt in new[]{0f,-1f,float.NaN,float.PositiveInfinity})throws.Tick(dt);
            Check(throws.PresentationTime==clock&&throws.BottleAt(0).position==before,"Nonpositive or nonfinite time cannot corrupt a bottle trajectory");
            game.Obstacles.Enabled=true;game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z);game.AdvanceForTest(1f/60);throws.Tick(0);
            Check(game.State==RunState.Finished&&throws.ActiveCount==0&&!throws.PendingThrow&&throws.LastThrowAge<0,"An actual fatal obstacle contact hides airborne props even when timeScale is zero");
            game.Restart();game.enabled=false;Check(throws.ThrowCount==0&&throws.ActiveCount==0&&throws.PresentationTime==0,"Restart resets the prop schedule and gesture state");

            var visibility=MobileVisibility();while(visibility.MoveNext())yield return visibility.Current;
            var closeup=Closeup();while(closeup.MoveNext())yield return closeup.Current;
        }
        IEnumerator MobileVisibility()
        {
            const int width=1080,height=1920;
            Camera camera=Camera.main;if(camera==null)throw new InvalidOperationException("Actual gameplay camera required for bottle visibility.");
            var speedField=typeof(RunnerGame).GetField("<CurrentSpeed>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic);
            if(speedField==null)throw new InvalidOperationException("CurrentSpeed test backing field not found.");
            float savedScale=Time.timeScale,savedAspect=camera.aspect;RenderTexture previousTarget=camera.targetTexture,target=null;
            Renderer hidden=null;bool hiddenEnabled=false;int bestOverall=0;
            var cases=new List<VisibilityCase>();
            try
            {
                target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=4};target.Create();
                camera.targetTexture=target;camera.aspect=width/(float)height;
                foreach(float speed in new[]{18f,42f})foreach(int side in new[]{-1,1})
                {
                    Reset();speedField.SetValue(game,speed);game.Motor.Speed=speed;
                    // Use the production speed-dependent lens, with the normal
                    // FollowCamera still updating its position during movement.
                    camera.fieldOfView=Mathf.Lerp(58,68,Mathf.InverseLerp(game.Settings.forwardSpeed,RunTempo.HardCap,speed));
                    float startingZ=game.Motor.transform.position.z;throws.StartThrowForTest(side);
                    var result=new VisibilityCase{side=side,speed=speed};cases.Add(result);
                    bool active=false;float duration=0;
                    foreach(float progress in new[]{.55f,.75f,.90f})
                    {
                        Time.timeScale=1;
                        while(!active||throws.AgeAt(0)/duration<progress)
                        {
                            game.Motor.Tick(1f/60);game.Course.Tick(game.Motor.transform.position.z);game.Animation.Tick(1f/60);throws.Tick(1f/60);
                            active=throws.IsActiveAt(0);duration=throws.FlightDurationAt(0);
                            yield return new WaitForEndOfFrame();
                            if(throws.PresentationTime>3)throw new InvalidOperationException("Native bottle visibility fixture never reached an active flight.");
                        }
                        Time.timeScale=0;
                        // The game and crowd shader clocks are held. Rendering
                        // across native URP frames preserves normal depth,
                        // transparency, lighting, fog and post processing.
                        yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                        Texture2D on=ReadFrame(target),repeated=null,off=null;
                        try
                        {
                            yield return new WaitForEndOfFrame();repeated=ReadFrame(target);
                            hidden=throws.BottleAt(0).GetComponent<Renderer>();hiddenEnabled=hidden.enabled;
                            Rect bounds=ProjectedBounds(camera,hidden.bounds,width,height);
                            Vector3 view=camera.WorldToViewportPoint(hidden.bounds.center);
                            bool airborne=throws.IsActiveAt(0)&&throws.AgeAt(0)<duration;
                            hidden.enabled=false;
                            yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();off=ReadFrame(target);
                            int noise=PixelDifference(on,repeated,bounds,12),visible=PixelDifference(repeated,off,bounds,12);
                            hidden.enabled=hiddenEnabled;hidden=null;
                            bool framed=view.z>0&&bounds.xMin>=8&&bounds.xMax<=width-8&&bounds.yMin>=8&&bounds.yMax<=height-8;
                            bool readable=airborne&&framed&&bounds.height>=18&&visible>=80&&noise<=4&&visible>noise*8;
                            if(visible>result.visiblePixels)
                            {
                                result.visiblePixels=visible;result.baselineNoisePixels=noise;result.flightProgress=throws.AgeAt(0)/duration;
                                result.projectedWidth=bounds.width;result.projectedHeight=bounds.height;
                                result.landingLead=throws.LastLanding.z-(startingZ+speed*(CrowdThrows.WindupSeconds+duration));
                                result.capture="v17-bottle-flight-"+(side<0?"left-":"right-")+(int)speed+".png";
                                File.WriteAllBytes(Evidence(result.capture),repeated.EncodeToPNG());
                                result.capture=Path.GetFileName(Evidence(result.capture));
                            }
                            result.passed|=readable;
                            if(readable&&visible>bestOverall){bestOverall=visible;File.WriteAllBytes(Evidence("v17-bottle-flight.png"),repeated.EncodeToPNG());}
                        }
                        finally{if(on!=null)Destroy(on);if(repeated!=null)Destroy(repeated);if(off!=null)Destroy(off);}
                    }
                    Check(result.passed,"Actual portrait game camera visibly renders an airborne bottle from "+(side<0?"left":"right")+" at "+speed+"m/s (actual-material paired pixels "+result.visiblePixels+", frozen noise "+result.baselineNoisePixels+")");
                    Check(result.landingLead>=6,"Bottle still lands at least6m ahead of a moving runner at "+speed+"m/s / "+(side<0?"left":"right"));
                }
                report.mobileVisibility=cases.ToArray();
            }
            finally
            {
                report.mobileVisibility=cases.ToArray();if(hidden!=null)hidden.enabled=hiddenEnabled;
                camera.targetTexture=previousTarget;camera.aspect=savedAspect;Time.timeScale=savedScale;
                if(target!=null){target.Release();Destroy(target);}
            }
        }
        static Texture2D ReadFrame(RenderTexture target)
        {
            RenderTexture previous=RenderTexture.active;
            try{RenderTexture.active=target;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();return pixels;}
            finally{RenderTexture.active=previous;}
        }
        static Rect ProjectedBounds(Camera camera,Bounds bounds,int width,int height)
        {
            Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            for(int i=0;i<8;i++)
            {
                Vector3 corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector3 p=camera.WorldToViewportPoint(corner);Vector2 pixel=new Vector2(p.x*width,p.y*height);min=Vector2.Min(min,pixel);max=Vector2.Max(max,pixel);
            }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        static int PixelDifference(Texture2D first,Texture2D second,Rect bounds,int threshold)
        {
            Color32[] a=first.GetPixels32(),b=second.GetPixels32();int changed=0,width=first.width;
            int x0=Mathf.Clamp(Mathf.FloorToInt(bounds.xMin)-2,0,width),x1=Mathf.Clamp(Mathf.CeilToInt(bounds.xMax)+2,0,width);
            int y0=Mathf.Clamp(Mathf.FloorToInt(bounds.yMin)-2,0,first.height),y1=Mathf.Clamp(Mathf.CeilToInt(bounds.yMax)+2,0,first.height);
            for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++){int i=y*width+x;if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>threshold)changed++;}
            return changed;
        }
        IEnumerator Closeup()
        {
            const int width=640,height=768,mask=1<<31;Vector3 origin=new Vector3(5000,50,5000);
            GameObject model=null,host=null,keyHost=null;RenderTexture target=null;Texture2D pixels=null;
            Light[] lights=FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None);int[] oldMasks=lights.Select(x=>x.cullingMask).ToArray();
            try
            {
                for(int i=0;i<lights.Length;i++)lights[i].cullingMask&=~mask;
                model=Instantiate(Resources.Load<GameObject>(CrowdThrows.ResourceName));model.transform.SetPositionAndRotation(origin,Quaternion.Euler(0,24,-12));model.layer=31;
                host=new GameObject("Native bottle detail camera",typeof(Camera),typeof(UniversalAdditionalCameraData));var camera=host.GetComponent<Camera>();
                camera.fieldOfView=32;camera.aspect=width/(float)height;camera.nearClipPlane=.03f;camera.farClipPlane=5;camera.cullingMask=mask;camera.depth=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.085f,.13f);camera.allowHDR=false;
                camera.transform.position=origin+new Vector3(.40f,.13f,-.92f);camera.transform.LookAt(origin);host.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
                target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=4};target.Create();camera.targetTexture=target;
                keyHost=new GameObject("Native bottle key",typeof(Light));var key=keyHost.GetComponent<Light>();key.type=LightType.Directional;key.intensity=2;key.color=new Color(.92f,.97f,1);key.cullingMask=mask;key.shadows=LightShadows.None;key.transform.rotation=Quaternion.Euler(35,-30,0);
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                RenderTexture previous=RenderTexture.active;try{RenderTexture.active=target;pixels=new Texture2D(width,height,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();}finally{RenderTexture.active=previous;}
                Color32[] colours=pixels.GetPixels32();Color32 back=colours[0];foreach(Color32 p in colours)if(Mathf.Abs(p.r-back.r)+Mathf.Abs(p.g-back.g)+Mathf.Abs(p.b-back.b)>35)report.previewPixels++;
                File.WriteAllBytes(Evidence("v17-bottle-prefab-closeup.png"),pixels.EncodeToPNG());Check(report.previewPixels>12000&&report.previewPixels<width*height*.6f,"Native closeup contains the real capped, labelled PET bottle rather than a blank render");
            }
            finally
            {
                for(int i=0;i<lights.Length;i++)if(lights[i]!=null)lights[i].cullingMask=oldMasks[i];
                if(host!=null){host.GetComponent<Camera>().enabled=false;host.GetComponent<Camera>().targetTexture=null;Destroy(host);}if(model!=null)Destroy(model);if(keyHost!=null)Destroy(keyHost);if(target!=null){target.Release();Destroy(target);}if(pixels!=null)Destroy(pixels);
            }
        }
        void Reset(){game.enabled=false;game.Tokens.Enabled=false;game.Powerups.Enabled=false;game.Play();game.Obstacles.SetActive(false);game.Powerups.IsolatePickupsForTest();throws.ResetPresentation();}
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);
        static string Evidence(string file){string path=ReviewEvidence.FileFor(17,"../Validation/"+file);Directory.CreateDirectory(Path.GetDirectoryName(path));return path;}
        void Check(bool value,string label){if(value)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<60)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;if(!prepared)return;if(wallet!=null)wallet.DeleteIsolatedTestData();Application.runInBackground=background;Application.targetFrameRate=oldRate;Time.captureDeltaTime=oldCapture;Time.timeScale=oldScale;}
    }
}
#endif
