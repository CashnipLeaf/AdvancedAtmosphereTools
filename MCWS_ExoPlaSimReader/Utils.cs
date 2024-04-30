using UnityEngine;

namespace MCWS_ExoPlaSimReader
{
    internal static class Utils
    {
        internal static void LogInfo(string msg) => Debug.Log("[MCWS ExoPlaSimReader] " + msg);
        internal static void LogWarning(string msg) => Debug.LogWarning("[MCWS ExoPlaSimReader][WARNING] " + msg);
        internal static void LogError(string msg) => Debug.LogError("[MCWS ExoPlaSimReader][ERROR] " + msg);
    }
}
