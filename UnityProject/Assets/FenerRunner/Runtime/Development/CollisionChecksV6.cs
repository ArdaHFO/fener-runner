#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    // Deliberately exercises the shipped Game.Update/Finish path as well as coarse
    // real-controller moves whose two endpoints both lie outside a thin trigger.
    public sealed class CollisionChecksV6 : MonoBehaviour
    {
        const string SessionKey = "FenerRunner.CollisionChecksV6";
        const float TestSpeed = 30f, FrameSeconds = 1f / 15f;
        static readonly FieldInfo SettingsField = typeof(RunnerGame).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic);
        [Serializable] sealed class Scenario
        {
            public string name;
            public float startZ, finalZ, speed, stepSeconds, obstacleLead;
            public int lane;
            public bool fatal, hitDetected, swept, nativeOracleChecked, nativeHit, nativeStartOverlap, nativeEndOverlap;
        }
        [Serializable] sealed class Report
        {
            public bool passed, sourceSettingsUnchanged;
            public string utc, unityVersion;
            public string[] checks, errors;
            public Scenario[] scenarios;
            public float wallSeconds, frameSeconds = FrameSeconds, testSpeed = TestSpeed;
            public string scope = "Actual Unity CharacterController, trigger capsule overlap/casts and Game.Update/Finish. Test-only settings clone sets30m/s. Deterministic15Hz simulation frames retain production1/60s substeps; separate0.2s moves verify continuous detection. This is Editor physics evidence, not physical-phone FPS. No production wallet or record writes; original settings and preferences are restored.";
        }
        RunnerGame game;
        RunnerSettings originalSettings, testSettings;
        string originalSettingsJson;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        readonly List<Scenario> scenarios = new List<Scenario>();
        readonly Report report = new Report();
        bool previousBackground, hadBest, hadScore, prepared;
        float previousBest, previousCaptureDelta, began;
        int previousScore, previousTargetRate;

        [MenuItem("Fener Runner/20 - Verify continuous fatal collisions")]
        public static void StartChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            SessionState.SetBool(SessionKey, true);
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Register()
        { EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed; }
        static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, false);
            new GameObject("Continuous collision checks v0.6").AddComponent<CollisionChecksV6>();
        }
        IEnumerator Start()
        {
            began = Time.realtimeSinceStartup;
            game = FindFirstObjectByType<RunnerGame>();
            hadBest = PlayerPrefs.HasKey("FenerRunner.Best"); hadScore = PlayerPrefs.HasKey("FenerRunner.BestScore");
            previousBest = PlayerPrefs.GetFloat("FenerRunner.Best", 0); previousScore = PlayerPrefs.GetInt("FenerRunner.BestScore", 0);
            previousBackground = Application.runInBackground; previousTargetRate = Application.targetFrameRate;
            previousCaptureDelta = Time.captureDeltaTime;
            Application.runInBackground = true; Application.targetFrameRate = 15; Time.captureDeltaTime = FrameSeconds;
            prepared = true; Application.logMessageReceived += Log;
            var routine = RunScenarios();
            while (true)
            {
                bool more; object wait = null;
                try { more = routine.MoveNext(); if (more) wait = routine.Current; }
                catch (Exception exception) { errors.Add(exception.ToString()); break; }
                if (!more) break;
                yield return wait;
            }
            if (originalSettings != null)
            {
                report.sourceSettingsUnchanged = JsonUtility.ToJson(originalSettings) == originalSettingsJson;
                Check(report.sourceSettingsUnchanged, "The original RunnerSettings asset is unchanged");
            }
            report.passed = errors.Count == 0; report.checks = checks.ToArray(); report.errors = errors.ToArray();
            report.scenarios = scenarios.ToArray(); report.wallSeconds = Time.realtimeSinceStartup - began;
            report.utc = DateTime.UtcNow.ToString("O"); report.unityVersion = Application.unityVersion;
            Directory.CreateDirectory("../Validation");
            File.WriteAllText(ReviewEvidence.File("../Validation/unity_fatal_collisions_v7.json"), JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= Log;
            Debug.Log("Continuous collision checks " + (report.passed ? "PASSED" : "FAILED") + ": " + string.Join("; ", errors));
            EditorApplication.isPlaying = false;
        }

        void ResetCase(bool live, bool powers = false, int lane = 0)
        {
            game.enabled = false;
            game.Powerups.Enabled = powers; game.Tokens.Enabled = false;
            game.Play(); game.Powerups.IsolatePickupsForTest();
            if (lane != 0)
            {
                game.Motor.Speed = 0;
                game.Motor.ChangeLane(lane);
                game.Motor.Tick(game.Settings.laneChangeSeconds);
            }
            game.Motor.Speed = TestSpeed;
            game.enabled = live;
        }

        IEnumerator RunScenarios()
        {
            if (game == null || game.Powerups == null || SettingsField == null)
                throw new InvalidOperationException("A built v0.6 scene with powers and settings is required.");
            game.AutomatedTest = true;
            originalSettings = game.Settings; originalSettingsJson = JsonUtility.ToJson(originalSettings);
            testSettings = Instantiate(originalSettings); testSettings.name = "Collision test settings (runtime clone)";
            testSettings.forwardSpeed = testSettings.maxSpeed = TestSpeed;
            SettingsField.SetValue(game, testSettings); game.Motor.Configure(testSettings);
            game.Obstacles.UseSeedForTest(1907);

            // An endpoint-only test would miss every one of these six crossings.
            foreach (ObstacleKind kind in new[] { ObstacleKind.JumpOver, ObstacleKind.SlideUnder })
                for (int lane = -1; lane <= 1; lane++)
                {
                    ResetCase(false, false, lane);
                    float start = game.Motor.transform.position.z;
                    RunnerObstacle obstacle = game.Obstacles.ArrangeTest(kind, start + 3, lane);
                    game.Obstacles.BeginMove(game.Motor);
                    game.Motor.Tick(.2f);
                    RunnerObstacle hit = game.Obstacles.Check(game.Motor);
                    string name = kind + " lane" + lane + " crosses6m in one controller move";
                    Check(hit == obstacle && game.Obstacles.LastHitWasSweep && game.Motor.transform.position.z > start + 5.5f, name);
                    scenarios.Add(new Scenario { name = name, lane = lane, startZ = start, finalZ = game.Motor.transform.position.z,
                        speed = TestSpeed, stepSeconds = .2f, hitDetected = hit != null, swept = game.Obstacles.LastHitWasSweep });
                }
            foreach (ObstacleKind kind in new[] { ObstacleKind.JumpOver, ObstacleKind.SlideUnder })
                foreach (int side in new[] { -1, 1 })
                foreach (float lead in new[] { 2.5f, 3.2f })
                {
                    ResetCase(false, false, side);
                    float start = game.Motor.transform.position.z;
                    RunnerObstacle obstacle = game.Obstacles.ArrangeTest(kind, start + lead);
                    ReadCapsule(out Vector3 bottom0,out Vector3 top0,out float radius0);
                    game.Obstacles.BeginMove(game.Motor);
                    game.Motor.ChangeLane(-side); game.Motor.Tick(.14f);
                    ReadCapsule(out Vector3 bottom1,out Vector3 top1,out float radius1);
                    RunnerObstacle hit = game.Obstacles.Check(game.Motor);
                    string name = kind + " diagonal entry from lane" + side + " / lead " + lead.ToString("F1",System.Globalization.CultureInfo.InvariantCulture) + "m";
                    bool fixedCapsule=radius0==radius1&&((top1-top0)-(bottom1-bottom0)).sqrMagnitude<1e-8f;
                    Check(fixedCapsule,name + " keeps a translating capsule for the independent native cast");
                    bool native=NativeMeshSweep(obstacle,bottom0,top0,bottom1,top1,radius0,out bool nativeStart,out bool nativeEnd);
                    // The former black contour box filled this 2.5m diagonal's
                    // empty rounded corner. Confirm that fact against native
                    // live-mesh physics, alongside the 3.2m real-contact control.
                    bool expectedContact=kind==ObstacleKind.SlideUnder||lead>3f;
                    Check(!nativeStart&&native==expectedContact,name + (expectedContact ? " independently crosses the visible body" : " independently clears the visible rounded corner"));
                    Check(native ? hit==obstacle : hit==null,name + " matches the independent native mesh capsule cast and overlaps");
                    if(native&&!nativeEnd)Check(game.Obstacles.LastHitWasSweep,name + " detects contact even though the endpoint is clear");
                    scenarios.Add(new Scenario { name = name, lane = side, startZ = start, finalZ = game.Motor.transform.position.z,
                        speed = TestSpeed, stepSeconds = .14f, obstacleLead=lead, hitDetected = hit != null, swept = game.Obstacles.LastHitWasSweep,
                        nativeOracleChecked=true,nativeHit=native,nativeStartOverlap=nativeStart,nativeEndOverlap=nativeEnd });
                }
            ResetCase(false);
            game.Obstacles.ArrangeTest(ObstacleKind.JumpOver, game.Motor.transform.position.z + 3, 1);
            game.Obstacles.BeginMove(game.Motor); game.Motor.Tick(.2f);
            Check(game.Obstacles.Check(game.Motor) == null, "An adjacent clear lane remains safe during a6m sweep");

            ResetCase(false);
            var scored = game.Obstacles.ArrangeTest(ObstacleKind.JumpOver, game.Motor.transform.position.z + 3);
            scored.Scored = true;
            game.Obstacles.BeginMove(game.Motor); game.Motor.Tick(.2f);
            Check(game.Obstacles.Check(game.Motor) == scored, "A scored obstacle remains collidable");

            ResetCase(false);
            var shifted = game.Obstacles.ArrangeTest(ObstacleKind.JumpOver, game.Motor.transform.position.z + 3);
            game.Obstacles.BeginMove(game.Motor);
            game.Course.ShiftOrigin(1000); game.Obstacles.ShiftOrigin(1000); game.Motor.ShiftOrigin(1000);
            game.Motor.Tick(.2f);
            Check(game.Obstacles.Check(game.Motor) == shifted && game.Obstacles.LastHitWasSweep,
                "Origin shifting preserves the sampled collision path");

            // Production Update owns all the moves, power decisions and Finish transitions below.
            foreach (ObstacleKind kind in new[] { ObstacleKind.JumpOver, ObstacleKind.SlideUnder })
                for (int lane = -1; lane <= 1; lane++)
                {
                    ResetCase(true, false, lane);
                    float start = game.Motor.transform.position.z;
                    RunnerObstacle obstacle = game.Obstacles.ArrangeTest(kind, start + 4, lane);
                    yield return WaitForLiveMotion(PassedVisibleBodyZ(obstacle));
                    bool fatal = game.State == RunState.Finished;
                    string name = "Real Game fatal " + kind + " lane" + lane + " at30m/s and15Hz";
                    Check(fatal && game.Obstacles.Check(game.Motor) == obstacle && Mathf.Abs(game.CurrentSpeed - TestSpeed) < .01f, name);
                    scenarios.Add(new Scenario { name = name, lane = lane, startZ = start, finalZ = game.Motor.transform.position.z,
                        speed = game.CurrentSpeed, stepSeconds = FrameSeconds, fatal = fatal, hitDetected = game.LastHit.Length > 0 });
                }
            foreach (ObstacleKind kind in new[] { ObstacleKind.JumpOver, ObstacleKind.SlideUnder })
                foreach (int side in new[] { -1, 1 })
                {
                    ResetCase(true, false, side);
                    var obstacle=game.Obstacles.ArrangeTest(kind, game.Motor.transform.position.z + 3.2f);
                    game.HandleCommand(side < 0 ? RunnerCommand.Right : RunnerCommand.Left);
                    yield return WaitForLiveMotion(PassedVisibleBodyZ(obstacle));
                    Check(game.State == RunState.Finished, "Real Game diagonal collision with " + kind + " from lane" + side + " is fatal");
                }
            foreach (PowerupKind power in new[] { PowerupKind.Dash, PowerupKind.Magnet, PowerupKind.DoubleCoins,
                PowerupKind.SlowMotion, PowerupKind.SuperJump, PowerupKind.CoinRain })
            {
                ResetCase(true, true);
                game.Powerups.Activate(power);
                var obstacle=game.Obstacles.ArrangeTest(ObstacleKind.JumpOver, game.Motor.transform.position.z + 4);
                yield return WaitForLiveMotion(PassedVisibleBodyZ(obstacle));
                Check(game.State == RunState.Finished && game.Powerups.IsActive(power) && !game.Powerups.IsProtected,
                    power + " does not grant accidental obstacle immunity");
            }

            ResetCase(true, true);
            game.Powerups.Activate(PowerupKind.Flight);
            var ascentObstacle=game.Obstacles.ArrangeTest(ObstacleKind.JumpOver, game.Motor.transform.position.z + 1.2f);
            yield return WaitForLiveMotion(PassedVisibleBodyZ(ascentObstacle));
            Check(game.State == RunState.Finished && game.Powerups.FlightActive && !game.Powerups.IsProtected,
                "Late flight activation cannot pass through an obstacle before physically gaining height");

            ResetCase(true, true);
            game.Powerups.Activate(PowerupKind.Shield);
            float shieldZ = game.Motor.transform.position.z + 4;
            RunnerObstacle firstShieldHit = game.Obstacles.ArrangeTest(ObstacleKind.JumpOver, shieldZ);
            yield return WaitForLiveMotion(shieldZ + 2.3f);
            Check(game.State == RunState.Running && !game.Powerups.ShieldActive && game.Powerups.HitGraceRemaining > 0 &&
                game.Powerups.TryAbsorbHit(firstShieldHit), "One shield absorbs the first obstacle and its remaining overlap");
            RunnerObstacle secondShieldHit = game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder, game.Motor.transform.position.z + 1.2f);
            Check(secondShieldHit != firstShieldHit && !game.Powerups.TryAbsorbHit(secondShieldHit),
                "Shield overlap grace rejects a different obstacle immediately");
            yield return WaitForLiveMotion(PassedVisibleBodyZ(secondShieldHit));
            Check(game.State == RunState.Finished && game.Powerups.HitGraceRemaining > 0,
                "A second real obstacle is fatal while same-obstacle shield grace is still active");

            foreach (RunnerCommand command in new[] { RunnerCommand.Jump, RunnerCommand.DebugFlip, RunnerCommand.Slide })
            {
                ResetCase(true);
                bool sliding = command == RunnerCommand.Slide;
                float obstacleZ = game.Motor.transform.position.z + TestSpeed * (sliding ? .42f : game.Settings.JumpRiseSeconds);
                game.Obstacles.ArrangeTest(sliding ? ObstacleKind.SlideUnder : ObstacleKind.JumpOver, obstacleZ);
                game.HandleCommand(command);
                yield return WaitForLiveMotion(obstacleZ + 18f,32);
                Check(game.State == RunState.Running && game.Motor.transform.position.z > obstacleZ + 15f && game.Motor.Grounded &&
                    !game.Motor.Sliding && !game.Powerups.IsProtected, "Correct " + command + " clears the real collider without protection");
            }
            foreach (bool useJump in new[] { false, true })
            {
                ResetCase(true);
                var obstacle=game.Obstacles.ArrangeTest(useJump ? ObstacleKind.SlideUnder : ObstacleKind.JumpOver,
                    game.Motor.transform.position.z + TestSpeed * game.Settings.JumpRiseSeconds);
                game.HandleCommand(useJump ? RunnerCommand.Jump : RunnerCommand.Slide);
                yield return WaitForLiveMotion(PassedVisibleBodyZ(obstacle));
                Check(game.State == RunState.Finished, useJump ? "Jumping into a slide gate is fatal" : "Sliding into a black obstacle is fatal");
            }
            ResetCase(false);
            Check(!game.Powerups.ShieldActive && !game.Powerups.IsProtected && game.Powerups.HitGraceRemaining == 0 &&
                game.Powerups.LandingGraceRemaining == 0 && !game.Motor.Flying && game.Coins == 0,
                "A fresh no-power run has no flight, shield, landing grace or fever immunity");
        }
        IEnumerator WaitForLiveMotion(float untilZ,int maximumFrames=16)
        {
            // Game.Update remains the sole driver. Editor rendering can consume a
            // second before enough simulation frames occur, so wall time alone
            // must not decide whether the controller had a chance to make contact.
            int firstFrame=Time.frameCount;float deadline=Time.realtimeSinceStartup+12f;
            while(game.State==RunState.Running&&game.Motor.transform.position.z<untilZ&&
                  Time.frameCount-firstFrame<maximumFrames&&Time.realtimeSinceStartup<deadline)
                yield return null;
            if(game.State==RunState.Running&&game.Motor.transform.position.z<untilZ)
                errors.Add("Actual Game.Update wait exhausted after "+(Time.frameCount-firstFrame)+
                    " frames at z="+game.Motor.transform.position.z+" before target z="+untilZ);
        }
        float PassedVisibleBodyZ(RunnerObstacle obstacle)
        {
            ReadCapsule(out Vector3 bottom,out Vector3 top,out float radius);
            float rearOffset=Mathf.Min(bottom.z,top.z)-radius-game.Motor.transform.position.z;
            // A missed fatal hit fails as soon as the entire capsule has passed
            // the actual model, without waiting for an unrelated later obstacle.
            return obstacle.SurfaceBounds.max.z-rearOffset+.02f;
        }
        void ReadCapsule(out Vector3 bottom,out Vector3 top,out float radius)
        {
            var capsule=game.Motor.Capsule;var root=game.Motor.transform;Vector3 scale=root.lossyScale;
            radius=Mathf.Max(.01f,capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z)));
            float height=Mathf.Max(2*radius,capsule.height*Mathf.Abs(scale.y));
            Vector3 centre=root.TransformPoint(capsule.center),offset=root.up*(height*.5f-radius);
            bottom=centre-offset;top=centre+offset;
        }
        static bool NativeMeshSweep(RunnerObstacle obstacle,Vector3 bottom0,Vector3 top0,Vector3 bottom1,Vector3 top1,float radius,out bool startOverlap,out bool endOverlap)
        {
            // Build after the controller move, so this temporary reference cannot
            // alter its path. Source only live final render meshes and transforms,
            // independently of the baked triangle collision asset and old boxes.
            var reference=new GameObject("Independent diagonal live-mesh oracle");reference.layer=31;
            var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            try
            {
                var vertices=new List<Vector3>();var indices=new List<int>();
                foreach(var filter in obstacle.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer=filter.GetComponent<MeshRenderer>();
                    if(renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                    int offset=vertices.Count;Matrix4x4 world=filter.transform.localToWorldMatrix;
                    foreach(var point in filter.sharedMesh.vertices)vertices.Add(world.MultiplyPoint3x4(point));
                    foreach(int index in filter.sharedMesh.triangles)indices.Add(offset+index);
                }
                if(vertices.Count==0)throw new InvalidOperationException("Live final diagonal obstacle geometry is missing.");
                mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
                var collider=reference.AddComponent<MeshCollider>();collider.sharedMesh=mesh;Physics.SyncTransforms();
                startOverlap=ContainsCollider(Physics.OverlapCapsule(bottom0,top0,radius,1<<31,QueryTriggerInteraction.Collide),collider);
                endOverlap=ContainsCollider(Physics.OverlapCapsule(bottom1,top1,radius,1<<31,QueryTriggerInteraction.Collide),collider);
                Vector3 travel=bottom1-bottom0;bool swept=false;
                if(travel.sqrMagnitude>0)
                    foreach(var hit in Physics.CapsuleCastAll(bottom0,top0,radius,travel.normalized,travel.magnitude,1<<31,QueryTriggerInteraction.Collide))
                        if(hit.collider==collider){swept=true;break;}
                return startOverlap||endOverlap||swept;
            }
            finally{DestroyImmediate(reference);DestroyImmediate(mesh);Physics.SyncTransforms();}
        }
        static bool ContainsCollider(Collider[] values,Collider expected)
        {foreach(var value in values)if(value==expected)return true;return false;}
        void Check(bool condition, string label) { if (condition) checks.Add(label); else errors.Add(label); }
        void Log(string message, string trace, LogType type)
        { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && errors.Count < 40) errors.Add(message); }
        void OnDestroy()
        {
            Application.logMessageReceived -= Log;
            Time.timeScale = 1f;
            if (!prepared) return;
            Application.runInBackground = previousBackground; Application.targetFrameRate = previousTargetRate;
            Time.captureDeltaTime = previousCaptureDelta;
            if (game != null && originalSettings != null)
            { SettingsField.SetValue(game, originalSettings); game.Motor.Configure(originalSettings); game.enabled = true; }
            if (testSettings != null) Destroy(testSettings);
            if (hadBest) PlayerPrefs.SetFloat("FenerRunner.Best", previousBest); else PlayerPrefs.DeleteKey("FenerRunner.Best");
            if (hadScore) PlayerPrefs.SetInt("FenerRunner.BestScore", previousScore); else PlayerPrefs.DeleteKey("FenerRunner.BestScore");
            PlayerPrefs.Save();
        }
    }
}
#endif
