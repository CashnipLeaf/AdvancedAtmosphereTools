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
        private Matrix4x4 Vesselframe = Matrix4x4.identity;
        internal double CurrentTime => Planetarium.GetUniversalTime();
        internal static MCWS_Startup Data => MCWS_Startup.Instance;

        internal bool HasWind = false;
        internal bool HasTemp = false;
        internal bool HasPress = false;

        internal Vector3 RawWind = Vector3.zero;
        internal Vector3 AppliedWind = Vector3.zero;

        //wind not multiplied by the wind speed multiplier. for API only.
        internal Vector3 normalwind = Vector3.zero;
        internal Vector3 transformedwind = Vector3.zero;

        //for the "disable wind while splashed or landed and craft is stationary" setting. for flight dynamics use only.
        internal Vector3 InternalAppliedWind = Vector3.zero; 
        private float DisableMultiplier = 1.0f;

        private double temperature = PhysicsGlobals.SpaceTemperature;
        internal double Temperature
        {
            get => temperature;
            private set => temperature = UtilMath.Clamp(value, PhysicsGlobals.SpaceTemperature, float.MaxValue);
        }

        private double pressure = 0.0;
        internal double Pressure
        {
            get => pressure;
            private set => pressure = UtilMath.Clamp(value, 0.0, float.MaxValue);
        }

        //stuff for wind speed variability
        private Random varywind;
        private double vary1 = 1.0;
        private double vary2 = 1.0;
        private const double varyinterval = 100d;
        private double timeofnextvary = 0d;

        void Awake()
        {
            if (Instance == null) //prevent multiple FlightHandler instances from running.
            {
                Utils.LogInfo("Initializing Flight Handler.");
                Instance = this;

                if (Settings.FAR_Exists)
                {
                    Utils.LogInfo("Registering MCWS with FerramAerospaceResearch.");
                    RegisterWithFAR();
                }

                //set up wind variability. seed the variability generator based on current time.
                varywind = new Random((DateTime.Now.Hour * 10000) + (DateTime.Now.Minute * 100) + DateTime.Now.Second);
                vary2 = varywind.NextDouble();
            }
            else
            {
                Utils.LogWarning("Destroying duplicate Flight Handler. Check your install for duplicate mod folders.");
                DestroyImmediate(this);
            }
        }

        void FixedUpdate()
        {
            Settings.CheckGameSettings();
            normalwind.Zero();
            transformedwind.Zero();
            RawWind.Zero();
            AppliedWind.Zero();
            InternalAppliedWind.Zero();

            if (!FlightGlobals.ready || FlightGlobals.ActiveVessel == null)
            {
                activevessel = null;
                return;
            }
            activevessel = FlightGlobals.ActiveVessel;
            double lon = activevessel.longitude;
            double lat = activevessel.latitude;
            double alt = activevessel.altitude;
            mainbody = activevessel.mainBody;

            //Get the worldframe of the vessel in question to transform the wind vectors to the global coordinate frame.
            Vesselframe.SetColumn(0, (Vector3)activevessel.north);
            Vesselframe.SetColumn(1, (Vector3)activevessel.upAxis);
            Vesselframe.SetColumn(2, (Vector3)activevessel.east);

            if (TimeWarp.CurrentRate <= 1.0f && CurrentTime > timeofnextvary) //pause fetching of new data when timewarp is active.
            {
                timeofnextvary = CurrentTime + varyinterval;
                vary1 = vary2;
                vary2 = varywind.NextDouble();
            }

            //pre-calculate a few factors to be used
            float VaryFactor = (float)(1.0 + UtilMath.Lerp(-Settings.WindSpeedVariability, Settings.WindSpeedVariability, UtilMath.Lerp(vary1, vary2, (CurrentTime - timeofnextvary) / varyinterval)));
            DisableMultiplier = activevessel != null && activevessel.LandedOrSplashed && Settings.DisableWindWhenStationary ? (float)UtilMath.Lerp(0.0, 1.0, (activevessel.srfSpeed - 5.0) * 0.2) : 1.0f;

            if (mainbody.atmosphere && alt <= mainbody.atmosphereDepth)
            {
                //set fallback data
                FlightIntegrator FI = activevessel.GetComponent<FlightIntegrator>();
                Temperature = FI != null ? mainbody.GetFullTemperature(alt, FI.atmosphereTemperatureOffset) : mainbody.GetTemperature(alt);
                Pressure = mainbody.GetPressure(alt);
                HasWind = HasTemp = HasPress = false;

                int extwindcode = MCWS_API.GetExternalWind(mainbody.name, lon, lat, alt, CurrentTime, out Vector3 extwind);
                if (extwindcode == 0)
                {
                    normalwind.Set(extwind);
                    normalwind.MultiplyByConstant(VaryFactor);
                    transformedwind = Vesselframe * normalwind;

                    RawWind.Set(normalwind);
                    RawWind.MultiplyByConstant(Settings.GlobalWindSpeedMultiplier);
                    AppliedWind = Vesselframe * RawWind;

                    if (activevessel.easingInToSurface)
                    {
                        InternalAppliedWind.Zero();
                    }
                    else
                    {
                        InternalAppliedWind.Set(AppliedWind);
                        InternalAppliedWind.MultiplyByConstant(DisableMultiplier);
                    }

                    HasWind = true;
                }
                else if (Data.HasWind(mainbody.name))
                {
                    try
                    {
                        int retcode = Data.GetWind(mainbody.name, lon, lat, alt, CurrentTime, out Vector3 Final);
                        switch (retcode)
                        {
                            case -2:
                                Utils.LogError("Error when reading wind data for body " + mainbody.name);
                                break;
                            case 0:
                                //add the variability factor to the wind vector, then transform it to the global coordinate frame
                                normalwind.Set(Final);
                                normalwind.MultiplyByConstant(VaryFactor);
                                transformedwind = Vesselframe * normalwind;

                                RawWind.Set(normalwind);
                                RawWind.MultiplyByConstant(Settings.GlobalWindSpeedMultiplier);
                                AppliedWind = Vesselframe * RawWind;

                                if (activevessel.easingInToSurface)
                                {
                                    InternalAppliedWind.Zero();
                                }
                                else
                                {
                                    InternalAppliedWind.Set(AppliedWind);
                                    InternalAppliedWind.MultiplyByConstant(DisableMultiplier);
                                }

                                HasWind = true;
                                break;
                            default:
                                break;
                        }
                    }
                    catch (Exception ex) //fallback data
                    {
                        Utils.LogError("Exception thrown when deriving point Wind data for " + mainbody.name + ": " + ex.ToString());
                        normalwind.Zero();
                        transformedwind.Zero();
                        RawWind.Zero();
                        AppliedWind.Zero();
                        InternalAppliedWind.Zero();
                        HasWind = false;
                    }
                }
                else
                {
                    HasWind = false;
                }

                int exttempcode = MCWS_API.GetExternalTemperature(mainbody.name, lon, lat, alt, CurrentTime, out double exttemp);
                if (exttempcode == 0) 
                {
                    Temperature = exttemp;
                    HasTemp = true;
                }
                else if (Data.HasTemperature(mainbody.name))
                {
                    try
                    {
                        int retcode = Data.GetTemperature(mainbody.name, lon, lat, alt, CurrentTime, out double Final);
                        switch (retcode)
                        {
                            case -2:
                                Utils.LogError("Error when reading temperature data for body " + mainbody.name);
                                break;
                            case 0:
                                Temperature = Final;
                                HasTemp = true;
                                break;
                            case 1:
                                double TempModelTop = Math.Min(Data.TemperatureModelTop(mainbody.name), mainbody.atmosphereDepth);
                                double extralerp = (alt - TempModelTop) / (mainbody.atmosphereDepth - TempModelTop);
                                double realtemp = FI != null ? mainbody.GetFullTemperature(alt, FI.atmosphereTemperatureOffset) : mainbody.GetTemperature(alt);
                                double temp = UtilMath.Lerp(Final, realtemp, Math.Pow(extralerp, 0.25));
                                Temperature = double.IsFinite(temp) ? temp : throw new NotFiniteNumberException();
                                HasTemp = true;
                                break;
                            default:
                                break;
                        }
                    }
                    catch (Exception ex) //fallback data
                    {
                        Utils.LogError("Exception thrown when deriving point Temperature data for " + mainbody.name + ": " + ex.ToString());
                        Temperature = FI != null ? mainbody.GetFullTemperature(alt, FI.atmosphereTemperatureOffset) : mainbody.GetTemperature(alt);
                        HasTemp = false;
                    }
                }
                else
                {
                    HasTemp = false;
                }

                int extpresscode = MCWS_API.GetExternalPressure(mainbody.name, lon, lat, alt, CurrentTime, out double extpress);
                if (extpresscode == 0)
                {
                    Pressure = extpress * 0.001;
                    HasPress = true;
                }
                else if (Data.HasPressure(mainbody.name))
                {
                    try
                    {
                        int retcode = Data.GetPressure(mainbody.name, lon, lat, alt, CurrentTime, out double Final);
                        switch (retcode)
                        {
                            case -2:
                                Utils.LogError("Error when reading pressure data for body " + mainbody.name);
                                break;
                            case 0:
                                Pressure = Final * 0.001;
                                HasPress= true;
                                break;
                            case 1:
                                double PressModelTop = Math.Min(Data.PressureModelTop(mainbody.name), mainbody.atmosphereDepth);
                                double extralerp = (alt - PressModelTop) / (mainbody.atmosphereDepth - PressModelTop);
                                double press0 = mainbody.GetPressure(0);
                                double press1 = mainbody.GetPressure(PressModelTop);
                                double scaleheight = PressModelTop / Math.Log(press0 / press1, Math.E);
                                double press = UtilMath.Lerp(Final * Math.Pow(Math.E, -((alt - PressModelTop) / scaleheight)), mainbody.GetPressure(alt) * 1000, Math.Pow(extralerp, 0.125)) * 0.001;
                                Pressure = double.IsFinite(press) ? press : throw new NotFiniteNumberException();
                                HasPress = true;
                                break;
                            default:
                                break;
                        }
                    }
                    catch (Exception ex) //fallback data
                    {
                        Utils.LogError("Exception thrown when deriving point Pressure data for " + mainbody.name + ": " + ex.ToString());
                        Pressure = mainbody.GetPressure(alt);
                        HasPress = false;
                    }
                }
            }
            else
            {
                Pressure = 0.0;
                Temperature = PhysicsGlobals.SpaceTemperature;
                HasWind = HasTemp = HasPress = false;
            }
        }

        void OnDestroy()
        {
            Utils.LogInfo("Flight Scene has ended. Unloading Flight Handler.");
            RemoveToolbarButton();
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(RemoveToolbarButton);
            activevessel = null;
            mainbody = null;
            varywind = null;
            if (Instance == this)
            {
                Instance = null;
            }
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
