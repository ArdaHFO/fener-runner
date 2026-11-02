using UnityEngine;

namespace FenerRunner
{
    public sealed class TokenCourse : MonoBehaviour
    {
        [SerializeField] RunnerSettings settings;
        [SerializeField] GameObject prefab;
        Transform[] pool;
        Renderer[][] renderers;
        bool[] collected;
        float[] baseHeights;
        float[] flightOffsets;
        bool[] attracted, routeBound;
        Vector3[] tokenStart,bonusStart;
        internal struct CapsulePose { public Vector3 root,bottom,top,up; public float radius; }
        CapsulePose moveStart;
        RunnerMotor sampledMotor;
        bool hasMoveStart,enabledTokens=true;
        float coinRadius=.38f;
        float nextZ, clock;
        const int BonusCapacity=20;
        Transform[] bonusPool;
        Renderer[][] bonusRenderers;
        bool[] bonusActive;
        bool[] bonusAttracted;
        float[] bonusTargets;
        int nextBonus;
        HenBonusCourse hens;
        public HenBonusCourse Hens=>hens;
        // Pickup width follows the visible wing/lean silhouette; obstacle contact
        // remains the original narrow capsule. This never expands vertical reach.
        public float CollectionReachBonus {get;set;}
        public int HenNestLevel {get;set;}
        public event System.Action HenCollected;
        public int BonusPoolCount=>bonusPool==null?0:bonusPool.Length;
        public int BonusSpawned{get;private set;}
        public int BonusCollected{get;private set;}
        public int BonusSkipped{get;private set;}
        public int TotalCollected{get;private set;}
        public float PickupRadius=>coinRadius;
        public int PoolCount => pool == null ? 0 : pool.Length;
        public bool Enabled { get=>enabledTokens;set{enabledTokens=value;hasMoveStart=false;sampledMotor=null;} }
        public float MagnetRadius {get;set;}
        public bool LaneSweepActive {get;set;}
        public void Configure(RunnerSettings s, GameObject token) { settings = s; prefab = token; }
        public void ResetCourse(ObstacleCourse obstacles)
        {
            if (pool == null)
            {
                pool = new Transform[48]; renderers = new Renderer[48][]; collected = new bool[48]; baseHeights = new float[48];
                flightOffsets=new float[48];attracted=new bool[48];routeBound=new bool[48];tokenStart=new Vector3[48];
                coinRadius=MeasureCoinRadius();
                for (int i = 0; i < pool.Length; i++)
                { pool[i] = Instantiate(prefab, transform).transform; renderers[i] = pool[i].GetComponentsInChildren<Renderer>(); }
            }
            nextZ = 24; clock = 0;MagnetRadius=0;LaneSweepActive=false;
            hasMoveStart=false;sampledMotor=null;TotalCollected=0;
            for (int i = 0; i < pool.Length; i++) Place(i, obstacles);
            if(bonusPool==null)
            {
                bonusPool=new Transform[BonusCapacity];bonusRenderers=new Renderer[BonusCapacity][];
                bonusActive=new bool[BonusCapacity];bonusAttracted=new bool[BonusCapacity];bonusTargets=new float[BonusCapacity];bonusStart=new Vector3[BonusCapacity];
                for(int i=0;i<BonusCapacity;i++){bonusPool[i]=Instantiate(prefab,transform).transform;bonusPool[i].name="Rain coin "+i;bonusRenderers[i]=bonusPool[i].GetComponentsInChildren<Renderer>();}
            }
            nextBonus=BonusSpawned=BonusCollected=BonusSkipped=0;
            for(int i=0;i<BonusCapacity;i++)SetBonusVisible(i,false);
            if(hens==null)hens=new HenBonusCourse(transform,prefab);
            hens.Reset(obstacles);
        }
        void SetBonusVisible(int i,bool value){bonusActive[i]=value;bonusAttracted[i]=false;foreach(var r in bonusRenderers[i])r.enabled=value;}
        public void SpawnBonusVolley(RunnerMotor motor,int count,ObstacleCourse obstacles)
        {
            if(!Enabled||bonusPool==null)return;
            // Permanent rain/trail levels and the flock combo request up to9.
            // The fixed pool still refuses to replace any active visible chick.
            int requested=Mathf.Clamp(count,0,9);
            for(int n=0;n<requested;n++)
            {
                int i=-1;
                for(int search=0;search<BonusCapacity;search++)
                {int candidate=(nextBonus+search)%BonusCapacity;if(!bonusActive[candidate]){i=candidate;nextBonus=(candidate+1)%BonusCapacity;break;}}
                // Coin rain and a lane-change reward may overlap. Existing visible
                // coins retain their identities until collected or retired.
                if(i<0){BonusSkipped+=requested-n;break;}
                float z=motor.transform.position.z+10+n*1.8f;
                Vector3 route=obstacles.RewardPositionAt(z,motor.Speed);
                float target=motor.Flying?motor.transform.position.y+.95f:route.y;
                bonusTargets[i]=target;
                bonusPool[i].position=new Vector3(route.x,target+2.5f,z);
                bonusPool[i].rotation=ChickRotation(clock,i+48);
                bonusStart[i]=bonusPool[i].position;
                SetBonusVisible(i,true);BonusSpawned++;
            }
        }
        void Place(int i, ObstacleCourse obstacles)
        {
            Vector3 position = obstacles.RewardPositionAt(nextZ);
            pool[i].position = position; baseHeights[i] = position.y;
            pool[i].rotation=ChickRotation(clock,i);
            tokenStart[i]=position;
            collected[i] = false;routeBound[i]=true;flightOffsets[i]=0;attracted[i]=false;foreach (var renderer in renderers[i]) renderer.enabled = Enabled;
            nextZ += 4.4f;
        }

