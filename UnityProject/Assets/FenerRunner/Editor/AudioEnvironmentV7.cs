using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace FenerRunner.Editor
{
    public static class AudioEnvironmentV7
    {
        [Serializable] class Snapshot
        {public string utc;public bool editorMasterMuted,gameMuted,listenerPaused;public float listenerVolume;public int sampleRate,speakerMode;}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.delayCall+=RecordInitial;}
        static Snapshot Read()=>new Snapshot{utc=DateTime.UtcNow.ToString("O"),editorMasterMuted=EditorUtility.audioMasterMute,
            gameMuted=PlayerPrefs.GetInt("FenerRunner.Muted",0)==1,listenerPaused=AudioListener.pause,listenerVolume=AudioListener.volume,
            sampleRate=AudioSettings.outputSampleRate,speakerMode=(int)AudioSettings.speakerMode};
        static void RecordInitial()
        {
            if(Application.isBatchMode)return;
            const string path="../Validation/audio_environment_before_v7.json";
            if(!File.Exists(path)){Directory.CreateDirectory("../Validation");File.WriteAllText(path,JsonUtility.ToJson(Read(),true));}
        }
        [MenuItem("Fener Runner/22 - Enable and diagnose Editor game audio")]
        public static void EnableAudio()
        {
            RecordInitial();
            EditorUtility.audioMasterMute=false;AudioListener.pause=false;AudioListener.volume=1;
            // Explicit repair command only. Normal Play and tests respect the player's mute choice.
            PlayerPrefs.SetInt("FenerRunner.Muted",0);PlayerPrefs.Save();
            File.WriteAllText("../Validation/audio_environment_after_v7.json",JsonUtility.ToJson(Read(),true));
            Debug.Log("Editor master audio enabled. Game sound enabled for this requested audio repair.");
        }
    }
}
