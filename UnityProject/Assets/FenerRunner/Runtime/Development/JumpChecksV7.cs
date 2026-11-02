#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace FenerRunner
{
    public sealed class JumpChecksV7:MonoBehaviour
    {
        [Serializable] class Arc {public float hz,apex,riseSeconds,airSeconds;}
        [Serializable] class Report {public bool passed;public string utc,unityVersion;public string[] checks,errors;public Arc[] arcs;public float riseGravity,fallGravity;public string scope="Real Unity CharacterController trajectories at15/30/60/120Hz, gesture paths, pause and actual obstacle overlap. Editor simulation, not phone performance.";}
        readonly List<string> checks=new List<string>(),errors=new List<string>();readonly List<Arc> arcs=new List<Arc>();RunnerGame game;bool background;bool ready;
        [MenuItem("Fener Runner/23 - Verify grounded jump feel")]
        public static void StartChecks(){SessionState.SetBool("FenerRunner.JumpV7",true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("FenerRunner.JumpV7",false)){SessionState.SetBool("FenerRunner.JumpV7",false);new GameObject("Jump checks v7").AddComponent<JumpChecksV7>();}}
        IEnumerator Start()
        {
            game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;game.enabled=false;background=Application.runInBackground;Application.runInBackground=true;ready=true;Application.logMessageReceived+=Log;
            yield return null;
            foreach(int hz in new[]{15,30,60,120})
            {
                Reset();game.Motor.TryJump();float time=0,peak=0,peakTime=0;
                while(!game.Motor.Grounded&&time<2){game.Motor.Tick(1f/hz);time+=1f/hz;if(game.Motor.transform.position.y>peak){peak=game.Motor.transform.position.y;peakTime=time;}}
                arcs.Add(new Arc{hz=hz,apex=peak,riseSeconds=peakTime,airSeconds=time});
                Check(Mathf.Abs(peak-game.Settings.jumpHeight)<.06f,"Height stays within6cm at "+hz+"Hz");
                Check(time>=.69f&&time<.81f,"Normal airborne duration is short and bounded at "+hz+"Hz");
                Check(peakTime>time*.5f&&game.Motor.Grounded,"Descent is quicker than ascent and feet land at "+hz+"Hz");
            }
            Reset();game.Motor.TryJump();Check(game.Motor.TryJump()&&!game.Motor.TryJump(),"An ordinary second upward swipe double jumps once, while a third cannot fly");
            game.Motor.Tick(.18f);game.Motor.TrySlide();Check(game.Motor.FastFalling&&game.Motor.VerticalVelocity<=-14,"Downward swipe immediately cancels upward momentum");
            for(int i=0;i<30&&!game.Motor.Grounded;i++)game.Motor.Tick(1f/60);
            Check(game.Motor.Grounded&&game.Motor.Sliding,"A downward swipe lands into a slide");
            Reset();game.Motor.TryJump();game.Motor.Tick(.12f);game.Motor.TryJump();while(!game.Motor.Grounded&&!(game.Motor.VerticalVelocity<0&&game.Motor.transform.position.y<.4f))game.Motor.Tick(1f/120);
            int before=game.Motor.JumpCount;game.Motor.TryJump();for(int i=0;i<15;i++)game.Motor.Tick(1f/120);
            Check(game.Motor.JumpCount==before+1&&!game.Motor.Grounded,"A near-landing upward swipe is buffered once without being lost");
            Reset();game.Motor.JumpHeightMultiplier=1.5f;game.Motor.TryJump();float superPeak=0,superTime=0;
            while(!game.Motor.Grounded&&superTime<2){game.Motor.Tick(1f/120);superTime+=1f/120;superPeak=Mathf.Max(superPeak,game.Motor.transform.position.y);}
            Check(Mathf.Abs(superPeak-game.Settings.jumpHeight*1.5f)<.07f&&superTime<.95f,"Super jump remains higher without the old long float");
            Reset();game.Motor.AirStepEnabled=true;game.Motor.TryJump();game.Motor.Tick(.27f);Check(game.Motor.TryJump()&&game.Motor.AirStepUsed,"AirStep permits one real second upward impulse");
            Check(!game.Motor.TryJump(),"Repeated AirStep inputs cannot chain infinite jumps");
            float airPeak=0;for(int i=0;i<240&&!game.Motor.Grounded;i++){game.Motor.Tick(1f/120);airPeak=Mathf.Max(airPeak,game.Motor.transform.position.y);}
            Check(airPeak<=5.04f&&game.Motor.Grounded&&!game.Motor.AirStepUsed,"AirStep height stays capped and its charge resets at landing");
            Reset();game.Motor.SetFlight(true);game.Motor.AirStepEnabled=true;Check(!game.Motor.TryJump(),"Flight cannot add an AirStep impulse");
            Reset();game.Motor.TryJump();Vector3 position=game.Motor.transform.position;game.Pause();game.enabled=true;yield return new WaitForSecondsRealtime(.18f);
            Check(game.Motor.transform.position==position,"Pause freezes the new jump trajectory");game.enabled=false;game.Resume();
            Check(Mathf.Abs(game.Settings.FallGravity)>Mathf.Abs(game.Settings.Gravity)*1.5f,"The descent has a stronger physical acceleration than ascent");
            Reset();game.Obstacles.SetActive(true);game.Motor.Speed=18;var obstacle=game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+18*game.Settings.JumpRiseSeconds);game.Motor.TryJump();bool clear=true;
            for(int i=0;i<65;i++){game.Obstacles.BeginMove(game.Motor);game.Motor.Tick(1f/60);clear&=game.Obstacles.Check(game.Motor)==null;}
            Check(clear&&game.Motor.Grounded,"The shorter arc physically clears the large black obstacle");
            Reset();game.Obstacles.SetActive(true);game.Motor.Speed=30;obstacle=game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+3);game.Obstacles.BeginMove(game.Motor);game.Motor.Tick(.2f);
            Check(game.Obstacles.Check(game.Motor)==obstacle,"Gravity tuning does not reintroduce obstacle pass-through");
            game.ReturnToMenu();var report=new Report{passed=errors.Count==0,utc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion,checks=checks.ToArray(),errors=errors.ToArray(),arcs=arcs.ToArray(),riseGravity=game.Settings.Gravity,fallGravity=game.Settings.FallGravity};
            File.WriteAllText(ReviewEvidence.File("../Validation/unity_jump_v7.json"),JsonUtility.ToJson(report,true));Application.logMessageReceived-=Log;Debug.Log("Jump v7 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void Reset(){game.enabled=false;game.Powerups.Enabled=false;game.Tokens.Enabled=false;game.Play();game.Obstacles.SetActive(false);game.Motor.Speed=0;}
        void Check(bool value,string label){if(value)checks.Add(label);else errors.Add(label);}
        void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception)errors.Add(text);}
        void OnDestroy(){Application.logMessageReceived-=Log;if(ready)Application.runInBackground=background;}
    }
}
#endif
