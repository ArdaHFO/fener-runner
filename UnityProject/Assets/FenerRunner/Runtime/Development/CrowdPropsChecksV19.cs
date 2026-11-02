#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class CrowdPropsChecksV19:MonoBehaviour
    {
        const string Flag="FenerRunner.CrowdPropsV19";
        [Serializable] sealed class Visibility
        {public string kind,capture;public float speed,height,landingLead;public int visiblePixels,noisePixels;public bool passed;}
        [Serializable] sealed class Report
        {
            public bool passed;public string utc;public string[] checks,errors;public int[] triangleCounts,kindCounts;
            public int automaticThrows,maximumActive;public float firstRelease,minimumInterval,maximumInterval;public long steadyAllocatedBytes;public Visibility[] visibility;
            public string scope="Native four shared mesh/material assets, automatic frequency/type mix, fixed pool and no gameplay changes, all-kind gravity/contact-floor sampling at15/30/60/120Hz, pause/reset/disable/rebase, and real portrait-camera paired-material visibility at18/42m/s. Props remain visual only. No APK or physical-phone FPS claim.";
        }
        readonly List<string> checks=new List<string>(),errors=new List<string>();readonly Report report=new Report();
        RunnerGame game;CrowdThrows throws;RunWallet wallet;bool prepared,background;float scale,capture;int rate;
        [MenuItem("Fener Runner/123 - Verify frequent varied crowd props v19")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod]static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Frequent varied crowd props checks v19").AddComponent<CrowdPropsChecksV19>();}}
        IEnumerator Start()
        {
            background=Application.runInBackground;rate=Application.targetFrameRate;scale=Time.timeScale;capture=Time.captureDeltaTime;prepared=true;
            Application.runInBackground=true;Application.targetFrameRate=60;Time.captureDeltaTime=1f/60;Application.logMessageReceived+=Log;
            var run=Run();while(true){bool more;object value=null;try{more=run.MoveNext();if(more)value=run.Current;}catch(Exception e){errors.Add(e.ToString());break;}if(!more)break;yield return value;}
            if(game!=null){game.enabled=false;game.ReturnToMenu();}
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.checks=checks.ToArray();report.errors=errors.ToArray();
            File.WriteAllText(Evidence("unity_crowd_props_v19.json"),JsonUtility.ToJson(report,true));Application.logMessageReceived-=Log;
            Debug.Log("Crowd props v19 "+(report.passed?"PASSED":"FAILED")+": "+checks.Count+" checks; "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Run()
        {
            game=FindFirstObjectByType<RunnerGame>();if(game==null)throw new InvalidOperationException("Actual built game scene required.");
            game.AutomatedTest=true;game.enabled=false;wallet=game.UseWalletForTest("FenerRunner.Test.PropsV19."+DateTime.UtcNow.Ticks+".");throws=game.CrowdThrows;
            var library=Resources.Load<CrowdPropLibrary>(CrowdPropLibrary.ResourceName);
            Check(throws!=null&&throws.HasPropVariety&&library!=null&&library.Complete,"Actual game loads the complete four-kind shared prop library");if(throws==null||library==null||!library.Complete)yield break;
            report.triangleCounts=library.meshes.Select(m=>m.triangles.Length/3).ToArray();
            Check(library.meshes.Distinct().Count()==4&&library.materials.Distinct().Count()==2&&report.triangleCounts.All(n=>n>50&&n<1400),"Four distinct prop silhouettes share two materials and stay below1400 triangles each");
            Check(library.meshes.All(m=>m.vertices.All(Finite)&&m.normals.All(n=>Finite(n)&&n.sqrMagnitude>.8f))&&library.materials.All(m=>m.shader.isSupported),"Every generated prop has finite geometry, usable normals and a native-supported material");
            Check(throws.VisualRoot.GetComponentsInChildren<Component>(true).All(c=>c is Transform||c is MeshFilter||c is MeshRenderer),"All pooled prop variants remain visual-only with no collider, physics, script or audio component");
            Reset();int objects=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            string random=JsonUtility.ToJson(UnityEngine.Random.state);int score=game.Score,coins=game.Coins;double distance=game.Distance;Vector3 player=game.Motor.transform.position;
            int previous=0;float last=0;report.minimumInterval=float.PositiveInfinity;
            var sequence=new List<CrowdPropKind>();
            for(int i=0;i<60*180;i++)
            {
                throws.Tick(1f/60);report.maximumActive=Mathf.Max(report.maximumActive,throws.ActiveCount);
                if(throws.ThrowCount>previous)
                {
                    if(previous==0)report.firstRelease=throws.PresentationTime;
                    else{float gap=throws.PresentationTime-last;report.minimumInterval=Mathf.Min(report.minimumInterval,gap);report.maximumInterval=Mathf.Max(report.maximumInterval,gap);}
                    previous=throws.ThrowCount;last=throws.PresentationTime;sequence.Add(throws.LastThrowKind);
                }
            }
            report.automaticThrows=throws.ThrowCount;report.kindCounts=Enumerable.Range(0,4).Select(i=>throws.ThrowsOfKind((CrowdPropKind)i)).ToArray();
            Check(report.automaticThrows>=48&&report.automaticThrows<=75&&report.firstRelease>=2.1f&&report.firstRelease<=3,"Automatic scene presentation releases16–25 props per minute and starts within3seconds");
            Check(report.kindCounts[0]>=24&&report.kindCounts.All(n=>n>=5)&&report.kindCounts.Sum()==report.automaticThrows,"Bottles remain at least eight per minute while cups, crumpled paper and foam footballs all appear repeatedly");
            Check(report.minimumInterval>=1.60f&&report.maximumInterval<=3.90f,"Throw gestures remain separated by recovery time while occasional closer pairs increase activity");
            Check(report.maximumActive<=3&&throws.PoolCount==8&&objects==FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,"Three minutes of mixed props reuse exactly eight objects with at most three live props");
            Check(game.Score==score&&game.Coins==coins&&game.Distance==distance&&game.Motor.transform.position==player&&JsonUtility.ToJson(UnityEngine.Random.state)==random,"Frequent props cannot affect score, coins, motion or gameplay randomness");
            Reset();var repeated=new List<CrowdPropKind>();previous=0;
            for(int i=0;i<60*180;i++){throws.Tick(1f/60);if(throws.ThrowCount>previous){previous=throws.ThrowCount;repeated.Add(throws.LastThrowKind);}}
            Check(sequence.SequenceEqual(repeated),"Resetting the same course seed reproduces the same weighted prop sequence");
            for(int i=0;i<60;i++)throws.Tick(1f/60);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<600;i++)throws.Tick(1f/60);report.steadyAllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(report.steadyAllocatedBytes==0,"Automatic scheduling, mesh reuse and steady mixed-prop ticks allocate zero managed bytes on the native thread");
            foreach(CrowdPropKind kind in Enum.GetValues(typeof(CrowdPropKind)))foreach(int hz in new[]{15,30,60,120})
            {
                Reset();Check(throws.StartThrowForTest(-1,kind),"Real prop kind can be selected for an isolated native throw / "+kind+" / "+hz+"Hz");
                throws.Tick(.36f);var item=throws.BottleAt(0);var renderer=item.GetComponent<Renderer>();var surface=item.GetComponent<MeshFilter>().sharedMesh.vertices;
                Check(throws.KindAt(0)==kind&&item.GetComponent<MeshFilter>().sharedMesh==library.MeshFor(kind)&&renderer.sharedMaterial==library.MaterialFor(kind),"Release selects the correct shared mesh/material without a new object / "+kind+" / "+hz+"Hz");
                bool finite=true,clear=true;float maximum=0;int frames=0;float duration=throws.FlightDurationAt(0);
                while(throws.IsActiveAt(0)&&throws.AgeAt(0)<duration+1.6f&&frames++<hz*5)
                {
                    throws.Tick(1f/hz);finite&=Finite(item.position);maximum=Mathf.Max(maximum,item.position.y);
                    // A rotated sphere's Renderer.bounds encloses its rotated
                    // local box, including empty corners below the true mesh.
                    // Transform actual baked vertices to measure the surface.
                    if(throws.AgeAt(0)>=duration+.5f)clear&=MinimumSurfaceY(item,surface)>=-.012f;
                }
                Check(finite&&clear&&maximum>2.3f&&maximum<4.6f,"Real prop trajectory and resting silhouette remain finite and above the road / "+kind+" / "+hz+"Hz");
            }
            foreach(CrowdPropKind kind in Enum.GetValues(typeof(CrowdPropKind)))
            {
                Reset();throws.StartThrowForTest(1,kind);throws.Tick(.7f);var item=throws.BottleAt(0);Vector3 p=item.position;Quaternion rotation=item.rotation;float age=throws.AgeAt(0);
                game.Pause();throws.Tick(3);Check(item.position==p&&item.rotation==rotation&&throws.AgeAt(0)==age,"Pause freezes active variant, spin and age / "+kind);game.Resume();
                throws.ShiftOrigin(1280);game.Course.ShiftOrigin(1280);game.Motor.ShiftOrigin(1280);
                Check((item.position-(p-Vector3.forward*1280)).sqrMagnitude<.000001f&&throws.KindAt(0)==kind&&throws.AgeAt(0)==age,"World rebasing preserves active variant and age / "+kind);
                game.ReturnToMenu();throws.Tick(0);Check(throws.ActiveCount==0&&!throws.PendingThrow,"Menu clears the whole mixed prop pool even with zero delta / "+kind);
                Reset();throws.StartThrowForTest(1,kind);throws.Tick(.7f);throws.enabled=false;throws.Tick(.5f);
                Check(throws.ActiveCount==0&&!throws.PendingThrow&&throws.LastThrowAge<0,"Disabling the presenter clears the active variant and cannot revive it through a manual tick / "+kind);
                throws.enabled=true;Reset();Check(throws.ThrowCount==0&&throws.ActiveCount==0&&throws.PresentationTime==0,"Re-enable and reset begin a clean finite schedule / "+kind);
            }
            var visibility=VisibleVariants();while(visibility.MoveNext())yield return visibility.Current;
        }
        IEnumerator VisibleVariants()
        {
            const int width=1080,height=1920;Camera camera=Camera.main;if(camera==null)throw new InvalidOperationException("Actual portrait camera required.");
            var speedField=typeof(RunnerGame).GetField("<CurrentSpeed>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic);
            float oldScale=Time.timeScale,oldAspect=camera.aspect,oldFov=camera.fieldOfView;RenderTexture previous=camera.targetTexture,target=null;Renderer hidden=null;bool hiddenEnabled=false;
            var results=new List<Visibility>();
            try
            {
                target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=4};target.Create();camera.targetTexture=target;camera.aspect=width/(float)height;
                foreach(CrowdPropKind kind in Enum.GetValues(typeof(CrowdPropKind)))foreach(float speed in new[]{18f,42f})
                {
                    Reset();speedField.SetValue(game,speed);game.Motor.Speed=speed;camera.fieldOfView=Mathf.Lerp(58,68,Mathf.InverseLerp(game.Settings.forwardSpeed,RunTempo.HardCap,speed));
                    int side=((int)kind&1)==0?-1:1;float start=game.Motor.transform.position.z;throws.StartThrowForTest(side,kind);
                    var row=new Visibility{kind=kind.ToString(),speed=speed};results.Add(row);
                    foreach(float progress in new[]{.65f,.85f,.93f})
                    {
                        Time.timeScale=1;int guard=0;
                        while(!throws.IsActiveAt(0)||throws.AgeAt(0)<throws.FlightDurationAt(0)*progress)
                        {
                            game.Motor.Tick(1f/60);game.Course.Tick(game.Motor.transform.position.z);game.Animation.Tick(1f/60);throws.Tick(1f/60);
                            yield return new WaitForEndOfFrame();if(guard++>180)throw new InvalidOperationException("Visibility fixture never reached airborne prop.");
                        }
                        Time.timeScale=0;yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                        Texture2D a=Read(target),b=null,c=null;
                        try
                        {
                            yield return new WaitForEndOfFrame();b=Read(target);hidden=throws.BottleAt(0).GetComponent<Renderer>();hiddenEnabled=hidden.enabled;
                            Rect bounds=BoundsOnScreen(camera,hidden.bounds,width,height);Vector3 viewport=camera.WorldToViewportPoint(hidden.bounds.center);
                            hidden.enabled=false;yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();c=Read(target);
                            int noise=Difference(a,b,bounds),pixels=Difference(b,c,bounds);hidden.enabled=hiddenEnabled;hidden=null;
                            bool framed=viewport.z>0&&bounds.xMin>=8&&bounds.xMax<=width-8&&bounds.yMin>=8&&bounds.yMax<=height-8;
                            bool pass=framed&&throws.IsActiveAt(0)&&throws.AgeAt(0)<throws.FlightDurationAt(0)&&bounds.height>=16&&pixels>=70&&noise<=4&&pixels>noise*8;
                            row.passed|=pass;
                            if(pixels>row.visiblePixels)
                            {
                                row.visiblePixels=pixels;row.noisePixels=noise;row.height=bounds.height;row.landingLead=throws.LastLanding.z-(start+speed*(CrowdThrows.WindupSeconds+throws.FlightDurationAt(0)));
                                row.capture="v19-prop-"+kind.ToString().ToLowerInvariant()+"-"+(int)speed+".png";File.WriteAllBytes(Evidence(row.capture),b.EncodeToPNG());
                            }
                        }
                        finally{if(a!=null)Destroy(a);if(b!=null)Destroy(b);if(c!=null)Destroy(c);}
                    }
                    Check(row.passed&&row.landingLead>=6,"Actual native portrait camera reads "+kind+" at "+speed+"m/s with a safe ahead landing (pixels "+row.visiblePixels+", noise "+row.noisePixels+")");
                }
            }
            finally
            {
                report.visibility=results.ToArray();if(hidden!=null)hidden.enabled=hiddenEnabled;camera.targetTexture=previous;camera.aspect=oldAspect;camera.fieldOfView=oldFov;Time.timeScale=oldScale;
                if(target!=null){target.Release();Destroy(target);}
            }
        }
        static Texture2D Read(RenderTexture target)
        {var old=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();RenderTexture.active=old;return texture;}
        static Rect BoundsOnScreen(Camera camera,Bounds b,int width,int height)
        {
            Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            for(int i=0;i<8;i++){Vector3 p=camera.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));Vector2 pixel=new Vector2(p.x*width,p.y*height);min=Vector2.Min(min,pixel);max=Vector2.Max(max,pixel);}
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        static int Difference(Texture2D a,Texture2D b,Rect bounds)
        {
            var x=a.GetPixels32();var y=b.GetPixels32();int count=0;
            int x0=Mathf.Clamp(Mathf.FloorToInt(bounds.xMin)-2,0,a.width),x1=Mathf.Clamp(Mathf.CeilToInt(bounds.xMax)+2,0,a.width),y0=Mathf.Clamp(Mathf.FloorToInt(bounds.yMin)-2,0,a.height),y1=Mathf.Clamp(Mathf.CeilToInt(bounds.yMax)+2,0,a.height);
            for(int row=y0;row<y1;row++)for(int col=x0;col<x1;col++){int i=row*a.width+col;if(Mathf.Abs(x[i].r-y[i].r)+Mathf.Abs(x[i].g-y[i].g)+Mathf.Abs(x[i].b-y[i].b)>12)count++;}return count;
        }
        void Reset(){game.enabled=false;game.Tokens.Enabled=false;game.Powerups.Enabled=false;game.Obstacles.UseSeedForTest(1907);game.Play();game.Obstacles.SetActive(false);game.Powerups.IsolatePickupsForTest();throws.ResetPresentation();}
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);
        static float MinimumSurfaceY(Transform item,Vector3[] vertices)
        {Matrix4x4 transform=item.localToWorldMatrix;float lowest=float.PositiveInfinity;foreach(var point in vertices)lowest=Mathf.Min(lowest,transform.MultiplyPoint3x4(point).y);return lowest;}
        static string Evidence(string file){string p=ReviewEvidence.FileFor(19,"../Validation/"+file);Directory.CreateDirectory(Path.GetDirectoryName(p));return p;}
        void Check(bool value,string label){if(value)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<60)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;if(!prepared)return;if(wallet!=null)wallet.DeleteIsolatedTestData();Application.runInBackground=background;Application.targetFrameRate=rate;Time.timeScale=scale;Time.captureDeltaTime=capture;}
    }
}
#endif
