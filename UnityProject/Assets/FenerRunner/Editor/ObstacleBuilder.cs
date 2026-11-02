using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FenerRunner.Editor
{
    public sealed class ObstacleImport : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if(!assetPath.StartsWith("Assets/FenerRunner/Art/Obstacles/"))return;
            var i=(ModelImporter)assetImporter;i.importAnimation=false;i.animationType=ModelImporterAnimationType.None;
            i.isReadable=true;i.importCameras=false;i.importLights=false;i.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        }
    }
    internal static class ObstacleBuilder
    {
        [Serializable] private class Layout { public Entry[] assets; }
        [Serializable] private class Entry { public string id,file_stem; public Box[] boxes; }
        [Serializable] private class Box { public string name; public float[] center,size; }
        public static RunnerObstacle[] Build()
        {
            const string art="Assets/FenerRunner/Art/Obstacles/";
            var data=JsonUtility.FromJson<Layout>(File.ReadAllText(art+"collider_layout.json"));
            return data.assets.Select(e=>BuildOne(e,art)).ToArray();
        }
        private static RunnerObstacle BuildOne(Entry entry,string art)
        {
            string path=art+entry.file_stem+".fbx";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var root=new GameObject(entry.file_stem,typeof(RunnerObstacle));
            try
            {
                root.GetComponent<RunnerObstacle>().kind=entry.id=="black"?ObstacleKind.JumpOver:ObstacleKind.SlideUnder;
                var visual=new GameObject("Original supplied model");visual.transform.SetParent(root.transform,false);
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),visual.transform);
                var renderer=model.GetComponentInChildren<MeshRenderer>();
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                int beak=Array.FindIndex(renderer.sharedMaterials,m=>m.name.Contains("Beak"));
                Vector3 heading=Vector3.forward;
                if(beak>=0)
                {
                    Vector3 sum=Vector3.zero;var vertices=mesh.vertices;var indices=mesh.GetTriangles(beak);
                    foreach(int i in indices)sum+=renderer.transform.TransformPoint(vertices[i]);
                    heading=sum/indices.Length-renderer.bounds.center;heading.y=0;
                }
                visual.transform.localRotation=Quaternion.Euler(0,-Mathf.Atan2(heading.x,heading.z)*Mathf.Rad2Deg,0);
                float target=entry.id=="black"?1.043624f:3.663322f;
                visual.transform.localScale=Vector3.one*(target/renderer.bounds.size.y);
                visual.transform.localPosition=Vector3.up*(-renderer.bounds.min.y);
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>MaterialFor(m,art,entry.id)).ToArray();
                CharacterIdentityBuilder.Obstacle(root,renderer,entry.id);
                // Face the approaching player. Collider and visual share the same parent rotation.
                root.transform.rotation=Quaternion.Euler(0,180,0);
                if(entry.id=="black")BuildBlackContour(root);
                else foreach(var b in entry.boxes)
                {
                    var child=new GameObject(b.name,typeof(BoxCollider));child.layer=8;child.transform.SetParent(root.transform,false);
                    var box=child.GetComponent<BoxCollider>();box.isTrigger=true;
                    box.center=new Vector3(b.center[0],b.center[1],b.center[2]);box.size=new Vector3(b.size[0],b.size[1],b.size[2]);
                }
                root.GetComponent<RunnerObstacle>().hitMesh=ObstacleHitMeshBuilder.Build(root);
                return PrefabUtility.SaveAsPrefabAsset(root,RunnerSceneBuilder.Generated+"/Prefabs/"+entry.file_stem+".prefab").GetComponent<RunnerObstacle>();
            }
            finally{Object.DestroyImmediate(root);}
        }
        private static void BuildBlackContour(GameObject root)
        {
            // A single enclosing box placed the shoulders' width around empty space
            // beside the narrower head. Clip the final model (including its beard)
            // into horizontal bands, preserving the real silhouette and full height.
            var filters=root.GetComponentsInChildren<MeshFilter>();
            var vertices=new Vector3[filters.Length][];
            var triangles=new int[filters.Length][];
            float top=0;
            for(int meshIndex=0;meshIndex<filters.Length;meshIndex++)
            {
                Mesh mesh=filters[meshIndex].sharedMesh;
                vertices[meshIndex]=mesh.vertices;triangles[meshIndex]=mesh.triangles;
                for(int i=0;i<vertices[meshIndex].Length;i++)
                {
                    Vector3 point=root.transform.InverseTransformPoint(filters[meshIndex].transform.TransformPoint(vertices[meshIndex][i]));
                    vertices[meshIndex][i]=point;top=Mathf.Max(top,point.y);
                }
            }
            float[] levels={0,.35f,.65f,.95f,1.25f,1.5f,1.65f,1.8f,top};
            var child=new GameObject("Body contour");child.layer=8;child.transform.SetParent(root.transform,false);
            for(int band=0;band<levels.Length-1;band++)
            {
                float low=levels[band],high=levels[band+1];
                if(high<=low)throw new InvalidOperationException("Black obstacle contour bands exceed the supplied model height.");
                Vector3 min=Vector3.one*float.PositiveInfinity,max=Vector3.one*float.NegativeInfinity;
                for(int meshIndex=0;meshIndex<vertices.Length;meshIndex++)
                {
                    Vector3[] points=vertices[meshIndex];int[] indices=triangles[meshIndex];
                    foreach(Vector3 point in points)if(point.y>=low&&point.y<=high)Include(point,ref min,ref max);
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        Vector3 a=points[indices[i]],b=points[indices[i+1]],c=points[indices[i+2]];
                        ClipEdge(a,b,low,high,ref min,ref max);
                        ClipEdge(b,c,low,high,ref min,ref max);
                        ClipEdge(c,a,low,high,ref min,ref max);
                    }
                }
                if(float.IsInfinity(min.x))throw new InvalidOperationException("Empty black obstacle contour band "+band);
                min.y=low;max.y=high;
                var collider=child.AddComponent<BoxCollider>();collider.isTrigger=true;
                collider.center=(min+max)*.5f;collider.size=max-min;
            }
        }
        private static void ClipEdge(Vector3 from,Vector3 to,float low,float high,ref Vector3 min,ref Vector3 max)
        {
            float change=to.y-from.y;
            if(Mathf.Abs(change)<.0000001f)return;
            float lowT=(low-from.y)/change,highT=(high-from.y)/change;
            if(lowT>0&&lowT<1)Include(Vector3.LerpUnclamped(from,to,lowT),ref min,ref max);
            if(highT>0&&highT<1)Include(Vector3.LerpUnclamped(from,to,highT),ref min,ref max);
        }
        private static void Include(Vector3 point,ref Vector3 min,ref Vector3 max)
        {min=Vector3.Min(min,point);max=Vector3.Max(max,point);}
        private static Material MaterialFor(Material source,string art,string id)
        {
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=source.name,enableInstancing=true};
            Color color=source.HasProperty("_Color")?source.color:Color.white;
            // Supplied FBX diffuse factors equal the source GLB linear factors. URP color properties use display-space values.
            mat.SetColor("_BaseColor",color.gamma);mat.SetFloat("_Smoothness",.22f);
            if(id=="curly")
            {
                string hex=source.name.Contains("Deep_Curly_Hair")?"382A23":source.name.Contains("Warm_Curl_Ridges")?"654933":source.name.Contains("Golden_Feathers")?"D6AF75":null;
                if(hex!=null){ColorUtility.TryParseHtmlString("#"+hex,out Color shade);mat.SetColor("_BaseColor",shade);}
            }
            if(source.name.Contains("Jersey"))
            {
                mat.SetColor("_BaseColor",Color.white);
                mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(art+"textures/"+(id=="black"?"black":"curly")+"_bird_jersey_basecolor.png"));
            }
            return RunnerSceneBuilder.Upsert(mat,RunnerSceneBuilder.Generated+"/Materials/"+source.name+".mat");
        }
    }
}
