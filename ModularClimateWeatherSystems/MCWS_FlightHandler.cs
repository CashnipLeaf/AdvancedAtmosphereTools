using System;
using System.Reflection;
using UnityEngine;

namespace ModularClimateWeatherSystems
{
    using Random = System.Random; //there apparently exists a UnityEngine.Random, which is not what I want.

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
        internal bool FARConnected = false;
        internal double CurrentTime => Planetarium.GetUniversalTime();
        internal static double DEFAULTINTERVAL => MCWS_API.DEFAULTINTERVAL;

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
        internal bool[] ScaleLog = new bool[3] { false, false, false }; 

        internal Vector3 RawWind = Vector3.zero;
        internal Vector3 AppliedWind = Vector3.zero;

        //wind not multiplied by the wind speed multiplier. for API only.
        internal Vector3 normalwind = Vector3.zero;
        internal Vector3 transformedwind = Vector3.zero;

        //for the "disable wind while splashed or landed and craft is stationary" setting. for flight dynamics use only.
        internal Vector3 InternalAppliedWind => AppliedWind * DisableMultiplier; 
        private float DisableMultiplier = 1.0f;

        //Arrays are stored as 1D to eliminate the extra bounds-checking overhead that comes with multidimensional arrays
        private float[] winddataX1; 
        private float[] winddataY1;
        private float[] winddataZ1;
        private float[] winddataX2;
        private float[] winddataY2;
        private float[] winddataZ2;
        //Attributes for easy conversion from 1D to 3D and vice versa
        internal float[,,] WindDataX1 //Why did I decide to write so much boilerplate?
        {
            get => To3D(winddataX1, windlengths[0], windlengths[1], windlengths[2]);
            private set => winddataX1 = To1D(value);
        }
        internal float[,,] WindDataY1
        {
            get => To3D(winddataY1, windlengths[0], windlengths[1], windlengths[2]);
            private set => winddataY1 = To1D(value);
        }
        internal float[,,] WindDataZ1
        {
            get => To3D(winddataZ1, windlengths[0], windlengths[1], windlengths[2]);
            private set => winddataZ1 = To1D(value);
        }
        internal float[,,] WindDataX2
        {
            get => To3D(winddataX2, windlengths[0], windlengths[1], windlengths[2]);
            private set => winddataX2 = To1D(value);
        }
        internal float[,,] WindDataY2
        {
            get => To3D(winddataY2, windlengths[0], windlengths[1], windlengths[2]);
            private set => winddataY2 = To1D(value);
        }
        internal float[,,] WindDataZ2
        {
            get => To3D(winddataZ2, windlengths[0], windlengths[1], windlengths[2]);
            private set => winddataZ2 = To1D(value);
        }
        private int[] windlengths = new int[3] { 0, 0, 0 };

        private double temperature = PhysicsGlobals.SpaceTemperature;
        internal double Temperature
        {
            get => temperature;
            private set => temperature = UtilMath.Clamp(value, PhysicsGlobals.SpaceTemperature, float.MaxValue);
        }
        private float[] temperaturedata1;
        private float[] temperaturedata2;
        internal float[,,] TemperatureData1
        {
            get => To3D(temperaturedata1, temperaturelengths[0], temperaturelengths[1], temperaturelengths[2]);
            private set => temperaturedata1 = To1D(value);
        }
        internal float[,,] TemperatureData2
        {
            get => To3D(temperaturedata2, temperaturelengths[0], temperaturelengths[1], temperaturelengths[2]);
            private set => temperaturedata2 = To1D(value);
        }
        private int[] temperaturelengths = new int[3] { 0, 0, 0 };

        private double pressure = 0.0;
        internal double Pressure
        {
            get => pressure;
            private set => pressure = UtilMath.Clamp(value, 0.0, float.MaxValue);
        }
        private float[] pressuredata1;
        private float[] pressuredata2;
        internal float[,,] PressureData1
        {
            get => To3D(pressuredata1, pressurelengths[0], pressurelengths[1], pressurelengths[2]);
            private set => pressuredata1 = To1D(value);
        }
        internal float[,,] PressureData2
        {
            get => To3D(pressuredata2, pressurelengths[0], pressurelengths[1], pressurelengths[2]);
            private set => pressuredata2 = To1D(value);
        }
        private int[] pressurelengths = new int[3] { 0, 0, 0 };

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

