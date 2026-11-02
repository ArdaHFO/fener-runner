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
    public sealed class CollectibleChecksV20 : MonoBehaviour
    {
        const string Key="FenerRunner.CollectiblesV20";
        static readonly FieldInfo SettingsField=typeof(RunnerGame).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic);
        [Serializable] sealed class Report
        {
            public bool passed;public string utc,unityVersion;public string[] checks,errors;
            public int events,normalPool,rainPool,henPool,henChickPool;public float oldCombinedRadius,newLateralRadius;
            public string scope="Native CharacterController moves at18/30/42m/s and15/30/60Hz, both swipe directions, lateral pickup silhouettes, fixed pools, fatal contact ordering, hen scatter, upgrade cap, magnet, reset and origin shift. The baseline already had continuous swept pickups. This corrects visible sleeve-edge misses with lateral-only forgiveness; it does not claim a newly discovered tunneling bug or physical-phone evidence.";
        }
        readonly List<string> checks=new List<string>(),errors=new List<string>();readonly Report report=new Report();
        RunnerGame game;RunnerSettings original,clone;bool prepared,previousBackground;int events;
        float previousCapture;int previousRate;
        [MenuItem("Fener Runner/120 - Verify chick swipes and hen burst v20")]
        public static void StartChecks()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
            SessionState.SetBool(Key,false);new GameObject("V20 collectible checks").AddComponent<CollectibleChecksV20>();
        }
        IEnumerator Start()
        {
            game=FindFirstObjectByType<RunnerGame>();previousBackground=Application.runInBackground;previousCapture=Time.captureDeltaTime;previousRate=Application.targetFrameRate;
            Application.runInBackground=true;Application.targetFrameRate=60;Time.captureDeltaTime=1f/60f;
            prepared=true;Application.logMessageReceived+=Log;
            try{Run();}catch(Exception e){errors.Add(e.ToString());}
            yield return null;
            report.events=events;report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.checks=checks.ToArray();report.errors=errors.ToArray();
            Directory.CreateDirectory("../Validation");File.WriteAllText("../Validation/unity_collectibles_v20.json",JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("V20 collectibles "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void OnHen(){events++;}
        void Reset(float speed=30)
        {
            game.enabled=false;clone.forwardSpeed=clone.maxSpeed=speed;game.Tokens.Enabled=true;game.Play();game.Obstacles.SetActive(false);game.Powerups.IsolatePickupsForTest();game.Powerups.Enabled=false;
            game.Motor.Speed=speed;game.Tokens.CollectionReachBonus=0;game.Tokens.HenNestLevel=0;
            for(int i=0;i<game.Tokens.PoolCount;i++)game.Tokens.PlaceForTest(i,new Vector3(12,1.12f,2000+i*4.4f));
            game.Tokens.Hens.HideForTest();
        }
        int Move(float dt,float limit=1)
        {game.Tokens.BeginMove(game.Motor);game.Motor.Tick(dt);return game.Tokens.Tick(game.Motor,game.Obstacles,dt,limit);}
        void Run()
        {
            if(game==null)throw new InvalidOperationException("Built game scene required.");
            game.AutomatedTest=true;original=game.Settings;clone=Instantiate(original);SettingsField.SetValue(game,clone);game.Motor.Configure(clone);game.Obstacles.UseSeedForTest(1907);
            game.Tokens.HenCollected+=OnHen;Reset();
            var tokens=game.Tokens;var hens=tokens.Hens;
            report.normalPool=tokens.PoolCount;report.rainPool=tokens.BonusPoolCount;report.henPool=hens.HenPoolCount;report.henChickPool=hens.PoolCount;
            Check(report.normalPool==48&&report.rainPool==20&&report.henPool==1&&report.henChickPool==18,"48normal +20rain +1hen +18scatter objects are pooled and separate");
            report.oldCombinedRadius=game.Motor.Capsule.radius+tokens.PickupRadius;report.newLateralRadius=report.oldCombinedRadius+.20f;
            Check(report.oldCombinedRadius<.90f&&report.newLateralRadius>.90f,"The .90m sleeve-edge graze lies outside the old pickup envelope and inside the revised lateral silhouette");
            foreach(int count in new[]{5,6,7,8,9})
            {
                Reset();tokens.SpawnBonusVolley(game.Motor,count,game.Obstacles);
                Check(tokens.BonusSpawned==count&&tokens.BonusSkipped==0,"Rain, trail and flock upgrades really spawn their requested"+count+" chicks");
            }
            Reset();tokens.SpawnBonusVolley(game.Motor,9,game.Obstacles);tokens.SpawnBonusVolley(game.Motor,9,game.Obstacles);tokens.SpawnBonusVolley(game.Motor,9,game.Obstacles);
            Check(tokens.BonusSpawned==20&&tokens.BonusSkipped==7,"Upgraded volleys saturate fixed20pool without replacing active chicks");
            foreach(float speed in new[]{18f,30f,42f})foreach(float rate in new[]{15f,30f,60f})foreach(int side in new[]{-1,1})
            {
                Reset(speed);float dt=1/rate;game.Motor.ChangeLane(side);
                float endX=game.Motor.LanePositionAfter(dt),endZ=game.Motor.transform.position.z+speed*dt;
                tokens.PlaceForTest(0,new Vector3(endX+side*.90f,1.12f,endZ));
                int picked=Move(dt);
                Check(picked==1&&tokens.CollectedForTest(0),"Visible swipe-edge chick collected once "+speed+"m/s "+rate+"Hz side"+side);
                Check(tokens.Tick(game.Motor,game.Obstacles,0)==0,"Swipe pickup cannot be replayed "+speed+"/"+rate+"/"+side);
            }
            foreach(int side in new[]{-1,1})
            {
                Reset();game.Motor.ChangeLane(side);float dt=.07f;
                Vector3 end=new Vector3(game.Motor.LanePositionAfter(dt),1.12f,game.Motor.transform.position.z+30*dt);
                tokens.PlaceBonusForTest(0,end+Vector3.right*side*.90f,1.12f);
                hens.PlaceChickForTest(0,end-Vector3.right*side*.90f);
                Check(Move(dt)==2&&tokens.BonusCollected==1&&hens.ChicksCollected==1,"Rain and hen chicks share continuous swipe pickup side"+side);
            }
            Reset();tokens.CollectionReachBonus=.18f;Vector3 p=game.Motor.transform.position;
            tokens.PlaceForTest(0,new Vector3(p.x+1.10f,1.12f,p.z+3));
            tokens.PlaceForTest(1,new Vector3(clone.laneSpacing,1.12f,p.z+3));tokens.PlaceForTest(2,new Vector3(0,3.1f,p.z+3));
            Check(Move(.2f)==1&&tokens.CollectedForTest(0)&&!tokens.CollectedForTest(1)&&!tokens.CollectedForTest(2),"ChickSense increases lateral reach without collecting adjacent lanes or high coins");
            Reset();p=game.Motor.transform.position;tokens.PlaceForTest(0,new Vector3(0,1.12f,p.z+4));
            Check(Move(.2f,.2f)==0,"A coin beyond fatal contact cannot be credited with wider pickup");

            Reset();p=game.Motor.transform.position;hens.PlaceForTest(new Vector3(0,1.12f,p.z+3));int beforeEvents=events;
            Check(Move(.2f,.2f)==0&&hens.HensCollected==0&&events==beforeEvents,"Hen beyond fatal contact never opens or emits an event");
            Reset();p=game.Motor.transform.position;hens.PlaceForTest(new Vector3(0,1.12f,p.z+2));beforeEvents=events;
            Check(Move(.14f)==0&&hens.HensCollected==1&&hens.ChicksSpawned==12&&tokens.TotalCollected==0&&events==beforeEvents+1,
                "Physical hen pickup emits exactly one event and scatters12 visible chicks with no direct currency grant");
            Check(tokens.Tick(game.Motor,game.Obstacles,0)==0&&hens.HensCollected==1&&events==beforeEvents+1,"Consumed hen cannot be collected or announced twice");
            for(int i=0;i<12;i++)
            {Vector3 target=hens.ChickTargetForTest(i);Check(hens.ChickActiveForTest(i)&&target.z>game.Motor.transform.position.z+10&&Mathf.Abs(target.x)<=clone.laneSpacing,"Hen chick"+i+" has an ahead-of-runner safe-route target");}
            Vector3 freeze=hens.ChickPositionForTest(0);tokens.Enabled=false;
            Check(tokens.Tick(game.Motor,game.Obstacles,1)==0&&hens.ChickPositionForTest(0)==freeze,"Disabled pickups freeze the entire hen burst without a hidden reward");tokens.Enabled=true;
            // Let the burst settle, then put one real scattered chick through the
            // real controller. Repeated contact still credits it only once.
            for(int i=0;i<12;i++)tokens.Tick(game.Motor,game.Obstacles,1f/60f);
            Check(hens.ChicksCollected==0&&hens.ActiveCount==12,"Newly scattered chicks stay visible during the.24-second emergence phase instead of instantly crossing into the runner");
            for(int i=0;i<28;i++)tokens.Tick(game.Motor,game.Obstacles,1f/60f);
            Check(hens.ChickActiveForTest(0)&&hens.ChicksCollected==0,"A stationary runner does not receive the scattered chicks before reaching their settled positions");
            Vector3 target0=hens.ChickPositionForTest(0);game.Motor.Capsule.enabled=false;game.Motor.transform.position=new Vector3(target0.x,0,target0.z-3);game.Motor.Capsule.enabled=true;
            int beforeTotal=tokens.TotalCollected,collected=Move(.13f);Check(collected>0&&hens.ChicksCollected>0&&tokens.TotalCollected-beforeTotal==collected,"Scattered chicks become actual collectable currency through normal physical contact");
            int total=tokens.TotalCollected;tokens.Tick(game.Motor,game.Obstacles,0);Check(tokens.TotalCollected==total,"A hen chick cannot pay twice");

            foreach(float speed in new[]{18f,30f,42f})foreach(float hz in new[]{15f,30f,60f})
            {
                Reset(speed);p=game.Motor.transform.position;hens.PlaceForTest(new Vector3(0,1.12f,p.z+2));Move(.14f);
                float finish=hens.ChickTargetForTest(11).z+4;int steps=0;
                while(game.Motor.transform.position.z<finish&&steps++<250)Move(1/hz);
                Check(hens.ChicksSpawned==12&&hens.ChicksCollected==12&&tokens.TotalCollected==12,
                    "All12 scattered chicks remain reachable in real continuous forward movement at"+speed+"m/s and"+hz+"Hz");
            }

            Reset();tokens.HenNestLevel=99;p=game.Motor.transform.position;hens.PlaceForTest(new Vector3(0,1.12f,p.z+2));Move(.14f);
            Check(hens.ChicksSpawned==18&&hens.ActiveCount==18,"Nest upgrade is capped at18 fixed pooled chicks even with malformed input");
            Vector3 old=hens.ChickPositionForTest(5),oldTarget=hens.ChickTargetForTest(5),oldHen=hens.Position;
            tokens.ShiftOrigin(1000);game.Motor.ShiftOrigin(1000);
            Check(Vector3.Distance(hens.ChickPositionForTest(5),old-Vector3.forward*1000)<.0001f&&Vector3.Distance(hens.ChickTargetForTest(5),oldTarget-Vector3.forward*1000)<.0001f&&Vector3.Distance(hens.Position,oldHen-Vector3.forward*1000)<.0001f,"Origin shift preserves live hen, burst origins and target trajectories");
            int spawned=hens.ChicksSpawned;game.Motor.Speed=0;hens.PlaceForTest(game.Motor.transform.position+new Vector3(0,1.12f,0));Move(0);
            Check(hens.ChicksSpawned==spawned&&hens.ActiveCount==18,"An overlapping hen cannot overwrite an already visible burst chick");
            Reset();Check(hens.HensCollected==0&&hens.ChicksCollected==0&&hens.ActiveCount==0&&hens.PoolCount==18,"Restart clears bonus counters and retains fixed pool capacity");
            tokens.ResetCourse(game.Obstacles);Vector3 first=hens.Position;
            tokens.ResetCourse(game.Obstacles);Check(hens.Position==first,"Hen spawn schedule is deterministic and does not consume gameplay RNG");
            Check(Resources.Load<GameObject>("HenBonus").GetComponentsInChildren<Collider>(true).Length==0,"Hen art has no physics colliders that could kill or block the runner");
        }
        void Check(bool value,string label){if(value)checks.Add(label);else errors.Add(label);}
        void Log(string text,string trace,LogType kind){if((kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)&&errors.Count<50)errors.Add(text);}
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;Time.timeScale=1;if(!prepared)return;
            Application.runInBackground=previousBackground;Time.captureDeltaTime=previousCapture;Application.targetFrameRate=previousRate;
            if(game!=null){game.Tokens.HenCollected-=OnHen;if(original!=null){SettingsField.SetValue(game,original);game.Motor.Configure(original);}game.enabled=true;}
            if(clone!=null)Destroy(clone);
        }
    }
}
#endif
