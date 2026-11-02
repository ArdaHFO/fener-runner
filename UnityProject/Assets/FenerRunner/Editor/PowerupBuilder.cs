using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FenerRunner.Editor
{
    internal static class PowerupBuilder
    {
        static readonly string[] names={"Dash","Flight","Magnet","Shield","DoubleCoins","SlowMotion","SuperJump","CoinRain","ScoreBoost","FeverRush","AirStep","LaneSweep","Rhythm","GoldenTrail","Encore"};
        static readonly string[] colors={"FFD33D","57DFFF","DB83FF","69F5B8","FFAF52","80B3FF","FF7B96","D4EF6B","FFE8A2","FF6F39","9CDFFF","5BFFD4","F3A6E8","FFDB70","AFA4FF"};
        public static GameObject[] BuildPickups()
        {
            var result=new GameObject[names.Length];
            for(int i=0;i<names.Length;i++)
            {
                var root=new GameObject("Powerup "+names[i]);
                var color=Material("Power"+names[i],colors[i],.65f);
                var backing=Material("PowerNavy","11223D",.05f);
                MeshObject(root,"Outer ring",Ring(.67f,.045f,36),color);
                Primitive(root,"Navy medallion",PrimitiveType.Cylinder,new Vector3(0,0,.05f),new Vector3(1.14f,.035f,1.14f),new Vector3(90,0,0),backing);
                if(i==0)
                {
                    Polygon(root,"Upper bolt",new[]{new Vector2(.12f,.47f),new Vector2(-.34f,-.04f),new Vector2(.07f,-.04f)},color);
                    Polygon(root,"Lower bolt",new[]{new Vector2(-.12f,-.47f),new Vector2(.34f,.07f),new Vector2(-.07f,.07f)},color);
                }
                if(i==1)
                {
                    Polygon(root,"Left wing",new[]{new Vector2(-.06f,-.04f),new Vector2(-.43f,.30f),new Vector2(-.39f,-.12f),new Vector2(-.13f,-.30f)},color);
                    Polygon(root,"Right wing",new[]{new Vector2(.06f,-.04f),new Vector2(.43f,.30f),new Vector2(.39f,-.12f),new Vector2(.13f,-.30f)},color);
                    Primitive(root,"Feather spine",PrimitiveType.Cube,new Vector3(0,0,-.09f),new Vector3(.105f,.63f,.08f),Vector3.zero,color);
                }
                if(i==2)
                {
                    Primitive(root,"Magnet left",PrimitiveType.Cube,new Vector3(-.25f,.04f,-.09f),new Vector3(.16f,.58f,.10f),Vector3.zero,color);
                    Primitive(root,"Magnet right",PrimitiveType.Cube,new Vector3(.25f,.04f,-.09f),new Vector3(.16f,.58f,.10f),Vector3.zero,color);
                    Primitive(root,"Magnet base",PrimitiveType.Cube,new Vector3(0,-.23f,-.09f),new Vector3(.50f,.17f,.10f),Vector3.zero,color);
                    var tips=Material("PowerIvory","F7F4DE",.25f);
                    for(int s=-1;s<=1;s+=2)Primitive(root,"Magnet tip",PrimitiveType.Cube,new Vector3(s*.25f,.31f,-.09f),new Vector3(.16f,.16f,.11f),Vector3.zero,tips);
                }
                if(i==3)Polygon(root,"Shield face",new[]{new Vector2(-.33f,.30f),new Vector2(.33f,.30f),new Vector2(.28f,-.13f),new Vector2(0,-.43f),new Vector2(-.28f,-.13f)},color);
                if(i==4)
                {
                    for(int s=-1;s<=1;s+=2)
                    {
                        Primitive(root,"Double coin",PrimitiveType.Cylinder,new Vector3(s*.16f,s*.10f,-.12f-s*.025f),new Vector3(.58f,.035f,.58f),new Vector3(90,0,0),color);
                        Primitive(root,"Coin stamp",PrimitiveType.Cube,new Vector3(s*.16f,s*.10f,-.19f),new Vector3(.035f,.21f,.025f),Vector3.zero,backing);
                    }
                }
                if(i==5)
                {
                    Primitive(root,"Hourglass top",PrimitiveType.Cube,new Vector3(0,.34f,-.10f),new Vector3(.64f,.07f,.09f),Vector3.zero,color);
                    Primitive(root,"Hourglass base",PrimitiveType.Cube,new Vector3(0,-.34f,-.10f),new Vector3(.64f,.07f,.09f),Vector3.zero,color);
                    Polygon(root,"Upper hourglass",new[]{new Vector2(-.27f,.27f),new Vector2(.27f,.27f),new Vector2(0,-.015f)},color);
                    Polygon(root,"Lower hourglass",new[]{new Vector2(-.27f,-.27f),new Vector2(.27f,-.27f),new Vector2(0,.015f)},color);
                }
                if(i==6)
                {
                    for(int j=0;j<3;j++)Primitive(root,"Spring stroke",PrimitiveType.Cube,new Vector3(0,-.30f+j*.14f,-.10f),new Vector3(.50f-j*.07f,.055f,.10f),Vector3.zero,color);
                    Polygon(root,"Jump arrow left",new[]{new Vector2(-.29f,.10f),new Vector2(0,.42f),new Vector2(0,.23f),new Vector2(-.19f,.04f)},color);
                    Polygon(root,"Jump arrow right",new[]{new Vector2(.29f,.10f),new Vector2(0,.42f),new Vector2(0,.23f),new Vector2(.19f,.04f)},color);
                }
                if(i==7)
                {
                    for(int j=-1;j<=1;j++)
                    {
                        Primitive(root,"Rain cloud",PrimitiveType.Sphere,new Vector3(j*.20f,j==0?.28f:.20f,-.10f),new Vector3(.34f,.27f,.12f),Vector3.zero,color);
                        Primitive(root,"Falling coin",PrimitiveType.Cylinder,new Vector3(j*.23f,-.17f-Mathf.Abs(j)*.08f,-.10f),new Vector3(.12f,.035f,.12f),new Vector3(90,0,0),color);
                    }
                }
                if(i==8)
                {
                    Polygon(root,"Trophy cup",new[]{new Vector2(-.29f,.31f),new Vector2(.29f,.31f),new Vector2(.19f,-.08f),new Vector2(-.19f,-.08f)},color);
                    Primitive(root,"Trophy stem",PrimitiveType.Cube,new Vector3(0,-.18f,-.10f),new Vector3(.09f,.23f,.10f),Vector3.zero,color);
                    Primitive(root,"Trophy foot",PrimitiveType.Cube,new Vector3(0,-.34f,-.10f),new Vector3(.45f,.08f,.10f),Vector3.zero,color);
                    for(int s=-1;s<=1;s+=2){var handle=MeshObject(root,"Cup handle",Ring(.12f,.025f,20),color);handle.transform.localPosition=new Vector3(s*.31f,.15f,-.04f);}
                }
                if(i==9)
                {
                    Polygon(root,"Upper flame",new[]{new Vector2(0,.49f),new Vector2(.26f,-.02f),new Vector2(-.26f,-.02f)},color);
                    Polygon(root,"Lower flame",new[]{new Vector2(-.26f,.04f),new Vector2(.26f,.04f),new Vector2(.24f,-.24f),new Vector2(0,-.43f),new Vector2(-.24f,-.24f)},color);
                    var glow=Material("PowerFlameCore","FFF1B0",.9f);
                    var core=new GameObject("Flame core");core.transform.SetParent(root.transform,false);core.transform.localPosition=new Vector3(0,-.07f,-.035f);
                    Polygon(core,"Inner flame",new[]{new Vector2(0,.28f),new Vector2(.11f,-.09f),new Vector2(0,-.24f),new Vector2(-.11f,-.09f)},glow);
                }
                if(i==10)
                {
                    for(int j=0;j<2;j++)
                    {
                        var arrow=new GameObject("Air step "+j);arrow.transform.SetParent(root.transform,false);arrow.transform.localPosition=new Vector3(0,j*.35f-.20f,0);
                        Polygon(arrow,"Left chevron",new[]{new Vector2(-.30f,-.07f),new Vector2(0,.22f),new Vector2(0,.06f),new Vector2(-.22f,-.14f)},color);
                        Polygon(arrow,"Right chevron",new[]{new Vector2(.30f,-.07f),new Vector2(0,.22f),new Vector2(0,.06f),new Vector2(.22f,-.14f)},color);
                    }
                }
                if(i==11)
                {
                    for(int j=-1;j<=1;j++)Primitive(root,"Lane rail",PrimitiveType.Cube,new Vector3(j*.26f,0,-.09f),new Vector3(.035f,.78f,.08f),Vector3.zero,color);
                    Primitive(root,"Sweep bar",PrimitiveType.Cube,new Vector3(0,-.03f,-.13f),new Vector3(.82f,.085f,.08f),Vector3.zero,color);
                    Polygon(root,"Sweep left",new[]{new Vector2(-.45f,-.03f),new Vector2(-.25f,.13f),new Vector2(-.25f,-.19f)},color);
                    Polygon(root,"Sweep right",new[]{new Vector2(.45f,-.03f),new Vector2(.25f,.13f),new Vector2(.25f,-.19f)},color);
                }
                if(i==12)
                {
                    for(int j=-2;j<=2;j++)Primitive(root,"Rhythm bar",PrimitiveType.Cube,new Vector3(j*.16f,0,-.10f),new Vector3(.09f,.78f-Mathf.Abs(j)*.22f,.08f),Vector3.zero,color);
                }
                if(i==13)
                {
                    for(int j=0;j<3;j++)Primitive(root,"Pass coin",PrimitiveType.Cylinder,new Vector3(-.28f+j*.28f,-.22f+j*.2f,-.11f),new Vector3(.28f,.035f,.28f),new Vector3(90,0,0),color);
                    Polygon(root,"Pass arrow",new[]{new Vector2(.03f,.38f),new Vector2(.43f,.38f),new Vector2(.43f,-.02f)},color);
                }
                if(i==14)
                {
                    var loop=MeshObject(root,"Encore time",Ring(.38f,.035f,32),color);loop.transform.localPosition=new Vector3(0,0,-.05f);
                    Primitive(root,"Plus horizontal",PrimitiveType.Cube,new Vector3(0,0,-.14f),new Vector3(.42f,.09f,.06f),Vector3.zero,color);
                    Primitive(root,"Plus vertical",PrimitiveType.Cube,new Vector3(0,0,-.14f),new Vector3(.09f,.42f,.06f),Vector3.zero,color);
                }
                result[i]=Save(root,"Powerup"+names[i]);
            }
            return result;
        }
        public static GameObject BuildFlightVisual()
        {
            var root=new GameObject("Flight visual");var blue=Material("PowerFlight","57DFFF",.65f);
            var silver=Material("PowerIvory","F7F4DE",.25f);var navy=Material("PowerNavy","11223D",.05f);
            Primitive(root,"Flight pack",PrimitiveType.Cube,new Vector3(0,0,-.25f),new Vector3(.46f,.63f,.35f),Vector3.zero,navy);
            for(int s=-1;s<=1;s+=2)
            {
                Primitive(root,"Wing spar",PrimitiveType.Cube,new Vector3(s*.77f,.16f,-.11f),new Vector3(1.18f,.16f,.35f),new Vector3(0,0,s*12),silver);
                for(int j=0;j<4;j++)Primitive(root,"Wing feather",PrimitiveType.Cube,new Vector3(s*(.36f+j*.25f),.12f-j*.055f,-.36f-j*.02f),new Vector3(.21f,.10f,.76f-j*.10f),new Vector3(-6,0,s*12),blue);
                Primitive(root,"Jet nozzle",PrimitiveType.Cylinder,new Vector3(s*.22f,-.31f,-.3f),new Vector3(.11f,.16f,.11f),Vector3.zero,silver);
                Primitive(root,"Jet glow",PrimitiveType.Sphere,new Vector3(s*.22f,-.58f,-.3f),new Vector3(.13f,.43f,.13f),Vector3.zero,blue);
            }
            return Save(root,"PowerFlightRig");
        }
        public static GameObject BuildShieldVisual()
        {
            var root=new GameObject("Shield visual");var mint=Material("PowerShield","69F5B8",.65f);
            var a=MeshObject(root,"Shield orbit",Ring(.91f,.022f,48),mint);a.transform.localRotation=Quaternion.Euler(90,0,0);
            var b=MeshObject(root,"Shield meridian",Ring(1.17f,.018f,48),mint);b.transform.localScale=new Vector3(.73f,1,1);
            return Save(root,"PowerShieldRig");
        }
        public static GameObject[] BuildAbilityVisuals()
        {
            var result=new GameObject[names.Length-4];
            for(int i=0;i<result.Length;i++)
            {
                int kind=i+4;var root=new GameObject(names[kind]+" active visual");var color=Material("Power"+names[kind],colors[kind],.65f);
                if(i==0)
                {
                    for(int s=-1;s<=1;s+=2)Primitive(root,"Orbiting double coin",PrimitiveType.Cylinder,new Vector3(s*.68f,1.13f,0),new Vector3(.27f,.035f,.27f),new Vector3(90,0,0),color);
                    var orbit=MeshObject(root,"Coin orbit",Ring(.68f,.012f,40),color);orbit.transform.localRotation=Quaternion.Euler(90,0,0);orbit.transform.localPosition=Vector3.up*1.13f;
                }
                if(i==1)
                {
                    var clockFace=MeshObject(root,"Slow-time clock",Ring(.32f,.025f,40),color);clockFace.transform.localPosition=new Vector3(0,1.23f,-.66f);
                    Primitive(root,"Minute hand",PrimitiveType.Cube,new Vector3(0,1.34f,-.74f),new Vector3(.035f,.22f,.03f),Vector3.zero,color);
                    Primitive(root,"Hour hand",PrimitiveType.Cube,new Vector3(.075f,1.23f,-.74f),new Vector3(.15f,.035f,.03f),Vector3.zero,color);
                    for(int s=-1;s<=1;s+=2)Primitive(root,"Clock marker",PrimitiveType.Cube,new Vector3(s*.31f,1.23f,-.73f),new Vector3(.07f,.035f,.03f),Vector3.zero,color);
                }
                if(i==2)
                {
                    for(int s=-1;s<=1;s+=2)for(int j=0;j<6;j++)
                    {
                        var spring=MeshObject(root,"Spring shoe coil",Ring(.12f,.018f,16),color);
                        spring.transform.localRotation=Quaternion.Euler(90,0,0);spring.transform.localPosition=new Vector3(s*.23f,.07f+j*.045f,0);
                    }
                }
                if(i==3)
                {
                    var ivory=Material("PowerIvory","F7F4DE",.25f);
                    for(int j=-1;j<=1;j++)
                    {
                        Primitive(root,"Reward cloud",PrimitiveType.Sphere,new Vector3(j*.22f,2.5f+(j==0?.08f:0),0),new Vector3(.37f,.22f,.21f),Vector3.zero,ivory);
                        Primitive(root,"Reward shower",PrimitiveType.Cylinder,new Vector3(j*.24f,2.18f-Mathf.Abs(j)*.05f,-.05f),new Vector3(.12f,.025f,.12f),new Vector3(90,0,0),color);
                    }
                }
                if(i==4)
                {
                    for(int s=-1;s<=1;s+=2)
                    {
                        var star=new GameObject("Score star");star.transform.SetParent(root.transform,false);star.transform.localPosition=new Vector3(s*.65f,1.45f,0);star.transform.localScale=Vector3.one*.42f;
                        for(int p=0;p<5;p++)
                        {
                            float a=(90+p*72)*Mathf.Deg2Rad,b=(90+p*72+36)*Mathf.Deg2Rad,c=(90+p*72-36)*Mathf.Deg2Rad;
                            Polygon(star,"Star point",new[]{new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.5f,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.22f,new Vector2(Mathf.Cos(c),Mathf.Sin(c))*.22f},color);
                        }
                    }
                }
                if(i==5)
                {
                    for(int s=-1;s<=1;s+=2)
                    {
                        var flame=new GameObject("Tribune flare");flame.transform.SetParent(root.transform,false);flame.transform.localPosition=new Vector3(s*.55f,.35f,-.20f);
                        Polygon(flame,"Rising flare",new[]{new Vector2(0,.95f),new Vector2(.12f,.28f),new Vector2(.09f,0),new Vector2(-.09f,0),new Vector2(-.12f,.28f)},color);
                    }
                }
                if(i==6)
                {
                    for(int s=-1;s<=1;s+=2)
                    {
                        var step=MeshObject(root,"Air stepping stone",Ring(.28f,.025f,28),color);step.transform.localRotation=Quaternion.Euler(90,0,0);step.transform.localPosition=new Vector3(s*.25f,-.08f,0);
                        Primitive(root,"Air tread",PrimitiveType.Cube,new Vector3(s*.25f,-.08f,0),new Vector3(.28f,.018f,.48f),Vector3.zero,color);
                    }
                }
                if(i==7)
                {
                    Primitive(root,"Coin collection sweep",PrimitiveType.Cube,new Vector3(0,.72f,0),new Vector3(6.65f,.025f,.025f),Vector3.zero,color);
                    for(int lane=-1;lane<=1;lane++)
                    {
                        var scanner=MeshObject(root,"Lane collector",Ring(.17f,.018f,24),color);scanner.transform.localPosition=new Vector3(lane*3.3f,.72f,0);
                    }
                }
                if(i==8)
                {
                    for(int j=-2;j<=2;j++)Primitive(root,"Rhythm pulse",PrimitiveType.Cube,new Vector3(j*.21f,.14f,-.5f),new Vector3(.08f,.27f-Mathf.Abs(j)*.07f,.08f),Vector3.zero,color);
                }
                if(i==9)
                {
                    for(int s=-1;s<=1;s+=2)for(int j=0;j<3;j++)Primitive(root,"Golden pass spark",PrimitiveType.Cylinder,new Vector3(s*(.4f+j*.14f),.32f+j*.15f,-.25f),new Vector3(.1f,.02f,.1f),new Vector3(90,0,0),color);
                }
                if(i==10)
                {
                    var ring=MeshObject(root,"Encore halo",Ring(.7f,.025f,36),color);ring.transform.localRotation=Quaternion.Euler(90,0,0);ring.transform.localPosition=Vector3.up*.2f;
                    Primitive(root,"Time plus vertical",PrimitiveType.Cube,new Vector3(.68f,1.4f,0),new Vector3(.06f,.3f,.06f),Vector3.zero,color);
                    Primitive(root,"Time plus horizontal",PrimitiveType.Cube,new Vector3(.68f,1.4f,0),new Vector3(.3f,.06f,.06f),Vector3.zero,color);
                }
                result[i]=Save(root,"Power"+names[kind]+"Rig");
            }
            return result;
        }
        static Material Material(string name,string hex,float emission)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            var mat=new Material(shader){name=name,enableInstancing=true};ColorUtility.TryParseHtmlString("#"+hex,out Color color);
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.5f);mat.SetFloat("_Metallic",.12f);
            mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*emission);
            return RunnerSceneBuilder.Upsert(mat,RunnerSceneBuilder.Generated+"/Materials/"+name+".mat");
        }
        static GameObject Primitive(GameObject parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Vector3 rotation,Material mat)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent.transform,false);
            go.transform.localPosition=position;go.transform.localScale=scale;go.transform.localRotation=Quaternion.Euler(rotation);
            Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;return go;
        }
        static GameObject MeshObject(GameObject parent,string name,Mesh mesh,Material mat)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent.transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;return go;
        }
        static void Polygon(GameObject root,string name,Vector2[] points,Material mat)
        {
            float signedArea=0;
            for(int i=0;i<points.Length;i++)
            {Vector2 a=points[i],b=points[(i+1)%points.Length];signedArea+=a.x*b.y-b.x*a.y;}
            if(signedArea<0)Array.Reverse(points);
            var v=new List<Vector3>();var triangles=new List<int>();
            for(int side=0;side<2;side++)foreach(var p in points)v.Add(new Vector3(p.x,p.y,side==0?-.11f:-.045f));
            for(int i=1;i<points.Length-1;i++)
            {triangles.Add(0);triangles.Add(i+1);triangles.Add(i);triangles.Add(points.Length);triangles.Add(points.Length+i);triangles.Add(points.Length+i+1);}
            for(int i=0;i<points.Length;i++)
            {int n=(i+1)%points.Length;triangles.AddRange(new[]{i,n,n+points.Length,i,n+points.Length,i+points.Length});}
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();MeshObject(root,name,mesh,mat);
        }
        static Mesh Ring(float radius,float thickness,int segments)
        {
            const int sides=6;var v=new Vector3[segments*sides];var t=new int[segments*sides*6];int k=0;
            for(int i=0;i<segments;i++)for(int j=0;j<sides;j++)
            {
                float a=i*Mathf.PI*2/segments,b=j*Mathf.PI*2/sides,r=radius+Mathf.Cos(b)*thickness;
                v[i*sides+j]=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,Mathf.Sin(b)*thickness-.06f);
                int n=((i+1)%segments)*sides+j,q=i*sides+(j+1)%sides,z=((i+1)%segments)*sides+(j+1)%sides;
                t[k++]=i*sides+j;t[k++]=n;t[k++]=q;t[k++]=q;t[k++]=n;t[k++]=z;
            }
            var mesh=new Mesh();mesh.vertices=v;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static GameObject Save(GameObject root,string name)
        {
            // Each badge/rig is combined by material once in the Editor. No runtime mesh creation.
            var filters=root.GetComponentsInChildren<MeshFilter>();var materials=filters.Select(f=>f.GetComponent<MeshRenderer>().sharedMaterial).Distinct().ToArray();
            for(int i=0;i<materials.Length;i++)
            {
                var instances=filters.Where(f=>f.GetComponent<MeshRenderer>().sharedMaterial==materials[i]).Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.transform.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray();
                var mesh=new Mesh{name=name+"_"+i};mesh.CombineMeshes(instances,true,true);mesh.RecalculateBounds();
                mesh=RunnerSceneBuilder.Upsert(mesh,RunnerSceneBuilder.Generated+"/Meshes/"+name+"_"+i+".asset");
                MeshObject(root,"Batched "+i,mesh,materials[i]);
            }
            foreach(var f in filters)Object.DestroyImmediate(f.gameObject);
            foreach(var r in root.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;}
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,RunnerSceneBuilder.Generated+"/Prefabs/"+name+".prefab");Object.DestroyImmediate(root);return prefab;
        }
    }
}
