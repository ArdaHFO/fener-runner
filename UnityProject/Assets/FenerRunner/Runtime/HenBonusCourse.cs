using UnityEngine;

namespace FenerRunner
{
    // One rare hen and a separate, fixed18-chick burst pool. It cannot steal an
    // existing coin-rain token or write the wallet; every chick still needs contact.
    public sealed class HenBonusCourse
    {
        public const int Capacity=18;
        readonly Transform hen,leftWing,rightWing;
        readonly Renderer[] henRenderers;
        readonly Transform[] chicks=new Transform[Capacity];
        readonly Renderer[][] renderers=new Renderer[Capacity][];
        readonly Vector3[] launch=new Vector3[Capacity],target=new Vector3[Capacity];
        readonly float[] age=new float[Capacity];
        readonly bool[] active=new bool[Capacity],attracted=new bool[Capacity];
        bool available,testPlacement;
        float nextZ,baseHeight;
        int sequence;
        public int HensCollected {get;private set;}
        public int ChicksSpawned {get;private set;}
        public int ChicksCollected {get;private set;}
        public int ActiveCount {get;private set;}
        public bool Available=>available;
        public Vector3 Position=>hen.position;
        public int PoolCount=>chicks.Length;
        public int HenPoolCount=>1;
        public float CollectionRadius=>.57f;

        public HenBonusCourse(Transform parent,GameObject chickPrefab)
        {
            GameObject asset=Resources.Load<GameObject>("HenBonus");
            if(asset==null)throw new System.InvalidOperationException("HenBonus resource is missing. Prepare v20 assets first.");
            hen=Object.Instantiate(asset,parent).transform;hen.name="Rare golden hen";
            henRenderers=hen.GetComponentsInChildren<Renderer>(true);
            leftWing=hen.Find("Left wing");rightWing=hen.Find("Right wing");
            for(int i=0;i<Capacity;i++)
            {
                chicks[i]=Object.Instantiate(chickPrefab,parent).transform;chicks[i].name="Hen burst chick "+i;
                renderers[i]=chicks[i].GetComponentsInChildren<Renderer>(true);SetActive(i,false);
            }
        }
        public void Reset(ObstacleCourse obstacles)
        {
            HensCollected=ChicksSpawned=ChicksCollected=ActiveCount=sequence=0;
            for(int i=0;i<Capacity;i++)SetActive(i,false);
            nextZ=166;Place(obstacles);
        }
        void Place(ObstacleCourse obstacles)
        {
            hen.position=obstacles.RewardPositionAt(nextZ);baseHeight=hen.position.y;
            available=true;testPlacement=false;SetHenVisible(true);
            nextZ+=230+(sequence++*47%83);
        }
        void SetHenVisible(bool value){foreach(var renderer in henRenderers)renderer.enabled=value;}
        void SetActive(int i,bool value)
        {active[i]=value;attracted[i]=false;age[i]=0;foreach(var renderer in renderers[i])renderer.enabled=value;}

