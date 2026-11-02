using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FenerRunner.Editor
{
    // Geometry evidence complements the actual Game-view motion capture. It does
    // not measure device FPS or claim a human-motion/motion-capture match.
    public static class SprintValidation
    {
        [Serializable] sealed class Report
        {
            public string utc,scope="Editor sampling of the actual generated rig and skinned foot vertices. Geometry/loop checks, not device performance or a subjective animation-quality score.";
            public bool passed,originalMascotMesh;
            public int sampledPoses,leftFootVertices,rightFootVertices;
            public float lowestSole,highestLowestSole,flightFraction,leftRecoveryHeight,rightRecoveryHeight;
            public float maximumFootSeparation,minimumElbowDegrees,maximumTorsoLeanDegrees;
            public float loopRotationError,loopPositionError,idleWingSpan,flightWingSpan;
            public string[] checks,errors;
        }

        [MenuItem("Fener Runner/18 - Verify mascot and sprint geometry")]
        public static void Verify()
        {
            var report=new Report{utc=DateTime.UtcNow.ToString("O"),sampledPoses=96};
            var checks=new List<string>();var errors=new List<string>();
            void Check(bool passed,string message){if(passed)checks.Add(message);else errors.Add(message);}
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(RunnerSceneBuilder.Generated+"/Prefabs/KartalPlayer.prefab");
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(KartalImport.ModelPath);
            var run=AssetDatabase.LoadAssetAtPath<AnimationClip>(RunnerSceneBuilder.Generated+"/Animations/Run.anim");
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(RunnerSceneBuilder.Generated+"/Animations/Idle.anim");
            var flight=AssetDatabase.LoadAssetAtPath<AnimationClip>(RunnerSceneBuilder.Generated+"/Animations/Flight.anim");
            if(prefab==null||original==null||run==null||idle==null||flight==null)
                throw new InvalidOperationException("Build the mascot, Run and Flight assets before geometry validation.");
            var instance=Object.Instantiate(prefab);instance.hideFlags=HideFlags.HideAndDontSave;
            var baked=new Mesh();
            try
            {
                instance.transform.SetPositionAndRotation(new Vector3(0,.02f,0),Quaternion.identity);
                var animator=instance.GetComponentInChildren<Animator>();
                var skin=instance.GetComponentInChildren<SkinnedMeshRenderer>();
                report.originalMascotMesh=skin.sharedMesh==original.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
                Check(report.originalMascotMesh,"Original yellow-feathered mascot mesh and tuft are preserved");
                Check(!instance.GetComponentsInChildren<Transform>().Any(t=>t.name.Contains("Coach swept")||t.name.Contains("Coach silver")),"No generated human hair or moustache overlays remain");
                int leftBone=Array.FindIndex(skin.bones,b=>b.name=="foot.L"),rightBone=Array.FindIndex(skin.bones,b=>b.name=="foot.R");
                var weights=skin.sharedMesh.boneWeights;
                var left=new List<int>();var right=new List<int>();
                for(int i=0;i<weights.Length;i++)
                {
                    var w=weights[i];int bone=w.boneIndex0;float weight=w.weight0;
                    if(w.weight1>weight){bone=w.boneIndex1;weight=w.weight1;}
                    if(w.weight2>weight){bone=w.boneIndex2;weight=w.weight2;}
                    if(w.weight3>weight)bone=w.boneIndex3;
                    if(bone==leftBone)left.Add(i);if(bone==rightBone)right.Add(i);
                }
                report.leftFootVertices=left.Count;report.rightFootVertices=right.Count;
                Check(left.Count>20&&right.Count>20,"Both feet are measured from weighted skinned vertices");
                Transform Bone(string name)=>skin.bones.Single(b=>b.name==name);
                var leftSoles=new float[report.sampledPoses];var rightSoles=new float[report.sampledPoses];
                float minElbow=180,maxLean=0,maxSeparation=0;
                for(int i=0;i<report.sampledPoses;i++)
                {
                    run.SampleAnimation(animator.gameObject,run.length*i/report.sampledPoses);
                    skin.BakeMesh(baked);var vertices=baked.vertices;
                    leftSoles[i]=MinimumY(left,vertices,skin.transform);
                    rightSoles[i]=MinimumY(right,vertices,skin.transform);
                    Vector3 separation=Bone("foot.L").position-Bone("foot.R").position;
                    maxSeparation=Mathf.Max(maxSeparation,Mathf.Abs(Vector3.Dot(separation,instance.transform.forward)));
                    foreach(string side in new[]{"L","R"})
                    {
                        Vector3 shoulder=Bone("upper_arm."+side).position,elbow=Bone("forearm."+side).position,hand=Bone("hand."+side).position;
                        minElbow=Mathf.Min(minElbow,Vector3.Angle(shoulder-elbow,hand-elbow));
                    }
                    Vector3 torso=Bone("neck").position-Bone("spine").position;
                    maxLean=Mathf.Max(maxLean,Mathf.Atan2(Vector3.Dot(torso,instance.transform.forward),torso.y)*Mathf.Rad2Deg);
                }
                float leftFloor=leftSoles.Min(),rightFloor=rightSoles.Min();
                report.lowestSole=Mathf.Min(leftFloor,rightFloor);
                report.highestLowestSole=Enumerable.Range(0,report.sampledPoses).Max(i=>Mathf.Min(leftSoles[i],rightSoles[i]));
                report.flightFraction=Enumerable.Range(0,report.sampledPoses).Count(i=>leftSoles[i]>leftFloor+.022f&&rightSoles[i]>rightFloor+.022f)/(float)report.sampledPoses;
                report.leftRecoveryHeight=leftSoles.Max()-leftFloor;report.rightRecoveryHeight=rightSoles.Max()-rightFloor;
                report.maximumFootSeparation=maxSeparation;report.minimumElbowDegrees=minElbow;report.maximumTorsoLeanDegrees=maxLean;
                Check(report.lowestSole>-.055f&&report.highestLowestSole<.22f,"Sprint stays grounded at support with a bounded flight phase");
                Check(report.flightFraction>=.10f&&report.flightFraction<=.48f,"Both feet visibly leave support during part of each stride");
                Check(report.leftRecoveryHeight>.15f&&report.rightRecoveryHeight>.15f,"Both heels recover clearly above their support height");
                Check(maxSeparation>.28f,"The stride has readable forward/backward leg separation");
                Check(minElbow<150f&&minElbow>35f,"Arm swing uses bent elbows without a folded-through joint");
                Check(maxLean>9f&&maxLean<28f,"The upper torso leans forward into the sprint");

                run.SampleAnimation(animator.gameObject,0);
                var poses=skin.bones.Select(t=>(position:t.localPosition,rotation:t.localRotation)).ToArray();
                run.SampleAnimation(animator.gameObject,run.length);
                for(int i=0;i<poses.Length;i++)
                {
                    report.loopRotationError=Mathf.Max(report.loopRotationError,Quaternion.Angle(poses[i].rotation,skin.bones[i].localRotation));
                    report.loopPositionError=Mathf.Max(report.loopPositionError,Vector3.Distance(poses[i].position,skin.bones[i].localPosition));
                }
                Check(report.loopRotationError<.1f&&report.loopPositionError<.001f,"The entire rig closes the Run loop without a pose discontinuity");
                idle.SampleAnimation(animator.gameObject,0);
                report.idleWingSpan=Vector3.Distance(Bone("hand.L").position,Bone("hand.R").position);
                float maximumSpan=0;
                for(int i=0;i<24;i++)
                {
                    flight.SampleAnimation(animator.gameObject,flight.length*i/24f);
                    maximumSpan=Mathf.Max(maximumSpan,Vector3.Distance(Bone("hand.L").position,Bone("hand.R").position));
                }
                report.flightWingSpan=maximumSpan;
                Check(flight.length>.5f&&AnimationUtility.GetAnimationClipSettings(flight).loopTime&&maximumSpan>report.idleWingSpan*1.25f,"Flight has a looping spread-wing pose distinct from standing or jumping");
            }
            finally{Object.DestroyImmediate(baked);Object.DestroyImmediate(instance);}
            report.passed=errors.Count==0;report.checks=checks.ToArray();report.errors=errors.ToArray();
            Directory.CreateDirectory("../Validation");File.WriteAllText("../Validation/unity_sprint_v5.json",JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidOperationException("Sprint geometry validation failed: "+string.Join("; ",errors));
            Debug.Log("Mascot and sprint geometry validation passed.");
        }

        static float MinimumY(List<int> indices,Vector3[] vertices,Transform space)
        {
            float minimum=float.PositiveInfinity;
            foreach(int index in indices)minimum=Mathf.Min(minimum,space.TransformPoint(vertices[index]).y);
            return minimum;
        }
    }
}
