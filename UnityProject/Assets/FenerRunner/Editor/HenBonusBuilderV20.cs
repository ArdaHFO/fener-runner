using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FenerRunner.Editor
{
    public static class HenBonusBuilderV20
    {
        public const string PrefabPath="Assets/FenerRunner/Resources/HenBonus.prefab";
        const string Generated="Assets/FenerRunner/Generated";
        static readonly Color[] Colours={new Color(1,.91f,.61f),new Color(1,.98f,.87f),new Color(.99f,.56f,.08f),
            new Color(.81f,.085f,.065f),new Color(.025f,.035f,.065f),Color.white,new Color(.93f,.73f,.27f)};
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before hen art preparation.");
            Directory.CreateDirectory("Assets/FenerRunner/Resources");
            string palettePath=Generated+"/Rendering/HenPalette.png";
            var palette=new Texture2D(Colours.Length*4,4,TextureFormat.RGBA32,false,false);
            for(int y=0;y<4;y++)for(int x=0;x<palette.width;x++)palette.SetPixel(x,y,Colours[x/4]);
            palette.Apply();File.WriteAllBytes(palettePath,palette.EncodeToPNG());UnityEngine.Object.DestroyImmediate(palette);
            AssetDatabase.ImportAsset(palettePath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(palettePath);
            importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("URP Lit is required.");
            var material=new Material(shader){name="Golden hen cream feathers",enableInstancing=true};
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(palettePath));material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Smoothness",.34f);material.SetFloat("_Metallic",.035f);
            material=RunnerSceneBuilder.Upsert(material,Generated+"/Materials/HenBonus.mat");
            var body=new Geometry();
            body.Egg(new Vector3(0,-.085f,.06f),new Vector3(.37f,.36f,.31f),Quaternion.identity,16,8,0);
            body.Egg(new Vector3(0,.27f,-.11f),new Vector3(.25f,.27f,.23f),Quaternion.identity,14,7,1);
            body.Egg(new Vector3(0,-.02f,-.20f),new Vector3(.25f,.26f,.06f),Quaternion.identity,12,6,1);
            for(int side=-1;side<=1;side+=2)
            {
                body.Egg(new Vector3(side*.104f,.31f,-.316f),new Vector3(.052f,.066f,.025f),Quaternion.identity,10,5,4);
                body.Egg(new Vector3(side*.09f,.33f,-.34f),new Vector3(.014f,.018f,.009f),Quaternion.identity,6,3,5);
                body.Egg(new Vector3(side*.125f,-.432f,-.03f),new Vector3(.03f,.058f,.026f),Quaternion.identity,6,3,2);
                for(int toe=-1;toe<=1;toe++)body.Egg(new Vector3(side*.12f+toe*.037f,-.47f,-.092f),new Vector3(.022f,.023f,.073f),Quaternion.Euler(0,-toe*17,0),6,3,2);
            }
            body.Egg(new Vector3(0,.221f,-.34f),new Vector3(.095f,.043f,.085f),Quaternion.identity,10,5,2);
            body.Egg(new Vector3(0,.14f,-.315f),new Vector3(.049f,.072f,.033f),Quaternion.identity,8,4,3);
            for(int i=0;i<3;i++)body.Egg(new Vector3(0,.53f+i*.006f,-.20f+i*.08f),new Vector3(.044f,.095f-(i%2)*.015f,.065f),Quaternion.Euler(-12,0,0),8,4,3);
            for(int i=-1;i<=1;i++)body.Egg(new Vector3(i*.11f,.11f,.33f),new Vector3(.085f,.23f,.052f),Quaternion.Euler(28,i*15,i*-20),8,4,6);
            var wing=new Geometry();wing.Egg(new Vector3(0,-.11f,0),new Vector3(.086f,.21f,.15f),Quaternion.Euler(0,0,0),12,6,6);
            for(int i=0;i<3;i++)wing.Egg(new Vector3(.025f,-.19f+i*.045f,-.06f+i*.045f),new Vector3(.066f,.105f,.05f),Quaternion.Euler(15,0,-10),8,4,1);
            Mesh bodyMesh=RunnerSceneBuilder.Upsert(body.Finish("Cream golden hen body"),Generated+"/Meshes/HenBody.asset");
            Mesh wingMesh=RunnerSceneBuilder.Upsert(wing.Finish("Hen soft feather wing"),Generated+"/Meshes/HenWing.asset");
            int triangles=(bodyMesh.triangles.Length+2*wingMesh.triangles.Length)/3;
            if(triangles>3500)throw new InvalidOperationException("Hen exceeds mobile geometry budget.");
            var root=new GameObject("Golden bonus hen");
            try
            {
                AddMesh(root,bodyMesh,material);
                for(int side=-1;side<=1;side+=2)
                {
                    var child=new GameObject(side<0?"Left wing":"Right wing");child.transform.SetParent(root.transform,false);
                    child.transform.localPosition=new Vector3(side*.335f,.02f,.015f);
                    child.transform.localScale=new Vector3(side,1,1);AddMesh(child,wingMesh,material);
                }
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();
                Debug.Log("V20 hen ready: "+triangles+" triangles, one shared material, animated wing pivots. No APK.");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static void AddMesh(GameObject owner,Mesh mesh,Material material)
        {
            owner.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=owner.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        sealed class Geometry
        {
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();readonly List<int> indices=new List<int>();
            public void Egg(Vector3 centre,Vector3 radii,Quaternion turn,int sides,int rings,int colour)
            {
                int first=vertices.Count;
                for(int ring=0;ring<=rings;ring++)
                {
                    float a=Mathf.PI*ring/rings,h=Mathf.Sin(a),v=Mathf.Cos(a);
                    for(int side=0;side<=sides;side++)
                    {
                        float angle=2*Mathf.PI*side/sides;Vector3 unit=new Vector3(h*Mathf.Cos(angle),v,h*Mathf.Sin(angle));
                        vertices.Add(centre+turn*Vector3.Scale(unit,radii));normals.Add(turn*new Vector3(unit.x/radii.x,unit.y/radii.y,unit.z/radii.z).normalized);
                        uv.Add(new Vector2((colour+.5f)/Colours.Length,.5f));
                    }
                }
                for(int ring=0;ring<rings;ring++)for(int side=0;side<sides;side++)
                {
                    int a=first+ring*(sides+1)+side,b=a+sides+1;
                    if(ring>0){indices.Add(a);indices.Add(a+1);indices.Add(b);}
                    if(ring<rings-1){indices.Add(a+1);indices.Add(b+1);indices.Add(b);}
                }
            }
            public Mesh Finish(string name)
            {
                var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);
                var tangents=new List<Vector4>(normals.Count);
                foreach(var normal in normals){Vector3 t=Vector3.Cross(Mathf.Abs(normal.y)<.98f?Vector3.up:Vector3.right,normal).normalized;tangents.Add(new Vector4(t.x,t.y,t.z,1));}
                mesh.SetTangents(tangents);mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
