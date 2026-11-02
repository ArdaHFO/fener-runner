using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace FenerRunner.Editor
{
    public static class MultiplayerSetupV17
    {
        [Serializable] sealed class Report
        {public bool passed;public string utc,projectId;public string[] checks,errors;public bool apkBuilt=false;}
        public static void Verify()
        {
            var checks=new List<string>();var errors=new List<string>();
            void Check(bool value,string label){checks.Add((value?"PASS ":"FAIL ")+label);if(!value)errors.Add(label);}
            PlayerSettings.Android.forceInternetPermission=true;
            AssetDatabase.SaveAssets();
            Check(!PlayerSettings.SplashScreen.show,"Unity splash screen is disabled");
            Check(!PlayerSettings.SplashScreen.showUnityLogo,"Unity splash logo is disabled");
            Check(Application.cloudProjectId=="08375041-53fd-427a-9533-bad3548255cc","Native runtime sees the authorized Fener Runner cloud project");
            Check(PlayerSettings.Android.forceInternetPermission,"Android player declares Internet permission");
            Check(PlayerSettings.bundleVersion=="0.17.0"&&PlayerSettings.Android.bundleVersionCode==17,"Development project uses v0.17 without overwriting the existing v0.12 APK");
            var defaultIcons=PlayerSettings.GetIcons(NamedBuildTarget.Unknown,UnityEditor.IconKind.Any);
            var adaptiveIcons=PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,AndroidPlatformIconKind.Adaptive);
            Check(defaultIcons.Length>0&&Array.TrueForAll(defaultIcons,t=>t!=null),"Default application icon uses the chick mascot");
            Check(defaultIcons.Length>0&&Array.TrueForAll(defaultIcons,t=>AssetDatabase.GetAssetPath(t)==ChickAppIconV15.IconPath),"Default icon references the new chick artwork, not an older generated icon");
            Check(adaptiveIcons.Length>0&&Array.TrueForAll(adaptiveIcons,i=>i.GetTextures().Length==2&&Array.TrueForAll(i.GetTextures(),t=>t!=null)),"Every Android adaptive icon has both branded layers");
            var packages=UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            Check(Array.Exists(packages,p=>p.name=="com.unity.services.multiplayer"&&p.version=="1.2.1"),"Pinned Multiplayer Services SDK resolved");
            Check(Array.Exists(packages,p=>p.name=="com.unity.transport"&&p.version=="2.6.0"),"Pinned Unity Transport resolved");
            Check(Resources.Load<Shader>("GhostRunner")!=null,"Ghost shader is retained in player Resources");
            var chase=Resources.Load<GameObject>(ChaseSUV.ResourceName);
            Check(chase!=null&&chase.GetComponentsInChildren<Collider>(true).Length==0,"Pooled chase vehicle is included in Resources without gameplay colliders");
            var result=new Report{passed=errors.Count==0,utc=DateTime.UtcNow.ToString("O"),projectId=Application.cloudProjectId,checks=checks.ToArray(),errors=errors.ToArray()};
            Directory.CreateDirectory("../Validation");File.WriteAllText("../Validation/unity_multiplayer_setup_v17.json",JsonUtility.ToJson(result,true));
            if(!result.passed)throw new InvalidOperationException(string.Join("; ",errors));
            Debug.Log("V17 multiplayer configuration PASSED");
        }
        public static void VerifyAndExit(){Verify();EditorApplication.Exit(0);}
    }
}
