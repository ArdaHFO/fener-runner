using UnityEngine;

namespace FenerRunner
{
    /// <summary>A pooled, purely visual pursuer. It never participates in the runner's physics or scoring.</summary>
    public sealed class ChaseSUV : MonoBehaviour
    {
        public const string ResourceName = "ChaseSUV";
        public const float VehicleHalfLength = 2.46f;
        public const float TireRadius = .495f;
        private RunnerGame game;
        private Transform visual, body, leftSteer, rightSteer;
        private readonly Transform[] vehicles=new Transform[RunWallet.VehicleCount];
        public ChaseVehicleKind SelectedVehicle{get;private set;}
        public int RetainedVehicleCount{get;private set;}
        private readonly Transform[] wheels = new Transform[4];
        private float clock, nextEntrance, entranceTime, episodeLength, lateral, lateralVelocity;
        private float wheelAngle, roll, pitch, previousGap, previousMotorZ, lastSpeed;
        private int episode;
        private bool activeEpisode, initialized, runningLastTick;

        public Transform VisualRoot => visual;
        public bool IsVisible => visual != null && visual.gameObject.activeSelf;
        public float Presence01 { get; private set; }
        public float ApproachGap { get; private set; }
        public int EpisodeCount => episode;
        public float PresentationTime => clock;

        public void Initialize(RunnerGame owner)
        {
            if (initialized && game == owner) return;
            game = owner;
            if (RetainedVehicleCount == 0)
            {
                for(int i=0;i<vehicles.Length;i++)
                {
                    var kind=(ChaseVehicleKind)i;
                    GameObject prefab=Resources.Load<GameObject>(RunnerShopCatalog.VehicleResource(kind));
                    if(prefab==null){Debug.LogWarning("Chase vehicle is missing: "+kind,this);continue;}
                    vehicles[i]=Instantiate(prefab).transform;vehicles[i].name=RunnerShopCatalog.VehicleName(kind)+" (visual chase)";
                    vehicles[i].gameObject.SetActive(false);RetainedVehicleCount++;
                }
            }
            ApplySelection(owner.SelectedVehicle);
            initialized = game != null;
            ResetPresentation();
        }

        public bool ApplySelection(ChaseVehicleKind kind)
        {
            int index=(int)kind;
            if(index<0||index>=vehicles.Length||vehicles[index]==null)return false;
            // Retain all three baked models. Equipping never allocates a new object.
            for(int i=0;i<vehicles.Length;i++)if(vehicles[i]!=null)vehicles[i].gameObject.SetActive(false);
            SelectedVehicle=kind;visual=vehicles[index];
            body=visual.Find("Suspension");leftSteer=visual.Find("WheelFLSteer");rightSteer=visual.Find("WheelFRSteer");
            wheels[0]=leftSteer!=null?leftSteer.Find("WheelFLSpin"):null;
            wheels[1]=rightSteer!=null?rightSteer.Find("WheelFRSpin"):null;
            wheels[2]=visual.Find("WheelRLSpin");wheels[3]=visual.Find("WheelRRSpin");
            ResetPresentation();return true;
        }

        public void ResetPresentation()
        {
            clock = 0f;
            nextEntrance = 4.5f;
            entranceTime = 0f;
            episodeLength = 5.7f;
            episode = 0;
            activeEpisode = runningLastTick = false;
            lateralVelocity = wheelAngle = roll = pitch = lastSpeed = 0f;
            previousGap = ApproachGap = 22f;
            Presence01 = 0f;
            lateral = game != null && game.Motor != null ? game.Motor.transform.position.x : 0f;
            previousMotorZ = game != null && game.Motor != null ? game.Motor.transform.position.z : 0f;
            if (visual != null)
            {
                visual.gameObject.SetActive(false);
                visual.rotation = Quaternion.identity;
            }
            if (body != null) { body.localPosition = Vector3.zero; body.localRotation = Quaternion.identity; }
            if (leftSteer != null) leftSteer.localRotation = Quaternion.identity;
            if (rightSteer != null) rightSteer.localRotation = Quaternion.identity;
            for (int i = 0; i < wheels.Length; i++) if (wheels[i] != null) wheels[i].localRotation = Quaternion.identity;
        }

