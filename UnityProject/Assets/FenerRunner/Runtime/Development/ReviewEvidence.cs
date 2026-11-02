#if UNITY_EDITOR
using UnityEditor;
namespace FenerRunner
{
    // Reusable scenarios keep earlier version evidence intact when a new suite runs.
    public static class ReviewEvidence
    {
        public static int Version => SessionState.GetInt("FenerRunner.ReviewVersion",7);
        public static string File(string path) => System.IO.Path.GetFullPath(path
            .Replace("_v7.", "_v"+Version+".").Replace("/v7-", "/v"+Version+"-"));
        public static string FileFor(int sourceVersion,string path)
        {
            int version=SessionState.GetInt("FenerRunner.ReviewVersion",sourceVersion);
            return System.IO.Path.GetFullPath(path.Replace("_v"+sourceVersion+".","_v"+version+".").Replace("/v"+sourceVersion+"-","/v"+version+"-"));
        }
    }
}
#endif
