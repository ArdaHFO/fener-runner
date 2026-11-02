#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class DifficultyChecks : MonoBehaviour
    {
        const string SessionKey = "FenerRunner.DifficultyChecks";
        const float TestTimeScale = 2f;
        [Serializable] sealed class Scenario
        {
            public string name;
            public float finalZ, wallSeconds;
            public double distance;
            public int finalLane, jumps, slideCommands;
            public bool finished, passedFirstJump;
        }
        [Serializable] sealed class Report
        {
            public bool passed;
            public string utc, unityVersion;
            public string[] checks, errors;
            public Scenario[] scenarios;
            public float wallSeconds, forwardSpeed, simulationTimeScale = TestTimeScale;
            public float firstJumpZ, firstSlideZ;
            public int seed = 1907;
            public bool powersDisabled, tokensDisabled;
            public string scope = "Real Unity motor, capsule collision and generated multi-lane rows. Baseline powers/tokens disabled. Normal speed settings, simulation runs at2x wall time only for this check. Scripted commands, not physical touchscreen or device FPS. Production best-score preferences restored; AutomatedTest suppresses production wallet settlement.";
        }
        RunnerGame game;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        readonly List<Scenario> scenarios = new List<Scenario>();
        readonly Report report = new Report();
        bool previousBackground, hadBest, hadScore, prepared;
        float previousBest, began;
        int previousScore;

        [MenuItem("Fener Runner/17 - Verify compulsory gesture difficulty")]
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
            new GameObject("Compulsory gesture difficulty checks").AddComponent<DifficultyChecks>();
        }
        IEnumerator Start()
        {
            began = Time.realtimeSinceStartup;
            game = FindFirstObjectByType<RunnerGame>();
            hadBest = PlayerPrefs.HasKey("FenerRunner.Best"); hadScore = PlayerPrefs.HasKey("FenerRunner.BestScore");
            previousBest = PlayerPrefs.GetFloat("FenerRunner.Best", 0); previousScore = PlayerPrefs.GetInt("FenerRunner.BestScore", 0);
            previousBackground = Application.runInBackground; Application.runInBackground = true;
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
            report.passed = errors.Count == 0; report.checks = checks.ToArray(); report.errors = errors.ToArray();
            report.scenarios = scenarios.ToArray(); report.wallSeconds = Time.realtimeSinceStartup - began;
            report.utc = DateTime.UtcNow.ToString("O"); report.unityVersion = Application.unityVersion;
            Directory.CreateDirectory("../Validation");
            File.WriteAllText("../Validation/unity_difficulty_v5.json", JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= Log;
            Debug.Log("Compulsory gesture checks " + (report.passed ? "PASSED" : "FAILED") + ": " + string.Join("; ", errors));
            EditorApplication.isPlaying = false;
        }

        IEnumerator RunScenarios()
        {
            if (game == null || game.Powerups == null) throw new InvalidOperationException("Built v0.5 game with powers is required.");
            game.AutomatedTest = true;
            game.Powerups.Enabled = false; game.Tokens.Enabled = false;
            game.Obstacles.UseSeedForTest(report.seed);
            report.powersDisabled = !game.Powerups.Enabled; report.tokensDisabled = !game.Tokens.Enabled;
            report.forwardSpeed = game.Settings.forwardSpeed;
            Check(report.powersDisabled && report.tokensDisabled, "All difficulty scenarios exclude power protection and token fever");
            Check(game.Settings.forwardSpeed == 18 && game.Settings.maxSpeed <= CoursePatternGenerator.MaximumSpeed,
                "Actual18m/s opening settings remain unchanged and below audited30m/s cap");
            game.Play();
            if (!game.Obstacles.TryGetNextRoute(0, out CourseRoute first) ||
                !game.Obstacles.TryGetNextRoute(first.Z + .01f, out CourseRoute second))
                throw new InvalidOperationException("Opening route rows are missing.");
            report.firstJumpZ = first.Z; report.firstSlideZ = second.Z;
            Check(first.Z == 70 && first.Action == RouteAction.Jump && first.Lane == 0,
                "Opening row70 is a centre-routed compulsory jump");
            Check(second.Z == 115 && second.Action == RouteAction.Slide && second.Lane == 0,
                "Second row115 is a centre-routed compulsory slide");
            int blacks = 0, gates = 0;
            foreach (var item in game.Obstacles.Items)
            {
                if (!item.gameObject.activeSelf) continue;
                if (Mathf.Abs(item.transform.position.z - first.Z) < .01f && item.kind == ObstacleKind.JumpOver) blacks++;
                if (Mathf.Abs(item.transform.position.z - second.Z) < .01f && item.kind == ObstacleKind.SlideUnder) gates++;
            }
            Check(blacks == 3 && gates == 3, "Both compulsory opening rows physically occupy all three lanes");
            VerifyRecurrence();

            string[] names = { "left lane, no actions", "centre lane, no actions", "right lane, no actions",
                "timed lane change at first row", "jump-only strategy", "correct lane/jump/slide strategy" };
            for (int scenarioIndex = 0; scenarioIndex < names.Length; scenarioIndex++)
            {
                game.Play(); Time.timeScale = TestTimeScale;
                var scenario = new Scenario { name = names[scenarioIndex] };
                float started = Time.realtimeSinceStartup;
                if (scenarioIndex == 0) game.HandleCommand(RunnerCommand.Left);
                if (scenarioIndex == 2) game.HandleCommand(RunnerCommand.Right);
                int actioned = -1; bool weaved = false;
                while (game.State == RunState.Running && game.Motor.transform.position.z < 222f && Time.realtimeSinceStartup - started < 10f)
                {
                    float z = game.Motor.transform.position.z;
                    if (z > first.Z + 3.5f) scenario.passedFirstJump = true;
                    if (game.Obstacles.TryGetNextRoute(z, out CourseRoute route))
                    {
                        float ahead = route.Z - z;
                        if (scenarioIndex == 3 && !weaved && ahead <= game.CurrentSpeed * .10f)
                        { game.HandleCommand(RunnerCommand.Right); weaved = true; }
                        if (scenarioIndex == 5 && ahead <= 21f && game.Motor.Lane != route.Lane)
                            game.HandleCommand(game.Motor.Lane < route.Lane ? RunnerCommand.Right : RunnerCommand.Left);
                        if (scenarioIndex >= 4 && route.Sequence != actioned && ahead > 0 && ahead <= game.CurrentSpeed * .45f)
                        {
                            if (scenarioIndex == 4 || route.Action == RouteAction.Jump) game.HandleCommand(RunnerCommand.Jump);
                            else if (route.Action == RouteAction.Slide)
                            { game.HandleCommand(RunnerCommand.Slide); scenario.slideCommands++; }
                            actioned = route.Sequence;
                        }
                    }
                    yield return null;
                }
                scenario.finished = game.State == RunState.Finished;
                scenario.finalZ = game.Motor.transform.position.z; scenario.finalLane = game.Motor.Lane;
                scenario.distance = game.Distance; scenario.jumps = game.Motor.JumpCount;
                scenario.wallSeconds = Time.realtimeSinceStartup - started;
                scenarios.Add(scenario);
                if (scenarioIndex < 4)
                {
                    RunnerObstacle hit = game.Obstacles.Check(game.Motor);
                    Check(scenario.finished && scenario.finalZ > first.Z - 3f && scenario.finalZ < first.Z + 3f &&
                        scenario.jumps == 0 && hit != null && hit.kind == ObstacleKind.JumpOver,
                        names[scenarioIndex] + " collides with the real first black row");
                    if (scenarioIndex == 3) Check(weaved, "The lane-change loophole attempt issues an actual timed movement command");
                }
                else if (scenarioIndex == 4)
                {
                    RunnerObstacle hit = game.Obstacles.Check(game.Motor);
                    Check(scenario.passedFirstJump && scenario.jumps >= 2 && scenario.finished &&
                        scenario.finalZ > second.Z - 3f && scenario.finalZ < second.Z + 3f &&
                        hit != null && hit.kind == ObstacleKind.SlideUnder,
                        "Jump-only play clears the first row but collides with the second slide gate");
                }
                else
                    Check(!scenario.finished && scenario.distance > 200 && scenario.jumps >= 2 && scenario.slideCommands >= 1 && game.Obstacles.Passed >= 4,
                        "Correct real lane/jump/slide commands survive beyond200m and pass at least four rows");
            }
            Check(!game.Powerups.Enabled && !game.Powerups.IsProtected && !game.Motor.Flying && game.Coins == 0,
                "No powerup, flight, shield or coin fever assisted baseline survival");
        }

        void VerifyRecurrence()
        {
            var generator = new CoursePatternGenerator(report.seed);
            double lastJump = -1, lastSlide = -1;
            bool bounded = true, speedSafe = true;
            RouteAction previous = RouteAction.None;
            for (int i = 0; i < 40; i++)
            {
                var row = generator.Next();
                bool jump = row.Left == CourseHazard.Jump && row.Centre == CourseHazard.Jump && row.Right == CourseHazard.Jump;
                bool slide = row.Left == CourseHazard.Slide && row.Centre == CourseHazard.Slide && row.Right == CourseHazard.Slide;
                if (jump) { if (lastJump >= 0) bounded &= row.Distance - lastJump <= CoursePatternGenerator.MaximumCompulsoryActionGap; lastJump = row.Distance; }
                if (slide) { if (lastSlide >= 0) bounded &= row.Distance - lastSlide <= CoursePatternGenerator.MaximumCompulsoryActionGap; lastSlide = row.Distance; }
                if (i > 0 && (row.Action != RouteAction.None || previous != RouteAction.None))
                    speedSafe &= row.Gap / CoursePatternGenerator.MaximumSpeed >= CoursePatternGenerator.MinimumActionGap / CoursePatternGenerator.MaximumSpeed;
                previous = row.Action;
            }
            Check(bounded && lastJump > 0 && lastSlide > 0, "The live seed repeats compulsory jump and slide within240m");
            Check(speedSafe, "The live seed preserves at least1.5s between action rows at30m/s");
        }
        void Check(bool condition, string label) { if (condition) checks.Add(label); else errors.Add(label); }
        void Log(string message, string trace, LogType type)
        { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && errors.Count < 30) errors.Add(message); }
        void OnDestroy()
        {
            Application.logMessageReceived -= Log;
            Time.timeScale = 1f;
            if (!prepared) return;
            Application.runInBackground = previousBackground;
            if (hadBest) PlayerPrefs.SetFloat("FenerRunner.Best", previousBest); else PlayerPrefs.DeleteKey("FenerRunner.Best");
            if (hadScore) PlayerPrefs.SetInt("FenerRunner.BestScore", previousScore); else PlayerPrefs.DeleteKey("FenerRunner.BestScore");
            PlayerPrefs.Save();
        }
    }
}
#endif
