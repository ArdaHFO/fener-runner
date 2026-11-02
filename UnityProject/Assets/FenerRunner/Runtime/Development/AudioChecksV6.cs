#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class AudioChecksV6:MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed,realPreferencesPreserved;
            public string utc,unityVersion;
            public string[] checks,errors;
            public float scheduledChatterSeconds,observedActiveSeconds,observedQuietGap;
            public int actualPickupKinds,powerRequests,powerStarts,powerCompletions,chatterStarts,fatalStarts,maximumPending,audioSourcesBefore,audioSourcesAfter;
            public string scope="Real Unity Play Mode AudioSource playback, actual pickup collisions, bounded voice scheduling and persisted-data preservation. One natural 14–24 second chatter interval plus forced-due contention cases. Normal pickup bursts complete; mute, reset and fatal priority intentionally cancel stale speech. Defensive over-capacity bursts start the newest power immediately. No auditory quality, loudspeaker or device performance claim.";
        }
        const string Flag="FenerRunner.AudioChecksV6";
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        readonly HashSet<PowerupKind> collectedKinds=new HashSet<PowerupKind>();
        Report report=new Report();RunnerGame game;RunnerPowerups powers;StadiumSound sound;RunWallet isolatedWallet;
        bool background,originalMuted,prepared,oldMuteExists;
        int oldMute,oldScore,oldBalance;float oldBest;

        [MenuItem("Fener Runner/21 - Verify voice scheduling and pickup audio")]
        public static void StartChecks()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before audio checks.");
            SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Flag,false))return;
            SessionState.SetBool(Flag,false);new GameObject("Audio v6 integration checks").AddComponent<AudioChecksV6>();
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
            isolatedWallet=game.UseWalletForTest("FenerRunner.Test.AudioV6."+DateTime.UtcNow.Ticks+".");
            originalMuted=sound.Muted;prepared=true;sound.UseAudioPreferencesForTest("FenerRunner.Test.AudioV6Mix."+DateTime.UtcNow.Ticks+".");sound.SetMutedForTest(false);
            report.audioSourcesBefore=game.GetComponents<AudioSource>().Length;
            Check(sound.HasUserVoices&&sound.HasRecordedCrowd,"Seven supplied voice clips and the real crowd recording are assigned");

            game.ReturnToMenu();int chatter=sound.ChatterPlayCount;sound.SetChatterDueForTest(0);
            yield return Wait(.15f);
            Check(sound.ChatterPlayCount==chatter,"Ambient commentary cannot start in the menu");
            int requests=sound.PowerVoiceRequestCount,starts=sound.PowerVoicePlayCount;
            sound.Powerup();yield return Wait(.06f);
            Check(game.State==RunState.Menu&&sound.PowerVoiceRequestCount==requests+1&&sound.PowerVoicePlayCount==starts+1&&sound.VoiceSource.isPlaying,
                "The existing Powerup API also plays a workshop-purchase voice in the menu");

            BeginRun(false);report.scheduledChatterSeconds=sound.LastChatterInterval;
            Check(report.scheduledChatterSeconds>=14&&report.scheduledChatterSeconds<=24,"A fresh run schedules its natural chatter in the 14–24 second range");
            chatter=sound.ChatterPlayCount;float activeStart=Time.time;
            yield return Wait(1f);game.Pause();yield return null;
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
            Check(sound.LastChatterInterval>=14&&sound.LastChatterInterval<=24,"Commentary schedules another bounded random interval");

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
            int completed=sound.PowerVoiceCompletedCount;game.Resume();yield return Wait(.08f);
            Check(sound.VoiceSource.time>voiceTime||sound.PowerVoiceCompletedCount>completed,"Resume continues the paused spoken clip");
            chatter=sound.ChatterPlayCount;int interruptions=sound.PowerVoiceInterruptedCount;sound.SetChatterDueForTest(0);
            deadline=Time.realtimeSinceStartup+5;
            while(sound.CurrentVoiceKind==VoiceKind.Power&&Time.realtimeSinceStartup<deadline)yield return null;
            float quietStart=Time.time;yield return Wait(4.7f);
            Check(sound.ChatterPlayCount==chatter&&sound.PowerVoiceInterruptedCount==interruptions,"Due chatter neither cuts a power voice nor immediately follows its final word");
            deadline=Time.realtimeSinceStartup+1.5f;
            while(sound.ChatterPlayCount==chatter&&Time.realtimeSinceStartup<deadline)yield return null;
            report.observedQuietGap=Time.time-quietStart;
            Check(sound.ChatterPlayCount==chatter+1&&report.observedQuietGap>=4.9f,"A due ambient line waits at least five quiet active seconds after a power voice");

            BeginRun(true);collectedKinds.Clear();powers.Collected+=Collected;
            requests=sound.PowerVoiceRequestCount;starts=sound.PowerVoicePlayCount;completed=sound.PowerVoiceCompletedCount;
            interruptions=sound.PowerVoiceInterruptedCount;chatter=sound.ChatterPlayCount;int overflow=sound.QueueOverflowCount;
            var pickupKinds=(PowerupKind[])Enum.GetValues(typeof(PowerupKind));int expectedPickups=pickupKinds.Length;
            foreach(PowerupKind kind in pickupKinds)PlacePower(kind);
            yield return Wait(.06f);
            Check(collectedKinds.Count==expectedPickups&&sound.PowerVoiceRequestCount==requests+expectedPickups,"Every defined powerup kind creates exactly one voice request on actual collection");
            Check(sound.PowerVoicePlayCount==starts+1&&sound.PendingPowerVoices==expectedPickups-1,"Simultaneous pickups use one active voice and a FIFO instead of cutting each other off");
            sound.SetChatterDueForTest(0);deadline=Time.realtimeSinceStartup+24;
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
            deadline=Time.realtimeSinceStartup+16;
            while(sound.PowerVoiceCompletedCount<completed+7&&Time.realtimeSinceStartup<deadline)
            {
                if(sound.PowerVoicePlayCount!=observedStarts)
                {heard.Add(sound.LastVoiceIndex);distinctNeighbours&=last!=sound.LastVoiceIndex;last=sound.LastVoiceIndex;observedStarts=sound.PowerVoicePlayCount;}
                report.maximumPending=Mathf.Max(report.maximumPending,sound.PendingPowerVoices);yield return null;
            }
            Check(heard.Count==7&&distinctNeighbours&&sound.PowerVoiceCompletedCount==completed+7,"The full seven-clip shuffle bag plays sequentially without adjacent repeats");
            Check(report.maximumPending<=StadiumSound.PowerVoiceQueueCapacity,"Pending voice storage remains within the fixed eight-entry capacity");
            int voiceCalls=sound.VoicePlayCount;sound.Pickup(false);sound.Pickup(true);
            Check(sound.VoicePlayCount==voiceCalls,"Ordinary coins remain silent");

            BeginRun(false);requests=sound.PowerVoiceRequestCount;starts=sound.PowerVoicePlayCount;overflow=sound.QueueOverflowCount;
            for(int i=0;i<32;i++)sound.Powerup();
            report.maximumPending=Mathf.Max(report.maximumPending,sound.PendingPowerVoices);
            Check(sound.PendingPowerVoices==StadiumSound.PowerVoiceQueueCapacity&&sound.QueueOverflowCount>overflow,
                "A defensive 32-event burst still uses only the fixed eight pending slots");
            Check(sound.PowerVoiceRequestCount==requests+32&&sound.PowerVoicePlayCount-starts+sound.PendingPowerVoices==32,
                "Every over-capacity request is represented by a started voice or a pending entry");

            BeginRun(false);sound.Powerup();sound.Powerup();sound.SetMutedForTest(true);voiceCalls=sound.VoicePlayCount;
            Check(sound.Muted&&sound.VoiceSource.mute&&!sound.VoiceSource.isPlaying&&sound.PendingPowerVoices==0,"Mute silences the active voice and discards its stale backlog");
            int suppressed=sound.SuppressedPowerVoiceCount;sound.Powerup();sound.SetChatterDueForTest(0);yield return Wait(.15f);
            Check(sound.VoicePlayCount==voiceCalls&&sound.SuppressedPowerVoiceCount==suppressed+1&&sound.PendingPowerVoices==0,"Muted pickups and due chatter cannot accumulate delayed speech");
            sound.SetMutedForTest(false);yield return Wait(.1f);
            Check(sound.VoicePlayCount==voiceCalls&&sound.ChatterDelayRemaining>13f,"Unmute starts a fresh chatter interval without replaying old pickups");

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
            game.ReturnToMenu();
            Check(!sound.VoiceSource.isPlaying&&sound.PendingPowerVoices==0,"Returning to the menu ends the old fatal line cleanly");

            report.audioSourcesAfter=game.GetComponents<AudioSource>().Length;
            Check(report.audioSourcesBefore==report.audioSourcesAfter,"Voice activity never adds extra AudioSources");
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
            sound.SetRunMusicDueForTest(999);
            powers.Enabled=enablePickups;if(enablePickups)powers.IsolatePickupsForTest();
        }
        void PlacePower(PowerupKind kind){powers.PlaceForTest(kind,game.Motor.transform.position+new Vector3(0,1.1f,.55f));}
        void Collected(PowerupKind kind){collectedKinds.Add(kind);}
        static WaitForSecondsRealtime Wait(float seconds)=>new WaitForSecondsRealtime(seconds);
        void Check(bool ok,string label){if(ok)checks.Add(label);else errors.Add(label);}
        void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text);}
        void Finish()
        {
            report.passed=errors.Count==0;report.checks=checks.ToArray();report.errors=errors.ToArray();report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;
            Directory.CreateDirectory("../Validation");File.WriteAllText("../Validation/unity_audio_v6.json",JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Audio v6 checks "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;Application.runInBackground=background;
            if(powers!=null)powers.Collected-=Collected;
            if(prepared&&sound!=null){sound.RestoreAudioPreferencesForTest();sound.SetMutedForTest(originalMuted);}
            if(isolatedWallet!=null)isolatedWallet.DeleteIsolatedTestData();
        }
    }
}
#endif
