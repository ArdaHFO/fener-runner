using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace FenerRunner.Editor
{
    // One instanced mesh/material per collectible, including eyes, catchlights,
    // beak, toes, wings and tuft. Colour comes from a tiny palette atlas so the
    // 48 normal + 20 bonus pool never pays several material draws per chick.
    public static class ChickCollectibleBuilder
    {
        public const string MeshPath="Assets/FenerRunner/Generated/Meshes/Token.asset";
        public const string MaterialPath="Assets/FenerRunner/Generated/Materials/TokenGold.mat";
        public const string PrefabPath="Assets/FenerRunner/Generated/Prefabs/SupporterToken.prefab";
        public const string PalettePath="Assets/FenerRunner/Generated/Rendering/ChickPalette.png";
        public static float LegacyVisualRadius=>Mathf.Sqrt(.34f*.34f+.06f*.06f);
        static readonly Color[] Colours={
            new Color(1f,.79f,.13f),new Color(1f,.90f,.39f),
            new Color(1f,.43f,.055f),new Color(.018f,.029f,.055f),
            new Color(1f,.985f,.90f),new Color(.94f,.64f,.065f),
            new Color(.83f,.27f,.025f),new Color(.23f,.095f,.025f)
        };

        [MenuItem("Fener Runner/100 - Build 3D chick collectibles (no APK)")]
        public static void Rebuild(){Build();}
        public static GameObject Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before rebuilding collectible art.");
            Directory.CreateDirectory(Path.GetDirectoryName(MeshPath));
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            Directory.CreateDirectory(Path.GetDirectoryName(PalettePath));
            Texture2D palette=BuildPalette();
            var art=new MeshWriter();
            // The camera approaches from -Z. The chick always faces that side;
            // gameplay applies a small friendly wobble instead of a full spin.
            art.Egg(new Vector3(0,-.092f,.031f),new Vector3(.177f,.182f,.145f),Quaternion.identity,14,8,0);
            art.Egg(new Vector3(0,.105f,-.024f),new Vector3(.219f,.188f,.172f),Quaternion.identity,16,8,0);
            art.Egg(new Vector3(0,-.105f,-.103f),new Vector3(.130f,.132f,.038f),Quaternion.identity,10,5,1);
            for(int side=-1;side<=1;side+=2)
            {
                // Plump wings and three separate rounded toes remain readable
                // when the whole token is only a few dozen screen pixels high.
                art.Egg(new Vector3(side*.195f,-.074f,.015f),new Vector3(.068f,.126f,.054f),Quaternion.Euler(0,side*8,side*22),10,5,5);
                art.Egg(new Vector3(side*.085f,.129f,-.184f),new Vector3(.057f,.074f,.028f),Quaternion.Euler(0,-side*13,0),10,4,3);
                art.Egg(new Vector3(side*.073f,.153f,-.212f),new Vector3(.014f,.019f,.010f),Quaternion.identity,6,3,4);
                art.Egg(new Vector3(side*.087f,-.264f,-.003f),new Vector3(.022f,.043f,.021f),Quaternion.identity,6,3,2);
                for(int toe=-1;toe<=1;toe++)
                    art.Egg(new Vector3(side*.088f+toe*.026f,-.288f,-.046f),new Vector3(.018f,.020f,.066f),Quaternion.Euler(0,-toe*14,0),6,3,2);
            }
            // A shallow dark mouth seam separates the two orange bill volumes.
            art.Egg(new Vector3(0,.065f,-.212f),new Vector3(.080f,.039f,.074f),Quaternion.identity,10,5,2);
            art.Egg(new Vector3(0,.039f,-.214f),new Vector3(.071f,.026f,.061f),Quaternion.identity,10,4,6);
            art.Egg(new Vector3(0,.047f,-.253f),new Vector3(.063f,.0055f,.027f),Quaternion.identity,6,3,7);
            art.Egg(new Vector3(-.035f,.293f,.015f),new Vector3(.030f,.062f,.018f),Quaternion.Euler(-14,0,-24),8,4,1);
            art.Egg(new Vector3(.014f,.309f,.010f),new Vector3(.026f,.063f,.016f),Quaternion.Euler(-11,0,13),8,4,0);
            art.Egg(new Vector3(.057f,.288f,.008f),new Vector3(.025f,.046f,.016f),Quaternion.Euler(-14,0,43),8,4,1);

            Mesh mesh=art.Finish(LegacyVisualRadius);
            int triangles=mesh.triangles.Length/3;
            if(triangles>1500)throw new InvalidOperationException("Chick collectible exceeds its mobile triangle budget.");
            mesh=RunnerSceneBuilder.Upsert(mesh,MeshPath);
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)throw new InvalidOperationException("The project's URP Lit shader is required for chick art.");
            var material=new Material(shader){name="Chick golden feathers",enableInstancing=true};
            material.SetTexture("_BaseMap",palette);material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Metallic",.055f);material.SetFloat("_Smoothness",.36f);
            material.EnableKeyword("_EMISSION");material.SetTexture("_EmissionMap",palette);
            material.SetColor("_EmissionColor",new Color(.065f,.065f,.065f));
            material=RunnerSceneBuilder.Upsert(material,MaterialPath);
            var root=new GameObject("Golden supporter chick",typeof(MeshFilter),typeof(MeshRenderer));
            try
            {
                root.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("3D chick collectible rebuilt: "+triangles+" triangles, one instanced material, unchanged "+(LegacyVisualRadius+.04f).ToString("F6")+" m pickup radius.");
                return prefab;
            }
            finally{Object.DestroyImmediate(root);}
        }

        static Texture2D BuildPalette()
        {
            var image=new Texture2D(Colours.Length*4,4,TextureFormat.RGBA32,false,false);
            for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++)image.SetPixel(x,y,Colours[x/4]);
            image.Apply();File.WriteAllBytes(PalettePath,image.EncodeToPNG());Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(PalettePath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(PalettePath);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
            importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.isReadable=false;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);
        }

        sealed class MeshWriter
        {
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();readonly List<int> indices=new List<int>();
            public void Egg(Vector3 centre,Vector3 radii,Quaternion turn,int sides,int rings,int colour)
            {
                int first=vertices.Count;
                for(int ring=0;ring<=rings;ring++)
                {
                    float a=Mathf.PI*ring/rings,vertical=Mathf.Cos(a),horizontal=Mathf.Sin(a);
                    for(int side=0;side<=sides;side++)
                    {
                        float theta=2*Mathf.PI*side/sides;
                        Vector3 unit=new Vector3(horizontal*Mathf.Cos(theta),vertical,horizontal*Mathf.Sin(theta));
                        vertices.Add(centre+turn*Vector3.Scale(unit,radii));
                        normals.Add(turn*new Vector3(unit.x/radii.x,unit.y/radii.y,unit.z/radii.z).normalized);
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
            public Mesh Finish(float envelope)
            {
                float maximum=0;foreach(var v in vertices)maximum=Mathf.Max(maximum,v.magnitude);
                if(maximum<=0)throw new InvalidOperationException("Empty chick geometry.");
                float scale=envelope/maximum;
                for(int i=0;i<vertices.Count;i++)vertices[i]*=scale;
                var mesh=new Mesh{name="Golden supporter chick - single material"};
                mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);
                // Each component samples one palette texel, so a UV derivative
                // cannot define its tangent. Supply a finite orthogonal basis.
                var tangents=new List<Vector4>(normals.Count);
                foreach(Vector3 normal in normals)
                {
                    Vector3 axis=Mathf.Abs(normal.y)<.98f?Vector3.up:Vector3.right;
                    Vector3 tangent=Vector3.Cross(axis,normal).normalized;
                    tangents.Add(new Vector4(tangent.x,tangent.y,tangent.z,1));
                }
                mesh.SetTangents(tangents);mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