            //if there is a change of body, resynchronize timesteps and request new data.
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
                        ScaleLog = MCWS_API.GetScaling(mainbody.name);

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
                        double mapx = UtilMath.WrapAround((normalizedlon * windlengths[2]) - 0.5, 0, windlengths[2]);
                        double mapy = (normalizedlat * windlengths[1]) - 0.5;
                        double mapz = (ScaleLog[0] ? Utils.ScaleLog(normalizedalt) : normalizedalt) * windlengths[0];

                        double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                        double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                        double lerpz = alt >= 0.0 ? UtilMath.Clamp01(mapz - Math.Truncate(mapz)) : 0.0;
                        double lerpt = UtilMath.Clamp01((CurrentTime % Temptimestep) / Temptimestep);

                        int x1 = (int)UtilMath.Clamp(Math.Truncate(mapx), 0, windlengths[2] - 1);
                        int x2 = UtilMath.WrapAround(x1 + 1, 0, windlengths[2] - 1);

                        int y1 = Utils.Clamp((int)Math.Truncate(mapy), 0, windlengths[1] - 1) * windlengths[2];
                        int y2 = Utils.Clamp(y1 + 1, 0, windlengths[1] - 1) * windlengths[2];

                        int z1 = Utils.Clamp((int)Math.Truncate(mapz), 0, windlengths[0] - 1) * windlengths[1] * windlengths[2];
                        int z2 = Utils.Clamp(z1 + 1, 0, windlengths[0] - 1) * windlengths[1] * windlengths[2];

                        //Bilinearly interpolate on the longitude and latitude axes 
                        float BottomPlaneX1 = Utils.BiLerp(winddataX1[z1 + y1 + x1], winddataX1[z1 + y1 + x2], winddataX1[z1 + y2 + x1], winddataX1[z1 + y2 + x2], (float)lerpx, (float)lerpy);
                        float TopPlaneX1 = Utils.BiLerp(winddataX1[z2 + y1 + x1], winddataX1[z2 + y1 + x2], winddataX1[z2 + y2 + x1], winddataX1[z2 + y2 + x2], (float)lerpx, (float)lerpy);

                        float BottomPlaneX2 = Utils.BiLerp(winddataX2[z1 + y1 + x1], winddataX2[z1 + y1 + x2], winddataX2[z1 + y2 + x1], winddataX2[z1 + y2 + x2], (float)lerpx, (float)lerpy);
                        float TopPlaneX2 = Utils.BiLerp(winddataX2[z2 + y1 + x1], winddataX2[z2 + y1 + x2], winddataX2[z2 + y2 + x1], winddataX2[z2 + y2 + x2], (float)lerpx, (float)lerpy);

                        float BottomPlaneY1 = Utils.BiLerp(winddataY1[z1 + y1 + x1], winddataY1[z1 + y1 + x2], winddataY1[z1 + y2 + x1], winddataY1[z1 + y2 + x2], (float)lerpx, (float)lerpy);
                        float TopPlaneY1 = Utils.BiLerp(winddataY1[z2 + y1 + x1], winddataY1[z2 + y1 + x2], winddataY1[z2 + y2 + x1], winddataY1[z2 + y2 + x2], (float)lerpx, (float)lerpy);

                        float BottomPlaneY2 = Utils.BiLerp(winddataY2[z1 + y1 + x1], winddataY2[z1 + y1 + x2], winddataY2[z1 + y2 + x1], winddataY2[z1 + y2 + x2], (float)lerpx, (float)lerpy);
                        float TopPlaneY2 = Utils.BiLerp(winddataY2[z2 + y1 + x1], winddataY2[z2 + y1 + x2], winddataY2[z2 + y2 + x1], winddataY2[z2 + y2 + x2], (float)lerpx, (float)lerpy);

                        float BottomPlaneZ1 = Utils.BiLerp(winddataZ1[z1 + y1 + x1], winddataZ1[z1 + y1 + x2], winddataZ1[z1 + y2 + x1], winddataZ1[z1 + y2 + x2], (float)lerpx, (float)lerpy);
                        float TopPlaneZ1 = Utils.BiLerp(winddataZ1[z2 + y1 + x1], winddataZ1[z2 + y1 + x2], winddataZ1[z2 + y2 + x1], winddataZ1[z2 + y2 + x2], (float)lerpx, (float)lerpy);

