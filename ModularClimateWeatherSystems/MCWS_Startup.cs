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
    partial class MCWS_Startup : MonoBehaviour
    {
        internal Dictionary<string, MCWS_BodyData> bodydata;

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
                DestroyImmediate(this);
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
            ReadConfigs();

            Utils.LogInfo("MCWS Setup Complete.");
            DontDestroyOnLoad(this);
        }

        internal bool BodyExists(string body) => bodydata != null && bodydata.ContainsKey(body);
        internal bool HasWind(string body) => BodyExists(body) && bodydata[body].HasWind;
        internal bool HasTemperature(string body) => BodyExists(body) && bodydata[body].HasTemperature;
        internal bool HasPressure(string body) => BodyExists(body) && bodydata[body].HasPressure;

        internal int GetWind(string body, double lon, double lat, double alt, double time, out Vector3 windvec)
        {
            windvec = Vector3.zero;
            return HasWind(body) ? bodydata[body].GetWind(lon, lat, alt, time, out windvec) : -1;
        }
        internal int GetTemperature(string body, double lon, double lat, double alt, double time, out double temp)
        {
            temp = 0.0;
            return HasTemperature(body) ? bodydata[body].GetTemperature(lon, lat, alt, time, out temp) : -1;
        }
        internal int GetPressure(string body, double lon, double lat, double alt, double time, out double press)
        {
            press = 0.0;
            return HasPressure(body) ? bodydata[body].GetPressure(lon, lat, alt, time, out press) : -1;
        }

        internal double WindModelTop(string body) => HasWind(body) ? bodydata[body].WindModelTop : double.MaxValue;
        internal double TemperatureModelTop(string body) => HasTemperature(body) ? bodydata[body].TempModelTop : double.MaxValue;
        internal double PressureModelTop(string body) => HasPressure(body) ? bodydata[body].PressModelTop : double.MaxValue;

        internal float[][,,] WindData(string body, double time)
        {
            if (HasWind(body))
            {
                if (bodydata[body].GetWindData(time, out float[][,,] data) == 0)
                {
                    return data;
                }
            }
            return null;
        }
        internal float[,,] TemperatureData(string body, double time)
        {
            if (HasTemperature(body))
            {
                if (bodydata[body].GetTemperatureData(time, out float[,,] data) == 0)
                {
                    return data;
                }
            }
            return null;
        }

        internal float[,,] PressureData(string body, double time)
        {
            if (HasPressure(body))
            {
                if (bodydata[body].GetPressureData(time, out float[,,] data) == 0)
                {
                    return data;
                }
            }
            return null;
        }
    }
}
