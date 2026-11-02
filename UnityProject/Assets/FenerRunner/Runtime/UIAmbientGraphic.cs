using UnityEngine;
using UnityEngine.UI;
namespace FenerRunner
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIAmbientGraphic:MaskableGraphic
    {
        public bool sheen;
        public float phase;
        float clock;CanvasGroup[] visibility;Button button;
        protected override void OnEnable(){base.OnEnable();raycastTarget=false;clock=phase;SetVerticesDirty();}
        protected override void Start(){base.Start();visibility=GetComponentsInParent<CanvasGroup>(true);if(sheen)button=GetComponentInParent<Button>();}
        void Update(){if(!UIAnimationVisibility.Visible(visibility))return;clock+=Time.unscaledDeltaTime;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Rect r=rectTransform.rect;
            if(sheen)
            {
                if(button!=null&&!button.interactable)return;
                float cycle=Mathf.Repeat(clock,5.4f);if(cycle>1.05f)return;
                float x=Mathf.Lerp(r.xMin-140,r.xMax+140,cycle/1.05f);
                float left=Mathf.Clamp(x-68,r.xMin,r.xMax),right=Mathf.Clamp(x+28,r.xMin,r.xMax);
                float topLeft=Mathf.Clamp(x-28,r.xMin,r.xMax),topRight=Mathf.Clamp(x+68,r.xMin,r.xMax);
                if(right<=left&&topRight<=topLeft)return;Color tint=color;tint.a*=Mathf.Sin(cycle/1.05f*Mathf.PI);
                Quad(vh,new Vector2(left,r.yMin),new Vector2(right,r.yMin),new Vector2(topRight,r.yMax),new Vector2(topLeft,r.yMax),tint);return;
            }
            // Thin club-colour traces and sparse glints stay at the screen edges.
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*(r.width*.5f-27);Color faint=color;faint.a*=.16f;
                Line(vh,new Vector2(x,r.yMin+590),new Vector2(x,r.yMax-460),1,faint);
                for(int i=0;i<9;i++)
                {
                    float u=Mathf.Repeat(i*.117f+clock*(.011f+i*.001f),1);
                    Vector2 p=new Vector2(x-side*(20+(i%3)*13),Mathf.Lerp(r.yMin+560,r.yMax-470,u));
                    Color tint=color;tint.a*=.18f*Mathf.Sin(u*Mathf.PI);float size=i%3==0?3:1.5f;
                    Quad(vh,p+new Vector2(-size,-size),p+new Vector2(size,-size),p+new Vector2(size,size),p+new Vector2(-size,size),tint);
                }
                float sweep=Mathf.Repeat(clock*.045f+phase,1);
                Vector2 head=new Vector2(x,Mathf.Lerp(r.yMin+590,r.yMax-460,sweep));Color bright=color;bright.a*=.52f*Mathf.Sin(sweep*Mathf.PI);
                Line(vh,head-Vector2.up*45,head+Vector2.up*45,2,bright);
            }
        }
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color tint)
        {Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;Quad(vh,a-n,b-n,b+n,a+n,tint);}
        static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
        {int i=vh.currentVertCount;vh.AddVert(a,tint,Vector2.zero);vh.AddVert(b,tint,Vector2.zero);vh.AddVert(c,tint,Vector2.zero);vh.AddVert(d,tint,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);}
    }
    internal static class UIAnimationVisibility
    {
        public static bool Visible(CanvasGroup[] groups)
        {if(groups==null)return true;foreach(CanvasGroup group in groups)if(group!=null&&group.alpha<.002f)return false;return true;}
    }
}
