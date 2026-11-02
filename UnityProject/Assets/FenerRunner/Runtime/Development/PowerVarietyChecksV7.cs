#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class PowerVarietyChecksV7 : MonoBehaviour
    {
        [Serializable] class Report
        {
            public string utc, unityVersion;
            public bool passed;
            public string[] checks, errors;
            public int types, pool, initialObjects, finalObjects, pickupVoiceRequests;
            public int retainedScoreBonus, ordinaryLaneCoins, sweptLaneCoins;
            public float elapsedSeconds, boostedScorePerMetre, airStepApex, airStepHeadViewportY;
            public string scope="Unity Editor Play Mode. Twelve actual proximity pickups; new score, fever, airborne swipe and three-lane coin effects; fixed pools, pause/reset and HUD framing. AutomatedTest suppresses records and wallet writes. No physical-device or listening claim.";
        }

        readonly List<string> checks=new List<string>(), errors=new List<string>();
        readonly PowerupKind[] kinds=(PowerupKind[])Enum.GetValues(typeof(PowerupKind));
        readonly int[] realLevels=new int[3];
        readonly Report report=new Report();
        RunnerGame game;
        RunnerPowerups powers;
        RunnerInput input;
        StadiumSound sound;
        bool background;
        int realBalance;
        float started;

        [MenuItem("Fener Runner/25 - Verify twelve distinct powers")]
        public static void StartChecks()
        {
            SessionState.SetBool("FenerRunner.PowerVarietyChecksV7",true);
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Register()
        {
            EditorApplication.playModeStateChanged-=Changed;
            EditorApplication.playModeStateChanged+=Changed;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("FenerRunner.PowerVarietyChecksV7",false))return;
            SessionState.SetBool("FenerRunner.PowerVarietyChecksV7",false);
            new GameObject("Twelve distinct power checks v7").AddComponent<PowerVarietyChecksV7>();
        }
        IEnumerator Start()
        {
            started=Time.realtimeSinceStartup;
            background=Application.runInBackground;
            Application.runInBackground=true;
            Application.logMessageReceived+=Log;
            game=FindFirstObjectByType<RunnerGame>();
            game.AutomatedTest=true;
            powers=game.Powerups;
            input=FindFirstObjectByType<RunnerInput>();
            sound=game.GetComponent<StadiumSound>();
            realBalance=game.WalletBalance;
            for(int i=0;i<3;i++)realLevels[i]=game.UpgradeLevel((PowerupKind)i);
            yield return Wait(.2f);
            report.initialObjects=Count();report.types=kinds.Length;report.pool=powers.PoolCount;
            Check(kinds.Length==RunnerPowerups.TypeCount&&powers.PoolCount==kinds.Length,"Every named ability has exactly one reusable pickup object");
            Check((int)PowerupKind.CoinRain==7&&(int)PowerupKind.ScoreBoost==8&&(int)PowerupKind.LaneSweep==11,"New ability IDs append after the original eight without changing them");

            game.Obstacles.UseSeedForTest(1907);game.Restart();
            var positions=new float[kinds.Length];var sorted=new float[kinds.Length];
            for(int i=0;i<kinds.Length;i++)positions[i]=sorted[i]=powers.PositionForTest(kinds[i]).z;
            Array.Sort(sorted);bool cadence=Mathf.Abs(sorted[0]-72)<.01f;
            for(int i=1;i<sorted.Length;i++)cadence&=sorted[i]-sorted[i-1]>=104.99f&&sorted[i]-sorted[i-1]<=135.01f;
            Check(cadence,"The raw pickup schedule starts at 72 metres with 105 to 135 metre intervals before route alignment");
            game.Restart();bool same=true;
            for(int i=0;i<kinds.Length;i++)same&=Mathf.Abs(powers.PositionForTest(kinds[i]).z-positions[i])<.01f;
            Check(same,"A fixed course seed reproduces the complete twelve-kind shuffled order");
            game.Obstacles.UseSeedForTest(1908);game.Restart();bool different=false;
            for(int i=0;i<kinds.Length;i++)different|=Mathf.Abs(powers.PositionForTest(kinds[i]).z-positions[i])>.01f;
            Check(different,"Changing the seed changes the shuffled ability order");

            int requestStart=sound.PowerVoiceRequestCount;
            foreach(PowerupKind kind in kinds)
            {
                Begin(false);Check(AllClear(),"Restart clears all twelve timers before "+kind);
                int requests=sound.PowerVoiceRequestCount;
                PlacePower(kind);yield return Wait(.13f);
                Check(powers.ActiveKind==kind&&powers.Remaining(kind)>game.PowerupDuration(kind)-.5f,"An actual pickup activates "+kind);
                Check(sound.PowerVoiceRequestCount==requests+1,"Collected "+kind+" requests exactly one voice");
                Check(game.Interface.powerPanel.activeSelf&&InScreen((RectTransform)game.Interface.powerPanel.transform)&&InScreen(game.Interface.powerStatus.rectTransform),kind+" has a visible power HUD inside the phone bounds");
                if((int)kind>=8)Check(!powers.IsProtected&&powers.ProtectionLabel.Length==0,"The new "+kind+" ability grants no hidden collision immunity");
                game.Pause();float remaining=powers.Remaining(kind);Vector3 position=game.Motor.transform.position;
                yield return Wait(.09f);
                Check(powers.Remaining(kind)==remaining&&game.Motor.transform.position==position,"Pause freezes movement and the "+kind+" timer");
                game.Resume();
            }
            report.pickupVoiceRequests=sound.PowerVoiceRequestCount-requestStart;

            // Points earned before collection must not be multiplied retrospectively.
            Begin(true);MoveRailsAway();PlaceCoin(0,0,1.12f,.55f);yield return Wait(.08f);
            Check(game.Coins==1&&BonusScore()==0,"An ordinary collected coin starts with its normal score and no hidden bonus");
            PlacePower(PowerupKind.ScoreBoost);yield return Wait(.13f);
            Check(powers.ScoreMultiplier==2&&BonusScore()>=0&&BonusScore()<12,"ScoreBoost doubles new earnings without repricing the earlier coin");
            int beforeCoins=game.Coins,beforeScore=game.Score,beforeBonus=BonusScore();
            PlaceCoin(1,0,1.12f,.55f);yield return Wait(.08f);
            Check(game.Coins==beforeCoins+1&&game.Score-beforeScore>=50&&BonusScore()-beforeBonus>=25,"A boosted actual coin awards fifty points but still only one wallet coin");
            game.Tokens.Enabled=false;
            double beforeDistance=game.Distance;beforeScore=game.Score;yield return Wait(.25f);
            report.boostedScorePerMetre=(game.Score-beforeScore)/(float)(game.Distance-beforeDistance);
            Check(Mathf.Abs(report.boostedScorePerMetre-2)<.25f,"ScoreBoost doubles newly travelled distance points");
            yield return Capture("v7-score-boost");
            int previousScore=game.Score;bool monotonic=true;float timeout=Time.realtimeSinceStartup+10;
            while(powers.ScoreBoostRemaining>0&&Time.realtimeSinceStartup<timeout)
            {
                yield return null;monotonic&=game.Score>=previousScore;previousScore=game.Score;
            }
            report.retainedScoreBonus=BonusScore();beforeBonus=report.retainedScoreBonus;beforeScore=game.Score;
            yield return Wait(.18f);
            Check(powers.ScoreMultiplier==1&&monotonic&&report.retainedScoreBonus>25&&BonusScore()==beforeBonus&&game.Score>=beforeScore,"Earned bonus points survive expiry while later distance returns to normal scoring");

            Begin(true);MoveRailsAway();PlacePower(PowerupKind.FeverRush);yield return Wait(.13f);
            Check(game.FeverActive&&game.FeverDuration==8&&game.FeverRemaining>7.5f&&game.Coins==0,"FeverRush starts eight seconds of real stadium fever without granting invisible coins");
            Check(game.CurrentSpeed>game.SpeedForDistance(game.Distance)+2.5f&&game.CoinValue==2,"Instant fever applies the normal speed and coin reward changes");
            PlaceCoin(0,0,1.12f,.55f);yield return Wait(.08f);
            Check(game.Coins==2,"An actual coin pays two during FeverRush");
            yield return Capture("v7-fever-rush");
            yield return Wait(.2f);int refreshRequests=sound.PowerVoiceRequestCount;
            PlacePower(PowerupKind.FeverRush);yield return Wait(.09f);
            Check(game.FeverRemaining>7.75f&&game.FeverRemaining<=8&&sound.PowerVoiceRequestCount==refreshRequests+1,"A second FeverRush refreshes eight seconds instead of stacking an unlimited timer");

            Begin(false);PlacePower(PowerupKind.AirStep);yield return Wait(.13f);
            Check(game.Motor.AirStepEnabled,"An AirStep pickup enables the motor's second airborne swipe");
            Swipe(Vector2.up*150);yield return Wait(.26f);
            int firstJumpCount=game.Motor.JumpCount;float beforeVelocity=game.Motor.VerticalVelocity;
            Swipe(Vector2.up*150);
            Check(game.Motor.AirStepUsed&&game.Motor.JumpCount==firstJumpCount+1&&game.Motor.VerticalVelocity>beforeVelocity,"A second real upward swipe gives a distinct upward impulse in the air");
            int secondJumpCount=game.Motor.JumpCount;float secondVelocity=game.Motor.VerticalVelocity;
            Swipe(Vector2.up*150);
            Check(game.Motor.JumpCount==secondJumpCount&&game.Motor.VerticalVelocity==secondVelocity,"A third airborne swipe cannot repeat the extra impulse");
            bool captured=false;timeout=Time.realtimeSinceStartup+2;
            while(!game.Motor.Grounded&&Time.realtimeSinceStartup<timeout)
            {
                report.airStepApex=Mathf.Max(report.airStepApex,game.Motor.transform.position.y);
                if(!captured&&game.Motor.VerticalVelocity<.8f)
                {
                    captured=true;yield return new WaitForEndOfFrame();
                    Vector3 head=Camera.main.WorldToViewportPoint(game.Motor.transform.position+Vector3.up*2.04f);
                    report.airStepHeadViewportY=head.y;
                    Check(head.z>0&&head.x>.05f&&head.x<.95f&&head.y<.98f,"The extra airborne step keeps the bird's head inside the camera frame");
                    ScreenCapture.CaptureScreenshot(ReviewEvidence.File("../Validation/v7-air-step.png"));
                }
                yield return null;
            }
            Check(captured&&game.Motor.Grounded&&!game.Motor.AirStepUsed&&report.airStepApex>game.Settings.jumpHeight+.5f&&report.airStepApex<=5.05f,"AirStep reaches a higher bounded apex and resets its one-use allowance after landing");
            Swipe(Vector2.up*150);yield return Wait(.15f);Swipe(Vector2.up*150);
            Check(game.Motor.AirStepUsed&&game.Motor.JumpCount==secondJumpCount+2,"The next ground jump receives its own single extra airborne step");
            Begin(false);PlacePower(PowerupKind.AirStep);yield return Wait(.12f);Swipe(Vector2.up*150);yield return Wait(.14f);Swipe(Vector2.down*150);
            int fastFallCount=game.Motor.JumpCount;Swipe(Vector2.up*150);
            Check(game.Motor.FastFalling&&!game.Motor.AirStepUsed&&game.Motor.JumpCount==fastFallCount,"AirStep cannot cancel a deliberate downward fast-fall swipe");
            Begin(false);PlacePower(PowerupKind.AirStep);yield return Wait(.12f);PlacePower(PowerupKind.Flight);yield return Wait(.12f);
            int flightJumpCount=game.Motor.JumpCount;Swipe(Vector2.up*150);
            Check(game.Motor.Flying&&game.Motor.JumpCount==flightJumpCount&&!game.Motor.AirStepUsed,"AirStep cannot add a jump while powered flight is controlling height");

            Begin(true);MoveRailsAway();PlaceThreeLanes(.6f,1.12f);yield return Wait(.13f);
            report.ordinaryLaneCoins=game.Coins;
            Check(game.Coins==1&&game.Tokens.CollectedForTest(1)&&!game.Tokens.CollectedForTest(0)&&!game.Tokens.CollectedForTest(2),"Normal coin collection reaches only the runner's own lane");
            Begin(true);MoveRailsAway();PlacePower(PowerupKind.LaneSweep);yield return Wait(.13f);
            Check(game.Tokens.LaneSweepActive&&!powers.MagnetActive&&game.Tokens.MagnetRadius==0,"LaneSweep enables a three-lane collection width without magnet attraction");
            PlaceThreeLanes(6,1.12f);yield return Capture("v7-lane-sweep");yield return Wait(.36f);
            report.sweptLaneCoins=game.Coins;
            Check(game.Coins==3&&game.Tokens.CollectedForTest(0)&&game.Tokens.CollectedForTest(1)&&game.Tokens.CollectedForTest(2),"Crossing the row with LaneSweep collects actual coins from all three lanes");
            PlaceCoin(3,-1,4.5f,.6f);PlaceCoin(4,1,4.5f,.6f);PlaceCoin(5,1,1.12f,10);
            yield return Wait(.12f);
            Check(game.Coins==3&&!game.Tokens.CollectedForTest(3)&&!game.Tokens.CollectedForTest(4)&&!game.Tokens.CollectedForTest(5),"LaneSweep preserves coin height and crossing distance instead of remotely vacuuming every coin");

            Begin(false);Check(AllClear()&&!game.Motor.AirStepEnabled&&!game.Tokens.LaneSweepActive&&powers.ScoreMultiplier==1&&!game.FeverActive,"Restart clears all twelve powers and their integrated gameplay effects");
            game.ReturnToMenu();report.finalObjects=Count();
            Check(report.initialObjects==report.finalObjects&&powers.PoolCount==kinds.Length,"All repeated pickups and new effect rigs retain fixed object pools");
            var wallet=new RunWallet();bool unchanged=wallet.Balance==realBalance;
            for(int i=0;i<3;i++)unchanged&=wallet.Level((PowerupKind)i)==realLevels[i];
            Check(unchanged,"Automated scenarios preserve the user's wallet and upgrade preferences without writing test preferences");
            report.elapsedSeconds=Time.realtimeSinceStartup-started;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;
            report.checks=checks.ToArray();report.errors=errors.ToArray();report.passed=errors.Count==0;
            File.WriteAllText(ReviewEvidence.File("../Validation/unity_variety_v7.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;
            Debug.Log("Power variety v7 checks "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));
            EditorApplication.isPlaying=false;
        }
        void Begin(bool coins)
        {
            game.Restart();game.Obstacles.SetActive(false);game.Tokens.Enabled=coins;
            powers.Enabled=true;powers.IsolatePickupsForTest();
        }
        void MoveRailsAway()
        {
            for(int i=0;i<game.Tokens.PoolCount;i++)game.Tokens.PlaceForTest(i,new Vector3(9,1.12f,game.Motor.transform.position.z+1000+i*4));
        }
        void PlacePower(PowerupKind kind)=>powers.PlaceForTest(kind,game.Motor.transform.position+new Vector3(0,1.1f,.55f));
        void PlaceCoin(int index,int lane,float height,float ahead)=>game.Tokens.PlaceForTest(index,new Vector3(lane*game.Settings.laneSpacing,height,game.Motor.transform.position.z+ahead));
        void PlaceThreeLanes(float ahead,float height){for(int i=0;i<3;i++)PlaceCoin(i,i-1,height,ahead);}
        int BonusScore()=>game.Score-((int)game.Distance+game.Coins*25+game.Obstacles.Passed*100);
        void Swipe(Vector2 offset)
        {
            Vector2 p=new Vector2(Screen.width*.5f,Screen.height*.5f);
            input.TestBegin(p);input.TestMove(p+offset);input.TestEnd(p+offset);
        }
        bool AllClear()
        {
            foreach(PowerupKind kind in kinds)if(powers.Remaining(kind)!=0)return false;
            return !powers.HasActive&&!game.Motor.Flying&&!powers.IsProtected&&powers.HitGraceRemaining==0;
        }
        static bool InScreen(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            foreach(Vector3 p in corners)if(p.x<-.5f||p.y<-.5f||p.x>Screen.width+.5f||p.y>Screen.height+.5f)return false;
            return true;
        }
        static int Count()=>FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
        static WaitForSecondsRealtime Wait(float seconds)=>new WaitForSecondsRealtime(seconds);
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(ReviewEvidence.File("../Validation/"+name+".png"));yield return null;
        }
        void Check(bool condition,string label){if(condition)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;Application.runInBackground=background;}
    }
}
#endif
