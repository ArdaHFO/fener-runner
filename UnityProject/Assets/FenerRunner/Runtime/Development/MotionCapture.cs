#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    // Optional Editor evidence harness. It is never attached to the shipped scene
    // and cannot be included in a Player build.
    public sealed class MotionCapture : MonoBehaviour
    {
        const int FramesPerSecond = 24;
        const int TargetFrames = 576;
        static string FrameRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Captures/v7-motion-frames"));
        const string SessionFlag = "FenerRunner.MotionCaptureV5";

        [Serializable] sealed class CaptureReport
        {
            public bool passed;
            public string utc, unityVersion, frameDirectory;
            public int expectedFrameCount, requestedFrameCount, actualFrameCount;
            public int frameRate, width, height, seed, jumpCommands, slideCommands, laneCommands;
            public float capturedSimulationSeconds, fixedDeltaTime, distance;
            public bool audioCaptured;
            public string[] errors;
            public string scope = "Actual Unity Game-view frames with fixed 24 Hz simulation and scripted controls. This is an animation demonstration, not measured device FPS or physical touchscreen input. ScreenCapture does not record audio.";
        }

        RunnerGame game;
        readonly List<string> errors = new List<string>();
        CaptureReport report;
        bool previousBackground, prepared, finished, bestExisted, bestScoreExisted;
        int previousCaptureRate, previousBestScore, actionedSequence = -1;
        float previousBest, previousTimeScale;
        DateTime startedUtc;

        [MenuItem("Fener Runner/13 - Capture 24-second Game motion")]
        public static void StartCapture()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play before motion capture.");
            if (!EditorApplication.ExecuteMenuItem("Fener Runner/5 - Open portrait Game view"))
                throw new InvalidOperationException("Portrait Game view command was not found.");
            SessionState.SetBool(SessionFlag, true);
            EditorApplication.delayCall += EditorApplication.EnterPlaymode;
        }

        [InitializeOnLoadMethod] static void Register()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionFlag, false)) return;
            SessionState.SetBool(SessionFlag, false);
            new GameObject("Editor motion recording harness").AddComponent<MotionCapture>();
        }

        IEnumerator Start()
        {
            previousCaptureRate = Time.captureFramerate;
            previousBackground = Application.runInBackground;
            previousTimeScale = Time.timeScale;
            bestExisted = PlayerPrefs.HasKey("FenerRunner.Best");
            bestScoreExisted = PlayerPrefs.HasKey("FenerRunner.BestScore");
            previousBest = PlayerPrefs.GetFloat("FenerRunner.Best", 0);
            previousBestScore = PlayerPrefs.GetInt("FenerRunner.BestScore", 0);
            prepared = true;
            Application.logMessageReceived += Log;
            startedUtc = DateTime.UtcNow;

            string directory = FrameRoot.Replace("v7-", "v"+ReviewEvidence.Version+"-");
            // Preserve any preceding evidence rather than mixing old and new frames.
            if (Directory.Exists(directory) && Directory.GetFiles(directory, "frame_*.png").Length != 0)
                directory = Path.Combine(directory, startedUtc.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(directory);
            report = new CaptureReport
            {
                utc = startedUtc.ToString("O"), unityVersion = Application.unityVersion,
                frameDirectory = directory, expectedFrameCount = TargetFrames,
                frameRate = FramesPerSecond, fixedDeltaTime = 1f / FramesPerSecond,
                width = Screen.width, height = Screen.height, seed = 1907,
                audioCaptured = false
            };

            game = FindFirstObjectByType<RunnerGame>();
            if (game == null) { errors.Add("RunnerGame was not found."); Complete(); yield break; }
            game.AutomatedTest = true;game.Powerups.Enabled=true;
            Application.runInBackground = true;
            yield return null;
            report.width = Screen.width; report.height = Screen.height;
            if (Screen.width != 1080 || Screen.height != 1920)
            {
                errors.Add("Game view must render at 1080x1920. Actual=" + Screen.width + "x" + Screen.height);
                Complete(); yield break;
            }

            Time.captureFramerate = FramesPerSecond;
            Time.timeScale = 1f;
            game.Obstacles.UseSeedForTest(report.seed);
            game.Obstacles.SetActive(true);
            game.Tokens.Enabled = true;
            game.Play();
            if (Mathf.Abs(game.Settings.forwardSpeed - 18f) > .001f)
                errors.Add("Motion capture requires the final 18 m/s starting speed.");
            if (!game.Obstacles.TryGetNextRoute(game.Motor.transform.position.z, out CourseRoute first)
                || !game.Obstacles.TryGetNextRoute(first.Z + .01f, out CourseRoute second)
                || first.Sequence != 0 || second.Sequence != 1
                || !CentreObstacle(first.Z, ObstacleKind.JumpOver) || !CentreObstacle(second.Z, ObstacleKind.SlideUnder))
            {
                errors.Add("Opening rows no longer match the central jump/slide demonstration.");
                Complete(); yield break;
            }

            for (int frame = 0; frame < TargetFrames; frame++)
            {
                if (game.State != RunState.Running)
                {
                    errors.Add("Run ended before frame " + frame + ": " + game.LastHit);
                    break;
                }
                Drive(frame);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "frame_" + frame.ToString("D4") + ".png"));
                report.requestedFrameCount++;
                yield return null;
            }

            report.distance = (float)game.Distance;
            report.capturedSimulationSeconds = report.requestedFrameCount / (float)FramesPerSecond;
            game.Pause();
            Time.captureFramerate = previousCaptureRate;
            // Screenshot writing may finish after CaptureScreenshot returns.
            float waitUntil = Time.realtimeSinceStartup + 8f;
            while (CountWritten() < report.requestedFrameCount && Time.realtimeSinceStartup < waitUntil)
                yield return null;
            Complete();
        }

        void Drive(int frame)
        {
            // Show normal lane blending before the first obstacle, with the same
            // production input path a swipe uses. No transform teleporting.
            if (frame == 10) Command(RunnerCommand.Left);
            if (frame == 30) Command(RunnerCommand.Right);
            if (!game.Obstacles.TryGetNextRoute(game.Motor.transform.position.z, out CourseRoute route)) return;
            int targetLane = route.Lane;
            RouteAction action = route.Action;
            // The generator's first two warm-up rows have a central black bird
            // then central curly gate. Demonstrate their intended actions instead
            // of choosing the also-valid empty side lane.
            if (route.Sequence == 0) { targetLane = 0; action = RouteAction.Jump; }
            if (route.Sequence == 1) { targetLane = 0; action = RouteAction.Slide; }
            float ahead = route.Z - game.Motor.transform.position.z;
            if (ahead <= 21 && game.Motor.Lane != targetLane)
                Command(game.Motor.Lane < targetLane ? RunnerCommand.Right : RunnerCommand.Left);
            if (route.Sequence == actionedSequence || ahead > game.CurrentSpeed * (action==RouteAction.Jump?game.Settings.JumpRiseSeconds:.42f) || ahead <= 0 || game.Motor.Lane != targetLane) return;
            if (action == RouteAction.Jump) Command(RunnerCommand.Jump);
            if (action == RouteAction.Slide) Command(RunnerCommand.Slide);
            actionedSequence = route.Sequence;
        }

        void Command(RunnerCommand command)
        {
            game.HandleCommand(command);
            if (command == RunnerCommand.Jump) report.jumpCommands++;
            else if (command == RunnerCommand.Slide) report.slideCommands++;
            else if (command == RunnerCommand.Left || command == RunnerCommand.Right) report.laneCommands++;
        }

        bool CentreObstacle(float z, ObstacleKind kind)
        {
            foreach (var item in game.Obstacles.Items)
                if (item.gameObject.activeSelf && item.kind == kind && Mathf.Abs(item.transform.position.x) < .01f
                    && Mathf.Abs(item.transform.position.z - z) < .01f) return true;
            return false;
        }

        int CountWritten()
        {
            int count = 0;
            for (int i = 0; i < report.requestedFrameCount; i++)
            {
                string file = Path.Combine(report.frameDirectory, "frame_" + i.ToString("D4") + ".png");
                if (File.Exists(file) && new FileInfo(file).Length > 0 && File.GetLastWriteTimeUtc(file) >= startedUtc.AddSeconds(-1)) count++;
            }
            return count;
        }

        void Complete()
        {
            finished = true;
            report.actualFrameCount = CountWritten();
            if (report.actualFrameCount != TargetFrames) errors.Add("Expected "+TargetFrames+" fresh frames; found " + report.actualFrameCount + ".");
            report.errors = errors.ToArray();
            report.passed = errors.Count == 0;
            Directory.CreateDirectory("../Validation");
            File.WriteAllText(ReviewEvidence.File("../Validation/unity_motion_capture_v7.json"), JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= Log;
            Debug.Log("Game motion capture " + (report.passed ? "PASSED" : "FAILED") + ": " + report.actualFrameCount + " frames in " + report.frameDirectory);
            EditorApplication.isPlaying = false;
        }

        void Log(string message, string trace, LogType kind)
        { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message); }

        void OnDestroy()
        {
            Application.logMessageReceived -= Log;
            if (!prepared) return;
            Time.captureFramerate = previousCaptureRate;
            Time.timeScale = previousTimeScale;
            Application.runInBackground = previousBackground;
            if (bestExisted) PlayerPrefs.SetFloat("FenerRunner.Best", previousBest); else PlayerPrefs.DeleteKey("FenerRunner.Best");
            if (bestScoreExisted) PlayerPrefs.SetInt("FenerRunner.BestScore", previousBestScore); else PlayerPrefs.DeleteKey("FenerRunner.BestScore");
            PlayerPrefs.Save();
            if (!finished) Debug.LogWarning("Motion capture was interrupted; no passing capture report was produced.");
        }
    }
}
#endif
