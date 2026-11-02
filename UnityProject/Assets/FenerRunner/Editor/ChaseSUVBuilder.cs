using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FenerRunner.Editor
{
    /// <summary>Original mesh-authored luxury SUV. All curved geometry and materials are shared assets.</summary>
    internal static class ChaseSUVBuilder
    {
        private const string Root = RunnerSceneBuilder.Generated;
        private const string PrefabPath = "Assets/FenerRunner/Resources/ChaseSUV.prefab";
        private const string LetteringPath = "Assets/FenerRunner/Art/SUVPresidentLettering.png";
        private enum Surface { Paint, Highlight, Glass, Trim, Rubber, Alloy, Gold, White, Red, Lettering }

        internal static GameObject Build()=>BuildVehicle(ChaseVehicleKind.RangeRover);
        internal static void BuildAll()
        {
            for(int i=0;i<RunWallet.VehicleCount;i++)BuildVehicle((ChaseVehicleKind)i);
            BuildPreviews();
        }
        private static GameObject BuildVehicle(ChaseVehicleKind kind)
        {
            Directory.CreateDirectory(Root + "/Meshes");
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory("Assets/FenerRunner/Resources");
            AssetDatabase.Refresh();
            var materials = new[]
            {
                Material("Obsidian", new Color(.016f,.016f,.016f),.48f,.78f),
                Material("Edge reflection", new Color(.085f,.085f,.085f),.65f,.7f),
                Material("Smoked panoramic glass", new Color(.038f,.043f,.048f),.68f,.93f),
                Material("Satin graphite", new Color(.027f,.027f,.027f),.25f,.35f),
                Material("Tire rubber", new Color(.018f,.018f,.018f),.02f,.25f),
                Material("Brushed alloy", new Color(.53f,.61f,.68f),.82f,.66f),
                Material("Coachline gold", new Color(.88f,.67f,.25f),.64f,.66f),
                Material("Daylight LED", new Color(.76f,.9f,1f),.15f,.6f,new Color(.68f,.83f,1f)*2.3f),
                Material("Tail LED", new Color(.66f,.018f,.035f),.2f,.65f,new Color(1f,.025f,.035f)*1.45f),
                LetteringMaterial()
            };
            string resource=RunnerShopCatalog.VehicleResource(kind);
            if(kind==ChaseVehicleKind.SportCoupe)materials[0]=Material("GT metallic navy",new Color(.025f,.075f,.19f),.7f,.84f);
            if(kind==ChaseVehicleKind.Trail4x4)materials[0]=Material("Trail warm ivory",new Color(.72f,.67f,.52f),.32f,.5f);
            var root = new GameObject(resource);
            try
            {
                Transform suspension = Child(root.transform, "Suspension", Vector3.zero);
                var body = new Geometry(materials.Length);
                if(kind==ChaseVehicleKind.RangeRover){BuildBody(body);BuildDetails(body);}
                else if(kind==ChaseVehicleKind.SportCoupe)BuildCoupe(body);
                else BuildOffroad(body);
                Mesh bodyMesh = Save(body, resource+"Body");
                Render(suspension, "Coachwork", bodyMesh, materials);
                var wheel = new Geometry(3);
                BuildWheel(wheel);
                Mesh wheelMesh = Save(wheel, "ChaseSUVWheel");
                Material[] wheelMaterials = { materials[(int)Surface.Rubber], materials[(int)Surface.Alloy], materials[(int)Surface.Trim] };
                for (int axle = 0; axle < 2; axle++)
                    for (int side = 0; side < 2; side++)
                    {
                        string name = (axle == 0 ? "F" : "R") + (side == 0 ? "L" : "R");
                        Vector3 position = new Vector3(side == 0 ? -1.065f : 1.065f, .49f, axle == 0 ? 1.43f : -1.43f);
                        Transform parent = root.transform;
                        if (axle == 0) { parent = Child(parent, "Wheel" + name + "Steer", position); position = Vector3.zero; }
                        Transform spin = Child(parent, "Wheel" + name + "Spin", position);
                        Render(spin, "Tire and twin spoke alloy", wheelMesh, wheelMaterials);
                    }
                var shadow = new Geometry(1);
                shadow.Quad(0, new Vector3(-1.34f,.012f,-2.6f), new Vector3(-1.34f,.012f,2.6f), new Vector3(1.34f,.012f,2.6f), new Vector3(1.34f,.012f,-2.6f));
                Render(root.transform, "Soft contact shadow", Save(shadow, "ChaseSUVShadow"), new[] { ShadowMaterial() });
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/FenerRunner/Resources/"+resource+".prefab");
                AssetDatabase.SaveAssets();
                Debug.Log(resource+" built: " + (bodyMesh.triangles.Length / 3 + 4 * wheelMesh.triangles.Length / 3 + 2) + " rendered triangles; one shared wheel mesh, no colliders.");
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        internal static void BuildPreviews()
        {
            // The same SRP-aware Editor path as the production app icon. These
            // are real rendered car meshes, not illustrations or mockups.
            for(int i=0;i<RunWallet.VehicleCount;i++)
            {
                var kind=(ChaseVehicleKind)i;var preview=new PreviewRenderUtility();
                GameObject model=null;Texture2D pixels=null;
                try
                {
                    model=UnityEngine.Object.Instantiate(Resources.Load<GameObject>(RunnerShopCatalog.VehicleResource(kind)));
                    model.hideFlags=HideFlags.HideAndDontSave;preview.AddSingleGO(model);model=null;
                    var camera=preview.camera;camera.clearFlags=CameraClearFlags.SolidColor;
                    camera.backgroundColor=new Color(.026f,.047f,.085f,1);camera.nearClipPlane=.1f;camera.farClipPlane=35;
                    camera.orthographic=true;camera.orthographicSize=1.85f;camera.allowHDR=false;camera.allowMSAA=true;
                    camera.transform.position=new Vector3(6.4f,4.5f,7.9f);camera.transform.LookAt(new Vector3(0,1.05f,0));
                    preview.ambientColor=new Color(.38f,.42f,.50f,1);
                    preview.lights[0].intensity=1.8f;preview.lights[0].color=new Color(1,.94f,.82f);preview.lights[0].transform.rotation=Quaternion.Euler(42,-40,0);
                    preview.lights[1].intensity=.9f;preview.lights[1].color=new Color(.54f,.75f,1);preview.lights[1].transform.rotation=Quaternion.Euler(32,150,0);
                    preview.BeginPreview(new Rect(0,0,760,300),GUIStyle.none);preview.Render(true,false);
                    Texture source=preview.EndPreview();var previous=RenderTexture.active;bool oldSrgb=GL.sRGBWrite;
                    var target=RenderTexture.GetTemporary(760,300,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                    try
                    {
                        GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;Graphics.Blit(source,target);RenderTexture.active=target;
                        pixels=new Texture2D(760,300,TextureFormat.RGBA32,false,false);pixels.ReadPixels(new Rect(0,0,760,300),0,0,false);pixels.Apply(false,false);
                    }
                    finally{GL.sRGBWrite=oldSrgb;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
                    Color32[] values=pixels.GetPixels32();Color32 background=values[0];int foreground=0;
                    foreach(var pixel in values)if(Mathf.Abs(pixel.r-background.r)+Mathf.Abs(pixel.g-background.g)+Mathf.Abs(pixel.b-background.b)>30)foreground++;
                    if(foreground<4000)throw new InvalidOperationException("Garage preview rendered blank: "+kind);
                    string path="Assets/FenerRunner/Resources/VehiclePreview_"+kind+".png";
                    File.WriteAllBytes(path,pixels.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                    importer.mipmapEnabled=false;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
                }
                finally{if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);if(model!=null)UnityEngine.Object.DestroyImmediate(model);preview.Cleanup();}
            }
        }

        private static void BuildCoupe(Geometry g)
        {
            // Dedicated fastback body: lower cowl, long hood, raked roof and rear wing.
            g.Loft((int)Surface.Paint,new[]{new Section(-2.34f,.89f,.84f,.19f,.10f),new Section(-1.86f,1.09f,.95f,.30f,.12f),new Section(-.7f,1.07f,.93f,.31f,.12f),new Section(1.4f,1.08f,.92f,.27f,.12f),new Section(2.32f,.91f,.83f,.18f,.10f)});
            Box(g,Surface.Trim,new Vector3(0,.45f,0),new Vector3(1.91f,.16f,3.98f),.055f);
            g.Cabin((int)Surface.Glass,.95f,.76f,1.10f,1.66f,-1.73f,-.72f,.78f,.12f);
            Box(g,Surface.Paint,new Vector3(0,1.68f,-.29f),new Vector3(1.61f,.075f,.98f),.035f);
            Box(g,Surface.Glass,new Vector3(0,1.722f,-.30f),new Vector3(1.20f,.012f,.62f),.004f);
            Box(g,Surface.Paint,new Vector3(0,1.205f,1.44f),new Vector3(1.90f,.09f,1.32f),.04f,new Vector3(4,0,0));
            Box(g,Surface.Trim,new Vector3(0,.62f,2.31f),new Vector3(1.99f,.13f,.21f),.045f);
            Box(g,Surface.Trim,new Vector3(0,.60f,-2.31f),new Vector3(1.99f,.20f,.21f),.04f);
            Box(g,Surface.Trim,new Vector3(0,.85f,2.335f),new Vector3(1.22f,.25f,.035f),.035f);
            for(int i=-4;i<=4;i++)Box(g,Surface.Alloy,new Vector3(i*.125f,.85f,2.362f),new Vector3(.018f,.16f,.012f),.004f);
            for(int side=-1;side<=1;side+=2)
            {
                foreach(float z in new[]{-1.43f,1.43f})g.Arc((int)Surface.Trim,new Vector3(side*1.09f,.49f,z),.545f,.035f,.065f,-5,185,28);
                Box(g,Surface.Gold,new Vector3(side*1.082f,.60f,-.05f),new Vector3(.025f,.028f,1.61f),.009f);
                Box(g,Surface.Paint,new Vector3(side*.866f,1.41f,.40f),new Vector3(.075f,.78f,.075f),.02f,new Vector3(-48,0,side*17));
                Box(g,Surface.Paint,new Vector3(side*.875f,1.40f,-1.20f),new Vector3(.09f,1.1f,.09f),.025f,new Vector3(59,0,-side*15));
                Box(g,Surface.Trim,new Vector3(side*1.081f,1.03f,-.45f),new Vector3(.018f,.035f,.22f),.009f);
                Box(g,Surface.Paint,new Vector3(side*1.15f,1.28f,.51f),new Vector3(.26f,.12f,.20f),.04f);
                Box(g,Surface.Glass,new Vector3(side*.79f,.99f,2.29f),new Vector3(.44f,.14f,.065f),.035f);
                Box(g,Surface.White,new Vector3(side*.79f,1.025f,2.335f),new Vector3(.39f,.023f,.018f),.008f);
                Box(g,Surface.Red,new Vector3(side*.78f,1.025f,-2.318f),new Vector3(.43f,.045f,.025f),.009f);
                Box(g,Surface.Trim,new Vector3(side*.68f,1.37f,-1.94f),new Vector3(.055f,.37f,.11f),.018f);
                Box(g,Surface.Alloy,new Vector3(side*.78f,.54f,-2.43f),new Vector3(.28f,.13f,.10f),.045f);
                Box(g,Surface.Rubber,new Vector3(side*.78f,.54f,-2.487f),new Vector3(.20f,.075f,.015f),.02f);
            }
            Box(g,Surface.Trim,new Vector3(0,1.57f,-1.94f),new Vector3(2.12f,.09f,.34f),.035f);
            Box(g,Surface.Gold,new Vector3(0,1.625f,-1.94f),new Vector3(1.97f,.013f,.09f),.004f);
            g.Quad((int)Surface.Lettering,new Vector3(-.67f,1.734f,-.66f),new Vector3(-.67f,1.734f,.06f),new Vector3(.67f,1.734f,.06f),new Vector3(.67f,1.734f,-.66f));
        }

        private static void BuildOffroad(Geometry g)
        {
            // Upright compact 4x4 with separate fenders, side steps, spare and a roof basket.
            g.Loft((int)Surface.Paint,new[]{new Section(-2.08f,.94f,1.15f,.38f,.06f),new Section(-1.7f,1.00f,1.15f,.38f,.065f),new Section(1.5f,1.00f,1.15f,.38f,.065f),new Section(2.12f,.91f,1.15f,.31f,.06f)});
            Box(g,Surface.Trim,new Vector3(0,.61f,0),new Vector3(1.87f,.22f,3.9f),.06f);
            g.Cabin((int)Surface.Glass,.95f,.90f,1.48f,2.08f,-1.88f,-1.78f,.55f,.31f);
            Box(g,Surface.Paint,new Vector3(0,2.12f,-.73f),new Vector3(1.96f,.11f,2.28f),.035f);
            Box(g,Surface.Paint,new Vector3(0,1.5f,1.29f),new Vector3(1.90f,.14f,1.62f),.045f);
            Box(g,Surface.Trim,new Vector3(0,.77f,2.19f),new Vector3(2.12f,.23f,.28f),.055f);
            Box(g,Surface.Trim,new Vector3(0,.77f,-2.18f),new Vector3(2.12f,.23f,.28f),.055f);
            Box(g,Surface.Trim,new Vector3(0,1.20f,2.15f),new Vector3(.99f,.39f,.035f),.035f);
            for(int i=-3;i<=3;i++)Box(g,Surface.Alloy,new Vector3(i*.13f,1.20f,2.18f),new Vector3(.045f,.29f,.018f),.01f);
            for(int side=-1;side<=1;side+=2)
            {
                foreach(float z in new[]{-1.43f,1.43f})
                {g.Arc((int)Surface.Trim,new Vector3(side*1.05f,.49f,z),.62f,.12f,.24f,-4,184,24);Box(g,Surface.Trim,new Vector3(side*1.12f,.34f,z-.52f),new Vector3(.23f,.25f,.08f),.02f);}
                Box(g,Surface.Trim,new Vector3(side*1.12f,.64f,-.04f),new Vector3(.22f,.12f,1.54f),.025f);
                foreach(float z in new[]{-1.79f,-.67f,.42f})Box(g,Surface.Paint,new Vector3(side*.95f,1.80f,z),new Vector3(.105f,.63f,.085f),.02f);
                Box(g,Surface.Trim,new Vector3(side*1.105f,1.19f,-.14f),new Vector3(.035f,.07f,.21f),.016f);
                Box(g,Surface.Trim,new Vector3(side*1.16f,1.63f,.50f),new Vector3(.27f,.22f,.16f),.025f);
                Box(g,Surface.White,new Vector3(side*.75f,1.25f,2.15f),new Vector3(.34f,.24f,.045f),.065f);
                Box(g,Surface.Trim,new Vector3(side*.67f,1.00f,2.33f),new Vector3(.07f,.66f,.075f),.025f);
                Box(g,Surface.Red,new Vector3(side*.79f,1.24f,-2.095f),new Vector3(.18f,.32f,.04f),.03f);
                Box(g,Surface.Trim,new Vector3(side*.86f,2.25f,-.72f),new Vector3(.045f,.22f,2.00f),.014f);
            }
            Box(g,Surface.Trim,new Vector3(0,1.33f,2.33f),new Vector3(1.40f,.08f,.075f),.025f);
            Box(g,Surface.Trim,new Vector3(0,2.25f,.25f),new Vector3(1.76f,.22f,.045f),.014f);
            Box(g,Surface.Trim,new Vector3(0,2.25f,-1.69f),new Vector3(1.76f,.22f,.045f),.014f);
            for(int i=-3;i<=3;i++)Box(g,Surface.Trim,new Vector3(i*.23f,2.17f,-.72f),new Vector3(.027f,.025f,1.95f),.008f);
            // Sculpted spare-wheel cover is physically modeled on the rear door.
            Box(g,Surface.Trim,new Vector3(0,1.43f,-2.26f),new Vector3(.91f,.91f,.27f),.23f);
            Box(g,Surface.Paint,new Vector3(0,1.43f,-2.405f),new Vector3(.71f,.71f,.035f),.17f);
            Box(g,Surface.Gold,new Vector3(0,1.43f,-2.43f),new Vector3(.33f,.055f,.012f),.012f);
            g.Quad((int)Surface.Lettering,new Vector3(.70f,1.576f,1.91f),new Vector3(.70f,1.576f,1.12f),new Vector3(-.70f,1.576f,1.12f),new Vector3(-.70f,1.576f,1.91f));
        }

        private static Material Material(string name, Color color, float metallic, float smoothness, Color emission = default)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "SUV " + name, enableInstancing = true };
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0f) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", emission); }
            return RunnerSceneBuilder.Upsert(mat, Root + "/Materials/SUV " + name + ".mat");
        }

        private static Material ShadowMaterial()
        {
            const string path = Root + "/Rendering/SUVContactShadow.png";
            Directory.CreateDirectory(Root + "/Rendering");
            var texture = new Texture2D(64,64,TextureFormat.RGBA32,false);
            var pixels = new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float radius = new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f).magnitude;
                pixels[y*64+x] = new Color(0f,0f,0f,Mathf.Pow(Mathf.Clamp01(1f-radius),.7f)*.38f);
            }
            texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name="SUV soft contact shadow",enableInstancing=true,renderQueue=2990 };
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path)); mat.SetColor("_BaseColor",Color.white);
            mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",0);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mat.SetFloat("_ZWrite",0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return RunnerSceneBuilder.Upsert(mat,Root+"/Materials/SUV soft contact shadow.mat");
        }

        private static Material LetteringMaterial()
        {
            if(!File.Exists(LetteringPath))throw new FileNotFoundException("The baked Fenerbahçe Başkanı lettering atlas is missing.",LetteringPath);
            AssetDatabase.ImportAsset(LetteringPath,ImportAssetOptions.ForceUpdate);
            var importer=(TextureImporter)AssetImporter.GetAtPath(LetteringPath);
            importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;
            importer.mipmapEnabled=true;importer.mipMapsPreserveCoverage=true;importer.alphaTestReferenceValue=.25f;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;
            importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.userData="Fenerbahçe Başkanı | BarlowCondensed-Bold.ttf | roof / hood / rear glass";
            importer.SaveAndReimport();
            // Baked into the existing coachwork mesh. No TextMesh, dynamic font atlas,
            // extra hierarchy, runtime texture creation or transparent sorting cost.
            var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {name="SUV Fenerbahçe Başkanı lettering",enableInstancing=true,renderQueue=(int)RenderQueue.AlphaTest};
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(LetteringPath));
            mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",.25f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType","TransparentCutout");
            return RunnerSceneBuilder.Upsert(mat,Root+"/Materials/SUV President lettering.mat");
        }

        private static void BuildBody(Geometry g)
        {
            // The upper shoulder clears the circular wheel openings. Lower body panels occupy only the spaces between them.
            g.Loft((int)Surface.Paint, new[]
            {
                new Section(-2.36f,1.00f,1.17f,.22f,.11f),new Section(-2.18f,1.085f,1.18f,.245f,.12f),
                new Section(-.9f,1.10f,1.20f,.25f,.12f),new Section(.9f,1.10f,1.205f,.255f,.12f),
                new Section(2.18f,1.06f,1.17f,.23f,.11f),new Section(2.38f,1.00f,1.14f,.18f,.10f)
            });
            Box(g,Surface.Trim,new Vector3(0,.64f,0),new Vector3(1.91f,.29f,3.95f),.09f);
            Box(g,Surface.Paint,new Vector3(0,.80f,0),new Vector3(2.17f,.43f,1.66f),.075f);
            Box(g,Surface.Paint,new Vector3(0,.79f,2.16f),new Vector3(2.08f,.4f,.46f),.09f);
            Box(g,Surface.Paint,new Vector3(0,.79f,-2.16f),new Vector3(2.08f,.4f,.46f),.09f);
            Box(g,Surface.Trim,new Vector3(0,.60f,2.22f),new Vector3(2.10f,.20f,.39f),.065f);
            Box(g,Surface.Trim,new Vector3(0,.60f,-2.21f),new Vector3(2.12f,.21f,.4f),.065f);
            // Sloping rear screen, long floating roof and raked windshield form the distinctive upright SUV silhouette.
            g.Cabin((int)Surface.Glass,1.025f,.912f,1.37f,2.025f,-1.93f,-1.62f,1.02f,.50f);
            Box(g,Surface.Paint,new Vector3(0,2.045f,-.55f),new Vector3(1.96f,.105f,2.23f),.055f);
            Box(g,Surface.Glass,new Vector3(0,2.101f,-.58f),new Vector3(1.57f,.022f,1.57f),.01f);
            Box(g,Surface.Paint,new Vector3(0,1.385f,1.53f),new Vector3(2.01f,.13f,1.45f),.055f);
            Box(g,Surface.Highlight,new Vector3(0,1.456f,1.55f),new Vector3(.025f,.009f,1.13f),.004f);
            for(int side=-1;side<=1;side+=2)
            {
                // Wheel-well liners and the raised arch moulding have real open centres.
                foreach(float z in new[]{-1.43f,1.43f})
                {
                    g.Arc((int)Surface.Trim,new Vector3(side*1.075f,.49f,z),.555f,.075f,.10f,-7f,187f,28);
                    g.Arc((int)Surface.Highlight,new Vector3(side*1.112f,.49f,z),.579f,.013f,.018f,5f,175f,24);
                }
                Box(g,Surface.Trim,new Vector3(side*1.093f,.57f,0),new Vector3(.08f,.13f,1.65f),.025f);
                Box(g,Surface.Alloy,new Vector3(side*1.139f,.61f,0),new Vector3(.025f,.025f,1.50f),.01f);
                // Painted B/C pillars sit over the continuous tinted glazing. Thin accents catch stadium lighting.
                Box(g,Surface.Paint,new Vector3(side*.974f,1.715f,-.38f),new Vector3(.105f,.61f,.095f),.025f,new Vector3(0,0,side*9.4f));
                Box(g,Surface.Paint,new Vector3(side*.974f,1.715f,-1.26f),new Vector3(.105f,.61f,.105f),.025f,new Vector3(0,0,side*9.4f));
                Box(g,Surface.Paint,new Vector3(side*.97f,1.69f,.77f),new Vector3(.10f,.78f,.085f),.022f,new Vector3(-38,0,side*9));
                Box(g,Surface.Highlight,new Vector3(side*1.065f,1.40f,-.42f),new Vector3(.028f,.028f,2.80f),.012f);
                Box(g,Surface.Trim,new Vector3(side*.80f,2.135f,-.56f),new Vector3(.045f,.045f,1.99f),.02f);
                foreach(float z in new[]{-.95f,.25f})
                {
                    Box(g,Surface.Trim,new Vector3(side*1.105f,1.18f,z),new Vector3(.026f,.045f,.21f),.012f);
                    Box(g,Surface.Alloy,new Vector3(side*1.12f,1.191f,z),new Vector3(.012f,.014f,.16f),.005f);
                }
                Box(g,Surface.Trim,new Vector3(side*1.105f,.97f,-.32f),new Vector3(.014f,.36f,.012f),.004f);
                Box(g,Surface.Paint,new Vector3(side*1.13f,1.54f,.79f),new Vector3(.24f,.065f,.07f),.025f);
                Box(g,Surface.Paint,new Vector3(side*1.23f,1.58f,.76f),new Vector3(.25f,.13f,.24f),.055f);
                Box(g,Surface.Glass,new Vector3(side*1.242f,1.59f,.639f),new Vector3(.185f,.085f,.008f),.003f);
                Box(g,Surface.White,new Vector3(side*1.24f,1.55f,.874f),new Vector3(.18f,.012f,.013f),.005f);
                // Gold coachline is subtle enough to preserve a black vehicle, but ties it to the game's yellow/navy palette.
                Box(g,Surface.Gold,new Vector3(side*1.11f,.75f,0),new Vector3(.015f,.016f,1.52f),.006f);
            }
        }

        private static void BuildDetails(Geometry g)
        {
            Box(g,Surface.Trim,new Vector3(0,1.105f,2.38f),new Vector3(1.19f,.36f,.055f),.045f);
            Box(g,Surface.Alloy,new Vector3(0,1.30f,2.405f),new Vector3(1.14f,.016f,.014f),.006f);
            for(int row=0;row<4;row++)
            {
                float y=.98f+row*.075f;
                Box(g,Surface.Highlight,new Vector3(0,y,2.418f),new Vector3(1.06f,.014f,.018f),.006f);
                for(int col=0;col<12;col++)
                    Box(g,Surface.Alloy,new Vector3(-.495f+col*.09f,y+.02f,2.425f),new Vector3(.032f,.025f,.01f),.004f,new Vector3(0,0,35));
            }
            for(int side=-1;side<=1;side+=2)
            {
                Box(g,Surface.Glass,new Vector3(side*.817f,1.245f,2.365f),new Vector3(.45f,.17f,.09f),.04f);
                Box(g,Surface.White,new Vector3(side*.817f,1.285f,2.416f),new Vector3(.39f,.024f,.018f),.009f);
                Box(g,Surface.White,new Vector3(side*.995f,1.245f,2.416f),new Vector3(.025f,.07f,.018f),.009f);
                Box(g,Surface.White,new Vector3(side*.80f,1.205f,2.416f),new Vector3(.26f,.014f,.016f),.006f);
                Box(g,Surface.Trim,new Vector3(side*.86f,.815f,2.38f),new Vector3(.29f,.13f,.045f),.025f);
                for(int rib=0;rib<3;rib++)Box(g,Surface.Highlight,new Vector3(side*.86f,.78f+rib*.035f,2.407f),new Vector3(.23f,.009f,.01f),.004f);
                // Range Rover-style concealed vertical rear lamps and a continuous
                // gloss-black rear bridge, readable from the chase camera.
                Box(g,Surface.Glass,new Vector3(side*.967f,1.19f,-2.37f),new Vector3(.115f,.53f,.072f),.03f);
                Box(g,Surface.Red,new Vector3(side*.967f,1.19f,-2.413f),new Vector3(.029f,.44f,.016f),.009f);
                Box(g,Surface.Glass,new Vector3(side*.47f,1.44f,-2.366f),new Vector3(.94f,.105f,.055f),.025f);
                Box(g,Surface.Red,new Vector3(side*.883f,.91f,-2.414f),new Vector3(.18f,.022f,.016f),.008f);
                Box(g,Surface.Alloy,new Vector3(side*.80f,.60f,-2.418f),new Vector3(.23f,.09f,.05f),.025f);
                Box(g,Surface.Rubber,new Vector3(side*.80f,.60f,-2.45f),new Vector3(.17f,.053f,.012f),.016f);
            }
            Box(g,Surface.Trim,new Vector3(0,.79f,2.417f),new Vector3(1.21f,.13f,.03f),.035f);
            for(int i=0;i<7;i++)Box(g,Surface.Highlight,new Vector3(-.48f+i*.16f,.78f,2.439f),new Vector3(.016f,.075f,.01f),.004f);
            Box(g,Surface.Paint,new Vector3(0,2.065f,-1.80f),new Vector3(1.91f,.09f,.28f),.04f);
            Box(g,Surface.Red,new Vector3(0,2.05f,-1.95f),new Vector3(.59f,.018f,.014f),.006f);
            Box(g,Surface.Trim,new Vector3(0,1.08f,-2.397f),new Vector3(.45f,.15f,.025f),.016f);
            Box(g,Surface.Gold,new Vector3(0,1.08f,-2.414f),new Vector3(.36f,.08f,.008f),.004f);
            Box(g,Surface.Glass,new Vector3(0,1.082f,-2.421f),new Vector3(.29f,.046f,.008f),.003f);
            // The roof inscription reads upright from the actual rear chase camera.
            // The front inscription faces the front, while the rear inscription follows
            // the real raked glass. All three use one fixed, antialiased Turkish atlas.
            Box(g,Surface.Trim,new Vector3(0,2.13f,-.58f),new Vector3(1.76f,.028f,1.01f),.012f);
            g.Quad((int)Surface.Lettering,new Vector3(-.88f,2.15f,-1.06f),new Vector3(-.88f,2.15f,-.10f),new Vector3(.88f,2.15f,-.10f),new Vector3(.88f,2.15f,-1.06f));
            g.Quad((int)Surface.Lettering,new Vector3(.79f,1.463f,1.95f),new Vector3(.79f,1.463f,1.14f),new Vector3(-.79f,1.463f,1.14f),new Vector3(-.79f,1.463f,1.95f));
            Vector3 rearCenter=new Vector3(0,1.72f,-1.779f),rearUp=new Vector3(0,.904f,.428f)*.265f;
            g.Quad((int)Surface.Lettering,rearCenter-Vector3.right*.76f-rearUp,rearCenter-Vector3.right*.76f+rearUp,rearCenter+Vector3.right*.76f+rearUp,rearCenter+Vector3.right*.76f-rearUp);
        }

        private static void BuildWheel(Geometry g)
        {
            // Axis X. A closed, rounded ten-ring tire profile gives actual sidewalls and tread shoulders.
            g.Lathe(0,new[] {new Vector2(-.13f,.30f),new Vector2(-.17f,.36f),new Vector2(-.17f,.445f),new Vector2(-.125f,.483f),new Vector2(-.05f,.495f),new Vector2(.05f,.495f),new Vector2(.125f,.483f),new Vector2(.17f,.445f),new Vector2(.17f,.36f),new Vector2(.13f,.30f)},40);
            g.Lathe(1,new[] {new Vector2(-.171f,.272f),new Vector2(-.178f,.315f),new Vector2(-.16f,.345f),new Vector2(.16f,.345f),new Vector2(.178f,.315f),new Vector2(.171f,.272f)},40);
            g.Lathe(2,new[] {new Vector2(-.138f,0f),new Vector2(-.138f,.274f),new Vector2(.138f,.274f),new Vector2(.138f,0f)},32);
            for(int side=-1;side<=1;side+=2)
            {
                g.Lathe(1,new[] {new Vector2(side*.181f,0f),new Vector2(side*.181f,.075f),new Vector2(side*.198f,.075f),new Vector2(side*.198f,0f)},24);
                for(int spoke=0;spoke<5;spoke++)for(int fork=-1;fork<=1;fork+=2)
                {
                    float angle=spoke*72f+fork*5f;
                    Quaternion rotation=Quaternion.Euler(angle,0,0);
                    Vector3 center=rotation*new Vector3(side*.18f,.19f,0f);
                    g.RoundedBox(1,center,new Vector3(.025f,.23f,.028f),.009f,rotation);
                }
                for(int bolt=0;bolt<5;bolt++)
                {
                    Quaternion rotation=Quaternion.Euler(bolt*72f,0,0);
                    g.RoundedBox(2,rotation*new Vector3(side*.205f,.043f,0),new Vector3(.012f,.016f,.016f),.004f,rotation);
                }
                // Subtle sidewall ring catches light without adding an oversized cartoon tire bevel.
                g.Lathe(0,new[] {new Vector2(side*.172f,.395f),new Vector2(side*.175f,.402f),new Vector2(side*.172f,.410f)},40);
            }
            for(int tread=0;tread<32;tread++)
            {
                Quaternion turn=Quaternion.Euler(tread*360f/32f,0,0);
                for(int side=-1;side<=1;side+=2)
                    g.RoundedBox(2,turn*new Vector3(side*.075f,.495f,0),new Vector3(.079f,.006f,.021f),.002f,turn*Quaternion.Euler(0,side*28f,0));
            }
        }

        private static void Box(Geometry g, Surface material, Vector3 position, Vector3 size, float bevel, Vector3 rotation = default)
            => g.RoundedBox((int)material,position,size,bevel,Quaternion.Euler(rotation));

        private static Transform Child(Transform parent, string name, Vector3 position)
        {var child=new GameObject(name).transform;child.SetParent(parent,false);child.localPosition=position;return child;}

        private static void Render(Transform parent,string name,Mesh mesh,Material[] materials)
        {
            var child=Child(parent,name,Vector3.zero);child.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=child.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }

        private static Mesh Save(Geometry geometry,string name)
        {return RunnerSceneBuilder.Upsert(geometry.ToMesh(name),Root+"/Meshes/"+name+".asset");}

        private readonly struct Section
        {
            public readonly float z,halfWidth,y,halfHeight,bevel;
            public Section(float z,float halfWidth,float y,float halfHeight,float bevel)
            {this.z=z;this.halfWidth=halfWidth;this.y=y;this.halfHeight=halfHeight;this.bevel=bevel;}
        }

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            private readonly List<Vector2> uv=new List<Vector2>();
            private readonly List<int>[] triangles;
            internal Geometry(int count){triangles=new List<int>[count];for(int i=0;i<count;i++)triangles[i]=new List<int>();}
            private int Vertex(Vector3 p,Vector3 n,Vector2 tex)
            {int index=vertices.Count;vertices.Add(p);normals.Add(n);uv.Add(tex);return index;}
            private void Tri(int material,int a,int b,int c){triangles[material].Add(a);triangles[material].Add(b);triangles[material].Add(c);}
            internal void Quad(int material,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                Vector3 normal=Vector3.Cross(b-a,c-a).normalized;int i=vertices.Count;
                Vertex(a,normal,new Vector2(0,0));Vertex(b,normal,new Vector2(0,1));Vertex(c,normal,new Vector2(1,1));Vertex(d,normal,new Vector2(1,0));
                Tri(material,i,i+1,i+2);Tri(material,i,i+2,i+3);
            }
            internal void RoundedBox(int material,Vector3 position,Vector3 size,float radius,Quaternion rotation)
            {
                Vector3 half=size*.5f;radius=Mathf.Max(.0005f,Mathf.Min(radius,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.95f));
                Vector3 inner=half-Vector3.one*radius;
                Vector3[] axes={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
                foreach(Vector3 axis in axes)
                {
                    Vector3 tangent=Mathf.Abs(axis.y)>.5f?Vector3.right:Vector3.up;
                    Vector3 bitangent=Vector3.Cross(axis,tangent);
                    float normalExtent=Vector3.Dot(Abs(axis),half), uExtent=Vector3.Dot(Abs(tangent),half),vExtent=Vector3.Dot(Abs(bitangent),half);
                    // Sub-centimetre seams, grille cells and tread blocks do not need 96-vertex bevels on a phone.
                    if(radius<.009f)
                    {
                        Vector3 center=axis*normalExtent,u=tangent*uExtent,v=bitangent*vExtent;
                        Quad(material,position+rotation*(center-u-v),position+rotation*(center+u-v),position+rotation*(center+u+v),position+rotation*(center-u+v));
                        continue;
                    }
                    float[] us={-uExtent,-uExtent+radius,uExtent-radius,uExtent};float[] vs={-vExtent,-vExtent+radius,vExtent-radius,vExtent};
                    int start=vertices.Count;
                    for(int v=0;v<4;v++)for(int u=0;u<4;u++)
                    {
                        Vector3 point=axis*normalExtent+tangent*us[u]+bitangent*vs[v];
                        Vector3 clamped=new Vector3(Mathf.Clamp(point.x,-inner.x,inner.x),Mathf.Clamp(point.y,-inner.y,inner.y),Mathf.Clamp(point.z,-inner.z,inner.z));
                        Vector3 normal=(point-clamped).normalized;
                        Vertex(position+rotation*(clamped+normal*radius),rotation*normal,new Vector2(u/3f,v/3f));
                    }
                    for(int v=0;v<3;v++)for(int u=0;u<3;u++)
                    {int a=start+v*4+u;Tri(material,a,a+1,a+5);Tri(material,a,a+5,a+4);}
                }
            }
            private static Vector3 Abs(Vector3 value)=>new Vector3(Mathf.Abs(value.x),Mathf.Abs(value.y),Mathf.Abs(value.z));
            internal void Loft(int material,Section[] sections)
            {
                const int sides=20;int start=vertices.Count;
                for(int s=0;s<sections.Length;s++)
                {
                    Section section=sections[s];
                    for(int corner=0;corner<4;corner++)for(int step=0;step<5;step++)
                    {
                        float angle=(corner*90f+step*22.5f)*Mathf.Deg2Rad;
                        float signX=corner==0||corner==3?1:-1,signY=corner<2?1:-1;
                        Vector3 normal=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                        Vector3 p=new Vector3(signX*(section.halfWidth-section.bevel),section.y+signY*(section.halfHeight-section.bevel),section.z)+normal*section.bevel;
                        Vertex(p,normal,new Vector2((corner*5+step)/(float)sides,s/(float)(sections.Length-1)));
                    }
                }
                for(int s=0;s<sections.Length-1;s++)for(int i=0;i<sides;i++)
                {int a=start+s*sides+i,b=start+s*sides+(i+1)%sides;Tri(material,a,b,b+sides);Tri(material,a,b+sides,a+sides);}
                for(int end=0;end<2;end++)
                {
                    int ring=start+(end==0?0:sections.Length-1)*sides;Vector3 n=end==0?Vector3.back:Vector3.forward;
                    Section section=sections[end==0?0:sections.Length-1];int center=Vertex(new Vector3(0,section.y,section.z),n,new Vector2(.5f,.5f));
                    for(int i=0;i<sides;i++)
                    {int a=Vertex(vertices[ring+i],n,Vector2.zero),b=Vertex(vertices[ring+(i+1)%sides],n,Vector2.one);if(end==0)Tri(material,center,b,a);else Tri(material,center,a,b);}
                }
            }
            internal void Cabin(int material,float bottomHalf,float topHalf,float bottomY,float topY,float rearBottom,float rearTop,float frontBottom,float frontTop)
            {
                Vector3 bl=new Vector3(-bottomHalf,bottomY,rearBottom),br=new Vector3(bottomHalf,bottomY,rearBottom);
                Vector3 fl=new Vector3(-bottomHalf,bottomY,frontBottom),fr=new Vector3(bottomHalf,bottomY,frontBottom);
                Vector3 tl=new Vector3(-topHalf,topY,rearTop),tr=new Vector3(topHalf,topY,rearTop);
                Vector3 hl=new Vector3(-topHalf,topY,frontTop),hr=new Vector3(topHalf,topY,frontTop);
                Quad(material,fl,hl,tl,bl);Quad(material,br,tr,hr,fr);Quad(material,fr,hr,hl,fl);
                Quad(material,bl,tl,tr,br);Quad(material,hl,hr,tr,tl);
            }
            internal void Arc(int material,Vector3 center,float radius,float thickness,float width,float from,float to,int segments)
            {
                for(int i=0;i<segments;i++)
                {
                    float a=Mathf.Lerp(from,to,i/(float)segments)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(i+1)/(float)segments)*Mathf.Deg2Rad;
                    Vector3 ra=new Vector3(0,Mathf.Sin(a),Mathf.Cos(a)),rb=new Vector3(0,Mathf.Sin(b),Mathf.Cos(b));
                    Vector3 x=Vector3.right*width*.5f;
                    Vector3 a0=center+ra*(radius-thickness)-x,a1=center+ra*radius-x,b0=center+rb*(radius-thickness)-x,b1=center+rb*radius-x;
                    Quad(material,a0,a1,b1,b0);Quad(material,a0+2*x,b0+2*x,b1+2*x,a1+2*x);
                    Quad(material,a1,a1+2*x,b1+2*x,b1);Quad(material,b0,b0+2*x,a0+2*x,a0);
                }
            }
            internal void Lathe(int material,Vector2[] profile,int segments)
            {
                int start=vertices.Count;
                for(int p=0;p<profile.Length;p++)
                {
                    Vector2 tangent=(profile[(p+1)%profile.Length]-profile[(p+profile.Length-1)%profile.Length]).normalized;
                    Vector2 normal=new Vector2(-tangent.y,tangent.x);
                    for(int i=0;i<segments;i++)
                    {
                        float a=i*2f*Mathf.PI/segments;Vector3 n=new Vector3(normal.x,normal.y*Mathf.Cos(a),normal.y*Mathf.Sin(a));
                        Vertex(new Vector3(profile[p].x,profile[p].y*Mathf.Cos(a),profile[p].y*Mathf.Sin(a)),n,new Vector2(i/(float)segments,p/(float)profile.Length));
                    }
                }
                // Profile orientation may differ for the two mirrored hubs. Choose the outward geometric winding.
                float signedArea=0;for(int p=0;p<profile.Length;p++){Vector2 a=profile[p],b=profile[(p+1)%profile.Length];signedArea+=a.x*b.y-b.x*a.y;}
                bool flip=signedArea>0f;
                if(flip)for(int i=start;i<vertices.Count;i++)normals[i]=-normals[i];
                for(int p=0;p<profile.Length;p++)for(int i=0;i<segments;i++)
                {
                    int a=start+p*segments+i,b=start+p*segments+(i+1)%segments,c=start+((p+1)%profile.Length)*segments+(i+1)%segments,d=start+((p+1)%profile.Length)*segments+i;
                    if(flip){Tri(material,a,c,b);Tri(material,a,d,c);}else{Tri(material,a,b,c);Tri(material,a,c,d);}
                }
            }
            internal Mesh ToMesh(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.subMeshCount=triangles.Length;
                for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i,false);
                mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
            }
        }
    }
}
