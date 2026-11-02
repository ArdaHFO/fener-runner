#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FenerRunner
{
    // Runs in real Play Mode, through the real CharacterController and Animator.
    // It is excluded from player builds and is never attached to the shipped scene.
    public sealed class PrototypeSmokeCheck : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public string unityVersion, utc;
            public bool passed;
            public float continuousRunSeconds;
            public int initialObjects, finalObjects, segmentCount;
            public string[] checks, errors;
            public string scope = "Real Editor Play Mode checks. No physical device FPS or real touchscreen validation. Screenshots require human visual review.";
        }
        private readonly List<string> checks = new List<string>();
        private readonly List<string> errors = new List<string>();
        private RunnerGame game;
        private int initialCount, finalCount;
        private float elapsed;
        private bool finished;
        private IEnumerator Start()
        {
            Application.logMessageReceived += OnLog;
            game = FindFirstObjectByType<RunnerGame>();
            if (game == null) { Check(false, "Game found"); Finish(); yield break; }
            game.AutomatedTest=true;game.Powerups.Enabled=false;Application.runInBackground=true;game.Obstacles.SetActive(false);game.Tokens.Enabled=false;
            yield return null;
            Check(game.State == RunState.Menu && game.Animation.CurrentClip == "Idle", "Menu and original character Idle");
            Directory.CreateDirectory("../Validation");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("../Validation/menu.png"));
            yield return null;
            Click("OYNA");
            yield return new WaitForSecondsRealtime(.25f);
            Check(game.State == RunState.Running && game.Distance > 0, "Play UI starts actual travel");
            for (int i = 0; i < 5; i++) game.HandleCommand(RunnerCommand.Left);
            yield return new WaitForSecondsRealtime(.3f);
            Check(game.Motor.Lane == -1 && Mathf.Abs(game.Motor.transform.position.x + game.Settings.laneSpacing) < .03f, "Left lane and clamp");
            for (int i = 0; i < 5; i++) game.HandleCommand(RunnerCommand.Right);
            yield return new WaitForSecondsRealtime(.3f);
            Check(game.Motor.Lane == 1 && Mathf.Abs(game.Motor.transform.position.x - game.Settings.laneSpacing) < .03f, "Right lane and clamp");
            game.HandleCommand(RunnerCommand.Jump);
            float normalVelocity = game.Motor.VerticalVelocity;
            game.HandleCommand(RunnerCommand.Jump);
            Check(game.Motor.JumpCount == 2 && game.Motor.AirStepUsed && game.Motor.VerticalVelocity > normalVelocity,
                "An ordinary second upward command spends one permanent air jump");
            float airJumpVelocity = game.Motor.VerticalVelocity;
            game.HandleCommand(RunnerCommand.Jump);
            Check(game.Motor.JumpCount == 2 && game.Motor.VerticalVelocity == airJumpVelocity,
                "A third airborne command cannot add another jump impulse");
            game.HandleCommand(RunnerCommand.Slide);
            game.HandleCommand(RunnerCommand.Left);
            Check(game.Motor.JumpCount == 2 && !game.Motor.Grounded && !game.Motor.Sliding && game.Motor.FastFalling && game.Motor.Lane == 0,
                "Airborne down cancels the double jump into fast-fall while aerial lane input remains available");
            yield return new WaitForSecondsRealtime(game.Settings.jumpAirSeconds + .2f);
            Check(game.Motor.Grounded, "Normal jump lands on course");
            game.HandleCommand(RunnerCommand.Slide);
            game.HandleCommand(RunnerCommand.Left);
            Check(game.Motor.Sliding && game.Motor.Lane == -1, "Slide and lateral input");
            game.HandleCommand(RunnerCommand.Jump);
            Check(!game.Motor.Sliding && !game.Motor.Grounded, "Jump cancels grounded slide");
            Check(game.Motor.JumpCount == 3 && game.Animation.CurrentClip == "JumpFlip" &&
                Mathf.Abs(normalVelocity - game.Motor.VerticalVelocity) < .001f, "The third accepted jump flips on ground launch with identical physics");
            yield return new WaitForSecondsRealtime(game.Settings.jumpAirSeconds + .2f);
            game.HandleCommand(RunnerCommand.Jump);
            Check(game.Motor.JumpCount == 4 && game.Animation.CurrentClip == "Jump" &&
                Mathf.Abs(normalVelocity - game.Motor.VerticalVelocity) < .001f, "The following ground jump returns to the ordinary clip with identical launch physics");
            yield return new WaitForSecondsRealtime(game.Settings.jumpAirSeconds + .2f);
            game.HandleCommand(RunnerCommand.Slide);
            yield return new WaitForSecondsRealtime(game.Settings.slideSeconds + .2f);
            Check(!game.Motor.Sliding && game.Motor.Grounded, "Slide ends and capsule restores");
            var cameraRig = FindFirstObjectByType<FollowCamera>();
            Quaternion cameraBeforeTurn = cameraRig.transform.rotation;
            game.HandleCommand(RunnerCommand.DebugTurn);
            yield return new WaitForSecondsRealtime(.35f);
            Check(game.Animation.Turning && Quaternion.Angle(game.Motor.transform.rotation, Quaternion.identity) < .01f, "Turn remains inside visual skeleton");
            Check(Quaternion.Angle(cameraBeforeTurn, cameraRig.transform.rotation) < .5f, "Camera direction stays stable during turn");
            yield return new WaitForSecondsRealtime(.15f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("../Validation/turn.png"));
            yield return null;
            game.HandleCommand(RunnerCommand.Right);
            Check(!game.Animation.Turning && game.Motor.Lane == 0, "Player command interrupts turn immediately");
            // Let the preceding lane-follow smoothing settle before isolating the flip camera check.
            yield return new WaitForSecondsRealtime(.75f);
            Quaternion cameraBeforeFlip = cameraRig.transform.rotation;
            game.HandleCommand(RunnerCommand.DebugFlip);
            yield return new WaitForSecondsRealtime(.15f);
            Check(Quaternion.Angle(cameraBeforeFlip, cameraRig.transform.rotation) < .5f &&
                Quaternion.Angle(game.Motor.transform.rotation, Quaternion.identity) < .01f, "Flip preserves camera and physics direction");
            Click("II");
            Vector3 pausedPosition = game.Motor.transform.position;
            float pausedVelocity = game.Motor.VerticalVelocity;
            double pausedDistance = game.Distance;
            float turnClock = game.Animation.TurnCountdown;
            float poseTime = game.Animation.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            yield return new WaitForSecondsRealtime(.3f);
            Check(game.State == RunState.Paused && game.Motor.transform.position == pausedPosition &&
                Math.Abs(game.Distance - pausedDistance) < .001 && game.Motor.VerticalVelocity == pausedVelocity &&
                game.Animation.TurnCountdown == turnClock &&
                Mathf.Abs(game.Animation.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime - poseTime) < .001f,
                "Pause freezes motion, pose, distance and timers");
            Click("DEVAM ET");
            yield return new WaitForSecondsRealtime(game.Settings.jumpAirSeconds + .2f);
            Check(game.State == RunState.Running && game.Distance > pausedDistance, "Resume UI");
            Click("II"); Click("YENİDEN BAŞLAT");
            Check(game.State == RunState.Running && game.Distance == 0 && game.Motor.Lane == 0 &&
                game.Motor.JumpCount == 0 && game.Motor.Grounded && !game.Motor.Sliding && !game.Animation.Turning &&
                game.Animation.CurrentClip == "Run" && game.Course.Covers(game.Motor.transform.position.z), "Restart resets all run state and pool");
            yield return null;
            initialCount = ObjectCount();
            float start = Time.realtimeSinceStartup;
            float requiredSeconds = Mathf.Max(60f, game.Settings.originShiftDistance / game.Settings.forwardSpeed + 5f);
            float previousZ = game.Motor.transform.position.z;
            double previousDistance = game.Distance;
            bool covered = true, bounded = true, running = true;
            bool originShifted = false, distanceMonotonic = true;
            while (Time.realtimeSinceStartup - start < requiredSeconds)
            {
                covered &= game.Course.Covers(game.Motor.transform.position.z) && game.Course.Covers(game.Motor.transform.position.z + 120f);
                bounded &= game.Course.ActiveChunkCount == game.Settings.chunkCount && ObjectCount() == initialCount;
                running &= game.State == RunState.Running;
                originShifted |= game.Motor.transform.position.z < previousZ - game.Settings.chunkLength;
                distanceMonotonic &= game.Distance >= previousDistance;
                previousZ = game.Motor.transform.position.z;
                previousDistance = game.Distance;
                yield return new WaitForSecondsRealtime(.25f);
            }
            elapsed = Time.realtimeSinceStartup - start;
            finalCount = ObjectCount();
            Check(covered && game.Motor.Grounded && game.Distance > game.Settings.forwardSpeed * 58f, "At least 60 seconds of continuous supported travel");
            Check(bounded && finalCount == initialCount, "Segment and scene object counts remain constant");
            Check(running, "Run stayed active during long check");
            Check(originShifted && distanceMonotonic, "Floating origin shifts without resetting total distance");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("../Validation/running.png"));
            yield return null;
            Finish();
        }
        private void Click(string name)
        {
            Button button = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).SingleOrDefault(b => b.name == name);
            if (button == null || !button.gameObject.activeInHierarchy) { Check(false, "UI button available: " + name); return; }
            button.onClick.Invoke();
        }
        private static int ObjectCount() => FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        private void Check(bool ok, string label) { if (ok) checks.Add(label); else errors.Add(label); }
        private void OnLog(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        private void Finish()
        {
            finished = true;
            Application.logMessageReceived -= OnLog;
            var report = new Report { unityVersion = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"), passed = errors.Count == 0,
                continuousRunSeconds = elapsed, initialObjects = initialCount, finalObjects = finalCount,
                segmentCount = game == null ? 0 : game.Course.ActiveChunkCount, checks = checks.ToArray(), errors = errors.ToArray() };
            Directory.CreateDirectory("../Validation");
            File.WriteAllText("../Validation/unity_playmode_v5.json", JsonUtility.ToJson(report, true));
            if (report.passed) Debug.Log("Fener Runner Play Mode checks passed. Review ../Validation/*.png visually.");
            else Debug.LogError("Fener Runner Play Mode checks failed: " + string.Join("; ", errors));
            Time.timeScale = 1f;
            EditorApplication.isPlaying = false;
        }
        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            if (!finished) Debug.LogWarning("Fener Runner Play Mode check interrupted. No passing report was produced.");
        }
    }
}
#endif
