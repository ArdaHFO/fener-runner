using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FenerRunner.Editor
{
    public sealed class KartalImport : AssetPostprocessor
    {
        public const string ModelPath = "Assets/FenerRunner/Art/Kartal/Kartal_Runner_v01.fbx";
        public static readonly string[] RequiredClips =
            { "Idle", "Run", "Jump", "Slide", "RunTurn", "JumpFlip", "CelebratePoint" };
        public static string ShortName(string value) => value.Split('|').Last().Trim();

        private void OnPreprocessModel()
        {
            if (assetPath != ModelPath) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.motionNodeName = "root";
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.bakeAxisConversion = true;
            importer.globalScale = 1f;
            importer.isReadable = true; // Setup measures the real beak submesh to establish forward.
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
        private void OnPreprocessAnimation()
        {
            if (assetPath != ModelPath) return;
            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] animations = importer.defaultClipAnimations;
            foreach (var clip in animations)
            {
                clip.name = ShortName(clip.name);
                clip.loopTime = clip.name == "Idle" || clip.name == "Run";
                clip.loopPose = clip.loopTime;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = animations;
        }
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/FenerRunner/Art/Kartal/textures/")) return;
            var importer = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_normal.png");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }
    }
}
