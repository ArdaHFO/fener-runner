using UnityEngine;
using UnityEngine.UI;
namespace FenerRunner
{
    public sealed class RunnerUI : MonoBehaviour
    {
        public RectTransform safeArea;
        public GameObject menu,hud,pausePanel,resultPanel;
        public Text distance,speed,coins,score,combo,chargeLabel,toast,resultScore,resultDistance,resultTip,bestMenu,soundLabel;
        public Text stage,atmosphere,resultBest,pauseSoundLabel;
        public Image chargeFill;
        public CanvasGroup swipeReminder;
        public Button play,pause,resume,restart,retry,sound,pauseSound,home;
        public GameObject shopPanel,powerPanel;
        public Button openShop,closeShop,resultShop;
        public Button[] upgradeButtons;
        public Text walletMenu,walletShop,powerStatus,shopMessage,powerDetails,protectionStatus;
        public UIIcon powerIcon;
        public Text[] upgradeLevels,upgradePrices;
        public Image powerFill;
        public UIShopCard[] upgradeCards;
        public Button[] shopTabs,permanentButtons,vehicleButtons;
        public GameObject[] shopPages;
        public Text[] permanentLevels,permanentPrices,vehiclePrices;
        public ScrollRect[] shopScrolls;
        public int ShopTab { get; private set; }
        public Button heroGesture;
        public GameObject audioPanel;
        public Slider[] volumeSliders;
        public Text[] volumeValues;
        public Button audioMute,closeAudio;
        public Text audioMuteLabel;
        public Text challengeLabel,challengeProgress,resultSkill;
        public Image challengeFill;
        public bool AudioOpen=>audioPanel!=null&&audioPanel.activeSelf;
        public bool ShopOpen => shopPanel!=null&&shopPanel.activeSelf;
        public bool MultiplayerOpen=>multiplayer!=null&&multiplayer.IsOpen;
        public bool MultiplayerConnected=>multiplayer!=null&&multiplayer.IsSessionActive;
        MultiplayerUI multiplayer;
        CanvasGroup menuVisibility,pauseVisibility;
        float menuOpacity=1f,purchaseCooldown,resultClock;
        int resultTarget,lastResultCount=-1;
        // Source compatibility for older development checks. No movement buttons exist in the scene.
        [System.NonSerialized] public Button left,right,jump,slide;
        RunnerGame game; StadiumSound audioSystem; Rect previousArea; Vector2Int previousSize;
        int shownDistance=-1,shownSpeed=-1,shownCoins=-1,shownScore=-1,shownPassed=-1,recordAtStart;
        float toastClock,coinPulse,reminderClock,skillPulse,goalCompleteClock,powerRefreshClock;
        int shownStreak=-1,shownGoalCurrent=-1,shownGoalTarget=-1,shownGoalReward=-1;
        float shownMultiplier=-1,goalFillTarget;
        string shownGoalName="",completedGoalText="";
        public bool GoalCompletionVisible=>goalCompleteClock>0;
        readonly Color gold=new Color(1,.81f,.26f),white=new Color(.96f,.96f,.94f);
        static readonly PowerupKind[] powerKinds=(PowerupKind[])System.Enum.GetValues(typeof(PowerupKind));

