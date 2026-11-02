using UnityEngine;

namespace FenerRunner
{
    public sealed class ObstacleCourse : MonoBehaviour
    {
        [SerializeField] private RunnerSettings settings;
        [SerializeField] private RunnerObstacle jumpPrefab, slidePrefab;
        public const int RowCapacity = 10;
        public const int ObstacleCapacity = RowCapacity * 6;
        private sealed class LiveRow
        {
            public PatternRow Pattern;
            public float Z;
            public bool Scored, Spoiled;
        }
        private RunnerObstacle[] pool;
        private LiveRow[] rows;
        private CoursePatternGenerator generator;
        private double originOffset;
        private int restartCount;
        private readonly Collider[] overlaps = new Collider[128];
        private readonly float[] sweepBreaks = new float[8];
        private readonly RunnerObstacle[] contacts = new RunnerObstacle[ObstacleCapacity + 8];
        private readonly float[] contactTimes = new float[ObstacleCapacity + 8];
        private readonly bool[] contactEndsOverlapping = new bool[ObstacleCapacity + 8];
        private RunnerMotor sampledMotor;
        private Vector3 moveStart, startBottom, startTop;
        private float startRadius;
        private bool hasMoveStart;
        private bool testArrangement;
        private int forcedSeed;
        private bool hasForcedSeed;
        private int sharedSeed;
        private bool hasSharedSeed;
        public int Passed { get; private set; }
        public int CleanPassed { get; private set; }
        public int Seed { get; private set; }
        public int PoolCount => pool == null ? 0 : pool.Length;
        public bool Enabled { get; set; } = true;
        public RunnerObstacle[] Items => pool;
        public bool LastHitWasSweep { get; private set; }
        public int ContactCount { get; private set; }
        public int LastNarrowPhaseNodes {get;private set;}
        public int LastNarrowPhaseTriangles {get;private set;}
        public RunnerObstacle ContactAt(int index) => index >= 0 && index < ContactCount ? contacts[index] : null;
        public float ContactTimeAt(int index) => index >= 0 && index < ContactCount ? contactTimes[index] : 1f;
        public string CurrentPattern { get; private set; } = "ISINMA";
        public void Configure(RunnerSettings s, RunnerObstacle jump, RunnerObstacle slide)
        { settings = s; jumpPrefab = jump; slidePrefab = slide; }

        public void ResetCourse()
        {
            if (pool == null)
            {
                pool = new RunnerObstacle[ObstacleCapacity];
                rows = new LiveRow[RowCapacity];
                for (int row = 0; row < rows.Length; row++)
                {
                    rows[row] = new LiveRow();
                    for (int lane = 0; lane < 3; lane++)
                        for (int type = 0; type < 2; type++)
                        {
                            var item = Instantiate(type == 0 ? jumpPrefab : slidePrefab, transform);
                            item.gameObject.SetActive(false);
                            pool[row * 6 + lane * 2 + type] = item;
                        }
                }
            }
            // Every fresh run varies. An explicit seed reproduces reported runs in the Editor.
            Seed = hasSharedSeed ? sharedSeed : hasForcedSeed ? forcedSeed : unchecked(System.Environment.TickCount * 397 ^ ++restartCount * 7919);
            generator = new CoursePatternGenerator(Seed);
            Passed = CleanPassed = 0; originOffset = 0; testArrangement = false; CurrentPattern = "ISINMA";
            hasMoveStart = false; sampledMotor = null; LastHitWasSweep = false; ContactCount = 0;
            for (int row = 0; row < rows.Length; row++) Place(row);
            Physics.SyncTransforms();
        }

        private void Place(int index)
        {
            LiveRow row = rows[index]; row.Pattern = generator.Next();
            row.Z = (float)(row.Pattern.Distance - originOffset); row.Scored = row.Spoiled = false;
            for (int lane = -1; lane <= 1; lane++)
                for (int type = 0; type < 2; type++)
                {
                    RunnerObstacle item = pool[index * 6 + (lane + 1) * 2 + type];
                    item.transform.position = new Vector3(lane * settings.laneSpacing, 0f, row.Z);
                    item.Scored = false;
                    item.gameObject.SetActive(Enabled && (int)row.Pattern.At(lane) == type + 1);
                }
        }

