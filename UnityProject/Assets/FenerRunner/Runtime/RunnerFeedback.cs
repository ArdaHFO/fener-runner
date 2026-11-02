using UnityEngine;

namespace FenerRunner
{
    public sealed class RunnerFeedback : MonoBehaviour
    {
        [SerializeField] Material sparkleMaterial;
        RunnerGame game;Transform accents;ParticleSystem sparks,streaks;Vector3 previous;
        LineRenderer rewardRing;
        float ringClock,trailClock;
        Color ringColor;
        readonly Vector3[] ringDirections=new Vector3[40],ringPositions=new Vector3[40];
        int lastFootContact=-1;float lastAirVelocity;
        float crowdImpulse;bool crowdApplied;
        static readonly int CrowdEnergyProperty=Shader.PropertyToID("_CrowdEnergy");
        public bool RewardPulseVisible=>rewardRing!=null&&rewardRing.enabled;
        public int ParticleCapacity=>104;
        public int LiveParticleCount=>(sparks==null?0:sparks.particleCount)+(streaks==null?0:streaks.particleCount);
        public int FootContactBurstCount{get;private set;}
        public int LandingBurstCount{get;private set;}
        public float RewardPulseRemaining=>ringClock;
        public float CrowdEnergy{get;private set;}
        bool grounded;float squash,lean;
        public void Configure(Material material){sparkleMaterial=material;}
        void Start()
        {
            game=GetComponent<RunnerGame>();accents=game.Motor.transform.Find("Motion accents");previous=game.Motor.transform.position;grounded=true;
            var go=new GameObject("Landing and reward sparks",typeof(ParticleSystem));go.transform.SetParent(game.Motor.transform,false);
            sparks=go.GetComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=sparks.main;main.loop=false;main.playOnAwake=false;main.startLifetime=.38f;main.startSpeed=2.5f;main.startSize=.1f;main.startColor=new Color(1,.8f,.15f);main.gravityModifier=.4f;main.maxParticles=64;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var emission=sparks.emission;emission.enabled=false;
            var shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.4f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial=sparkleMaterial;
            var trailObject=new GameObject("Pooled speed and slide accents",typeof(ParticleSystem));trailObject.transform.SetParent(game.Motor.transform,false);
            streaks=trailObject.GetComponent<ParticleSystem>();streaks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var trailMain=streaks.main;trailMain.loop=false;trailMain.playOnAwake=false;trailMain.startLifetime=.24f;trailMain.startSpeed=0;trailMain.startSize=.045f;trailMain.maxParticles=40;trailMain.simulationSpace=ParticleSystemSimulationSpace.Local;
            var trailEmission=streaks.emission;trailEmission.enabled=false;
            var trailShape=streaks.shape;trailShape.enabled=false;
            var trailRender=trailObject.GetComponent<ParticleSystemRenderer>();trailRender.sharedMaterial=sparkleMaterial;trailRender.renderMode=ParticleSystemRenderMode.Stretch;trailRender.lengthScale=3;trailRender.velocityScale=.035f;
            var ringObject=new GameObject("Reward pulse ring",typeof(LineRenderer));ringObject.transform.SetParent(game.Motor.transform,false);
            rewardRing=ringObject.GetComponent<LineRenderer>();rewardRing.sharedMaterial=sparkleMaterial;rewardRing.useWorldSpace=false;rewardRing.loop=true;rewardRing.positionCount=40;rewardRing.startWidth=rewardRing.endWidth=.035f;rewardRing.enabled=false;
            rewardRing.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;rewardRing.receiveShadows=false;
            for(int i=0;i<ringDirections.Length;i++){float a=i*Mathf.PI*2/ringDirections.Length;ringDirections[i]=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));}
        }
        public void Burst(){if(sparks!=null){var emit=new ParticleSystem.EmitParams{position=Vector3.up,startColor=new Color(1,.8f,.15f)};sparks.Emit(emit,5);}}
        public void PowerBurst(PowerupKind kind)
        {
            Pulse(RunnerUI.PowerColor(kind));
            if(sparks==null)return;
            var emit=new ParticleSystem.EmitParams{position=Vector3.up*1.1f,startColor=ringColor,startSize=.12f};sparks.Emit(emit,24);
        }
        public void Celebrate(){Pulse(new Color(1,.82f,.2f));Burst();}
        void Pulse(Color color){ringColor=color;ringClock=.52f;crowdImpulse=1;if(rewardRing!=null)rewardRing.enabled=true;}
        public void ResetFeedback()
        {
            squash=lean=ringClock=trailClock=lastAirVelocity=0;grounded=true;lastFootContact=-1;
            FootContactBurstCount=LandingBurstCount=0;
            crowdImpulse=0;SetCrowdEnergy(0);
            if(game!=null)previous=game.Motor.transform.position;
            if(accents!=null){accents.localPosition=Vector3.zero;accents.localRotation=Quaternion.identity;accents.localScale=Vector3.one;}
            if(sparks!=null)sparks.Clear();
            if(streaks!=null)streaks.Clear();if(rewardRing!=null)rewardRing.enabled=false;
        }
        void LateUpdate()
        {
            if(game==null||accents==null)return;
            if(game.State!=RunState.Running)
            {
                if(game.State==RunState.Menu)accents.localRotation=Quaternion.Euler(0,180,0);
                if(game.State!=RunState.Paused){crowdImpulse=0;SetCrowdEnergy(0);}
                previous=game.Motor.transform.position;return;
            }
            float dt=Time.deltaTime;if(dt<=0)return;
            TickEffects(dt);
            TickFootContacts();
            Vector3 p=game.Motor.transform.position;
            float target=Mathf.Clamp(-(p.x-previous.x)/dt*.35f,-8,8);
            lean=Mathf.Lerp(lean,target,1-Mathf.Exp(-dt*16));
            if(!grounded&&game.Motor.Grounded)
            {
                squash=.14f;LandingBurstCount++;
                if(sparks!=null)
                {
                    var emit=new ParticleSystem.EmitParams{position=Vector3.up*.1f,startColor=new Color(1,.86f,.45f,.7f),startLifetime=.3f,startSize=.075f};
                    sparks.Emit(emit,Mathf.RoundToInt(Mathf.Lerp(14,22,Mathf.Clamp01(Mathf.Abs(lastAirVelocity)/24f))));
                }
                if(ringClock<.12f)Pulse(new Color(1,.84f,.38f,.7f));
            }
            if(!game.Motor.Grounded)lastAirVelocity=game.Motor.VerticalVelocity;
            grounded=game.Motor.Grounded;squash=Mathf.Max(0,squash-dt);
            // The imported somersault swings its head below the normal foot pivot.
            // A small, closed pose offset keeps the tucked mesh above the blocker
            // while preserving the same real capsule, apex and short landing time.
            float flipLift=!grounded&&game.Animation.CurrentClip=="JumpFlip"?.14f*Mathf.Sin(Mathf.PI*game.Motor.JumpAnimationPhase):0;
            accents.localPosition=Vector3.up*flipLift;
            accents.localRotation=Quaternion.Euler(0,0,game.Motor.Sliding?0:lean);
            float amount=game.Motor.Sliding?0:Mathf.Sin(squash/.14f*Mathf.PI)*.045f;
            accents.localScale=new Vector3(1+amount,1-amount,1+amount);
            previous=p;
        }
        void TickFootContacts()
        {
            var motor=game.Motor;var animator=game.Animation.Animator;
            if(sparks==null||!motor.Grounded||motor.Sliding||motor.Flying||game.Animation.CurrentClip!="Run"||animator.IsInTransition(0))
            {lastFootContact=-1;return;}
            var state=animator.GetCurrentAnimatorStateInfo(0);
            if(!state.IsName("Run")){lastFootContact=-1;return;}
            int contact=Mathf.FloorToInt(state.normalizedTime*2);
            if(lastFootContact<0||contact<lastFootContact){lastFootContact=contact;return;}
            // Footfalls follow the visible Run contact phases, including upper-body
            // variants. At most two tiny puffs may catch up after a long frame.
            int due=Mathf.Min(2,contact-lastFootContact);
            for(int i=0;i<due;i++)
            {
                float side=((lastFootContact+i+1)&1)==0?-.23f:.23f;
                var emit=new ParticleSystem.EmitParams{position=new Vector3(side,.065f,-.08f),velocity=new Vector3(side*.45f,.28f,-.55f),startLifetime=.24f,startSize=.06f,startColor=new Color(.82f,.84f,.77f,.4f)};
                sparks.Emit(emit,3);FootContactBurstCount++;
            }
            lastFootContact=contact;
        }
        void TickEffects(float dt)
        {
            crowdImpulse=Mathf.Max(0,crowdImpulse-dt*.85f);
            float crowdTarget=Mathf.Clamp01(.04f+Mathf.InverseLerp(18,42,game.CurrentSpeed)*.3f+(game.FeverActive?.25f:0)+crowdImpulse*.6f);
            SetCrowdEnergy(Mathf.Lerp(CrowdEnergy,crowdTarget,1-Mathf.Exp(-dt*5f)));
            if(rewardRing!=null&&ringClock>0)
            {
                ringClock=Mathf.Max(0,ringClock-dt);float t=1-ringClock/.52f;
                float radius=Mathf.Lerp(.38f,1.05f,1-Mathf.Pow(1-t,3));
                for(int i=0;i<ringPositions.Length;i++)ringPositions[i]=ringDirections[i]*radius+Vector3.up*(.12f+t*.45f);
                rewardRing.SetPositions(ringPositions);
                Color c=ringColor;c.a=(1-t)*.85f;rewardRing.startColor=rewardRing.endColor=c;rewardRing.enabled=ringClock>0;
            }
            bool slide=game.Motor.Sliding;
            bool boosted=game.FeverActive||(game.Powerups!=null&&game.Powerups.DashRemaining>0);
            if(streaks==null||(!slide&&!boosted)){trailClock=0;return;}
            trailClock+=dt;
            if(trailClock<.045f)return;trailClock=0;
            // Two bounded local-space streaks hug the feet; no screen shake,
            // collider changes, new GameObjects or growing effect pools.
            var emit=new ParticleSystem.EmitParams{startColor=slide?new Color(.68f,.8f,.86f,.55f):new Color(1,.8f,.22f,.75f),velocity=new Vector3(0,slide?.3f:.15f,-5.5f),startLifetime=.24f,startSize=slide?.035f:.055f};
            emit.position=new Vector3(-.28f,.12f,-.1f);streaks.Emit(emit,1);emit.position=new Vector3(.28f,.12f,-.1f);streaks.Emit(emit,1);
        }
        void SetCrowdEnergy(float value)
        {value=Mathf.Clamp01(value);if(crowdApplied&&CrowdEnergy==value)return;crowdApplied=true;CrowdEnergy=value;Shader.SetGlobalFloat(CrowdEnergyProperty,CrowdEnergy);}
        void OnDestroy(){Shader.SetGlobalFloat(CrowdEnergyProperty,0);}
    }
}