        float MeasureCoinRadius()
        {
            // Cache the visible mesh envelope once. The chick is normalized to
            // the previous disc's exact radius, preserving collection fairness.
            // Four centimetres of forgiveness cannot reach another lane.
            float radius=0;
            foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null)continue;
                if(!filter.sharedMesh.isReadable)
                {radius=Mathf.Max(radius,filter.sharedMesh.bounds.center.magnitude+filter.sharedMesh.bounds.extents.magnitude);continue;}
                foreach(Vector3 vertex in filter.sharedMesh.vertices)
                    radius=Mathf.Max(radius,prefab.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)).magnitude);
            }
            Vector3 scale=prefab.transform.lossyScale;
            return Mathf.Max(.1f,radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Max(Mathf.Abs(scale.y),Mathf.Abs(scale.z))))+.04f;
        }

        static CapsulePose ReadCapsule(RunnerMotor motor)
        {
            var capsule=motor.Capsule;Vector3 scale=motor.transform.lossyScale;
            float radius=capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
            float height=Mathf.Max(radius*2,capsule.height*Mathf.Abs(scale.y));
            Vector3 up=motor.transform.up,centre=motor.transform.TransformPoint(capsule.center);
            Vector3 offset=up*(height*.5f-radius);
            return new CapsulePose{root=motor.transform.position,bottom=centre-offset,top=centre+offset,up=up,radius=radius};
        }

        // Called before each real controller move. Coin motion is sampled at the same
        // time so a magnet, falling token or jump cannot pass between end-point tests.
        public void BeginMove(RunnerMotor motor)
        {
            hasMoveStart=Enabled&&motor!=null&&motor.Capsule!=null&&motor.Capsule.enabled;
            sampledMotor=motor;if(!hasMoveStart)return;
            moveStart=ReadCapsule(motor);
            if(pool!=null)for(int i=0;i<pool.Length;i++)tokenStart[i]=pool[i].position;
            if(bonusPool!=null)for(int i=0;i<bonusPool.Length;i++)bonusStart[i]=bonusPool[i].position;
        }

        static bool ClipPositive(float start,float end,ref float low,ref float high)
        {
            float change=end-start;
            if(Mathf.Abs(change)<.000001f)return start>=0;
            float crossing=-start/change;
            if(change>0)low=Mathf.Max(low,crossing);else high=Mathf.Min(high,crossing);
            return low<=high;
        }
        static float MinimumSquared(Vector3 start,Vector3 end,float low,float high)
        {
            Vector3 delta=end-start;
            float t=delta.sqrMagnitude>.0000001f?Mathf.Clamp(-Vector3.Dot(start,delta)/delta.sqrMagnitude,low,high):low;
            return (start+delta*t).sqrMagnitude;
        }
        bool EntersMagnet(Vector3 from,Vector3 to,CapsulePose before,CapsulePose after,out float entry)
        {
            Vector3 a=from-before.root,b=to-after.root;float exit=1;entry=0;
            return ClipPositive(a.x+MagnetRadius,b.x+MagnetRadius,ref entry,ref exit)&&
                ClipPositive(MagnetRadius-a.x,MagnetRadius-b.x,ref entry,ref exit)&&
                ClipPositive(a.z+1,b.z+1,ref entry,ref exit)&&ClipPositive(14-a.z,14-b.z,ref entry,ref exit)&&
                ClipPositive(a.y+8,b.y+8,ref entry,ref exit)&&ClipPositive(8-a.y,8-b.y,ref entry,ref exit);
        }
        internal bool CrossesReward(Vector3 from,Vector3 to,CapsulePose before,CapsulePose after,float maximumTime,float rewardRadius,bool allowLaneSweep=true,float minimumTime=0)
        {
            float low=Mathf.Clamp01(minimumTime),high=Mathf.Clamp01(maximumTime),radius=Mathf.Max(before.radius,after.radius)+rewardRadius;
            if(low>high)return false;
            bool laneSweep=LaneSweepActive&&allowLaneSweep;
            if(laneSweep)
            {
                float edge=settings.laneSpacing+coinRadius;
                if(!ClipPositive(from.x+edge,to.x+edge,ref low,ref high)||
                    !ClipPositive(edge-from.x,edge-to.x,ref low,ref high))return false;
            }
            Vector3 lower0=from-before.bottom,lower1=to-after.bottom;
            Vector3 upper0=from-before.top,upper1=to-after.top;
            if(laneSweep){lower0.x=lower1.x=upper0.x=upper1.x=0;}
            else
            {
                // The bird's animated sleeves extend beyond the .38m damage
                // capsule. Stretch only the collection shape laterally, including
                // its continuous sweep, so a visible graze during a swipe counts.
                float lateralScale=radius/(radius+.20f+Mathf.Clamp(CollectionReachBonus,0,.18f));
                lower0.x*=lateralScale;lower1.x*=lateralScale;upper0.x*=lateralScale;upper1.x*=lateralScale;
            }
            float square=radius*radius;
            // Each sphere cap is a relative segment against a fixed sphere.
            if(MinimumSquared(lower0,lower1,low,high)<=square||MinimumSquared(upper0,upper1,low,high)<=square)return true;
            // The middle cylinder is considered only while the coin is between the
            // two cap centres. This keeps slide/jump height changes time-aligned.
            float lowerY0=Vector3.Dot(lower0,before.up),lowerY1=Vector3.Dot(lower1,before.up);
            float upperY0=-Vector3.Dot(upper0,before.up),upperY1=-Vector3.Dot(upper1,before.up);
            if(!ClipPositive(lowerY0,lowerY1,ref low,ref high)||!ClipPositive(upperY0,upperY1,ref low,ref high))return false;
            return MinimumSquared(lower0-before.up*lowerY0,lower1-before.up*lowerY1,low,high)<=square;
        }

        public int Tick(RunnerMotor motor, ObstacleCourse obstacles, float dt,float maxContactTime=1f)
        {
            if (!Enabled||motor==null||motor.Capsule==null||!motor.Capsule.enabled){hasMoveStart=false;return 0;}
            dt=Mathf.Max(0,dt);
            CapsulePose end=ReadCapsule(motor);
            bool sampled=hasMoveStart&&sampledMotor==motor;
            CapsulePose start=sampled?moveStart:end;
            clock += dt; int picked = 0; Vector3 p = motor.transform.position;
            for (int i = 0; i < pool.Length; i++)
            {
                var token = pool[i]; Vector3 position = token.position;
                Vector3 from=sampled?tokenStart[i]:position;
                // Keep the authored rail aligned with the actual jump length at higher speeds.
                // Test placements and magnet-owned tokens retain their explicit trajectory.
                if(routeBound[i]&&!collected[i]&&!attracted[i]&&position.z>p.z-10f&&position.z<p.z+80f)
                    baseHeights[i]=Mathf.MoveTowards(baseHeights[i],obstacles.RewardPositionAt(position.z,motor.Speed).y,dt*8f);
                float flightOffset=motor.FlightRequested&&position.z-p.z<70?5.92f-baseHeights[i]:0;
                flightOffsets[i]=Mathf.MoveTowards(flightOffsets[i],flightOffset,dt*8f);
                if(!attracted[i])position.y = baseHeights[i]+flightOffsets[i]+Mathf.Sin(clock * 3 + i * .6f) * .08f;
                float attractionTime=dt;
                if(!collected[i]&&!attracted[i]&&MagnetRadius>0&&EntersMagnet(from,position,start,end,out float entry))
                {attracted[i]=true;attractionTime*=1-entry;}
                if(!collected[i]&&attracted[i])position=Vector3.MoveTowards(position,p+Vector3.up*.95f,attractionTime*42f);
                token.position = position;
                token.rotation = ChickRotation(clock,i);
                if (!collected[i] && CrossesReward(from,position,start,end,maxContactTime,coinRadius))
                { collected[i] = true; foreach (var renderer in renderers[i]) renderer.enabled = false; picked++; }
                if (position.z < p.z - 12) Place(i, obstacles);
            }
            for(int i=0;i<BonusCapacity;i++)
            {
                if(!bonusActive[i])continue;
                var item=bonusPool[i];Vector3 position=item.position;
                Vector3 from=sampled?bonusStart[i]:position;
                position.y=Mathf.MoveTowards(position.y,bonusTargets[i],dt*10f);
                float attractionTime=dt;
                if(!bonusAttracted[i]&&MagnetRadius>0&&EntersMagnet(from,position,start,end,out float entry))
                {bonusAttracted[i]=true;attractionTime*=1-entry;}
                if(bonusAttracted[i])position=Vector3.MoveTowards(position,p+Vector3.up*.95f,attractionTime*42f);
                item.position=position;item.rotation=ChickRotation(clock,i+48);
                if(CrossesReward(from,position,start,end,maxContactTime,coinRadius))
                {SetBonusVisible(i,false);picked++;BonusCollected++;}
                else if(position.z<p.z-12)SetBonusVisible(i,false);
            }
            if(hens!=null)
            {
                int previous=hens.HensCollected;
                picked+=hens.Tick(this,motor,obstacles,start,end,dt,maxContactTime,clock,coinRadius,HenNestLevel);
                if(hens.HensCollected>previous)HenCollected?.Invoke();
            }
            TotalCollected+=picked;
            BeginMove(motor);
            return picked;
        }
        public void ShiftOrigin(float amount)
        {
            Vector3 shift=Vector3.forward*amount;nextZ-=amount;
            if(hasMoveStart){moveStart.root-=shift;moveStart.bottom-=shift;moveStart.top-=shift;}
            if(pool!=null)for(int i=0;i<pool.Length;i++){pool[i].position-=shift;tokenStart[i]-=shift;}
            if(bonusPool!=null)for(int i=0;i<bonusPool.Length;i++){bonusPool[i].position-=shift;bonusStart[i]-=shift;}
            hens?.ShiftOrigin(amount);
        }
        static Quaternion ChickRotation(float time,int index)
        {
            float phase=time*3+index*.6f;
            return Quaternion.Euler(Mathf.Sin(phase*.83f)*4,Mathf.Sin(phase*.61f)*13,Mathf.Sin(phase)*7);
        }
#if UNITY_EDITOR
        public Vector3 BonusTargetForTest(int index)=>new Vector3(bonusPool[index].position.x,bonusTargets[index],bonusPool[index].position.z);
        public void PlaceForTest(int index, Vector3 position)
        { pool[index].position = position;tokenStart[index]=position; baseHeights[index] = position.y;routeBound[index]=false;collected[index] = false;flightOffsets[index]=0;attracted[index]=false;foreach (var renderer in renderers[index]) renderer.enabled = true; }
        public Vector3 PositionForTest(int index)=>pool[index].position;
        public bool CollectedForTest(int index)=>collected[index];
        public Vector3 BonusPositionForTest(int index)=>bonusPool[index].position;
        public bool BonusActiveForTest(int index)=>bonusActive[index];
        public void PlaceBonusForTest(int index,Vector3 position,float targetHeight)
        {bonusPool[index].position=bonusStart[index]=position;bonusTargets[index]=targetHeight;SetBonusVisible(index,true);}
#endif
    }
}
