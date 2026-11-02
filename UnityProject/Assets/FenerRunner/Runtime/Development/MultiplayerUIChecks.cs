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
    public sealed class MultiplayerUIChecks:MonoBehaviour
    {
        const string Flag="FenerRunner.MultiplayerUIChecksV14";
        [Serializable] sealed class Report
        {
            public bool passed,preferencesPreserved;public string utc,unityVersion;public string[] checks,errors,captures;
            public float wallSeconds;
            public string scope="Native Unity UI raycasts, modal isolation, real InputField filtering, two-step keyboard Back/cancel recovery and rendered 1080x1920/2340 layouts. Keyboard occlusion uses a labelled half-screen geometry fixture, not a physical keyboard. No Relay request is made by this harness. It does not claim two-phone connectivity, mobile keyboard performance or a successful remote race.";
        }
        struct Preference
        {
            public string key;public bool floating,exists;public int integer;public float number;
            public Preference(string name,bool isFloat=false){key=name;floating=isFloat;exists=PlayerPrefs.HasKey(name);integer=isFloat?0:PlayerPrefs.GetInt(name,0);number=isFloat?PlayerPrefs.GetFloat(name,0):0;}
            public bool Unchanged=>PlayerPrefs.HasKey(key)==exists&&(floating?PlayerPrefs.GetFloat(key,0)==number:PlayerPrefs.GetInt(key,0)==integer);
        }
        RunnerGame game;RunnerUI ui;MultiplayerUI room;float began;bool background;
        readonly List<string> checks=new List<string>(),errors=new List<string>(),captures=new List<string>();
        readonly List<RaycastResult> hits=new List<RaycastResult>();readonly Vector3[] corners=new Vector3[4];
        Preference[] preferences;
        [MenuItem("Fener Runner/85 - Verify friend race mobile UI")]
        public static void StartChecks()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before UI checks.");
            SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Friend race mobile UI checks").AddComponent<MultiplayerUIChecks>();}}
        IEnumerator Start()
        {
            began=Time.realtimeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;Application.logMessageReceived+=Log;
            preferences=new[]{new Preference(RunWallet.DefaultPrefix+"Coins"),new Preference(RunWallet.DefaultPrefix+"Upgrade.0"),new Preference(RunWallet.DefaultPrefix+"Upgrade.1"),new Preference(RunWallet.DefaultPrefix+"Upgrade.2"),new Preference("FenerRunner.Best",true),new Preference("FenerRunner.BestScore"),new Preference("FenerRunner.Muted"),new Preference(StadiumSound.DefaultAudioPrefix+"Master",true),new Preference(StadiumSound.DefaultAudioPrefix+"Music",true),new Preference(StadiumSound.DefaultAudioPrefix+"Voices",true),new Preference(StadiumSound.DefaultAudioPrefix+"Effects",true)};
            var stack=new Stack<IEnumerator>();stack.Push(Scenarios());
            while(stack.Count>0)
            {
                bool more;object wait=null;try{more=stack.Peek().MoveNext();if(more)wait=stack.Peek().Current;}catch(Exception exception){errors.Add(exception.ToString());break;}
                if(!more){stack.Pop();continue;}if(wait is IEnumerator child){stack.Push(child);continue;}yield return wait;
            }
            if(room!=null&&room.IsOpen)room.Close();Time.timeScale=1;
            if(game!=null){game.enabled=true;game.ReturnToMenu();}
            bool preserved=true;for(int i=0;i<preferences.Length;i++)preserved&=preferences[i].Unchanged;
            Check(preserved,"Production wallet, upgrades, records and all audio settings are unchanged, including absent keys");
            var report=new Report{passed=errors.Count==0,preferencesPreserved=preserved,utc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion,wallSeconds=Time.realtimeSinceStartup-began,checks=checks.ToArray(),errors=errors.ToArray(),captures=captures.ToArray()};
            Directory.CreateDirectory("../Validation");File.WriteAllText(ReviewEvidence.FileFor(14,"../Validation/unity_multiplayer_ui_v14.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Multiplayer UI v14 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Scenarios()
        {
            game=FindFirstObjectByType<RunnerGame>();room=FindFirstObjectByType<MultiplayerUI>();
            if(game==null||room==null||EventSystem.current==null)throw new InvalidOperationException("The built scene must initialize its multiplayer UI.");
            game.AutomatedTest=true;ui=game.Interface;game.ReturnToMenu();
            Check(!room.IsOpen&&!room.IsSessionActive,"Fresh launch remains offline and keeps the solo menu available");
            Check(MultiplayerUI.NormalizeCode(" ab-ç!90xyZ123 ")=="AB90XYZ1","Room codes discard spaces, punctuation and non-ASCII letters, uppercase and cap at eight characters");
            Check(MultiplayerUI.NormalizeCode(null)==""&&MultiplayerUI.NormalizeCode(" ")=="","Empty room codes stay empty");
            // Unity creates its InputField caret lazily on first editing. Warm that
            // built-in object once, then assert that repeated room visits reuse it.
            room.Open();room.CodeInput.ActivateInputField();yield return null;yield return null;
            room.CodeInput.DeactivateInputField();room.Close();yield return null;
            int objectCount=ui.GetComponentsInChildren<Transform>(true).Length;
            foreach(int height in new[]{1920,2340})
            {
                EditorApplication.ExecuteMenuItem(height==1920?"Fener Runner/5 - Open portrait Game view":"Fener Runner/10 - Open tall phone Game view");yield return Wait(.85f);
                Check(Screen.width==1080&&Screen.height==height,"Native Game view uses 1080x"+height);
                Measure(ui.menu,"menu "+height);
                Check(((RectTransform)room.OpenButton.transform).rect.height>=132&&((RectTransform)ui.openShop.transform).rect.height>=132,"Both menu tiles retain a full 132px logical touch height at "+height);
                yield return Capture("v14-friends-menu-"+height);
                Check(Click(room.OpenButton),"Native pointer opens the friend room at "+height);yield return Wait(.6f);
                var menuGroup=ui.menu.GetComponent<CanvasGroup>();
                Check(room.IsOpen&&ui.MultiplayerOpen&&game.State==RunState.Menu&&!menuGroup.interactable&&!menuGroup.blocksRaycasts&&menuGroup.alpha<.02f,"Room blocks and fades the menu while gameplay remains stopped at "+height);
                Measure(room.Panel,"room "+height);
                yield return Capture("v14-friends-room-"+height);
                Check(room.CodeInput.characterLimit==8&&room.CodeInput.lineType==InputField.LineType.SingleLine,"Code field is single-line with an eight-character limit");
                room.CodeInput.text="aB- c!12";Check(room.CodeInput.text=="ABC12","Actual InputField normalizes typed/pasted code characters");
                room.CodeInput.text="1234567890AB";Check(room.CodeInput.text=="12345678","Actual InputField refuses a ninth code character");
                room.CodeInput.text="";Check(Click(room.JoinButton),"Native pointer reaches the join control");yield return null;
                Check(!room.IsSessionActive&&room.IsOpen&&!string.IsNullOrWhiteSpace(room.DisplayedStatus),"Empty-code validation stays in the room without starting a service request");
                Check(!RaycastTargets(ui.play),"Full room backdrop prevents pointer hits from reaching the solo CTA");
                ui.play.onClick.Invoke();ui.openShop.onClick.Invoke();ui.sound.onClick.Invoke();
                Check(game.State==RunState.Menu&&!ui.ShopOpen&&!ui.AudioOpen&&room.IsOpen,"Background solo, shop and audio callbacks are all guarded while the room is open");
                room.CodeInput.text="AB12";room.CodeInput.ActivateInputField();yield return Wait(.1f);
                Check(room.CodeInput.isFocused,"Actual code field enters editing before the keyboard layout fixture");
                room.SetKeyboardHeightForTest(height*.5f);yield return Wait(.15f);Canvas.ForceUpdateCanvases();
                Check(room.KeyboardLayoutActive,"Half-screen keyboard fixture selects compact code-entry layout at "+height);
                Measure(room.Panel,"keyboard fixture "+height);
                Check(AboveKeyboard((RectTransform)room.CodeInput.transform,height*.5f)&&AboveKeyboard((RectTransform)room.JoinButton.transform,height*.5f),"Actual code field and join control stay above the keyboard exclusion area at "+height);
                game.HandleBack();yield return Wait(.15f);
                Check(room.IsOpen&&!room.CodeInput.isFocused&&!room.KeyboardLayoutActive&&room.CodeInput.text=="AB12","First Back dismisses code editing, restores the full room and retains the entered code");
                room.SetKeyboardHeightForTest(-1);
                game.HandleBack();yield return Wait(.3f);
                Check(!room.IsOpen&&!ui.MultiplayerOpen&&!room.IsSessionActive&&game.State==RunState.Menu,"Back cancels the room without starting or resuming gameplay");
                Check(!ui.TryCloseOverlay(),"Closed room does not consume subsequent Back handling");
                ui.OpenShop();Check(ui.ShopOpen,"Shop is usable after room cancellation");room.Open();Check(!room.IsOpen,"Room cannot open above the workshop");ui.CloseShop();
                ui.OpenAudioSettings();Check(ui.AudioOpen,"Saved sound settings remain accessible after room cancellation");room.Open();Check(!room.IsOpen,"Room cannot open above audio settings");ui.CloseAudioSettings();
                room.Open();Time.timeScale=0;yield return Wait(.6f);
                Check(room.IsOpen&&Click(room.LeaveButton),"Room entry and cancellation remain responsive with timeScale zero");
                Check(!room.IsOpen&&game.State==RunState.Menu,"Explicit leave returns to the solo menu");Time.timeScale=1;
            }
            Check(ui.GetComponentsInChildren<Transform>(true).Length==objectCount,"Opening, cancelling and resizing reuse the fixed UI hierarchy");
            var hudButtons=ui.hud.GetComponentsInChildren<Button>(true);
            Check(hudButtons.Length==1&&hudButtons[0]==ui.pause,"Ghost race HUD adds no movement or extra gameplay buttons");
            game.Powerups.Enabled=false;game.Tokens.Enabled=false;ui.play.onClick.Invoke();game.Obstacles.SetActive(false);yield return Wait(.15f);
            Check(game.State==RunState.Running&&game.Distance>0,"Solo play still starts and moves after repeated room cancellation");
            game.ReturnToMenu();EditorApplication.ExecuteMenuItem("Fener Runner/5 - Open portrait Game view");
        }
        bool RaycastTargets(Button target)
        {
            hits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=Centre((RectTransform)target.transform)},hits);
            return hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==target;
        }
        bool Click(Button target)
        {
            if(target==null||!target.gameObject.activeInHierarchy||!target.IsInteractable()||!RaycastTargets(target))return false;
            var pointer=new PointerEventData(EventSystem.current){position=Centre((RectTransform)target.transform),button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(target.gameObject,pointer,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(target.gameObject,pointer,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(target.gameObject,pointer,ExecuteEvents.pointerClickHandler);return true;
        }
        static Vector2 Centre(RectTransform target)=>RectTransformUtility.WorldToScreenPoint(null,target.TransformPoint(target.rect.center));
        bool InScreen(RectTransform target)
        {
            target.GetWorldCorners(corners);
            for(int i=0;i<4;i++){var p=RectTransformUtility.WorldToScreenPoint(null,corners[i]);if(p.x<-.5f||p.y<-.5f||p.x>Screen.width+.5f||p.y>Screen.height+.5f)return false;}return true;
        }
        bool AboveKeyboard(RectTransform target,float top)
        {target.GetWorldCorners(corners);for(int i=0;i<4;i++)if(RectTransformUtility.WorldToScreenPoint(null,corners[i]).y<top+8)return false;return true;}
        void Measure(GameObject panel,string description)
        {
            Canvas.ForceUpdateCanvases();int controls=0,labels=0;
            foreach(var control in panel.GetComponentsInChildren<Selectable>()){controls++;Check(InScreen((RectTransform)control.transform),description+" keeps "+control.name+" within the screen");}
            foreach(var text in panel.GetComponentsInChildren<Text>())
            {
                if(string.IsNullOrWhiteSpace(text.text)||text.color.a<.02f)continue;labels++;
                Check(InScreen(text.rectTransform),description+" keeps text "+text.name+" within the screen");
                var settings=text.GetGenerationSettings(text.rectTransform.rect.size);settings.resizeTextForBestFit=false;
                float needed=text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,settings)/text.pixelsPerUnit;
                Check(needed<=text.rectTransform.rect.height+2,description+" fits text height "+text.name+" ("+needed.ToString("F1")+"/"+text.rectTransform.rect.height.ToString("F1")+")");
            }
            Check(controls>0&&labels>0,description+" contains visible controls and labels");
        }
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();string path=ReviewEvidence.FileFor(14,"../Validation/"+name+".png");ScreenCapture.CaptureScreenshot(path);captures.Add(path);yield return null;}
        static WaitForSecondsRealtime Wait(float seconds)=>new WaitForSecondsRealtime(seconds);
        void Check(bool condition,string message){if(condition)checks.Add(message);else errors.Add(message);}
        void Log(string message,string trace,LogType type){if(type==LogType.Assert||type==LogType.Error||type==LogType.Exception)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;Application.runInBackground=background;Time.timeScale=1;}
    }
}
#endif