        // Called immediately before each motor move. The end-point overlap alone cannot
        // detect a trigger crossed completely during a long frame or a diagonal lane move.
        public void BeginMove(RunnerMotor motor)
        {
            hasMoveStart = Enabled && motor != null && motor.Capsule != null && motor.Capsule.enabled;
            sampledMotor = motor;
            if (!hasMoveStart) return;
            moveStart = motor.transform.position;
            ReadCapsule(motor, out startBottom, out startTop, out startRadius);
        }

        private static void ReadCapsule(RunnerMotor motor, out Vector3 bottom, out Vector3 top, out float radius)
        {
            var capsule = motor.Capsule;
            Vector3 scale = motor.transform.lossyScale;
            radius = Mathf.Max(.01f, capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            float height = Mathf.Max(radius * 2f, capsule.height * Mathf.Abs(scale.y));
            Vector3 centre = motor.transform.TransformPoint(capsule.center);
            Vector3 offset = motor.transform.up * (height * .5f - radius);
            bottom = centre - offset; top = centre + offset;
        }

        private void AppendOverlap(Vector3 bottom, Vector3 top, float radius)
        {
            Bounds envelope=new Bounds(bottom,Vector3.zero);envelope.Encapsulate(top);envelope.Expand(2*radius);
            if(pool!=null)foreach(var obstacle in pool)
            {
                if(!obstacle.gameObject.activeInHierarchy||obstacle.hitMesh==null||!envelope.Intersects(obstacle.SurfaceBounds))continue;
                bool hit=obstacle.hitMesh.Sweep(obstacle.transform,bottom,top,bottom,top,radius,radius,out _);
                LastNarrowPhaseNodes+=obstacle.hitMesh.LastNodeTests;LastNarrowPhaseTriangles+=obstacle.hitMesh.LastTriangleTests;
                if(hit)AddContact(obstacle,hasMoveStart?1f:0f,true);
            }
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps, 1 << 8, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var hit = overlaps[i].GetComponentInParent<RunnerObstacle>();
                if (hit != null && hit.hitMesh==null && hit.gameObject.activeInHierarchy) AddContact(hit, hasMoveStart ? 1f : 0f, true);
            }
            if(count==overlaps.Length&&pool!=null)
                foreach(var obstacle in pool)
                {
                    if(!obstacle.gameObject.activeInHierarchy||obstacle.hitMesh!=null)continue;
                    foreach(var box in obstacle.CollisionBoxes)
                        if(box.enabled&&box.gameObject.activeInHierarchy&&SweepBox(box.bounds,bottom,top,bottom,top,radius,radius,out float ignored))
                            AddContact(obstacle,hasMoveStart ? 1f : 0f,true);
                }
        }

        private void AddContact(RunnerObstacle obstacle, float time, bool endpoint)
        {
            for(int i=0;i<ContactCount;i++)
                if(contacts[i]==obstacle)
                {contactTimes[i]=Mathf.Min(contactTimes[i],time);contactEndsOverlapping[i]|=endpoint;return;}
            if(ContactCount>=contacts.Length)return;
            contacts[ContactCount]=obstacle;contactTimes[ContactCount]=Mathf.Clamp01(time);
            contactEndsOverlapping[ContactCount]=endpoint;ContactCount++;
        }
        private void SortContacts()
        {
            for(int i=1;i<ContactCount;i++)
            {
                RunnerObstacle item=contacts[i];float time=contactTimes[i];bool endpoint=contactEndsOverlapping[i];int j=i-1;
                while(j>=0&&contactTimes[j]>time)
                {contacts[j+1]=contacts[j];contactTimes[j+1]=contactTimes[j];contactEndsOverlapping[j+1]=contactEndsOverlapping[j];j--;}
                contacts[j+1]=item;contactTimes[j+1]=time;contactEndsOverlapping[j+1]=endpoint;
            }
        }

