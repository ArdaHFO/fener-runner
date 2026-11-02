using UnityEngine;
using UnityEngine.UI;
namespace FenerRunner
{
    public sealed class UIShopCard:MonoBehaviour
    {
        [SerializeField] Text durationLabel;
        [SerializeField] Image[] levelPips;
        [SerializeField] RectTransform medallion;
        [SerializeField] Image glow;
        [SerializeField] UIButtonMotion purchaseMotion;
        [SerializeField] UIRewardBurst reward;
        [SerializeField] Color accent;
        float clock,feedback;bool successful;CanvasGroup[] visibility;
        public int ShownLevel{get;private set;}
        public float ShownDuration{get;private set;}
        public bool Affordable{get;private set;}
        public bool FeedbackPlaying=>feedback>0||(reward!=null&&reward.IsPlaying);
        public Text DurationLabel=>durationLabel;
        public Image[] LevelPips=>levelPips;
        public void Configure(Text duration,Image[] pips,RectTransform icon,Image flash,UIButtonMotion button,UIRewardBurst burst,Color tint)
        {durationLabel=duration;levelPips=pips;medallion=icon;glow=flash;purchaseMotion=button;reward=burst;accent=tint;}
        public void SetState(int level,float currentSeconds,bool canPurchase,float step=1f)
        {
            ShownLevel=level;ShownDuration=currentSeconds;Affordable=canPurchase;
            string current=currentSeconds.ToString("0.#")+" <size=22>SN</size>";
            durationLabel.text=level>=3?current+"  <size=23>· MAKSİMUM</size>":current+"  <color=#A8B8CA>→</color>  <color=#"+ColorUtility.ToHtmlStringRGB(accent)+">"+(currentSeconds+step).ToString("0.#")+" <size=22>SN</size></color>";
            for(int i=0;i<levelPips.Length;i++)levelPips[i].color=i<level?accent:new Color(.32f,.42f,.53f,.45f);
        }
        public void PurchaseFeedback(bool success)
        {
            successful=success;feedback=success?.8f:.32f;
            if(success){purchaseMotion.Confirm();reward.Play(new Vector2(285,38),accent);}
            else purchaseMotion.Reject();
        }
        void OnEnable(){clock=0;feedback=0;}
        void Start(){visibility=GetComponentsInParent<CanvasGroup>(true);}
        void Update()
        {
            if(!UIAnimationVisibility.Visible(visibility))return;
            float dt=Time.unscaledDeltaTime;feedback=Mathf.Max(0,feedback-dt);
            // Only the few cards inside the mobile scroll viewport animate.
            if(durationLabel.canvasRenderer.cull)return;
            clock+=dt;
            float sparkle=feedback>0?Mathf.Sin(Mathf.Clamp01(feedback/.8f)*Mathf.PI):0;
            medallion.localRotation=Quaternion.Euler(0,0,Mathf.Sin(clock*.8f)*3f);
            medallion.localScale=Vector3.one*(1+.018f*Mathf.Sin(clock*1.4f)+sparkle*.075f);
            Color tint=successful?accent:new Color(1,.45f,.3f);tint.a=feedback>0?sparkle*.18f:0;glow.color=tint;
        }
        void OnDisable(){if(medallion!=null){medallion.localScale=Vector3.one;medallion.localRotation=Quaternion.identity;}}
    }
}
