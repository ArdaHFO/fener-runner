using UnityEngine;

namespace FenerRunner
{
    public enum VoiceKind { None, Chatter, Power, Fatal }
    public enum AudioChannel { Master, Music, Voices, Effects }

    public sealed class StadiumSound:MonoBehaviour
    {
        [SerializeField] AudioClip recordedCrowd;
        [SerializeField] AudioClip[] userVoices;
        [SerializeField] AudioClip menuRecording;
        AudioSource menuMusic,lane;
        public const int SourceCount=6;
        public const string DefaultAudioPrefix="FenerRunner.Audio.";
        static readonly string[] VolumeNames={"Master","Music","Voices","Effects"};
        readonly float[] levels={1,1,1,1};
        string preferencesPrefix=DefaultAudioPrefix;
        float ambienceGain,musicGain,nextRunMusic,runMusicRemaining,runMusicAge,settingsSaveDelay;
        bool runMusicActive,musicPaused,lanePaused,settingsDirty;
        const float LaneRelativeGain=.025f,RunMusicSeconds=5.5f;
        AudioSource ambience,steps,effects,voice;AudioClip step,impact;RunnerGame game;float stride;
        readonly int[] voiceBag={0,1,2,3,4,5,6};int bagPosition=7,lastScheduled=-1;
        // One active voice plus this bounded FIFO covers every current power kind.
        public const int PowerVoiceQueueCapacity=16;
        const float ChatterQuietGap=2f;
        readonly int[] pendingPowers=new int[PowerVoiceQueueCapacity];int pendingRead,pendingCount;
        readonly System.Random random=new System.Random();
        bool voicePaused;
        readonly AudioSource[] applicationSources=new AudioSource[SourceCount];
        readonly bool[] resumeSources=new bool[SourceCount];
        public bool ApplicationSuspended {get;private set;}
#if UNITY_EDITOR
        bool isolateVoiceForTest;
        string savedPreferencesPrefix;float[] savedLevels;bool savedMuted;
#endif
        float nextChatter,quietSeconds,queueGap;
        public bool Muted {get;private set;}
        public bool HasRecordedCrowd=>recordedCrowd!=null;
        public bool HasUserVoices=>userVoices!=null&&userVoices.Length==7&&System.Array.TrueForAll(userVoices,c=>c!=null);
        public int LastVoiceIndex {get;private set;}=-1;
        public int VoicePlayCount {get;private set;}
        public int PowerVoiceRequestCount {get;private set;}
        public int PowerVoicePlayCount {get;private set;}
        public int PowerVoiceCompletedCount {get;private set;}
        public int PowerVoiceInterruptedCount {get;private set;}
        public int SuppressedPowerVoiceCount {get;private set;}
        public int ChatterPlayCount {get;private set;}
        public int FatalVoicePlayCount {get;private set;}
        public int ChatterInterruptedCount {get;private set;}
        public int QueueOverflowCount {get;private set;}
        public int PendingPowerVoices=>pendingCount;
        public float ChatterDelayRemaining=>nextChatter;
        public float QuietSeconds=>quietSeconds;
        public float LastChatterInterval {get;private set;}
        public VoiceKind CurrentVoiceKind {get;private set;}
        public AudioSource VoiceSource=>voice;
        public AudioSource CrowdSource=>ambience;
        public AudioSource MenuMusicSource=>menuMusic;
        public AudioSource LaneSource=>lane;
        public int LaneRequestCount{get;private set;}
        public int LanePlayCount{get;private set;}
        public int RunMusicPlayCount{get;private set;}
        public bool RunMusicActive=>runMusicActive;
        public float RunMusicRemaining=>runMusicRemaining;
        public float RunMusicDelayRemaining=>nextRunMusic;
        public float LastRunMusicInterval{get;private set;}
        public float GetVolume(AudioChannel channel)=>levels[(int)channel];
        bool VoiceAudible=>!Muted&&levels[0]>0&&levels[2]>0;
        bool MusicAudible
        {
            get
            {
#if UNITY_EDITOR
                if(isolateVoiceForTest)return false;
#endif
                return !Muted&&levels[0]>0&&levels[1]>0&&menuRecording!=null;
            }
        }
        string MuteKey=>preferencesPrefix==DefaultAudioPrefix?"FenerRunner.Muted":preferencesPrefix+"Muted";

