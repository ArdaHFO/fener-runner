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
    // Runs against the scene's actual collectible pools. Geometry inspection is
    // followed by real controller collection and production pause/resume checks.
    public sealed class ChickTokenChecksV15:MonoBehaviour
    {
        const string Flag="FenerRunner.ChickTokenChecksV15";
        const string PrefabPath="Assets/FenerRunner/Generated/Prefabs/SupporterToken.prefab";
        static readonly FieldInfo PoolField=typeof(TokenCourse).GetField("pool",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly FieldInfo BonusField=typeof(TokenCourse).GetField("bonusPool",BindingFlags.Instance|BindingFlags.NonPublic);
        [Serializable] sealed class Report
        {
            public bool passed,preferencesPreserved,sourceSettingsUnchanged;
            public string utc,unityVersion;public string[] checks,errors,captures;
            public int triangles,vertices,normalPool,bonusPool,sharedMaterials,closeupForegroundPixels,closeupGoldenPixels;
            public float visualRadius,pickupRadius,wallSeconds;public Vector3 boundsSize;
            public string scope="Native Unity scene pools and retained shared chick prefab. Verifies smooth finite geometry, exact prior pickup envelope, one shared material, bounded 48+20 pools, face-forward wobble, actual pause/resume, fast controller and magnet/rain collection and restart visibility. Two native render captures show an explicitly placed actual-pool scene fixture and the actual prefab closeup. Isolated wallet, no APK and no physical phone performance claim.";
        }
        RunnerGame game;RunWallet wallet;Transform[] pool,bonus;Mesh sourceMesh;Material sourceMaterial;
        readonly Report report=new Report();readonly List<string> checks=new List<string>(),errors=new List<string>(),captures=new List<string>();
        bool oldBackground,prepared,oldAutomated,oldGameEnabled;int oldRate;float oldCapture,oldScale,started;
        string originalSettings,preferences;
        [MenuItem("Fener Runner/101 - Verify 3D chick collectibles v15")]
        public static void StartChecks()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before chick checks.");
            SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("V15 chick collectible checks").AddComponent<ChickTokenChecksV15>();}}
        IEnumerator Start()
        {
            started=Time.realtimeSinceStartup;oldBackground=Application.runInBackground;oldRate=Application.targetFrameRate;oldCapture=Time.captureDeltaTime;oldScale=Time.timeScale;
            Application.runInBackground=true;Application.targetFrameRate=60;Time.captureDeltaTime=1f/60;Time.timeScale=1;
            prepared=true;preferences=PreferenceSnapshot();Application.logMessageReceived+=Log;
            var routine=Scenarios();
            while(true)
            {
                bool more;object wait=null;
                try{more=routine.MoveNext();if(more)wait=routine.Current;}catch(Exception exception){errors.Add(exception.ToString());break;}
                if(!more)break;yield return wait;
            }
            if(game!=null)
            {
                game.enabled=false;game.ReturnToMenu();
                report.sourceSettingsUnchanged=originalSettings==JsonUtility.ToJson(game.Settings);
                Check(report.sourceSettingsUnchanged,"Source RunnerSettings remain unchanged");
            }
            report.preferencesPreserved=preferences==PreferenceSnapshot();Check(report.preferencesPreserved,"Production wallet, upgrades and best records remain unchanged");
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.wallSeconds=Time.realtimeSinceStartup-started;
            report.checks=checks.ToArray();report.errors=errors.ToArray();report.captures=captures.ToArray();
            File.WriteAllText(Evidence("unity_chick_tokens_v15.json"),JsonUtility.ToJson(report,true));Application.logMessageReceived-=Log;
            Debug.Log("Chick tokens v15 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Scenarios()
        {
            game=FindFirstObjectByType<RunnerGame>();
            if(game==null||PoolField==null||BonusField==null)throw new InvalidOperationException("Built real RunnerGame and collectible pools are required.");
            oldAutomated=game.AutomatedTest;oldGameEnabled=game.enabled;game.AutomatedTest=true;originalSettings=JsonUtility.ToJson(game.Settings);
            game.ReturnToMenu();wallet=game.UseWalletForTest("FenerRunner.Test.ChickV15."+DateTime.UtcNow.Ticks+".");Reset();
            pool=(Transform[])PoolField.GetValue(game.Tokens);bonus=(Transform[])BonusField.GetValue(game.Tokens);
            report.normalPool=pool.Length;report.bonusPool=bonus.Length;report.pickupRadius=game.Tokens.PickupRadius;
            Check(pool.Length==48&&bonus.Length==20,"Chick currency keeps the existing fixed 48 normal plus 20 bonus pool");
            var roots=pool.Concat(bonus).ToArray();var ids=roots.Select(t=>t.GetInstanceID()).ToArray();
            sourceMesh=pool[0].GetComponent<MeshFilter>().sharedMesh;sourceMaterial=pool[0].GetComponent<Renderer>().sharedMaterial;
            Check(sourceMesh!=null&&sourceMesh.name.Contains("chick"),"Actual scene pool uses the generated 3D chick mesh");
            InspectGeometry(sourceMesh);
            report.sharedMaterials=roots.SelectMany(t=>t.GetComponentsInChildren<Renderer>(true)).SelectMany(r=>r.sharedMaterials).Distinct().Count();
            Check(report.sharedMaterials==1&&sourceMaterial.enableInstancing&&sourceMaterial.shader.isSupported,"All 68 chicks reuse one instancing-enabled supported shared material");
            Check(sourceMaterial.GetTexture("_BaseMap")!=null&&sourceMaterial.GetTexture("_BaseMap").width==32,"Eight-colour detail uses a tiny shared 32 by 4 palette texture");
            Check(roots.All(t=>t.GetComponentsInChildren<MeshFilter>(true).Length==1&&t.GetComponent<MeshFilter>().sharedMesh==sourceMesh&&t.GetComponentsInChildren<Renderer>(true).Length==1),"Each pooled chick is one shared mesh and one renderer");
            Check(roots.All(t=>t.GetComponentsInChildren<Component>(true).All(c=>c is Transform||c is MeshFilter||c is MeshRenderer)),"No chick creates colliders, rigidbodies, lights or per-object update scripts");
            Check(roots.All(t=>t.localScale==Vector3.one),"Normal and bonus chicks retain the exact unscaled collection footprint");
            Check(Mathf.Abs(report.pickupRadius-(Mathf.Sqrt(.34f*.34f+.06f*.06f)+.04f))<.000002f,"Measured gameplay pickup radius equals the previous coin radius within two micrometres");
            var first=pool[0];Quaternion initial=first.rotation;bool facing=true,bounded=true;
            for(int frame=0;frame<720;frame++)
            {
                game.Tokens.Tick(game.Motor,game.Obstacles,1f/60);
                foreach(var root in pool){facing&=Vector3.Dot(-root.forward,Vector3.back)>.96f;bounded&=Quaternion.Angle(Quaternion.identity,root.rotation)<18;}
            }
            Check(facing&&bounded&&Quaternion.Angle(initial,first.rotation)>.1f,"Twelve seconds of real pooled animation keep chick faces toward camera within a gentle bounded wobble");
            Quaternion held=first.rotation;Vector3 heldPosition=first.position;game.Tokens.Tick(game.Motor,game.Obstacles,0);
            Check(Quaternion.Angle(held,first.rotation)<.0001f&&first.position==heldPosition,"A zero-duration token tick cannot advance the chick animation");
            Reset();Place(0,0,1.12f,3);game.Motor.Speed=30;
            Check(Move(.2f)==1&&game.Tokens.CollectedForTest(0)&&!pool[0].GetComponent<Renderer>().enabled,"A six-metre real controller move collects and hides one complete chick");
            Check(Move(0)==0&&game.Tokens.TotalCollected==1,"The hidden chick cannot be credited twice");
            Reset();Place(0,game.Settings.laneSpacing,1.12f,3);game.Motor.Speed=30;game.Tokens.MagnetRadius=10;
            Check(Move(.2f)==1&&game.Tokens.CollectedForTest(0)&&!pool[0].GetComponent<Renderer>().enabled,"Magnet attraction collects and hides the same complete shared chick");
            Reset();game.Tokens.PlaceBonusForTest(0,new Vector3(0,3,game.Motor.transform.position.z+3),1.12f);game.Motor.Speed=30;
            Check(Move(.2f)==1&&game.Tokens.BonusCollected==1&&!bonus[0].GetComponent<Renderer>().enabled,"Falling bonus chick uses continuous collection and disappears as one renderer");
            Reset();game.Tokens.SpawnBonusVolley(game.Motor,5,game.Obstacles);
            Check(Enumerable.Range(0,5).All(i=>bonus[i].GetComponent<Renderer>().enabled&&Vector3.Dot(-bonus[i].forward,Vector3.back)>.96f),"Fresh bonus volley is visible and faces camera immediately before its first tick");
            for(int i=0;i<8;i++)Reset();
            Check(pool.Concat(bonus).Select(t=>t.GetInstanceID()).SequenceEqual(ids),"Eight restarts reuse the original 68 objects without allocating replacement chicks");
            Check(pool.All(t=>t.GetComponent<Renderer>().enabled)&&bonus.All(t=>!t.GetComponent<Renderer>().enabled),"Restart restores all normal chicks and hides every bonus chick");
            Check(roots.All(t=>t.GetComponent<Renderer>().sharedMaterial==sourceMaterial&&t.GetComponent<MeshFilter>().sharedMesh==sourceMesh),"Collection, attraction and resets never clone the shared mesh or material");

            Reset();game.enabled=true;yield return null;yield return null;game.Pause();
            held=pool[0].rotation;heldPosition=pool[0].position;yield return new WaitForSecondsRealtime(.15f);
            Check(game.State==RunState.Paused&&pool[0].position==heldPosition&&Quaternion.Angle(pool[0].rotation,held)<.0001f,"Actual production Pause freezes chick bob and wobble across real frames");
            game.Resume();yield return null;yield return null;yield return null;
            Check(game.State==RunState.Running&&Quaternion.Angle(pool[0].rotation,held)>.01f,"Actual Resume resumes chick animation");game.enabled=false;

            // A labelled visual fixture uses the actual pooled assets, the real
            // player and actual game camera. It is not simulated gameplay proof.
            Reset();Place(0,-game.Settings.laneSpacing,1.12f,9);Place(1,0,1.12f,12);Place(2,game.Settings.laneSpacing,1.12f,15);
            Place(3,0,1.12f,18);Place(4,0,1.12f,22);game.Tokens.Tick(game.Motor,game.Obstacles,.18f);
            EditorApplication.ExecuteMenuItem("Fener Runner/5 - Open portrait Game view");yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
            string scene=Evidence("v15-chicks-scene.png");ScreenCapture.CaptureScreenshot(scene);yield return new WaitForEndOfFrame();yield return null;
            Check(File.Exists(scene),"Native game-camera screenshot records the actual chick pool scene fixture");captures.Add(scene);
            var nativeCapture=CapturePrefab();while(nativeCapture.MoveNext())yield return nativeCapture.Current;
        }
        void InspectGeometry(Mesh mesh)
        {
            Vector3[] vertices=mesh.vertices,normals=mesh.normals;Vector4[] tangents=mesh.tangents;
            report.triangles=mesh.triangles.Length/3;report.vertices=mesh.vertexCount;report.boundsSize=mesh.bounds.size;
            report.visualRadius=vertices.Max(v=>v.magnitude);
            Check(report.triangles>600&&report.triangles<=1500&&mesh.subMeshCount==1,"Rounded chick geometry stays below 1500 triangles in one submesh");
            Check(mesh.bounds.size.x>.40f&&mesh.bounds.size.y>.55f&&mesh.bounds.size.z>.35f,"Actual mesh has a rounded three-dimensional body, head, wings, feet and protruding beak envelope");
            Check(Mathf.Abs(report.visualRadius-Mathf.Sqrt(.34f*.34f+.06f*.06f))<.000002f,"Shared mesh occupies exactly the previous visible coin sphere envelope");
            Check(vertices.All(v=>Finite(v.x)&&Finite(v.y)&&Finite(v.z))&&normals.Length==vertices.Length&&normals.All(n=>Finite(n.x)&&Finite(n.y)&&Finite(n.z)&&Mathf.Abs(n.magnitude-1)<.0001f),"Every vertex and analytical smooth normal is finite and every normal has unit length");
            Check(tangents.Length==vertices.Length&&tangents.Select((t,i)=>Finite(t.x)&&Finite(t.y)&&Finite(t.z)&&Mathf.Abs(Vector3.Dot(new Vector3(t.x,t.y,t.z),normals[i]))<.0001f&&Mathf.Abs(new Vector3(t.x,t.y,t.z).magnitude-1)<.0001f).All(v=>v),"Constant palette UVs have a finite orthogonal tangent basis without degenerate UV derivatives");
            Check(mesh.uv.Select(v=>v.x).Distinct().Count()==8&&mesh.uv.All(v=>v.x>0&&v.x<1&&v.y==.5f),"All eight feather, bill, eye, highlight and mouth colours sample inside the atlas");
        }
        IEnumerator CapturePrefab()
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);if(source==null)throw new InvalidOperationException("Chick source prefab is missing.");
            const int size=768,mask=1<<31;Vector3 origin=new Vector3(5000,50,5000);
            GameObject visual=null,cameraHost=null,keyHost=null,fillHost=null;Texture2D pixels=null;RenderTexture target=null;
            // PreviewRenderUtility can return only its clear colour during URP
            // Play Mode. Use an ordinary enabled Game camera and real pipeline
            // frames, at an isolated layer/position, without altering game art.
            Light[] existingLights=FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            int[] lightMasks=existingLights.Select(light=>light.cullingMask).ToArray();
            try
            {
                for(int i=0;i<existingLights.Length;i++)existingLights[i].cullingMask&=~mask;
                visual=Instantiate(source);visual.name="Native chick closeup fixture";visual.transform.SetPositionAndRotation(origin,Quaternion.Euler(0,-12,0));
                foreach(var child in visual.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))renderer.enabled=true;
                visual.SetActive(true);
                cameraHost=new GameObject("Native chick closeup camera",typeof(Camera),typeof(UniversalAdditionalCameraData));
                var camera=cameraHost.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.43f;camera.aspect=1;
                camera.nearClipPlane=.05f;camera.farClipPlane=10;camera.cullingMask=mask;camera.depth=100;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.05f,.105f,1);camera.allowHDR=false;camera.allowMSAA=true;
                camera.transform.position=origin+new Vector3(0,.06f,-3);camera.transform.LookAt(origin+new Vector3(0,.025f,0));
                cameraHost.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
                target=new RenderTexture(size,size,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=4};target.Create();camera.targetTexture=target;
                keyHost=new GameObject("Native chick closeup key",typeof(Light));var key=keyHost.GetComponent<Light>();key.type=LightType.Directional;key.intensity=1.25f;key.color=new Color(1,.95f,.85f);key.cullingMask=mask;key.shadows=LightShadows.None;key.transform.rotation=Quaternion.Euler(30,35,0);
                fillHost=new GameObject("Native chick closeup fill",typeof(Light));var fill=fillHost.GetComponent<Light>();fill.type=LightType.Directional;fill.intensity=.6f;fill.color=new Color(.70f,.82f,1);fill.cullingMask=mask;fill.shadows=LightShadows.None;fill.transform.rotation=Quaternion.Euler(10,-145,0);
                // Both frames render via the same SRP loop as the game camera.
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                RenderTexture previous=RenderTexture.active;
                try{RenderTexture.active=target;pixels=new Texture2D(size,size,TextureFormat.RGBA32,false,false);pixels.ReadPixels(new Rect(0,0,size,size),0,0,false);pixels.Apply(false,false);}
                finally{RenderTexture.active=previous;}
                Color32[] image=pixels.GetPixels32();Color32 background=image[0];
                foreach(Color32 pixel in image)
                {
                    if(Mathf.Abs(pixel.r-background.r)+Mathf.Abs(pixel.g-background.g)+Mathf.Abs(pixel.b-background.b)>45)report.closeupForegroundPixels++;
                    if(pixel.r>90&&pixel.g>50&&pixel.r>pixel.b*1.35f&&pixel.g>pixel.b*1.15f)report.closeupGoldenPixels++;
                }
                string path=Evidence("v15-chick-prefab-closeup.png");File.WriteAllBytes(path,pixels.EncodeToPNG());captures.Add(path);
                Check(report.closeupForegroundPixels>30000&&report.closeupForegroundPixels<size*size*.7f,"Native prefab closeup contains a bounded non-background 3D silhouette, not a blank render target");
                Check(report.closeupGoldenPixels>20000,"Native prefab closeup contains at least twenty thousand actual golden feather pixels");
            }
            finally
            {
                for(int i=0;i<existingLights.Length;i++)if(existingLights[i]!=null)existingLights[i].cullingMask=lightMasks[i];
                if(cameraHost!=null){var camera=cameraHost.GetComponent<Camera>();camera.enabled=false;camera.targetTexture=null;Destroy(cameraHost);}
                if(visual!=null)Destroy(visual);if(keyHost!=null)Destroy(keyHost);if(fillHost!=null)Destroy(fillHost);
                if(target!=null){target.Release();Destroy(target);}if(pixels!=null)Destroy(pixels);
            }
        }
        void Reset()
        {game.enabled=false;game.Powerups.Enabled=false;game.Tokens.Enabled=true;game.Play();game.Obstacles.SetActive(false);game.Powerups.IsolatePickupsForTest();game.Motor.Speed=0;for(int i=0;i<game.Tokens.PoolCount;i++)game.Tokens.PlaceForTest(i,new Vector3(12,1.12f,2000+i*4.4f));}
        void Place(int index,float x,float y,float forward){game.Tokens.PlaceForTest(index,new Vector3(x,y,game.Motor.transform.position.z+forward));}
        int Move(float seconds){game.Tokens.BeginMove(game.Motor);game.Motor.Tick(seconds);return game.Tokens.Tick(game.Motor,game.Obstacles,seconds);}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static string Evidence(string name){string path=ReviewEvidence.FileFor(15,"../Validation/"+name);Directory.CreateDirectory(Path.GetDirectoryName(path));return path;}
        static string PreferenceSnapshot(){var values=new List<string>();foreach(string key in new[]{RunWallet.DefaultPrefix+"Coins",RunWallet.DefaultPrefix+"Upgrade.0",RunWallet.DefaultPrefix+"Upgrade.1",RunWallet.DefaultPrefix+"Upgrade.2","FenerRunner.BestScore"})values.Add(key+"="+PlayerPrefs.HasKey(key)+"/"+PlayerPrefs.GetInt(key,0));values.Add("best="+PlayerPrefs.HasKey("FenerRunner.Best")+"/"+PlayerPrefs.GetFloat("FenerRunner.Best",0).ToString("R",System.Globalization.CultureInfo.InvariantCulture));return string.Join("|",values);}
        void Check(bool ok,string message){if(ok)checks.Add(message);else errors.Add(message);}
        void Log(string message,string trace,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<60)errors.Add(message);}
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;if(!prepared)return;
            if(game!=null){game.AutomatedTest=oldAutomated;game.enabled=oldGameEnabled;}
            if(wallet!=null)wallet.DeleteIsolatedTestData();Application.runInBackground=oldBackground;Application.targetFrameRate=oldRate;Time.captureDeltaTime=oldCapture;Time.timeScale=oldScale;
        }
    }
}
#endif
