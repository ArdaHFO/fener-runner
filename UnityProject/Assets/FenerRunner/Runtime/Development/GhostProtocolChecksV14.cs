#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class GhostProtocolChecksV14 : MonoBehaviour
    {
        const string Flag="FenerRunner.GhostProtocolChecksV14";
        [Serializable] sealed class Report
        {
            public bool passed;public string utc,unityVersion;public string[] checks,errors;
            public int controlKinds,randomSnapshots=5000,malformedPackets=10000,courseSeeds=128,courseRowsPerSeed=256;
            public string scope="Pure production v1 packet codec and bounded jitter buffer exercised inside Unity Editor. Known wire bytes, round trips, malformed/truncated data, finite ranges, epoch/order, sequence rollover, interpolation/extrapolation, terminal death and shared-seed course generation. This does not establish Relay connectivity, two-phone execution, network security or physical-device timing.";
        }
        readonly List<string> checks=new List<string>(),errors=new List<string>();readonly Report report=new Report();
        readonly byte[] bytes=new byte[GhostProtocol.MaxPacketBytes];
        [MenuItem("Fener Runner/95 - Verify ghost protocol v14")]
        public static void StartChecks()
        {if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("V14 ghost protocol checks").AddComponent<GhostProtocolChecksV14>();}}
        void Start()
        {
            try{CodecCases();CompatibilityCases();InvalidCases();BufferCases();CourseCases();}
            catch(Exception error){errors.Add(error.ToString());}
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.checks=checks.ToArray();report.errors=errors.ToArray();
            Directory.CreateDirectory("../Validation");File.WriteAllText(ReviewEvidence.FileFor(14,"../Validation/unity_ghost_protocol_v14.json"),JsonUtility.ToJson(report,true));
            Debug.Log("Ghost protocol v14 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        static GhostSnapshot Snapshot(uint sequence=1,uint run=7,double time=1,double distance=20)
        {return new GhostSnapshot{Sequence=sequence,RunId=run,Time=time,Distance=distance,LaneX=0,Height=.02f,Speed=20,Phase=.25f,Pose=GhostPose.Run};}
        static bool Equal(GhostSnapshot a,GhostSnapshot b)
        {return a.Sequence==b.Sequence&&a.RunId==b.RunId&&a.Time==b.Time&&a.Distance==b.Distance&&a.LaneX==b.LaneX&&a.Height==b.Height&&a.Speed==b.Speed&&a.Phase==b.Phase&&a.Pose==b.Pose;}
        GhostMessage RoundTrip(GhostMessage original,out bool good)
        {good=GhostProtocol.TryEncode(original,bytes,out int length)&&GhostProtocol.TryDecode(bytes,length,out _);GhostProtocol.TryDecode(bytes,length,out GhostMessage decoded);return decoded;}
        void CodecCases()
        {
            var hello=new GhostMessage{Kind=GhostMessageKind.Hello,Compatibility=0x0102030405060708UL,Nonce=0x11223344};
            GhostMessage decoded=RoundTrip(hello,out bool valid);
            byte[] expected={0x46,0x52,0x4e,0x31,1,1,12,0,8,7,6,5,4,3,2,1,0x44,0x33,0x22,0x11};bool golden=valid;
            for(int i=0;i<expected.Length;i++)golden&=bytes[i]==expected[i];
            Check(golden&&decoded.Compatibility==hello.Compatibility&&decoded.Nonce==hello.Nonce,"Hello matches independently specified little-endian wire bytes");
            var controls=new[]{
                hello,
                new GhostMessage{Kind=GhostMessageKind.Welcome,Compatibility=hello.Compatibility,RunId=7,Seed=int.MinValue,Nonce=hello.Nonce,HostTime=123.125},
                new GhostMessage{Kind=GhostMessageKind.Ready,RunId=7,Ready=true},
                new GhostMessage{Kind=GhostMessageKind.Start,RunId=7,Seed=-1907,HostTime=126.125,StartDelay=3},
                new GhostMessage{Kind=GhostMessageKind.Leave,RunId=7,LeaveReason=3},
                new GhostMessage{Kind=GhostMessageKind.Ping,Nonce=15,Time=67.25},
                new GhostMessage{Kind=GhostMessageKind.Pong,Nonce=15,EchoTime=67.25,HostTime=122.5}};
            bool controlRoundTrips=true,truncation=true,oversize=true;
            foreach(var control in controls)
            {
                decoded=RoundTrip(control,out valid);controlRoundTrips&=valid&&decoded.Kind==control.Kind&&decoded.RunId==control.RunId&&
                    decoded.Compatibility==control.Compatibility&&decoded.Nonce==control.Nonce&&decoded.Seed==control.Seed&&decoded.Ready==control.Ready&&
                    decoded.HostTime==control.HostTime&&decoded.EchoTime==control.EchoTime&&decoded.StartDelay==control.StartDelay&&decoded.LeaveReason==control.LeaveReason;
                GhostProtocol.TryEncode(control,bytes,out int count);
                for(int i=0;i<count;i++)truncation&=!GhostProtocol.TryDecode(bytes,i,out _);
                oversize&=!GhostProtocol.TryDecode(bytes,count+1,out _);
            }
            report.controlKinds=controls.Length;
            Check(controlRoundTrips,"Handshake, host start, ready, leave and clock messages retain all declared fields");
            Check(truncation&&oversize,"Every control truncation and extra trailing byte is rejected");
            var random=new System.Random(14001);bool all=true;int length=0;
            for(int i=0;i<report.randomSnapshots;i++)
            {
                double time=random.NextDouble()*7200;
                var s=new GhostSnapshot{Sequence=unchecked((uint)random.Next())*2,RunId=(uint)random.Next(1,int.MaxValue),Time=time,Distance=time*random.NextDouble()*40,
                    LaneX=(float)(random.NextDouble()*6.6-3.3),Height=(float)(random.NextDouble()*5),Speed=(float)(random.NextDouble()*42),Phase=(float)random.NextDouble(),Pose=(GhostPose)random.Next(6)};
                var message=new GhostMessage{Kind=GhostMessageKind.Snapshot,Snapshot=s};
                bool encoded=GhostProtocol.TryEncode(message,bytes,out length);bool read=GhostProtocol.TryDecode(bytes,length,out decoded);
                all&=encoded&&read&&Equal(s,decoded.Snapshot)&&decoded.RunId==s.RunId;
            }
            Check(all&&length==49,"Five thousand varied snapshots round-trip exactly in 49 bytes");
            GhostProtocol.TryEncode(new GhostMessage{Kind=GhostMessageKind.Snapshot,Snapshot=Snapshot()},bytes,out length);
            bool everyCut=true;for(int i=0;i<length;i++)everyCut&=!GhostProtocol.TryDecode(bytes,i,out _);
            Check(everyCut,"All 49 truncated snapshot lengths are rejected");
            CultureInfo before=CultureInfo.CurrentCulture;ulong hash=GhostProtocol.ComputeCompatibility("lane=3.3;cap=42;rules=v14");
            try{CultureInfo.CurrentCulture=new CultureInfo("tr-TR");Check(hash==GhostProtocol.ComputeCompatibility("lane=3.3;cap=42;rules=v14")&&hash!=GhostProtocol.ComputeCompatibility("lane=3.4;cap=42;rules=v14"),"Compatibility is culture-independent and detects a changed rule profile");}
            finally{CultureInfo.CurrentCulture=before;}
        }
        void CompatibilityCases()
        {
            // Use a private settings object: the regression must never mutate
            // the authored course asset or production multiplayer state.
            var settings=ScriptableObject.CreateInstance<RunnerSettings>();
            var game=FindFirstObjectByType<RunnerGame>();
            if(game!=null)JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(game.Settings),settings);
            CultureInfo before=CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
                ulong current=RunnerMultiplayer.CompatibilityFor(settings);
                ulong oldRules=GhostProtocol.ComputeCompatibility("v14-mps1.2.1-utp2.6.0|"+
                    JsonUtility.ToJson(settings)+"|focus1.15|slide1.5|cap42|coins25|shared-course");
                Check(current!=oldRules,"Current double-action rules reject the exact pre-v16 compatibility profile with identical settings");
                ulong previousContacts=GhostProtocol.ComputeCompatibility("v16-double-jump-chained-slide-mps1.2.1-utp2.6.0|"+
                    JsonUtility.ToJson(settings)+"|focus1.15|slide1.5|cap42|coins25|shared-course");
                Check(current!=previousContacts,"Mesh-accurate obstacle rules reject the pre-v17 broad-box contact profile with identical settings");
                settings.laneSpacing+=.1f;
                ulong changed=RunnerMultiplayer.CompatibilityFor(settings);
                Check(changed!=current,"Production compatibility still distinguishes changed course settings under double-action rules");
                CultureInfo.CurrentCulture=new CultureInfo("tr-TR");
                Check(RunnerMultiplayer.CompatibilityFor(settings)==changed,"Production settings serialization and compatibility remain invariant under Turkish numeric culture");
            }
            finally
            {
                CultureInfo.CurrentCulture=before;
                DestroyImmediate(settings);
            }
        }
        void InvalidCases()
        {
            bool rejected=true;
            foreach(double bad in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity,-1d})
            {
                var s=Snapshot();s.Time=bad;rejected&=!GhostProtocol.TryEncode(new GhostMessage{Kind=GhostMessageKind.Snapshot,Snapshot=s},bytes,out _);
                s=Snapshot();s.Distance=bad;rejected&=!GhostProtocol.IsValidSnapshot(s);
            }
            foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            {
                var s=Snapshot();s.LaneX=bad;rejected&=!GhostProtocol.IsValidSnapshot(s);s=Snapshot();s.Height=bad;rejected&=!GhostProtocol.IsValidSnapshot(s);
                s=Snapshot();s.Speed=bad;rejected&=!GhostProtocol.IsValidSnapshot(s);s=Snapshot();s.Phase=bad;rejected&=!GhostProtocol.IsValidSnapshot(s);
            }
            var invalid=Snapshot();invalid.RunId=0;rejected&=!GhostProtocol.IsValidSnapshot(invalid);
            invalid=Snapshot();invalid.Speed=43;rejected&=!GhostProtocol.IsValidSnapshot(invalid);invalid=Snapshot();invalid.Phase=1.01f;rejected&=!GhostProtocol.IsValidSnapshot(invalid);
            invalid=Snapshot();invalid.LaneX=6.1f;rejected&=!GhostProtocol.IsValidSnapshot(invalid);invalid=Snapshot();invalid.Height=8.1f;rejected&=!GhostProtocol.IsValidSnapshot(invalid);
            invalid=Snapshot();invalid.Distance=1000;rejected&=!GhostProtocol.IsValidSnapshot(invalid);invalid=Snapshot();invalid.Pose=(GhostPose)6;rejected&=!GhostProtocol.IsValidSnapshot(invalid);
            Check(rejected,"Nonfinite values, impossible positions/speeds, invalid phase/pose and zero race ID are rejected before encoding");
            var message=new GhostMessage{Kind=GhostMessageKind.Snapshot,Snapshot=Snapshot()};
            GhostProtocol.TryEncode(message,bytes,out int length);byte[] original=(byte[])bytes.Clone();
            bool headers=true;
            foreach(int at in new[]{0,4,5,6,7}){Array.Copy(original,bytes,bytes.Length);bytes[at]=255;headers&=!GhostProtocol.TryDecode(bytes,length,out _);}
            Check(headers,"Wrong magic, protocol, kind and declared lengths are rejected");
            bool body=true;
            // IEEE little-endian NaN bit patterns bypass the encoder intentionally.
            foreach(int at in new[]{32,36,40,44})
            {Array.Copy(original,bytes,bytes.Length);bytes[at]=0;bytes[at+1]=0;bytes[at+2]=0xc0;bytes[at+3]=0x7f;body&=!GhostProtocol.TryDecode(bytes,length,out _);}
            foreach(int at in new[]{16,24})
            {Array.Copy(original,bytes,bytes.Length);for(int i=0;i<8;i++)bytes[at+i]=0;bytes[at+6]=0xf8;bytes[at+7]=0x7f;body&=!GhostProtocol.TryDecode(bytes,length,out _);}
            Array.Copy(original,bytes,bytes.Length);bytes[48]=255;body&=!GhostProtocol.TryDecode(bytes,length,out _);
            Check(body,"Hostile wire NaNs and unknown pose are rejected after decoding");
            GhostProtocol.TryEncode(new GhostMessage{Kind=GhostMessageKind.Ready,RunId=7,Ready=true},bytes,out length);bytes[12]=2;
            Check(!GhostProtocol.TryDecode(bytes,length,out _),"Ready accepts only canonical zero or one");
            Check(!GhostProtocol.TryDecode(null,0,out _)&&!GhostProtocol.TryDecode(bytes,-1,out _)&&!GhostProtocol.TryDecode(bytes,81,out _)&&
                !GhostProtocol.TryEncode(message,new byte[48],out int count)&&count==0,"Null, negative, oversized and insufficient buffers fail safely");
            var random=new System.Random(14999);bool total=true;
            for(int i=0;i<report.malformedPackets;i++)
            {
                int size=random.Next(0,100);byte[] packet=new byte[size];random.NextBytes(packet);
                try{if(GhostProtocol.TryDecode(packet,size,out GhostMessage value))total&=(byte)value.Kind>=(byte)GhostMessageKind.Hello&&(byte)value.Kind<=(byte)GhostMessageKind.Pong;}
                catch{total=false;break;}
            }
            Check(total,"Ten thousand bounded random packets cannot throw from the production decoder");
        }
        void BufferCases()
        {
            var buffer=new GhostJitterBuffer();
            Check(!buffer.Push(Snapshot())&&!buffer.TrySample(1,out _),"An uninitialized race cannot accept or display snapshots");buffer.Reset(7);
            Check(buffer.Push(Snapshot())&&!buffer.Push(Snapshot())&&!buffer.Push(Snapshot(2,8)),"First snapshot is accepted while duplicates and other race epochs are rejected");
            Check(!buffer.Push(Snapshot(2,7,.9,20))&&!buffer.Push(Snapshot(2,7,1.1,19))&&!buffer.Push(Snapshot(2,7,1.01,25)),"Backward time, backward distance and implausible speed jumps are rejected");
            buffer.Reset(7);var a=Snapshot(10,7,1,20);a.Phase=.9f;var b=Snapshot(11,7,2,40);b.LaneX=3;b.Height=2;b.Phase=.1f;
            buffer.Push(a);buffer.Push(b);bool sampled=buffer.TrySample(1.62,out GhostSnapshot middle);
            Check(sampled&&Math.Abs(middle.Distance-30)<.000001&&Math.Abs(middle.LaneX-1.5f)<.00001&&Math.Abs(middle.Height-1.01f)<.00001,
                "A 120 ms presentation delay yields exact midpoint distance, lane and height interpolation");
            Check(middle.Phase<.01f||middle.Phase>.99f,"Looping run phase crosses zero without rewinding through half a cycle");
            buffer.TrySample(100,out GhostSnapshot frozen);buffer.TrySample(101,out GhostSnapshot later);
            Check(Math.Abs(frozen.Distance-43)<.000001&&frozen.Distance==later.Distance&&Math.Abs(frozen.Time-2.15)<.000001,"Loss extrapolation stops after 150 ms and then freezes");
            Check(!buffer.TrySample(double.NaN,out _)&&!buffer.TrySample(-1,out _),"Invalid presentation clocks are rejected");
            foreach(GhostPose pose in new[]{GhostPose.Paused,GhostPose.Dead})
            {
                buffer.Reset(7);var stopped=Snapshot();stopped.Pose=pose;buffer.Push(stopped);buffer.TrySample(100,out GhostSnapshot still);
                Check(still.Distance==stopped.Distance&&still.Time==stopped.Time,pose+" never extrapolates forward even if last reported speed was nonzero");
            }
            Check(!buffer.Push(Snapshot(2,7,1.1,20)),"A dead race cannot resurrect from a later running packet");
            buffer.Reset(8);Check(buffer.Push(Snapshot(1,8,0,0))&&buffer.Count==1,"An explicit new race reset clears death and historical samples");
            buffer.Reset(7);bool wrap=buffer.Push(Snapshot(uint.MaxValue,7,1,20))&&buffer.Push(Snapshot(0,7,1.1,22))&&
                !buffer.Push(Snapshot(uint.MaxValue,7,1.2,24));
            Check(wrap&&!GhostProtocol.IsNewerSequence(0x80000000,0),"Sequence rollover accepts the new packet and rejects stale or ambiguous half-range ordering");
            buffer.Reset(7);bool bounded=true;for(uint i=0;i<200;i++)bounded&=buffer.Push(Snapshot(i,7,i*.05,i));
            Check(bounded&&buffer.Count==GhostJitterBuffer.Capacity&&Math.Abs(buffer.LatestTime-9.95)<.000001,"Two hundred snapshots retain the fixed 32-sample buffer capacity");
        }
        void CourseCases()
        {
            bool identical=true;var fingerprints=new HashSet<ulong>();
            for(int seed=-64;seed<64;seed++)
            {
                var a=new CoursePatternGenerator(seed);var b=new CoursePatternGenerator(seed);ulong hash=14695981039346656037UL;
                for(int row=0;row<report.courseRowsPerSeed;row++)
                {
                    PatternRow x=a.Next(),y=b.Next();
                    identical&=x.Distance==y.Distance&&x.Gap==y.Gap&&x.Lane==y.Lane&&x.Left==y.Left&&x.Centre==y.Centre&&x.Right==y.Right&&x.Action==y.Action;
                    unchecked{hash^=(ulong)BitConverter.DoubleToInt64Bits(x.Distance);hash*=1099511628211UL;hash^=(uint)((int)x.Left+4*(int)x.Centre+16*(int)x.Right+64*(x.Lane+1));hash*=1099511628211UL;}
                }
                fingerprints.Add(hash);
            }
            Check(identical,"128 shared seeds reproduce 256 production course rows independently of render timing");
            Check(fingerprints.Count==report.courseSeeds,"Different shared seeds retain genuine course variation");
        }
        void Check(bool ok,string label){checks.Add((ok?"PASS ":"FAIL ")+label);if(!ok)errors.Add(label);}
    }
}
#endif
