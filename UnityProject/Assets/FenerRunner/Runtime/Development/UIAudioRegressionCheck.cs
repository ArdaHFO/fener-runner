#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FenerRunner
{
    public sealed class UIAudioRegressionCheck:MonoBehaviour
    {
        const string Flag="FenerRunner.UIAudioV12";
        static readonly string[] VolumeKeys={"Master","Music","Voices","Effects"};
        static readonly string[] WalletKeys={"Coins","Upgrade.0","Upgrade.1","Upgrade.2"};
        [Serializable] sealed class Report
        {
            public bool passed,preferencesPreserved;public string utc,unityVersion;public string[] checks,errors;
            public float audibleMusicPeak,wallSeconds;public int sourcesBefore,sourcesAfter;
            public string scope="Actual Editor UI, pointer raycasts, swipe input, HandleBack, AudioSource output and a real final obstacle collision. HUD boundary fixtures pause Game.Update while supplying presentation data, then restore live gameplay. Native1920/2340 layouts, bounded goal reveal, visible result count-up, pause/reset and isolated audio preferences. No phone-performance claim.";
        }
        readonly List<string> checks=new List<string>(),errors=new List<string>();readonly List<RaycastResult> hits=new List<RaycastResult>();
        readonly bool[] hadVolume=new bool[4],hadWallet=new bool[4];readonly float[] originalVolume=new float[4],samples=new float[2048];readonly int[] originalWallet=new int[4];
        RunnerGame game;RunnerUI ui;StadiumSound sound;bool background,prepared,hadMute;int originalMute;float began;Report report=new Report();
        [MenuItem("Fener Runner/65 - Verify v12 mobile progress and audio flow")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("UI and audio regression v12").AddComponent<UIAudioRegressionCheck>();}}
        IEnumerator Start()
        {
            began=Time.realtimeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;Application.logMessageReceived+=Log;
            game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;ui=game.Interface;sound=game.GetComponent<StadiumSound>();
            for(int i=0;i<4;i++)
            {
                string key=StadiumSound.DefaultAudioPrefix+VolumeKeys[i];hadVolume[i]=PlayerPrefs.HasKey(key);originalVolume[i]=PlayerPrefs.GetFloat(key,1);
                key=RunWallet.DefaultPrefix+WalletKeys[i];hadWallet[i]=PlayerPrefs.HasKey(key);originalWallet[i]=PlayerPrefs.GetInt(key,0);
            }
            hadMute=PlayerPrefs.HasKey("FenerRunner.Muted");originalMute=PlayerPrefs.GetInt("FenerRunner.Muted",0);
            sound.UseAudioPreferencesForTest("FenerRunner.Test.UIAudioV12."+DateTime.UtcNow.Ticks+".");prepared=true;sound.SetMutedForTest(false);
            report.sourcesBefore=game.GetComponents<AudioSource>().Length;
            var stack=new Stack<IEnumerator>();stack.Push(Scenarios());
            while(stack.Count>0)
            {
                bool more;object wait=null;try{more=stack.Peek().MoveNext();if(more)wait=stack.Peek().Current;}catch(Exception e){errors.Add(e.ToString());break;}
                if(!more){stack.Pop();continue;}if(wait is IEnumerator child){stack.Push(child);continue;}yield return wait;
            }
            game.enabled=true;game.ReturnToMenu();sound.RestoreAudioPreferencesForTest();prepared=false;
            report.preferencesPreserved=PreferencesUnchanged();Check(report.preferencesPreserved,"Production wallet, upgrades, mute and four volume preferences remain unchanged, including absent keys");
            report.sourcesAfter=game.GetComponents<AudioSource>().Length;Check(report.sourcesBefore==StadiumSound.SourceCount&&report.sourcesAfter==report.sourcesBefore,"Progress and overlay transitions retain the fixed six-source audio pool");
            report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.wallSeconds=Time.realtimeSinceStartup-began;
            report.checks=checks.ToArray();report.errors=errors.ToArray();report.passed=errors.Count==0;
            Directory.CreateDirectory("../Validation");File.WriteAllText(ReviewEvidence.FileFor(12,"../Validation/unity_ui_audio_v12.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("UI/audio v12 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void Begin(bool tokens=false)
        {
            game.enabled=true;game.Powerups.Enabled=false;game.Tokens.Enabled=tokens;game.Obstacles.Enabled=true;game.Play();game.Obstacles.SetActive(false);
            sound.SetChatterDueForTest(999);sound.SetRunMusicDueForTest(999);
        }
        IEnumerator Scenarios()
        {
            game.ReturnToMenu();yield return Wait(.6f);ui.OpenAudioSettings();yield return Wait(.2f);
            ui.volumeSliders[0].value=.81f;ui.volumeSliders[1].value=.63f;ui.volumeSliders[2].value=.74f;ui.volumeSliders[3].value=.42f;
            game.HandleBack();Check(!ui.AudioOpen&&game.State==RunState.Menu,"Menu Back closes audio settings without starting a run");
            ui.OpenShop();yield return Wait(.15f);game.HandleBack();Check(!ui.ShopOpen&&game.State==RunState.Menu,"Menu Back closes the workshop before changing gameplay state");
            Check(!ui.TryCloseOverlay(),"Back handler reports no modal when both overlays are closed");
            Begin();yield return Wait(.3f);game.Pause();double distance=game.Distance;ui.OpenAudioSettings();yield return Wait(.2f);
            game.HandleBack();yield return Wait(.15f);
            Check(game.State==RunState.Paused&&!ui.AudioOpen&&game.Distance==distance&&Time.timeScale==0,"Back from paused audio closes only its modal and preserves frozen travel");
            Check(Mathf.Abs(sound.GetVolume(AudioChannel.Master)-.81f)<.001f&&Mathf.Abs(sound.GetVolume(AudioChannel.Music)-.63f)<.001f&&Mathf.Abs(sound.GetVolume(AudioChannel.Voices)-.74f)<.001f&&Mathf.Abs(sound.GetVolume(AudioChannel.Effects)-.42f)<.001f,"Modal Back preserves independent channel selections");
            game.HandleBack();yield return new WaitForEndOfFrame();
            Check(game.State==RunState.Running&&Vector3.Distance(ui.coins.rectTransform.localScale,Vector3.one)<.002f,"Resume does not fabricate a new-coin pulse for an unchanged balance");

            // Presentation fixtures keep the real HUD/canvas/input, but freeze Game.Update
            // so mechanics cannot replace intentionally extreme test values each frame.
            game.enabled=false;
            foreach(int height in new[]{1920,2340})
            {
                EditorApplication.ExecuteMenuItem(height==1920?"Fener Runner/5 - Open portrait Game view":"Fener Runner/10 - Open tall phone Game view");yield return Wait(.35f);
                ui.ClearFeedback();ui.SetRunProgress(int.MaxValue,2,"UZUN TELEFON EKRANINDA TEMİZ GEÇİŞ HEDEFİ",int.MaxValue-1,int.MaxValue,int.MaxValue);
                ui.score.text=int.MaxValue.ToString("N0")+" <size=18>×2</size>";ui.coins.text=int.MaxValue.ToString();yield return Wait(.4f);
                Check(Screen.width==1080&&Screen.height==height,"Native progress fixture uses1080x"+height);
                Check(Fits(ui.combo)&&Fits(ui.challengeLabel)&&Fits(ui.challengeProgress)&&Fits(ui.score)&&Fits(ui.coins),"Large score/streak/coin counters and long challenge labels fit at height "+height);
                Check(!Intersects(ui.challengeProgress.rectTransform,(RectTransform)ui.powerPanel.transform)&&!Intersects(ui.combo.rectTransform,ui.challengeLabel.rectTransform),"The compact skill/goal row leaves space for the power card at height "+height);
                var buttons=ui.hud.GetComponentsInChildren<Button>();Check(buttons.Length==1&&buttons[0]==ui.pause,"The running HUD retains only its pause button at height "+height);
                foreach(Text text in new[]{ui.combo,ui.challengeLabel,ui.challengeProgress})
                    Check(PointerTarget(Centre(text.rectTransform))==null,"Progress readout does not consume touch at height "+height+": "+text.name);
                var input=FindFirstObjectByType<RunnerInput>();int before=game.Motor.Lane;var start=Centre(ui.challengeLabel.rectTransform);
                int direction=before==1?-1:1;input.TestBegin(start);input.TestMove(start+Vector2.right*direction*140);input.TestEnd(start+Vector2.right*direction*140);
                Check(game.Motor.Lane==before+direction,"A real swipe starting over the challenge row still changes lane at height "+height);
                ui.ClearFeedback();ui.Show(RunState.Running);ui.SetRunProgress(8,1.5f,"TEMİZ GEÇİŞ",4,6,20);yield return Wait(.4f);
                Check(ui.combo.text.Contains("8")&&ui.combo.text.Contains("1,5")&&Mathf.Abs(ui.challengeFill.fillAmount-4f/6)<.015f,"Skill multiplier and challenge fraction show the supplied values at height "+height);
                yield return Capture("v12-progression-hud-"+height);
            }
            ui.SetRunProgress(9,1.6f,"TEMİZ GEÇİŞ",6,6,20);yield return Wait(.1f);
            Check(ui.GoalCompletionVisible&&ui.challengeLabel.text=="HEDEF TAMAM"&&ui.challengeProgress.text.Contains("20")&&ui.challengeFill.fillAmount==1,"A completed goal visibly reveals its one-shot reward in the existing row");
            ui.SetRunProgress(9,1.6f,"JETON TOPLA",0,18,25);game.Pause();yield return Wait(1f);
            Check(ui.GoalCompletionVisible,"Pause freezes the remaining goal-completion reveal");game.Resume();yield return Wait(1.3f);
            Check(!ui.GoalCompletionVisible&&ui.challengeLabel.text=="JETON TOPLA"&&ui.challengeProgress.text.Contains("18")&&ui.challengeFill.fillAmount<.01f,"The reveal yields to the next rotating goal without retaining the old progress");
            ui.ClearFeedback();ui.SetRunProgress(0,1,"JETON AVI",0,20,20);ui.SetRunProgress(0,1,"MESAFE USTASI",0,350,25);yield return Wait(.05f);
            Check(ui.GoalCompletionVisible&&ui.challengeProgress.text.Contains("20"),"A goal completed from zero within one gameplay update still reveals the previous reward after rotation");
            ui.ClearFeedback();ui.SetRunProgress(0,1,"TEMİZ GEÇİŞ",0,4,15);
            Check(!ui.GoalCompletionVisible,"Reset placeholder to first real goal never invents a completion reveal");
            game.enabled=true;Begin();yield return Wait(.1f);
            Check(!ui.GoalCompletionVisible&&ui.combo.text.Contains("SERİ 0"),"Restart clears the previous skill streak and completion reveal");

            for(int i=0;i<4;i++)sound.SetVolume((AudioChannel)i,1);
            sound.SetRunMusicDueForTest(0);float deadline=Time.realtimeSinceStartup+2;
            while(!sound.RunMusicActive&&Time.realtimeSinceStartup<deadline)yield return null;
            float sampleUntil=Time.realtimeSinceStartup+.2f;
            while(Time.realtimeSinceStartup<sampleUntil){sound.MenuMusicSource.GetOutputData(samples,0);foreach(float sample in samples)report.audibleMusicPeak=Mathf.Max(report.audibleMusicPeak,Mathf.Abs(sample));yield return null;}
            Check(sound.RunMusicActive&&report.audibleMusicPeak>.00001f,"The preserved ses8 excerpt emits a real AudioSource signal after UI flow changes");
            sound.LaneChanged(0,1);sound.Hit(ObstacleKind.JumpOver);
            Check(!sound.RunMusicActive&&!sound.MenuMusicSource.isPlaying&&!sound.LaneSource.isPlaying,"A black-obstacle fatal audio event stops both music and lane clip synchronously");

            Begin(true);
            for(int i=0;i<game.Tokens.PoolCount;i++)game.Tokens.PlaceForTest(i,new Vector3(15,1,game.Motor.transform.position.z+1000+i*4));
            for(int i=0;i<3;i++)game.Tokens.PlaceForTest(i,new Vector3(0,1.1f,game.Motor.transform.position.z+.8f+i*.8f));
            game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+12,0);
            deadline=Time.realtimeSinceStartup+2;while(game.State==RunState.Running&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(game.State==RunState.Finished&&game.Score>=50,"Result timing scenario reaches an actual fatal obstacle after collecting real coins");
            ui.SetRunSummary(12,2,40);yield return Wait(.35f);
            Check(!ui.resultPanel.activeSelf&&ui.resultScore.text=="0","The result counter stays at zero while the real death presentation hides it");
            deadline=Time.realtimeSinceStartup+2;while(!ui.resultPanel.activeSelf&&Time.realtimeSinceStartup<deadline)yield return null;
            int first=ReadScore();Check(ui.resultPanel.activeSelf&&first>=0&&first<game.Score,"Result count-up starts after the result panel becomes visible");
            yield return Wait(.22f);int middle=ReadScore();Check(middle>first&&middle<game.Score,"The user can see intermediate result-score values");
            yield return Wait(.8f);Check(ui.resultScore.text==game.Score.ToString("N0"),"The visible result animation reaches the exact earned score");
            Check(ui.resultSkill.text.Contains("12")&&ui.resultSkill.text.Contains("2 HEDEF")&&ui.resultSkill.text.Contains("40")&&Fits(ui.resultSkill),"The result summary shows best streak, completed goals and goal coins without overlap");
            EditorApplication.ExecuteMenuItem("Fener Runner/5 - Open portrait Game view");yield return Wait(.25f);yield return Capture("v12-progression-result");
        }
        int ReadScore(){return int.TryParse(ui.resultScore.text,NumberStyles.AllowThousands,CultureInfo.CurrentCulture,out int value)?value:-1;}
        bool PreferencesUnchanged()
        {
            bool result=PlayerPrefs.HasKey("FenerRunner.Muted")==hadMute&&PlayerPrefs.GetInt("FenerRunner.Muted",0)==originalMute;
            for(int i=0;i<4;i++)
            {
                string key=StadiumSound.DefaultAudioPrefix+VolumeKeys[i];result&=PlayerPrefs.HasKey(key)==hadVolume[i]&&PlayerPrefs.GetFloat(key,1)==originalVolume[i];
                key=RunWallet.DefaultPrefix+WalletKeys[i];result&=PlayerPrefs.HasKey(key)==hadWallet[i]&&PlayerPrefs.GetInt(key,0)==originalWallet[i];
            }
            return result;
        }
        bool Fits(Text text)
        {
            Canvas.ForceUpdateCanvases();var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
            foreach(var p in corners)if(p.x<-.5f||p.y<-.5f||p.x>Screen.width+.5f||p.y>Screen.height+.5f)return false;
            var settings=text.GetGenerationSettings(text.rectTransform.rect.size);
            if(text.resizeTextForBestFit&&text.cachedTextGenerator.fontSizeUsedForBestFit>0)settings.fontSize=text.cachedTextGenerator.fontSizeUsedForBestFit;
            settings.resizeTextForBestFit=false;
            return text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,settings)/text.pixelsPerUnit<=text.rectTransform.rect.height+2;
        }
        static bool Intersects(RectTransform a,RectTransform b)
        {var ca=new Vector3[4];var cb=new Vector3[4];a.GetWorldCorners(ca);b.GetWorldCorners(cb);return ca[0].x<cb[2].x&&ca[2].x>cb[0].x&&ca[0].y<cb[2].y&&ca[2].y>cb[0].y;}
        static Vector2 Centre(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);return(c[0]+c[2])*.5f;}
        GameObject PointerTarget(Vector2 point)
        {var data=new PointerEventData(EventSystem.current){position=point};hits.Clear();EventSystem.current.RaycastAll(data,hits);return hits.Count==0?null:hits[0].gameObject;}
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(ReviewEvidence.FileFor(12,"../Validation/"+name+".png"));yield return null;}
        static WaitForSecondsRealtime Wait(float value)=>new WaitForSecondsRealtime(value);
        void Check(bool condition,string message){if(condition)checks.Add(message);else errors.Add(message);}
        void Log(string message,string trace,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<40)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;Application.runInBackground=background;if(game!=null)game.enabled=true;if(prepared&&sound!=null)sound.RestoreAudioPreferencesForTest();Time.timeScale=1;}
    }
}
#endif
