using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace FenerRunner
{
    public sealed class UIButtonMotion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool pulse;
        bool pressed;Button button;float scale=1,velocity,clock,confirm;CanvasGroup[] visibility;
        void Start(){visibility=GetComponentsInParent<CanvasGroup>(true);}
        public float CurrentScale=>scale;
        public bool IsAnimating=>pressed||Mathf.Abs(velocity)>.002f||Mathf.Abs(scale-1)>.002f;
        void OnEnable() { button=GetComponent<Button>();pressed=false;scale=1;velocity=clock=confirm=0;transform.localScale=Vector3.one; }
        public void OnPointerDown(PointerEventData data) { if(button==null||button.interactable)pressed=true; }
        public void OnPointerUp(PointerEventData data) { pressed = false; }
        public void OnPointerExit(PointerEventData data) { pressed = false; }
        void Update()
        {
            if(!UIAnimationVisibility.Visible(visibility))return;
            float dt=Mathf.Min(.033f,Time.unscaledDeltaTime);clock+=dt;confirm=Mathf.Max(0,confirm-dt);
            if(button!=null&&!button.interactable)pressed=false;
            float breath=pulse&&(button==null||button.interactable)?Mathf.Sin(clock*1.7f)*.006f:0;
            float target=pressed?.955f:1+breath+(confirm>0?.025f*Mathf.Sin(confirm/.32f*Mathf.PI):0);
            velocity+=(target-scale)*240f*dt;velocity*=Mathf.Exp(-dt*21);
            scale=Mathf.Clamp(scale+velocity*dt,.93f,1.045f);transform.localScale=Vector3.one*scale;
        }
        public void Confirm(){pressed=false;confirm=.32f;velocity=.45f;}
        public void Reject(){pressed=false;velocity=-.5f;}
        void OnDisable(){pressed=false;transform.localScale=Vector3.one;}
    }


}
