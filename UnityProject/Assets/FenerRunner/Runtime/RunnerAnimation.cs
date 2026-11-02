using UnityEngine;

namespace FenerRunner
{
    public enum RunStyle { None, CompactDrive, OpenArms, ShoulderGlance }

    public sealed class RunnerAnimation : MonoBehaviour
    {
        [SerializeField] private RunnerSettings settings;
        [SerializeField] private Animator animator;
        [SerializeField] private RunnerMotor motor;
        [SerializeField] private AnimationClip[] clips;
        private string current;
        private float nextTurn, turnRemaining, quietSeconds, menuClock, runRate, sprintLean, lastRunPhase;
        private Transform spine, chest, neck, head, leftFoot, rightFoot;
        private Transform rewardUpper,rewardFore,rewardHand;
        private Transform leftUpper,leftFore,leftHand;
        private Vector3 modelRestPosition;
        private bool deathActive,deathPaused;
        private float deathElapsed,deathFallHeight;
        private float variantCountdown,variantElapsed,variantDuration,variantBlend;
        private int nextVariant;
        private RunStyle runStyle;
        public const float DeathDuration=1.05f;
        private bool menuMode,pendingMajor;
        private int menuGestureIndex;
        private float menuGestureRemaining,menuDebounce,rewardRemaining,rewardDuration,rewardCooldown,pendingReward;
        private static readonly string[] MenuGestures={"MenuWave","MenuPride","MenuReady"};
        private bool fastFallPose, flightPose;
        private bool wasAirborne;
        private float landingRemaining,slidePosePhase,slideEntryElapsed,slideChainRemaining;
        private int lastSlideCount,lastSlideCycle;
        public float LandingReactionWeight {get;private set;}
        public float JumpBalanceWeight {get;private set;}
        public float SlidePosePhase=>slidePosePhase;
        public float SlideChainReactionWeight {get;private set;}
        public int LandingReactionCount {get;private set;}
        public bool TurnAllowed {get;set;} = true;
        private static readonly int MotionRate = Animator.StringToHash("MotionRate");
        public bool Turning => turnRemaining > 0f;
        public string CurrentClip => current;
        public float TurnCountdown => nextTurn;
        public Animator Animator => animator;
        public float RunCadence => runRate;
        public float SprintLeanDegrees => sprintLean;
        public float AppliedSprintLeanDegrees {get;private set;}
        public float RewardGestureWeight {get;private set;}
        public int RewardGestureCount {get;private set;}
        public int MenuGestureCount {get;private set;}
        public float MenuGestureRemaining=>menuGestureRemaining;
        public bool DeathPlaying=>deathActive&&deathElapsed<DeathDuration;
        public bool DeathComplete=>deathActive&&deathElapsed>=DeathDuration;
        public bool DeathPresentationPaused=>deathActive&&deathPaused;
        public float DeathProgress=>deathActive?Mathf.Clamp01(deathElapsed/DeathDuration):0;
        public Vector3 DeathFocusPosition=>head!=null&&spine!=null?(head.position+spine.position)*.5f:transform.position+Vector3.up;
        public int DeathCount {get;private set;}
        public RunStyle CurrentRunStyle=>runStyle;
        public float RunStyleWeight=>variantBlend;
        public float RunStyleCountdown=>variantCountdown;
        public int RunStyleCount {get;private set;}

        public void Configure(RunnerSettings value, RunnerMotor movement, Animator model, AnimationClip[] motions)
        { settings = value; motor = movement; animator = model; clips = motions; }

        public void Initialize()
        {
            motor.Jumped -= OnJump;
            motor.Jumped += OnJump;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            modelRestPosition=animator.transform.localPosition;
            foreach(var bone in animator.GetComponentsInChildren<Transform>())
            {
                if(bone.name=="spine")spine=bone;else if(bone.name=="chest")chest=bone;
                else if(bone.name=="neck")neck=bone;else if(bone.name=="foot.L")leftFoot=bone;
                else if(bone.name=="head")head=bone;
                else if(bone.name=="foot.R")rightFoot=bone;
                else if(bone.name=="upper_arm.R")rewardUpper=bone;
                else if(bone.name=="forearm.R")rewardFore=bone;
                else if(bone.name=="hand.R")rewardHand=bone;
                else if(bone.name=="upper_arm.L")leftUpper=bone;
                else if(bone.name=="forearm.L")leftFore=bone;
                else if(bone.name=="hand.L")leftHand=bone;
            }
        }

