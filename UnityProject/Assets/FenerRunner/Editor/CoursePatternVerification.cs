using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FenerRunner.Editor
{
    public static class CoursePatternVerification
    {
        [MenuItem("Fener Runner/11 - Audit procedural route seeds")]
        public static void Run()
        {
            var result = CoursePatternAudit.Run(4096, 512);
            string path = ReviewEvidence.FileFor(12,"../Validation/unity_pattern_audit_v12.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
            if (!result.passed) throw new InvalidOperationException("Pattern audit failed: " + string.Join("; ", result.errors));
            Debug.Log("Pattern audit passed: " + result.checkedRows + " rows across " + result.seeds + " seeds. " + path);
        }
    }
}
