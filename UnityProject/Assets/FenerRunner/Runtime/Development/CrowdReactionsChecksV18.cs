#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class CrowdReactionsChecksV18:MonoBehaviour
    {
        const string Flag="FenerRunner.CrowdReactionsV18";
        [Serializable] sealed class Report
        {
            public bool passed;public string utc;public string[] checks,errors;
            public int nativeCleanPasses,ambientEvents,observedTypes;public long steadyTickAllocatedBytes;
            public float baseCrowdVolume,reactingCrowdVolume,reactingPan;
            public string scope="Native game-command, real token/power collection and route-following clean-pass hooks; bounded priority/coalescing, pause/menu/death/restart/disable/rebase, actual six-source recorded crowd gain and volume controls. Shader visual diversity is checked separately. No physical-phone performance claim.";
        }
        readonly List<string> checks=new List<string>(),errors=new List<string>();readonly Report report=new Report();
        RunnerGame game;CrowdReactions crowd;StadiumSound sound;RunWallet wallet;
        bool prepared,background,audioIsolated;int oldRate;float oldCapture,oldScale;
        [MenuItem("Fener Runner/111 - Verify crowd reactions v18")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod]static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Crowd reaction checks v18").AddComponent<CrowdReactionsChecksV18>();}}
        IEnumerator Start()
        {
            background=Application.runInBackground;oldRate=Application.targetFrameRate;oldCapture=Time.captureDeltaTime;oldScale=Time.timeScale;prepared=true;
            Application.runInBackground=true;Application.targetFrameRate=60;Time.captureDeltaTime=1f/60;Application.logMessageReceived+=Log;
            var run=Run();while(true){bool more;object value=null;try{more=run.MoveNext();if(more)value=run.Current;}catch(Exception e){errors.Add(e.ToString());break;}if(!more)break;yield return value;}
            if(game!=null){game.enabled=false;game.ReturnToMenu();}
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.checks=checks.ToArray();report.errors=errors.ToArray();
            File.WriteAllText(Evidence("unity_crowd_reactions_v18.json"),JsonUtility.ToJson(report,true));Application.logMessageReceived-=Log;
            Debug.Log("Crowd reactions v18 "+(report.passed?"PASSED":"FAILED")+": "+checks.Count+" checks; "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Run()
        {
            game=FindFirstObjectByType<RunnerGame>();if(game==null)throw new InvalidOperationException("Actual Fener Runner scene required.");
            game.AutomatedTest=true;game.enabled=false;wallet=game.UseWalletForTest("FenerRunner.Test.CrowdReactionsV18."+DateTime.UtcNow.Ticks+".");
            sound=game.GetComponent<StadiumSound>();sound.UseAudioPreferencesForTest("FenerRunner.Test.CrowdAudioV18."+DateTime.UtcNow.Ticks+".");audioIsolated=true;
            crowd=game.CrowdReactions;Check(crowd!=null,"Actual game creates its crowd reaction director");if(crowd==null)yield break;
            Reset();Check(crowd.ReactionAge<0&&crowd.ReactionCount==0&&crowd.AudioExcitement==0,"Fresh run has no stale reaction or audio envelope");
            game.HandleCommand(RunnerCommand.Jump);
            Check(game.Motor.JumpCount==1&&crowd.JumpRequests==1&&crowd.LastKind==CrowdReactionKind.Jump,"Accepted real jump immediately starts an applause reaction");
            game.HandleCommand(RunnerCommand.Jump);game.HandleCommand(RunnerCommand.Jump);
            Check(game.Motor.JumpCount==2&&crowd.JumpRequests==2&&crowd.AirJumpRequests==1,"Only the accepted second jump earns an air-jump response; rejected third input earns none");
            crowd.Tick(.5f);Check(crowd.AudioExcitement>.4f&&crowd.AudioPan>0&&crowd.ReactionRadius>=12,"A local action builds an audible directional reaction envelope");
            Vector4 heldData=Shader.GetGlobalVector("_CrowdReactionData"),heldPlayer=Shader.GetGlobalVector("_CrowdPlayer");float clock=crowd.PresentationTime,age=crowd.ReactionAge,gain=crowd.AudioExcitement;
            game.Pause();game.HandleCommand(RunnerCommand.Jump);crowd.Tick(4);
            Check(crowd.JumpRequests==2&&crowd.PresentationTime==clock&&crowd.ReactionAge==age&&crowd.AudioExcitement==gain&&Shader.GetGlobalVector("_CrowdReactionData")==heldData&&Shader.GetGlobalVector("_CrowdPlayer")==heldPlayer,"Pause rejects input and holds the same pose, attention and audio envelope without snapping");
            game.Resume();crowd.Tick(.1f);Check(crowd.ReactionAge>age,"Resume continues the same local reaction");
            Reset();game.HandleCommand(RunnerCommand.Slide);game.HandleCommand(RunnerCommand.Slide);game.HandleCommand(RunnerCommand.Slide);
            Check(crowd.SlideRequests==2&&crowd.ChainedSlideRequests==1&&game.Motor.SlideCount==2,"Real double-slide cues count both accepted cycles and reject a third queued swipe");

            Reset();game.HandleCommand(RunnerCommand.Jump);game.Powerups.Activate(PowerupKind.Magnet);
            Check(crowd.PowerRequests==1&&crowd.HasPending&&crowd.PendingKind==CrowdReactionKind.Power,"Real power activation queues a higher-priority two-stand salute");
            for(int i=0;i<220;i++){crowd.NotifySlide(false);crowd.Tick(1f/60);}
            Check(crowd.LastKind==CrowdReactionKind.Power&&crowd.ReactionCount==2&&crowd.ReactionSide==0,"Repeated movement cues cannot evict a waiting power salute or reset its protected animation");
            var types=new HashSet<CrowdReactionKind>{CrowdReactionKind.Jump,CrowdReactionKind.Slide,CrowdReactionKind.Power};
            Reset();game.Tokens.Enabled=true;for(int i=0;i<game.Tokens.PoolCount;i++)game.Tokens.PlaceForTest(i,new Vector3(20,1,1000+i*4));
            for(int i=0;i<6;i++)game.Tokens.PlaceForTest(i,new Vector3(0,1.05f,game.Motor.transform.position.z+.25f));
            game.AdvanceForTest(1f/60);
            Check(game.Tokens.TotalCollected==6&&crowd.TokensObserved==6&&crowd.TokenStreakRequests==1&&crowd.LastKind==CrowdReactionKind.CoinStreak,"Six genuinely collected chicks trigger one rhythmic crowd response through the production pickup path");
            types.Add(crowd.LastKind);

            Reset();game.Obstacles.UseSeedForTest(1907);game.Obstacles.Enabled=true;game.Play();game.enabled=false;game.Tokens.Enabled=false;game.Powerups.Enabled=false;game.Powerups.IsolatePickupsForTest();
            int actioned=-1;
            for(int frame=0;frame<1800&&game.State==RunState.Running&&game.Obstacles.CleanPassed<3;frame++)
            {
                float z=game.Motor.transform.position.z;
                if(game.Obstacles.TryGetNextRoute(z,out CourseRoute route))
                {
                    float ahead=route.Z-z;
                    if(ahead<=CoursePatternGenerator.LaneChangeLead&&game.Motor.Lane!=route.Lane)game.HandleCommand(game.Motor.Lane<route.Lane?RunnerCommand.Right:RunnerCommand.Left);
                    if(route.Sequence!=actioned&&ahead>0&&ahead<=game.CurrentSpeed*(route.Action==RouteAction.Jump?game.Settings.JumpRiseSeconds:.42f)&&game.Motor.Lane==route.Lane)
                    {if(route.Action==RouteAction.Jump)game.HandleCommand(RunnerCommand.Jump);if(route.Action==RouteAction.Slide)game.HandleCommand(RunnerCommand.Slide);actioned=route.Sequence;}
                }
                game.AdvanceForTest(1f/60);crowd.Tick(1f/60);
            }
            report.nativeCleanPasses=game.Obstacles.CleanPassed;
            Check(game.State==RunState.Running&&report.nativeCleanPasses>=3&&crowd.CleanPassRequests==report.nativeCleanPasses,"Real generated obstacles cleared along their published route notify the crowd exactly once per clean row");

            Reset();int objects=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            int score=game.Score,coins=game.Coins;double distance=game.Distance;Vector3 player=game.Motor.transform.position;
            string random=JsonUtility.ToJson(UnityEngine.Random.state);
            for(int i=0;i<60*180;i++){crowd.Tick(1f/60);if(crowd.ReactionAge>=0)types.Add(crowd.LastKind);}
            report.ambientEvents=crowd.AmbientReactionCount;report.observedTypes=types.Count;
            Check(report.ambientEvents>=10&&report.ambientEvents<=17&&types.Contains(CrowdReactionKind.Arrival)&&types.Contains(CrowdReactionKind.Wave),"Quiet periods still contain occasional greeting and coordinated wave bursts with rests");
            Check(report.observedTypes>=6,"The scene exposes six distinct used reaction families across actions and ambient behaviour");
            Check(game.Score==score&&game.Coins==coins&&game.Distance==distance&&game.Motor.transform.position==player&&JsonUtility.ToJson(UnityEngine.Random.state)==random,"Crowd presentation cannot affect motion, score, currency or gameplay randomness");
            Check(objects==FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,"Three minutes of crowd reactions allocate no per-fan or per-event GameObjects");
            for(int i=0;i<64;i++)crowd.Tick(1f/60);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<360;i++)crowd.Tick(1f/60);report.steadyTickAllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(report.steadyTickAllocatedBytes==0,"Steady director ticks allocate zero managed bytes on the native Editor thread");

            foreach(int hz in new[]{15,30,60,120})
            {
                Reset();game.HandleCommand(RunnerCommand.Jump);float max=0;
                for(int i=0;i<hz*5;i++){crowd.Tick(1f/hz);max=Mathf.Max(max,crowd.AudioExcitement);}
                Check(max>.6f&&max<=1&&crowd.ReactionAge<0&&crowd.AudioExcitement==0,"Reaction attack/recovery completes and returns to silence at "+hz+"Hz");
            }
            Reset();game.HandleCommand(RunnerCommand.Slide);crowd.Tick(.5f);
            Vector3 origin=crowd.ReactionOrigin;heldPlayer=Shader.GetGlobalVector("_CrowdPlayer");age=crowd.ReactionAge;
            crowd.ShiftOrigin(1024);game.Course.ShiftOrigin(1024);game.Motor.ShiftOrigin(1024);
            Check((crowd.ReactionOrigin-(origin-Vector3.forward*1024)).sqrMagnitude<.000001f&&crowd.ReactionAge==age&&Mathf.Abs(Shader.GetGlobalVector("_CrowdPlayer").z-heldPlayer.z+1024)<.001f,"Origin rebasing preserves the active age and shifts both cheering pocket and player attention together");
            crowd.Tick(.01f);Check(Mathf.Abs(crowd.ReactionOrigin.z-origin.z+1024)<.01f,"Moving attention continues at the same relative position after rebasing");
            Reset();game.HandleCommand(RunnerCommand.Jump);crowd.Tick(.4f);clock=crowd.PresentationTime;age=crowd.ReactionAge;
            foreach(float dt in new[]{0f,-1f,float.NaN,float.PositiveInfinity})crowd.Tick(dt);
            Check(crowd.PresentationTime==clock&&crowd.ReactionAge==age,"Invalid time steps cannot damage or rewind crowd presentation");
            crowd.enabled=false;crowd.Tick(1);game.HandleCommand(RunnerCommand.Slide);
            Check(crowd.ReactionAge<0&&crowd.AudioExcitement==0&&!crowd.HasPending&&Shader.GetGlobalVector("_CrowdPlayer").w==0,"Disabling clears shader/audio/pending state and manual ticks cannot reactivate it");crowd.enabled=true;
            Reset();game.HandleCommand(RunnerCommand.Slide);crowd.Tick(.4f);game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z);game.AdvanceForTest(1f/60);crowd.Tick(0);
            Check(game.State==RunState.Finished&&crowd.ReactionAge<0&&crowd.AudioExcitement==0&&!crowd.HasPending&&Shader.GetGlobalVector("_CrowdPlayer").w==0,"Actual fatal contact clears all reaction state even with zero scaled delta");
            game.Restart();game.enabled=false;Check(crowd.ReactionCount==0&&crowd.PowerRequests==0&&crowd.PresentationTime==0,"Restart clears all counters and pending reactions");game.ReturnToMenu();crowd.Tick(5);
            Check(crowd.ReactionAge<0&&crowd.ReactionCount==0&&Shader.GetGlobalVector("_CrowdPlayer").w==0,"Menu never starts gameplay chants or points fan attention at a running player");

            Reset();sound.SetMutedForTest(false);sound.SetVolume(AudioChannel.Master,1);sound.SetVolume(AudioChannel.Effects,1);sound.SetVolume(AudioChannel.Voices,0);sound.SetVolume(AudioChannel.Music,0);sound.SetChatterDueForTest(999);sound.SetRunMusicDueForTest(999);
            for(int i=0;i<30;i++)yield return null;
            report.baseCrowdVolume=sound.CrowdSource.volume;
            game.HandleCommand(RunnerCommand.Jump);crowd.Tick(.6f);
            for(int i=0;i<30;i++)yield return null;
            report.reactingCrowdVolume=sound.CrowdSource.volume;report.reactingPan=sound.CrowdSource.panStereo;
            Check(sound.HasRecordedCrowd&&sound.CrowdSource.clip!=null&&sound.CrowdSource.isPlaying&&report.reactingCrowdVolume>report.baseCrowdVolume+.05f,"Actual existing fan recording audibly lifts during the action response");
            Check(report.reactingPan>.05f&&report.reactingPan<=.221f&&sound.GetComponents<AudioSource>().Length==StadiumSound.SourceCount,"Reaction lightly pans toward its stand while preserving the existing six-source audio budget");
            sound.SetMutedForTest(true);yield return null;Check(sound.CrowdSource.mute,"The new crowd lift still obeys the player's mute control");
            sound.SetMutedForTest(false);sound.SetVolume(AudioChannel.Effects,0);yield return null;Check(sound.CrowdSource.volume==0,"Effects volume zero silences the crowd response");
            sound.SetVolume(AudioChannel.Effects,1);sound.SetVolume(AudioChannel.Master,0);yield return null;Check(sound.CrowdSource.volume==0,"Master volume zero silences the crowd response");
        }
        void Reset()
        {game.enabled=false;game.Tokens.Enabled=false;game.Powerups.Enabled=true;game.Play();game.Obstacles.SetActive(false);game.Powerups.IsolatePickupsForTest();crowd.ResetPresentation();}
        static string Evidence(string file){string path=ReviewEvidence.FileFor(18,"../Validation/"+file);Directory.CreateDirectory(Path.GetDirectoryName(path));return path;}
        void Check(bool value,string label){if(value)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<60)errors.Add(message);}
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;if(!prepared)return;
            if(audioIsolated&&sound!=null)sound.RestoreAudioPreferencesForTest();if(wallet!=null)wallet.DeleteIsolatedTestData();
            Application.runInBackground=background;Application.targetFrameRate=oldRate;Time.captureDeltaTime=oldCapture;Time.timeScale=oldScale;
        }
    }
}
#endif
