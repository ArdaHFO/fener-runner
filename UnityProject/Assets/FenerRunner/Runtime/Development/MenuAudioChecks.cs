#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class MenuAudioChecks:MonoBehaviour
    {
        const string Flag="FenerRunner.MenuAudioChecks";
        [Serializable] sealed class Report
        {
            public bool passed;
            public string utc,unityVersion;
            public string[] checks,errors;
            public float duration,sourcePeak,listenerPeak;
            public int observedLoops;
            public string scope="Actual Unity Play Mode menu/shop/run/pause transitions, streamed ses8 loop and Unity AudioSource/AudioListener output. Does not measure physical loudspeaker output. No production currency or records are changed.";
        }
        readonly Report report=new Report();
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        readonly float[] sourceSamples=new float[2048],listenerSamples=new float[2048];
        RunnerGame game;StadiumSound sound;AudioSource music;
        bool previousBackground,previousMute;
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register()
        {EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Flag,false))return;
            SessionState.SetBool(Flag,false);new GameObject("ses8 menu playback checks").AddComponent<MenuAudioChecks>();
        }
        IEnumerator Start()
        {
            previousBackground=Application.runInBackground;Application.runInBackground=true;
            Application.logMessageReceived+=Log;
            game=FindFirstObjectByType<RunnerGame>();sound=game.GetComponent<StadiumSound>();
            previousMute=sound.Muted;game.AutomatedTest=true;sound.UseAudioPreferencesForTest("FenerRunner.Test.MenuAudioMix."+DateTime.UtcNow.Ticks+".");
            var stack=new Stack<IEnumerator>();stack.Push(Scenarios());
            while(stack.Count>0)
            {
                bool more;object wait=null;
                try{more=stack.Peek().MoveNext();if(more)wait=stack.Peek().Current;}
                catch(Exception exception){errors.Add(exception.ToString());break;}
                if(!more){stack.Pop();continue;}
                if(wait is IEnumerator child){stack.Push(child);continue;}
                yield return wait;
            }
            game.ReturnToMenu();sound.RestoreAudioPreferencesForTest();sound.SetMutedForTest(previousMute);
            report.passed=errors.Count==0;report.checks=checks.ToArray();report.errors=errors.ToArray();
            report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;
            Directory.CreateDirectory("../Validation");File.WriteAllText("../Validation/unity_menu_audio_v9_1.json",JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;
            Debug.Log("ses8 menu audio "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));
            EditorApplication.isPlaying=false;
        }
        IEnumerator Scenarios()
        {
            int realBalance=game.WalletBalance,realScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);
            float realBest=PlayerPrefs.GetFloat("FenerRunner.Best",0);
            bool hadMute=PlayerPrefs.HasKey("FenerRunner.Muted");int mute=PlayerPrefs.GetInt("FenerRunner.Muted",0);
            game.Powerups.Enabled=false;game.Tokens.Enabled=false;game.ReturnToMenu();sound.SetMutedForTest(false);
            music=sound.MenuMusicSource;yield return Wait(.8f);
            Check(music!=null&&music.clip!=null&&music.clip.name=="ses8"&&music.clip.loadType==AudioClipLoadType.Streaming,"ses8 is assigned as a separate streamed menu recording");
            report.duration=music.clip.length;
            Check(music.loop&&music.isPlaying&&!music.mute&&Mathf.Abs(music.volume-.8f)<.001f&&music.spatialBlend==0,"Menu opens with looping 2D music at increased 0.80 volume");
            Check(sound.HasUserVoices&&sound.HasRecordedCrowd&&sound.CrowdSource.clip!=music.clip&&sound.CrowdSource.volume<.001f,"Seven voices and crowd remain assigned; menu music replaces menu crowd sound");
            int sourceCount=game.GetComponents<AudioSource>().Length;
            game.Interface.openShop.onClick.Invoke();float before=music.time;yield return Wait(.65f);
            Check(game.Interface.ShopOpen&&game.State==RunState.Menu&&music.isPlaying&&music.time>before,"Opening the shop continues the same recording without restart");
            game.Interface.closeShop.onClick.Invoke();yield return Wait(.2f);
            Check(!game.Interface.ShopOpen&&music.isPlaying,"Returning from the shop keeps music playing");
            Time.timeScale=0;
            float end=Time.realtimeSinceStartup+music.clip.length+.5f,previous=music.time;
            while(Time.realtimeSinceStartup<end)
            {
                yield return Wait(.05f);
                if(music.time<previous-.5f)report.observedLoops++;
                previous=music.time;music.GetOutputData(sourceSamples,0);AudioListener.GetOutputData(listenerSamples,0);
                for(int i=0;i<sourceSamples.Length;i++)
                {report.sourcePeak=Mathf.Max(report.sourcePeak,Mathf.Abs(sourceSamples[i]));report.listenerPeak=Mathf.Max(report.listenerPeak,Mathf.Abs(listenerSamples[i]));}
            }
            Check(report.observedLoops>=1&&music.isPlaying,"The actual recording loops while scaled game time is stopped");
            Check(report.sourcePeak>.01f&&report.listenerPeak>.005f,"ses8 produces nonzero AudioSource and isolated menu AudioListener mixer output");
            Time.timeScale=1;sound.Powerup();yield return Wait(.3f);
            Check(sound.VoiceSource.isPlaying&&sound.VoiceSource.clip!=music.clip&&music.volume<.3f,"Power voice uses the original voice bank and ducks menu music");
            sound.SetMutedForTest(true);yield return Wait(.15f);
            Check(music.mute&&sound.CrowdSource.mute&&sound.VoiceSource.mute,"Existing global mute silences menu music, crowd and voices");
            sound.SetMutedForTest(false);yield return Wait(.5f);
            Check(!music.mute&&music.isPlaying&&music.volume>.75f,"Unmuting restores menu music");
            game.Play();yield return Wait(.65f);
            Check(game.State==RunState.Running&&!music.isPlaying&&music.volume==0&&sound.CrowdSource.volume>.2f,"Starting a run fades and stops menu music while crowd returns");
            game.Pause();yield return Wait(.35f);
            Check(game.State==RunState.Paused&&!music.isPlaying,"Pause does not restart menu music");
            game.Resume();yield return Wait(.1f);game.ReturnToMenu();yield return Wait(.65f);
            Check(game.State==RunState.Menu&&music.isPlaying&&music.volume>.75f&&music.time<1.5f,"Returning to menu restarts ses8 with a fade in");
            Check(game.GetComponents<AudioSource>().Length==sourceCount,"Screen transitions do not add duplicate audio sources");
            Check(game.WalletBalance==realBalance&&PlayerPrefs.GetInt("FenerRunner.BestScore",0)==realScore&&PlayerPrefs.GetFloat("FenerRunner.Best",0)==realBest&&PlayerPrefs.HasKey("FenerRunner.Muted")==hadMute&&PlayerPrefs.GetInt("FenerRunner.Muted",0)==mute,"Production wallet, records and mute preference remain unchanged");
        }
        static WaitForSecondsRealtime Wait(float seconds)=>new WaitForSecondsRealtime(seconds);
        void Check(bool passed,string description){checks.Add((passed?"PASS ":"FAIL ")+description);if(!passed)errors.Add(description);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception)errors.Add(message+"\n"+trace);}
        void OnDestroy(){Application.logMessageReceived-=Log;Application.runInBackground=previousBackground;Time.timeScale=1;if(sound!=null)sound.RestoreAudioPreferencesForTest();}
    }
}
#endif
