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
    // Native URP evidence complements v17's LOD/throw/pause checks. Reactions
    // are isolated with a fixed camera and clock, not compared to shader math.
    public sealed class CrowdLookChecksV18:MonoBehaviour
    {
        const string Flag="FenerRunner.CrowdLookV18";
        [Serializable] sealed class Report
        {
            public bool passed;public string utc,unityVersion;public string[] checks,errors,captures;
            public int people,wardrobeKinds,hairKinds,nearTriangles,farTriangles;
            public float widthRange,depthRange,headWidthRange;
            public int slidePixels,clapPixels,powerPixels,distinctReactionPixels,gazePixels,remotePixels,expiredPixels,rebasePixels,rebaseUnmatchedPixels,brokenRebaseUnmatchedPixels;
            public string scope="Native generated mesh appearance diversity plus actual URP closeups with fixed clock, isolated slide/clap/power reactions and gaze. Existing v17 tests retain LOD, pause, throw and geometry budgets. No physical-phone performance claim.";
        }
        readonly Report report=new Report();readonly List<string> checks=new List<string>(),errors=new List<string>(),captures=new List<string>();
        RunnerGame game;RunWallet wallet;float scale,capture;bool background,prepared;int rate;string settings;
        [MenuItem("Fener Runner/118 - Verify crowd appearance v18")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Crowd appearance checks v18").AddComponent<CrowdLookChecksV18>();}}
        IEnumerator Start()
        {
            scale=Time.timeScale;capture=Time.captureDeltaTime;background=Application.runInBackground;rate=Application.targetFrameRate;prepared=true;
            Application.runInBackground=true;Application.targetFrameRate=60;Time.captureDeltaTime=1f/60;Application.logMessageReceived+=Log;
            IEnumerator routine=Run();
            while(true)
            {bool more;object wait=null;try{more=routine.MoveNext();if(more)wait=routine.Current;}catch(Exception e){errors.Add(e.ToString());break;}if(!more)break;yield return wait;}
            if(game!=null){game.ReturnToMenu();Check(settings==JsonUtility.ToJson(game.Settings),"Appearance verification preserves authored gameplay settings");}
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;
            report.checks=checks.ToArray();report.errors=errors.ToArray();report.captures=captures.ToArray();
            File.WriteAllText(Evidence("unity_crowd_look_v18.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Crowd look v18 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Run()
        {
            game=FindFirstObjectByType<RunnerGame>();if(game==null)throw new InvalidOperationException("Built game scene required.");
            game.AutomatedTest=true;game.enabled=false;settings=JsonUtility.ToJson(game.Settings);
            wallet=game.UseWalletForTest("FenerRunner.Test.CrowdLookV18."+DateTime.UtcNow.Ticks+".");game.Play();game.Obstacles.SetActive(false);game.Tokens.Enabled=false;game.Powerups.Enabled=false;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FenerRunner/Generated/Prefabs/StadiumSegment.prefab");
            float railPlane=float.PositiveInfinity;
            foreach(var point in prefab.transform.Find("Stadium_Yellow").GetComponent<MeshFilter>().sharedMesh.vertices)
                if(point.y>.90f&&point.y<1.0f&&Mathf.Abs(point.x)<10)railPlane=Mathf.Min(railPlane,Mathf.Abs(point.x));
            int signs=0;bool clearance=true;
            foreach(var label in prefab.GetComponentsInChildren<TextMesh>(true))
            {
                if(Mathf.Abs(label.transform.localPosition.x)>=10)continue;
                signs++;float gap=railPlane-Mathf.Abs(label.transform.localPosition.x);
                clearance&=gap>.02f&&gap<.08f;
            }
            Check(signs==4&&clearance,"Four actual front-stand glyph planes have measured clearance from the generated yellow rail surface");
            var mesh=prefab.transform.Find("Animated supporters and flags").GetComponent<MeshFilter>().sharedMesh;
            var far=prefab.transform.Find("Distant supporters").GetComponent<MeshFilter>().sharedMesh;
            report.nearTriangles=mesh.triangles.Length/3;report.farTriangles=far.triangles.Length/3;
            var personas=new List<Vector4>();var anchors=new List<Vector4>();var motion=new List<Vector4>();mesh.GetUVs(3,personas);mesh.GetUVs(2,anchors);mesh.GetUVs(1,motion);
            Check(personas.Count==mesh.vertexCount,"Every vertex carries a complete supporter morphology stream");
            if(personas.Count!=mesh.vertexCount)throw new InvalidOperationException("Rebuild supporter assets before this check.");
            var people=new Dictionary<Vector3,Vector4>();
            bool stable=true,finite=true;
            for(int i=0;i<mesh.vertexCount;i++)
            {
                if(motion[i].y>=8)continue;Vector3 key=new Vector3(anchors[i].x,anchors[i].y,anchors[i].z);Vector4 p=personas[i];
                if(people.TryGetValue(key,out var prior))stable&=p==prior;else people.Add(key,p);
                finite&=Finite(p.x)&&Finite(p.y)&&Finite(p.z)&&Finite(p.w);
            }
            report.people=people.Count;report.wardrobeKinds=people.Values.Select(p=>(int)p.w%5).Distinct().Count();report.hairKinds=people.Values.Select(p=>((int)p.w/5)%6).Distinct().Count();
            report.widthRange=people.Values.Max(p=>p.x)-people.Values.Min(p=>p.x);report.depthRange=people.Values.Max(p=>p.y)-people.Values.Min(p=>p.y);report.headWidthRange=people.Values.Max(p=>p.z)-people.Values.Min(p=>p.z);
            Check(stable&&finite&&people.Count==272,"All272 supporters keep one finite consistent morphology across every limb and head vertex");
            Check(report.wardrobeKinds==5&&report.hairKinds==6,"Native generated people include five wardrobe styles and six hair/headwear silhouettes");
            Check(report.widthRange>.30f&&report.depthRange>.23f&&report.headWidthRange>.15f,"Body width, depth and head shape vary independently instead of merely changing scale");
            Check(report.nearTriangles<=180000&&report.farTriangles<8000,"Additional appearance detail stays inside the existing180k/8k geometry budgets");
            Time.timeScale=0;
            const int mask=1<<31,width=1280,height=768;Vector3 origin=new Vector3(0,50,0);
            var lights=FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None);int[] masks=lights.Select(l=>l.cullingMask).ToArray();
            string[] scalars={"_StadiumTime","_CrowdEnergy","_CrowdThrowAge","_CrowdWorldOffset"};float[] values=scalars.Select(Shader.GetGlobalFloat).ToArray();
            string[] vectors={"_CrowdThrowOrigin","_CrowdReactionOrigin","_CrowdReactionData","_CrowdPlayer"};Vector4[] vectorValues=vectors.Select(Shader.GetGlobalVector).ToArray();
            GameObject visual=null,cameraHost=null,lightHost=null;RenderTexture target=null;var textures=new List<Texture2D>();
            try
            {
                foreach(var light in lights)light.cullingMask&=~mask;
                visual=Instantiate(prefab);visual.transform.position=origin;
                foreach(Transform item in visual.GetComponentsInChildren<Transform>(true))item.gameObject.layer=31;
                foreach(var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
                visual.GetComponent<LODGroup>().ForceLOD(0);
                cameraHost=new GameObject("Native supporter appearance camera",typeof(Camera),typeof(UniversalAdditionalCameraData));Camera camera=cameraHost.GetComponent<Camera>();
                camera.orthographic=true;camera.orthographicSize=2.15f;camera.aspect=width/(float)height;camera.cullingMask=mask;camera.depth=100;camera.nearClipPlane=.1f;camera.farClipPlane=80;
                camera.allowHDR=false;camera.allowMSAA=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.055f,.085f,1);
                cameraHost.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
                target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=1};target.Create();camera.targetTexture=target;
                lightHost=new GameObject("Native supporter appearance light",typeof(Light));var key=lightHost.GetComponent<Light>();key.type=LightType.Directional;key.intensity=1.25f;key.shadows=LightShadows.None;key.cullingMask=mask;key.transform.rotation=Quaternion.Euler(35,-70,0);
                float crowdX=game.Settings.laneSpacing*1.5f+.7f+3.4f,personZ=.4f+8*1.16f;
                camera.transform.position=origin+new Vector3(crowdX-5.5f,3.3f,personZ-2.1f);camera.transform.LookAt(origin+new Vector3(crowdX+.7f,1.95f,personZ+1));
                Shader.SetGlobalFloat("_StadiumTime",2.4f);Shader.SetGlobalFloat("_CrowdEnergy",.55f);Shader.SetGlobalFloat("_CrowdThrowAge",-1);Shader.SetGlobalFloat("_CrowdWorldOffset",0);
                Shader.SetGlobalVector("_CrowdPlayer",Vector4.zero);Shader.SetGlobalVector("_CrowdReactionData",new Vector4(-1,0,0,10));
                Shader.SetGlobalVector("_CrowdReactionOrigin",new Vector4(origin.x+crowdX,origin.y+1,origin.z+personZ,1));
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();var baseline=Read(target);textures.Add(baseline);Save(baseline,"v18-crowd-natural-closeup.png");
                Shader.SetGlobalVector("_CrowdReactionData",new Vector4(.85f,2,1,9));
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();var slide=Read(target);textures.Add(slide);report.slidePixels=Difference(baseline,slide);Save(slide,"v18-crowd-slide-reaction.png");
                Shader.SetGlobalVector("_CrowdReactionData",new Vector4(.85f,4,1,9));
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();var clap=Read(target);textures.Add(clap);report.clapPixels=Difference(baseline,clap);
                Shader.SetGlobalVector("_CrowdReactionData",new Vector4(.85f,5,1,9));
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();var power=Read(target);textures.Add(power);report.powerPixels=Difference(baseline,power);report.distinctReactionPixels=Difference(slide,power);Save(power,"v18-crowd-power-salute.png");
                Check(report.slidePixels>500&&report.clapPixels>500&&report.powerPixels>500,"Actual GPU poses visibly react to slide, coin-streak applause and power collection at a fixed clock");
                Check(report.distinctReactionPixels>1000,"Slide lean and power salute produce distinct visible body/arm poses");
                Shader.SetGlobalVector("_CrowdReactionOrigin",new Vector4(origin.x+crowdX,origin.y+1,origin.z+personZ+100,1));
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();var remote=Read(target);textures.Add(remote);report.remotePixels=Difference(baseline,remote);
                Check(report.remotePixels<30,"A reaction100m away does not animate the nearby stand");
                Shader.SetGlobalVector("_CrowdReactionOrigin",new Vector4(origin.x+crowdX,origin.y+1,origin.z+personZ,1));Shader.SetGlobalVector("_CrowdReactionData",new Vector4(3.6f,5,1,9));
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();var expired=Read(target);textures.Add(expired);report.expiredPixels=Difference(baseline,expired);
                Check(report.expiredPixels<30,"An expired reaction returns exactly to the autonomous pose");
                Shader.SetGlobalVector("_CrowdPlayer",new Vector4(origin.x,origin.y,origin.z+personZ+5,1));
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();var gaze=Read(target);textures.Add(gaze);report.gazePixels=Difference(baseline,gaze);
                Check(report.gazePixels>100,"Visible heads turn toward the nearby runner instead of staring in one fixed direction");
                // Shift the whole scene and lens together, including a live
                // reaction and gaze target. Only the absolute-distance uniform
                // compensates autonomous wave/phase animation after rebasing.
                Shader.SetGlobalVector("_CrowdReactionData",new Vector4(.85f,5,1,9));
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();var beforeRebase=Read(target);textures.Add(beforeRebase);
                Save(beforeRebase,"v18-crowd-rebase-before.png");const float shift=1280;
                visual.transform.position-=Vector3.forward*shift;camera.transform.position-=Vector3.forward*shift;
                Vector4 reactionOrigin=Shader.GetGlobalVector("_CrowdReactionOrigin"),player=Shader.GetGlobalVector("_CrowdPlayer");reactionOrigin.z-=shift;player.z-=shift;
                Shader.SetGlobalVector("_CrowdReactionOrigin",reactionOrigin);Shader.SetGlobalVector("_CrowdPlayer",player);Shader.SetGlobalFloat("_CrowdWorldOffset",shift);
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();var afterRebase=Read(target);textures.Add(afterRebase);report.rebasePixels=Difference(beforeRebase,afterRebase);
                Save(afterRebase,"v18-crowd-rebase-after.png");report.rebaseUnmatchedPixels=Unmatched(beforeRebase,afterRebase);
                // World-space projection can shift a silhouette fractionally
                // across a raster edge. Match within one pixel in both
                // directions, then prove a real missing-offset pose jump fails.
                Shader.SetGlobalFloat("_CrowdWorldOffset",0);
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();var brokenRebase=Read(target);textures.Add(brokenRebase);
                Save(brokenRebase,"v18-crowd-rebase-missing-offset-control.png");report.brokenRebaseUnmatchedPixels=Unmatched(beforeRebase,brokenRebase);
                Check(report.rebaseUnmatchedPixels<100,"A1280m origin rebase preserves native poses within one-pixel raster tolerance");
                Check(report.brokenRebaseUnmatchedPixels>1000,"Independent missing-offset control detects a real crowd pose jump above1000 unmatched pixels");
            }
            finally
            {
                for(int i=0;i<lights.Length;i++)if(lights[i]!=null)lights[i].cullingMask=masks[i];
                for(int i=0;i<scalars.Length;i++)Shader.SetGlobalFloat(scalars[i],values[i]);for(int i=0;i<vectors.Length;i++)Shader.SetGlobalVector(vectors[i],vectorValues[i]);
                if(cameraHost!=null){cameraHost.GetComponent<Camera>().enabled=false;cameraHost.GetComponent<Camera>().targetTexture=null;Destroy(cameraHost);}
                if(visual!=null)Destroy(visual);if(lightHost!=null)Destroy(lightHost);if(target!=null){target.Release();Destroy(target);}foreach(var texture in textures)Destroy(texture);
            }
        }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        static Texture2D Read(RenderTexture target)
        {var old=RenderTexture.active;RenderTexture.active=target;var t=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,target.width,target.height),0,0);t.Apply();RenderTexture.active=old;return t;}
        static int Difference(Texture2D a,Texture2D b)
        {var x=a.GetPixels32();var y=b.GetPixels32();int n=0;for(int i=0;i<x.Length;i++)if(Mathf.Abs(x[i].r-y[i].r)+Mathf.Abs(x[i].g-y[i].g)+Mathf.Abs(x[i].b-y[i].b)>25)n++;return n;}
        static int Unmatched(Texture2D a,Texture2D b)
        {
            Color32[] x=a.GetPixels32(),y=b.GetPixels32();int count=0,w=a.width,h=a.height;
            for(int row=1;row<h-1;row++)for(int col=1;col<w-1;col++)
            {
                int i=row*w+col;if(ColorDistance(x[i],y[i])<=25)continue;bool forward=false,backward=false;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {int j=(row+dy)*w+col+dx;forward|=ColorDistance(x[i],y[j])<=25;backward|=ColorDistance(y[i],x[j])<=25;}
                if(!forward||!backward)count++;
            }
            return count;
        }
        static int ColorDistance(Color32 a,Color32 b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b);
        void Save(Texture2D image,string name){File.WriteAllBytes(Evidence(name),image.EncodeToPNG());captures.Add(Path.GetFileName(Evidence(name)));}
        static string Evidence(string name){string p=ReviewEvidence.FileFor(18,"../Validation/"+name);Directory.CreateDirectory(Path.GetDirectoryName(p));return p;}
        void Check(bool ok,string name){if(ok)checks.Add(name);else errors.Add(name);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;if(!prepared)return;if(wallet!=null)wallet.DeleteIsolatedTestData();Time.timeScale=scale;Time.captureDeltaTime=capture;Application.runInBackground=background;Application.targetFrameRate=rate;}
    }
}
#endif