        public RunnerObstacle Check(RunnerMotor motor)
        {
            LastHitWasSweep = false; ContactCount = 0;LastNarrowPhaseNodes=LastNarrowPhaseTriangles=0;
            if (!Enabled || motor == null || motor.Capsule == null || !motor.Capsule.enabled) { hasMoveStart = false; return null; }
            Vector3 p = motor.transform.position;
            ReadCapsule(motor, out Vector3 bottom, out Vector3 top, out float radius);
            if (hasMoveStart && sampledMotor == motor) AppendSweptContacts(bottom, top, radius);
            AppendOverlap(bottom, top, radius);SortContacts();
            // Remember physical contact on its own row, including absorbed shield hits.
            // A later clear frame must not retroactively turn that row into a clean pass.
            for (int contact = 0; contact < ContactCount; contact++)
                for (int item = 0; item < pool.Length; item++)
                    if (pool[item] == contacts[contact]) { rows[item / 6].Spoiled = true; break; }
            RunnerObstacle collision = ContactCount > 0 ? contacts[0] : null;
            LastHitWasSweep = collision != null && !contactEndsOverlapping[0];
            // Advance the sample even for absorbed hits. Reusing a protected hit must not
            // create a sweep from several frames ago or silently ignore future obstacles.
            BeginMove(motor);
            if (collision != null) return collision;
            if (testArrangement) return null;
            bool changed = false;
            for (int i = 0; i < rows.Length; i++)
            {
                LiveRow row = rows[i];
                if (!row.Scored && p.z > row.Z + 3.5f)
                {
                    row.Scored = true;
                    if (row.Pattern.HazardCount > 0) { Passed++; if (!row.Spoiled) CleanPassed++; }
                    for (int j = 0; j < 6; j++) pool[i * 6 + j].Scored = true;
                }
                if (row.Z < p.z - 14f) { Place(i); changed = true; }
            }
            if (TryGetNextRoute(p.z, out CourseRoute route)) CurrentPattern = route.Pattern;
            if (changed) Physics.SyncTransforms();
            return null;
        }

        // The standing capsule must not be dragged through an entire crouching
        // move. Both sphere centres and the radius follow their real start/end
        // values. For the course's upright boxes, segment-to-box distance is a
        // piecewise quadratic, so every instant can be checked without sampling
        // gaps, a taller envelope, allocations, or jump invulnerability.
        private void AppendSweptContacts(Vector3 bottom, Vector3 top, float radius)
        {
            Bounds envelope = new Bounds(startBottom, Vector3.zero);
            envelope.Encapsulate(startTop); envelope.Encapsulate(bottom); envelope.Encapsulate(top);
            envelope.Expand(2f * Mathf.Max(startRadius, radius));
            // The pool is bounded. Its actual rendered bounds also cover hair or
            // accessories beyond an old authoring box, so broad-phase boxes can
            // never hide a visible surface from the precise contact query.
            if(pool!=null)foreach(var obstacle in pool)
            {
                if(!obstacle.gameObject.activeInHierarchy||obstacle.hitMesh==null||!envelope.Intersects(obstacle.SurfaceBounds))continue;
                bool hit=obstacle.hitMesh.Sweep(obstacle.transform,startBottom,startTop,bottom,top,startRadius,radius,out float time);
                LastNarrowPhaseNodes+=obstacle.hitMesh.LastNodeTests;LastNarrowPhaseTriangles+=obstacle.hitMesh.LastTriangleTests;
                if(hit)AddContact(obstacle,time,false);
            }
            int count = Physics.OverlapBoxNonAlloc(envelope.center, envelope.extents, overlaps,
                Quaternion.identity, 1 << 8, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var box = overlaps[i] as BoxCollider;
                if (box == null) continue; // ObstacleBuilder supplies only upright box slices.
                var obstacle = box.GetComponentInParent<RunnerObstacle>();
                if (obstacle == null || obstacle.hitMesh!=null || !obstacle.gameObject.activeInHierarchy) continue;
                if (SweepBox(box.bounds, startBottom, startTop, bottom, top, startRadius, radius, out float time))
                    AddContact(obstacle, time, false);
            }
            // A saturated broad phase must not silently hide a second unprotected
            // object behind an already absorbed one. The actual pool is bounded.
            if(count==overlaps.Length&&pool!=null)
                foreach(var obstacle in pool)
                {
                    if(!obstacle.gameObject.activeInHierarchy||obstacle.hitMesh!=null)continue;
                    foreach(var box in obstacle.CollisionBoxes)
                        if(box.enabled&&box.gameObject.activeInHierarchy&&envelope.Intersects(box.bounds)&&
                            SweepBox(box.bounds,startBottom,startTop,bottom,top,startRadius,radius,out float time))
                            AddContact(obstacle,time,false);
                }
        }

