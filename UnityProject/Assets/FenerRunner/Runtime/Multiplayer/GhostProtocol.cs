using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace FenerRunner
{
    public enum GhostPose : byte { Run, Jump, Slide, Flight, Dead, Paused }
    public enum GhostMessageKind : byte { Hello=1, Welcome, Ready, Start, Snapshot, Leave, Ping, Pong }

    public struct GhostSnapshot
    {
        public uint Sequence,RunId;
        public double Time,Distance;
        public float LaneX,Height,Speed,Phase;
        public GhostPose Pose;
    }

    public struct GhostMessage
    {
        public GhostMessageKind Kind;
        public ulong Compatibility;
        public uint RunId,Nonce;
        public int Seed;
        public bool Ready;
        public float StartDelay;
        public double HostTime,EchoTime;
        public byte LeaveReason;
        public GhostSnapshot Snapshot;
        // Ping's originating local clock is echoed unchanged by Pong.
        public double Time { get=>EchoTime; set=>EchoTime=value; }
    }

    // Pure, allocation-free packet codec. Transport/session code authenticates the
    // two Relay peers and enforces host/client roles and reliable control ordering.
    // This compatibility fingerprint is not a cryptographic authentication token.
    public static class GhostProtocol
    {
        public const byte ProtocolVersion=1;
        public const int CourseRevision=14;
        public const int MaxPacketBytes=80;
        public const double MaximumRaceSeconds=86400d;
        public const float MaximumSpeed=42.01f;
        const uint Magic=0x314E5246; // ASCII FRN1, explicitly little endian.
        const int HeaderBytes=8;
        const double MaximumClockSeconds=1000000000d;
        [StructLayout(LayoutKind.Explicit)] struct FloatBits
        { [FieldOffset(0)] public float Value; [FieldOffset(0)] public uint Bits; }

        public static ulong ComputeCompatibility(string invariantProfile)
        {
            if(invariantProfile==null||invariantProfile.Length>4096)throw new ArgumentException("A bounded invariant rule profile is required.",nameof(invariantProfile));
            byte[] data=Encoding.UTF8.GetBytes("FenerRunner.Ghost/"+ProtocolVersion.ToString(CultureInfo.InvariantCulture)+"/Course/"+CourseRevision.ToString(CultureInfo.InvariantCulture)+"/"+invariantProfile);
            ulong hash=14695981039346656037UL;
            unchecked{foreach(byte value in data){hash^=value;hash*=1099511628211UL;}}
            return hash==0?1UL:hash;
        }
        public static bool IsNewerSequence(uint candidate,uint previous)
        {return unchecked((int)(candidate-previous))>0;}
        static bool Finite(double value){return !double.IsNaN(value)&&!double.IsInfinity(value);}
        static bool InRange(double value,double minimum,double maximum)
        {return Finite(value)&&value>=minimum&&value<=maximum;}
        static bool Clock(double value){return InRange(value,0,MaximumClockSeconds);}
        public static bool IsValidSnapshot(GhostSnapshot value)
        {
            return value.RunId!=0&&(byte)value.Pose<=(byte)GhostPose.Paused&&
                InRange(value.Time,0,MaximumRaceSeconds)&&InRange(value.Distance,0,4000000)&&
                value.Distance<=MaximumSpeed*value.Time+8d&&
                InRange(value.LaneX,-6,6)&&InRange(value.Height,-.5,8)&&
                InRange(value.Speed,0,MaximumSpeed)&&InRange(value.Phase,0,1);
        }
        static int PayloadBytes(GhostMessageKind kind)
        {
            switch(kind)
            {
                case GhostMessageKind.Hello:return 12;
                case GhostMessageKind.Welcome:return 28;
                case GhostMessageKind.Ready:return 5;
                case GhostMessageKind.Start:return 20;
                case GhostMessageKind.Snapshot:return 41;
                case GhostMessageKind.Leave:return 5;
                case GhostMessageKind.Ping:return 12;
                case GhostMessageKind.Pong:return 20;
                default:return -1;
            }
        }
        static bool Valid(GhostMessage value)
        {
            switch(value.Kind)
            {
                case GhostMessageKind.Hello:return value.Compatibility!=0&&value.Nonce!=0;
                case GhostMessageKind.Welcome:return value.Compatibility!=0&&value.RunId!=0&&value.Nonce!=0&&Clock(value.HostTime);
                case GhostMessageKind.Ready:return value.RunId!=0;
                case GhostMessageKind.Start:return value.RunId!=0&&Clock(value.HostTime)&&InRange(value.StartDelay,0,15);
                case GhostMessageKind.Snapshot:return IsValidSnapshot(value.Snapshot);
                case GhostMessageKind.Leave:return value.LeaveReason<=15;
                case GhostMessageKind.Ping:return value.Nonce!=0&&Clock(value.EchoTime);
                case GhostMessageKind.Pong:return value.Nonce!=0&&Clock(value.EchoTime)&&Clock(value.HostTime);
                default:return false;
            }
        }
        public static bool TryEncode(GhostMessage value,byte[] buffer,out int count)
        {
            count=0;int payload=PayloadBytes(value.Kind);
            if(payload<0||buffer==null||buffer.Length<HeaderBytes+payload||!Valid(value))return false;
            int p=0;Write32(buffer,ref p,Magic);buffer[p++]=ProtocolVersion;buffer[p++]=(byte)value.Kind;
            buffer[p++]=(byte)payload;buffer[p++]=0;
            switch(value.Kind)
            {
                case GhostMessageKind.Hello:
                    Write64(buffer,ref p,value.Compatibility);Write32(buffer,ref p,value.Nonce);break;
                case GhostMessageKind.Welcome:
                    Write64(buffer,ref p,value.Compatibility);Write32(buffer,ref p,value.RunId);Write32(buffer,ref p,unchecked((uint)value.Seed));
                    Write32(buffer,ref p,value.Nonce);WriteDouble(buffer,ref p,value.HostTime);break;
                case GhostMessageKind.Ready:
                    Write32(buffer,ref p,value.RunId);buffer[p++]=value.Ready?(byte)1:(byte)0;break;
                case GhostMessageKind.Start:
                    Write32(buffer,ref p,value.RunId);Write32(buffer,ref p,unchecked((uint)value.Seed));WriteDouble(buffer,ref p,value.HostTime);WriteFloat(buffer,ref p,value.StartDelay);break;
                case GhostMessageKind.Snapshot:
                    GhostSnapshot s=value.Snapshot;Write32(buffer,ref p,s.Sequence);Write32(buffer,ref p,s.RunId);
                    WriteDouble(buffer,ref p,s.Time);WriteDouble(buffer,ref p,s.Distance);WriteFloat(buffer,ref p,s.LaneX);WriteFloat(buffer,ref p,s.Height);
                    WriteFloat(buffer,ref p,s.Speed);WriteFloat(buffer,ref p,s.Phase);buffer[p++]=(byte)s.Pose;break;
                case GhostMessageKind.Leave:
                    Write32(buffer,ref p,value.RunId);buffer[p++]=value.LeaveReason;break;
                case GhostMessageKind.Ping:
                    Write32(buffer,ref p,value.Nonce);WriteDouble(buffer,ref p,value.EchoTime);break;
                case GhostMessageKind.Pong:
                    Write32(buffer,ref p,value.Nonce);WriteDouble(buffer,ref p,value.EchoTime);WriteDouble(buffer,ref p,value.HostTime);break;
            }
            count=p;return true;
        }
        public static bool TryDecode(byte[] buffer,int count,out GhostMessage value)
        {
            value=default;
            if(buffer==null||count<HeaderBytes||count>MaxPacketBytes||count>buffer.Length)return false;
            int p=0;if(Read32(buffer,ref p)!=Magic||buffer[p++]!=ProtocolVersion)return false;
            GhostMessageKind kind=(GhostMessageKind)buffer[p++];int declared=buffer[p++]|buffer[p++]<<8;
            int expected=PayloadBytes(kind);if(expected<0||declared!=expected||count!=HeaderBytes+expected)return false;
            var decoded=new GhostMessage{Kind=kind};
            switch(kind)
            {
                case GhostMessageKind.Hello:
                    decoded.Compatibility=Read64(buffer,ref p);decoded.Nonce=Read32(buffer,ref p);break;
                case GhostMessageKind.Welcome:
                    decoded.Compatibility=Read64(buffer,ref p);decoded.RunId=Read32(buffer,ref p);decoded.Seed=unchecked((int)Read32(buffer,ref p));
                    decoded.Nonce=Read32(buffer,ref p);decoded.HostTime=ReadDouble(buffer,ref p);break;
                case GhostMessageKind.Ready:
                    decoded.RunId=Read32(buffer,ref p);byte ready=buffer[p++];if(ready>1)return false;decoded.Ready=ready!=0;break;
                case GhostMessageKind.Start:
                    decoded.RunId=Read32(buffer,ref p);decoded.Seed=unchecked((int)Read32(buffer,ref p));decoded.HostTime=ReadDouble(buffer,ref p);decoded.StartDelay=ReadFloat(buffer,ref p);break;
                case GhostMessageKind.Snapshot:
                    decoded.Snapshot=new GhostSnapshot{Sequence=Read32(buffer,ref p),RunId=Read32(buffer,ref p),Time=ReadDouble(buffer,ref p),Distance=ReadDouble(buffer,ref p),
                        LaneX=ReadFloat(buffer,ref p),Height=ReadFloat(buffer,ref p),Speed=ReadFloat(buffer,ref p),Phase=ReadFloat(buffer,ref p),Pose=(GhostPose)buffer[p++]};
                    decoded.RunId=decoded.Snapshot.RunId;break;
                case GhostMessageKind.Leave:
                    decoded.RunId=Read32(buffer,ref p);decoded.LeaveReason=buffer[p++];break;
                case GhostMessageKind.Ping:
                    decoded.Nonce=Read32(buffer,ref p);decoded.EchoTime=ReadDouble(buffer,ref p);break;
                case GhostMessageKind.Pong:
                    decoded.Nonce=Read32(buffer,ref p);decoded.EchoTime=ReadDouble(buffer,ref p);decoded.HostTime=ReadDouble(buffer,ref p);break;
            }
            if(p!=count||!Valid(decoded))return false;value=decoded;return true;
        }
        static void Write32(byte[] b,ref int p,uint v){for(int i=0;i<4;i++){b[p++]=(byte)v;v>>=8;}}
        static uint Read32(byte[] b,ref int p){uint v=0;for(int i=0;i<4;i++)v|=(uint)b[p++]<<(8*i);return v;}
        static void Write64(byte[] b,ref int p,ulong v){for(int i=0;i<8;i++){b[p++]=(byte)v;v>>=8;}}
        static ulong Read64(byte[] b,ref int p){ulong v=0;for(int i=0;i<8;i++)v|=(ulong)b[p++]<<(8*i);return v;}
        static void WriteFloat(byte[] b,ref int p,float v){Write32(b,ref p,new FloatBits{Value=v}.Bits);}
        static float ReadFloat(byte[] b,ref int p){return new FloatBits{Bits=Read32(b,ref p)}.Value;}
        static void WriteDouble(byte[] b,ref int p,double v){Write64(b,ref p,unchecked((ulong)BitConverter.DoubleToInt64Bits(v)));}
        static double ReadDouble(byte[] b,ref int p){return BitConverter.Int64BitsToDouble(unchecked((long)Read64(b,ref p)));}
    }

    public sealed class GhostJitterBuffer
    {
        public const int Capacity=32;
        public const double InterpolationDelay=.12,MaximumExtrapolation=.15;
        readonly GhostSnapshot[] samples=new GhostSnapshot[Capacity];
        public uint RunId{get;private set;}
        public int Count{get;private set;}
        public int RejectedCount{get;private set;}
        public double LatestTime=>Count>0?samples[Count-1].Time:0;
        public void Reset(uint runId){RunId=runId;Count=RejectedCount=0;}
        public bool Push(GhostSnapshot value)
        {
            if(RunId==0||value.RunId!=RunId||!GhostProtocol.IsValidSnapshot(value))return Reject();
            if(Count>0)
            {
                GhostSnapshot previous=samples[Count-1];double dt=value.Time-previous.Time,travel=value.Distance-previous.Distance;
                if(!GhostProtocol.IsNewerSequence(value.Sequence,previous.Sequence)||dt<0||travel<-.001||
                    (dt<=.0000001&&Math.Abs(travel)>.001)||travel>GhostProtocol.MaximumSpeed*dt+.35||
                    (previous.Pose==GhostPose.Dead&&value.Pose!=GhostPose.Dead))return Reject();
            }
            if(Count==Capacity){Array.Copy(samples,1,samples,0,Capacity-1);Count--;}
            samples[Count++]=value;return true;
        }
        bool Reject(){RejectedCount++;return false;}
        public bool TrySample(double raceTime,out GhostSnapshot result)
        {
            result=default;if(Count==0||double.IsNaN(raceTime)||double.IsInfinity(raceTime)||raceTime<0)return false;
            double target=raceTime-InterpolationDelay;
            if(target<=samples[0].Time){result=samples[0];return true;}
            for(int i=1;i<Count;i++)
            {
                GhostSnapshot b=samples[i];if(target>b.Time)continue;GhostSnapshot a=samples[i-1];
                double span=b.Time-a.Time;if(span<=.0000001){result=b;return true;}
                double t=Math.Max(0,Math.Min(1,(target-a.Time)/span));result=a;result.Time=target;
                result.Distance=a.Distance+(b.Distance-a.Distance)*t;
                result.LaneX=Lerp(a.LaneX,b.LaneX,t);result.Height=Lerp(a.Height,b.Height,t);result.Speed=Lerp(a.Speed,b.Speed,t);
                if(a.Pose==b.Pose)result.Phase=Phase(a.Phase,b.Phase,t,a.Pose==GhostPose.Run||a.Pose==GhostPose.Flight);
                if(t>=1){result.Pose=b.Pose;result.Phase=b.Phase;result.Sequence=b.Sequence;}
                return true;
            }
            result=samples[Count-1];
            if(result.Pose!=GhostPose.Dead&&result.Pose!=GhostPose.Paused)
            {
                double extra=Math.Max(0,Math.Min(MaximumExtrapolation,target-result.Time));
                result.Distance+=result.Speed*extra;result.Time+=extra;
            }
            return true;
        }
        static float Lerp(float a,float b,double t){return (float)(a+(b-a)*t);}
        static float Phase(float a,float b,double t,bool looping)
        {
            if(!looping)return Lerp(a,b,t);
            double delta=b-a;if(delta<-.5)delta+=1;else if(delta>.5)delta-=1;
            double phase=a+delta*t;return (float)(phase-Math.Floor(phase));
        }
    }
}
