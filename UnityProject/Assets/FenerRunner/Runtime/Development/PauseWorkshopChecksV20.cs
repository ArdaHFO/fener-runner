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
    public sealed class PauseWorkshopChecksV20:MonoBehaviour
    {
        const string Flag="FenerRunner.PauseWorkshopV20";
        [Serializable] sealed class Report{public bool passed,preferencesPreserved;public string utc;public string[] checks,errors,captures;public string scope="Native real-scene UI pointer raycasts and button events, 1080x1920 and1080x2340 portrait layouts, all scrollable purchases, pause-home settlement and resume. Isolated wallet. No APK or physical touch-device test.";}
        readonly List<string> checks=new List<string>(),errors=new List<string>(),captures=new List<string>();
        readonly List<RaycastResult> hits=new List<RaycastResult>();
        RunnerGame game;RunnerUI ui;RunWallet wallet;bool background;string preferences;
        [MenuItem("Fener Runner/121 - Verify pause home and workshop v20")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod]static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("V20 pause workshop validation").AddComponent<PauseWorkshopChecksV20>();}}
        IEnumerator Start()
        {
            background=Application.runInBackground;Application.runInBackground=true;preferences=PreferenceSnapshot();Application.logMessageReceived+=Log;
            var work=Scenarios();while(true){bool more;object wait=null;try{more=work.MoveNext();if(more)wait=work.Current;}catch(Exception e){errors.Add(e.ToString());break;}if(!more)break;yield return wait;}
            if(game!=null)game.ReturnToMenu();bool preserved=preferences==PreferenceSnapshot();Check(preserved,"Real wallet, all20 upgrade keys, vehicles and records preserved");
            var report=new Report{passed=errors.Count==0,preferencesPreserved=preserved,utc=DateTime.UtcNow.ToString("O"),checks=checks.ToArray(),errors=errors.ToArray(),captures=captures.ToArray()};
            File.WriteAllText("../Validation/unity_pause_workshop_v20.json",JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("V20 pause workshop "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Scenarios()
        {
            game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;game.enabled=false;ui=game.Interface;
            game.ReturnToMenu();wallet=game.UseWalletForTest("FenerRunner.Test.PauseWorkshopV20."+DateTime.UtcNow.Ticks+".");
            yield return Wait(1);
            Check(ui.upgradeButtons.Length==RunnerPowerups.TypeCount&&ui.permanentButtons.Length==5&&ui.vehicleButtons.Length==3,"All15 power, five permanent and three vehicle cards are present");
            game.Play();game.Tokens.Enabled=false;game.Obstacles.SetActive(false);game.Powerups.IsolatePickupsForTest();
            // Real pickup accounting method, then actual pause and home UI; no direct wallet credit in this settlement scenario.
            game.SendMessage("Collect",7,SendMessageOptions.RequireReceiver);int earnings=game.RunEarnings;
            game.Pause();yield return Wait(.9f);int settled=game.WalletBalance;
            Check(game.State==RunState.Paused&&settled==earnings&&Time.timeScale==0,"Pause checkpoints the earned chicks once");
            yield return Capture("v20-pause-home");Check(Click(ui.home),"Pause home button receives a native pointer click");yield return Wait(.9f);
            Check(game.State==RunState.Menu&&!ui.pausePanel.activeSelf&&ui.menu.activeSelf&&Time.timeScale==1&&game.WalletBalance==settled,"Home returns to menu with earnings preserved and normal time scale");
            game.ReturnToMenu();Check(game.WalletBalance==settled,"Repeated home transition never pays the same run twice");
            game.Play();game.Pause();yield return Wait(.8f);Check(Click(ui.resume),"Resume remains clickable beside the new home control");
            Check(game.State==RunState.Running&&Time.timeScale==1,"Resume still restores running state");game.ReturnToMenu();yield return Wait(.8f);
            wallet.Credit(20000);ui.OpenShop();yield return Wait(.9f);
            for(int i=0;i<ui.upgradeButtons.Length;i++)
            {
                Focus(0,i,280);yield return null;int before=game.WalletBalance,cost=game.UpgradeCost((PowerupKind)i);
                Check(Click(ui.upgradeButtons[i]),"Power card "+i+" reachable through the clipped native scroll viewport");
                Check(game.UpgradeLevel((PowerupKind)i)==1&&game.WalletBalance==before-cost,"Power card "+i+" buys exactly one persistent level");
                ui.upgradeButtons[i].onClick.Invoke();Check(game.UpgradeLevel((PowerupKind)i)==1,"Duplicate same-frame purchase is debounced "+i);yield return Wait(.58f);
            }
            Check(Click(ui.shopTabs[1]),"Permanent tab receives pointer");yield return null;
            for(int i=0;i<5;i++){Focus(1,i,292);yield return null;int before=game.WalletBalance,cost=game.PermanentCost((RunnerUpgrade)i);Check(Click(ui.permanentButtons[i]),"Permanent card pointer "+i);Check(game.PermanentLevel((RunnerUpgrade)i)==1&&game.WalletBalance==before-cost,"Permanent level is applied and charged once "+i);yield return Wait(.58f);}
            Check(Mathf.Abs(game.Tokens.CollectionReachBonus-.06f)<.0001f&&game.Tokens.HenNestLevel==1,"Permanent collection and hen effects reach the active course");
            Check(Click(ui.shopTabs[2]),"Garage tab receives pointer");yield return null;
            for(int i=1;i<3;i++){Focus(2,i,470);yield return null;int before=game.WalletBalance,cost=game.VehicleCost((ChaseVehicleKind)i);Check(Click(ui.vehicleButtons[i]),"Garage purchase pointer "+i);Check(game.OwnsVehicle((ChaseVehicleKind)i)&&game.SelectedVehicle==(ChaseVehicleKind)i&&game.WalletBalance==before-cost,"Vehicle purchase owns and equips exactly once "+i);yield return Wait(.58f);}
            Focus(2,0,470);yield return null;int balance=game.WalletBalance;Check(Click(ui.vehicleButtons[0]),"Owned SUV can be reselected");Check(game.SelectedVehicle==ChaseVehicleKind.RangeRover&&game.WalletBalance==balance,"Equipping an owned vehicle is free");yield return Wait(.6f);
            for(int size=0;size<2;size++)
            {
                int height=size==0?1920:2340;OpenSize(height);yield return Wait(1);
                Check(Screen.width==1080&&Screen.height==height,"Native portrait render size "+height);
                for(int tab=0;tab<3;tab++)
                {
                    Check(Click(ui.shopTabs[tab]),"Tab native pointer at "+height+" / "+tab);Focus(tab,0,tab==0?280:tab==1?292:470);yield return Wait(.1f);
                    Check(InScreen((RectTransform)ui.closeShop.transform)&&InScreen((RectTransform)ui.shopTabs[tab].transform),"Fixed navigation stays visible "+height+" / "+tab);
                    CheckText(ui.shopPanel);yield return Capture("v20-shop-"+tab+"-"+height);
                    int last=tab==0?14:tab==1?4:2;Focus(tab,last,tab==0?280:tab==1?292:470);yield return null;
                    Button end=tab==0?ui.upgradeButtons[last]:tab==1?ui.permanentButtons[last]:ui.vehicleButtons[last];
                    Check(Target(Centre((RectTransform)end.transform))==end.gameObject,"Last card is reachable without overlays "+height+" / "+tab);
                }
                ui.CloseShop();yield return Wait(.7f);game.Play();game.Pause();yield return Wait(.8f);
                Check(InScreen((RectTransform)ui.home.transform)&&InScreen((RectTransform)ui.pauseSound.transform),"Pause home and sound fit "+height);
                Check(Click(ui.home),"Pause home pointer works at "+height);yield return Wait(.7f);ui.OpenShop();yield return Wait(.8f);
            }
            ui.CloseShop();game.Play();int locked=game.WalletBalance;
            Check(!game.TryPurchaseUpgrade(PowerupKind.Shield)&&!game.TryPurchasePermanent(RunnerUpgrade.HenNest)&&!game.TryEquipVehicle(ChaseVehicleKind.SportCoupe)&&game.WalletBalance==locked,"Gameplay cannot spend or swap vehicles through public shop methods");
            game.ReturnToMenu();OpenSize(1920);
        }
        static void OpenSize(int height){Type.GetType("FenerRunner.Editor.PortraitReview, Assembly-CSharp-Editor",true).GetMethod("OpenSize").Invoke(null,new object[]{height});}
        void Focus(int tab,int index,float pitch)
        {
            ui.SelectShopTab(tab);var scroll=ui.shopScrolls[tab];Canvas.ForceUpdateCanvases();scroll.StopMovement();
            float height=scroll.viewport.rect.height;scroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(index*pitch+pitch*.5f+16-height*.5f,0,Mathf.Max(0,scroll.content.rect.height-height)));Canvas.ForceUpdateCanvases();
        }
        void CheckText(GameObject panel)
        {
            foreach(var t in panel.GetComponentsInChildren<Text>())
            {
                if(string.IsNullOrWhiteSpace(t.text)||!InScreen(t.rectTransform))continue;
                var generation=t.GetGenerationSettings(t.rectTransform.rect.size);if(t.resizeTextForBestFit&&t.cachedTextGenerator.fontSizeUsedForBestFit>0)generation.fontSize=t.cachedTextGenerator.fontSizeUsedForBestFit;generation.resizeTextForBestFit=false;
                float preferred=t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text,generation)/t.pixelsPerUnit;
                Check(preferred<=t.rectTransform.rect.height+2,"Visible text fits: "+t.name+" / "+t.text.Replace("\n"," "));
            }
        }
        bool Click(Button b){if(b==null||!b.gameObject.activeInHierarchy)return false;var point=Centre((RectTransform)b.transform);if(Target(point)!=b.gameObject)return false;var data=new PointerEventData(EventSystem.current){position=point,pressPosition=point,button=PointerEventData.InputButton.Left,clickCount=1};ExecuteEvents.Execute(b.gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(b.gameObject,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,data,ExecuteEvents.pointerClickHandler);return true;}
        GameObject Target(Vector2 point){hits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);return hits.Count==0?null:ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);}
        static Vector2 Centre(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);return (c[0]+c[2])*.5f;}
        static bool InScreen(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);foreach(var x in c)if(x.x<-.5f||x.y<-.5f||x.x>Screen.width+.5f||x.y>Screen.height+.5f)return false;return true;}
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();string path=Path.GetFullPath("../Validation/"+name+".png");ScreenCapture.CaptureScreenshot(path);captures.Add(path);yield return null;}
        static WaitForSecondsRealtime Wait(float s)=>new WaitForSecondsRealtime(s);
        void Check(bool condition,string message){if(condition)checks.Add(message);else errors.Add(message);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        static string PreferenceSnapshot(){var keys=new List<string>{"Coins","Vehicles","Vehicle.Selected"};for(int i=0;i<15;i++)keys.Add("Upgrade."+i);for(int i=0;i<5;i++)keys.Add("Permanent."+i);var s=new List<string>();foreach(string k in keys){string key=RunWallet.DefaultPrefix+k;s.Add(key+"="+PlayerPrefs.HasKey(key)+"/"+PlayerPrefs.GetInt(key));}s.Add(PlayerPrefs.GetInt("FenerRunner.BestScore").ToString());s.Add(PlayerPrefs.GetFloat("FenerRunner.Best").ToString("R"));return string.Join("|",s);}
        void OnDestroy(){Application.logMessageReceived-=Log;Application.runInBackground=background;Time.timeScale=1;wallet?.DeleteIsolatedTestData();}
    }
}
#endif
