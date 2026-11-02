#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

namespace FenerRunner
{
    public sealed class MultiplayerSessionChecksV14 : MonoBehaviour
    {
        const string Flag="FenerRunner.MultiplayerSessionV14",RelayFlag="FenerRunner.MultiplayerRelayV14";
        const string HostProfile="fener-v14-host",GuestProfile="fener-v14-guest";
        static readonly FieldInfo TransportField=typeof(RunnerMultiplayer).GetField("transport",BindingFlags.Instance|BindingFlags.NonPublic);
        [Serializable] sealed class Report
        {
            public bool passed,realRelay,twoIndependentProfiles,preferencesPreserved,defaultServicesPreserved,sameHostCodeRejoined;
            public string utc,unityVersion,scope,lastStage,hostState,guestState,hostError,guestError;
            public string[] checks,errors;
            public float wallSeconds,countdownClockDifference,hostPingMs,guestPingMs,snapshotWindowSeconds,hostSnapshotHz,guestSnapshotHz;
            public int hostSnapshots,guestSnapshots,driverDisposals,hostLoopbackPort;
            public int controlledLongRttCases;
        }
        sealed class Track
        {public RunnerMultiplayer Peer;public uint RunId;public double LastTime,Distance;public bool Dead,Paused;}
        readonly List<string> checks=new List<string>(),errors=new List<string>();readonly Report report=new Report();
        readonly Track hostTrack=new Track(),guestTrack=new Track();
        RunnerMultiplayer host,guest;RunnerGame sceneGame;bool oldAutomated,oldBackground,prepared,relay,stopping;int oldRate;
        float oldScale,oldCapture,started;ushort port;string originalPreferences,defaultProfile;ServicesInitializationState defaultServices;
        ulong profileHash;