        private bool SweepBox(Bounds box, Vector3 b0, Vector3 t0, Vector3 b1, Vector3 t1,
            float r0, float r1, out float time)
        {
            int n = 2; sweepBreaks[0] = 0; sweepBreaks[1] = 1;
            AddBreak(b0.x, b1.x, box.min.x, ref n); AddBreak(b0.x, b1.x, box.max.x, ref n);
            AddBreak(b0.z, b1.z, box.min.z, ref n); AddBreak(b0.z, b1.z, box.max.z, ref n);
            AddBreak(b0.y, b1.y, box.max.y, ref n); AddBreak(t0.y, t1.y, box.min.y, ref n);
            for (int i = 1; i < n; i++)
            { float v = sweepBreaks[i]; int j = i - 1; while (j >= 0 && sweepBreaks[j] > v) { sweepBreaks[j + 1] = sweepBreaks[j]; j--; } sweepBreaks[j + 1] = v; }
            for (int i = 0; i < n - 1; i++)
            {
                float lo = sweepBreaks[i], hi = sweepBreaks[i + 1], mid = (lo + hi) * .5f;
                Vector3 b = Vector3.Lerp(b0, b1, mid), t = Vector3.Lerp(t0, t1, mid);
                Vector3 c = Vector3.zero, d = Vector3.zero;
                DistanceLine(b.x, b0.x, b1.x, box.min.x, box.max.x, out c.x, out d.x);
                DistanceLine(b.z, b0.z, b1.z, box.min.z, box.max.z, out c.z, out d.z);
                if (b.y > box.max.y) { c.y = b0.y - box.max.y; d.y = b1.y - b0.y; }
                else if (t.y < box.min.y) { c.y = t0.y - box.min.y; d.y = t1.y - t0.y; }
                float dr = r1 - r0;
                float a = Vector3.Dot(d, d) - dr * dr;
                float q = 2f * (Vector3.Dot(c, d) - r0 * dr);
                float k = Vector3.Dot(c, c) - r0 * r0;
                float vLo = (a * lo + q) * lo + k;
                float vHi = (a * hi + q) * hi + k;
                float probe = a > .000001f ? Mathf.Clamp(-q / (2f * a), lo, hi) : (vLo <= vHi ? lo : hi);
                if (vLo <= .000001f) { time = lo; return true; }
                if ((a * probe + q) * probe + k > .000001f && vHi > .000001f) continue;
                // Find entry within this convex interval for nearest-hit ordering.
                float end = (a * probe + q) * probe + k <= .000001f ? probe : hi;
                for (int j = 0; j < 12; j++)
                { float m = (lo + end) * .5f; if ((a * m + q) * m + k <= .000001f) end = m; else lo = m; }
                time = end; return true;
            }
            time = 0; return false;
        }
        private void AddBreak(float a, float b, float plane, ref int n)
        { if (Mathf.Abs(b - a) < .000001f) return; float t = (plane - a) / (b - a); if (t > 0 && t < 1) sweepBreaks[n++] = t; }
        private static void DistanceLine(float value, float start, float end, float min, float max, out float c, out float d)
        { float edge = value < min ? min : max; if (value >= min && value <= max) { c = d = 0; return; } c = start - edge; d = end - start; }

        public bool TryGetNextRoute(float localZ, out CourseRoute route)
        {
            route = default;
            if (rows == null || testArrangement) return false;
            LiveRow chosen = null;
            foreach (var row in rows)
                if (row.Z >= localZ && (chosen == null || row.Z < chosen.Z)) chosen = row;
            if (chosen == null) return false;
            route = new CourseRoute { Z = chosen.Z, Lane = chosen.Pattern.Lane, Action = chosen.Pattern.Action,
                Pattern = chosen.Pattern.Pattern, Recovery = chosen.Pattern.Recovery, Sequence = chosen.Pattern.Sequence };
            return true;
        }

