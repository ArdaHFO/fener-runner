#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class CrowdNaturalMotionChecksV19:MonoBehaviour
    {
        const string Flag="FenerRunner.CrowdNaturalMotionV19";
        [Serializable] sealed class Report
        {
            public bool passed;public string utc;public string[] checks,errors;
            public int people,gpuSamples,positiveControlRaised,restControlRaised,distinctOnsetFrames,throwHandsChecked;
            public float maxBothAboveShoulders,maxBothAboveHead,minimumUnaffected,averageRestFraction,maximumFootDrift,rebasePositionError,calibrationError,maximumThrowHandError;
            public string scope="Native GPU readback of the exact production supporter pose function through an explicitly selected diagnostic pass. Actual mesh hand/head/sole landmarks test simultaneous arm raising, personal timing, feet, lifecycle and origin continuity. A synthetic all-raised geometry control validates sampling and classification. No CPU duplicate of the animation and no physical-phone performance claim.";
        }
        sealed class Person
        {
            public Vector4 anchor,persona,motion;
            public readonly int[] indices={-1,-1,-1,-1,-1};
            public readonly float[] best={float.PositiveInfinity,float.PositiveInfinity,float.NegativeInfinity,float.PositiveInfinity,float.PositiveInfinity};
        }
        readonly Report report=new Report();readonly List<string> checks=new List<string>(),errors=new List<string>();
        RunnerGame game;RunWallet wallet;bool prepared,oldBackground;float oldScale,oldCapture;int oldRate;
        Mesh probes,calibration;Material material;RenderTexture target;Texture2D pixels;
        readonly List<Person> people=new List<Person>();
        [MenuItem("Fener Runner/121 - Verify individual crowd movement v19")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod]static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Native crowd pose checks v19").AddComponent<CrowdNaturalMotionChecksV19>();}}
        IEnumerator Start()
        {
            prepared=true;oldBackground=Application.runInBackground;oldScale=Time.timeScale;oldCapture=Time.captureDeltaTime;oldRate=Application.targetFrameRate;
            Application.runInBackground=true;Application.targetFrameRate=60;Time.captureDeltaTime=1f/60;Application.logMessageReceived+=Log;
            var run=Run();while(true){bool more;object value=null;try{more=run.MoveNext();if(more)value=run.Current;}catch(Exception e){errors.Add(e.ToString());break;}if(!more)break;yield return value;}
            if(game!=null){game.enabled=false;game.ReturnToMenu();}
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.checks=checks.ToArray();report.errors=errors.ToArray();
            File.WriteAllText(ReviewEvidence.FileFor(19,"../Validation/unity_crowd_natural_motion_v19.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Crowd natural motion v19 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Run()
        {
            game=FindFirstObjectByType<RunnerGame>();if(game==null)throw new InvalidOperationException("Actual game scene required.");
            game.AutomatedTest=true;game.enabled=false;wallet=game.UseWalletForTest("FenerRunner.Test.CrowdMotionV19."+DateTime.UtcNow.Ticks+".");game.Play();
            game.Obstacles.SetActive(false);game.Tokens.Enabled=false;game.Powerups.Enabled=false;Time.timeScale=0;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FenerRunner/Generated/Prefabs/StadiumSegment.prefab");
            var source=prefab.transform.Find("Animated supporters and flags").GetComponent<MeshFilter>().sharedMesh;
            material=new Material(prefab.transform.Find("Animated supporters and flags").GetComponent<Renderer>().sharedMaterial);
            Check(material.FindPass("CrowdPoseProbe")>=0,"Production crowd shader exposes an explicitly selected GPU pose probe pass outside the URP forward draw");
            BuildProbes(source);report.people=people.Count;Check(report.people==272,"Native landmarks cover every person on both stands");
            Check(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat),"Editor supports full-float pose readback");
            target=new RenderTexture(people.Count*5,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear){antiAliasing=1,filterMode=FilterMode.Point};target.Create();
            pixels=new Texture2D(target.width,1,TextureFormat.RGBAFloat,false,true);
            string[] names={"_StadiumTime","_CrowdEnergy","_CrowdThrowAge","_CrowdWorldOffset","_CrowdReactionSeed"};float[] saved=new float[names.Length];for(int i=0;i<names.Length;i++)saved[i]=Shader.GetGlobalFloat(names[i]);
            string[] vectors={"_CrowdReactionData","_CrowdReactionOrigin","_CrowdPlayer","_CrowdThrowOrigin"};Vector4[] savedVectors=new Vector4[vectors.Length];for(int i=0;i<vectors.Length;i++)savedVectors[i]=Shader.GetGlobalVector(vectors[i]);
            try
            {
                Shader.SetGlobalFloat("_CrowdThrowAge",-1);Shader.SetGlobalFloat("_CrowdWorldOffset",0);Shader.SetGlobalFloat("_CrowdEnergy",.5f);
                Shader.SetGlobalVector("_CrowdPlayer",Vector4.zero);Shader.SetGlobalVector("_CrowdReactionOrigin",new Vector4(0,.55f,20,0));
                Shader.SetGlobalVector("_CrowdReactionData",new Vector4(-1,0,0,200));Shader.SetGlobalFloat("_CrowdReactionSeed",0);
                var known=Read(calibration,Matrix4x4.identity);
                for(int i=0;i<people.Count;i++)
                {
                    report.calibrationError=Mathf.Max(report.calibrationError,Mathf.Abs(known[i*5].g-1.85f),Mathf.Abs(known[i*5+1].g-1.85f),Mathf.Abs(known[i*5+2].g-1.67f));
                    if(known[i*5].g>known[i*5+2].g&&known[i*5+1].g>known[i*5+2].g)report.positiveControlRaised++;
                }
                Check(report.calibrationError<.0002f&&report.positiveControlRaised==people.Count,"Independent known-position all-raised control is read accurately and detected for every person");
                var restPositions=calibration.vertices;
                for(int i=0;i<people.Count;i++)for(int hand=0;hand<2;hand++)restPositions[i*5+hand].y=people[i].anchor.y+.7f*people[i].anchor.w;
                calibration.vertices=restPositions;var knownRest=Read(calibration,Matrix4x4.identity);
                for(int i=0;i<people.Count;i++)if(knownRest[i*5].g>knownRest[i*5+2].g&&knownRest[i*5+1].g>knownRest[i*5+2].g)report.restControlRaised++;
                Check(report.restControlRaised==0,"Independent known-position all-rest control detects no raised pairs of hands");
                Shader.SetGlobalFloat("_StadiumTime",.5f);var baseline=Read(probes,Matrix4x4.identity);var repeat=Read(probes,Matrix4x4.identity);
                Check(Difference(baseline,repeat)<.00001f,"The same frozen production shader clock returns identical GPU landmark positions");
                float rest=0;int moving=0;bool[] moved=new bool[people.Count];var onsetFrames=new HashSet<int>();int[] firstRaise=new int[people.Count];for(int i=0;i<firstRaise.Length;i++)firstRaise[i]=-1;
                for(int frame=0;frame<32;frame++)
                {
                    Shader.SetGlobalFloat("_StadiumTime",frame*1.13f);var pose=Read(probes,Matrix4x4.identity);int low=0;
                    for(int i=0;i<people.Count;i++)
                    {
                        int at=i*5;float shoulder=pose[at+2].g-.36f;
                        if(pose[at].g<shoulder&&pose[at+1].g<shoulder)low++;
                        else if(firstRaise[i]<0){firstRaise[i]=frame;onsetFrames.Add(frame);}
                        if(Vector3.Distance(ToVector(pose[at]),ToVector(baseline[at]))>.08f||Vector3.Distance(ToVector(pose[at+1]),ToVector(baseline[at+1]))>.08f)moved[i]=true;
                        report.maximumFootDrift=Mathf.Max(report.maximumFootDrift,Vector3.Distance(ToVector(pose[at+3]),ToVector(baseline[at+3])),Vector3.Distance(ToVector(pose[at+4]),ToVector(baseline[at+4])));
                    }
                    rest+=low/(float)people.Count;MeasureRaising(pose);
                    if(frame%8==0)yield return null;
                }
                foreach(bool value in moved)if(value)moving++;report.averageRestFraction=rest/32;report.distinctOnsetFrames=onsetFrames.Count;
                Check(report.averageRestFraction>.65f,"Most people naturally watch or rest below shoulder height between their private cheering bursts");
                Check(moving>people.Count*.85f&&report.distinctOnsetFrames>=8,"Individual hands visibly move across private cycles with at least eight separate first-raise times");
                Check(report.maximumFootDrift<.0001f,"Actual left and right sole landmarks stay planted throughout the personal movement cycles");
                report.minimumUnaffected=1;
                foreach(float clock in new[]{1.1f,12.7f})foreach(int kind in new[]{0,1,2,4,5,6})
                {
                    Shader.SetGlobalFloat("_StadiumTime",clock);Shader.SetGlobalFloat("_CrowdReactionSeed",3+kind);Shader.SetGlobalVector("_CrowdReactionData",new Vector4(-1,kind,0,200));
                    var idle=Read(probes,Matrix4x4.identity);bool[] responded=new bool[people.Count];
                    for(int frame=0;frame<15;frame++)
                    {
                        Shader.SetGlobalVector("_CrowdReactionData",new Vector4(frame*.25f,kind,1,200));var pose=Read(probes,Matrix4x4.identity);MeasureRaising(pose);
                        for(int i=0;i<people.Count;i++)
                        {int at=i*5;if(Vector3.Distance(ToVector(pose[at]),ToVector(idle[at]))>.025f||Vector3.Distance(ToVector(pose[at+1]),ToVector(idle[at+1]))>.025f)responded[i]=true;}
                    }
                    int responding=0;foreach(bool response in responded)if(response)responding++;
                    report.minimumUnaffected=Mathf.Min(report.minimumUnaffected,1-responding/(float)people.Count);
                    Check(responding>people.Count*.3f&&responding<people.Count*.8f,"Reaction family "+kind+" at clock "+clock+" reaches a subset instead of commanding every supporter");
                    Shader.SetGlobalVector("_CrowdReactionData",new Vector4(3.6f,kind,1,200));var expired=Read(probes,Matrix4x4.identity);
                    Check(Difference(idle,expired)<.00001f,"Reaction family "+kind+" returns to the original personal pose after its recovery");yield return null;
                }
                Check(report.maxBothAboveShoulders<.30f&&report.maxBothAboveHead<.18f,"Native GPU samples never lift both arms together for more than30% of fans, or above the head for more than18%");
                Check(report.minimumUnaffected>.2f,"Every event leaves at least20% of supporters in their own activity even with a deliberately oversized reaction radius");
                Shader.SetGlobalFloat("_StadiumTime",14.8f);Shader.SetGlobalFloat("_CrowdReactionSeed",9);Shader.SetGlobalVector("_CrowdReactionData",new Vector4(1.2f,5,1,24));Shader.SetGlobalVector("_CrowdPlayer",new Vector4(0,0,11,1));
                var unshifted=Read(probes,Matrix4x4.identity);
                const float shift=1280;Shader.SetGlobalFloat("_CrowdWorldOffset",shift);Shader.SetGlobalVector("_CrowdPlayer",new Vector4(0,0,11-shift,1));Shader.SetGlobalVector("_CrowdReactionOrigin",new Vector4(0,.55f,20-shift,0));
                var shifted=Read(probes,Matrix4x4.Translate(Vector3.back*shift));report.rebasePositionError=Difference(unshifted,shifted);
                Check(report.rebasePositionError<.001f,"A real floating-origin matrix shift preserves individual GPU poses and head attention");
                Shader.SetGlobalFloat("_CrowdWorldOffset",0);var broken=Read(probes,Matrix4x4.Translate(Vector3.back*shift));
                Check(Difference(shifted,broken)>.05f,"Missing world-offset compensation deliberately produces a detectable pose discontinuity");
                Shader.SetGlobalVector("_CrowdPlayer",Vector4.zero);Shader.SetGlobalVector("_CrowdReactionData",new Vector4(-1,0,0,200));
                for(int i=0;i<people.Count;i++)
                {
                    Person person=people[i];if(Mathf.Abs(person.anchor.y-.55f)>.001f||i%4!=0)continue;
                    float side=person.anchor.x<0?-1:1;Vector3 feet=new Vector3(person.anchor.x,person.anchor.y,person.anchor.z);
                    Vector3 shoulder=new Vector3(.22f*person.persona.x,1.28f,0),launch=new Vector3(.24f*person.persona.x,1.83f,.20f);
                    launch=shoulder+Vector3.ClampMagnitude(launch-shoulder,.596f);
                    Vector3 release=feet+new Vector3(-side*launch.z,launch.y,side*launch.x)*person.anchor.w;
                    Shader.SetGlobalVector("_CrowdThrowOrigin",release);Shader.SetGlobalFloat("_CrowdThrowAge",.35f);
                    Vector3 hand=ToVector(Read(probes,Matrix4x4.identity)[i*5+1]);Vector3 actual=feet+new Vector3(-side*hand.z,hand.y,side*hand.x)*person.anchor.w;
                    report.maximumThrowHandError=Mathf.Max(report.maximumThrowHandError,Vector3.Distance(release,actual));report.throwHandsChecked++;
                }
                Check(report.throwHandsChecked>=8&&report.maximumThrowHandError<.12f,"Actual native throwing hands meet the shared projectile launch position within12cm across front-row anatomy variants");
                Shader.SetGlobalFloat("_CrowdThrowAge",-1);
                var crowd=game.CrowdReactions;crowd.ResetPresentation();game.HandleCommand(RunnerCommand.Jump);crowd.Tick(.5f);
                float serial=Shader.GetGlobalFloat("_CrowdReactionSeed");game.Pause();crowd.Tick(2);
                Check(Shader.GetGlobalFloat("_CrowdReactionSeed")==serial,"Pause preserves which individual supporters are reacting");
                game.Resume();game.Powerups.Enabled=true;game.Powerups.Activate(PowerupKind.Magnet);for(int i=0;i<240;i++)crowd.Tick(1f/60);
                Check(Shader.GetGlobalFloat("_CrowdReactionSeed")>serial,"A subsequent event receives a fresh individual participation pattern");
                game.ReturnToMenu();Check(Shader.GetGlobalFloat("_CrowdReactionSeed")==0,"Menu reset clears the reaction serial as well as the active pose");
            }
            finally
            {
                for(int i=0;i<names.Length;i++)Shader.SetGlobalFloat(names[i],saved[i]);for(int i=0;i<vectors.Length;i++)Shader.SetGlobalVector(vectors[i],savedVectors[i]);
            }
        }
        void BuildProbes(Mesh source)
        {
            var vertices=source.vertices;var normals=source.normals;var colours=source.colors;
            var motions=new List<Vector4>();var anchors=new List<Vector4>();var personas=new List<Vector4>();source.GetUVs(1,motions);source.GetUVs(2,anchors);source.GetUVs(3,personas);
            var grouped=new Dictionary<Vector3,Person>();
            for(int i=0;i<vertices.Length;i++)
            {
                var motion=motions[i];if(motion.y>=8)continue;var anchor=anchors[i];var key=new Vector3(anchor.x,anchor.y,anchor.z);
                if(!grouped.TryGetValue(key,out Person p)){p=new Person{anchor=anchor,persona=personas[i],motion=motion};grouped.Add(key,p);people.Add(p);}
                float side=anchor.x<0?-1:1;Vector3 d=(vertices[i]-key)/anchor.w;Vector3 local=new Vector3(d.z*side,d.y,-d.x*side);
                int slot=-1;float distance=0;
                if(motion.y==5){slot=motion.z<0?0:1;Vector3 center=new Vector3(motion.z*.22f*p.persona.x,.68f,0);distance=(local-center).sqrMagnitude;}
                else if(motion.y==2){if(local.y>p.best[2]){p.best[2]=local.y;p.indices[2]=i;}continue;}
                else if(motion.y==7){slot=local.x<0?3:4;distance=local.y;}
                if(slot>=0&&distance<p.best[slot]){p.best[slot]=distance;p.indices[slot]=i;}
            }
            var positions=new List<Vector3>();var ns=new List<Vector3>();var cs=new List<Color>();var uv=new List<Vector4>();var ms=new List<Vector4>();var ass=new List<Vector4>();var ps=new List<Vector4>();var indices=new List<int>();
            int total=people.Count*5;
            foreach(var p in people)for(int slot=0;slot<5;slot++)
            {
                int i=p.indices[slot];if(i<0)throw new InvalidOperationException("A generated person is missing a native hand/head/sole landmark.");
                positions.Add(vertices[i]);ns.Add(normals[i]);cs.Add(colours.Length==vertices.Length?colours[i]:Color.white);ms.Add(motions[i]);ass.Add(anchors[i]);ps.Add(personas[i]);
                int at=positions.Count-1;uv.Add(new Vector4((at+.5f)/total*2-1,0,0,0));indices.Add(at);
            }
            probes=new Mesh{name="Actual crowd mesh landmarks"};probes.SetVertices(positions);probes.SetNormals(ns);probes.SetColors(cs);probes.SetUVs(0,uv);probes.SetUVs(1,ms);probes.SetUVs(2,ass);probes.SetUVs(3,ps);probes.SetIndices(indices.ToArray(),MeshTopology.Points,0);probes.bounds=new Bounds(Vector3.zero,Vector3.one*10000);
            calibration=Instantiate(probes);calibration.name="Known native pose readback control";
            for(int i=0;i<positions.Count;i++)
            {
                var a=ass[i];float side=a.x<0?-1:1;float y=i%5<2?1.85f:i%5==2?1.67f:.02f;
                positions[i]=new Vector3(a.x,a.y+y*a.w,a.z);ms[i]=new Vector4(0,7,0,0);
            }
            calibration.SetVertices(positions);calibration.SetUVs(1,ms);calibration.bounds=probes.bounds;
        }
        Color[] Read(Mesh mesh,Matrix4x4 matrix)
        {
            RenderTexture previous=RenderTexture.active;Graphics.SetRenderTarget(target);GL.Clear(false,true,new Color(-10,-10,-10,-10));GL.PushMatrix();
            try{if(!material.SetPass(material.FindPass("CrowdPoseProbe")))throw new InvalidOperationException("Native probe material pass failed.");Graphics.DrawMeshNow(mesh,matrix);pixels.ReadPixels(new Rect(0,0,target.width,1),0,0);pixels.Apply(false);}
            finally{GL.PopMatrix();RenderTexture.active=previous;}
            report.gpuSamples++;var data=pixels.GetPixels();foreach(Color point in data)if(point.a<.5f||float.IsNaN(point.g)||float.IsInfinity(point.g))throw new InvalidOperationException("GPU pose probe pixel was not rasterized.");return data;
        }
        void MeasureRaising(Color[] data)
        {
            int shoulders=0,heads=0;for(int i=0;i<people.Count;i++){int p=i*5;float head=data[p+2].g;if(data[p].g>head-.36f&&data[p+1].g>head-.36f)shoulders++;if(data[p].g>head&&data[p+1].g>head)heads++;}
            report.maxBothAboveShoulders=Mathf.Max(report.maxBothAboveShoulders,shoulders/(float)people.Count);report.maxBothAboveHead=Mathf.Max(report.maxBothAboveHead,heads/(float)people.Count);
        }
        static Vector3 ToVector(Color c)=>new Vector3(c.r,c.g,c.b);
        static float Difference(Color[] a,Color[] b){float result=0;for(int i=0;i<a.Length;i++)result=Mathf.Max(result,Vector3.Distance(ToVector(a[i]),ToVector(b[i])));return result;}
        void Check(bool value,string message){if(value)checks.Add(message);else errors.Add(message);}
        void Log(string message,string trace,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<60)errors.Add(message);}
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;if(!prepared)return;
            if(probes!=null)Destroy(probes);if(calibration!=null)Destroy(calibration);if(material!=null)Destroy(material);if(pixels!=null)Destroy(pixels);if(target!=null){target.Release();Destroy(target);}
            if(wallet!=null)wallet.DeleteIsolatedTestData();Application.runInBackground=oldBackground;Time.timeScale=oldScale;Time.captureDeltaTime=oldCapture;Application.targetFrameRate=oldRate;
        }
    }
}
#endif
