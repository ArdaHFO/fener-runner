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
    public sealed class HitboxChecksV17:MonoBehaviour
    {
        const string Flag="FenerRunner.HitboxesV17";
        [Serializable] sealed class ClearCase{public string kind;public Vector3 position;public int nodes,triangles;}
        [Serializable] sealed class Report
        {public bool passed;public string utc;public string[] checks,errors;public ClearCase[] formerlyFatalAir;public int geometryTriangles,maximumQueriedTriangles,maximumQueriedNodes;public long steadyQueryAllocatedBytes;public string scope="Native scene CharacterController vs baked obstacle contacts, independently checked against native static MeshColliders built directly from each live final rendered MeshFilter and its world transform, without reusing baked hitMesh geometry. Analytic cube sweep/inside/scale controls, formerly fatal empty-box corners, full-frame passage, pause/reset and zero-allocation steady queries. No APK or physical-phone performance claim.";}
        readonly List<string> checks=new List<string>(),errors=new List<string>();readonly List<ClearCase> clearCases=new List<ClearCase>();
        readonly Collider[] referenceHits=new Collider[16];
        RunnerGame game;RunnerMotor motor;RunWallet wallet;Report report=new Report();bool background;float oldScale;int oldRate;
        readonly List<UnityEngine.Object> temporary=new List<UnityEngine.Object>();
        [MenuItem("Fener Runner/106 - Verify precise obstacle contacts v17")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Precise hitbox checks v17").AddComponent<HitboxChecksV17>();}}
        IEnumerator Start()
        {
            background=Application.runInBackground;oldScale=Time.timeScale;oldRate=Application.targetFrameRate;Application.runInBackground=true;Application.targetFrameRate=60;Application.logMessageReceived+=Log;
            var routine=Run();while(true){bool more;object wait=null;try{more=routine.MoveNext();if(more)wait=routine.Current;}catch(Exception e){errors.Add(e.ToString());break;}if(!more)break;yield return wait;}
            if(game!=null){game.enabled=false;game.ReturnToMenu();}
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.checks=checks.ToArray();report.errors=errors.ToArray();report.formerlyFatalAir=clearCases.ToArray();
            File.WriteAllText(Evidence("unity_hitboxes_v17.json"),JsonUtility.ToJson(report,true));Application.logMessageReceived-=Log;
            Debug.Log("Precise hitboxes v17 "+(report.passed?"PASSED":"FAILED")+": "+checks.Count+" checks; "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Run()
        {
            game=FindFirstObjectByType<RunnerGame>();if(game==null)throw new InvalidOperationException("Actual scene required.");
            game.AutomatedTest=true;game.enabled=false;motor=game.Motor;wallet=game.UseWalletForTest("FenerRunner.Test.HitboxesV17."+DateTime.UtcNow.Ticks+".");
            AnalyticGeometry();
            foreach(ObstacleKind kind in new[]{ObstacleKind.JumpOver,ObstacleKind.SlideUnder})
            {
                Reset();RunnerObstacle obstacle=game.Obstacles.ArrangeTest(kind,18);
                Check(obstacle.hitMesh!=null&&obstacle.hitMesh.nodes.Length>1,"Final rendered triangle geometry is baked and pooled / "+kind);
                if(obstacle.hitMesh==null)continue;
                report.geometryTriangles+=obstacle.hitMesh.triangles.Length;
                GameObject reference=Reference(obstacle);int clear=0,positive=0,mismatch=0;Vector3 savedClear=default;
                foreach(float y in kind==ObstacleKind.SlideUnder?new[]{0f,.8f,1.6f,2.4f,2.75f,3.1f}:new[]{0f,.7f,1.2f,1.6f,1.9f})
                foreach(float x in new[]{-.01f,.65f,.85f,1.05f,1.25f,1.4f})
                foreach(float z in new[]{-.85f,-.6f,-.3f,0f,.3f,.6f,.85f})
                {
                    Vector3 position=obstacle.transform.position+new Vector3(x,y,z);Teleport(position);Capsule(out Vector3 bottom,out Vector3 top,out float radius);
                    bool native=Physics.OverlapCapsuleNonAlloc(bottom,top,radius,referenceHits,1<<31,QueryTriggerInteraction.Collide)>0;
                    bool authored=false;foreach(var box in obstacle.CollisionBoxes)if(box.bounds.Intersects(new Bounds((bottom+top)*.5f,new Vector3(radius*2,top.y-bottom.y+radius*2,radius*2))))
                    {int count=Physics.OverlapCapsuleNonAlloc(bottom,top,radius,referenceHits,1<<8,QueryTriggerInteraction.Collide);authored=count>0;break;}
                    // Reset the sample each independent pose, otherwise teleporting
                    // would deliberately sweep between unrelated grid cases.
                    game.Obstacles.BeginMove(motor);bool actual=game.Obstacles.Check(motor)!=null;
                    if(native!=actual)mismatch++;
                    if(native&&actual)positive++;
                    if(authored&&!native&&!actual)
                    {
                        clear++;savedClear=position;
                        if(clear<=10)clearCases.Add(new ClearCase{kind=kind.ToString(),position=position,nodes=game.Obstacles.LastNarrowPhaseNodes,triangles=game.Obstacles.LastNarrowPhaseTriangles});
                    }
                    report.maximumQueriedNodes=Mathf.Max(report.maximumQueriedNodes,game.Obstacles.LastNarrowPhaseNodes);
                    report.maximumQueriedTriangles=Mathf.Max(report.maximumQueriedTriangles,game.Obstacles.LastNarrowPhaseTriangles);
                }
                Check(mismatch==0,"Independent native triangle overlaps agree across full-height/lateral/depth pose grid / "+kind+" mismatches="+mismatch);
                Check(positive>10,"Actual visible body contacts remain lethal / "+kind+" positives="+positive);
                Check(clear>=3,"Previously lethal authoring-box air is now safely passable / "+kind+" clear poses="+clear);
                // Coarse high-speed movement crosses the entire real body between
                // clear endpoints, retaining ordered continuous fatal contacts.
                foreach(float travel in new[]{1.2f,2.8f,7f,20f})
                {
                    Bounds visual=obstacle.SurfaceBounds;
                    float halfTravel=Mathf.Max(travel,Mathf.Max(visual.max.z-obstacle.transform.position.z,obstacle.transform.position.z-visual.min.z)+motor.Capsule.radius+.1f);
                    Teleport(obstacle.transform.position+new Vector3(0,0,-halfTravel));game.Obstacles.BeginMove(motor);Teleport(obstacle.transform.position+new Vector3(0,0,halfTravel));
                    Capsule(out Vector3 eb,out Vector3 et,out float er);
                    bool endClear=Physics.OverlapCapsuleNonAlloc(eb,et,er,referenceHits,1<<31,QueryTriggerInteraction.Collide)==0;
                    Check(endClear&&game.Obstacles.Check(motor)==obstacle&&game.Obstacles.LastHitWasSweep,"Continuous full-body traversal stays fatal with independently clear endpoints / "+kind+" travel="+halfTravel*2);
                }
                if(clear>0)
                {
                    Teleport(savedClear);Capsule(out Vector3 b,out Vector3 t,out float radius);
                    for(int i=0;i<4;i++)obstacle.hitMesh.Sweep(obstacle.transform,b,t,b,t,radius,radius,out _);
                    long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<64;i++)obstacle.hitMesh.Sweep(obstacle.transform,b,t,b,t,radius,radius,out _);
                    long bytes=GC.GetAllocatedBytesForCurrentThread()-before;report.steadyQueryAllocatedBytes+=bytes;
                    Check(bytes==0,"Steady precise contact queries allocate no managed memory / "+kind+" bytes="+bytes);
                }
                reference.SetActive(false);yield return null;
            }
            // Real production death continues to consume a genuine collision,
            // including after the new double action changes.
            Reset();motor.TryJump();motor.TryJump();game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder,motor.transform.position.z+.08f);
            game.AdvanceForTest(1f/60);Check(game.State==RunState.Finished,"A genuinely intersecting header still ends a double-jump run through production death logic");
            Reset();motor.TrySlide();motor.TrySlide();game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,motor.transform.position.z+.08f);
            game.AdvanceForTest(1f/60);Check(game.State==RunState.Finished,"A real black body still ends a double-slide run");
            Reset();game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder,22);motor.TrySlide();bool hit=false;
            for(int i=0;i<40;i++){game.Obstacles.BeginMove(motor);motor.Tick(1f/60);hit|=game.Obstacles.Check(motor)!=null;}
            Check(!hit,"The visible open passage remains clear while sliding through its centre");
            game.Pause();Vector3 paused=motor.transform.position;game.enabled=true;yield return new WaitForSecondsRealtime(.12f);game.enabled=false;
            Check(motor.transform.position==paused,"Pause preserves actual controller position after precise hitbox checks");
            game.Restart();game.enabled=false;Check(game.State==RunState.Running&&game.Obstacles.ContactCount==0,"Restart discards old narrow-phase contact results");
        }
        void AnalyticGeometry()
        {
            var owner=new GameObject("Analytic hit mesh pose");temporary.Add(owner);ObstacleHitMesh cube=Cube(false);temporary.Add(cube);
            bool Hit(Vector3 b0,Vector3 t0,Vector3 b1,Vector3 t1,float radius)=>cube.Sweep(owner.transform,b0,t0,b1,t1,radius,radius,out _);
            Check(Hit(Vector3.zero,Vector3.up*.2f,Vector3.zero,Vector3.up*.2f,.1f),"A capsule wholly embedded inside a closed solid is fatal, not a hollow triangle-shell gap");
            Check(!Hit(new Vector3(1.381f,-.4f,-3),new Vector3(1.381f,.4f,-3),new Vector3(1.381f,-.4f,3),new Vector3(1.381f,.4f,3),.38f),"One-millimetre true lateral clearance stays clear on a parallel sweep");
            Check(Hit(new Vector3(1.379f,-.4f,-3),new Vector3(1.379f,.4f,-3),new Vector3(1.379f,-.4f,3),new Vector3(1.379f,.4f,3),.38f),"One-millimetre true surface penetration still collides");
            Check(Hit(new Vector3(0,-.4f,-50),new Vector3(0,.4f,-50),new Vector3(0,-.4f,50),new Vector3(0,.4f,50),.38f),"An entire100m between-frame traversal cannot tunnel through a solid surface");
            Check(Hit(new Vector3(0,-4,0),new Vector3(0,-3,0),new Vector3(0,4,0),new Vector3(0,5,0),.2f),"Pure vertical passage is continuously detected without horizontal motion");
            Check(!Hit(new Vector3(0,1.201f,-3),new Vector3(0,2,-3),new Vector3(0,1.201f,3),new Vector3(0,2,3),.2f),"One-millimetre overhead clearance is not a false landing hit");
            Check(Hit(new Vector3(0,1.199f,-3),new Vector3(0,2,-3),new Vector3(0,1.199f,3),new Vector3(0,2,3),.2f),"A real overhead grazing penetration remains a contact");
            owner.transform.SetPositionAndRotation(new Vector3(700,-4,-500),Quaternion.Euler(13,47,-8));owner.transform.localScale=new Vector3(1.7f,.8f,1.2f);
            Vector3 centre=owner.transform.TransformPoint(Vector3.zero);
            Check(Hit(centre,centre+Vector3.up*.1f,centre,centre+Vector3.up*.1f,.1f),"Solid containment survives rotation, nonuniform scaling and a shifted world origin");
            Vector3 from=owner.transform.TransformPoint(new Vector3(0,0,-5)),to=owner.transform.TransformPoint(new Vector3(0,0,5));
            Check(Hit(from,from+Vector3.up*.2f,to,to+Vector3.up*.2f,.1f),"A rotated nonuniformly scaled surface is swept in actual world coordinates");
            ObstacleHitMesh overlap=Cube(true);temporary.Add(overlap);owner.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);owner.transform.localScale=Vector3.one;
            Check(overlap.Sweep(owner.transform,Vector3.zero,Vector3.up*.2f,Vector3.zero,Vector3.up*.2f,.1f,.1f,out _),"Two overlapping solids with opposite triangle winding still contain the player");
        }
        static ObstacleHitMesh Cube(bool overlapping)
        {
            var mesh=ScriptableObject.CreateInstance<ObstacleHitMesh>();Vector3[] p={new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
            int[] index={0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
            int parts=overlapping?2:1;mesh.triangles=new ObstacleHitMesh.Triangle[12*parts];mesh.components=new ObstacleHitMesh.Component[parts];mesh.bounds=new Bounds(Vector3.zero,new Vector3(2,2,2));
            for(int part=0;part<parts;part++)
            {
                for(int i=0;i<12;i++)mesh.triangles[part*12+i]=new ObstacleHitMesh.Triangle{a=p[index[i*3]],b=p[index[i*3+(part==0?1:2)]],c=p[index[i*3+(part==0?2:1)]],component=part};
                mesh.components[part]=new ObstacleHitMesh.Component{bounds=mesh.bounds,closed=true,triangles=Enumerable.Range(part*12,12).ToArray()};
            }
            mesh.nodes=new[]{new ObstacleHitMesh.Node{bounds=mesh.bounds,start=0,count=mesh.triangles.Length,escape=1}};return mesh;
        }
        GameObject Reference(RunnerObstacle obstacle)
        {
            // This separate native collider starts from the live rendered meshes,
            // not the cooked narrow-phase asset, so a bake-coordinate mistake is
            // visible to the pose matrix instead of being shared by both sides.
            var go=new GameObject("Independent live-render-mesh contact oracle");temporary.Add(go);go.layer=31;
            var mesh=new Mesh();temporary.Add(mesh);mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            var vertices=new List<Vector3>();var indices=new List<int>();
            foreach(var filter in obstacle.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                int offset=vertices.Count;Matrix4x4 toWorld=filter.transform.localToWorldMatrix;
                foreach(Vector3 point in filter.sharedMesh.vertices)vertices.Add(toWorld.MultiplyPoint3x4(point));
                foreach(int index in filter.sharedMesh.triangles)indices.Add(offset+index);
            }
            if(vertices.Count==0)throw new InvalidOperationException("Live final obstacle render geometry is missing.");
            mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
            go.AddComponent<MeshCollider>().sharedMesh=mesh;Physics.SyncTransforms();return go;
        }
        void Reset(){game.enabled=false;game.Play();game.Obstacles.SetActive(false);game.Tokens.Enabled=false;game.Powerups.Enabled=false;motor.Speed=18;}
        void Teleport(Vector3 position){motor.Capsule.enabled=false;motor.transform.position=position;motor.Capsule.enabled=true;Physics.SyncTransforms();}
        void Capsule(out Vector3 b,out Vector3 t,out float r){r=motor.Capsule.radius;Vector3 c=motor.transform.TransformPoint(motor.Capsule.center);b=c-Vector3.up*(motor.Capsule.height*.5f-r);t=c+Vector3.up*(motor.Capsule.height*.5f-r);}
        static string Evidence(string name){string path=ReviewEvidence.FileFor(17,"../Validation/"+name);Directory.CreateDirectory(Path.GetDirectoryName(path));return path;}
        void Check(bool valid,string label){if(valid)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<100)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;foreach(var item in temporary)if(item!=null)Destroy(item);if(wallet!=null)wallet.DeleteIsolatedTestData();Application.runInBackground=background;Application.targetFrameRate=oldRate;Time.timeScale=oldScale;}
    }
}
#endif
