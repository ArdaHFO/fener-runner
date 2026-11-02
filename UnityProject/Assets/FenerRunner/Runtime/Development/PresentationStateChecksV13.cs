#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    // Native-state regression checks. They exercise real generated scene objects
    // and the real gesture dispatcher without writing production preferences.
    public sealed class PresentationStateChecksV13:MonoBehaviour
    {
        const string Flag="FenerRunner.PresentationStateV13";
        static readonly string[] IntKeys={RunWallet.DefaultPrefix+"Coins",RunWallet.DefaultPrefix+"Upgrade.0",RunWallet.DefaultPrefix+"Upgrade.1",RunWallet.DefaultPrefix+"Upgrade.2","FenerRunner.BestScore","FenerRunner.Muted"};
        static readonly string[] FloatKeys={"FenerRunner.Best",StadiumSound.DefaultAudioPrefix+"Master",StadiumSound.DefaultAudioPrefix+"Music",StadiumSound.DefaultAudioPrefix+"Voices",StadiumSound.DefaultAudioPrefix+"Effects"};
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        readonly List<RunnerCommand> commands=new List<RunnerCommand>();
        readonly bool[] hadInts=new bool[IntKeys.Length],hadFloats=new bool[FloatKeys.Length];
        readonly int[] intValues=new int[IntKeys.Length];readonly float[] floatValues=new float[FloatKeys.Length];
        RunnerGame game;RunnerInput input;RunnerFeedback feedback;StadiumWeather weather;
        bool background;float began;int objectCount;
        [Serializable] sealed class Report
        {
            public bool passed,preferencesPreserved;
            public string utc,unityVersion;public string[] checks,errors;
            public int footBursts,landingBursts,particleCapacity,maximumObservedParticles,weatherWrites,objectsBefore,objectsAfter;
            public float wallSeconds;
            public string scope="Native Unity Editor state checks using the generated runner, Animator contact phases, fixed particle pool, actual weather/light/material changes, pause/reset, modal Back and RunnerInput gesture dispatcher. Includes stale-release and UI-blocked swipe regressions. No physical touchscreen, GPU frame-time, APK or multiplayer claim. Production wallet, records and sound preferences are read and compared, never edited.";
        }
        readonly Report report=new Report();

        [MenuItem("Fener Runner/75 - Verify v13 presentation state and gestures")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Presentation state v13 checks").AddComponent<PresentationStateChecksV13>();}}
        IEnumerator Start()
        {
            began=Time.realtimeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;Application.logMessageReceived+=Log;
            game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;input=FindFirstObjectByType<RunnerInput>();
            feedback=game.GetComponent<RunnerFeedback>();weather=game.GetComponent<StadiumWeather>();
            for(int i=0;i<IntKeys.Length;i++){hadInts[i]=PlayerPrefs.HasKey(IntKeys[i]);intValues[i]=PlayerPrefs.GetInt(IntKeys[i],0);}
            for(int i=0;i<FloatKeys.Length;i++){hadFloats[i]=PlayerPrefs.HasKey(FloatKeys[i]);floatValues[i]=PlayerPrefs.GetFloat(FloatKeys[i],0);}
            input.Command+=Heard;
            yield return null; // Feedback.Start creates its fixed pool once.
            objectCount=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            report.objectsBefore=objectCount;
            var stack=new Stack<IEnumerator>();stack.Push(Scenarios());
            while(stack.Count>0)
            {
                bool more;object wait=null;
                try{more=stack.Peek().MoveNext();if(more)wait=stack.Peek().Current;}
                catch(Exception error){errors.Add(error.ToString());break;}
                if(!more){stack.Pop();continue;}if(wait is IEnumerator child){stack.Push(child);continue;}yield return wait;
            }
            game.enabled=true;game.ReturnToMenu();
            report.preferencesPreserved=PreferencesUnchanged();Check(report.preferencesPreserved,"Wallet, upgrades, records and all sound preferences preserve their exact values and absent-key state");
            report.objectsAfter=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            Check(report.objectsAfter==objectCount,"Gestures, weather, landings and rewards retain the same scene object count");
            report.weatherWrites=weather.AppliedStateCount;report.particleCapacity=feedback.ParticleCapacity;
            report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.wallSeconds=Time.realtimeSinceStartup-began;
            report.checks=checks.ToArray();report.errors=errors.ToArray();report.passed=errors.Count==0;
            Directory.CreateDirectory("../Validation");File.WriteAllText(ReviewEvidence.FileFor(13,"../Validation/unity_presentation_state_v13.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;
            Debug.Log("Presentation state v13 "+(report.passed?"PASSED":"FAILED")+": "+checks.Count+" checks; "+string.Join("; ",errors));
            EditorApplication.isPlaying=false;
        }
        void Begin()
        {
            game.Powerups.Enabled=false;game.Tokens.Enabled=false;game.Obstacles.Enabled=true;
            game.Play();game.Obstacles.SetActive(false);commands.Clear();
        }
        IEnumerator Scenarios()
        {
            game.ReturnToMenu();yield return Wait(.15f);int writes=weather.AppliedStateCount;
            yield return Wait(.2f);
            Check(weather.Clock==0&&weather.AppliedStateCount==writes,"An idle menu does not repeatedly write unchanged sky, light or fog state");
            Begin();yield return Wait(.3f);writes=weather.AppliedStateCount;float clock=weather.Clock;
            yield return Wait(.3f);
            Check(weather.Clock>clock&&weather.AppliedStateCount==writes,"Ordinary daylight advances game weather time without redundant settled-state writes");
            game.Pause();weather.Seek(96f);float half=weather.RainAmount;writes=weather.AppliedStateCount;
            Check(Mathf.Abs(half-.5f)<.001f,"The rain transition retains its authored twelve-second smooth midpoint");
            weather.Seek(96.01f);
            Check(weather.RainAmount>half&&weather.RainAmount-half<.01f&&weather.AppliedStateCount==writes+1,"A real transition change is applied once and stays continuous");
            clock=weather.Clock;writes=weather.AppliedStateCount;yield return Wait(.15f);
            Check(weather.Clock==clock&&weather.AppliedStateCount==writes,"Paused transition time and rendering writes both remain frozen");
            weather.Seek(108f);game.Resume();yield return Wait(.6f);
            Check(weather.RainAmount==1&&weather.WeatherName=="YAĞMUR"&&game.Interface.atmosphere.text=="YAĞMUR","Cached settled rain retains its real weather and HUD state");
            Check(weather.Rain.particleCount>0&&weather.Rain.particleCount<=weather.MaxRainParticles&&weather.Rain.main.maxParticles==320,"Rain remains visible within its existing 320-particle pool");
            writes=weather.AppliedStateCount;yield return Wait(.15f);
            Check(weather.AppliedStateCount==writes,"Settled rain does not rewrite emission and global rendering state every frame");
            weather.Seek(150f);yield return Wait(.1f);
            Check(weather.WeatherName=="GECE MAÇI"&&weather.RainAmount==0&&game.Interface.atmosphere.text=="GECE MAÇI","Night transition still updates material state and the compact label");
            Begin();
            Check(weather.Clock==0&&weather.RainAmount==0&&weather.Rain.particleCount==0&&weather.WeatherName=="AÇIK HAVA","Restart forces daylight and clears previously visible rain");

            float deadline=Time.realtimeSinceStartup+1.2f;
            while(Time.realtimeSinceStartup<deadline)
            {ObserveParticles();yield return null;}
            report.footBursts=feedback.FootContactBurstCount;
            Check(report.footBursts>=2&&report.footBursts<=16,"Ordinary grounded running emits bounded puffs at actual Animator contact phases");
            Check(game.Motor.Grounded&&!game.Motor.Sliding&&!game.Motor.Flying,"Footstep evidence comes from ordinary grounded locomotion");
            var emitter=game.Motor.transform.Find("Landing and reward sparks");var streak=game.Motor.transform.Find("Pooled speed and slide accents");
            Check(emitter!=null&&streak!=null&&emitter.GetComponent<ParticleSystem>().main.maxParticles+streak.GetComponent<ParticleSystem>().main.maxParticles==104&&feedback.ParticleCapacity==104,"Vitality reuses the original two systems and total104-particle capacity");
            Vector3 emitterOrigin=emitter.localPosition;float crowdBefore=feedback.CrowdEnergy;feedback.Burst();feedback.PowerBurst(PowerupKind.Shield);
            Check(emitter.localPosition==emitterOrigin&&emitterOrigin==Vector3.zero,"Coin and power bursts use emission positions without teleporting already-live local particles");
            yield return Wait(.12f);
            Check(feedback.CrowdEnergy>crowdBefore&&feedback.CrowdEnergy<=1&&Mathf.Approximately(Shader.GetGlobalFloat("_CrowdEnergy"),feedback.CrowdEnergy),"A reward smoothly raises bounded cheering energy on the existing crowd shader");
            game.Pause();yield return new WaitForEndOfFrame();int footCount=feedback.FootContactBurstCount,liveCount=feedback.LiveParticleCount;float ring=feedback.RewardPulseRemaining,crowd=feedback.CrowdEnergy;
            yield return Wait(.2f);
            Check(feedback.FootContactBurstCount==footCount&&feedback.LiveParticleCount==liveCount&&feedback.RewardPulseRemaining==ring&&feedback.CrowdEnergy==crowd,"Pause freezes foot contacts, live particles, cheering energy and reward-ring lifetime");
            game.Resume();yield return Wait(.7f);Check(!feedback.RewardPulseVisible,"Reward ring expires after its short visible event");
            int landings=feedback.LandingBurstCount;footCount=feedback.FootContactBurstCount;
            Check(game.Motor.TryJump(),"Landing-feedback scenario begins with a real motor jump");yield return Wait(.14f);
            Check(!game.Motor.Grounded&&feedback.FootContactBurstCount==footCount,"Airborne animation cannot emit grounded contact dust");
            deadline=Time.realtimeSinceStartup+1.5f;
            while(!game.Motor.Grounded&&Time.realtimeSinceStartup<deadline){ObserveParticles();yield return null;}
            yield return new WaitForEndOfFrame();report.landingBursts=feedback.LandingBurstCount;
            Check(game.Motor.Grounded&&feedback.LandingBurstCount==landings+1,"One physical landing creates exactly one bounded landing response");
            Check(emitter.localPosition==emitterOrigin,"Landing emission does not move existing coin or power particles");
            ObserveParticles();Check(report.maximumObservedParticles<=feedback.ParticleCapacity,"Observed combined particles never exceed the fixed pool budget");
            game.ReturnToMenu();Check(feedback.LiveParticleCount==0&&!feedback.RewardPulseVisible&&feedback.FootContactBurstCount==0&&feedback.CrowdEnergy==0&&Shader.GetGlobalFloat("_CrowdEnergy")==0,"Menu reset clears all feedback, cheering energy and contact counters");

            Begin();yield return Wait(.1f);
            Vector2 start=new Vector2(Screen.width*.5f,Screen.height*.43f);
            float threshold=Mathf.Min(Screen.width,Screen.height)*game.Settings.swipeScreenFraction;
            input.TestBegin(start);input.TestMove(start+Vector2.right*threshold*.4f);input.TestEnd(start+Vector2.right*threshold*.6f);
            Check(commands.Count==0&&game.Motor.Lane==0,"Sub-threshold touch jitter cannot change lane");
            input.TestBegin(start);input.TestMove(start+Vector2.right*threshold*1.5f);input.TestMove(start+Vector2.left*threshold*3);input.TestEnd(start+Vector2.left*threshold*3);
            Check(commands.Count==1&&commands[0]==RunnerCommand.Right&&game.Motor.Lane==1,"A consumed swipe emits once even if the held finger reverses before release");
            commands.Clear();input.TestBegin(start);input.TestMove(start+Vector2.up*threshold*.3f);game.Pause();game.Resume();
            int jumps=game.Motor.JumpCount;input.TestEnd(start+Vector2.up*threshold*3);
            Check(commands.Count==0&&game.Motor.JumpCount==jumps,"Pause/resume discards the pending touch so a stale release cannot jump");
            Vector2 pausePoint=RectTransformUtility.WorldToScreenPoint(null,game.Interface.pause.transform.position);
            input.TestBegin(pausePoint);input.TestMove(start);input.TestEnd(start);
            Check(commands.Count==0,"A gesture beginning on pause remains blocked after dragging into the course");
            game.Pause();int lane=game.Motor.Lane;input.TestBegin(start);input.TestEnd(start+Vector2.left*threshold*2);
            Check(game.State==RunState.Paused&&game.Motor.Lane==lane,"A dispatched gesture cannot move the paused motor");
            game.Interface.OpenAudioSettings();game.HandleBack();Check(!game.Interface.AudioOpen&&game.State==RunState.Paused,"Back closes the audio modal before resuming gameplay");
            game.HandleBack();Check(game.State==RunState.Running,"The next Back event resumes the already-closed pause view");
            game.ReturnToMenu();game.Interface.OpenShop();game.HandleBack();
            Check(!game.Interface.ShopOpen&&game.State==RunState.Menu,"Workshop Back leaves the runner at the menu");
        }
        void ObserveParticles(){report.maximumObservedParticles=Mathf.Max(report.maximumObservedParticles,feedback.LiveParticleCount);}
        void Heard(RunnerCommand command){commands.Add(command);}
        bool PreferencesUnchanged()
        {
            bool equal=true;
            for(int i=0;i<IntKeys.Length;i++)equal&=PlayerPrefs.HasKey(IntKeys[i])==hadInts[i]&&PlayerPrefs.GetInt(IntKeys[i],0)==intValues[i];
            for(int i=0;i<FloatKeys.Length;i++)equal&=PlayerPrefs.HasKey(FloatKeys[i])==hadFloats[i]&&PlayerPrefs.GetFloat(FloatKeys[i],0)==floatValues[i];
            return equal;
        }
        static WaitForSecondsRealtime Wait(float seconds)=>new WaitForSecondsRealtime(seconds);
        void Check(bool ok,string name){checks.Add((ok?"PASS ":"FAIL ")+name);if(!ok)errors.Add(name);}
        void Log(string message,string trace,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<60)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;if(input!=null)input.Command-=Heard;Application.runInBackground=background;if(game!=null)game.enabled=true;Time.timeScale=1;}
    }
}
#endif
