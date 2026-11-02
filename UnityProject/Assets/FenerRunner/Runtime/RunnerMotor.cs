using System;
using UnityEngine;

namespace FenerRunner
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class RunnerMotor : MonoBehaviour
    {
        [SerializeField] private RunnerSettings settings;
        private CharacterController capsule;
        private float laneStartX, laneElapsed, slideRemaining, verticalVelocity;
        private bool fastFalling;
        private bool flightRequested,flying;
        private float flightVelocity;
        private bool airStepUsed, slideQueued;
        private float jumpBuffer;
        private float slideRecoveryRemaining;
        private const float StandingHeight = 2.05f;
        private const float SlidingHeight = 1.5f;
        public CharacterController Capsule => capsule;
        public float Speed { get; set; }
        public float JumpHeightMultiplier {get;set;}=1f;
        public float JumpArcMultiplier {get;private set;}=1f;
        public bool AirStepEnabled {get;set;}
        public bool AirStepUsed=>airStepUsed;
        public float ExpectedAirSeconds {get;private set;}
        public float JumpAnimationPhase {get;private set;}
        public int Lane { get; private set; }
        public int JumpCount { get; private set; }
        public bool Grounded { get; private set; }
        public bool Sliding => slideRemaining > 0f;
        public float SlideRemaining => slideRemaining+(slideQueued?settings.slideSeconds:0f);
        public bool SlideQueued => slideQueued;
        public int SlideCycleCount {get;private set;}
        public bool SlideRecovering=>slideRecoveryRemaining>0f;
        public float SlideRecoverySeconds=>Mathf.Max(0f,Mathf.Min(settings.blendSeconds,.09f));
        public float SlideRecoveryProgress=>SlideRecovering&&SlideRecoverySeconds>0f?Mathf.Clamp01(1f-slideRecoveryRemaining/SlideRecoverySeconds):1f;
        public float SlideProgress => Sliding ? Mathf.Clamp01(1f-slideRemaining/settings.slideSeconds) : 1f;
        public int SlideCount { get; private set; }
        public bool ChangingLane => laneElapsed < settings.laneChangeSeconds;
        public float VerticalVelocity => verticalVelocity;
        public bool FastFalling => fastFalling;
        public bool Flying => flying;
        public bool FlightRequested => flightRequested;
        public event Action<bool> Jumped;

        public void Configure(RunnerSettings value) { settings = value; }

        public void ResetRun()
        {
            if (capsule == null) capsule = GetComponent<CharacterController>();
            capsule.enabled = false;
            transform.SetPositionAndRotation(new Vector3(0f, 0.02f, 8f), Quaternion.identity);
            Lane = 0;
            Speed = settings.forwardSpeed;
            JumpHeightMultiplier=JumpArcMultiplier=1f;
            AirStepEnabled=airStepUsed=false;jumpBuffer=0;ExpectedAirSeconds=settings.jumpAirSeconds;JumpAnimationPhase=0;
            JumpCount = 0;
            laneStartX = 0f;
            laneElapsed = settings.laneChangeSeconds;
            verticalVelocity = 0f;
            slideRemaining = 0f;slideQueued=false;SlideCycleCount=0;slideRecoveryRemaining=0;
            fastFalling = false;
            SlideCount = 0;
            flightRequested=flying=false;flightVelocity=0;
            SetCapsule(StandingHeight);
            capsule.enabled = true;
            // Establish contact on the actual course, rather than trusting a stale isGrounded.
            Grounded = (capsule.Move(Vector3.down * 0.1f) & CollisionFlags.Below) != 0;
        }

        public void ChangeLane(int direction)
        {
            int next = Mathf.Clamp(Lane + direction, -1, 1);
            if (next == Lane) return;
            Lane = next;
            laneStartX = transform.position.x;
            laneElapsed = 0f;
        }

        public float LanePositionAfter(float secondsAhead)
        {
            float t=Mathf.Clamp01((laneElapsed+Mathf.Max(0,secondsAhead))/settings.laneChangeSeconds);
            return Mathf.Lerp(laneStartX,Lane*settings.laneSpacing,t*t*(3f-2f*t));
        }

        public bool TryJump(bool forceFlip = false)
        {
            if(flying||fastFalling)return false;
            if(!Grounded)
            {
                if(!airStepUsed)
                {
                    // Every run has one air jump. Keep the unspent upward energy
                    // so two rapid swipes never weaken the first launch. AirStep
                    // strengthens this same charge, it cannot create infinite hops.
                    float riseLeft=verticalVelocity>0?verticalVelocity*verticalVelocity/(-2f*settings.Gravity):0f;
                    float extraHeight=Mathf.Min(riseLeft+(AirStepEnabled?1.65f:1.10f),5f-transform.position.y);
                    if(extraHeight<.2f)return false;
                    airStepUsed=true;jumpBuffer=0;
                    verticalVelocity=Mathf.Sqrt(-2f*settings.Gravity*extraHeight);
                    JumpArcMultiplier=Mathf.Max(1.01f,JumpArcMultiplier);
                    ExpectedAirSeconds=verticalVelocity/-settings.Gravity+Mathf.Sqrt(2f*(transform.position.y+extraHeight)/-settings.FallGravity);
                    JumpAnimationPhase=0;JumpCount++;Jumped?.Invoke(false);return true;
                }
                // A slightly early upward swipe is remembered until the feet touch down.
                if(verticalVelocity<0&&transform.position.y<.6f)jumpBuffer=.1f;
                return false;
            }
            EndSlide();
            Grounded = false;
            jumpBuffer=0;airStepUsed=false;JumpAnimationPhase=0;
            JumpArcMultiplier=Mathf.Clamp(JumpHeightMultiplier,1f,1.5f);
            verticalVelocity = settings.JumpVelocity*Mathf.Sqrt(JumpArcMultiplier);
            ExpectedAirSeconds=settings.jumpAirSeconds*Mathf.Sqrt(JumpArcMultiplier);
            JumpCount++;
            bool flip = forceFlip || JumpCount % Mathf.Max(1, settings.flipEveryJumps) == 0;
            Jumped?.Invoke(flip);
            return true;
        }

        public bool TrySlide()
        {
            if(flying)return false;
            jumpBuffer=0;
            if (!Grounded)
            {
                if (fastFalling) return false;
                // A deliberate downward swipe can cancel an arc. The same command
                // queues the landing slide; it never adds a second jump impulse.
                fastFalling = true;
                slideRecoveryRemaining=0;
                verticalVelocity = Mathf.Min(verticalVelocity, -14f);
                SetCapsule(SlidingHeight);
                return true;
            }
            // Keep one complete next cycle. Two quick swipes therefore produce
            // two slides instead of losing almost all of the second duration.
            // Additional input is accepted once that queued cycle has begun.
            if(Sliding)
            {
                if(slideQueued)return false;
                slideQueued=true;SlideCount++;SetCapsule(SlidingHeight);return true;
            }
            BeginSlide();
            return true;
        }

        private void BeginSlide()
        {
            slideRemaining = settings.slideSeconds;slideRecoveryRemaining=0;
            slideQueued=false;SlideCount++;SlideCycleCount++;
            // Input is sampled before collision movement. The previous 170 ms
            // shrinking envelope made a timely swipe hit an invisible tall body.
            // Animation eases into this stance independently of input response.
            SetCapsule(SlidingHeight);
        }

        public void SetFlight(bool active)
        {
            if(active&&!flightRequested)
            {
                EndSlide();fastFalling=false;verticalVelocity=0;flightVelocity=0;
                flying=true;Grounded=false;
            }
            flightRequested=active;
        }

        public float Tick(float dt)
        {
            if (dt <= 0f) return 0f;
            jumpBuffer=Mathf.Max(0,jumpBuffer-dt);
            if(SlideRecovering)
            {slideRecoveryRemaining=Mathf.Max(0,slideRecoveryRemaining-dt);ApplyRecoveryCapsule();}
            if(!Grounded&&!flying)JumpAnimationPhase=Mathf.Min(1,JumpAnimationPhase+dt/Mathf.Max(.1f,ExpectedAirSeconds));
            if (Sliding)
            {
                slideRemaining -= dt;
                if (slideRemaining <= 0f)
                {
                    if(slideQueued)
                    {
                        // Preserve overshoot on a coarse frame and keep the low
                        // collider continuously through the cycle boundary.
                        slideRemaining+=settings.slideSeconds;slideQueued=false;SlideCycleCount++;
                        if(slideRemaining<=0f)EndSlide(true,-slideRemaining);else SetCapsule(SlidingHeight);
                    }
                    else EndSlide(true,-slideRemaining);
                }
                else SetCapsule(SlidingHeight);
            }
            laneElapsed = Mathf.Min(laneElapsed + dt, settings.laneChangeSeconds);
            float x = LanePositionAfter(0);
            float dy;
            if(flying)
            {
                // Flight moves the real capsule, so scenery and collisions agree with the visible character.
                float target=flightRequested?4.8f:-.03f;
                float next=Mathf.SmoothDamp(transform.position.y,target,ref flightVelocity,flightRequested ? .26f : .34f,22f,dt);
                dy=next-transform.position.y;verticalVelocity=dt>0?dy/dt:0;
                if(!flightRequested&&transform.position.y<.045f)dy=-.15f;
            }
            else if (Grounded)
            {
                verticalVelocity = -2f;
                dy = verticalVelocity * dt;
            }
            else
            {
                // Integrate the apex exactly even on a coarse frame. The stronger
                // descent has no artificial hang time or discontinuous velocity.
                float rise=verticalVelocity>0?Mathf.Min(dt,verticalVelocity/-settings.Gravity):0;
                dy=verticalVelocity*rise+.5f*settings.Gravity*rise*rise;
                verticalVelocity+=settings.Gravity*rise;
                float fall=dt-rise;
                if(fall>0){dy+=verticalVelocity*fall+.5f*settings.FallGravity*fall*fall;verticalVelocity+=settings.FallGravity*fall;}
            }
            float before = transform.position.z;
            CollisionFlags flags = capsule.Move(new Vector3(x - transform.position.x, dy, Speed * dt));
            Grounded = (flags & CollisionFlags.Below) != 0 && verticalVelocity <= 0f;
            if (Grounded)
            {
                JumpArcMultiplier=1f;
                airStepUsed=false;
                if(flying&&!flightRequested){flying=false;flightVelocity=0;}
                verticalVelocity = -2f;
                if (fastFalling)
                {
                    fastFalling = false;
                    BeginSlide();
                }
                if(jumpBuffer>0&&!Sliding)TryJump();
            }
            return Mathf.Max(0f, transform.position.z - before);
        }

        public void ShiftOrigin(float amount)
        {
            capsule.enabled = false;
            transform.position -= Vector3.forward * amount;
            capsule.enabled = true;
        }

        private void EndSlide(bool recover=false,float elapsed=0f)
        {
            slideRemaining = 0f;slideQueued=false;
            // Natural expiry uses the same short rise as the existing Run blend.
            // An immediate 2.05m body would hit a header while the rendered chick
            // is still crouched. Explicit jump/flight/reset retain immediate stance.
            slideRecoveryRemaining=recover?Mathf.Max(0,SlideRecoverySeconds-elapsed):0;
            ApplyRecoveryCapsule();
        }

        private void ApplyRecoveryCapsule()
        {
            // Native poses rise slowly at the start of the Slide -> Run blend.
            // SmoothStep expanded the body to1.82m at50ms while the visible head
            // was still1.61m, causing another invisible hit under a real header.
            // Ease in with that measured head rise, then restore the established
            // standing envelope by the end of the same short animation blend.
            float t=SlideRecoveryProgress;
            SetCapsule(Mathf.Lerp(SlidingHeight,StandingHeight,t*t*t));
        }

        public void CancelBufferedActions(){slideQueued=false;jumpBuffer=0;}

        private void SetCapsule(float height)
        {
            capsule.height = height;
            capsule.center = Vector3.up * (height * 0.5f);
        }
    }
}
