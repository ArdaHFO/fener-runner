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
    public sealed class CoinChecksV7 : MonoBehaviour
    {
        const string SessionKey="FenerRunner.CoinChecksV7";
        static readonly FieldInfo SettingsField=typeof(RunnerGame).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic);
        [Serializable] sealed class Report
        {
            public bool passed,sourceSettingsUnchanged;
            public string utc,unityVersion;
            public string[] checks,errors;
            public float wallSeconds,pickupRadius,frameSeconds=1f/15f;
            public int normalPool,bonusPool,actualGameCoins;
            public string scope="Real Unity controller and coin meshes, continuous relative pickup paths, pooled normal/rain/magnet coins, and actual Game coin credits at deterministic15Hz. Separate6m controller moves exercise tunneling. Runtime settings clone and AutomatedTest preserve source settings, production records and wallet. This is Editor evidence, not physical-device frame-rate evidence.";
        }
        RunnerGame game;RunnerSettings originalSettings,testSettings;string originalSettingsJson;
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        readonly Report report=new Report();
        bool prepared,previousBackground,hadBest,hadScore;
        float began,previousCaptureDelta,previousBest;int previousTargetRate,previousScore;

        [MenuItem("Fener Runner/24 - Verify continuous coin collection")]
        public static void StartChecks()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            SessionState.SetBool(SessionKey,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Register()
        {EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(SessionKey,false))return;
            SessionState.SetBool(SessionKey,false);new GameObject("Continuous coin checks v0.7").AddComponent<CoinChecksV7>();
        }
        IEnumerator Start()
        {
            began=Time.realtimeSinceStartup;game=FindFirstObjectByType<RunnerGame>();
            previousBackground=Application.runInBackground;previousTargetRate=Application.targetFrameRate;previousCaptureDelta=Time.captureDeltaTime;
            hadBest=PlayerPrefs.HasKey("FenerRunner.Best");hadScore=PlayerPrefs.HasKey("FenerRunner.BestScore");
            previousBest=PlayerPrefs.GetFloat("FenerRunner.Best",0);previousScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);
            Application.runInBackground=true;Application.targetFrameRate=15;Time.captureDeltaTime=1f/15f;
            prepared=true;Application.logMessageReceived+=Log;
            var routine=RunScenarios();
            while(true)
            {
                bool more;object wait=null;
                try{more=routine.MoveNext();if(more)wait=routine.Current;}
                catch(Exception exception){errors.Add(exception.ToString());break;}
                if(!more)break;yield return wait;
            }
            if(originalSettings!=null)
            {report.sourceSettingsUnchanged=JsonUtility.ToJson(originalSettings)==originalSettingsJson;Check(report.sourceSettingsUnchanged,"Original RunnerSettings remain unchanged");}
            report.checks=checks.ToArray();report.errors=errors.ToArray();report.passed=errors.Count==0;
            report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.wallSeconds=Time.realtimeSinceStartup-began;
            Directory.CreateDirectory("../Validation");File.WriteAllText(ReviewEvidence.File("../Validation/unity_coins_v7.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Coin v7 checks "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));
            EditorApplication.isPlaying=false;
        }
        void ResetCase(bool live=false,float speed=30,int lane=0)
        {
            game.enabled=false;testSettings.forwardSpeed=testSettings.maxSpeed=speed;
            game.Powerups.Enabled=false;game.Tokens.Enabled=true;game.Play();game.Obstacles.SetActive(false);
            game.Powerups.IsolatePickupsForTest();
            if(lane!=0){game.Motor.Speed=0;game.Motor.ChangeLane(lane);game.Motor.Tick(game.Settings.laneChangeSeconds);}
            game.Motor.Speed=speed;
            for(int i=0;i<game.Tokens.PoolCount;i++)game.Tokens.PlaceForTest(i,new Vector3(12,1.12f,2000+i*4.4f));
            game.enabled=live;
        }
        int Move(float dt)
        {game.Tokens.BeginMove(game.Motor);game.Motor.Tick(dt);return game.Tokens.Tick(game.Motor,game.Obstacles,dt);}
        void Place(int index,float x,float y,float forward)
        {game.Tokens.PlaceForTest(index,new Vector3(x,y,game.Motor.transform.position.z+forward));}

        IEnumerator RunScenarios()
        {
            if(game==null||game.Powerups==null||SettingsField==null)throw new InvalidOperationException("A built v0.7 scene is required.");
            game.AutomatedTest=true;originalSettings=game.Settings;originalSettingsJson=JsonUtility.ToJson(originalSettings);
            testSettings=Instantiate(originalSettings);testSettings.name="Coin test settings (runtime clone)";
            SettingsField.SetValue(game,testSettings);game.Motor.Configure(testSettings);game.Obstacles.UseSeedForTest(1907);
            ResetCase();report.normalPool=game.Tokens.PoolCount;report.bonusPool=game.Tokens.BonusPoolCount;report.pickupRadius=game.Tokens.PickupRadius;
            Check(report.normalPool==48&&report.bonusPool==20,"Normal and rain coins use fixed48+20 pools");
            Check(report.pickupRadius>.34f&&report.pickupRadius<.45f,"Pickup forgiveness follows the visible coin mesh radius");
            foreach(int lane in new[]{-1,0,1})
                foreach(float height in new[]{.25f,1.12f,2.15f})
                {
                    ResetCase(false,30,lane);Place(0,lane*game.Settings.laneSpacing,height,3);
                    int collected=Move(.2f);
                    Check(collected==1&&game.Tokens.CollectedForTest(0),"A6m move collects lane"+lane+" coin at height"+height);
                    Check(game.Tokens.Tick(game.Motor,game.Obstacles,0)==0&&game.Tokens.TotalCollected==1,
                        "The same lane"+lane+" height"+height+" coin cannot be credited twice");
                }
            ResetCase();
            Place(0,game.Settings.laneSpacing,1.12f,3);Place(1,-game.Settings.laneSpacing,1.12f,3);
            Place(2,0,4,3);Place(3,0,-1,3);Place(4,0,1.12f,20);
            Check(Move(.2f)==0&&game.Tokens.TotalCollected==0,"Normal pickup excludes adjacent lanes, far heights and unreached forward coins");

            ResetCase();game.Motor.TrySlide();game.Motor.Speed=0;game.Motor.Tick(.2f);game.Motor.Speed=30;
            Place(0,0,1.45f,3);Place(1,0,2.8f,3);
            Check(Move(.2f)==1&&game.Tokens.CollectedForTest(0)&&!game.Tokens.CollectedForTest(1),
                "A sliding capsule collects its visible head-height coin and rejects the high coin");

            ResetCase();game.Motor.Speed=0;game.Motor.TryJump();
            for(int i=0;i<18;i++)game.Motor.Tick(1f/60f);
            game.Motor.Speed=30;Place(0,0,3.57f,3);Place(1,0,-1,3);
            Check(Move(.2f)==1&&game.Tokens.CollectedForTest(0),"A real jumping capsule catches an elevated coin during a6m crossing");

            foreach(int side in new[]{-1,1})
            {
                ResetCase(false,30,side);float x=side*game.Settings.laneSpacing*.5f;
                Place(0,x,1.12f,2.1f);game.Motor.ChangeLane(-side);
                Check(Move(.14f)==1&&game.Tokens.CollectedForTest(0),"Diagonal movement from lane"+side+" collects a coin crossed between endpoints");
            }

            ResetCase();game.Motor.Speed=0;game.Motor.SetFlight(true);
            for(int i=0;i<25;i++)game.Motor.Tick(.05f);
            game.Motor.Speed=30;Place(0,0,5.92f,3);Place(1,0,1.12f,3);
            Check(Move(.2f)==1&&game.Tokens.CollectedForTest(0)&&!game.Tokens.CollectedForTest(1),
                "Flight collects the elevated rail without claiming distant ground coins");

            ResetCase();game.Tokens.MagnetRadius=10;Place(0,game.Settings.laneSpacing,1.12f,3);
            Check(Move(.2f)==1&&game.Tokens.CollectedForTest(0),"Magnet and player motion use one relative continuous crossing");
            Check(game.Tokens.Tick(game.Motor,game.Obstacles,.2f)==0,"A magnet-collected coin is never counted again");

            ResetCase();game.Tokens.PlaceBonusForTest(0,new Vector3(0,3,game.Motor.transform.position.z+3),1.12f);
            Check(Move(.2f)==1&&game.Tokens.BonusCollected==1&&!game.Tokens.BonusActiveForTest(0),
                "A falling rain coin and6m player move produce one real collection");
            Check(game.Tokens.Tick(game.Motor,game.Obstacles,.2f)==0&&game.Tokens.BonusCollected==1,
                "A collected rain coin disappears and cannot pay twice");

            ResetCase();game.Motor.Speed=0;
            game.Tokens.PlaceBonusForTest(0,new Vector3(0,4,game.Motor.transform.position.z),-1);
            Check(Move(.5f)==1&&game.Tokens.BonusCollected==1,
                "A fast falling coin crossing the whole stationary body is caught between its endpoints");

            ResetCase();Place(0,0,1.12f,3);game.Tokens.BeginMove(game.Motor);
            game.Course.ShiftOrigin(1000);game.Obstacles.ShiftOrigin(1000);game.Tokens.ShiftOrigin(1000);game.Motor.ShiftOrigin(1000);
            game.Motor.Tick(.2f);
            Check(game.Tokens.Tick(game.Motor,game.Obstacles,.2f)==1,"Origin shifting preserves both player and coin path history");
            ResetCase();Place(0,0,1.12f,3);
            Check(game.Tokens.Tick(game.Motor,game.Obstacles,0)==0,"Restart cannot collect along stale pre-reset travel");
            game.Tokens.BeginMove(game.Motor);game.Motor.Tick(.2f);game.Tokens.Enabled=false;
            Check(game.Tokens.Tick(game.Motor,game.Obstacles,.2f)==0,"Disabled rewards do not collect a queued movement");
            game.Tokens.Enabled=true;
            Check(game.Tokens.Tick(game.Motor,game.Obstacles,0)==0,"Re-enabling rewards does not replay the disabled movement");

            ResetCase();game.Tokens.LaneSweepActive=true;
            for(int lane=-1;lane<=1;lane++)Place(lane+1,lane*game.Settings.laneSpacing,1.12f,3);
            Place(3,0,4,3);Place(4,0,1.12f,20);Place(5,7,1.12f,3);
            Check(Move(.2f)==3&&game.Tokens.TotalCollected==3,"LaneSweep collects all three actual lane coins at the same forward crossing");
            Check(!game.Tokens.CollectedForTest(3)&&!game.Tokens.CollectedForTest(4)&&!game.Tokens.CollectedForTest(5),
                "LaneSweep keeps height, forward-distance and track-edge restrictions");
            game.Tokens.LaneSweepActive=false;Place(6,game.Settings.laneSpacing,1.12f,3);
            Check(Move(.2f)==0&&!game.Tokens.CollectedForTest(6),"Normal adjacent-lane isolation resumes after LaneSweep expires");

            // Count simulation frames, not wall-time, so an Editor modal cannot turn
            // a scenario that has not reached its coins into a false physics failure.
            foreach(int lane in new[]{-1,0,1})
            {
                ResetCase(true,30,lane);float startZ=game.Motor.transform.position.z;
                for(int i=0;i<5;i++)Place(i,lane*game.Settings.laneSpacing,1.12f,2+i*2);
                int frames=0;while(game.State==RunState.Running&&game.Motor.transform.position.z<startZ+14&&frames++<30)yield return null;
                Check(game.State==RunState.Running&&game.Tokens.TotalCollected==5&&game.Coins==5,
                    "Real Game at30m/s and15Hz credits exactly five coins in lane"+lane);
                report.actualGameCoins+=game.Coins;
            }
            foreach(float speed in new[]{18f,30f})
            {
                ResetCase(false,speed);game.Motor.Tick((54-game.Motor.transform.position.z)/speed);
                float[] offsets={-6,-2,2,6};
                for(int i=0;i<offsets.Length;i++)game.Tokens.PlaceForTest(i,game.Obstacles.RewardPositionAt(70+offsets[i]));
                game.enabled=true;bool jumped=false;int frames=0;
                while(game.State==RunState.Running&&game.Motor.transform.position.z<82&&frames++<60)
                {
                    if(!jumped&&70-game.Motor.transform.position.z<=speed*.4f)
                    {game.HandleCommand(RunnerCommand.Jump);jumped=true;}
                    yield return null;
                }
                Check(jumped&&game.State==RunState.Running&&game.Tokens.TotalCollected==4&&game.Coins==4,
                    "Actual revised jump collects all four safe-route arc coins at"+speed+"m/s");
                report.actualGameCoins+=game.Coins;
            }
            ResetCase();Check(game.Tokens.TotalCollected==0&&game.Tokens.BonusCollected==0&&!game.Tokens.LaneSweepActive,
                "New runs clear counters, movement samples and lane-sweep effects");
            Check(game.Tokens.PoolCount==report.normalPool&&game.Tokens.BonusPoolCount==report.bonusPool,
                "Every pickup mode retains the original fixed pools");
        }
        void Check(bool condition,string label){if(condition)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type)
        {if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<40)errors.Add(message);}
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;Time.timeScale=1;
            if(!prepared)return;
            Application.runInBackground=previousBackground;Application.targetFrameRate=previousTargetRate;Time.captureDeltaTime=previousCaptureDelta;
            if(game!=null&&originalSettings!=null){SettingsField.SetValue(game,originalSettings);game.Motor.Configure(originalSettings);game.enabled=true;}
            if(testSettings!=null)Destroy(testSettings);
            if(hadBest)PlayerPrefs.SetFloat("FenerRunner.Best",previousBest);else PlayerPrefs.DeleteKey("FenerRunner.Best");
            if(hadScore)PlayerPrefs.SetInt("FenerRunner.BestScore",previousScore);else PlayerPrefs.DeleteKey("FenerRunner.BestScore");
            PlayerPrefs.Save();
        }
    }
}
#endif
