using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FenerRunner
{
    // A subpage of the existing multiplayer modal, so Back and pointer isolation
    // retain a single owner. Friend names are plain text, never rich text.
    public sealed class FriendsUI : MonoBehaviour
    {
        static readonly Color Navy=new Color(.031f,.071f,.129f),Gold=new Color(1,.81f,.26f),White=new Color(.96f,.96f,.94f),Muted=new Color(.66f,.73f,.81f),Mint=new Color(.42f,.91f,.77f);
        RunnerGame game;RunnerUI ui;RunnerFriends social;MultiplayerUI room;
        RectTransform root,card,mainCard,content;GameObject body,heading,footer;
        Font font,bold;Sprite round;Text status,identity,empty,pageLabel,keyboardTitle,modeTitle;
        Button addButton,connectButton,renameButton,backButton,prev,next;Button[] tabs;
        InputField input;ScrollRect scroll;bool dirty=true,renaming,compact,closing;int selected,page;
        readonly List<GameObject> rowObjects=new List<GameObject>();
        readonly List<RunnerFriend> filtered=new List<RunnerFriend>();
        const int PageSize=12;
        public bool IsOpen=>root!=null&&root.gameObject.activeSelf;
        public Button AddButton=>addButton;
        public GameObject RootPanel=>root==null?null:root.gameObject;
        public IReadOnlyList<Button> TabButtons=>tabs;
        public InputField NameInput=>input;
        public bool KeyboardLayoutActive=>compact;
        public string DisplayedStatus=>status==null?"":status.text;
#if UNITY_EDITOR
        float keyboardFixture=-1;
        public void SetKeyboardHeightForTest(float value){keyboardFixture=value;UpdateKeyboard();}
        public void OpenForTest(){Open(false,false);}
#endif
        public void Initialize(RunnerGame owner,RunnerFriends backend,MultiplayerUI ownerRoom,Transform overlay,RectTransform originalCard)
        {
            game=owner;ui=game.Interface;social=backend;room=ownerRoom;mainCard=originalCard;
            bold=ui.play.GetComponentInChildren<Text>().font;font=ui.walletMenu.font;round=ui.play.GetComponent<Image>().sprite;
            Build(overlay);social.Changed+=MarkDirty;
        }
        public void Open(bool invites=false,bool connect=true)
        {
            if(!room.IsOpen)return;selected=invites?2:0;page=0;renaming=false;input.text="";
            root.gameObject.SetActive(true);mainCard.gameObject.SetActive(false);dirty=true;Refresh();
            if(connect)_=social.ConnectAsync();
        }
        public void Close()
        {
            if(root==null)return;closing=true;input.DeactivateInputField();root.gameObject.SetActive(false);
            mainCard.gameObject.SetActive(true);closing=false;
        }
        public bool TryBack()
        {
            if(!IsOpen)return false;
            if(input.isFocused){input.DeactivateInputField();if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);UpdateKeyboard();return true;}
            if(renaming){renaming=false;input.text="";dirty=true;Refresh();return true;}
            Close();return true;
        }
        void MarkDirty(){dirty=true;}
        void Update(){if(IsOpen){if(dirty)Refresh();UpdateKeyboard();}}
        void Refresh()
        {
            dirty=false;
            identity.text=string.IsNullOrEmpty(social.PlayerName)?"OYUNCU KİMLİĞİN HAZIRLANIYOR":social.PlayerName;
            modeTitle.text=renaming?"OYUNCU ADINI DÜZENLE":"ARKADAŞINI EKLE";
            ((Text)input.placeholder).text=renaming?"Yeni adın (3–18 karakter)":"Arkadaşının adı#etiketi";
            addButton.GetComponentInChildren<Text>().text=renaming?"KAYDET":"EKLE";
            status.text=social.Status;status.color=social.Ready?Muted:new Color(1,.70f,.45f);
            connectButton.GetComponentInChildren<Text>().text=social.Connecting?"BAĞLANIYOR…":social.Ready?"YENİLE":"YENİDEN BAĞLAN";
            connectButton.interactable=!social.Busy;addButton.interactable=input.interactable=social.Ready&&!social.Busy;
            renameButton.interactable=social.Ready&&!social.Busy;
            for(int i=0;i<tabs.Length;i++)tabs[i].GetComponentInChildren<Text>().color=selected==i?Gold:Muted;
            tabs[0].GetComponentInChildren<Text>().text="ARKADAŞLAR";
            int requests=0;foreach(var row in social.People)if(row.Kind==RunnerFriendKind.Incoming)requests++;
            tabs[1].GetComponentInChildren<Text>().text="İSTEKLER"+(requests>0?" ("+requests+")":"");
            tabs[2].GetComponentInChildren<Text>().text="DAVETLER"+(social.Invites.Count>0?" ("+social.Invites.Count+")":"");
            RebuildRows();
        }
        void RebuildRows()
        {
            foreach(var row in rowObjects){row.SetActive(false);Destroy(row);}rowObjects.Clear();filtered.Clear();
            if(selected!=2)foreach(var row in social.People)if((selected==0)==(row.Kind==RunnerFriendKind.Friend))filtered.Add(row);
            int count=selected==2?social.Invites.Count:filtered.Count;int pages=Mathf.Max(1,Mathf.CeilToInt(count/(float)PageSize));page=Mathf.Clamp(page,0,pages-1);
            prev.interactable=page>0;next.interactable=page+1<pages;pageLabel.text=(page+1)+" / "+pages;
            empty.gameObject.SetActive(count==0);empty.text=!social.Ready?"Bağlanınca arkadaşların burada görünür.":selected==2?"Henüz davet yok.\nArkadaşın bu ekranı açıp seni davet edebilir.":selected==1?"Bekleyen arkadaşlık isteği yok.":"İlk koşu arkadaşını ekle.\nTam adını yukarıya yaz ve EKLE'ye dokun.";
            int start=page*PageSize,end=Mathf.Min(count,start+PageSize);
            content.sizeDelta=new Vector2(0,Mathf.Max(360,(end-start)*304));
            for(int i=start;i<end;i++)
            {
                int index=i-start;var row=Panel("Friend row",content,new Vector2(808,288),new Vector2(0,-index*304-144),new Color(.09f,.16f,.25f));
                row.anchorMin=row.anchorMax=new Vector2(.5f,1);rowObjects.Add(row.gameObject);
                if(selected==2)BuildInvite(row,social.Invites[i]);else BuildPerson(row,filtered[i]);
            }
        }
        void BuildPerson(RectTransform row,RunnerFriend person)
        {
            Label(row,person.Name,29,White,new Vector2(430,100),new Vector2(-170,51),true,TextAnchor.MiddleLeft);
            string caption=person.Kind==RunnerFriendKind.Incoming?"ARKADAŞLIK İSTEĞİ":person.Kind==RunnerFriendKind.Outgoing?"İSTEĞİN BEKLENİYOR":person.Online?"ÇEVRİMİÇİ":"ÇEVRİMDIŞI";
            Label(row,caption,22,person.Online?Mint:Muted,new Vector2(410,66),new Vector2(-180,-55),false,TextAnchor.MiddleLeft);
            string id=person.Id;
            if(person.Kind!=RunnerFriendKind.Outgoing)
            {
                var action=Button(row,"Friend action",person.Kind==RunnerFriendKind.Incoming?"KABUL ET":"DAVET ET",new Vector2(264,132),new Vector2(258,70),true);
                action.interactable=social.Ready&&!social.Busy&&(person.Kind==RunnerFriendKind.Incoming||(person.Online&&social.NotificationsConnected));
                action.onClick.AddListener(()=>Run(()=>person.Kind==RunnerFriendKind.Incoming?social.AcceptFriendAsync(id):social.InviteAsync(id)));
            }
            var remove=Button(row,"Remove friend",person.Kind==RunnerFriendKind.Incoming?"REDDET":person.Kind==RunnerFriendKind.Outgoing?"İPTAL ET":"ARKADAŞLIKTAN\nÇIKAR",new Vector2(264,132),new Vector2(258,-70),false);
            remove.GetComponentInChildren<Text>().fontSize=23;remove.interactable=social.Ready&&!social.Busy;remove.onClick.AddListener(()=>Run(()=>social.RemoveAsync(id)));
        }
        void BuildInvite(RectTransform row,RunnerFriendInvite invite)
        {
            Label(row,invite.SenderName,29,White,new Vector2(430,100),new Vector2(-170,51),true,TextAnchor.MiddleLeft);
            Label(row,"SENİ KOŞUYA ÇAĞIRIYOR",22,Mint,new Vector2(410,66),new Vector2(-180,-55),false,TextAnchor.MiddleLeft);
            string id=invite.Id;
            var accept=Button(row,"Accept invitation","KATIL",new Vector2(264,132),new Vector2(258,70),true);accept.interactable=!social.Busy;
            accept.onClick.AddListener(()=>Accept(id));
            var decline=Button(row,"Decline invitation","ŞİMDİ DEĞİL",new Vector2(264,132),new Vector2(258,-70),false);decline.GetComponentInChildren<Text>().fontSize=23;
            decline.onClick.AddListener(()=>social.DismissInvite(id));
        }
        async void Accept(string id)
        {
            try{if(await social.AcceptInviteAsync(id)){if(this!=null){Close();room.Open();}}}
            catch{ /* the backend retains its user-safe status; no exception leaks into gameplay */ }
        }
        async void Run(Func<Task> action){try{await action();}catch{}if(this!=null)dirty=true;}
        void Add(){input.DeactivateInputField();string value=input.text;Run(()=>renaming?social.RenameAsync(value):social.AddByNameAsync(value));}
        void Select(int tab){selected=tab;page=0;scroll.verticalNormalizedPosition=1;dirty=true;}
        void Build(Transform parent)
        {
            root=Rect("Friends and invitations",parent);Stretch(root);
            card=Panel("Friends card",root,new Vector2(932,1540),Vector2.zero,Navy);
            heading=Rect("Friends heading",card).gameObject;
            Label(heading.transform,"KADIKÖY RUN CLUB",24,Gold,new Vector2(810,42),new Vector2(0,697),true);
            Label(heading.transform,"KOŞU ARKADAŞLARIN",54,White,new Vector2(850,90),new Vector2(0,622),true);
            var identityButton=Button(heading.transform,"Copy player name","",new Vector2(534,132),new Vector2(-139,506),false);
            identity=Label(identityButton.transform,"",30,Gold,new Vector2(506,110),Vector2.zero,true);
            identityButton.onClick.AddListener(()=>{if(social.PlayerName.Length>0){GUIUtility.systemCopyBuffer=social.PlayerName;status.text="Adın kopyalandı. Arkadaşına bu tam adı ver.";}});
            renameButton=Button(heading.transform,"Rename player","ADIMI\nDEĞİŞTİR",new Vector2(254,132),new Vector2(279,506),false);renameButton.GetComponentInChildren<Text>().fontSize=24;
            renameButton.onClick.AddListener(()=>{renaming=!renaming;input.text="";dirty=true;});
            Label(heading.transform,"Adına dokun, kopyala ve paylaş.",20,Muted,new Vector2(810,30),new Vector2(0,420));
            modeTitle=Label(heading.transform,"ARKADAŞINI EKLE",22,Muted,new Vector2(810,32),new Vector2(0,385),true,TextAnchor.MiddleLeft);
            var field=Panel("Friend name input",card,new Vector2(542,132),new Vector2(-135,300),new Color(.12f,.21f,.31f));field.GetComponent<Image>().raycastTarget=true;
            input=field.gameObject.AddComponent<InputField>();input.targetGraphic=field.GetComponent<Image>();input.characterLimit=48;input.lineType=InputField.LineType.SingleLine;
            input.keyboardType=TouchScreenKeyboardType.Default;input.navigation=new Navigation{mode=Navigation.Mode.None};
            input.textComponent=Label(field,"",28,White,new Vector2(500,82),Vector2.zero);input.placeholder=Label(field,"Arkadaşının adı#etiketi",24,Muted,new Vector2(500,82),Vector2.zero);
            input.customCaretColor=true;input.caretColor=Gold;input.selectionColor=new Color(1,.8f,.2f,.28f);
            addButton=Button(card,"Add friend","EKLE",new Vector2(246,132),new Vector2(283,300),true);addButton.onClick.AddListener(Add);
            body=Rect("Friends list body",card).gameObject;
            tabs=new Button[3];string[] labels={"ARKADAŞLAR","İSTEKLER","DAVETLER"};
            for(int i=0;i<3;i++){int n=i;tabs[i]=Button(body.transform,"Friends tab "+i,labels[i],new Vector2(264,132),new Vector2((i-1)*275,157),false);tabs[i].GetComponentInChildren<Text>().fontSize=22;tabs[i].onClick.AddListener(()=>Select(n));}
            var viewport=Rect("Friends list viewport",body.transform);viewport.sizeDelta=new Vector2(826,492);viewport.anchoredPosition=new Vector2(0,-171);
            var maskImage=viewport.gameObject.AddComponent<Image>();maskImage.color=new Color(0,0,0,.005f);viewport.gameObject.AddComponent<RectMask2D>();
            content=Rect("Friends list content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(0,612);
            scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=45;
            empty=Label(body.transform,"",30,Muted,new Vector2(760,220),new Vector2(0,-40));
            prev=Button(body.transform,"Previous friends","ÖNCEKİ",new Vector2(220,132),new Vector2(-285,-492),false);prev.GetComponentInChildren<Text>().fontSize=21;prev.onClick.AddListener(()=>{page--;dirty=true;});
            next=Button(body.transform,"Next friends","SONRAKİ",new Vector2(220,132),new Vector2(285,-492),false);next.GetComponentInChildren<Text>().fontSize=21;next.onClick.AddListener(()=>{page++;dirty=true;});
            pageLabel=Label(body.transform,"1 / 1",23,Muted,new Vector2(200,60),new Vector2(0,-492));
            status=Label(card,"",23,Muted,new Vector2(812,58),new Vector2(0,-593));
            footer=Rect("Friends footer",card).gameObject;
            backButton=Button(footer.transform,"Back to room","ODAYA DÖN",new Vector2(396,132),new Vector2(-208,-691),false);backButton.GetComponentInChildren<Text>().fontSize=27;backButton.onClick.AddListener(Close);
            connectButton=Button(footer.transform,"Refresh friends","BAĞLAN",new Vector2(396,132),new Vector2(208,-691),true);connectButton.GetComponentInChildren<Text>().fontSize=27;connectButton.onClick.AddListener(()=>Run(()=>social.ConnectAsync()));
            keyboardTitle=Label(card,"OYUNCU ADINI YAZ",28,Gold,new Vector2(810,56),new Vector2(0,245),true);keyboardTitle.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
        }
        void UpdateKeyboard()
        {
            if(!IsOpen||closing)return;bool visible=TouchScreenKeyboard.visible;float height=TouchScreenKeyboard.area.yMax;
#if UNITY_EDITOR
            if(keyboardFixture>=0){visible=keyboardFixture>0;height=keyboardFixture;}
#endif
            bool nextCompact=input.isFocused&&visible;
            if(nextCompact!=compact)
            {
                compact=nextCompact;heading.SetActive(!compact);body.SetActive(!compact);footer.SetActive(!compact);keyboardTitle.gameObject.SetActive(compact);
                card.sizeDelta=new Vector2(932,compact?620:1540);
                ((RectTransform)input.transform).anchoredPosition=new Vector2(-135,compact?93:300);
                ((RectTransform)addButton.transform).anchoredPosition=new Vector2(283,compact?93:300);
                status.rectTransform.anchoredPosition=new Vector2(0,compact?-134:-593);
                if(!compact){card.localScale=Vector3.one;card.anchoredPosition=Vector2.zero;}
            }
            if(!compact)return;if(height<=0||height>=Screen.height)height=Screen.height*.5f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(ui.safeArea,new Vector2(Screen.width*.5f,height),null,out var local);
            float low=Mathf.Max(ui.safeArea.rect.yMin,local.y),high=ui.safeArea.rect.yMax;
            card.localScale=Vector3.one*Mathf.Min(1,Mathf.Max(.25f,(high-low-32)/620));card.anchoredPosition=new Vector2(0,(high+low)*.5f);
        }
        RectTransform Panel(string name,Transform parent,Vector2 size,Vector2 pos,Color color)
        {var r=Rect(name,parent);r.sizeDelta=size;r.anchoredPosition=pos;var im=r.gameObject.AddComponent<Image>();im.color=color;im.sprite=round;im.type=Image.Type.Sliced;im.raycastTarget=false;return r;}
        Button Button(Transform parent,string name,string label,Vector2 size,Vector2 pos,bool primary)
        {
            var r=Panel(name,parent,size,pos,primary?Gold:new Color(.12f,.21f,.31f));r.GetComponent<Image>().raycastTarget=true;
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();b.navigation=new Navigation{mode=Navigation.Mode.None};r.gameObject.AddComponent<UIButtonMotion>();
            if(label.Length>0)Label(r,label,26,primary?Navy:White,size-new Vector2(20,12),Vector2.zero,true);return b;
        }
        Text Label(Transform parent,string label,int size,Color color,Vector2 dimensions,Vector2 pos,bool heavy=false,TextAnchor align=TextAnchor.MiddleCenter)
        {var r=Rect("Label",parent);r.sizeDelta=dimensions;r.anchoredPosition=pos;var t=r.gameObject.AddComponent<Text>();t.text=label;t.font=heavy?bold:font;t.fontSize=size;t.color=color;t.alignment=align;t.raycastTarget=false;t.supportRichText=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;}
        static RectTransform Rect(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return(RectTransform)go.transform;}
        static void Stretch(RectTransform value){value.anchorMin=Vector2.zero;value.anchorMax=Vector2.one;value.offsetMin=value.offsetMax=Vector2.zero;}
        void OnDestroy(){if(social!=null)social.Changed-=MarkDirty;}
    }
}
