using UnityEngine;

namespace FenerRunner
{
    public sealed class EndlessCourse : MonoBehaviour
    {
        [SerializeField] private RunnerSettings settings;
        [SerializeField] private GameObject chunkPrefab;
        private Transform[] chunks;
        public int ActiveChunkCount => chunks == null ? 0 : chunks.Length;
        public void Configure(RunnerSettings value, GameObject segment) { settings = value; chunkPrefab = segment; }
        public void ResetCourse()
        {
            if (chunks == null)
            {
                chunks = new Transform[settings.chunkCount];
                for (int i = 0; i < chunks.Length; i++)
                {
                    chunks[i] = Instantiate(chunkPrefab, transform).transform;
                    chunks[i].name = "Stadium segment " + i;
                }
            }
            for (int i = 0; i < chunks.Length; i++)
                chunks[i].position = Vector3.forward * ((i - 2) * settings.chunkLength);
            Physics.SyncTransforms();
        }
        public void Tick(float playerZ)
        {
            float furthest = float.NegativeInfinity;
            foreach (Transform chunk in chunks) furthest = Mathf.Max(furthest, chunk.position.z);
            foreach (Transform chunk in chunks)
            {
                if (chunk.position.z + settings.chunkLength >= playerZ - settings.chunkLength) continue;
                furthest += settings.chunkLength;
                chunk.position = Vector3.forward * furthest;
            }
        }
        public void ShiftOrigin(float amount)
        {
            foreach (Transform chunk in chunks) chunk.position -= Vector3.forward * amount;
            Physics.SyncTransforms();
        }
        public bool Covers(float z)
        {
            foreach (Transform chunk in chunks)
                if (z >= chunk.position.z && z <= chunk.position.z + settings.chunkLength) return true;
            return false;
        }
    }
}
