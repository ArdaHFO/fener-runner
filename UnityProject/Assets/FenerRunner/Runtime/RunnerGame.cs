using UnityEngine;

namespace FenerRunner
{
    public enum RunState { Menu, Running, Paused, Finished }

    public sealed class RunnerGame : MonoBehaviour
    {
        [SerializeField] private RunnerSettings settings;
        [SerializeField] private RunnerMotor motor;
        [SerializeField] private RunnerAnimation animationDriver;
        [SerializeField] private RunnerInput input;
        [SerializeField] private FollowCamera followCamera;
        [SerializeField] private EndlessCourse course;
        [SerializeField] private RunnerUI ui;
        [SerializeField] private ObstacleCourse obstacles;
        [SerializeField] private TokenCourse tokens;
        private StadiumSound sound;
        private RunnerFeedback feedback;
        private StadiumWeather weather;
        private RunnerPowerups powerups;
        private RunWallet wallet;
        private bool runOpen,runSettled,resultPending;
        private bool applicationPaused,applicationFocused=true;
        private int scoredPasses, checkpointedCoins;
        private readonly RunFlow flow = new RunFlow();
        public RunFlow Flow => flow;
        public int RunBonusCoins => flow.BonusCoins;
        public int RunEarnings => Coins + RunBonusCoins;
        private double scoreBonus;
        public int Coins {get;private set;}
        public int Charge {get;private set;}
        public float FeverRemaining {get;private set;}
        public bool FeverActive => FeverRemaining>0;
        public float CurrentSpeed {get;private set;}
        public int BestScore {get;private set;}
        public int Score => (int)Distance+Coins*25+obstacles.Passed*100+(int)scoreBonus;
        public float FeverDuration {get;private set;}=5f;
        public TokenCourse Tokens => tokens;
        public RunnerUI Interface => ui;
        public RunnerPowerups Powerups=>powerups;
        public int WalletBalance=>wallet==null?0:wallet.Balance;
        public int UpgradeLevel(PowerupKind kind)=>wallet==null?0:wallet.Level(kind);
        public int UpgradeCost(PowerupKind kind)=>wallet==null?0:wallet.Cost(kind);
        public float PowerupDuration(PowerupKind kind)=>wallet==null?5f:wallet.Duration(kind);
        public float EncoreExtension=>wallet==null?2f:wallet.EncoreExtension;
        public int PermanentLevel(RunnerUpgrade kind)=>wallet==null?0:wallet.Level(kind);
        public int PermanentCost(RunnerUpgrade kind)=>wallet==null?0:wallet.Cost(kind);
        public ChaseVehicleKind SelectedVehicle=>wallet==null?ChaseVehicleKind.RangeRover:wallet.EquippedVehicle;
        public bool OwnsVehicle(ChaseVehicleKind kind)=>wallet!=null&&wallet.OwnsVehicle(kind);
        public int VehicleCost(ChaseVehicleKind kind)=>RunWallet.VehiclePrice(kind);
        bool CanSpend
        {
            get
            {
#if UNITY_EDITOR
                if(AutomatedTest&&wallet!=null&&!wallet.IsIsolated)return false;
#endif
                return State==RunState.Menu&&wallet!=null&&(Multiplayer==null||!Multiplayer.IsActive);
            }
        }
        public bool TryPurchasePermanent(RunnerUpgrade kind)
        {
            if(!CanSpend||!wallet.Purchase(kind))return false;
            ApplyPermanentUpgrades();return true;
        }
        public bool TryPurchaseVehicle(ChaseVehicleKind kind)
        {
            if(!CanSpend||!wallet.PurchaseVehicle(kind))return false;
            Chase?.ApplySelection(wallet.EquippedVehicle);return true;
        }
        public bool TryEquipVehicle(ChaseVehicleKind kind)
        {
            if(!CanSpend||!wallet.EquipVehicle(kind))return false;
            Chase?.ApplySelection(wallet.EquippedVehicle);return true;
        }
        void ApplyPermanentUpgrades()
        {
            tokens.CollectionReachBonus=.06f*PermanentLevel(RunnerUpgrade.ChickSense);
            tokens.HenNestLevel=PermanentLevel(RunnerUpgrade.HenNest);
        }
        public bool TryPurchaseUpgrade(PowerupKind kind)
        {
#if UNITY_EDITOR
            if(AutomatedTest&&wallet!=null&&!wallet.IsIsolated)return false;
#endif
            return CanSpend&&wallet.Purchase(kind);
        }
        private float stadiumTime;
#if UNITY_EDITOR
        public bool AutomatedTest;
#endif
        public ObstacleCourse Obstacles => obstacles;
        public string LastHit { get; private set; }
        public double BestDistance { get; private set; }
        public RunState State { get; private set; }
        public double Distance { get; private set; }
        public RunnerMotor Motor => motor;
        public RunnerAnimation Animation => animationDriver;
        public EndlessCourse Course => course;
        public RunnerSettings Settings => settings;
        public RunnerMultiplayer Multiplayer { get; private set; }
        public MultiplayerUI MultiplayerInterface { get; private set; }
        public ChaseSUV Chase { get; private set; }
        public CrowdThrows CrowdThrows { get; private set; }
        public CrowdReactions CrowdReactions { get; private set; }

