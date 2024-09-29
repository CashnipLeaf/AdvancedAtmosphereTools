using System;
using System.Reflection;
using UnityEngine;
using Random = System.Random; //there apparently exists a UnityEngine.Random, which is not what I want or need.

namespace AdvancedAtmosphereTools
{
    //Delegates for FAR
    using WindDelegate = Func<CelestialBody, Part, Vector3, Vector3>;
    using PropertyDelegate = Func<CelestialBody, Vector3d, double, double>;

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    partial class AAT_FlightHandler : MonoBehaviour //This is a partial class because I do not feel like passing variables to a separate GUI class.
    {
        public static AAT_FlightHandler Instance { get; private set; }

        #region variables
        private Matrix4x4 Vesselframe = Matrix4x4.identity;
        internal static AAT_Startup Data => AAT_Startup.Instance;

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

        private double temperature = 0.0;
        internal double Temperature
        {
            get => temperature;
            private set => temperature = UtilMath.Clamp(value, 0.0, float.MaxValue);
        }
        private double stocktemperature = 0.0;
        private double derivedtemp = 0.0;
        private double maptemperature = 0.0;
        private double tempswingmult = 1.0;
        private double tempoffset = 0.0;
        private bool hastempoffset = false;
        private bool hastempswingmult = false;
        private bool hastempdata = false;
        private bool hastempmaps = false;

        private double pressure = 0.0;
        internal double Pressure
        {
            get => pressure;
            private set => pressure = UtilMath.Clamp(value, 0.0, float.MaxValue);
        }
        private double stockpressure = 0.0;
        private double derivedpressure = 0.0;
        private double mappressure = 0.0;
        private double pressmult = 1.0;
        private bool haspressmult = false;
        private bool haspressdata = false;

        private double pressuremultiplier = 1.0;
        internal double FIPressureMultiplier
        {
            get => HasPress ? pressuremultiplier : 1.0;
            set => pressuremultiplier = double.IsFinite(value) ? value : 1.0;
        }

        private DataInfo WindDataInfo = DataInfo.Zero;
        private DataInfo TemperatureDataInfo = DataInfo.Zero;
        private DataInfo PressureDataInfo = DataInfo.Zero;

        private double molarmass = 0.0;
        internal double MolarMass
        {
            get => molarmass;
            set => molarmass = Math.Max(value, 0.0);
        }
        private double stockmolarmass = 0.0;
        private double basemolarmass = 0.0;
        private double molarmassoffset = 0.0;
        internal bool HasMolarMass = false;
        private bool hasbasemolarmass = false;
        private bool hasmolarmassoffset = false;

        //have you had enough boilerplate? too bad! have some more!
        private double adiabaticindex = 0.0;
        internal double AdiabaticIndex
        {
            get => adiabaticindex;
            set => adiabaticindex = Math.Max(value, 0.0);
        }
        private double stockadiabaticindex = 0.0;
        private double baseadiabaticindex = 0.0;
        private double adiabaticindexoffset = 0.0;
        internal bool HasAdiabaticIndex = false;
        private bool hasbaseadiabaticindex = false;
        private bool hasadiabaticindexoffset = false;

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
            HasWind = hasflowmaps = haswinddata = HasTemp = hastempdata = hastempmaps = hastempoffset = hastempswingmult = HasPress = haspressdata = haspressmult =  HasMolarMass = hasbasemolarmass = hasmolarmassoffset = HasAdiabaticIndex = hasbaseadiabaticindex = hasadiabaticindexoffset = false;
            FIPressureMultiplier = 1.0;

            if (!FlightGlobals.ready || FlightGlobals.ActiveVessel == null)
            {
                return;
            }
            Vessel Activevessel = FlightGlobals.ActiveVessel;
            double lon = Activevessel.longitude;
            double lat = Activevessel.latitude;
            double alt = Activevessel.altitude;
            CelestialBody mainbody = Activevessel.mainBody;
            string bodyname = mainbody.name;
            double CurrentTime = Planetarium.GetUniversalTime();

            double trueAnomaly;
            try
            {
                CelestialBody starref = Utils.GetLocalPlanet(mainbody);
                trueAnomaly = ((starref.orbit.trueAnomaly * UtilMath.Rad2Deg) + 360.0) % 360.0;
            }
            catch
            {
                trueAnomaly = 0.0;
            }

            //Get the worldframe of the vessel in question to transform the wind vectors to the global coordinate frame.
            Vesselframe.SetColumn(0, (Vector3)Activevessel.north);
            Vesselframe.SetColumn(1, (Vector3)Activevessel.upAxis);
            Vesselframe.SetColumn(2, (Vector3)Activevessel.east);

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

            DisableMultiplier = Activevessel != null && Activevessel.LandedOrSplashed && Settings.DisableWindWhenStationary ? (float)UtilMath.Lerp(0.0, 1.0, (Activevessel.srfSpeed - 5.0) * 0.2) : 1.0f;

            if (mainbody.atmosphere && alt <= mainbody.atmosphereDepth)
            {
                //set fallback data
                FlightIntegrator FI = Activevessel.GetComponent<FlightIntegrator>();
                double temperatureoffset = FI != null ? FI.atmosphereTemperatureOffset : 0.0;
                Temperature = stocktemperature = derivedtemp = maptemperature = FI != null ? mainbody.GetFullTemperature(alt, temperatureoffset) : mainbody.GetTemperature(alt);
                Pressure = derivedpressure = mappressure = stockpressure = mainbody.GetPressure(alt);
                MolarMass = stockmolarmass = basemolarmass = mainbody.atmosphereMolarMass;
                AdiabaticIndex = stockadiabaticindex = baseadiabaticindex = mainbody.atmosphereAdiabaticIndex;
                molarmassoffset = adiabaticindexoffset = 0.0;

                //Yes, I use return codes. They're more useful than you'd think.
                int extwindcode = AAT_API.GetExternalWind(bodyname, lon, lat, Math.Max(alt, 0.0), CurrentTime, out Vector3 extwind);
                if (extwindcode == 0)
                {
                    normalwind.Set(extwind);
                    HasWind = true;
                }
                else if (Data.HasWind(bodyname))
                {
                    try
                    {
                        int retcode = Data.GetWind(bodyname, lon, lat, Math.Max(alt, 0.0), CurrentTime, trueAnomaly, out Vector3 datavec, out Vector3 flowmapvec, out DataInfo winfo);
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

                            if (Activevessel.easingInToSurface)
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
                    }
                    catch (Exception ex) //fallback data
                    {
                        Utils.LogError("Exception thrown when deriving point Wind data for " + mainbody.name + ": " + ex.ToString());
                        normalwind.Zero();
                        HasWind = haswinddata = hasflowmaps = false;
                    }
                }
                else
                {
                    normalwind.Zero();
                    HasWind = hasflowmaps = haswinddata = false;
                }

                int exttempcode = AAT_API.GetExternalTemperature(bodyname, lon, lat, Math.Max(alt, 0.0), CurrentTime, out double exttemp);
                if (exttempcode == 0) 
                {
                    Temperature = exttemp;
                    HasTemp = true;
                    hastempdata = hastempmaps = false;
                }
                else if (Data.HasTemperature(bodyname))
                {
                    try
                    {
                        int tempretcode = Data.GetTemperature(bodyname, lon, lat, Math.Max(alt, 0.0), CurrentTime, out double Final, out DataInfo tinfo);
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
                                double TempModelTop = Math.Min(Data.TemperatureModelTop(bodyname), mainbody.atmosphereDepth);
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

                        int tempmapretcode = Data.GetTemperatureMapData(bodyname, lon, lat, Math.Max(alt, 0.0), CurrentTime, trueAnomaly, out tempoffset, out tempswingmult);
                        if (tempmapretcode >= 0)
                        {
                            Utils.ReverseEngineerTemperatureOffset(mainbody, temperatureoffset, lat, trueAnomaly, out double latbias, out double latsunbias, out double axialbias, out double eccentricitybias);
                            double newoffset = (latsunbias * tempswingmult) + latbias + axialbias + eccentricitybias;
                            switch (tempmapretcode)
                            {
                                case 1:
                                    maptemperature = stocktemperature + tempoffset;
                                    hastempoffset = true;
                                    hastempswingmult = false;
                                    break;
                                case 2:
                                    maptemperature = mainbody.GetFullTemperature(alt, newoffset);
                                    hastempoffset = false;
                                    hastempswingmult = true;
                                    break;
                                default:
                                    maptemperature = mainbody.GetFullTemperature(alt, newoffset) + tempoffset;
                                    hastempoffset = hastempswingmult = true;
                                    break;
                            }
                            if (!double.IsFinite(maptemperature))
                            {
                                tempmapretcode = -1;
                            }
                        }

                        bool blendtemp = Data.BlendTemperature(bodyname, out double tempblendfactor);
                        if (tempretcode >= 0 && tempmapretcode >= 0)
                        {
                            Temperature = UtilMath.Lerp(derivedtemp, maptemperature, tempblendfactor);
                            HasTemp = hastempdata = hastempmaps = true;
                        }
                        else if (tempretcode >= 0 && tempmapretcode < 0)
                        {
                            Temperature = blendtemp ? UtilMath.Lerp(derivedtemp, stocktemperature, tempblendfactor) : derivedtemp;
                            HasTemp = hastempdata = true;
                            hastempmaps = false;
                        }
                        else if (tempretcode < 0 && tempmapretcode >= 0)
                        {
                            Temperature = maptemperature;
                            HasTemp = hastempmaps = true;
                            hastempdata = false;
                        }
                        else
                        {
                            Temperature = derivedtemp;
                            HasTemp = hastempdata = hastempmaps = false;
                        }
                    }
                    catch (Exception ex) //fallback data
                    {
                        Utils.LogError("Exception thrown when deriving point Temperature data for " + mainbody.name + ": " + ex.ToString());
                        Temperature = stocktemperature;
                        HasTemp = hastempdata = hastempmaps = false;
                    }
                }
                else
                {
                    Temperature = stocktemperature;
                    HasTemp = hastempdata = hastempmaps = false;
                }

                int extpresscode = AAT_API.GetExternalPressure(bodyname, lon, lat, Math.Max(alt, 0.0), CurrentTime, out double extpress);
                if (extpresscode == 0)
                {
                    Pressure = extpress * 0.001;
                    HasPress = true;
                    haspressdata = haspressmult = false;
                    FIPressureMultiplier = Pressure / stockpressure;
                }
                else if (Data.HasPressure(bodyname))
                {
                    try
                    {
                        int pressretcode = Data.GetPressure(bodyname, lon, lat, Math.Max(alt, 0.0), CurrentTime, out double Final, out DataInfo pinfo);
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
                                double PressModelTop = Math.Min(Data.PressureModelTop(bodyname), mainbody.atmosphereDepth);
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

                        int pressmapretcode = Data.GetPressureMultiplier(bodyname, lon, lat, Math.Max(alt, 0.0), CurrentTime, trueAnomaly, out pressmult);
                        if (pressmapretcode >= 0)
                        {
                            mappressure = stockpressure * pressmult;
                            haspressmult = true;
                        }
                        else
                        {
                            haspressmult = false;
                        }

                        bool blendpress = Data.BlendPressure(bodyname, out double pressblendfactor);
                        if (pressretcode >= 0 && pressmapretcode >= 0)
                        {
                            Pressure = UtilMath.Lerp(derivedpressure, mappressure, pressblendfactor);
                            FIPressureMultiplier = Pressure / stockpressure;
                            HasPress = haspressdata = haspressmult =  true;
                        }
                        else if (pressretcode >= 0 && pressmapretcode < 0)
                        {
                            Pressure = blendpress ? UtilMath.Lerp(derivedpressure, stockpressure, pressblendfactor) : derivedpressure;
                            FIPressureMultiplier = Pressure / stockpressure;
                            HasPress = haspressdata = true;
                            haspressmult = false;
                        }
                        else if (pressretcode < 0 && pressmapretcode >= 0)
                        {
                            Pressure = mappressure;
                            FIPressureMultiplier = pressmult;
                            HasPress = haspressmult = true;
                            haspressdata = false;
                        }
                        else
                        {
                            Pressure = stockpressure;
                            FIPressureMultiplier = 1.0;
                            HasPress = haspressdata = haspressmult = false;
                        }
                        
                    }
                    catch (Exception ex) //fallback data
                    {
                        Utils.LogError("Exception thrown when deriving point Pressure data for " + mainbody.name + ": " + ex.ToString());
                        Pressure = stockpressure;
                        HasPress = haspressdata = haspressmult = false;
                    }
                }
                else
                {
                    Pressure = stockpressure;
                    HasPress = haspressdata = haspressmult = false;
                }

                int mmretcode = Data.GetMolarMass(bodyname, lon, lat, Math.Max(alt, 0.0), CurrentTime, trueAnomaly, out basemolarmass, out molarmassoffset);
                switch (mmretcode)
                {
                    case 0:
                        MolarMass = basemolarmass + molarmassoffset;
                        HasMolarMass = hasmolarmassoffset = hasbasemolarmass = true;
                        break;
                    case 1:
                        MolarMass = basemolarmass;
                        HasMolarMass = hasbasemolarmass = true;
                        hasmolarmassoffset = false;
                        break;
                    case 2:
                        MolarMass = stockmolarmass + molarmassoffset;
                        HasMolarMass = hasmolarmassoffset = true;
                        hasbasemolarmass = false;
                        break;
                    default:
                        MolarMass = stockmolarmass;
                        HasMolarMass = hasmolarmassoffset = hasbasemolarmass = false;
                        break;
                }

                int adbidxretcode = Data.GetAdiabaticIndex(bodyname, lon, lat, alt, CurrentTime, out baseadiabaticindex, out adiabaticindexoffset);
                switch (adbidxretcode)
                {
                    case 0:
                        AdiabaticIndex = baseadiabaticindex + adiabaticindexoffset;
                        HasAdiabaticIndex = hasbaseadiabaticindex = hasadiabaticindexoffset = true; 
                        break;
                    case 1:
                        AdiabaticIndex = baseadiabaticindex;
                        HasAdiabaticIndex = hasbaseadiabaticindex = true;
                        hasadiabaticindexoffset = false;
                        break;
                    case 2:
                        AdiabaticIndex = stockadiabaticindex + adiabaticindexoffset;
                        HasAdiabaticIndex = hasadiabaticindexoffset = true;
                        hasbaseadiabaticindex = false;
                        break;
                    default:
                        AdiabaticIndex = stockadiabaticindex;
                        HasAdiabaticIndex = hasbaseadiabaticindex = hasadiabaticindexoffset = false;
                        break;
                }
            }
            else
            {
                Temperature = Pressure = MolarMass = AdiabaticIndex = 0.0;
                FIPressureMultiplier = 1.0;
                HasWind = hasflowmaps = haswinddata = HasTemp = hastempdata = hastempmaps = hastempoffset = hastempswingmult = HasPress = haspressdata = haspressmult = HasMolarMass = hasbasemolarmass = hasmolarmassoffset = HasAdiabaticIndex = hasbaseadiabaticindex = hasadiabaticindexoffset = false;
            }
        }

        void OnDestroy()
        {
            Utils.LogInfo("Flight Scene has ended. Unloading Flight Handler.");
            RemoveToolbarButton();
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(RemoveToolbarButton);
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

        internal bool RegisterWithFAR() //Register AdvAtmoTools with FAR.
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
                    var del = Delegate.CreateDelegate(FARWindFunc, this, typeof(AAT_FlightHandler).GetMethod("GetTheWind"), true);
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