        public void ResetRun(bool running)
        {
            turnRemaining = 0f;
            quietSeconds = 0f; menuClock=0f;
            fastFallPose = flightPose = false;
            wasAirborne=false;landingRemaining=LandingReactionWeight=JumpBalanceWeight=slidePosePhase=slideEntryElapsed=0;lastSlideCount=motor.SlideCount;
            slideChainRemaining=SlideChainReactionWeight=0;lastSlideCycle=motor.SlideCycleCount;
            sprintLean=AppliedSprintLeanDegrees=lastRunPhase=0;
            menuMode=!running;menuGestureIndex=0;menuGestureRemaining=menuDebounce=0;
            rewardRemaining=rewardCooldown=pendingReward=RewardGestureWeight=0;
            deathActive=deathPaused=false;deathElapsed=deathFallHeight=0;animator.transform.localPosition=modelRestPosition;
            runStyle=RunStyle.None;variantBlend=variantElapsed=variantDuration=0;nextVariant=0;ScheduleVariant();
            runRate = TargetRunRate();
            ScheduleTurn();
            animator.speed = 1f;
            animator.updateMode=running?AnimatorUpdateMode.Normal:AnimatorUpdateMode.UnscaledTime;
            animator.Rebind();
            current = null;
            Play(running ? "Run" : "Idle", Rate(running ? "Run" : "Idle"), true);
            animator.Update(0f);
        }

        public void SetPaused(bool paused)
        {if(deathActive)deathPaused=paused;animator.speed=paused||DeathComplete?0f:1f;}

        public bool BeginDeath(ObstacleKind kind)
        {
            if(deathActive)return false;
            if(!HasClip("CollisionFall"))throw new System.InvalidOperationException("CollisionFall must be generated before death playback.");
            deathActive=true;deathPaused=false;deathElapsed=0;deathFallHeight=Mathf.Max(0,motor.transform.position.y-.02f);DeathCount++;
            menuMode=false;menuGestureRemaining=menuDebounce=0;turnRemaining=0;
            rewardRemaining=rewardCooldown=pendingReward=RewardGestureWeight=0;
            runStyle=RunStyle.None;variantBlend=0;fastFallPose=flightPose=false;
            slideChainRemaining=SlideChainReactionWeight=0;
            landingRemaining=LandingReactionWeight=JumpBalanceWeight=0;
            animator.updateMode=AnimatorUpdateMode.UnscaledTime;animator.speed=1;
            float offset=motor.Sliding?.55f:!motor.Grounded?.20f:0;
            Play("CollisionFall",Length("CollisionFall")*(1-offset)/DeathDuration,false,offset);
            return true;
        }
        private void Update()
        {
            if(!deathActive||deathPaused||DeathComplete)return;
            deathElapsed=Mathf.Min(DeathDuration,deathElapsed+Time.unscaledDeltaTime);
            if(DeathComplete)
            {animator.Play(Animator.StringToHash("Base Layer.CollisionFall"),0,1);animator.Update(0);animator.speed=0;}
        }

        public void PlayerCommand()
        {
            quietSeconds = 0f;
            if (!Turning) return;
            turnRemaining = 0f;
            ScheduleTurn();
            Play("Run", Rate("Run"));
        }

        public bool TryTurn()
        {
            if (deathActive || !motor.Grounded || motor.Flying || motor.Sliding || motor.ChangingLane || Turning) return false;
            turnRemaining = Length("RunTurn");
            Play("RunTurn", 1f);
            return true;
        }

