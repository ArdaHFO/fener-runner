using System;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;

namespace FenerRunner
{
    // Main-thread owner of one UTP driver and at most one accepted race peer.
    // Authentication/allocation and session authority belong to RunnerMultiplayer.
    public sealed class GhostTransport:IDisposable
    {
        const int MaximumEventsPerPump=128,MaximumAcceptsPerPump=8;
        readonly byte[] sendBuffer=new byte[GhostProtocol.MaxPacketBytes];
        readonly byte[] receiveBuffer=new byte[GhostProtocol.MaxPacketBytes];
        NetworkDriver driver;
        NetworkConnection peer;
        NetworkPipeline controlPipeline,snapshotPipeline;
        bool host,connected,pumping,disposing,disconnectNeedsFlush,usesRelay;
        public bool IsCreated=>!disposing&&driver.IsCreated;
        public bool Connected=>IsCreated&&connected&&peer.IsCreated;
        public bool UsesRelay=>usesRelay;
        public RelayConnectionStatus RelayStatus=>IsCreated?driver.GetRelayConnectionStatus():RelayConnectionStatus.NotEstablished;
        public bool IsRelayBound=>IsCreated&&usesRelay&&RelayStatus==RelayConnectionStatus.Established;
        public bool RelayFailed=>IsCreated&&usesRelay&&RelayStatus==RelayConnectionStatus.AllocationInvalid;
        public int RejectedPackets {get;private set;}
        public int RejectedConnections {get;private set;}
        public event Action ConnectedEvent;
        public event Action DisconnectedEvent;
        public event Action<GhostMessage> Message;

