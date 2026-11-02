#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    // Exercise the actual controller and generated gate geometry, including
    // positive controls: a failed dodge must remain a collision.
    public sealed class SlideResponseChecksV13 : MonoBehaviour
    {
        const string Flag="FenerRunner.SlideResponseV13";
        [Serializable] sealed class Report
        {
            public bool passed;
            public string utc,unityVersion;
            public string scope="Actual Unity CharacterController and obstacle colliders at 18/30/42 m/s, 15/30/60/120 Hz command frames and <=1/60 physics steps. Close-entry slides, held stance, renewed slides, no-command/floor-obstacle controls, gestures, reset and pause. Editor simulation, not physical phone validation.";
            public string[] checks,errors;
        }
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        RunnerGame game;
        bool oldBackground;
        [MenuItem("Fener Runner/70 - Verify responsive slides v13")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Flag,false))return;
            SessionState.SetBool(Flag,false);new GameObject("Slide response v13").AddComponent<SlideResponseChecksV13>();
        }
        IEnumerator Start()
        {
            game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;game.enabled=false;
            oldBackground=Application.runInBackground;Application.runInBackground=true;
            Application.logMessageReceived+=Log;
            var routine=Run();
            while(true)
            {
                bool more;object wait=null;
                try{more=routine.MoveNext();if(more)wait=routine.Current;}
                catch(Exception e){errors.Add(e.ToString());break;}
                if(!more)break;yield return wait;
            }
            game.enabled=false;game.ReturnToMenu();
            var report=new Report{passed=errors.Count==0,utc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion,checks=checks.ToArray(),errors=errors.ToArray()};
            Directory.CreateDirectory("../Validation");File.WriteAllText(ReviewEvidence.FileFor(13,"../Validation/unity_slide_response_v13.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;
            Debug.Log("V13 responsive slide "+(report.passed?"PASSED":"FAILED")+": "+checks.Count+" checks; "+string.Join("; ",errors));
            EditorApplication.isPlaying=false;
        }
        IEnumerator Run()
        {
            foreach(int hz in new[]{15,30,60,120})
            foreach(float speed in new[]{18f,30f,42f})
            {
                string label=speed+"m/s "+hz+"Hz";
                foreach(float margin in new[]{.025f,.12f,.5f})
                {
                    Reset(speed);PlaceCloseGate(margin);
                    Check(game.Obstacles.Check(game.Motor)==null,"Before contact / "+label+" / "+margin);
                    Check(game.Motor.TrySlide()&&game.Motor.Capsule.height<1.51f,"Swipe lowers collision body before movement / "+label+" / "+margin);
                    Check(!Move(.35f,hz),"Late slide clears actual gate / "+label+" / "+margin);
                    Reset(speed);PlaceCloseGate(margin);
                    Check(Move(.35f,hz),"No swipe remains a real hit / "+label+" / "+margin);
                }
                Reset(speed);
                float start=game.Motor.transform.position.z;
                game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder,start+speed*.33f);
                game.Obstacles.AddTestObstacle(ObstacleKind.SlideUnder,start+speed*1.12f);
                game.Motor.TrySlide();bool hit=Move(.72f,hz);
                int generation=game.Motor.SlideCount;
                Check(game.Motor.TrySlide()&&game.Motor.SlideCount==generation+1,"A second swipe is accepted during active slide / "+label);
                Check(game.Motor.SlideRemaining>=game.Settings.slideSeconds-.001f&&game.Motor.Capsule.height<1.51f,"Chained slide refreshes duration without a standing gap / "+label);
                hit|=Move(.72f,hz);
                Check(!hit,"Consecutive gates clear with two deliberate swipes / "+label);
                Reset(speed);game.Motor.TrySlide();Move(.70f,hz);game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+1);
                Check(Move(.15f,hz),"Sliding still collides with the solid black obstacle / "+label);
                Reset(speed);game.Motor.TrySlide();Move(.70f,hz);
                Check(game.Motor.Capsule.height<1.51f,"No premature rise in last 150ms / "+label);
                Move(.25f,hz);PlaceCloseGate(.025f);
                Check(!game.Motor.Sliding&&Move(.15f,hz),"Expired slide cannot bypass the next gate / "+label);
                yield return null;
            }
            Reset(0);game.Motor.TrySlide();Move(.2f,60);game.Motor.TrySlide();
            Check(game.Motor.TryJump()&&!game.Motor.Sliding&&game.Motor.Capsule.height>2,"Jump cleanly cancels chained slide");
            Reset(0);game.Motor.TryJump();game.Motor.Tick(.12f);Check(game.Motor.TrySlide(),"Down swipe cancels airborne ascent");
            for(int i=0;i<100&&!game.Motor.Grounded;i++)game.Motor.Tick(1f/120);
            Check(game.Motor.Grounded&&game.Motor.Sliding&&game.Motor.TrySlide(),"Landing slide immediately accepts another swipe");
            float beforeShift=game.Motor.transform.position.z;
            game.Course.ShiftOrigin(500);game.Obstacles.ShiftOrigin(500);game.Tokens.ShiftOrigin(500);game.Powerups.ShiftOrigin(500);game.Motor.ShiftOrigin(500);
            Move(.01f,120);
            Check(game.Motor.Capsule.height<1.51f&&game.Motor.Grounded&&Mathf.Abs(game.Motor.transform.position.z-(beforeShift-500))<.001f,"A real 500m origin shift preserves the grounded held stance");
            game.Pause();float remaining=game.Motor.SlideRemaining;Vector3 p=game.Motor.transform.position;
            game.enabled=true;yield return new WaitForSecondsRealtime(.12f);game.enabled=false;
            Check(game.Motor.SlideRemaining==remaining&&game.Motor.transform.position==p,"Pause freezes chained slide state");
            game.Resume();game.Restart();game.enabled=false;
            Check(!game.Motor.Sliding&&game.Motor.SlideCount==0&&game.Motor.Capsule.height>2,"Restart clears all slide state");
            var input=FindFirstObjectByType<RunnerInput>();int commands=0;
            Action<RunnerCommand> observe=c=>{if(c==RunnerCommand.Slide)commands++;};input.Command+=observe;
            Vector2 from=new Vector2(Screen.width*.5f,Screen.height*.55f),to=from-Vector2.up*Mathf.Min(Screen.width,Screen.height)*.2f;
            for(int i=0;i<3;i++){input.TestBegin(from);input.TestMove(to);input.TestEnd(to);}
            input.Command-=observe;
            Check(commands==3&&game.Motor.SlideCount==2&&game.Motor.SlideQueued,"Three immediate mobile gestures emit three commands but reserve at most one extra full slide");
        }
        void Reset(float speed)
        {
            game.enabled=false;game.Powerups.Enabled=false;game.Tokens.Enabled=false;game.Play();game.Obstacles.SetActive(false);game.Motor.Speed=speed;
        }
        void PlaceCloseGate(float margin)
        {
            var gate=game.Obstacles.ArrangeTest(ObstacleKind.SlideUnder,game.Motor.transform.position.z+3);
            BoxCollider header=null;
            foreach(var b in gate.CollisionBoxes)if(header==null||b.bounds.size.x>header.bounds.size.x)header=b;
            float offset=game.Motor.transform.position.z+game.Motor.Capsule.radius+margin-header.bounds.min.z;
            gate.transform.position+=Vector3.forward*offset;Physics.SyncTransforms();
        }
        bool Move(float duration,int hz)
        {
            bool hit=false;float elapsed=0;
            while(elapsed<duration-.00001f)
            {
                float frame=Mathf.Min(1f/hz,duration-elapsed);
                while(frame>.000001f)
                {
                    float dt=Mathf.Min(frame,1f/60);game.Obstacles.BeginMove(game.Motor);game.Motor.Tick(dt);
                    hit|=game.Obstacles.Check(game.Motor)!=null;frame-=dt;elapsed+=dt;
                }
            }
            return hit;
        }
        void Check(bool ok,string label){if(ok)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;Application.runInBackground=oldBackground;}
    }
}
#endif