        public void Tick(float dt)
        {
            if(deathActive||dt<=0||animator.speed<=0)return;
            slideChainRemaining=motor.Sliding?Mathf.Max(0,slideChainRemaining-dt):0;
            menuMode=false;
            if(wasAirborne&&motor.Grounded&&!motor.Sliding){landingRemaining=.26f;LandingReactionCount++;}
            wasAirborne=!motor.Grounded;
            landingRemaining=Mathf.Max(0,landingRemaining-dt);
            TickVariants(dt);
            rewardCooldown=Mathf.Max(0,rewardCooldown-dt);rewardRemaining=Mathf.Max(0,rewardRemaining-dt);
            if(pendingReward>0)
            {
                pendingReward=Mathf.Max(0,pendingReward-dt);
                if(CanReact&&rewardCooldown<=0){BeginReward(pendingMajor);pendingReward=0;}
            }
            quietSeconds += dt;
            nextTurn -= dt;
            runRate = Mathf.Lerp(runRate, TargetRunRate(), 1f - Mathf.Exp(-dt * 8f));
            sprintLean=Mathf.Lerp(sprintLean,Mathf.Clamp((motor.Speed-settings.forwardSpeed)*.35f,-2f,4.2f),1f-Mathf.Exp(-dt*6f));
            if(current=="Run")
            {
                var state=animator.GetCurrentAnimatorStateInfo(0);
                if(state.IsName("Run"))lastRunPhase=Mathf.Repeat(state.normalizedTime,1);
            }
            if(motor.Flying)
            {
                if(!flightPose)
                {
                    flightPose=true;fastFallPose=false;turnRemaining=0;
                    Play("Flight",1f);
                }
                else animator.SetFloat(MotionRate,motor.FlightRequested?1f:.75f);
                return;
            }
            if(flightPose)
            {
                flightPose=false;
                if(!motor.Grounded)Play("Jump",0f,false,.65f);
            }
            if (!motor.Grounded)
            {
                if (motor.FastFalling && !fastFallPose)
                {
                    fastFallPose = true;
                    turnRemaining = 0f;
                    Play("Slide", 0f, false, .3f,.06f);
                }
                return;
            }
            if (motor.Sliding)
            {
                if (fastFallPose||current!="Slide")
                {
                    fastFallPose = false;
                    slidePosePhase=.32f;slideEntryElapsed=0;lastSlideCount=motor.SlideCount;lastSlideCycle=motor.SlideCycleCount;
                    // Move into the supplied low pose promptly. Its standing
                    // entry/recovery frames must not play under an active bar.
                    Play("Slide",0f,false,slidePosePhase,.06f);
                }
                slideEntryElapsed+=dt;
                if(lastSlideCount!=motor.SlideCount||lastSlideCycle!=motor.SlideCycleCount)
                {
                    lastSlideCount=motor.SlideCount;lastSlideCycle=motor.SlideCycleCount;
                    slideChainRemaining=.28f;
                }
                float phase=.32f+.18f*motor.SlideProgress;
                slidePosePhase=Mathf.MoveTowards(slidePosePhase,phase,dt*.35f);
                animator.SetFloat(MotionRate,0f);
                // A refreshed swipe smoothly returns within the crouched range,
                // never through the clip's upright start frame.
                if(slideEntryElapsed>=.06f&&!animator.IsInTransition(0))
                    animator.Play(Animator.StringToHash("Base Layer.Slide"),0,slidePosePhase);
                return;
            }
            if (Turning)
            {
                turnRemaining -= dt;
                if (Turning) return;
                ScheduleTurn();
            }
            if (current != "Run") Play("Run", Rate("Run"), false, LandingContactPhase());
            else animator.SetFloat(MotionRate,Rate("Run"));
            if (TurnAllowed && nextTurn <= 0f && quietSeconds >= 0.7f) TryTurn();
        }

        private float LandingContactPhase()
        {
            // Resume on the foot already farther forward, avoiding a long blend
            // through a mismatched recovery pose at the moment of ground contact.
            if(leftFoot!=null&&rightFoot!=null)
            {
                float separation=Vector3.Dot(leftFoot.position-rightFoot.position,motor.transform.forward);
                if(Mathf.Abs(separation)>.015f)return separation>0?0:.5f;
            }
            return lastRunPhase<.5f?.5f:0;
        }

