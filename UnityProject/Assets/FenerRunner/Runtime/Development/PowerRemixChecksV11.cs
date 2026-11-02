#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class PowerRemixChecksV11:MonoBehaviour
    {
        const string Flag="FenerRunner.PowerRemixV11";
        [Serializable] sealed class Report {public bool passed;public string utc;public string[] checks,errors;public int types,normalPool,bonusPool;public string scope="Real Editor game flow: physical jump/slide pass rewards, accepted/rejected lane inputs, pooled bonuses, extension stacking, pause/restart and no hidden invulnerability. No device claim.";}
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        RunnerGame game;RunnerPowerups powers;bool background;int balance;
        [MenuItem("Fener Runner/54 - Verify power remixes v11")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Power remix checks v11").AddComponent<PowerRemixChecksV11>();}}
        IEnumerator Start()
        {
            background=Application.runInBackground;Application.runInBackground=true;Application.logMessageReceived+=Log;
            game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;powers=game.Powerups;balance=game.WalletBalance;
            yield return null;var routine=Scenarios();
            while(true){bool more;object current=null;try{more=routine.MoveNext();if(more)current=routine.Current;}catch(Exception e){errors.Add(e.ToString());break;}if(!more)break;yield return current;}
            Check(game.WalletBalance==balance,"Real wallet preserved");
            var report=new Report{passed=errors.Count==0,utc=DateTime.UtcNow.ToString("O"),checks=checks.ToArray(),errors=errors.ToArray(),types=RunnerPowerups.TypeCount,normalPool=powers.PoolCount,bonusPool=game.Tokens.BonusPoolCount};
            File.WriteAllText(ReviewEvidence.FileFor(11,"../Validation/unity_power_remix_v11.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Power remixes v11 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void Begin(bool coins=false)
        {
            powers.Enabled=true;game.Tokens.Enabled=coins;game.Obstacles.Enabled=true;game.Play();powers.IsolatePickupsForTest();
            game.Obstacles.SetActive(false);
            if(coins)for(int i=0;i<game.Tokens.PoolCount;i++)game.Tokens.PlaceForTest(i,new Vector3(15,1,game.Motor.transform.position.z+1000+i*4));
        }
        IEnumerator Scenarios()
        {
            Begin();Check(powers.PoolCount==15,"Fifteen ability objects are available without runtime pool growth");
            powers.Activate(PowerupKind.Rhythm);game.Obstacles.SetActive(true);
            int actioned=-1,before=-1;float deadline=Time.realtimeSinceStartup+8;
            while(game.State==RunState.Running&&game.Obstacles.Passed<2&&Time.realtimeSinceStartup<deadline)
            {
                if(game.Obstacles.TryGetNextRoute(game.Motor.transform.position.z,out CourseRoute route))
                {
                    float ahead=route.Z-game.Motor.transform.position.z;
                    if(route.Sequence!=actioned&&ahead>0&&ahead<=game.CurrentSpeed*(route.Action==RouteAction.Jump?game.Settings.JumpRiseSeconds:.42f))
                    {game.HandleCommand(route.Action==RouteAction.Jump?RunnerCommand.Jump:RunnerCommand.Slide);actioned=route.Sequence;}
                }
                if(game.Obstacles.Passed==1&&before<0)
                {before=game.Score-(int)game.Distance-game.Coins*25-game.Obstacles.Passed*100;Check(powers.RhythmChain==1,"A real successful jump starts the rhythm reward chain");}
                yield return null;
            }
            Check(before==50,"First clean rhythm row earns fifty extra points");
            int after=game.Score-(int)game.Distance-game.Coins*25-game.Obstacles.Passed*100;
            Check(game.State==RunState.Running&&powers.RhythmChain==2&&after-before==100,"A physical slide through the next gate increases the bonus to one hundred");
            powers.Activate(PowerupKind.Shield);game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+3);
            yield return new WaitForSecondsRealtime(.35f);Check(game.State==RunState.Running&&powers.RhythmChain==0,"A consumed shield breaks the clean rhythm chain");

            Begin(true);powers.Activate(PowerupKind.GoldenTrail);var tokenObjects=game.Tokens.GetComponentsInChildren<Transform>(true);
            game.HandleCommand(RunnerCommand.Left);Check(powers.TrailVolleyCount==1&&game.Tokens.BonusSpawned==4,"Accepted lane change creates four pooled reward coins");
            game.HandleCommand(RunnerCommand.Left);Check(powers.TrailVolleyCount==1,"A blocked edge swipe earns no volley");
            game.HandleCommand(RunnerCommand.Right);Check(powers.TrailVolleyCount==1,"Rapid reversal cannot farm repeated immediate volleys");
            yield return new WaitForSecondsRealtime(.86f);game.HandleCommand(RunnerCommand.Right);
            Check(powers.TrailVolleyCount==2&&game.Tokens.BonusSpawned>=8,"A later accepted lane change gives another reward pass");
            var afterVolley=game.Tokens.GetComponentsInChildren<Transform>(true);
            bool samePool=tokenObjects.Length==afterVolley.Length;
            for(int i=0;i<tokenObjects.Length&&samePool;i++)samePool=tokenObjects[i]==afterVolley[i];
            Check(samePool&&game.Tokens.PoolCount==48&&game.Tokens.BonusPoolCount==20,"Lane rewards reuse the same fixed token objects, including inactive children");
            game.Pause();float remaining=powers.GoldenTrailRemaining;int count=powers.TrailVolleyCount;
            yield return new WaitForSecondsRealtime(.2f);game.HandleCommand(RunnerCommand.Left);
            Check(powers.GoldenTrailRemaining==remaining&&powers.TrailVolleyCount==count,"Pause freezes the power and ignores lane reward requests");game.Resume();
            Check(!powers.IsProtected&&!powers.TryAbsorbHit(),"GoldenTrail does not silently prevent death");

            Begin();powers.Activate(PowerupKind.Dash);powers.Activate(PowerupKind.Rhythm);yield return new WaitForSecondsRealtime(.35f);
            float dash=powers.DashRemaining,rhythm=powers.RhythmRemaining;powers.Activate(PowerupKind.Encore);
            Check(powers.LastExtendedCount==2&&Mathf.Abs(powers.DashRemaining-dash-2)<.02f&&Mathf.Abs(powers.RhythmRemaining-rhythm-2)<.02f,"Encore extends each active ability by two seconds");
            for(int i=0;i<5;i++)powers.Activate(PowerupKind.Encore);
            Check(powers.DashRemaining<=game.PowerupDuration(PowerupKind.Dash)+2.01f&&powers.RhythmRemaining<=game.PowerupDuration(PowerupKind.Rhythm)+2.01f,"Repeated Encore collection has a bounded duration cap");
            Check(!powers.IsProtected&&!powers.TryAbsorbHit(),"Encore cannot create protection when no shield is active");
            Begin();powers.Activate(PowerupKind.FeverRush);powers.Activate(PowerupKind.Encore);
            Check(Mathf.Abs(game.FeverRemaining-powers.FeverRushRemaining)<.02f&&game.FeverRemaining>9.9f,"Encore extends the actual fever benefit as well as its displayed timer");
            yield return new WaitForSecondsRealtime(8.15f);
            Check(game.FeverActive&&powers.FeverRushRemaining>0&&game.CoinValue==2,"Extended fever still grants its real coin bonus after the original eight-second expiry");
            Begin();powers.Activate(PowerupKind.Encore);Check(powers.LastExtendedCount==0&&Mathf.Abs(powers.MagnetRemaining-3)<.01f,"Standalone Encore gives a useful three-second magnet");
            game.Restart();bool clear=true;foreach(PowerupKind kind in Enum.GetValues(typeof(PowerupKind)))clear&=powers.Remaining(kind)==0;
            Check(clear&&powers.RhythmChain==0&&powers.TrailVolleyCount==0,"Restart clears all fifteen effects and their counters");
        }
        void Check(bool ok,string label){checks.Add((ok?"PASS ":"FAIL ")+label);if(!ok)errors.Add(label);}
        void Log(string text,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)errors.Add(text);}
        void OnDestroy(){Application.logMessageReceived-=Log;Application.runInBackground=background;}
    }
}
#endif
