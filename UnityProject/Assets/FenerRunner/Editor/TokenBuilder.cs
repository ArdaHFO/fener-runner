using UnityEngine;
using UnityEditor;
namespace FenerRunner.Editor
{
    internal static class TokenBuilder
    {
        public static GameObject Build()
        {
            return ChickCollectibleBuilder.Build();
        }
        public static Material SparkleMaterial()
        {
            string path=RunnerSceneBuilder.Generated+"/Rendering/Sparkle.png";
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float r=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))/31.5f;texture.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),2)));}
            texture.Apply();System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
            var m=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")){name="Golden sparks"};m.SetColor("_BaseColor",Color.white);m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.One);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;
            return RunnerSceneBuilder.Upsert(m,RunnerSceneBuilder.Generated+"/Materials/GoldenSparks.mat");
        }
    }
}
