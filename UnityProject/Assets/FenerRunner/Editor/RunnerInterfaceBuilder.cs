using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace FenerRunner.Editor
{
    internal static class RunnerInterfaceBuilder
    {
        static readonly Color Navy=Hex("081729"),Gold=Hex("FFCF42"),White=Hex("F4F5EF"),Muted=Hex("A8B8CA");
        static Font bold,regular; static Sprite round;
        static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out Color c);return c;}
        public static RunnerUI Build(Font fallback)
        {
            bold=AssetDatabase.LoadAssetAtPath<Font>("Assets/FenerRunner/Art/Fonts/BarlowCondensed-Bold.ttf");
            regular=AssetDatabase.LoadAssetAtPath<Font>("Assets/FenerRunner/Art/Fonts/BarlowCondensed-SemiBold.ttf");
            if(bold==null||regular==null)throw new System.InvalidOperationException("Mobile UI fonts missing");
            round=Rounded();
            var root=new GameObject("Fener Runner UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(RunnerUI));
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=0;
            var v=root.GetComponent<RunnerUI>();v.safeArea=Rect("Safe Area",root.transform);Stretch(v.safeArea);
            BuildMenu(v);BuildHud(v);BuildPause(v);BuildResult(v);BuildShop(v);BuildAudioSettings(v);
            v.hud.SetActive(false);v.pausePanel.SetActive(false);v.resultPanel.SetActive(false);
            new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule)).GetComponent<EventSystem>().sendNavigationEvents=false;
            return v;
        }
        static void BuildMenu(RunnerUI v)
        {
            var menu=Rect("Start",v.safeArea);Stretch(menu);v.menu=menu.gameObject;
            menu.gameObject.AddComponent<CanvasGroup>();
            Fade("Menu upper wash",menu,new Vector2(.5f,1),new Vector2(1080,650),new Vector2(0,-325),new Color(.014f,.027f,.06f,.98f),new Color(.014f,.027f,.06f,0));
            Fade("Menu lower wash",menu,new Vector2(.5f,0),new Vector2(1080,680),new Vector2(0,340),new Color(.014f,.027f,.06f,0),new Color(.014f,.027f,.06f,.99f));
            var ornament=Rect("Animated club edge lights",menu);Stretch(ornament);ornament.gameObject.AddComponent<Canvas>();
            var ambient=ornament.gameObject.AddComponent<UIAmbientGraphic>();ambient.color=Gold;ambient.raycastTarget=false;
            var masthead=Rect("Club masthead",menu);Anchor(masthead,new Vector2(.5f,1),new Vector2(0,-89));masthead.sizeDelta=new Vector2(952,132);Entrance(masthead,0,new Vector2(0,14));
            var year=Panel("Club year",masthead,new Vector2(.5f,.5f),new Vector2(92,54),new Vector2(-430,0),Gold);
            Label(year,"1907",30,Navy,new Vector2(88,48),Vector2.zero,true);
            Label(masthead,"KADIKÖY RUN CLUB",24,White,new Vector2(480,48),new Vector2(-126,0),false,TextAnchor.MiddleLeft);
            v.sound=IconButton(masthead,"SES",IconKind.Sound,new Vector2(132,132),new Vector2(410,0),false);
            v.soundLabel=Label(v.sound.transform,"SES",22,Muted,new Vector2(100,30),new Vector2(0,-34));
            var title=Rect("Title composition",menu);Anchor(title,new Vector2(.5f,1),new Vector2(0,-340));title.sizeDelta=new Vector2(940,350);Entrance(title,.08f,new Vector2(0,-22));
            Label(title,"İSMAİL KARTAL İLE",27,Gold,new Vector2(880,43),new Vector2(0,137));
            Label(title,"FENER",116,White,new Vector2(900,160),new Vector2(0,55),true);
            Label(title,"RUNNER",148,Gold,new Vector2(920,200),new Vector2(0,-72),true);
            Panel("Left title rule",title,new Vector2(.5f,.5f),new Vector2(66,2),new Vector2(-411,-167),Gold);
            Panel("Right title rule",title,new Vector2(.5f,.5f),new Vector2(66,2),new Vector2(411,-167),Gold);
            Label(title,"BİR SEMT. BİR RUH. SONSUZ KOŞU.",24,White,new Vector2(720,42),new Vector2(0,-167));
            var hero=Rect("Kartala dokun",menu);Anchor(hero,new Vector2(.5f,.5f),new Vector2(0,80));hero.sizeDelta=new Vector2(820,560);
            var heroImage=hero.gameObject.AddComponent<Image>();heroImage.color=Color.clear;heroImage.raycastTarget=true;
            v.heroGesture=hero.gameObject.AddComponent<Button>();v.heroGesture.targetGraphic=heroImage;v.heroGesture.transition=Selectable.Transition.None;v.heroGesture.navigation=new Navigation{mode=Navigation.Mode.None};
            var foot=Rect("Start controls",menu);Anchor(foot,new Vector2(.5f,0),new Vector2(0,270));foot.sizeDelta=new Vector2(920,490);Entrance(foot,.22f,new Vector2(0,-40));
            var record=Rect("Personal best",foot);record.sizeDelta=new Vector2(912,62);record.anchoredPosition=new Vector2(0,208);
            Label(record,"KENDİ REKORUNU AŞ",22,Muted,new Vector2(450,40),new Vector2(-229,0),false,TextAnchor.MiddleLeft);
            v.bestMenu=Label(record,"EN İYİ  0",30,White,new Vector2(400,54),new Vector2(255,0),true,TextAnchor.MiddleRight);
            v.play=Button(foot,"OYNA","SAHAYA ÇIK",new Vector2(920,148),new Vector2(0,94),true);v.play.GetComponent<UIButtonMotion>().pulse=true;
            Icon(v.play.transform,IconKind.Right,new Vector2(36,36),new Vector2(380,0),Navy);
            v.openShop=Button(foot,"Jeton atölyesi","TRİBÜN ATÖLYESİ",new Vector2(920,132),new Vector2(0,-70),false);
            Icon(v.openShop.transform,IconKind.Chick,new Vector2(35,35),new Vector2(-380,0),Gold);
            Label(foot,"TEK PARMAK. DÖRT YÖN.",22,Muted,new Vector2(540,42),new Vector2(-189,-193),false,TextAnchor.MiddleLeft);
            v.walletMenu=Label(foot,"0 JETON",28,Gold,new Vector2(320,44),new Vector2(300,-193),true,TextAnchor.MiddleRight);
        }
        static void BuildHud(RunnerUI v)
        {
            var hud=Rect("Running",v.safeArea);Stretch(hud);v.hud=hud.gameObject;
            Fade("HUD top shade",hud,new Vector2(.5f,1),new Vector2(1080,355),new Vector2(0,-177.5f),new Color(.012f,.026f,.054f,.94f),new Color(.012f,.026f,.054f,0));
            var scoreGroup=Rect("Run score",hud);Anchor(scoreGroup,new Vector2(0,1),new Vector2(240,-100));scoreGroup.sizeDelta=new Vector2(370,164);
            Label(scoreGroup,"KOŞU SKORU",20,Muted,new Vector2(350,33),new Vector2(0,54),false,TextAnchor.MiddleLeft);
            v.score=Label(scoreGroup,"0",76,White,new Vector2(360,100),new Vector2(4,-6),true,TextAnchor.MiddleLeft);
            Fit(v.score,42,76);
            v.distance=Label(scoreGroup,"0 m",29,White,new Vector2(220,45),new Vector2(-65,-72),false,TextAnchor.MiddleLeft);
            v.speed=Label(scoreGroup,"65 KM/SA",26,Muted,new Vector2(200,45),new Vector2(131,-72),false,TextAnchor.MiddleLeft);
            var tokens=Panel("Token counter",hud,new Vector2(1,1),new Vector2(186,70),new Vector2(-278,-91),new Color(.11f,.19f,.29f,.66f));
            Icon(tokens,IconKind.Chick,new Vector2(29,29),new Vector2(-54,0),Gold);v.coins=Label(tokens,"0",36,Gold,new Vector2(113,56),new Vector2(22,0),true);
            Fit(v.coins,20,36);
            var pauseAnchor=Rect("Pause anchor",hud);Anchor(pauseAnchor,new Vector2(1,1),new Vector2(-94,-91));
            v.pause=IconButton(pauseAnchor,"II",IconKind.Pause,new Vector2(132,132),Vector2.zero,false);
            var track=Panel("Charge track",hud,new Vector2(.5f,1),new Vector2(968,5),new Vector2(0,-216),new Color(.37f,.46f,.57f,.38f));
            var fill=Panel("Charge fill",track,new Vector2(.5f,.5f),new Vector2(968,5),Vector2.zero,Gold);v.chargeFill=fill.GetComponent<Image>();v.chargeFill.type=Image.Type.Filled;v.chargeFill.fillMethod=Image.FillMethod.Horizontal;v.chargeFill.fillAmount=0;
            v.chargeLabel=Label(hud,"ATEŞİ YAK   0 / 10",21,Gold,new Vector2(450,36),Vector2.zero);Anchor(v.chargeLabel.rectTransform,new Vector2(0,1),new Vector2(281,-248));v.chargeLabel.alignment=TextAnchor.MiddleLeft;
            v.stage=Label(hud,"ISINMA",21,White,new Vector2(185,36),Vector2.zero);Anchor(v.stage.rectTransform,new Vector2(1,1),new Vector2(-365,-248));v.stage.alignment=TextAnchor.MiddleRight;
            v.atmosphere=Label(hud,"GÜNBATIMI",21,Muted,new Vector2(170,36),Vector2.zero);Anchor(v.atmosphere.rectTransform,new Vector2(1,1),new Vector2(-142,-248));v.atmosphere.alignment=TextAnchor.MiddleRight;
            v.stage.resizeTextForBestFit=v.atmosphere.resizeTextForBestFit=true;v.stage.resizeTextMinSize=v.atmosphere.resizeTextMinSize=15;v.stage.resizeTextMaxSize=v.atmosphere.resizeTextMaxSize=21;
            Panel("Context separator",hud,new Vector2(1,1),new Vector2(1,21),new Vector2(-247,-248),new Color(.5f,.62f,.76f,.35f));
            var progress=Rect("Skill and rotating challenge",hud);Anchor(progress,new Vector2(.5f,1),new Vector2(0,-292));progress.sizeDelta=new Vector2(968,44);
            v.combo=Label(progress,"SERİ 0  ·  ×1",26,White,new Vector2(296,42),new Vector2(-336,0),true,TextAnchor.MiddleLeft);Fit(v.combo,20,26);
            Panel("Skill separator",progress,new Vector2(.5f,.5f),new Vector2(1,28),new Vector2(-175,1),new Color(.5f,.62f,.76f,.3f));
            v.challengeLabel=Label(progress,"YENİ HEDEF",22,White,new Vector2(370,36),new Vector2(22,3),true,TextAnchor.MiddleLeft);Fit(v.challengeLabel,18,22);
            v.challengeProgress=Label(progress,"0 / 1  ·  +0",21,Gold,new Vector2(252,36),new Vector2(358,3),false,TextAnchor.MiddleRight);Fit(v.challengeProgress,17,21);
            var goalTrack=Panel("Challenge track",progress,new Vector2(.5f,.5f),new Vector2(659,3),new Vector2(154.5f,-21),new Color(.37f,.46f,.57f,.3f));
            var goalFill=Panel("Challenge fill",goalTrack,new Vector2(.5f,.5f),new Vector2(659,3),Vector2.zero,Gold);v.challengeFill=goalFill.GetComponent<Image>();v.challengeFill.type=Image.Type.Filled;v.challengeFill.fillMethod=Image.FillMethod.Horizontal;v.challengeFill.fillAmount=0;
            v.toast=Label(hud,"",35,Gold,new Vector2(920,70),Vector2.zero,true);Anchor(v.toast.rectTransform,new Vector2(.5f,1),new Vector2(0,-500));
            var power=Panel("Collected power status",hud,new Vector2(.5f,1),new Vector2(900,128),new Vector2(0,-394),new Color(.035f,.09f,.15f,.94f));v.powerPanel=power.gameObject;
            v.powerIcon=Icon(power,IconKind.Bolt,new Vector2(38,38),new Vector2(-394,20),Gold);
            v.powerStatus=Label(power,"",27,White,new Vector2(750,40),new Vector2(20,32),true,TextAnchor.MiddleLeft);
            v.powerDetails=Label(power,"",21,Muted,new Vector2(750,31),new Vector2(20,0),false,TextAnchor.MiddleLeft);
            v.protectionStatus=Label(power,"",21,Gold,new Vector2(750,31),new Vector2(20,-29),false,TextAnchor.MiddleLeft);
            var powerTrack=Panel("Power time",power,new Vector2(.5f,.5f),new Vector2(814,4),new Vector2(0,-52),Gold);
            v.powerFill=powerTrack.GetComponent<Image>();v.powerFill.type=Image.Type.Filled;v.powerFill.fillMethod=Image.FillMethod.Horizontal;power.gameObject.SetActive(false);
            var hint=Rect("Brief swipe reminder",hud);Anchor(hint,new Vector2(.5f,0),new Vector2(0,124));hint.sizeDelta=new Vector2(870,80);
            v.swipeReminder=hint.gameObject.AddComponent<CanvasGroup>();v.swipeReminder.blocksRaycasts=false;v.swipeReminder.interactable=false;
            var hintBack=hint;
            Icon(hintBack,IconKind.Left,new Vector2(23,23),new Vector2(-370,0),Gold);Icon(hintBack,IconKind.Right,new Vector2(23,23),new Vector2(-330,0),Gold);
            Label(hintBack,"ŞERİT",22,White,new Vector2(110,34),new Vector2(-235,0));
            Icon(hintBack,IconKind.Jump,new Vector2(25,25),new Vector2(-50,0),Gold);Label(hintBack,"ZIPLA ×2",22,White,new Vector2(110,34),new Vector2(40,0));
            Icon(hintBack,IconKind.Slide,new Vector2(25,25),new Vector2(218,0),Gold);Label(hintBack,"KAY ×2",22,White,new Vector2(110,34),new Vector2(310,0));
            // No movement Buttons. The guide is visual only and never consumes a swipe.
        }
        static void BuildPause(RunnerUI v)
        {
            var paused=Overlay("Paused",v.safeArea);v.pausePanel=paused.gameObject;var card=Card(paused,"Pause card",new Vector2(900,1110));
            Panel("Pause gold rule",card,new Vector2(.5f,.5f),new Vector2(74,5),new Vector2(0,465),Gold);
            Label(card,"MAÇ ARASI",78,White,new Vector2(790,128),new Vector2(0,362),true);
            Label(card,"Ritmini kaybetme. Tribün seni bekliyor.",29,Muted,new Vector2(790,58),new Vector2(0,263));
            v.resume=Button(card,"DEVAM ET","DEVAM ET",new Vector2(744,126),new Vector2(0,125),true);
            v.restart=Button(card,"YENİDEN BAŞLAT","YENİDEN BAŞLAT",new Vector2(744,108),new Vector2(0,-28),false);
            v.home=Button(card,"Ana menüye dön","ANA MENÜYE DÖN",new Vector2(744,132),new Vector2(0,-184),false);
            Label(card,"Topladığın civcivler cüzdanında kalır.",23,Muted,new Vector2(744,38),new Vector2(0,-281));
            var soundRow=Panel("Sound row",card,new Vector2(.5f,.5f),new Vector2(744,132),new Vector2(0,-386),new Color(.18f,.29f,.4f,.4f));
            v.pauseSound=soundRow.gameObject.AddComponent<Button>();soundRow.GetComponent<Image>().raycastTarget=true;v.pauseSound.targetGraphic=soundRow.GetComponent<Image>();v.pauseSound.navigation=new Navigation{mode=Navigation.Mode.None};soundRow.gameObject.AddComponent<UIButtonMotion>();
            Icon(soundRow,IconKind.Sound,new Vector2(27,27),new Vector2(-237,0),Gold);
            v.pauseSoundLabel=Label(soundRow,"TRİBÜN SESİ  AÇIK",25,White,new Vector2(500,45),new Vector2(43,0));
        }
        static void BuildResult(RunnerUI v)
        {
            var ended=Overlay("Finished",v.safeArea);v.resultPanel=ended.gameObject;var card=Card(ended,"Result card",new Vector2(920,1300));
            Label(card,"KADIKÖY RUN CLUB",24,Gold,new Vector2(800,42),new Vector2(0,453));
            Label(card,"İYİ KOŞTUN.",80,White,new Vector2(820,125),new Vector2(0,347),true);
            Label(card,"KOŞU SKORUN",24,Muted,new Vector2(800,42),new Vector2(0,236));
            v.resultScore=Label(card,"0",140,Gold,new Vector2(840,184),new Vector2(0,126),true);
            Fit(v.resultScore,72,140);
            v.resultBest=Label(card,"EN İYİ  0",25,White,new Vector2(820,42),new Vector2(0,17));
            Panel("Result divider",card,new Vector2(.5f,.5f),new Vector2(752,1),new Vector2(0,-39),new Color(.48f,.6f,.72f,.3f));
            v.resultDistance=Label(card,"",30,White,new Vector2(840,74),new Vector2(0,-106));
            v.resultSkill=Label(card,"EN İYİ SERİ 0   ·   0 HEDEF   ·   +0 JETON",24,Gold,new Vector2(786,42),new Vector2(0,-169),true);Fit(v.resultSkill,20,24);
            v.resultTip=Label(card,"",29,Muted,new Vector2(786,92),new Vector2(0,-247));
            v.retry=Button(card,"TEKRAR KOŞ","BİR TUR DAHA",new Vector2(764,130),new Vector2(0,-419),true);
            Icon(v.retry.transform,IconKind.Right,new Vector2(37,37),new Vector2(302,0),Navy);
            v.resultShop=Button(card,"Atölyeye dön","JETONLARINI DEĞERLENDİR",new Vector2(764,132),new Vector2(0,-570),false);
            v.resultShop.GetComponentInChildren<Text>().fontSize=29;
        }
        static void BuildShop(RunnerUI v)
        {
            var overlay=Overlay("Token workshop",v.safeArea);v.shopPanel=overlay.gameObject;
            var card=Card(overlay,"Workshop card",new Vector2(948,1720));
            Label(card,"KADIKÖY RUN CLUB  /  1907",22,Gold,new Vector2(844,38),new Vector2(0,791));
            Label(card,"TRİBÜN ATÖLYESİ",64,White,new Vector2(850,90),new Vector2(0,722),true);
            var bank=Panel("Permanent wallet",card,new Vector2(.5f,.5f),new Vector2(844,100),new Vector2(0,605),new Color(.16f,.24f,.32f,.84f));
            Icon(bank,IconKind.Chick,new Vector2(42,42),new Vector2(-355,0),Gold);
            Label(bank,"CÜZDANIN",24,Muted,new Vector2(300,44),new Vector2(-153,0),false,TextAnchor.MiddleLeft);
            v.walletShop=Label(bank,"0 CİVCİV",40,Gold,new Vector2(390,66),new Vector2(184,0),true,TextAnchor.MiddleRight);Fit(v.walletShop,28,40);
            v.shopTabs=new Button[3];v.shopPages=new GameObject[3];v.shopScrolls=new ScrollRect[3];
            string[] tabs={"GÜÇLER","YETENEKLER","GARAJ"};
            for(int i=0;i<3;i++)
            {
                v.shopTabs[i]=Button(card,"Shop tab "+i,tabs[i],new Vector2(270,132),new Vector2(-287+i*287,462),false);
                v.shopTabs[i].GetComponentInChildren<Text>().fontSize=28;
            }
            Label(card,"KEŞFETMEK İÇİN LİSTEYİ KAYDIR",21,Muted,new Vector2(844,34),new Vector2(0,369));
            int count=RunnerPowerups.TypeCount;
            v.upgradeButtons=new Button[count];v.upgradeLevels=new Text[count];v.upgradePrices=new Text[count];v.upgradeCards=new UIShopCard[count];
            RectTransform powers=ShopScroll(v,card,0,count*280+16);
            for(int i=0;i<count;i++)
            {
                var kind=(PowerupKind)i;Color accent=RunnerUI.PowerColor(kind);
                var row=Panel("Upgrade "+kind,powers,new Vector2(.5f,1),new Vector2(824,260),new Vector2(0,-146-i*280),new Color(.09f,.16f,.24f,.98f));
                var glow=Panel("Purchase glow",row,new Vector2(.5f,.5f),new Vector2(824,260),Vector2.zero,Color.clear).GetComponent<Image>();
                Panel("Power accent",row,new Vector2(.5f,.5f),new Vector2(752,2),new Vector2(0,128),accent);
                var medal=Rect("Power medallion",row);medal.sizeDelta=new Vector2(96,96);medal.anchoredPosition=new Vector2(-340,40);
                Icon(medal,IconKind.Ring,new Vector2(94,94),Vector2.zero,accent);Icon(medal,RunnerUI.PowerIcon(kind),new Vector2(48,48),Vector2.zero,accent);
                var label=Label(row,RunnerPowerups.LabelFor(kind),36,White,new Vector2(430,52),new Vector2(-54,85),true,TextAnchor.MiddleLeft);Fit(label,28,36);
                Text duration=Label(row,"",31,White,new Vector2(402,48),new Vector2(-40,30),true,TextAnchor.MiddleLeft);
                var description=Label(row,RunnerShopCatalog.PowerDescription(kind),23,Muted,new Vector2(402,68),new Vector2(-40,-32),false,TextAnchor.MiddleLeft);Fit(description,21,23);
                var pips=new Image[3];for(int j=0;j<3;j++)pips[j]=Panel("Level "+j,row,new Vector2(.5f,.5f),new Vector2(42,8),new Vector2(-353+j*51,-102),new Color(.32f,.42f,.53f,.45f)).GetComponent<Image>();
                v.upgradeLevels[i]=Label(row,"SEVİYE 0 / 3",22,accent,new Vector2(260,38),new Vector2(-20,-102),false,TextAnchor.MiddleLeft);
                v.upgradeButtons[i]=Button(row,"Buy "+kind,"",new Vector2(220,132),new Vector2(284,40),true);
                v.upgradePrices[i]=Label(v.upgradeButtons[i].transform,"",30,Navy,new Vector2(204,116),Vector2.zero,true);
                var particles=Rect("Purchase particles",row);Stretch(particles);var burst=particles.gameObject.AddComponent<UIRewardBurst>();burst.raycastTarget=false;
                v.upgradeCards[i]=row.gameObject.AddComponent<UIShopCard>();v.upgradeCards[i].Configure(duration,pips,medal,glow,v.upgradeButtons[i].GetComponent<UIButtonMotion>(),burst,accent);
            }
            v.permanentButtons=new Button[RunWallet.PermanentUpgradeCount];v.permanentLevels=new Text[v.permanentButtons.Length];v.permanentPrices=new Text[v.permanentButtons.Length];
            RectTransform talents=ShopScroll(v,card,1,v.permanentButtons.Length*292+16);
            IconKind[] talentIcons={IconKind.Magnet,IconKind.Rain,IconKind.Rhythm,IconKind.Trail,IconKind.Chick};
            for(int i=0;i<v.permanentButtons.Length;i++)
            {
                var kind=(RunnerUpgrade)i;var row=Panel("Talent "+kind,talents,new Vector2(.5f,1),new Vector2(824,272),new Vector2(0,-152-i*292),new Color(.09f,.16f,.24f,.98f));
                Icon(row,talentIcons[i],new Vector2(50,50),new Vector2(-350,75),Gold);
                Label(row,RunnerShopCatalog.Name(kind),35,White,new Vector2(460,54),new Vector2(-60,77),true,TextAnchor.MiddleLeft);
                var desc=Label(row,RunnerShopCatalog.Description(kind),26,Muted,new Vector2(480,100),new Vector2(-130,-6),false,TextAnchor.MiddleLeft);Fit(desc,23,26);
                v.permanentLevels[i]=Label(row,"",23,Gold,new Vector2(744,42),new Vector2(0,-101),false,TextAnchor.MiddleLeft);
                v.permanentButtons[i]=Button(row,"Buy talent "+kind,"",new Vector2(220,132),new Vector2(284,28),true);
                v.permanentPrices[i]=Label(v.permanentButtons[i].transform,"",30,Navy,new Vector2(204,116),Vector2.zero,true);
            }
            v.vehicleButtons=new Button[RunWallet.VehicleCount];v.vehiclePrices=new Text[v.vehicleButtons.Length];
            RectTransform garage=ShopScroll(v,card,2,v.vehicleButtons.Length*470+16);
            for(int i=0;i<v.vehicleButtons.Length;i++)
            {
                var kind=(ChaseVehicleKind)i;var row=Panel("Garage "+kind,garage,new Vector2(.5f,1),new Vector2(824,450),new Vector2(0,-241-i*470),new Color(.075f,.14f,.22f,.98f));
                Label(row,RunnerShopCatalog.VehicleName(kind),37,White,new Vector2(744,55),new Vector2(0,180),true,TextAnchor.MiddleLeft);
                var art=Rect("Vehicle preview",row);art.sizeDelta=new Vector2(744,230);art.anchoredPosition=new Vector2(0,39);
                var picture=art.gameObject.AddComponent<Image>();picture.sprite=Resources.Load<Sprite>("VehiclePreview_"+kind);picture.preserveAspect=true;picture.raycastTarget=false;
                Label(row,RunnerShopCatalog.VehicleDescription(kind),25,Muted,new Vector2(475,64),new Vector2(-132,-126),false,TextAnchor.MiddleLeft);
                Label(row,"GÖRÜNÜM · OYUN DENGESİ AYNI",20,Gold,new Vector2(475,34),new Vector2(-132,-178),false,TextAnchor.MiddleLeft);
                v.vehicleButtons[i]=Button(row,"Select vehicle "+kind,"",new Vector2(238,132),new Vector2(277,-139),true);
                v.vehiclePrices[i]=Label(v.vehicleButtons[i].transform,"",29,Navy,new Vector2(222,116),Vector2.zero,true);
            }
            v.shopMessage=Label(card,"",25,Muted,new Vector2(814,72),new Vector2(0,-599));Fit(v.shopMessage,23,25);
            v.closeShop=Button(card,"Back to run","KOŞUYA DÖN",new Vector2(844,132),new Vector2(0,-719),false);
            Icon(v.closeShop.transform,IconKind.Left,new Vector2(30,30),new Vector2(-340,0),Gold);
            Label(card,"SADECE OYNAYARAK KAZANDIĞIN CİVCİVLER.",21,Muted,new Vector2(844,34),new Vector2(0,-818));
            overlay.gameObject.SetActive(false);
        }
        static RectTransform ShopScroll(RunnerUI v,Transform parent,int index,float contentHeight)
        {
            var page=Rect("Shop page "+index,parent);page.sizeDelta=new Vector2(860,864);page.anchoredPosition=new Vector2(0,-95);
            var hit=page.gameObject.AddComponent<Image>();hit.color=new Color(0,0,0,.01f);hit.raycastTarget=true;
            page.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Scrollable cards",page);content.anchorMin=content.anchorMax=new Vector2(.5f,1);content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(844,contentHeight);content.anchoredPosition=Vector2.zero;
            var scroll=page.gameObject.AddComponent<ScrollRect>();scroll.content=content;scroll.viewport=page;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.inertia=true;scroll.decelerationRate=.1f;scroll.scrollSensitivity=45;
            var rail=Rect("Scroll rail",page);rail.anchorMin=new Vector2(1,0);rail.anchorMax=new Vector2(1,1);rail.pivot=new Vector2(1,.5f);rail.sizeDelta=new Vector2(6,0);rail.anchoredPosition=Vector2.zero;
            var track=rail.gameObject.AddComponent<Image>();track.color=new Color(.4f,.5f,.6f,.15f);track.raycastTarget=false;
            var handle=Rect("Scroll handle",rail);Stretch(handle);var image=handle.gameObject.AddComponent<Image>();image.color=new Color(1,.81f,.26f,.7f);image.raycastTarget=false;
            var bar=rail.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.direction=Scrollbar.Direction.BottomToTop;bar.targetGraphic=image;bar.navigation=new Navigation{mode=Navigation.Mode.None};scroll.verticalScrollbar=bar;
            v.shopPages[index]=page.gameObject;v.shopScrolls[index]=scroll;page.gameObject.SetActive(index==0);return content;
        }
        static RectTransform Card(Transform parent,string name,Vector2 size)
        {var r=Panel(name,parent,new Vector2(.5f,.5f),size,Vector2.zero,Navy);var g=r.gameObject.AddComponent<UIGradient>();g.top=Hex("162C43");g.bottom=Hex("071322");r.GetComponent<Image>().color=Color.white;Entrance(r);return r;}
        static void BuildAudioSettings(RunnerUI v)
        {
            var overlay=Overlay("Sound studio",v.safeArea);v.audioPanel=overlay.gameObject;
            var card=Card(overlay,"Sound settings card",new Vector2(920,1540));
            Icon(card,IconKind.Sound,new Vector2(40,40),new Vector2(0,672),Gold);
            Label(card,"TRİBÜNÜN SESİ",66,White,new Vector2(820,100),new Vector2(0,572),true);
            Label(card,"RİTMİ KENDİNE GÖRE AYARLA",25,Muted,new Vector2(820,48),new Vector2(0,494));
            v.volumeSliders=new Slider[4];v.volumeValues=new Text[4];
            string[] names={"GENEL SES","MÜZİK","KONUŞMALAR","EFEKTLER"};
            Color[] accents={Gold,Hex("79DFFF"),Hex("DBA0FF"),Hex("90EBC3")};
            for(int i=0;i<4;i++)
            {
                var row=Rect(names[i],card);row.sizeDelta=new Vector2(800,150);row.anchoredPosition=new Vector2(0,325-i*185);
                var hit=row.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
                Label(row,names[i],31,White,new Vector2(590,51),new Vector2(-95,40),true,TextAnchor.MiddleLeft);
                v.volumeValues[i]=Label(row,"%100",32,accents[i],new Vector2(130,51),new Vector2(325,40),true,TextAnchor.MiddleRight);
                var track=Panel("Volume rail",row,new Vector2(.5f,.5f),new Vector2(756,12),new Vector2(0,-28),new Color(.28f,.37f,.48f,.65f));
                var fill=Panel("Volume fill",track,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero,accents[i]);Stretch(fill);
                var handles=Rect("Handle travel",row);handles.sizeDelta=new Vector2(756,12);handles.anchoredPosition=new Vector2(0,-28);
                var handle=Panel("Volume handle",handles,new Vector2(1,.5f),new Vector2(46,46),Vector2.zero,White);handle.GetComponent<Image>().raycastTarget=true;
                var dot=Panel("Handle centre",handle,new Vector2(.5f,.5f),new Vector2(16,16),Vector2.zero,accents[i]);
                var slider=row.gameObject.AddComponent<Slider>();slider.direction=Slider.Direction.LeftToRight;slider.minValue=0;slider.maxValue=1;slider.wholeNumbers=false;
                slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.navigation=new Navigation{mode=Navigation.Mode.None};slider.value=1;
                v.volumeSliders[i]=slider;
            }
            v.audioMute=Button(card,"Mute all audio","",new Vector2(800,132),new Vector2(0,-422),false);
            v.audioMuteLabel=Label(v.audioMute.transform,"TÜM SESLERİ SUSTUR",30,White,new Vector2(720,60),Vector2.zero,true);
            v.closeAudio=Button(card,"Close sound settings","TAMAM",new Vector2(800,144),new Vector2(0,-600),true);
            Label(card,"Kaydırarak ayarla. Tercihin kaydedilir.",24,Muted,new Vector2(820,48),new Vector2(0,-710));
            overlay.gameObject.SetActive(false);
        }
        static RectTransform Overlay(string name,Transform parent){var r=Rect(name,parent);Stretch(r);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.006f,.015f,.03f,.78f);return r;}
        static void Entrance(RectTransform r,float delay=0,Vector2? offset=null)
        {r.gameObject.AddComponent<CanvasGroup>();var entrance=r.gameObject.AddComponent<UIEntrance>();entrance.delay=delay;entrance.offset=offset??new Vector2(0,-32);}
        static void Fade(string name,Transform parent,Vector2 anchor,Vector2 size,Vector2 pos,Color top,Color bottom)
        {var r=Panel(name,parent,anchor,size,pos,Color.white);r.GetComponent<Image>().sprite=null;var g=r.gameObject.AddComponent<UIGradient>();g.top=top;g.bottom=bottom;}
        static Button IconButton(Transform parent,string name,IconKind kind,Vector2 size,Vector2 pos,bool primary)
        {var b=Button(parent,name,"",size,pos,primary);Icon(b.transform,kind,size*.31f,new Vector2(0,name=="SES"?8:0),primary?Navy:White);return b;}
        static Button Button(Transform parent,string name,string label,Vector2 size,Vector2 pos,bool primary)
        {
            size.y=Mathf.Max(132,size.y);
            var r=Panel(name,parent,new Vector2(.5f,.5f),size,pos,Color.white);r.GetComponent<Image>().raycastTarget=true;
            var gradient=r.gameObject.AddComponent<UIGradient>();gradient.top=primary?Hex("FFDF68"):new Color(.16f,.27f,.38f,.94f);gradient.bottom=primary?Hex("F4BD25"):new Color(.08f,.15f,.24f,.94f);
            var button=r.gameObject.AddComponent<Button>();button.targetGraphic=r.GetComponent<Image>();button.navigation=new Navigation{mode=Navigation.Mode.None};
            var c=button.colors;c.pressedColor=new Color(.8f,.86f,.94f);c.highlightedColor=Color.white;button.colors=c;
            r.gameObject.AddComponent<UIButtonMotion>();
            if(primary)
            {
                var shine=Rect("Gold light sweep",r);shine.sizeDelta=size-new Vector2(28,28);shine.gameObject.AddComponent<Canvas>();
                var graphic=shine.gameObject.AddComponent<UIAmbientGraphic>();graphic.sheen=true;graphic.phase=4.85f;graphic.color=new Color(1,1,.9f,.22f);graphic.raycastTarget=false;
            }
            if(label.Length>0)Label(r,label,41,primary?Navy:White,size-new Vector2(90,16),Vector2.zero,true);return button;
        }
        static UIIcon Icon(Transform parent,IconKind kind,Vector2 size,Vector2 pos,Color color){var r=Rect(kind.ToString(),parent);r.sizeDelta=size;r.anchoredPosition=pos;var icon=r.gameObject.AddComponent<UIIcon>();icon.kind=kind;icon.color=color;icon.raycastTarget=false;return icon;}
        static Text Label(Transform parent,string text,int size,Color color,Vector2 dimensions,Vector2 pos,bool heavy=false,TextAnchor align=TextAnchor.MiddleCenter)
        {var r=Rect(text.Length==0?"Readout":text,parent);r.sizeDelta=dimensions;r.anchoredPosition=pos;var t=r.gameObject.AddComponent<Text>();t.font=heavy?bold:regular;t.text=text;t.fontSize=size;t.color=color;t.alignment=align;t.supportRichText=true;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;}
        static void Fit(Text label,int minimum,int maximum)
        {label.resizeTextForBestFit=true;label.resizeTextMinSize=minimum;label.resizeTextMaxSize=maximum;}
        static RectTransform Panel(string name,Transform parent,Vector2 anchor,Vector2 size,Vector2 pos,Color color)
        {var r=Rect(name,parent);Anchor(r,anchor,pos);r.sizeDelta=size;var image=r.gameObject.AddComponent<Image>();image.color=color;image.sprite=round;image.type=Image.Type.Sliced;image.raycastTarget=false;return r;}
        static RectTransform Rect(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
        static void Anchor(RectTransform r,Vector2 anchor,Vector2 pos){r.anchorMin=r.anchorMax=anchor;r.anchoredPosition=pos;}
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        static Sprite Rounded()
        {
            string path=RunnerSceneBuilder.Generated+"/Rendering/MobileRounded.png";var tex=new Texture2D(128,128,TextureFormat.RGBA32,false);
            for(int y=0;y<128;y++)for(int x=0;x<128;x++){float dx=Mathf.Max(24-x,Mathf.Max(x-103,0)),dy=Mathf.Max(24-y,Mathf.Max(y-103,0));tex.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(24.5f-Mathf.Sqrt(dx*dx+dy*dy))));}
            tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spriteBorder=new Vector4(26,26,26,26);importer.mipmapEnabled=false;importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(sprite==null)throw new System.InvalidOperationException("Rounded button sprite was not imported");return sprite;
        }
    }
}
