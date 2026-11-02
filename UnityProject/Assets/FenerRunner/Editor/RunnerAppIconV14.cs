#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FenerRunner.Editor
{
    // Native editor asset generation only; never invokes a player build.
    // Native render of the shipped model. No AI image, new identity, gameplay
    // scene edit, APK build, or source-material mutation is involved.
    public static class RunnerAppIconV14
    {
        const int Size=1024;
        const string Generated="Assets/FenerRunner/Generated";
        const string PrefabPath=Generated+"/Prefabs/KartalPlayer.prefab";
        const string Art=Generated+"/Art";
        const string IconPath=Art+"/RunnerAppIcon.png";
        const string BackgroundPath=Art+"/RunnerAppIconBackground.png";
        const string ForegroundPath=Art+"/RunnerAppIconForeground.png";
        static readonly Color Navy=new Color(9f/255,23f/255,51f/255,1);
        static readonly Color Gold=new Color(246f/255,204f/255,57f/255,1);

        [Serializable] sealed class IconReport
        {
            public string scope="Unity-native render of the existing coach model; project icon assignments only. No APK or device launch test.";
            public string model=PrefabPath,pose="MenuPride",icon=IconPath,adaptiveBackground=BackgroundPath,adaptiveForeground=ForegroundPath;
            public int width=Size,height=Size,defaultSlots,legacySlots,roundSlots,adaptiveSlots;
            public float opaqueModelFraction,whiteJerseyFraction,goldenFeatherFraction;
            public bool passed,allAssigned,sourceDependenciesUnchanged,unitySplashHidden,unityLogoHidden;
            public string beforeSourceHash,afterSourceHash;
        }

        [MenuItem("Fener Runner/99 Render branded app icon (no APK)")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before generating the application icon.");
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
                throw new InvalidOperationException("The native model render needs a graphics device. Do not invoke with -nographics.");
            var legacy=PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,AndroidPlatformIconKind.Legacy);
            var round=PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,AndroidPlatformIconKind.Round);
            var adaptive=PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,AndroidPlatformIconKind.Adaptive);
            if(legacy.Length==0||round.Length==0||adaptive.Length==0)
                throw new InvalidOperationException("Android icon slots are unavailable. Install Android Build Support first.");
            if(adaptive.Any(slot=>slot.maxLayerCount!=2))
                throw new InvalidOperationException("Review the installed Android adaptive-icon layer contract before generating icons.");

            var report=new IconReport {beforeSourceHash=AssetDatabase.GetAssetDependencyHash(PrefabPath).ToString()};
            Texture2D native=null,foreground=null,background=null,composite=null;
            try
            {
                native=RenderExistingMascot();
                ValidateModelPixels(native,report);
                // Android adaptive layers are 108dp canvases. The entire mascot
                // stays inside the central 66dp safe circle, including its tuft.
                foreground=MakeForeground(native,.67f);
                background=MakeBackground(.307f,.010f);
                composite=Composite(native,MakeBackgroundPixels(.433f,.014f));
                Directory.CreateDirectory(Art);
                SaveTexture(composite,IconPath,false);
                SaveTexture(background,BackgroundPath,false);
                SaveTexture(foreground,ForegroundPath,true);
                var icon=AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
                var back=AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
                var front=AssetDatabase.LoadAssetAtPath<Texture2D>(ForegroundPath);
                if(icon==null||back==null||front==null)throw new InvalidOperationException("Generated icon import failed.");

                int defaultCount=PlayerSettings.GetIconSizes(NamedBuildTarget.Unknown,UnityEditor.IconKind.Any).Length;
                if(defaultCount==0)defaultCount=1;
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown,Enumerable.Repeat(icon,defaultCount).ToArray(),UnityEditor.IconKind.Any);
                Assign(legacy,icon);Assign(round,icon);
                // Verified in the installed 6000.3 AndroidPlatformIconKind
                // metadata: layer 0 is Background and layer 1 is Foreground.
                foreach(var slot in adaptive)slot.SetTextures(new[]{back,front});
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android,AndroidPlatformIconKind.Legacy,legacy);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android,AndroidPlatformIconKind.Round,round);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android,AndroidPlatformIconKind.Adaptive,adaptive);
                PlayerSettings.SplashScreen.show=false;
                PlayerSettings.SplashScreen.showUnityLogo=false;
                AssetDatabase.SaveAssets();

                report.defaultSlots=PlayerSettings.GetIcons(NamedBuildTarget.Unknown,UnityEditor.IconKind.Any).Length;
                report.legacySlots=legacy.Length;report.roundSlots=round.Length;report.adaptiveSlots=adaptive.Length;
                report.allAssigned=PlayerSettings.GetIcons(NamedBuildTarget.Unknown,UnityEditor.IconKind.Any).All(t=>t==icon)&&
                    CheckAssigned(AndroidPlatformIconKind.Legacy,icon)&&CheckAssigned(AndroidPlatformIconKind.Round,icon)&&
                    CheckAssigned(AndroidPlatformIconKind.Adaptive,back,front);
                report.afterSourceHash=AssetDatabase.GetAssetDependencyHash(PrefabPath).ToString();
                report.sourceDependenciesUnchanged=report.beforeSourceHash==report.afterSourceHash;
                report.unitySplashHidden=!PlayerSettings.SplashScreen.show;
                report.unityLogoHidden=!PlayerSettings.SplashScreen.showUnityLogo;
                report.passed=report.allAssigned&&report.sourceDependenciesUnchanged&&report.unitySplashHidden&&report.unityLogoHidden;
                string validation=Path.GetFullPath("../Validation");Directory.CreateDirectory(validation);
                File.WriteAllBytes(Path.Combine(validation,"v14-app-icon.png"),composite.EncodeToPNG());
                File.WriteAllText(Path.Combine(validation,"unity_app_icon_v14.json"),JsonUtility.ToJson(report,true));
                if(!report.allAssigned||!report.sourceDependenciesUnchanged)
                    throw new InvalidOperationException("Application icon validation failed; inspect unity_app_icon_v14.json.");
                Debug.Log("Fener Runner branded application icon rendered and assigned; no APK was built.");
            }
            finally
            {
                if(native!=null)Object.DestroyImmediate(native);
                if(foreground!=null)Object.DestroyImmediate(foreground);
                if(background!=null)Object.DestroyImmediate(background);
                if(composite!=null)Object.DestroyImmediate(composite);
            }
        }

        public static void BuildAndExit(){Build();EditorApplication.Exit(0);}

        static Texture2D RenderExistingMascot()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var source=prefab!=null?prefab.transform.Find("Motion accents/Visual Only"):null;
            var pose=AssetDatabase.LoadAssetAtPath<AnimationClip>(Generated+"/Animations/MenuPride.anim");
            if(source==null||pose==null)throw new InvalidOperationException("Generate KartalPlayer and MenuPride before rendering the application icon.");
            var preview=new PreviewRenderUtility();
            GameObject visual=null;
            try
            {
                visual=Object.Instantiate(source.gameObject);
                visual.name="Application icon preview only";visual.hideFlags=HideFlags.HideAndDontSave;
                visual.transform.SetPositionAndRotation(source.localPosition,source.localRotation);
                visual.transform.localScale=source.localScale;
                // The visual hierarchy contains the mesh and Animator, not the
                // production Motor/RunnerAnimation/controller root.
                foreach(var script in visual.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(script);
                foreach(var collider in visual.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
                var animator=visual.GetComponentInChildren<Animator>();
                if(animator==null)throw new InvalidOperationException("The icon model has no animation root.");
                animator.enabled=false;pose.SampleAnimation(animator.gameObject,pose.length*.43f);
                foreach(var renderer in visual.GetComponentsInChildren<Renderer>())
                {renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;}
                var skin=visual.GetComponentInChildren<SkinnedMeshRenderer>();
                var head=skin.bones.First(b=>b!=null&&b.name=="head");
                // Head and white knit chest fill the emblem. The actual source
                // silhouette is retained; no facial or material changes occur.
                Vector3 target=head.position-Vector3.up*.18f;
                preview.AddSingleGO(visual);visual=null; // preview now owns it
                var camera=preview.camera;
                camera.orthographic=true;camera.orthographicSize=.91f;
                camera.nearClipPlane=.05f;camera.farClipPlane=20;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
                camera.allowHDR=false;camera.allowMSAA=true;
                camera.transform.position=target+new Vector3(.16f,.055f,5f);
                camera.transform.LookAt(target);
                preview.ambientColor=new Color(.40f,.43f,.49f,1);
                preview.lights[0].intensity=1.3f;
                preview.lights[0].color=new Color(1f,.95f,.84f,1);
                preview.lights[0].transform.rotation=Quaternion.Euler(35,-145,0);
                preview.lights[1].intensity=.65f;
                preview.lights[1].color=new Color(.70f,.81f,1,1);
                preview.lights[1].transform.rotation=Quaternion.Euler(15,40,0);
                preview.BeginPreview(new Rect(0,0,Size,Size),GUIStyle.none);
                preview.Render(true,false);
                Texture rendered=preview.EndPreview();
                var result=ReadSrgb(rendered);
                // A circular alpha edge gives the white torso a designed crop,
                // and leaves clear padding for the Android adaptive safe zone.
                Color[] pixels=result.GetPixels();
                for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
                {
                    float radius=new Vector2((x+.5f)/Size-.5f,(y+.5f)/Size-.5f).magnitude;
                    int index=y*Size+x;Color color=pixels[index];
                    color.a*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.427f,.435f,radius));pixels[index]=color;
                }
                result.SetPixels(pixels);result.Apply(false,false);return result;
            }
            finally{if(visual!=null)Object.DestroyImmediate(visual);preview.Cleanup();}
        }

        static Texture2D ReadSrgb(Texture source)
        {
            RenderTexture previous=RenderTexture.active;
            bool oldSrgb=GL.sRGBWrite;
            var target=RenderTexture.GetTemporary(Size,Size,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            try
            {
                GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;
                Graphics.Blit(source,target);RenderTexture.active=target;
                var result=new Texture2D(Size,Size,TextureFormat.RGBA32,false,false);
                result.ReadPixels(new Rect(0,0,Size,Size),0,0,false);result.Apply(false,false);return result;
            }
            finally{GL.sRGBWrite=oldSrgb;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
        }
        static Texture2D MakeForeground(Texture2D native,float scale)
        {
            var pixels=new Color[Size*Size];
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
            {
                float u=((x+.5f)/Size-.5f)/scale+.5f,v=((y+.5f)/Size-.5f)/scale+.5f;
                pixels[y*Size+x]=u>=0&&u<=1&&v>=0&&v<=1?native.GetPixelBilinear(u,v):Color.clear;
            }
            return Texture(pixels);
        }
        static Texture2D MakeBackground(float ringRadius,float ringWidth)=>Texture(MakeBackgroundPixels(ringRadius,ringWidth));
        static Color[] MakeBackgroundPixels(float ringRadius,float ringWidth)
        {
            var pixels=new Color[Size*Size];
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
            {
                Vector2 p=new Vector2((x+.5f)/Size-.5f,(y+.5f)/Size-.5f);float r=p.magnitude;
                Color blue=Color.Lerp(Navy,new Color(.065f,.17f,.32f,1),Mathf.Clamp01(1-r*1.7f));
                float ring=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(ringWidth*.5f,ringWidth*.5f+.002f,Mathf.Abs(r-ringRadius)));
                pixels[y*Size+x]=Color.Lerp(blue,Gold,ring);
            }
            return pixels;
        }
        static Texture2D Composite(Texture2D model,Color[] background)
        {
            Color[] pixels=model.GetPixels();
            for(int i=0;i<pixels.Length;i++){Color color=Color.Lerp(background[i],pixels[i],pixels[i].a);color.a=1;pixels[i]=color;}
            return Texture(pixels);
        }
        static Texture2D Texture(Color[] pixels)
        {var result=new Texture2D(Size,Size,TextureFormat.RGBA32,false,false);result.SetPixels(pixels);result.Apply(false,false);return result;}
        static void SaveTexture(Texture2D texture,string path,bool transparent)
        {
            File.WriteAllBytes(path,texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=transparent;
            importer.mipmapEnabled=false;importer.isReadable=false;importer.maxTextureSize=Size;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
        static void Assign(PlatformIcon[] slots,Texture2D texture)
        {foreach(var slot in slots)slot.SetTextures(new[]{texture});}
        static bool CheckAssigned(PlatformIconKind kind,params Texture2D[] expected)
        {
            var slots=PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,kind);
            return slots.Length>0&&slots.All(slot=>slot.GetTextures().SequenceEqual(expected));
        }
        static void ValidateModelPixels(Texture2D texture,IconReport report)
        {
            Color[] pixels=texture.GetPixels();int visible=0,white=0,golden=0;
            foreach(Color p in pixels)
            {
                if(p.a<.5f)continue;visible++;
                if(p.r>.48f&&p.g>.48f&&p.b>.43f&&Mathf.Max(p.r,Mathf.Max(p.g,p.b))-Mathf.Min(p.r,Mathf.Min(p.g,p.b))<.22f)white++;
                if(p.r>.40f&&p.g>.25f&&p.r>p.b*1.35f&&p.g>p.b*1.18f)golden++;
            }
            report.opaqueModelFraction=(float)visible/pixels.Length;
            report.whiteJerseyFraction=(float)white/pixels.Length;
            report.goldenFeatherFraction=(float)golden/pixels.Length;
            if(visible<pixels.Length*.06f||white<pixels.Length*.008f||golden<pixels.Length*.008f)
                throw new InvalidOperationException("Native mascot render is empty, dark, or missing its white/gold identity. Review the render before assigning icons.");
        }
    }
}
#endif
