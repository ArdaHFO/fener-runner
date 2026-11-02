using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FenerRunner
{
    /// <summary>Presentation only. Room lifecycle, race authority and cancellation belong to RunnerMultiplayer.</summary>
    public sealed class MultiplayerUI : MonoBehaviour
    {
        static readonly Color Navy=new Color(.031f,.071f,.129f),Gold=new Color(1f,.81f,.26f),White=new Color(.96f,.96f,.94f),Muted=new Color(.66f,.73f,.81f),Mint=new Color(.42f,.91f,.77f);
        RunnerGame game;RunnerUI ui;RunnerMultiplayer session;
        Font bold,regular;Sprite rounded;
        GameObject overlay,choicePage,roomPage,countdownPage,raceStrip,header,joinDivider,choiceHint,keyboardTitle;
        RectTransform card;Text title,subtitle,status,roomCode,localState,remoteState,roomDetail,countdown,raceText,copyHint;
        Button openButton,hostButton,joinButton,readyButton,leaveButton,codeButton;
        InputField codeInput;
        MultiplayerState shownState;bool initialized,closing,changingCode,wasLocalFinished,pendingResult,requestPending,keyboardEditing;
        UIEntrance entrance;
#if UNITY_EDITOR
        float testKeyboardHeight=-1;
#endif
        int requestGeneration;
        float refreshClock,copyClock;string localError="";int shownCountdown=-1;
        Vector2 powerRest,toastRest;bool shiftedHud;
        RunnerFriends friends;FriendsUI friendsUI;Text friendBadge;Button friendsButton,roomFriendsButton;
        public RunnerFriends Friends=>friends;
        public FriendsUI FriendPanel=>friendsUI;
        public Button FriendsButton=>friendsButton;
        public bool IsOpen=>overlay!=null&&overlay.activeSelf;
        public bool IsSessionActive=>session!=null&&session.State!=MultiplayerState.Offline&&session.State!=MultiplayerState.Disconnected;
        public GameObject Panel=>overlay;
        public Button OpenButton=>openButton;
        public Button HostButton=>hostButton;
        public Button JoinButton=>joinButton;
        public Button ReadyButton=>readyButton;
        public Button LeaveButton=>leaveButton;
        public InputField CodeInput=>codeInput;
        public string DisplayedStatus=>status==null?"":status.text;
        public string RaceReadout=>raceText==null?"":raceText.text;
        public bool KeyboardLayoutActive=>keyboardEditing;

        public void Initialize(RunnerGame owner,RunnerMultiplayer multiplayer)
        {
            if(initialized)return;
            if(owner==null||multiplayer==null)throw new ArgumentNullException(owner==null?nameof(owner):nameof(multiplayer));
            game=owner;ui=game.Interface;session=multiplayer;
            bold=ui.play.GetComponentInChildren<Text>().font;regular=ui.walletMenu.font;rounded=ui.play.GetComponent<Image>().sprite;
            powerRest=((RectTransform)ui.powerPanel.transform).anchoredPosition;toastRest=ui.toast.rectTransform.anchoredPosition;
            BuildMenuTile();BuildModal();BuildRaceStrip();initialized=true;
            friends=game.gameObject.AddComponent<RunnerFriends>();friends.Initialize(game,session);
            friendsUI=gameObject.AddComponent<FriendsUI>();friendsUI.Initialize(game,friends,this,overlay.transform,card);
            friends.Changed+=FriendsChanged;friends.InviteArrived+=InvitationArrived;
            FriendsChanged();
            shownState=session.State;ui.AttachMultiplayer(this);session.Changed+=SessionChanged;Refresh();
        }

        public void Open()
        {
            if(!initialized||closing||ui.ShopOpen||ui.AudioOpen)return;
            if(game.State==RunState.Running&&session.State!=MultiplayerState.Countdown)return;
            localError="";overlay.SetActive(true);overlay.transform.SetAsLastSibling();ui.RefreshMultiplayerModal();Refresh();
        }
        public void Close()
        {
            if(!initialized||closing)return;
            friendsUI?.Close();friends?.CancelRoomActions();
            closing=true;requestGeneration++;requestPending=false;codeInput.DeactivateInputField();overlay.SetActive(false);localError="";copyClock=0;pendingResult=false;
            session.Leave();
            if(game.State!=RunState.Menu)game.ReturnToMenu();
            closing=false;shownState=session.State;ui.RefreshMultiplayerModal();Refresh();
        }
        public bool TryCloseOverlay()
        {
            if(!IsOpen)return false;
            if(friendsUI!=null&&friendsUI.TryBack())return true;
            if(codeInput.isFocused)
            {
                codeInput.DeactivateInputField();
                if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
                UpdateKeyboardLayout();return true;
            }
            Close();return true;
        }
        public void OpenFriends()
        {if(!IsOpen)Open();if(IsOpen&&session.State!=MultiplayerState.Countdown)friendsUI.Open();}
        void FriendsChanged()
        {
            if(friendBadge==null||friends==null)return;
            int count=friends.Invites.Count;foreach(var row in friends.People)if(row.Kind==RunnerFriendKind.Incoming)count++;
            friendBadge.gameObject.SetActive(count>0);friendBadge.text=count.ToString();
        }
        void InvitationArrived()
        {
            FriendsChanged();
            if(game.State==RunState.Running)ui.Notify("ARKADAŞINDAN KOŞU DAVETİ GELDİ");
        }

        void SessionChanged()
        {
            if(!initialized||closing)return;
            var next=session.State;
            if(next!=shownState)
            {
                shownState=next;localError="";codeInput.DeactivateInputField();shownCountdown=-1;
                if(next==MultiplayerState.Racing){pendingResult=false;friendsUI?.Close();HideForRace();}
                else if(next==MultiplayerState.Finished&&game.State==RunState.Finished&&!ui.resultPanel.activeSelf)pendingResult=true;
                else if(next==MultiplayerState.Waiting||next==MultiplayerState.Countdown||next==MultiplayerState.Finished||next==MultiplayerState.Disconnected)
                {overlay.SetActive(true);overlay.transform.SetAsLastSibling();ui.RefreshMultiplayerModal();}
            }
            Refresh();
        }
        void HideForRace()
        {overlay.SetActive(false);codeInput.DeactivateInputField();ui.RefreshMultiplayerModal();}
        void Update()
        {
            if(!initialized)return;
            UpdateKeyboardLayout();
            if(session.State!=shownState)SessionChanged();
            bool localFinished=session.State==MultiplayerState.Racing&&game.State==RunState.Finished&&ui.resultPanel.activeSelf;
            if(pendingResult&&ui.resultPanel.activeSelf)
            {pendingResult=false;overlay.SetActive(true);overlay.transform.SetAsLastSibling();ui.RefreshMultiplayerModal();Refresh();}
            if(localFinished&&!wasLocalFinished)
            {overlay.SetActive(true);overlay.transform.SetAsLastSibling();ui.RefreshMultiplayerModal();Refresh();}
            wasLocalFinished=localFinished;
            refreshClock-=Time.unscaledDeltaTime;
            if(copyClock>0){copyClock=Mathf.Max(0,copyClock-Time.unscaledDeltaTime);if(copyClock<=0)copyHint.text="PAYLAŞMAK İÇİN KODA DOKUN";}
            if(refreshClock<=0)
            {
                refreshClock=.1f;
                if(IsOpen)Refresh();
                RefreshRaceStrip();
            }
        }
        void Refresh()
        {
            if(!initialized)return;
            var state=session.State;
            bool selecting=state==MultiplayerState.Offline||state==MultiplayerState.Disconnected||state==MultiplayerState.Connecting;
            bool counting=state==MultiplayerState.Countdown;
            choicePage.SetActive(selecting);roomPage.SetActive(!selecting&&!counting);countdownPage.SetActive(counting);
            bool connecting=state==MultiplayerState.Connecting||requestPending;
            hostButton.interactable=joinButton.interactable=codeInput.interactable=!connecting;
            Set(title,counting?"HAZIR OL!":state==MultiplayerState.Finished?"BİR TUR DAHA?":"ARKADAŞINLA KOŞ");
            Set(subtitle,selecting?"Bir oda aç ya da arkadaşının koduyla katıl.\nİki telefon. Aynı parkur. Birlikte heyecan.":counting?"Aynı anda başlıyoruz.\nKaydır, ritmini bul, öne geç.":state==MultiplayerState.Finished?"Yarış tamamlandı. Rövanş için ikiniz de hazır olun.":"Aynı parkurda kendi koşunu yap.\nHayaletler birbirine çarpmaz.");
            Set(leaveButton.GetComponentInChildren<Text>(),connecting?"VAZGEÇ":selecting?"GERİ DÖN":"ODADAN AYRIL");
            string error=string.IsNullOrWhiteSpace(localError)?session.Error:localError;
            string nextStatus=!string.IsNullOrWhiteSpace(error)?error:connecting?"Odan hazırlanıyor. Birazdan sahadayız…":session.Status;
            if(keyboardEditing&&string.IsNullOrWhiteSpace(error))nextStatus="Kodu yaz, ardından KODLA KATIL'a dokun.";
            if(string.IsNullOrWhiteSpace(nextStatus))nextStatus=selecting?"İki telefonda da internet bağlantısı gerekir.":"İkiniz de hazır olduğunuzda yarış başlar.";
            Set(status,Limit(nextStatus,190));status.color=string.IsNullOrWhiteSpace(error)?Muted:new Color(1f,.62f,.49f);
            if(!selecting&&!counting)
            {
                Set(roomCode,string.IsNullOrWhiteSpace(session.JoinCode)?"—":session.JoinCode.ToUpperInvariant());
                Set(localState,session.LocalReady?"HAZIRSIN":"HAZIR MISIN?");localState.color=session.LocalReady?Mint:White;
                Set(remoteState,!session.HasPeer?"ODAYA BEKLENİYOR":session.RemoteReady?"HAZIR":"HAZIRLANIYOR");remoteState.color=session.RemoteReady?Mint:Muted;
                bool waitingForFinish=state==MultiplayerState.Racing;
                bool canResume=waitingForFinish&&game.State==RunState.Paused;
                readyButton.interactable=canResume||(session.HasPeer&&!waitingForFinish);
                Set(readyButton.GetComponentInChildren<Text>(),canResume?"YARIŞA DÖN":waitingForFinish?"ARKADAŞIN KOŞUYOR":session.LocalReady?"HAZIRLIĞI GERİ AL":state==MultiplayerState.Finished?"RÖVANŞA HAZIRIM":"HAZIRIM");
                string details=canResume?"Koşun duraklatıldı. Aynı yarışa kaldığın yerden dön.":state==MultiplayerState.Finished?"SEN  "+Distance(game.Distance)+"     ·     ARKADAŞIN  "+Distance(session.RemoteDistance):waitingForFinish?"Senin koşun bitti. Arkadaşının sonucunu bekliyoruz.":!session.HasPeer?"Kodu arkadaşına gönder. Katılınca burada görünecek.":"İkiniz de HAZIRIM'a dokunun. Geri sayım başlasın.";
                Set(roomDetail,details);
            }
            if(counting)
            {
                int seconds=Mathf.Max(1,Mathf.CeilToInt(session.CountdownSeconds));
                if(seconds!=shownCountdown){shownCountdown=seconds;countdown.text=seconds.ToString();}
            }
            RefreshRaceStrip();
        }
        void RefreshRaceStrip()
        {
            bool racing=session.State==MultiplayerState.Racing&&game.State==RunState.Running;
            if(raceStrip.activeSelf!=racing)raceStrip.SetActive(racing);
            if(shiftedHud!=racing)
            {
                shiftedHud=racing;((RectTransform)ui.powerPanel.transform).anchoredPosition=powerRest+(racing?new Vector2(0,-74):Vector2.zero);
                ui.toast.rectTransform.anchoredPosition=toastRest+(racing?new Vector2(0,-74):Vector2.zero);
            }
            if(!racing)return;
            double gap=game.Distance-session.RemoteDistance;
            string label=!session.HasPeer?"ARKADAŞININ BAĞLANTISI BEKLENİYOR":session.RemotePose==GhostPose.Dead?"ARKADAŞININ KOŞUSU BİTTİ  ·  "+Distance(session.RemoteDistance):session.RemotePose==GhostPose.Paused?"ARKADAŞIN DURAKLATTI":Math.Abs(gap)<2?"ARKADAŞINLA YAN YANA":gap>0?Distance(gap)+" ÖNDESİN":Distance(-gap)+" GERİDESİN";
            Set(raceText,label);raceText.color=gap>=-2?Gold:White;
        }

        async void Host()
        {
            if(!IsOpen||!hostButton.interactable||IsSessionActive)return;
            int request=++requestGeneration;requestPending=true;localError="";codeInput.DeactivateInputField();Refresh();
            try{await session.HostAsync();}
            catch(Exception){if(this!=null&&request==requestGeneration&&IsOpen)localError="Oda açılamadı. Bağlantını kontrol edip yeniden dene.";}
            finally{if(this!=null&&request==requestGeneration){requestPending=false;Refresh();}}
        }
        async void Join()
        {
            if(!IsOpen||!joinButton.interactable||IsSessionActive)return;
            string code=NormalizeCode(codeInput.text);
            if(code.Length<4){localError="Arkadaşının paylaştığı oda kodunu yaz.";Refresh();return;}
            int request=++requestGeneration;requestPending=true;localError="";codeInput.DeactivateInputField();Refresh();
            try{await session.JoinAsync(code);}
            catch(Exception){if(this!=null&&request==requestGeneration&&IsOpen)localError="Odaya katılınamadı. Kodu ve bağlantını kontrol et.";}
            finally{if(this!=null&&request==requestGeneration){requestPending=false;Refresh();}}
        }
        void Ready()
        {
            if(!IsOpen||!readyButton.interactable)return;
            if(session.State==MultiplayerState.Racing&&game.State==RunState.Paused){HideForRace();game.Resume();return;}
            session.SetReady(!session.LocalReady);
        }
        void CopyCode()
        {if(string.IsNullOrWhiteSpace(session.JoinCode))return;GUIUtility.systemCopyBuffer=session.JoinCode;copyHint.text="KOD KOPYALANDI · ARKADAŞINA GÖNDER";copyClock=2f;}
        void FilterCode(string value)
        {
            if(changingCode)return;string clean=NormalizeCode(value);
            if(clean!=value){changingCode=true;codeInput.SetTextWithoutNotify(clean);changingCode=false;}
            if(localError.Length>0){localError="";Refresh();}
        }
        public static string NormalizeCode(string value)
        {
            if(string.IsNullOrEmpty(value))return "";
            var clean=new StringBuilder(8);
            for(int i=0;i<value.Length&&clean.Length<8;i++)
            {char c=char.ToUpperInvariant(value[i]);if((c>='A'&&c<='Z')||(c>='0'&&c<='9'))clean.Append(c);}
            return clean.ToString();
        }
        static char ValidateCode(string text,int index,char value)
        {char c=char.ToUpperInvariant(value);return(c>='A'&&c<='Z')||(c>='0'&&c<='9')?c:'\0';}
        static string Distance(double value)=>Math.Max(0,value).ToString("N0")+" m";
        static string Limit(string value,int max)=>string.IsNullOrEmpty(value)?"":value.Length<=max?value:value.Substring(0,max-1)+"…";
        static void Set(Text target,string value){if(target.text!=value)target.text=value;}

        void UpdateKeyboardLayout()
        {
            if(!initialized)return;
            bool visible=TouchScreenKeyboard.visible;
            float top=TouchScreenKeyboard.area.yMax;
#if UNITY_EDITOR
            if(testKeyboardHeight>=0){visible=testKeyboardHeight>0;top=testKeyboardHeight;}
#endif
            bool compact=IsOpen&&choicePage.activeSelf&&codeInput.isFocused&&visible;
            if(compact!=keyboardEditing)
            {
                keyboardEditing=compact;entrance.enabled=false;
                header.SetActive(!compact);keyboardTitle.SetActive(compact);hostButton.gameObject.SetActive(!compact);joinDivider.SetActive(!compact);choiceHint.SetActive(!compact);leaveButton.gameObject.SetActive(!compact);
                card.sizeDelta=new Vector2(932,compact?640:1540);
                ((RectTransform)codeInput.transform).anchoredPosition=new Vector2(0,compact?100:-4);
                ((RectTransform)joinButton.transform).anchoredPosition=new Vector2(0,compact?-78:-182);
                status.rectTransform.anchoredPosition=new Vector2(0,compact?-220:-506);status.rectTransform.sizeDelta=new Vector2(812,compact?110:122);
                if(!compact){card.anchoredPosition=Vector2.zero;card.localScale=Vector3.one;entrance.enabled=true;}
                Refresh();
            }
            if(!compact)return;
            // Some keyboards report an empty area. In that case reserve the lower
            // half; the OS still owns its native editing field and Done button.
            if(top<=0||top>=Screen.height)top=Screen.height*.5f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(ui.safeArea,new Vector2(Screen.width*.5f,top),null,out var localTop);
            float bottom=Mathf.Max(ui.safeArea.rect.yMin,localTop.y),upper=ui.safeArea.rect.yMax;
            float scale=Mathf.Min(1,Mathf.Max(.25f,(upper-bottom-32)/640f));
            card.localScale=Vector3.one*scale;card.anchoredPosition=new Vector2(0,(upper+bottom)*.5f);
        }
#if UNITY_EDITOR
        // Layout-only fixture. It never opens a keyboard or claims device validation.
        public void SetKeyboardHeightForTest(float pixels){testKeyboardHeight=pixels;UpdateKeyboardLayout();}
#endif

        void BuildMenuTile()
        {
            var shop=(RectTransform)ui.openShop.transform;shop.sizeDelta=new Vector2(448,132);shop.anchoredPosition=new Vector2(-236,shop.anchoredPosition.y);
            var shopText=ui.openShop.GetComponentInChildren<Text>();shopText.text="TRİBÜN\nATÖLYESİ";shopText.fontSize=31;shopText.rectTransform.sizeDelta=new Vector2(310,100);shopText.rectTransform.anchoredPosition=new Vector2(28,0);
            var shopIcon=ui.openShop.GetComponentInChildren<UIIcon>();if(shopIcon!=null){shopIcon.rectTransform.anchoredPosition=new Vector2(-164,0);shopIcon.rectTransform.sizeDelta=new Vector2(32,32);}
            openButton=Button(shop.parent,"Arkadaşınla koş","ARKADAŞINLA\nKOŞ",new Vector2(448,132),new Vector2(236,shop.anchoredPosition.y),false);
            var label=openButton.GetComponentInChildren<Text>();label.fontSize=31;label.rectTransform.sizeDelta=new Vector2(310,100);label.rectTransform.anchoredPosition=new Vector2(28,0);
            var badge=BuildPanel("Two runners",openButton.transform,new Vector2(54,58),new Vector2(-164,0),Gold);
            Label(badge,"2",34,Navy,new Vector2(50,54),Vector2.zero,true);
            friendBadge=Label(openButton.transform,"",26,Gold,new Vector2(54,48),new Vector2(190,41),true);friendBadge.gameObject.SetActive(false);
            openButton.onClick.AddListener(Open);
        }
        void BuildModal()
        {
            var root=Rect("Friend race room",ui.safeArea);Stretch(root);overlay=root.gameObject;
            var backdrop=root.gameObject.AddComponent<Image>();backdrop.color=new Color(.006f,.015f,.03f,.88f);backdrop.raycastTarget=true;
            card=BuildPanel("Friend race card",root,new Vector2(932,1540),Vector2.zero,Navy);
            header=Rect("Room header",card).gameObject;
            Label(header.transform,"KADIKÖY RUN CLUB  /  2 OYUNCU",24,Gold,new Vector2(820,44),new Vector2(0,686));
            title=Label(header.transform,"ARKADAŞINLA KOŞ",67,White,new Vector2(830,110),new Vector2(0,584),true);
            subtitle=Label(header.transform,"",30,Muted,new Vector2(812,112),new Vector2(0,466));
            BuildPanel("Header gold rule",header.transform,new Vector2(76,4),new Vector2(0,378),Gold);
            keyboardTitle=Label(card,"ARKADAŞININ ODA KODU",30,Gold,new Vector2(812,60),new Vector2(0,246),true).gameObject;keyboardTitle.SetActive(false);
            choicePage=Rect("Create or join",card).gameObject;
            hostButton=Button(choicePage.transform,"Create room","ODA OLUŞTUR",new Vector2(812,144),new Vector2(0,249),true);hostButton.onClick.AddListener(Host);
            joinDivider=Label(choicePage.transform,"YA DA KODLA KATIL",24,Muted,new Vector2(600,42),new Vector2(0,112)).gameObject;
            var input=BuildPanel("Room code input",choicePage.transform,new Vector2(812,144),new Vector2(0,-4),new Color(.12f,.21f,.31f));input.GetComponent<Image>().raycastTarget=true;
            codeInput=input.gameObject.AddComponent<InputField>();codeInput.targetGraphic=input.GetComponent<Image>();codeInput.characterLimit=8;
            codeInput.lineType=InputField.LineType.SingleLine;codeInput.contentType=InputField.ContentType.Custom;codeInput.characterValidation=InputField.CharacterValidation.None;
            codeInput.keyboardType=TouchScreenKeyboardType.ASCIICapable;codeInput.navigation=new Navigation{mode=Navigation.Mode.None};
            codeInput.textComponent=Label(input,"",48,White,new Vector2(740,100),Vector2.zero,true);codeInput.textComponent.supportRichText=false;
            codeInput.placeholder=Label(input,"ODA KODUNU YAZ",34,Muted,new Vector2(740,94),Vector2.zero);
            codeInput.caretColor=Gold;codeInput.customCaretColor=true;codeInput.selectionColor=new Color(1,.81f,.26f,.28f);
            codeInput.onValidateInput=ValidateCode;codeInput.onValueChanged.AddListener(FilterCode);
            joinButton=Button(choicePage.transform,"Join room","KODLA KATIL",new Vector2(812,144),new Vector2(0,-182),false);joinButton.onClick.AddListener(Join);
            friendsButton=Button(choicePage.transform,"Open friends","ARKADAŞLARIM  ·  DAVETLER",new Vector2(812,132),new Vector2(0,-352),false);
            friendsButton.GetComponentInChildren<Text>().fontSize=30;friendsButton.onClick.AddListener(OpenFriends);choiceHint=friendsButton.gameObject;
            roomPage=Rect("Room players",card).gameObject;
            codeButton=Button(roomPage.transform,"Copy room code","",new Vector2(396,176),new Vector2(-208,249),false);codeButton.onClick.AddListener(CopyCode);
            Label(codeButton.transform,"ODA KODUN",23,Muted,new Vector2(370,40),new Vector2(0,52));
            roomCode=Label(codeButton.transform,"—",48,Gold,new Vector2(370,94),new Vector2(0,-9),true);
            roomFriendsButton=Button(roomPage.transform,"Invite a friend","ARKADAŞ\nDAVET ET",new Vector2(396,176),new Vector2(208,249),false);
            roomFriendsButton.GetComponentInChildren<Text>().fontSize=29;roomFriendsButton.onClick.AddListener(OpenFriends);
            copyHint=Label(roomPage.transform,"PAYLAŞMAK İÇİN KODA DOKUN",22,Muted,new Vector2(812,40),new Vector2(0,122));
            var local=BuildPanel("Local ready state",roomPage.transform,new Vector2(812,106),new Vector2(0,22),new Color(.10f,.18f,.27f));
            Label(local,"SEN",26,White,new Vector2(230,56),new Vector2(-242,0),true,TextAnchor.MiddleLeft);
            localState=Label(local,"HAZIR MISIN?",29,White,new Vector2(440,58),new Vector2(141,0),true,TextAnchor.MiddleRight);
            var remote=BuildPanel("Remote ready state",roomPage.transform,new Vector2(812,106),new Vector2(0,-100),new Color(.10f,.18f,.27f));
            Label(remote,"ARKADAŞIN",26,White,new Vector2(250,56),new Vector2(-232,0),true,TextAnchor.MiddleLeft);
            remoteState=Label(remote,"ODAYA BEKLENİYOR",27,Muted,new Vector2(440,58),new Vector2(141,0),true,TextAnchor.MiddleRight);
            readyButton=Button(roomPage.transform,"Ready for race","HAZIRIM",new Vector2(812,144),new Vector2(0,-254),true);readyButton.onClick.AddListener(Ready);
            roomDetail=Label(roomPage.transform,"",28,White,new Vector2(812,94),new Vector2(0,-380));
            countdownPage=Rect("Race countdown",card).gameObject;
            Label(countdownPage.transform,"TRİBÜN SENİ BEKLİYOR",30,Gold,new Vector2(810,60),new Vector2(0,246),true);
            countdown=Label(countdownPage.transform,"3",220,Gold,new Vector2(810,330),new Vector2(0,25),true);
            Label(countdownPage.transform,"SAĞ · SOL · YUKARI · AŞAĞI\nKoşunu kaydırarak yönet.",31,White,new Vector2(812,112),new Vector2(0,-240));
            status=Label(card,"",28,Muted,new Vector2(812,122),new Vector2(0,-506));status.supportRichText=false;
            leaveButton=Button(card,"Leave friend race","GERİ DÖN",new Vector2(812,132),new Vector2(0,-666),false);leaveButton.onClick.AddListener(Close);
            // Keep the complete hit shield active during entry. Only the card animates.
            overlay.SetActive(false);entrance=card.gameObject.AddComponent<UIEntrance>();entrance.duration=.34f;
        }
        void BuildRaceStrip()
        {
            var strip=BuildPanel("Friend race gap",ui.hud.transform,new Vector2(900,52),new Vector2(0,-352),new Color(.035f,.09f,.15f,.88f));
            strip.anchorMin=strip.anchorMax=new Vector2(.5f,1);raceStrip=strip.gameObject;
            Label(strip,"2",26,Navy,new Vector2(36,38),new Vector2(-410,0),true).color=Gold;
            raceText=Label(strip,"ARKADAŞINLA YAN YANA",25,Gold,new Vector2(796,40),new Vector2(20,0),true);raceStrip.SetActive(false);
        }

        RectTransform BuildPanel(string name,Transform parent,Vector2 size,Vector2 position,Color color)
        {var r=Rect(name,parent);r.sizeDelta=size;r.anchoredPosition=position;var image=r.gameObject.AddComponent<Image>();image.color=color;image.sprite=rounded;image.type=Image.Type.Sliced;image.raycastTarget=false;return r;}
        Button Button(Transform parent,string name,string label,Vector2 size,Vector2 position,bool primary)
        {
            var r=BuildPanel(name,parent,size,position,Color.white);var image=r.GetComponent<Image>();image.raycastTarget=true;
            var gradient=r.gameObject.AddComponent<UIGradient>();gradient.top=primary?new Color(1,.87f,.41f):new Color(.16f,.27f,.38f,.98f);gradient.bottom=primary?new Color(.96f,.74f,.15f):new Color(.08f,.15f,.24f,.98f);
            var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;button.navigation=new Navigation{mode=Navigation.Mode.None};
            var colors=button.colors;colors.highlightedColor=Color.white;colors.pressedColor=new Color(.8f,.86f,.94f);button.colors=colors;
            r.gameObject.AddComponent<UIButtonMotion>();if(label.Length>0)Label(r,label,39,primary?Navy:White,size-new Vector2(64,20),Vector2.zero,true);
            return button;
        }
        Text Label(Transform parent,string value,int size,Color color,Vector2 dimensions,Vector2 position,bool heavy=false,TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            var r=Rect(value.Length==0?"Readout":value,parent);r.sizeDelta=dimensions;r.anchoredPosition=position;
            var text=r.gameObject.AddComponent<Text>();text.font=heavy?bold:regular;text.text=value;text.fontSize=size;text.color=color;text.alignment=alignment;
            text.raycastTarget=false;text.supportRichText=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;
        }
        static RectTransform Rect(string name,Transform parent)
        {var value=new GameObject(name,typeof(RectTransform));value.transform.SetParent(parent,false);return(RectTransform)value.transform;}
        static void Stretch(RectTransform value){value.anchorMin=Vector2.zero;value.anchorMax=Vector2.one;value.offsetMin=value.offsetMax=Vector2.zero;}
        void OnDestroy(){if(session!=null)session.Changed-=SessionChanged;if(friends!=null){friends.Changed-=FriendsChanged;friends.InviteArrived-=InvitationArrived;}}
    }
}
