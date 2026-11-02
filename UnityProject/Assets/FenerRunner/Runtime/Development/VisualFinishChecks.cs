#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace FenerRunner
{
    public sealed class VisualFinishChecks:MonoBehaviour
    {
        [Serializable] class Report{public bool passed;public string utc;public string[] checks,errors;public string scope="Actual Game-view menu/workshop after entrance transitions at two portrait sizes. No wallet transactions or phone device measurement.";}
        readonly List<string> checks=new List<string>(),errors=new List<string>();RunnerGame game;bool background;
        [MenuItem("Fener Runner/19 - Capture final workshop views")]
        public static void StartChecks(){UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/FenerRunner/Generated/Scenes/FenerRunner.unity");EditorApplication.ExecuteMenuItem("Fener Runner/5 - Open portrait Game view");SessionState.SetBool("FenerRunner.FinalVisualV5",true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("FenerRunner.FinalVisualV5",false))return;SessionState.SetBool("FenerRunner.FinalVisualV5",false);new GameObject("Final visual checks").AddComponent<VisualFinishChecks>();}
        IEnumerator Start()
        {
            background=Application.runInBackground;Application.runInBackground=true;Application.logMessageReceived+=Log;
            game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;int balance=game.WalletBalance;
            yield return new WaitForSecondsRealtime(.8f);yield return Capture("v7-menu");
            game.Interface.OpenShop();yield return new WaitForSecondsRealtime(.45f);
            CheckShop("portrait");yield return Capture("v7-shop");
            EditorApplication.ExecuteMenuItem("Fener Runner/10 - Open tall phone Game view");yield return new WaitForSecondsRealtime(.4f);
            Check(Screen.width==1080&&Screen.height==2340,"Tall-phone render target is 1080x2340");CheckShop("tall phone");yield return Capture("v7-shop-tall");
            EditorApplication.ExecuteMenuItem("Fener Runner/5 - Open portrait Game view");yield return new WaitForSecondsRealtime(.3f);game.Interface.CloseShop();
            Check(game.State==RunState.Menu&&game.WalletBalance==balance,"Workshop inspection changes neither the wallet nor run state");
            var report=new Report{passed=errors.Count==0,utc=DateTime.UtcNow.ToString("O"),checks=checks.ToArray(),errors=errors.ToArray()};
            File.WriteAllText("../Validation/unity_final_visual_v7.json",JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Final workshop visual checks "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        void CheckShop(string size)
        {
            var shop=game.Interface.shopPanel;
            Check(shop.activeSelf&&shop.GetComponentsInChildren<CanvasGroup>().All(g=>g.alpha>.99f),"Workshop entrance is fully visible on "+size);
            Check(shop.GetComponentsInChildren<Button>().All(b=>InScreen((RectTransform)b.transform)),"Every workshop button fits on "+size);
            Check(shop.GetComponentsInChildren<Text>().All(t=>t.preferredHeight<=t.rectTransform.rect.height+1),"Workshop text is not vertically clipped on "+size);
        }
        static bool InScreen(RectTransform rect){var c=new Vector3[4];rect.GetWorldCorners(c);return c.All(p=>p.x>=-1&&p.y>=-1&&p.x<=Screen.width+1&&p.y<=Screen.height+1);}
        void Check(bool ok,string label){if(ok)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.GetFullPath("../Validation/"+name+".png"));yield return null;}
        void OnDestroy(){Application.logMessageReceived-=Log;Application.runInBackground=background;}
    }
}
#endif
