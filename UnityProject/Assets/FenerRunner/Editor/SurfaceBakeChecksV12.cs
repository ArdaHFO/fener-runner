using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FenerRunner.Editor
{
    public static class SurfaceBakeChecksV12
    {
        const float ColorTolerance=.00001f;
        sealed class Snapshot
        {
            public string path,guid;public Vector3[] positions,normals;public Vector4[] tangents;public Vector2[] uv;
            public BoneWeight[] weights;public Matrix4x4[] bindposes;public Color[] colors;public int[][] indices;public MeshTopology[] topologies;
            public bool[] referenced;public IndexFormat indexFormat;
        }
        [Serializable] sealed class MeshResult
        {
            public string path,guidBefore,guidAfter;public int vertices,referencedVertices,unusedVertices;
            public bool guidPreserved,trianglesUnchanged,positionsUnchanged,normalsUnchanged,tangentsUnchanged,uvUnchanged,weightsUnchanged,bindposesUnchanged,colorsWithinTolerance;
            public float maximumVisibleColorDifference,maximumUnusedColorDifference;
        }
        [Serializable] sealed class Report
        {
            public bool passed;public string utc,unityVersion;public string[] checks,errors,nativeLogSamples;public MeshResult[] meshes;
            public int nativeAssertions,nativeErrors,nativeExceptions;public float colorTolerance=ColorTolerance,maximumVisibleColorDifference;
            public string scope="Native Editor rebuild plus SetupVerification. Every Generated/Meshes/Finished*.asset is snapshotted before rebuilding. Referenced positions/normals/tangents/UV0/bone weights and bindposes, submesh indices/topologies and asset GUID are compared exactly; visible vertex RGBA uses1e-5 tolerance. Unreferenced vertex colors may change. Native Assert/Error/Exception logs make the report fail. A passing comparison launches a fresh actual character gallery. No device-performance claim.";
        }
        static readonly List<string> checks=new List<string>(),errors=new List<string>(),nativeSamples=new List<string>();
        static int assertions,errorLogs,exceptions;

        [MenuItem("Fener Runner/08 - Rebuild and verify v12 surface bake")]
        public static void RebuildAndVerify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before surface validation.");
            checks.Clear();errors.Clear();nativeSamples.Clear();assertions=errorLogs=exceptions=0;
            var results=new List<MeshResult>();var report=new Report();
            try
            {
                string folder=RunnerSceneBuilder.Generated+"/Meshes";
                string[] paths=Directory.GetFiles(folder,"Finished*.asset",SearchOption.TopDirectoryOnly).Select(p=>p.Replace('\\','/')).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
                if(paths.Length==0)throw new InvalidOperationException("No finished generated mesh assets were available to snapshot.");
                Snapshot[] snapshots=paths.Select(Read).ToArray();
                Application.logMessageReceived+=Log;
                try{RunnerSceneBuilder.Build();SetupVerification.Verify();}
                finally{Application.logMessageReceived-=Log;}
                string[] afterPaths=Directory.GetFiles(folder,"Finished*.asset",SearchOption.TopDirectoryOnly).Select(p=>p.Replace('\\','/')).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
                Check(paths.SequenceEqual(afterPaths),"The complete set of finished mesh asset paths is preserved");
                foreach(Snapshot before in snapshots)
                {
                    Snapshot after=Read(before.path);MeshResult result=Compare(before,after);results.Add(result);
                    Check(result.guidPreserved,before.path+": asset GUID is preserved");
                    Check(result.trianglesUnchanged,before.path+": vertex count, index format and every submesh index/topology are unchanged");
                    Check(result.positionsUnchanged&&result.normalsUnchanged&&result.tangentsUnchanged&&result.uvUnchanged&&result.weightsUnchanged&&result.bindposesUnchanged,
                        before.path+": all referenced position/normal/tangent/UV/skin values remain exact");
                    Check(result.colorsWithinTolerance,before.path+": maximum visible RGBA change is within1e-5 ("+result.maximumVisibleColorDifference.ToString("R")+")");
                    report.maximumVisibleColorDifference=Mathf.Max(report.maximumVisibleColorDifference,result.maximumVisibleColorDifference);
                }
            }
            catch(Exception exception){errors.Add(exception.ToString());}
            finally{Application.logMessageReceived-=Log;}
            Check(assertions==0&&errorLogs==0&&exceptions==0,"Rebuild and setup emit zero native Assert/Error/Exception messages");
            report.nativeAssertions=assertions;report.nativeErrors=errorLogs;report.nativeExceptions=exceptions;report.nativeLogSamples=nativeSamples.ToArray();
            report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.meshes=results.ToArray();report.checks=checks.ToArray();report.errors=errors.ToArray();report.passed=errors.Count==0;
            Directory.CreateDirectory("../Validation");File.WriteAllText("../Validation/unity_surface_bake_v12.json",JsonUtility.ToJson(report,true));
            if(!report.passed){Debug.LogError("Surface bake v12 FAILED; see unity_surface_bake_v12.json");return;}
            Debug.Log("Surface bake v12 PASSED: "+results.Count+" meshes, zero native assertions/errors, maximum visible RGBA difference "+report.maximumVisibleColorDifference.ToString("R"));
            SessionState.SetInt("FenerRunner.ReviewVersion",12);CharacterLookReview.StartChecks("showcase");
        }
        static Snapshot Read(string path)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh==null)throw new InvalidOperationException("Missing mesh: "+path);
            var result=new Snapshot{path=path,guid=AssetDatabase.AssetPathToGUID(path),positions=mesh.vertices,normals=mesh.normals,tangents=mesh.tangents,uv=mesh.uv,weights=mesh.boneWeights,bindposes=mesh.bindposes,colors=mesh.colors,
                indices=new int[mesh.subMeshCount][],topologies=new MeshTopology[mesh.subMeshCount],referenced=new bool[mesh.vertexCount],indexFormat=mesh.indexFormat};
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                result.indices[sub]=mesh.GetIndices(sub);result.topologies[sub]=mesh.GetTopology(sub);
                foreach(int vertex in result.indices[sub])
                {if(vertex<0||vertex>=result.referenced.Length)throw new InvalidOperationException("Out-of-range mesh index in "+path);result.referenced[vertex]=true;}
            }
            return result;
        }
        static MeshResult Compare(Snapshot a,Snapshot b)
        {
            var result=new MeshResult{path=a.path,guidBefore=a.guid,guidAfter=b.guid,vertices=a.positions.Length,referencedVertices=a.referenced.Count(v=>v),unusedVertices=a.referenced.Count(v=>!v),
                guidPreserved=a.guid==b.guid&&a.guid.Length>0,trianglesUnchanged=a.positions.Length==b.positions.Length&&a.indexFormat==b.indexFormat&&a.indices.Length==b.indices.Length,
                positionsUnchanged=SameVisible(a.positions,b.positions,a.referenced,Same),normalsUnchanged=SameVisible(a.normals,b.normals,a.referenced,Same),
                tangentsUnchanged=SameVisible(a.tangents,b.tangents,a.referenced,Same),uvUnchanged=SameVisible(a.uv,b.uv,a.referenced,Same),
                weightsUnchanged=SameVisible(a.weights,b.weights,a.referenced,Same),bindposesUnchanged=a.bindposes.Length==b.bindposes.Length,
                colorsWithinTolerance=a.colors.Length==b.colors.Length&&a.colors.Length==a.positions.Length};
            if(a.indices.Length==b.indices.Length)for(int sub=0;sub<a.indices.Length;sub++)result.trianglesUnchanged&=a.topologies[sub]==b.topologies[sub]&&a.indices[sub].SequenceEqual(b.indices[sub]);
            if(a.bindposes.Length==b.bindposes.Length)for(int i=0;i<a.bindposes.Length;i++)for(int j=0;j<16;j++)result.bindposesUnchanged&=a.bindposes[i][j]==b.bindposes[i][j];
            if(result.colorsWithinTolerance)for(int i=0;i<a.colors.Length;i++)
            {
                Color x=a.colors[i],y=b.colors[i];float difference=Mathf.Max(Mathf.Abs(x.r-y.r),Mathf.Abs(x.g-y.g),Mathf.Abs(x.b-y.b),Mathf.Abs(x.a-y.a));
                if(float.IsNaN(difference)||float.IsInfinity(difference)){result.colorsWithinTolerance=false;continue;}
                if(a.referenced[i]){result.maximumVisibleColorDifference=Mathf.Max(result.maximumVisibleColorDifference,difference);result.colorsWithinTolerance&=difference<=ColorTolerance;}
                else result.maximumUnusedColorDifference=Mathf.Max(result.maximumUnusedColorDifference,difference);
            }
            return result;
        }
        static bool SameVisible<T>(T[] a,T[] b,bool[] used,Func<T,T,bool> same)
        {
            if(a.Length!=b.Length)return false;if(a.Length==0)return true;if(a.Length!=used.Length)return false;
            for(int i=0;i<used.Length;i++)if(used[i]&&!same(a[i],b[i]))return false;return true;
        }
        static bool Same(Vector2 a,Vector2 b)=>a.x==b.x&&a.y==b.y;
        static bool Same(Vector3 a,Vector3 b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;
        static bool Same(Vector4 a,Vector4 b)=>a.x==b.x&&a.y==b.y&&a.z==b.z&&a.w==b.w;
        static bool Same(BoneWeight a,BoneWeight b)=>a.boneIndex0==b.boneIndex0&&a.boneIndex1==b.boneIndex1&&a.boneIndex2==b.boneIndex2&&a.boneIndex3==b.boneIndex3&&a.weight0==b.weight0&&a.weight1==b.weight1&&a.weight2==b.weight2&&a.weight3==b.weight3;
        static void Check(bool value,string label){if(value)checks.Add(label);else errors.Add(label);}
        static void Log(string message,string trace,LogType type)
        {
            if(type==LogType.Assert)assertions++;else if(type==LogType.Error)errorLogs++;else if(type==LogType.Exception)exceptions++;else return;
            if(nativeSamples.Count<40)nativeSamples.Add(type+": "+message+"\n"+trace);
        }
    }
}
