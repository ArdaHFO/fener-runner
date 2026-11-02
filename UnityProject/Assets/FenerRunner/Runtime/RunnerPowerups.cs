using System;
using UnityEngine;

namespace FenerRunner
{
    public enum PowerupKind { Dash, Flight, Magnet, Shield, DoubleCoins, SlowMotion, SuperJump, CoinRain, ScoreBoost, FeverRush, AirStep, LaneSweep, Rhythm, GoldenTrail, Encore }

    public sealed class RunnerPowerups : MonoBehaviour
    {
        [SerializeField] RunnerSettings settings;
        [SerializeField] GameObject[] pickupPrefabs;
        [SerializeField] GameObject flightVisualPrefab,shieldVisualPrefab;
        [SerializeField] GameObject[] abilityVisualPrefabs;
        public const int TypeCount=15;
        const int Capacity=TypeCount;
        const float LandingSeconds=1.25f,ShieldOverlapSeconds=.35f;
        public const float FocusSpeedFactor=.68f,FocusRecoverySeconds=1.15f;
        readonly float[] timers=new float[Capacity];
        readonly int[] activatedAt=new int[Capacity],spawnOrder=new int[Capacity];
        readonly int[] pickupContacts=new int[Capacity];
        readonly float[] pickupContactTimes=new float[Capacity];
        Vector3[] pickupStart;
        struct PickupCapsule {public Vector3 bottom,top;public float radius;}
        PickupCapsule moveStart;
        bool hasMoveStart;
        int nextPickupContact;
        const float MedallionRadius=.72f;
        public int PickupContactCount{get;private set;}
        public float PickupContactTimeAt(int index)=>index>=0&&index<PickupContactCount?pickupContactTimes[index]:1f;
        public PowerupKind PickupContactKindAt(int index)=>(PowerupKind)pickupContacts[index];
        Transform[] pickups;Renderer[][] pickupRenderers;
        bool[] collected,resolved;float[] baseHeights;
        GameObject flightVisual,shieldVisual;GameObject[] abilityVisuals;
        RunnerGame game;RunnerMotor motor;RunnerObstacle absorbedObstacle;
        float nextZ,clock,hitGrace,landingGrace,coinRainClock,trailCooldown,focusRecovery;
        float shieldAtStepStart,landingAtStepStart,overlapAtStepStart,protectionStep;
        RunnerObstacle overlapObstacleAtStepStart;
        bool shieldConsumedThisStep;
        int rhythmChain;
        bool skipSpoiledRhythmPass;
        public int RhythmChain=>rhythmChain;
        public int TrailVolleyCount{get;private set;}
        public int LastExtendedCount{get;private set;}
        int activationSequence,spawnSequence;
        bool wasFlying,enabledPowers=true;
#if UNITY_EDITOR
        bool isolatedPickups;
#endif
        public event Action<PowerupKind> Collected;
        public event Action<int> CoinRainVolley;
        public event Action FeverRushTriggered;
        public float DashRemaining=>timers[0];
        public float FlightRemaining=>timers[1];
        public float MagnetRemaining=>timers[2];
        public float ShieldRemaining=>timers[3];
        public float DoubleCoinsRemaining=>timers[4];
        public float SlowMotionRemaining=>timers[5];
        public float SuperJumpRemaining=>timers[6];
        public float CoinRainRemaining=>timers[7];
        public float ScoreBoostRemaining=>timers[8];
        public float FeverRushRemaining=>timers[9];
        public float AirStepRemaining=>timers[10];
        public float LaneSweepRemaining=>timers[11];
        public float RhythmRemaining=>timers[12];
        public float GoldenTrailRemaining=>timers[13];
        public float LandingGraceRemaining=>landingGrace;
        public float HitGraceRemaining=>hitGrace;
        public bool ShieldActive=>ShieldRemaining>0;
        public bool FlightActive=>FlightRemaining>0;
        public bool MagnetActive=>MagnetRemaining>0;
        public bool AirStepActive=>AirStepRemaining>0;
        public bool LaneSweepActive=>LaneSweepRemaining>0;
        public bool FlockComboActive=>MagnetRemaining>0&&DoubleCoinsRemaining>0;
        public int FlockComboVolleys{get;private set;}
        public int ScoreMultiplier=>ScoreBoostRemaining>0?2:1;
        public bool SlowMotionRecovering=>SlowMotionRemaining<=0&&focusRecovery>0;
        public float SpeedRecoveryRemaining=>SlowMotionRecovering?focusRecovery:0;
        public float SpeedFactor
        {
            get
            {
                if(SlowMotionRemaining>0)return FocusSpeedFactor;
                if(focusRecovery<=0)return 1f;
                float t=Mathf.Clamp01(1f-focusRecovery/FocusRecoverySeconds);
                return Mathf.Lerp(FocusSpeedFactor,1f,t*t*(3f-2f*t));
            }
        }
        public float JumpMultiplier=>SuperJumpRemaining>0?1.5f:1f;
        public int CoinMultiplier=>DoubleCoinsRemaining>0?2:1;
        bool DescendingOrJustLanded=>FlightRemaining<=0&&motor!=null&&(motor.Flying||wasFlying);
        // General protection is restricted to a visible, controlled flight exit.
        // Shield overlap grace is obstacle-specific and is checked only by TryAbsorbHit(hit).
        public bool IsProtected=>DescendingOrJustLanded||landingGrace>0;
        public string ProtectionLabel=>ShieldActive?"KALKAN":IsProtected?"GÜVENLİ İNİŞ":hitGrace>0?"KALKAN KIRILDI":"";
        public float ProtectionRemaining=>ShieldActive?ShieldRemaining:DescendingOrJustLanded?LandingSeconds:landingGrace>0?landingGrace:hitGrace;
        public float ProtectionProgress=>ShieldActive?Progress(PowerupKind.Shield):IsProtected?Mathf.Clamp01(ProtectionRemaining/LandingSeconds):Mathf.Clamp01(hitGrace/ShieldOverlapSeconds);
        public int PoolCount=>pickups==null?0:pickups.Length;
        int ActiveIndex
        {
            get{int chosen=-1,stamp=-1;for(int i=0;i<Capacity;i++)if(timers[i]>0&&activatedAt[i]>stamp){chosen=i;stamp=activatedAt[i];}return chosen;}
        }
        public bool HasActive=>ActiveIndex>=0;
        public bool HasActivePower=>HasActive;
        public int ActiveCount{get{int n=0;for(int i=0;i<Capacity;i++)if(timers[i]>0)n++;return n;}}
        public PowerupKind ActiveKind{get{int i=ActiveIndex;return i>=0?(PowerupKind)i:PowerupKind.Dash;}}
        public string ActiveLabel=>HasActive?LabelFor(ActiveKind):ProtectionLabel;
        public float ActiveRemaining=>HasActive?Remaining(ActiveKind):ProtectionRemaining;
        public float ActiveProgress=>HasActive?Progress(ActiveKind):ProtectionProgress;
        public bool IsActive(PowerupKind kind)=>Remaining(kind)>0;
        public float Remaining(PowerupKind kind){int i=(int)kind;return i>=0&&i<Capacity?timers[i]:0;}
        public float Progress(PowerupKind kind)=>game==null?0:Mathf.Clamp01(Remaining(kind)/Mathf.Max(.1f,game.PowerupDuration(kind)));
        public static string LabelFor(PowerupKind kind)
        {
            switch(kind)
            {
                case PowerupKind.Dash:return "DEPAR";
                case PowerupKind.Flight:return "KANAT AÇ";
                case PowerupKind.Magnet:return "MIKNATIS";
                case PowerupKind.Shield:return "KALKAN";
                case PowerupKind.DoubleCoins:return "ÇİFT JETON";
                case PowerupKind.SlowMotion:return "ODAK";
                case PowerupKind.SuperJump:return "SÜPER SIÇRAYIŞ";
                case PowerupKind.CoinRain:return "JETON YAĞMURU";
                case PowerupKind.ScoreBoost:return "SKOR ÇARPANI";
                case PowerupKind.FeverRush:return "TRİBÜN PATLAMASI";
                case PowerupKind.AirStep:return "HAVA ADIMI";
                case PowerupKind.LaneSweep:return "ŞERİT SÜPÜRGESİ";
                case PowerupKind.Rhythm:return "RİTİM USTASI";
                case PowerupKind.GoldenTrail:return "JETON PASI";
                case PowerupKind.Encore:return "UZATMA";
                default:return "";
            }
        }
        public bool Enabled
        {
            get=>enabledPowers;
            set
            {
                enabledPowers=value;
                if(!value){ClearPowers();if(pickups!=null)for(int i=0;i<pickups.Length;i++)SetVisible(i,false);}
                else if(pickups!=null)for(int i=0;i<pickups.Length;i++)SetVisible(i,!collected[i]);
            }
        }
        public void Configure(RunnerSettings value,GameObject[] prefabs,GameObject flightPrefab,GameObject shieldPrefab,GameObject[] extraVisuals=null)
        {settings=value;pickupPrefabs=prefabs;flightVisualPrefab=flightPrefab;shieldVisualPrefab=shieldPrefab;abilityVisualPrefabs=extraVisuals;}
        public void Initialize(RunnerGame owner)
        {
            game=owner;motor=owner.Motor;if(pickups!=null)return;
            if(pickupPrefabs==null||pickupPrefabs.Length!=Capacity)throw new InvalidOperationException(Capacity+" powerup prefabs are required");
            if(abilityVisualPrefabs==null||abilityVisualPrefabs.Length!=Capacity-4)throw new InvalidOperationException((Capacity-4)+" additional power effect prefabs are required");
            pickups=new Transform[Capacity];pickupRenderers=new Renderer[Capacity][];
            collected=new bool[Capacity];resolved=new bool[Capacity];baseHeights=new float[Capacity];pickupStart=new Vector3[Capacity];
            for(int i=0;i<Capacity;i++)
            {pickups[i]=Instantiate(pickupPrefabs[i],transform).transform;pickupRenderers[i]=pickups[i].GetComponentsInChildren<Renderer>();}
            flightVisual=Instantiate(flightVisualPrefab,motor.transform);flightVisual.name="Flight wings";
            shieldVisual=Instantiate(shieldVisualPrefab,motor.transform);shieldVisual.name="Explicit protection halo";
            abilityVisuals=new GameObject[Capacity-4];
            for(int i=0;i<abilityVisuals.Length;i++){abilityVisuals[i]=Instantiate(abilityVisualPrefabs[i],motor.transform);abilityVisuals[i].SetActive(false);}
            flightVisual.SetActive(false);shieldVisual.SetActive(false);
        }
        public void ResetCourse()
        {
            ClearPowers();clock=0;nextZ=72;spawnSequence=0;
#if UNITY_EDITOR
            isolatedPickups=false;
#endif
            if(pickups==null)return;
            for(int i=0;i<Capacity;i++)spawnOrder[i]=i;
            var random=new System.Random(game.Obstacles.Seed^17431);
            for(int i=Capacity-1;i>0;i--){int j=random.Next(i+1);int t=spawnOrder[i];spawnOrder[i]=spawnOrder[j];spawnOrder[j]=t;}
            for(int i=0;i<Capacity;i++)Place(spawnOrder[i]);
        }
        void ClearPowers()
        {
            for(int i=0;i<Capacity;i++){timers[i]=0;activatedAt[i]=0;}
            hitGrace=landingGrace=coinRainClock=trailCooldown=focusRecovery=0;activationSequence=rhythmChain=TrailVolleyCount=LastExtendedCount=FlockComboVolleys=0;absorbedObstacle=null;skipSpoiledRhythmPass=false;
            hasMoveStart=false;PickupContactCount=nextPickupContact=0;
            shieldAtStepStart=landingAtStepStart=overlapAtStepStart=protectionStep=0;
            overlapObstacleAtStepStart=null;shieldConsumedThisStep=false;
            wasFlying=false;if(motor!=null)motor.SetFlight(false);
            if(flightVisual!=null)flightVisual.SetActive(false);if(shieldVisual!=null)shieldVisual.SetActive(false);
            if(abilityVisuals!=null)foreach(var effect in abilityVisuals)effect.SetActive(false);
        }
        void Place(int i)
        {
            pickups[i].position=new Vector3(0,1.3f,nextZ);baseHeights[i]=1.3f;collected[i]=false;resolved[i]=false;
            pickupStart[i]=pickups[i].position;
            nextZ+=105+(spawnSequence++%3)*15;SetVisible(i,enabledPowers);
        }
        void SetVisible(int i,bool visible){foreach(var r in pickupRenderers[i])r.enabled=visible;}
        public void TickTimers(float dt)
        {
            if(!enabledPowers||motor==null||dt<=0)return;
            if(wasFlying&&!motor.Flying)landingGrace=LandingSeconds;
            // Keep protection valid at its actual contact instant even if its
            // timer ends partway through this motor step. The public counters
            // still describe the end of the step for HUD and ordinary callers.
            shieldAtStepStart=ShieldRemaining;landingAtStepStart=landingGrace;
            overlapAtStepStart=hitGrace;overlapObstacleAtStepStart=absorbedObstacle;
            protectionStep=dt;shieldConsumedThisStep=false;
            float rainBefore=CoinRainRemaining,focusBefore=SlowMotionRemaining;
            for(int i=0;i<Capacity;i++)timers[i]=Mathf.Max(0,timers[i]-dt);
            // Advance only the part of this step after the exact focus expiry.
            // Smoothstep starts/ends with zero slope and cannot overshoot.
            if(focusBefore>0&&SlowMotionRemaining<=0)
                focusRecovery=Mathf.Max(0,FocusRecoverySeconds-Mathf.Max(0,dt-focusBefore));
            else if(focusBefore<=0)focusRecovery=Mathf.Max(0,focusRecovery-dt);
            trailCooldown=Mathf.Max(0,trailCooldown-dt);if(RhythmRemaining<=0)rhythmChain=0;
            landingGrace=Mathf.Max(0,landingGrace-dt);hitGrace=Mathf.Max(0,hitGrace-dt);
            if(hitGrace<=0)absorbedObstacle=null;
            if(rainBefore>0)
            {
                coinRainClock-=Mathf.Min(dt,rainBefore);
                while(coinRainClock<=.0001f){CoinRainVolley?.Invoke(5+game.PermanentLevel(RunnerUpgrade.RainVolley));coinRainClock+=1f;}
            }
            motor.SetFlight(FlightRemaining>0);wasFlying=motor.Flying;
            flightVisual.SetActive(motor.Flying);shieldVisual.SetActive(ShieldActive||IsProtected||hitGrace>0);
            if(flightVisual.activeSelf)
            {flightVisual.transform.localPosition=new Vector3(0,1.15f,-.23f);flightVisual.transform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(clock*5)*3);}
            if(shieldVisual.activeSelf)
            {shieldVisual.transform.localPosition=Vector3.up*1.05f;shieldVisual.transform.localRotation=Quaternion.Euler(0,clock*65,0);}
            for(int i=0;i<abilityVisuals.Length;i++)
            {
                GameObject effect=abilityVisuals[i];effect.SetActive(timers[i+4]>0);if(!effect.activeSelf)continue;
                effect.transform.localPosition=Vector3.zero;
                effect.transform.localRotation=i==0||i==4?Quaternion.Euler(0,clock*65,0):Quaternion.identity;
                float pulse=1+Mathf.Sin(clock*(i==1?2.1f:4f))*.045f;
                effect.transform.localScale=i==2?new Vector3(1,motor.Grounded?pulse:1.4f,1):Vector3.one*pulse;
            }
        }
        static PickupCapsule ReadPickupCapsule(RunnerMotor runner)
        {
            var capsule=runner.Capsule;Vector3 scale=runner.transform.lossyScale;
            float radius=capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
            float height=Mathf.Max(radius*2,capsule.height*Mathf.Abs(scale.y));
            Vector3 centre=runner.transform.TransformPoint(capsule.center),offset=runner.transform.up*(height*.5f-radius);
            return new PickupCapsule{bottom=centre-offset,top=centre+offset,radius=radius};
        }
        public void BeginMove()
        {
            hasMoveStart=enabledPowers&&motor!=null&&motor.Capsule.enabled&&pickups!=null;
            PickupContactCount=nextPickupContact=0;if(!hasMoveStart)return;
            moveStart=ReadPickupCapsule(motor);
            for(int i=0;i<Capacity;i++)pickupStart[i]=pickups[i].position;
        }
        static bool ClipPositive(float a,float b,ref float low,ref float high)
        {
            float d=b-a;if(Mathf.Abs(d)<.000001f)return a>=0;
            float t=-a/d;if(d>0)low=Mathf.Max(low,t);else high=Mathf.Min(high,t);return low<=high;
        }
        static bool SphereEntry(Vector3 a,Vector3 b,float radius,float low,float high,out float entry)
        {
            entry=1f;if(low>high)return false;Vector3 d=b-a;float square=radius*radius;
            if((a+d*low).sqrMagnitude<=square){entry=low;return true;}
            float aa=d.sqrMagnitude;if(aa<.0000001f)return false;
            float bb=Vector3.Dot(a,d),cc=a.sqrMagnitude-square,disc=bb*bb-aa*cc;
            if(disc<0)return false;float t=(-bb-Mathf.Sqrt(disc))/aa;
            if(t<low||t>high)return false;entry=t;return true;
        }
        static bool PickupEntry(Vector3 from,Vector3 to,PickupCapsule before,PickupCapsule after,out float entry)
        {
            float radius=Mathf.Max(before.radius,after.radius)+MedallionRadius;entry=1f;bool hit=false;
            Vector3 lower0=from-before.bottom,lower1=to-after.bottom,upper0=from-before.top,upper1=to-after.top;
            if(SphereEntry(lower0,lower1,radius,0,1,out float t)){hit=true;entry=t;}
            if(SphereEntry(upper0,upper1,radius,0,1,out t)){hit=true;entry=Mathf.Min(entry,t);}
            float low=0,high=1;
            if(ClipPositive(lower0.y,lower1.y,ref low,ref high)&&ClipPositive(-upper0.y,-upper1.y,ref low,ref high))
            {
                lower0.y=lower1.y=0;
                if(SphereEntry(lower0,lower1,radius,low,high,out t)){hit=true;entry=Mathf.Min(entry,t);}
            }
            return hit;
        }
        // Gather actual capsule/medallion contacts without activating effects.
        // Game merges these entry times with obstacle contacts before resolving death.
        public void PreparePickups(float dt)
        {
            PickupContactCount=nextPickupContact=0;
            if(!enabledPowers||pickups==null||motor==null||!motor.Capsule.enabled)return;
            PickupCapsule end=ReadPickupCapsule(motor),start=hasMoveStart?moveStart:end;
            clock+=dt;Vector3 p=motor.transform.position;
            for(int i=0;i<pickups.Length;i++)
            {
                Transform item=pickups[i];Vector3 pos=item.position;bool relocated=false;
                if(!resolved[i]&&pos.z-p.z<180)
                {
                    if(game.Obstacles.TryGetNextRoute(pos.z,out CourseRoute route))
                    {pos.z=route.Z-18f;pos.x=route.Lane*settings.laneSpacing;item.position=pos;relocated=true;}
                    resolved[i]=true;
                }
                pos=item.position;pos.y=baseHeights[i]+Mathf.Sin(clock*2.7f+i)*.16f;item.position=pos;
                item.localRotation=Quaternion.Euler(0,Mathf.Sin(clock*1.4f+i)*18f,0);
                Vector3 from=hasMoveStart&&!relocated?pickupStart[i]:pos;
                if(!collected[i]&&PickupEntry(from,pos,start,end,out float time))
                {
                    int at=PickupContactCount++;
                    while(at>0&&pickupContactTimes[at-1]>time)
                    {pickupContacts[at]=pickupContacts[at-1];pickupContactTimes[at]=pickupContactTimes[at-1];at--;}
                    pickupContacts[at]=i;pickupContactTimes[at]=time;
                }
            }
        }
        public void CollectPickupsThrough(float time)
        {
            if(!enabledPowers)return;
            while(nextPickupContact<PickupContactCount&&pickupContactTimes[nextPickupContact]<=time)
            {
                int i=pickupContacts[nextPickupContact++];if(collected[i])continue;
                collected[i]=true;SetVisible(i,false);ActivateCore((PowerupKind)i,true);
            }
        }
        public void CompletePickups()
        {
            hasMoveStart=false;if(!enabledPowers||pickups==null)return;
            float z=motor.transform.position.z;
            for(int i=0;i<Capacity;i++)
            {
                Vector3 pos=pickups[i].position;
                if(pos.z<z-14)
                {
#if UNITY_EDITOR
                    if(!isolatedPickups)
#endif
                    Place(i);
                }
            }
        }
        public void TickPickups(float dt)
        {PreparePickups(dt);CollectPickupsThrough(1f);CompletePickups();}
        public void Activate(PowerupKind kind) {ActivateCore(kind,false);}
        private void ActivateCore(PowerupKind kind,bool fromMovement)
        {
            int index=(int)kind;if(!enabledPowers||index<0||index>=Capacity)return;
            bool alreadyActive=timers[index]>0;
            // Refresh never takes away seconds granted by Encore. Existing
            // rhythm progress and the next rain beat belong to the live effect.
            timers[index]=Mathf.Max(timers[index],game.PowerupDuration(kind));activatedAt[index]=++activationSequence;
            if(kind==PowerupKind.SlowMotion)focusRecovery=0;
            // A swept pickup occurs after the obstacle pose was sampled.
            // Defer only its flight/body transition to the next timer/motor
            // step, keeping the obstacle and coin capsules identical now.
            // Direct Activate remains immediate for existing callers.
            if(kind==PowerupKind.Flight&&!fromMovement){motor.SetFlight(true);wasFlying=true;}
            if(kind==PowerupKind.CoinRain&&!alreadyActive)coinRainClock=1f;
            if(kind==PowerupKind.FeverRush)FeverRushTriggered?.Invoke();
            if(kind==PowerupKind.Rhythm&&!alreadyActive)rhythmChain=0;
            if(kind==PowerupKind.Encore)
            {
                LastExtendedCount=0;float extension=game.EncoreExtension;
                for(int i=0;i<Capacity-1;i++)if(timers[i]>0)
                {timers[i]=Mathf.Min(game.PowerupDuration((PowerupKind)i)+extension,timers[i]+extension);LastExtendedCount++;}
                if(FeverRushRemaining>0)game.ExtendFeverRushTo(FeverRushRemaining);
                // A standalone Encore is still useful and explicit: one short magnet.
                if(LastExtendedCount==0){timers[(int)PowerupKind.Magnet]=3f+game.UpgradeLevel(PowerupKind.Encore)*.5f;activatedAt[(int)PowerupKind.Magnet]=activationSequence-1;}
            }
            Collected?.Invoke(kind);
        }
        void LateUpdate()
        {
            // Timers stop on collision, but effect objects sit on the physics
            // root. Hide them while the separate visual model performs its fall.
            if(game==null||game.State!=RunState.Finished)return;
            if(flightVisual!=null)flightVisual.SetActive(false);
            if(shieldVisual!=null)shieldVisual.SetActive(false);
            if(abilityVisuals!=null)foreach(var effect in abilityVisuals)effect.SetActive(false);
        }
        public int RewardCleanPasses(int count)
        {
            if(count>0&&skipSpoiledRhythmPass){count--;skipSpoiledRhythmPass=false;}
            if(count<=0)return 0;
            if(FlockComboActive){FlockComboVolleys++;CoinRainVolley?.Invoke(Mathf.Min(count,3)*3);}
            if(RhythmRemaining<=0)return 0;
            int unit=50+game.PermanentLevel(RunnerUpgrade.RhythmBonus)*15;
            int reward=0;for(int i=0;i<count;i++){rhythmChain=Mathf.Min(4,rhythmChain+1);reward+=rhythmChain*unit;}
            return reward;
        }
        public void SuccessfulLaneChange()
        {
            if(GoldenTrailRemaining<=0||trailCooldown>0||game.State!=RunState.Running)return;
            trailCooldown=.8f;TrailVolleyCount++;CoinRainVolley?.Invoke(4+game.PermanentLevel(RunnerUpgrade.TrailVolley));
        }
        public bool TryAbsorbHit(RunnerObstacle hit=null) {return TryAbsorbHitAt(hit,1f);}
        public bool TryAbsorbHitAt(RunnerObstacle hit,float stepFraction)
        {
            float elapsed=protectionStep*Mathf.Clamp01(stepFraction);
            bool landingProtected=IsProtected||landingAtStepStart>elapsed;
            if(landingProtected){rhythmChain=0;skipSpoiledRhythmPass=true;return true;}
            if((hitGrace>0&&hit==absorbedObstacle)||
                (overlapAtStepStart>elapsed&&hit==overlapObstacleAtStepStart))return true;
            bool historicalShield=!shieldConsumedThisStep&&shieldAtStepStart>elapsed;
            if(!ShieldActive&&!historicalShield)return false;
            // A newly collected shield can still protect the next contact;
            // once spent, neither it nor the historical shield can resurrect.
            shieldConsumedThisStep=true;
            timers[(int)PowerupKind.Shield]=0;absorbedObstacle=hit;hitGrace=ShieldOverlapSeconds;rhythmChain=0;skipSpoiledRhythmPass=true;
            game.Interface.Notify("KALKAN KIRILDI · YENİ ENGELDEN KAÇ");return true;
        }
        public void ShiftOrigin(float amount)
        {
            Vector3 shift=Vector3.forward*amount;nextZ-=amount;
            if(hasMoveStart){moveStart.bottom-=shift;moveStart.top-=shift;}
            if(pickups!=null)for(int i=0;i<Capacity;i++){pickups[i].position-=shift;pickupStart[i]-=shift;}
        }
#if UNITY_EDITOR
        public void IsolatePickupsForTest()
        {isolatedPickups=true;for(int i=0;i<pickups.Length;i++){collected[i]=true;SetVisible(i,false);}}
        public void PlaceForTest(PowerupKind kind,Vector3 position)
        {int i=(int)kind;pickups[i].position=pickupStart[i]=position;baseHeights[i]=position.y;collected[i]=false;resolved[i]=true;SetVisible(i,true);}
        public Vector3 PositionForTest(PowerupKind kind)=>pickups[(int)kind].position;
#endif
    }
}
