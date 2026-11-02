using System;
using System.IO;
using UnityEngine;
using UnityEditor;
namespace FenerRunner.Editor
{
    internal static class AudioBuilder
    {
        public static AudioClip BuildMenuMusic()
        {
            const string path="Assets/FenerRunner/Art/Audio/MenuMusic/ses8.wav";
            if(!File.Exists(path))throw new FileNotFoundException("Menu music missing",path);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);
            var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.Streaming;
            settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.7f;settings.preloadAudioData=true;
            importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.loadInBackground=false;importer.SaveAndReimport();
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if(clip==null||clip.length<=0)throw new InvalidOperationException("Menu music decode failed");
            return clip;
        }

        [MenuItem("Fener Runner/47 - Apply and verify ses8 menu music")]
        public static void InstallMenuMusic()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            var scene=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if(scene.path!=RunnerSceneBuilder.ScenePath)throw new InvalidOperationException("Open the FenerRunner scene first.");
            var sound=UnityEngine.Object.FindFirstObjectByType<StadiumSound>();
            if(sound==null)throw new InvalidOperationException("Scene sound component missing");
            var serialized=new SerializedObject(sound);
            serialized.FindProperty("menuRecording").objectReferenceValue=BuildMenuMusic();serialized.ApplyModifiedProperties();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            MenuAudioChecks.StartChecks();
        }

        public static AudioClip[] BuildVoices()
        {
            var result=new AudioClip[7];
            for(int i=0;i<result.Length;i++)
            {
                string path="Assets/FenerRunner/Art/Audio/UserVoices/ses"+(i+1)+".wav";
                if(!File.Exists(path))throw new FileNotFoundException("User voice missing",path);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);
                var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.8f;settings.preloadAudioData=true;
                importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.SaveAndReimport();
                result[i]=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if(result[i]==null||result[i].length<=0)throw new InvalidOperationException("User voice decode failed: "+path);
            }
            return result;
        }
        public static AudioClip Build()
        {
            const string input="Assets/FenerRunner/Art/Audio/StadiumSource.mp3";
            const string output="Assets/FenerRunner/Art/Audio/StadiumCrowdLoop.wav";
            if(!File.Exists(output))
            {
                AssetDatabase.ImportAsset(input,ImportAssetOptions.ForceSynchronousImport);
                var importer=(AudioImporter)AssetImporter.GetAtPath(input);
                var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
                var source=AssetDatabase.LoadAssetAtPath<AudioClip>(input);source.LoadAudioData();
                var samples=new float[source.samples*source.channels];
                if(!source.GetData(samples,0))throw new InvalidOperationException("Crowd source cannot be decoded");
                const int rate=22050,seconds=42,cross=2;int channels=source.channels;
                using(var writer=new BinaryWriter(File.Create(output)))
                {
                    int bytes=rate*seconds*channels*2;
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+bytes);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)channels);writer.Write(rate);writer.Write(rate*channels*2);writer.Write((short)(channels*2));writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(bytes);
                    for(int frame=0;frame<rate*seconds;frame++)for(int c=0;c<channels;c++)
                    {
                        float time=(float)frame/rate;int index=(int)((30+time)*source.frequency)*channels+c;float value=samples[index];
                        if(time>=seconds-cross)
                        {float t=(time-(seconds-cross))/cross;int start=(int)((30-cross+time-(seconds-cross))*source.frequency)*channels+c;value=Mathf.Lerp(value,samples[start],t);}
                        writer.Write((short)Mathf.Clamp(value*26000,-32767,32767));
                    }
                }
                source.UnloadAudioData();AssetDatabase.ImportAsset(output,ImportAssetOptions.ForceSynchronousImport);
            }
            var loopImporter=(AudioImporter)AssetImporter.GetAtPath(output);var loopSettings=loopImporter.defaultSampleSettings;
            loopSettings.loadType=AudioClipLoadType.Streaming;loopSettings.compressionFormat=AudioCompressionFormat.Vorbis;loopSettings.quality=.6f;
            loopImporter.defaultSampleSettings=loopSettings;loopImporter.loadInBackground=true;loopImporter.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(output);
        }
    }
}
