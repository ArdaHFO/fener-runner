using System;
using UnityEngine;

namespace FenerRunner
{
    // Immutable final-render-mesh geometry shared by the whole obstacle pool.
    // The broad boxes remain useful authoring/diagnostic envelopes, but empty
    // space inside them must never be a lethal surface.
    public sealed class ObstacleHitMesh : ScriptableObject
    {
        [Serializable] public struct Triangle { public Vector3 a,b,c; public int component; }
        [Serializable] public struct Node { public Bounds bounds; public int start,count,escape; }
        [Serializable] public sealed class Component { public Bounds bounds; public bool closed; public int[] triangles; }
        public Triangle[] triangles;
        public Node[] nodes;
        public Component[] components;
        public Bounds bounds;
        public int LastNodeTests {get;private set;}
        public int LastTriangleTests {get;private set;}
        const float Epsilon=0.000001f;

        public bool Sweep(Transform owner,Vector3 b0,Vector3 t0,Vector3 b1,Vector3 t1,float r0,float r1,out float time)
        {
            LastNodeTests=LastTriangleTests=0;time=1;
            if(nodes==null||nodes.Length==0)return false;
            Matrix4x4 world=owner.localToWorldMatrix,inverse=owner.worldToLocalMatrix;
            Bounds query=new Bounds(b0,Vector3.zero);query.Encapsulate(t0);query.Encapsulate(b1);query.Encapsulate(t1);query.Expand(Mathf.Max(r0,r1)*2);
            Bounds localQuery=TransformBounds(inverse,query);
            if(!bounds.Intersects(localQuery))return false;
            // Per connected component parity makes the model a union of solids:
            // being inside two overlapping body parts cannot cancel occupancy.
            if(Contains(inverse.MultiplyPoint3x4(b0))||Contains(inverse.MultiplyPoint3x4(t0))||Contains(inverse.MultiplyPoint3x4((b0+t0)*.5f)))
            {time=0;return true;}
            bool hit=false;
            for(int n=0;n<nodes.Length;)
            {
                Node node=nodes[n];LastNodeTests++;
                if(!node.bounds.Intersects(localQuery)){n=node.escape;continue;}
                if(node.count>0)for(int i=node.start;i<node.start+node.count;i++)
                {
                    Triangle source=triangles[i];LastTriangleTests++;
                    Vector3 a=world.MultiplyPoint3x4(source.a),b=world.MultiplyPoint3x4(source.b),c=world.MultiplyPoint3x4(source.c);
                    if(TriangleContact(b0,t0,b1,t1,r0,r1,a,b,c,out float entry)&&entry<=time)
                    {hit=true;time=entry;if(time<=0)return true;}
                }
                n++;
            }
            return hit;
        }

        public static Bounds TransformBounds(Matrix4x4 m,Bounds value)
        {
            Vector3 e=value.extents;
            Vector3 ext=new Vector3(Mathf.Abs(m.m00)*e.x+Mathf.Abs(m.m01)*e.y+Mathf.Abs(m.m02)*e.z,
                Mathf.Abs(m.m10)*e.x+Mathf.Abs(m.m11)*e.y+Mathf.Abs(m.m12)*e.z,
                Mathf.Abs(m.m20)*e.x+Mathf.Abs(m.m21)*e.y+Mathf.Abs(m.m22)*e.z);
            return new Bounds(m.MultiplyPoint3x4(value.center),ext*2);
        }

        bool Contains(Vector3 point)
        {
            if(components==null)return false;
            foreach(Component part in components)
            {
                // An open cloth/accessory surface does not define an interior.
                // Its triangles still collide normally, but cannot fill empty air.
                if(!part.closed||!part.bounds.Contains(point))continue;
                bool first=RayParity(part,point,new Vector3(.81231f,.32357f,.48511f),out bool ambiguous);
                if(!ambiguous){if(first)return true;continue;}
                bool second=RayParity(part,point,new Vector3(-.23171f,.91419f,.33293f),out _);
                if(first==second){if(first)return true;continue;}
                bool third=RayParity(part,point,new Vector3(.34213f,-.43817f,.83147f),out _);
                if((first?1:0)+(second?1:0)+(third?1:0)>=2)return true;
            }
            return false;
        }
        bool RayParity(Component part,Vector3 origin,Vector3 direction,out bool ambiguous)
        {
            int count=0;ambiguous=false;
            foreach(int index in part.triangles)
            {
                LastTriangleTests++;Triangle tr=triangles[index];Vector3 edge1=tr.b-tr.a,edge2=tr.c-tr.a;
                Vector3 p=Vector3.Cross(direction,edge2);float det=Vector3.Dot(edge1,p);
                if(Mathf.Abs(det)<1e-10f)continue;
                float inv=1/det;Vector3 s=origin-tr.a;
                float u=Vector3.Dot(s,p)*inv;if(u<0||u>1)continue;
                Vector3 q=Vector3.Cross(s,edge1);float v=Vector3.Dot(direction,q)*inv;if(v<0||u+v>1)continue;
                float distance=Vector3.Dot(edge2,q)*inv;if(distance<=.00001f)continue;
                if(u<.00001f||v<.00001f||1-u-v<.00001f)ambiguous=true;
                count++;
            }
            return (count&1)==1;
        }

