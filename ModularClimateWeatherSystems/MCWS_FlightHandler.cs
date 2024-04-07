using System;
using System.Reflection;
using UnityEngine;

namespace ModularClimateWeatherSystems
{
    //Delegates for FAR
    using WindDelegate = Func<CelestialBody, Part, Vector3, Vector3>;
    using PropertyDelegate = Func<CelestialBody, Part, Vector3, double>;

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public partial class MCWS_FlightHandler : MonoBehaviour
    {
        public static MCWS_FlightHandler Instance { get; private set; }

        private Vessel activevessel;
        private CelestialBody mainbody;
        private CelestialBody previousbody;
        private Matrix4x4 vesselframe = Matrix4x4.identity;
        private Matrix4x4 inversevesselframe = Matrix4x4.identity;
        private double alt = 0.0;
        internal bool FARConnected = false;
        internal double CurrentTime { get; private set; } = 0.0;

        internal double Windtimestep { get; private set; } = MCWS_API.DEFAULTINTERVAL;
        internal double Windtimeofnextstep { get; private set; } = MCWS_API.DEFAULTINTERVAL;

        internal double Temptimestep { get; private set; } = MCWS_API.DEFAULTINTERVAL;
        internal double Temptimeofnextstep { get; private set; } = MCWS_API.DEFAULTINTERVAL;

        internal double Presstimestep { get; private set; } = MCWS_API.DEFAULTINTERVAL;
        internal double Presstimeofnextstep { get; private set; } = MCWS_API.DEFAULTINTERVAL;

        internal string WindSource { get; private set; } = "None";
        internal bool HasWind { get; private set; } = false;
        internal Vector3 RawWind { get; private set; } = Vector3.zero;
        internal Vector3 Multipliedwindvec => RawWind * Utils.GlobalWindSpeedMultiplier;
        internal Vector3 CachedWind { get; private set; } = Vector3.zero;
        internal Vector3 AppliedWind => CachedWind * Utils.GlobalWindSpeedMultiplier;
        internal Vector3[][][] winddata1;
        internal Vector3[][][] winddata2;

        internal string TempSource { get; private set; } = "None";
        internal bool HasTemp { get; private set; } = false;
        private double temperature = PhysicsGlobals.SpaceTemperature;
        internal double Temperature
        {
            get => temperature;
            private set => temperature = UtilMath.Clamp(value, PhysicsGlobals.SpaceTemperature, float.MaxValue);
        }
        internal float[][][] temperaturedata1;
        internal float[][][] temperaturedata2;

        internal bool HasPress { get; private set; } = false;
        internal string PressureSource { get; private set; } = "None";
        private double pressure = 0.0;
        internal double Pressure
        {
            get => pressure;
            private set => pressure = UtilMath.Clamp(value, 0.0, float.MaxValue);
        }
        internal float[][][] pressuredata1;
        internal float[][][] pressuredata2;

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
            CurrentTime = Planetarium.GetUniversalTime();

            //Get the worldframe of the vessel in question to transform the wind vector to be relative to the worldframe.
            vesselframe = Matrix4x4.identity;
            vesselframe.SetColumn(0, (Vector3)activevessel.north);
            vesselframe.SetColumn(1, (Vector3)activevessel.upAxis);
            vesselframe.SetColumn(2, (Vector3)activevessel.east);
            inversevesselframe = vesselframe.inverse;

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
                    bool[] hasdata = MCWS_API.HasExternalData(mainbody.name);
                    double[] timesteps = MCWS_API.GetTimeSteps(mainbody.name);
                    string[] sources = MCWS_API.GetSources(mainbody.name);

                    HasWind = hasdata[0];
                    HasTemp = hasdata[1];
                    HasPress = hasdata[2];
                    if (HasWind)
                    {
                        Windtimestep = double.IsFinite(timesteps[0]) && timesteps[0] > 0.0 ? timesteps[0] : MCWS_API.DEFAULTINTERVAL;
                        double prevstep = Math.Truncate(CurrentTime / Windtimestep) * Windtimestep;
                        Windtimeofnextstep = prevstep + Windtimestep;
                        GetNewWindData(mainbody.name, prevstep, Windtimeofnextstep);
                        WindSource = sources[0];
                    }
                    if (HasTemp)
                    {
                        Temptimestep = double.IsFinite(timesteps[1]) && timesteps[1] > 0.0 ? timesteps[1] : MCWS_API.DEFAULTINTERVAL;
                        double prevstep = Math.Truncate(CurrentTime / Temptimestep) * Temptimestep;
                        Temptimeofnextstep = prevstep + Temptimestep;
                        GetNewTemperatureData(mainbody.name, prevstep, Temptimeofnextstep);
                        TempSource = sources[1];
                    }
                    if (HasPress)
                    {
                        Presstimestep = double.IsFinite(timesteps[2]) && timesteps[2] > 0.0 ? timesteps[2] : MCWS_API.DEFAULTINTERVAL;
                        double prevstep = Math.Truncate(CurrentTime / Presstimestep) * Presstimestep;
                        Presstimeofnextstep = prevstep + Presstimestep;
                        GetNewPressureData(mainbody.name, prevstep, Presstimeofnextstep);
                        PressureSource = sources[2];
                    }
                }
                catch (Exception ex)
                {
                    Utils.LogError("Exception thrown when initializing data for body " + mainbody.name + ": " + ex.ToString());
                    HasWind = HasTemp = HasPress = false;
                    ClearGlobalData();
                    SetFallbackData();
                    return;
                }
            }

            //pause fetching of new data when timewarp is active.
            if(TimeWarp.CurrentRate <= 1.0f)
            {
                if (CurrentTime >= Windtimeofnextstep && HasWind)
                {
                    double prevstep = Math.Truncate(CurrentTime / Windtimestep) * Windtimestep;
                    Windtimeofnextstep = prevstep + Windtimestep;
                    GetNewWindData(mainbody.name, prevstep, Windtimeofnextstep);
                }
                if (CurrentTime >= Temptimeofnextstep && HasTemp)
                {
                    double prevstep = Math.Truncate(CurrentTime / Temptimestep) * Temptimestep;
                    Temptimeofnextstep = prevstep + Temptimestep;
                    GetNewTemperatureData(mainbody.name, prevstep, Temptimeofnextstep);
                }
                if (CurrentTime >= Presstimeofnextstep && HasPress)
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
                if (HasWind && winddata1 != null && winddata2 != null)
                {
                    try //some fun 4D interpolation
                    {
                        //derive the locations of the data in the arrays
                        double mapx = UtilMath.WrapAround((normalizedlon * winddata1.Length) - 0.5, 0, winddata1.Length);
                        double mapy = (normalizedlat * winddata1[0].Length) - 0.5;
                        double mapz = normalizedalt * winddata1[0][0].Length;
                        
                        float lerpx = (float)UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                        float lerpy = (float)UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                        float lerpz = alt >= 0.0 ? (float)UtilMath.Clamp01(mapz - Math.Truncate(mapz)) : 0.0f;
                        float lerpt = (float)UtilMath.Clamp01((CurrentTime % Windtimestep) / Windtimestep);

                        int leftx = (int)Math.Truncate(mapx);
                        int rightx = UtilMath.WrapAround((int)Math.Truncate(mapx) + 1, 0, winddata1.Length);

                        int bottomy = Utils.Clamp((int)Math.Truncate(mapy), 0, winddata1[0].Length - 1);
                        int topy = Utils.Clamp((int)Math.Truncate(mapy) + 1, 0, winddata1[0].Length - 1);

                        int bottomz = Utils.Clamp((int)Math.Truncate(mapz), 0, winddata1[0][0].Length - 2);
                        int topz = Utils.Clamp(bottomz + 1, 0, winddata1[0][0].Length - 1);

                        //Bilinearly interpolate on the longitude and latitude axes
                        Vector3 BottomPlane1 = Utils.BiLerpVector(winddata1[leftx][bottomy][bottomz], winddata1[rightx][bottomy][bottomz], winddata1[leftx][topy][bottomz], winddata1[rightx][topy][bottomz], lerpx, lerpy);
                        Vector3 TopPlane1 = Utils.BiLerpVector(winddata1[leftx][bottomy][topz], winddata1[rightx][bottomy][topz], winddata1[leftx][topy][topz], winddata1[rightx][topy][topz], lerpx, lerpy);

                        Vector3 BottomPlane2 = Utils.BiLerpVector(winddata2[leftx][bottomy][bottomz], winddata2[rightx][bottomy][bottomz], winddata2[leftx][topy][bottomz], winddata2[rightx][topy][bottomz], lerpx, lerpy);
                        Vector3 TopPlane2 = Utils.BiLerpVector(winddata2[leftx][bottomy][topz], winddata2[rightx][bottomy][topz], winddata2[leftx][topy][topz], winddata2[rightx][topy][topz], lerpx, lerpy);

                        //Bilinearly interpolate on the altitude and time axes
                        Vector3 Final = Utils.BiLerpVector(BottomPlane1, TopPlane1, BottomPlane2, TopPlane2, lerpz, lerpt);
                        RawWind = Utils.IsVectorFinite(Final) ? Final : throw new NotFiniteNumberException();
                        CachedWind = vesselframe * RawWind;
                    }
                    catch (Exception ex) //fallback data
                    {
                        Utils.LogError("Exception thrown when deriving point Wind data for " + mainbody.name + ":" + ex.ToString());
                        winddata1 = winddata2 = null;
                        CachedWind = RawWind = Vector3.zero;
                    }
                }
                if (HasTemp && temperaturedata1 != null && temperaturedata2 != null)
                {
                    try //some fun 4D interpolation
                    {
                        //derive the locations of the data in the arrays
                        double mapx = UtilMath.WrapAround((normalizedlon * temperaturedata1.Length) - 0.5, 0, temperaturedata1.Length);
                        double mapy = (normalizedlat * temperaturedata1[0].Length) - 0.5;
                        double mapz = normalizedalt * temperaturedata1[0][0].Length;

                        double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                        double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                        double lerpz = alt >= 0.0 ? UtilMath.Clamp01(mapz - Math.Truncate(mapz)) : 0.0;
                        double lerpt = UtilMath.Clamp01((CurrentTime % Temptimestep) / Temptimestep);

                        int leftx = (int)Math.Truncate(mapx);
                        int rightx = UtilMath.WrapAround((int)Math.Truncate(mapx) + 1, 0, temperaturedata1.Length);

                        int bottomy = Utils.Clamp((int)Math.Truncate(mapy), 0, temperaturedata1[0].Length - 1);
                        int topy = Utils.Clamp((int)Math.Truncate(mapy) + 1, 0, temperaturedata1[0].Length - 1);

                        int bottomz = Utils.Clamp((int)Math.Truncate(mapz), 0, temperaturedata1[0][0].Length - 2);
                        int topz = Utils.Clamp(bottomz + 1, 0, temperaturedata1[0][0].Length - 1);

                        //Bilinearly interpolate on the longitude and latitude axes
                        float BottomPlane1 = Utils.BiLerp(temperaturedata1[leftx][bottomy][bottomz], temperaturedata1[rightx][bottomy][bottomz], temperaturedata1[leftx][topy][bottomz], temperaturedata1[rightx][topy][bottomz], (float)lerpx, (float)lerpy);
                        float TopPlane1 = Utils.BiLerp(temperaturedata1[leftx][bottomy][topz], temperaturedata1[rightx][bottomy][topz], temperaturedata1[leftx][topy][topz], temperaturedata1[rightx][topy][topz], (float)lerpx, (float)lerpy);

                        float BottomPlane2 = Utils.BiLerp(temperaturedata2[leftx][bottomy][bottomz], temperaturedata2[rightx][bottomy][bottomz], temperaturedata2[leftx][topy][bottomz], temperaturedata2[rightx][topy][bottomz], (float)lerpx, (float)lerpy);
                        float TopPlane2 = Utils.BiLerp(temperaturedata2[leftx][bottomy][topz], temperaturedata2[rightx][bottomy][topz], temperaturedata2[leftx][topy][topz], temperaturedata2[rightx][topy][topz], (float)lerpx, (float)lerpy);

                        //Bilinearly interpolate on the altitude and time axes
                        double Final = Utils.BiLerp((double)BottomPlane1, (double)TopPlane1, (double)BottomPlane2, (double)TopPlane2, lerpz, lerpt);
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
                if (HasPress && pressuredata1 != null && pressuredata2 != null)
                {
                    try //some less fun 4D interpolation
                    {
                        //derive the locations of the data in the arrays
                        double mapx = UtilMath.WrapAround((normalizedlon * pressuredata1.Length) - 0.5, 0.0, pressuredata1.Length);
                        double mapy = (normalizedlat * pressuredata1[0].Length) - 0.5;
                        double mapz = normalizedalt * pressuredata1[0][0].Length;

                        double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                        double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                        double lerpz = alt >= 0.0 ? UtilMath.Clamp01(mapz - Math.Truncate(mapz)) : 0.0f;
                        double lerpt = UtilMath.Clamp01((CurrentTime % Presstimestep) / Presstimestep);

                        int leftx = (int)Math.Truncate(mapx);
                        int rightx = UtilMath.WrapAround((int)Math.Truncate(mapx) + 1, 0, pressuredata1.Length);

                        int bottomy = Utils.Clamp((int)Math.Truncate(mapy), 0, pressuredata1[0].Length - 1);
                        int topy = Utils.Clamp((int)Math.Truncate(mapy) + 1, 0, pressuredata1[0].Length - 1);

                        int bottomz = Utils.Clamp((int)Math.Truncate(mapz), 0, pressuredata1[0][0].Length - 1);
                        int topz = Utils.Clamp(bottomz + 1, 0, pressuredata1[0][0].Length - 1);

                        //Bilinearly interpolate on the longitude and latitude axes
                        float BottomPlane1 = Utils.BiLerp(pressuredata1[leftx][bottomy][bottomz], pressuredata1[rightx][bottomy][bottomz], pressuredata1[leftx][topy][bottomz], pressuredata1[rightx][topy][bottomz], (float)lerpx, (float)lerpy);
                        float TopPlane1 = Utils.BiLerp(pressuredata1[leftx][bottomy][topz], pressuredata1[rightx][bottomy][topz], pressuredata1[leftx][topy][topz], pressuredata1[rightx][topy][topz], (float)lerpx, (float)lerpy);

                        float BottomPlane2 = Utils.BiLerp(pressuredata2[leftx][bottomy][bottomz], pressuredata2[rightx][bottomy][bottomz], pressuredata2[leftx][topy][bottomz], pressuredata2[rightx][topy][bottomz], (float)lerpx, (float)lerpy);
                        float TopPlane2 = Utils.BiLerp(pressuredata2[leftx][bottomy][topz], pressuredata2[rightx][bottomy][topz], pressuredata2[leftx][topy][topz], pressuredata2[rightx][topy][topz], (float)lerpx, (float)lerpy);

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
            Utils.LogInfo("Fetching new Wind data for " + body + " from " + WindSource + ".");
            try
            {
                winddata1 = MCWS_API.FetchGlobalWindData(body, step1);
                winddata2 = MCWS_API.FetchGlobalWindData(body, step2);
            }
            catch (Exception ex)
            {
                Utils.LogError("Exception thrown when fetching new Wind data: " + ex.ToString());
                winddata1 = winddata2 = null;
            }
        }

        private void GetNewTemperatureData(string body, double step1, double step2)
        {
            Utils.LogInfo("Fetching new Temperature data for " + body + " from " + TempSource + ".");
            try
            {
                temperaturedata1 = MCWS_API.FetchGlobalTemperatureData(body, step1);
                temperaturedata2 = MCWS_API.FetchGlobalTemperatureData(body, step2);
            }
            catch (Exception ex)
            {
                Utils.LogError("Exception thrown when fetching new Temperature data: " + ex.ToString());
                temperaturedata1 = temperaturedata2 = null;
            } 
        }

        private void GetNewPressureData(string body, double step1, double step2)
        {
            Utils.LogInfo("Fetching new Pressure data for " + body + " from " + PressureSource + ".");
            try
            {
                pressuredata1 = MCWS_API.FetchGlobalPressureData(body, step1);
                pressuredata2 = MCWS_API.FetchGlobalPressureData(body, step2);
            }
            catch (Exception ex)
            {
                Utils.LogError("Exception thrown when fetching new Pressure data: " + ex.ToString());
                pressuredata1 = pressuredata2 = null;
            }
        }

        internal void ClearGlobalData()
        {
            winddata1 = winddata2 = null;
            temperaturedata1 = temperaturedata2 = null;
            pressuredata1 = pressuredata2 = null;
            WindSource = PressureSource = TempSource = "None";
        }

        internal void ClearAllData()
        {
            HasWind = HasPress = HasTemp = false;
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
