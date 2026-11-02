#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    // Native AudioSource clocks are independent of Time.timeScale. These cases
    // cover the menu-to-run tail and application lifecycle, not only run excerpts.
    public sealed class AudioLifecycleChecksV18 : MonoBehaviour
    {
        const string Flag="FenerRunner.AudioLifecycleChecksV18";
        [Serializable] sealed class Report
        {
            public bool passed,preferencesPreserved;
            public string utc,unityVersion;
            public string[] checks,errors;
            public float suspendedListenerPeak;
            public int sourceCount;
            public string scope="Actual scene AudioSource clocks and AudioListener output for immediate Play/Pause during menu ses8 fade, both focus/pause callback orders, menu/fatal voice suspension, manual resume, muted background recovery and reset while suspended. Uses isolated audio/wallet preferences. Does not certify physical phone speakers or platform callback delivery.";
        }
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        readonly float[] samples=new float[2048];
        RunnerGame game;StadiumSound sound;RunWallet wallet;
        bool oldBackground,oldEnabled,oldAutomated,oldTokens,oldPowers,oldObstacles,prepared,isolatedAudio;
        float oldScale,oldCapture;int oldRate;string preferences;
        readonly Report report=new Report();

        [MenuItem("Fener Runner/117 - Verify audio application lifecycle v18")]
        public static void StartChecks()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before audio lifecycle checks.");
            SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Flag,false))return;
            SessionState.SetBool(Flag,false);new GameObject("Audio application lifecycle checks v18").AddComponent<AudioLifecycleChecksV18>();
        }
        IEnumerator Start()
        {
            oldBackground=Application.runInBackground;oldScale=Time.timeScale;oldCapture=Time.captureDeltaTime;oldRate=Application.targetFrameRate;
            Application.runInBackground=true;Time.captureDeltaTime=1f/60;Application.targetFrameRate=60;
            Application.logMessageReceived+=Log;preferences=PreferenceSnapshot();prepared=true;
            var stack=new Stack<IEnumerator>();stack.Push(Scenarios());
            while(stack.Count>0)
            {
                bool more;object wait=null;
                try{more=stack.Peek().MoveNext();if(more)wait=stack.Peek().Current;}
                catch(Exception e){errors.Add(e.ToString());break;}
                if(!more){stack.Pop();continue;}
                if(wait is IEnumerator child){stack.Push(child);continue;}yield return wait;
            }
            if(game!=null){game.SetApplicationPauseForTest(false);game.SetApplicationFocusForTest(true);game.ReturnToMenu();}
            if(isolatedAudio&&sound!=null){sound.RestoreAudioPreferencesForTest();isolatedAudio=false;}
            report.preferencesPreserved=preferences==PreferenceSnapshot();Check(report.preferencesPreserved,"Real wallet, upgrades, records, mute and four audio levels remain unchanged");
            report.sourceCount=game==null?0:game.GetComponents<AudioSource>().Length;
            Check(report.sourceCount==StadiumSound.SourceCount,"Repeated pause, focus, death and reset keep the six-source audio budget");
            report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;
            report.checks=checks.ToArray();report.errors=errors.ToArray();report.passed=errors.Count==0;
            string path=ReviewEvidence.FileFor(18,"../Validation/unity_audio_lifecycle_v18.json");Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Audio lifecycle v18 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Scenarios()
        {
            game=FindFirstObjectByType<RunnerGame>();if(game==null)throw new InvalidOperationException("Actual Fener Runner scene required.");
            sound=game.GetComponent<StadiumSound>();oldEnabled=game.enabled;oldAutomated=game.AutomatedTest;
            oldTokens=game.Tokens.Enabled;oldPowers=game.Powerups.Enabled;oldObstacles=game.Obstacles.Enabled;
            game.AutomatedTest=true;game.enabled=false;game.ReturnToMenu();
            wallet=game.UseWalletForTest("FenerRunner.Test.AudioLifecycleV18."+DateTime.UtcNow.Ticks+".");
            sound.UseAudioPreferencesForTest("FenerRunner.Test.AudioLifecycleV18.Levels."+DateTime.UtcNow.Ticks+".");isolatedAudio=true;sound.SetMutedForTest(false);
            for(int i=0;i<4;i++)sound.SetVolume((AudioChannel)i,1);
            Check(!EditorUtility.audioMasterMute&&!AudioListener.pause&&AudioListener.volume>.95f,"Native Editor audio listener is enabled for output checks");

            // No rendered running frame occurs between menu audio and pause.
            yield return Wait(.65f);
            Check(sound.MenuMusicSource.isPlaying&&sound.MenuMusicSource.loop&&sound.MenuMusicSource.volume>.65f,"Supplied ses8 is genuinely playing in the menu before the immediate-pause regression");
            BeginRun();game.Pause();yield return null;
            float at=sound.MenuMusicSource.time;yield return Wait(.2f);
            Check(!sound.RunMusicActive&&!sound.MenuMusicSource.isPlaying&&Mathf.Abs(sound.MenuMusicSource.time-at)<.035f,"Immediate Play then Pause freezes the still-fading menu recording, even before any gameplay excerpt");
            game.Resume();yield return null;
            Check(game.State==RunState.Running&&sound.MenuMusicSource.isPlaying,"Manual resume continues the same paused menu tail before it fades away");
            yield return Wait(.6f);
            Check(!sound.MenuMusicSource.isPlaying&&!sound.RunMusicActive,"The resumed menu tail completes its fade and does not become an endless gameplay loop");

            for(int order=0;order<2;order++)
            {
                game.ReturnToMenu();yield return Wait(.65f);
                Check(sound.MenuMusicSource.isPlaying,"Menu sound is live before application suspension / order "+order);
                Suspend(order);yield return null;
                float music=sound.MenuMusicSource.time,crowd=sound.CrowdSource.time;
                ClearFirst(order);yield return Wait(.18f);
                Check(sound.ApplicationSuspended&&!sound.MenuMusicSource.isPlaying&&!sound.CrowdSource.isPlaying&&Mathf.Abs(sound.MenuMusicSource.time-music)<.035f&&Mathf.Abs(sound.CrowdSource.time-crowd)<.035f,
                    "Clearing only one lifecycle signal keeps menu music and ambience frozen / order "+order);
                yield return SampleSilence(.1f);
                ClearLast(order);yield return Wait(.1f);
                Check(!sound.ApplicationSuspended&&sound.MenuMusicSource.isPlaying&&sound.CrowdSource.isPlaying,"Clearing both signals restores the existing menu recording and ambience / order "+order);
                game.SetApplicationPauseForTest(false);game.SetApplicationFocusForTest(true);
                Check(!sound.ApplicationSuspended,"Repeated foreground notifications do not pause audio again / order "+order);
            }

            // A short lane sound and a longer power voice must remain paused even
            // after foreground callbacks, until the player explicitly resumes.
            BeginRun();sound.Powerup();game.HandleCommand(RunnerCommand.Right);yield return null;
            Check(sound.VoiceSource.isPlaying&&sound.LaneSource.isPlaying,"Real supplied power voice and ses3 lane clip begin before backgrounding");
            Suspend(0);yield return null;float voice=sound.VoiceSource.time,lane=sound.LaneSource.time;
            ClearFirst(0);ClearLast(0);yield return Wait(.16f);
            Check(game.State==RunState.Paused&&!sound.ApplicationSuspended&&!sound.VoiceSource.isPlaying&&!sound.LaneSource.isPlaying&&Mathf.Abs(sound.VoiceSource.time-voice)<.035f&&Mathf.Abs(sound.LaneSource.time-lane)<.035f,
                "Foreground return keeps gameplay voice and lane clip paused until explicit Resume");
            game.Resume();yield return null;
            Check(sound.VoiceSource.isPlaying&&sound.LaneSource.isPlaying,"Explicit Resume restores both paused gameplay clips without dropping them");

            // Fatal voices use real audio time while the death animation is unscaled.
            BeginRun();game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder,game.Motor.transform.position.z,game.Motor.Lane);game.AdvanceForTest(1f/60);
            Check(game.State==RunState.Finished&&sound.CurrentVoiceKind==VoiceKind.Fatal&&sound.LastVoiceIndex==0&&sound.VoiceSource.isPlaying,"Actual curly-obstacle collision starts the supplied ses1 fatal voice");
            Suspend(1);yield return null;voice=sound.VoiceSource.time;
            ClearFirst(1);yield return Wait(.18f);
            Check(sound.ApplicationSuspended&&!sound.VoiceSource.isPlaying&&Mathf.Abs(sound.VoiceSource.time-voice)<.035f,"Fatal voice clock remains frozen while either background signal remains");
            ClearLast(1);yield return null;
            Check(game.State==RunState.Finished&&!sound.ApplicationSuspended&&sound.VoiceSource.isPlaying,"Foreground resumes the existing fatal voice without restarting the run");

            game.ReturnToMenu();yield return Wait(.5f);Suspend(0);yield return null;
            sound.SetMutedForTest(true);ClearFirst(0);ClearLast(0);yield return Wait(.12f);
            bool muted=true;foreach(var source in game.GetComponents<AudioSource>())muted&=source.mute;
            Check(muted&&sound.Muted,"Mute selected during suspension survives audio recovery");yield return SampleSilence(.12f);sound.SetMutedForTest(false);

            BeginRun();sound.Powerup();yield return null;Suspend(1);yield return null;
            game.ReturnToMenu();yield return Wait(.1f);
            Check(sound.ApplicationSuspended&&!sound.MenuMusicSource.isPlaying&&!sound.VoiceSource.isPlaying,"A reset to menu while suspended cannot leak old voices or start menu audio in the background");
            ClearFirst(1);ClearLast(1);yield return Wait(.2f);
            Check(!sound.ApplicationSuspended&&game.State==RunState.Menu&&sound.MenuMusicSource.isPlaying&&sound.CurrentVoiceKind==VoiceKind.None&&!sound.VoiceSource.isPlaying,
                "Foreground after a suspended reset starts menu music without resurrecting the interrupted power voice");
        }
        void BeginRun()
        {
            game.SetApplicationPauseForTest(false);game.SetApplicationFocusForTest(true);
            game.Play();game.Obstacles.SetActive(false);game.Tokens.Enabled=false;game.Powerups.Enabled=false;
            sound.SetChatterDueForTest(999);sound.SetRunMusicDueForTest(999);
        }
        void Suspend(int order)
        {
            if(order==0){game.SetApplicationFocusForTest(false);game.SetApplicationPauseForTest(true);}
            else{game.SetApplicationPauseForTest(true);game.SetApplicationFocusForTest(false);}
        }
        void ClearFirst(int order){if(order==0)game.SetApplicationPauseForTest(false);else game.SetApplicationFocusForTest(true);}
        void ClearLast(int order){if(order==0)game.SetApplicationFocusForTest(true);else game.SetApplicationPauseForTest(false);}
        IEnumerator SampleSilence(float seconds)
        {
            // Flush the native output buffer once before measuring suspended audio.
            yield return Wait(.08f);float peak=0,until=Time.realtimeSinceStartup+seconds;
            while(Time.realtimeSinceStartup<until)
            {AudioListener.GetOutputData(samples,0);foreach(float value in samples)peak=Mathf.Max(peak,Mathf.Abs(value));yield return null;}
            report.suspendedListenerPeak=Mathf.Max(report.suspendedListenerPeak,peak);
            Check(peak<.0001f,"Native listener output is silent during suspension or mute (peak "+peak.ToString("G4")+")");
        }
        static WaitForSecondsRealtime Wait(float seconds)=>new WaitForSecondsRealtime(seconds);
        void Check(bool value,string message){if(value)checks.Add(message);else errors.Add(message);}
        void Log(string message,string trace,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<50)errors.Add(message);}
        static string PreferenceSnapshot()
        {
            var values=new List<string>();
            foreach(string key in new[]{RunWallet.DefaultPrefix+"Coins",RunWallet.DefaultPrefix+"Upgrade.0",RunWallet.DefaultPrefix+"Upgrade.1",RunWallet.DefaultPrefix+"Upgrade.2","FenerRunner.BestScore","FenerRunner.Muted"})
                values.Add(key+"="+PlayerPrefs.HasKey(key)+"/"+PlayerPrefs.GetInt(key,0));
            foreach(string key in new[]{"FenerRunner.Best",StadiumSound.DefaultAudioPrefix+"Master",StadiumSound.DefaultAudioPrefix+"Music",StadiumSound.DefaultAudioPrefix+"Voices",StadiumSound.DefaultAudioPrefix+"Effects"})
                values.Add(key+"="+PlayerPrefs.HasKey(key)+"/"+PlayerPrefs.GetFloat(key,0).ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            return string.Join("|",values);
        }
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;if(!prepared)return;
            if(isolatedAudio&&sound!=null)sound.RestoreAudioPreferencesForTest();
            if(game!=null){game.enabled=oldEnabled;game.AutomatedTest=oldAutomated;game.Tokens.Enabled=oldTokens;game.Powerups.Enabled=oldPowers;game.Obstacles.Enabled=oldObstacles;}
            if(wallet!=null)wallet.DeleteIsolatedTestData();Application.runInBackground=oldBackground;Time.timeScale=oldScale;Time.captureDeltaTime=oldCapture;Application.targetFrameRate=oldRate;
        }
    }
}
#endif
