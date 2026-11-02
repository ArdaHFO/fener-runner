using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FenerRunner.Editor
{
    // Shared per-LOD geometry. UV1 records body part/gesture; UV2 stores stable
    // foot anchors and height. All joint animation stays on the GPU.
    internal sealed class SupporterBuilder
    {
        readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
        readonly List<Color32> colors=new List<Color32>();
        readonly List<Vector4> motion=new List<Vector4>(),anchors=new List<Vector4>(),personas=new List<Vector4>();
        readonly List<int> triangles=new List<int>();
        bool distant;Vector3 feet;float height=1,phase,side=1,width=1,depth=1,headWidth=1,identity;int gesture;
        readonly Color yellow=new Color(.97f,.76f,.08f),navy=new Color(.025f,.07f,.18f);
        Vector3 Human(Vector3 p)=>feet+new Vector3(-side*p.z,p.y,side*p.x)*height;
        Vector3 Direction(Vector3 n)=>new Vector3(-side*n.z,n.y,side*n.x);
        void Vertex(Vector3 p,Vector3 n,Color color,int part,float arm=0,float cloth=0)
        {
            bool flag=part>=8;
            vertices.Add(flag?p:Human(p));normals.Add(flag?n:Direction(n));colors.Add(color);
            motion.Add(new Vector4(phase,part,arm,flag?cloth:gesture));
            anchors.Add(new Vector4(feet.x,feet.y,feet.z,height));
            personas.Add(new Vector4(width,depth,headWidth,identity));
        }
        void Ellipsoid(Vector3 p,Vector3 size,Color color,int part,float arm=0,int longitude=5,int latitude=3)
        {
            int start=vertices.Count;
            if(distant)
            {
                foreach(var c in new[]{new Vector2(-1,-1),new Vector2(-1,1),new Vector2(1,1),new Vector2(1,-1)})
                    Vertex(p+new Vector3(c.x*size.x,c.y*size.y,0),Vector3.forward,color,part,arm);
                triangles.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});return;
            }
            for(int y=0;y<=latitude;y++)for(int x=0;x<=longitude;x++)
            {
                float a=y*Mathf.PI/latitude,b=x*Mathf.PI*2/longitude;
                Vector3 n=new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b));
                Vector3 surfaceNormal=new Vector3(n.x/size.x,n.y/size.y,n.z/size.z).normalized;
                Vertex(p+Vector3.Scale(n,size),surfaceNormal,color,part,arm);
            }
            for(int y=0;y<latitude;y++)for(int x=0;x<longitude;x++)
            {
                int i=start+y*(longitude+1)+x;
                // Pole rows share a point. Keep one genuine triangle per cell,
                // rather than spending half the small-sphere budget on zero area.
                if(y>0)triangles.AddRange(new[]{i,i+longitude+1,i+1});
                if(y<latitude-1)triangles.AddRange(new[]{i+1,i+longitude+1,i+longitude+2});
            }
        }
        void Bar(Vector3 a,Vector3 b,float radius,Color color,int part,float arm=0)
        {
            Vector3 mid=(a+b)*.5f;Quaternion q=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
            int start=vertices.Count;Ellipsoid(Vector3.zero,new Vector3(radius,(b-a).magnitude*.5f,radius),color,part,arm);
            for(int i=start;i<vertices.Count;i++)
            {
                if(part>=8){vertices[i]=mid+q*vertices[i];normals[i]=q*normals[i];}
                else
                {
                    Vector3 d=(vertices[i]-feet)/height;
                    Vector3 local=new Vector3(d.z*side,d.y,-d.x*side);
                    Vector3 n=normals[i];n=new Vector3(n.z*side,n.y,-n.x*side);
                    vertices[i]=Human(mid+q*local);normals[i]=Direction(q*n);
                }
            }
        }
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,int part)
        {
            int start=vertices.Count;Vector3 n=Vector3.Cross(c-a,b-a).normalized;
            Vertex(a,n,color,part);Vertex(b,n,color,part);Vertex(c,n,color,part);Vertex(d,n,color,part);
            triangles.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
        }
        int detailRow;
        const float ShoulderY=1.28f,ArmLength=.30f;
        static readonly float[] torsoY={.735f,.84f,1.0f,1.17f,1.285f,1.335f,1.365f};
        static readonly float[] torsoX={.163f,.164f,.153f,.192f,.214f,.145f,.062f};
        static readonly float[] torsoZ={.104f,.112f,.104f,.120f,.113f,.083f,.058f};
        static readonly float[] headY={1.421f,1.442f,1.478f,1.520f,1.567f,1.612f,1.650f,1.664f};
        static readonly float[] headX={.034f,.062f,.081f,.095f,.092f,.079f,.046f,.008f};
        static readonly float[] headZ={.045f,.066f,.084f,.103f,.101f,.086f,.054f,.012f};
        void Shoe(float x,Color color)
        {
            // Horizontal contour rings form a flat sole, rounded toe and heel.
            // Two-latitude ellipsoids previously looked like separate diamonds.
            Tube(new[]{new Vector3(x,.006f,.053f),new Vector3(x,.035f,.053f),new Vector3(x,.084f,.012f)},
                new[]{.061f,.062f,.041f},new[]{.125f,.125f,.063f},color,7,0,detailRow<2?8:6);
        }
        void Neck(Color skin)
        {
            if(distant){Ellipsoid(new Vector3(0,1.393f,0),new Vector3(.044f,.065f,.048f),skin,1);return;}
            Tube(new[]{new Vector3(0,1.344f,0),new Vector3(0,1.392f,0),new Vector3(0,1.448f,.004f)},
                new[]{.047f,.041f,.043f},new[]{.050f,.045f,.047f},skin,1,0,6);
        }
        void Tube(Vector3[] centres,float[] radiiX,float[] radiiZ,Color color,int part,float arm=0,int sides=6)
        {
            if(distant){Bar(centres[0],centres[centres.Length-1],Mathf.Max(radiiX[0],radiiX[radiiX.Length-1]),color,part,arm);return;}
            int start=vertices.Count;
            for(int ring=0;ring<centres.Length;ring++)for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides;Vector3 normal=new Vector3(Mathf.Cos(a)/radiiX[ring],0,Mathf.Sin(a)/radiiZ[ring]).normalized;
                // Smooth the longitudinal taper as well as the circumference.
                int previous=Mathf.Max(0,ring-1),next=Mathf.Min(centres.Length-1,ring+1);
                float dy=centres[next].y-centres[previous].y;
                if(Mathf.Abs(dy)>.0001f)normal.y=-(Mathf.Cos(a)*Mathf.Cos(a)*(radiiX[next]-radiiX[previous])+Mathf.Sin(a)*Mathf.Sin(a)*(radiiZ[next]-radiiZ[previous]))/dy;
                Vertex(centres[ring]+new Vector3(Mathf.Cos(a)*radiiX[ring],0,Mathf.Sin(a)*radiiZ[ring]),normal.normalized,color,part,arm);
            }
            for(int ring=0;ring<centres.Length-1;ring++)for(int i=0;i<sides;i++)
            {int a=start+ring*sides+i,b=start+ring*sides+(i+1)%sides;triangles.AddRange(new[]{a,a+sides,b,b,a+sides,b+sides});}
            for(int i=1;i<sides-1;i++)triangles.AddRange(new[]{start,start+i+1,start+i,start+(centres.Length-1)*sides,start+(centres.Length-1)*sides+i,start+(centres.Length-1)*sides+i+1});
        }
        void Torso(Color color)
        {
            if(distant){Ellipsoid(new Vector3(0,1.04f,0),new Vector3(.205f*width,.315f,.12f*depth),color,1);return;}
            int[] rings=detailRow<2?new[]{0,1,2,3,4,6}:new[]{0,2,3,4,6};
            var centres=new Vector3[rings.Length];var rx=new float[rings.Length];var rz=new float[rings.Length];
            for(int i=0;i<centres.Length;i++){int r=rings[i];centres[i]=new Vector3(0,torsoY[r],0);rx[i]=torsoX[r]*width;rz[i]=torsoZ[r]*depth;}
            Tube(centres,rx,rz,color,1,0,detailRow<2?10:6);
        }
        void JerseyStripe(float centre,Color color)
        {
            int start=vertices.Count;
            for(int ring=0;ring<5;ring++)for(int edge=0;edge<2;edge++)
            {
                float x=(centre+(edge==0?-.018f:.018f))*width;
                float z=torsoZ[ring]*depth*Mathf.Sqrt(Mathf.Max(0,1-x*x/(torsoX[ring]*torsoX[ring]*width*width)))+.0035f;
                Vertex(new Vector3(x,torsoY[ring]+.003f,z),new Vector3(x/(torsoX[ring]*width),0,1).normalized,color,1);
            }
            for(int i=0;i<4;i++){int k=start+i*2;triangles.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}
        }
        void ChestBand(Color color)
        {
            int start=vertices.Count;const int sections=6;
            for(int y=0;y<2;y++)for(int i=0;i<=sections;i++)
            {
                float a=.18f+(Mathf.PI-.36f)*i/sections;
                Vertex(new Vector3(Mathf.Cos(a)*.195f*width,1.115f+y*.042f,Mathf.Sin(a)*.123f*depth),new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),color,1);
            }
            for(int i=0;i<sections;i++){int k=start+i;triangles.AddRange(new[]{k,k+sections+1,k+1,k+1,k+sections+1,k+sections+2});}
        }
        void FacePatch(float x,float y,float rx,float ry,float z,Color color,int segments=6)
        {
            int start=vertices.Count;float offset=z-FaceSurface(x,y);Vertex(new Vector3(x,y,z+.0005f),Vector3.forward,color,2);
            for(int i=0;i<=segments;i++){float a=i*2*Mathf.PI/segments,px=x+rx*Mathf.Cos(a),py=y+ry*Mathf.Sin(a);Vertex(new Vector3(px,py,FaceSurface(px,py)+offset),Vector3.forward,color,2);}
            for(int i=0;i<segments;i++)triangles.AddRange(new[]{start,start+i+2,start+i+1});
        }
        float FaceSurface(float x,float y)
        {
            int[] rings=detailRow<2?new[]{0,1,2,3,4,5,7}:new[]{0,3,5,7};
            int lower=0;while(lower<rings.Length-2&&y>headY[rings[lower+1]])lower++;
            int a=rings[lower],b=rings[lower+1];float t=Mathf.InverseLerp(headY[a],headY[b],y);
            float rx=Mathf.Lerp(headX[a],headX[b],t)*headWidth,rz=Mathf.Lerp(headZ[a],headZ[b],t);
            int sides=detailRow<2?10:6;float step=2*Mathf.PI/sides,angle=Mathf.Acos(Mathf.Clamp(x/rx,-1,1));
            float a0=Mathf.Floor(angle/step)*step,a1=a0+step,x0=Mathf.Cos(a0)*rx,x1=Mathf.Cos(a1)*rx;
            return .008f+Mathf.Lerp(Mathf.Sin(a0)*rz,Mathf.Sin(a1)*rz,Mathf.InverseLerp(x0,x1,x));
        }
        void Head(Color skin)
        {
            if(distant){Ellipsoid(new Vector3(0,1.54f,.008f),new Vector3(.095f*headWidth,.125f,.105f),skin,2);return;}
            // Jaw, cheekbone, temple and forehead are separate curved rings.
            // The old almost-spherical 4-head-high silhouette read as a doll.
            int[] rings=detailRow<2?new[]{0,1,2,3,4,5,7}:new[]{0,3,5,7};
            var centres=new Vector3[rings.Length];var rx=new float[rings.Length];var rz=new float[rings.Length];
            for(int i=0;i<rings.Length;i++){int r=rings[i];centres[i]=new Vector3(0,headY[r],.008f);rx[i]=headX[r]*headWidth;rz[i]=headZ[r];}
            Tube(centres,rx,rz,skin,2,0,detailRow<2?10:6);
        }
        void Hair(int style,Color color)
        {
            if(distant){Ellipsoid(new Vector3(0,1.624f,-.006f),new Vector3(.103f*headWidth,.055f,.108f),color,2);return;}
            int longitude=detailRow<2?10:6,latitude=detailRow<2?5:2,start=vertices.Count;
            // Curved shell with an open forehead, tapered sideburns and a real
            // hairline. Front/back lengths vary without a helmet-shaped rim.
            Vector3 size=new Vector3((style==4?.112f:.107f)*headWidth,style==4?.151f:.143f,style==4?.120f:.114f);
            for(int y=0;y<=latitude;y++)for(int x=0;x<=longitude;x++)
            {
                float b=x*Mathf.PI*2/longitude,front=Mathf.Max(0,Mathf.Sin(b));
                float limit=style==3?Mathf.Lerp(.83f,.37f,front):style==2?Mathf.Lerp(.53f,.37f,front):style==0?Mathf.Lerp(.58f,.30f,front):Mathf.Lerp(.64f,.35f,front);
                float a=y*Mathf.PI*limit/latitude;
                Vector3 n=new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b));
                Vector3 p=new Vector3(0,1.54f,.008f)+Vector3.Scale(n,size);
                if(style==1)p.x+=.008f*Mathf.Max(0,n.y);
                if(style==4)p+=n*(.004f*Mathf.Sin(b*5+a*7));
                Vertex(p,new Vector3(n.x/size.x,n.y/size.y,n.z/size.z).normalized,color,2);
            }
            for(int y=0;y<latitude;y++)for(int x=0;x<longitude;x++)
            {int i=start+y*(longitude+1)+x;if(y>0)triangles.AddRange(new[]{i,i+longitude+1,i+1});triangles.AddRange(new[]{i+1,i+longitude+1,i+longitude+2});}
        }
        void Nose(Color skin)
        {
            int start=vertices.Count;
            Vector3[] p={new Vector3(-.009f,1.566f,.108f),new Vector3(.009f,1.566f,.108f),new Vector3(-.012f,1.524f,.130f),new Vector3(.012f,1.524f,.130f),new Vector3(-.018f,1.505f,.114f),new Vector3(.018f,1.505f,.114f),new Vector3(0,1.511f,.139f)};
            foreach(var point in p)Vertex(point,new Vector3(point.x*20,.12f,1).normalized,skin,2);
            triangles.AddRange(new[]{start,start+2,start+1,start+1,start+2,start+3,start+2,start+6,start+3,start+2,start+4,start+6,start+6,start+5,start+3,start+4,start+5,start+6});
        }
        void Person(int n,int row,System.Random random)
        {
            detailRow=row;
            int look=(int)identity,clothes=look%5,hairStyle=(look/5)%6;
            Color shirt=clothes==0?yellow:clothes==3?new Color(.84f,.85f,.79f):navy,trim=clothes==0?navy:yellow;
            Color skin=Color.Lerp(new Color(.30f,.16f,.095f),new Color(.91f,.70f,.52f),(float)random.NextDouble());
            Color hair=Color.Lerp(new Color(.024f,.018f,.014f),new Color(.29f,.16f,.065f),(float)random.NextDouble());
            if(look%13==0)hair=new Color(.36f,.38f,.36f);
            Color trousers=look%3==0?new Color(.16f,.21f,.28f):new Color(.035f,.065f,.115f);
            Color shoes=look%4==0?new Color(.79f,.80f,.76f):new Color(.045f,.055f,.065f);
            Head(skin);
            Neck(skin);
            Hair(hairStyle,hairStyle==2?navy:hair);
            if(!distant)
            {
                if(hairStyle==2)
                {
                    // Curved baseball-cap visor projects from the hairline.
                    int cap=vertices.Count;
                    for(int ring=0;ring<2;ring++)for(int i=0;i<5;i++)
                    {float x=(i-2)*.038f;Vertex(new Vector3(x,1.589f-.008f*Mathf.Abs(i-2),ring==0?.074f:.167f-.012f*Mathf.Abs(i-2)),Vector3.up,trim,2);}
                    for(int i=0;i<4;i++){int k=cap+i;triangles.AddRange(new[]{k,k+5,k+1,k+1,k+5,k+6});}
                }
                if(hairStyle==5)Ellipsoid(new Vector3(.016f,1.535f,-.126f),new Vector3(.030f,.075f,.034f),hair,2,0,4,2);
                for(int e=-1;e<=1;e+=2)
                {
                    if(row<2)Ellipsoid(new Vector3(e*.094f*headWidth,1.529f,.005f),new Vector3(.015f,.027f,.015f),skin,2,0,6,2);
                    float x=e*.034f*headWidth;
                    float eyeZ=FaceSurface(x,1.552f)+.0018f;
                    if(row<2)FacePatch(x,1.552f,.017f,.0058f,eyeZ,Color.Lerp(skin,new Color(.78f,.77f,.70f),.60f),6);
                    FacePatch(x,1.552f,.0049f,.0051f,eyeZ+.001f,new Color(.035f,.029f,.023f),4);
                    FacePatch(x,1.568f,.020f,.0038f,FaceSurface(x,1.568f)+.0015f,hair,4);
                }
                FacePatch(0,1.483f,.024f,.004f,FaceSurface(0,1.483f)+.0015f,Color.Lerp(skin,new Color(.24f,.105f,.075f),.60f),row<2?6:4);
                Nose(skin);
                if(look%7==0)
                    FacePatch(0,1.452f,.035f,.015f,FaceSurface(0,1.452f)+.001f,Color.Lerp(skin,hair,.58f),row<2?6:4);
            }
            Torso(shirt);
            if(!distant)Ellipsoid(new Vector3(0,.748f,0),new Vector3(.161f*width,.083f,.106f*depth),trousers,0,0,6,2);
            for(int leg=-1;leg<=1;leg+=2)
            {
                if(row<2||distant)
                    Tube(new[]{new Vector3(leg*.096f*width,.77f,0),new Vector3(leg*.103f*width,.62f,.003f),new Vector3(leg*.105f*width,.42f,.012f),new Vector3(leg*.112f*width,.28f,.002f),new Vector3(leg*.115f*width,.087f,0)},
                        new[]{.078f*width,.076f*width,.053f,.062f,.039f},new[]{.088f*depth,.079f*depth,.054f,.061f,.041f},trousers,0,leg,row<2?6:4);
                else
                    Tube(new[]{new Vector3(leg*.096f*width,.77f,0),new Vector3(leg*.105f*width,.42f,.012f),new Vector3(leg*.112f*width,.28f,.002f),new Vector3(leg*.115f*width,.087f,0)},
                        new[]{.078f*width,.053f,.062f,.039f},new[]{.088f*depth,.054f,.061f,.041f},trousers,0,leg,4);
                if(!distant)Shoe(leg*.115f*width,shoes);
            }
            for(int arm=-1;arm<=1;arm+=2)
            {
                Vector3 shoulder=new Vector3(arm*.22f*width,ShoulderY,0),elbow=shoulder+Vector3.down*ArmLength,hand=elbow+Vector3.down*ArmLength;
                if(row<2||distant)
                    Tube(new[]{shoulder+Vector3.up*.062f,shoulder+Vector3.up*.006f,shoulder+Vector3.down*.12f,elbow+Vector3.up*.018f},new[]{.020f,.075f*width,.063f,.043f},new[]{.021f,.075f*depth,.061f,.043f},shirt,3,arm,5);
                else
                    Tube(new[]{shoulder+Vector3.up*.023f,elbow},new[]{.072f*width,.044f},new[]{.073f*depth,.044f},shirt,3,arm,4);
                Color forearm=clothes==4?shirt:skin;
                if(row<2||distant)
                    Tube(new[]{elbow+Vector3.down*.015f,elbow+Vector3.down*.11f,hand+Vector3.down*.020f},new[]{.045f,.043f,.029f},new[]{.044f,.042f,.027f},forearm,4,arm,5);
                else
                    Tube(new[]{elbow,hand+Vector3.down*.020f},new[]{.044f,.029f},new[]{.043f,.027f},forearm,4,arm,4);
                // This cap is centred on the actual part4 elbow pivot. It covers
                // every bend direction without an axial sleeve/skin overlap.
                if(!distant&&row<2)Ellipsoid(elbow,new Vector3(.062f,.053f,.060f),forearm,4,arm,5,3);
                Ellipsoid(hand+Vector3.down*.027f,new Vector3(.035f,.063f,.022f),skin,5,arm,row<2?6:4,row<2?4:2);
                if(!distant&&row<2)Ellipsoid(hand+new Vector3(-arm*.033f,-.010f,.010f),new Vector3(.014f,.031f,.014f),skin,5,arm,4,3);
            }
            if(!distant)
            {
                if(row<2){if(clothes==1||clothes==3){JerseyStripe(-.073f,trim);JerseyStripe(.073f,trim);}else if(clothes!=4)ChestBand(trim);}
                if(row<2)Quad(new Vector3(-.105f*width,1.205f,.113f*depth+.004f),new Vector3(-.105f*width,1.236f,.112f*depth+.004f),new Vector3(-.077f*width,1.236f,.115f*depth+.004f),new Vector3(-.077f*width,1.205f,.116f*depth+.004f),yellow,1);
            }
            if(gesture==2)
                for(int j=0;j<(distant?4:8);j++)
                {
                    float strip=distant?.22f:.11f,x0=-.44f+j*strip,x1=x0+strip;
                    Quad(new Vector3(x0,1.83f,.12f),new Vector3(x0,1.96f,.12f),new Vector3(x1,1.96f,.12f),new Vector3(x1,1.83f,.12f),j%2==0?yellow:navy,6);
                }
        }
        void Flag(Vector3 origin,int flagSide,float flagPhase)
        {
            phase=flagPhase;feet=origin;height=1;
            Bar(origin,origin+Vector3.up*3,.035f,new Color(.7f,.75f,.8f),9);
            for(int row=0;row<4;row++)for(int col=0;col<12;col++)
            {
                int start=vertices.Count;
                for(int k=0;k<4;k++)
                {
                    float u=(col+(k==1||k==2?1:0))/12f,v=(row+(k>=2?1:0))/4f;
                    Vertex(origin+new Vector3(flagSide*u*1.9f,3-v*1.15f,.04f*Mathf.Sin(u*7)),Vector3.back,row%2==0?yellow:navy,8,flagSide,u);
                }
                triangles.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
            }
        }
        public static void Build(Transform parent,RunnerSettings settings)
        {
            var material=RunnerSceneBuilder.Upsert(new Material(Shader.Find("FenerRunner/Supporters")){name="Supporters",enableInstancing=true},RunnerSceneBuilder.Generated+"/Materials/Supporters.mat");
            var near=Make(parent,settings,material,false);var far=Make(parent,settings,material,true);
            var lod=parent.gameObject.AddComponent<LODGroup>();
            // The combined stands span a full40m segment. A .5 threshold kept
            // that costly near mesh beyond120m at the Editor's Ultra LOD bias.
            lod.SetLODs(new[]{new LOD(.65f,new Renderer[]{near}),new LOD(.025f,new Renderer[]{far})});lod.RecalculateBounds();
        }
        static MeshRenderer Make(Transform parent,RunnerSettings settings,Material material,bool distant)
        {
            var b=new SupporterBuilder{distant=distant};var random=new System.Random(1907);
            float edge=settings.laneSpacing*1.5f+.7f;
            for(int side=-1;side<=1;side+=2)
            {
                for(int row=0;row<4;row++)for(int n=0;n<34;n++)
                {
                    Vector4 shape=CrowdAnatomy.Sample(side,row,n);
                    b.side=side;b.phase=CrowdAnatomy.Phase(side,row,n);b.height=shape.x;
                    b.width=shape.y;b.depth=shape.z;b.headWidth=shape.w;
                    b.identity=CrowdAnatomy.Identity(side,row,n);
                    b.feet=new Vector3(side*(edge+3.4f+row*1.2f),.55f+row*.72f,n*1.16f+.4f);
                    b.gesture=(n*5+row*3+(side>0?2:0))%6;b.Person(n,row,random);
                }
                for(int n=0;n<4;n++)b.Flag(new Vector3(side*(edge+2.8f),1.5f,n*10+4),-side,n*1.7f);
            }
            string name=distant?"DistantSupporters":"Supporters";
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(b.vertices);mesh.SetNormals(b.normals);
            mesh.SetColors(b.colors);mesh.SetUVs(1,b.motion);mesh.SetUVs(2,b.anchors);mesh.SetUVs(3,b.personas);mesh.SetTriangles(b.triangles,0);mesh.RecalculateBounds();
            var bounds=mesh.bounds;bounds.Expand(new Vector3(1.6f,2.2f,1.5f));mesh.bounds=bounds;
            mesh=RunnerSceneBuilder.Upsert(mesh,RunnerSceneBuilder.Generated+"/Meshes/"+name+".asset");
            var go=new GameObject(distant?"Distant supporters":"Animated supporters and flags",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return renderer;
        }
    }
}
