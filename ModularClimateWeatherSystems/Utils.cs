﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace ModularClimateWeatherSystems
{
    internal static class Utils //this class contains a bunch of helper and utility functions
    {
        //------------------------------LOGGING FUNCTIONS--------------------------------
        internal static void LogInfo(string message) => Debug.Log("[MCWS] " + message); //General information
        internal static void LogAPI(string message) => Debug.Log("[MCWS][API] " + message); //API Logging
        internal static void LogAPIWarning(string message) => Debug.LogWarning("[MCWS][API WARNING] " + message); //API warnings
        internal static void LogWarning(string message) => Debug.LogWarning("[MCWS][WARNING] " + message);
        internal static void LogError(string message) => Debug.LogError("[MCWS][ERROR] " + message); //Errors that invoke fail-safe protections.

        //------------------------------SETTINGS AND SETUP--------------------------------
        internal const string version = "DEVELOPMENT VER 0.1.0";
        internal static string GameDataPath => KSPUtil.ApplicationRootPath + "GameData/";

        internal static bool DevMode { get; private set; } = false;
        internal static bool Minutesforcoords { get; private set; } = false;
        internal static bool AdjustedIndicatorsDisabled { get; private set; } = false;
        internal static float GlobalWindSpeedMultiplier { get; private set; } = 1.0f;
        internal static bool FAR_Exists = false;

        internal static void CheckSettings()
        {
            bool debug = false;
            bool mins = false;
            bool indicator = true;
            float windspeedmult = 1.0f;
            ConfigNode[] settings = GameDatabase.Instance.GetConfigNodes("MCWS_SETTINGS");
            settings[0].TryGetValue("DeveloperMode", ref debug);
            settings[0].TryGetValue("UseMOAForCoords", ref mins);
            settings[0].TryGetValue("GlobalWindSpeedMultiplier", ref windspeedmult);
            settings[0].TryGetValue("DisableAdjustedProgradeIndicators", ref indicator);

            DevMode = debug;
            AdjustedIndicatorsDisabled = indicator;
            Minutesforcoords = mins;
            GlobalWindSpeedMultiplier = float.IsFinite(windspeedmult) ? Mathf.Clamp(windspeedmult, 0.0f, float.MaxValue) : 0.0f;
        }

        //------------------------------INTERPOLATION-------------------------

        //Bilinear Interpolation to SAVE ME SOME LINES
        internal static double BiLerp(double first1, double second1, double first2, double second2, double by1, double by2)
        {
            return UtilMath.Lerp(UtilMath.Lerp(first1, second1, by1), UtilMath.Lerp(first2, second2, by1), by2);
        }
        internal static Vector3 BiLerpVector(Vector3 first1, Vector3 second1, Vector3 first2, Vector3 second2, float by1, float by2)
        {
            return Vector3.Lerp(Vector3.Lerp(first1, second1, by1), Vector3.Lerp(first2, second2, by1), by2);
        }

        //Pressure cannot be interpolated linearly on the Z (up/down) axis due to its exponential decay. This algorithm does an exponential interpolation of sorts.
        internal static double InterpolatePressure(double first, double second, double by)
        {
            if (first < 0.0 || second < 0.0) //negative values will break the logarithm, so they are not allowed.
            {
                throw new ArgumentOutOfRangeException();
            }
            double scalefactor = Math.Log(first / second);
            if (double.IsNaN(scalefactor))
            {
                throw new NotFiniteNumberException();
            }
            return first * Math.Pow(Math.E, -1 * UtilMath.Lerp(0.0, UtilMath.Clamp(scalefactor, float.MinValue, float.MaxValue), by));
        }

        //------------------------------LOCALIZATION CACHE------------------------- 
        internal static Dictionary<string, string> LOCCache;

        internal static void CacheLOC()
        {
            LOCCache = new Dictionary<string, string>();
            IEnumerator tags = Localizer.Tags.Keys.GetEnumerator();
            while (tags.MoveNext())
            {
                if (tags.Current == null)
                {
                    continue;
                }
                string tag = tags.Current.ToString();
                if (tag.Contains("#LOC_MCWS_"))
                {
                    LOCCache.Add(tag, Localizer.GetStringByTag(tag).Replace("\\n", "\n"));
                }
            }
        }

        //------------------------------MISCELLANEOUS------------------------- 
        internal static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);
        internal static bool IsVectorFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        internal static bool IsVectorFinite(Vector3d vd) => double.IsFinite(vd.x) && double.IsFinite(vd.y) && double.IsFinite(vd.z);
    }
}