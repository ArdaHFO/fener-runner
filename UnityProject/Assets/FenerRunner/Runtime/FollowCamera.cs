using UnityEngine;

namespace FenerRunner
{
    public sealed class FollowCamera : MonoBehaviour
    {
        [SerializeField] private RunnerSettings settings;
        [SerializeField] private Transform target;
        private Vector3 velocity;
        private float groundY;
        private RunnerMotor motor;
        private Camera lens;
        private bool menuPresentation;
        private float menuClock;
        private RunnerAnimation deathSubject;
        private LineRenderer landingGuide;
        private Material landingMaterial;
        private bool wasAirborne;
        private float jumpLookAhead,landingElapsed=.24f;
        private RunnerMultiplayer multiplayer;
        private Vector3 friendFraming;
        private ChaseSUV chase;
        private Vector3 chaseFraming;
        public Vector3 PredictedLandingPosition {get;private set;}
        public float RemainingAirSeconds {get;private set;}
        public bool LandingGuideVisible=>landingGuide!=null&&landingGuide.enabled;
        public float JumpLookAheadMeters=>jumpLookAhead;
        public bool MenuPresentation => menuPresentation;
        public void SetMultiplayer(RunnerMultiplayer value){multiplayer=value;}
        public void SetChase(ChaseSUV value){chase=value;}
        public void SetMenuPresentation(bool value){menuPresentation=value;menuClock=0;deathSubject=null;HideGuide();}
        public void BeginDeathPresentation(RunnerAnimation actor){menuPresentation=false;deathSubject=actor;velocity=Vector3.zero;HideGuide();}
        public void Configure(RunnerSettings value, Transform player) { settings = value; target = player;motor=player.GetComponent<RunnerMotor>();lens=GetComponent<Camera>(); }
        private Vector3 Anchor => new Vector3(target.position.x * settings.cameraLaneFollow, groundY, target.position.z);
        public void Snap()
        {
            if(Application.isPlaying)EnsureLandingGuide();
            if(lens==null)lens=GetComponent<Camera>();lens.fieldOfView=58;
            groundY = 0f;
            jumpLookAhead=0;landingElapsed=.24f;wasAirborne=false;RemainingAirSeconds=0;friendFraming=chaseFraming=Vector3.zero;HideGuide();
            velocity = Vector3.zero;
            if(menuPresentation){MenuPose(0,true);return;}
            transform.position = Anchor + settings.cameraOffset;
            Aim();
        }
        private void LateUpdate()
        {
            if (target == null) return;
            if(motor==null)motor=target.GetComponent<RunnerMotor>();if(lens==null)lens=GetComponent<Camera>();
            if(deathSubject!=null)
            {
                if(deathSubject.DeathPresentationPaused)return;
                Vector3 look=deathSubject.DeathFocusPosition;
                float t=1-Mathf.Exp(-Time.unscaledDeltaTime*5f);
                Vector3 position=look+new Vector3(.7f,2.2f,-5.2f);
                transform.position=Vector3.Lerp(transform.position,position,t);
                transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(look-transform.position,Vector3.up),t);
                lens.fieldOfView=Mathf.Lerp(lens.fieldOfView,53,t);return;
            }
            if(menuPresentation){menuClock+=Time.unscaledDeltaTime;MenuPose(Time.unscaledDeltaTime,false);return;}
            if(Time.timeScale==0f){HideGuide();return;}
            float dt=Time.deltaTime;
            bool airborne=!motor.Grounded&&!motor.Flying;
            if(wasAirborne&&motor.Grounded&&!motor.Sliding)landingElapsed=0;
            wasAirborne=airborne;
            landingElapsed=Mathf.Min(.24f,landingElapsed+dt);
            UpdateLandingGuide(airborne);
            // Read the ground ahead without chasing the skeleton or jolting the
            // phone camera. The guide is a current-speed projection, not a promise
            // that an occupied landing lane is safe.
            float anticipation=airborne?Mathf.Clamp(RemainingAirSeconds*motor.Speed*.065f,0,1.3f):0;
            jumpLookAhead=Mathf.Lerp(jumpLookAhead,anticipation,1-Mathf.Exp(-dt*9f));
            // Ordinary jumps keep the stable ground anchor. Sustained powered flight
            // lifts the framing smoothly so the bird and its high coin rail stay visible.
            float flightAnchor=motor.Flying?Mathf.Max(0,target.position.y-.35f):motor.JumpArcMultiplier>1?Mathf.Max(0,target.position.y-1.4f)*.85f:0;
            groundY=Mathf.Lerp(groundY,flightAnchor,1-Mathf.Exp(-Time.deltaTime*4f));
            lens.fieldOfView=Mathf.Lerp(lens.fieldOfView,Mathf.Lerp(58,68,Mathf.InverseLerp(settings.forwardSpeed,RunTempo.HardCap,motor.Speed)),1-Mathf.Exp(-Time.deltaTime*3));
            UpdateFriendFraming(dt);
            // A brief, eased lift reveals the pursuer below the runner's feet.
            // Multiplayer already widens the camera, so only fill its missing
            // space instead of stacking two independent zoom-outs.
            float presence=chase!=null?chase.Presence01:0;
            float portraitCompensation=Mathf.Clamp(.5625f/Mathf.Max(.35f,lens.aspect),1f,1.25f);
            float extraBack=3f+(Mathf.Abs(settings.cameraOffset.z)+3f)*(portraitCompensation-1f);
            float extraHeight=1.4f+(settings.cameraOffset.y+1.4f)*(portraitCompensation-1f);
            Vector3 chaseTarget=new Vector3(0,Mathf.Max(0,extraHeight-friendFraming.y),-Mathf.Max(0,extraBack+friendFraming.z))*presence;
            chaseFraming=Vector3.Lerp(chaseFraming,chaseTarget,1-Mathf.Exp(-dt*5f));
            if(chaseTarget==Vector3.zero&&chaseFraming.sqrMagnitude<.000001f)chaseFraming=Vector3.zero;
            Vector3 settle=Vector3.down*(.055f*Mathf.Sin(Mathf.PI*landingElapsed/.24f));
            Vector3 follow=Vector3.SmoothDamp(transform.position, Anchor + settings.cameraOffset+settle+friendFraming+chaseFraming,
                ref velocity, settings.cameraSmoothSeconds, Mathf.Infinity, Time.deltaTime);
            // Forward movement is already integrated by the motor. Smoothing it
            // again creates speed-dependent distance (6.3 m of extra lag at42).
            // Retain soft lane/height framing without shrinking the fast runner.
            follow.z=Anchor.z+settings.cameraOffset.z+friendFraming.z+chaseFraming.z;velocity.z=0;transform.position=follow;
            Aim();
        }
        private void Aim()
        {
            // Skeleton motion never reaches the rig; only sustained flight raises its anchor.
            Vector3 direction = Anchor + settings.cameraLookOffset + Vector3.forward*jumpLookAhead + Vector3.right*friendFraming.x - transform.position;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
        private void UpdateFriendFraming(float dt)
        {
            Vector3 desired=Vector3.zero;
            if(multiplayer!=null&&multiplayer.HasPeer&&multiplayer.Ghost!=null&&multiplayer.Ghost.IsVisible&&multiplayer.RemotePose!=GhostPose.Dead)
            {
                Vector3 peer=multiplayer.Ghost.VisualRoot.position;
                float gap=peer.z-target.position.z;
                float near=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,16,Mathf.Abs(gap)));
                // A portrait camera cannot fit a neighbouring runner with the
                // solo chase distance, especially behind the interpolation delay.
                // Frame both nearby runners without moving either world position.
                float narrow=Mathf.Clamp(.5625f/Mathf.Max(.35f,lens.aspect),1,1.4f);
                float back=Mathf.Clamp((4+Mathf.Abs(peer.x-target.position.x)*1.1f+Mathf.Max(0,-gap)*.55f)*narrow,4,14);
                desired=new Vector3((peer.x+target.position.x)*.5f-Anchor.x,back*.32f,-back)*near;
            }
            friendFraming=Vector3.Lerp(friendFraming,desired,1-Mathf.Exp(-dt*4));
            if(desired==Vector3.zero&&friendFraming.sqrMagnitude<.000001f)friendFraming=Vector3.zero;
        }
        private void MenuPose(float dt,bool snap)
        {
            // A restrained showcase orbit keeps the whole mascot inside the mobile
            // hero window. It never rotates the runner or moves its collision root.
            Vector3 centre=target.position;
            Vector3 position=centre+new Vector3(.36f+Mathf.Sin(menuClock*.31f)*.18f,2.25f,-6.25f);
            Vector3 look=centre+new Vector3(0,1.42f,0);
            float t=snap?1f:1-Mathf.Exp(-dt*5f);
            lens.fieldOfView=Mathf.Lerp(lens.fieldOfView,48f,t);
            transform.position=Vector3.Lerp(transform.position,position,t);
            transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(look-transform.position,Vector3.up),t);
        }
        private void UpdateLandingGuide(bool airborne)
        {
            if(!airborne){RemainingAirSeconds=0;HideGuide();return;}
            float height=Mathf.Max(0,target.position.y),v=motor.VerticalVelocity;
            float rise=v>0?v/-settings.Gravity:0;
            float apex=height+(v>0?v*v/(-2*settings.Gravity):0);
            float gravity=Mathf.Max(.01f,-settings.FallGravity);
            RemainingAirSeconds=rise+(v>0?Mathf.Sqrt(2*apex/gravity):(v+Mathf.Sqrt(v*v+2*gravity*height))/gravity);
            PredictedLandingPosition=new Vector3(motor.LanePositionAfter(RemainingAirSeconds),.035f,target.position.z+motor.Speed*RemainingAirSeconds);
            if(landingGuide==null)return;
            float opacity=Mathf.Clamp01(height/.35f)*.76f;
            Color color=new Color(1,.79f,.18f,opacity);landingGuide.startColor=landingGuide.endColor=color;
            landingGuide.SetPosition(0,PredictedLandingPosition+new Vector3(-.52f,0,-.38f));
            landingGuide.SetPosition(1,PredictedLandingPosition+new Vector3(0,0,.42f));
            landingGuide.SetPosition(2,PredictedLandingPosition+new Vector3(.52f,0,-.38f));
            landingGuide.enabled=opacity>.01f;
        }
        private void EnsureLandingGuide()
        {
            if(landingGuide==null)
            {
                // This shader is already retained by the scene's serialized rain
                // material. Allocate once at session setup, never on the jump path.
                Shader shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if(shader==null)return;
                landingMaterial=new Material(shader){name="Runtime landing guide",renderQueue=3000};
                landingMaterial.SetFloat("_Surface",1);landingMaterial.SetFloat("_ZWrite",0);
                landingMaterial.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                landingMaterial.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                landingMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                landingMaterial.SetColor("_BaseColor",Color.white);
                var guide=new GameObject("Projected jump landing",typeof(LineRenderer));guide.transform.SetParent(transform,false);
                landingGuide=guide.GetComponent<LineRenderer>();landingGuide.sharedMaterial=landingMaterial;
                landingGuide.useWorldSpace=true;landingGuide.positionCount=3;landingGuide.numCapVertices=2;landingGuide.numCornerVertices=2;
                landingGuide.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;landingGuide.receiveShadows=false;
                landingGuide.startWidth=landingGuide.endWidth=.075f;
                landingGuide.enabled=false;
            }
        }
        private void HideGuide(){if(landingGuide!=null)landingGuide.enabled=false;}
        private void OnDestroy(){if(landingMaterial!=null)Destroy(landingMaterial);}
        public void ShiftOrigin(float amount) { transform.position -= Vector3.forward * amount; }
    }
}
