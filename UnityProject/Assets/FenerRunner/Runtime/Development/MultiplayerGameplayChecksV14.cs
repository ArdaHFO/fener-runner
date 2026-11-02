#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FenerRunner
{
    // Draft staged outside Assets. The host is the scene's real RunnerGame owner;
    // only the second UDP peer has an explicitly controlled snapshot track.
    public sealed class MultiplayerGameplayChecksV14:MonoBehaviour
    {
        const string Flag="FenerRunner.MultiplayerGameplayChecksV14";
        static readonly FieldInfo TestPeer=typeof(RunnerMultiplayer).GetField("testPeer",BindingFlags.NonPublic|BindingFlags.Instance);
        static readonly FieldInfo Owner=typeof(RunnerMultiplayer).GetField("game",BindingFlags.NonPublic|BindingFlags.Instance);
        static readonly FieldInfo SharedOverride=typeof(ObstacleCourse).GetField("hasSharedSeed",BindingFlags.NonPublic|BindingFlags.Instance);
        [Serializable] sealed class Report
        {
            public bool passed,productionHostCapture,preferencesPreserved,isolatedWalletSettledOnce;
            public string utc,unityVersion,lastStage;public string[] checks,errors,captures;
            public float wallSeconds,realMovementSeconds,guestTrackSpeed=18,guestLaneX,deathGateSeconds;
            public double realMovementMetres,guestObservedHostDistance,hostObservedGuestDistance,fatalDistance;
            public int hostSnapshots,guestSnapshots,pausedPackets,deathGateFrames,firstSeed,rematchSeed,firstRunEarnings,isolatedFinalBalance,ghostAllocations,hostLoopbackPort;
            public Vector3 localViewport,ghostViewport;
            public string scope="Native scene RunnerGame with its production-owned RunnerMultiplayer and actual Game.Update/motor/contact/coin/animation/UI. A second actual loopback UDP peer provides a controlled 18 m/s reference track and explicit remote Flight/Dead poses. Verifies shared start, live snapshots, pause/resume, real fatal contact, death presentation, rematch, isolated wallet settlement and return to solo. This is not two physical phones, two independently simulated RunnerGames, Relay/NAT coverage or an APK/device performance test.";
        }
        struct Preference
        {
            public string Key;public bool Float,Exists;public int Integer;public float Number;
            public Preference(string key,bool floating=false){Key=key;Float=floating;Exists=PlayerPrefs.HasKey(key);Integer=floating?0:PlayerPrefs.GetInt(key,0);Number=floating?PlayerPrefs.GetFloat(key,0):0;}
            public bool Unchanged=>Exists==PlayerPrefs.HasKey(Key)&&(Float?Number==PlayerPrefs.GetFloat(Key,0):Integer==PlayerPrefs.GetInt(Key,0));
        }
        RunnerGame game;RunnerUI ui;MultiplayerUI room;RunnerMultiplayer host,guest;RunWallet wallet;
        readonly Report report=new Report();readonly List<string> checks=new List<string>(),errors=new List<string>(),captures=new List<string>();readonly List<RaycastResult> hits=new List<RaycastResult>();
        Preference[] preferences;bool prepared,stopping,cleanedScene,oldBackground,oldAutomated,oldPowerEnabled,oldTokenEnabled,oldObstacleEnabled;
        float oldCapture,oldScale,started;int oldFrameRate;uint guestRun;double guestDistance,guestLastTime;bool guestDead;GhostPose guestPose=GhostPose.Run;
        [MenuItem("Fener Runner/98 - Verify connected real gameplay v14")]
        public static void StartChecks()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before connected gameplay checks.");
            SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("V14 connected scene gameplay checks").AddComponent<MultiplayerGameplayChecksV14>();}}
        IEnumerator Start()
        {
            started=Time.realtimeSinceStartup;oldBackground=Application.runInBackground;oldCapture=Time.captureDeltaTime;oldScale=Time.timeScale;oldFrameRate=Application.targetFrameRate;
            Application.runInBackground=true;Time.captureDeltaTime=0;Time.timeScale=1;Application.targetFrameRate=60;prepared=true;Application.logMessageReceived+=Log;
            preferences=new[]{new Preference(RunWallet.DefaultPrefix+"Coins"),new Preference(RunWallet.DefaultPrefix+"Upgrade.0"),new Preference(RunWallet.DefaultPrefix+"Upgrade.1"),new Preference(RunWallet.DefaultPrefix+"Upgrade.2"),new Preference("FenerRunner.Best",true),new Preference("FenerRunner.BestScore"),new Preference("FenerRunner.Muted"),new Preference(StadiumSound.DefaultAudioPrefix+"Master",true),new Preference(StadiumSound.DefaultAudioPrefix+"Music",true),new Preference(StadiumSound.DefaultAudioPrefix+"Voices",true),new Preference(StadiumSound.DefaultAudioPrefix+"Effects",true)};
            var stack=new Stack<IEnumerator>();stack.Push(Scenarios());
            while(stack.Count>0)
            {
                bool more;object wait=null;
                try{more=stack.Peek().MoveNext();if(more)wait=stack.Peek().Current;}
                catch(Exception exception){errors.Add("During "+report.lastStage+": "+exception);break;}
                if(!more){stack.Pop();continue;}if(wait is IEnumerator nested){stack.Push(nested);continue;}yield return wait;
            }
            stopping=true;CleanupScene();
            report.preferencesPreserved=true;for(int i=0;i<preferences.Length;i++)report.preferencesPreserved&=preferences[i].Unchanged;
            Check(report.preferencesPreserved,"Production wallet, upgrade levels, records and all audio settings remain exact, including absent keys");
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.wallSeconds=Time.realtimeSinceStartup-started;
            report.checks=checks.ToArray();report.errors=errors.ToArray();report.captures=captures.ToArray();
            Directory.CreateDirectory("../Validation");File.WriteAllText(ReviewEvidence.FileFor(14,"../Validation/unity_multiplayer_gameplay_v14.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("V14 connected real gameplay "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void Update()
        {
            if(stopping||guest==null)return;
            if(guestRun!=guest.RunId){guestRun=guest.RunId;guestLastTime=guestDistance=0;guestDead=false;guestPose=GhostPose.Run;}
            if(guest.State!=MultiplayerState.Racing&&guest.State!=MultiplayerState.Finished)return;
            double time=guest.RaceTime,delta=Math.Max(0,time-guestLastTime);guestLastTime=time;
            if(!guestDead&&guest.State==MultiplayerState.Racing)guestDistance+=delta*18;
            guest.SetSnapshotForTest(new GhostSnapshot{Distance=guestDistance,Speed=guestDead?0:18,LaneX=report.guestLaneX,Height=guestPose==GhostPose.Flight?3.5f:0,
                Phase=guestDead?1:(float)(time-Math.Floor(time)),Pose=guestDead?GhostPose.Dead:guestPose});
        }
        IEnumerator Scenarios()
        {
            report.lastStage="real owner and isolated economy";game=FindFirstObjectByType<RunnerGame>();
            if(game==null||EventSystem.current==null)throw new InvalidOperationException("A built real scene and EventSystem are required.");
            oldAutomated=game.AutomatedTest;oldPowerEnabled=game.Powerups.Enabled;oldTokenEnabled=game.Tokens.Enabled;oldObstacleEnabled=game.Obstacles.Enabled;
            game.AutomatedTest=true;ui=game.Interface;room=game.MultiplayerInterface;host=game.Multiplayer;
            if(host==null||room==null)throw new InvalidOperationException("The production scene must initialize multiplayer before this harness.");
            game.ReturnToMenu();wallet=game.UseWalletForTest("FenerRunner.Test.ConnectedGameplayV14."+DateTime.UtcNow.Ticks+".");
            game.Powerups.Enabled=false;game.Tokens.Enabled=true;game.Obstacles.Enabled=true;
            report.productionHostCapture=TestPeer!=null&&Owner!=null&&!(bool)TestPeer.GetValue(host)&&ReferenceEquals(Owner.GetValue(host),game);
            Check(report.productionHostCapture,"Host is the scene-owned session using production Capture, not InitializeTestPeer or injected local snapshots");
            Check(game.enabled&&Time.captureDeltaTime==0&&wallet.IsIsolated&&wallet.Balance==0,"Live Game.Update and real frame time run with an initially empty isolated wallet");
            EditorApplication.ExecuteMenuItem("Fener Runner/5 - Open portrait Game view");yield return WaitSeconds(.7f);
            guest=new GameObject("V14 controlled UDP reference runner").AddComponent<RunnerMultiplayer>();
            guest.InitializeTestPeer(RunnerMultiplayer.CompatibilityFor(game.Settings));
            report.guestLaneX=game.Settings.laneSpacing*.55f;
            host.HostLoopbackForTest(0);ushort port=host.LocalPortForTest;report.hostLoopbackPort=port;
            if(!Check(port!=0,"The live loopback host binds an OS-assigned port without a temporary reservation socket"))yield break;
            guest.JoinLoopbackForTest(port);
            yield return Wait(()=>host.HasPeer&&guest.HasPeer&&host.State==MultiplayerState.Waiting&&guest.State==MultiplayerState.Waiting,8);
            if(!Check(host.HasPeer&&guest.HasPeer&&room.IsOpen&&game.State==RunState.Menu,"Real scene host and UDP guest handshake into the visible ready room"))yield break;
            yield return Wait(()=>host.PingMilliseconds>0&&guest.PingMilliseconds>0,4);
            Check(host.PingMilliseconds>0&&guest.PingMilliseconds>0,"Both scene-connected peers measure their clock before starting");
            yield return WaitSeconds(.4f);
            Check(Click(room.ReadyButton),"EventSystem pointer marks the actual scene host ready");
            yield return Wait(()=>host.LocalReady&&guest.RemoteReady,2);
            Check(host.LocalReady&&guest.RemoteReady&&host.State==MultiplayerState.Waiting,"One ready player remains in the room without starting the real motor");
            yield return Capture("v14-room-ready.png");guest.SetReady(true);
            yield return Wait(()=>host.State==MultiplayerState.Countdown&&guest.State==MultiplayerState.Countdown,4);
            Check(host.State==MultiplayerState.Countdown&&guest.State==MultiplayerState.Countdown&&game.State==RunState.Menu&&room.IsOpen,"Two ready players show countdown while the real scene is still stopped");
            uint firstRun=host.RunId;report.firstSeed=host.SharedSeed;
            yield return Wait(()=>host.State==MultiplayerState.Racing&&guest.State==MultiplayerState.Racing&&game.State==RunState.Running,5);
            if(!Check(host.State==MultiplayerState.Racing&&guest.State==MultiplayerState.Racing&&game.State==RunState.Running&&!room.IsOpen,"Network countdown starts actual scene gameplay and dismisses the room"))yield break;
            Check(host.RunId==guest.RunId&&host.SharedSeed==guest.SharedSeed&&game.Obstacles.Seed==host.SharedSeed,"The running real obstacle course uses the agreed race seed and identity");

            report.lastStage="actual movement snapshots and visible replica";
            float movementStart=Time.realtimeSinceStartup;double startDistance=game.Distance;Vector3 startPosition=game.Motor.transform.position;
            yield return WaitSeconds(1.5f);
            report.realMovementSeconds=Time.realtimeSinceStartup-movementStart;report.realMovementMetres=game.Distance-startDistance;
            Check(game.State==RunState.Running&&game.enabled&&report.realMovementSeconds>=1.5f&&report.realMovementMetres>20&&game.Motor.transform.position.z-startPosition.z>20,"At least 1.5 seconds of normal Game.Update move the real physics runner more than 20 metres");
            yield return Wait(()=>host.ReceivedSnapshots>5&&guest.ReceivedSnapshots>5&&host.Ghost.IsVisible,2);
            report.hostSnapshots=host.ReceivedSnapshots;report.guestSnapshots=guest.ReceivedSnapshots;report.guestObservedHostDistance=guest.RemoteDistance;report.hostObservedGuestDistance=host.RemoteDistance;
            Check(host.ReceivedSnapshots>5&&guest.ReceivedSnapshots>5&&guest.RemoteDistance>20&&Math.Abs(game.Distance-guest.RemoteDistance)<game.CurrentSpeed*.35f+2,"Guest receives the real local travel, with a bounded live network lag");
            Check(host.Ghost.IsVisible&&host.RemoteDistance>20&&host.Ghost.AllocationCount==1,"Controlled UDP snapshots drive one visible scene ghost through the real session");
            Check(game.Coins>0&&game.Tokens.TotalCollected>0,"The live local run collects actual course coins before its first obstacle");
            var ghostRoot=host.Ghost.VisualRoot;if(ghostRoot==null)throw new InvalidOperationException("Received snapshots did not allocate the scene replica.");
            bool visualOnly=true;foreach(var component in ghostRoot.GetComponentsInChildren<Component>(true))visualOnly&=component is Transform||component is Animator||component is SkinnedMeshRenderer||component is MeshFilter||component is MeshRenderer;
            Check(visualOnly&&ghostRoot.GetComponentsInChildren<Collider>(true).Length==0&&ghostRoot.GetComponentsInChildren<Rigidbody>(true).Length==0,"Network-created ghost has only visual components and no collider, rigidbody, input, perk or gameplay script");
            var viewport=Camera.main.WorldToViewportPoint(ghostRoot.position+Vector3.up);
            report.ghostViewport=viewport;report.localViewport=Camera.main.WorldToViewportPoint(game.Motor.transform.position+Vector3.up);
            Check(viewport.z>0&&viewport.x>.03f&&viewport.x<.97f&&viewport.y>.03f&&viewport.y<.97f,"The network-driven ghost body is inside the real game-camera viewport");
            var localViewport=report.localViewport;
            Check(localViewport.z>0&&localViewport.x>.10f&&localViewport.x<.90f&&localViewport.y>.08f&&localViewport.y<.80f,"Nearby-peer framing keeps the real local player inside the mobile gameplay area");
            Check(Camera.main.fieldOfView>=58&&Camera.main.fieldOfView<=68,"Nearby-peer framing preserves the established field-of-view limits");
            yield return Capture("v14-connected-race.png");
            EditorApplication.ExecuteMenuItem("Fener Runner/10 - Open tall phone Game view");yield return WaitSeconds(.25f);
            viewport=Camera.main.WorldToViewportPoint(ghostRoot.position+Vector3.up);
            localViewport=Camera.main.WorldToViewportPoint(game.Motor.transform.position+Vector3.up);
            Check(Screen.width==1080&&Screen.height==2340&&viewport.z>0&&viewport.x>.03f&&viewport.x<.97f&&viewport.y>.03f&&viewport.y<.97f,"Actual 1080x2340 Game view keeps the connected ghost inside its narrow portrait frame");
            Check(localViewport.z>0&&localViewport.x>.10f&&localViewport.x<.90f&&localViewport.y>.08f&&localViewport.y<.80f,"Tall-phone framing keeps the local runner readable inside the gameplay area");
            yield return Capture("v14-connected-race-tall.png");
            EditorApplication.ExecuteMenuItem("Fener Runner/5 - Open portrait Game view");yield return WaitSeconds(.2f);

            report.lastStage="actual pause and safe room resume";
            Check(Click(ui.pause),"Native pause control pauses the connected local run");
            yield return Wait(()=>game.State==RunState.Paused&&guest.RemotePose==GhostPose.Paused,2);
            if(!Check(game.State==RunState.Paused&&Time.timeScale==0&&guest.RemotePose==GhostPose.Paused,"Actual local pause propagates as a paused remote snapshot"))yield break;
            double pausedDistance=game.Distance;Vector3 pausedPosition=game.Motor.transform.position;int pausedScore=game.Score,pausedCoins=game.Coins,pausedWallet=game.WalletBalance,pausedCharge=game.Charge;
            float pausedHeight=game.Motor.Capsule.height,pausedRadius=game.Motor.Capsule.radius;int packets=guest.ReceivedSnapshots;
            Vector3 pausedCameraPosition=Camera.main.transform.position;Quaternion pausedCameraRotation=Camera.main.transform.rotation;
            guestPose=GhostPose.Flight;
            yield return Wait(()=>host.RemotePose==GhostPose.Flight&&host.Ghost.CurrentClip=="Flight",2);
            yield return WaitSeconds(.25f);report.pausedPackets=guest.ReceivedSnapshots-packets;
            Check(report.pausedPackets>0&&guest.RemotePose==GhostPose.Paused&&Math.Abs(guest.RemoteDistance-pausedDistance)<.001,"Packets continue during actual local pause without advancing local distance");
            Check(Camera.main.transform.position==pausedCameraPosition&&Quaternion.Angle(Camera.main.transform.rotation,pausedCameraRotation)<.001f,"Local pause freezes friend-camera framing while remote packets continue");
            Check(host.RemotePose==GhostPose.Flight&&host.Ghost.CurrentClip=="Flight"&&!game.Motor.Flying&&!game.Powerups.HasActive&&game.Distance==pausedDistance&&game.Motor.transform.position==pausedPosition&&game.Motor.Capsule.height==pausedHeight&&game.Motor.Capsule.radius==pausedRadius&&game.Score==pausedScore&&game.Coins==pausedCoins&&game.Charge==pausedCharge&&game.WalletBalance==pausedWallet,"Real remote Flight packets animate only the ghost and cannot change paused local physics, powers, score or wallet");
            Check(Click(ui.restart),"Connected pause restart opens the actual room instead of a solo reset");yield return WaitSeconds(.4f);
            Check(room.IsOpen&&host.RunId==firstRun&&host.HasPeer&&game.State==RunState.Paused&&room.ReadyButton.GetComponentInChildren<Text>().text=="YARIŞA DÖN","Alive paused player receives an accurate return-to-race action");
            guestPose=GhostPose.Run;Check(Click(room.ReadyButton),"Native YARIŞA DÖN control resumes without leaving");
            yield return Wait(()=>game.State==RunState.Running&&guest.RemotePose==GhostPose.Run,2);
            Check(game.State==RunState.Running&&!room.IsOpen&&host.State==MultiplayerState.Racing&&host.HasPeer&&guest.HasPeer&&host.RunId==firstRun&&game.Distance>pausedDistance,"Resume continues the same real race, peer connection and distance");

            report.lastStage="normal collision death and result gate";
            var obstacle=game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+4,game.Motor.Lane);
            Check(obstacle.gameObject.activeInHierarchy&&!game.Powerups.HasActive&&game.Motor.Grounded,"Fatal fixture uses an actual active obstacle in the unprotected grounded lane");
            yield return Wait(()=>game.State==RunState.Finished,2);
            if(!Check(game.State==RunState.Finished&&!string.IsNullOrEmpty(game.LastHit)&&game.Animation.DeathPlaying,"Normal Game.Update contact ends the local run and starts the real death animation"))yield break;
            report.fatalDistance=game.Distance;report.firstRunEarnings=game.RunEarnings;float gateStart=Time.realtimeSinceStartup;
            Check(!room.IsOpen&&!ui.resultPanel.activeSelf,"The first actual fatal frame does not cover the collision animation with a room/result panel");
            yield return Wait(()=>guest.RemotePose==GhostPose.Dead,2);
            Check(guest.RemotePose==GhostPose.Dead&&Math.Abs(guest.RemoteDistance-report.fatalDistance)<.001&&host.State==MultiplayerState.Racing,"Production Capture sends the exact fatal distance and Dead pose while the reference peer still runs");
            guestDead=true;
            double deathUntil=Time.realtimeSinceStartupAsDouble+4;
            while(Time.realtimeSinceStartupAsDouble<deathUntil&&!(host.State==MultiplayerState.Finished&&guest.State==MultiplayerState.Finished&&room.IsOpen))
            {
                if(game.Animation.DeathPlaying){report.deathGateFrames++;if(room.IsOpen)errors.Add("Room obscured a live collision animation frame");}
                yield return null;
            }
            report.deathGateSeconds=Time.realtimeSinceStartup-gateStart;
            Check(host.State==MultiplayerState.Finished&&guest.State==MultiplayerState.Finished&&game.State==RunState.Finished&&game.Animation.DeathComplete&&ui.resultPanel.activeSelf&&room.IsOpen,"Both observed deaths reveal the room only after the actual collision animation completes");
            Check(report.deathGateFrames>0&&report.deathGateSeconds>=.7f,"Multiple live frames exercise the unscaled death-to-room presentation gate");
            Check(wallet.Balance==report.firstRunEarnings&&wallet.Balance>0,"Pause checkpoint plus fatal settlement credit actual earned coins exactly once to the isolated wallet");

            report.lastStage="rematch real reset";yield return WaitSeconds(.4f);
            Check(Click(room.ReadyButton),"Native result room marks the real player ready for a rematch");guest.SetReady(true);
            yield return Wait(()=>host.State==MultiplayerState.Countdown&&guest.State==MultiplayerState.Countdown,4);
            report.rematchSeed=host.SharedSeed;uint rematchRun=host.RunId;
            Check(host.State==MultiplayerState.Countdown&&guest.State==MultiplayerState.Countdown&&host.RunId==guest.RunId&&GhostProtocol.IsNewerSequence(rematchRun,firstRun)&&host.SharedSeed==guest.SharedSeed&&report.rematchSeed!=report.firstSeed,"Rematch agrees on a fresh race identity and seed through the real UI/network path");
            Check(game.State==RunState.Menu&&game.Distance==0&&game.Coins==0&&game.Score==0&&!game.Powerups.HasActive&&!game.Animation.DeathPlaying&&!game.Animation.DeathComplete&&game.Motor.Grounded,"Rematch countdown performs a real scene reset with no inherited local death, coins, perks or travel");
            yield return Wait(()=>host.State==MultiplayerState.Racing&&guest.State==MultiplayerState.Racing&&game.State==RunState.Running,5);
            Check(game.State==RunState.Running&&game.Obstacles.Seed==report.rematchSeed&&game.Distance<10&&game.Coins==0&&host.RunId==rematchRun,"Second countdown starts the real motor on the new shared course");
            yield return WaitSeconds(.2f);report.ghostAllocations=host.Ghost.AllocationCount;
            Check(game.Distance>0&&host.Ghost.AllocationCount==1&&!game.Animation.DeathPlaying&&wallet.Balance==report.firstRunEarnings,"Rematch reuses the single replica and does not double-credit the completed run");

            report.lastStage="leave and ordinary solo return";
            Check(Click(ui.pause),"Real rematch can be paused before leaving");yield return Wait(()=>ui.restart.gameObject.activeInHierarchy&&ui.restart.IsInteractable(),1);
            Check(Click(ui.restart),"Connected rematch opens the room from pause");yield return WaitSeconds(.4f);
            Check(Click(room.LeaveButton),"Native room leave returns the real game to its normal menu");
            yield return Wait(()=>!guest.HasPeer&&guest.State==MultiplayerState.Disconnected,4);
            Check(game.State==RunState.Menu&&host.State==MultiplayerState.Offline&&!host.HasPeer&&!room.IsOpen&&!host.Ghost.IsVisible,"Leave clears the production session, room and visible ghost");
            Check(SharedOverride!=null&&!(bool)SharedOverride.GetValue(game.Obstacles),"Returning to solo clears the course shared-seed override");
            yield return WaitSeconds(.5f);Check(Click(ui.play),"Original solo CTA works after a connected rematch and leave");yield return WaitSeconds(.2f);
            Check(game.State==RunState.Running&&game.Distance>0&&host.State==MultiplayerState.Offline&&!room.IsOpen&&!host.Ghost.IsVisible&&!(bool)SharedOverride.GetValue(game.Obstacles),"Ordinary solo Game.Update advances with no peer, ghost or multiplayer course override");
            game.ReturnToMenu();report.isolatedFinalBalance=wallet.Balance;report.isolatedWalletSettledOnce=wallet.Balance==report.firstRunEarnings;
            Check(report.isolatedWalletSettledOnce,"Rematch, leave, solo start and menu return preserve exactly the first run's isolated earnings");
        }
        IEnumerator Wait(Func<bool> condition,float seconds){double until=Time.realtimeSinceStartupAsDouble+seconds;while(!condition()&&Time.realtimeSinceStartupAsDouble<until)yield return null;}
        static WaitForSecondsRealtime WaitSeconds(float seconds)=>new WaitForSecondsRealtime(seconds);
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();string path=ReviewEvidence.FileFor(14,"../Validation/"+name);DateTime previous=File.Exists(path)?File.GetLastWriteTimeUtc(path):DateTime.MinValue;
            ScreenCapture.CaptureScreenshot(path);captures.Add(path);
            yield return Wait(()=>File.Exists(path)&&File.GetLastWriteTimeUtc(path)>previous,2);Check(File.Exists(path)&&File.GetLastWriteTimeUtc(path)>previous,"Fresh native scene capture exists: "+name);
        }
        bool Click(Button button)
        {
            if(button==null||!button.gameObject.activeInHierarchy||!button.IsInteractable())return false;
            var r=(RectTransform)button.transform;var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};
            hits.Clear();EventSystem.current.RaycastAll(data,hits);if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=button)return false;
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);return true;
        }
        bool Check(bool condition,string text){checks.Add((condition?"PASS ":"FAIL ")+text);if(!condition)errors.Add(text);return condition;}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(type+" during "+report.lastStage+": "+message);}
        void CleanupScene(bool restoreScene=true)
        {
            if(cleanedScene)return;cleanedScene=true;
            if(guest!=null){if(restoreScene)guest.Leave();Destroy(guest.gameObject);guest=null;}
            // Scene objects may already be destroyed during Play Mode teardown.
            // The normal completion path restores once before writing the report;
            // OnDestroy owns only remaining test objects and isolated prefs.
            if(restoreScene)
            {
                if(room!=null&&room.IsOpen)room.Close();else if(host!=null)host.Leave();
                if(game!=null)
                {
                    game.ReturnToMenu();game.Powerups.Enabled=oldPowerEnabled;game.Tokens.Enabled=oldTokenEnabled;game.Obstacles.SetActive(oldObstacleEnabled);
                    game.AutomatedTest=oldAutomated;
                }
            }
            if(wallet!=null){wallet.DeleteIsolatedTestData();wallet=null;}
        }
        void OnDestroy()
        {
            stopping=true;Application.logMessageReceived-=Log;
            if(prepared){CleanupScene(false);Application.runInBackground=oldBackground;Application.targetFrameRate=oldFrameRate;Time.captureDeltaTime=oldCapture;Time.timeScale=oldScale;}
        }
    }
}
#endif
