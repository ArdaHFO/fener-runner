using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FenerRunner.Editor
{
    public static class CrowdPropBuilderV19
    {
        const string Generated="Assets/FenerRunner/Generated";
        public const string LibraryPath="Assets/FenerRunner/Resources/CrowdPropLibrary.asset";
        public static void Build()
        {
            CrowdBottleBuilder.Build();
            string palettePath=Generated+"/Rendering/CrowdPropPalette.png";
            var palette=new Texture2D(24,4,TextureFormat.RGBA32,false,false);
            Color[] colours={new Color(.93f,.91f,.82f),new Color(.045f,.085f,.16f),new Color(1,.77f,.075f),new Color(.65f,.61f,.52f),new Color(.99f,.98f,.92f),new Color(.27f,.32f,.38f)};
            for(int y=0;y<4;y++)for(int x=0;x<24;x++)palette.SetPixel(x,y,colours[x/4]);
            palette.Apply();File.WriteAllBytes(palettePath,palette.EncodeToPNG());UnityEngine.Object.DestroyImmediate(palette);
            AssetDatabase.ImportAsset(palettePath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(palettePath);importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=false;importer.SaveAndReimport();
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Shared stadium litter",enableInstancing=true,renderQueue=(int)RenderQueue.Transparent};
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(palettePath));material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.18f);material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");
            material=RunnerSceneBuilder.Upsert(material,Generated+"/Materials/CrowdProps.mat");
            var library=ScriptableObject.CreateInstance<CrowdPropLibrary>();library.name="Shared crowd prop library";
            library.meshes=new[]{AssetDatabase.LoadAssetAtPath<Mesh>(Generated+"/Meshes/CrowdBottle.asset"),Cup(),Paper(),Ball()};
            library.materials=new[]{AssetDatabase.LoadAssetAtPath<Material>(Generated+"/Materials/CrowdBottle.mat"),material,material,material};
            for(int i=1;i<4;i++)
            {
                if(library.meshes[i].triangles.Length/3>1400)throw new InvalidOperationException("Crowd prop triangle budget exceeded.");
                library.meshes[i]=RunnerSceneBuilder.Upsert(library.meshes[i],Generated+"/Meshes/Crowd"+(CrowdPropKind)i+".asset");
            }
            RunnerSceneBuilder.Upsert(library,LibraryPath);AssetDatabase.SaveAssets();
            Debug.Log("V19 four shared crowd prop kinds prepared without colliders or player build.");
        }
        static Mesh Cup()
        {
            var b=new Geometry();
            // Open, tapered stadium cup with a rolled lip and a visible dark
            // interior. A stripe reads at portrait-gameplay distance.
            b.Lathe(new[]{new Vector2(-.17f,.077f),new Vector2(-.155f,.082f),new Vector2(-.085f,.093f),new Vector2(.08f,.118f),new Vector2(.16f,.129f),new Vector2(.17f,.126f)},16,0,false);
            b.Lathe(new[]{new Vector2(-.155f,.072f),new Vector2(.16f,.119f),new Vector2(.17f,.126f)},16,3,true);
            b.Lathe(new[]{new Vector2(-.054f,.098f),new Vector2(.055f,.115f)},16,1,false);
            b.Lathe(new[]{new Vector2(-.047f,.100f),new Vector2(-.031f,.1025f)},16,2,false);
            b.Lathe(new[]{new Vector2(.13f,.125f),new Vector2(.145f,.133f),new Vector2(.16f,.134f),new Vector2(.17f,.126f)},16,4,false);
            b.Disc(-.17f,.077f,16,0,false);b.Disc(-.155f,.072f,16,3,true);
            return b.Finish("Tapered paper cup",.76f);
        }
        static Mesh Paper()
        {
            var b=new Geometry();const int sides=10,rings=6;
            // Flat-shaded, irregular creases make paper distinct from the round
            // foam ball. Every vertex lies inside a palm-sized finite envelope.
            Vector3 Point(int y,int x)
            {
                x%=sides;float a=y*Mathf.PI/rings,z=x*Mathf.PI*2/sides;
                float crease=Mathf.Sin(a);
                float r=.11f*(1+crease*(.15f*Mathf.Sin(x*2.7f+y*4.13f)+.07f*Mathf.Cos(x*5.1f-y*.73f)));
                return new Vector3(Mathf.Sin(a)*Mathf.Cos(z)*r,Mathf.Cos(a)*r*.84f,Mathf.Sin(a)*Mathf.Sin(z)*r*.92f);
            }
            for(int y=0;y<rings;y++)for(int x=0;x<sides;x++)
            {
                int colour=(x+y*3)%7==0?1:(x+y)%3==0?3:4;
                b.Triangle(Point(y,x),Point(y,x+1),Point(y+1,x),colour);
                b.Triangle(Point(y,x+1),Point(y+1,x+1),Point(y+1,x),colour);
            }
            return b.Finish("Crumpled paper");
        }
        static Mesh Ball()
        {
            var b=new Geometry();const int sides=12,rings=8;
            Vector3 Point(int y,int x){float a=y*Mathf.PI/rings,z=x*Mathf.PI*2/sides;return new Vector3(Mathf.Sin(a)*Mathf.Cos(z),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(z))*.145f;}
            for(int y=0;y<rings;y++)for(int x=0;x<sides;x++)
            {
                int colour=((x/2+y/2)%3==0)?1:2;
                b.SmoothTriangle(Point(y,x),Point(y,x+1),Point(y+1,x),colour);
                b.SmoothTriangle(Point(y,x+1),Point(y+1,x+1),Point(y+1,x),colour);
            }
            return b.Finish("Soft fan foam football");
        }
        sealed class Geometry
        {
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Vector2> uvs=new List<Vector2>();readonly List<int> indices=new List<int>();
            void Vertex(Vector3 p,Vector3 n,int colour){vertices.Add(p);normals.Add(n);uvs.Add(new Vector2((colour+.5f)/6,.5f));}
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,int colour)
            {Vector3 n=Vector3.Cross(b-a,c-a).normalized;if(n.sqrMagnitude<.5f)return;int start=vertices.Count;Vertex(a,n,colour);Vertex(b,n,colour);Vertex(c,n,colour);indices.AddRange(new[]{start,start+1,start+2});}
            public void SmoothTriangle(Vector3 a,Vector3 b,Vector3 c,int colour)
            {if(Vector3.Cross(b-a,c-a).sqrMagnitude<.00000001f)return;int start=vertices.Count;Vertex(a,a.normalized,colour);Vertex(b,b.normalized,colour);Vertex(c,c.normalized,colour);indices.AddRange(new[]{start,start+1,start+2});}
            public void Lathe(Vector2[] profile,int sides,int colour,bool inner)
            {
                int start=vertices.Count;
                for(int y=0;y<profile.Length;y++)
                {
                    Vector2 slope=(profile[Mathf.Min(profile.Length-1,y+1)]-profile[Mathf.Max(0,y-1)]).normalized;
                    for(int x=0;x<=sides;x++)
                    {
                        float angle=x*Mathf.PI*2/sides;Vector3 radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                        Vertex(radial*profile[y].y+Vector3.up*profile[y].x,(radial*slope.x-Vector3.up*slope.y).normalized*(inner?-1:1),colour);
                    }
                }
                for(int y=0;y<profile.Length-1;y++)for(int x=0;x<sides;x++)
                {int a=start+y*(sides+1)+x,b=a+sides+1;indices.AddRange(inner?new[]{a,a+1,b,a+1,b+1,b}:new[]{a,b,a+1,a+1,b,b+1});}
            }
            public void Disc(float y,float radius,int sides,int colour,bool up)
            {for(int x=0;x<sides;x++){float a=x*Mathf.PI*2/sides,b=(x+1)*Mathf.PI*2/sides;Vector3 p=new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius),q=new Vector3(Mathf.Cos(b)*radius,y,Mathf.Sin(b)*radius);if(up)Triangle(Vector3.up*y,q,p,colour);else Triangle(Vector3.up*y,p,q,colour);}}
            public Mesh Finish(string name,float scale=1){if(scale!=1)for(int i=0;i<vertices.Count;i++)vertices[i]*=scale;var m=new Mesh{name=name};m.SetVertices(vertices);m.SetNormals(normals);m.SetUVs(0,uvs);m.SetTriangles(indices,0);m.RecalculateBounds();return m;}
        }
    }
}
