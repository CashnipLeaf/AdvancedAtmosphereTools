using System;
using System.Reflection;
using UnityEngine;

namespace ModularClimateWeatherSystems
{
    //Delegates for FAR
    using WindDelegate = Func<CelestialBody, Part, Vector3, Vector3>;
    using PropertyDelegate = Func<CelestialBody, Part, Vector3, double>;

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    partial class MCWS_FlightHandler : MonoBehaviour
    {
        public static MCWS_FlightHandler Instance { get; private set; }

        private Vessel activevessel;
        private CelestialBody mainbody;
        private CelestialBody previousbody;
        private Matrix4x4 Vesselframe = Matrix4x4.identity;
        private Matrix4x4 Inversevesselframe => Vesselframe.inverse;
        private double alt = 0.0;
        internal bool FARConnected = false;
        internal double CurrentTime => Planetarium.GetUniversalTime();

        internal double Windtimestep = MCWS_API.DEFAULTINTERVAL;
        internal double Windtimeofnextstep = MCWS_API.DEFAULTINTERVAL;

        internal double Temptimestep = MCWS_API.DEFAULTINTERVAL;
        internal double Temptimeofnextstep = MCWS_API.DEFAULTINTERVAL;

        internal double Presstimestep = MCWS_API.DEFAULTINTERVAL;
        internal double Presstimeofnextstep = MCWS_API.DEFAULTINTERVAL;

        internal bool[] HasData = new bool[3] { false, false, false };
        internal bool HasWind => HasData[0];
        internal bool HasTemp => HasData[1];
        internal bool HasPress=> HasData[2];

        internal string[] Sources = new string[3] { "None", "None", "None" };
        internal bool[] ScaleLog = new bool[3] { false, false, false };

        internal Vector3 RawWind = Vector3.zero;
        internal Vector3 Multipliedwindvec => RawWind * Utils.GlobalWindSpeedMultiplier;
        internal Vector3 CachedWind = Vector3.zero;
        internal Vector3 AppliedWind => CachedWind * Utils.GlobalWindSpeedMultiplier;
        internal float[,,] winddataX1;
        internal float[,,] winddataY1;
        internal float[,,] winddataZ1;
        internal float[,,] winddataX2;
        internal float[,,] winddataY2;
        internal float[,,] winddataZ2;

        private double temperature = PhysicsGlobals.SpaceTemperature;
        internal double Temperature
        {
            get => temperature;
            private set => temperature = UtilMath.Clamp(value, PhysicsGlobals.SpaceTemperature, float.MaxValue);
        }
        internal float[,,] temperaturedata1;
        internal float[,,] temperaturedata2;

        private double pressure = 0.0;
        internal double Pressure
        {
            get => pressure;
            private set => pressure = UtilMath.Clamp(value, 0.0, float.MaxValue);
        }
        internal float[,,] pressuredata1;
        internal float[,,] pressuredata2;

        public MCWS_FlightHandler()
        {
            if (Instance == null)
            {
                Utils.LogInfo("Initializing Flight Handler.");
                Instance = this;
            }
            else
            {
                Destroy(this);
            }
        }

        void Awake()
        {
            if (Utils.FAR_Exists)
            {
                Utils.LogInfo("Attempting to Register with FerramAerospaceResearch.");
                FARConnected = RegisterWithFAR();
            }
        }

        void FixedUpdate()
        {
            activevessel = null;
            if (!FlightGlobals.ready || FlightGlobals.ActiveVessel == null)
            {
                return;
            }
            activevessel = FlightGlobals.ActiveVessel;
            alt = activevessel.altitude;
            mainbody = activevessel.mainBody;

            //Get the worldframe of the vessel in question to transform the wind vector to be relative to the worldframe.
            Vesselframe.SetColumn(0, (Vector3)activevessel.north);
            Vesselframe.SetColumn(1, (Vector3)activevessel.upAxis);
            Vesselframe.SetColumn(2, (Vector3)activevessel.east);

            //if there is a change of body, resynchronize timesteps and request new data.
            if (mainbody != previousbody || previousbody == null)
            {
                previousbody = mainbody;
                try
                {
                    if (!mainbody.atmosphere)
                    {
                        ClearAllData();
                        return;
                    }

                    ClearGlobalData();
                    HasData = MCWS_API.HasExternalData(mainbody.name);
                    double[] timesteps = MCWS_API.GetTimeSteps(mainbody.name);
                    Sources = MCWS_API.GetSources(mainbody.name);
                    ScaleLog = MCWS_API.GetScaling(mainbody.name);

                    if (HasData[0])
                    {
                        Windtimestep = double.IsFinite(timesteps[0]) && timesteps[0] > 0.0 ? timesteps[0] : MCWS_API.DEFAULTINTERVAL;
                        double prevstep = Math.Truncate(CurrentTime / Windtimestep) * Windtimestep;
                        Windtimeofnextstep = prevstep + Windtimestep;
                        GetNewWindData(mainbody.name, prevstep, Windtimeofnextstep);
                    }
                    if (HasData[1])
                    {
                        Temptimestep = double.IsFinite(timesteps[1]) && timesteps[1] > 0.0 ? timesteps[1] : MCWS_API.DEFAULTINTERVAL;
                        double prevstep = Math.Truncate(CurrentTime / Temptimestep) * Temptimestep;
                        Temptimeofnextstep = prevstep + Temptimestep;
                        GetNewTemperatureData(mainbody.name, prevstep, Temptimeofnextstep);
                    }
                    if (HasData[2])
                    {
                        Presstimestep = double.IsFinite(timesteps[2]) && timesteps[2] > 0.0 ? timesteps[2] : MCWS_API.DEFAULTINTERVAL;
                        double prevstep = Math.Truncate(CurrentTime / Presstimestep) * Presstimestep;
                        Presstimeofnextstep = prevstep + Presstimestep;
                        GetNewPressureData(mainbody.name, prevstep, Presstimeofnextstep);
                    }
                }
                catch (Exception ex)
                {
                    Utils.LogError("Exception thrown when initializing data for body " + mainbody.name + ": " + ex.ToString());
                    HasData[0] = HasData[1] = HasData[2] = false;
                    ClearGlobalData();
                    SetFallbackData();
                    return;
                }
            }

            //pause fetching of new data when timewarp is active.
            if(TimeWarp.CurrentRate <= 1.0f)
            {
                if (CurrentTime >= Windtimeofnextstep && HasData[0])
                {
                    double prevstep = Math.Truncate(CurrentTime / Windtimestep) * Windtimestep;
                    Windtimeofnextstep = prevstep + Windtimestep;
                    GetNewWindData(mainbody.name, prevstep, Windtimeofnextstep);
                }
                if (CurrentTime >= Temptimeofnextstep && HasData[1])
                {
                    double prevstep = Math.Truncate(CurrentTime / Temptimestep) * Temptimestep;
                    Temptimeofnextstep = prevstep + Temptimestep;
                    GetNewTemperatureData(mainbody.name, prevstep, Temptimeofnextstep);
                }
                if (CurrentTime >= Presstimeofnextstep && HasData[2])
                {
                    double prevstep = Math.Truncate(CurrentTime / Presstimestep) * Presstimestep;
                    Presstimeofnextstep = prevstep + Presstimestep;
                    GetNewPressureData(mainbody.name, prevstep, Presstimeofnextstep);
                }
            }

            //cache information so it only needs to be calculated once per frame.
            SetFallbackData();
            if (mainbody.atmosphere && alt <= mainbody.atmosphereDepth)
            {
                double normalizedlon = (activevessel.longitude + 180.0) / 360.0;
                double normalizedlat = (180.0 - (activevessel.latitude + 90.0)) / 180.0;
                double normalizedalt = alt / mainbody.atmosphereDepth;
                if (HasData[0] && winddataX1 != null && winddataX2 != null && winddataY1 != null && winddataY2 != null && winddataY1 != null && winddataY2 != null)
                {
                    try //some very nasty 4D interpolation
                    {
                        //derive the locations of the data in the arrays
                        double mapx = UtilMath.WrapAround((normalizedlon * (winddataX1.GetUpperBound(2) + 1)) - 0.5, 0, winddataX1.GetUpperBound(2) + 1);
                        double mapy = (normalizedlat * (winddataX1.GetUpperBound(1) + 1)) - 0.5;
                        double mapz = (ScaleLog[0] ? Utils.ScaleLog(normalizedalt) : normalizedalt) * (winddataX1.GetUpperBound(0) + 1);

                        double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                        double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                        double lerpz = alt >= 0.0 ? UtilMath.Clamp01(mapz - Math.Truncate(mapz)) : 0.0;
                        double lerpt = UtilMath.Clamp01((CurrentTime % Temptimestep) / Temptimestep);

                        int x1 = (int)Math.Truncate(mapx);
                        int x2 = UtilMath.WrapAround(x1 + 1, 0, winddataX1.GetUpperBound(2));

                        int y1 = Utils.Clamp((int)Math.Truncate(mapy), 0, winddataX1.GetUpperBound(1));
                        int y2 = Utils.Clamp(y1 + 1, 0, winddataX1.GetUpperBound(1));

                        int z1 = Utils.Clamp((int)Math.Truncate(mapz), 0, winddataX1.GetUpperBound(0));
                        int z2 = Utils.Clamp(z1 + 1, 0, winddataX1.GetUpperBound(0));

                        //Bilinearly interpolate on the longitude and latitude axes
                        float BottomPlaneX1 = Utils.BiLerp(winddataX1[z1, y1, x1], winddataX1[z1, y1, x2], winddataX1[z1, y2, x1], winddataX1[z1, y2, x2], (float)lerpx, (float)lerpy);
                        float TopPlaneX1 = Utils.BiLerp(winddataX1[z2, y1, x1], winddataX1[z2, y1, x2], winddataX1[z2, y2, x1], winddataX1[z2, y2, x2], (float)lerpx, (float)lerpy);

                        float BottomPlaneX2 = Utils.BiLerp(winddataX2[z1, y1, x1], winddataX2[z1, y1, x2], winddataX2[z1, y2, x1], winddataX2[z1, y2, x2], (float)lerpx, (float)lerpy);
                        float TopPlaneX2 = Utils.BiLerp(winddataX2[z2, y1, x1], winddataX2[z2, y1, x2], winddataX2[z2, y2, x1], winddataX2[z2, y2, x2], (float)lerpx, (float)lerpy);

                        float BottomPlaneY1 = Utils.BiLerp(winddataY1[z1, y1, x1], winddataY1[z1, y1, x2], winddataY1[z1, y2, x1], winddataY1[z1, y2, x2], (float)lerpx, (float)lerpy);
                        float TopPlaneY1 = Utils.BiLerp(winddataY1[z2, y1, x1], winddataY1[z2, y1, x2], winddataY1[z2, y2, x1], winddataY1[z2, y2, x2], (float)lerpx, (float)lerpy);

                        float BottomPlaneY2 = Utils.BiLerp(winddataY2[z1, y1, x1], winddataY2[z1, y1, x2], winddataY2[z1, y2, x1], winddataY2[z1, y2, x2], (float)lerpx, (float)lerpy);
                        float TopPlaneY2 = Utils.BiLerp(winddataY2[z2, y1, x1], winddataY2[z2, y1, x2], winddataY2[z2, y2, x1], winddataY2[z2, y2, x2], (float)lerpx, (float)lerpy);

                        float BottomPlaneZ1 = Utils.BiLerp(winddataZ1[z1, y1, x1], winddataZ1[z1, y1, x2], winddataZ1[z1, y2, x1], winddataZ1[z1, y2, x2], (float)lerpx, (float)lerpy);
                        float TopPlaneZ1 = Utils.BiLerp(winddataZ1[z2, y1, x1], winddataZ1[z2, y1, x2], winddataZ1[z2, y2, x1], winddataZ1[z2, y2, x2], (float)lerpx, (float)lerpy);

                        float BottomPlaneZ2 = Utils.BiLerp(winddataZ2[z1, y1, x1], winddataZ2[z1, y1, x2], winddataZ2[z1, y2, x1], winddataZ2[z1, y2, x2], (float)lerpx, (float)lerpy);
                        float TopPlaneZ2 = Utils.BiLerp(winddataZ2[z2, y1, x1], winddataZ2[z2, y1, x2], winddataZ2[z2, y2, x1], winddataZ2[z2, y2, x2], (float)lerpx, (float)lerpy);

                        Vector3 BottomPlane1 = new Vector3(BottomPlaneX1, BottomPlaneY1, BottomPlaneZ1);
                        Vector3 TopPlane1 = new Vector3(TopPlaneX1, TopPlaneY1, TopPlaneZ1);
                        Vector3 BottomPlane2 = new Vector3(BottomPlaneX2, BottomPlaneY2, BottomPlaneZ2);
                        Vector3 TopPlane2 = new Vector3(TopPlaneX2, TopPlaneY2, TopPlaneZ2);

                        //Bilinearly interpolate on the altitude and time axes
                        Vector3 Final = Vector3.Lerp(Vector3.Lerp(BottomPlane1, TopPlane1, (float)lerpz), Vector3.Lerp(BottomPlane2, TopPlane2, (float)lerpz), (float)lerpt);
                        RawWind = Utils.IsVectorFinite(Final) ? Final : throw new NotFiniteNumberException();
                        CachedWind = Vesselframe * RawWind;
                    }
                    catch (Exception ex) //fallback data
                    {
                        Utils.LogError("Exception thrown when deriving point Wind data for " + mainbody.name + ":" + ex.ToString());
                        winddataX1 = winddataX2 = winddataY1 = winddataY2 = winddataZ1 = winddataZ2 = null;
                        CachedWind = RawWind = Vector3.zero;
                    }
                }
                if (HasData[1] && temperaturedata1 != null && temperaturedata2 != null)
                {
                    try //some less nasty 4D interpolation
                    {
                        //derive the locations of the data in the arrays
                        double mapx = UtilMath.WrapAround((normalizedlon * (temperaturedata1.GetUpperBound(2) + 1)) - 0.5, 0, temperaturedata1.GetUpperBound(2) + 1);
                        double mapy = (normalizedlat * (temperaturedata1.GetUpperBound(1) + 1)) - 0.5;
                        double mapz = (ScaleLog[1] ? Utils.ScaleLog(normalizedalt) : normalizedalt) * (temperaturedata1.GetUpperBound(0) + 1);

                        double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                        double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                        double lerpz = alt >= 0.0 ? UtilMath.Clamp01(mapz - Math.Truncate(mapz)) : 0.0;
                        double lerpt = UtilMath.Clamp01((CurrentTime % Temptimestep) / Temptimestep);

                        int x1 = (int)Math.Truncate(mapx);
                        int x2 = UtilMath.WrapAround(x1 + 1, 0, temperaturedata1.GetUpperBound(2));

                        int y1 = Utils.Clamp((int)Math.Truncate(mapy), 0, temperaturedata1.GetUpperBound(1));
                        int y2 = Utils.Clamp(y1 + 1, 0, temperaturedata1.GetUpperBound(1));

                        int z1 = Utils.Clamp((int)Math.Truncate(mapz), 0, temperaturedata1.GetUpperBound(0));
                        int z2 = Utils.Clamp(z1 + 1, 0, temperaturedata1.GetUpperBound(0));

                        //Bilinearly interpolate on the longitude and latitude axes
                        float BottomPlane1 = Utils.BiLerp(temperaturedata1[z1, y1, x1], temperaturedata1[z1, y1, x2], temperaturedata1[z1, y2, x1], temperaturedata1[z1, y2, x2], (float)lerpx, (float)lerpy);
                        float TopPlane1 = Utils.BiLerp(temperaturedata1[z2, y1, x1], temperaturedata1[z2, y1, x2], temperaturedata1[z2, y2, x1], temperaturedata1[z2, y2, x2], (float)lerpx, (float)lerpy);

                        float BottomPlane2 = Utils.BiLerp(temperaturedata2[z1, y1, x1], temperaturedata2[z1, y1, x2], temperaturedata2[z1, y2, x1], temperaturedata2[z1, y2, x2], (float)lerpx, (float)lerpy);
                        float TopPlane2 = Utils.BiLerp(temperaturedata2[z2, y1, x1], temperaturedata2[z2, y1, x2], temperaturedata2[z2, y2, x1], temperaturedata2[z2, y2, x2], (float)lerpx, (float)lerpy);

                        //Bilinearly interpolate on the altitude and time axes
                        double Final = UtilMath.Lerp(UtilMath.Lerp((double)BottomPlane1, (double)TopPlane1, lerpz), UtilMath.Lerp((double)BottomPlane2, (double)TopPlane2, lerpz), lerpt);
                        Temperature = double.IsFinite(Final) ? Final : throw new NotFiniteNumberException();
                    }
                    catch (Exception ex) //fallback data
                    {
                        Utils.LogError("Exception thrown when deriving point Temperature data for " + mainbody.name + ":" + ex.ToString());
                        temperaturedata1 = temperaturedata2 = null;
                        FlightIntegrator FI = activevessel.GetComponent<FlightIntegrator>();
                        Temperature = FI != null ? mainbody.GetFullTemperature(alt, FI.atmosphereTemperatureOffset) : mainbody.GetTemperature(alt);
                    }
                }
                if (HasData[2] && pressuredata1 != null && pressuredata2 != null)
                {
                    try //some nasty 4D interpolation
                    {
                        //derive the locations of the data in the arrays
                        double mapx = UtilMath.WrapAround((normalizedlon * (pressuredata1.GetUpperBound(2) + 1)) - 0.5, 0.0, pressuredata1.GetUpperBound(2) + 1);
                        double mapy = (normalizedlat * (pressuredata1.GetUpperBound(1) + 1)) - 0.5;
                        double mapz = (ScaleLog[2] ? Utils.ScaleLog(normalizedalt) : normalizedalt) * (pressuredata1.GetUpperBound(0) + 1);

                        double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                        double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                        double lerpz = alt >= 0.0 ? UtilMath.Clamp01(mapz - Math.Truncate(mapz)) : 0.0f;
                        double lerpt = UtilMath.Clamp01((CurrentTime % Presstimestep) / Presstimestep);

                        int x1 = (int)Math.Truncate(mapx);
                        int x2 = UtilMath.WrapAround(x1 + 1, 0, pressuredata1.GetUpperBound(2));

                        int y1 = Utils.Clamp((int)Math.Truncate(mapy), 0, pressuredata1.GetUpperBound(1));
                        int y2 = Utils.Clamp(y1 + 1, 0, pressuredata1.GetUpperBound(1));

                        int z1 = Utils.Clamp((int)Math.Truncate(mapz), 0, pressuredata1.GetUpperBound(0));
                        int z2 = Utils.Clamp(z1 + 1, 0, pressuredata1.GetUpperBound(0));

                        //Bilinearly interpolate on the longitude and latitude axes
                        float BottomPlane1 = Utils.BiLerp(pressuredata1[z1, y1, x1], pressuredata1[z1, y1, x2], pressuredata1[z1, y2, x1], pressuredata1[z1, y2, x2], (float)lerpx, (float)lerpy);
                        float TopPlane1 = Utils.BiLerp(pressuredata1[z2, y1, x1], pressuredata1[z2, y1, x2], pressuredata1[z2, y2, x1], pressuredata1[z2, y2, x2], (float)lerpx, (float)lerpy);

                        float BottomPlane2 = Utils.BiLerp(pressuredata2[z1, y1, x1], pressuredata2[z1, y1, x2], pressuredata2[z1, y2, x1], pressuredata2[z1, y2, x2], (float)lerpx, (float)lerpy);
                        float TopPlane2 = Utils.BiLerp(pressuredata2[z2, y1, x1], pressuredata2[z2, y1, x2], pressuredata2[z2, y2, x1], pressuredata2[z2, y2, x2], (float)lerpx, (float)lerpy);

                        //Linearly interpolate on the time axis
                        double BottomPlaneFinal = UtilMath.Lerp((double)BottomPlane1, (double)BottomPlane2, lerpt);
                        double TopPlaneFinal = UtilMath.Lerp((double)TopPlane1, (double)TopPlane2, lerpt);

                        //Exponentially interpolate on the altitude axis
                        double Final = Utils.InterpolatePressure(BottomPlaneFinal, TopPlaneFinal, lerpz);
                        Pressure = double.IsFinite(Final) ? Final : throw new NotFiniteNumberException();
                    }
                    catch (Exception ex) //fallback data
                    {
                        Utils.LogError("Exception thrown when deriving point Pressure data for " + mainbody.name + ":" + ex.ToString());
                        pressuredata1 = pressuredata2 = null;
                        Pressure = mainbody.GetPressure(alt);
                    }
                }
            }
        }

        void OnDestroy()
        {
            Utils.LogInfo("Flight Scene has ended. Unloading Flight Handler.");
            ClearAllData();
            RemoveToolbarButton();
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(RemoveToolbarButton);
        }

        //---------------------HELPER FUNCTIONS--------------------

        private void GetNewWindData(string body, double step1, double step2)
        {
            Utils.LogInfo("Fetching new Wind data for " + body + " from " + Sources[0] + ".");
            try
            {
                float[][,,] winddata1 = MCWS_API.FetchGlobalWindData(body, step1);
                float[][,,] winddata2 = MCWS_API.FetchGlobalWindData(body, step2);
                if (winddata1 != null && winddata2 != null)
                {
                    if (!MCWS_API.CheckArraySizes(winddata1[0], winddata2[0]))
                    {
                        throw new FormatException("The two sets of Wind data arrays are not of identical dimensions.");
                    }
                    winddataX1 = winddata1[0];
                    winddataY1 = winddata1[1];
                    winddataZ1 = winddata1[2];
                    winddataX2 = winddata2[0];
                    winddataY2 = winddata2[1];
                    winddataZ2 = winddata2[2];
                }
            }
            catch (Exception ex)
            {
                Utils.LogError("Exception thrown when fetching new Wind data: " + ex.ToString());
                winddataX1 = winddataX2 = winddataY1 = winddataY2 = winddataZ1 = winddataZ2 = null;
            }
        }

        private void GetNewTemperatureData(string body, double step1, double step2)
        {
            Utils.LogInfo("Fetching new Temperature data for " + body + " from " + Sources[1] + ".");
            try
            {
                temperaturedata1 = MCWS_API.FetchGlobalTemperatureData(body, step1);
                temperaturedata2 = MCWS_API.FetchGlobalTemperatureData(body, step2);
                if(!MCWS_API.CheckArraySizes(temperaturedata1, temperaturedata2))
                {
                    throw new FormatException("The two Temperature data arrays are not of identical dimensions.");
                }
            }
            catch (Exception ex)
            {
                Utils.LogError("Exception thrown when fetching new Temperature data: " + ex.ToString());
                temperaturedata1 = temperaturedata2 = null;
            } 
        }

        private void GetNewPressureData(string body, double step1, double step2)
        {
            Utils.LogInfo("Fetching new Pressure data for " + body + " from " + Sources[2] + ".");
            try
            {
                pressuredata1 = MCWS_API.FetchGlobalPressureData(body, step1);
                pressuredata2 = MCWS_API.FetchGlobalPressureData(body, step2);
                if(!MCWS_API.CheckArraySizes(pressuredata1, pressuredata2))
                {
                    throw new FormatException("The two Pressure data arrays are not of identical dimensions.");
                }
            }
            catch (Exception ex)
            {
                Utils.LogError("Exception thrown when fetching new Pressure data: " + ex.ToString());
                pressuredata1 = pressuredata2 = null;
            }
        }

        internal void ClearGlobalData()
        {
            winddataX1 = winddataX2 = winddataY1 = winddataY2 = winddataZ1 = winddataZ2 = null;
            temperaturedata1 = temperaturedata2 = null;
            pressuredata1 = pressuredata2 = null;
            Sources[0] = Sources[1] = Sources[2] = "None";
            ScaleLog[0] = ScaleLog[1] = ScaleLog[2] = false;
        }

        internal void ClearAllData()
        {
            HasData[0] = HasData[1] = HasData[2] = false;
            RawWind = CachedWind = Vector3.zero;
            Pressure = 0.0;
            Temperature = PhysicsGlobals.SpaceTemperature;
            ClearGlobalData();
        }

        //fallback to stock data if something goes wrong when retrieving things
        internal void SetFallbackData()
        {
            CachedWind = RawWind = Vector3.zero;
            if (mainbody.atmosphere && alt <= mainbody.atmosphereDepth)
            {
                FlightIntegrator FI = activevessel.GetComponent<FlightIntegrator>();
                Temperature = FI != null ? mainbody.GetFullTemperature(alt, FI.atmosphereTemperatureOffset) : mainbody.GetTemperature(alt);
                Pressure = mainbody.GetPressure(alt);
            }
            else
            {
                Pressure = 0.0;
                Temperature = PhysicsGlobals.SpaceTemperature;
            }
        }

        //----------------FerramAerospaceResearch Compatibility--------------

        //Functions for FAR to call
        internal Vector3 GetTheWind(CelestialBody body, Part p, Vector3 pos) => AppliedWind;
        internal double GetTheTemperature(CelestialBody body, Part p, Vector3 pos) => Temperature;
        internal double GetThePressure(CelestialBody body, Part p, Vector3 pos) => Pressure;

        //Register MCWS with FAR.
        internal bool RegisterWithFAR()
        {
            try
            {
                Type FARWindFunc = null;
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
                            if (t.FullName.Equals("FerramAerospaceResearch.FARWind+WindFunction"))
                            {
                                FARWindFunc = t;
                            }
                            if (t.FullName.Equals("FerramAerospaceResearch.FARAtmosphere"))
                            {
                                FARAtm = t;
                            }
                        }
                    }
                }
                if (FARAtm == null)
                {
                    goto NoFAR;
                }
                if (FARWindFunc != null) //Check if an older version of FAR is installed
                {
                    //Get FAR Wind Method
                    MethodInfo SetWindFunction = FARAtm.GetMethod("SetWindFunction");
                    if (SetWindFunction == null)
                    {
                        goto NoFAR;
                    }
                    //Set FARWind function
                    Utils.LogInfo("An older version of FerramAerospaceResearch is installed. Temperature and Pressure data will not be available to FAR.");
                    var del = Delegate.CreateDelegate(FARWindFunc, this, typeof(MCWS_FlightHandler).GetMethod("GetTheWind"), true);
                    SetWindFunction.Invoke(null, new object[] { del });
                }
                else
                {
                    //Get FAR Atmosphere Methods 
                    MethodInfo SetWindFunction = FARAtm.GetMethod("SetWindFunction");
                    MethodInfo SetTemperatureFunction = FARAtm.GetMethod("SetTemperatureFunction");
                    MethodInfo SetPressureFunction = FARAtm.GetMethod("SetPressureFunction");
                    if (SetWindFunction == null && SetTemperatureFunction == null && SetPressureFunction == null)
                    {
                        goto NoFAR;
                    }
                    if (SetWindFunction != null)
                    {
                        Utils.LogInfo("Registering Wind function with FerramAerospaceResearch");
                        SetWindFunction.Invoke(null, new object[] { (WindDelegate)GetTheWind });
                    }
                    if (SetTemperatureFunction != null)
                    {
                        Utils.LogInfo("Registering Temperature function with FerramAerospaceResearch");
                        SetTemperatureFunction.Invoke(null, new object[] { (PropertyDelegate)GetTheTemperature });
                    }
                    if (SetPressureFunction != null)
                    {
                        Utils.LogInfo("Registering Pressure function with FerramAerospaceResearch");
                        SetPressureFunction.Invoke(null, new object[] { (PropertyDelegate)GetThePressure });
                    }
                }
                Utils.LogInfo("Successfully registered with FerramAerospaceResearch.");
                return true;
            NoFAR:
                //If no wind or atmosphere is available return false
                Utils.LogWarning("Unable to register with FerramAerospaceResearch.");
                return false;
            }
            catch (Exception e)
            {
                Utils.LogError("Exception thrown when registering with FerramAerospaceResearch: " + e.ToString());
            }
            return false;
        }
    }
}