                        float BottomPlaneZ2 = Utils.BiLerp(winddataZ2[z1 + y1 + x1], winddataZ2[z1 + y1 + x2], winddataZ2[z1 + y2 + x1], winddataZ2[z1 + y2 + x2], (float)lerpx, (float)lerpy);
                        float TopPlaneZ2 = Utils.BiLerp(winddataZ2[z2 + y1 + x1], winddataZ2[z2 + y1 + x2], winddataZ2[z2 + y2 + x1], winddataZ2[z2 + y2 + x2], (float)lerpx, (float)lerpy);

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
                        double mapx = UtilMath.WrapAround((normalizedlon * temperaturelengths[2]) - 0.5, 0, temperaturelengths[2]);
                        double mapy = (normalizedlat * temperaturelengths[1]) - 0.5;
                        double mapz = (ScaleLog[1] ? Utils.ScaleLog(normalizedalt) : normalizedalt) * temperaturelengths[0];

                        double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                        double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                        double lerpz = alt >= 0.0 ? UtilMath.Clamp01(mapz - Math.Truncate(mapz)) : 0.0;
                        double lerpt = UtilMath.Clamp01((CurrentTime % Temptimestep) / Temptimestep);

                        int x1 = (int)UtilMath.Clamp(Math.Truncate(mapx), 0, temperaturelengths[2] - 1);
                        int x2 = UtilMath.WrapAround(x1 + 1, 0, temperaturelengths[2] - 1);

                        int y1 = Utils.Clamp((int)Math.Truncate(mapy), 0, temperaturelengths[1] - 1) * temperaturelengths[2];
                        int y2 = Utils.Clamp(y1 + 1, 0, temperaturelengths[1] - 1) * temperaturelengths[2];

                        int z1 = Utils.Clamp((int)Math.Truncate(mapz), 0, temperaturelengths[0] - 1) * temperaturelengths[1] * temperaturelengths[2];
                        int z2 = Utils.Clamp(z1 + 1, 0, temperaturelengths[0] - 1) * temperaturelengths[1] * temperaturelengths[2];

                        //Bilinearly interpolate on the longitude and latitude axes
                        float BottomPlane1 = Utils.BiLerp(temperaturedata1[z1 + y1 + x1], temperaturedata1[z1 + y1 + x2], temperaturedata1[z1 + y2 + x1], temperaturedata1[z1 + y2 + x2], (float)lerpx, (float)lerpy);
                        float TopPlane1 = Utils.BiLerp(temperaturedata1[z2 + y1 + x1], temperaturedata1[z2 + y1 + x2], temperaturedata1[z2 + y2 + x1], temperaturedata1[z2 + y2 + x2], (float)lerpx, (float)lerpy);

                        float BottomPlane2 = Utils.BiLerp(temperaturedata2[z1 + y1 + x1], temperaturedata2[z1 + y1 + x2], temperaturedata2[z1 + y2 + x1], temperaturedata2[z1 + y2 + x2], (float)lerpx, (float)lerpy);
                        float TopPlane2 = Utils.BiLerp(temperaturedata2[z2 + y1 + x1], temperaturedata2[z2 + y1 + x2], temperaturedata2[z2 + y2 + x1], temperaturedata2[z2 + y2 + x2], (float)lerpx, (float)lerpy);

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
                        double mapx = UtilMath.WrapAround((normalizedlon * pressurelengths[2]) - 0.5, 0.0, pressurelengths[2]);
                        double mapy = (normalizedlat * pressurelengths[1]) - 0.5;
                        double mapz = (ScaleLog[2] ? Utils.ScaleLog(normalizedalt) : normalizedalt) * pressurelengths[0];

                        double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                        double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                        double lerpz = alt >= 0.0 ? UtilMath.Clamp01(mapz - Math.Truncate(mapz)) : 0.0f;
                        double lerpt = UtilMath.Clamp01((CurrentTime % Presstimestep) / Presstimestep);

                        int x1 = (int)UtilMath.Clamp(Math.Truncate(mapx), 0, pressurelengths[2] - 1);
                        int x2 = UtilMath.WrapAround(x1 + 1, 0, pressurelengths[2] - 1);

                        int y1 = Utils.Clamp((int)Math.Truncate(mapy), 0, pressurelengths[1] - 1) * pressurelengths[2];
                        int y2 = Utils.Clamp(y1 + 1, 0, pressurelengths[1] - 1) * pressurelengths[2];