        // Coin rails change lanes only between obstacle cross-sections. Jump arcs advertise
        // jump phrases and never send a ground-level reward through a standing obstacle.
        public Vector3 RewardPositionAt(float z, float speed=30f)
        {
            if (rows == null || testArrangement) return new Vector3(0, 1.12f, z);
            LiveRow before = null, after = null, nearest = null;
            foreach (var row in rows)
            {
                if (row.Z < z && (before == null || row.Z > before.Z)) before = row;
                if (row.Z >= z && (after == null || row.Z < after.Z)) after = row;
                if (nearest == null || Mathf.Abs(row.Z - z) < Mathf.Abs(nearest.Z - z)) nearest = row;
            }
            float x;
            if (after == null) x = before.Pattern.Lane * settings.laneSpacing;
            else
            {
                int from = before == null ? 0 : before.Pattern.Lane;
                float start = before == null ? 24f : before.Z + 7f;
                float end = Mathf.Max(start + 4f, after.Z - 14f);
                float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, z));
                x = Mathf.Lerp(from, after.Pattern.Lane, t) * settings.laneSpacing;
            }
            float height = 1.12f;
            if (nearest != null && nearest.Pattern.Action == RouteAction.Jump)
            {
                float offset = Mathf.Abs(z - nearest.Z);
                // The coin rail descends with the faster landing arc. A symmetric
                // tail left visible coins above the bird after it had touched down.
                float width=(z>nearest.Z?8f:10f)*Mathf.Max(1f,speed/30f);
                if (offset < width) height += 2.45f * Mathf.Cos(offset / width * Mathf.PI * .5f);
            }
            return new Vector3(x, height, z);
        }

        public void ShiftOrigin(float amount)
        {
            originOffset += amount;
            if (hasMoveStart)
            { Vector3 shift = Vector3.forward * amount; moveStart -= shift; startBottom -= shift; startTop -= shift; }
            if (rows != null) foreach (var row in rows) row.Z -= amount;
            if (pool != null) foreach (var item in pool) item.transform.position -= Vector3.forward * amount;
            Physics.SyncTransforms();
        }
        public void SetActive(bool enabled)
        {
            Enabled = enabled;
            hasMoveStart = false; ContactCount = 0;
            if (pool == null) return;
            for (int row = 0; row < rows.Length; row++)
                for (int lane = -1; lane <= 1; lane++)
                    for (int type = 0; type < 2; type++)
                        pool[row * 6 + (lane + 1) * 2 + type].gameObject.SetActive(enabled && (int)rows[row].Pattern.At(lane) == type + 1);
            Physics.SyncTransforms();
        }
        public void SetSharedSeed(int seed){sharedSeed=seed;hasSharedSeed=true;}
        public void ClearSharedSeed(){hasSharedSeed=false;}
#if UNITY_EDITOR
        public void UseSeedForTest(int seed) { forcedSeed = seed; hasForcedSeed = true; }
        public RunnerObstacle ArrangeTest(ObstacleKind kind, float z, int lane = 0)
        {
            Enabled = true; testArrangement = true; hasMoveStart = false; ContactCount = 0;
            foreach (var item in pool) item.gameObject.SetActive(false);
            var chosen = pool[kind == ObstacleKind.JumpOver ? 0 : 1]; chosen.gameObject.SetActive(true);
            chosen.transform.position = new Vector3(lane * settings.laneSpacing, 0, z); chosen.Scored = false;
            Physics.SyncTransforms(); return chosen;
        }
        public RunnerObstacle AddTestObstacle(ObstacleKind kind,float z,int lane=0)
        {
            testArrangement=true;Enabled=true;
            foreach(var item in pool)
                if(!item.gameObject.activeSelf&&item.kind==kind)
                {item.transform.position=new Vector3(lane*settings.laneSpacing,0,z);item.Scored=false;item.gameObject.SetActive(true);Physics.SyncTransforms();return item;}
            throw new System.InvalidOperationException("No unused obstacle of the requested type remains in the fixed test pool.");
        }
#endif
    }
}
