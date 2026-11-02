using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FenerRunner
{
    // A visual replica only. The networking/session owner supplies already
    // interpolated snapshots and hides this view when the peer is unavailable.
    public sealed class GhostRunnerView:MonoBehaviour
    {
        public const float MinimumDrawDistance=-15f,MaximumDrawDistance=110f;
        static readonly int MotionRate=Animator.StringToHash("MotionRate");
        static readonly int Opacity=Shader.PropertyToID("_Opacity");
        static readonly int RunState=Animator.StringToHash("Base Layer.Run"),JumpState=Animator.StringToHash("Base Layer.Jump"),
            SlideState=Animator.StringToHash("Base Layer.Slide"),FlightState=Animator.StringToHash("Base Layer.Flight"),DeadState=Animator.StringToHash("Base Layer.CollisionFall");
        RunnerGame game;
        Transform sourceVisual,sourceAnimatorTransform,replica;
        Animator sourceAnimator,ghostAnimator;
        Vector3 modelRestPosition;
        Renderer[] renderers;
        readonly List<Material> ownedMaterials=new List<Material>();
        MaterialPropertyBlock opacityBlock;
        uint runId;
        bool hasRun,hasPose;
        GhostPose previousPose;
        string currentClip;
        float currentPhase,alpha;
        public bool IsVisible=>replica!=null&&replica.gameObject.activeSelf&&alpha>.001f;
        public bool IsAllocated=>replica!=null;
        public Transform VisualRoot=>replica;
        public Animator VisualAnimator=>ghostAnimator;
        public int AllocationCount {get;private set;}
        public int MaterialCount=>ownedMaterials.Count;
        public string CurrentClip=>currentClip;

        public void Initialize(RunnerGame value)
        {
            if(value==null)throw new ArgumentNullException(nameof(value));
            if(game==value)return;
            DisposeVisual();game=value;
            sourceAnimator=game.Animation.Animator;sourceAnimatorTransform=sourceAnimator.transform;
            sourceVisual=game.Motor.transform.Find("Motion accents/Visual Only");
            if(sourceVisual==null||!sourceAnimatorTransform.IsChildOf(sourceVisual))
                throw new InvalidOperationException("The generated character's Visual Only hierarchy is required for ghost presentation.");
            // The production Animator's only dynamic object-level position is
            // its death descent. Keep the neutral model offset, not that descent.
            modelRestPosition=game.Animation.DeathPlaying||game.Animation.DeathComplete?Vector3.zero:sourceAnimatorTransform.localPosition;
            hasRun=hasPose=false;currentClip=null;alpha=0;
            // Deliberately no GameObjects/materials while offline or in a lobby.
        }

        public void Show(GhostSnapshot sample,float dt)
        {
            if(game==null)throw new InvalidOperationException("Initialize the ghost view before showing snapshots.");
            double delta=sample.Distance-game.Distance;
            if(!Finite(delta)||!Finite(sample.LaneX)||!Finite(sample.Height)||!Finite(sample.Phase)||!Finite(sample.Speed)||
                delta<MinimumDrawDistance||delta>MaximumDrawDistance||sample.Height<-.15f||sample.Height>10f||
                Mathf.Abs(sample.LaneX)>game.Settings.laneSpacing+.5f||(int)sample.Pose<0||(int)sample.Pose>(int)GhostPose.Paused)
            {Hide();return;}
            if(replica==null)CreateVisual();
            if(!replica.gameObject.activeSelf)replica.gameObject.SetActive(true);
            if(!hasRun||runId!=sample.RunId)
            {
                runId=sample.RunId;hasRun=true;hasPose=false;alpha=0;
                ghostAnimator.Rebind();ghostAnimator.SetFloat(MotionRate,0);ghostAnimator.Update(0);
            }
            float height=Mathf.Max(0,sample.Height);
            GhostPose effectivePose=sample.Pose==GhostPose.Paused&&hasPose?previousPose:sample.Pose;
            float effectivePhase=sample.Pose==GhostPose.Paused&&hasPose?currentPhase:sample.Phase;
            if(effectivePose==GhostPose.Dead)height*=1-Smooth(Mathf.Clamp01(effectivePhase));
            replica.SetPositionAndRotation(new Vector3(sample.LaneX,height,game.Motor.transform.position.z+(float)delta),Quaternion.identity);
            SamplePose(sample);
            alpha=Mathf.MoveTowards(alpha,.48f,Mathf.Clamp(Finite(dt)?dt:0,0,.1f)*3.2f);
            // One cached block, reused for every renderer. No per-frame mesh,
            // material, renderer array or hierarchy allocation.
            opacityBlock.SetFloat(Opacity,alpha);
            foreach(var renderer in renderers)renderer.SetPropertyBlock(opacityBlock);
        }

        void SamplePose(GhostSnapshot sample)
        {
            if(sample.Pose==GhostPose.Paused&&hasPose)return;
            GhostPose pose=sample.Pose==GhostPose.Paused?GhostPose.Run:sample.Pose;
            string clip=pose==GhostPose.Jump?"Jump":pose==GhostPose.Slide?"Slide":pose==GhostPose.Flight?"Flight":pose==GhostPose.Dead?"CollisionFall":"Run";
            float phase=pose==GhostPose.Run||pose==GhostPose.Flight?Mathf.Repeat(sample.Phase,1):Mathf.Clamp01(sample.Phase);
            if(!hasPose||pose!=previousPose||Mathf.Abs(currentPhase-phase)>.00001f)
            {
                int state=pose==GhostPose.Jump?JumpState:pose==GhostPose.Slide?SlideState:pose==GhostPose.Flight?FlightState:pose==GhostPose.Dead?DeadState:RunState;
                ghostAnimator.Play(state,0,phase);
                ghostAnimator.Update(0);
            }
            previousPose=pose;currentClip=clip;currentPhase=phase;hasPose=true;
        }

        void CreateVisual()
        {
            Shader shader=Resources.Load<Shader>("GhostRunner");
            if(shader==null)throw new InvalidOperationException("The retained Resources/GhostRunner shader is missing.");
            if(opacityBlock==null)opacityBlock=new MaterialPropertyBlock();
            var root=new GameObject("Remote runner visual");root.SetActive(false);root.transform.SetParent(transform,false);replica=root.transform;
            var transforms=new Dictionary<Transform,Transform>();
            CopyTransforms(sourceVisual,replica,transforms);
            var materials=new Dictionary<Material,Material>();
            var result=new List<Renderer>();
            foreach(var entry in transforms)
            {
                var source=entry.Key;var destination=entry.Value;
                var skin=source.GetComponent<SkinnedMeshRenderer>();
                if(skin!=null&&skin.sharedMesh!=null)
                {
                    var copy=destination.gameObject.AddComponent<SkinnedMeshRenderer>();copy.sharedMesh=skin.sharedMesh;
                    var originalBones=skin.bones;var clonedBones=new Transform[originalBones.Length];
                    for(int i=0;i<originalBones.Length;i++)
                        if(originalBones[i]!=null&&!transforms.TryGetValue(originalBones[i],out clonedBones[i]))
                            throw new InvalidOperationException("A ghost skin bone lies outside the safe visual hierarchy.");
                    copy.bones=clonedBones;
                    if(skin.rootBone!=null){if(!transforms.TryGetValue(skin.rootBone,out var rootBone))throw new InvalidOperationException("Ghost root bone is outside the visual hierarchy.");copy.rootBone=rootBone;}
                    copy.localBounds=skin.localBounds;copy.quality=skin.quality;copy.updateWhenOffscreen=true;
                    for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)copy.SetBlendShapeWeight(i,skin.GetBlendShapeWeight(i));
                    CopyRenderer(skin,copy,shader,materials);result.Add(copy);
                }
                var mesh=source.GetComponent<MeshFilter>();var meshRenderer=source.GetComponent<MeshRenderer>();
                if(mesh!=null&&mesh.sharedMesh!=null&&meshRenderer!=null)
                {
                    destination.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
                    var copy=destination.gameObject.AddComponent<MeshRenderer>();CopyRenderer(meshRenderer,copy,shader,materials);result.Add(copy);
                }
            }
            Transform model=transforms[sourceAnimatorTransform];model.localPosition=modelRestPosition;
            ghostAnimator=model.gameObject.AddComponent<Animator>();ghostAnimator.avatar=sourceAnimator.avatar;
            ghostAnimator.runtimeAnimatorController=sourceAnimator.runtimeAnimatorController;
            ghostAnimator.applyRootMotion=false;ghostAnimator.fireEvents=false;ghostAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            ghostAnimator.updateMode=AnimatorUpdateMode.UnscaledTime;ghostAnimator.speed=0;
            renderers=result.ToArray();AllocationCount++;
            // Only component types explicitly created above can exist inside the
            // replica. Instantiate(source) is intentionally never used.
        }

        static void CopyTransforms(Transform source,Transform parent,Dictionary<Transform,Transform> map)
        {
            var copy=new GameObject(source.name).transform;copy.SetParent(parent,false);
            copy.localPosition=source.localPosition;copy.localRotation=source.localRotation;copy.localScale=source.localScale;
            map.Add(source,copy);
            for(int i=0;i<source.childCount;i++)CopyTransforms(source.GetChild(i),copy,map);
        }
        void CopyRenderer(Renderer source,Renderer copy,Shader shader,Dictionary<Material,Material> cache)
        {
            var original=source.sharedMaterials;var mapped=new Material[original.Length];
            for(int i=0;i<original.Length;i++)
            {
                var sourceMaterial=original[i];
                if(sourceMaterial==null)throw new InvalidOperationException("Character material is missing.");
                if(!cache.TryGetValue(sourceMaterial,out var ghost))
                {
                    ghost=new Material(shader){name="Ghost "+sourceMaterial.name};
                    Color color=sourceMaterial.HasProperty("_BaseColor")?sourceMaterial.GetColor("_BaseColor"):sourceMaterial.HasProperty("_Color")?sourceMaterial.GetColor("_Color"):Color.white;
                    ghost.SetColor("_BaseColor",color);
                    string textureProperty=sourceMaterial.HasProperty("_BaseMap")?"_BaseMap":sourceMaterial.HasProperty("_MainTex")?"_MainTex":null;
                    if(textureProperty!=null)
                    {ghost.SetTexture("_BaseMap",sourceMaterial.GetTexture(textureProperty));ghost.SetTextureScale("_BaseMap",sourceMaterial.GetTextureScale(textureProperty));ghost.SetTextureOffset("_BaseMap",sourceMaterial.GetTextureOffset(textureProperty));}
                    cache.Add(sourceMaterial,ghost);ownedMaterials.Add(ghost);
                }
                mapped[i]=ghost;
            }
            copy.sharedMaterials=mapped;copy.shadowCastingMode=ShadowCastingMode.Off;copy.receiveShadows=false;
            copy.lightProbeUsage=LightProbeUsage.Off;copy.reflectionProbeUsage=ReflectionProbeUsage.Off;copy.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
        }
        public void Hide(){if(replica!=null)replica.gameObject.SetActive(false);alpha=0;}
        void DisposeVisual()
        {
            if(replica!=null){replica.gameObject.SetActive(false);Destroy(replica.gameObject);}
            foreach(var material in ownedMaterials)if(material!=null)Destroy(material);
            ownedMaterials.Clear();replica=null;ghostAnimator=null;renderers=null;hasRun=hasPose=false;
        }
        void OnDestroy(){DisposeVisual();}
        static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
        static float Smooth(float t)=>t*t*(3-2*t);
    }
}
