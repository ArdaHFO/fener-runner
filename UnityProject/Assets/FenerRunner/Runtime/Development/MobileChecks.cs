#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
namespace FenerRunner
{
    public sealed class MobileChecks : MonoBehaviour
    {
        [Serializable] class Report
        {
            public bool passed;public string utc,unityVersion;
            public string[] checks,errors;public int tokenPool,obstaclePool,initialObjects,finalObjects;
            public float recordingSeconds,runSeconds;public int nearCrowdTriangles,distantCrowdTriangles;
            public int routeSeed,routeJumps,routeSlides,routeLaneChanges;
            public string scope="Real Unity Editor Play Mode. Swipe state-machine input drives the actual motor, two portrait resolutions, recorded audio wiring and pooled procedural route gameplay. Not physical touchscreen, listening or on-device FPS validation.";
        }
        RunnerGame game;readonly List<string> checks=new List<string>(),errors=new List<string>();Report report=new Report();
        float previousBest;int previousScore;bool background;
        [MenuItem("Fener Runner/9 - Verify mobile interface and rewards")]
        public static void StartChecks(){SessionState.SetBool("FenerRunner.MobileChecks",true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("FenerRunner.MobileChecks",false))return;
            SessionState.SetBool("FenerRunner.MobileChecks",false);new GameObject("Mobile integration checks").AddComponent<MobileChecks>();
        }
        IEnumerator Start()
        {
            previousBest=PlayerPrefs.GetFloat("FenerRunner.Best",0);previousScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);
            background=Application.runInBackground;Application.runInBackground=true;Application.logMessageReceived+=Log;
            game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;game.Powerups.Enabled=false;
            game.GetComponent<StadiumSound>().UseAudioPreferencesForTest("FenerRunner.Test.MobileAudio."+DateTime.UtcNow.Ticks+".");
            yield return Wait(.8f);
            var weather=game.GetComponent<StadiumWeather>();
            Check(weather!=null&&weather.WeatherName=="AÇIK HAVA"&&game.Interface.atmosphere.text==weather.WeatherName,"New run begins in clear daylight with the matching HUD label");
            Check(Screen.width==1080&&Screen.height==1920,"Portrait 1080x1920 render target");
            Check(game.Interface.score.font.name=="BarlowCondensed-Bold","Bundled Barlow mobile typography");
            var title=game.Interface.menu.GetComponentsInChildren<Text>().Single(t=>t.text=="RUNNER");
            Check(title.preferredHeight<=title.rectTransform.rect.height,"Full RUNNER title fits its text rectangle");
            Check(game.Interface.play.GetComponent<Image>().sprite!=null,"Rounded button sprite is assigned");
            Check(game.Interface.menu.GetComponentsInChildren<UIIcon>().All(i=>i.GetComponent<CanvasRenderer>()!=null),"Menu icons have their required canvas renderer");
            Check(InScreen(game.Interface.play.transform as RectTransform)&&InScreen(game.Interface.sound.transform as RectTransform),"Menu CTA and sound control fit portrait screen");
            Check(game.Interface.left==null&&game.Interface.right==null&&game.Interface.jump==null&&game.Interface.slide==null,"No movement buttons are bound or generated");
            var runningButtons=game.Interface.hud.GetComponentsInChildren<Button>(true);
            Check(runningButtons.Length==1&&runningButtons[0]==game.Interface.pause,"Running HUD contains only the pause button");
            Check(((RectTransform)game.Interface.pause.transform).rect.width>=132&&((RectTransform)game.Interface.pause.transform).rect.height>=132,"Pause retains a 44-point touch target at a 360-point reference phone width");
            Check(game.Interface.hud.GetComponentsInChildren<Graphic>(true).Where(g=>g.raycastTarget).All(g=>g.gameObject==game.Interface.pause.gameObject),"HUD graphics and swipe guide do not consume movement gestures");
            yield return Capture("v7-menu");
            game.Interface.openShop.onClick.Invoke();
            Check(game.Interface.shopPanel.activeSelf&&game.Interface.walletShop.text.Contains("JETON"),"Workshop opens from menu and displays the persistent coin balance");
            Check(game.Interface.upgradeButtons.Length==3&&game.Interface.upgradeButtons.All(b=>InScreen((RectTransform)b.transform)),"All three earned-coin upgrades fit the portrait workshop");
            Check(InScreen((RectTransform)game.Interface.closeShop.transform),"Workshop return control fits portrait screen");
            yield return Wait(.35f);yield return Capture("v7-shop");game.Interface.closeShop.onClick.Invoke();
            Check(!game.Interface.shopPanel.activeSelf&&game.State==RunState.Menu,"Closing workshop returns to menu without starting a run");
            var recording=game.GetComponent<StadiumSound>();
            var crowd=game.GetComponent<StadiumSound>().CrowdSource;
            report.recordingSeconds=crowd.clip==null?0:crowd.clip.length;
            Check(recording.HasRecordedCrowd&&crowd.clip.length>40&&crowd.isPlaying,"Real recorded crowd loop is assigned and playing");
            var near=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FenerRunner/Generated/Meshes/Supporters.asset");
            var far=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FenerRunner/Generated/Meshes/DistantSupporters.asset");
            report.nearCrowdTriangles=near.triangles.Length/3;report.distantCrowdTriangles=far.triangles.Length/3;
            Check(report.distantCrowdTriangles<report.nearCrowdTriangles/8&&FindObjectsByType<LODGroup>(FindObjectsSortMode.None).Length>=9,"Distant crowd LOD reduces geometry by more than eight times");
            var black=game.Obstacles.Items.First(o=>o.kind==ObstacleKind.JumpOver);
            float blackHeight=black.GetComponentInChildren<Renderer>(true).bounds.size.y;
            Check(blackHeight>1.85f&&blackHeight<2.1f,"Black obstacle is taller than 1.85 metres and no longer flattened");
            Check(game.Obstacles.Items.Where(o=>o.gameObject.activeSelf).Min(o=>o.transform.position.z)>=70,"Opening gives a readable approach to the first obstacle");
            Check(game.Settings.forwardSpeed==18&&game.SpeedForDistance(750)==24&&game.SpeedForDistance(5000)==24,"Speed starts at 18 m/s and ramps to a capped 24 m/s");
            game.Interface.play.onClick.Invoke();game.Obstacles.SetActive(false);
            yield return Wait(.35f);
            Check(game.State==RunState.Running&&game.CurrentSpeed>18,"Mobile play starts progressive travel");
            var input=FindFirstObjectByType<RunnerInput>();
            Vector2 p=new Vector2(Screen.width*.5f,Screen.height*.5f);
            Swipe(input,p,Vector2.left*150);yield return Wait(.2f);
            Check(game.Motor.Lane==-1&&Mathf.Abs(game.Motor.transform.position.x+game.Settings.laneSpacing)<.03f,"Left swipe changes actual lane within 0.2 seconds");
            Swipe(input,p,Vector2.right*150);yield return Wait(.2f);
            Check(game.Motor.Lane==0,"Right swipe returns the actual runner to centre");
            Swipe(input,p,Vector2.up*150);yield return Wait(.2f);
            Check(!game.Motor.Grounded&&game.Animation.CurrentClip=="Jump","Up swipe drives actual jump physics and animation");
            yield return Wait(1f);Swipe(input,p,Vector2.down*150);yield return Wait(.2f);
            Check(game.Motor.Sliding&&game.Animation.CurrentClip=="Slide","Down swipe drives actual slide capsule and animation");
            yield return Wait(.9f);
            var emitted=new List<RunnerCommand>();Action<RunnerCommand> handler=emitted.Add;input.Command+=handler;
            input.TestBegin(p);input.TestMove(p+Vector2.right*150);input.TestEnd(p+Vector2.right*200);
            Check(emitted.Count==1&&emitted[0]==RunnerCommand.Right,"Swipe fires while moving and does not repeat on release");
            emitted.Clear();input.TestBegin(p);input.TestEnd(p+Vector2.right*5);
            Check(emitted.Count==0,"Tap jitter does not become a swipe");
            input.TestBegin(RectTransformUtility.WorldToScreenPoint(null,game.Interface.pause.transform.position));input.TestMove(p);input.TestEnd(p);
            Check(emitted.Count==0,"Gesture beginning on pause cannot trigger movement");
            input.Command-=handler;game.Restart();game.Obstacles.SetActive(false);
            for(int i=0;i<10;i++){game.Tokens.PlaceForTest(i,new Vector3(0,1.12f,game.Motor.transform.position.z+.4f));yield return null;}
            yield return null;
            Check(game.Coins==10&&game.FeverActive&&game.Charge==0,"Ten rewards activate five-second stadium fever");
            Check(game.CurrentSpeed>21&&game.CurrentSpeed<=27,"Fever adds a bounded 3 m/s speed boost");
            int coins=game.Coins;game.Tokens.PlaceForTest(10,new Vector3(0,1.12f,game.Motor.transform.position.z+.4f));yield return null;yield return null;
            Check(game.Coins==coins+2,"Fever doubles each token reward");
            yield return Capture("v7-fever");
            game.Interface.pause.onClick.Invoke();float fever=game.FeverRemaining,weatherClock=weather.Clock;double distance=game.Distance;
            yield return Wait(.4f);Check(game.FeverRemaining==fever&&game.Distance==distance,"Pause freezes bonus duration and distance");
            Check(weather.Clock==weatherClock,"Pause also freezes the weather timeline");
            bool muted=recording.Muted;game.Interface.pauseSound.onClick.Invoke();
            Check(game.Interface.AudioOpen&&game.Interface.volumeSliders.Length==4&&game.State==RunState.Paused,"Pause opens four adjustable volume channels while gameplay stays paused");
            game.Interface.audioMute.onClick.Invoke();
            Check(recording.Muted!=muted&&game.GetComponents<AudioSource>().All(s=>s.mute==recording.Muted),"Audio panel mute silences every fixed source");game.Interface.audioMute.onClick.Invoke();game.Interface.CloseAudioSettings();
            yield return Capture("v7-pause");
            game.Interface.resume.onClick.Invoke();yield return Wait(.2f);
            Check(game.State==RunState.Running&&game.FeverRemaining<fever,"Resume continues bonus countdown");
            game.Restart();Check(game.Coins==0&&game.Charge==0&&!game.FeverActive&&game.CurrentSpeed==18,"Retry clears tokens, bonus, speed and transient feedback");
            game.Obstacles.SetActive(false);
            weather.Seek(96f);float halfRain=weather.RainAmount;
            Check(halfRain>.45f&&halfRain<.55f,"Rain enters through a continuous twelve-second blend");
            weather.Seek(96.01f);Check(Mathf.Abs(weather.RainAmount-halfRain)<.01f,"Neighbouring weather times have continuous rain intensity");
            weather.Seek(108f);yield return Wait(.6f);
            Check(weather.RainAmount>.99f&&weather.Rain.particleCount>0&&weather.Rain.particleCount<=weather.MaxRainParticles&&weather.Rain.main.maxParticles==weather.MaxRainParticles,"Rain is visible and bounded to its fixed particle budget");
            Check(weather.WeatherName=="YAĞMUR"&&game.Interface.atmosphere.text==weather.WeatherName,"Rain atmosphere is reflected in the compact HUD");
            yield return Capture("v7-rain");
            weather.Seek(150f);yield return Wait(.9f);
            Check(weather.WeatherName=="GECE MAÇI"&&weather.RainAmount==0&&game.Interface.atmosphere.text==weather.WeatherName,"Night stadium atmosphere completes its smooth transition");
            yield return Capture("v7-night");
            game.Restart();game.Obstacles.SetActive(false);
            Check(weather.Clock==0&&weather.Rain.particleCount==0&&weather.WeatherName=="AÇIK HAVA","Restart resets weather and clears rain particles");
            EditorApplication.ExecuteMenuItem("Fener Runner/10 - Open tall phone Game view");yield return Wait(.7f);
            Check(Screen.width==1080&&Screen.height==2340&&InScreen((RectTransform)game.Interface.pause.transform)&&InScreen(game.Interface.score.rectTransform)&&InScreen(game.Interface.chargeFill.rectTransform),"Tall phone 1080x2340 keeps the compact HUD in bounds");
            Check(game.Interface.safeArea.anchorMin==Screen.safeArea.min/new Vector2(Screen.width,Screen.height)&&game.Interface.safeArea.anchorMax==Screen.safeArea.max/new Vector2(Screen.width,Screen.height),"Safe-area anchors follow the actual screen safe area");
            yield return Capture("v7-tall-phone");EditorApplication.ExecuteMenuItem("Fener Runner/5 - Open portrait Game view");yield return Wait(.5f);
            game.Obstacles.UseSeedForTest(1907);game.Obstacles.Enabled=true;game.Restart();yield return null;
            report.routeSeed=game.Obstacles.Seed;report.initialObjects=Count();report.tokenPool=game.Tokens.PoolCount;report.obstaclePool=game.Obstacles.PoolCount;
            float begin=Time.realtimeSinceStartup,oldZ=game.Motor.transform.position.z;bool shifted=false,stable=true,alive=true,captured=false;
            int lastActionSequence=-1;
            while(Time.realtimeSinceStartup-begin<60)
            {
                float z=game.Motor.transform.position.z;
                if(game.Obstacles.TryGetNextRoute(z,out CourseRoute route))
                {
                    float delta=route.Z-z;
                    if(delta<=25&&delta>12&&game.Motor.Lane!=route.Lane)
                    {game.HandleCommand(game.Motor.Lane<route.Lane?RunnerCommand.Right:RunnerCommand.Left);report.routeLaneChanges++;}
                    if(delta<=game.CurrentSpeed*(route.Action==RouteAction.Jump?game.Settings.JumpRiseSeconds:.42f)&&delta>0&&lastActionSequence!=route.Sequence&&game.Motor.Grounded)
                    {
                        if(route.Action==RouteAction.Jump){game.HandleCommand(RunnerCommand.Jump);report.routeJumps++;}
                        if(route.Action==RouteAction.Slide){game.HandleCommand(RunnerCommand.Slide);report.routeSlides++;}
                        lastActionSequence=route.Sequence;
                    }
                }
                shifted|=z<oldZ-100;oldZ=z;stable&=Count()==report.initialObjects;alive&=game.State==RunState.Running;
                if(!captured&&game.Distance>225){captured=true;yield return Capture("v7-running");}
                if(!alive)break;
                yield return Wait(.05f);
            }
            report.runSeconds=Time.realtimeSinceStartup-begin;report.finalObjects=Count();
            Check(alive&&shifted&&report.runSeconds>=60,"60-second procedural reward run survives floating-origin transition");
            Check(report.routeJumps>0&&report.routeSlides>0&&report.routeLaneChanges>5,"Procedural route exercises jump, slide and repeated lane changes");
            Check(stable&&game.Tokens.PoolCount==48&&game.Obstacles.PoolCount==ObstacleCourse.ObstacleCapacity&&report.initialObjects==report.finalObjects,"48 rewards, 60 obstacles, crowd and effects keep bounded object pools");
            Check(game.Coins>0&&game.Score>(int)game.Distance,"Route rewards and obstacle passes contribute to score");
            Check(game.Interface.swipeReminder.alpha==0,"Swipe reminder clears the playfield after the opening seconds");
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.checks=checks.ToArray();report.errors=errors.ToArray();
            File.WriteAllText(ReviewEvidence.File("../Validation/unity_mobile_v7.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Mobile v6 checks "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        static void Swipe(RunnerInput input,Vector2 origin,Vector2 offset){input.TestBegin(origin);input.TestMove(origin+offset);input.TestEnd(origin+offset);}
        static bool InScreen(RectTransform rect){var corners=new Vector3[4];rect.GetWorldCorners(corners);return corners.All(p=>p.x>=-1&&p.y>=-1&&p.x<=Screen.width+1&&p.y<=Screen.height+1);}
        static int Count()=>FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
        static WaitForSecondsRealtime Wait(float seconds)=>new WaitForSecondsRealtime(seconds);
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(ReviewEvidence.File("../Validation/"+name+".png"));yield return null;}
        void Check(bool ok,string label){if(ok)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;Application.runInBackground=background;if(game!=null)game.GetComponent<StadiumSound>().RestoreAudioPreferencesForTest();PlayerPrefs.SetFloat("FenerRunner.Best",previousBest);PlayerPrefs.SetInt("FenerRunner.BestScore",previousScore);PlayerPrefs.Save();}
    }
}
#endif