        public void Configure(AudioClip clip,AudioClip[] voices=null,AudioClip menuClip=null)
        {recordedCrowd=clip;userVoices=voices;menuRecording=menuClip;}
        void Awake()
        {
            game=GetComponent<RunnerGame>();LoadSettings();
            ambience=gameObject.AddComponent<AudioSource>();steps=gameObject.AddComponent<AudioSource>();
            effects=gameObject.AddComponent<AudioSource>();voice=gameObject.AddComponent<AudioSource>();
            menuMusic=gameObject.AddComponent<AudioSource>();menuMusic.playOnAwake=false;
            lane=gameObject.AddComponent<AudioSource>();lane.playOnAwake=false;lane.loop=false;
            lane.spatialBlend=0;lane.dopplerLevel=0;lane.reverbZoneMix=0;lane.priority=180;
            lane.clip=HasUserVoices?userVoices[2]:null;
            menuMusic.clip=menuRecording;menuMusic.loop=true;menuMusic.spatialBlend=0;
            menuMusic.dopplerLevel=0;menuMusic.reverbZoneMix=0;menuMusic.volume=0;
            ambience.playOnAwake=steps.playOnAwake=effects.playOnAwake=voice.playOnAwake=false;
            ambience.loop=true;step=Thud("Soft footfall",.08f,72);impact=Thud("Impact",.22f,58);
            ambience.clip=recordedCrowd;ambienceGain=menuRecording!=null?0:.12f;
            applicationSources[0]=ambience;applicationSources[1]=steps;applicationSources[2]=effects;
            applicationSources[3]=voice;applicationSources[4]=menuMusic;applicationSources[5]=lane;
            // Spoken commentary is screen-space audio, independent of camera distance.
            voice.spatialBlend=0;voice.dopplerLevel=0;voice.panStereo=0;voice.priority=0;
            voice.loop=false;voice.outputAudioMixerGroup=null;voice.reverbZoneMix=0;
            voice.ignoreListenerPause=false;voice.ignoreListenerVolume=false;
            if(userVoices!=null)foreach(var clip in userVoices)if(clip!=null)clip.LoadAudioData();
            if(recordedCrowd!=null)ambience.Play();ApplyMute();ApplyVolumes();ResetSession();
        }
        void LoadSettings()
        {for(int i=0;i<levels.Length;i++)levels[i]=Mathf.Clamp01(PlayerPrefs.GetFloat(preferencesPrefix+VolumeNames[i],1));Muted=PlayerPrefs.GetInt(MuteKey,0)==1;}
        public void SetVolume(AudioChannel channel,float value)
        {
            int index=(int)channel;if(index<0||index>=levels.Length)return;
            levels[index]=Mathf.Clamp01(value);PlayerPrefs.SetFloat(preferencesPrefix+VolumeNames[index],levels[index]);
            settingsDirty=true;settingsSaveDelay=.35f;
            if(!VoiceAudible){CancelVoice();ClearPending();}
            if(!MusicAudible&&runMusicActive)EndRunMusic();
            if((levels[0]<=0||levels[3]<=0)&&lane!=null)lane.Stop();
            ApplyVolumes();
        }
        public void SaveSettings(){if(!settingsDirty)return;PlayerPrefs.Save();settingsDirty=false;}
        void ApplyVolumes()
        {
            float master=levels[0];
            if(ambience!=null)ambience.volume=ambienceGain*master*levels[3];
            if(steps!=null)steps.volume=.075f*master*levels[3];
            if(effects!=null)effects.volume=.2f*master*levels[3];
            if(voice!=null)voice.volume=master*levels[2];
            if(menuMusic!=null)menuMusic.volume=musicGain*master*levels[1];
            if(lane!=null)lane.volume=LaneRelativeGain*master*levels[3]*(voice!=null&&voice.isPlaying?.18f:1f);
        }
        public void Toggle(){SetMuted(!Muted,true);}
        void SetMuted(bool value,bool persist)
        {
            if(Muted==value){ApplyMute();return;}
            Muted=value;
            if(persist){PlayerPrefs.SetInt(MuteKey,Muted?1:0);PlayerPrefs.Save();}
            ApplyMute();
            if(Muted){CancelVoice();ClearPending();EndRunMusic();if(lane!=null)lane.Stop();}
            else{quietSeconds=ChatterQuietGap;ScheduleChatter(true);}
        }
        void ApplyMute()
        {
            bool otherMuted=Muted;
#if UNITY_EDITOR
            otherMuted|=isolateVoiceForTest;
#endif
            if(ambience!=null)ambience.mute=otherMuted;if(steps!=null)steps.mute=otherMuted;
            if(effects!=null)effects.mute=otherMuted;if(voice!=null)voice.mute=Muted;
            if(menuMusic!=null)menuMusic.mute=otherMuted;
            if(lane!=null)lane.mute=otherMuted;
        }
        // Ordinary coins stay silent; voices are reserved for powers and occasional commentary.
        public void Pickup(bool fever){}
        public void LaneChanged(int previousLane,int newLane)
        {
            if(ApplicationSuspended||game==null||game.State!=RunState.Running||previousLane==newLane||Mathf.Abs(previousLane)>1||Mathf.Abs(newLane)>1)return;
            LaneRequestCount++;
            if(Muted||levels[0]<=0||levels[3]<=0||lane==null||lane.clip==null)return;
            // The supplied ses3 is a short 0.41-second clip. Each accepted turn
            // restarts this one quiet source, never an overlapping OneShot pile.
            lane.Stop();lane.Play();LanePlayCount++;
            ApplyVolumes();
        }

