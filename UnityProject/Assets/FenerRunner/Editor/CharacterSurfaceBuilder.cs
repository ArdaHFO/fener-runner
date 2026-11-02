using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace FenerRunner.Editor
{
    // Offline surface finishing only. Positions, UVs, skinning, silhouette and colliders are retained.
    public static class CharacterSurfaceBuilder
    {
        const string ShaderName="FenerRunner/CharacterSoftLit";
        public static readonly string[] Prefabs={"KartalPlayer","Engel_Atlama_SiyahKus_v01","Engel_Kayma_KivircikKus_v01"};
        [Serializable] class MeshCheck
        {public string name;public int vertices,triangles,normalChanges;public float minimumAO,meanAO;public bool geometryUnchanged,skinningUnchanged,uvUnchanged;}
        [Serializable] class Report
        {public bool passed;public string utc;public string[] checks;public MeshCheck[] meshes;public string scope="Editor finishing of existing meshes. No extra vertices, triangles, renderers, textures, bones, material slots or runtime animation work. Colors, original clothing textures and collider data are retained. Mobile GPU performance still requires a real device.";}
        static readonly List<MeshCheck> measured=new List<MeshCheck>();
        static readonly Dictionary<string,Mesh> cache=new Dictionary<string,Mesh>();
        public static void BeginBuild(){cache.Clear();measured.Clear();}

        [MenuItem("Fener Runner/48 - Capture original character surfaces")]
        public static void CaptureBefore(){CharacterLookReview.StartChecks("before");}
        [MenuItem("Fener Runner/49 - Finish character surfaces and review")]
        public static void ApplyAndReview()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            measured.Clear();cache.Clear();var checks=new List<string>();
            foreach(string name in Prefabs)
            {
                string path=RunnerSceneBuilder.Generated+"/Prefabs/"+name+".prefab";
                GameObject root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    string before=Structure(root);
                    foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())Finish(skin,name);
                    foreach(var mesh in root.GetComponentsInChildren<MeshFilter>())Finish(mesh.GetComponent<MeshRenderer>(),name+"_"+mesh.name);
                    if(before!=Structure(root))throw new InvalidOperationException("Identity, topology or collider change in "+name);
                    checks.Add(name+": original palette, clothing maps, transforms, colliders and renderer counts unchanged");
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            var pipeline=(UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            EnableSoftShadows(pipeline);FixWorldSigns();AssetDatabase.SaveAssets();
            // Reload the saved game scene so all prefab references pick up the finished surfaces.
            EditorSceneManager.OpenScene(RunnerSceneBuilder.ScenePath);
            SetupVerification.Verify();
            var report=new Report{passed=measured.All(m=>m.geometryUnchanged&&m.skinningUnchanged&&m.uvUnchanged),utc=DateTime.UtcNow.ToString("O"),checks=checks.ToArray(),meshes=measured.ToArray()};
            Directory.CreateDirectory("../Validation");if(measured.Count>0)File.WriteAllText("../Validation/unity_character_surfaces_v10.json",JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidOperationException("Character surface preservation failed");
            CharacterLookReview.StartChecks("after");
        }

        public static Material WorldSignMaterial(Font font)
        {
            const string path=RunnerSceneBuilder.Generated+"/Materials/WorldSignDepth.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("FenerRunner/WorldSign")){name="WorldSignDepth"};AssetDatabase.CreateAsset(material,path);}
            material.mainTexture=font.material.mainTexture;EditorUtility.SetDirty(material);return material;
        }
        static void FixWorldSigns()
        {
            string path=RunnerSceneBuilder.Generated+"/Prefabs/StadiumSegment.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var label in root.GetComponentsInChildren<TextMesh>())label.GetComponent<MeshRenderer>().sharedMaterial=WorldSignMaterial(label.font);
                if(root.GetComponent<WorldSignFont>()==null)root.AddComponent<WorldSignFont>();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        public static void EnableSoftShadows(UniversalRenderPipelineAsset pipeline)
        {
            var serialized=new SerializedObject(pipeline);
            serialized.FindProperty("m_SoftShadowsSupported").boolValue=true;
            // Low PCF quality retains a mobile-friendly fixed sampling budget.
            var quality=serialized.FindProperty("m_SoftShadowQuality");if(quality!=null)quality.intValue=1;
            serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);
        }
        public static void Style(Material material)
        {
            var shader=Shader.Find(ShaderName);if(shader==null)throw new InvalidOperationException("Character surface shader missing");
            string name=material.name.ToLowerInvariant();material.shader=shader;material.enableInstancing=true;
            bool eye=name.Contains("eye")||name.Contains("pupil")||name.Contains("iris")||name.Contains("catchlight");
            bool cloth=name.Contains("knit")||name.Contains("sweater")||name.Contains("jersey");
            bool hair=name.Contains("hair")||name.Contains("curl")||name.Contains("beard")||name.Contains("brow");
            bool beak=name.Contains("beak")||name.Contains("feet")||name.Contains("toe");
            material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",eye?.72f:cloth?.15f:hair?.23f:beak?.32f:.22f);
            material.SetFloat("_SoftWrap",eye?.035f:cloth?.12f:hair?.16f:.26f);
            material.SetFloat("_Sheen",eye?0:cloth?.15f:hair?.12f:.08f);
            material.SetFloat("_Grain",eye||beak?0:cloth?.035f:hair?.04f:.045f);
            material.SetFloat("_OcclusionStrength",eye?.35f:.65f);
            if(name.Contains("cream_cableknit"))material.SetFloat("_BumpScale",.36f);
            if(material.GetTexture("_BumpMap")!=null)material.EnableKeyword("_NORMALMAP");else material.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
        }
        public static void Finish(Renderer renderer,string id)
        {
            if(renderer==null)return;
            foreach(var material in renderer.sharedMaterials)Style(material);
            Mesh original=renderer is SkinnedMeshRenderer s?s.sharedMesh:renderer.GetComponent<MeshFilter>().sharedMesh;
            if(original.name.StartsWith("Finished ",StringComparison.Ordinal))return;
            string path=RunnerSceneBuilder.Generated+"/Meshes/Finished_"+id.Replace(' ','_')+".asset";
            if(!cache.TryGetValue(path,out Mesh finished))
            {
                finished=BuildSurface(original,id);finished=RunnerSceneBuilder.Upsert(finished,path);cache[path]=finished;
            }
            if(renderer is SkinnedMeshRenderer skin)skin.sharedMesh=finished;else renderer.GetComponent<MeshFilter>().sharedMesh=finished;
        }
        static Mesh BuildSurface(Mesh original,string id)
        {
            var mesh=Object.Instantiate(original);mesh.name="Finished "+id;
            var vertices=mesh.vertices;var oldNormals=mesh.normals;var normals=mesh.normals;
            if(normals.Length!=vertices.Length){mesh.RecalculateNormals();normals=mesh.normals;}
            var groups=new Dictionary<Vector3Int,List<int>>();float quantum=Mathf.Max(mesh.bounds.size.magnitude*.000001f,.0000001f);
            for(int i=0;i<vertices.Length;i++)
            {
                var p=vertices[i]/quantum;var key=new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));
                if(!groups.TryGetValue(key,out var list)){list=new List<int>();groups.Add(key,list);}list.Add(i);
            }
            // Join only coincident shading seams with compatible normals, never move vertices.
            var smooth=(Vector3[])normals.Clone();
            foreach(var list in groups.Values)if(list.Count>1)foreach(int i in list)
            {
                Vector3 sum=Vector3.zero;foreach(int j in list)if(Vector3.Dot(normals[i],normals[j])>.55f)sum+=normals[j];
                if(sum.sqrMagnitude>.00001f)smooth[i]=sum.normalized;
            }
            mesh.normals=smooth;mesh.RecalculateTangents();
            var colors=new Color[vertices.Length];var referenced=new bool[vertices.Length];int referencedCount=0,skippedGroups=0,degenerateGroups=0,aoRays=0;
            // Removed triangle islands retain their vertex slots and zero normals.
            // They are not surfaces to bake; leave those colors neutral white.
            foreach(int index in mesh.triangles)if(!referenced[index]){referenced[index]=true;referencedCount++;}
            for(int i=0;i<colors.Length;i++)colors[i]=Color.white;
            float distance=mesh.bounds.size.y*.085f,bias=Mathf.Max(distance*.015f,.00002f);
            var temp=new GameObject("Offline character contact bake"){hideFlags=HideFlags.HideAndDontSave};
            var collider=temp.AddComponent<MeshCollider>();collider.sharedMesh=original;
            try
            {
                foreach(var list in groups.Values)
                {
                    int i=-1;foreach(int candidate in list)if(referenced[candidate]){i=candidate;break;}
                    if(i<0){skippedGroups++;continue;}
                    Vector3 n=smooth[i];float normalLength=n.sqrMagnitude;
                    if(!Finite(vertices[i])||!Finite(n)||float.IsNaN(normalLength)||float.IsInfinity(normalLength))
                        throw new InvalidOperationException("Non-finite referenced AO vertex/normal in "+id+" at "+i);
                    // Procedural sphere poles can be referenced only by zero-area
                    // triangles. Their old zero-direction ray misses left the whole
                    // coincident group white; retain that neutral AO without casting.
                    if(normalLength==0f){degenerateGroups++;continue;}
                    // Preserve the existing valid normal/basis exactly. Normalize only
                    // a non-unit imported normal; never invent a direction for zero.
                    if(Mathf.Abs(normalLength-1f)>.0001f)n/=Mathf.Sqrt(normalLength);
                    Vector3 t=Vector3.Cross(n,Mathf.Abs(n.y)<.9f?Vector3.up:Vector3.right).normalized,b=Vector3.Cross(n,t);
                    float blocked=0;const int rays=16;
                    for(int ray=0;ray<rays;ray++)
                    {
                        float radius=Mathf.Sqrt((ray+.5f)/rays),angle=ray*2.39996323f;
                        Vector3 direction=t*(Mathf.Cos(angle)*radius)+b*(Mathf.Sin(angle)*radius)+n*Mathf.Sqrt(1-radius*radius);
                        var sample=new Ray(vertices[i]+n*bias,direction);
                        if(!Finite(sample.direction)||Mathf.Abs(sample.direction.sqrMagnitude-1f)>.0001f)
                            throw new InvalidOperationException("Invalid AO ray in "+id+" at vertex "+i+", sample "+ray);
                        aoRays++;
                        if(collider.Raycast(sample,out RaycastHit hit,distance))blocked+=1-hit.distance/distance;
                    }
                    float ao=Mathf.Clamp(1-blocked/rays*.65f,.55f,1);
                    foreach(int index in list)if(referenced[index])colors[index]=new Color(ao,ao,ao,1);
                }
            }
            finally{Object.DestroyImmediate(temp);}
            mesh.colors=colors;
            Debug.Log("Offline character AO "+id+": "+referencedCount+" referenced vertices, "+(vertices.Length-referencedCount)+" unused vertices, "+skippedGroups+" unreferenced groups skipped, "+degenerateGroups+" degenerate-normal groups skipped, "+aoRays+" valid rays.");
            var check=new MeshCheck{name=id,vertices=vertices.Length,triangles=mesh.triangles.Length/3,
                geometryUnchanged=vertices.SequenceEqual(original.vertices)&&mesh.triangles.SequenceEqual(original.triangles),
                skinningUnchanged=mesh.boneWeights.SequenceEqual(original.boneWeights)&&mesh.bindposes.SequenceEqual(original.bindposes),
                uvUnchanged=mesh.uv.SequenceEqual(original.uv),minimumAO=colors.Min(c=>c.r),meanAO=colors.Average(c=>c.r)};
            for(int i=0;i<oldNormals.Length;i++)if(Vector3.Angle(oldNormals[i],smooth[i])>.1f)check.normalChanges++;
            measured.Add(check);return mesh;
        }
        static bool Finite(Vector3 value)
        {return !float.IsNaN(value.x)&&!float.IsInfinity(value.x)&&!float.IsNaN(value.y)&&!float.IsInfinity(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.z);}
        static string Structure(GameObject root)
        {
            var text=new System.Text.StringBuilder();
            foreach(var t in root.GetComponentsInChildren<Transform>(true))text.Append(t.name).Append(t.localPosition.ToString("R")).Append(t.localRotation.ToString("R")).Append(t.localScale.ToString("R"));
            foreach(var b in root.GetComponentsInChildren<BoxCollider>())text.Append(b.center.ToString("R")).Append(b.size.ToString("R")).Append(b.isTrigger);
            foreach(var c in root.GetComponentsInChildren<CharacterController>())text.Append(c.center.ToString("R")).Append(c.radius).Append(c.height);
            foreach(var r in root.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials)
                text.Append(m.name).Append(m.GetColor("_BaseColor").ToString("R")).Append(AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap")));
            return text.ToString();
        }
    }
}
