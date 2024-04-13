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

        //Desperate solution to try and save lines, did not end up saving that many lines. Might inline.
        internal static float BiLerp(float first1, float second1, float first2, float second2, float by1, float by2)
        {
            return Mathf.Lerp(Mathf.Lerp(first1, second1, by1), Mathf.Lerp(first2, second2, by1), by2);
        }

        //Logarithmic interpolation, specifically used for pressure on the Z axis
        internal static double InterpolateLog(double first, double second, double by)
        {
            //control factors needed to prevent it from going straight to zero if either value is zero. 
            //This will only do things with positive values anyways, so support for negative values isn't required.
            return Math.Pow(Math.Max(first, 0.0000001), 1d - by) * Math.Pow(Math.Max(second, 0.0000001), by);
        }

        internal static double ScaleLog(double n) => Math.Log(UtilMath.Clamp01(n) + 1.0, 2.0); //apply a logarithm to altitude scaling if requested
        internal static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max); //Apparently no such function exists for integers. Why?
        internal static bool IsVectorFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        internal static bool IsVectorFinite(Vector3d vd) => double.IsFinite(vd.x) && double.IsFinite(vd.y) && double.IsFinite(vd.z); //might remove since nothing uses it.
    }
}
