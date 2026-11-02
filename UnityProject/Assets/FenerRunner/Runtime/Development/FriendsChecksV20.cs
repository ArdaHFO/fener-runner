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
    public sealed class FriendsChecksV20:MonoBehaviour
    {
        const string Flag="FenerRunner.FriendsChecksV20";
        [Serializable] sealed class Report{public bool passed;public string utc,scope;public string[] checks,errors,captures,raycastLifecycle;}
        readonly List<string> checks=new List<string>(),errors=new List<string>(),captures=new List<string>();
        readonly List<string> raycastLifecycle=new List<string>();
        readonly List<RaycastResult> hits=new List<RaycastResult>();
        RunnerGame game;RunnerFriends fixture;GameObject fixtureObject;bool auto;int enabledPreference;bool enabledExisted;
        [MenuItem("Fener Runner/110 - Verify friends safety and UI v20")]
        public static void StartChecks(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod]static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Friends v20 safety and UI checks").AddComponent<FriendsChecksV20>();}}
        IEnumerator Start()
        {
            Application.logMessageReceived+=Log;game=FindFirstObjectByType<RunnerGame>();
            if(game!=null){auto=game.AutomatedTest;game.AutomatedTest=true;game.MultiplayerInterface.Friends.DisableAutoConnectForTest();}
            enabledExisted=PlayerPrefs.HasKey(RunnerFriends.EnabledKey);enabledPreference=PlayerPrefs.GetInt(RunnerFriends.EnabledKey);
            var stack=new Stack<IEnumerator>();stack.Push(Scenarios());
            while(stack.Count>0)
            {
                bool more;object wait=null;try{more=stack.Peek().MoveNext();if(more)wait=stack.Peek().Current;}catch(Exception error){errors.Add(error.ToString());break;}
                if(!more){stack.Pop();continue;}if(wait is IEnumerator nested){stack.Push(nested);continue;}yield return wait;
            }
            if(fixtureObject!=null)Destroy(fixtureObject);
            if(game!=null){game.MultiplayerInterface.Friends.ClearFixturesForTest();game.MultiplayerInterface.Close();game.AutomatedTest=auto;}
            Check(enabledExisted==PlayerPrefs.HasKey(RunnerFriends.EnabledKey)&&enabledPreference==PlayerPrefs.GetInt(RunnerFriends.EnabledKey),"Local UI checks preserve social opt-in preference");
            var report=new Report{passed=errors.Count==0,utc=DateTime.UtcNow.ToString("O"),scope="Native UI and actual friend controller seeded with clearly synthetic offline rows, keyboard geometry fixture, invite parsing/consent/dedup/expiry/limits. Row Buttons temporarily enabled only for native pointer tests while backend Ready remains false. Rebuilt rows are probed after one rendered Canvas frame; pre/post Graphic.depth and raycasts are recorded. No cloud calls here. Cloud validation is separate. No APK or phone test.",checks=checks.ToArray(),errors=errors.ToArray(),captures=captures.ToArray(),raycastLifecycle=raycastLifecycle.ToArray()};
            Directory.CreateDirectory("../Validation");File.WriteAllText("../Validation/unity_friends_v20.json",JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Friends v20 local "+(report.passed?"PASSED":"FAILED")+" "+string.Join(";",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Scenarios()
        {
            if(game==null)throw new InvalidOperationException("Built RunnerGame required");game.ReturnToMenu();
            fixtureObject=new GameObject("Synthetic friend inbox fixture");fixture=fixtureObject.AddComponent<RunnerFriends>();
            var peer=fixtureObject.AddComponent<RunnerMultiplayer>();peer.InitializeTestPeer(17);fixture.Initialize(null,peer);
            for(int i=0;i<8;i++)fixture.SeedFriendForTest("peer"+i,"Taraftar"+i+"#1000");
            long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Check(RunnerFriends.ValidFriendName("Taraftar#1234")&&!RunnerFriends.ValidFriendName("Taraftar")&&!RunnerFriends.ValidFriendName("<b>#123")&&!RunnerFriends.ValidFriendName("A#\n123"),"Friend identifiers require full name tag and reject markup/control input");
            Check(!RunnerFriends.ValidInvite(null,now),"Null message rejected");
            var incomplete=Newtonsoft.Json.JsonConvert.DeserializeObject<RunnerInvitePayload>("{\"id\":\""+Guid.NewGuid().ToString("N")+"\",\"code\":\"ABCD12\",\"expires\":"+(now+120)+"}");
            Check(!RunnerFriends.ValidInvite(incomplete,now),"Actual JSON missing discriminator and version cannot inherit valid payload defaults");
            foreach(string code in new[]{"", "A", "AB!2", "ş123", "123456789",null}){var p=Invite(now);p.code=code;Check(!RunnerFriends.ValidInvite(p,now),"Malformed room code rejected: "+(code??"null"));}
            foreach(long expiry in new[]{now-1,now,now+10000}){var p=Invite(now);p.expires=expiry;Check(!RunnerFriends.ValidInvite(p,now),"Expired or oversized lifetime rejected "+expiry);}
            var invalid=Invite(now);invalid.version=19;Check(!RunnerFriends.ValidInvite(invalid,now),"Old gameplay version cannot invite v20 client");
            invalid=Invite(now);invalid.kind="chat";Check(!RunnerFriends.ValidInvite(invalid,now),"Unrelated message kinds discarded");
            invalid=Invite(now);invalid.id="123";Check(!RunnerFriends.ValidInvite(invalid,now),"Invalid invite nonce discarded");
            invalid=Invite(now);invalid.id=new string('z',32);Check(!RunnerFriends.ValidInvite(invalid,now),"Nonhex nonce discarded");
            var first=Invite(now);Check(!fixture.ReceiveForTest("stranger",first,now)&&fixture.Invites.Count==0,"Untrusted sender cannot add an invitation");
            int wallet=game.WalletBalance;var state=game.State;
            Check(fixture.ReceiveForTest("peer0",first,now),"Known friend valid message enters inbox");
            Check(peer.State==MultiplayerState.Offline&&game.State==state&&game.WalletBalance==wallet,"Receiving invite never joins, starts gameplay or changes currency");
            Check(!fixture.ReceiveForTest("peer0",first,now)&&fixture.Invites.Count==1,"Repeated message nonce is ignored");
            fixture.DismissInvite(first.id);Check(fixture.Invites.Count==0&&!fixture.ReceiveForTest("peer0",first,now),"Dismissed invitation cannot replay");
            for(int i=0;i<8;i++)Check(fixture.ReceiveForTest("peer"+i,Invite(now),now),"Distinct friend invitation accepted "+i);
            Check(fixture.Invites.Count==RunnerFriends.MaxInvites,"Invitation inbox stays bounded to five entries");
            int before=fixture.Invites.Count;fixture.ReceiveForTest("peer7",Invite(now),now);Check(fixture.Invites.Count==before,"A friend's newer invite replaces their previous one");
            var expired=Invite(now);expired.expires=now-1;Check(!fixture.ReceiveForTest("peer0",expired,now),"Expired invitation never enters inbox");
            var room=game.MultiplayerInterface;room.Open();yield return null;var panel=room.FriendPanel;panel.OpenForTest();yield return Wait(.4f);
            Check(room.IsOpen&&panel.IsOpen&&game.Interface.MultiplayerOpen,"Friends is a subpage of the one multiplayer modal");
            var menuGroup=game.Interface.menu.GetComponent<CanvasGroup>();Check(!menuGroup.interactable&&!menuGroup.blocksRaycasts,"Friends subpage keeps solo controls blocked");
            game.Interface.play.onClick.Invoke();game.Interface.openShop.onClick.Invoke();Check(game.State==RunState.Menu&&!game.Interface.ShopOpen,"Background callbacks cannot start run or shop from Friends");
            Check(!panel.NameInput.textComponent.supportRichText,"Remote names and input never render markup");
            var uiFriends=room.Friends;Check(!uiFriends.Ready&&!uiFriends.Connecting,"Actual UI backend remains offline before row fixtures");
            uiFriends.SeedFriendForTest("fixture-friend",new string('Ş',27)+"#1234");
            uiFriends.SeedFriendForTest("fixture-request","Çağrı_İstanbul#1234",RunnerFriendKind.Incoming);
            uiFriends.SeedFriendForTest("fixture-outgoing","İstek_Bekleyen#4567",RunnerFriendKind.Outgoing);
            Check(uiFriends.ReceiveForTest("fixture-friend",Invite(now),now),"Actual UI controller receives a synthetic accepted-friend invite");
            yield return null;
            panel.NameInput.interactable=true;panel.NameInput.text=new string('X',70);Check(panel.NameInput.text.Length==48,"Actual player-name input bounds pasted content");panel.NameInput.text="Taraftar#1234";
            foreach(int height in new[]{1920,2340})
            {
                EditorApplication.ExecuteMenuItem(height==1920?"Fener Runner/5 - Open portrait Game view":"Fener Runner/10 - Open tall phone Game view");yield return Wait(.7f);
                Check(Screen.width==1080&&Screen.height==height,"Friends rendered at native portrait "+height);
                for(int tab=0;tab<3;tab++)
                {
                    Check(Click(panel.TabButtons[tab]),"Native pointer switches actual Friends tab "+tab+" at "+height);yield return null;yield return null;Canvas.ForceUpdateCanvases();
                    foreach(var button in panel.RootPanel.GetComponentsInChildren<Button>())Check(((RectTransform)button.transform).rect.height>=132,"Mobile target at least132 logical pixels: "+button.name+" tab"+tab+" at"+height);
                    var action=FindButton(panel.RootPanel,tab==2?"Accept invitation":"Friend action");
                    Check(action!=null&&Raycast(action),"First populated row action receives native raycast tab"+tab+" at"+height);
                    if(action!=null){action.interactable=true;Check(Click(action),"Native pointer dispatches offline fixture row action tab"+tab);yield return null;}
                    var secondary=FindButton(panel.RootPanel,tab==2?"Decline invitation":"Remove friend");
                    raycastLifecycle.Add("tab"+tab+" height"+height+" before render depth="+(secondary==null?-999:secondary.GetComponent<Image>().depth)+" hit="+Raycast(secondary));
                    // Primary actions mark the list dirty. Its replacement rows
                    // must receive their first Canvas build before a user could
                    // see or tap them. Keep the exact topmost-raycast assertion.
                    yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
                    secondary=FindButton(panel.RootPanel,tab==2?"Decline invitation":"Remove friend");
                    raycastLifecycle.Add("tab"+tab+" height"+height+" after render depth="+(secondary==null?-999:secondary.GetComponent<Image>().depth)+" hit="+Raycast(secondary));
                    Check(secondary!=null&&Raycast(secondary),"First populated row secondary action receives native raycast tab"+tab+" at"+height);
                    Check(!uiFriends.Ready&&!uiFriends.Connecting&&game.Multiplayer.State==MultiplayerState.Offline,"Offline UI fixture action never connects, joins or sends cloud messages tab"+tab);
                    foreach(var text in panel.RootPanel.GetComponentsInChildren<Text>())
                    {
                        if(string.IsNullOrEmpty(text.text))continue;var settings=text.GetGenerationSettings(text.rectTransform.rect.size);settings.resizeTextForBestFit=false;
                        float needed=text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,settings)/text.pixelsPerUnit;
                        Check(needed<=text.rectTransform.rect.height+2,"Populated text fits height tab"+tab+" "+text.text.Substring(0,Mathf.Min(12,text.text.Length)));
                    }
                    yield return Capture("v20-friends-populated-fixture-"+tab+"-"+height);
                }
                panel.NameInput.interactable=true;
                panel.NameInput.ActivateInputField();yield return null;yield return null;
                panel.SetKeyboardHeightForTest(height*.5f);yield return Wait(.15f);
                Check(panel.KeyboardLayoutActive,"Friends input uses compact keyboard-safe layout "+height);
                yield return Capture("v20-friends-keyboard-fixture-"+height);
                Check(room.TryCloseOverlay()&&panel.IsOpen,"First Back closes keyboard and retains friend list "+height);
                panel.SetKeyboardHeightForTest(-1);yield return null;
                Check(!panel.KeyboardLayoutActive,"Normal friend layout restores after keyboard closes "+height);
            }
            Check(room.TryCloseOverlay()&&!panel.IsOpen&&room.IsOpen,"Second Back returns from Friends to room");
            Check(room.TryCloseOverlay()&&!room.IsOpen&&game.State==RunState.Menu,"Final Back returns to menu");
            yield return null;
        }
        static RunnerInvitePayload Invite(long now)=>new RunnerInvitePayload{kind="fener-runner-invite",version=20,id=Guid.NewGuid().ToString("N"),code="ABCD12",expires=now+120};
        static Button FindButton(GameObject root,string name){foreach(var button in root.GetComponentsInChildren<Button>())if(button.name==name)return button;return null;}
        bool Raycast(Button target)
        {if(target==null)return false;hits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=Centre((RectTransform)target.transform)},hits);return hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==target;}
        bool Click(Button target)
        {
            if(target==null||!target.gameObject.activeInHierarchy||!target.IsInteractable()||!Raycast(target))return false;
            var pointer=new PointerEventData(EventSystem.current){position=Centre((RectTransform)target.transform),button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(target.gameObject,pointer,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(target.gameObject,pointer,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(target.gameObject,pointer,ExecuteEvents.pointerClickHandler);return true;
        }
        static Vector2 Centre(RectTransform target)=>RectTransformUtility.WorldToScreenPoint(null,target.TransformPoint(target.rect.center));
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();var texture=ScreenCapture.CaptureScreenshotAsTexture();var path=Path.GetFullPath("../Validation/"+name+".png");File.WriteAllBytes(path,texture.EncodeToPNG());Destroy(texture);captures.Add(path);}
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        void Check(bool ok,string label){if(ok)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
    }
}
#endif
