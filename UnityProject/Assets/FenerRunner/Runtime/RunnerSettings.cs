using UnityEngine;

namespace FenerRunner
{
    [CreateAssetMenu(menuName = "Fener Runner/Runner Settings")]
    public sealed class RunnerSettings : ScriptableObject
    {
        [Header("Movement (metres, seconds)")]
        [Min(1f)] public float forwardSpeed = 18f;
        [Min(1f)] public float maxSpeed = 34f;
        [Min(100f)] public float speedRampDistance = 1800f;
        [Min(0f)] public float feverSpeedBonus = 3f;
        [Min(1.8f)] public float laneSpacing = 3.3f;
        [Range(0.1f, 0.4f)] public float laneChangeSeconds = 0.14f;
        [Min(0.3f)] public float jumpHeight = 2.65f;
        [Range(0.5f, 1.5f)] public float jumpAirSeconds = 0.72f;
        [Range(.5f,.65f)] public float jumpAscentFraction = .56f;
        [Range(0.3f, 1.5f)] public float slideSeconds = 0.85f;
        [Min(1)] public int flipEveryJumps = 3;
        [Header("Animation")]
        [Range(0.1f, 0.18f)] public float blendSeconds = 0.14f;
        [Min(1f)] public float runReferenceSpeed = 18f;
        public Vector2 turnIntervalSeconds = new Vector2(10f, 16f);
        [Header("Camera")]
        public Vector3 cameraOffset = new Vector3(0f, 4.0f, -7.4f);
        public Vector3 cameraLookOffset = new Vector3(0f, 1.35f, 4.4f);
        [Range(0f, 1f)] public float cameraLaneFollow = 0.65f;
        [Min(0.01f)] public float cameraSmoothSeconds = 0.15f;
        [Header("Touch")]
        [Range(0.03f, 0.2f)] public float swipeScreenFraction = 0.065f;
        [Header("Course")]
        [Min(30f)] public float chunkLength = 40f;
        [Range(8, 14)] public int chunkCount = 9;
        [Min(512f)] public float originShiftDistance = 1024f;

        public float JumpRiseSeconds => jumpAirSeconds * jumpAscentFraction;
        public float Gravity => -2f * jumpHeight / (JumpRiseSeconds * JumpRiseSeconds);
        public float FallGravity => -2f * jumpHeight / Mathf.Pow(jumpAirSeconds * (1f-jumpAscentFraction),2);
        public float JumpVelocity => 2f * jumpHeight / JumpRiseSeconds;

        private void OnValidate()
        {
            turnIntervalSeconds.x = Mathf.Max(1f, turnIntervalSeconds.x);
            turnIntervalSeconds.y = Mathf.Max(turnIntervalSeconds.x, turnIntervalSeconds.y);
        }
    }
}
