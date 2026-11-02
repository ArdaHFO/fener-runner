using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace FenerRunner
{
    public sealed class UIGradient : BaseMeshEffect
    {
        public Color top = Color.white, bottom = Color.white;
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            Rect rect = graphic.rectTransform.rect;
            UIVertex vertex = new UIVertex();
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                vertex.color *= Color.Lerp(bottom, top, Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y));
                mesh.SetUIVertex(vertex, i);
            }
        }
    }


}
