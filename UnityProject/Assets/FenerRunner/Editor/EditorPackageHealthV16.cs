using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace FenerRunner.Editor
{
    // Read-only installed-package diagnostics. Never changes package versions,
    // registry trust, verification, project manifests or Editor preferences.
    public static class EditorPackageHealthV16
    {
        [Serializable] sealed class Entry{public string name,version,source,signature;public string[] errors;}
        [Serializable] sealed class Report{public string utc,unityVersion;public bool errorsEmpty;public Entry[] packages;}
        public static void Record()
        {
            var packages=UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            var result=new Report{utc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion,errorsEmpty=true,packages=new Entry[packages.Length]};
            for(int i=0;i<packages.Length;i++)
            {
                var p=packages[i];
                string[] errors=p.errors==null?Array.Empty<string>():p.errors.Select(e=>e.errorCode+": "+e.message).ToArray();
                result.errorsEmpty&=errors.Length==0;
                string signature="not exposed";
                var prop=p.GetType().GetProperty("signature",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                var value=prop==null?null:prop.GetValue(p);
                if(value!=null)
                {
                    var fields=value.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance);
                    var properties=value.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(x=>x.GetIndexParameters().Length==0&&(x.Name=="status"||x.Name=="reason"||x.Name=="errorCode"));
                    signature=string.Join("; ",fields.Select(f=>f.Name+"="+f.GetValue(value)).Concat(properties.Select(x=>x.Name+"="+x.GetValue(value))));
                    if(signature.Length==0)signature=value.ToString();
                }
                result.packages[i]=new Entry{name=p.name,version=p.version,source=p.source.ToString(),errors=errors,signature=signature};
            }
            Directory.CreateDirectory("../Validation");File.WriteAllText("../Validation/unity_package_health_v16.json",JsonUtility.ToJson(result,true));
            Debug.Log("V16 package health recorded: "+packages.Length+" registered packages, errorsEmpty="+result.errorsEmpty);
        }
    }
}