        public void Configure(RunnerSettings value, RunnerMotor runner, RunnerAnimation motions,
            RunnerInput controls, FollowCamera cameraRig, EndlessCourse environment, RunnerUI interfaceView, ObstacleCourse obstacleCourse,TokenCourse tokenCourse)
        {
            settings = value; motor = runner; animationDriver = motions; input = controls;
            followCamera = cameraRig; course = environment; ui = interfaceView; obstacles=obstacleCourse;tokens=tokenCourse;
        }
        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            BestDistance=PlayerPrefs.GetFloat("FenerRunner.Best",0f);BestScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);
            sound=GetComponent<StadiumSound>();feedback=GetComponent<RunnerFeedback>();weather=GetComponent<StadiumWeather>();
            wallet=new RunWallet();powerups=GetComponent<RunnerPowerups>();
            if(powerups!=null){powerups.Initialize(this);powerups.Collected+=OnPowerupCollected;powerups.CoinRainVolley+=SpawnCoinRain;powerups.FeverRushTriggered+=StartFeverRush;}
            animationDriver.Initialize();
            motor.Jumped += OnMotorJumped;
            input.Command += HandleCommand;
            ui.Bind(this);
            tokens.HenCollected+=OnHenCollected;
            ResetSession(false);
            Multiplayer=gameObject.AddComponent<RunnerMultiplayer>();Multiplayer.Initialize(this);followCamera.SetMultiplayer(Multiplayer);
            MultiplayerInterface=ui.gameObject.AddComponent<MultiplayerUI>();MultiplayerInterface.Initialize(this,Multiplayer);
            Chase=gameObject.AddComponent<ChaseSUV>();Chase.Initialize(this);followCamera.SetChase(Chase);
            CrowdThrows=gameObject.AddComponent<CrowdThrows>();CrowdThrows.Initialize(this);
            CrowdReactions=gameObject.AddComponent<CrowdReactions>();CrowdReactions.Initialize(this);
        }
        private void LateUpdate()
        {
            if(Chase!=null)Chase.Tick(Time.deltaTime);
            if(CrowdThrows!=null)CrowdThrows.Tick(Time.deltaTime);
            if(CrowdReactions!=null)CrowdReactions.Tick(Time.deltaTime);
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) HandleBack();
            if(State==RunState.Finished)
            {
                if(resultPending&&animationDriver.DeathComplete){resultPending=false;ui.Show(State);}
                return;
            }
            if (State != RunState.Paused && State != RunState.Finished) stadiumTime+=Time.deltaTime;
            Shader.SetGlobalFloat("_StadiumTime",stadiumTime);
            if(State==RunState.Menu)animationDriver.MenuTick(Time.unscaledDeltaTime);
            if (State != RunState.Running) return;
            ApplyPowerState();
            input.Poll();
            if (State != RunState.Running) return;
            AdvanceRun(Time.deltaTime);
            if(State!=RunState.Running)return;
            if(obstacles.Passed>scoredPasses)
            {
                scoredPasses=obstacles.Passed;
                ui.Notify(flow.Streak>0&&flow.Streak%8==0?"AKIŞ MODU · 6 SANİYE EK TEMPO":flow.Streak>3?"TEMİZ SERİ · ×"+flow.Multiplier.ToString("0.0"):"ENGEL GEÇİLDİ  +100");
                if(flow.Streak>0&&flow.Streak%8==0){animationDriver.CelebrateReward(true);feedback.Celebrate();}
            }
            RefreshRunProgress();
            // Looking back is an explicit Editor preview only. Gameplay keeps attention on the route.
            animationDriver.TurnAllowed=false;
            animationDriver.Tick(Time.deltaTime);
            if (motor.transform.position.z >= settings.originShiftDistance)
            {
                float shift = Mathf.Floor(motor.transform.position.z / settings.chunkLength) * settings.chunkLength;
                course.ShiftOrigin(shift);
                obstacles.ShiftOrigin(shift);
                tokens.ShiftOrigin(shift);
                if(powerups!=null)powerups.ShiftOrigin(shift);
                if(CrowdThrows!=null)CrowdThrows.ShiftOrigin(shift);
                if(CrowdReactions!=null)CrowdReactions.ShiftOrigin(shift);
                motor.ShiftOrigin(shift);
                followCamera.ShiftOrigin(shift);
            }
            ui.SetDistance(Distance);
            ui.SetStats(CurrentSpeed,obstacles.Passed,BestDistance);
            ui.SetStage(Distance<250?"ISINMA":Distance<700?"DERBİ TEMPOSU":Distance<1400?"TRİBÜN COŞKUSU":"UZATMA DAKİKALARI");
        }
        private void AdvanceRun(float seconds)
        {
            // Small substeps retain contact on low frame rates without changing the animation clock.
            float remaining = seconds;
            while (remaining > 0f)
            {
                float step = Mathf.Min(remaining, 1f / 60f);
                // Timers and the speed envelope share the motor's bounded clock.
                // A slow rendered frame cannot apply its final recovered speed
                // retroactively to every movement substep in that frame.
                FeverRemaining=Mathf.Max(0,FeverRemaining-step);
                flow.Tick(step);
                if(powerups!=null)powerups.TickTimers(step);
                ApplyPowerState();
                obstacles.BeginMove(motor);
                tokens.BeginMove(motor);
                if(powerups!=null)powerups.BeginMove();
                int multiplier=powerups!=null?powerups.ScoreMultiplier:1;
                int coinValue=CoinValue;
                float travelled=motor.Tick(step);Distance+=travelled;
                course.Tick(motor.transform.position.z);
                int passedBefore=obstacles.Passed, cleanBefore=obstacles.CleanPassed;
                obstacles.Check(motor);
                if(powerups!=null)powerups.PreparePickups(step);
                int passed=obstacles.Passed-passedBefore, clean=obstacles.CleanPassed-cleanBefore;
                scoreBonus+=passed*100*(multiplier-1);
                RunnerObstacle fatal=null;float fatalTime=1f;
                for(int contact=0;contact<obstacles.ContactCount;contact++)
                {
                    if(powerups!=null)powerups.CollectPickupsThrough(obstacles.ContactTimeAt(contact));
                    RunnerObstacle hit=obstacles.ContactAt(contact);
                    flow.BreakStreak();
                    if(powerups==null||!powerups.TryAbsorbHitAt(hit,obstacles.ContactTimeAt(contact))){fatal=hit;fatalTime=obstacles.ContactTimeAt(contact);break;}
                }
                if(fatal==null&&powerups!=null)powerups.CollectPickupsThrough(1f);
                float creditedTravel=travelled*(fatal!=null?fatalTime:1f);
                Distance-=travelled-creditedTravel;scoreBonus+=creditedTravel*(multiplier-1);flow.Travel(creditedTravel);
                // Credit tokens crossed before impact, never those beyond the fatal contact.
                // Non-protective pickup benefits start with the next bounded
                // substep. They never change rewards already crossed this step.
                CollectStep(tokens.Tick(motor,obstacles,step,fatalTime),coinValue,multiplier);
                if(fatal!=null){Finish(fatal);break;}
                if(passed>clean)flow.BreakStreak();
                for(int row=0;row<clean;row++)scoreBonus+=flow.CleanPass()*multiplier;
                if(clean>0&&CrowdReactions!=null)CrowdReactions.NotifyCleanPass(clean,flow.Streak);
                if(powerups!=null)
                {
                    int rhythm=powerups.RewardCleanPasses(passed)*multiplier;scoreBonus+=rhythm;
                    if(rhythm>0){ui.Notify("RİTİM "+powerups.RhythmChain+" · +"+rhythm);feedback.Celebrate();}
                }
                if(powerups!=null)powerups.CompletePickups();
                remaining -= step;
            }
        }
        public float SpeedForDistance(double value)
        {
            double normalizedDistance=System.Math.Max(0,value)*1800d/Mathf.Max(1f,settings.speedRampDistance);
            float progress=(RunTempo.BaseSpeedAtDistance(normalizedDistance)-18f)/16f;
            return Mathf.Lerp(settings.forwardSpeed,settings.maxSpeed,progress);
        }
        private void ApplyPowerState()
        {
            float bonus=(FeverActive?settings.feverSpeedBonus:0)+(powerups!=null&&powerups.DashRemaining>0?4f:0)+flow.SpeedBonus;
            CurrentSpeed=Mathf.Clamp((SpeedForDistance(Distance)+bonus)*(powerups!=null?powerups.SpeedFactor:1f),0f,RunTempo.HardCap);
            motor.Speed=CurrentSpeed;
            motor.JumpHeightMultiplier=powerups!=null?powerups.JumpMultiplier:1;
            motor.AirStepEnabled=powerups!=null&&powerups.AirStepActive;
            tokens.MagnetRadius=powerups!=null&&powerups.MagnetActive?10f:0;
            tokens.LaneSweepActive=powerups!=null&&powerups.LaneSweepActive;
        }
        private int announcedChallenges, announcedBonus;
        private void RefreshRunProgress()
        {
            ui.SetRunProgress(flow.Streak,flow.Multiplier,flow.ChallengeLabel,flow.ChallengeProgress,flow.ChallengeTarget,flow.ChallengeReward);
            if(flow.CompletedChallenges>announcedChallenges)
            {
                announcedChallenges=flow.CompletedChallenges;
                ui.Notify("HEDEF TAMAM! · +"+(flow.BonusCoins-announcedBonus)+" BONUS JETON");
                announcedBonus=flow.BonusCoins;
                feedback.Celebrate();animationDriver.CelebrateReward();sound.Powerup();
            }
        }
        public void HandleBack()
        {
            if(ui.TryCloseOverlay())return;
            if(State==RunState.Running)Pause();else if(State==RunState.Paused)Resume();
        }
        private void Collect(int count)
        {CollectStep(count,CoinValue,powerups!=null?powerups.ScoreMultiplier:1);}
        private void CollectStep(int count,int coinValue,int scoreMultiplier)
        {
            flow.Collect(count);
            if(count>0&&CrowdReactions!=null)CrowdReactions.NotifyTokens(count);
            for(int i=0;i<count;i++)
            {
                int value=coinValue;Coins+=value;scoreBonus+=value*25*(scoreMultiplier-1);
                if(!FeverActive){Charge++;if(Charge>=10){Charge=0;FeverDuration=5;FeverRemaining=5;ui.Notify("TRİBÜN ATEŞİ!  2× ÖDÜL");}}
                sound.Pickup(FeverActive);feedback.Burst();
            }
        }
        public int CoinValue=>(FeverActive?2:1)*(powerups!=null?powerups.CoinMultiplier:1);
        private void SpawnCoinRain(int count){if(State==RunState.Running)tokens.SpawnBonusVolley(motor,count,obstacles);}
        private void OnHenCollected()
        {
            if(State!=RunState.Running)return;
            sound.Powerup();feedback.Celebrate();animationDriver.CelebrateReward();
            ui.Notify("TAVUK BONUSU! · SAÇILAN CİVCİVLERİ TOPLA");
        }
        private void StartFeverRush(){Charge=0;FeverDuration=PowerupDuration(PowerupKind.FeverRush);FeverRemaining=Mathf.Max(FeverRemaining,FeverDuration);}
        public void ExtendFeverRushTo(float remaining)
        {FeverRemaining=Mathf.Max(FeverRemaining,remaining);FeverDuration=Mathf.Max(FeverDuration,FeverRemaining);}
        private void OnPowerupCollected(PowerupKind kind)
        {
            sound.Powerup();
            if(CrowdReactions!=null)CrowdReactions.NotifyPower(kind);
            string label=kind==PowerupKind.Dash?"DEPAR · ENGELLERE DİKKAT":kind==PowerupKind.Flight?"KANAT AÇ · YÜKSEL":kind==PowerupKind.Magnet?"MIKNATIS · JETONLARI ÇEK":kind==PowerupKind.Shield?"KALKAN · TEK DARBE":kind==PowerupKind.DoubleCoins?"ALTIN DOKUNUŞ · ÇİFT JETON":kind==PowerupKind.SlowMotion?"ODAK · KARAR VERME ZAMANI":kind==PowerupKind.SuperJump?"SÜPER SIÇRAMA · YÜKSEĞE ÇIK":"JETON YAĞMURU · TOPLA";
            if(kind==PowerupKind.ScoreBoost)label="SKOR ÇARPANI · YENİ PUANLAR ×2";
            else if(kind==PowerupKind.FeverRush)label="TRİBÜN PATLAMASI · ATEŞİ YAK";
            else if(kind==PowerupKind.AirStep)label="HAVA ADIMI · İKİNCİ ZIPLAMA DAHA GÜÇLÜ";
            else if(kind==PowerupKind.LaneSweep)label="ŞERİT SÜPÜRGESİ · ÜÇ ŞERİDİ TOPLA";
            else if(kind==PowerupKind.Rhythm)label="RİTİM USTASI · TEMİZ GEÇİŞLE SERİ YAP";
            else if(kind==PowerupKind.GoldenTrail)label="JETON PASI · ŞERİT DEĞİŞTİR, ÖDÜLÜ YAKALA";
            else if(kind==PowerupKind.Encore)label=powerups.LastExtendedCount>0?"UZATMA · ETKİN GÜÇLERE +"+EncoreExtension.ToString("0.#")+" SANİYE":"UZATMA · KISA MIKNATIS DESTEĞİ";
            if(powerups.FlockComboActive&&(kind==PowerupKind.Magnet||kind==PowerupKind.DoubleCoins))label="ALTIN SÜRÜ! · TEMİZ GEÇİŞTE BONUS CİVCİV";
            ui.Notify(label);feedback.PowerBurst(kind);animationDriver.CelebrateReward();
        }
        public void HandleCommand(RunnerCommand command)
        {
            if (State != RunState.Running) return;
            if (command == RunnerCommand.DebugTurn) { animationDriver.TryTurn(); return; }
            animationDriver.PlayerCommand();
            int previousLane=motor.Lane;
            switch (command)
            {
                case RunnerCommand.Left: motor.ChangeLane(-1); break;
                case RunnerCommand.Right: motor.ChangeLane(1); break;
                case RunnerCommand.Jump: motor.TryJump(); break;
                case RunnerCommand.Slide:
                    bool chained=motor.Sliding;
                    if(motor.TrySlide()&&CrowdReactions!=null)CrowdReactions.NotifySlide(chained);
                    break;
                case RunnerCommand.DebugFlip: motor.TryJump(true); break;
            }
            if(previousLane!=motor.Lane)
            {sound.LaneChanged(previousLane,motor.Lane);if(powerups!=null)powerups.SuccessfulLaneChange();}
        }
        private void OnMotorJumped(bool flip)
        {
            // The motor event also covers a buffered swipe accepted on landing.
            if(CrowdReactions!=null)CrowdReactions.NotifyJump(motor.AirStepUsed);
        }
        private void Finish(RunnerObstacle hit)
        {
            LastHit=hit.kind==ObstacleKind.JumpOver?"Yukarı kaydır ve üzerinden atla":"Aşağı kaydır ve altından geç";
            State=RunState.Finished;input.ResetGesture();Time.timeScale=0f;
            feedback.ResetFeedback();resultPending=animationDriver.BeginDeath(hit.kind);
            motor.CancelBufferedActions();
            followCamera.BeginDeathPresentation(animationDriver);
            bool persistRecords=true;
#if UNITY_EDITOR
            persistRecords=!AutomatedTest;
#endif
            if(Distance>BestDistance){BestDistance=Distance;if(persistRecords)PlayerPrefs.SetFloat("FenerRunner.Best",(float)BestDistance);}
            if(Score>BestScore){BestScore=Score;if(persistRecords)PlayerPrefs.SetInt("FenerRunner.BestScore",BestScore);}
            if(persistRecords)PlayerPrefs.Save();
            SettleRun();sound.Hit(hit.kind);
            ui.SetResult(Distance,obstacles.Passed,BestDistance,LastHit);
            ui.SetRunSummary(flow.BestStreak,flow.CompletedChallenges,flow.BonusCoins);
            if(!resultPending)ui.Show(State);
        }
        public void Play()
        {
            if(Multiplayer!=null&&Multiplayer.IsActive){MultiplayerInterface.Open();return;}
            if(ui.MultiplayerOpen)return;
            obstacles.ClearSharedSeed();ResetSession(true);
        }
        public void Restart(){Play();}
        public void ReturnToMenu(){if(Multiplayer!=null&&Multiplayer.IsActive)Multiplayer.Leave();obstacles.ClearSharedSeed();ResetSession(false);}
        internal void BeginMultiplayerRun(int seed){obstacles.SetSharedSeed(seed);ResetSession(true);}
        internal void ReturnToMultiplayerLobby(){ResetSession(false);}
        public void Pause()
        {
            if (State != RunState.Running) return;
            State = RunState.Paused;
            CheckpointEarnings();
            input.ResetGesture();
            animationDriver.SetPaused(true);
            Time.timeScale = 0f;
            ui.Show(State);
        }
        public void Resume()
        {
            if (State != RunState.Paused) return;
            input.ResetGesture();
            State = RunState.Running;
            animationDriver.SetPaused(false);
            Time.timeScale = 1f;
            ui.Show(State);
        }
        private void ResetSession(bool running)
        {
            SettleRun();
            resultPending=false;
            Time.timeScale = 1f;
            course.ResetCourse();
            motor.ResetRun();
            feedback.ResetFeedback();
            ApplyPermanentUpgrades();
            obstacles.ResetCourse();tokens.ResetCourse(obstacles); LastHit="";
            if(powerups!=null)powerups.ResetCourse();
            if(sound!=null)sound.ResetSession();
            Coins=Charge=scoredPasses=checkpointedCoins=announcedChallenges=announcedBonus=0;flow.Reset();scoreBonus=0;FeverRemaining=0;FeverDuration=5;CurrentSpeed=settings.forwardSpeed;motor.Speed=CurrentSpeed;ui.ClearFeedback();
            animationDriver.ResetRun(running);
            input.ResetGesture();
            Distance = 0;
            State = running ? RunState.Running : RunState.Menu;
            if(Chase!=null)Chase.ResetPresentation();
            if(CrowdThrows!=null)CrowdThrows.ResetPresentation();
            if(CrowdReactions!=null)CrowdReactions.ResetPresentation();
            runOpen=running;runSettled=false;
            weather.ResetAtmosphere();ui.SetStage("ISINMA");
            followCamera.SetMenuPresentation(!running);followCamera.Snap();
            ui.SetDistance(0);
            RefreshRunProgress();ui.SetRunSummary(0,0,0);
            ui.Show(State);
        }
        private void OnApplicationPause(bool paused)
        {
            applicationPaused=paused;
            ApplyApplicationState();
        }
        private void OnApplicationFocus(bool focus) {
#if UNITY_EDITOR
            if(AutomatedTest)return;
#endif
            applicationFocused=focus;
            ApplyApplicationState();
        }
        private void ApplyApplicationState()
        {
            // Focus and suspension can arrive in either order. Clearing just one
            // signal must not restart an unscaled death/camera in the background.
            bool suspended=applicationPaused||!applicationFocused;
            if(suspended){Pause();CheckpointEarnings();}
            if(State==RunState.Finished)animationDriver.SetPaused(suspended);
            if(sound!=null)sound.SetApplicationSuspended(suspended);
        }
