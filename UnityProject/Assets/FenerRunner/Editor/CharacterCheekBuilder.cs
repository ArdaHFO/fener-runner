using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FenerRunner.Editor
{
    // Replace the supplied intersecting cheek balls with a small, continuous cheek
    // fullness sculpted into the existing head surface. Source FBX remains untouched.
    public static class CharacterCheekBuilder
    {
        sealed class Island {public int id;public int[] indices;public Bounds bounds;public float Volume=>bounds.size.x*bounds.size.y*bounds.size.z;}
        [Serializable] sealed class Report {public bool passed;public string utc;public int removedCheekIslands,removedTriangles,sculptedHeadVertices;public bool skinningPreserved,clothingAndHairPreserved,otherSubmeshesPreserved;public string scope="Only the two disconnected cheek spheres and existing golden head vertices are edited. Original materials, eyes, beak, hair, sweater, bones and collider remain unchanged. Source FBX is immutable.";}
        [MenuItem("Fener Runner/05 - Rebuild and capture v11 gallery")]
        public static void RefreshBuffersAndReview()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before refreshing mesh buffers.");
            RunnerSceneBuilder.Build();SetupVerification.Verify();
            SessionState.SetInt("FenerRunner.ReviewVersion",11);CharacterLookReview.StartChecks("showcase");
        }
        public static void Sculpt(GameObject root,SkinnedMeshRenderer skin)
        {
            Mesh source=skin.sharedMesh;var vertices=source.vertices;
            int material=Array.FindIndex(skin.sharedMaterials,m=>m.name.Contains("Golden_Feathers"));
            if(material<0)throw new InvalidOperationException("Golden feather surface missing");
            int[] triangles=source.GetTriangles(material),parent=Enumerable.Range(0,vertices.Length).ToArray();
            int Find(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            void Join(int a,int b){parent[Find(a)]=Find(b);}
            for(int i=0;i<triangles.Length;i+=3){Join(triangles[i],triangles[i+1]);Join(triangles[i],triangles[i+2]);}
            var weld=new Dictionary<Vector3Int,int>();
            foreach(int i in triangles.Distinct())
            {
                Vector3 p=vertices[i]*100000f;var key=new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));
                if(weld.TryGetValue(key,out int previous))Join(i,previous);else weld[key]=i;
            }
            Vector3 World(int i)=>root.transform.InverseTransformPoint(skin.transform.TransformPoint(vertices[i]));
            var islands=triangles.Distinct().GroupBy(Find).Select(g=>
            {
                int[] ids=g.ToArray();var bounds=new Bounds(World(ids[0]),Vector3.zero);foreach(int i in ids)bounds.Encapsulate(World(i));
                return new Island{id=g.Key,indices=ids,bounds=bounds};
            }).ToArray();
            Island head=islands.OrderByDescending(i=>i.Volume).First();Bounds h=head.bounds;
            var cheeks=islands.Where(i=>
            {
                Vector3 ratio=new Vector3(i.bounds.size.x/h.size.x,i.bounds.size.y/h.size.y,i.bounds.size.z/h.size.z);
                Vector3 offset=i.bounds.center-h.center;
                return ratio.x>.25f&&ratio.x<.43f&&ratio.y>.25f&&ratio.y<.45f&&ratio.z>.3f&&ratio.z<.55f&&
                    Mathf.Abs(offset.x)>.2f*h.size.x&&offset.y<0&&offset.y>-.25f*h.size.y&&offset.z>.1f*h.size.z;
            }).ToArray();
            if(cheeks.Length!=2)throw new InvalidOperationException("Expected exactly two cheek spheres, found "+cheeks.Length+". Head "+h+"; islands "+string.Join("; ",islands.Select(i=>i.bounds.ToString())));
            var removed=new HashSet<int>(cheeks.Select(c=>c.id));
            var kept=new List<int>(triangles.Length);
            for(int i=0;i<triangles.Length;i+=3)if(!removed.Contains(Find(triangles[i])))kept.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});
            var mesh=Object.Instantiate(source);mesh.name="Kartal continuous cheek surface";int edited=0;
            foreach(int index in head.indices)
            {
                Vector3 p=World(index);Vector3 d=p-h.center;
                // A broad, feather-soft lobe, fading to zero well before eyes and neckline.
                float x=Mathf.Abs(d.x)/h.size.x,y=d.y/h.size.y,z=d.z/h.size.z;
                float weight=Mathf.Exp(-Mathf.Pow((x-.30f)/.18f,2)-Mathf.Pow((y+.15f)/.16f,2));
                weight*=Mathf.SmoothStep(0,1,Mathf.Clamp01((z-.02f)/.32f));
                if(weight<.005f)continue;
                p.x+=Mathf.Sign(d.x)*h.size.x*.014f*weight;p.z+=h.size.z*.035f*weight;
                vertices[index]=skin.transform.InverseTransformPoint(root.transform.TransformPoint(p));edited++;
            }
            mesh.vertices=vertices;mesh.SetTriangles(kept,material);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            bool otherSurfaces=true;var headIds=new HashSet<int>(head.indices);
            var original=source.vertices;for(int i=0;i<vertices.Length;i++)if(!headIds.Contains(i)&&vertices[i]!=original[i])otherSurfaces=false;
            var report=new Report{passed=edited>0&&otherSurfaces,utc=DateTime.UtcNow.ToString("O"),removedCheekIslands=cheeks.Length,
                removedTriangles=(triangles.Length-kept.Count)/3,sculptedHeadVertices=edited,
                skinningPreserved=source.boneWeights.SequenceEqual(mesh.boneWeights)&&source.bindposes.SequenceEqual(mesh.bindposes),clothingAndHairPreserved=otherSurfaces};
            report.otherSubmeshesPreserved=true;
            for(int i=0;i<source.subMeshCount;i++)if(i!=material&&!source.GetTriangles(i).SequenceEqual(mesh.GetTriangles(i)))report.otherSubmeshesPreserved=false;
            report.passed&=report.skinningPreserved&&report.otherSubmeshesPreserved;
            if(!report.passed)throw new InvalidOperationException("Cheek sculpt preservation failed");
            skin.sharedMesh=RunnerSceneBuilder.Upsert(mesh,RunnerSceneBuilder.Generated+"/Meshes/KartalContinuousCheeks.asset");
            Directory.CreateDirectory("../Validation");File.WriteAllText("../Validation/unity_cheeks_v11.json",JsonUtility.ToJson(report,true));
        }
    }
}
