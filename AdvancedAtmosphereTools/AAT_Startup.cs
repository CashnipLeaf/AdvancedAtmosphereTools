using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;
using ToolbarControl_NS;

namespace AdvancedAtmosphereTools
{
    //Sets up plugin settings.
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    partial class AAT_Startup : MonoBehaviour
    {
        public static AAT_Startup Instance { get; private set; }

        internal Dictionary<string, AAT_BodyData> bodydata;

        #region startup
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                Utils.LogInfo("Initializing Advanced Atmosphere Tools: Version " + Utils.version);

                ConfigNode[] settingsnodes = GameDatabase.Instance.GetConfigNodes("AAT_SETTINGS");
                if(settingsnodes.Length > 0)
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
                            if (tag.Contains("#LOC_AAT_"))
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

                Utils.LogInfo("Registering AAT with the toolbar controller.");
                try
                {
                    if (ToolbarControl.RegisterMod(AAT_FlightHandler.modID, AAT_FlightHandler.modNAME))
                    {
                        Utils.LogInfo("Successfully registered AAT with the toolbar controller.");
                    }
                    else
                    {
                        Utils.LogWarning("Unable to register AAT with the toolbar. AAT's UI will not be available.");
                    }
                }
                catch (Exception e)
                {
                    Utils.LogError("Exception thrown when registering AAT with the toolbar controller: " + e.ToString());
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

                Utils.LogInfo("Loading configs.");
                bodydata = new Dictionary<string, AAT_BodyData>();

                ConfigNode[] DataNodes = GameDatabase.Instance.GetConfigNodes("AdvancedAtmosphereTools");
                ReadConfigs(DataNodes, false);

                //support for legacy MCWS configs
                ConfigNode[] legacyDataNodes = GameDatabase.Instance.GetConfigNodes("MCWS_DATA");
                ReadConfigs(legacyDataNodes, true);

                Utils.LogInfo("All configs loaded. Performing cleanup.");

                //clean up BodyData objects with no data in them, or that somehow got assigned to a body with no atmosphere.
                List<string> todelete = new List<string>();
                foreach (KeyValuePair<string, AAT_BodyData> pair in bodydata)
                {
                    AAT_BodyData val = pair.Value;
                    if (!val.HasAtmo || (!val.HasWind && !val.HasTemperature && !val.HasPressure && !val.HasFlowmaps && !val.HasMolarMass && !val.HasAdiabaticIndex && !val.AtmosphereIsToxic && val.maxTempAngleOffset == 45.0))
                    {
                        todelete.Add(pair.Key);
                    }
                }
                foreach (string deleteme in todelete)
                {
                    Utils.LogInfo(string.Format("Removing empty data object for body {0}.", deleteme));
                    bodydata.Remove(deleteme);
                }
                Utils.LogInfo("Cleanup Complete.");

                Utils.LogInfo("Advanced Atmosphere Tools Setup Complete.");
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

        #region boilerplate
        internal bool BodyExists(string body) => bodydata != null && bodydata.ContainsKey(body);
        internal bool HasWind(string body) => BodyExists(body) && bodydata[body].HasWind;
        internal bool HasWindData(string body) => BodyExists(body) && bodydata[body].HasWindData;
        internal bool HasFlowmaps(string body) => BodyExists(body) && bodydata[body].HasFlowmaps;
        internal bool HasTemperature(string body) => BodyExists(body) && bodydata[body].HasTemperature;
        internal bool HasTemperatureData(string body) => BodyExists(body) && bodydata[body].HasTemperatureData;
        internal bool HasTemperatureMaps(string body) => BodyExists(body) && (bodydata[body].HasTemperatureOffsetMaps || bodydata[body].HasTemperatureSwingMaps);
        internal bool HasPressure(string body) => BodyExists(body) && bodydata[body].HasPressure;
        internal bool HasPressureData(string body) => BodyExists(body) && bodydata[body].HasPressureData;
        internal bool HasPressureMultiplier(string body) => BodyExists(body) && bodydata[body].HasPressureMultiplier;
        internal bool HasMolarMass(string body) => BodyExists(body) && bodydata[body].HasMolarMass;
        internal bool HasAdiabaticIndex(string body) => BodyExists(body) && bodydata[body].HasAdiabaticIndex;

        internal int GetWind(string body, double lon, double lat, double alt, double time, double trueanomaly, out Vector3 windvec, out Vector3 flowmapvec, out DataInfo windinfo)
        {
            windvec = Vector3.zero;
            flowmapvec = Vector3.zero;
            windinfo = DataInfo.Zero;
            int dataretcode = HasWindData(body) ? bodydata[body].GetDataWind(lon, lat, alt, time, ref windvec,ref windinfo) : -1;
            int flowmapretcode = HasFlowmaps(body) ? bodydata[body].GetFlowMapWind(lon, lat, alt, time, trueanomaly, ref flowmapvec) : -1;
            return SetDualRetCode(dataretcode, flowmapretcode);
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

        internal int GetTemperatureMapData(string body, double lon, double lat, double alt, double time, double trueanomaly, out double tempoffset, out double tempswingmult)
        {
            tempoffset = 0.0;
            tempswingmult = 1.0;
            if (HasTemperatureMaps(body))
            {
                int offsetcode = bodydata[body].GetTemperatureOffset(lon, lat, alt, time, trueanomaly, out tempoffset);
                int swingmultcode = bodydata[body].GetTemperatureSwingMultiplier(lon, lat, alt, time, trueanomaly, out tempswingmult);
                return SetDualRetCode(offsetcode, swingmultcode);
            }
            return -1;
        }
        internal int GetPressureMultiplier(string body, double lon, double lat, double alt, double time, double trueanomaly, out double pressmult)
        {
            pressmult = 1.0;
            return HasPressureMultiplier(body) ? bodydata[body].GetPressureMultiplier(lon, lat, alt, time, trueanomaly, out pressmult) : -1;
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

        internal int GetMolarMass(string body, double lon, double lat, double alt, double time, double trueanomaly, out double molarmass, out double molarmassoffset)
        {
            molarmass = 0.0;
            molarmassoffset = 0.0;
            if (HasMolarMass(body))
            {
                int baseretcode = bodydata[body].MolarMassCurve != null ? 0 : -1;
                if (baseretcode == 0)
                {
                    molarmass = bodydata[body].MolarMassCurve.Evaluate((float)alt);
                }
                int offsetretcode = bodydata[body].GetMolarMassOffset(lon, lat, alt, time, trueanomaly, out molarmassoffset);
                return SetDualRetCode(baseretcode, offsetretcode);
            }
            return -1;
        }

        internal int GetAdiabaticIndex(string body, double lon, double lat, double alt, double time, out double idx, out double idxoffset)
        {
            idx = 0.0;
            idxoffset = 0.0;
            if (HasAdiabaticIndex(body))
            {
                int baseretcode = bodydata[body].AdiabaticIndexCurve != null ? 0 : -1;
                if (baseretcode == 0)
                {
                    idx = bodydata[body].AdiabaticIndexCurve.Evaluate((float)alt);
                }
                int offsetretcode = bodydata[body].GetAdiabaticIndexOffset(lon, lat, alt, time, out idxoffset);
                return SetDualRetCode(baseretcode, offsetretcode);
            }
            return -1;
        }

        internal bool IsAtmosphereToxic(string body) => BodyExists(body) && bodydata[body].AtmosphereIsToxic;
        internal string AtmosphereToxicMessage(string body) => Localizer.Format(BodyExists(body) ? bodydata[body].atmosphereIsToxicMessage : "");

        internal bool HasMaxTempAngleOffset(string body, out double angleoffset)
        {
            angleoffset = 45.0;
            if (BodyExists(body))
            {
                angleoffset = bodydata[body].maxTempAngleOffset;
                return true;
            }
            return false;
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

        private static int SetDualRetCode(int retcode1, int retcode2)
        {
            if (retcode1 == 0 && retcode2 == 0)
            {
                return 0;
            }
            else if (retcode1 == 0 && retcode2 != 0)
            {
                return 1;
            }
            else if (retcode1 != 0 && retcode2 == 0)
            {
                return 2;
            }
            return -1;
        }
    }
}
