using System;
using System.Reflection;
using UnityEngine;

namespace ModularClimateWeatherSystems
{
    using Random = System.Random; //there apparently exists a UnityEngine.Random, which is not what I want or need.

    //Delegates for FAR
    using WindDelegate = Func<CelestialBody, Part, Vector3, Vector3>;
    using PropertyDelegate = Func<CelestialBody, Vector3d, double, double>;

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    partial class MCWS_FlightHandler : MonoBehaviour //This is a partial class because I do not feel like passing variables to a separate GUI class.
    {
        public static MCWS_FlightHandler Instance { get; private set; }

        #region variables
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

        private Vector3 datawind = Vector3.zero;
        private Vector3 flowmapwind = Vector3.zero;

        private bool haswinddata = false;
        private bool hasflowmaps = false;

        private double temperature = PhysicsGlobals.SpaceTemperature;
        internal double Temperature
        {
            get => temperature;
            private set => temperature = UtilMath.Clamp(value, PhysicsGlobals.SpaceTemperature, float.MaxValue);
        }
        private double stocktemperature = PhysicsGlobals.SpaceTemperature;
        private double derivedtemp = PhysicsGlobals.SpaceTemperature;
        private double maptemperature = PhysicsGlobals.SpaceTemperature;
        private bool usinginternaltemperature = false;

        private double pressure = 0.0;
        internal double Pressure
        {
            get => pressure;
            private set => pressure = UtilMath.Clamp(value, 0.0, float.MaxValue);
        }
        private double stockpressure = 0.0;
        private double derivedpressure = 0.0;
        private double mappressure = 0.0;
        private bool usinginternalpressure = false;

        private double pressuremultiplier = 1.0;
        internal double FIPressureMultiplier
        {
            get => HasPress ? pressuremultiplier : 1.0;
            set => pressuremultiplier = double.IsFinite(value) ? value : 1.0;
        }

        private DataInfo WindDataInfo = DataInfo.Zero;
        private DataInfo TemperatureDataInfo = DataInfo.Zero;
        private DataInfo PressureDataInfo = DataInfo.Zero;

        //stuff for wind speed variability
        private Random varywind;
        private double varyonex = 0.0;
        private double varyoney = 0.0;
        private double varyonez = 0.0;
        private double varytwox = 0.0;
        private double varytwoy = 0.0;
        private double varytwoz = 0.0;
        private const double varyinterval = 60d;
        private double timeofnextvary = 0d;
        #endregion

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
                varytwox = varywind.NextDouble();
                varytwoy = varywind.NextDouble();
                varytwoz = varywind.NextDouble();
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
            datawind.Zero();
            flowmapwind.Zero();

            WindDataInfo.SetZero();
            TemperatureDataInfo.SetZero();
            PressureDataInfo.SetZero();
            HasWind = HasTemp = HasPress = hasflowmaps = haswinddata = usinginternaltemperature = usinginternalpressure = false;
            FIPressureMultiplier = 1.0;

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

            if (TimeWarp.CurrentRate <= 1.0f && CurrentTime > timeofnextvary) //pause updating the vary shenanigans when timewarp is active
            {
                timeofnextvary = CurrentTime + varyinterval;
                varyonex = varytwox;
                varyoney = varytwoy;
                varyonez = varytwoz;
                varytwox = varywind.NextDouble();
                varytwoy = varywind.NextDouble();
                varytwoz = varywind.NextDouble();
            }

            //pre-calculate a few factors to be used
            float varyx = (float)(1.0 + UtilMath.Lerp(-Settings.WindSpeedVariability, Settings.WindSpeedVariability, UtilMath.Lerp(varyonex, varytwox, (CurrentTime - timeofnextvary) / varyinterval)));
            float varyy = (float)(1.0 + UtilMath.Lerp(-Settings.WindSpeedVariability, Settings.WindSpeedVariability, UtilMath.Lerp(varyoney, varytwoy, (CurrentTime - timeofnextvary) / varyinterval)));
            float varyz = (float)(1.0 + UtilMath.Lerp(-Settings.WindSpeedVariability, Settings.WindSpeedVariability, UtilMath.Lerp(varyonez, varytwoz, (CurrentTime - timeofnextvary) / varyinterval)));

            DisableMultiplier = activevessel != null && activevessel.LandedOrSplashed && Settings.DisableWindWhenStationary ? (float)UtilMath.Lerp(0.0, 1.0, (activevessel.srfSpeed - 5.0) * 0.2) : 1.0f;

            if (mainbody.atmosphere && alt <= mainbody.atmosphereDepth)
            {
                //set fallback data
                FlightIntegrator FI = activevessel.GetComponent<FlightIntegrator>();
                double temperatureoffset = FI != null ? FI.atmosphereTemperatureOffset : 0.0;
                Temperature = stocktemperature = derivedtemp = maptemperature = FI != null ? mainbody.GetFullTemperature(alt, temperatureoffset) : mainbody.GetTemperature(alt);
                Pressure = derivedpressure = mappressure = stockpressure = mainbody.GetPressure(alt);

                int extwindcode = MCWS_API.GetExternalWind(mainbody.name, lon, lat, Math.Max(alt, 0.0), CurrentTime, out Vector3 extwind);
                if (extwindcode == 0)
                {
                    normalwind.Set(extwind);
                    HasWind = true;
                }
                else if (Data.HasWind(mainbody.name))
                {
                    try
                    {
                        int retcode = Data.GetWind(mainbody.name, lon, lat, Math.Max(alt, 0.0), CurrentTime, out Vector3 datavec, out Vector3 flowmapvec, out DataInfo winfo);
                        if (retcode >= 0)
                        {
                            if (retcode == 2)
                            {
                                datawind.Zero();
                                flowmapwind.Set(flowmapvec);
                                normalwind.Set(flowmapvec);
                                hasflowmaps = true;
                            }
                            else if (retcode == 1)
                            {
                                flowmapwind.Zero();
                                datawind.Set(datavec);
                                normalwind.Set(datawind);
                                haswinddata = true;
                                WindDataInfo.SetNew(winfo);
                            }
                            else
                            {
                                datawind.Set(datavec);
                                flowmapwind.Set(flowmapvec);
                                normalwind = datawind + flowmapwind;
                                haswinddata = hasflowmaps = true;
                                WindDataInfo.SetNew(winfo);
                            }

                            HasWind = true;
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
                        datawind.Zero();
                        flowmapwind.Zero();
                        WindDataInfo.SetZero();
                        HasWind = haswinddata = hasflowmaps = false;
                    }
                }
                else
                {
                    HasWind = hasflowmaps = haswinddata = false;
                }

                int exttempcode = MCWS_API.GetExternalTemperature(mainbody.name, lon, lat, Math.Max(alt, 0.0), CurrentTime, out double exttemp);
                if (exttempcode == 0) 
                {
                    Temperature = exttemp;
                    HasTemp = true;
                    usinginternaltemperature = false;
                }
                else if (Data.HasTemperature(mainbody.name))
                {
                    try
                    {
                        int tempretcode = Data.GetTemperature(mainbody.name, lon, lat, Math.Max(alt, 0.0), CurrentTime, out double Final, out DataInfo tinfo);
                        bool tempdatagood = false;
                        switch (tempretcode)
                        {
                            case -2:
                                Utils.LogError("Error when reading temperature data for body " + mainbody.name);
                                break;
                            case 0:
                                derivedtemp = Final;
                                tempdatagood = true;
                                TemperatureDataInfo.SetNew(tinfo);
                                break;
                            case 1:
                                double TempModelTop = Math.Min(Data.TemperatureModelTop(mainbody.name), mainbody.atmosphereDepth);
                                double extralerp = (alt - TempModelTop) / (mainbody.atmosphereDepth - TempModelTop);
                                double temp = UtilMath.Lerp(Final, stocktemperature, Math.Pow(extralerp, 0.25));
                                if (double.IsFinite(temp))
                                {
                                    derivedtemp = double.IsFinite(temp) ? temp : stocktemperature;
                                    tempdatagood = true;
                                    TemperatureDataInfo.SetNew(tinfo);
                                }
                                break;
                            default:
                                break;
                        }
                        if (!tempdatagood && tempretcode >= 0)
                        {
                            tempretcode = -1;
                        }

                        int tempmapretcode = Data.GetTemperatureMapData(mainbody.name, lon, lat, Math.Max(alt, 0.0), CurrentTime, out double tempoffset, out double tempswingmult);
                        if (tempmapretcode >= 0)
                        {
                            double latbias = mainbody.latitudeTemperatureBiasCurve.Evaluate((float)Math.Abs(lat));
                            double offsetnolatbias = temperatureoffset - latbias;
                            double newoffset = offsetnolatbias * tempswingmult;
                            switch (tempmapretcode)
                            {
                                case 1:
                                    maptemperature = stocktemperature + tempoffset;
                                    break;
                                case 2:
                                    maptemperature = mainbody.GetFullTemperature(alt, newoffset + latbias);
                                    break;
                                default:
                                    maptemperature = mainbody.GetFullTemperature(alt, newoffset + latbias) + tempoffset;
                                    break;
                            }
                            if (!double.IsFinite(maptemperature))
                            {
                                tempmapretcode = -1;
                            }
                        }

                        bool blendtemp = Data.BlendTemperature(mainbody.name, out double tempblendfactor);
                        if (tempretcode >= 0 && tempmapretcode >= 0)
                        {
                            Temperature = UtilMath.Lerp(derivedtemp, maptemperature, tempblendfactor);
                            HasTemp = usinginternaltemperature = true;
                        }
                        else if (tempretcode >= 0 && tempmapretcode < 0)
                        {
                            Temperature = blendtemp ? UtilMath.Lerp(derivedtemp, stocktemperature, tempblendfactor) : derivedtemp;
                            HasTemp = usinginternaltemperature = true;
                        }
                        else if (tempretcode < 0 && tempmapretcode >= 0)
                        {
                            Temperature = maptemperature;
                            HasTemp = true;
                            usinginternaltemperature = false;
                        }
                        else
                        {
                            Temperature = derivedtemp;
                            HasTemp = usinginternaltemperature = false;
                        }
                    }
                    catch (Exception ex) //fallback data
                    {
                        Utils.LogError("Exception thrown when deriving point Temperature data for " + mainbody.name + ": " + ex.ToString());
                        Temperature = stocktemperature;
                        HasTemp = usinginternaltemperature = false;
                    }
                }
                else
                {
                    Temperature = stocktemperature;
                    HasTemp = usinginternaltemperature = false;
                }

                int extpresscode = MCWS_API.GetExternalPressure(mainbody.name, lon, lat, Math.Max(alt, 0.0), CurrentTime, out double extpress);
                if (extpresscode == 0)
                {
                    Pressure = extpress * 0.001;
                    HasPress = true;
                    usinginternalpressure = false;
                    FIPressureMultiplier = Pressure / stockpressure;
                }
                else if (Data.HasPressure(mainbody.name))
                {
                    try
                    {
                        int pressretcode = Data.GetPressure(mainbody.name, lon, lat, Math.Max(alt, 0.0), CurrentTime, out double Final, out DataInfo pinfo);
                        bool pressdatagood = false;
                        switch (pressretcode)
                        {
                            case -2:
                                Utils.LogError("Error when reading pressure data for body " + mainbody.name);
                                break;
                            case 0:
                                derivedpressure = Final * 0.001;
                                pressdatagood = true;
                                PressureDataInfo.SetNew(pinfo);
                                break;
                            case 1:
                                double PressModelTop = Math.Min(Data.PressureModelTop(mainbody.name), mainbody.atmosphereDepth);
                                double extralerp = (alt - PressModelTop) / (mainbody.atmosphereDepth - PressModelTop);
                                double press0 = mainbody.GetPressure(0);
                                double press1 = mainbody.GetPressure(PressModelTop);
                                double scaleheight = PressModelTop / Math.Log(press0 / press1, Math.E);
                                double press = UtilMath.Lerp(Final * Math.Pow(Math.E, -((alt - PressModelTop) / scaleheight)), mainbody.GetPressure(alt) * 1000, Math.Pow(extralerp, 0.125)) * 0.001;
                                if (double.IsFinite(press))
                                {
                                    derivedpressure = press;
                                    pressdatagood = true;
                                    PressureDataInfo.SetNew(pinfo);
                                }
                                break;
                            default:
                                break;
                        }
                        if (!pressdatagood && pressretcode >= 0)
                        {
                            pressretcode = -1;
                        }

                        int pressmapretcode = Data.GetPressureMapData(mainbody.name, lon, lat, Math.Max(alt, 0.0), CurrentTime, out double pressmult);
                        if(pressmapretcode >= 0)
                        {
                            mappressure = stockpressure * pressmult;
                        }

                        bool blendpress = Data.BlendPressure(mainbody.name, out double pressblendfactor);
                        if (pressretcode >= 0 && pressmapretcode == 0)
                        {
                            Pressure = UtilMath.Lerp(derivedpressure, mappressure, pressblendfactor);
                            FIPressureMultiplier = Pressure / stockpressure;
                            HasPress = usinginternalpressure = true;
                        }
                        else if (pressretcode >= 0 && pressmapretcode != 0)
                        {
                            Pressure = blendpress ? UtilMath.Lerp(derivedpressure, stockpressure, pressblendfactor) : derivedpressure;
                            FIPressureMultiplier = Pressure / stockpressure;
                            HasPress = usinginternalpressure = true;
                        }
                        else if (pressretcode < 0 && pressmapretcode == 0)
                        {
                            Pressure = mappressure;
                            FIPressureMultiplier = pressmult;
                            HasPress = true;
                            usinginternalpressure = false;
                        }
                        else
                        {
                            Pressure = stockpressure;
                            FIPressureMultiplier = 1.0;
                            HasPress = usinginternalpressure = false;
                        }
                        
                    }
                    catch (Exception ex) //fallback data
                    {
                        Utils.LogError("Exception thrown when deriving point Pressure data for " + mainbody.name + ": " + ex.ToString());
                        Pressure = stockpressure;
                        HasPress = usinginternalpressure = false;
                    }
                }
                else
                {
                    Pressure = stockpressure;
                    HasPress = usinginternalpressure = false;
                }

                //post-processing through API
                Vector3 windbackup = normalwind;
                double tempbackup = Temperature;
                double pressbackup = Pressure;

                int postretcode = MCWS_API.PostProcess(ref windbackup, ref tempbackup, ref pressbackup, mainbody.name, lon, lat, alt, CurrentTime);
                if (postretcode == 0)
                {
                    if (windbackup.IsFinite() && !windbackup.IsZero())
                    {
                        normalwind.Set(windbackup);
                    }
                    if (double.IsFinite(tempbackup))
                    {
                        Temperature = tempbackup;
                    }
                    if (double.IsFinite(pressbackup))
                    {
                        Pressure = pressbackup;
                    }
                }

                //do other shenanigans with the wind
                if (!Settings.debugmode)
                {
                    normalwind.x *= varyx;
                    normalwind.y *= varyy;
                    normalwind.z *= varyz;
                }
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
            }
            else
            {
                Pressure = 0.0;
                Temperature = PhysicsGlobals.SpaceTemperature;
                HasWind = HasTemp = HasPress = hasflowmaps = haswinddata = usinginternalpressure = usinginternaltemperature = false;
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

        #region FAR
        //----------------FerramAerospaceResearch Compatibility--------------

        //Functions for FAR to call
        internal Vector3 GetTheWind(CelestialBody body, Part p, Vector3 pos) => InternalAppliedWind;
        internal double GetTheTemperature(CelestialBody body, Vector3d pos, double time) => Temperature;
        internal double GetThePressure(CelestialBody body, Vector3d pos, double time) => Pressure;

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
        #endregion
    }
}