        public void Powerup()
        {
            if(!HasUserVoices||voice==null||game==null||game.State==RunState.Finished)return;
            PowerVoiceRequestCount++;
            if(!VoiceAudible){SuppressedPowerVoiceCount++;return;}
            RefreshCompletion();
            int index=NextVoice();
            if(CurrentVoiceKind==VoiceKind.Chatter)CancelVoice();
            if(pendingCount==PowerVoiceQueueCapacity)
            {
                // Current pickup and workshop bursts fit this fixed queue plus its
                // active voice. Defensive overflow still starts
                // the new event immediately; memory and delayed chatter cannot grow.
                QueueOverflowCount++;CancelVoice();StartVoice(index,VoiceKind.Power);return;
            }
            pendingPowers[(pendingRead+pendingCount)%pendingPowers.Length]=index;pendingCount++;
            if(CurrentVoiceKind==VoiceKind.None&&!voicePaused&&queueGap<=0)StartQueuedPower();
        }

        int NextVoice()
        {
            if(bagPosition>=voiceBag.Length)
            {
                for(int i=voiceBag.Length-1;i>0;i--){int j=random.Next(i+1);int t=voiceBag[i];voiceBag[i]=voiceBag[j];voiceBag[j]=t;}
                if(voiceBag[0]==lastScheduled){int t=voiceBag[0];voiceBag[0]=voiceBag[1];voiceBag[1]=t;}
                bagPosition=0;
            }
            if(voiceBag[bagPosition]==lastScheduled)
            {
                if(bagPosition==voiceBag.Length-1){bagPosition=voiceBag.Length;return NextVoice();}
                int t=voiceBag[bagPosition];voiceBag[bagPosition]=voiceBag[bagPosition+1];voiceBag[bagPosition+1]=t;
            }
            lastScheduled=voiceBag[bagPosition++];return lastScheduled;
        }
        void StartQueuedPower()
        {
            if(pendingCount==0)return;
            int index=pendingPowers[pendingRead];pendingRead=(pendingRead+1)%pendingPowers.Length;pendingCount--;
            StartVoice(index,VoiceKind.Power);
        }
        void StartVoice(int index,VoiceKind kind)
        {
            if(kind==VoiceKind.Power||kind==VoiceKind.Fatal)EndRunMusic();
            voice.Stop();voice.clip=userVoices[index];voice.pitch=1;voicePaused=false;
            CurrentVoiceKind=kind;quietSeconds=0;queueGap=0;
            voice.Play();LastVoiceIndex=index;VoicePlayCount++;
            if(kind==VoiceKind.Power)PowerVoicePlayCount++;
            else if(kind==VoiceKind.Chatter)ChatterPlayCount++;
            else if(kind==VoiceKind.Fatal)FatalVoicePlayCount++;
            if(game.State==RunState.Paused){voice.Pause();voicePaused=true;}
            if(ApplicationSuspended)SuspendPlayingSources();
        }
        void RefreshCompletion()
        {
            if(voice==null||ApplicationSuspended||voicePaused||CurrentVoiceKind==VoiceKind.None||voice.isPlaying)return;
            if(CurrentVoiceKind==VoiceKind.Power)PowerVoiceCompletedCount++;
            CurrentVoiceKind=VoiceKind.None;quietSeconds=0;queueGap=.14f;
        }
        void CancelVoice()
        {
            if(CurrentVoiceKind==VoiceKind.Power)PowerVoiceInterruptedCount++;
            if(CurrentVoiceKind==VoiceKind.Chatter)ChatterInterruptedCount++;
            if(voice!=null)voice.Stop();voicePaused=false;CurrentVoiceKind=VoiceKind.None;quietSeconds=0;queueGap=0;
        }
        void ClearPending(){pendingRead=pendingCount=0;}
        void ScheduleChatter(bool first=false)
        {
            LastChatterInterval=first?1.2f+(float)random.NextDouble():6f+(float)random.NextDouble()*4f;
            nextChatter=LastChatterInterval;
        }

