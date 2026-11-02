using UnityEngine;

namespace FenerRunner
{
    public enum ObstacleKind { JumpOver, SlideUnder }
    public sealed class RunnerObstacle : MonoBehaviour
    {
        public ObstacleKind kind;
        public ObstacleHitMesh hitMesh;
        public Bounds SurfaceBounds => hitMesh==null?new Bounds(transform.position,Vector3.zero):ObstacleHitMesh.TransformBounds(transform.localToWorldMatrix,hitMesh.bounds);
        public bool Scored { get; set; }
        private BoxCollider[] collisionBoxes;
        public BoxCollider[] CollisionBoxes
        {
            get
            {
                if(collisionBoxes==null)collisionBoxes=GetComponentsInChildren<BoxCollider>(true);
                return collisionBoxes;
            }
        }
    }
}
