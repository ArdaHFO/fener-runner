using UnityEngine;
using UnityEngine.UI;
namespace FenerRunner
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIRewardBurst:MaskableGraphic
    {
        const float Duration=.8f;
        float remaining;Vector2 origin;
        public bool IsPlaying=>remaining>0;
        public void Play(Vector2 position,Color tint){origin=position;color=tint;remaining=Duration;SetVerticesDirty();}
        protected override void OnEnable(){base.OnEnable();raycastTarget=false;remaining=0;SetVerticesDirty();}
        void Update(){if(remaining<=0)return;remaining=Mathf.Max(0,remaining-Time.unscaledDeltaTime);SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(remaining<=0)return;
            float t=1-remaining/Duration;
            for(int i=0;i<14;i++)
            {
                float angle=i*2.399963f,travel=(54+(i%4)*22)*(1-Mathf.Pow(1-t,3));
                Vector2 p=origin+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*travel+Vector2.down*(t*t*35);
                float size=(i%3==0?5:3)*(1-t);Color tint=color;tint.a=Mathf.Pow(1-t,1.6f);
                int start=vh.currentVertCount;
                vh.AddVert(p+Vector2.up*size,tint,Vector2.zero);vh.AddVert(p+Vector2.right*size,tint,Vector2.zero);
                vh.AddVert(p+Vector2.down*size,tint,Vector2.zero);vh.AddVert(p+Vector2.left*size,tint,Vector2.zero);
                vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
            }
        }
    }
}
