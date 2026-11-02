#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class AudioChecksV7:MonoBehaviour
    {
        [Serializable] sealed class ClipSignal
        {
            public string name,loadState;
            public int channels,frequency,samples,measuredWindows,audibleSourceWindows,audibleListenerWindows;
            public bool pcmRead;
            public float duration,pcmRms,pcmPeak,sourceMaximumWindowRms,listenerMaximumWindowRms,listenerPeak;
        }
        [Serializable] sealed class Report
        {
            public bool passed,realPreferencesPreserved;
            public bool editorMasterMuted,listenerPaused;
            public string utc,unityVersion;
            public string[] checks,errors;
            public ClipSignal[] voiceSignals;
            public float scheduledChatterSeconds,observedActiveSeconds,scheduledRepeatSeconds,observedRepeatSeconds,observedQuietGap;
            public float listenerVolume,mutedListenerMaximumRms,pausedListenerMaximumRms,fatalListenerMaximumRms;
            public int enabledListeners,outputSampleRate;
            public int actualPickupKinds,powerRequests,powerStarts,powerCompletions,chatterStarts,fatalStarts,maximumPending,audioSourcesBefore,audioSourcesAfter;
            public string scope="Real Unity Play Mode AudioSource playback, actual pickup collisions, bounded voice scheduling and persisted-data preservation. Natural 1.2–2.2 second first and 6–10 second repeat chatter plus forced-due contention cases. Normal pickup bursts complete; mute, reset and fatal priority intentionally cancel stale speech. Defensive over-capacity bursts start the newest power immediately. All seven supplied voices measured through decoded PCM, actual AudioSource output, and isolated AudioListener master output. The output measurements prove Unity mixer signal, not the physical loudspeaker or OS volume.";
        }
        const string Flag="FenerRunner.AudioChecksV7";
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        readonly HashSet<PowerupKind> collectedKinds=new HashSet<PowerupKind>();
        readonly Dictionary<int,ClipSignal> signals=new Dictionary<int,ClipSignal>();
        readonly float[] sourceSamples=new float[2048],listenerSamples=new float[2048];
        Report report=new Report();RunnerGame game;RunnerPowerups powers;StadiumSound sound;RunWallet isolatedWallet;
        bool background,originalMuted,prepared,oldMuteExists;
        int oldMute,oldScore,oldBalance;float oldBest;

        [MenuItem("Fener Runner/26 - Verify audible voice output and frequent commentary")]
        public static void StartChecks()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before audio checks.");
            SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Flag,false))return;
            SessionState.SetBool(Flag,false);new GameObject("Audio v7 integration checks").AddComponent<AudioChecksV7>();
        }
        IEnumerator Start()
        {
            background=Application.runInBackground;Application.runInBackground=true;Application.logMessageReceived+=Log;
            oldMuteExists=PlayerPrefs.HasKey("FenerRunner.Muted");oldMute=PlayerPrefs.GetInt("FenerRunner.Muted",0);
            oldScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);oldBest=PlayerPrefs.GetFloat("FenerRunner.Best",0);
            oldBalance=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0);
            game=FindFirstObjectByType<RunnerGame>();
            if(game==null){Check(false,"RunnerGame exists");Finish();yield break;}
            game.AutomatedTest=true;powers=game.Powerups;sound=game.GetComponent<StadiumSound>();
            isolatedWallet=game.UseWalletForTest("FenerRunner.Test.AudioV7."+DateTime.UtcNow.Ticks+".");
            originalMuted=sound.Muted;prepared=true;sound.UseAudioPreferencesForTest("FenerRunner.Test.AudioV7Mix."+DateTime.UtcNow.Ticks+".");sound.SetMutedForTest(false);
            sound.IsolateVoiceForTest(true);
            report.audioSourcesBefore=game.GetComponents<AudioSource>().Length;
            Check(sound.HasUserVoices&&sound.HasRecordedCrowd,"Seven supplied voice clips and the real crowd recording are assigned");
            report.editorMasterMuted=EditorUtility.audioMasterMute;report.listenerPaused=AudioListener.pause;
            report.listenerVolume=AudioListener.volume;report.outputSampleRate=AudioSettings.outputSampleRate;
            foreach(var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))if(listener.isActiveAndEnabled)report.enabledListeners++;
            Check(!report.editorMasterMuted&&!report.listenerPaused&&report.listenerVolume>.95f&&report.enabledListeners==1,
                "The real Editor mixer is unmuted, with one active listener and full master volume");
            Check(sound.VoiceSource.spatialBlend==0&&sound.VoiceSource.priority==0&&sound.VoiceSource.volume>=.99f&&!sound.VoiceSource.mute,
                "Speech uses an audible, highest-priority 2D source independent of camera distance");
            // Unity starts output-history recording on the first call; warm both buffers
            // before the shortest supplied line. All other game sources are isolated.
            SampleOutput(out _,out _,out _);

            game.ReturnToMenu();int chatter=sound.ChatterPlayCount;sound.SetChatterDueForTest(0);
            yield return Wait(.15f);
            Check(sound.ChatterPlayCount==chatter,"Ambient commentary cannot start in the menu");
            int requests=sound.PowerVoiceRequestCount,starts=sound.PowerVoicePlayCount;
            sound.Powerup();yield return Wait(.06f);
            Check(game.State==RunState.Menu&&sound.PowerVoiceRequestCount==requests+1&&sound.PowerVoicePlayCount==starts+1&&sound.VoiceSource.isPlaying,
                "The existing Powerup API also plays a workshop-purchase voice in the menu");

            BeginRun(false);report.scheduledChatterSeconds=sound.LastChatterInterval;
            Check(report.scheduledChatterSeconds>=1.2f&&report.scheduledChatterSeconds<=2.2f,"A fresh run schedules its first natural chatter in the 1.2–2.2 second range");
            chatter=sound.ChatterPlayCount;float activeStart=Time.time;
            yield return Wait(.4f);game.Pause();yield return null;
            float frozenDelay=sound.ChatterDelayRemaining;
            yield return Wait(.4f);
            Check(Mathf.Abs(sound.ChatterDelayRemaining-frozenDelay)<.001f&&sound.ChatterPlayCount==chatter,"Pause freezes the active-run chatter countdown");
            game.Resume();float deadline=Time.realtimeSinceStartup+report.scheduledChatterSeconds+3;
            while(sound.ChatterPlayCount==chatter&&Time.realtimeSinceStartup<deadline)yield return null;
            report.observedActiveSeconds=Time.time-activeStart;
            Check(sound.ChatterPlayCount==chatter+1&&sound.CurrentVoiceKind==VoiceKind.Chatter&&sound.VoiceSource.isPlaying,
                "Natural scheduled commentary starts an actual supplied AudioSource clip during running");
            Check(report.observedActiveSeconds>=report.scheduledChatterSeconds-.15f&&report.observedActiveSeconds<=report.scheduledChatterSeconds+.6f,
                "The natural delay uses active game time rather than paused wall time");
            Check(sound.LastChatterInterval>=6&&sound.LastChatterInterval<=10,"Subsequent commentary schedules a 6–10 second random interval");
            report.scheduledRepeatSeconds=sound.LastChatterInterval;float repeatStart=Time.time;
            deadline=Time.realtimeSinceStartup+report.scheduledRepeatSeconds+2;
            while(sound.ChatterPlayCount<chatter+2&&Time.realtimeSinceStartup<deadline)
            {SampleOutput(out _,out _,out _);yield return null;}
            report.observedRepeatSeconds=Time.time-repeatStart;
            Check(sound.ChatterPlayCount==chatter+2&&report.observedRepeatSeconds>=report.scheduledRepeatSeconds-.15f&&
                report.observedRepeatSeconds<=report.scheduledRepeatSeconds+.6f,
                "A second natural spoken line actually starts after 6–10 active seconds");

            powers.Enabled=true;powers.IsolatePickupsForTest();
            requests=sound.PowerVoiceRequestCount;starts=sound.PowerVoicePlayCount;int interruptedChatter=sound.ChatterInterruptedCount;
            PlacePower(PowerupKind.Dash);yield return null;yield return null;
            Check(sound.PowerVoiceRequestCount==requests+1&&sound.PowerVoicePlayCount==starts+1&&sound.CurrentVoiceKind==VoiceKind.Power&&sound.VoiceSource.isPlaying,
                "An actual power pickup immediately takes priority over ambient commentary");
            Check(sound.ChatterInterruptedCount==interruptedChatter+1,"Pickup priority interrupts only the lower-priority chatter");
            game.Pause();yield return null;float voiceTime=sound.VoiceSource.time;int pending=sound.PendingPowerVoices;frozenDelay=sound.ChatterDelayRemaining;
            yield return Wait(.35f);
            Check(Mathf.Abs(sound.VoiceSource.time-voiceTime)<.03f&&sound.PendingPowerVoices==pending&&Mathf.Abs(sound.ChatterDelayRemaining-frozenDelay)<.001f,
                "Pause freezes the current spoken clip, pending voices and chatter timer");
            float signalDeadline=Time.realtimeSinceStartup+.12f;
            while(Time.realtimeSinceStartup<signalDeadline)
            {SampleOutput(out _,out float listenerRms,out _);report.pausedListenerMaximumRms=Mathf.Max(report.pausedListenerMaximumRms,listenerRms);yield return null;}
            Check(report.pausedListenerMaximumRms<.0001f,"Paused speech produces silence at the isolated master output");
            int completed=sound.PowerVoiceCompletedCount;game.Resume();yield return Wait(.08f);
            Check(sound.VoiceSource.time>voiceTime||sound.PowerVoiceCompletedCount>completed,"Resume continues the paused spoken clip");
            chatter=sound.ChatterPlayCount;int interruptions=sound.PowerVoiceInterruptedCount;sound.SetChatterDueForTest(0);
            deadline=Time.realtimeSinceStartup+5;
            while(sound.CurrentVoiceKind==VoiceKind.Power&&Time.realtimeSinceStartup<deadline)yield return null;
            float quietStart=Time.time;yield return Wait(1.75f);
            Check(sound.ChatterPlayCount==chatter&&sound.PowerVoiceInterruptedCount==interruptions,"Due chatter neither cuts a power voice nor immediately follows its final word");
            deadline=Time.realtimeSinceStartup+1.5f;
            while(sound.ChatterPlayCount==chatter&&Time.realtimeSinceStartup<deadline)yield return null;
            report.observedQuietGap=Time.time-quietStart;
            Check(sound.ChatterPlayCount==chatter+1&&report.observedQuietGap>=1.9f,"A due ambient line waits at least two quiet active seconds after a power voice");

            BeginRun(true);collectedKinds.Clear();powers.Collected+=Collected;
            requests=sound.PowerVoiceRequestCount;starts=sound.PowerVoicePlayCount;completed=sound.PowerVoiceCompletedCount;
            interruptions=sound.PowerVoiceInterruptedCount;chatter=sound.ChatterPlayCount;int overflow=sound.QueueOverflowCount;
            var pickupKinds=(PowerupKind[])Enum.GetValues(typeof(PowerupKind));int expectedPickups=pickupKinds.Length;
            foreach(PowerupKind kind in pickupKinds)PlacePower(kind);
            yield return Wait(.06f);
            Check(collectedKinds.Count==expectedPickups&&sound.PowerVoiceRequestCount==requests+expectedPickups,"Every defined powerup kind creates exactly one voice request on actual collection");
            Check(sound.PowerVoicePlayCount==starts+1&&sound.PendingPowerVoices==expectedPickups-1,"Simultaneous pickups use one active voice and a FIFO instead of cutting each other off");
            sound.SetChatterDueForTest(0);deadline=Time.realtimeSinceStartup+expectedPickups*3.6f+2f;
            while(sound.PowerVoiceCompletedCount<completed+expectedPickups&&Time.realtimeSinceStartup<deadline)
            {report.maximumPending=Mathf.Max(report.maximumPending,sound.PendingPowerVoices);yield return null;}
            Check(sound.PowerVoicePlayCount==starts+expectedPickups&&sound.PowerVoiceCompletedCount==completed+expectedPickups&&sound.PendingPowerVoices==0,
                "All actual pickup voices start and finish exactly once");
            Check(sound.PowerVoiceInterruptedCount==interruptions&&sound.ChatterPlayCount==chatter&&sound.QueueOverflowCount==overflow,
                "Queued pickup voices complete without interruption by due chatter");
            report.actualPickupKinds=collectedKinds.Count;powers.Collected-=Collected;

            BeginRun(false);requests=sound.PowerVoiceRequestCount;starts=sound.PowerVoicePlayCount;completed=sound.PowerVoiceCompletedCount;
            for(int i=0;i<7;i++)sound.Powerup();
            Check(sound.PowerVoiceRequestCount==requests+7&&sound.PendingPowerVoices==6,"A burst of seven power events is accepted without an expanding queue");
            var heard=new HashSet<int>();int last=-1,observedStarts=starts;bool distinctNeighbours=true;
            float outputWarmUntil=0;
            deadline=Time.realtimeSinceStartup+16;
            while(sound.PowerVoiceCompletedCount<completed+7&&Time.realtimeSinceStartup<deadline)
            {
                if(sound.PowerVoicePlayCount!=observedStarts)
                {
                    heard.Add(sound.LastVoiceIndex);distinctNeighbours&=last!=sound.LastVoiceIndex;last=sound.LastVoiceIndex;observedStarts=sound.PowerVoicePlayCount;
                    signals[last]=ReadSignal(sound.VoiceSource.clip);outputWarmUntil=Time.realtimeSinceStartup+.065f;
                }
                SampleOutput(out float sourceRms,out float listenerRms,out float listenerPeak);
                if(sound.CurrentVoiceKind==VoiceKind.Power&&signals.TryGetValue(sound.LastVoiceIndex,out ClipSignal signal)&&Time.realtimeSinceStartup>=outputWarmUntil)
                {
                    signal.measuredWindows++;if(sourceRms>.01f)signal.audibleSourceWindows++;if(listenerRms>.01f)signal.audibleListenerWindows++;
                    signal.sourceMaximumWindowRms=Mathf.Max(signal.sourceMaximumWindowRms,sourceRms);
                    signal.listenerMaximumWindowRms=Mathf.Max(signal.listenerMaximumWindowRms,listenerRms);
                    signal.listenerPeak=Mathf.Max(signal.listenerPeak,listenerPeak);
                }
                report.maximumPending=Mathf.Max(report.maximumPending,sound.PendingPowerVoices);yield return null;
            }
            Check(heard.Count==7&&distinctNeighbours&&sound.PowerVoiceCompletedCount==completed+7,"The full seven-clip shuffle bag plays sequentially without adjacent repeats");
            report.voiceSignals=new ClipSignal[signals.Count];int signalIndex=0;
            for(int i=0;i<7;i++)
            {
                if(!signals.TryGetValue(i,out ClipSignal signal)){Check(false,"ses"+(i+1)+" was available for signal measurement");continue;}
                report.voiceSignals[signalIndex++]=signal;
                Check(signal.pcmRead&&signal.pcmRms>.025f&&signal.pcmPeak>.1f&&signal.pcmPeak<=1.001f,
                    signal.name+" contains non-silent decoded Unity PCM with valid amplitude");
                Check(signal.sourceMaximumWindowRms>.02f&&signal.audibleSourceWindows>=2,
                    signal.name+" produces actual audible-level AudioSource output over multiple windows");
                Check(signal.listenerMaximumWindowRms>.01f&&signal.audibleListenerWindows>=2&&signal.listenerPeak>.05f,
                    signal.name+" reaches the isolated AudioListener master output over multiple windows");
            }
            Check(report.maximumPending<=StadiumSound.PowerVoiceQueueCapacity,"Pending voice storage remains within the fixed sixteen-entry capacity");
            int voiceCalls=sound.VoicePlayCount;sound.Pickup(false);sound.Pickup(true);
            Check(sound.VoicePlayCount==voiceCalls,"Ordinary coins remain silent");

            BeginRun(false);requests=sound.PowerVoiceRequestCount;starts=sound.PowerVoicePlayCount;overflow=sound.QueueOverflowCount;
            for(int i=0;i<32;i++)sound.Powerup();
            report.maximumPending=Mathf.Max(report.maximumPending,sound.PendingPowerVoices);
            Check(sound.PendingPowerVoices==StadiumSound.PowerVoiceQueueCapacity&&sound.QueueOverflowCount>overflow,
                "A defensive 32-event burst still uses only the fixed sixteen pending slots");
            Check(sound.PowerVoiceRequestCount==requests+32&&sound.PowerVoicePlayCount-starts+sound.PendingPowerVoices==32,
                "Every over-capacity request is represented by a started voice or a pending entry");

            BeginRun(false);sound.Powerup();sound.Powerup();sound.SetMutedForTest(true);voiceCalls=sound.VoicePlayCount;
            Check(sound.Muted&&sound.VoiceSource.mute&&!sound.VoiceSource.isPlaying&&sound.PendingPowerVoices==0,"Mute silences the active voice and discards its stale backlog");
            int suppressed=sound.SuppressedPowerVoiceCount;sound.Powerup();sound.SetChatterDueForTest(0);yield return Wait(.15f);
            Check(sound.VoicePlayCount==voiceCalls&&sound.SuppressedPowerVoiceCount==suppressed+1&&sound.PendingPowerVoices==0,"Muted pickups and due chatter cannot accumulate delayed speech");
            signalDeadline=Time.realtimeSinceStartup+.15f;
            while(Time.realtimeSinceStartup<signalDeadline)
            {SampleOutput(out _,out float listenerRms,out _);report.mutedListenerMaximumRms=Mathf.Max(report.mutedListenerMaximumRms,listenerRms);yield return null;}
            Check(report.mutedListenerMaximumRms<.0001f,"An explicit user mute produces silence at the isolated master output");
            sound.SetMutedForTest(false);yield return Wait(.1f);
            Check(sound.VoicePlayCount==voiceCalls&&sound.ChatterDelayRemaining>1f,"Unmute starts a fresh chatter interval without replaying old pickups");

            sound.Powerup();sound.Powerup();requests=sound.PowerVoiceRequestCount;game.Restart();game.Obstacles.SetActive(false);game.Tokens.Enabled=false;powers.Enabled=false;
            Check(sound.PendingPowerVoices==0&&!sound.VoiceSource.isPlaying&&sound.CurrentVoiceKind==VoiceKind.None&&sound.PowerVoiceRequestCount==requests,
                "Restart clears speech and queues while keeping monotonic request diagnostics");
            sound.Powerup();sound.Powerup();game.Pause();yield return null;pending=sound.PendingPowerVoices;voiceCalls=sound.VoicePlayCount;voiceTime=sound.VoiceSource.time;
            yield return Wait(.3f);
            Check(sound.PendingPowerVoices==pending&&sound.VoicePlayCount==voiceCalls&&Mathf.Abs(sound.VoiceSource.time-voiceTime)<.03f,"A queued power cannot start or disappear while the game is paused");
            game.Resume();

            BeginRun(false);sound.Powerup();sound.Powerup();int fatal=sound.FatalVoicePlayCount;chatter=sound.ChatterPlayCount;
            game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder,game.Motor.transform.position.z+.6f,game.Motor.Lane);
            yield return Wait(.18f);
            Check(game.State==RunState.Finished&&sound.FatalVoicePlayCount==fatal+1&&sound.CurrentVoiceKind==VoiceKind.Fatal&&sound.LastVoiceIndex==0&&sound.VoiceSource.clip.name=="ses1",
                "A real fatal curly-gate collision immediately selects ses1 over pending powers");
            Check(sound.PendingPowerVoices==0&&sound.VoiceSource.isPlaying,"Fatal speech clears the queue and plays despite the frozen game clock");
            voiceCalls=sound.VoicePlayCount;voiceTime=sound.VoiceSource.time;sound.SetChatterDueForTest(0);sound.Powerup();yield return Wait(.2f);
            Check(sound.VoicePlayCount==voiceCalls&&sound.ChatterPlayCount==chatter&&sound.VoiceSource.time>voiceTime,"Finished runs reject chatter and new powers without interrupting the fatal line");
            signalDeadline=Time.realtimeSinceStartup+.35f;
            while(Time.realtimeSinceStartup<signalDeadline)
            {SampleOutput(out _,out float listenerRms,out _);report.fatalListenerMaximumRms=Mathf.Max(report.fatalListenerMaximumRms,listenerRms);yield return null;}
            Check(report.fatalListenerMaximumRms>.01f,"Fatal ses1 reaches the actual master output while gameplay time is frozen");
            game.ReturnToMenu();
            Check(!sound.VoiceSource.isPlaying&&sound.PendingPowerVoices==0,"Returning to the menu ends the old fatal line cleanly");

            report.audioSourcesAfter=game.GetComponents<AudioSource>().Length;
            Check(report.audioSourcesBefore==StadiumSound.SourceCount&&report.audioSourcesBefore==report.audioSourcesAfter,"Voice, lane and music activity retain the fixed six AudioSources");
            Check(EditorUtility.audioMasterMute==report.editorMasterMuted&&AudioListener.pause==report.listenerPaused&&AudioListener.volume==report.listenerVolume,
                "The audio checks preserve Editor mute and listener settings");
            report.realPreferencesPreserved=PlayerPrefs.HasKey("FenerRunner.Muted")==oldMuteExists&&PlayerPrefs.GetInt("FenerRunner.Muted",0)==oldMute&&
                PlayerPrefs.GetInt("FenerRunner.BestScore",0)==oldScore&&PlayerPrefs.GetFloat("FenerRunner.Best",0)==oldBest&&PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0)==oldBalance;
            Check(report.realPreferencesPreserved,"Real mute, best-score, distance and wallet preferences are unchanged");
            report.powerRequests=sound.PowerVoiceRequestCount;report.powerStarts=sound.PowerVoicePlayCount;report.powerCompletions=sound.PowerVoiceCompletedCount;
            report.chatterStarts=sound.ChatterPlayCount;report.fatalStarts=sound.FatalVoicePlayCount;
            Finish();
        }
        void BeginRun(bool enablePickups)
        {
            game.Restart();game.Obstacles.SetActive(false);game.Tokens.Enabled=false;
            powers.Enabled=enablePickups;if(enablePickups)powers.IsolatePickupsForTest();
        }
        void PlacePower(PowerupKind kind){powers.PlaceForTest(kind,game.Motor.transform.position+new Vector3(0,1.1f,.55f));}
        void Collected(PowerupKind kind){collectedKinds.Add(kind);}
        static ClipSignal ReadSignal(AudioClip clip)
        {
            clip.LoadAudioData();var pcm=new float[clip.samples*clip.channels];
            var result=new ClipSignal{name=clip.name,loadState=clip.loadState.ToString(),duration=clip.length,channels=clip.channels,frequency=clip.frequency,samples=clip.samples};
            result.pcmRead=clip.GetData(pcm,0);result.pcmRms=Rms(pcm,out float peak);result.pcmPeak=peak;return result;
        }
        void SampleOutput(out float sourceRms,out float listenerRms,out float listenerPeak)
        {
            sound.VoiceSource.GetOutputData(sourceSamples,0);AudioListener.GetOutputData(listenerSamples,0);
            sourceRms=Rms(sourceSamples,out _);listenerRms=Rms(listenerSamples,out listenerPeak);
        }
        static float Rms(float[] samples,out float peak)
        {
            double squares=0;peak=0;foreach(float sample in samples){squares+=(double)sample*sample;peak=Mathf.Max(peak,Mathf.Abs(sample));}
            return (float)Math.Sqrt(squares/samples.Length);
        }
        static WaitForSecondsRealtime Wait(float seconds)=>new WaitForSecondsRealtime(seconds);
        void Check(bool ok,string label){if(ok)checks.Add(label);else errors.Add(label);}
        void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text);}
        void Finish()
        {
            report.passed=errors.Count==0;report.checks=checks.ToArray();report.errors=errors.ToArray();report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;
            Directory.CreateDirectory("../Validation");File.WriteAllText("../Validation/unity_audio_v7.json",JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Audio v7 checks "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;Application.runInBackground=background;
            if(powers!=null)powers.Collected-=Collected;
            if(prepared&&sound!=null){sound.IsolateVoiceForTest(false);sound.RestoreAudioPreferencesForTest();sound.SetMutedForTest(originalMuted);}
            if(isolatedWallet!=null)isolatedWallet.DeleteIsolatedTestData();
        }
    }
}
#endif
