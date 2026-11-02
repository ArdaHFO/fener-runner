#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FenerRunner
{
    public sealed class AudioChecksV11:MonoBehaviour
    {
        const string SessionKey="FenerRunner.AudioChecksV11";
        static readonly string[] Keys={"Master","Music","Voices","Effects"};
        [Serializable] sealed class Report
        {
            public bool passed,realPreferencesPreserved;
            public string utc,unityVersion;
            public string[] checks,errors;
            public int sourcesBefore,sourcesAfter,laneRequests,laneStarts,runMusicStarts;
            public float menuSourcePeak,menuListenerPeak,runSourcePeak,runListenerPeak,laneSourcePeak,mutedListenerPeak,zeroMasterListenerPeak,wallSeconds;
            public string scope="Actual Unity Editor AudioSource and AudioListener sample output, accepted/rejected lane commands, streamed ses8 loop and run excerpts, priority interruption, pause and native Slider pointer dispatch. Audio preferences use an isolated temporary prefix and production keys are compared afterward. Does not measure physical loudspeaker output.";
        }
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        readonly float[] sourceSamples=new float[2048],listenerSamples=new float[2048],savedVolumes=new float[4];
        readonly bool[] hadVolume=new bool[4];
        readonly Report report=new Report();
        RunnerGame game;RunnerUI ui;StadiumSound sound;
        bool background,hadMute,prepared;int savedMute;float began,sourcePeak,listenerPeak;string prefix;

        [MenuItem("Fener Runner/56 - Verify adjustable audio and gameplay clips")]
        public static void StartChecks(){SessionState.SetBool(SessionKey,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register()
        {EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(SessionKey,false))return;
            SessionState.SetBool(SessionKey,false);new GameObject("Adjustable audio checks v11").AddComponent<AudioChecksV11>();
        }
        IEnumerator Start()
        {
            began=Time.realtimeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;Application.logMessageReceived+=Log;
            game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;ui=game.Interface;sound=game.GetComponent<StadiumSound>();
            for(int i=0;i<4;i++){string key=StadiumSound.DefaultAudioPrefix+Keys[i];hadVolume[i]=PlayerPrefs.HasKey(key);savedVolumes[i]=PlayerPrefs.GetFloat(key,1);}
            hadMute=PlayerPrefs.HasKey("FenerRunner.Muted");savedMute=PlayerPrefs.GetInt("FenerRunner.Muted",0);
            prefix="FenerRunner.Test.AudioV11."+DateTime.UtcNow.Ticks+".";sound.UseAudioPreferencesForTest(prefix);prepared=true;sound.SetMutedForTest(false);
            report.sourcesBefore=game.GetComponents<AudioSource>().Length;
            foreach(AudioSource source in game.GetComponents<AudioSource>())source.GetOutputData(sourceSamples,0);AudioListener.GetOutputData(listenerSamples,0);
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                bool more;object wait=null;
                try{more=stack.Peek().MoveNext();if(more)wait=stack.Peek().Current;}
                catch(Exception e){errors.Add(e.ToString());break;}
                if(!more){stack.Pop();continue;}
                if(wait is IEnumerator child){stack.Push(child);continue;}yield return wait;
            }
            game.ReturnToMenu();sound.RestoreAudioPreferencesForTest();prepared=false;
            bool unchanged=PlayerPrefs.HasKey("FenerRunner.Muted")==hadMute&&PlayerPrefs.GetInt("FenerRunner.Muted",0)==savedMute;
            for(int i=0;i<4;i++){string key=StadiumSound.DefaultAudioPrefix+Keys[i];unchanged&=PlayerPrefs.HasKey(key)==hadVolume[i]&&PlayerPrefs.GetFloat(key,1)==savedVolumes[i];}
            report.realPreferencesPreserved=unchanged;Check(unchanged,"All real mute and independent volume preferences are preserved, including key absence");
            report.sourcesAfter=game.GetComponents<AudioSource>().Length;
            Check(report.sourcesBefore==StadiumSound.SourceCount&&report.sourcesAfter==report.sourcesBefore,"All transitions and repeated sounds retain exactly six AudioSources");
            report.laneRequests=sound.LaneRequestCount;report.laneStarts=sound.LanePlayCount;report.runMusicStarts=sound.RunMusicPlayCount;
            report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.wallSeconds=Time.realtimeSinceStartup-began;
            report.checks=checks.ToArray();report.errors=errors.ToArray();report.passed=errors.Count==0;
            File.WriteAllText(ReviewEvidence.FileFor(11,"../Validation/unity_audio_v11.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Audio v11 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Run()
        {
            Check(!EditorUtility.audioMasterMute&&!AudioListener.pause&&AudioListener.volume>.95f,"The native Editor output and listener are available for real signal checks");
            game.Powerups.Enabled=false;game.Tokens.Enabled=false;game.ReturnToMenu();yield return Wait(.7f);
            Check(sound.MenuMusicSource.clip.name=="ses8"&&sound.MenuMusicSource.loop&&sound.MenuMusicSource.isPlaying&&Mathf.Abs(sound.MenuMusicSource.volume-.8f)<.002f,"Default menu ses8 retains its existing audible 0.8 looping gain");
            yield return Sample(sound.MenuMusicSource,.35f);report.menuSourcePeak=sourcePeak;report.menuListenerPeak=listenerPeak;
            Check(sourcePeak>.005f&&listenerPeak>.005f,"Menu ses8 reaches both the source output and actual listener mix");
            float musicTime=sound.MenuMusicSource.time;ui.OpenShop();yield return Wait(.25f);
            Check(ui.ShopOpen&&sound.MenuMusicSource.isPlaying&&sound.MenuMusicSource.loop&&sound.MenuMusicSource.time>musicTime,"The shop preserves the same continuously playing ses8 recording");ui.CloseShop();
            sound.MenuMusicSource.time=Mathf.Max(0,sound.MenuMusicSource.clip.length-.2f);yield return Wait(.45f);
            Check(sound.MenuMusicSource.isPlaying&&sound.MenuMusicSource.loop&&sound.MenuMusicSource.time<1.2f,"The actual streamed ses8 recording wraps to its beginning without stopping");

            ui.sound.onClick.Invoke();yield return Wait(.8f);
            Check(ui.AudioOpen&&ui.volumeSliders.Length==4&&game.State==RunState.Menu,"The menu sound control opens four independent volume channels");
            ui.play.onClick.Invoke();ui.heroGesture.onClick.Invoke();Check(game.State==RunState.Menu&&ui.AudioOpen,"The audio modal blocks underlying menu actions");
            float[] values={.64f,.52f,.73f,.41f};
            for(int i=0;i<4;i++)
            {
                Drag(ui.volumeSliders[i],values[i]);
                Check(Mathf.Abs(sound.GetVolume((AudioChannel)i)-values[i])<.015f,"A native slider pointer changes "+Keys[i]+" independently");
                Check(ui.volumeValues[i].text.Contains(Mathf.RoundToInt(sound.GetVolume((AudioChannel)i)*100).ToString()),Keys[i]+" displays its actual percentage");
            }
            yield return Wait(.4f);sound.SaveSettings();
            bool stored=true;for(int i=0;i<4;i++)stored&=Mathf.Abs(PlayerPrefs.GetFloat(prefix+Keys[i],-1)-sound.GetVolume((AudioChannel)i))<.001f;
            Check(stored,"Each adjusted volume is persisted under the isolated preference prefix");
            Check(Mathf.Abs(sound.MenuMusicSource.volume-.8f*.64f*.52f)<.012f&&Mathf.Abs(sound.VoiceSource.volume-.64f*.73f)<.012f&&Mathf.Abs(sound.LaneSource.volume-.025f*.64f*.41f)<.002f,"Master volume multiplies each independent channel instead of replacing its balance");
            ui.volumeSliders[0].value=0;yield return Wait(.18f);yield return Sample(sound.MenuMusicSource,.16f);report.zeroMasterListenerPeak=listenerPeak;
            bool allZero=true;foreach(AudioSource source in game.GetComponents<AudioSource>())allZero&=source.volume==0;
            Check(allZero&&listenerPeak<.0001f&&!sound.Muted,"Master zero produces real silence without changing the separate mute state");
            ui.volumeSliders[0].value=1;ui.volumeSliders[1].value=0;yield return Wait(.18f);yield return Sample(sound.MenuMusicSource,.15f);
            Check(sound.MenuMusicSource.volume==0&&listenerPeak<.0001f,"Music zero silences the menu recording while preserving other channel settings");
            ui.volumeSliders[1].value=.42f;ui.audioMute.onClick.Invoke();yield return Wait(.18f);yield return Sample(sound.MenuMusicSource,.15f);report.mutedListenerPeak=listenerPeak;
            bool allMuted=true;foreach(AudioSource source in game.GetComponents<AudioSource>())allMuted&=source.mute;
            Check(allMuted&&listenerPeak<.0001f&&Mathf.Abs(sound.GetVolume(AudioChannel.Music)-.42f)<.001f,"Mute silences every source without erasing the chosen volume levels");
            ui.audioMute.onClick.Invoke();ui.CloseAudioSettings();ui.OpenAudioSettings();yield return Wait(.65f);
            Check(!sound.Muted&&Mathf.Abs(ui.volumeSliders[1].value-.42f)<.001f,"Closing and reopening settings preserves the independent slider values");
            foreach(int height in new[]{1920,2340})
            {
                EditorApplication.ExecuteMenuItem(height==1920?"Fener Runner/5 - Open portrait Game view":"Fener Runner/10 - Open tall phone Game view");yield return Wait(.45f);Canvas.ForceUpdateCanvases();
                bool fits=Screen.height==height;
                foreach(Slider slider in ui.volumeSliders)fits&=InScreen((RectTransform)slider.transform)&&((RectTransform)slider.transform).rect.height>=132;
                foreach(Text text in ui.audioPanel.GetComponentsInChildren<Text>())fits&=InScreen(text.rectTransform)&&text.preferredHeight<=text.rectTransform.rect.height+2;
                Check(fits,"All four sliders and their text fit the actual 1080x"+height+" phone view with large touch targets");
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(ReviewEvidence.FileFor(11,"../Validation/v11-volume-"+height+".png"));yield return null;
            }
            EditorApplication.ExecuteMenuItem("Fener Runner/5 - Open portrait Game view");ui.CloseAudioSettings();SetAll(1);

            BeginRun();sound.SetRunMusicDueForTest(999);int requests=sound.LaneRequestCount,plays=sound.LanePlayCount;
            game.HandleCommand(RunnerCommand.Right);yield return Sample(sound.LaneSource,.18f);report.laneSourcePeak=sourcePeak;
            Check(game.Motor.Lane==1&&sound.LaneRequestCount==requests+1&&sound.LanePlayCount==plays+1&&sound.LaneSource.clip.name=="ses3","A successful right-lane change plays the actual supplied ses3");
            Check(sourcePeak>.00001f&&sound.LaneSource.volume<=.026f&&sound.LaneSource.volume<sound.VoiceSource.volume*.03f,"Lane ses3 emits a real signal at an extremely low level relative to full commentary");
            game.HandleCommand(RunnerCommand.Right);Check(sound.LaneRequestCount==requests+1,"A rejected turn beyond the right edge produces no lane sound");
            game.HandleCommand(RunnerCommand.Left);game.HandleCommand(RunnerCommand.Left);
            Check(game.Motor.Lane==-1&&sound.LaneRequestCount==requests+3&&sound.LanePlayCount==plays+3,"Each accepted left turn retriggers one fixed source without adding overlapping sources");
            game.HandleCommand(RunnerCommand.Left);Check(sound.LaneRequestCount==requests+3,"A rejected turn beyond the left edge also stays silent");
            yield return Wait(.05f);game.Pause();yield return null;float laneTime=sound.LaneSource.time;requests=sound.LaneRequestCount;
            game.HandleCommand(RunnerCommand.Right);yield return Wait(.2f);
            Check(sound.LaneRequestCount==requests&&!sound.LaneSource.isPlaying&&Mathf.Abs(sound.LaneSource.time-laneTime)<.03f,"Pause freezes the existing lane clip and rejects new movement sounds");game.Resume();
            sound.SetVolume(AudioChannel.Effects,0);plays=sound.LanePlayCount;game.HandleCommand(RunnerCommand.Right);yield return Wait(.08f);
            Check(sound.LanePlayCount==plays&&sound.LaneSource.volume==0&&!sound.LaneSource.isPlaying,"Effects zero suppresses lane playback without disabling movement");SetAll(1);
            game.ReturnToMenu();requests=sound.LaneRequestCount;game.HandleCommand(RunnerCommand.Left);Check(sound.LaneRequestCount==requests,"Menu input cannot create gameplay lane sounds");

            BeginRun();Check(sound.LastRunMusicInterval>=7&&sound.LastRunMusicInterval<=11,"Fresh runs schedule occasional ses8 after seven to eleven active seconds");
            sound.SetRunMusicDueForTest(.4f);yield return Wait(.15f);game.Pause();yield return null;float delay=sound.RunMusicDelayRemaining;yield return Wait(.2f);
            Check(sound.RunMusicDelayRemaining==delay&&!sound.RunMusicActive,"Pause freezes the gameplay music schedule");game.Resume();
            float deadline=Time.realtimeSinceStartup+2;while(!sound.RunMusicActive&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return Wait(.65f);yield return Sample(sound.MenuMusicSource,.4f);report.runSourcePeak=sourcePeak;report.runListenerPeak=listenerPeak;
            Check(sound.RunMusicActive&&!sound.MenuMusicSource.loop&&sound.MenuMusicSource.isPlaying&&sourcePeak>.003f&&listenerPeak>.003f,"A real non-looping ses8 excerpt becomes audible during gameplay");
            Check(sound.LastRunMusicInterval>=18&&sound.LastRunMusicInterval<=26,"The next gameplay excerpt is separated by an eighteen to twenty-six second interval");
            game.Pause();yield return null;float remaining=sound.RunMusicRemaining,position=sound.MenuMusicSource.time;double distance=game.Distance;
            ui.pauseSound.onClick.Invoke();yield return Wait(.8f);ui.resume.onClick.Invoke();
            Check(ui.AudioOpen&&game.State==RunState.Paused&&game.Distance==distance&&sound.RunMusicRemaining==remaining&&Mathf.Abs(sound.MenuMusicSource.time-position)<.03f,"Pause settings keep the physical run and in-progress ses8 excerpt frozen, including blocked underlying resume");
            ui.volumeSliders[1].value=.5f;ui.CloseAudioSettings();game.Resume();yield return Wait(.12f);
            Check(sound.RunMusicActive&&sound.MenuMusicSource.isPlaying&&sound.RunMusicRemaining<remaining,"Closing pause settings and resuming continues the same excerpt");
            int power=sound.PowerVoicePlayCount;sound.Powerup();yield return Wait(.08f);
            Check(!sound.RunMusicActive&&!sound.MenuMusicSource.isPlaying&&sound.PowerVoicePlayCount==power+1&&sound.CurrentVoiceKind==VoiceKind.Power,"A collected-power voice immediately takes priority over a gameplay ses8 excerpt");

            BeginRun();SetAll(1);sound.SetVolume(AudioChannel.Voices,0);int suppressed=sound.SuppressedPowerVoiceCount;sound.Powerup();yield return Wait(.08f);
            Check(sound.SuppressedPowerVoiceCount==suppressed+1&&sound.PendingPowerVoices==0&&!sound.VoiceSource.isPlaying,"Voices zero suppresses new commentary without building a silent backlog");
            sound.SetRunMusicDueForTest(0);yield return Wait(2.2f);
            Check(sound.RunMusicActive,"Disabling commentary does not starve the independent music schedule");
            sound.SetMutedForTest(true);yield return Wait(.1f);Check(!sound.RunMusicActive&&!sound.MenuMusicSource.isPlaying,"Muting cancels an active gameplay excerpt");sound.SetMutedForTest(false);
            SetAll(1);BeginRun();sound.SetRunMusicDueForTest(0);yield return Wait(.6f);
            Check(sound.RunMusicActive,"The fatal-priority scenario begins with an actual running excerpt");
            game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder,game.Motor.transform.position.z+.6f,game.Motor.Lane);yield return Wait(.15f);
            Check(game.State==RunState.Finished&&!sound.RunMusicActive&&!sound.MenuMusicSource.isPlaying&&sound.CurrentVoiceKind==VoiceKind.Fatal&&sound.LastVoiceIndex==0&&sound.VoiceSource.isPlaying,"A real curly collision stops ses8 and plays fatal ses1 immediately through the finish presentation");
            game.ReturnToMenu();yield return Wait(.6f);Check(sound.MenuMusicSource.loop&&sound.MenuMusicSource.isPlaying,"Returning from the finished run restores the original looping menu ses8");
        }
        void BeginRun(){game.Restart();game.Obstacles.SetActive(false);game.Tokens.Enabled=false;game.Powerups.Enabled=false;sound.SetChatterDueForTest(999);}
        void SetAll(float value){for(int i=0;i<4;i++)sound.SetVolume((AudioChannel)i,value);}
        void Drag(Slider slider,float value)
        {
            var travel=(RectTransform)slider.handleRect.parent;Rect bounds=travel.rect;
            Vector3 local=new Vector3(Mathf.Lerp(bounds.xMin,bounds.xMax,value),0,0);Vector2 point=RectTransformUtility.WorldToScreenPoint(null,travel.TransformPoint(local));
            var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(slider.gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(slider.gameObject,data,ExecuteEvents.dragHandler);ExecuteEvents.Execute(slider.gameObject,data,ExecuteEvents.pointerUpHandler);
        }
        IEnumerator Sample(AudioSource source,float seconds)
        {
            sourcePeak=listenerPeak=0;float until=Time.realtimeSinceStartup+seconds;
            while(Time.realtimeSinceStartup<until)
            {
                source.GetOutputData(sourceSamples,0);AudioListener.GetOutputData(listenerSamples,0);
                for(int i=0;i<sourceSamples.Length;i++){sourcePeak=Mathf.Max(sourcePeak,Mathf.Abs(sourceSamples[i]));listenerPeak=Mathf.Max(listenerPeak,Mathf.Abs(listenerSamples[i]));}
                yield return null;
            }
        }
        static bool InScreen(RectTransform rect)
        {var corners=new Vector3[4];rect.GetWorldCorners(corners);foreach(Vector3 p in corners)if(p.x<-.5f||p.y<-.5f||p.x>Screen.width+.5f||p.y>Screen.height+.5f)return false;return true;}
        static WaitForSecondsRealtime Wait(float seconds)=>new WaitForSecondsRealtime(seconds);
        void Check(bool value,string message){if(value)checks.Add(message);else errors.Add(message);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;Application.runInBackground=background;if(prepared&&sound!=null)sound.RestoreAudioPreferencesForTest();}
    }
}
#endif
