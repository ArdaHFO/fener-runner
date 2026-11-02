using UnityEngine;

namespace FenerRunner
{
    /// <summary>Small stadium props, entirely separate from gameplay collision and rewards.</summary>
    public sealed class CrowdThrows : MonoBehaviour
    {
        public const string ResourceName="CrowdBottle";
        public const int Capacity=8;
        public const float WindupSeconds=.35f;
        const float GroundHeight=.095f, Gravity=9.81f;
        static readonly int ThrowOriginId=Shader.PropertyToID("_CrowdThrowOrigin");
        static readonly int ThrowAgeId=Shader.PropertyToID("_CrowdThrowAge");
        static readonly int BaseColorId=Shader.PropertyToID("_BaseColor");
        sealed class Bottle
        {
            public Transform Transform;public Renderer Renderer;public MeshFilter Filter;public bool Active;
            public Vector3 Start,End,Axis;public Quaternion Initial,Rest;
            public float Released,Duration,Spin,Age,Opacity;
            public CrowdPropKind Kind;
        }
        readonly Bottle[] bottles=new Bottle[Capacity];
        readonly int[] kindCounts=new int[4];
        static readonly CrowdPropKind[] kindCycle={CrowdPropKind.Bottle,CrowdPropKind.PaperCup,CrowdPropKind.Bottle,CrowdPropKind.CrumpledPaper,CrowdPropKind.Bottle,CrowdPropKind.FoamBall,CrowdPropKind.Bottle,CrowdPropKind.PaperCup};
        MaterialPropertyBlock properties;
        CrowdPropLibrary library;Mesh bottleMesh;Material bottleMaterial;
        RunnerGame game;Transform root;
        bool initialized,pending;
        float clock,nextEvent,eventTime,pendingDuration,originShift;
        int cursor,eventCount,seed;
        Vector3 pendingStart,pendingEnd;
        CrowdPropKind pendingKind;
        public Transform VisualRoot=>root;
        public int PoolCount=>initialized?Capacity:0;
        public int ActiveCount {get {int n=0;foreach(var bottle in bottles)if(bottle!=null&&bottle.Active)n++;return n;}}
        public int ThrowCount {get;private set;}
        public int LeftThrowCount {get;private set;}
        public int RightThrowCount {get;private set;}
        public float PresentationTime=>clock;
        public bool PendingThrow=>pending;
        public float LastThrowAge {get;private set;}=-1f;
        public Vector3 LastThrowOrigin {get;private set;}
        public Vector3 LastLanding {get;private set;}
        public int LastThrowSide {get;private set;}
        public CrowdPropKind LastThrowKind {get;private set;}
        public bool HasPropVariety=>library!=null&&library.Complete;
        public CrowdPropKind KindAt(int index)=>index>=0&&index<Capacity&&bottles[index]!=null?bottles[index].Kind:CrowdPropKind.Bottle;
        public int ThrowsOfKind(CrowdPropKind kind)=>kindCounts[(int)kind];
        public Transform BottleAt(int index)=>index>=0&&index<Capacity&&bottles[index]!=null?bottles[index].Transform:null;
        public bool IsActiveAt(int index)=>index>=0&&index<Capacity&&bottles[index]!=null&&bottles[index].Active;
        public float AgeAt(int index)=>index>=0&&index<Capacity&&bottles[index]!=null?bottles[index].Age:0;
        public float OpacityAt(int index)=>index>=0&&index<Capacity&&bottles[index]!=null?bottles[index].Opacity:0;
        public float FlightDurationAt(int index)=>index>=0&&index<Capacity&&bottles[index]!=null?bottles[index].Duration:0;

