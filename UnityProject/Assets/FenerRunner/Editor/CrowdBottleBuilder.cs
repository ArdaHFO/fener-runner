using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FenerRunner.Editor
{
    public static class CrowdBottleBuilder
    {
        const string Root="Assets/FenerRunner/Generated";
        public const string PrefabPath="Assets/FenerRunner/Resources/CrowdBottle.prefab";
        public const string PalettePath=Root+"/Rendering/BottlePalette.png";
        public static GameObject Build()
        {
            Directory.CreateDirectory(Root+"/Meshes");Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Rendering");Directory.CreateDirectory("Assets/FenerRunner/Resources");
            var colours=new[]{new Color(.64f,.85f,.91f,.40f),new Color(.38f,.69f,.82f,.30f),new Color(.025f,.08f,.20f,1),new Color(1,.79f,.08f,1),new Color(.87f,.95f,.98f,1)};
            var palette=new Texture2D(20,4,TextureFormat.RGBA32,false,false);
            for(int y=0;y<4;y++)for(int x=0;x<20;x++)palette.SetPixel(x,y,colours[x/4]);
            palette.Apply();File.WriteAllBytes(PalettePath,palette.EncodeToPNG());UnityEngine.Object.DestroyImmediate(palette);
            AssetDatabase.ImportAsset(PalettePath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(PalettePath);importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=false;importer.alphaIsTransparency=false;importer.SaveAndReimport();
            var mesh=new Geometry();
            // A pinched neck, five ribbed PET rings and separate screw cap give a
            // recognizable real bottle silhouette even when only a few pixels wide.
            mesh.Lathe(new[]{new Vector2(-.205f,.046f),new Vector2(-.194f,.071f),new Vector2(-.175f,.082f),new Vector2(-.148f,.078f),new Vector2(-.134f,.085f),new Vector2(-.123f,.077f),new Vector2(-.080f,.081f),new Vector2(.075f,.081f),new Vector2(.09f,.077f),new Vector2(.102f,.084f),new Vector2(.114f,.077f),new Vector2(.131f,.076f),new Vector2(.153f,.058f),new Vector2(.17f,.035f),new Vector2(.195f,.034f)},16,0);
            mesh.Lathe(new[]{new Vector2(-.17f,.066f),new Vector2(.025f,.071f)},12,1);
            mesh.Lathe(new[]{new Vector2(-.067f,.083f),new Vector2(.063f,.083f)},16,2);
            mesh.Lathe(new[]{new Vector2(-.060f,.084f),new Vector2(-.047f,.084f)},16,3);
            mesh.Lathe(new[]{new Vector2(.041f,.084f),new Vector2(.054f,.084f)},16,3);
            // Four small white label marks, native geometry rather than unreadable tiny text.
            for(int face=0;face<4;face++)for(int line=0;line<3;line++)
                mesh.LabelMark(face*Mathf.PI*.5f,-.017f+line*.014f,line==1?.045f:.033f,4);
            mesh.Lathe(new[]{new Vector2(.184f,.039f),new Vector2(.191f,.044f),new Vector2(.218f,.044f),new Vector2(.224f,.037f)},20,3);
            var asset=mesh.Finish();if(asset.triangles.Length/3>1400)throw new InvalidOperationException("Bottle exceeded its mobile triangle budget.");
            asset=RunnerSceneBuilder.Upsert(asset,Root+"/Meshes/CrowdBottle.asset");
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Stadium PET bottle",enableInstancing=true,renderQueue=(int)RenderQueue.Transparent};
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath));material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Metallic",.02f);material.SetFloat("_Smoothness",.72f);material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");
            material=RunnerSceneBuilder.Upsert(material,Root+"/Materials/CrowdBottle.mat");
            var go=new GameObject("PET stadium bottle",typeof(MeshFilter),typeof(MeshRenderer));
            try
            {
                go.GetComponent<MeshFilter>().sharedMesh=asset;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                GameObject prefab=PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);AssetDatabase.SaveAssets();Debug.Log("Visual crowd bottle prepared: "+asset.triangles.Length/3+" triangles, one shared mesh/material, no colliders.");return prefab;
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        sealed class Geometry
        {
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();readonly List<Vector2> uvs=new List<Vector2>();readonly List<int> indices=new List<int>();
            void Vertex(Vector3 p,Vector3 n,int colour){vertices.Add(p);normals.Add(n);uvs.Add(new Vector2((colour+.5f)/5,.5f));}
            public void Lathe(Vector2[] profile,int sides,int colour)
            {
                int start=vertices.Count;
                for(int y=0;y<profile.Length;y++)
                {
                    Vector2 previous=profile[Mathf.Max(0,y-1)],next=profile[Mathf.Min(profile.Length-1,y+1)],slope=(next-previous).normalized;
                    for(int x=0;x<=sides;x++){float a=x*2*Mathf.PI/sides;Vector3 radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));Vertex(radial*profile[y].y+Vector3.up*profile[y].x,(radial*slope.x-Vector3.up*slope.y).normalized,colour);}
                }
                for(int y=0;y<profile.Length-1;y++)for(int x=0;x<sides;x++){int a=start+y*(sides+1)+x,b=a+sides+1;indices.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
                Cap(profile[0],sides,colour,false);Cap(profile[profile.Length-1],sides,colour,true);
            }
            void Cap(Vector2 ring,int sides,int colour,bool up)
            {int c=vertices.Count;Vector3 n=up?Vector3.up:Vector3.down;Vertex(Vector3.up*ring.x,n,colour);for(int x=0;x<=sides;x++){float a=x*2*Mathf.PI/sides;Vertex(new Vector3(Mathf.Cos(a)*ring.y,ring.x,Mathf.Sin(a)*ring.y),n,colour);}for(int x=0;x<sides;x++){indices.Add(c);indices.Add(c+1+(up?x+1:x));indices.Add(c+1+(up?x:x+1));}}
            public void LabelMark(float angle,float y,float width,int colour)
            {Vector3 n=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)),t=new Vector3(-n.z,0,n.x);int start=vertices.Count;foreach(var uv in new[]{new Vector2(-1,-1),new Vector2(-1,1),new Vector2(1,1),new Vector2(1,-1)})Vertex(n*.085f+t*(uv.x*width*.5f)+Vector3.up*(y+uv.y*.0025f),n,colour);indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});}
            public Mesh Finish()
            {var mesh=new Mesh{name="Ribbed PET bottle"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;}
        }
    }
}