        private void LateUpdate()
        {
            AppliedSprintLeanDegrees=RewardGestureWeight=0;
            if(deathActive)
            {
                Vector3 offset=Vector3.down*(deathFallHeight*Smooth(DeathProgress));
                animator.transform.localPosition=modelRestPosition+animator.transform.parent.InverseTransformVector(offset);
                return;
            }
            if(animator!=null&&animator.speed<=0)return;
            LandingReactionWeight=JumpBalanceWeight=SlideChainReactionWeight=0;
            if(animator==null||motor==null||spine==null||chest==null||neck==null||Time.deltaTime<=0)return;
            if(current=="Slide"&&motor.Grounded&&motor.Sliding)
            {
                // A small renewed push marks the next slide while the authored
                // low pelvis and feet stay under the bar throughout the chain.
                float push=slideChainRemaining>0?Mathf.Sin(Mathf.PI*(1-slideChainRemaining/.28f)):0;
                chest.rotation=Quaternion.AngleAxis(3.5f*push,motor.transform.right)*chest.rotation;
                BalanceArm(leftUpper,-1,push*.55f);BalanceArm(rewardUpper,1,push*.55f);
                SlideChainReactionWeight=push;return;
            }
            if(current=="Jump"&&!motor.Grounded&&!motor.FastFalling&&!motor.Flying)
            {
                // A restrained balance adjustment opens the wings on ascent and
                // gathers them before landing. Flip, pelvis and leg curves stay intact.
                float balance=Mathf.Sin(Mathf.PI*Mathf.Clamp01(motor.JumpAnimationPhase));
                BalanceArm(leftUpper,-1,balance);BalanceArm(rewardUpper,1,balance);JumpBalanceWeight=balance;
                return;
            }
            if(current!="Run"||!motor.Grounded||motor.Sliding||motor.Flying)return;
            float weight=animator.IsInTransition(0)?Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime):1;
            float degrees=sprintLean*weight;Vector3 axis=motor.transform.right;
            // Upper-body only: ankles, pelvis and the collision root retain
            // their authored positions throughout grounded presentation.
            spine.rotation=Quaternion.AngleAxis(degrees*.72f,axis)*spine.rotation;
            chest.rotation=Quaternion.AngleAxis(degrees*.28f,axis)*chest.rotation;
            neck.rotation=Quaternion.AngleAxis(-degrees*.78f,axis)*neck.rotation;
            AppliedSprintLeanDegrees=degrees;
            float cycle=animator.GetCurrentAnimatorStateInfo(0).normalizedTime*Mathf.PI*2;
            float drive=Mathf.Lerp(.08f,.16f,Mathf.InverseLerp(settings.forwardSpeed,RunTempo.HardCap,motor.Speed))*weight;
            StyleArm(leftUpper,leftFore,leftHand,cycle,true,drive,motor.transform.up,motor.transform.forward,axis);
            StyleArm(rewardUpper,rewardFore,rewardHand,cycle+Mathf.PI,true,drive,motor.transform.up,motor.transform.forward,axis);
            if(landingRemaining>0)
            {
                float t=1-landingRemaining/.26f;
                float recover=Mathf.Sin(Mathf.PI*t)*weight;
                chest.rotation=Quaternion.AngleAxis(5.5f*recover,axis)*chest.rotation;
                neck.rotation=Quaternion.AngleAxis(-4.5f*recover,axis)*neck.rotation;
                LandingReactionWeight=recover;
            }
            if(variantBlend>.0001f)ApplyVariant(variantBlend*weight);
            if(rewardRemaining>0&&rewardUpper!=null&&rewardFore!=null&&rewardHand!=null)
            {
                float t=1-rewardRemaining/rewardDuration;
                float reaction=Smooth(Mathf.Clamp01(t/.22f))*(1-Smooth(Mathf.Clamp01((t-.55f)/.45f)))*weight;
                ApplyReward(reaction,t);RewardGestureWeight=reaction;
            }
        }
        private void BalanceArm(Transform arm,float side,float weight)
        {
            if(arm==null)return;
            arm.rotation=Quaternion.AngleAxis(side*7f*weight,motor.transform.forward)*arm.rotation;
        }

        public void MenuTick(float dt)
        {
            if(!menuMode||animator.speed<=0)return;
            dt=Time.unscaledDeltaTime;menuDebounce=Mathf.Max(0,menuDebounce-dt);
            if(menuGestureRemaining>0)
            {
                menuGestureRemaining=Mathf.Max(0,menuGestureRemaining-dt);
                if(menuGestureRemaining<=0){Play("Idle",1f);menuClock=-1.8f;}
                return;
            }
            menuClock+=dt;if(menuClock>=.7f)PlayMenuGesture();
        }

