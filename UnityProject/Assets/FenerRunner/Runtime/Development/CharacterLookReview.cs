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
    public sealed class CharacterLookReview:MonoBehaviour
    {
        const string Flag="FenerRunner.CharacterLookReview";
        [Serializable] class Report {public bool passed;public string utc,phase;public string[] checks,errors,images;public int[] vertices,triangles;public string scope="Native Unity rendering of the actual game and three current game prefabs, with identical camera framing and daylight in before/after portraits. No image-generation or retouching. No physical-phone performance claim.";}
        readonly List<string> checks=new List<string>(),errors=new List<string>(),images=new List<string>();
        RunnerGame game;Camera cameraRig;RenderTexture previousTarget;bool background;string phase;int evidenceVersion;
        public static void StartChecks(string mode)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first");
            SessionState.SetString(Flag,mode);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||SessionState.GetString(Flag,"")=="")return;
            new GameObject("Native character look review").AddComponent<CharacterLookReview>();
        }
        IEnumerator Start()
        {
            phase=SessionState.GetString(Flag,"");SessionState.EraseString(Flag);evidenceVersion=Mathf.Max(10,ReviewEvidence.Version);
            background=Application.runInBackground;Application.runInBackground=true;Application.logMessageReceived+=Log;
            game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;game.ReturnToMenu();
            int balance=game.WalletBalance;var counts=new List<int>();var triangleCounts=new List<int>();
            Directory.CreateDirectory("../Validation");yield return new WaitForSecondsRealtime(.8f);
            yield return new WaitForEndOfFrame();
            string menu="v"+evidenceVersion+"-characters-"+phase+"-menu.png";ScreenCapture.CaptureScreenshot(Path.GetFullPath("../Validation/"+menu));images.Add(menu);yield return null;
            game.enabled=false;game.Interface.gameObject.SetActive(false);game.Motor.gameObject.SetActive(false);
            game.Obstacles.SetActive(false);game.Tokens.Enabled=false;game.Powerups.Enabled=false;
            FindFirstObjectByType<FollowCamera>().enabled=false;game.GetComponent<StadiumWeather>().Seek(0);
            game.GetComponent<StadiumWeather>().enabled=false;
            cameraRig=Camera.main;previousTarget=cameraRig.targetTexture;cameraRig.fieldOfView=38;
            const int width=720,height=960;
            var target=new RenderTexture(width,height,24){antiAliasing=4};target.Create();cameraRig.targetTexture=target;
            var frame=new Texture2D(width,height,TextureFormat.RGB24,false);var sheet=new Texture2D(width*3,height,TextureFormat.RGB24,false);
            string[] prefabs={"KartalPlayer","Engel_Atlama_SiyahKus_v01","Engel_Kayma_KivircikKus_v01"};
            for(int index=0;index<3;index++)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FenerRunner/Generated/Prefabs/"+prefabs[index]+".prefab");
                var actor=Instantiate(prefab);actor.name="Review "+prefabs[index];actor.transform.SetPositionAndRotation(new Vector3(0,0,8),Quaternion.Euler(0,phase=="showcase"?168:-12,0));
                foreach(var behaviour in actor.GetComponents<MonoBehaviour>())behaviour.enabled=false;
                var animator=actor.GetComponentInChildren<Animator>();if(animator!=null){animator.enabled=false;AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/FenerRunner/Generated/Animations/Idle.anim").SampleAnimation(animator.gameObject,0);}
                Bounds bounds=new Bounds();bool first=true;int vertexCount=0,triangleCount=0;
                foreach(var renderer in actor.GetComponentsInChildren<Renderer>())
                {
                    Mesh mesh;bool baked=renderer is SkinnedMeshRenderer;
                    if(renderer is SkinnedMeshRenderer skin){mesh=new Mesh();skin.BakeMesh(mesh);}else mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                    foreach(var vertex in mesh.vertices){Vector3 p=renderer.transform.TransformPoint(vertex);if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
                    vertexCount+=mesh.vertexCount;triangleCount+=mesh.triangles.Length/3;if(baked)Destroy(mesh);
                    Check(renderer.sharedMaterials.All(m=>m!=null&&m.shader.isSupported),prefabs[index]+" has supported material shaders");
                    if(phase!="before")Check(renderer.sharedMaterials.All(m=>m.shader.name=="FenerRunner/CharacterSoftLit"),prefabs[index]+" uses finished character surfaces");
                }
                counts.Add(vertexCount);triangleCounts.Add(triangleCount);
                Vector3 aim=bounds.center;float distance=Mathf.Max(bounds.size.y*.5f/Mathf.Tan(19*Mathf.Deg2Rad),bounds.size.x*.5f/(.75f*Mathf.Tan(19*Mathf.Deg2Rad)))*1.18f;
                cameraRig.transform.position=aim+new Vector3(.06f*distance,.04f*distance,phase=="showcase"?-distance:distance);cameraRig.transform.LookAt(aim);
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                var active=RenderTexture.active;RenderTexture.active=target;frame.ReadPixels(new Rect(0,0,width,height),0,0);frame.Apply();RenderTexture.active=active;
                sheet.SetPixels(index*width,0,width,height,frame.GetPixels());
                string file="v"+evidenceVersion+"-"+prefabs[index]+"-"+phase+".png";File.WriteAllBytes("../Validation/"+file,frame.EncodeToPNG());images.Add(file);
                Destroy(actor);yield return null;
            }
            sheet.Apply();string group="v"+evidenceVersion+"-characters-"+phase+".png";File.WriteAllBytes("../Validation/"+group,sheet.EncodeToPNG());images.Add(group);
            cameraRig.targetTexture=previousTarget;target.Release();Destroy(target);Destroy(frame);Destroy(sheet);
            Check(game.WalletBalance==balance,"Character review leaves production currency unchanged");
            var report=new Report{passed=errors.Count==0,utc=DateTime.UtcNow.ToString("O"),phase=phase,checks=checks.ToArray(),errors=errors.ToArray(),images=images.ToArray(),vertices=counts.ToArray(),triangles=triangleCounts.ToArray()};
            File.WriteAllText("../Validation/unity_character_look_"+phase+"_v"+evidenceVersion+".json",JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Character surfaces "+phase+" "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void Check(bool ok,string label){checks.Add((ok?"PASS ":"FAIL ")+label);if(!ok)errors.Add(label);}
        void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text);}
        void OnDestroy(){Application.logMessageReceived-=Log;Application.runInBackground=background;if(cameraRig!=null)cameraRig.targetTexture=previousTarget;}
    }
}
#endif
