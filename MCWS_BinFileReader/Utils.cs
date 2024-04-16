using UnityEngine;

namespace MCWS_BinFileReader
{
    internal static class Utils
    {
        internal static void LogInfo(string msg) => Debug.Log("[MCWS BinFileReader] " + msg);
        internal static void LogWarning(string msg) => Debug.LogWarning("[MCWS BinFileReader][WARNING] " + msg);
        internal static void LogError(string msg) => Debug.LogError("[MCWS BinFileReader][ERROR] " + msg);
    }
}