        public bool PlayMenuGesture()
        {
            if(!menuMode||animator.speed<=0||menuDebounce>0)return false;
            string clip=MenuGestures[menuGestureIndex++%MenuGestures.Length];return BeginMenuGesture(clip);
        }
        private bool BeginMenuGesture(string clip)
        {
            if(!HasClip(clip))return false;
            menuGestureRemaining=Length(clip);menuClock=0;menuDebounce=.6f;MenuGestureCount++;
            Play(clip,1f);return true;
        }
        public bool CelebrateReward(bool major=false)
        {
            if(deathActive||animator==null||animator.speed<=0)return false;
            if(menuMode)return menuDebounce<=0&&BeginMenuGesture(major?"MenuPride":"MenuWave");
            if(rewardCooldown>0||rewardRemaining>0)return false;
            if(CanReact){BeginReward(major);return true;}
            pendingReward=1.1f;pendingMajor=major;return true;
        }
        private bool CanReact=>!deathActive&&motor!=null&&motor.Grounded&&!motor.Sliding&&!motor.Flying&&!Turning;
        private void BeginReward(bool major)
        {rewardDuration=major?1.05f:.82f;rewardRemaining=rewardDuration;rewardCooldown=rewardDuration+.65f;RewardGestureCount++;}
        private void ApplyReward(float weight,float phase)
        {
            Vector3 up=motor.transform.up,forward=motor.transform.forward,right=motor.transform.right;
            float side=Mathf.Sign(Vector3.Dot(rewardUpper.position-chest.position,right));Vector3 outward=right*side;
            chest.rotation=Quaternion.AngleAxis(-side*2.4f*weight,up)*chest.rotation;
            neck.rotation=Quaternion.AngleAxis(side*2.4f*weight,up)*neck.rotation;
            Vector3 target=chest.position+outward*.28f+forward*.30f+up*(.24f+.035f*Mathf.Sin(phase*Mathf.PI*2));
            Vector3 start=rewardUpper.position,delta=target-start;float upperLength=Vector3.Distance(start,rewardFore.position),lowerLength=Vector3.Distance(rewardFore.position,rewardHand.position);
            float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(upperLength-lowerLength)+.015f,upperLength+lowerLength-.015f);
            Vector3 direction=delta.normalized,pole=Vector3.ProjectOnPlane(outward-up*.3f,direction).normalized;
            float cosine=Mathf.Clamp((upperLength*upperLength+distance*distance-lowerLength*lowerLength)/(2*upperLength*distance),-1,1);
            Quaternion upperRest=rewardUpper.localRotation,foreRest=rewardFore.localRotation;
            rewardUpper.rotation=Quaternion.FromToRotation(rewardFore.position-start,direction*cosine+pole*Mathf.Sqrt(1-cosine*cosine))*rewardUpper.rotation;
            rewardFore.rotation=Quaternion.FromToRotation(rewardHand.position-rewardFore.position,start+direction*distance-rewardFore.position)*rewardFore.rotation;
            Quaternion upperPose=rewardUpper.localRotation,forePose=rewardFore.localRotation;
            rewardUpper.localRotation=Quaternion.Slerp(upperRest,upperPose,weight);rewardFore.localRotation=Quaternion.Slerp(foreRest,forePose,weight);
        }
        private static float Smooth(float t)=>t*t*t*(t*(t*6-15)+10);
        private bool HasClip(string name){foreach(var clip in clips)if(clip!=null&&clip.name==name)return true;return false;}

        private void ScheduleVariant(){variantCountdown=Random.Range(8f,14f);}
        private void TickVariants(float dt)
        {
            variantCountdown=Mathf.Max(0,variantCountdown-dt);
            if(runStyle!=RunStyle.None)variantElapsed+=dt;
            if(runStyle==RunStyle.None&&variantCountdown<=0&&CanReact&&!motor.ChangingLane&&rewardRemaining<=0&&current=="Run"&&!animator.IsInTransition(0))
            {
                float contact=Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,.5f);
                if(contact<.12f)
                {runStyle=(RunStyle)(1+nextVariant++%3);variantElapsed=0;variantDuration=runStyle==RunStyle.ShoulderGlance?1.65f:2.2f;RunStyleCount++;ScheduleVariant();}
            }
            float target=0;
            if(runStyle!=RunStyle.None&&CanReact&&rewardRemaining<=0)
            {
                float t=variantElapsed/variantDuration;
                target=Smooth(Mathf.Clamp01(t/.20f))*(1-Smooth(Mathf.Clamp01((t-.68f)/.32f)));
            }
            variantBlend=Mathf.MoveTowards(variantBlend,target,dt*7f);
            if(runStyle!=RunStyle.None&&variantElapsed>=variantDuration&&variantBlend<=0)runStyle=RunStyle.None;
        }
        private void ApplyVariant(float weight)
        {
            Vector3 up=motor.transform.up,forward=motor.transform.forward,right=motor.transform.right;
            float cycle=animator.GetCurrentAnimatorStateInfo(0).normalizedTime*Mathf.PI*2;
            if(runStyle==RunStyle.ShoulderGlance)
            {
                chest.rotation=Quaternion.AngleAxis(-5*weight,up)*chest.rotation;
                neck.rotation=Quaternion.AngleAxis(-15*weight,up)*neck.rotation;
                head.rotation=Quaternion.AngleAxis(-3*weight,forward)*head.rotation;
                return;
            }
            bool compact=runStyle==RunStyle.CompactDrive;
            float lean=compact?3.6f:-1.5f;
            chest.rotation=Quaternion.AngleAxis(lean*weight,right)*Quaternion.AngleAxis((compact?1.5f:4f)*Mathf.Sin(cycle)*weight,up)*chest.rotation;
            neck.rotation=Quaternion.AngleAxis(-lean*.7f*weight,right)*neck.rotation;
            StyleArm(leftUpper,leftFore,leftHand,cycle,compact,weight,up,forward,right);
            StyleArm(rewardUpper,rewardFore,rewardHand,cycle+Mathf.PI,compact,weight,up,forward,right);
        }
        private void StyleArm(Transform upper,Transform fore,Transform hand,float phase,bool compact,float weight,Vector3 up,Vector3 forward,Vector3 right)
        {
            if(upper==null||fore==null||hand==null)return;
            float sign=Mathf.Sign(Vector3.Dot(upper.position-chest.position,right));
            Vector3 direction=(-up-forward*((compact?.75f:.45f)*Mathf.Cos(phase-.12f))+right*(sign*(compact?.10f:.36f))).normalized;
            Quaternion upperRest=upper.localRotation,foreRest=fore.localRotation;
            upper.rotation=Quaternion.FromToRotation(fore.position-upper.position,direction)*upper.rotation;
            Vector3 lowerDirection=Quaternion.AngleAxis(compact?-97f:-65f,right)*direction;
            fore.rotation=Quaternion.FromToRotation(hand.position-fore.position,lowerDirection)*fore.rotation;
            Quaternion upperPose=upper.localRotation,forePose=fore.localRotation;
            upper.localRotation=Quaternion.Slerp(upperRest,upperPose,weight);fore.localRotation=Quaternion.Slerp(foreRest,forePose,weight);
        }
