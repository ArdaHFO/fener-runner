using UnityEngine;

namespace FenerRunner
{
    // These values are also the compact pose selector in Supporters.shader.
    public enum CrowdReactionKind { Arrival, Jump, Slide, NearMiss, CoinStreak, Power, Wave, Loss }

    /// <summary>
    /// A moving pocket of supporters reacts to accepted game actions. The people
    /// remain in two shared meshes; this director publishes only three shader
    /// vectors and an envelope for the existing recorded stadium ambience.
    /// </summary>
    public sealed class CrowdReactions : MonoBehaviour
    {
        public const float ReactionSeconds=3.6f;
        const float RecoveryStart=ReactionSeconds,PendingLifetime=ReactionSeconds+.5f;
        static readonly int OriginId=Shader.PropertyToID("_CrowdReactionOrigin");
        static readonly int DataId=Shader.PropertyToID("_CrowdReactionData");
        static readonly int PlayerId=Shader.PropertyToID("_CrowdPlayer");
        static readonly int WorldOffsetId=Shader.PropertyToID("_CrowdWorldOffset");
        static readonly int ReactionSeedId=Shader.PropertyToID("_CrowdReactionSeed");
        RunnerGame game;
        bool initialized,pending;
        CrowdReactionKind pendingKind;
        float clock,nextAmbient,pendingAt,pendingIntensity,pendingRadius,intensity,radius,worldOffset;
        int seed,side,pendingSide,tokenProgress,cleanProgress,sequence;
        public float PresentationTime=>clock;
        public float ReactionAge {get;private set;}=-1f;
        public CrowdReactionKind LastKind {get;private set;}
        public Vector3 ReactionOrigin {get;private set;}
        public int ReactionSide=>side;
        public float ReactionIntensity=>intensity;
        public float ReactionRadius=>radius;
        public float AudioExcitement {get;private set;}
        public float AudioPan {get;private set;}
        public bool HasPending=>pending;
        public CrowdReactionKind PendingKind=>pendingKind;
        public int ReactionCount {get;private set;}
        public int AmbientReactionCount {get;private set;}
        public int JumpRequests {get;private set;}
        public int AirJumpRequests {get;private set;}
        public int SlideRequests {get;private set;}
        public int ChainedSlideRequests {get;private set;}
        public int CleanPassRequests {get;private set;}
        public int TokensObserved {get;private set;}
        public int TokenStreakRequests {get;private set;}
        public int PowerRequests {get;private set;}
        bool CanReact=>initialized&&isActiveAndEnabled&&game!=null&&game.Motor!=null&&game.State==RunState.Running;

