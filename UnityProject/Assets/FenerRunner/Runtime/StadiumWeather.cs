using UnityEngine;

namespace FenerRunner
{
    public sealed class StadiumWeather : MonoBehaviour
    {
        [SerializeField] Light sun;
        [SerializeField] Material skyTemplate,rainMaterial;
        RunnerGame game;Material sky;ParticleSystem rain;float clock;
        int appliedPrevious=-1,appliedCurrent=-1;float appliedBlend=-1;
        static readonly int ZenithColor=Shader.PropertyToID("_ZenithColor"),HorizonColor=Shader.PropertyToID("_HorizonColor"),CloudCover=Shader.PropertyToID("_CloudCover"),Night=Shader.PropertyToID("_Night");
        static readonly Color[] zenith={new Color(.15f,.34f,.58f),new Color(.12f,.18f,.33f),new Color(.19f,.25f,.33f),new Color(.025f,.055f,.14f)};
        static readonly Color[] horizon={new Color(.69f,.82f,.91f),new Color(.91f,.51f,.30f),new Color(.48f,.57f,.64f),new Color(.18f,.26f,.43f)};
        static readonly Color[] sunlight={new Color(1,.97f,.91f),new Color(1,.80f,.65f),new Color(.78f,.87f,1),new Color(.69f,.79f,1)};
        static readonly float[] intensity={1.22f,1.08f,.75f,.62f},cloud={.15f,.12f,.9f,.20f};
        static readonly string[] labels={"AÇIK HAVA","GÜN BATIMI","YAĞMUR","GECE MAÇI"};
        public float Clock=>clock;
        public float RainAmount{get;private set;}
        public string WeatherName{get;private set;}="AÇIK HAVA";
        public int MaxRainParticles=>320;
        public ParticleSystem Rain=>rain;
        // Counts real material/light/fog writes, not calls that reuse a settled state.
        public int AppliedStateCount{get;private set;}
        public void Configure(Light light,Material skySource,Material rainSource){sun=light;skyTemplate=skySource;rainMaterial=rainSource;}
        void Ensure()
        {
            if(sky!=null)return;
            game=GetComponent<RunnerGame>();sky=new Material(skyTemplate);RenderSettings.skybox=sky;
            var go=new GameObject("Local rain pool",typeof(ParticleSystem));go.transform.SetParent(transform,false);
            rain=go.GetComponent<ParticleSystem>();rain.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=rain.main;main.loop=true;main.playOnAwake=false;main.startLifetime=.8f;main.startSpeed=0;main.maxParticles=MaxRainParticles;
            main.startSize3D=true;main.startSizeX=.012f;main.startSizeY=.36f;main.startSizeZ=.012f;
            main.startColor=new Color(.73f,.85f,1,.34f);main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var emission=rain.emission;emission.rateOverTime=0;
            var shape=rain.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(12,1,32);
            var velocity=rain.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;
            velocity.x=-1f;velocity.y=-20f;velocity.z=-3f;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=rainMaterial;
            renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            rain.Play();
        }
        public void ResetAtmosphere(){Ensure();clock=0;rain.Clear();appliedPrevious=-1;Evaluate();}
        public void Seek(float seconds){Ensure();clock=Mathf.Max(0,seconds);Evaluate();}
        void LateUpdate()
        {
            Ensure();
            if(game.State==RunState.Running)clock+=Time.deltaTime;
            Vector3 anchor=game.Motor.transform.position+new Vector3(0,10,17);
            if(rain.transform.position!=anchor)rain.transform.position=anchor;
            Evaluate();
        }
        void Evaluate()
        {
            int current=Mathf.FloorToInt(clock/45f)%4,previous=(current+3)%4;
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((clock%45f)/12f));
            if(clock<45){previous=0;current=0;t=1;}
            // Each plateau is visually constant. Menu/pause/finished frames and
            // settled weather must not dirty global rendering state every frame.
            if(previous==appliedPrevious&&current==appliedCurrent&&t==appliedBlend)return;
            appliedPrevious=previous;appliedCurrent=current;appliedBlend=t;AppliedStateCount++;
            sky.SetColor(ZenithColor,Color.Lerp(zenith[previous],zenith[current],t));
            sky.SetColor(HorizonColor,Color.Lerp(horizon[previous],horizon[current],t));
            sky.SetFloat(CloudCover,Mathf.Lerp(cloud[previous],cloud[current],t));
            sky.SetFloat(Night,Mathf.Lerp(previous==3?1:0,current==3?1:0,t));
            sun.color=Color.Lerp(sunlight[previous],sunlight[current],t);sun.intensity=Mathf.Lerp(intensity[previous],intensity[current],t);
            RenderSettings.ambientSkyColor=Color.Lerp(zenith[previous]*.4f+Color.gray*.7f,zenith[current]*.4f+Color.gray*.7f,t);
            RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.49f,.54f,.60f),new Color(.34f,.41f,.55f),Mathf.Lerp(previous==3?1:0,current==3?1:0,t));
            RenderSettings.fogColor=Color.Lerp(horizon[previous],horizon[current],t)*.72f;
            RainAmount=Mathf.Lerp(previous==2?1:0,current==2?1:0,t);
            RenderSettings.fogStartDistance=Mathf.Lerp(115,82,RainAmount);RenderSettings.fogEndDistance=Mathf.Lerp(210,174,RainAmount);
            var emission=rain.emission;emission.rateOverTime=RainAmount*280;
            WeatherName=labels[t>.5f?current:previous];
            if(game.Interface!=null)game.Interface.SetAtmosphere(WeatherName);
        }
        void OnDestroy(){if(sky!=null){if(RenderSettings.skybox==sky)RenderSettings.skybox=skyTemplate;Destroy(sky);}}
    }
}