        internal int Tick(TokenCourse owner,RunnerMotor motor,ObstacleCourse obstacles,TokenCourse.CapsulePose before,
            TokenCourse.CapsulePose after,float dt,float maximumTime,float clock,float chickRadius,int nestLevel)
        {
            int collected=0;Vector3 p=motor.transform.position;
            for(int i=0;i<Capacity;i++)
            {
                if(!active[i])continue;
                Vector3 from=chicks[i].position,to;
                float previousAge=age[i];age[i]+=dt;float duration=.42f+(i%3)*.035f,t=Mathf.Clamp01(age[i]/duration);
                if(!attracted[i])
                {
                    to=Vector3.Lerp(launch[i],target[i],Mathf.SmoothStep(0,1,t));
                    // A spreading fan then a short landing arc keeps the entire
                    // reward on the safe rail instead of scattering into barriers.
                    to+=new Vector3(Mathf.Sin(t*Mathf.PI)*(i%2==0?-1:1)*(.42f+i%3*.12f),Mathf.Sin(t*Mathf.PI)*1.7f,0);
                    if(t>=1)to.y+=Mathf.Sin(clock*3+i*.6f)*.08f;
                }
                else to=from;
                if(owner.MagnetRadius>0&&Mathf.Abs(to.x-p.x)<owner.MagnetRadius&&to.z-p.z>-1&&to.z-p.z<14&&Mathf.Abs(to.y-p.y)<8)attracted[i]=true;
                if(attracted[i])to=Vector3.MoveTowards(to,p+Vector3.up*.95f,dt*42);
                chicks[i].position=to;chicks[i].rotation=Quaternion.Euler(Mathf.Sin(clock*5+i)*6,Mathf.Sin(clock*3+i)*15,Mathf.Sin(clock*4+i)*9);
                // Let the hatch fan emerge before collection. Without this brief
                // phase it passes through a nearby runner and disappears before
                // the player sees the scatter. Clip eligibility within a coarse
                // frame so a pre-emergence touch is never credited retroactively.
                const float emergenceSeconds=.24f;
                float eligibleFrom=dt>0?Mathf.Clamp01((emergenceSeconds-previousAge)/dt):0;
                if(age[i]>=emergenceSeconds&&owner.CrossesReward(from,to,before,after,maximumTime,chickRadius,true,eligibleFrom))
                {SetActive(i,false);ChicksCollected++;collected++;}
                else if(to.z<p.z-12)SetActive(i,false);
            }
            Vector3 previous=hen.position;
            if(available)
            {
                if(!testPlacement&&hen.position.z>p.z-10&&hen.position.z<p.z+80)
                    baseHeight=Mathf.MoveTowards(baseHeight,obstacles.RewardPositionAt(hen.position.z,motor.Speed).y,dt*8);
                Vector3 position=hen.position;position.y=baseHeight+Mathf.Sin(clock*2.4f)*.11f;hen.position=position;
                hen.rotation=Quaternion.Euler(0,Mathf.Sin(clock*1.7f)*16,Mathf.Sin(clock*2.4f)*4);
                if(leftWing!=null)leftWing.localRotation=Quaternion.Euler(0,0,-16-Mathf.Sin(clock*7)*13);
                if(rightWing!=null)rightWing.localRotation=Quaternion.Euler(0,0,16+Mathf.Sin(clock*7)*13);
                // A wide sweeping power still needs physical contact with the hen.
                if(owner.CrossesReward(previous,hen.position,before,after,maximumTime,CollectionRadius,false))
                {
                    available=false;SetHenVisible(false);HensCollected++;
                    Burst(hen.position,motor,obstacles,12+2*Mathf.Clamp(nestLevel,0,3));
                }
            }
            if(hen.position.z<p.z-12)Place(obstacles);
            ActiveCount=0;for(int i=0;i<Capacity;i++)if(active[i])ActiveCount++;
            return collected;
        }
        void Burst(Vector3 origin,RunnerMotor motor,ObstacleCourse obstacles,int count)
        {
            // Scatter begins above the hen, then flies ahead of the runner. New
            // chicks are evaluated on the NEXT movement, never retroactively along
            // the segment that broke the hen (important at a fatal contact).
            int spawned=0;
            for(int i=0;i<Capacity&&spawned<count;i++)
            {
                if(active[i])continue;
                // At42m/s a fixed16m lead would be overtaken while the fan is
                // still opening. Add a speed-dependent lead so the emergence
                // window never leaves the first chick behind the runner.
                float z=motor.transform.position.z+16+Mathf.Max(0,motor.Speed)*.3f+spawned*1.35f;
                target[i]=obstacles.RewardPositionAt(z,motor.Speed);
                if(motor.Flying)target[i].y=motor.transform.position.y+.95f;
                launch[i]=origin+new Vector3((spawned%3-1)*.16f,.22f+(spawned%2)*.15f,.2f);
                chicks[i].position=launch[i];SetActive(i,true);ChicksSpawned++;spawned++;
            }
        }
        public void ShiftOrigin(float amount)
        {
            Vector3 offset=Vector3.forward*amount;nextZ-=amount;hen.position-=offset;
            for(int i=0;i<Capacity;i++){chicks[i].position-=offset;launch[i]-=offset;target[i]-=offset;}
        }
#if UNITY_EDITOR
        public void PlaceForTest(Vector3 position)
        {hen.position=position;baseHeight=position.y;available=true;testPlacement=true;SetHenVisible(true);}
        public bool ChickActiveForTest(int i)=>active[i];
        public Vector3 ChickPositionForTest(int i)=>chicks[i].position;
        public Vector3 ChickTargetForTest(int i)=>target[i];
        public void PlaceChickForTest(int i,Vector3 position)
        {launch[i]=target[i]=position;chicks[i].position=position;SetActive(i,true);age[i]=1;}
        public void HideForTest(){available=false;SetHenVisible(false);}
#endif
    }
}
