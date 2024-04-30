using System;
using System.Reflection;
using UnityEngine;

namespace ModularClimateWeatherSystems
{
    using Random = System.Random; //there apparently exists a UnityEngine.Random, which is not what I want or need.

    //Delegates for FAR
    using WindDelegate = Func<CelestialBody, Part, Vector3, Vector3>;
    using PropertyDelegate = Func<CelestialBody, Part, Vector3, double>;

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    partial class MCWS_FlightHandler : MonoBehaviour //This is a partial class because I do not feel like passing variables to a separate GUI class.
    {
        public static MCWS_FlightHandler Instance { get; private set; }

        private Vessel activevessel;
        private CelestialBody mainbody;
        private CelestialBody previousbody;
        private Matrix4x4 Vesselframe = Matrix4x4.identity;
        internal bool FARConnected = false;
        internal double CurrentTime => Planetarium.GetUniversalTime();
        internal const double DEFAULTINTERVAL = 300.0;

        internal double Windtimestep = DEFAULTINTERVAL;
        internal double Windtimeofnextstep = DEFAULTINTERVAL;

        internal double Temptimestep = DEFAULTINTERVAL;
        internal double Temptimeofnextstep = DEFAULTINTERVAL;

        internal double Presstimestep = DEFAULTINTERVAL;
        internal double Presstimeofnextstep = DEFAULTINTERVAL;

        internal bool[] HasData = new bool[3] { false, false, false };
        internal bool HasWind => HasData[0];
        internal bool HasTemp => HasData[1];
        internal bool HasPress => HasData[2];

        internal string[] Sources = new string[3] { "None", "None", "None" };
        internal double[] ScaleFactors = new double[3] { 1d, 1d, 1d }; 

        internal Vector3 RawWind = Vector3.zero;
        internal Vector3 AppliedWind = Vector3.zero;

        //wind not multiplied by the wind speed multiplier. for API only.
        internal Vector3 normalwind = Vector3.zero;
        internal Vector3 transformedwind = Vector3.zero;

        //for the "disable wind while splashed or landed and craft is stationary" setting. for flight dynamics use only.
        internal Vector3 InternalAppliedWind => AppliedWind * DisableMultiplier; 
        private float DisableMultiplier = 1.0f;

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

        //stuff for wind speed variability
        private Random varywind;
        private double vary1 = 1.0;
        private double vary2 = 1.0;
        private const double varyinterval = 100d;
        private double timeofnextvary = 0d;

        public MCWS_FlightHandler() //prevent multiple FlightHandler instances from running.
        {
            if (Instance == null)
            {
                Utils.LogInfo("Initializing Flight Handler.");
                Instance = this;
            }
            else
            {
                Utils.LogWarning("Destroying duplicate Flight Handler. Check your install for duplicate mod folders.");
                Destroy(this);
            }
        }

        void Awake()
        {
            if (Settings.FAR_Exists)
            {
                Utils.LogInfo("Registering MCWS with FerramAerospaceResearch.");
                FARConnected = RegisterWithFAR();
            }

            //set up wind variability. seed the variability generator based on current time.
            varywind = new Random((DateTime.Now.Hour * 10000) + (DateTime.Now.Minute * 100) + DateTime.Now.Second); 
            vary2 = varywind.NextDouble();
        }

        void FixedUpdate()
        {
            Settings.CheckGameSettings();
            if (!FlightGlobals.ready || FlightGlobals.ActiveVessel == null)
            {
                activevessel = null;
                return;
            }
            activevessel = FlightGlobals.ActiveVessel;
            double alt = activevessel.altitude;
            mainbody = activevessel.mainBody;

            //Get the worldframe of the vessel in question to transform the wind vectors to the global coordinate frame.
            Vesselframe.SetColumn(0, (Vector3)activevessel.north);
            Vesselframe.SetColumn(1, (Vector3)activevessel.upAxis);
            Vesselframe.SetColumn(2, (Vector3)activevessel.east);

            //if there is a change of body, re-synchronize timesteps and request new data.
            if (mainbody != previousbody || previousbody == null) 
            {
                previousbody = mainbody;
                try
                {
                    ClearGlobalData();
                    if (mainbody.atmosphere)
                    {
                        HasData = MCWS_API.HasExternalData(mainbody.name);
                        double[] timesteps = MCWS_API.GetTimeSteps(mainbody.name);
                        Sources = MCWS_API.GetSources(mainbody.name);
                        ScaleFactors = MCWS_API.GetScaling(mainbody.name);

                        if (HasData[0])
                        {
                            Windtimestep = double.IsFinite(timesteps[0]) && timesteps[0] > 0.0 ? timesteps[0] : DEFAULTINTERVAL;
                            double prevstep = Math.Truncate(CurrentTime / Windtimestep) * Windtimestep;
                            Windtimeofnextstep = prevstep + Windtimestep;
                            GetNewWindData(mainbody.name, prevstep, Windtimeofnextstep);
                        }
                        if (HasData[1])
                        {
                            Temptimestep = double.IsFinite(timesteps[1]) && timesteps[1] > 0.0 ? timesteps[1] : DEFAULTINTERVAL;
                            double prevstep = Math.Truncate(CurrentTime / Temptimestep) * Temptimestep;
                            Temptimeofnextstep = prevstep + Temptimestep;
                            GetNewTemperatureData(mainbody.name, prevstep, Temptimeofnextstep);
                        }
                        if (HasData[2])
                        {
                            Presstimestep = double.IsFinite(timesteps[2]) && timesteps[2] > 0.0 ? timesteps[2] : DEFAULTINTERVAL;
                            double prevstep = Math.Truncate(CurrentTime / Presstimestep) * Presstimestep;
                            Presstimeofnextstep = prevstep + Presstimestep;
                            GetNewPressureData(mainbody.name, prevstep, Presstimeofnextstep);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Utils.LogError("Exception thrown when initializing data for body " + mainbody.name + ": " + ex.ToString());
                    ClearGlobalData();
                }
            }

            if (TimeWarp.CurrentRate <= 1.0f) //pause fetching of new data when timewarp is active.
            {
                if (CurrentTime > timeofnextvary)
                {
                    timeofnextvary = CurrentTime + varyinterval;
                    vary1 = vary2;
                    vary2 = varywind.NextDouble();
                }
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

            //pre-calculate a few factors to be used
            float VaryFactor = (float)(1.0 + UtilMath.Lerp(-Settings.WindSpeedVariability, Settings.WindSpeedVariability, UtilMath.Lerp(vary1, vary2, (CurrentTime - timeofnextvary) / varyinterval)));
            DisableMultiplier = activevessel != null && activevessel.LandedOrSplashed && Settings.DisableWindWhenStationary ? (float)UtilMath.Lerp(0.0, 1.0, (activevessel.srfSpeed - 5.0) * 0.2) : 1.0f;

            normalwind = transformedwind = AppliedWind = RawWind = Vector3.zero;
            if (mainbody.atmosphere && alt <= mainbody.atmosphereDepth)
            {
                //set fallback data
                FlightIntegrator FI = activevessel.GetComponent<FlightIntegrator>();
                Temperature = FI != null ? mainbody.GetFullTemperature(alt, FI.atmosphereTemperatureOffset) : mainbody.GetTemperature(alt);
                Pressure = mainbody.GetPressure(alt);

                double normalizedlon = (activevessel.longitude + 180.0) / 360.0;
                double normalizedlat = (180.0 - (activevessel.latitude + 90.0)) / 180.0;
                double normalizedalt = alt / mainbody.atmosphereDepth;

                if (HasData[0] && winddataX1 != null && winddataX2 != null && winddataY1 != null && winddataY2 != null && winddataZ1 != null && winddataZ2 != null)
                {
                    try //some very nasty 4D interpolation
                    {
                        //derive the locations of the data in the arrays
                        double mapx = UtilMath.WrapAround((normalizedlon * winddataX1.GetLength(2)) - 0.5, 0, winddataX1.GetLength(2));
                        double mapy = normalizedlat * winddataX1.GetUpperBound(1);

                        int x1 = (int)UtilMath.Clamp(Math.Truncate(mapx), 0, winddataX1.GetUpperBound(2));
                        int x2 = UtilMath.WrapAround(x1 + 1, 0, winddataX1.GetUpperBound(2));

                        int y1 = Utils.Clamp((int)Math.Floor(mapy), 0, winddataX1.GetUpperBound(1));
                        int y2 = Utils.Clamp(y1 + 1, 0, winddataX1.GetUpperBound(1));

                        double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                        double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                        double lerpz = Utils.ScaleAltitude(normalizedalt, ScaleFactors[0], winddataX1.GetUpperBound(0), out int z1, out int z2);
                        double lerpt = UtilMath.Clamp01((CurrentTime % Windtimestep) / Windtimestep);

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
                        //This is Fine™

                        //create some vectors out of the floats from the last calculations.
                        Vector3 BottomPlane1 = new Vector3(BottomPlaneX1, BottomPlaneY1, BottomPlaneZ1);
                        Vector3 TopPlane1 = new Vector3(TopPlaneX1, TopPlaneY1, TopPlaneZ1);
                        Vector3 BottomPlane2 = new Vector3(BottomPlaneX2, BottomPlaneY2, BottomPlaneZ2);
                        Vector3 TopPlane2 = new Vector3(TopPlaneX2, TopPlaneY2, TopPlaneZ2);

                        //Bilinearly interpolate on the altitude and time axes
                        Vector3 Final = Vector3.Lerp(Vector3.Lerp(BottomPlane1, TopPlane1, (float)lerpz), Vector3.Lerp(BottomPlane2, TopPlane2, (float)lerpz), (float)lerpt);

                        //add the variability factor to the wind vector, then transform it to the global coordinate frame
                        normalwind = Utils.IsVectorFinite(Final) ? Final * VaryFactor : throw new NotFiniteNumberException();
                        transformedwind = Vesselframe * normalwind;

                        //add wind speed multiplier
                        RawWind = normalwind * Settings.GlobalWindSpeedMultiplier;
                        AppliedWind = Vesselframe * RawWind;
                    }
                    catch (Exception ex) //fallback data
                    {
                        Utils.LogError("Exception thrown when deriving point Wind data for " + mainbody.name + ":" + ex.ToString());
                        winddataX1 = winddataX2 = winddataY1 = winddataY2 = winddataZ1 = winddataZ2 = null;
                        normalwind = transformedwind = AppliedWind = RawWind = Vector3.zero;
                    }
                }
                if (HasData[1] && temperaturedata1 != null && temperaturedata2 != null)
                {
                    try //some less nasty 4D interpolation
                    {
                        //derive the locations of the data in the arrays
                        double mapx = UtilMath.WrapAround((normalizedlon * temperaturedata1.GetLength(2)) - 0.5, 0, temperaturedata1.GetLength(2));
                        double mapy = normalizedlat * temperaturedata1.GetUpperBound(1);

                        int x1 = (int)UtilMath.Clamp(Math.Truncate(mapx), 0, temperaturedata1.GetUpperBound(2));
                        int x2 = UtilMath.WrapAround(x1 + 1, 0, temperaturedata1.GetUpperBound(2));

                        int y1 = Utils.Clamp((int)Math.Floor(mapy), 0, temperaturedata1.GetUpperBound(1));
                        int y2 = Utils.Clamp(y1 + 1, 0, temperaturedata1.GetUpperBound(1));

                        double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                        double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                        double lerpz = Utils.ScaleAltitude(normalizedalt, ScaleFactors[1], temperaturedata1.GetUpperBound(0), out int z1, out int z2);
                        double lerpt = UtilMath.Clamp01((CurrentTime % Temptimestep) / Temptimestep);

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
                        Temperature = FI != null ? mainbody.GetFullTemperature(alt, FI.atmosphereTemperatureOffset) : mainbody.GetTemperature(alt);
                    }
                }
                if (HasData[2] && pressuredata1 != null && pressuredata2 != null)
                {
                    try //some nasty 4D interpolation
                    {
                        //derive the locations of the data in the arrays
                        double mapx = UtilMath.WrapAround((normalizedlon * pressuredata1.GetLength(2)) - 0.5, 0.0, pressuredata1.GetLength(2));
                        double mapy = normalizedlat * pressuredata1.GetUpperBound(1);

                        int x1 = (int)UtilMath.Clamp(Math.Truncate(mapx), 0, pressuredata1.GetUpperBound(2));
                        int x2 = UtilMath.WrapAround(x1 + 1, 0, pressuredata1.GetUpperBound(2));

                        int y1 = Utils.Clamp((int)Math.Floor(mapy), 0, pressuredata1.GetUpperBound(1));
                        int y2 = Utils.Clamp(y1 + 1, 0, pressuredata1.GetUpperBound(1));

                        double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                        double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                        double lerpz = Utils.ScaleAltitude(normalizedalt, ScaleFactors[2], pressuredata1.GetUpperBound(0), out int z1, out int z2);
                        double lerpt = UtilMath.Clamp01((CurrentTime % Presstimestep) / Presstimestep);

                        //Bilinearly interpolate on the longitude and latitude axes
                        float BottomPlane1 = Utils.BiLerp(pressuredata1[z1, y1, x1], pressuredata1[z1, y1, x2], pressuredata1[z1, y2, x1], pressuredata1[z1, y2, x2], (float)lerpx, (float)lerpy);
                        float TopPlane1 = Utils.BiLerp(pressuredata1[z2, y1, x1], pressuredata1[z2, y1, x2], pressuredata1[z2, y2, x1], pressuredata1[z2, y2, x2], (float)lerpx, (float)lerpy);

                        float BottomPlane2 = Utils.BiLerp(pressuredata2[z1, y1, x1], pressuredata2[z1, y1, x2], pressuredata2[z1, y2, x1], pressuredata2[z1, y2, x2], (float)lerpx, (float)lerpy);
                        float TopPlane2 = Utils.BiLerp(pressuredata2[z2, y1, x1], pressuredata2[z2, y1, x2], pressuredata2[z2, y2, x1], pressuredata2[z2, y2, x2], (float)lerpx, (float)lerpy);

                        //Linearly interpolate on the time axis
                        double BottomPlaneFinal = UtilMath.Lerp((double)BottomPlane1, (double)BottomPlane2, lerpt);
                        double TopPlaneFinal = UtilMath.Lerp((double)TopPlane1, (double)TopPlane2, lerpt);

                        double Final = Utils.InterpolatePressure(BottomPlaneFinal,TopPlaneFinal, lerpz);
                        Pressure = double.IsFinite(Final) ? Final * 0.001 : throw new NotFiniteNumberException(); //convert to kPa
                    }
                    catch (Exception ex) //fallback data
                    {
                        Utils.LogError("Exception thrown when deriving point Pressure data for " + mainbody.name + ":" + ex.ToString());
                        pressuredata1 = pressuredata2 = null;
                        Pressure = mainbody.GetPressure(alt);
                    }
                }
            }
            else
            {
                Pressure = 0.0;
                Temperature = PhysicsGlobals.SpaceTemperature;
            }
        }

        void OnDestroy()
        {
            Utils.LogInfo("Flight Scene has ended. Unloading Flight Handler.");
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
                if (!MCWS_API.CheckArraySizes(temperaturedata1, temperaturedata2))
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
                if (!MCWS_API.CheckArraySizes(pressuredata1, pressuredata2))
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

        private void ClearGlobalData()
        {
            winddataX1 = winddataX2 = winddataY1 = winddataY2 = winddataZ1 = winddataZ2 = temperaturedata1 = temperaturedata2 = pressuredata1 = pressuredata2 = null;
            HasData[0] = HasData[1] = HasData[2] = false;
            ScaleFactors[0] = ScaleFactors[1] = ScaleFactors[2] = 1.0;
            Sources[0] = Sources[1] = Sources[2] = "None";
        }

        //----------------FerramAerospaceResearch Compatibility--------------

        //Functions for FAR to call
        internal Vector3 GetTheWind(CelestialBody body, Part p, Vector3 pos) => InternalAppliedWind;
        internal double GetTheTemperature(CelestialBody body, Part p, Vector3 pos) => Temperature;
        internal double GetThePressure(CelestialBody body, Part p, Vector3 pos) => Pressure;

        internal bool RegisterWithFAR() //Register MCWS with FAR.
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
                    Utils.LogWarning("Unable to register with FerramAerospaceResearch.");
                    return false;
                }
                if (FARWindFunc != null) //Check if an older version of FAR is installed
                {
                    //Get FAR Wind Method
                    MethodInfo SetWindFunction = FARAtm.GetMethod("SetWindFunction");
                    if (SetWindFunction == null)
                    {
                        Utils.LogWarning("Unable to register with FerramAerospaceResearch.");
                        return false;
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
                        Utils.LogWarning("Unable to register with FerramAerospaceResearch.");
                        return false;
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
            }
            catch (Exception e)
            {
                Utils.LogError("Exception thrown when registering with FerramAerospaceResearch: " + e.ToString());
            }
            return false;
        }
    }
}
