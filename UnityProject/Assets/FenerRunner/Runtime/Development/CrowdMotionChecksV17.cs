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
    public sealed class CrowdMotionChecksV17:MonoBehaviour
    {
        const string Flag="FenerRunner.CrowdMotionV17";
        const string PrefabPath="Assets/FenerRunner/Generated/Prefabs/StadiumSegment.prefab";
        [Serializable] sealed class Report
        {
            public bool passed;public string utc,unityVersion;public string[] checks,errors,captures;
            public int people,gestureKinds,nearVertices,nearTriangles,farVertices,farTriangles;
            public int changingPixels,pausedPixels,throwPixels,nearLodPixels,farLodPixels,nearRedPixels,farWhitePixels;
            public float qualityLodBias,lodNearTransition;
            public string scope="Native generated stadium mesh streams and actual URP rendering. Fixed-camera closeups compare genuine GPU poses, repeat the same shader clock, and isolate a synchronized throw gesture. Actual prefab LODs are rendered from gameplay lens at two distances. Actual game pause freezes the stadium clock. No APK, physical-phone FPS or touchscreen claim.";
        }
        readonly Report report=new Report();readonly List<string> checks=new List<string>(),errors=new List<string>(),captures=new List<string>();
        RunnerGame game;RunWallet wallet;bool prepared,background;int rate;float capture,scale;string settings;
        [MenuItem("Fener Runner/107 - Verify crowd animation v17")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Crowd motion checks v17").AddComponent<CrowdMotionChecksV17>();}}
        IEnumerator Start()
        {
            background=Application.runInBackground;rate=Application.targetFrameRate;capture=Time.captureDeltaTime;scale=Time.timeScale;prepared=true;
            Application.runInBackground=true;Application.targetFrameRate=60;Time.captureDeltaTime=1f/60;Application.logMessageReceived+=Log;
            IEnumerator routine=Run();
            while(true)
            {
                bool more;object wait=null;
                try{more=routine.MoveNext();if(more)wait=routine.Current;}catch(Exception e){errors.Add(e.ToString());break;}
                if(!more)break;yield return wait;
            }
            if(game!=null){game.enabled=false;game.ReturnToMenu();Check(settings==JsonUtility.ToJson(game.Settings),"Crowd checks do not change authored gameplay settings");}
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;
            report.checks=checks.ToArray();report.errors=errors.ToArray();report.captures=captures.ToArray();
            File.WriteAllText(Evidence("unity_crowd_motion_v17.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Crowd motion v17 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Run()
        {
            game=FindFirstObjectByType<RunnerGame>();if(game==null)throw new InvalidOperationException("Built game scene required.");
            game.AutomatedTest=true;game.enabled=false;settings=JsonUtility.ToJson(game.Settings);
            wallet=game.UseWalletForTest("FenerRunner.Test.CrowdV17."+DateTime.UtcNow.Ticks+".");game.Play();game.Obstacles.SetActive(false);game.Tokens.Enabled=false;game.Powerups.Enabled=false;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var near=prefab.transform.Find("Animated supporters and flags").GetComponent<MeshFilter>().sharedMesh;
            var far=prefab.transform.Find("Distant supporters").GetComponent<MeshFilter>().sharedMesh;
            report.nearVertices=near.vertexCount;report.nearTriangles=near.triangles.Length/3;report.farVertices=far.vertexCount;report.farTriangles=far.triangles.Length/3;
            Check(report.nearTriangles<=180000&&report.farTriangles<report.nearTriangles/8&&report.farTriangles<8000,"Near crowd stays below180k triangles and far LOD below8k with greater-than-eightfold reduction");
            List<Vector4> motion=new List<Vector4>(),anchors=new List<Vector4>();near.GetUVs(1,motion);near.GetUVs(2,anchors);
            Check(motion.Count==near.vertexCount&&anchors.Count==near.vertexCount,"Every shared vertex carries a complete articulated part and stable person anchor");
            var people=new Dictionary<Vector3,HashSet<int>>();var gestures=new HashSet<int>();var phases=new HashSet<int>();
            bool finite=true,feetSafe=true,limbs=true;
            var vertices=near.vertices;
            for(int i=0;i<near.vertexCount;i++)
            {
                Vector4 a=anchors[i],m=motion[i];finite&=Finite(vertices[i])&&Finite(a)&&Finite(m);
                if(m.y>=8)continue;
                Vector3 key=new Vector3(a.x,a.y,a.z);if(!people.ContainsKey(key))people[key]=new HashSet<int>();people[key].Add((int)m.y);
                gestures.Add((int)m.w);phases.Add((int)(m.x*1000));
                if(m.y==7)feetSafe&=vertices[i].y>=a.y-.02f&&vertices[i].y<=a.y+.15f;
                if(m.y>=3&&m.y<=5)limbs&=Mathf.Abs(m.z)==1&&a.w>=.92f&&a.w<=1.08f;
            }
            report.people=people.Count;report.gestureKinds=gestures.Count;
            Check(finite&&near.normals.All(n=>Finite(n)&&n.sqrMagnitude>.8f),"Crowd positions, correctly transformed normals and animation streams remain finite");
            Check(report.people==272&&report.gestureKinds==6&&phases.Count>240,"Both stands contain272 distinct people with six gestures and varied stable phases");
            Check(limbs&&people.Values.All(parts=>new[]{0,1,2,3,4,5,7}.All(parts.Contains)),"Every person has connected shoulder/forearm/hand groups and a shared foot anchor");
            Check(feetSafe,"Authored soles stay close to the stand level rather than floating over it");
            var nearRenderer=prefab.transform.Find("Animated supporters and flags").GetComponent<MeshRenderer>();
            var farRenderer=prefab.transform.Find("Distant supporters").GetComponent<MeshRenderer>();
            Check(nearRenderer.sharedMaterial==farRenderer.sharedMaterial&&nearRenderer.sharedMaterial.shader.isSupported,"Both LODs share one supported crowd material");
            Check(new[]{nearRenderer.gameObject,farRenderer.gameObject}.All(g=>g.GetComponents<Component>().All(c=>c is Transform||c is MeshRenderer||c is MeshFilter)),"No per-person scripts, skinned renderers, animators or physics objects are introduced");
            var lod=prefab.GetComponent<LODGroup>().GetLODs();
            report.qualityLodBias=QualitySettings.lodBias;report.lodNearTransition=lod[0].screenRelativeTransitionHeight;
            Check(lod.Length==2&&lod.All(l=>l.renderers.Length==1)&&lod[0].screenRelativeTransitionHeight>lod[1].screenRelativeTransitionHeight,"The actual stadium prefab selects one crowd renderer per LOD");
            Time.timeScale=0;
            const int width=1280,height=768,mask=1<<31;
            Vector3 origin=new Vector3(5000,50,5000);
            GameObject visual=null,cameraHost=null,keyHost=null;RenderTexture target=null;
            var originals=FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None);int[] masks=originals.Select(l=>l.cullingMask).ToArray();
            bool oldFog=RenderSettings.fog;
            float oldTime=Shader.GetGlobalFloat("_StadiumTime"),oldEnergy=Shader.GetGlobalFloat("_CrowdEnergy"),oldAge=Shader.GetGlobalFloat("_CrowdThrowAge");Vector4 oldOrigin=Shader.GetGlobalVector("_CrowdThrowOrigin");
            var pixels=new List<Texture2D>();Material white=null,red=null;
            try
            {
                foreach(var light in originals)light.cullingMask&=~mask;
                visual=Instantiate(prefab);visual.transform.position=origin;
                foreach(Transform item in visual.GetComponentsInChildren<Transform>(true))item.gameObject.layer=31;
                foreach(var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
                var instanceLod=visual.GetComponent<LODGroup>();instanceLod.ForceLOD(0);
                cameraHost=new GameObject("Native crowd closeup camera",typeof(Camera),typeof(UniversalAdditionalCameraData));
                Camera camera=cameraHost.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=2.45f;camera.aspect=width/(float)height;
                camera.nearClipPlane=.1f;camera.farClipPlane=80;camera.cullingMask=mask;camera.depth=100;camera.allowHDR=false;camera.allowMSAA=false;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.055f,.085f,1);
                cameraHost.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
                target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=1};target.Create();camera.targetTexture=target;
                keyHost=new GameObject("Native crowd key light",typeof(Light));var keyLight=keyHost.GetComponent<Light>();keyLight.type=LightType.Directional;keyLight.intensity=1.25f;keyLight.shadows=LightShadows.None;keyLight.cullingMask=mask;keyLight.transform.rotation=Quaternion.Euler(35,-70,0);
                float crowdX=game.Settings.laneSpacing*1.5f+.7f+3.4f,personZ=.4f+8*1.16f;
                camera.transform.position=origin+new Vector3(crowdX-5.5f,3.4f,personZ-2.5f);camera.transform.LookAt(origin+new Vector3(crowdX+1,1.9f,personZ+1.2f));
                Shader.SetGlobalFloat("_CrowdEnergy",.7f);Shader.SetGlobalFloat("_CrowdThrowAge",-1);Shader.SetGlobalFloat("_StadiumTime",.15f);
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                Texture2D first=Read(target);pixels.Add(first);Save(first,"v17-crowd-closeup-right-a.png");
                Shader.SetGlobalFloat("_StadiumTime",.53f);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                Texture2D second=Read(target);pixels.Add(second);Save(second,"v17-crowd-closeup-right-b.png");
                report.changingPixels=Difference(first,second,30);
                Check(report.changingPixels>2000,"A fixed-camera native stadium closeup visibly changes more than2000 pixels as articulated gestures advance");
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                Texture2D held=Read(target);pixels.Add(held);report.pausedPixels=Difference(second,held,10);
                Check(report.pausedPixels<30,"An unchanged shader clock holds the exact same rendered crowd pose");
                Shader.SetGlobalVector("_CrowdThrowOrigin",new Vector4(origin.x+crowdX,origin.y+2.15f,origin.z+personZ,1));Shader.SetGlobalFloat("_CrowdThrowAge",.22f);
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                Texture2D throwing=Read(target);pixels.Add(throwing);Save(throwing,"v17-crowd-throw-windup.png");report.throwPixels=Difference(held,throwing,20);
                Check(report.throwPixels>30&&report.throwPixels<50000,"One nearby front-row fan visibly winds up for the synchronized bottle throw, without changing the whole stand");
                Shader.SetGlobalFloat("_CrowdThrowAge",-1);
                camera.transform.position=origin+new Vector3(-crowdX+5.5f,3.4f,personZ+3);camera.transform.LookAt(origin+new Vector3(-crowdX-1,1.9f,personZ));
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                Texture2D left=Read(target);pixels.Add(left);Save(left,"v17-crowd-closeup-left.png");
                // Actual LOD selection uses the real game lens. Only the selected
                // crowd can produce white/red pixels, unlike Renderer.enabled.
                foreach(var r in visual.GetComponentsInChildren<Renderer>(true))r.enabled=false;
                var nr=visual.transform.Find("Animated supporters and flags").GetComponent<MeshRenderer>();var fr=visual.transform.Find("Distant supporters").GetComponent<MeshRenderer>();nr.enabled=fr.enabled=true;
                white=new Material(Shader.Find("Universal Render Pipeline/Unlit"));white.SetColor("_BaseColor",Color.white);
                red=new Material(Shader.Find("Universal Render Pipeline/Unlit"));red.SetColor("_BaseColor",Color.red);nr.sharedMaterial=white;fr.sharedMaterial=red;
                // Match the production crowd's double-sided rendering. Fog is
                // excluded from this diagnostic so it cannot recolour LOD IDs.
                white.SetFloat("_Cull",0);red.SetFloat("_Cull",0);RenderSettings.fog=false;
                instanceLod.ForceLOD(-1);
                camera.orthographic=false;camera.fieldOfView=Camera.main.fieldOfView;camera.aspect=Camera.main.aspect;camera.farClipPlane=300;
                camera.transform.SetPositionAndRotation(origin+Camera.main.transform.position,Camera.main.transform.rotation);
                camera.backgroundColor=Color.black;
                visual.transform.position=origin;
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();Texture2D closeLod=Read(target);pixels.Add(closeLod);
                report.nearLodPixels=closeLod.GetPixels32().Count(c=>c.r>200&&c.g>200&&c.b>200);
                report.nearRedPixels=closeLod.GetPixels32().Count(c=>c.r>200&&c.g<40&&c.b<40);Save(closeLod,"v17-crowd-lod-near.png");
                visual.transform.position=origin+Vector3.forward*120;
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();Texture2D distantLod=Read(target);pixels.Add(distantLod);
                report.farLodPixels=distantLod.GetPixels32().Count(c=>c.r>200&&c.g<40&&c.b<40);
                report.farWhitePixels=distantLod.GetPixels32().Count(c=>c.r>200&&c.g>200&&c.b>200);Save(distantLod,"v17-crowd-lod-far.png");
                Check(report.nearLodPixels>100&&report.nearRedPixels<10,"The actual game lens selects only the detailed crowd nearby");
                Check(report.farLodPixels>10&&report.farWhitePixels<10,"The actual game lens selects only the lighter crowd120m ahead at the current quality LOD bias");
            }
            finally
            {
                for(int i=0;i<originals.Length;i++)if(originals[i]!=null)originals[i].cullingMask=masks[i];
                RenderSettings.fog=oldFog;
                Shader.SetGlobalFloat("_StadiumTime",oldTime);Shader.SetGlobalFloat("_CrowdEnergy",oldEnergy);Shader.SetGlobalFloat("_CrowdThrowAge",oldAge);Shader.SetGlobalVector("_CrowdThrowOrigin",oldOrigin);
                if(cameraHost!=null){cameraHost.GetComponent<Camera>().enabled=false;cameraHost.GetComponent<Camera>().targetTexture=null;Destroy(cameraHost);}
                if(visual!=null)Destroy(visual);if(keyHost!=null)Destroy(keyHost);if(target!=null){target.Release();Destroy(target);}
                foreach(var p in pixels)Destroy(p);if(white!=null)Destroy(white);if(red!=null)Destroy(red);
            }
            game.enabled=true;game.Pause();float frozen=Shader.GetGlobalFloat("_StadiumTime");
            yield return new WaitForSecondsRealtime(.16f);
            Check(Shader.GetGlobalFloat("_StadiumTime")==frozen,"Production pause freezes the crowd clock and all shader-driven gestures");
            game.Resume();yield return new WaitForSecondsRealtime(.10f);
            Check(Shader.GetGlobalFloat("_StadiumTime")>frozen,"Production resume advances the crowd clock again");
            game.enabled=false;
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z);
        static bool Finite(Vector4 p)=>Finite(new Vector3(p.x,p.y,p.z))&&!float.IsNaN(p.w)&&!float.IsInfinity(p.w);
        static Texture2D Read(RenderTexture target)
        {var old=RenderTexture.active;RenderTexture.active=target;var t=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,target.width,target.height),0,0);t.Apply();RenderTexture.active=old;return t;}
        static int Difference(Texture2D a,Texture2D b,int threshold)
        {var x=a.GetPixels32();var y=b.GetPixels32();int changed=0;for(int i=0;i<x.Length;i++)if(Mathf.Abs(x[i].r-y[i].r)+Mathf.Abs(x[i].g-y[i].g)+Mathf.Abs(x[i].b-y[i].b)>threshold)changed++;return changed;}
        void Save(Texture2D image,string name){File.WriteAllBytes(Evidence(name),image.EncodeToPNG());captures.Add(Path.GetFileName(Evidence(name)));}
        static string Evidence(string name){string path=ReviewEvidence.FileFor(17,"../Validation/"+name);Directory.CreateDirectory(Path.GetDirectoryName(path));return path;}
        void Check(bool ok,string text){if(ok)checks.Add(text);else errors.Add(text);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;if(!prepared)return;if(wallet!=null)wallet.DeleteIsolatedTestData();Application.runInBackground=background;Application.targetFrameRate=rate;Time.captureDeltaTime=capture;Time.timeScale=scale;}
    }
}
#endif