        public void Initialize(RunnerGame owner)
        {
            if(initialized&&game==owner)return;
            game=owner;
            var prefab=Resources.Load<GameObject>(ResourceName);
            if(prefab==null){Debug.LogWarning("Crowd bottle prefab is missing. Prepare the v17 stadium assets.",this);return;}
            properties=new MaterialPropertyBlock();
            library=Resources.Load<CrowdPropLibrary>(CrowdPropLibrary.ResourceName);
            bottleMesh=prefab.GetComponent<MeshFilter>().sharedMesh;bottleMaterial=prefab.GetComponent<Renderer>().sharedMaterial;
            root=new GameObject("Pooled crowd props (visual only)").transform;
            for(int i=0;i<Capacity;i++)
            {
                Transform item=Instantiate(prefab,root).transform;item.name="Stadium bottle "+i;
                bottles[i]=new Bottle{Transform=item,Renderer=item.GetComponent<Renderer>(),Filter=item.GetComponent<MeshFilter>()};item.gameObject.SetActive(false);
            }
            initialized=game!=null;ResetPresentation();
        }

        public void ResetPresentation()
        {
            HideAll();clock=0;cursor=eventCount=ThrowCount=LeftThrowCount=RightThrowCount=0;originShift=0;
            seed=game!=null&&game.Obstacles!=null?game.Obstacles.Seed:1907;
            System.Array.Clear(kindCounts,0,kindCounts.Length);
            foreach(var bottle in bottles)if(bottle!=null)SetAppearance(bottle,CrowdPropKind.Bottle);
            nextEvent=1.8f+Hash(1)*.8f;pending=false;LastThrowAge=-1;LastThrowOrigin=LastLanding=Vector3.zero;LastThrowSide=0;LastThrowKind=CrowdPropKind.Bottle;
            PublishCue();
        }

