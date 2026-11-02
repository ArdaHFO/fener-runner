using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FenerRunner.Editor
{
    internal static class ObstacleHitMeshBuilder
    {
        struct Indexed {public int a,b,c;}
        sealed class DisjointSet
        {
            readonly int[] parent;
            public DisjointSet(int size){parent=Enumerable.Range(0,size).ToArray();}
            public int Find(int n){while(parent[n]!=n){parent[n]=parent[parent[n]];n=parent[n];}return n;}
            public void Join(int a,int b){a=Find(a);b=Find(b);if(a!=b)parent[b]=a;}
        }
        public static ObstacleHitMesh Build(GameObject root)
        {
            var vertices=new List<Vector3>();var welded=new Dictionary<Vector3Int,int>();
            var raw=new List<Indexed>();var unique=new HashSet<(int,int,int)>();
            foreach(MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||!renderer.enabled)continue;
                Vector3[] points=filter.sharedMesh.vertices;int[] map=new int[points.Length];
                for(int i=0;i<points.Length;i++)
                {
                    Vector3 p=root.transform.InverseTransformPoint(filter.transform.TransformPoint(points[i]));
                    var key=Vector3Int.RoundToInt(p*100000);
                    if(!welded.TryGetValue(key,out int at)){at=vertices.Count;vertices.Add(p);welded.Add(key,at);}map[i]=at;
                }
                int[] indices=filter.sharedMesh.triangles;
                for(int i=0;i<indices.Length;i+=3)
                {
                    int a=map[indices[i]],b=map[indices[i+1]],c=map[indices[i+2]];
                    if(a==b||b==c||a==c||Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).sqrMagnitude<1e-14f)continue;
                    int x=Mathf.Min(a,Mathf.Min(b,c)),z=Mathf.Max(a,Mathf.Max(b,c)),y=a+b+c-x-z;
                    if(unique.Add((x,y,z)))raw.Add(new Indexed{a=a,b=b,c=c});
                }
            }
            if(raw.Count==0)throw new InvalidOperationException("An obstacle must have nondegenerate final rendered triangles.");
            var set=new DisjointSet(vertices.Count);foreach(Indexed tri in raw){set.Join(tri.a,tri.b);set.Join(tri.b,tri.c);}
            var roots=new Dictionary<int,int>();var edges=new List<Dictionary<(int,int),int>>();var componentBounds=new List<Bounds>();
            var triangles=new ObstacleHitMesh.Triangle[raw.Count];
            for(int i=0;i<raw.Count;i++)
            {
                Indexed tri=raw[i];int group=set.Find(tri.a);
                if(!roots.TryGetValue(group,out int component))
                {component=roots.Count;roots.Add(group,component);edges.Add(new Dictionary<(int,int),int>());componentBounds.Add(new Bounds(vertices[tri.a],Vector3.zero));}
                triangles[i]=new ObstacleHitMesh.Triangle{a=vertices[tri.a],b=vertices[tri.b],c=vertices[tri.c],component=component};
                Bounds bounds=componentBounds[component];bounds.Encapsulate(vertices[tri.a]);bounds.Encapsulate(vertices[tri.b]);bounds.Encapsulate(vertices[tri.c]);componentBounds[component]=bounds;
                Edge(edges[component],tri.a,tri.b);Edge(edges[component],tri.b,tri.c);Edge(edges[component],tri.c,tri.a);
            }
            var nodes=new List<ObstacleHitMesh.Node>();BuildNode(triangles,0,triangles.Length,nodes);
            var components=new ObstacleHitMesh.Component[roots.Count];
            for(int i=0;i<components.Length;i++)components[i]=new ObstacleHitMesh.Component
            {bounds=componentBounds[i],closed=edges[i].Values.All(n=>n==2),triangles=Enumerable.Range(0,triangles.Length).Where(n=>triangles[n].component==i).ToArray()};
            var mesh=ScriptableObject.CreateInstance<ObstacleHitMesh>();mesh.triangles=triangles;mesh.nodes=nodes.ToArray();mesh.components=components;mesh.bounds=nodes[0].bounds;
            string path=RunnerSceneBuilder.Generated+"/Meshes/HitMesh_"+root.name+".asset";
            mesh=RunnerSceneBuilder.Upsert(mesh,path);
            Debug.Log(root.name+" precise hit mesh: "+triangles.Length+" triangles, "+nodes.Count+" BVH nodes, "+components.Length+" solid parts ("+components.Count(c=>c.closed)+" closed). Shared by the pool.");
            return mesh;
        }
        static void Edge(Dictionary<(int,int),int> edges,int a,int b)
        {var key=(Mathf.Min(a,b),Mathf.Max(a,b));edges.TryGetValue(key,out int count);edges[key]=count+1;}
        static void BuildNode(ObstacleHitMesh.Triangle[] triangles,int start,int count,List<ObstacleHitMesh.Node> nodes)
        {
            Bounds bounds=new Bounds(triangles[start].a,Vector3.zero),centres=new Bounds(Centre(triangles[start]),Vector3.zero);
            for(int i=start;i<start+count;i++){bounds.Encapsulate(triangles[i].a);bounds.Encapsulate(triangles[i].b);bounds.Encapsulate(triangles[i].c);centres.Encapsulate(Centre(triangles[i]));}
            int at=nodes.Count;nodes.Add(default);
            if(count<=8)nodes[at]=new ObstacleHitMesh.Node{bounds=bounds,start=start,count=count,escape=at+1};
            else
            {
                Vector3 size=centres.size;int axis=size.x>size.y?(size.x>size.z?0:2):(size.y>size.z?1:2);
                Array.Sort(triangles,start,count,Comparer<ObstacleHitMesh.Triangle>.Create((a,b)=>Centre(a)[axis].CompareTo(Centre(b)[axis])));
                int half=count/2;BuildNode(triangles,start,half,nodes);BuildNode(triangles,start+half,count-half,nodes);
                nodes[at]=new ObstacleHitMesh.Node{bounds=bounds,start=start,count=0,escape=nodes.Count};
            }
        }
        static Vector3 Centre(ObstacleHitMesh.Triangle t)=>(t.a+t.b+t.c)/3;
    }
}
