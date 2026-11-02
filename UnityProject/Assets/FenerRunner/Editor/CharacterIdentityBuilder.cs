using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FenerRunner.Editor
{
    // Editable mascot interpretations. The supplied meshes and textures remain untouched.
    internal static class CharacterIdentityBuilder
    {
        public static void Coach(GameObject root, SkinnedMeshRenderer skin)
        {
            // The supplied kartal.jpeg / kartal.png reference is a yellow-feathered
            // mascot with its original swept tuft and white knit. Do not flatten
            // the crest or add a human scalp, helmet, grey hair, or moustache.
            // BuildPlayer starts from the original FBX on every rebuild, so keeping
            // the imported shared mesh also removes the previous generated overlay.
            if(root==null||skin==null||skin.sharedMesh==null)
                throw new InvalidOperationException("The original Kartal mascot mesh is required.");
            CharacterCheekBuilder.Sculpt(root,skin);
            CharacterSurfaceBuilder.Finish(skin,"KartalPlayer");
        }

        public static void Obstacle(GameObject root, MeshRenderer renderer, string id)
        {
            var filter=renderer.GetComponent<MeshFilter>();var mesh=Object.Instantiate(filter.sharedMesh);
            mesh.name=id=="black"?"Striker upright proportions":"Midfielder flowing curls";
            var vertices=mesh.vertices;var materials=renderer.sharedMaterials;
            var hair=new HashSet<int>();
            if(id=="curly")for(int s=0;s<materials.Length;s++)
                if(materials[s].name.Contains("Hair")||materials[s].name.Contains("Curl"))
                    foreach(int i in mesh.GetTriangles(s))hair.Add(i);
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 p=root.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertices[i]));
                if(id=="black")
                {
                    p.x*=1.45f;p.z*=1.35f;
                    p.y=Mathf.Min(p.y,.995f);
                    p.y=p.y<.23f?p.y*2.7f:.621f+(p.y-.23f)*1.7f;
                }
                else if(hair.Contains(i))
                {
                    p.x*=1.12f;p.z*=1.06f;
                    if(p.y<3.02f)p.y=3.02f+(p.y-3.02f)*1.12f;
                }
                vertices[i]=renderer.transform.InverseTransformPoint(root.transform.TransformPoint(p));
            }
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
            filter.sharedMesh=RunnerSceneBuilder.Upsert(mesh,RunnerSceneBuilder.Generated+"/Meshes/"+mesh.name+".asset");
            if(id=="black")
            {
                var beard=new Shapes();
                for(int side=-1;side<=1;side+=2)
                    beard.Ellipsoid(new Vector3(side*.245f,1.44f,.254f),new Vector3(.115f,.14f,.09f));
                beard.Ellipsoid(new Vector3(0,1.397f,.326f),new Vector3(.25f,.09f,.07f));
                Save(beard,"Striker close beard",root.transform,root.transform,Mat("StrikerBeard","101112"));
            }
            foreach(var filterPart in root.GetComponentsInChildren<MeshFilter>())
                CharacterSurfaceBuilder.Finish(filterPart.GetComponent<MeshRenderer>(),root.name+"_"+filterPart.name);
        }

        static Material Mat(string name,string color)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};
            ColorUtility.TryParseHtmlString("#"+color,out Color c);m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.12f);
            return RunnerSceneBuilder.Upsert(m,RunnerSceneBuilder.Generated+"/Materials/"+name+".mat");
        }
        static void Save(Shapes shapes,string name,Transform coordinates,Transform parent,Material material)
        {
            var mesh=new Mesh{name=name};
            mesh.SetVertices(shapes.vertices.Select(p=>parent.InverseTransformPoint(coordinates.TransformPoint(p))).ToList());
            mesh.SetTriangles(shapes.triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=RunnerSceneBuilder.Upsert(mesh,RunnerSceneBuilder.Generated+"/Meshes/"+name+".asset");
            go.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        sealed class Shapes
        {
            public readonly List<Vector3> vertices=new List<Vector3>();public readonly List<int> triangles=new List<int>();
            public void Ellipsoid(Vector3 centre,Vector3 radius,float yaw=0)
            {
                int start=vertices.Count;const int columns=16,rows=10;Quaternion q=Quaternion.Euler(0,yaw,0);
                for(int r=0;r<=rows;r++)for(int c=0;c<=columns;c++)
                {
                    float p=Mathf.PI*r/rows,a=2*Mathf.PI*c/columns;
                    vertices.Add(centre+q*Vector3.Scale(radius,new Vector3(Mathf.Sin(p)*Mathf.Cos(a),Mathf.Cos(p),Mathf.Sin(p)*Mathf.Sin(a))));
                }
                Join(start,columns,rows);
            }
            public void Cap(Vector3 centre,Vector3 radius)
            {
                int start=vertices.Count;const int columns=40,rows=12;
                for(int r=0;r<=rows;r++)for(int c=0;c<=columns;c++)
                {
                    float a=2*Mathf.PI*c/columns;
                    float limit=Mathf.Lerp(1.42f,.98f,Mathf.Pow(Mathf.Max(0,Mathf.Sin(a)),1.5f));
                    float p=.003f+(limit-.003f)*r/rows;
                    Vector3 wave=radius*(1+.005f*Mathf.Sin(a*24+p*9));
                    vertices.Add(centre+Vector3.Scale(wave,new Vector3(Mathf.Sin(p)*Mathf.Cos(a),Mathf.Cos(p),Mathf.Sin(p)*Mathf.Sin(a))));
                }
                Join(start,columns,rows);
            }
            void Join(int start,int columns,int rows)
            {
                for(int r=0;r<rows;r++)for(int c=0;c<columns;c++)
                {int a=start+r*(columns+1)+c,b=a+columns+1;triangles.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            }
        }
    }
}