#if UNITY_EDITOR
        public void SetApplicationPauseForTest(bool paused)
        {
            if(!AutomatedTest)throw new System.InvalidOperationException("Lifecycle injection requires an isolated automated test.");
            applicationPaused=paused;ApplyApplicationState();
        }
        public void SetApplicationFocusForTest(bool focused)
        {
            if(!AutomatedTest)throw new System.InvalidOperationException("Lifecycle injection requires an isolated automated test.");
            applicationFocused=focused;ApplyApplicationState();
        }
#endif
        private void OnDestroy()
        {
            SettleRun();
            if (input != null) input.Command -= HandleCommand;
            if (motor != null) motor.Jumped -= OnMotorJumped;
            if(tokens!=null)tokens.HenCollected-=OnHenCollected;
            if(powerups!=null){powerups.Collected-=OnPowerupCollected;powerups.CoinRainVolley-=SpawnCoinRain;powerups.FeverRushTriggered-=StartFeverRush;}
            Time.timeScale = 1f;
        }
        private void SettleRun()
        {
            if(!runOpen||runSettled||wallet==null)return;
            CheckpointEarnings();
            runSettled=true;
        }
        // Mobile processes can be reclaimed without OnDestroy/OnApplicationQuit.
        // Save only the unpaid delta on backgrounding; later finish cannot pay it twice.
        public void CheckpointEarnings()
        {
            if(!runOpen||runSettled||wallet==null)return;
#if UNITY_EDITOR
            if(AutomatedTest&&!wallet.IsIsolated)return;
#endif
            int delta=RunEarnings-checkpointedCoins;
            if(delta<=0)return;
            wallet.Credit(delta);checkpointedCoins=RunEarnings;
        }
        private void OnApplicationQuit(){SettleRun();}
#if UNITY_EDITOR
        // Native Editor regressions drive the same movement/contact routine as Update.
        public void AdvanceForTest(float seconds)
        {
            if(State!=RunState.Running||seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds))return;
            AdvanceRun(seconds);
        }
        public RunWallet UseWalletForTest(string prefix)
        {
            if(!prefix.StartsWith("FenerRunner.Test.",System.StringComparison.Ordinal))throw new System.InvalidOperationException("Tests require an isolated wallet prefix");
            SettleRun();wallet=new RunWallet(prefix);ApplyPermanentUpgrades();Chase?.ApplySelection(wallet.EquippedVehicle);return wallet;
        }
#endif
    }
}
