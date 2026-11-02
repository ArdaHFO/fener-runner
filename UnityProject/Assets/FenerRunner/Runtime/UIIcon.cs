using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace FenerRunner
{
    public enum IconKind { Star, Left, Right, Jump, Slide, Pause, Sound, Bolt, Ring, Wing, Magnet, Shield, Clock, Coins, Rain, Flame, DoubleJump, Sweep, Rhythm, Trail, Encore, Chick }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIIcon : MaskableGraphic
    {
        public IconKind kind;
        Color meshColor;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();meshColor=color;
            if(kind==IconKind.Chick)
            {
                Disc(vh,new Vector2(0,-.13f),.29f);Disc(vh,new Vector2(0,.12f),.33f);
                Disc(vh,new Vector2(-.31f,-.1f),.13f);Disc(vh,new Vector2(.31f,-.1f),.13f);
                Triangle(vh,new Vector2(-.12f,.36f),new Vector2(-.14f,.5f),new Vector2(.08f,.36f));
                meshColor=new Color(1,.47f,.08f,color.a);
                Triangle(vh,new Vector2(-.1f,.02f),new Vector2(.1f,.02f),new Vector2(0,-.1f));
                Rect(vh,-.22f,-.44f,.16f,.07f);Rect(vh,.06f,-.44f,.16f,.07f);
                meshColor=new Color(.035f,.065f,.12f,color.a);Disc(vh,new Vector2(-.12f,.15f),.065f);Disc(vh,new Vector2(.12f,.15f),.065f);
                meshColor=new Color(1,1,1,color.a);Disc(vh,new Vector2(-.135f,.17f),.022f);Disc(vh,new Vector2(.105f,.17f),.022f);return;
            }
            if(kind==IconKind.Rhythm){for(int j=-2;j<=2;j++){float h=.84f-Mathf.Abs(j)*.23f;Rect(vh,j*.18f-.05f,-h*.5f,.10f,h);}return;}
            if(kind==IconKind.Trail){for(int j=0;j<3;j++)Disc(vh,new Vector2(-.31f+j*.28f,-.28f+j*.21f),.13f);Triangle(vh,new Vector2(.03f,.45f),new Vector2(.46f,.45f),new Vector2(.46f,.02f));return;}
            if(kind==IconKind.Encore){for(int i=0;i<32;i++){float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;Quad(vh,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.46f,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.46f,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.38f,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.38f);}Rect(vh,-.05f,-.25f,.1f,.5f);Rect(vh,-.25f,-.05f,.5f,.1f);return;}
            if(kind==IconKind.DoubleJump){for(int i=0;i<2;i++){float y=i*.4f-.2f;Quad(vh,new Vector2(-.38f,y-.17f),new Vector2(0,y+.17f),new Vector2(0,y+.37f),new Vector2(-.38f,y+.03f));Quad(vh,new Vector2(0,y+.17f),new Vector2(.38f,y-.17f),new Vector2(.38f,y+.03f),new Vector2(0,y+.37f));}return;}
            if(kind==IconKind.Sweep){Rect(vh,-.4f,-.42f,.08f,.84f);Rect(vh,-.04f,-.42f,.08f,.84f);Rect(vh,.32f,-.42f,.08f,.84f);Rect(vh,-.48f,-.06f,.96f,.12f);return;}
            if(kind==IconKind.Flame){Triangle(vh,new Vector2(-.36f,-.3f),new Vector2(0,.5f),new Vector2(.17f,-.32f));Triangle(vh,new Vector2(-.24f,-.35f),new Vector2(.38f,.18f),new Vector2(.36f,-.3f));Disc(vh,new Vector2(0,-.23f),.26f);return;}
            if(kind==IconKind.Shield){Quad(vh,new Vector2(-.38f,.4f),new Vector2(.38f,.4f),new Vector2(.3f,-.1f),new Vector2(-.3f,-.1f));Triangle(vh,new Vector2(-.3f,-.1f),new Vector2(.3f,-.1f),new Vector2(0,-.48f));return;}
            if(kind==IconKind.Clock){for(int i=0;i<32;i++){float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;Quad(vh,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.46f,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.46f,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.37f,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.37f);}Rect(vh,-.04f,-.04f,.08f,.34f);Rect(vh,0,-.04f,.26f,.08f);return;}
            if(kind==IconKind.Coins){Disc(vh,new Vector2(-.16f,.14f),.29f);Disc(vh,new Vector2(.18f,-.16f),.27f);return;}
            if(kind==IconKind.Rain){Disc(vh,new Vector2(-.27f,.27f),.13f);Disc(vh,new Vector2(.25f,.32f),.13f);Disc(vh,new Vector2(.02f,-.09f),.13f);Rect(vh,-.3f,-.3f,.06f,.2f);Rect(vh,.23f,-.34f,.06f,.22f);Rect(vh,-.01f,-.46f,.06f,.18f);return;}
            if(kind==IconKind.Wing){Triangle(vh,new Vector2(-.44f,.36f),new Vector2(-.02f,-.3f),new Vector2(.02f,.14f));Triangle(vh,new Vector2(.44f,.36f),new Vector2(.02f,-.3f),new Vector2(-.02f,.14f));Triangle(vh,new Vector2(-.48f,.06f),new Vector2(-.02f,-.3f),new Vector2(-.16f,.05f));Triangle(vh,new Vector2(.48f,.06f),new Vector2(.02f,-.3f),new Vector2(.16f,.05f));return;}
            if(kind==IconKind.Magnet){Rect(vh,-.38f,-.3f,.2f,.75f);Rect(vh,.18f,-.3f,.2f,.75f);Rect(vh,-.38f,-.4f,.76f,.2f);return;}
            if(kind == IconKind.Ring)
            {
                for(int i=0;i<48;i++)
                {
                    float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48;
                    Quad(vh,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.48f,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.48f,
                        new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.43f,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.43f);
                }
                return;
            }
            if(kind==IconKind.Star)
            {
                for(int i=0;i<10;i++)
                {
                    float a=(90+i*36)*Mathf.Deg2Rad,b=(90+(i+1)*36)*Mathf.Deg2Rad;
                    Triangle(vh,Vector2.zero,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(i%2==0?.48f:.21f),new Vector2(Mathf.Cos(b),Mathf.Sin(b))*(i%2==0?.21f:.48f));
                }
                return;
            }
            if(kind==IconKind.Pause){Rect(vh,-.31f,-.4f,.19f,.8f);Rect(vh,.12f,-.4f,.19f,.8f);return;}
            if(kind==IconKind.Sound){Rect(vh,-.42f,-.19f,.23f,.38f);Quad(vh,new Vector2(-.19f,-.19f),new Vector2(.16f,-.4f),new Vector2(.16f,.4f),new Vector2(-.19f,.19f));Rect(vh,.3f,-.25f,.08f,.5f);return;}
            if(kind==IconKind.Bolt){Triangle(vh,new Vector2(.12f,.49f),new Vector2(-.36f,-.07f),new Vector2(.1f,-.03f));Triangle(vh,new Vector2(-.1f,.03f),new Vector2(.36f,.07f),new Vector2(-.12f,-.49f));return;}
            Vector2[] points={new Vector2(-.35f,-.2f),new Vector2(0,.19f),new Vector2(.35f,-.2f),new Vector2(.35f,.03f),new Vector2(0,.42f),new Vector2(-.35f,.03f)};
            float angle=kind==IconKind.Left?90:kind==IconKind.Right?-90:kind==IconKind.Slide?180:0;
            for(int i=0;i<points.Length;i++)points[i]=Quaternion.Euler(0,0,angle)*points[i];
            Quad(vh,points[0],points[1],points[4],points[5]);Quad(vh,points[1],points[2],points[3],points[4]);
            if(kind==IconKind.Slide)Rect(vh,-.36f,-.42f,.72f,.08f);
        }
        void Disc(VertexHelper vh,Vector2 centre,float radius){for(int i=0;i<24;i++){float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;Triangle(vh,centre,centre+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,centre+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius);}}
        void Rect(VertexHelper vh,float x,float y,float w,float h){Quad(vh,new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h));}
        void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c){int n=vh.currentVertCount;Vert(vh,a);Vert(vh,b);Vert(vh,c);vh.AddTriangle(n,n+1,n+2);}
        void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d){int n=vh.currentVertCount;Vert(vh,a);Vert(vh,b);Vert(vh,c);Vert(vh,d);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
        void Vert(VertexHelper vh,Vector2 p){Rect r=rectTransform.rect;vh.AddVert(new Vector3(r.center.x+p.x*r.width,r.center.y+p.y*r.height),meshColor,Vector2.zero);}
    }
}