        public void Initialize(RunnerGame owner)
        {
            game=owner;initialized=game!=null;ResetPresentation();
        }
        public void ResetPresentation()
        {
            clock=worldOffset=0;Shader.SetGlobalFloat(WorldOffsetId,0);sequence=tokenProgress=cleanProgress=0;
            ReactionCount=AmbientReactionCount=JumpRequests=AirJumpRequests=SlideRequests=ChainedSlideRequests=0;
            CleanPassRequests=TokensObserved=TokenStreakRequests=PowerRequests=0;
            seed=game!=null&&game.Obstacles!=null?game.Obstacles.Seed:1907;
            nextAmbient=.65f;LastKind=CrowdReactionKind.Arrival;Clear();PublishPlayer();
        }
        public void NotifyJump(bool airJump)
        {
            if(!CanReact)return;
            JumpRequests++;if(airJump)AirJumpRequests++;
            Request(CrowdReactionKind.Jump,airJump?.94f:.65f,airJump?18f:13f,ChooseSide());
        }
        public void NotifySlide(bool chained)
        {
            if(!CanReact)return;
            SlideRequests++;if(chained)ChainedSlideRequests++;
            Request(CrowdReactionKind.Slide,chained?.83f:.61f,chained?17f:12f,ChooseSide());
        }
        public void NotifyCleanPass(int count,int streak)
        {
            if(!CanReact||count<=0)return;
            CleanPassRequests+=count;cleanProgress+=count;
            if(cleanProgress<3)return;
            cleanProgress%=3;
            Request(streak>=8?CrowdReactionKind.Wave:CrowdReactionKind.CoinStreak,streak>=8?1f:.73f,streak>=8?24f:16f,streak>=8?0:ChooseSide());
        }
        public void NotifyTokens(int count)
        {
            if(!CanReact||count<=0)return;
            TokensObserved+=count;tokenProgress+=count;
            if(tokenProgress<6)return;
            tokenProgress%=6;TokenStreakRequests++;
            Request(CrowdReactionKind.CoinStreak,.78f,17f,ChooseSide());
        }
        public void NotifyPower(PowerupKind kind)
        {
            if(!CanReact)return;
            PowerRequests++;
            // Both stands may notice a power, but the GPU chooses different
            // participants, delays and gestures for this event serial.
            Request(CrowdReactionKind.Power,1f,22f,0);
        }
        void Request(CrowdReactionKind kind,float strength,float extent,int selectedSide)
        {
            if(ReactionAge<0)
            {Begin(kind,strength,extent,selectedSide);return;}
            // Keep just the most important recent cue. Repeated swipes cannot
            // restart a pose every frame or postpone a waiting power salute.
            if(!pending||Priority(kind)>Priority(pendingKind)||kind==pendingKind)
            {
                pending=true;pendingKind=kind;pendingIntensity=strength;pendingRadius=extent;
                pendingSide=selectedSide;pendingAt=clock;
            }
        }
        static int Priority(CrowdReactionKind kind)
        {return kind==CrowdReactionKind.Power?4:kind==CrowdReactionKind.NearMiss?3:kind==CrowdReactionKind.CoinStreak?2:kind==CrowdReactionKind.Arrival?0:1;}
        void Begin(CrowdReactionKind kind,float strength,float extent,int selectedSide)
        {
            LastKind=kind;intensity=strength;radius=extent;side=selectedSide;ReactionAge=0;ReactionCount++;sequence++;
            UpdateOrigin();Publish();
        }
        public void Tick(float dt)
        {
            if(!isActiveAndEnabled||!initialized||game==null||game.Motor==null)return;
            if(game.State==RunState.Paused)return;
            if(game.State!=RunState.Running){Clear();PublishPlayer();return;}
            PublishPlayer();
            if(dt<=0||float.IsNaN(dt)||float.IsInfinity(dt))return;
            // A suspended debugger frame must not overflow clocks or emit a
            // backlog of chants when the application becomes active again.
            dt=Mathf.Min(dt,1f);clock+=dt;
            if(ReactionAge>=0)ReactionAge+=dt;
            if(pending&&clock-pendingAt>PendingLifetime)pending=false;
            if(pending&&(ReactionAge<0||ReactionAge>=RecoveryStart))
            {
                pending=false;Begin(pendingKind,pendingIntensity,pendingRadius,pendingSide);
            }
            else if(ReactionAge>=ReactionSeconds)ReactionAge=-1;
            if(clock>=nextAmbient)
            {
                nextAmbient=clock+11f+Hash(sequence+17)*6f;
                if(ReactionAge<0&&!pending)
                {AmbientReactionCount++;Begin(ReactionCount==0?CrowdReactionKind.Arrival:CrowdReactionKind.Wave,.76f,23f,0);}
            }
            if(ReactionAge>=0)UpdateOrigin();
            float envelope=ReactionAge<0?0:Mathf.SmoothStep(0,1,Mathf.Clamp01(ReactionAge/.38f))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.1f,ReactionSeconds,ReactionAge)));
            AudioExcitement=Mathf.MoveTowards(AudioExcitement,envelope*intensity,dt*2.8f);
            AudioPan=Mathf.MoveTowards(AudioPan,side*.22f*AudioExcitement,dt*.7f);
            Publish();
        }
        void UpdateOrigin()
        {
            Vector3 player=game.Motor.transform.position;
            float edge=game.Settings.laneSpacing*1.5f+.7f;
            // Supporters stay anchored. Only their attention travels along the
            // stand with the runner, keeping the reaction visible at42m/s too.
            ReactionOrigin=new Vector3(side*(edge+3.4f),.55f,player.z+Mathf.Clamp(game.CurrentSpeed,0,42)*.55f);
        }
        public void ShiftOrigin(float amount)
        {
            if(float.IsNaN(amount)||float.IsInfinity(amount)||amount==0)return;
            worldOffset+=amount;Shader.SetGlobalFloat(WorldOffsetId,worldOffset);
            Vector3 origin=ReactionOrigin;origin.z-=amount;ReactionOrigin=origin;
            Publish();
            // RunnerGame calls this before motor.ShiftOrigin. Rebase the
            // published attention position directly to avoid a one-frame jump.
            Vector4 player=Shader.GetGlobalVector(PlayerId);player.z-=amount;Shader.SetGlobalVector(PlayerId,player);
        }
        void PublishPlayer()
        {
            if(game==null||game.Motor==null){Shader.SetGlobalVector(PlayerId,Vector4.zero);return;}
            Vector3 p=game.Motor.transform.position;
            Shader.SetGlobalVector(PlayerId,new Vector4(p.x,p.y,p.z,isActiveAndEnabled&&(game.State==RunState.Running||game.State==RunState.Paused)?1:0));
        }
        void Publish()
        {
            Vector3 p=ReactionOrigin;
            Shader.SetGlobalVector(OriginId,new Vector4(p.x,p.y,p.z,side));
            Shader.SetGlobalVector(DataId,new Vector4(ReactionAge,(int)LastKind,intensity,radius));
            Shader.SetGlobalFloat(ReactionSeedId,sequence);
        }
        void Clear()
        {
            pending=false;ReactionAge=-1;AudioExcitement=AudioPan=0;intensity=radius=0;side=0;ReactionOrigin=Vector3.zero;Publish();
        }
        int ChooseSide()
        {
            int lane=game.Motor.Lane;
            return lane!=0?lane:((JumpRequests+SlideRequests+TokenStreakRequests+CleanPassRequests)&1)==0?-1:1;
        }
        float Hash(int value)
        {uint n=unchecked((uint)(value^seed))*747796405u+2891336453u;n=((n>>((int)(n>>28)+4))^n)*277803737u;return ((n>>22)^n)/(float)uint.MaxValue;}
        void OnDisable(){Clear();Shader.SetGlobalVector(PlayerId,Vector4.zero);}
        void OnDestroy(){Clear();Shader.SetGlobalVector(PlayerId,Vector4.zero);Shader.SetGlobalFloat(WorldOffsetId,0);}
    }
}
