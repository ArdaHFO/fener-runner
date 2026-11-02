using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace FenerRunner
{
    public enum MultiplayerState { Offline, Connecting, Waiting, Countdown, Racing, Finished, Disconnected }

    // Two independent local runs, with a visual-only remote peer. No remote packet
    // can mutate the local wallet, motor, physics or collected rewards.
    public sealed class RunnerMultiplayer : MonoBehaviour
    {
        public MultiplayerState State { get; private set; }
        public string JoinCode { get; private set; } = "";
        public string Status { get; private set; } = "Arkadaşınla aynı parkurda koş.";
        public string Error { get; private set; } = "";
        public bool IsHost { get; private set; }
        public bool HasPeer { get; private set; }
        public bool Connected => HasPeer;
        public bool LocalReady { get; private set; }
        public bool RemoteReady { get; private set; }
        public bool IsActive => State!=MultiplayerState.Offline && State!=MultiplayerState.Disconnected;
        public float CountdownSeconds => State==MultiplayerState.Countdown?(float)Math.Max(0,startAt-HostClock):0;
        public double RemoteDistance { get; private set; }
        public GhostPose RemotePose { get; private set; }
        public uint RunId { get; private set; }
        public int SharedSeed { get; private set; }
        public float PingMilliseconds { get; private set; }
        public int ReceivedSnapshots { get; private set; }
        public int RejectedMessages { get; private set; }
        public GhostRunnerView Ghost => ghost;
        public event Action Changed;

        RunnerGame game;
        GhostRunnerView ghost;
        GhostTransport transport;
        readonly GhostJitterBuffer buffer=new GhostJitterBuffer();
        ulong compatibility;
        uint nonce,sequence,pingNonce;
        double operationStarted,lastReceived,lastSnapshotReceived,nextSend,nextPing,startAt,clockOffset,pingSent;
        double bestRtt=double.MaxValue;
        int generation;
        bool destroyed,localDead,remoteDead,disconnecting,clockSynchronized,pingPending,readyOffered;
        Task servicesTask,authenticationTask;
        IUnityServices services;
        string profile="";
        static double Now=>Time.realtimeSinceStartupAsDouble;
        double HostClock=>Now+(IsHost?0:clockOffset);
        public double RaceTime=>Math.Max(0,HostClock-startAt);

        public void Initialize(RunnerGame owner)
        {
            game=owner;
            compatibility=CompatibilityFor(owner.Settings);
            ghost=gameObject.AddComponent<GhostRunnerView>();ghost.Initialize(owner);
        }
        public static ulong CompatibilityFor(RunnerSettings settings)
        {
            return GhostProtocol.ComputeCompatibility("v20-hen-swept-chicks-upgrade-catalog-friends1.3-mps1.2.1-utp2.6.0|"+
                JsonUtility.ToJson(settings)+"|focus1.15|slide1.5|cap42|coins25|shared-course");
        }
        static uint NewId()
        {
            byte[] bytes=Guid.NewGuid().ToByteArray();uint value=BitConverter.ToUInt32(bytes,0);return value==0?1:value;
        }
        public static string NormalizeCode(string value)
        {return (value??"").Trim().ToUpperInvariant();}
        public static bool ValidCode(string value)
        {
            if(value==null||value.Length<4||value.Length>8)return false;
            foreach(char c in value)if(!((c>='A'&&c<='Z')||(c>='0'&&c<='9')))return false;
            return true;
        }
        public Task HostAsync()=>ConnectAsync(true,"");
        public Task JoinAsync(string code)=>ConnectAsync(false,NormalizeCode(code));
        async Task ConnectAsync(bool host,string code)
        {
            if(destroyed||IsActive)return;
            if(!host&&!ValidCode(code)){Error="Oda kodunu kontrol et (4–8 harf veya rakam).";Notify();return;}
            int request=++generation;ReleaseTransport();ResetPeer();
            IsHost=host;JoinCode="";Error="";nonce=NewId();RunId=NewId();
            SharedSeed=unchecked((int)NewId());operationStarted=Now;
            SetState(MultiplayerState.Connecting,host?"Odan hazırlanıyor…":"Odaya bağlanılıyor…");
            try
            {
                await GetAuthenticatedServicesAsync();
                if(!Current(request))return;
                var relay=services.GetRelayService();
                if(host)
                {
                    var allocation=await Bounded(relay.CreateAllocationAsync(1));if(!Current(request))return;
                    string createdCode=await Bounded(relay.GetJoinCodeAsync(allocation.AllocationId));if(!Current(request))return;
                    Install(GhostTransport.HostRelay(AllocationUtils.ToRelayServerData(allocation,"dtls")));
                    double bindDeadline=Now+15;
                    while(Current(request)&&transport!=null&&!transport.IsRelayBound&&!transport.RelayFailed&&Now<bindDeadline)await Task.Delay(25);
                    if(!Current(request))return;
                    if(transport==null||!transport.IsRelayBound)throw new TimeoutException();
                    JoinCode=createdCode;
                    SetState(MultiplayerState.Waiting,"Kodu arkadaşına söyle. İkiniz de hazır olunca başlarız.");
                }
                else
                {
                    var allocation=await Bounded(relay.JoinAllocationAsync(code));if(!Current(request))return;
                    Install(GhostTransport.JoinRelay(AllocationUtils.ToRelayServerData(allocation,"dtls")));
                    JoinCode=code;operationStarted=Now;
                }
            }
            catch(Exception exception)
            {
                if(!Current(request))return;
                Fail(exception is TimeoutException?"Bağlantı zaman aşımına uğradı. Yeniden deneyebilirsin.":
                    exception is RelayServiceException?"Oda bulunamadı, dolu veya bağlantı hizmetine ulaşılamıyor.":
                    "Çevrimiçi bağlantı kurulamadı. İnternetini kontrol edip tekrar dene.");
                // Keep private allocation keys and authentication responses out of logs/UI.
            }
        }
        // Social and Relay share one authenticated identity and the same pending
        // sign-in task. Opening Friends while creating a room cannot double-sign-in.
        public async Task<IUnityServices> GetAuthenticatedServicesAsync()
        {
            if(destroyed)throw new ObjectDisposedException(nameof(RunnerMultiplayer));
            if(servicesTask==null||servicesTask.IsFaulted||servicesTask.IsCanceled)servicesTask=InitializeServices();
            await Bounded(servicesTask);
            if(destroyed)throw new ObjectDisposedException(nameof(RunnerMultiplayer));
            var auth=services.GetAuthenticationService();
            if(!auth.IsSignedIn)
            {
                if(authenticationTask==null||authenticationTask.IsCompleted)authenticationTask=auth.SignInAnonymouslyAsync();
                await Bounded(authenticationTask);
            }
            if(destroyed)throw new ObjectDisposedException(nameof(RunnerMultiplayer));
            return services;
        }
        async Task InitializeServices()
        {
            if(services==null)
            {
                if(profile.Length==0)
                {await UnityServices.InitializeAsync();services=UnityServices.Instance;}
                else
                {services=UnityServices.CreateServices("FenerRunner-"+Guid.NewGuid().ToString("N"));await services.InitializeAsync(new InitializationOptions().SetProfile(profile));}
            }
            else if(services.State!=ServicesInitializationState.Initialized)
                await services.InitializeAsync(profile.Length==0?new InitializationOptions():new InitializationOptions().SetProfile(profile));
        }
        static async Task Bounded(Task task)
        {
            if(await Task.WhenAny(task,Task.Delay(20000))!=task)
            {Observe(task);throw new TimeoutException();}
            await task;
        }
        static async Task<T> Bounded<T>(Task<T> task)
        {await Bounded((Task)task);return await task;}
        static async void Observe(Task task){try{await task;}catch{ /* timed-out operation is observed, never applied */ }}
        bool Current(int request)=>!destroyed&&request==generation&&State==MultiplayerState.Connecting;
        void Install(GhostTransport value)
        {
            transport=value;transport.ConnectedEvent+=OnConnected;transport.DisconnectedEvent+=OnDisconnected;transport.Message+=Receive;
            lastReceived=Now;nextPing=Now+.1;
            clockSynchronized=IsHost;pingPending=false;
        }
        void OnConnected()
        {
            lastReceived=Now;
            if(!IsHost)Control(new GhostMessage{Kind=GhostMessageKind.Hello,Compatibility=compatibility,Nonce=nonce});
        }
        void Update()
        {
            if(transport!=null)transport.Pump();
            if(destroyed)return;
            if(State==MultiplayerState.Connecting&&Now-operationStarted>45){Fail("Bağlantı kurulamadı. Yeniden deneyebilirsin.");return;}
            if(transport==null)return;
            if(transport.RelayFailed){Fail("Oda bağlantısı sona erdi. Yeni bir oda oluşturabilirsin.");return;}
            if(HasPeer&&!IsHost&&!clockSynchronized&&State==MultiplayerState.Waiting&&Now-operationStarted>20)
            {Fail("Bağlantı gecikmesi başlangıcı eşleştirmeye yetmedi. Bağlantını kontrol edip yeniden katıl.");return;}
            if(transport.Connected&&Now-lastReceived>12){OnDisconnected();return;}
            if(HasPeer&&Now>=nextPing&&(!pingPending||Now-pingSent>=2.25))
            {
                nextPing=Now+1;pingNonce=NewId();pingSent=Now;pingPending=true;
                Control(new GhostMessage{Kind=GhostMessageKind.Ping,Nonce=pingNonce,EchoTime=pingSent});
            }
            if(State==MultiplayerState.Countdown&&HostClock>=startAt)BeginRace();
            if(State==MultiplayerState.Racing||State==MultiplayerState.Finished)
            {
                if(Now>=nextSend&&HasPeer)
                {
                    nextSend=Now+.05;var snapshot=Capture();
                    transport?.Send(new GhostMessage{Kind=GhostMessageKind.Snapshot,Snapshot=snapshot},false);
                    if(snapshot.Pose==GhostPose.Dead)localDead=true;
                }
                if(State==MultiplayerState.Racing&&localDead&&remoteDead)
                {LocalReady=RemoteReady=false;SetState(MultiplayerState.Finished,"Koşu bitti. Birlikte yeniden koşabilirsiniz.");}
                if(ghost!=null)
                {
                    if(Now-lastSnapshotReceived<2&&buffer.TrySample(RaceTime,out var sample))ghost.Show(sample,Time.unscaledDeltaTime);
                    else ghost.Hide();
                }
            }
        }
        GhostSnapshot Capture()
        {
#if UNITY_EDITOR
            if(testPeer)
            {var s=testSnapshot;s.RunId=RunId;s.Sequence=++sequence;s.Time=RaceTime;return s;}
#endif
            var motor=game.Motor;
            GhostPose pose=game.State==RunState.Finished?GhostPose.Dead:game.State==RunState.Paused?GhostPose.Paused:
                motor.Sliding?GhostPose.Slide:motor.Flying?GhostPose.Flight:!motor.Grounded?GhostPose.Jump:GhostPose.Run;
            float phase=pose==GhostPose.Slide?game.Animation.SlidePosePhase:pose==GhostPose.Dead?game.Animation.DeathProgress:
                Mathf.Repeat(game.Animation.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime,1);
            return new GhostSnapshot{Sequence=++sequence,RunId=RunId,Time=RaceTime,Distance=game.Distance,
                LaneX=motor.transform.position.x,Height=motor.transform.position.y,Speed=game.State==RunState.Running?game.CurrentSpeed:0,Phase=phase,Pose=pose};
        }
        void Receive(GhostMessage message)
        {
            switch(message.Kind)
            {
                case GhostMessageKind.Hello:
                    if(!IsHost||HasPeer||State!=MultiplayerState.Waiting){RejectedMessages++;return;}
                    if(message.Compatibility!=compatibility){Control(new GhostMessage{Kind=GhostMessageKind.Leave,LeaveReason=2});transport?.DisconnectPeer();return;}
                    HasPeer=true;LocalReady=RemoteReady=readyOffered=false;nonce=message.Nonce;
                    if(!Control(new GhostMessage{Kind=GhostMessageKind.Welcome,Compatibility=compatibility,Nonce=nonce,RunId=RunId,Seed=SharedSeed,HostTime=Now}))return;
                    Status="Arkadaşın katıldı. Hazır olduğunda düğmeye dokun.";Notify();break;
                case GhostMessageKind.Welcome:
                    if(IsHost||HasPeer||State!=MultiplayerState.Connecting||message.Nonce!=nonce||message.Compatibility!=compatibility){RejectedMessages++;return;}
                    RunId=message.RunId;SharedSeed=message.Seed;clockOffset=message.HostTime-Now;bestRtt=double.MaxValue;operationStarted=Now;
                    HasPeer=true;LocalReady=RemoteReady=readyOffered=false;SetState(MultiplayerState.Waiting,"Bağlandın. İkiniz de hazır olunca başlarız.");break;
                case GhostMessageKind.Ready:
                    if(!HasPeer||message.RunId!=RunId||(State!=MultiplayerState.Waiting&&State!=MultiplayerState.Finished)){RejectedMessages++;return;}
                    RemoteReady=message.Ready;Notify();TryStart();break;
                case GhostMessageKind.Start:
                    if(IsHost||!HasPeer||!readyOffered||(State!=MultiplayerState.Waiting&&State!=MultiplayerState.Finished)||!GhostProtocol.IsNewerSequence(message.RunId,RunId))
                    {RejectedMessages++;return;}
                    if(message.HostTime-HostClock<-.5||message.HostTime-HostClock>10){Fail("Başlangıç eşleşmedi. Odayı yeniden oluşturun.");return;}
                    RunId=message.RunId;SharedSeed=message.Seed;startAt=message.HostTime;PrepareCountdown();break;
                case GhostMessageKind.Snapshot:
                    if(!HasPeer||(State!=MultiplayerState.Racing&&State!=MultiplayerState.Finished)||message.Snapshot.Time>RaceTime+1||!buffer.Push(message.Snapshot))
                    {RejectedMessages++;return;}
                    lastSnapshotReceived=Now;RemoteDistance=message.Snapshot.Distance;RemotePose=message.Snapshot.Pose;remoteDead|=RemotePose==GhostPose.Dead;ReceivedSnapshots++;break;
                case GhostMessageKind.Ping:
                    if(!HasPeer){RejectedMessages++;return;}
                    Control(new GhostMessage{Kind=GhostMessageKind.Pong,Nonce=message.Nonce,EchoTime=message.EchoTime,HostTime=IsHost?Now:HostClock});break;
                case GhostMessageKind.Pong:
                    if(!HasPeer||!pingPending||message.Nonce!=pingNonce||Math.Abs(message.EchoTime-pingSent)>.001){RejectedMessages++;return;}
                    // Keep one outstanding request until its reply or the 2.25 s
                    // timeout. Replacing its nonce every second rejected every
                    // legitimate 1–2 s RTT reply and permanently blocked readiness.
                    double rtt=Now-pingSent;pingPending=false;nextPing=Now+1;
                    if(rtt<0||rtt>2)return;PingMilliseconds=(float)(rtt*1000);
                    if(!IsHost&&rtt<bestRtt)
                    {
                        bestRtt=rtt;
                        // Freeze the race clock after countdown; later RTT samples
                        // must never make an already-running ghost travel backwards.
                        if(State==MultiplayerState.Waiting||State==MultiplayerState.Finished)clockOffset=message.HostTime-(pingSent+Now)*.5;
                    }
                    if(!IsHost&&!clockSynchronized)
                    {clockSynchronized=true;if(LocalReady&&Control(new GhostMessage{Kind=GhostMessageKind.Ready,RunId=RunId,Ready=true}))readyOffered=true;}
                    break;
                case GhostMessageKind.Leave:
                    if(!HasPeer&&!(!IsHost&&State==MultiplayerState.Connecting&&message.LeaveReason==2)){RejectedMessages++;return;}
                    if(message.RunId!=0&&message.RunId!=RunId){RejectedMessages++;return;}
                    if(message.LeaveReason==2){Fail("Oyun sürümleri farklı. İkiniz de aynı sürümü kullanın.");return;}
                    OnDisconnected();break;
                default:RejectedMessages++;return;
            }
            lastReceived=Now;
        }
        public void SetReady(bool value)
        {
            if(!HasPeer||(State!=MultiplayerState.Waiting&&State!=MultiplayerState.Finished))return;
            LocalReady=value;
            if(!IsHost&&!clockSynchronized){Notify();return;}
            if(!Control(new GhostMessage{Kind=GhostMessageKind.Ready,RunId=RunId,Ready=value}))return;
            // Host Start commits a previously offered readiness. An unready
            // request can cross that commit in transit, so retain the offer for
            // this lobby epoch. Leaving always cancels the local session.
            if(!IsHost&&value)readyOffered=true;
            Notify();TryStart();
        }
        void TryStart()
        {
            if(!IsHost||!HasPeer||!LocalReady||!RemoteReady||(State!=MultiplayerState.Waiting&&State!=MultiplayerState.Finished))return;
            uint next=unchecked(RunId+1);if(next==0)next=1;
            SharedSeed=unchecked((int)NewId());startAt=Now+3;
            if(!Control(new GhostMessage{Kind=GhostMessageKind.Start,RunId=next,Seed=SharedSeed,HostTime=startAt,StartDelay=3}))return;
            RunId=next;PrepareCountdown();
        }
        void PrepareCountdown()
        {
            buffer.Reset(RunId);sequence=0;localDead=remoteDead=readyOffered=false;RemoteDistance=0;RemotePose=GhostPose.Run;
            if(ghost!=null)ghost.Hide();
            if(game!=null)game.ReturnToMultiplayerLobby();
            SetState(MultiplayerState.Countdown,"Aynı parkur. İki koşucu. Hazır ol!");
        }
        void BeginRace()
        {
            if(!HasPeer){OnDisconnected();return;}
            if(game!=null)game.BeginMultiplayerRun(SharedSeed);
            nextSend=Now;
            SetState(MultiplayerState.Racing,"Rakibinle aynı parkurdasın.");
        }
        bool Control(GhostMessage message)
        {
            if(transport!=null&&transport.Send(message,true))return true;
            Fail("Bağlantı kesildi. Yeni bir oda açabilirsin.");return false;
        }
        void OnDisconnected()
        {
            if(!IsActive||disconnecting)return;
            disconnecting=true;
            try
            {
            if(ghost!=null)ghost.Hide();
            HasPeer=false;LocalReady=RemoteReady=readyOffered=false;localDead=remoteDead=false;
            if(IsHost&&transport!=null)
            {
                transport.DisconnectPeer();RunId=NewId();buffer.Reset(RunId);
                if(game!=null)game.ReturnToMultiplayerLobby();
                SetState(MultiplayerState.Waiting,"Arkadaşının bağlantısı kesildi. Aynı kodla yeniden katılabilir.");
            }
            else Fail("Oda bağlantısı kapandı. Arkadaşının koduyla tekrar katılabilirsin.");
            }
            finally{disconnecting=false;}
        }
        void Fail(string message)
        {
            ++generation;ReleaseTransport();ResetPeer();Error=message;
            if(game!=null&&game.State!=RunState.Menu)game.ReturnToMultiplayerLobby();
            SetState(MultiplayerState.Disconnected,message);
        }
        public void Leave()
        {
            ++generation;
            if(transport!=null&&transport.Connected)
            {transport.Send(new GhostMessage{Kind=GhostMessageKind.Leave,RunId=RunId,LeaveReason=0},true);transport.Pump();}
            ReleaseTransport();ResetPeer();JoinCode="";Error="";
            SetState(MultiplayerState.Offline,"Arkadaşınla aynı parkurda koş.");
        }
        void ResetPeer()
        {HasPeer=LocalReady=RemoteReady=localDead=remoteDead=clockSynchronized=pingPending=readyOffered=false;RemoteDistance=0;clockOffset=0;bestRtt=double.MaxValue;buffer.Reset(0);if(ghost!=null)ghost.Hide();}
        void ReleaseTransport()
        {
            if(transport==null)return;
            transport.ConnectedEvent-=OnConnected;transport.DisconnectedEvent-=OnDisconnected;transport.Message-=Receive;
            transport.Dispose();transport=null;
        }
        void SetState(MultiplayerState value,string label){State=value;Status=label;Notify();}
        void Notify(){Changed?.Invoke();}
        void OnDestroy(){destroyed=true;++generation;ReleaseTransport();}
        void OnApplicationQuit(){destroyed=true;++generation;ReleaseTransport();}
        void OnApplicationPause(bool value)
        {if(value&&State==MultiplayerState.Countdown){Leave();if(game!=null)game.ReturnToMultiplayerLobby();}}

#if UNITY_EDITOR
        bool testPeer;GhostSnapshot testSnapshot;
        public void InitializeTestPeer(ulong profileHash,string authProfile="")
        {testPeer=true;compatibility=profileHash;profile=authProfile;}
        public void SetSnapshotForTest(GhostSnapshot value){testSnapshot=value;}
        public ushort LocalPortForTest=>transport==null?(ushort)0:transport.LocalPortForTest;
        public void HostLoopbackForTest(ushort port)
        {
            Leave();IsHost=true;RunId=NewId();SharedSeed=17;nonce=NewId();Install(GhostTransport.HostLoopback(port));
            JoinCode="LOCAL";SetState(MultiplayerState.Waiting,"Yerel ağ denetimi");
        }
        public void JoinLoopbackForTest(ushort port)
        {
            Leave();IsHost=false;nonce=NewId();operationStarted=Now;SetState(MultiplayerState.Connecting,"Yerel ağ denetimi");Install(GhostTransport.JoinLoopback(port));
        }
        public void InjectForTest(GhostMessage message){Receive(message);}
        public void DropTransportForTest(){ReleaseTransport();Fail("Denetim bağlantısı kesildi.");}
        public string AuthenticationProfileForTest=>services==null?"":services.GetAuthenticationService().Profile;
#endif
    }
}
