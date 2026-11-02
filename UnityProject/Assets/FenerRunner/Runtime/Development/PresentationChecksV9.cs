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
    public sealed class PresentationChecksV9:MonoBehaviour
    {
        const string SessionKey="FenerRunner.PresentationChecksV9";
        [Serializable] sealed class ScreenCheck
        {public string screen;public int width,height,buttons,texts;public string[] outOfBounds,overflowingText;}
        [Serializable] sealed class Report
        {
            public bool passed,productionWalletUnchanged;
            public string utc,unityVersion;
            public string[] checks,errors,captures;
            public ScreenCheck[] screens;
            public int isolatedPurchases,initialRealBalance,finalRealBalance,feedbackParticleCapacity;
            public float wallSeconds;
            public string scope="Actual Unity Editor Game view and EventSystem pointer dispatch, native rendered screenshots at 1080x1920 and 1080x2340, UI layout/text measurements, unscaled transitions and real isolated-wallet purchases. Gameplay transitions use the real RunnerGame and obstacle contact. Synthetic pointers are not physical touchscreen input; this does not establish physical-device performance.";
        }
        RunnerGame game;RunnerUI ui;FollowCamera cameraRig;RunWallet wallet;
        readonly List<string> checks=new List<string>(),errors=new List<string>(),captures=new List<string>();
        readonly List<ScreenCheck> screens=new List<ScreenCheck>();
        readonly List<RaycastResult> hits=new List<RaycastResult>();
        readonly Report report=new Report();
        readonly string[] realWalletKeys={RunWallet.DefaultPrefix+"Coins",RunWallet.DefaultPrefix+"Upgrade.0",RunWallet.DefaultPrefix+"Upgrade.1",RunWallet.DefaultPrefix+"Upgrade.2"};
        readonly int[] realWalletValues=new int[4];readonly bool[] realWalletPresent=new bool[4];
        bool prepared,previousBackground,hadBest,hadScore;float previousBest,began;int previousScore;

        [MenuItem("Fener Runner/45 - Verify menu shop and presentation")]
        public static void StartChecks()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            SessionState.SetBool(SessionKey,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Register()
        {EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(SessionKey,false))return;
            SessionState.SetBool(SessionKey,false);new GameObject("Presentation checks v0.9").AddComponent<PresentationChecksV9>();
        }
        IEnumerator Start()
        {
            began=Time.realtimeSinceStartup;game=FindFirstObjectByType<RunnerGame>();
            previousBackground=Application.runInBackground;Application.runInBackground=true;
            hadBest=PlayerPrefs.HasKey("FenerRunner.Best");hadScore=PlayerPrefs.HasKey("FenerRunner.BestScore");
            previousBest=PlayerPrefs.GetFloat("FenerRunner.Best",0);previousScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);
            for(int i=0;i<realWalletKeys.Length;i++){realWalletPresent[i]=PlayerPrefs.HasKey(realWalletKeys[i]);realWalletValues[i]=PlayerPrefs.GetInt(realWalletKeys[i],0);}
            prepared=true;Application.logMessageReceived+=Log;
            var stack=new Stack<IEnumerator>();stack.Push(RunScenarios());
            while(stack.Count>0)
            {
                bool more;object wait=null;
                try{more=stack.Peek().MoveNext();if(more)wait=stack.Peek().Current;}
                catch(Exception exception){errors.Add(exception.ToString());break;}
                if(!more){stack.Pop();continue;}
                if(wait is IEnumerator child){stack.Push(child);continue;}
                yield return wait;
            }
            report.productionWalletUnchanged=WalletUnchanged();report.finalRealBalance=new RunWallet().Balance;
            Check(report.productionWalletUnchanged,"All production wallet keys and values remain unchanged");
            report.checks=checks.ToArray();report.errors=errors.ToArray();report.captures=captures.ToArray();report.screens=screens.ToArray();
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.wallSeconds=Time.realtimeSinceStartup-began;
            Directory.CreateDirectory("../Validation");File.WriteAllText(ReviewEvidence.FileFor(9,"../Validation/unity_presentation_v9.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Presentation v9 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));
            EditorApplication.isPlaying=false;
        }
        IEnumerator RunScenarios()
        {
            if(game==null||EventSystem.current==null)throw new InvalidOperationException("A built v0.9 scene and EventSystem are required.");
            game.AutomatedTest=true;ui=game.Interface;cameraRig=FindFirstObjectByType<FollowCamera>();
            report.initialRealBalance=game.WalletBalance;
            wallet=game.UseWalletForTest("FenerRunner.Test.PresentationV9."+DateTime.UtcNow.Ticks+".");
            game.Powerups.Enabled=false;game.Tokens.Enabled=false;game.ReturnToMenu();
            EditorApplication.ExecuteMenuItem("Fener Runner/5 - Open portrait Game view");yield return Wait(1f);
            Check(Screen.width==1080&&Screen.height==1920&&game.State==RunState.Menu&&ui.menu.activeSelf&&!ui.hud.activeSelf&&!ui.ShopOpen,
                "Portrait opens on the real menu with no running HUD or shop");
            Check(cameraRig!=null&&cameraRig.MenuPresentation&&ui.heroGesture!=null,
                "The menu has its showcase camera and mascot gesture target");
            Measure("portrait menu",ui.menu);yield return Capture("v9-presentation-menu");

            Vector2 backgroundPlay=Centre(ui.play.transform as RectTransform);
            Check(ClickButton(ui.openShop),"Native pointer opens the shop from the menu");yield return Wait(1f);
            var menuGroup=ui.menu.GetComponent<CanvasGroup>();
            Check(ui.ShopOpen&&game.State==RunState.Menu&&menuGroup!=null&&!menuGroup.interactable&&!menuGroup.blocksRaycasts&&menuGroup.alpha<.02f,
                "Shop is modal and fades out the noninteractive menu behind it");
            Check(ui.upgradeButtons.Length==3&&ui.upgradeCards!=null&&ui.upgradeCards.Length==3,
                "Exactly three visible permanent upgrade cards are bound");
            float[] baseline={5,5,6};bool initialCards=true;
            for(int i=0;i<3;i++)
                initialCards&=ui.upgradeCards[i].ShownLevel==0&&Mathf.Abs(ui.upgradeCards[i].ShownDuration-baseline[i])<.001f&&
                    !ui.upgradeCards[i].Affordable&&!ui.upgradeButtons[i].IsInteractable()&&ui.upgradePrices[i].text.Contains("30")&&
                    ui.upgradeCards[i].DurationLabel.text.Contains((baseline[i]+1).ToString("0"))&&ui.upgradeCards[i].LevelPips.Length==3;
            Check(initialCards,"Empty-wallet cards show correct starting durations, levels, prices and affordability");
            int emptyBalance=game.WalletBalance;ClickButton(ui.upgradeButtons[0]);
            Check(game.WalletBalance==emptyBalance&&game.UpgradeLevel(PowerupKind.Dash)==0,"An unaffordable native purchase cannot spend currency");
            GameObject intercepted=PointerTarget(backgroundPlay);
            Check(intercepted!=ui.play.gameObject&&intercepted!=ui.heroGesture.gameObject,
                "A pointer over the old menu CTA cannot hit a background menu control");
            ui.play.onClick.Invoke();Vector3 menuRoot=game.Motor.transform.position;game.HandleCommand(RunnerCommand.Right);
            Check(game.State==RunState.Menu&&ui.ShopOpen&&game.Distance==0&&game.Motor.transform.position==menuRoot,
                "Even a queued menu play callback and movement command cannot launch through the shop");
            Measure("portrait shop empty",ui.shopPanel);yield return Capture("v9-presentation-shop-empty");

            // Unscaled transitions remain usable while gameplay time is explicitly stopped.
            ui.CloseShop();yield return Wait(.35f);Time.timeScale=0;ui.OpenShop();
            var entrances=ui.shopPanel.GetComponentsInChildren<UIEntrance>();bool beganEntrance=false;
            foreach(var entrance in entrances){var group=entrance.GetComponent<CanvasGroup>();beganEntrance|=group!=null&&group.alpha<.5f;}
            Vector3 cameraBefore=cameraRig.transform.position;yield return Wait(1f);
            bool entrancesFinished=entrances.Length>0;
            foreach(var entrance in entrances)
            {var group=entrance.GetComponent<CanvasGroup>();entrancesFinished&=group!=null&&group.alpha>.99f&&!entrance.IsAnimating;}
            Check(beganEntrance&&entrancesFinished,"Shop entrances finish on unscaled time while Time.timeScale is zero");
            Check(Vector3.Distance(cameraBefore,cameraRig.transform.position)>.0001f&&game.Motor.transform.position==menuRoot&&game.Distance==0,
                "The restrained menu camera continues while the runner root remains stationary");
            var press=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=Centre((RectTransform)ui.closeShop.transform)};
            ExecuteEvents.Execute(ui.closeShop.gameObject,press,ExecuteEvents.pointerDownHandler);yield return Wait(.12f);
            float pressedScale=ui.closeShop.transform.localScale.x;
            ExecuteEvents.Execute(ui.closeShop.gameObject,press,ExecuteEvents.pointerUpHandler);yield return Wait(.35f);
            Check(pressedScale<.985f&&ui.closeShop.transform.localScale.x>.99f,
                "A real button press and release animate at zero gameplay time");

            wallet.Credit(1000);ui.CloseShop();ui.OpenShop();yield return Wait(1f);
            bool affordable=true;foreach(var button in ui.upgradeButtons)affordable&=button.IsInteractable();
            Check(affordable,"Earned isolated currency enables all three affordable upgrades");
            int before=game.WalletBalance;ClickButton(ui.upgradeButtons[0]);ClickButton(ui.upgradeButtons[0]);
            Check(game.WalletBalance==before-30&&game.UpgradeLevel(PowerupKind.Dash)==1&&Mathf.Abs(game.PowerupDuration(PowerupKind.Dash)-6)<.001f,
                "Two immediate pointer clicks produce exactly one level and one 30-coin deduction");
            report.isolatedPurchases++;
            Check(ui.upgradeCards[0].FeedbackPlaying&&ui.upgradeCards[0].ShownLevel==1&&ui.upgradeCards[0].ShownDuration==6&&
                ui.upgradeCards[0].LevelPips[0].color.a>.99f&&ui.upgradeCards[0].LevelPips[1].color.a<.5f,
                "Purchase feedback and card duration immediately reflect the saved level");
            yield return Wait(.7f);
            for(int i=1;i<3;i++)
            {
                before=game.WalletBalance;ClickButton(ui.upgradeButtons[i]);report.isolatedPurchases++;
                Check(game.WalletBalance==before-30&&game.UpgradeLevel((PowerupKind)i)==1&&
                    Mathf.Abs(ui.upgradeCards[i].ShownDuration-(baseline[i]+1))<.001f&&ui.upgradePrices[i].text.Contains("75"),
                    "Upgrade card "+i+" saves one level, adds one second and shows the next price");
                yield return Wait(.7f);
            }
            before=game.WalletBalance;ClickButton(ui.upgradeButtons[0]);report.isolatedPurchases++;yield return Wait(.7f);
            ClickButton(ui.upgradeButtons[0]);report.isolatedPurchases++;yield return Wait(.7f);
            Check(game.WalletBalance==before-75-150&&game.UpgradeLevel(PowerupKind.Dash)==3&&game.PowerupDuration(PowerupKind.Dash)==8&&
                !ui.upgradeButtons[0].IsInteractable()&&!ui.upgradeCards[0].Affordable&&ui.upgradePrices[0].text.Contains("TAMAM"),
                "Final two price tiers cap the upgrade at three levels and disable further purchase");
            before=game.WalletBalance;ClickButton(ui.upgradeButtons[0]);
            Check(game.WalletBalance==before&&game.UpgradeLevel(PowerupKind.Dash)==3,"Clicking a completed card cannot spend again");
            Measure("portrait shop upgraded",ui.shopPanel);yield return Capture("v9-presentation-shop-upgraded");
            Check(ClickButton(ui.closeShop),"Native return control closes the shop");yield return Wait(1f);
            Check(!ui.ShopOpen&&game.State==RunState.Menu&&menuGroup.interactable&&menuGroup.blocksRaycasts&&menuGroup.alpha>.99f,
                "Closing the shop restores menu visibility and input without starting travel");
            Time.timeScale=1;

            // The same transition sequence is rendered at both supported phone aspects.
            foreach(int height in new[]{1920,2340})
            {
                EditorApplication.ExecuteMenuItem(height==1920?"Fener Runner/5 - Open portrait Game view":"Fener Runner/10 - Open tall phone Game view");
                game.ReturnToMenu();yield return Wait(1f);
                Check(Screen.width==1080&&Screen.height==height,"Native Game view has 1080x"+height+" pixels");
                Measure(height+" menu",ui.menu);
                Check(ClickButton(ui.sound),"Native menu sound control opens settings at height "+height);yield return Wait(.8f);
                Check(ui.AudioOpen&&ui.volumeSliders.Length==4&&game.State==RunState.Menu,"Audio settings expose four channels without starting a run at height "+height);
                Measure(height+" audio settings",ui.audioPanel);yield return Capture("v11-audio-settings-"+height);
                Check(!ClickButton(ui.play)&&game.State==RunState.Menu,"The audio modal blocks the underlying menu CTA at height "+height);
                ClickButton(ui.closeAudio);yield return Wait(.3f);
                ClickButton(ui.openShop);yield return Wait(1f);Measure(height+" shop",ui.shopPanel);
                yield return Capture("v9-presentation-shop-"+height);
                ClickButton(ui.closeShop);yield return Wait(1f);
                Check(ClickButton(ui.play),"Native menu CTA starts the run at height "+height);game.Obstacles.SetActive(false);
                yield return Wait(.3f);
                Check(game.State==RunState.Running&&game.Distance>0&&ui.hud.activeSelf&&!ui.menu.activeSelf&&!ui.ShopOpen&&!cameraRig.MenuPresentation,
                    "Menu transitions immediately into the real running HUD at height "+height);
                Measure(height+" running HUD",ui.hud);yield return Capture("v9-presentation-run-"+height);
                if(height==1920)
                {
                    int objectCount=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
                    var feedback=game.GetComponent<RunnerFeedback>();game.Powerups.Enabled=true;game.Powerups.IsolatePickupsForTest();
                    game.Powerups.Activate(PowerupKind.Dash);yield return null;
                    int capacity=0,particles=0;
                    foreach(var system in game.Motor.GetComponentsInChildren<ParticleSystem>())
                        if(system.name=="Landing and reward sparks"||system.name=="Pooled speed and slide accents")
                        {capacity+=system.main.maxParticles;particles+=system.particleCount;}
                    report.feedbackParticleCapacity=capacity;
                    Check(feedback!=null&&feedback.ParticleCapacity==104&&capacity==104&&particles<=capacity&&feedback.RewardPulseVisible,
                        "A real power activation shows its reward pulse within the fixed 104-particle budget");
                    yield return Wait(.7f);game.Powerups.Enabled=false;
                    Check(!feedback.RewardPulseVisible&&FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==objectCount,
                        "Power presentation fades away without growing scene object pools");
                }
                ClickButton(ui.pause);Vector3 pausedRoot=game.Motor.transform.position;double pausedDistance=game.Distance;yield return Wait(1f);
                Check(game.State==RunState.Paused&&Time.timeScale==0&&ui.pausePanel.activeSelf&&!ui.hud.activeSelf&&
                    game.Distance==pausedDistance&&game.Motor.transform.position==pausedRoot,
                    "Pause presentation animates without advancing gameplay at height "+height);
                Measure(height+" pause",ui.pausePanel);yield return Capture("v9-presentation-pause-"+height);
                Check(ClickButton(ui.pauseSound),"Native pause sound control opens settings at height "+height);yield return Wait(.8f);
                Check(ui.AudioOpen&&game.State==RunState.Paused&&game.Distance==pausedDistance,"Audio adjustment preserves the paused run at height "+height);
                Check(!ClickButton(ui.resume),"The audio modal blocks the underlying resume control at height "+height);
                ClickButton(ui.closeAudio);yield return Wait(.2f);
                ClickButton(ui.resume);yield return Wait(.15f);
                Check(game.State==RunState.Running&&game.Distance>pausedDistance&&!ui.pausePanel.activeSelf,
                    "Native resume restores motion and removes the pause overlay at height "+height);
                game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+3);
                int frames=0;while(game.State==RunState.Running&&frames++<180)yield return null;
                // The death pose completes before Show(Finished) starts the card's own
                // unscaled entrance. Wait for the real control to receive a pointer,
                // rather than treating a populated score label as a clickable result.
                float resultDeadline=Time.realtimeSinceStartup+3f;
                while(Time.realtimeSinceStartup<resultDeadline&&!ResultReady(ui))yield return null;
                Check(game.State==RunState.Finished&&Time.timeScale==0&&ui.resultPanel.activeSelf&&!ui.hud.activeSelf&&
                    ui.resultScore.text==game.Score.ToString("N0"),
                    "Real obstacle contact opens a completed unscaled score count-up at height "+height);
                Measure(height+" result",ui.resultPanel);yield return Capture("v9-presentation-result-"+height);
                Check(ClickButton(ui.resultShop),"Native result-to-shop control receives the pointer after its entrance at height "+height);yield return Wait(1f);
                Check(game.State==RunState.Menu&&ui.ShopOpen&&!ui.resultPanel.activeSelf&&game.UpgradeLevel(PowerupKind.Dash)==3,
                    "Result-to-shop navigation preserves earned upgrade levels at height "+height);
                ClickButton(ui.closeShop);yield return Wait(.5f);
            }
            EditorApplication.ExecuteMenuItem("Fener Runner/5 - Open portrait Game view");game.ReturnToMenu();yield return Wait(1f);
            Check(game.WalletBalance==685&&game.UpgradeLevel(PowerupKind.Dash)==3&&game.UpgradeLevel(PowerupKind.Flight)==1&&game.UpgradeLevel(PowerupKind.Magnet)==1,
                "Only five intended isolated purchases changed currency across all navigation and run resets");
        }
        bool ResultReady(RunnerUI ui)
        {
            if(!ui.resultPanel.activeInHierarchy||!ui.resultShop.IsInteractable()||
                ui.resultScore.text!=game.Score.ToString("N0"))return false;
            foreach(var entrance in ui.resultPanel.GetComponentsInChildren<UIEntrance>())
                if(entrance.IsAnimating)return false;
            return PointerTarget(Centre((RectTransform)ui.resultShop.transform))==ui.resultShop.gameObject;
        }
        GameObject PointerTarget(Vector2 point)
        {
            var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
            hits.Clear();EventSystem.current.RaycastAll(data,hits);
            return hits.Count==0?null:ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        }
        bool ClickButton(Button button)
        {
            if(button==null||!button.gameObject.activeInHierarchy)return false;
            Vector2 point=Centre((RectTransform)button.transform);GameObject target=PointerTarget(point);
            if(target!=button.gameObject)return false;
            var data=new PointerEventData(EventSystem.current){position=point,pressPosition=point,button=PointerEventData.InputButton.Left,clickCount=1};
            ExecuteEvents.Execute(target,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(target,data,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target,data,ExecuteEvents.pointerClickHandler);return true;
        }
        static Vector2 Centre(RectTransform rect)
        {var corners=new Vector3[4];rect.GetWorldCorners(corners);return RectTransformUtility.WorldToScreenPoint(null,(corners[0]+corners[2])*.5f);}
        static bool InScreen(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            foreach(var corner in corners)if(corner.x<-.5f||corner.y<-.5f||corner.x>Screen.width+.5f||corner.y>Screen.height+.5f)return false;
            return true;
        }
        static float EffectiveAlpha(Transform target)
        {
            float alpha=1;
            while(target!=null){var group=target.GetComponent<CanvasGroup>();if(group!=null)alpha*=group.alpha;target=target.parent;}
            return alpha;
        }
        void Measure(string name,GameObject panel)
        {
            Canvas.ForceUpdateCanvases();var outside=new List<string>();var overflow=new List<string>();
            var sample=new ScreenCheck{screen=name,width=Screen.width,height=Screen.height};
            foreach(var button in panel.GetComponentsInChildren<Button>())
            {
                if(EffectiveAlpha(button.transform)<.02f)continue;sample.buttons++;
                if(!InScreen((RectTransform)button.transform))outside.Add("button: "+button.name);
            }
            foreach(var text in panel.GetComponentsInChildren<Text>())
            {
                if(string.IsNullOrWhiteSpace(text.text)||text.color.a<.02f||EffectiveAlpha(text.transform)<.02f)continue;sample.texts++;
                if(!InScreen(text.rectTransform))outside.Add("text: "+text.text);
                var settings=text.GetGenerationSettings(text.rectTransform.rect.size);
                if(text.resizeTextForBestFit&&text.cachedTextGenerator.fontSizeUsedForBestFit>0)
                    settings.fontSize=text.cachedTextGenerator.fontSizeUsedForBestFit;
                settings.resizeTextForBestFit=false;
                float preferred=text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,settings)/text.pixelsPerUnit;
                if(preferred>text.rectTransform.rect.height+2f)overflow.Add(text.text+" (needs "+preferred.ToString("F1")+", has "+text.rectTransform.rect.height.ToString("F1")+")");
            }
            sample.outOfBounds=outside.ToArray();sample.overflowingText=overflow.ToArray();screens.Add(sample);
            Check(outside.Count==0&&overflow.Count==0&&sample.buttons>0&&sample.texts>0,
                name+" keeps all visible controls/text on-screen without vertical text truncation");
        }
        bool WalletUnchanged()
        {
            for(int i=0;i<realWalletKeys.Length;i++)if(PlayerPrefs.HasKey(realWalletKeys[i])!=realWalletPresent[i]||PlayerPrefs.GetInt(realWalletKeys[i],0)!=realWalletValues[i])return false;
            return true;
        }
        IEnumerator Capture(string name)
        {yield return new WaitForEndOfFrame();string path=ReviewEvidence.FileFor(9,"../Validation/"+name+".png");ScreenCapture.CaptureScreenshot(path);captures.Add(path);yield return null;}
        static WaitForSecondsRealtime Wait(float seconds)=>new WaitForSecondsRealtime(seconds);
        void Check(bool condition,string label){if(condition)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type)
        {if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<40)errors.Add(message);}
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;Time.timeScale=1;
            if(!prepared)return;Application.runInBackground=previousBackground;
            if(wallet!=null)wallet.DeleteIsolatedTestData();
            if(hadBest)PlayerPrefs.SetFloat("FenerRunner.Best",previousBest);else PlayerPrefs.DeleteKey("FenerRunner.Best");
            if(hadScore)PlayerPrefs.SetInt("FenerRunner.BestScore",previousScore);else PlayerPrefs.DeleteKey("FenerRunner.BestScore");
            PlayerPrefs.Save();
        }
    }
}
#endif