        // A translating upright capsule's axis sweeps a planar trapezoid, even
        // when its height changes. Triangle/trapezoid distance therefore tests
        // the entire frame, rather than relying on discrete time samples.
        static bool TriangleContact(Vector3 b0,Vector3 t0,Vector3 b1,Vector3 t1,float r0,float r1,Vector3 a,Vector3 b,Vector3 c,out float entry)
        {
            entry=0;
            float start=SegmentTriangle(b0,t0,a,b,c,out _);
            if(start<=r0*r0+Epsilon)return true;
            // Endpoint overlap queries have no swept volume beyond this axis.
            // Use exact equality so even a tiny real movement still gets swept.
            if(b0.Equals(b1)&&t0.Equals(t1)&&r0==r1)return false;
            float distance=TriangleTriangle(b0,t0,t1,a,b,c,out Vector3 witness);
            float other=TriangleTriangle(b0,t1,b1,a,b,c,out Vector3 second);
            if(other<distance){distance=other;witness=second;}
            float maxRadius=Mathf.Max(r0,r1);
            if(distance>maxRadius*maxRadius+Epsilon)return false;
            Vector3 horizontal=b1-b0;horizontal.y=0;
            float at;
            if(horizontal.sqrMagnitude>1e-10f)
                at=Mathf.Clamp01(Vector3.Dot(witness-b0,horizontal)/horizontal.sqrMagnitude);
            else
            {
                float lo=0,hi=1;
                if(!LinearInterval(b0.y-witness.y,b1.y-b0.y,ref lo,ref hi)||!LinearInterval(witness.y-t0.y,t0.y-t1.y,ref lo,ref hi))return false;
                at=(lo+hi)*.5f;
            }
            if(Mathf.Abs(r1-r0)>1e-6f)
            {
                // Radius is constant in production, but preserve correctness for
                // explicit controller scaling without using its largest sphere.
                float lo=0,hi=1;
                for(int i=0;i<30;i++)
                {
                    float x=(2*lo+hi)/3,y=(lo+2*hi)/3;
                    float dx=Mathf.Sqrt(SegmentTriangle(Vector3.Lerp(b0,b1,x),Vector3.Lerp(t0,t1,x),a,b,c,out _))-Mathf.Lerp(r0,r1,x);
                    float dy=Mathf.Sqrt(SegmentTriangle(Vector3.Lerp(b0,b1,y),Vector3.Lerp(t0,t1,y),a,b,c,out _))-Mathf.Lerp(r0,r1,y);
                    if(dx<dy)hi=y;else lo=x;
                }
                at=(lo+hi)*.5f;
            }
            float radius=Mathf.Lerp(r0,r1,at);
            if(SegmentTriangle(Vector3.Lerp(b0,b1,at),Vector3.Lerp(t0,t1,at),a,b,c,out _)>radius*radius+Epsilon)return false;
            float left=0,right=at;
            for(int i=0;i<15;i++)
            {
                float mid=(left+right)*.5f,r=Mathf.Lerp(r0,r1,mid);
                if(SegmentTriangle(Vector3.Lerp(b0,b1,mid),Vector3.Lerp(t0,t1,mid),a,b,c,out _)<=r*r+Epsilon)right=mid;else left=mid;
            }
            entry=right;return true;
        }
        // a+b*t<=0, intersected with the current time interval.
        static bool LinearInterval(float a,float b,ref float lo,ref float hi)
        {
            if(Mathf.Abs(b)<1e-10f)return a<=Epsilon;
            float boundary=-a/b;if(b>0)hi=Mathf.Min(hi,boundary);else lo=Mathf.Max(lo,boundary);
            return lo<=hi+Epsilon;
        }
        static float TriangleTriangle(Vector3 p,Vector3 q,Vector3 r,Vector3 a,Vector3 b,Vector3 c,out Vector3 onFirst)
        {
            float best=SegmentTriangle(p,q,a,b,c,out onFirst);
            float d=SegmentTriangle(q,r,a,b,c,out Vector3 point);if(d<best){best=d;onFirst=point;}
            d=SegmentTriangle(r,p,a,b,c,out point);if(d<best){best=d;onFirst=point;}
            d=SegmentTrianglePair(a,b,p,q,r,out _,out point);if(d<best){best=d;onFirst=point;}
            d=SegmentTrianglePair(b,c,p,q,r,out _,out point);if(d<best){best=d;onFirst=point;}
            d=SegmentTrianglePair(c,a,p,q,r,out _,out point);if(d<best){best=d;onFirst=point;}
            return best;
        }
        static float SegmentTriangle(Vector3 p,Vector3 q,Vector3 a,Vector3 b,Vector3 c,out Vector3 onSegment)
            =>SegmentTrianglePair(p,q,a,b,c,out onSegment,out _);
        static float SegmentTrianglePair(Vector3 p,Vector3 q,Vector3 a,Vector3 b,Vector3 c,out Vector3 onSegment,out Vector3 onTriangle)
        {
            Vector3 d=q-p,e1=b-a,e2=c-a,h=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,h);
            if(Mathf.Abs(det)>1e-10f)
            {
                float inv=1/det;Vector3 s=p-a;float u=Vector3.Dot(s,h)*inv;Vector3 k=Vector3.Cross(s,e1);
                float v=Vector3.Dot(d,k)*inv,t=Vector3.Dot(e2,k)*inv;
                if(u>=0&&v>=0&&u+v<=1&&t>=0&&t<=1){onSegment=onTriangle=p+d*t;return 0;}
            }
            onSegment=p;onTriangle=PointTriangle(p,a,b,c);float best=(p-onTriangle).sqrMagnitude;
            Vector3 x=PointTriangle(q,a,b,c);float distance=(q-x).sqrMagnitude;if(distance<best){best=distance;onSegment=q;onTriangle=x;}
            distance=SegmentSegment(p,q,a,b,out Vector3 sp,out Vector3 tp);if(distance<best){best=distance;onSegment=sp;onTriangle=tp;}
            distance=SegmentSegment(p,q,b,c,out sp,out tp);if(distance<best){best=distance;onSegment=sp;onTriangle=tp;}
            distance=SegmentSegment(p,q,c,a,out sp,out tp);if(distance<best){best=distance;onSegment=sp;onTriangle=tp;}
            return best;
        }
        static Vector3 PointTriangle(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            Vector3 ab=b-a,ac=c-a,ap=p-a;
            if(Vector3.Cross(ab,ac).sqrMagnitude<1e-16f)
            {
                Vector3 x=PointSegment(p,a,b),y=PointSegment(p,b,c),z=PointSegment(p,c,a);
                if((y-p).sqrMagnitude<(x-p).sqrMagnitude)x=y;if((z-p).sqrMagnitude<(x-p).sqrMagnitude)x=z;return x;
            }
            float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
            Vector3 bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            Vector3 cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float denominator=1/(va+vb+vc);return a+ab*(vb*denominator)+ac*(vc*denominator);
        }
        static Vector3 PointSegment(Vector3 p,Vector3 a,Vector3 b)
        {Vector3 d=b-a;return a+d*(d.sqrMagnitude>1e-16f?Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude):0);}
        static float SegmentSegment(Vector3 p1,Vector3 q1,Vector3 p2,Vector3 q2,out Vector3 c1,out Vector3 c2)
        {
            Vector3 d1=q1-p1,d2=q2-p2,r=p1-p2;float a=Vector3.Dot(d1,d1),e=Vector3.Dot(d2,d2),f=Vector3.Dot(d2,r),s,t;
            if(a<=1e-16f&&e<=1e-16f){c1=p1;c2=p2;return (c1-c2).sqrMagnitude;}
            if(a<=1e-16f){s=0;t=Mathf.Clamp01(f/e);}
            else
            {
                float c=Vector3.Dot(d1,r);
                if(e<=1e-16f){t=0;s=Mathf.Clamp01(-c/a);}
                else{float b=Vector3.Dot(d1,d2),den=a*e-b*b;s=den>1e-16f?Mathf.Clamp01((b*f-c*e)/den):0;t=(b*s+f)/e;
                    if(t<0){t=0;s=Mathf.Clamp01(-c/a);}else if(t>1){t=1;s=Mathf.Clamp01((b-c)/a);}}
            }
            c1=p1+d1*s;c2=p2+d2*t;return (c1-c2).sqrMagnitude;
        }
    }
}
