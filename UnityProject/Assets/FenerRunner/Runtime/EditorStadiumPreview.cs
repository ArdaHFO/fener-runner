using UnityEngine;

namespace FenerRunner
{
    [DefaultExecutionOrder(-1000)]
    public sealed class EditorStadiumPreview : MonoBehaviour
    {
        private void Awake()
        {
            // Disable immediately: deferred destruction must never leave duplicate ground colliders active.
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