        /// <summary>Call once per game frame, also in paused/menu/finished states. Paused state does not consume chase time.</summary>
        public void Tick(float dt)
        {
            if (!isActiveAndEnabled || !initialized || game == null || visual == null || game.Motor == null) return;
            if (game.State == RunState.Paused) return;
            if (game.State != RunState.Running)
            {
                visual.gameObject.SetActive(false);
                Presence01 = 0f;
                runningLastTick = false;
                return;
            }
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f) return;
            Vector3 runner = game.Motor.transform.position;
            // Restarting and an origin rebase are deliberately separate. A rebase must not restart the pursuit.
            if (!runningLastTick)
            {
                lateral = runner.x;
                lateralVelocity = 0f;
                previousMotorZ = runner.z;
            }
            runningLastTick = true;
            if (Mathf.Abs(runner.z - previousMotorZ) > 100f) lateralVelocity = 0f;
            previousMotorZ = runner.z;
            clock += dt;
            if (!activeEpisode && clock >= nextEntrance)
            {
                activeEpisode = true;
                episode++;
                entranceTime = clock;
                episodeLength = 5.6f + Hash01(episode * 7) * .9f;
                previousGap = 22f;
                visual.gameObject.SetActive(true);
            }
            if (!activeEpisode) return;
            float age = clock - entranceTime;
            if (age >= episodeLength)
            {
                activeEpisode = false;
                Presence01 = 0f;
                visual.gameObject.SetActive(false);
                nextEntrance = clock + 10f + Hash01(episode * 17) * 7f;
                return;
            }
            if (!visual.gameObject.activeSelf) visual.gameObject.SetActive(true);

            // Ease in, breathe alongside the chase camera, then fall back. No camera shake or sightline blockage.
            float entering = Mathf.SmoothStep(0f, 1f, age / 1.85f);
            float leaving = 1f - Mathf.SmoothStep(0f, 1f, (age - episodeLength + 2.35f) / 2.35f);
            Presence01 = entering * leaving;
            float spacing = game.Settings != null ? game.Settings.laneSpacing : 3.3f;
            float side = Mathf.Abs(runner.x) > spacing * .45f ? -Mathf.Sign(runner.x) : ((episode & 1) == 0 ? 1f : -1f);
            float targetX = Mathf.Clamp(runner.x + side * 1.1f, -spacing, spacing);
            float oldX = lateral;
            lateral = Mathf.SmoothDamp(lateral, targetX, ref lateralVelocity, .37f, 12f, dt);
            float xSpeed = (lateral - oldX) / dt;
            // The nearest point remains >1.2 m behind the local player, including yaw and the bumper overhang.
            float nearGap = 5.6f + .12f * Mathf.Sin(age * 2.2f);
            ApproachGap = Mathf.Lerp(22f, nearGap, Presence01);
            float closingSpeed = (previousGap - ApproachGap) / dt;
            float vehicleSpeed = Mathf.Clamp(game.CurrentSpeed + closingSpeed, 0f, 55f);
            previousGap = ApproachGap;
            float yaw = Mathf.Clamp(Mathf.Atan2(xSpeed, Mathf.Max(10f, vehicleSpeed)) * Mathf.Rad2Deg, -8f, 8f);
            visual.SetPositionAndRotation(new Vector3(lateral, .025f, runner.z - ApproachGap), Quaternion.Euler(0f, yaw, 0f));
            float response = 1f - Mathf.Exp(-dt * 7f);
            roll = Mathf.Lerp(roll, Mathf.Clamp(-xSpeed * .65f, -2.2f, 2.2f), response);
            float acceleration = Mathf.Clamp((vehicleSpeed - lastSpeed) / dt, -12f, 12f);
            pitch = Mathf.Lerp(pitch, -acceleration * .115f, response);
            lastSpeed = vehicleSpeed;
            if (body != null)
            {
                float travel = clock * (7.5f + vehicleSpeed * .045f);
                body.localPosition = Vector3.up * (.009f * Mathf.Sin(travel) + .004f * Mathf.Sin(travel * 1.73f));
                body.localRotation = Quaternion.Euler(pitch + .16f * Mathf.Sin(travel * .64f), 0f, roll);
            }
            float steer = Mathf.Clamp(yaw * 1.5f, -14f, 14f);
            if (leftSteer != null) leftSteer.localRotation = Quaternion.Euler(0f, steer, 0f);
            if (rightSteer != null) rightSteer.localRotation = Quaternion.Euler(0f, steer, 0f);
            wheelAngle = Mathf.Repeat(wheelAngle + vehicleSpeed / TireRadius * Mathf.Rad2Deg * dt, 360f);
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] != null) wheels[i].localRotation = Quaternion.Euler(wheelAngle, 0f, 0f);
        }

        private static float Hash01(int seed)
        {
            // A private deterministic sequence leaves obstacle, token and voice randomness untouched.
            uint x = (uint)seed * 747796405u + 2891336453u;
            x = ((x >> (int)((x >> 28) + 4)) ^ x) * 277803737u;
            return ((x >> 22) ^ x) / (float)uint.MaxValue;
        }

        private void OnDisable() { if (visual != null) visual.gameObject.SetActive(false); Presence01 = 0f; }
        private void OnDestroy() { foreach(var vehicle in vehicles)if(vehicle!=null)Destroy(vehicle.gameObject); }
    }
}
