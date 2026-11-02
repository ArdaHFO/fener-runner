#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class RunFlowChecksV12 : MonoBehaviour
    {
        const string Flag="FenerRunner.RunFlowV12";
        [Serializable] sealed class Report { public bool passed; public string utc; public string[] checks,errors; public string scope="Pure progression boundaries and native Game.Update speed, pause, fatal collision and isolated mobile wallet checkpoints. Editor evidence, not physical-phone performance."; }
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        RunnerGame game; RunnerSettings source,fixture; RunWallet wallet; bool background; int realBalance;
        static readonly FieldInfo Settings=typeof(RunnerGame).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic);
        [MenuItem("Fener Runner/06 - Verify v12 run flow and mobile earnings")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Run flow v12 checks").AddComponent<RunFlowChecksV12>();}}
        IEnumerator Start()
        {
            background=Application.runInBackground;Application.runInBackground=true;Application.logMessageReceived+=Log;
            game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;realBalance=new RunWallet().Balance;
            source=game.Settings;fixture=Instantiate(source);Settings.SetValue(game,fixture);
            wallet=game.UseWalletForTest("FenerRunner.Test.FlowV12."+DateTime.UtcNow.Ticks+".");
            var routine=Scenarios();
            while(true){bool more;object wait=null;try{more=routine.MoveNext();if(more)wait=routine.Current;}catch(Exception e){errors.Add(e.ToString());break;}if(!more)break;yield return wait;}
            game.ReturnToMenu();Check(new RunWallet().Balance==realBalance,"Production wallet unchanged by isolated tests");
            var report=new Report{passed=errors.Count==0,utc=DateTime.UtcNow.ToString("O"),checks=checks.ToArray(),errors=errors.ToArray()};
            File.WriteAllText(ReviewEvidence.FileFor(12,"../Validation/unity_run_flow_v12.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Run flow v12 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Scenarios()
        {
            var flow=new RunFlow();flow.Reset();
            Check(flow.ChallengeTarget==4&&flow.BonusCoins==0&&flow.Multiplier==1,"First goal is four real clean rows, no free rewards");
            for(int i=0;i<3;i++)Check(flow.CleanPass()==0,"Opening clean row "+(i+1)+" keeps standard score");
            Check(flow.CleanPass()==10&&flow.CompletedChallenges==1&&flow.BonusCoins==15,"Fourth clean row starts skill multiplier and grants fifteen bonus coins once");
            flow.Collect(19);Check(flow.ChallengeProgress==19&&flow.CompletedChallenges==1,"Coin goal counts actual pickups, not multiplied coin value");
            flow.Collect(1);Check(flow.CompletedChallenges==2&&flow.BonusCoins==35&&flow.ChallengeProgress==0,"Coin reward does not recursively feed next goal");
            flow.Travel(349.5);Check(flow.ChallengeProgress==349,"Distance goal retains fractional progress");flow.Travel(.5);
            Check(flow.CompletedChallenges==3&&flow.BonusCoins==60&&flow.ChallengeTarget==5,"Goals rotate with bounded increasing difficulty and rewards");
            for(int i=0;i<4;i++)flow.CleanPass();
            Check(flow.Streak==8&&flow.BurstRemaining==6&&flow.SpeedBonus==1,"Eight consecutive clean rows earn six seconds of one-metre-per-second flow");
            flow.Tick(2);Check(flow.BurstRemaining==4,"Flow uses active game time");flow.Tick(-9);Check(flow.BurstRemaining==4,"Invalid negative tick cannot extend a burst");
            for(int i=0;i<50;i++)flow.CleanPass();Check(Mathf.Abs(flow.Multiplier-2)<.001f,"Skill score multiplier caps at two");
            int best=flow.BestStreak;flow.BreakStreak();Check(flow.Streak==0&&flow.SpeedBonus==0&&flow.BestStreak==best,"Contact breaks active combo and boost while retaining best run streak");
            flow.Reset();Check(flow.Streak==0&&flow.BonusCoins==0&&flow.CompletedChallenges==0&&flow.BurstRemaining==0,"Restart resets every progression value");
            double[] points={0,220,800,1800,100000};float[] speeds={18,24,30,34,34};
            for(int i=0;i<points.Length;i++)Check(Mathf.Abs(game.SpeedForDistance(points[i])-speeds[i])<.001f,"Production tempo milestone "+points[i]+"m = "+speeds[i]+"m/s");
            float previous=0;bool monotonic=true;for(int i=0;i<2200;i++){float current=game.SpeedForDistance(i);monotonic&=current>=previous;previous=current;}Check(monotonic,"Progressive base speed never drops at tier boundaries");
            fixture.forwardSpeed=fixture.maxSpeed=34;Begin(false);
            game.Powerups.Activate(PowerupKind.Dash);game.Powerups.Activate(PowerupKind.FeverRush);for(int i=0;i<8;i++)game.Flow.CleanPass();yield return null;yield return null;
            Check(Mathf.Abs(game.CurrentSpeed-42)<.001f,"Actual Game.Update combines late base, dash, fever and earned flow at hard cap42");
            Check(!game.Powerups.IsProtected,"Flow and dash give no collision invulnerability");
            game.Pause();float burst=game.Flow.BurstRemaining;double distance=game.Distance;yield return new WaitForSecondsRealtime(.15f);
            Check(game.Distance==distance&&game.Flow.BurstRemaining==burst,"Pause freezes both actual movement and skill burst");game.Resume();
            game.Powerups.Activate(PowerupKind.Shield);game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+.4f,game.Motor.Lane);yield return new WaitForSecondsRealtime(.25f);
            Check(game.State==RunState.Running&&game.Flow.Streak==0&&game.Flow.BurstRemaining==0,"Actual protected collision breaks permanent streak and earned tempo");
            fixture.forwardSpeed=fixture.maxSpeed=18;Begin(true);
            for(int i=0;i<4;i++)game.Flow.CleanPass();
            game.Tokens.PlaceForTest(0,new Vector3(0,1.12f,game.Motor.transform.position.z+.4f));yield return null;yield return null;
            Check(game.Coins>0&&game.RunBonusCoins==15,"Earned challenge currency stays separate from physical coin counter");
            int starting=game.WalletBalance,earned=game.RunEarnings;
            game.SendMessage("OnApplicationPause",true);Check(game.State==RunState.Paused&&game.WalletBalance==starting+earned,"Mobile background callback checkpoints coin and goal earnings");
            game.SendMessage("OnApplicationPause",true);game.CheckpointEarnings();Check(game.WalletBalance==starting+earned,"Repeated pause and checkpoint cannot double-pay");
            game.Resume();game.Tokens.PlaceForTest(1,new Vector3(0,1.12f,game.Motor.transform.position.z+.4f));yield return null;yield return null;
            int finalEarned=game.RunEarnings;Check(finalEarned>earned,"Resumed run can earn more after a checkpoint");
            game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z+.4f,game.Motor.Lane);yield return new WaitForSecondsRealtime(.15f);
            Check(game.State==RunState.Finished&&game.WalletBalance==starting+finalEarned,"Fatal collision settles only the unpaid delta including goal rewards");
            game.SendMessage("OnApplicationPause",true);float death=game.Animation.DeathProgress;yield return new WaitForSecondsRealtime(.15f);
            Check(game.Animation.DeathPresentationPaused&&Mathf.Abs(game.Animation.DeathProgress-death)<.001f,"Background callback freezes unscaled death presentation");game.SendMessage("OnApplicationPause",false);
            game.ReturnToMenu();game.ReturnToMenu();Check(game.WalletBalance==starting+finalEarned,"Repeated result/menu resets do not re-credit settled earnings");
            Check(game.Flow.Streak==0&&game.RunBonusCoins==0,"Menu resets skill and reward UI source data");
            Begin(false);for(int i=0;i<4;i++)game.Flow.CleanPass();game.Flow.Collect(20);game.Flow.Travel(349.8);
            game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z,game.Motor.Lane);yield return null;yield return null;
            Check(game.State==RunState.Finished&&game.Flow.CompletedChallenges==2&&game.RunBonusCoins==35,"Distance goal cannot be completed using movement beyond an immediate fatal contact");
        }
        void Begin(bool coins)
        {
            game.Obstacles.Enabled=true;game.Tokens.Enabled=coins;game.Powerups.Enabled=true;game.Play();game.Obstacles.SetActive(false);game.Powerups.IsolatePickupsForTest();
            if(coins)for(int i=0;i<game.Tokens.PoolCount;i++)game.Tokens.PlaceForTest(i,new Vector3(20,1,1000+i*4));
        }
        void Check(bool ok,string name){checks.Add((ok?"PASS ":"FAIL ")+name);if(!ok)errors.Add(name);}
        void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text);}
        void OnDestroy(){Application.logMessageReceived-=Log;Application.runInBackground=background;if(game!=null&&source!=null)Settings.SetValue(game,source);if(wallet!=null)wallet.DeleteIsolatedTestData();if(fixture!=null)Destroy(fixture);}
    }
}
#endif
