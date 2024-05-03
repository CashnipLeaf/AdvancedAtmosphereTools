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
        internal Dictionary<string, BodyData> bodydata;
        
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
            ReadConfigs();

            Utils.LogInfo("MCWS Setup Complete.");
            DontDestroyOnLoad(this);
        }

        internal bool BodyExists(string body) => bodydata != null && bodydata.ContainsKey(body);
        internal bool HasWind(string body) => BodyExists(body) && bodydata[body].HasWind;
        internal bool HasFlowMaps(string body) => BodyExists(body) && bodydata[body].HasFlowmaps;
        internal bool HasTemperature(string body) => BodyExists(body) && bodydata[body].HasTemperature;
        internal bool HasPressure(string body) => BodyExists(body) && bodydata[body].HasPressure;

        internal double WindTimeStep(string body) => HasWind(body) ? bodydata[body].WindTimeStep : double.NaN;
        internal double TemperatureTimeStep(string body) => HasTemperature(body) ? bodydata[body].TemperatureTimeStep : double.NaN;
        internal double PressureTimeStep(string body) => HasPressure(body) ? bodydata[body].PressureTimeStep : double.NaN;

        internal double WindScaling(string body) => HasWind(body) ? bodydata[body].WindScaleFactor : 1d;
        internal double TemperatureScaling(string body) => HasTemperature(body) ?   bodydata[body].TemperatureScaleFactor : 1d;
        internal double PressureScaling(string body) => HasPressure(body) ? bodydata[body].PressureScaleFactor : 1d;

        internal double WindModelTop(string body) => HasWind(body) ? bodydata[body].WindModelTop : 0.0;
        internal double TemperatureModelTop(string body) => HasTemperature(body) ? bodydata[body].TemperatureModelTop : 0.0;
        internal double PressureModelTop(string body) => HasPressure(body) ? bodydata[body].PressureModelTop : 0.0;

        internal float[][,,] WindData(string body, double time) => HasWind(body) ? new float[3][,,] { bodydata[body].GetWindX(time), bodydata[body].GetWindY(time), bodydata[body].GetWindZ(time) } : null;
        internal float[,,] TemperatureData(string body, double time) => HasTemperature(body) ? bodydata[body].GetTemperature(time) : null;
        internal float[,,] PressureData(string body, double time) => HasPressure(body) ? bodydata[body].GetPressure(time) : null;

        internal Vector3 GetFlowMapWind(string body, double lon, double lat, double alt, double time) => HasFlowMaps(body) ? bodydata[body].GetFlowmapWind(lon, lat, alt, time) : Vector3.zero;
    }

    internal static class Utils 
    {
        internal const string version = "0.9.2";
        internal static string GameDataPath => KSPUtil.ApplicationRootPath + "GameData/";
        internal static Dictionary<string, string> LOCCache; //localization cache

        internal static void LogInfo(string message) => Debug.Log("[MCWS] " + message); //General information
        internal static void LogWarning(string message) => Debug.LogWarning("[MCWS][WARNING] " + message); //Warnings indicate that MCWS may be operating at reduced functionality.
        internal static void LogAPI(string message) => Debug.Log("[MCWS][API] " + message); //API Logging
        internal static void LogAPIWarning(string message) => Debug.LogWarning("[MCWS][API WARNING] " + message); //API warnings
        internal static void LogError(string message) => Debug.LogError("[MCWS][ERROR] " + message); //Errors that invoke fail-safe protections.

        //------------------------------MATH AND RELATED-------------------------
        internal static double Epsilon => float.Epsilon * 16d; //value that is very nearly zero to prevent the log interpolation from breaking

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
            return nX >= 1d ? 1d : UtilMath.Clamp01(z - Math.Truncate(z));
        }

        internal static double InterpolatePressure(double first, double second, double by)
        {
            if (first < 0.0 || second < 0.0) //negative values will break the logarithm, so they are not allowed.
            {
                throw new ArgumentOutOfRangeException();
            }
            if (first <= Epsilon || second <= Epsilon)
            {
                return UtilMath.Lerp(first, second, by);
            }
            double scalefactor = Math.Log(first / second);
            if (double.IsNaN(scalefactor))
            {
                throw new NotFiniteNumberException();
            }
            return first * Math.Pow(Math.E, -1 * UtilMath.Lerp(0.0, UtilMath.Clamp(scalefactor, float.MinValue, float.MaxValue), by));
        }

        internal static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max); //Apparently no such function exists for integers. Why?
        internal static bool IsVectorFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }

    internal class BodyData
    {
        private readonly string body;
        
        private float[][,,] WindDataX;
        private float[][,,] WindDataY;
        private float[][,,] WindDataZ;
        internal float WindScaleFactor = float.NaN;
        internal double WindTimeStep = double.NaN;
        internal double WindModelTop = 0.0;
        internal bool HasWind => WindDataX != null && WindDataY != null && WindDataZ != null && !double.IsNaN(WindTimeStep) && !float.IsNaN(WindScaleFactor);

        private float[][,,] TemperatureData;
        internal float TemperatureScaleFactor = float.NaN;
        internal double TemperatureTimeStep = double.NaN;
        internal double TemperatureModelTop = 0.0;
        internal bool HasTemperature => TemperatureData != null && !double.IsNaN(TemperatureTimeStep) && !float.IsNaN(TemperatureScaleFactor);

        private float[][,,] PressureData;
        internal float PressureScaleFactor = float.NaN;
        internal double PressureTimeStep = double.NaN;
        internal double PressureModelTop = 0.0f;
        internal bool HasPressure => PressureData != null && !double.IsNaN(PressureTimeStep) && !float.IsNaN(PressureScaleFactor);

        internal List<FlowMap> Flowmaps;
        internal bool HasFlowmaps => Flowmaps != null && Flowmaps.Count > 0;

        internal BodyData(string body)
        {
            Flowmaps = new List<FlowMap>();
            this.body = body;
        }

        internal void AddWindData(float[][,,] WindX, float[][,,] WindY, float[][,,] WindZ, float scalefactor, double timestep, double modeltop)
        {
            if (HasWind)
            {
                Utils.LogWarning(string.Format("Wind data already exists for {0}.", body));
            }
            else
            {
                WindDataX = WindX;
                WindDataY = WindY;
                WindDataZ = WindZ;
                WindScaleFactor = scalefactor;
                WindTimeStep = timestep;
                WindModelTop = modeltop;
                Utils.LogInfo(string.Format("Successfully added Wind Data to {0}.", body));
            }
        }

        internal void AddTemperatureData(float[][,,] Temp, float scalefactor, double timestep, double modeltop)
        {
            if (HasTemperature)
            {
                Utils.LogWarning(string.Format("Temperature data already exists for {0}.", body));
            }
            else
            {
                TemperatureData = Temp;
                TemperatureScaleFactor = scalefactor;
                TemperatureTimeStep = timestep;
                TemperatureModelTop = modeltop;
                Utils.LogInfo(string.Format("Successfully added Temperature Data to {0}.", body));
            }
        }

        internal void AddPressureData(float[][,,] Press, float scalefactor, double timestep, double modeltop)
        {
            if (HasPressure)
            {
                Utils.LogWarning(string.Format("Pressure data already exists for {0}.", body));
            }
            else
            {
                PressureData = Press;
                PressureScaleFactor = scalefactor;
                PressureTimeStep = timestep;
                PressureModelTop = modeltop;
                Utils.LogInfo(string.Format("Successfully added Pressure Data to {0}.", body));
            }
        }

        internal void AddFlowMap(FlowMap flowmap) => Flowmaps.Add(flowmap);

        internal float[,,] GetWindX(double time) => HasWind ? WindDataX[(int)Math.Floor(time / WindTimeStep) % WindDataX.Length] : null;
        internal float[,,] GetWindY(double time) => HasWind ? WindDataY[(int)Math.Floor(time / WindTimeStep) % WindDataY.Length] : null;
        internal float[,,] GetWindZ(double time) => HasWind ? WindDataZ[(int)Math.Floor(time / WindTimeStep) % WindDataZ.Length] : null;
        internal float[,,] GetTemperature(double time) => HasTemperature ? TemperatureData[(int)Math.Floor(time / TemperatureTimeStep) % TemperatureData.Length] : null;
        internal float[,,] GetPressure(double time) => HasPressure ? PressureData[(int)Math.Floor(time / PressureTimeStep) % PressureData.Length] : null;

        internal Vector3 GetFlowmapWind(double lon, double lat, double alt, double time)
        {
            Vector3 windvec = Vector3.zero;
            foreach (FlowMap map in Flowmaps)
            {
                windvec += map.GetWindVec(lon, lat, alt, time);
            }
            return windvec;
        }
    }

    internal class FlowMap
    {
        internal Texture2D flowmap;
        internal bool useThirdChannel; //whether or not to use the Blue channel to add a vertical component to the winds.
        internal FloatCurve AltitudeSpeedMultCurve;
        internal FloatCurve EW_AltitudeSpeedMultCurve;
        internal FloatCurve NS_AltitudeSpeedMultCurve;
        internal FloatCurve V_AltitudeSpeedMultCurve;
        internal FloatCurve WindSpeedMultiplierTimeCurve;
        internal float EWwind;
        internal float NSwind;
        internal float vWind;

        internal float timeoffset;

        internal int x;
        internal int y;

        internal FlowMap(Texture2D path, bool use3rdChannel, FloatCurve altmult, FloatCurve ewaltmultcurve, FloatCurve nsaltmultcurve, FloatCurve valtmultcurve, float EWwind, float NSwind, float vWind, FloatCurve speedtimecurve, float offset)
        {
            flowmap = path;
            useThirdChannel = use3rdChannel;
            AltitudeSpeedMultCurve = altmult;
            EW_AltitudeSpeedMultCurve = ewaltmultcurve;
            NS_AltitudeSpeedMultCurve = nsaltmultcurve;
            V_AltitudeSpeedMultCurve = valtmultcurve;
            WindSpeedMultiplierTimeCurve = speedtimecurve;
            this.EWwind = EWwind;
            this.NSwind = NSwind;
            this.vWind = vWind;
            timeoffset = WindSpeedMultiplierTimeCurve.maxTime != 0.0f ? offset % WindSpeedMultiplierTimeCurve.maxTime : 0.0f;

            x = flowmap.width;
            y = flowmap.height;
        }

        internal Vector3 GetWindVec(double lon, double lat, double alt, double time)
        {
            //AltitudeSpeedMultiplierCurve cannot go below 0.
            float speedmult = Math.Max(AltitudeSpeedMultCurve.Evaluate((float)alt), 0.0f) * WindSpeedMultiplierTimeCurve.Evaluate((float)(time - timeoffset + WindSpeedMultiplierTimeCurve.maxTime) % WindSpeedMultiplierTimeCurve.maxTime);
            if (speedmult > 0.0f)
            {
                //adjust longitude so the center of the map is the prime meridian for the purposes of these calculations
                double mapx = (((lon + 270.0) / 360.0) * x) - 0.5;
                double mapy = (((lat + 90.0) / 180.0) * y) - 0.5;
                double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));

                //locate the four nearby points, but don't go over the poles.
                int leftx = UtilMath.WrapAround((int)Math.Truncate(mapx), 0, x);
                int topy = Utils.Clamp((int)Math.Truncate(mapy), 0, y - 1);
                int rightx = UtilMath.WrapAround(leftx + 1, 0, x);
                int bottomy = Utils.Clamp(topy + 1, 0, y - 1);

                Color[] colors = new Color[4];
                Vector3[] vectors = new Vector3[4];
                colors[0] = flowmap.GetPixel(leftx, topy);
                colors[1] = flowmap.GetPixel(rightx, topy);
                colors[2] = flowmap.GetPixel(leftx, bottomy);
                colors[3] = flowmap.GetPixel(rightx, bottomy);

                for (int i = 0; i < 4; i++)
                {
                    Vector3 windvec = Vector3.zero;

                    windvec.z = (colors[i].r * 2.0f) - 1.0f;
                    windvec.x = (colors[i].g * 2.0f) - 1.0f;
                    windvec.y = useThirdChannel ? (colors[i].b * 2.0f) - 1.0f : 0.0f;
                    vectors[i] = windvec;
                }
                Vector3 wind = Vector3.Lerp(Vector3.Lerp(vectors[0], vectors[1], (float)lerpx), Vector3.Lerp(vectors[2], vectors[3], (float)lerpx), (float)lerpy);
                wind.x = wind.x * NSwind * NS_AltitudeSpeedMultCurve.Evaluate((float)alt);
                wind.y = wind.y * vWind * V_AltitudeSpeedMultCurve.Evaluate((float)alt);
                wind.z = wind.z * EWwind * EW_AltitudeSpeedMultCurve.Evaluate((float)alt);
                return wind * speedmult;
            }
            return Vector3.zero;
        }
    }
}
