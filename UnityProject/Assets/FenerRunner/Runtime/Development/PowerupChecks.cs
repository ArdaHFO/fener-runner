#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class PowerupChecks : MonoBehaviour
    {
        [Serializable] class Report
        {
            public string utc,unityVersion;
            public bool passed;
            public string[] checks,errors;
            public int initialObjects,finalObjects,powerPool,walletEarned,voiceCalls;
            public float measuredFlightHeight;
            public string scope="Real Unity Editor Play Mode. Actual pickup proximity, capsule flight/protection, magnetic coin motion, timer pause/reset, origin shift and earned-currency upgrade persistence using isolated test-only PlayerPrefs. Not device performance or auditory review.";
        }
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        readonly List<RunWallet> isolatedWallets=new List<RunWallet>();
        Report report=new Report();RunnerGame game;RunnerPowerups powers;StadiumSound sound;
        bool background;float oldBest;int oldScore;
        [MenuItem("Fener Runner/16 - Verify powers and earned upgrades")]
        public static void StartChecks(){SessionState.SetBool("FenerRunner.PowerChecks",true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("FenerRunner.PowerChecks",false))return;
            SessionState.SetBool("FenerRunner.PowerChecks",false);new GameObject("Powerup integration checks").AddComponent<PowerupChecks>();
        }
        IEnumerator Start()
        {
            background=Application.runInBackground;Application.runInBackground=true;Application.logMessageReceived+=Log;
            oldBest=PlayerPrefs.GetFloat("FenerRunner.Best",0);oldScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);
            game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;powers=game.Powerups;sound=game.GetComponent<StadiumSound>();
            string prefix="FenerRunner.Test.Powerups."+DateTime.UtcNow.Ticks+".";
            isolatedWallets.Add(game.UseWalletForTest(prefix));
            yield return Wait(.3f);
            Check(game.WalletBalance==0&&game.UpgradeLevel(PowerupKind.Dash)==0,"A fresh wallet starts with zero earned currency and no upgrades");
            Check(!game.TryPurchaseUpgrade(PowerupKind.Dash)&&game.WalletBalance==0,"Insufficient currency cannot buy an upgrade");
            Check(sound.HasUserVoices,"All seven supplied powerup voice clips are assigned");
            int silentCount=sound.VoicePlayCount;sound.Pickup(false);sound.Pickup(true);
            Check(sound.VoicePlayCount==silentCount,"Ordinary coins do not trigger the user voice clips");

            BeginIsolatedRun(false);report.initialObjects=Count();report.powerPool=powers.PoolCount;
            int voicesBefore=sound.VoicePlayCount;
            PlacePower(PowerupKind.Dash);yield return Wait(.13f);
            Check(powers.DashRemaining>4.7f&&game.CurrentSpeed>22&&game.CurrentSpeed<=30,"Dash pickup increases actual forward speed within the hard cap");
            Check(sound.VoicePlayCount==voicesBefore+1&&sound.LastVoiceIndex>=0&&sound.LastVoiceIndex<7,"One collected powerup triggers exactly one supplied voice");
            yield return Capture("v7-dash");
            Check(!powers.IsProtected,"Dash grants speed without hidden collision immunity");
            game.Pause();float dash=powers.DashRemaining;Vector3 pausedAt=game.Motor.transform.position;
            yield return Wait(.3f);Check(powers.DashRemaining==dash&&game.Motor.transform.position==pausedAt,"Pause freezes the active power timer and physical movement");game.Resume();
            yield return Wait(powers.DashRemaining+.7f);
            Check(powers.DashRemaining==0&&game.CurrentSpeed<24&&!powers.IsProtected,"Dash expires and restores baseline speed");

            BeginIsolatedRun(true);game.HandleCommand(RunnerCommand.Jump);yield return Wait(.12f);
            PlacePower(PowerupKind.Flight);yield return Wait(1f);
            report.measuredFlightHeight=game.Motor.transform.position.y;
            Check(powers.FlightRemaining>0&&game.Motor.Flying&&game.Motor.FlightRequested&&report.measuredFlightHeight>4.5f,"Flight collected during a jump raises the actual capsule smoothly above obstacles");
            Check(game.Animation.CurrentClip=="Flight","Flight selects the dedicated airborne animation instead of a running or jumping pose");
            Vector3 flightViewport=Camera.main.WorldToViewportPoint(game.Motor.transform.position+Vector3.up*1.05f);
            Check(flightViewport.z>0&&flightViewport.x>.1f&&flightViewport.x<.9f&&flightViewport.y>.1f&&flightViewport.y<.85f,"Camera keeps the flying character centre within the readable playfield");
            Check(!game.Motor.TryJump()&&!game.Motor.TrySlide()&&!game.Motor.FastFalling,"Flight cannot be corrupted by jump or downward-swipe input");
            game.Tokens.PlaceForTest(0,new Vector3(game.Motor.transform.position.x,1.12f,game.Motor.transform.position.z+24));yield return Wait(.7f);
            Check(game.Tokens.PositionForTest(0).y>5.3f,"Nearby reward rails rise into the actual flight path");
            yield return Capture("v7-flight");
            float timeout=Time.realtimeSinceStartup+8;
            while((powers.FlightRemaining>0||game.Motor.Flying)&&Time.realtimeSinceStartup<timeout)yield return Wait(.04f);
            yield return null;
            Check(game.Motor.Grounded&&!game.Motor.Flying&&powers.LandingGraceRemaining>0,"Flight returns to ground with a landing protection window");
            game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+.4f,game.Motor.Lane);yield return Wait(.2f);
            Check(game.State==RunState.Running,"Landing grace prevents a collision at the flight exit");
            yield return Wait(powers.LandingGraceRemaining+.15f);
            Check(!powers.IsProtected,"Landing protection expires after the safe handover");

            BeginIsolatedRun(true);PlacePower(PowerupKind.Magnet);yield return Wait(.1f);
            int beforeCoins=game.Coins;Vector3 coinStart=new Vector3(game.Settings.laneSpacing,1.12f,game.Motor.transform.position.z+10);
            game.Tokens.PlaceForTest(0,coinStart);yield return Wait(.1f);
            Check(powers.MagnetActive&&Mathf.Abs(game.Tokens.PositionForTest(0).x)<Mathf.Abs(coinStart.x),"Magnet physically pulls a neighbouring-lane coin toward the player");
            yield return Wait(.35f);Check(game.Coins>beforeCoins,"Attracted coins reach the actual collection volume");
            yield return Capture("v7-magnet");

            BeginIsolatedRun(false);PlacePower(PowerupKind.Shield);yield return Wait(.1f);
            Check(powers.ShieldActive&&powers.ShieldRemaining<=8,"Shield pickup has a finite eight-second charge");
            yield return Capture("v7-shield");
            game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+.6f,game.Motor.Lane);yield return Wait(.22f);
            Check(game.State==RunState.Running&&!powers.ShieldActive,"One actual collision consumes the shield and preserves the run");
            yield return Wait(1.3f);game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+.6f,game.Motor.Lane);yield return Wait(.15f);
            Check(game.State==RunState.Finished,"A second collision after shield grace ends the run");
            BeginIsolatedRun(false);powers.Activate(PowerupKind.Shield);yield return Wait(8.1f);
            Check(!powers.ShieldActive,"An unused shield expires instead of granting permanent protection");
            powers.Activate(PowerupKind.Dash);powers.Activate(PowerupKind.Flight);powers.Activate(PowerupKind.Magnet);powers.Activate(PowerupKind.Shield);
            game.Restart();game.Obstacles.SetActive(false);
            Check(powers.DashRemaining==0&&powers.FlightRemaining==0&&powers.MagnetRemaining==0&&!powers.ShieldActive&&!game.Motor.Flying&&!powers.IsProtected,"Restart clears every power, flight state and grace timer");
            Vector3 position=powers.PositionForTest(PowerupKind.Magnet);powers.ShiftOrigin(1000);
            Check(Vector3.Distance(powers.PositionForTest(PowerupKind.Magnet),position-Vector3.forward*1000)<.001f,"Powerup pool shifts with the floating world origin");powers.ShiftOrigin(-1000);
            report.finalObjects=Count();Check(report.powerPool==RunnerPowerups.TypeCount&&report.initialObjects==report.finalObjects,"All pickup types and their effect rigs remain a fixed object pool through resets");

            game.ReturnToMenu();string economyPrefix=prefix+"Economy.";isolatedWallets.Add(game.UseWalletForTest(economyPrefix));
            Check(game.WalletBalance==0,"Economy test uses a separate empty wallet");
            BeginIsolatedRun(true);powers.Enabled=false;
            for(int i=0;i<20;i++){game.Tokens.PlaceForTest(i,new Vector3(0,1.12f,game.Motor.transform.position.z+.4f));yield return null;}
            yield return null;int earned=game.Coins;report.walletEarned=earned;game.Restart();game.Obstacles.SetActive(false);game.Tokens.Enabled=false;
            Check(earned>=30&&game.WalletBalance==earned&&game.Coins==0,"Restart settles the just-earned run coins exactly once");
            game.ReturnToMenu();game.ReturnToMenu();
            Check(game.WalletBalance==earned&&new RunWallet(economyPrefix).Balance==earned,"Repeated menu resets cannot double-credit coins and balance persists");
            Check(game.TryPurchaseUpgrade(PowerupKind.Dash)&&game.WalletBalance==earned-30&&game.UpgradeLevel(PowerupKind.Dash)==1,"Buying the first dash tier deducts exactly thirty earned coins");
            Check(game.PowerupDuration(PowerupKind.Dash)==6&&game.UpgradeCost(PowerupKind.Dash)==75,"Purchased tier adds one second and advances the next price");
            int balance=game.WalletBalance;
            Check(!game.TryPurchaseUpgrade(PowerupKind.Dash)&&game.WalletBalance==balance&&!game.TryPurchaseUpgrade(PowerupKind.Shield),"Insufficient balance and unsupported shield upgrades cannot spend coins");
            BeginIsolatedRun(true);powers.Enabled=false;int frames=0;
            while(game.Coins<740&&frames<500)
            {game.Tokens.PlaceForTest(frames%48,new Vector3(0,1.12f,game.Motor.transform.position.z+.4f));frames++;yield return null;}
            game.ReturnToMenu();int funded=game.WalletBalance;
            bool purchases=game.TryPurchaseUpgrade(PowerupKind.Dash)&&game.TryPurchaseUpgrade(PowerupKind.Dash);
            for(int tier=0;tier<3;tier++){purchases&=game.TryPurchaseUpgrade(PowerupKind.Flight);purchases&=game.TryPurchaseUpgrade(PowerupKind.Magnet);}
            Check(purchases&&game.WalletBalance==funded-735,"All remaining tiers are purchased only with coins earned in the run");
            balance=game.WalletBalance;
            Check(game.UpgradeLevel(PowerupKind.Dash)==3&&game.UpgradeLevel(PowerupKind.Flight)==3&&game.UpgradeLevel(PowerupKind.Magnet)==3&&game.UpgradeCost(PowerupKind.Dash)==0&&!game.TryPurchaseUpgrade(PowerupKind.Dash)&&game.WalletBalance==balance,"Upgrade levels stop at three and max-tier clicks cannot deduct currency");
            var restored=new RunWallet(economyPrefix);
            Check(restored.Level(PowerupKind.Dash)==3&&restored.Level(PowerupKind.Flight)==3&&restored.Level(PowerupKind.Magnet)==3&&restored.Balance==balance,"All purchased levels and remaining wallet reload from persistence");
            BeginIsolatedRun(false);powers.Enabled=true;powers.Activate(PowerupKind.Flight);
            Check(powers.FlightRemaining==8,"An earned flight upgrade changes the actual collected-power duration");
            game.ReturnToMenu();
            report.voiceCalls=sound.VoicePlayCount;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.checks=checks.ToArray();report.errors=errors.ToArray();report.passed=errors.Count==0;
            File.WriteAllText("../Validation/unity_powerups_v7.json",JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Powerup v6 checks "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void BeginIsolatedRun(bool coins)
        {game.Restart();game.Obstacles.SetActive(false);game.Tokens.Enabled=coins;powers.Enabled=true;powers.IsolatePickupsForTest();}
        void PlacePower(PowerupKind kind)
        {powers.PlaceForTest(kind,game.Motor.transform.position+new Vector3(0,1.1f,.55f));}
        static int Count()=>FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
        static WaitForSecondsRealtime Wait(float seconds)=>new WaitForSecondsRealtime(seconds);
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.GetFullPath("../Validation/"+name+".png"));yield return null;}
        void Check(bool condition,string label){if(condition)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception)errors.Add(message);}
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;Application.runInBackground=background;
            PlayerPrefs.SetFloat("FenerRunner.Best",oldBest);PlayerPrefs.SetInt("FenerRunner.BestScore",oldScore);
            foreach(var wallet in isolatedWallets)wallet.DeleteIsolatedTestData();PlayerPrefs.Save();
        }
    }
}
#endif