        public void Tick(float dt)
        {
            if(!isActiveAndEnabled||!initialized||game==null||game.Motor==null)return;
            if(game.State==RunState.Paused)return;
            if(game.State!=RunState.Running)
            {HideAll();pending=false;LastThrowAge=-1;PublishCue();return;}
            if(dt<=0||float.IsNaN(dt)||float.IsInfinity(dt))return;
            clock+=dt;
            if(!pending&&clock>=nextEvent)Schedule(0);
            if(LastThrowAge>=0)
            {
                LastThrowAge=clock-eventTime;
                if(LastThrowAge>1.25f)LastThrowAge=-1;
                PublishCue();
            }
            if(pending&&clock-eventTime>=WindupSeconds)Release();
            foreach(var bottle in bottles)
            {
                if(!bottle.Active)continue;
                bottle.Age=clock-bottle.Released;
                float landed=bottle.Age-bottle.Duration;
                if(landed>2.4f||bottle.Transform.position.z<game.Motor.transform.position.z-3.5f)
                {Hide(bottle);continue;}
                if(landed<0)
                {
                    float t=Mathf.Clamp01(bottle.Age/bottle.Duration);
                    Vector3 p=Vector3.Lerp(bottle.Start,bottle.End,t);
                    p.y+=.5f*Gravity*bottle.Duration*bottle.Duration*t*(1-t);
                    Quaternion tumble=Quaternion.AngleAxis(bottle.Spin*bottle.Age,bottle.Axis)*bottle.Initial;
                    if(bottle.Kind==CrowdPropKind.CrumpledPaper)
                    {float flutter=Mathf.Sin(bottle.Age*19)*.065f*Mathf.Sin(t*Mathf.PI);p.x+=flutter;p.z+=Mathf.Cos(bottle.Age*14)*.035f*Mathf.Sin(t*Mathf.PI);}
                    float settle=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.84f,1f,t));
                    bottle.Transform.SetPositionAndRotation(p,Quaternion.Slerp(tumble,bottle.Rest,settle));
                    bottle.Opacity=1;
                }
                else
                {
                    // Two gravity-driven rebounds, then friction slows a short skid.
                    float bounce=Bounce(landed,BounceVelocity(bottle.Kind));
                    float drag=bottle.Kind==CrowdPropKind.CrumpledPaper?5.5f:bottle.Kind==CrowdPropKind.FoamBall?1.6f:2.7f;
                    float drift=(1-Mathf.Exp(-landed*drag))/drag;
                    Vector3 direction=new Vector3(-Mathf.Sign(bottle.Start.x)*.8f,0,1.7f);
                    Vector3 p=bottle.End+direction*drift+Vector3.up*bounce;
                    float wobble=Mathf.Sin(landed*18)*Mathf.Exp(-landed*6)*8;
                    Quaternion roll=bottle.Kind==CrowdPropKind.FoamBall?Quaternion.AngleAxis(drift*400,Vector3.right):Quaternion.Euler(0,0,wobble);
                    bottle.Transform.SetPositionAndRotation(p,bottle.Rest*roll);
                    bottle.Opacity=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.7f,2.4f,landed));
                }
                properties.SetColor(BaseColorId,new Color(1,1,1,bottle.Opacity));bottle.Renderer.SetPropertyBlock(properties);
            }
        }

        void Schedule(int forcedSide,CrowdPropKind? forcedKind=null)
        {
            eventCount++;
            int side=forcedSide==0?(((eventCount+(Hash(19)>.5f?1:0))&1)==0?-1:1):forcedSide;
            int typeIndex=(eventCount-1+(int)(Hash(53)*8))%kindCycle.Length;
            pendingKind=HasPropVariety?(forcedKind??kindCycle[typeIndex]):CrowdPropKind.Bottle;
            pendingDuration=(pendingKind==CrowdPropKind.CrumpledPaper?1.19f:pendingKind==CrowdPropKind.FoamBall?.99f:1.04f)+Hash(eventCount*11)*.20f;
            float speed=Mathf.Clamp(game.CurrentSpeed,18,42);
            // Predict the runner's travel, then leave a modest clear lead. A
            // longer lead made a transparent PET bottle disappear into the
            // distant crowd on a portrait phone camera instead of crossing
            // visibly over the road. This still lands safely ahead of the player.
            float desiredZ=game.Motor.transform.position.z+speed*(WindupSeconds+pendingDuration)+6f+Hash(eventCount*13)*4;
            float length=game.Settings.chunkLength,globalZ=desiredZ+originShift;
            float segment=Mathf.Floor(globalZ/length)*length;
            int supporter=Mathf.Clamp(Mathf.RoundToInt((globalZ-segment-.4f)/1.16f),0,33);
            float z=segment+supporter*1.16f+.4f-originShift;
            float edge=game.Settings.laneSpacing*1.5f+.7f;
            Vector4 anatomy=CrowdAnatomy.Sample(side,0,supporter);
            Vector3 shoulder=new Vector3(.22f*anatomy.y,1.28f,0),target=new Vector3(.24f*anatomy.y,1.83f,.20f);
            Vector3 towards=target-shoulder;
            Vector3 release=shoulder+towards.normalized*Mathf.Min(towards.magnitude,.596f);
            pendingStart=new Vector3(side*(edge+3.4f-release.z*anatomy.x),.55f+release.y*anatomy.x,z+side*release.x*anatomy.x);
            // Keep the landing on the track and well ahead of the moving player.
            float landingX=Mathf.Lerp(-game.Settings.laneSpacing,game.Settings.laneSpacing,Hash(eventCount*23));
            pendingEnd=new Vector3(landingX,RestHeight(pendingKind),z+2.2f+Hash(eventCount*29)*2.4f);
            eventTime=clock;pending=true;LastThrowAge=0;LastThrowSide=side;LastThrowOrigin=pendingStart;LastLanding=pendingEnd;LastThrowKind=pendingKind;
            // Occasional pairs use a full gesture/recovery gap. The next pair
            // cannot queue recursively or turn the screen into a projectile wall.
            float gap=eventCount%5==0?1.65f+Hash(eventCount*31)*.60f:2.55f+Hash(eventCount*31)*1.30f;
            nextEvent=clock+gap;PublishCue();
        }

        void Release()
        {
            pending=false;Bottle bottle=bottles[cursor];cursor=(cursor+1)%Capacity;
            SetAppearance(bottle,pendingKind);
            bottle.Start=pendingStart;bottle.End=pendingEnd;bottle.Duration=pendingDuration;bottle.Released=eventTime+WindupSeconds;
            bottle.Axis=new Vector3(.25f+Hash(eventCount*37),.2f,.65f).normalized;
            bottle.Initial=Quaternion.Euler(20,Hash(eventCount*41)*360,LastThrowSide*38);
            bottle.Rest=Quaternion.Euler(bottle.Kind==CrowdPropKind.FoamBall?0:90,Hash(eventCount*43)*360,0);
            float spin=bottle.Kind==CrowdPropKind.CrumpledPaper?240:bottle.Kind==CrowdPropKind.FoamBall?190:340;
            bottle.Spin=spin+Hash(eventCount*47)*280;bottle.Age=0;bottle.Opacity=1;bottle.Active=true;
            bottle.Transform.SetPositionAndRotation(bottle.Start,bottle.Initial);bottle.Transform.gameObject.SetActive(true);
            ThrowCount++;kindCounts[(int)bottle.Kind]++;if(LastThrowSide<0)LeftThrowCount++;else RightThrowCount++;
        }
        void SetAppearance(Bottle item,CrowdPropKind kind)
        {
            item.Kind=kind;
            item.Filter.sharedMesh=HasPropVariety?library.MeshFor(kind):bottleMesh;
            item.Renderer.sharedMaterial=HasPropVariety?library.MaterialFor(kind):bottleMaterial;
            item.Transform.localScale=Vector3.one;
        }
        static float RestHeight(CrowdPropKind kind)=>kind==CrowdPropKind.FoamBall?.15f:kind==CrowdPropKind.CrumpledPaper?.14f:kind==CrowdPropKind.PaperCup?.11f:GroundHeight;
        static float BounceVelocity(CrowdPropKind kind)=>kind==CrowdPropKind.FoamBall?3.0f:kind==CrowdPropKind.CrumpledPaper?.7f:kind==CrowdPropKind.PaperCup?1.3f:2.05f;

        public void ShiftOrigin(float amount)
        {
            if(float.IsNaN(amount)||float.IsInfinity(amount)||amount==0)return;
            originShift+=amount;pendingStart.z-=amount;pendingEnd.z-=amount;
            Vector3 origin=LastThrowOrigin,landing=LastLanding;origin.z-=amount;landing.z-=amount;LastThrowOrigin=origin;LastLanding=landing;
            foreach(var bottle in bottles)if(bottle!=null&&bottle.Active)
            {bottle.Start.z-=amount;bottle.End.z-=amount;bottle.Transform.position-=Vector3.forward*amount;}
            PublishCue();
        }

        static float Bounce(float t,float velocity)
        {
            float first=2*velocity/Gravity;
            if(t<first)return Mathf.Max(0,velocity*t-.5f*Gravity*t*t);
            t-=first;velocity*=.40f;float second=2*velocity/Gravity;
            return t<second?Mathf.Max(0,velocity*t-.5f*Gravity*t*t):0;
        }
        void PublishCue()
        {Shader.SetGlobalVector(ThrowOriginId,new Vector4(LastThrowOrigin.x,LastThrowOrigin.y,LastThrowOrigin.z,LastThrowSide));Shader.SetGlobalFloat(ThrowAgeId,LastThrowAge);}
        void Hide(Bottle bottle){bottle.Active=false;bottle.Opacity=0;if(bottle.Transform!=null)bottle.Transform.gameObject.SetActive(false);}
        void HideAll(){foreach(var bottle in bottles)if(bottle!=null)Hide(bottle);}
        float Hash(int value)
        {uint x=unchecked((uint)(value^seed))*747796405u+2891336453u;x=((x>>((int)(x>>28)+4))^x)*277803737u;return ((x>>22)^x)/(float)uint.MaxValue;}
#if UNITY_EDITOR
        public bool StartThrowForTest(int side)
        {return StartThrowForTest(side,CrowdPropKind.Bottle);}
        public bool StartThrowForTest(int side,CrowdPropKind kind)
        {if(!initialized||game==null||!game.AutomatedTest||game.State!=RunState.Running||(int)kind<0||(int)kind>3)return false;Schedule(side<0?-1:1,kind);nextEvent=clock+20f;return true;}
#endif
        void OnDisable(){HideAll();pending=false;LastThrowAge=-1;PublishCue();}
        void OnDestroy(){if(root!=null)Destroy(root.gameObject);}
    }
}