        public void Bind(RunnerGame owner)
        {
            game=owner;audioSystem=game.GetComponent<StadiumSound>();
            menuVisibility=menu.GetComponent<CanvasGroup>();
            if(menuVisibility==null)menuVisibility=menu.AddComponent<CanvasGroup>();
            pauseVisibility=pausePanel.GetComponent<CanvasGroup>();if(pauseVisibility==null)pauseVisibility=pausePanel.AddComponent<CanvasGroup>();
            play.onClick.AddListener(()=>{if(!ShopOpen&&!AudioOpen&&!MultiplayerOpen&&!MultiplayerConnected&&game.State==RunState.Menu)game.Play();});
            retry.onClick.AddListener(()=>{if(MultiplayerConnected){multiplayer.Open();return;}if(!MultiplayerOpen)game.Restart();});
            pause.onClick.AddListener(()=>{if(!MultiplayerOpen)game.Pause();});resume.onClick.AddListener(()=>{if(!AudioOpen&&!MultiplayerOpen)game.Resume();});
            restart.onClick.AddListener(()=>{if(AudioOpen||MultiplayerOpen)return;if(MultiplayerConnected){multiplayer.Open();return;}game.Restart();});
            if(home!=null)home.onClick.AddListener(ReturnHome);
            sound.onClick.AddListener(OpenAudioSettings);pauseSound.onClick.AddListener(OpenAudioSettings);
            audioMute.onClick.AddListener(ToggleSound);closeAudio.onClick.AddListener(CloseAudioSettings);
            for(int i=0;i<volumeSliders.Length;i++){int channel=i;volumeSliders[i].onValueChanged.AddListener(value=>{audioSystem.SetVolume((AudioChannel)channel,value);UpdateSound();});}
            openShop.onClick.AddListener(OpenShop);closeShop.onClick.AddListener(CloseShop);
            resultShop.onClick.AddListener(()=>{if(MultiplayerConnected){multiplayer.Open();return;}if(MultiplayerOpen)return;game.ReturnToMenu();OpenShop();});
            if(heroGesture!=null)heroGesture.onClick.AddListener(()=>{if(!ShopOpen&&!AudioOpen&&!MultiplayerOpen&&!MultiplayerConnected&&game.State==RunState.Menu)game.Animation.PlayMenuGesture();});
            for(int i=0;i<upgradeButtons.Length;i++){int index=i;upgradeButtons[i].onClick.AddListener(()=>BuyUpgrade(index));}
            for(int i=0;i<shopTabs.Length;i++){int index=i;shopTabs[i].onClick.AddListener(()=>SelectShopTab(index));}
            for(int i=0;i<permanentButtons.Length;i++){int index=i;permanentButtons[i].onClick.AddListener(()=>BuyPermanent(index));}
            for(int i=0;i<vehicleButtons.Length;i++){int index=i;vehicleButtons[i].onClick.AddListener(()=>BuyVehicle(index));}
            ApplySafeArea();UpdateSound();
        }
        public void AttachMultiplayer(MultiplayerUI value){multiplayer=value;RefreshMultiplayerModal();}
        public void RefreshMultiplayerModal(){SetMenuInput(!ShopOpen&&!AudioOpen&&!MultiplayerOpen);}
        void Start(){UpdateSound();bestMenu.text="EN İYİ  "+game.BestScore.ToString("N0");}
        void ToggleSound(){if(audioSystem==null)return;audioSystem.Toggle();UpdateSound();}
        void UpdateSound()
        {
            bool muted=audioSystem!=null&&audioSystem.Muted;
            int master=audioSystem==null?100:Mathf.RoundToInt(audioSystem.GetVolume(AudioChannel.Master)*100);
            soundLabel.text=muted?"KAPALI":"%"+master;
            pauseSoundLabel.text="SES AYARLARI  ·  "+(muted?"SESSİZ":"%"+master);
            if(audioMuteLabel!=null)audioMuteLabel.text=muted?"SESLERİ YENİDEN AÇ":"TÜM SESLERİ SUSTUR";
            if(audioSystem!=null&&volumeSliders!=null)for(int i=0;i<volumeSliders.Length;i++)
            {float value=audioSystem.GetVolume((AudioChannel)i);volumeSliders[i].SetValueWithoutNotify(value);volumeValues[i].text="%"+Mathf.RoundToInt(value*100);}
        }
        public void OpenAudioSettings()
        {
            if(audioSystem==null||AudioOpen||ShopOpen||MultiplayerOpen||(game.State!=RunState.Menu&&game.State!=RunState.Paused))return;
            UpdateSound();SetMenuInput(false);pauseVisibility.interactable=pauseVisibility.blocksRaycasts=false;audioPanel.SetActive(true);
        }
        public void CloseAudioSettings()
        {
            audioPanel.SetActive(false);if(audioSystem!=null)audioSystem.SaveSettings();
            SetMenuInput(!ShopOpen&&!MultiplayerOpen);pauseVisibility.interactable=pauseVisibility.blocksRaycasts=true;
        }
        public bool TryCloseOverlay()
        {
            if(multiplayer!=null&&multiplayer.TryCloseOverlay())return true;
            if(AudioOpen){CloseAudioSettings();return true;}
            if(ShopOpen){CloseShop();return true;}
            return false;
        }
        public void Show(RunState state)
        {
            if(shopPanel!=null)shopPanel.SetActive(false);
            if(audioPanel!=null)audioPanel.SetActive(false);if(audioSystem!=null)audioSystem.SaveSettings();
            if(pauseVisibility!=null)pauseVisibility.interactable=pauseVisibility.blocksRaycasts=true;
            SetMenuInput(!MultiplayerOpen);menuOpacity=1f;
            if(menuVisibility!=null)menuVisibility.alpha=1f;
            menu.SetActive(state==RunState.Menu);hud.SetActive(state==RunState.Running);
            pausePanel.SetActive(state==RunState.Paused);resultPanel.SetActive(state==RunState.Finished);
            if(state==RunState.Running)
            {shownSpeed=shownCoins=shownScore=shownPassed=-1;SetStats(game.CurrentSpeed,game.Obstacles.Passed,game.BestDistance);}
            if(state==RunState.Menu)bestMenu.text="EN İYİ  "+game.BestScore.ToString("N0");
            UpdateSound();
            RefreshShop();
        }
        public void OpenShop()
        {
            if(game.State!=RunState.Menu||ShopOpen||AudioOpen||MultiplayerOpen||MultiplayerConnected)return;
            shopMessage.text="Civcivlerini güçlere, kalıcı yeteneklere ve garajına yatır.";
            purchaseCooldown=0;RefreshShop();SetMenuInput(false);shopPanel.SetActive(true);SelectShopTab(0);
        }
        public void CloseShop(){shopPanel.SetActive(false);SetMenuInput(!AudioOpen&&!MultiplayerOpen);}
        public void ReturnHome()
        {
            if(game.State!=RunState.Paused||AudioOpen||MultiplayerOpen)return;
            if(MultiplayerConnected){multiplayer.Close();return;}
            game.ReturnToMenu();
        }
        public void SelectShopTab(int index)
        {
            if(index<0||index>=shopPages.Length)return;
            if(ShopTab!=index)shopMessage.text=index==0?"Topladığın güçlerin etkisini kalıcı olarak geliştir.":index==1?"Civciv kazanma yollarını kalıcı olarak geliştir.":"Seçtiğin araç, seni parkurda kovalayan araç olur.";
            ShopTab=index;
            for(int i=0;i<shopPages.Length;i++)
            {
                shopPages[i].SetActive(i==index);
                var label=shopTabs[i].GetComponentInChildren<Text>();label.color=i==index?gold:white;
            }
            Canvas.ForceUpdateCanvases();
            RefreshShop();
        }
        bool CanShop(int tab)=>ShopOpen&&ShopTab==tab&&game.State==RunState.Menu&&purchaseCooldown<=0&&!MultiplayerConnected;
        void PurchaseResponse(bool success,string message)
        {
            purchaseCooldown=.55f;shopMessage.text=message;
            if(success){audioSystem?.Powerup();game.Animation.CelebrateReward(true);}
            RefreshShop();
        }
        void BuyPermanent(int index)
        {
            if(!CanShop(1)||index<0||index>=permanentButtons.Length)return;
            var kind=(RunnerUpgrade)index;bool bought=game.TryPurchasePermanent(kind);
            PurchaseResponse(bought,bought?RunnerShopCatalog.Name(kind)+" GELİŞTİ":game.PermanentLevel(kind)>=3?"BU YETENEK TAM GELİŞTİRİLDİ":"Biraz daha civciv biriktir.");
            if(bought)permanentButtons[index].GetComponent<UIButtonMotion>().Confirm();
        }
        void BuyVehicle(int index)
        {
            if(!CanShop(2)||index<0||index>=vehicleButtons.Length)return;
            var kind=(ChaseVehicleKind)index;bool owned=game.OwnsVehicle(kind);
            bool bought=owned?game.TryEquipVehicle(kind):game.TryPurchaseVehicle(kind);
            PurchaseResponse(bought,bought?RunnerShopCatalog.VehicleName(kind)+" SEÇİLDİ":"Bu araç için biraz daha civciv biriktir.");
            if(bought)vehicleButtons[index].GetComponent<UIButtonMotion>().Confirm();
        }
        void SetMenuInput(bool enabled)
        {if(menuVisibility!=null){menuVisibility.interactable=enabled;menuVisibility.blocksRaycasts=enabled;}}
        void BuyUpgrade(int index)
        {
            if(!CanShop(0)||index<0||index>=upgradeButtons.Length)return;
            var kind=(PowerupKind)index;
            bool purchased=game.TryPurchaseUpgrade(kind);
            purchaseCooldown=.55f;
            if(purchased){if(audioSystem!=null)audioSystem.Powerup();game.Animation.CelebrateReward(true);}
            shopMessage.text=purchased?RunnerPowerups.LabelFor(kind)+(kind==PowerupKind.Encore?" GELİŞTİ · +0,5 SN EK SÜRE":" GELİŞTİ · +1 SN KALICI GÜÇ"):game.UpgradeLevel(kind)>=3?"BU GÜÇ TAM GELİŞTİRİLDİ":"Daha fazla civciv için bir tur daha koş.";
            RefreshShop();
            if(upgradeCards!=null&&index<upgradeCards.Length&&upgradeCards[index]!=null)upgradeCards[index].PurchaseFeedback(purchased);
        }
        void RefreshShop()
        {
            if(game==null||walletMenu==null)return;
            walletMenu.text=game.WalletBalance.ToString("N0")+" CİVCİV";walletShop.text=walletMenu.text;
            for(int i=0;i<upgradeButtons.Length;i++)
            {
                var kind=(PowerupKind)i;int level=game.UpgradeLevel(kind),price=game.UpgradeCost(kind);
                upgradeLevels[i].text="SEVİYE "+level+" / 3";
                upgradePrices[i].text=level>=3?"TAMAM":price+" CİVCİV\n<size=22>GELİŞTİR</size>";
                upgradeButtons[i].interactable=level<3&&game.WalletBalance>=price;
                if(upgradeCards!=null&&i<upgradeCards.Length&&upgradeCards[i]!=null)upgradeCards[i].SetState(level,kind==PowerupKind.Encore?game.EncoreExtension:game.PowerupDuration(kind),upgradeButtons[i].interactable,kind==PowerupKind.Encore?.5f:1f);
            }
            for(int i=0;i<permanentButtons.Length;i++)
            {
                var kind=(RunnerUpgrade)i;int level=game.PermanentLevel(kind),price=game.PermanentCost(kind);
                permanentLevels[i].text="SEVİYE "+level+" / 3  ·  "+RunnerShopCatalog.Value(kind,level);
                permanentPrices[i].text=level>=3?"TAMAM":price+" CİVCİV\n<size=22>GELİŞTİR</size>";
                permanentButtons[i].interactable=level<3&&game.WalletBalance>=price;
            }
            for(int i=0;i<vehicleButtons.Length;i++)
            {
                var kind=(ChaseVehicleKind)i;bool owned=game.OwnsVehicle(kind),selected=game.SelectedVehicle==kind;
                vehiclePrices[i].text=selected?"SEÇİLİ":owned?"ARACI SEÇ":game.VehicleCost(kind)+" CİVCİV\n<size=22>SATIN AL & SEÇ</size>";
                vehicleButtons[i].interactable=!selected&&(owned||game.WalletBalance>=game.VehicleCost(kind));
            }
        }
        public void SetDistance(double value)
        {
            int m=(int)value;if(m==shownDistance)return;shownDistance=m;
            distance.text=m.ToString("N0")+" <size=22>m</size>";
        }
        public void SetStats(float value,int passed,double best)
        {
            int kmh=Mathf.RoundToInt(value*3.6f);
            if(kmh!=shownSpeed){shownSpeed=kmh;speed.text=kmh+" <size=18>KM/SA</size>";}
            if(shownCoins!=game.Coins){if(shownCoins>=0&&game.Coins>shownCoins)coinPulse=.18f;shownCoins=game.Coins;coins.text=shownCoins.ToString();}
            if(shownScore!=game.Score){shownScore=game.Score;score.text=shownScore.ToString("N0")+(game.Powerups.ScoreMultiplier>1?" <size=18>×2</size>":"");}
            shownPassed=passed;
            chargeFill.fillAmount=game.FeverActive?game.FeverRemaining/Mathf.Max(1,game.FeverDuration):game.Charge/10f;
            chargeFill.color=game.FeverActive?white:gold;
            string next=game.FeverActive?"TRİBÜN ATEŞİ   ×"+game.CoinValue:game.CoinValue>1?"ALTIN DOKUNUŞ   ×"+game.CoinValue:"ATEŞİ YAK   "+game.Charge+" / 10";
            if(chargeLabel.text!=next)chargeLabel.text=next;
        }
        public void SetAtmosphere(string value){if(atmosphere.text!=value)atmosphere.text=value;}
        public void SetStage(string value){if(stage.text!=value)stage.text=value;}
        public void SetRunProgress(int streak,float multiplier,string challengeName,int current,int target,int rewardCoins)
        {
            streak=Mathf.Max(0,streak);current=Mathf.Max(0,current);target=Mathf.Max(1,target);rewardCoins=Mathf.Max(0,rewardCoins);
            challengeName=string.IsNullOrWhiteSpace(challengeName)?"YENİ HEDEF":challengeName.Trim();
            if(shownStreak!=streak||!Mathf.Approximately(shownMultiplier,multiplier))
            {
                if(shownStreak>=0&&streak>shownStreak)skillPulse=.35f;
                shownStreak=streak;shownMultiplier=multiplier;
                combo.text="SERİ "+Compact(streak)+"  ·  ×"+Mathf.Clamp(multiplier,1,2).ToString("0.#").Replace('.',',');
                combo.color=streak>0?gold:white;
            }
            bool changed=shownGoalName!=challengeName;
            bool previousGoalValid=shownGoalTarget>0&&shownGoalName!="YENİ HEDEF";
            if(game!=null&&game.State==RunState.Running&&previousGoalValid&&
                (changed||(!changed&&current>=target&&shownGoalCurrent<shownGoalTarget)))
            {completedGoalText="+"+Compact(Mathf.Max(0,shownGoalReward))+" JETON";goalCompleteClock=.85f;}
            if(changed||shownGoalCurrent!=current||shownGoalTarget!=target||shownGoalReward!=rewardCoins)
            {
                shownGoalName=challengeName;shownGoalCurrent=current;shownGoalTarget=target;shownGoalReward=rewardCoins;
                goalFillTarget=Mathf.Clamp01((float)current/target);
                if(goalCompleteClock<=0)RefreshGoalText();
            }
        }
        void RefreshGoalText()
        {
            challengeLabel.text=ShortGoal(shownGoalName);
            challengeProgress.text=Compact(Mathf.Min(shownGoalCurrent,shownGoalTarget))+" / "+Compact(shownGoalTarget)+"  ·  +"+Compact(shownGoalReward);
            challengeLabel.color=white;challengeProgress.color=gold;challengeFill.color=gold;
        }
        static string ShortGoal(string value)=>value.Length<=29?value:value.Substring(0,28)+"…";
        static string Compact(int value)
        {return value>=1000000?(value/1000000f).ToString("0.#").Replace('.',',')+" MN":value>=10000?(value/1000f).ToString("0.#").Replace('.',',')+" B":value.ToString("N0");}
        public void SetRunSummary(int bestStreak,int completedChallenges,int rewardCoins)
        {
            resultSkill.text="EN İYİ SERİ "+Compact(Mathf.Max(0,bestStreak))+"   ·   "+Compact(Mathf.Max(0,completedChallenges))+" HEDEF   ·   +"+Compact(Mathf.Max(0,rewardCoins))+" JETON";
        }
        public void Notify(string message)
        {toast.text=message;toastClock=1.3f;toast.color=gold;toast.rectTransform.localScale=Vector3.one*1.055f;}
        public void ClearFeedback()
        {
            toastClock=coinPulse=skillPulse=goalCompleteClock=powerRefreshClock=0;toast.text="";reminderClock=5.5f;swipeReminder.alpha=1;
            coins.rectTransform.localScale=toast.rectTransform.localScale=Vector3.one;
            combo.rectTransform.localScale=Vector3.one;shownStreak=shownGoalCurrent=shownGoalTarget=shownGoalReward=-1;shownMultiplier=-1;shownGoalName="";challengeFill.fillAmount=0;
            SetRunProgress(0,1,"YENİ HEDEF",0,1,0);SetRunSummary(0,0,0);
            recordAtStart=game==null?0:game.BestScore;
        }
        public void SetResult(double metres,int passed,double best,string hint)
        {
            resultTarget=game.Score;resultClock=0;lastResultCount=-1;resultScore.text="0";
            resultBest.text=game.Score>recordAtStart?"YENİ KİŞİSEL REKOR":"EN İYİ  "+game.BestScore.ToString("N0");
            resultBest.color=game.Score>recordAtStart?gold:white;
            resultDistance.text=((int)metres)+" m     ·     "+game.Coins+" jeton     ·     "+passed+" geçiş";
            resultTip.text=hint.TrimEnd('.')+".\nKaydır, ritmini bul ve yeniden dene.";
        }
        void Update()
        {
            if(previousArea!=Screen.safeArea||previousSize.x!=Screen.width||previousSize.y!=Screen.height)ApplySafeArea();
            float unscaled=Time.unscaledDeltaTime;
            purchaseCooldown=Mathf.Max(0,purchaseCooldown-unscaled);
            if(game!=null&&game.State==RunState.Menu&&menuVisibility!=null)
            {
                float target=ShopOpen||AudioOpen||MultiplayerOpen?0:1;
                menuOpacity=Mathf.MoveTowards(menuOpacity,target,unscaled*5f);
                if(Mathf.Abs(menuVisibility.alpha-menuOpacity)>.001f)menuVisibility.alpha=menuOpacity;
            }
            if(game!=null&&game.State==RunState.Finished&&resultPanel.activeInHierarchy)
            {
                resultClock=Mathf.Min(1,resultClock+unscaled/.85f);
                int value=Mathf.RoundToInt(resultTarget*(1-Mathf.Pow(1-resultClock,3)));
                if(value!=lastResultCount){lastResultCount=value;resultScore.text=value.ToString("N0");}
            }
            if(game==null||game.State!=RunState.Running)return;
            float dt=Time.unscaledDeltaTime;
            var powers=game.Powerups;
            powerRefreshClock-=dt;
            if(powers!=null&&powerRefreshClock<=0)
            {
                powerRefreshClock=.1f;powerPanel.SetActive(powers.HasActive||powers.ProtectionLabel.Length>0);
                powerStatus.text=powers.ActiveLabel+"  ·  "+Mathf.CeilToInt(powers.ActiveRemaining)+" SN";
                powerFill.fillAmount=powers.ActiveProgress;
                Color accent=PowerColor(powers.HasActive?powers.ActiveKind:PowerupKind.Shield);powerFill.color=accent;powerIcon.color=accent;
                powerIcon.kind=powers.HasActive?PowerIcon(powers.ActiveKind):IconKind.Shield;powerIcon.SetVerticesDirty();
                string others="";int extras=0;
                foreach(PowerupKind kind in powerKinds)
                    if(kind!=powers.ActiveKind&&powers.IsActive(kind)){if(extras<2)others+=(extras>0?"   ·   ":"")+RunnerPowerups.LabelFor(kind);extras++;}
                if(extras>2)others+="  +"+(extras-2);
                powerDetails.text=extras>0?others:"GÜÇ TOPLANDI · OTOMATİK ETKİN";
                protectionStatus.text=powers.ProtectionLabel.Length>0?powers.ProtectionLabel:"ÇARPIŞMA KOŞUYU BİTİRİR";
                protectionStatus.color=powers.ProtectionLabel.Length>0?new Color(.4f,1,.8f):new Color(1,.72f,.5f);
            }
            toastClock=Mathf.Max(0,toastClock-dt);Color c=gold;c.a=Mathf.Clamp01(toastClock*3);toast.color=c;
            toast.rectTransform.localScale=Vector3.Lerp(toast.rectTransform.localScale,Vector3.one,1-Mathf.Exp(-dt*14));
            coinPulse=Mathf.Max(0,coinPulse-dt);coins.rectTransform.localScale=Vector3.one*(1+coinPulse*.4f);
            reminderClock=Mathf.Max(0,reminderClock-dt);swipeReminder.alpha=Mathf.Clamp01(reminderClock);
            skillPulse=Mathf.Max(0,skillPulse-dt);combo.rectTransform.localScale=Vector3.one*(1+Mathf.Sin(skillPulse/.35f*Mathf.PI)*.045f);
            if(goalCompleteClock>0)
            {
                goalCompleteClock=Mathf.Max(0,goalCompleteClock-dt);
                if(goalCompleteClock>0)
                {challengeLabel.text="HEDEF TAMAM";challengeProgress.text=completedGoalText;challengeFill.fillAmount=1;challengeLabel.color=challengeProgress.color=challengeFill.color=new Color(.4f,1,.8f);}
                else RefreshGoalText();
            }
            else if(Mathf.Abs(challengeFill.fillAmount-goalFillTarget)>.001f)challengeFill.fillAmount=Mathf.MoveTowards(challengeFill.fillAmount,goalFillTarget,dt*3f);
        }
        public static Color PowerColor(PowerupKind kind)
        {
            if(kind==PowerupKind.Rhythm)return new Color(243f/255,166f/255,232f/255);
            if(kind==PowerupKind.GoldenTrail)return new Color(1,219f/255,112f/255);
            if(kind==PowerupKind.Encore)return new Color(175f/255,164f/255,1);
            switch(kind){case PowerupKind.ScoreBoost:return new Color(1,.82f,.2f);case PowerupKind.FeverRush:return new Color(1,.35f,.19f);case PowerupKind.AirStep:return new Color(.25f,.94f,1);case PowerupKind.LaneSweep:return new Color(.45f,1,.65f);case PowerupKind.Flight:return new Color(.47f,.87f,1);case PowerupKind.Magnet:return new Color(.86f,.51f,1);case PowerupKind.Shield:return new Color(.4f,1,.8f);case PowerupKind.DoubleCoins:return new Color(1,.85f,.25f);case PowerupKind.SlowMotion:return new Color(.35f,.75f,1);case PowerupKind.SuperJump:return new Color(1,.48f,.65f);case PowerupKind.CoinRain:return new Color(.7f,1,.35f);default:return new Color(1,.7f,.24f);}
        }
        public static IconKind PowerIcon(PowerupKind kind)
        {
            if(kind==PowerupKind.Rhythm)return IconKind.Rhythm;
            if(kind==PowerupKind.GoldenTrail)return IconKind.Trail;
            if(kind==PowerupKind.Encore)return IconKind.Encore;
            switch(kind){case PowerupKind.ScoreBoost:return IconKind.Star;case PowerupKind.FeverRush:return IconKind.Flame;case PowerupKind.AirStep:return IconKind.DoubleJump;case PowerupKind.LaneSweep:return IconKind.Sweep;case PowerupKind.Flight:return IconKind.Wing;case PowerupKind.Magnet:return IconKind.Magnet;case PowerupKind.Shield:return IconKind.Shield;case PowerupKind.DoubleCoins:return IconKind.Coins;case PowerupKind.SlowMotion:return IconKind.Clock;case PowerupKind.SuperJump:return IconKind.Jump;case PowerupKind.CoinRain:return IconKind.Rain;default:return IconKind.Bolt;}
        }
        void ApplySafeArea()
        {
            if(Screen.width<=0||Screen.height<=0)return;
            previousArea=Screen.safeArea;previousSize=new Vector2Int(Screen.width,Screen.height);
            safeArea.anchorMin=previousArea.min/new Vector2(Screen.width,Screen.height);
            safeArea.anchorMax=previousArea.max/new Vector2(Screen.width,Screen.height);safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;
        }
    }
}