        public void Hit(ObstacleKind kind)
        {
            EndRunMusic();if(lane!=null)lane.Stop();lanePaused=false;
            if(kind==ObstacleKind.SlideUnder&&HasUserVoices)
            {
                RefreshCompletion();CancelVoice();ClearPending();lastScheduled=0;
                if(VoiceAudible&&voice!=null)StartVoice(0,VoiceKind.Fatal);
                return;
            }
            // Other fatal impacts should also discard stale pickup commentary.
            RefreshCompletion();CancelVoice();ClearPending();Hit();
        }
        public void Hit(){if(effects!=null&&!ApplicationSuspended){effects.pitch=1;effects.PlayOneShot(impact,.75f);}}
        public void ResetSession()
        {
            stride=-1;RefreshCompletion();CancelVoice();ClearPending();voicePaused=false;quietSeconds=ChatterQuietGap;
            bagPosition=voiceBag.Length;lastScheduled=LastVoiceIndex;ScheduleChatter(true);
            if(steps!=null)steps.Stop();if(effects!=null)effects.Stop();if(lane!=null)lane.Stop();lanePaused=false;
            EndRunMusic();ScheduleRunMusic(true);
            // A reset invalidates suspended transient sounds from the previous run.
            for(int i=1;i<resumeSources.Length;i++)resumeSources[i]=false;
            if(ApplicationSuspended)SuspendPlayingSources();
        }
        void ScheduleRunMusic(bool first=false)
        {LastRunMusicInterval=(first?7f:18f)+(float)random.NextDouble()*(first?4f:8f);nextRunMusic=LastRunMusicInterval;}
        void EndRunMusic()
        {
            if(runMusicActive&&menuMusic!=null)menuMusic.Stop();
            runMusicActive=musicPaused=false;runMusicRemaining=runMusicAge=0;
            if(game!=null&&game.State!=RunState.Menu)musicGain=0;
        }
        void TickMusic(bool moving,bool paused)
        {
            bool inMenu=game.State==RunState.Menu;
            if(paused)
            {if(menuMusic.isPlaying&&!musicPaused){menuMusic.Pause();musicPaused=true;}return;}
            if(musicPaused){menuMusic.UnPause();musicPaused=false;}
            if(inMenu)
            {
                if(menuRecording!=null&&(!menuMusic.loop||!menuMusic.isPlaying))
                {menuMusic.Stop();menuMusic.loop=true;menuMusic.time=0;menuMusic.Play();}
                musicGain=Mathf.MoveTowards(musicGain,voice.isPlaying?.18f:.8f,Time.unscaledDeltaTime*1.8f);return;
            }
            if(!moving)
            {EndRunMusic();musicGain=Mathf.MoveTowards(musicGain,0,Time.unscaledDeltaTime*1.8f);if(musicGain<=0&&menuMusic.isPlaying)menuMusic.Stop();return;}
            if(runMusicActive)
            {
                runMusicRemaining=Mathf.Max(0,runMusicRemaining-Time.deltaTime);runMusicAge+=Time.deltaTime;
                if(runMusicRemaining<=0||!MusicAudible){EndRunMusic();return;}
                musicGain=.48f*Mathf.Min(Mathf.Clamp01(runMusicAge/.5f),Mathf.Clamp01(runMusicRemaining/.65f));return;
            }
            musicGain=Mathf.MoveTowards(musicGain,0,Time.unscaledDeltaTime*1.8f);
            if(musicGain<=0&&menuMusic.isPlaying)menuMusic.Stop();
            if(MusicAudible)nextRunMusic=Mathf.Max(0,nextRunMusic-Time.deltaTime);
            if(nextRunMusic>0||!MusicAudible||musicGain>0||CurrentVoiceKind!=VoiceKind.None||pendingCount>0||quietSeconds<ChatterQuietGap)return;
            menuMusic.loop=false;menuMusic.Stop();
            float excerpt=Mathf.Min(RunMusicSeconds,menuRecording.length);
            menuMusic.time=(float)random.NextDouble()*Mathf.Max(0,menuRecording.length-excerpt-.1f);
            menuMusic.Play();runMusicActive=true;runMusicAge=0;runMusicRemaining=excerpt;RunMusicPlayCount++;ScheduleRunMusic();
        }