        public static GhostTransport HostRelay(RelayServerData data)=>CreateRelay(data,true);
        public static GhostTransport JoinRelay(RelayServerData data)=>CreateRelay(data,false);
        static GhostTransport CreateRelay(RelayServerData data,bool isHost)
        {
            if(data.IsSecure==0)throw new ArgumentException("Ghost Relay requires secure allocation data (DTLS).",nameof(data));
            var result=new GhostTransport{usesRelay=true};var settings=Settings();
            try
            {
                settings.WithRelayParameters(ref data);
                result.CreateDriver(settings,isHost);
                result.RequireBind(NetworkEndpoint.AnyIpv4);
                if(isHost)result.RequireListen();
                else
                {
                    result.peer=result.driver.Connect();
                    if(!result.peer.IsCreated)throw new InvalidOperationException("Relay connection could not be created.");
                }
                return result;
            }
            catch{result.Dispose();throw;}
            finally{settings.Dispose();}
        }
#if UNITY_EDITOR
        // Native integration tests use loopback only; no direct-network host is
        // exposed by a production player build.
        public static GhostTransport HostLoopback(ushort port)=>CreateLoopback(port,true);
        public static GhostTransport JoinLoopback(ushort port)=>CreateLoopback(port,false);
        public ushort LocalPortForTest=>IsCreated?driver.GetLocalEndpoint().Port:(ushort)0;
        static GhostTransport CreateLoopback(ushort port,bool isHost)
        {
            // A host may ask the OS for a free ephemeral port. Selecting a random
            // high port can hit an occupied or Windows-reserved UDP range.
            // A joining peer still requires the host's actual assigned port.
            if(port==0&&!isHost)throw new ArgumentOutOfRangeException(nameof(port));
            var result=new GhostTransport();var settings=Settings();
            try
            {
                result.CreateDriver(settings,isHost);
                result.RequireBind(NetworkEndpoint.LoopbackIpv4.WithPort(isHost?port:(ushort)0));
                if(isHost)result.RequireListen();
                else
                {
                    result.peer=result.driver.Connect(NetworkEndpoint.LoopbackIpv4.WithPort(port));
                    if(!result.peer.IsCreated)throw new InvalidOperationException("Loopback connection could not be created.");
                }
                return result;
            }
            catch{result.Dispose();throw;}
            finally{settings.Dispose();}
        }
#endif
        static NetworkSettings Settings()
        {
            var settings=new NetworkSettings(Allocator.Temp);
            settings.WithNetworkConfigParameters(connectTimeoutMS:750,maxConnectAttempts:12,disconnectTimeoutMS:8000,
                heartbeatTimeoutMS:500,receiveQueueCapacity:128,sendQueueCapacity:128);
            return settings;
        }
        void CreateDriver(NetworkSettings settings,bool isHost)
        {
            host=isHost;driver=NetworkDriver.Create(settings);
            // Pipeline IDs must be created in identical order on both phones.
            controlPipeline=driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            snapshotPipeline=driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage));
        }
        void RequireBind(NetworkEndpoint endpoint)
        {int result=driver.Bind(endpoint);if(result!=0)throw new InvalidOperationException("Ghost transport bind failed ("+result+").");}
        void RequireListen()
        {int result=driver.Listen();if(result!=0)throw new InvalidOperationException("Ghost transport listen failed ("+result+").");}

        public void Pump()
        {
            if(!IsCreated||pumping)return;
            pumping=true;
            try
            {
                driver.ScheduleUpdate().Complete();disconnectNeedsFlush=false;
                // Accept may occur in the same frame as the previous peer's
                // disconnection. Retire that handle before considering a new one.
                if(peer.IsCreated&&driver.GetConnectionState(peer)==NetworkConnection.State.Disconnected)
                {ClearPeer(true);if(!IsCreated)return;}
                int budget=MaximumEventsPerPump;
                if(host)
                {
                    for(int i=0;i<MaximumAcceptsPerPump&&IsCreated&&budget>0;i++)
                    {
                        var candidate=driver.Accept();if(!candidate.IsCreated)break;budget--;
                        if(peer.IsCreated)
                        {driver.Disconnect(candidate);disconnectNeedsFlush=true;RejectedConnections++;continue;}
                        peer=candidate;connected=true;ConnectedEvent?.Invoke();
                        if(!IsCreated)return;
                    }
                }
                while(IsCreated&&budget-->0)
                {
                    var type=driver.PopEvent(out var connection,out var reader,out var pipeline);
                    if(type==NetworkEvent.Type.Empty)break;
                    if(connection!=peer)
                    {if(type==NetworkEvent.Type.Data)RejectedPackets++;continue;}
                    if(type==NetworkEvent.Type.Connect)
                    {
                        if(!connected){connected=true;ConnectedEvent?.Invoke();}
                        if(!IsCreated)return;
                    }
                    else if(type==NetworkEvent.Type.Disconnect)
                    {ClearPeer(true);if(!IsCreated)return;}
                    else if(type==NetworkEvent.Type.Data)
                    {
                        if(!connected||reader.Length<=0||reader.Length>receiveBuffer.Length||
                            (pipeline!=controlPipeline&&pipeline!=snapshotPipeline))
                        {RejectedPackets++;continue;}
                        int length=reader.Length;
                        for(int i=0;i<length;i++)receiveBuffer[i]=reader.ReadByte();
                        if(reader.HasFailedReads||!GhostProtocol.TryDecode(receiveBuffer,length,out var message))
                        {RejectedPackets++;continue;}
                        // Session control must not bypass reliable sequencing,
                        // and position streams must not block behind retransmits.
                        if(message.Kind==GhostMessageKind.Snapshot?pipeline!=snapshotPipeline:pipeline!=controlPipeline)
                        {RejectedPackets++;continue;}
                        Message?.Invoke(message);
                        // A subscriber may synchronously leave/dispose the room.
                        // No native driver/reader access is valid after that call.
                        if(!IsCreated)return;
                    }
                }
                if(IsCreated)driver.ScheduleFlushSend().Complete();
            }
            finally
            {
                pumping=false;
                if(IsCreated&&disconnectNeedsFlush)
                {driver.ScheduleUpdate().Complete();disconnectNeedsFlush=false;}
            }
        }

        public bool Send(GhostMessage message,bool reliable=true)
        {
            if(!Connected||!GhostProtocol.TryEncode(message,sendBuffer,out int count))return false;
            NetworkPipeline pipeline=reliable?controlPipeline:snapshotPipeline;
            if(driver.BeginSend(pipeline,peer,out var writer,count)!=0)return false;
            for(int i=0;i<count;i++)writer.WriteByte(sendBuffer[i]);
            if(writer.HasFailedWrites){driver.AbortSend(writer);return false;}
            return driver.EndSend(writer)>=0;
        }
        public void DisconnectPeer()
        {
            if(!IsCreated||!peer.IsCreated)return;
            driver.Disconnect(peer);disconnectNeedsFlush=true;
            ClearPeer(true);
            // The next Pump (or Dispose) completes the required disconnect update.
        }
        void ClearPeer(bool notify)
        {
            bool hadPeer=peer.IsCreated;peer=default;connected=false;
            if(notify&&hadPeer)DisconnectedEvent?.Invoke();
        }
        public void Dispose()
        {
            if(disposing)return;disposing=true;
            // Disposal deliberately does not raise session callbacks recursively.
            ConnectedEvent=null;DisconnectedEvent=null;Message=null;connected=false;
            if(driver.IsCreated)
            {
                try
                {
                    if(peer.IsCreated){driver.Disconnect(peer);disconnectNeedsFlush=true;}
                    if(disconnectNeedsFlush)driver.ScheduleUpdate().Complete();
                }
                finally{driver.Dispose();}
            }
            peer=default;driver=default;disconnectNeedsFlush=false;
        }
    }
}
