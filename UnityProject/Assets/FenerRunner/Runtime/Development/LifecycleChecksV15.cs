#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class LifecycleChecksV15:MonoBehaviour
    {
        const string Flag="FenerRunner.LifecycleChecksV15";
        [Serializable] sealed class Report
        {
            public bool passed,preferencesPreserved;
            public string utc;
            public string[] checks,errors;
            public string scope="Native scene death/camera presentation under both interleavings of focus and application-pause callbacks, repeated signals and ordinary run resume. Same production lifecycle helper, isolated test wallet, no APK or claim of physical-device callback ordering.";
        }
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        RunnerGame game;RunnerAnimation motion;FollowCamera cameraRig;RunWallet wallet;
        bool prepared,background,oldAutomated,oldEnabled,oldTokens,oldPowers,oldObstacles;
        float oldScale,oldCapture;int oldRate;string preferences;
        [MenuItem("Fener Runner/103 - Verify application lifecycle v15")]
        public static void StartChecks()
        {if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before lifecycle checks.");SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("V15 application lifecycle checks").AddComponent<LifecycleChecksV15>();}}
        IEnumerator Start()
        {
            background=Application.runInBackground;oldScale=Time.timeScale;oldCapture=Time.captureDeltaTime;oldRate=Application.targetFrameRate;
            prepared=true;preferences=PreferenceSnapshot();Application.runInBackground=true;Application.targetFrameRate=60;Time.captureDeltaTime=1f/60;
            Application.logMessageReceived+=Log;
            var routine=Scenarios();
            while(true)
            {
                bool more;object wait=null;
                try{more=routine.MoveNext();if(more)wait=routine.Current;}catch(Exception e){errors.Add(e.ToString());break;}
                if(!more)break;yield return wait;
            }
            if(game!=null)
            {game.SetApplicationPauseForTest(false);game.SetApplicationFocusForTest(true);game.ReturnToMenu();}
            bool preserved=preferences==PreferenceSnapshot();Check(preserved,"Real wallet, upgrades, best records and audio preferences unchanged");
            var report=new Report{passed=errors.Count==0,preferencesPreserved=preserved,utc=DateTime.UtcNow.ToString("O"),checks=checks.ToArray(),errors=errors.ToArray()};
            string path=ReviewEvidence.FileFor(15,"../Validation/unity_lifecycle_v15.json");Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Lifecycle v15 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Scenarios()
        {
            game=FindFirstObjectByType<RunnerGame>();cameraRig=FindFirstObjectByType<FollowCamera>();
            if(game==null||cameraRig==null)throw new InvalidOperationException("The actual game scene and follow camera are required.");
            oldAutomated=game.AutomatedTest;oldEnabled=game.enabled;oldTokens=game.Tokens.Enabled;oldPowers=game.Powerups.Enabled;oldObstacles=game.Obstacles.Enabled;
            game.AutomatedTest=true;game.enabled=false;motion=game.Animation;game.ReturnToMenu();
            wallet=game.UseWalletForTest("FenerRunner.Test.LifecycleV15."+DateTime.UtcNow.Ticks+".");
            for(int order=0;order<2;order++)
            {
                Begin();game.Obstacles.ArrangeTest(ObstacleKind.JumpOver,game.Motor.transform.position.z,game.Motor.Lane);game.AdvanceForTest(1f/60);
                Check(game.State==RunState.Finished&&motion.DeathPlaying,"Actual obstacle collision starts native death presentation / order "+order);
                if(order==0){game.SetApplicationFocusForTest(false);game.SetApplicationPauseForTest(true);}
                else{game.SetApplicationPauseForTest(true);game.SetApplicationFocusForTest(false);}
                float death=motion.DeathProgress;Vector3 position=cameraRig.transform.position;Quaternion rotation=cameraRig.transform.rotation;float fov=cameraRig.GetComponent<Camera>().fieldOfView;
                if(order==0)game.SetApplicationPauseForTest(false);else game.SetApplicationFocusForTest(true);
                Check(motion.DeathPresentationPaused,"Clearing only one background signal retains death pause / order "+order);
                yield return new WaitForSecondsRealtime(.12f);
                Check(motion.DeathProgress==death&&cameraRig.transform.position==position&&Quaternion.Angle(rotation,cameraRig.transform.rotation)<.0001f&&cameraRig.GetComponent<Camera>().fieldOfView==fov,
                    "Native unscaled animation and camera remain frozen after partial resume / order "+order);
                if(order==0)game.SetApplicationFocusForTest(true);else game.SetApplicationPauseForTest(false);
                Check(!motion.DeathPresentationPaused,"Both signals clear the death pause / order "+order);
                yield return new WaitForSecondsRealtime(.12f);
                Check(motion.DeathProgress>death&&game.State==RunState.Finished,"Native death playback resumes without restarting the run / order "+order);
                game.SetApplicationPauseForTest(false);game.SetApplicationFocusForTest(true);
                Check(!motion.DeathPresentationPaused,"Repeated foreground notifications remain idempotent / order "+order);
            }
            for(int order=0;order<2;order++)
            {
                Begin();double distance=game.Distance;
                if(order==0){game.SetApplicationFocusForTest(false);game.SetApplicationPauseForTest(true);game.SetApplicationPauseForTest(false);game.SetApplicationFocusForTest(true);}
                else{game.SetApplicationPauseForTest(true);game.SetApplicationFocusForTest(false);game.SetApplicationFocusForTest(true);game.SetApplicationPauseForTest(false);}
                game.AdvanceForTest(.2f);yield return null;
                Check(game.State==RunState.Paused&&game.Distance==distance,"Foreground callbacks never auto-resume an ordinary paused run / order "+order);
                game.Resume();game.AdvanceForTest(1f/60);
                Check(game.State==RunState.Running&&game.Distance>distance,"Explicit player resume still advances the real motor / order "+order);
            }
        }
        void Begin()
        {
            game.SetApplicationPauseForTest(false);game.SetApplicationFocusForTest(true);
            game.Tokens.Enabled=false;game.Powerups.Enabled=false;game.Obstacles.Enabled=true;
            game.Play();game.Obstacles.SetActive(false);game.Powerups.IsolatePickupsForTest();
        }
        static string PreferenceSnapshot()
        {
            var values=new List<string>();
            foreach(string key in new[]{RunWallet.DefaultPrefix+"Coins",RunWallet.DefaultPrefix+"Upgrade.0",RunWallet.DefaultPrefix+"Upgrade.1",RunWallet.DefaultPrefix+"Upgrade.2","FenerRunner.BestScore","FenerRunner.Muted"})
                values.Add(key+"="+PlayerPrefs.HasKey(key)+"/"+PlayerPrefs.GetInt(key,0));
            foreach(string key in new[]{"FenerRunner.Best",StadiumSound.DefaultAudioPrefix+"Master",StadiumSound.DefaultAudioPrefix+"Music",StadiumSound.DefaultAudioPrefix+"Voices",StadiumSound.DefaultAudioPrefix+"Effects"})
                values.Add(key+"="+PlayerPrefs.HasKey(key)+"/"+PlayerPrefs.GetFloat(key,0).ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            return string.Join("|",values);
        }
        void Check(bool valid,string message){if(valid)checks.Add(message);else errors.Add(message);}
        void Log(string message,string trace,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<60)errors.Add(message);}
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;if(!prepared)return;
            // Never reset a scene while Unity is destroying it. Only owned global
            // settings and the isolated preference prefix require cleanup here.
            if(game!=null){game.AutomatedTest=oldAutomated;game.enabled=oldEnabled;game.Tokens.Enabled=oldTokens;game.Powerups.Enabled=oldPowers;game.Obstacles.Enabled=oldObstacles;}
            if(wallet!=null)wallet.DeleteIsolatedTestData();Application.runInBackground=background;Application.targetFrameRate=oldRate;Time.captureDeltaTime=oldCapture;Time.timeScale=oldScale;
        }
    }
}
#endif
