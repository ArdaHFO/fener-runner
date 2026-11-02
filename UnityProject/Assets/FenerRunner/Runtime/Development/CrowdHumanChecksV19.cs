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
    // Measure final generated vertices, then render those same people in URP.
    // No alternate test mannequin or replacement skin is used by these captures.
    public sealed class CrowdHumanChecksV19:MonoBehaviour
    {
        const string Flag="FenerRunner.CrowdHumanV19";
        [Serializable] sealed class Report
        {
            public bool passed;public string utc,unityVersion;public string[] checks,errors,captures;
            public int people,nearTriangles,farTriangles;
            public float minimumBodyToHead,maximumBodyToHead,minimumLegFraction,maximumLegFraction;
            public int frontPixels,sidePixels,backPixels;
            public string scope="Independent final generated mesh measurements for adult proportions, planted soles, connected limb rest endpoints and distinct real facial relief. Native URP front/side/back views use the actual crowd prefab/material with stadium architecture hidden solely to expose the whole human silhouette. Full stadium rendering, natural-motion and lifecycle regressions are separate stages. No physical-phone FPS claim.";
        }
        sealed class PersonBounds
        {
            public float minY=float.PositiveInfinity,maxY=float.NegativeInfinity,headMin=float.PositiveInfinity,headMax=float.NegativeInfinity,legMax=float.NegativeInfinity,soleMin=float.PositiveInfinity,soleMax=float.NegativeInfinity;
            public float headFront=float.NegativeInfinity,headBack=float.PositiveInfinity;
            public readonly HashSet<int> parts=new HashSet<int>();
            public readonly List<Vector3> leftHand=new List<Vector3>(),rightHand=new List<Vector3>();
        }
        readonly Report report=new Report();readonly List<string> checks=new List<string>(),errors=new List<string>(),captures=new List<string>();
        RunnerGame game;RunWallet wallet;bool prepared,background;float scale,capture;int rate;string settings;
        [MenuItem("Fener Runner/119 - Verify adult supporter geometry v19")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod]static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Adult supporter checks v19").AddComponent<CrowdHumanChecksV19>();}}
        IEnumerator Start()
        {
            scale=Time.timeScale;capture=Time.captureDeltaTime;background=Application.runInBackground;rate=Application.targetFrameRate;prepared=true;
            Application.runInBackground=true;Application.targetFrameRate=60;Time.captureDeltaTime=1f/60;Application.logMessageReceived+=Log;
            IEnumerator routine=Run();while(true){bool more;object wait=null;try{more=routine.MoveNext();if(more)wait=routine.Current;}catch(Exception e){errors.Add(e.ToString());break;}if(!more)break;yield return wait;}
            if(game!=null){game.ReturnToMenu();Check(settings==JsonUtility.ToJson(game.Settings),"Human verification preserves authored gameplay settings");}
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.checks=checks.ToArray();report.errors=errors.ToArray();report.captures=captures.ToArray();
            File.WriteAllText(Evidence("unity_crowd_human_v19.json"),JsonUtility.ToJson(report,true));Application.logMessageReceived-=Log;
            Debug.Log("Crowd human v19 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Run()
        {
            game=FindFirstObjectByType<RunnerGame>();if(game==null)throw new InvalidOperationException("Built game scene required.");
            game.AutomatedTest=true;game.enabled=false;settings=JsonUtility.ToJson(game.Settings);wallet=game.UseWalletForTest("FenerRunner.Test.HumanV19."+DateTime.UtcNow.Ticks+".");game.Play();game.Obstacles.SetActive(false);game.Tokens.Enabled=false;game.Powerups.Enabled=false;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FenerRunner/Generated/Prefabs/StadiumSegment.prefab");
            var near=prefab.transform.Find("Animated supporters and flags").GetComponent<MeshFilter>().sharedMesh;var far=prefab.transform.Find("Distant supporters").GetComponent<MeshFilter>().sharedMesh;
            report.nearTriangles=near.triangles.Length/3;report.farTriangles=far.triangles.Length/3;
            Check(report.nearTriangles<=180000&&report.farTriangles<=8000,"Measured shared geometry respects the explicit180k near/8k far mobile budget");
            var anchors=new List<Vector4>();var motion=new List<Vector4>();near.GetUVs(2,anchors);near.GetUVs(1,motion);Vector3[] vertices=near.vertices;
            var people=new Dictionary<Vector3,PersonBounds>();bool finite=true;
            for(int i=0;i<vertices.Length;i++)
            {
                if(motion[i].y>=8)continue;
                Vector4 a=anchors[i];Vector3 key=new Vector3(a.x,a.y,a.z);if(!people.TryGetValue(key,out var person)){person=new PersonBounds();people.Add(key,person);}
                float side=a.x<0?-1:1;Vector3 d=(vertices[i]-key)/a.w;Vector3 p=new Vector3(d.z*side,d.y,-d.x*side);int part=(int)motion[i].y;
                finite&=Finite(p.x)&&Finite(p.y)&&Finite(p.z);person.parts.Add(part);
                if(part!=6){person.minY=Mathf.Min(person.minY,p.y);person.maxY=Mathf.Max(person.maxY,p.y);}
                if(part==2){person.headMin=Mathf.Min(person.headMin,p.y);person.headMax=Mathf.Max(person.headMax,p.y);person.headFront=Mathf.Max(person.headFront,p.z);person.headBack=Mathf.Min(person.headBack,p.z);}
                if(part==0)person.legMax=Mathf.Max(person.legMax,p.y);
                if(part==7){person.soleMin=Mathf.Min(person.soleMin,p.y);person.soleMax=Mathf.Max(person.soleMax,p.y);}
                if(part==5)(motion[i].z<0?person.leftHand:person.rightHand).Add(p);
            }
            report.people=people.Count;report.minimumBodyToHead=float.PositiveInfinity;report.maximumBodyToHead=0;report.minimumLegFraction=float.PositiveInfinity;report.maximumLegFraction=0;
            bool anatomy=true,soles=true,limbs=true,relief=true;
            foreach(var person in people.Values)
            {
                float body=person.maxY-person.minY,head=person.headMax-person.headMin,ratio=body/head,leg=(person.legMax-person.minY)/body;
                report.minimumBodyToHead=Mathf.Min(report.minimumBodyToHead,ratio);report.maximumBodyToHead=Mathf.Max(report.maximumBodyToHead,ratio);report.minimumLegFraction=Mathf.Min(report.minimumLegFraction,leg);report.maximumLegFraction=Mathf.Max(report.maximumLegFraction,leg);
                anatomy&=body>1.63f&&body<1.73f&&ratio>5.8f&&ratio<7.4f&&leg>.44f&&leg<.53f;
                soles&=person.soleMin>=-.004f&&person.soleMin<.01f&&person.soleMax<.10f;
                limbs&=person.leftHand.Count>12&&person.rightHand.Count>12&&person.leftHand.Min(p=>p.y)<.64f&&person.rightHand.Min(p=>p.y)<.64f&&person.leftHand.Max(p=>p.y)<.75f&&person.rightHand.Max(p=>p.y)<.75f;
                relief&=person.headFront>.135f&&person.headBack<-.075f;
            }
            Check(finite&&people.Count==272,"All272 final generated people have finite independent whole-body measurements");
            Check(anatomy,"Actual body/head and leg/body dimensions form adult proportions instead of the previous toddler-like silhouette");
            Check(soles,"Every final trainer sole stays on the original standing surface");
            Check(limbs&&people.Values.All(p=>new[]{0,1,2,3,4,5,7}.All(p.parts.Contains)),"Both hands, thumbs and articulated limb groups are present at adult-length rest endpoints");
            Check(relief,"Every final human head has genuine front/back geometry depth including the projected nose");
            Check(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==0&&prefab.GetComponentsInChildren<Animator>(true).Length==0,"Added anatomy introduces no per-person Animator or skinned renderer");
            Time.timeScale=0;
            const int mask=1<<31,width=1280,height=960;Vector3 origin=new Vector3(0,50,0);
            var lights=FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None);int[] masks=lights.Select(l=>l.cullingMask).ToArray();
            string[] scalars={"_StadiumTime","_CrowdEnergy","_CrowdThrowAge","_CrowdWorldOffset"};float[] values=scalars.Select(Shader.GetGlobalFloat).ToArray();string[] vectors={"_CrowdThrowOrigin","_CrowdReactionOrigin","_CrowdReactionData","_CrowdPlayer"};Vector4[] vectorValues=vectors.Select(Shader.GetGlobalVector).ToArray();
            GameObject visual=null,cameraHost=null,lightHost=null;RenderTexture target=null;var textures=new List<Texture2D>();
            try
            {
                foreach(var light in lights)light.cullingMask&=~mask;visual=Instantiate(prefab);visual.transform.position=origin;
                foreach(Transform item in visual.GetComponentsInChildren<Transform>(true))item.gameObject.layer=31;
                foreach(var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
                visual.GetComponent<LODGroup>().ForceLOD(0);
                foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))if(renderer.name!="Animated supporters and flags"&&renderer.name!="Distant supporters")renderer.enabled=false;
                cameraHost=new GameObject("Native adult-human inspection camera",typeof(Camera),typeof(UniversalAdditionalCameraData));var camera=cameraHost.GetComponent<Camera>();
                camera.orthographic=true;camera.orthographicSize=1.30f;camera.aspect=width/(float)height;camera.cullingMask=mask;camera.depth=100;camera.nearClipPlane=.10f;camera.farClipPlane=80;camera.allowHDR=false;camera.allowMSAA=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.055f,.085f,1);
                target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
                lightHost=new GameObject("Human inspection key",typeof(Light));var key=lightHost.GetComponent<Light>();key.type=LightType.Directional;key.intensity=1.15f;key.cullingMask=mask;key.transform.rotation=Quaternion.Euler(36,-35,0);
                float crowdX=game.Settings.laneSpacing*1.5f+.7f+3.4f,personZ=.4f+8*1.16f;
                Shader.SetGlobalFloat("_StadiumTime",.82f);Shader.SetGlobalFloat("_CrowdEnergy",.15f);Shader.SetGlobalFloat("_CrowdThrowAge",-1);Shader.SetGlobalFloat("_CrowdWorldOffset",0);Shader.SetGlobalVector("_CrowdPlayer",Vector4.zero);Shader.SetGlobalVector("_CrowdReactionData",new Vector4(-1,0,0,10));
                Vector3 subject=origin+new Vector3(crowdX,.55f+.88f,personZ);
                camera.transform.position=subject+new Vector3(-4.5f,.10f,0);camera.transform.LookAt(subject);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();var front=Read(target);textures.Add(front);report.frontPixels=Visible(front);Save(front,"v19-human-front.png");
                camera.transform.position=subject+new Vector3(-3.6f,.12f,-3.1f);camera.transform.LookAt(subject);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();var sideView=Read(target);textures.Add(sideView);report.sidePixels=Visible(sideView);Save(sideView,"v19-human-three-quarter.png");
                // Inspect the actual rear contour from between the first two rows.
                camera.transform.position=subject+new Vector3(.65f,.08f,-3.2f);camera.transform.LookAt(subject);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();var back=Read(target);textures.Add(back);report.backPixels=Visible(back);Save(back,"v19-human-profile.png");
                Check(report.frontPixels>20000&&report.sidePixels>20000&&report.backPixels>20000,"Actual native crowd material renders substantial adult silhouettes in three independent lens positions");
                Check(Difference(front,sideView)>30000&&Difference(front,back)>30000,"Native turn-around views reveal three-dimensional heads, torsos and limbs rather than fixed camera cards");
            }
            finally
            {
                for(int i=0;i<lights.Length;i++)if(lights[i]!=null)lights[i].cullingMask=masks[i];for(int i=0;i<scalars.Length;i++)Shader.SetGlobalFloat(scalars[i],values[i]);for(int i=0;i<vectors.Length;i++)Shader.SetGlobalVector(vectors[i],vectorValues[i]);
                if(cameraHost!=null){cameraHost.GetComponent<Camera>().enabled=false;cameraHost.GetComponent<Camera>().targetTexture=null;Destroy(cameraHost);}if(visual!=null)Destroy(visual);if(lightHost!=null)Destroy(lightHost);if(target!=null){target.Release();Destroy(target);}foreach(var texture in textures)Destroy(texture);
            }
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static Texture2D Read(RenderTexture target){var prior=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();RenderTexture.active=prior;return image;}
        static int Visible(Texture2D image){var pixels=image.GetPixels32();Color32 background=pixels[0];int count=0;foreach(var pixel in pixels)if(Mathf.Abs(pixel.r-background.r)+Mathf.Abs(pixel.g-background.g)+Mathf.Abs(pixel.b-background.b)>45)count++;return count;}
        static int Difference(Texture2D a,Texture2D b){var x=a.GetPixels32();var y=b.GetPixels32();int count=0;for(int i=0;i<x.Length;i++)if(Mathf.Abs(x[i].r-y[i].r)+Mathf.Abs(x[i].g-y[i].g)+Mathf.Abs(x[i].b-y[i].b)>30)count++;return count;}
        void Save(Texture2D image,string name){File.WriteAllBytes(Evidence(name),image.EncodeToPNG());captures.Add(name);}
        static string Evidence(string name){string path=ReviewEvidence.FileFor(19,"../Validation/"+name);Directory.CreateDirectory(Path.GetDirectoryName(path));return path;}
        void Check(bool condition,string text){if(condition)checks.Add(text);else errors.Add(text);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;if(!prepared)return;if(wallet!=null)wallet.DeleteIsolatedTestData();Time.timeScale=scale;Time.captureDeltaTime=capture;Application.runInBackground=background;Application.targetFrameRate=rate;}
    }
}
#endif