        void Update()
        {
            if(game==null||voice==null)return;
            bool moving=game.State==RunState.Running,paused=game.State==RunState.Paused;
            if(settingsDirty){settingsSaveDelay-=Time.unscaledDeltaTime;if(settingsSaveDelay<=0)SaveSettings();}
            if(ApplicationSuspended)return;
            if(paused&&!lanePaused){lane.Pause();lanePaused=true;}
            else if(!paused&&lanePaused){lane.UnPause();lanePaused=false;}
            if(!moving&&!paused&&lane.isPlaying)lane.Stop();
            if(paused&&!voicePaused){voice.Pause();voicePaused=true;}
            else if(!paused&&voicePaused){voice.UnPause();voicePaused=false;}
            RefreshCompletion();
            if(moving&&!Muted&&!VoiceAudible&&CurrentVoiceKind==VoiceKind.None&&pendingCount==0)quietSeconds+=Time.deltaTime;
            if(VoiceAudible&&!paused)
            {
                queueGap=Mathf.Max(0,queueGap-Time.unscaledDeltaTime);
                if(moving)
                {
                    nextChatter=Mathf.Max(0,nextChatter-Time.deltaTime);
                    if(CurrentVoiceKind==VoiceKind.None&&pendingCount==0)quietSeconds+=Time.deltaTime;
                }
                if(CurrentVoiceKind==VoiceKind.None&&pendingCount>0&&queueGap<=0&&game.State!=RunState.Finished)
                    StartQueuedPower();
                else if(moving&&!runMusicActive&&(nextRunMusic>0||!MusicAudible)&&CurrentVoiceKind==VoiceKind.None&&pendingCount==0&&nextChatter<=0&&quietSeconds>=ChatterQuietGap&&HasUserVoices)
                {StartVoice(NextVoice(),VoiceKind.Chatter);ScheduleChatter();}
            }
            bool inMenu=game.State==RunState.Menu;
            TickMusic(moving,paused);
            float target=moving?(game.FeverActive?.5f:.34f):inMenu?(menuRecording!=null?0:.12f):.07f;
            if(moving&&game.CrowdReactions!=null)target+=game.CrowdReactions.AudioExcitement*.12f;
            if(voice.isPlaying)target*=.32f;
            if(runMusicActive)target*=.6f;
            ambienceGain=Mathf.MoveTowards(ambienceGain,target,Time.unscaledDeltaTime*1.2f);ApplyVolumes();
            if(ambience!=null)ambience.panStereo=Mathf.MoveTowards(ambience.panStereo,moving&&game.CrowdReactions!=null?game.CrowdReactions.AudioPan:0,Time.unscaledDeltaTime*.8f);
            if(!moving||!game.Motor.Grounded||game.Motor.Sliding||game.Animation.CurrentClip!="Run"){stride=-1;return;}
            var state=game.Animation.Animator.GetCurrentAnimatorStateInfo(0);
            if(!state.IsName("Run")){stride=-1;return;}
            int contact=Mathf.FloorToInt(state.normalizedTime*2);
            if(stride<0){stride=contact;return;}
            if(contact!=(int)stride){stride=contact;steps.pitch=Random.Range(.94f,1.06f);steps.PlayOneShot(step);}
        }
#if UNITY_EDITOR
        // These helpers never persist mute preferences or alter the production intervals.
        public void SetMutedForTest(bool value){SetMuted(value,false);}
        public void SetChatterDueForTest(float seconds){nextChatter=Mathf.Max(0,seconds);}
        public void IsolateVoiceForTest(bool value){isolateVoiceForTest=value;ApplyMute();}
        public void SetRunMusicDueForTest(float seconds){nextRunMusic=Mathf.Max(0,seconds);}
        public void UseAudioPreferencesForTest(string prefix)
        {
            if(savedLevels!=null||!prefix.StartsWith("FenerRunner.Test."))throw new System.InvalidOperationException("An isolated test prefix is required.");
            SaveSettings();savedPreferencesPrefix=preferencesPrefix;savedLevels=(float[])levels.Clone();savedMuted=Muted;
            preferencesPrefix=prefix;LoadSettings();ApplyMute();ApplyVolumes();ResetSession();
        }
        public void RestoreAudioPreferencesForTest()
        {
            if(savedLevels==null)return;
            for(int i=0;i<levels.Length;i++)PlayerPrefs.DeleteKey(preferencesPrefix+VolumeNames[i]);PlayerPrefs.DeleteKey(MuteKey);
            preferencesPrefix=savedPreferencesPrefix;System.Array.Copy(savedLevels,levels,levels.Length);savedLevels=null;
            Muted=savedMuted;settingsDirty=false;ApplyMute();ApplyVolumes();ResetSession();PlayerPrefs.Save();
        }
#endif
        static AudioClip Thud(string name,float seconds,float hz)
        {
            const int rate=22050;var samples=new float[(int)(rate*seconds)];var rng=new System.Random(1907);float filtered=0;
            for(int i=0;i<samples.Length;i++){float t=(float)i/rate;filtered=Mathf.Lerp(filtered,(float)rng.NextDouble()*2-1,.12f);samples[i]=(Mathf.Sin(t*hz*6.28318f)*.25f+filtered*.2f)*Mathf.Exp(-t*38)*Mathf.Min(1,t*450);}
            var clip=AudioClip.Create(name,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        void SuspendPlayingSources()
        {
            for(int i=0;i<applicationSources.Length;i++)
            {
                var source=applicationSources[i];
                if(source==null||!source.isPlaying)continue;
                resumeSources[i]=true;source.Pause();
            }
        }
        public void SetApplicationSuspended(bool suspended)
        {
            if(ApplicationSuspended==suspended)return;
            ApplicationSuspended=suspended;
            if(suspended){SaveSettings();SuspendPlayingSources();return;}
            bool runPaused=game!=null&&game.State==RunState.Paused;
            for(int i=0;i<applicationSources.Length;i++)
            {
                if(!resumeSources[i])continue;
                resumeSources[i]=false;var source=applicationSources[i];if(source==null)continue;
                if(runPaused&&i>0)
                {
                    if(i==3)voicePaused=true;
                    else if(i==4)musicPaused=true;
                    else if(i==5)lanePaused=true;
                    else source.Stop();
                }
                else source.UnPause();
            }
        }
        void OnApplicationPause(bool paused){if(paused)SaveSettings();}
        void OnDestroy()
        {
#if UNITY_EDITOR
            RestoreAudioPreferencesForTest();
#endif
            SaveSettings();if(step!=null)Destroy(step);if(impact!=null)Destroy(impact);
        }
    }
}
