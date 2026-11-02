using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace FenerRunner
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UIEntrance : MonoBehaviour
    {
        public float delay;
        public float duration=.58f;
        public Vector2 offset=new Vector2(0,-32);
        public float initialScale=.975f;
        CanvasGroup group;RectTransform rect;Vector2 restingPosition;Vector3 restingScale;
        float clock;bool initialized;
        public bool IsAnimating=>clock<delay+duration;
        public float Progress=>Mathf.Clamp01((clock-delay)/Mathf.Max(.1f,duration));
        void OnEnable()
        {
            if(!initialized)
            {
                group=GetComponent<CanvasGroup>();rect=(RectTransform)transform;
                restingPosition=rect.anchoredPosition;restingScale=rect.localScale;initialized=true;
            }
            clock=0;Apply(0);StartCoroutine(Animate());
        }
        IEnumerator Animate()
        {
            while(IsAnimating){yield return null;clock+=Time.unscaledDeltaTime;Apply(Progress);}
        }
        void Apply(float progress)
        {
            float eased=1-Mathf.Pow(1-progress,4);
            group.alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(progress*1.8f));
            group.blocksRaycasts=group.interactable=progress>.4f;
            rect.anchoredPosition=restingPosition+offset*(1-eased);
            rect.localScale=restingScale*Mathf.Lerp(initialScale,1,eased);
        }
        void OnDisable()
        {StopAllCoroutines();if(!initialized)return;rect.anchoredPosition=restingPosition;rect.localScale=restingScale;group.alpha=1;group.blocksRaycasts=group.interactable=true;}
    }


}
