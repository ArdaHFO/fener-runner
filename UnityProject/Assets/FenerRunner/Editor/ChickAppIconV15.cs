using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace FenerRunner.Editor
{
    public static class ChickAppIconV15
    {
        public const string IconPath="Assets/FenerRunner/Art/ChickAppIcon-v15.png";
        public const string AdaptivePath="Assets/FenerRunner/Art/ChickAppIcon-v15-Adaptive.png";
        [Serializable] sealed class Report
        {public bool passed;public string utc,icon=IconPath,adaptive=AdaptivePath;public int defaultSlots,legacySlots,roundSlots,adaptiveSlots;public string source="Built-in ImageGen; unchanged source PNGs and prompts in SourceAssets/Branding. Native icon assignments only, no APK/device test.";}
        public static void Build()
        {
            var icon=Import(IconPath);var padded=Import(AdaptivePath);
            string backgroundPath="Assets/FenerRunner/Generated/Art/ChickIconBackground.png";
            var background=new Texture2D(4,4,TextureFormat.RGBA32,false);
            background.SetPixels(Enumerable.Repeat(new Color(.015f,.035f,.13f,1),16).ToArray());background.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(backgroundPath));File.WriteAllBytes(backgroundPath,background.EncodeToPNG());UnityEngine.Object.DestroyImmediate(background);
            var back=Import(backgroundPath);
            int count=Math.Max(1,PlayerSettings.GetIconSizes(NamedBuildTarget.Unknown,UnityEditor.IconKind.Any).Length);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown,Enumerable.Repeat(icon,count).ToArray(),UnityEditor.IconKind.Any);
            var legacy=PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,AndroidPlatformIconKind.Legacy);
            var round=PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,AndroidPlatformIconKind.Round);
            var adaptive=PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,AndroidPlatformIconKind.Adaptive);
            foreach(var slot in legacy)slot.SetTextures(new[]{icon});
            foreach(var slot in round)slot.SetTextures(new[]{icon});
            foreach(var slot in adaptive)slot.SetTextures(new[]{back,padded});
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android,AndroidPlatformIconKind.Legacy,legacy);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android,AndroidPlatformIconKind.Round,round);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android,AndroidPlatformIconKind.Adaptive,adaptive);
            PlayerSettings.SplashScreen.show=false;PlayerSettings.SplashScreen.showUnityLogo=false;
            AssetDatabase.SaveAssets();
            bool valid=count>0&&legacy.Length>0&&round.Length>0&&adaptive.Length>0&&
                PlayerSettings.GetIcons(NamedBuildTarget.Unknown,UnityEditor.IconKind.Any).All(t=>t==icon)&&
                Assigned(AndroidPlatformIconKind.Legacy,icon)&&Assigned(AndroidPlatformIconKind.Round,icon)&&Assigned(AndroidPlatformIconKind.Adaptive,back,padded);
            var report=new Report{passed=valid,utc=DateTime.UtcNow.ToString("O"),defaultSlots=count,legacySlots=legacy.Length,roundSlots=round.Length,adaptiveSlots=adaptive.Length};
            Directory.CreateDirectory("../Validation");File.Copy(IconPath,"../Validation/v15-app-icon.png",true);
            File.WriteAllText("../Validation/unity_app_icon_v15.json",JsonUtility.ToJson(report,true));
            if(!valid)throw new InvalidOperationException("Chick application icon assignment failed.");
        }
        static Texture2D Import(string path)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;importer.sRGBTexture=true;
            importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=false;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static bool Assigned(PlatformIconKind kind,params Texture2D[] expected)=>
            PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,kind).All(s=>s.GetTextures().SequenceEqual(expected));
    }
}