#if UNITY_EDITOR
        public void QueueRunStyleForTest(int index){nextVariant=Mathf.Clamp(index,0,2);variantCountdown=0;}
#endif

        private void OnJump(bool flip)
        {
            if(deathActive)return;
            slideChainRemaining=SlideChainReactionWeight=0;
            wasAirborne=true;landingRemaining=LandingReactionWeight=0;
            rewardRemaining=RewardGestureWeight=0;
            runStyle=RunStyle.None;variantBlend=0;
            fastFallPose = flightPose = false;
            turnRemaining = 0f;
            string clip = flip ? "JumpFlip" : "Jump";
            Play(clip, Length(clip) / Mathf.Max(.2f,motor.ExpectedAirSeconds));
        }

        // The authored cycle has real support and flight phases. Cadence rises
        // gently with travel speed while preserving a readable foot recovery.
        private float TargetRunRate()
        {
            float reference = Mathf.Max(settings.forwardSpeed, settings.runReferenceSpeed);
            return Mathf.Clamp(1.34f + (motor.Speed / reference - 1f) * .40f, 1.25f, 1.8f);
        }
        private float Rate(string clip) => clip == "Run" ? runRate : 1f;
        private float Length(string clip)
        {
            foreach (AnimationClip item in clips) if (item != null && item.name == clip) return item.length;
            throw new System.InvalidOperationException("Required character clip is missing: " + clip);
        }
        private void ScheduleTurn() { nextTurn = Random.Range(settings.turnIntervalSeconds.x, settings.turnIntervalSeconds.y); }
        private void Play(string clip, float speed, bool immediate = false, float normalizedOffset = 0f,float blendOverride=-1f)
        {
            current = clip;
            animator.SetFloat(MotionRate, speed);
            int state = Animator.StringToHash("Base Layer." + clip);
            if (immediate) animator.Play(state, 0, normalizedOffset);
            else animator.CrossFadeInFixedTime(state,blendOverride>=0?blendOverride:clip=="Run"?Mathf.Min(settings.blendSeconds,.09f):settings.blendSeconds, 0, normalizedOffset * Length(clip));
        }
        private void OnDestroy(){if(motor!=null)motor.Jumped-=OnJump;}
    }
}
