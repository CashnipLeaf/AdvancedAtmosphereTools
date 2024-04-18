using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;
using ToolbarControl_NS;

namespace ModularClimateWeatherSystems
{
    //Sets up plugin settings.
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class MCWS_Startup : MonoBehaviour
    {
        public static MCWS_Startup Instance { get; private set; }
        public MCWS_Startup()
        {
            if (Instance == null)
            {
                Instance = this;
                Utils.LogInfo("Initializing Modular Climate & Weather Systems: Version " + Utils.version);
            }
            else
            {
                Utils.LogWarning("Destroying duplicate instance. Check your install for duplicate mod folders.");
                Destroy(this);
            }
        }

        void Awake()
        {
            Utils.LogInfo("Caching Localization Tags.");
            try
            {
                Utils.LOCCache = new Dictionary<string, string>();
                IEnumerator tags = Localizer.Tags.Keys.GetEnumerator();
                while (tags.MoveNext())
                {
                    if (tags.Current != null)
                    {
                        string tag = tags.Current.ToString();
                        if (tag.Contains("#LOC_MCWS_"))
                        {
                            Utils.LOCCache.Add(tag, Localizer.GetStringByTag(tag).Replace("\\n", "\n"));
                        }
                    }
                }
                Utils.LogInfo("Successfully cached Localization Tags.");
            }
            catch (Exception ex)
            {
                Utils.LogError("Exception thrown when caching Localization Tags: " + ex.ToString());
            }

            Utils.LogInfo("Registering MCWS with the toolbar controller.");
            try
            {
                if (ToolbarControl.RegisterMod(MCWS_FlightHandler.modID, MCWS_FlightHandler.modNAME))
                {
                    Utils.LogInfo("Successfully registered MCWS with the toolbar controller.");
                }
                else
                {
                    Utils.LogWarning("Unable to register MCWS with the toolbar. MCWS's UI will not be available.");
                }
            }
            catch (Exception e)
            {
                Utils.LogError("Exception thrown when registering MCWS with the toolbar controller: " + e.ToString());
            }
            
            Utils.LogInfo("Checking for an instance of FerramAerospaceResearch.");
            try
            {
                Type FARAtm = null;
                foreach (var assembly in AssemblyLoader.loadedAssemblies)
                {
                    if (assembly.name == "FerramAerospaceResearch")
                    {
                        var types = assembly.assembly.GetExportedTypes();
                        foreach (Type t in types)
                        {
                            if (t.FullName.Equals("FerramAerospaceResearch.FARWind"))
                            {
                                FARAtm = t;
                            }
                            if (t.FullName.Equals("FerramAerospaceResearch.FARAtmosphere"))
                            {
                                FARAtm = t;
                            }
                        }
                    }
                }
                Settings.FAR_Exists = FARAtm != null;
                Utils.LogInfo(Settings.FAR_Exists ? "FerramAerospaceResearch detected. Flight Dynamics calculations will be deferred to FAR." : "No instances of FerramAerospaceResearch detected.");
            }
            catch (Exception ex)
            {
                Utils.LogError("Exception thrown when checking for FerramAerospaceResearch: " + ex.ToString());
                Settings.FAR_Exists = false;
            }
            Utils.LogInfo("MCWS Setup Complete.");
        }
    }

    internal static class Utils //This used to be its own file, but it shrank so much that I thought it would be better to merge it with the startup file.
    {
        internal const string version = "0.9.0";
        internal static string GameDataPath => KSPUtil.ApplicationRootPath + "GameData/";
        internal static Dictionary<string, string> LOCCache; //localization cache

        internal static void LogInfo(string message) => Debug.Log("[MCWS] " + message); //General information
        internal static void LogWarning(string message) => Debug.LogWarning("[MCWS][WARNING] " + message); //Warnings indicate that MCWS may be operating at reduced functionality.
        internal static void LogAPI(string message) => Debug.Log("[MCWS][API] " + message); //API Logging
        internal static void LogAPIWarning(string message) => Debug.LogWarning("[MCWS][API WARNING] " + message); //API warnings
        internal static void LogError(string message) => Debug.LogError("[MCWS][ERROR] " + message); //Errors that invoke fail-safe protections.

        //------------------------------MATH AND RELATED-------------------------

        internal static double Epsilon => Mathf.Epsilon * 16d; //value that is very nearly zero to prevent the log interpolation from breaking

        //Desperate solution to try and save lines, did not end up saving that many lines. Might inline.
        internal static float BiLerp(float first1, float second1, float first2, float second2, float by1, float by2)
        {
            return Mathf.Lerp(Mathf.Lerp(first1, second1, by1), Mathf.Lerp(first2, second2, by1), by2);
        }

        //allow for altitude spacing based on some factor
        internal static double ScaleAltitude(double nX, double xBase, int upperbound, out int int1, out int int2)
        {
            nX = UtilMath.Clamp01(nX);
            double z = (xBase <= 1.0 ? nX : ((Math.Pow(xBase, -nX * upperbound) - 1) / (Math.Pow(xBase, -1 * upperbound) - 1))) * upperbound;
            int1 = Clamp((int)Math.Floor(z), 0, upperbound); //layer 1
            int2 = Clamp(int1 + 1, 0, upperbound); //layer 2
            return UtilMath.Clamp01(z - Math.Truncate(z)); //hack fix since the interpolation below did not work
            /*
            double z1 = xBase <= 1.0 ? (double)int1 / (double)upperbound : ((Math.Pow(xBase, int1) - 1) / (Math.Pow(xBase, upperbound) - 1));
            double z2 = xBase <= 1.0 ? (double)int2 / (double)upperbound : ((Math.Pow(xBase, int2) - 1) / (Math.Pow(xBase, upperbound) - 1));
            if (nX >= z2 || z2 <= z1)
            {
                return 1.0;
            }
            else if (nX <= z1)
            {
                return 0.0;
            }
            else
            {
                return UtilMath.Clamp01((nX - z1) / (z2 - z1)); //returns the lerp factor between the two layers
            }
            */
        }

        internal static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max); //Apparently no such function exists for integers. Why?
        internal static bool IsVectorFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        internal static bool IsVectorFinite(Vector3d vd) => double.IsFinite(vd.x) && double.IsFinite(vd.y) && double.IsFinite(vd.z); //might remove since nothing uses it.
    }
}
