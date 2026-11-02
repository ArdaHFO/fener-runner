#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class AbilityChecksV6 : MonoBehaviour
    {
        [Serializable] class Report
        {
            public string utc,unityVersion;
            public bool passed;
            public string[] checks,errors;
            public int initialObjects,finalObjects,powerPool,bonusPool,bonusSpawned,bonusCollected;
            public int pickupVoiceRequests;
            public float elapsedSeconds,normalJumpApex,superJumpApex,superJumpCentreViewportY,superJumpHeadViewportY,focusTravelSpeed;
            public string scope="Unity Editor Play Mode. Eight actual proximity pickups, gesture-driven controls, physical jump trajectories, real coin multiplication and falling pooled bonus tokens, timer pause/reset, HUD bounds and flight-exit clarity. Isolated temporary wallet; not physical-device or listening validation.";
        }
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        readonly PowerupKind[] kinds={PowerupKind.Dash,PowerupKind.Flight,PowerupKind.Magnet,PowerupKind.Shield,PowerupKind.DoubleCoins,PowerupKind.SlowMotion,PowerupKind.SuperJump,PowerupKind.CoinRain};
        Report report=new Report();RunnerGame game;RunnerPowerups powers;RunnerInput input;StadiumSound sound;RunWallet testWallet;
        bool background;int realBalance;readonly int[] realLevels=new int[3];float started;

        [MenuItem("Fener Runner/20 - Verify eight abilities and reward effects")]
        public static void StartChecks(){SessionState.SetBool("FenerRunner.AbilityChecksV6",true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("FenerRunner.AbilityChecksV6",false))return;
            SessionState.SetBool("FenerRunner.AbilityChecksV6",false);new GameObject("Eight ability checks v6").AddComponent<AbilityChecksV6>();
        }
        IEnumerator Start()
        {
            started=Time.realtimeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;Application.logMessageReceived+=Log;
            game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;powers=game.Powerups;input=FindFirstObjectByType<RunnerInput>();sound=game.GetComponent<StadiumSound>();
            realBalance=game.WalletBalance;for(int i=0;i<3;i++)realLevels[i]=game.UpgradeLevel((PowerupKind)i);
            testWallet=game.UseWalletForTest("FenerRunner.Test.AbilitiesV6."+DateTime.UtcNow.Ticks+".");
            yield return Wait(.3f);
            report.initialObjects=Count();report.powerPool=powers.PoolCount;report.bonusPool=game.Tokens.BonusPoolCount;
            Check(report.powerPool==RunnerPowerups.TypeCount&&report.bonusPool==20,"Exactly twelve pickup objects and twenty bonus coins are preallocated");
            Check(sound.HasUserVoices,"The supplied power voice collection remains assigned");

            game.Obstacles.UseSeedForTest(1907);game.Restart();
            var allKinds=(PowerupKind[])Enum.GetValues(typeof(PowerupKind));var firstPositions=new float[allKinds.Length];var sorted=new float[allKinds.Length];
            for(int i=0;i<allKinds.Length;i++)firstPositions[i]=sorted[i]=powers.PositionForTest(allKinds[i]).z;
            Array.Sort(sorted);bool gaps=Mathf.Abs(sorted[0]-72)<.01f;
            for(int i=1;i<sorted.Length;i++)gaps&=sorted[i]-sorted[i-1]>=104.99f&&sorted[i]-sorted[i-1]<=135.01f;
            Check(gaps,"Pickup placement begins at72 metres and uses105–135 metre intervals");
            game.Restart();bool reproducible=true;
            for(int i=0;i<allKinds.Length;i++)reproducible&=Mathf.Abs(powers.PositionForTest(allKinds[i]).z-firstPositions[i])<.01f;
            Check(reproducible,"The same route seed reproduces the eight-type pickup order");
            game.Obstacles.UseSeedForTest(1908);game.Restart();bool different=false;
            for(int i=0;i<allKinds.Length;i++)different|=Mathf.Abs(powers.PositionForTest(allKinds[i]).z-firstPositions[i])>.01f;
            Check(different,"A different route seed changes which ability arrives first");

            int requestStart=sound.PowerVoiceRequestCount;
            for(int i=0;i<kinds.Length;i++)
            {
                Begin(false);Check(AllTimersClear(),"Restart clears every timer before "+kinds[i]);
                int requests=sound.PowerVoiceRequestCount;PlacePower(kinds[i]);yield return Wait(.14f);
                Check(powers.IsActive(kinds[i])&&powers.ActiveKind==kinds[i]&&powers.Remaining(kinds[i])>game.PowerupDuration(kinds[i])-.5f,"Actual pickup activates "+kinds[i]);
                Check(sound.PowerVoiceRequestCount==requests+1,"Exactly one voice request for collected "+kinds[i]);
                Check(game.Interface.powerPanel.activeSelf&&InScreen((RectTransform)game.Interface.powerPanel.transform)&&InScreen(game.Interface.powerStatus.rectTransform),"Power HUD remains inside the phone for "+kinds[i]);
                if(kinds[i]==PowerupKind.Dash)
                {
                    var halo=game.Motor.transform.Find("Explicit protection halo");
                    Check(!powers.IsProtected&&powers.ProtectionLabel.Length==0&&game.Interface.protectionStatus.text.Contains("ÇARPIŞMA")&&halo!=null&&!halo.gameObject.activeSelf,"Dash explicitly reports lethal collisions and displays no protective halo");
                }
                game.Pause();float remaining=powers.Remaining(kinds[i]);Vector3 position=game.Motor.transform.position;
                yield return Wait(.14f);
                Check(powers.Remaining(kinds[i])==remaining&&game.Motor.transform.position==position,"Pause freezes the timer and movement for "+kinds[i]);game.Resume();
            }
            report.pickupVoiceRequests=sound.PowerVoiceRequestCount-requestStart;

            Begin(true);PlacePower(PowerupKind.DoubleCoins);yield return Wait(.12f);
            int before=game.Coins;PlaceCoin(0);yield return null;yield return null;
            Check(game.Coins==before+2&&game.CoinValue==2,"A physically collected coin pays two during DoubleCoins");
            for(int i=1;i<10;i++){PlaceCoin(i);yield return null;}
            yield return null;Check(game.FeverActive&&game.CoinValue==4,"DoubleCoins stacks with earned stadium fever to four coins");
            before=game.Coins;PlaceCoin(10);yield return null;yield return null;
            Check(game.Coins==before+4,"The actual collection volume awards four coins during the combined bonus");
            yield return Capture("v7-double-coins");

            Begin(false);PlacePower(PowerupKind.SlowMotion);yield return Wait(.12f);
            double distance=game.Distance;float time=Time.time;yield return Wait(.5f);
            report.focusTravelSpeed=(float)(game.Distance-distance)/(Time.time-time);
            float expected=game.SpeedForDistance((game.Distance+distance)*.5)*.68f;
            Check(Mathf.Abs(report.focusTravelSpeed-expected)<.3f&&Time.timeScale==1&&game.CurrentSpeed<14,"Focus reduces actual forward travel by32 percent without slowing the global clock");
            Swipe(Vector2.right*150);yield return Wait(.18f);
            Check(game.Motor.Lane==1&&Mathf.Abs(game.Motor.transform.position.x-game.Settings.laneSpacing)<.035f,"A swipe still completes a lane change promptly during focus");
            yield return Capture("v7-focus");

            Begin(false);Swipe(Vector2.up*150);float timeout=Time.realtimeSinceStartup+2;
            while(!game.Motor.Grounded&&Time.realtimeSinceStartup<timeout)
            {report.normalJumpApex=Mathf.Max(report.normalJumpApex,game.Motor.transform.position.y);yield return null;}
            Check(Mathf.Abs(report.normalJumpApex-game.Settings.jumpHeight)<.08f,"Baseline jump reaches the configured2.65 metre apex");
            Begin(false);PlacePower(PowerupKind.SuperJump);yield return Wait(.12f);Swipe(Vector2.up*150);timeout=Time.realtimeSinceStartup+2.5f;bool captured=false;
            while(!game.Motor.Grounded&&Time.realtimeSinceStartup<timeout)
            {
                report.superJumpApex=Mathf.Max(report.superJumpApex,game.Motor.transform.position.y);
                if(!captured&&game.Motor.VerticalVelocity<=.8f&&game.Motor.transform.position.y>3.8f)
                {
                    captured=true;yield return new WaitForEndOfFrame();
                    Vector3 centre=Camera.main.WorldToViewportPoint(game.Motor.transform.position+Vector3.up*1.05f);
                    Vector3 head=Camera.main.WorldToViewportPoint(game.Motor.transform.position+Vector3.up*2.04f);
                    report.superJumpCentreViewportY=centre.y;report.superJumpHeadViewportY=head.y;
                    Check(centre.z>0&&centre.x>.1f&&centre.x<.9f&&centre.y>.1f&&centre.y<.85f&&head.y<.98f,"Powered jump keeps the bird centre and head visible at the apex");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("../Validation/v7-superjump.png"));
                }
                yield return null;
            }
            Check(captured&&Mathf.Abs(report.superJumpApex-game.Settings.jumpHeight*1.5f)<.1f&&report.superJumpApex>report.normalJumpApex+1.3f,"SuperJump raises the actual capsule to approximately3.975 metres and lands normally");

            Begin(true);
            // Keep ordinary rails outside this short scenario so bonus counters reflect falling coins alone.
            for(int i=0;i<48;i++)game.Tokens.PlaceForTest(i,new Vector3(9,1.12f,game.Motor.transform.position.z+1000+i*4));
            PlacePower(PowerupKind.CoinRain);yield return Wait(.13f);
            Check(game.Tokens.BonusSpawned==0&&game.Coins==0,"CoinRain does not credit invisible currency immediately");
            yield return Wait(1.02f);
            Check(game.Tokens.BonusSpawned==5&&VisibleBonusCount()>0,"First coin-rain volley creates five visible collectible coins");
            game.Pause();int spawned=game.Tokens.BonusSpawned;Transform sample=FirstVisibleBonus();Vector3 bonusPosition=sample==null?Vector3.zero:sample.position;
            yield return Wait(.18f);
            Check(game.Tokens.BonusSpawned==spawned&&sample!=null&&sample.position==bonusPosition,"Pause freezes both rain volleys and falling bonus geometry");game.Resume();
            yield return Capture("v7-coin-rain");yield return Wait(1.5f);
            float elapsed=game.PowerupDuration(PowerupKind.CoinRain)-powers.CoinRainRemaining;
            int expectedVolleys=Mathf.FloorToInt(elapsed+.001f);
            report.bonusSpawned=game.Tokens.BonusSpawned;report.bonusCollected=game.Tokens.BonusCollected;
            Check(report.bonusSpawned==expectedVolleys*5&&report.bonusCollected>0&&game.Coins>0,"CoinRain emits five per active second and its real coins are collected");
            sample=FirstVisibleBonus();
            if(sample!=null)
            {
                bonusPosition=sample.position;game.Tokens.ShiftOrigin(1000);
                Check(Vector3.Distance(sample.position,bonusPosition-Vector3.forward*1000)<.001f,"Active bonus coins shift with the floating origin");game.Tokens.ShiftOrigin(-1000);
            }
            else Check(false,"Active bonus coin available for origin-shift verification");
            Begin(false);Check(game.Tokens.BonusSpawned==0&&game.Tokens.BonusCollected==0&&VisibleBonusCount()==0,"Restart resets bonus counters and hides the entire bonus pool");

            game.Obstacles.SetActive(true);game.Tokens.Enabled=true;game.Pause();
            game.Motor.Capsule.enabled=false;game.Motor.transform.position=new Vector3(0,.02f,60);game.Motor.Capsule.enabled=true;
            Physics.SyncTransforms();game.Tokens.SpawnBonusVolley(game.Motor,5,game.Obstacles);
            Vector3 bonusTarget=game.Tokens.BonusTargetForTest(0);RunnerObstacle rowBlack=null;
            foreach(var obstacle in game.Obstacles.Items)
                if(obstacle.gameObject.activeInHierarchy&&obstacle.kind==ObstacleKind.JumpOver&&Mathf.Abs(obstacle.transform.position.z-70)<.01f&&Mathf.Abs(obstacle.transform.position.x-bonusTarget.x)<.1f){rowBlack=obstacle;break;}
            bool clearRainTarget=false;
            if(rowBlack!=null)
            {
                Collider blackCollider=rowBlack.GetComponentInChildren<Collider>();
                clearRainTarget=blackCollider!=null&&blackCollider.enabled&&Mathf.Abs(bonusTarget.z-70)<.01f&&bonusTarget.y-.2f>blackCollider.bounds.max.y&&
                    !Physics.CheckSphere(bonusTarget,.2f,1<<8,QueryTriggerInteraction.Collide);
            }
            Check(clearRainTarget,"Rain rewards over the active black row settle above its real collider instead of baiting a collision");
            Begin(false);Check(game.Tokens.BonusSpawned==0&&game.Tokens.BonusCollected==0,"The obstacle-aware rain scenario resets its counters before the next run");

            PlacePower(PowerupKind.Flight);yield return Wait(.9f);
            Check(game.Motor.Flying&&!powers.IsProtected,"Flight height clears obstacles without blanket airborne immunity");
            timeout=Time.realtimeSinceStartup+7;
            while(powers.FlightRemaining>0&&Time.realtimeSinceStartup<timeout)yield return Wait(.04f);
            yield return Wait(.15f);
            Check(!powers.HasActive&&powers.IsProtected&&powers.ProtectionLabel=="GÜVENLİ İNİŞ"&&game.Interface.powerPanel.activeSelf&&game.Interface.protectionStatus.text=="GÜVENLİ İNİŞ","The HUD keeps explicit landing protection visible after the last power timer expires");
            while(game.Motor.Flying&&Time.realtimeSinceStartup<timeout)yield return Wait(.04f);
            yield return Wait(.15f);
            Check(game.Motor.Grounded&&powers.LandingGraceRemaining>0&&game.Interface.powerPanel.activeSelf,"Landing grace remains visible during the handover to normal ground play");
            yield return Capture("v7-landing-grace");
            yield return Wait(powers.LandingGraceRemaining+.15f);
            Check(!powers.IsProtected&&powers.ProtectionLabel.Length==0&&!game.Interface.powerPanel.activeSelf,"Landing protection and its HUD disappear when grace ends");

            game.ReturnToMenu();report.finalObjects=Count();
            Check(report.initialObjects==report.finalObjects&&powers.PoolCount==RunnerPowerups.TypeCount&&game.Tokens.BonusPoolCount==20,"All ability resets, effects and bonus rain retain fixed object pools");
            var realWallet=new RunWallet();bool walletUnchanged=realWallet.Balance==realBalance;
            for(int i=0;i<3;i++)walletUnchanged&=realWallet.Level((PowerupKind)i)==realLevels[i];
            Check(walletUnchanged,"Ability tests preserve the user's earned wallet and purchased upgrade levels");
            report.elapsedSeconds=Time.realtimeSinceStartup-started;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.checks=checks.ToArray();report.errors=errors.ToArray();report.passed=errors.Count==0;
            File.WriteAllText("../Validation/unity_abilities_v7.json",JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Ability v6 checks "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void Begin(bool coins)
        {game.Restart();game.Obstacles.SetActive(false);game.Tokens.Enabled=coins;powers.Enabled=true;powers.IsolatePickupsForTest();}
        void PlacePower(PowerupKind kind){powers.PlaceForTest(kind,game.Motor.transform.position+new Vector3(0,1.1f,.55f));}
        void PlaceCoin(int index){game.Tokens.PlaceForTest(index,game.Motor.transform.position+new Vector3(0,1.12f,.4f));}
        void Swipe(Vector2 offset){Vector2 p=new Vector2(Screen.width*.5f,Screen.height*.5f);input.TestBegin(p);input.TestMove(p+offset);input.TestEnd(p+offset);}
        bool AllTimersClear(){foreach(var kind in kinds)if(powers.Remaining(kind)!=0)return false;return !game.Motor.Flying&&!powers.IsProtected&&powers.HitGraceRemaining==0;}
        int VisibleBonusCount(){int count=0;for(int i=0;i<20;i++){var t=game.Tokens.transform.Find("Rain coin "+i);if(t!=null&&Visible(t))count++;}return count;}
        Transform FirstVisibleBonus(){for(int i=0;i<20;i++){var t=game.Tokens.transform.Find("Rain coin "+i);if(t!=null&&Visible(t))return t;}return null;}
        static bool Visible(Transform t){foreach(var r in t.GetComponentsInChildren<Renderer>(true))if(r.enabled)return true;return false;}
        static bool InScreen(RectTransform rect){var corners=new Vector3[4];rect.GetWorldCorners(corners);foreach(var p in corners)if(p.x<-.5f||p.y<-.5f||p.x>Screen.width+.5f||p.y>Screen.height+.5f)return false;return true;}
        static int Count()=>FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
        static WaitForSecondsRealtime Wait(float seconds)=>new WaitForSecondsRealtime(seconds);
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.GetFullPath("../Validation/"+name+".png"));yield return null;}
        void Check(bool condition,string label){if(condition)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;Application.runInBackground=background;if(testWallet!=null)testWallet.DeleteIsolatedTestData();}
    }
}
#endif
