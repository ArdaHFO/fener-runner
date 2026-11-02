#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    // Finite, deterministic runtime coverage. These tests move the real controller,
    // query generated colliders and use the real token and Game.Update paths.
    public sealed class ContactMatrixChecksV11 : MonoBehaviour
    {
        [Serializable] sealed class Case
        {
            public string name;
            public int hz, expected, actual;
            public float speed;
            public bool passed;
        }
        [Serializable] sealed class Report
        {
            public bool passed;
            public string utc, unityVersion;
            public string scope = "Finite Unity Editor matrix using actual CharacterController, generated contour/gate colliders, token pools and Game.Update. 18/24/30 m/s at simulated 15/30/60/120 Hz, with production 1/60 maximum physics steps, plus deliberate 6 m unsplit sweeps. Positive and negative controls include lane reversals, jump phases, slide, fast fall, flight, boundary clearance, simultaneous shield contacts, origin shifts and reward pool saturation. No claim of every possible input sequence or physical-device validation. No PlayerPrefs writes are made by this harness.";
            public int[] sampleHz = {15,30,60,120};
            public float[] speeds = {18,24,30};
            public int normalPool, bonusPool, obstaclePool, movementCases, liveCases;
            public float wallSeconds;
            public Case[] cases;
            public string[] checks, errors;
        }
        enum Motion { Straight, Left, Right, Reverse, Jump, JumpLeft, Fall, LateJump, Slide, SlideRight, FastFall, Flight }
        const string SessionKey = "FenerRunner.ContactMatrixChecksV11";
        static readonly FieldInfo SettingsField = typeof(RunnerGame).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic);
        readonly List<Case> cases = new List<Case>();
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        readonly Report report = new Report();
        RunnerGame game;
        RunnerSettings originalSettings, testSettings;
        bool prepared, oldBackground;
        int oldTarget;
        float oldCapture, started;
        string prefsBefore;

        [MenuItem("Fener Runner/55 - Verify v11 contact and coin matrix")]
        public static void StartChecks()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            SessionState.SetBool(SessionKey,true); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Register()
        { EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed; }
        static void Changed(PlayModeStateChange state)
        {
            if(state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionKey,false)) return;
            SessionState.SetBool(SessionKey,false);
            new GameObject("Contact and coin matrix v11").AddComponent<ContactMatrixChecksV11>();
        }
        IEnumerator Start()
        {
            started = Time.realtimeSinceStartup;
            if(ReviewEvidence.Version>=12)
            {
                report.speeds=new[]{18f,24f,30f,34f,38f,RunTempo.HardCap};
                report.scope=report.scope.Replace("18/24/30 m/s","18/24/30/34/38/42 m/s");
            }
            game = FindFirstObjectByType<RunnerGame>(); game.AutomatedTest = true; game.enabled = false;
            prefsBefore = PreferenceSnapshot();
            oldBackground = Application.runInBackground; oldTarget = Application.targetFrameRate; oldCapture = Time.captureDeltaTime;
            Application.runInBackground = true; Application.targetFrameRate = 120; prepared = true;
            originalSettings = game.Settings; testSettings = Instantiate(originalSettings);
            testSettings.name = "Contact matrix runtime settings";
            SettingsField.SetValue(game,testSettings); game.Motor.Configure(testSettings);
            report.normalPool = game.Tokens.PoolCount; report.bonusPool = game.Tokens.BonusPoolCount; report.obstaclePool = game.Obstacles.PoolCount;
            Application.logMessageReceived += Log;
            IEnumerator work = Run();
            while(true)
            {
                bool more; object wait = null;
                try { more = work.MoveNext(); if(more) wait = work.Current; }
                catch(Exception exception) { errors.Add(exception.ToString()); break; }
                if(!more) break;
                yield return wait;
            }
            game.enabled = false; game.ReturnToMenu();
            Check(PreferenceSnapshot() == prefsBefore,"Earned wallet, upgrade levels and records remain byte-for-value unchanged without restoring or writing preferences");
            Check(game.Tokens.PoolCount == report.normalPool && game.Tokens.BonusPoolCount == report.bonusPool && game.Obstacles.PoolCount == report.obstaclePool,
                "All scenarios retain fixed token and obstacle pool capacities");
            report.utc = DateTime.UtcNow.ToString("O"); report.unityVersion = Application.unityVersion;
            report.wallSeconds = Time.realtimeSinceStartup - started; report.cases = cases.ToArray(); report.checks = checks.ToArray(); report.errors = errors.ToArray(); report.passed = errors.Count == 0;
            Directory.CreateDirectory("../Validation"); File.WriteAllText(ReviewEvidence.FileFor(11,"../Validation/unity_contacts_v11.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived -= Log;
            Debug.Log("Contact matrix v11 " + (report.passed ? "PASSED" : "FAILED") + ": " + cases.Count + " scenarios; " + string.Join("; ",errors));
            EditorApplication.isPlaying = false;
        }

        IEnumerator Run()
        {
            foreach(int hz in report.sampleHz)
                foreach(float speed in report.speeds)
                {
                    foreach(Motion motion in Enum.GetValues(typeof(Motion)))
                    {
                        ObstacleKind kind = motion == Motion.Slide || motion == Motion.SlideRight || motion == Motion.Flight ? ObstacleKind.SlideUnder : ObstacleKind.JumpOver;
                        bool expectedHit = motion == Motion.Straight || motion == Motion.Reverse || motion == Motion.Fall || motion == Motion.LateJump || motion == Motion.FastFall;
                        Reset(speed);
                        float lead = motion == Motion.Fall ? .76f : motion == Motion.FastFall ? .60f : motion == Motion.Flight ? .70f : .40f;
                        game.Obstacles.ArrangeTest(kind,game.Motor.transform.position.z + speed * lead);
                        int hit = RunMotion(motion,hz,.96f,null) > 0 ? 1 : 0;
                        Record("Actual collider / " + motion,hz,speed,expectedHit ? 1 : 0,hit);

                        // Record real body positions in a first pass, then replay the same
                        // accepted inputs with independent visible coins at those positions.
                        Reset(speed); var points = new List<Vector3>();
                        RunMotion(motion,hz,.9f,points);
                        Reset(speed); game.Tokens.Enabled = true; ParkTokens();
                        for(int i=0;i<points.Count;i++)
                        {
                            Vector3 point=points[i];
                            // Flight deliberately raises the production ground rail toward
                            // 5.92 m. A pre-raised body sample would be lifted a second time
                            // and would no longer describe the trajectory being tested.
                            if(motion==Motion.Flight)point.y=1.12f;
                            game.Tokens.PlaceForTest(i,point);
                        }
                        game.Tokens.PlaceForTest(10,new Vector3(10,1.12f,game.Motor.transform.position.z + speed*.35f));
                        game.Tokens.PlaceForTest(11,new Vector3(0,12,game.Motor.transform.position.z + speed*.35f));
                        RunMotion(motion,hz,.9f,null);
                        Record((motion==Motion.Flight?"Actual ascending flight rail coins / ":"Actual body path coins / ") + motion,hz,speed,points.Count,game.Tokens.TotalCollected);
                        Check(!game.Tokens.CollectedForTest(10) && !game.Tokens.CollectedForTest(11),"Off-path lane and height controls remain uncollected / " + motion + " / " + speed + " / " + hz);
                        int before = game.Tokens.TotalCollected;
                        game.Tokens.Tick(game.Motor,game.Obstacles,0);
                        Check(game.Tokens.TotalCollected == before,"Repeated overlap never duplicates a token / " + motion + " / " + speed + " / " + hz);
                        report.movementCases += 2;
                    }
                    // Yield between bounded blocks so the native Editor remains responsive.
                    yield return null;
                }

            if(ReviewEvidence.Version>=12)
                foreach(int hz in new[]{15,30,60})
                    foreach(float speed in new[]{34f,38f,RunTempo.HardCap})
                    {
                        Reset(speed);game.Tokens.Enabled=true;
                        // Keep real route-managed coins around the jump. Only unrelated
                        // coins are moved with the deliberately unbound placement helper.
                        int expected=0;
                        for(int i=0;i<game.Tokens.PoolCount;i++)
                        {
                            float coinZ=game.Tokens.PositionForTest(i).z;
                            if(coinZ>=54&&coinZ<=86)expected++;
                            else game.Tokens.PlaceForTest(i,new Vector3(0,1.12f,1000+i*4.4f));
                        }
                        game.Obstacles.SetActive(true);game.Motor.Tick((44-game.Motor.transform.position.z)/speed);
                        Time.captureDeltaTime=1f/hz;game.enabled=true;bool jumped=false;int frames=0;
                        while(game.State==RunState.Running&&game.Motor.transform.position.z<92&&frames++<120)
                        {
                            if(!jumped&&70-game.Motor.transform.position.z<=game.CurrentSpeed*game.Settings.JumpRiseSeconds)
                            {game.HandleCommand(RunnerCommand.Jump);jumped=true;}
                            yield return null;
                        }
                        Record("Game.Update actual speed-adaptive jump rail",hz,speed,expected,game.Tokens.TotalCollected);
                        Check(expected==8&&jumped&&game.State==RunState.Running,
                            "Eight real route coins remain reachable through a surviving jump / "+speed+" m/s / "+hz+" Hz");
                        report.liveCases++;
                    }

            GeometryAndOrdering();
            TokenEdgesAndPools();
            ProtectionLifetime();

            foreach(int hz in report.sampleHz)
                foreach(float speed in report.speeds)
                    foreach(ObstacleKind kind in new[]{ObstacleKind.JumpOver,ObstacleKind.SlideUnder})
                        foreach(int lane in new[]{-1,0,1})
                        {
                            Reset(speed); SetLane(lane);
                            RunnerObstacle arranged=game.Obstacles.ArrangeTest(kind,game.Motor.transform.position.z+3,lane);
                            Check(game.Motor.Lane==lane&&Mathf.Abs(game.Motor.transform.position.x-arranged.transform.position.x)<.01f,
                                "Fatal scenario starts on the same physical lane as its obstacle / "+kind+" / "+lane+" / "+speed+" / "+hz);
                            Time.captureDeltaTime=1f/hz; game.enabled=true;
                            int frames=0; while(game.State==RunState.Running && frames++<40) yield return null;
                            Record("Game.Update fatal " + kind + " lane " + lane,hz,speed,1,game.State==RunState.Finished ? 1 : 0); report.liveCases++;
                        }

            foreach(PowerupKind power in new[]{PowerupKind.Dash,PowerupKind.Rhythm,PowerupKind.GoldenTrail,PowerupKind.Encore})
            {
                Reset(30); EnablePowers(); game.Powerups.Activate(power);
                game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+2);
                Time.captureDeltaTime=1f/15;game.enabled=true;
                int frames=0;while(game.State==RunState.Running&&frames++<20)yield return null;
                Record("Game.Update non-protective power " + power,15,30,1,game.State==RunState.Finished ? 1:0);report.liveCases++;
            }
            Reset(30); EnablePowers(); game.Powerups.Activate(PowerupKind.Shield);
            float z=game.Motor.transform.position.z+2;
            game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,z);
            game.Obstacles.AddTestObstacle(ObstacleKind.SlideUnder,z+.05f);
            Time.captureDeltaTime=1f/15;game.enabled=true;
            int shieldFrames=0;while(game.State==RunState.Running&&shieldFrames++<20)yield return null;
            Check(game.State==RunState.Finished && !game.Powerups.ShieldActive && game.Powerups.HitGraceRemaining>0,
                "Game.Update evaluates a distinct overlapping obstacle after the first consumes the shield in the same frame");
            report.liveCases++;

            for(int multiplierCase=0;multiplierCase<4;multiplierCase++)
            {
                Reset(30);EnablePowers();game.Tokens.Enabled=true;ParkTokens();
                if((multiplierCase&1)!=0)game.Powerups.Activate(PowerupKind.DoubleCoins);
                if((multiplierCase&2)!=0)game.Powerups.Activate(PowerupKind.FeverRush);
                game.Powerups.Activate(PowerupKind.LaneSweep);
                float coinZ=game.Motor.transform.position.z+2;
                for(int lane=-1;lane<=1;lane++)game.Tokens.PlaceForTest(lane+1,new Vector3(lane*testSettings.laneSpacing,1.12f,coinZ));
                Time.captureDeltaTime=1f/15;game.enabled=true;
                int frames=0;while(game.Tokens.TotalCollected<3&&frames++<20)yield return null;
                int value=((multiplierCase&1)!=0?2:1)*((multiplierCase&2)!=0?2:1);
                Record("Game.Update lane sweep raw coins / multiplier " + value,15,30,3,game.Tokens.TotalCollected);
                Record("Game.Update credited coins / multiplier " + value,15,30,3*value,game.Coins);report.liveCases++;
            }
            Reset(30);game.Tokens.Enabled=true;ParkTokens();
            float startZ=game.Motor.transform.position.z;
            game.Tokens.PlaceForTest(0,new Vector3(0,1.12f,startZ+1));
            game.Tokens.PlaceForTest(1,new Vector3(0,1.12f,startZ+5));
            game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,startZ+3);
            Time.captureDeltaTime=1f/15;game.enabled=true;
            int fatalFrames=0;while(game.State==RunState.Running&&fatalFrames++<20)yield return null;
            Check(game.State==RunState.Finished&&game.Coins==1&&game.Tokens.TotalCollected==1,
                "Real fatal Game updates credit the preceding coin and leave the coin beyond impact uncollected");report.liveCases++;
        }

        int RunMotion(Motion motion,int hz,float duration,List<Vector3> samples)
        {
            int frameCount=Mathf.CeilToInt(duration*hz),hits=0,nextSample=0;
            float elapsed=0; bool initial=false,secondary=false;
            bool lateral=motion==Motion.Left||motion==Motion.Right||motion==Motion.Reverse||motion==Motion.JumpLeft||motion==Motion.SlideRight;
            float[] sampleTimes=lateral?new[]{.065f,.115f,.43f}:new[]{.20f,.43f,.67f};
            for(int frame=0;frame<frameCount;frame++)
            {
                if(!initial)
                {
                    initial=true;
                    if(motion==Motion.Left||motion==Motion.JumpLeft)game.Motor.ChangeLane(-1);
                    if(motion==Motion.Right||motion==Motion.Reverse||motion==Motion.SlideRight)game.Motor.ChangeLane(1);
                    if(motion==Motion.Jump||motion==Motion.JumpLeft||motion==Motion.Fall||motion==Motion.FastFall)game.Motor.TryJump();
                    if(motion==Motion.Slide||motion==Motion.SlideRight)game.Motor.TrySlide();
                    if(motion==Motion.Flight)game.Motor.SetFlight(true);
                }
                if(!secondary)
                {
                    if(motion==Motion.Reverse&&elapsed>=.10f){game.Motor.ChangeLane(-1);secondary=true;}
                    if(motion==Motion.FastFall&&elapsed>=.20f){game.Motor.TrySlide();secondary=true;}
                    if(motion==Motion.LateJump&&elapsed>=.34f){game.Motor.TryJump();secondary=true;}
                }
                float remaining=1f/hz;
                while(remaining>.000001f)
                {
                    float step=Mathf.Min(remaining,1f/60f);
                    game.Obstacles.BeginMove(game.Motor);game.Tokens.BeginMove(game.Motor);
                    game.Motor.Tick(step);
                    if(game.Obstacles.Check(game.Motor)!=null)hits++;
                    game.Tokens.Tick(game.Motor,game.Obstacles,step);
                    elapsed+=step;remaining-=step;
                    if(samples!=null&&nextSample<sampleTimes.Length&&elapsed>=sampleTimes[nextSample])
                    {samples.Add(game.Motor.transform.position+Vector3.up*(game.Motor.Capsule.height*.5f));nextSample++;}
                }
            }
            return hits;
        }

        void GeometryAndOrdering()
        {
            Reset(30);float z=game.Motor.transform.position.z;
            RunnerObstacle near=game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,z+2),far=game.Obstacles.AddTestObstacle(ObstacleKind.SlideUnder,z+5.6f);
            game.Obstacles.BeginMove(game.Motor);game.Motor.Tick(.2f);game.Obstacles.Check(game.Motor);
            Check(game.Obstacles.ContactCount==2&&game.Obstacles.ContactAt(0)==near&&game.Obstacles.ContactAt(1)==far&&game.Obstacles.ContactTimeAt(0)<game.Obstacles.ContactTimeAt(1),
                "A 6 metre unsplit move orders a swept-only earlier contact ahead of a later endpoint contact");
            game.Obstacles.BeginMove(game.Motor);game.Obstacles.Check(game.Motor);
            Check(game.Obstacles.ContactCount==1&&game.Obstacles.ContactAt(0)==far&&game.Obstacles.ContactTimeAt(0)<.00001f,
                "An existing overlap starts at time zero and previous swept contacts do not linger");

            Reset(30);z=game.Motor.transform.position.z;
            game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,z+3);
            for(int i=1;i<20;i++)game.Obstacles.AddTestObstacle(ObstacleKind.JumpOver,z+3+i*.002f);
            game.Obstacles.BeginMove(game.Motor);game.Motor.Tick(.2f);game.Obstacles.Check(game.Motor);
            Check(game.Obstacles.ContactCount==20,"A broad phase with 160 contour boxes retains all 20 unique obstacle contacts after buffer saturation");

            foreach(int side in new[]{-1,1})
            {
                Reset(18);var black=game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,14);
                Teleport(new Vector3(side*1.08f,1.72f,14));game.Obstacles.BeginMove(game.Motor);
                Check(game.Obstacles.Check(game.Motor)==null,"The visible upper contour corner stays clear on side "+side);
                Teleport(new Vector3(0,1.30f,14));game.Obstacles.BeginMove(game.Motor);
                Check(game.Obstacles.Check(game.Motor)==black,"True head contact remains detected after the corner-clearance control on side "+side);
            }
            Reset(18);var gate=game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder,14);
            game.Motor.TrySlide();game.Motor.Speed=0;game.Motor.Tick(.2f);
            float opening=VisibleHeaderUnderside(gate);
            float touchingY=opening-game.Motor.Capsule.height;
            foreach(float margin in new[]{-.04f,.04f})
            {
                Teleport(new Vector3(0,touchingY+margin,14));game.Obstacles.BeginMove(game.Motor);
                Check((game.Obstacles.Check(game.Motor)!=null)==(margin>0),"Visible sliding header boundary has correct clear/contact result at "+margin.ToString("F2",CultureInfo.InvariantCulture)+" metres");
            }

            Reset(30);z=game.Motor.transform.position.z;
            near=game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,z+3);near.Scored=true;
            game.Obstacles.BeginMove(game.Motor);game.Obstacles.ShiftOrigin(600);game.Motor.ShiftOrigin(600);game.Course.ShiftOrigin(600);
            game.Motor.Tick(.2f);
            Check(game.Obstacles.Check(game.Motor)==near,"Floating-origin adjustment preserves a sampled sweep, including an already-scored obstacle");
            game.Obstacles.SetActive(false);game.Obstacles.Check(game.Motor);
            Check(game.Obstacles.ContactCount==0,"Disabling the course clears the previous contact list");
        }

        void TokenEdgesAndPools()
        {
            foreach(float margin in new[]{-.04f,.04f})
            {
                Reset(30);game.Tokens.Enabled=true;ParkTokens();
                // v20 deliberately widens only the horizontal pickup silhouette.
                // Keep the same inside/outside margins at its new actual edge.
                float x=game.Motor.Capsule.radius+game.Tokens.PickupRadius+.20f+Mathf.Clamp(game.Tokens.CollectionReachBonus,0,.18f)+margin,z=game.Motor.transform.position.z;
                game.Tokens.PlaceForTest(0,new Vector3(x,1.12f,z+3));
                game.Tokens.BeginMove(game.Motor);game.Motor.Tick(.2f);
                int picked=game.Tokens.Tick(game.Motor,game.Obstacles,.2f);
                Record("Coin radial boundary " + margin.ToString("F2",CultureInfo.InvariantCulture),5,30,margin<0?1:0,picked);
            }
            foreach(float margin in new[]{-.12f,.12f})
            {
                Reset(30);game.Tokens.Enabled=true;ParkTokens();
                float y=game.Motor.transform.position.y+game.Motor.Capsule.height+game.Tokens.PickupRadius+margin;
                game.Tokens.PlaceForTest(0,new Vector3(0,y,game.Motor.transform.position.z+3));
                game.Tokens.BeginMove(game.Motor);game.Motor.Tick(.2f);
                Record("Coin upper cap boundary "+margin.ToString("F2",CultureInfo.InvariantCulture),5,30,margin<0?1:0,game.Tokens.Tick(game.Motor,game.Obstacles,.2f));
            }
            Reset(30);game.Tokens.Enabled=true;ParkTokens();float start=game.Motor.transform.position.z;
            game.Tokens.PlaceForTest(0,new Vector3(0,1.12f,start+1));game.Tokens.PlaceForTest(1,new Vector3(0,1.12f,start+5.5f));
            game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,start+3.5f);
            game.Tokens.BeginMove(game.Motor);game.Obstacles.BeginMove(game.Motor);game.Motor.Tick(.2f);game.Obstacles.Check(game.Motor);
            float impact=game.Obstacles.ContactTimeAt(0);
            Check(impact>0&&impact<1&&game.Tokens.Tick(game.Motor,game.Obstacles,.2f,impact)==1&&!game.Tokens.CollectedForTest(1),
                "Fractional fatal contact collects a crossed earlier coin but clips a later coin within the same 6 metre move");

            Reset(30);game.Tokens.Enabled=true;ParkTokens();start=game.Motor.transform.position.z;
            game.Tokens.PlaceForTest(0,new Vector3(0,1.12f,start+3));
            game.Tokens.BeginMove(game.Motor);game.Tokens.ShiftOrigin(600);game.Motor.ShiftOrigin(600);game.Course.ShiftOrigin(600);game.Obstacles.ShiftOrigin(600);
            game.Motor.Tick(.2f);
            Check(game.Tokens.Tick(game.Motor,game.Obstacles,.2f)==1,"Coin and player histories remain aligned across a floating-origin shift during a sampled move");

            foreach(bool magnet in new[]{false,true})
            {
                Reset(30);game.Tokens.Enabled=true;ParkTokens();game.Tokens.MagnetRadius=magnet?10:0;
                start=game.Motor.transform.position.z;game.Tokens.PlaceForTest(0,new Vector3(testSettings.laneSpacing,1.12f,start+7));
                game.Tokens.PlaceBonusForTest(0,new Vector3(-testSettings.laneSpacing,3.6f,start+8),1.12f);
                for(int i=0;i<50;i++){game.Tokens.BeginMove(game.Motor);game.Motor.Tick(1f/60);game.Tokens.Tick(game.Motor,game.Obstacles,1f/60);}
                Record("Adjacent normal and falling bonus coins / magnet "+magnet,60,30,magnet?2:0,game.Tokens.TotalCollected);
            }
            Reset(30);game.Tokens.Enabled=true;ParkTokens();
            var positions=new Vector3[game.Tokens.BonusPoolCount];
            for(int i=0;i<positions.Length;i++){positions[i]=new Vector3((i%3-1)*3.3f,3+i*.01f,100+i);game.Tokens.PlaceBonusForTest(i,positions[i],1.12f);}
            game.Tokens.SpawnBonusVolley(game.Motor,5,game.Obstacles);
            bool retained=game.Tokens.BonusSpawned==0&&game.Tokens.BonusSkipped==5;
            for(int i=0;i<positions.Length;i++)retained &= game.Tokens.BonusActiveForTest(i)&&game.Tokens.BonusPositionForTest(i)==positions[i];
            Check(retained,"A full bonus pool drops the incoming request without replacing any of its 20 visible coins");
            game.Tokens.PlaceBonusForTest(7,game.Motor.transform.position+Vector3.up,1.12f);game.Tokens.BeginMove(game.Motor);game.Tokens.Tick(game.Motor,game.Obstacles,0);
            game.Tokens.SpawnBonusVolley(game.Motor,4,game.Obstacles);
            Check(game.Tokens.BonusSpawned==1&&game.Tokens.BonusSkipped==8&&game.Tokens.BonusActiveForTest(7),
                "A collected bonus slot is reused once while the other three requests are dropped without overwriting active coins");

            Reset(30);game.Tokens.Enabled=true;ParkTokens();EnablePowers();
            game.Powerups.Activate(PowerupKind.CoinRain);game.Powerups.Activate(PowerupKind.GoldenTrail);
            for(int i=0;i<8;i++)
            {game.Powerups.TickTimers(.81f);game.HandleCommand(i%2==0?RunnerCommand.Right:RunnerCommand.Left);}
            int active=0;for(int i=0;i<game.Tokens.BonusPoolCount;i++)if(game.Tokens.BonusActiveForTest(i))active++;
            Check(active==20&&game.Tokens.BonusSpawned==20&&game.Tokens.BonusSkipped>0&&game.Powerups.TrailVolleyCount>0,
                "Combined actual rain and successful-lane reward events remain bounded and skip excess emissions");
        }

        void ProtectionLifetime()
        {
            Reset(30);EnablePowers();float z=game.Motor.transform.position.z;
            RunnerObstacle first=game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,z+3),other=game.Obstacles.AddTestObstacle(ObstacleKind.JumpOver,z+4);
            game.Powerups.Activate(PowerupKind.Shield);
            Check(game.Powerups.TryAbsorbHit(first)&&game.Powerups.TryAbsorbHit(first)&&!game.Powerups.TryAbsorbHit(other),
                "Shield consumption protects repeated contact with exactly the absorbed object, not a distinct nearby object");
            game.Powerups.TickTimers(.36f);
            Check(!game.Powerups.TryAbsorbHit(first)&&!game.Powerups.IsProtected,"The consumed shield expires without leaving general immunity");
            Reset(30);EnablePowers();game.Powerups.Activate(PowerupKind.Flight);
            Check(!game.Powerups.IsProtected&&!game.Powerups.TryAbsorbHit(first),"Flight ascent itself grants no invisible obstacle immunity");
            for(int i=0;i<45;i++){game.Powerups.TickTimers(1f/60);game.Motor.Tick(1f/60);}
            game.Powerups.TickTimers(game.Powerups.FlightRemaining+.01f);
            Check(game.Powerups.IsProtected&&game.Powerups.ProtectionLabel=="GÜVENLİ İNİŞ"&&game.Powerups.TryAbsorbHit(first),
                "Controlled flight descent is explicitly labelled and intentionally protects contact");
            for(int i=0;i<180;i++){game.Motor.Tick(1f/60);game.Powerups.TickTimers(1f/60);}
            Check(game.Motor.Grounded&&!game.Powerups.IsProtected&&!game.Powerups.TryAbsorbHit(first),
                "Landing protection expires and ordinary fatal-contact rules resume");
        }

        static float VisibleHeaderUnderside(RunnerObstacle obstacle)
        {
            // The former enclosing header box extends below the visible body.
            // Derive this fixture independently from the live rendered geometry,
            // rather than reusing either that box or the cooked hitMesh data.
            var reference=new GameObject("Independent contact-matrix header surface");reference.layer=31;
            var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            try
            {
                var vertices=new List<Vector3>();var indices=new List<int>();
                foreach(var filter in obstacle.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer=filter.GetComponent<MeshRenderer>();
                    if(renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                    int offset=vertices.Count;Matrix4x4 toWorld=filter.transform.localToWorldMatrix;
                    foreach(var point in filter.sharedMesh.vertices)vertices.Add(toWorld.MultiplyPoint3x4(point));
                    foreach(int index in filter.sharedMesh.triangles)indices.Add(offset+index);
                }
                if(vertices.Count==0)throw new InvalidOperationException("Live final gate rendering geometry is missing.");
                mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
                var collider=reference.AddComponent<MeshCollider>();collider.sharedMesh=mesh;Physics.SyncTransforms();
                var ray=new Ray(obstacle.transform.position+Vector3.up*.02f,Vector3.up);
                if(!collider.Raycast(ray,out RaycastHit underside,5))
                    throw new InvalidOperationException("Independent native ray did not find the visible gate underside.");
                return underside.point.y;
            }
            finally
            {
                DestroyImmediate(reference);DestroyImmediate(mesh);Physics.SyncTransforms();
            }
        }

        void Reset(float speed)
        {
            game.enabled=false;testSettings.forwardSpeed=testSettings.maxSpeed=speed;
            game.Powerups.Enabled=false;game.Tokens.Enabled=false;game.Play();
            game.Obstacles.SetActive(false);game.Motor.Speed=speed;game.Animation.TurnAllowed=false;
        }
        void EnablePowers(){game.Powerups.Enabled=true;game.Powerups.IsolatePickupsForTest();}
        void ParkTokens(){for(int i=0;i<game.Tokens.PoolCount;i++)game.Tokens.PlaceForTest(i,new Vector3(0,1.12f,1000+i*4.4f));}
        void SetLane(int lane)
        {game.Motor.Speed=0;game.Motor.ChangeLane(lane);game.Motor.Tick(.2f);game.Motor.Speed=testSettings.forwardSpeed;}
        void Teleport(Vector3 position)
        {game.Motor.Capsule.enabled=false;game.Motor.transform.position=position;game.Motor.Capsule.enabled=true;Physics.SyncTransforms();}
        void Record(string name,int hz,float speed,int expected,int actual)
        {
            bool pass=expected==actual;cases.Add(new Case{name=name,hz=hz,speed=speed,expected=expected,actual=actual,passed=pass});
            if(!pass)errors.Add(name+" / "+speed+" m/s / "+hz+" Hz: expected "+expected+", actual "+actual);
        }
        void Check(bool condition,string label){if(condition)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type)
        {if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<100)errors.Add(message);}
        static string PreferenceSnapshot()
        {
            var values=new List<string>();string prefix=RunWallet.DefaultPrefix;
            foreach(string key in new[]{prefix+"Coins",prefix+"Upgrade.0",prefix+"Upgrade.1",prefix+"Upgrade.2","FenerRunner.BestScore"})
                values.Add(key+"="+PlayerPrefs.HasKey(key)+"/"+PlayerPrefs.GetInt(key,int.MinValue));
            values.Add("FenerRunner.Best="+PlayerPrefs.HasKey("FenerRunner.Best")+"/"+PlayerPrefs.GetFloat("FenerRunner.Best",-1).ToString("R",CultureInfo.InvariantCulture));
            return string.Join("|",values);
        }
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;if(!prepared)return;
            Time.timeScale=1;Time.captureDeltaTime=oldCapture;Application.targetFrameRate=oldTarget;Application.runInBackground=oldBackground;
            if(game!=null&&originalSettings!=null){SettingsField.SetValue(game,originalSettings);game.Motor.Configure(originalSettings);game.enabled=true;}
            if(testSettings!=null)Destroy(testSettings);
        }
    }
}
#endif