        [MenuItem("Fener Runner/96 - Verify two local ghost sessions v14")]
        public static void StartChecks(){StartRun(false);}
        [MenuItem("Fener Runner/97 - Verify actual Relay ghost sessions v14")]
        public static void StartRelayChecks(){StartRun(true);}
        static void StartRun(bool cloud)
        {if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");SessionState.SetBool(RelayFlag,cloud);SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("V14 native multiplayer session checks").AddComponent<MultiplayerSessionChecksV14>();}}
        IEnumerator Start()
        {
            relay=SessionState.GetBool(RelayFlag,false);started=Time.realtimeSinceStartup;
            oldBackground=Application.runInBackground;oldScale=Time.timeScale;oldCapture=Time.captureDeltaTime;oldRate=Application.targetFrameRate;prepared=true;
            Application.runInBackground=true;Application.targetFrameRate=120;Time.captureDeltaTime=0;Time.timeScale=1;
            originalPreferences=Preferences();defaultServices=UnityServices.State;
            defaultProfile=defaultServices==ServicesInitializationState.Initialized?AuthenticationService.Instance.Profile:"";
            sceneGame=FindFirstObjectByType<RunnerGame>();if(sceneGame!=null){oldAutomated=sceneGame.AutomatedTest;sceneGame.AutomatedTest=true;}
            Application.logMessageReceived+=Log;
            profileHash=GhostProtocol.ComputeCompatibility("native-session-v14|course14|speed42|two-test-peers");
            host=new GameObject("V14 isolated host peer").AddComponent<RunnerMultiplayer>();guest=new GameObject("V14 isolated guest peer").AddComponent<RunnerMultiplayer>();
            host.InitializeTestPeer(profileHash,relay?HostProfile:"");guest.InitializeTestPeer(profileHash,relay?GuestProfile:"");
            hostTrack.Peer=host;guestTrack.Peer=guest;
            var stack=new Stack<IEnumerator>();stack.Push(Scenarios());
            while(stack.Count>0)
            {
                bool more;object wait=null;
                try{more=stack.Peek().MoveNext();if(more)wait=stack.Peek().Current;}
                catch(Exception error){errors.Add("Harness exception during "+report.lastStage+": "+error.GetType().Name);break;}
                if(!more){stack.Pop();continue;}
                if(wait is IEnumerator nested){stack.Push(nested);continue;}yield return wait;
            }
            stopping=true;Time.timeScale=1;
            report.hostState=host.State.ToString();report.guestState=guest.State.ToString();report.hostError=host.Error;report.guestError=guest.Error;
            DisposePeer(host);DisposePeer(guest);
            report.preferencesPreserved=originalPreferences==Preferences();
            report.defaultServicesPreserved=UnityServices.State==defaultServices&&(defaultServices!=ServicesInitializationState.Initialized||AuthenticationService.Instance.Profile==defaultProfile);
            Check(report.preferencesPreserved,"Real wallet, upgrades and run records remain unchanged");
            Check(report.defaultServicesPreserved,"Custom test service instances preserve the default Unity Services state and profile");
            report.passed=errors.Count==0;report.realRelay=relay;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;
            report.wallSeconds=Time.realtimeSinceStartup-started;report.checks=checks.ToArray();report.errors=errors.ToArray();
            report.scope=relay?"Two actual Unity Relay connections and separately profiled anonymous UGS service instances in one native Editor process. Real allocation/join/handshake, UDP/DTLS transport, countdown, snapshots, reconnect and disposal. Cloud failure remains a failed result. Long RTT scheduler fixtures use explicitly controlled Editor clock fields and injected matching replies, not measured carrier latency. No physical phones, carrier/NAT diversity, concurrent production race physics or APK build are claimed.":
                "Two actual loopback Unity Transport peers in native Editor Play Mode. Production session state machine, reliable controls, snapshots, unscaled countdown, cancellation, reconnect and native driver disposal. Long RTT scheduler fixtures use controlled Editor clock fields and injected matching replies, not measured network latency. No cloud, physical phones or concurrent production race physics are claimed.";
            Directory.CreateDirectory("../Validation");File.WriteAllText(ReviewEvidence.FileFor(14,relay?"../Validation/unity_multiplayer_relay_v14.json":"../Validation/unity_multiplayer_session_v14.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("V14 "+(relay?"Relay":"local UDP")+" sessions "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void Update(){if(!stopping){TickTrack(hostTrack);TickTrack(guestTrack);}}
        static void TickTrack(Track track)
        {
            RunnerMultiplayer peer=track.Peer;if(peer==null)return;
            if(track.RunId!=peer.RunId){track.RunId=peer.RunId;track.LastTime=track.Distance=0;track.Dead=track.Paused=false;}
            if(peer.State!=MultiplayerState.Racing&&peer.State!=MultiplayerState.Finished)return;
            double time=peer.RaceTime,elapsed=Math.Max(0,time-track.LastTime);track.LastTime=time;
            if(!track.Dead&&!track.Paused&&peer.State==MultiplayerState.Racing)track.Distance+=elapsed*18;
            peer.SetSnapshotForTest(new GhostSnapshot{Distance=track.Distance,Speed=track.Dead||track.Paused?0:18,LaneX=0,Height=.02f,
                Phase=(float)(time-Math.Floor(time)),Pose=track.Dead?GhostPose.Dead:track.Paused?GhostPose.Paused:GhostPose.Run});
        }
        IEnumerator Scenarios()
        {
            report.lastStage="validation and connection";
            yield return WaitTask(guest.JoinAsync("bad$"),2,"Invalid code returns promptly");
            Check(guest.State==MultiplayerState.Offline&&!guest.HasPeer&&TransportField.GetValue(guest)==null&&guest.Error.Length>0,"Invalid room code creates no native transport or active session");
            if(relay)
            {
                yield return WaitTask(host.HostAsync(),65,"Relay host operation finishes within its bound");
                if(!Check(host.State==MultiplayerState.Waiting&&RunnerMultiplayer.ValidCode(host.JoinCode),"Actual Relay host allocation returns a usable room code"))yield break;
                yield return WaitTask(guest.JoinAsync(host.JoinCode),45,"Relay guest join operation finishes within its bound");
            }
            else
            {
                host.HostLoopbackForTest(0);port=host.LocalPortForTest;report.hostLoopbackPort=port;
                if(!Check(port!=0,"The live loopback host binds an OS-assigned port that remains open for reconnects"))yield break;
                guest.JoinLoopbackForTest(port);
            }
            yield return Wait(()=>host.HasPeer&&guest.HasPeer,relay?15:8);
            if(!Check(host.HasPeer&&guest.HasPeer&&host.State==MultiplayerState.Waiting&&guest.State==MultiplayerState.Waiting,"Two real transports complete the production Hello/Welcome handshake"))yield break;
            Check(host.RunId!=0&&host.RunId==guest.RunId&&host.SharedSeed==guest.SharedSeed,"Handshake agrees on nonzero race identity and shared course seed");
            if(relay)
            {
                report.twoIndependentProfiles=host.AuthenticationProfileForTest==HostProfile&&guest.AuthenticationProfileForTest==GuestProfile;
                Check(report.twoIndependentProfiles,"Host and guest retain two independently initialized authentication profiles");
            }
            yield return Wait(()=>host.PingMilliseconds>0&&guest.PingMilliseconds>0,4);
            report.hostPingMs=host.PingMilliseconds;report.guestPingMs=guest.PingMilliseconds;
            Check(host.PingMilliseconds>0&&guest.PingMilliseconds>0,"Both native peers exchange measured Ping/Pong clock samples");
            CheckLongRttScheduling();
            int rejected=host.RejectedMessages+guest.RejectedMessages;uint epoch=host.RunId;
            host.InjectForTest(new GhostMessage{Kind=GhostMessageKind.Start,RunId=epoch+1,HostTime=Time.realtimeSinceStartupAsDouble+3,StartDelay=3});
            guest.InjectForTest(new GhostMessage{Kind=GhostMessageKind.Hello,Compatibility=profileHash,Nonce=1});
            guest.InjectForTest(new GhostMessage{Kind=GhostMessageKind.Ready,RunId=epoch^0x40000000,Ready=true});
            Check(host.RejectedMessages+guest.RejectedMessages==rejected+3&&host.State==MultiplayerState.Waiting&&guest.State==MultiplayerState.Waiting,
                "Wrong host/client roles and stale ready epochs cannot advance the session");

            report.lastStage="ready countdown and snapshots";Time.timeScale=0;
            host.SetReady(true);host.SetReady(true);yield return Wait(()=>guest.RemoteReady,3);
            Check(host.LocalReady&&guest.RemoteReady&&host.State==MultiplayerState.Waiting,"One ready player and repeated ready presses cannot start a race");
            // Both requests are queued on the real reliable transport before
            // either peer pumps again. Host commits the first offer, then the
            // crossed unready must not strand the guest in the old lobby.
            guest.SetReady(true);guest.SetReady(false);
            Check(!guest.LocalReady,"Guest queues a real ready/unready crossing before receiving host Start");
            yield return Wait(()=>host.State==MultiplayerState.Countdown&&guest.State==MultiplayerState.Countdown,4);
            if(!Check(host.State==MultiplayerState.Countdown&&guest.State==MultiplayerState.Countdown,"Host-committed readiness survives the crossed unready and both peers enter countdown"))yield break;
            uint race=host.RunId;report.countdownClockDifference=Math.Abs(host.CountdownSeconds-guest.CountdownSeconds);
            host.SetReady(true);guest.SetReady(true);
            Check(race==guest.RunId&&GhostProtocol.IsNewerSequence(race,epoch)&&host.SharedSeed==guest.SharedSeed&&report.countdownClockDifference<.35f,
                "Start allocates one newer race epoch with shared seed and synchronized countdown");
            yield return Wait(()=>host.State==MultiplayerState.Racing&&guest.State==MultiplayerState.Racing,5);
            if(!Check(host.State==MultiplayerState.Racing&&guest.State==MultiplayerState.Racing&&Time.timeScale==0,"Unscaled network countdown starts while global gameplay time is paused"))yield break;
            yield return Wait(()=>host.ReceivedSnapshots>2&&guest.ReceivedSnapshots>2,3);
            int hostCount=host.ReceivedSnapshots,guestCount=guest.ReceivedSnapshots;float snapshotStart=Time.realtimeSinceStartup;
            yield return Wait(()=>Time.realtimeSinceStartup-snapshotStart>=2,3);
            report.snapshotWindowSeconds=Time.realtimeSinceStartup-snapshotStart;
            report.hostSnapshots=host.ReceivedSnapshots-hostCount;report.guestSnapshots=guest.ReceivedSnapshots-guestCount;
            report.hostSnapshotHz=report.hostSnapshots/report.snapshotWindowSeconds;report.guestSnapshotHz=report.guestSnapshots/report.snapshotWindowSeconds;
            Check(report.hostSnapshotHz>=10&&report.hostSnapshotHz<=22&&report.guestSnapshotHz>=10&&report.guestSnapshotHz<=22&&host.RemoteDistance>0&&guest.RemoteDistance>0,
                "Actual native snapshots progress in both directions at a bounded cadence targeting 20 Hz");
            rejected=guest.RejectedMessages;
            guest.InjectForTest(new GhostMessage{Kind=GhostMessageKind.Ready,RunId=race,Ready=true});
            guest.InjectForTest(new GhostMessage{Kind=GhostMessageKind.Snapshot,Snapshot=new GhostSnapshot{RunId=epoch,Sequence=100,Time=guest.RaceTime,Distance=0,Pose=GhostPose.Run}});
            Check(guest.RejectedMessages==rejected+2&&guest.RunId==race,"Late ready and old-race snapshot packets cannot alter a running race");

            report.lastStage="pause death and rematch";hostTrack.Paused=true;
            yield return Wait(()=>guest.RemotePose==GhostPose.Paused,3);double pausedDistance=guest.RemoteDistance;int pausePackets=guest.ReceivedSnapshots;
            yield return new WaitForSecondsRealtime(.25f);
            Check(guest.RemotePose==GhostPose.Paused&&Math.Abs(guest.RemoteDistance-pausedDistance)<.001&&guest.ReceivedSnapshots>pausePackets,
                "Paused ghost sends live packets while its race distance stays still");
            hostTrack.Paused=false;yield return Wait(()=>guest.RemotePose==GhostPose.Run&&guest.RemoteDistance>pausedDistance,3);
            Check(guest.RemotePose==GhostPose.Run&&guest.RemoteDistance>pausedDistance,"Paused peer resumes without a backward or impossible distance jump");
            hostTrack.Dead=true;yield return Wait(()=>guest.RemotePose==GhostPose.Dead,3);
            Check(host.State==MultiplayerState.Racing&&guest.State==MultiplayerState.Racing,"One death leaves the surviving peer in the active race");
            host.SetReady(true);Check(host.State==MultiplayerState.Racing&&host.RunId==race,"A dead player cannot force a rematch while the other is still running");
            guestTrack.Dead=true;yield return Wait(()=>host.State==MultiplayerState.Finished&&guest.State==MultiplayerState.Finished,4);
            if(!Check(host.State==MultiplayerState.Finished&&guest.State==MultiplayerState.Finished&&!host.LocalReady&&!guest.LocalReady,"Both observed deaths finish the shared race and clear readiness"))yield break;
            host.SetReady(true);guest.SetReady(true);yield return Wait(()=>host.State==MultiplayerState.Countdown&&guest.State==MultiplayerState.Countdown,4);
            Check(host.RunId==guest.RunId&&GhostProtocol.IsNewerSequence(host.RunId,race),"Ready again creates a new rematch epoch instead of resurrecting old snapshots");
            yield return Wait(()=>host.State==MultiplayerState.Racing&&guest.State==MultiplayerState.Racing,5);
            Check(host.State==MultiplayerState.Racing&&guest.State==MultiplayerState.Racing&&host.RemotePose!=GhostPose.Dead&&guest.RemotePose!=GhostPose.Dead,"Rematch starts clean with no inherited death state");

            report.lastStage="leave cancellation and same-code rejoin";Time.timeScale=1;string code=host.JoinCode;
            object guestTransport=TransportField.GetValue(guest);guest.Leave();yield return Wait(()=>!host.HasPeer&&host.State==MultiplayerState.Waiting,5);
            Check(guest.State==MultiplayerState.Offline&&!host.HasPeer&&host.JoinCode==code,"Guest leave releases its session while host keeps the same join code");
            CheckDisposed(guestTransport,"Guest leave disposes its actual native driver");
            if(relay)
            {
                Task cancel=guest.JoinAsync(code);guest.Leave();yield return WaitTask(cancel,45,"Cancelled join callback settles within its bound");
                Check(guest.State==MultiplayerState.Offline&&!guest.HasPeer&&TransportField.GetValue(guest)==null,"Cancelled asynchronous join cannot resurrect a native transport");
                yield return WaitTask(guest.JoinAsync(code),45,"Relay same-code rejoin operation finishes within its bound");
            }
            else
            {
                guest.JoinLoopbackForTest(port);guest.Leave();yield return new WaitForSecondsRealtime(.15f);
                Check(guest.State==MultiplayerState.Offline&&!guest.HasPeer&&TransportField.GetValue(guest)==null,"Cancelled local connection remains offline after transport callbacks");
                guest.JoinLoopbackForTest(port);
            }
            yield return Wait(()=>host.HasPeer&&guest.HasPeer&&guest.State==MultiplayerState.Waiting,relay?15:8);
            report.sameHostCodeRejoined=host.HasPeer&&guest.HasPeer&&host.JoinCode==code;
            if(!Check(report.sameHostCodeRejoined,"Guest rejoins the existing host allocation with the same code"))yield break;
            if(relay)Check(host.AuthenticationProfileForTest==HostProfile&&guest.AuthenticationProfileForTest==GuestProfile,"Leave, cancelled join and rejoin preserve both isolated authentication profiles");
            // Controlled age fixture exercises the real timeout and driver
            // disposal, without presenting it as a measured slow carrier link.
            const BindingFlags timingFlags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(RunnerMultiplayer).GetField("clockSynchronized",timingFlags).SetValue(guest,false);
            typeof(RunnerMultiplayer).GetField("operationStarted",timingFlags).SetValue(guest,Time.realtimeSinceStartupAsDouble-21);
            typeof(RunnerMultiplayer).GetField("pingPending",timingFlags).SetValue(guest,false);
            object timedOut=TransportField.GetValue(guest);
            typeof(RunnerMultiplayer).GetMethod("Update",timingFlags).Invoke(guest,null);
            Check(guest.State==MultiplayerState.Disconnected&&!guest.HasPeer&&guest.Error.Length>0,"Controlled unsynchronized clock deadline fails visibly instead of leaving readiness stuck forever");
            CheckDisposed(timedOut,"Clock deadline disposes its actual native driver");
            yield return Wait(()=>!host.HasPeer&&host.State==MultiplayerState.Waiting,15);
            if(relay)yield return WaitTask(guest.JoinAsync(code),45,"Guest can rejoin after clock synchronization timeout");
            else guest.JoinLoopbackForTest(port);
            yield return Wait(()=>host.HasPeer&&guest.HasPeer&&guest.State==MultiplayerState.Waiting,relay?15:8);
            if(!Check(host.HasPeer&&guest.HasPeer,"Both real peers reconnect after a clock deadline failure"))yield break;
            object dropped=TransportField.GetValue(guest);guest.DropTransportForTest();yield return Wait(()=>!host.HasPeer&&host.State==MultiplayerState.Waiting,15);
            Check(guest.State==MultiplayerState.Disconnected&&!guest.HasPeer&&!host.HasPeer&&host.State==MultiplayerState.Waiting,"Actual transport loss removes the peer and leaves the host available for rejoin");
            CheckDisposed(dropped,"Abrupt transport drop disposes its actual native driver");
        }
        void CheckLongRttScheduling()
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            Type type=typeof(RunnerMultiplayer);
            FieldInfo nonce=type.GetField("pingNonce",flags),sent=type.GetField("pingSent",flags),due=type.GetField("nextPing",flags),
                pending=type.GetField("pingPending",flags),synchronized=type.GetField("clockSynchronized",flags),best=type.GetField("bestRtt",flags);
            MethodInfo update=type.GetMethod("Update",flags);
            foreach(double elapsed in new[]{1.1,1.5,1.9})
            {
                uint id=0x70000000+(uint)Math.Round(elapsed*100);
                double origin=Time.realtimeSinceStartupAsDouble-elapsed;
                nonce.SetValue(guest,id);sent.SetValue(guest,origin);due.SetValue(guest,origin+1);
                pending.SetValue(guest,true);synchronized.SetValue(guest,false);best.SetValue(guest,double.MaxValue);
                // Execute the actual session update/scheduler, including its
                // real transport pump, without waiting another slow frame.
                update.Invoke(guest,null);
                bool retained=(uint)nonce.GetValue(guest)==id&&(bool)pending.GetValue(guest);
                double now=Time.realtimeSinceStartupAsDouble;
                guest.InjectForTest(new GhostMessage{Kind=GhostMessageKind.Pong,Nonce=id,EchoTime=origin,HostTime=(origin+now)*.5});
                bool accepted=retained&&!(bool)pending.GetValue(guest)&&(bool)synchronized.GetValue(guest)&&
                    guest.PingMilliseconds>=elapsed*1000-.5&&guest.PingMilliseconds<=2000;
                if(accepted)report.controlledLongRttCases++;
                Check(accepted,"Controlled "+elapsed.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" s RTT retains its pending nonce and permits first-clock synchronization");
            }
            uint timeoutId=0x71000001;double timeoutOrigin=Time.realtimeSinceStartupAsDouble-2.3;
            nonce.SetValue(guest,timeoutId);sent.SetValue(guest,timeoutOrigin);due.SetValue(guest,timeoutOrigin+1);pending.SetValue(guest,true);
            update.Invoke(guest,null);
            Check((uint)nonce.GetValue(guest)!=timeoutId&&(bool)pending.GetValue(guest),"A pending ping is renewed after its 2.25 s timeout");
            // Let this genuine new ping synchronize normally during the remaining
            // handshake tests. Controlled old replies cannot change its nonce.
        }
        IEnumerator Wait(Func<bool> condition,float seconds)
        {double until=Time.realtimeSinceStartupAsDouble+seconds;while(!condition()&&Time.realtimeSinceStartupAsDouble<until)yield return null;}
        IEnumerator WaitTask(Task task,float seconds,string label)
        {yield return Wait(()=>task.IsCompleted,seconds);Check(task.IsCompleted&&!task.IsCanceled&&!task.IsFaulted,label);}
        void DisposePeer(RunnerMultiplayer peer)
        {
            if(peer==null)return;object previous=TransportField.GetValue(peer);peer.Leave();
            if(previous!=null)CheckDisposed(previous,"Final "+(peer==host?"host":"guest")+" cleanup disposes its native driver");
        }
        void CheckDisposed(object transport,string label)
        {
            bool disposed=transport!=null&&NativeDisposed(transport);if(disposed)report.driverDisposals++;Check(disposed,label);
        }
        static bool NativeDisposed(object transport)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            PropertyInfo property=transport.GetType().GetProperty("IsCreated",flags)??transport.GetType().GetProperty("DriverCreated",flags);
            if(property!=null&&property.PropertyType==typeof(bool))return !(bool)property.GetValue(transport);
            foreach(var field in transport.GetType().GetFields(flags))
            {
                if(field.FieldType.Name!="NetworkDriver")continue;object driver=field.GetValue(transport);
                var created=field.FieldType.GetProperty("IsCreated",flags);if(created!=null)return !(bool)created.GetValue(driver);
            }
            return false;
        }
        static string Preferences()
        {
            string value=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0)+"|"+PlayerPrefs.GetInt("FenerRunner.BestScore",0)+"|"+PlayerPrefs.GetFloat("FenerRunner.Best",0).ToString("R");
            for(int i=0;i<3;i++)value+="|"+PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Upgrade."+i,0);return value;
        }
        bool Check(bool ok,string label){checks.Add((ok?"PASS ":"FAIL ")+label);if(!ok)errors.Add(label);return ok;}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add("Unity logged "+type+" during "+report.lastStage);}
        void OnDestroy()
        {
            stopping=true;Application.logMessageReceived-=Log;
            if(host!=null){host.Leave();Destroy(host.gameObject);}if(guest!=null){guest.Leave();Destroy(guest.gameObject);}
            if(sceneGame!=null)sceneGame.AutomatedTest=oldAutomated;
            if(prepared){Application.runInBackground=oldBackground;Application.targetFrameRate=oldRate;Time.captureDeltaTime=oldCapture;Time.timeScale=oldScale;}
        }
    }
}
#endif
