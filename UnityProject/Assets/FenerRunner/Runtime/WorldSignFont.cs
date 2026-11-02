using UnityEngine;
namespace FenerRunner
{
    // Keep the shared depth-tested sign material attached to Unity's dynamic font atlas.
    // Event-driven, with no Update work and no extra material instances per stadium chunk.
    public sealed class WorldSignFont:MonoBehaviour
    {
        Font font;Material material;
        void OnEnable()
        {
            var label=GetComponentInChildren<TextMesh>();if(label==null)return;
            font=label.font;material=label.GetComponent<MeshRenderer>().sharedMaterial;
            Refresh(font);Font.textureRebuilt+=Refresh;
        }
        void Refresh(Font changed){if(changed==font&&font!=null&&material!=null)material.mainTexture=font.material.mainTexture;}
        void OnDisable(){Font.textureRebuilt-=Refresh;}
    }
}