                        int z1 = Utils.Clamp((int)Math.Truncate(mapz), 0, pressurelengths[0] - 1) * pressurelengths[1] * pressurelengths[2];
                        int z2 = Utils.Clamp(z1 + 1, 0, pressurelengths[0] - 1) * pressurelengths[1] * pressurelengths[2];

                        //Bilinearly interpolate on the longitude and latitude axes
                        float BottomPlane1 = Utils.BiLerp(pressuredata1[z1 + y1 + x1], pressuredata1[z1 + y1 + x2], pressuredata1[z1 + y2 + x1], pressuredata1[z1 + y2 + x2], (float)lerpx, (float)lerpy);
                        float TopPlane1 = Utils.BiLerp(pressuredata1[z2 + y1 + x1], pressuredata1[z2 + y1 + x2], pressuredata1[z2 + y2 + x1], pressuredata1[z2 + y2 + x2], (float)lerpx, (float)lerpy);

                        float BottomPlane2 = Utils.BiLerp(pressuredata2[z1 + y1 + x1], pressuredata2[z1 + y1 + x2], pressuredata2[z1 + y2 + x1], pressuredata2[z1 + y2 + x2], (float)lerpx, (float)lerpy);
                        float TopPlane2 = Utils.BiLerp(pressuredata2[z2 + y1 + x1], pressuredata2[z2 + y1 + x2], pressuredata2[z2 + y2 + x1], pressuredata2[z2 + y2 + x2], (float)lerpx, (float)lerpy);

                        //Linearly interpolate on the time axis
                        double BottomPlaneFinal = UtilMath.Lerp((double)BottomPlane1, (double)BottomPlane2, lerpt);
                        double TopPlaneFinal = UtilMath.Lerp((double)TopPlane1, (double)TopPlane2, lerpt);

                        //Logarithmically interpolate on the altitude axis.
                        double Final = Utils.InterpolateLog(BottomPlaneFinal, TopPlaneFinal, lerpz);
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
                    windlengths[0] = winddata1[0].GetLength(0);
                    windlengths[1] = winddata1[0].GetLength(1);
                    windlengths[2] = winddata1[0].GetLength(2);
                    WindDataX1 = winddata1[0];
                    WindDataY1 = winddata1[1];
                    WindDataZ1 = winddata1[2];
                    WindDataX2 = winddata2[0];
                    WindDataY2 = winddata2[1];
                    WindDataZ2 = winddata2[2];
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
                float[,,] tdata1 = MCWS_API.FetchGlobalTemperatureData(body, step1);
                float[,,] tdata2 = MCWS_API.FetchGlobalTemperatureData(body, step2);
                if (!MCWS_API.CheckArraySizes(tdata1, tdata2))
                {
                    throw new FormatException("The two Temperature data arrays are not of identical dimensions.");
                }
                temperaturelengths[0] = tdata1.GetLength(0);
                temperaturelengths[1] = tdata1.GetLength(1);
                temperaturelengths[2] = tdata1.GetLength(2);
                TemperatureData1 = tdata1;
                TemperatureData2 = tdata2;
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
                float[,,] pdata1 = MCWS_API.FetchGlobalPressureData(body, step1);
                float[,,] pdata2 = MCWS_API.FetchGlobalPressureData(body, step2);
                if (!MCWS_API.CheckArraySizes(pdata1, pdata2))
                {
                    throw new FormatException("The two Pressure data arrays are not of identical dimensions.");
                }
                pressurelengths[0] = pdata1.GetLength(0);
                pressurelengths[1] = pdata1.GetLength(1);
                pressurelengths[2] = pdata1.GetLength(2);
                PressureData1 = pdata1;
                PressureData2 = pdata2;
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
            HasData[0] = HasData[1] = HasData[2] = ScaleLog[0] = ScaleLog[1] = ScaleLog[2] = false;
            Sources[0] = Sources[1] = Sources[2] = "None";
        }

        //lightning fast conversions between 3D arrays and 1D arrays
        private static float[] To1D(float[,,] src)
        {
            if (src != null)
            {
                float[] newarr = new float[src.Length];
                Buffer.BlockCopy(src, 0, newarr, 0, Buffer.ByteLength(newarr));
                return newarr;
            }
            return null;
        }
        private static float[,,] To3D(float[] src, int x, int y, int z)
        {
            if (src != null)
            {
                float[,,] retarray = new float[x, y, z];
                Buffer.BlockCopy(src, 0, retarray, 0, Buffer.ByteLength(retarray));
                return retarray;
            }
            return null;
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
