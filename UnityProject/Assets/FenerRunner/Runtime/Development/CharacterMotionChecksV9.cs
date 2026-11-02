#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class CharacterMotionChecksV9:MonoBehaviour
    {
        [Serializable] sealed class Gesture
        {
            public string name;public float seconds,maximumHandTravel,maximumFootDrift,endPoseAngleError,waveHandHeight;
        }
        [Serializable] sealed class Report
        {
            public bool passed,preferencesPreserved;
            public string utc,unityVersion;
            public string[] checks,errors;
            public Gesture[] gestures;
            public float peakReactionWeight,maximumRewardHandTravel,maximumLowerBodyPositionError,maximumLowerBodyAngleError;
            public int menuGestureStarts,rewardStarts;
            public string image="v9-character-gestures.png";
            public string imageRows="MenuWave, MenuPride, MenuReady; columns at 20%, 40%, 60%, 85% of each authored clip.";
            public string scope="Real Unity clip sampling, menu animation on unscaled time, contextual running upper-body motion compared with the same Run phase, explicit pause and airborne suppression. Image contains the actual mascot rendered in the stadium scene. No root motion, collision or phone performance claim.";
        }
        const string Flag="FenerRunner.CharacterMotionV9",Animations="Assets/FenerRunner/Generated/Animations/";
        readonly List<string> checks=new List<string>(),errors=new List<string>();readonly List<Gesture> gestures=new List<Gesture>();
        Report report=new Report();RunnerGame game;RunnerAnimation motion;RunWallet wallet;Transform[] bones;
        GameObject reference;Transform[] referenceBones;bool background,ready;float previousScale;int oldCoins,oldScore,oldMute;float oldBest;
        Camera viewCamera;RenderTexture oldTarget;

        [MenuItem("Fener Runner/46 - Verify mascot gestures and reward reactions")]
        public static void StartChecks(){if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before character checks.");SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Character motion v9 checks").AddComponent<CharacterMotionChecksV9>();}}
        IEnumerator Start()
        {
            background=Application.runInBackground;previousScale=Time.timeScale;Application.runInBackground=true;Application.logMessageReceived+=Log;
            oldCoins=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0);oldScore=PlayerPrefs.GetInt("FenerRunner.BestScore",0);oldBest=PlayerPrefs.GetFloat("FenerRunner.Best",0);oldMute=PlayerPrefs.GetInt("FenerRunner.Muted",0);
            game=FindFirstObjectByType<RunnerGame>();if(game==null){Check(false,"RunnerGame exists");Finish();yield break;}
            game.AutomatedTest=true;wallet=game.UseWalletForTest("FenerRunner.Test.CharacterV9."+DateTime.UtcNow.Ticks+".");
            motion=game.Animation;motion.TurnAllowed=false;bones=motion.Animator.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            game.ReturnToMenu();game.enabled=false;motion.Animator.enabled=false;ready=true;
            MeasureClips();yield return new WaitForSecondsRealtime(.5f);yield return CaptureGestures();
            motion.Animator.enabled=true;game.ReturnToMenu();game.enabled=true;
            int menuBefore=motion.MenuGestureCount;Time.timeScale=0;float until=Time.realtimeSinceStartup+1.2f;
            while(Time.realtimeSinceStartup<until)yield return null;
            Check(motion.CurrentClip=="MenuWave"&&motion.MenuGestureCount==menuBefore+1&&motion.Animator.updateMode==AnimatorUpdateMode.UnscaledTime,
                "The menu starts an authored greeting even when gameplay time is zero");
            Quaternion menuHand=Bone("hand.R").localRotation;yield return new WaitForSecondsRealtime(.25f);
            Check(Quaternion.Angle(menuHand,Bone("hand.R").localRotation)>.5f,"The visible menu hand keeps moving on unscaled time");
            until=Time.realtimeSinceStartup+2.2f;while(motion.CurrentClip!="Idle"&&Time.realtimeSinceStartup<until)yield return null;
            Check(motion.CurrentClip=="Idle"&&motion.MenuGestureRemaining==0,"The greeting recovers naturally to Idle");
            bool tapped=motion.PlayMenuGesture();string tappedClip=motion.CurrentClip;
            Check(tapped&&tappedClip=="MenuPride"&&!motion.PlayMenuGesture(),"Hero taps advance to the next gesture and debounce repeated taps");
            yield return new WaitForSecondsRealtime(.7f);
            Check(motion.PlayMenuGesture()&&motion.CurrentClip=="MenuReady","A later hero tap advances to the ready pose");
            yield return new WaitForSecondsRealtime(.7f);
            Check(motion.CelebrateReward(true)&&motion.CurrentClip=="MenuPride","A successful workshop reward requests the proud jersey gesture");
            Time.timeScale=1;game.Restart();game.enabled=false;game.Obstacles.SetActive(false);game.Tokens.Enabled=false;game.Powerups.Enabled=false;game.Motor.Speed=18;
            motion.TurnAllowed=false;
            until=Time.realtimeSinceStartup+.25f;while(Time.realtimeSinceStartup<until){motion.Tick(Time.deltaTime);yield return null;}
            reference=Instantiate(motion.Animator.gameObject,motion.Animator.transform.parent);reference.name="Invisible Run phase reference";
            reference.GetComponent<Animator>().enabled=false;foreach(var renderer in reference.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            referenceBones=reference.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            var run=AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations+"Run.anim");
            Vector3 rootPosition=game.Motor.transform.position;var capsule=game.Motor.GetComponent<CharacterController>();float height=capsule.height,radius=capsule.radius;Vector3 centre=capsule.center;
            int rewardBefore=motion.RewardGestureCount;
            Check(motion.CelebrateReward()&&!motion.CelebrateReward()&&motion.RewardGestureCount==rewardBefore+1,"A running reward starts one bounded reaction without stacking repeated inputs");
            until=Time.realtimeSinceStartup+1.2f;
            string[] lower={"root","pelvis","thigh.L","shin.L","foot.L","thigh.R","shin.R","foot.R"};
            while(Time.realtimeSinceStartup<until)
            {
                motion.Tick(Time.deltaTime);yield return new WaitForEndOfFrame();
                var state=motion.Animator.GetCurrentAnimatorStateInfo(0);run.SampleAnimation(reference,Mathf.Repeat(state.normalizedTime,1)*run.length);
                report.peakReactionWeight=Mathf.Max(report.peakReactionWeight,motion.RewardGestureWeight);
                foreach(string name in lower)
                {
                    Transform actual=Bone(name),baseline=referenceBones.Single(b=>b.name==name);
                    report.maximumLowerBodyPositionError=Mathf.Max(report.maximumLowerBodyPositionError,Vector3.Distance(actual.localPosition,baseline.localPosition));
                    report.maximumLowerBodyAngleError=Mathf.Max(report.maximumLowerBodyAngleError,Quaternion.Angle(actual.localRotation,baseline.localRotation));
                }
                report.maximumRewardHandTravel=Mathf.Max(report.maximumRewardHandTravel,Vector3.Distance(Bone("hand.R").position,referenceBones.Single(b=>b.name=="hand.R").position));
            }
            Check(report.peakReactionWeight>.9f&&report.maximumRewardHandTravel>.12f&&motion.RewardGestureWeight==0,"The reward raises one wing visibly and completely recovers into the running cycle");
            Check(report.maximumLowerBodyPositionError<.002f&&report.maximumLowerBodyAngleError<.15f,"Reward reactions leave every lower-body bone on the unchanged Run phase");
            Check(game.Motor.transform.position==rootPosition&&capsule.height==height&&capsule.radius==radius&&capsule.center==centre,"Reward animation does not move the physics root or change its capsule");
            yield return new WaitForSecondsRealtime(.4f);motion.Tick(.4f);motion.CelebrateReward(true);
            until=Time.realtimeSinceStartup+.25f;while(Time.realtimeSinceStartup<until){motion.Tick(Time.deltaTime);yield return null;}
            game.Pause();yield return new WaitForEndOfFrame();Quaternion held=Bone("hand.R").rotation;int heldCount=motion.RewardGestureCount;
            Check(!motion.CelebrateReward(),"Paused gameplay rejects new character reactions");
            yield return new WaitForSecondsRealtime(.2f);
            Check(Quaternion.Angle(held,Bone("hand.R").rotation)<.01f&&motion.RewardGestureCount==heldCount,"Pause freezes an active reaction without pose drift or duplicate events");
            game.Resume();game.Restart();game.enabled=false;game.Obstacles.SetActive(false);game.Tokens.Enabled=false;game.Powerups.Enabled=false;game.Motor.Speed=0;
            game.Motor.TryJump();motion.CelebrateReward();bool airClear=true;
            for(int i=0;i<65&&!game.Motor.Grounded;i++)
            {game.Motor.Tick(1f/60);motion.Tick(1f/60);yield return new WaitForEndOfFrame();if(!game.Motor.Grounded)airClear&=motion.RewardGestureWeight==0;}
            Check(airClear&&game.Motor.Grounded,"Queued rewards never alter airborne poses and wait for a real landing");
            game.Restart();game.enabled=false;game.Obstacles.SetActive(false);game.Tokens.Enabled=false;game.Powerups.Enabled=false;
            game.Motor.TrySlide();motion.CelebrateReward();motion.Tick(1f/60);yield return new WaitForEndOfFrame();
            Check(game.Motor.Sliding&&motion.RewardGestureWeight==0,"The sliding pose keeps its full clearance without a reward overlay");
            game.ReturnToMenu();report.menuGestureStarts=motion.MenuGestureCount;report.rewardStarts=motion.RewardGestureCount;
            report.preferencesPreserved=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0)==oldCoins&&PlayerPrefs.GetInt("FenerRunner.BestScore",0)==oldScore&&PlayerPrefs.GetFloat("FenerRunner.Best",0)==oldBest&&PlayerPrefs.GetInt("FenerRunner.Muted",0)==oldMute;
            Check(report.preferencesPreserved,"Real wallet, records and mute preferences remain unchanged");Finish();
        }
        Transform Bone(string name)=>bones.Single(b=>b.name==name);
        void MeasureClips()
        {
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations+"Idle.anim");
            foreach(string name in new[]{"MenuWave","MenuPride","MenuReady"})
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations+name+".anim");
                if(clip==null){Check(false,name+" exists");continue;}
                idle.SampleAnimation(motion.Animator.gameObject,0);
                var neutral=bones.Select(b=>b.localRotation).ToArray();Vector3 leftFoot=Bone("foot.L").position,rightFoot=Bone("foot.R").position;
                Vector3 handL=Bone("hand.L").position,handR=Bone("hand.R").position;
                var gesture=new Gesture{name=name,seconds=clip.length};
                for(int i=0;i<=60;i++)
                {
                    clip.SampleAnimation(motion.Animator.gameObject,clip.length*i/60);gesture.maximumHandTravel=Mathf.Max(gesture.maximumHandTravel,Vector3.Distance(handL,Bone("hand.L").position),Vector3.Distance(handR,Bone("hand.R").position));
                    gesture.maximumFootDrift=Mathf.Max(gesture.maximumFootDrift,Vector3.Distance(leftFoot,Bone("foot.L").position),Vector3.Distance(rightFoot,Bone("foot.R").position));
                    gesture.waveHandHeight=Mathf.Max(gesture.waveHandHeight,Bone("hand.R").position.y-Bone("upper_arm.R").position.y);
                }
                for(int i=0;i<bones.Length;i++)gesture.endPoseAngleError=Mathf.Max(gesture.endPoseAngleError,Quaternion.Angle(neutral[i],bones[i].localRotation));
                gestures.Add(gesture);
                Check(gesture.seconds>=1.5f&&gesture.maximumHandTravel>.22f,name+" has a distinct authored hand arc");
                Check(gesture.maximumFootDrift<.0005f&&gesture.endPoseAngleError<.1f,name+" preserves planted feet and returns exactly to the Idle pose");
                if(name=="MenuWave")Check(gesture.waveHandHeight>.28f,"The greeting raises its waving hand clearly above the shoulder");
            }
            report.gestures=gestures.ToArray();
        }
        IEnumerator CaptureGestures()
        {
            const int width=384,height=640;var target=new RenderTexture(width,height,24);target.Create();
            var frame=new Texture2D(width,height,TextureFormat.RGB24,false);var sheet=new Texture2D(width*4,height*3,TextureFormat.RGB24,false);
            viewCamera=Camera.main;oldTarget=viewCamera.targetTexture;viewCamera.targetTexture=target;
            float[] phases={.2f,.4f,.6f,.85f};string[] names={"MenuWave","MenuPride","MenuReady"};
            for(int row=0;row<3;row++)for(int column=0;column<4;column++)
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations+names[row]+".anim");clip.SampleAnimation(motion.Animator.gameObject,clip.length*phases[column]);
                yield return new WaitForEndOfFrame();var old=RenderTexture.active;RenderTexture.active=target;frame.ReadPixels(new Rect(0,0,width,height),0,0);frame.Apply();RenderTexture.active=old;
                sheet.SetPixels(column*width,(2-row)*height,width,height,frame.GetPixels());
            }
            sheet.Apply();Directory.CreateDirectory("../Validation");File.WriteAllBytes("../Validation/v9-character-gestures.png",sheet.EncodeToPNG());
            viewCamera.targetTexture=oldTarget;target.Release();Destroy(target);Destroy(frame);Destroy(sheet);
            Check(File.Exists("../Validation/v9-character-gestures.png"),"The three authored gestures are captured from the actual game scene");
        }
        void Check(bool ok,string label){if(ok)checks.Add(label);else errors.Add(label);}
        void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text);}
        void Finish()
        {
            report.passed=errors.Count==0;report.utc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;report.checks=checks.ToArray();report.errors=errors.ToArray();
            Directory.CreateDirectory("../Validation");File.WriteAllText("../Validation/unity_character_motion_v9.json",JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Character motion v9 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void OnDestroy()
        {
            Application.logMessageReceived-=Log;Application.runInBackground=background;Time.timeScale=previousScale;
            if(viewCamera!=null)viewCamera.targetTexture=oldTarget;if(reference!=null)Destroy(reference);
            if(ready&&motion!=null)motion.Animator.enabled=true;if(wallet!=null)wallet.DeleteIsolatedTestData();
        }
    }
}
#endif
