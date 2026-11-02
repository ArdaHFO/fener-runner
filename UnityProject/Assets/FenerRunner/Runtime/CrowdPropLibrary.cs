using UnityEngine;

namespace FenerRunner
{
    public enum CrowdPropKind { Bottle, PaperCup, CrumpledPaper, FoamBall }

    // Four immutable shared visual meshes; pooled throw objects only select a
    // reference. No physics, gameplay effect or per-throw asset allocation.
    public sealed class CrowdPropLibrary : ScriptableObject
    {
        public const string ResourceName="CrowdPropLibrary";
        public Mesh[] meshes;
        public Material[] materials;
        public bool Complete=>meshes!=null&&materials!=null&&meshes.Length==4&&materials.Length==4&&
            System.Array.TrueForAll(meshes,m=>m!=null)&&System.Array.TrueForAll(materials,m=>m!=null);
        public Mesh MeshFor(CrowdPropKind kind)=>meshes[(int)kind];
        public Material MaterialFor(CrowdPropKind kind)=>materials[(int)kind];
    }
}
