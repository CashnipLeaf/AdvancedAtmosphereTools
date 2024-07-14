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
        public static MCWS_Startup Instance { get; private set; }

        internal Dictionary<string, MCWS_BodyData> bodydata;

        #region startup
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                Utils.LogInfo("Initializing Modular Climate & Weather Systems: Version " + Utils.version);

                ConfigNode[] settingsnodes = GameDatabase.Instance.GetConfigNodes("MCWS_SETTINGS");
                if(settingsnodes.Length > 0 )
                {
                    bool debug = false;
                    settingsnodes[0].TryGetValue("debugMode", ref debug);
                    if (debug)
                    {
                        Utils.LogInfo("Debug mode enabled.");
                        Settings.debugmode = true;
                    }
                }

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
            else
            {
                Utils.LogWarning("Destroying duplicate instance. Check your install for duplicate mod folders.");
                DestroyImmediate(this);
            }
        }

        void OnDestroy()
        {
            if(bodydata != null)
            {
                bodydata?.Clear();
            }
            bodydata = null;
            if (Instance == this)
            {
                Instance = null;
            }
        }
        #endregion

        #region getdata
        internal bool BodyExists(string body) => bodydata != null && bodydata.ContainsKey(body);
        internal bool HasWind(string body) => BodyExists(body) && bodydata[body].HasWind;
        internal bool HasTemperature(string body) => BodyExists(body) && bodydata[body].HasTemperature;
        internal bool HasTemperatureData(string body) => BodyExists(body) && bodydata[body].HasTemperatureData;
        internal bool HasTemperatureMaps(string body) => BodyExists(body) && (bodydata[body].HasTemperatureOffsetMaps || bodydata[body].HasTemperatureSwingMaps);
        internal bool HasPressure(string body) => BodyExists(body) && bodydata[body].HasPressure;
        internal bool HasPressureData(string body) => BodyExists(body) && bodydata[body].HasPressureData;
        internal bool HasPressureMaps(string body) => BodyExists(body) && bodydata[body].HasPressureMaps;

        internal int GetWind(string body, double lon, double lat, double alt, double time, out Vector3 windvec, out Vector3 flowmapvec, out DataInfo windinfo)
        {
            windvec = Vector3.zero;
            flowmapvec = Vector3.zero;
            windinfo = DataInfo.Zero;
            return HasWind(body) ? bodydata[body].GetWind(lon, lat, alt, time, ref windvec, ref flowmapvec, ref windinfo) : -1;
        }
        internal int GetTemperature(string body, double lon, double lat, double alt, double time, out double temp, out DataInfo tempinfo)
        {
            temp = 0.0;
            tempinfo = DataInfo.Zero;
            return HasTemperatureData(body) ? bodydata[body].GetTemperature(lon, lat, alt, time, out temp, ref tempinfo) : -1;
        }
        internal int GetPressure(string body, double lon, double lat, double alt, double time, out double press, out DataInfo pressinfo)
        {
            press = 0.0;
            pressinfo = DataInfo.Zero;
            return HasPressureData(body) ? bodydata[body].GetPressure(lon, lat, alt, time, out press, ref pressinfo) : -1;
        }

        internal double WindModelTop(string body) => HasWind(body) ? bodydata[body].WindModelTop : double.MaxValue;
        internal double TemperatureModelTop(string body) => HasTemperatureData(body) ? bodydata[body].TempModelTop : double.MaxValue;
        internal double PressureModelTop(string body) => HasPressureData(body) ? bodydata[body].PressModelTop : double.MaxValue;

        //TODO: implement
        internal int GetTemperatureMapData(string body, double lon, double lat, double alt, double time, out double tempoffset, out double tempswingmult)
        {
            tempoffset = 0.0;
            tempswingmult = 1.0;
            if (HasTemperatureMaps(body))
            {
                int offsetcode = bodydata[body].GetTemperatureOffset(lon, lat, alt, time, out tempoffset);
                int swingmultcode = bodydata[body].GetTemperatureSwingMultiplier(lon, lat, alt, time, out tempswingmult);
                if (offsetcode == 0 && swingmultcode == 0)
                {
                    return 0;
                }
                else if (offsetcode == 0 && swingmultcode != 0)
                {
                    return 1;
                }
                else if (offsetcode != 0 && swingmultcode == 0)
                {
                    return 2;
                }
                else
                {
                    return -1;
                }
            }
            return -1;
        }
        internal int GetPressureMapData(string body, double lon, double lat, double alt, double time, out double pressmult)
        {
            pressmult = 1.0;
            return HasPressureMaps(body) ? bodydata[body].GetPressureMultiplier(lon, lat, alt, time, out pressmult) : -1;
        }

        internal bool BlendTemperature(string body, out double blendfactor)
        {
            blendfactor = HasTemperature(body) ? bodydata[body].BlendTempFactor : 0.0; 
            return HasTemperature(body) && bodydata[body].BlendTempWithStock;
        }
        internal bool BlendPressure(string body, out double blendfactor)
        {
            blendfactor = HasPressure(body) ? bodydata[body].BlendPressFactor : 0.0;
            return HasPressure(body) && bodydata[body].BlendPressWithStock;
        }

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
        #endregion
    }
}
