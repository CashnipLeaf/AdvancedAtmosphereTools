using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularClimateWeatherSystems
{
    using GlobalWindDelegate = Func<string, double, double, double, double, Vector3>; //body, lon, lat, alt, time, Vector3 (return value)
    using GlobalPropertyDelegate = Func<string, double, double, double, double, double>; //body, lon, lat, alt, time, double (return value)
    
    //API for interfacing with this mod.
    public static class MCWS_API
    {
        private static Dictionary<string, ExternalBodyData> externalbodydata;

        //--------------------------REGISTER EXTERNAL DATA--------------------------
        public static bool RegisterWindData(string body, GlobalWindDelegate dlg, string name)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(body) || dlg == null)
            {
                throw new ArgumentNullException("One or more arguments was null or empty.");
            }
            CheckRegistration(body);
            ToRegister("Wind", name, body);
            if (!externalbodydata[body].HasWind)
            {
                externalbodydata[body].SetWindFunc(name, dlg);
                SuccessfulRegistration("Wind", name, body);
                return true;
            }
            CannotRegister("Wind", name, body);
            return false;
        }

        public static bool RegisterTemperatureData(string body, GlobalPropertyDelegate dlg, string name)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(body) || dlg == null)
            {
                throw new ArgumentNullException("One or more arguments was null or empty.");
            }
            CheckRegistration(body);
            ToRegister("Temperature", name, body);
            if (!externalbodydata[body].HasTemperature)
            {
                externalbodydata[body].SetTemperatureFunc(name, dlg);
                SuccessfulRegistration("Temperature", name, body);
                return true;
            }
            CannotRegister("Temperature", name, body);
            return false;
        }

        public static bool RegisterPressureData(string body, GlobalPropertyDelegate dlg, string name)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(body) || dlg == null)
            {
                throw new ArgumentNullException("One or more arguments was null or empty.");
            }
            CheckRegistration(body);
            ToRegister("Pressure", name, body);
            if (!externalbodydata[body].HasPressure)
            {
                externalbodydata[body].SetPressureFunc(name, dlg);
                SuccessfulRegistration("Pressure", name, body);
                return true;
            }
            CannotRegister("Pressure", name, body);
            return false;
        }

        //-------------FETCH EXTERNAL DATA-------------
        internal static int GetExternalWind(string body, double lon, double lat, double alt, double time, out Vector3 windvec)
        {
            windvec = Vector3.zero;
            if (BodyExists(body) && externalbodydata[body].HasWind)
            {
                try
                {
                    return externalbodydata[body].GetWind(lon, lat, alt, time, out windvec);
                }
                catch
                {
                    return -2;
                }
            }
            return -1;
        }

        internal static int GetExternalTemperature(string body, double lon, double lat, double alt, double time, out double temp)
        {
            temp = 0.0;
            if (BodyExists(body) && externalbodydata[body].HasTemperature)
            {
                try
                {
                    return externalbodydata[body].GetTemperature(lon, lat, alt, time, out temp);
                }
                catch
                {
                    return -2;
                }
            }
            return -1;
        }

        internal static int GetExternalPressure(string body, double lon, double lat, double alt, double time, out double press)
        {
            press = 0.0;
            if (BodyExists(body) && externalbodydata[body].HasPressure)
            {
                try
                {
                    return externalbodydata[body].GetPressure(lon, lat, alt, time, out press);
                }
                catch 
                { 
                    return -2; 
                }
            }
            return -1;
        }


        //-------------BODY DATA CLASS------------------
        internal class ExternalBodyData
        {
            internal string Body;

            private string windsource;
            internal string WindSource => string.IsNullOrEmpty(windsource) ? "None" : windsource;
            internal GlobalWindDelegate windfunc;

            private string tempsource;
            internal string TemperatureSource => string.IsNullOrEmpty(tempsource) ? "None" : tempsource;
            private GlobalPropertyDelegate temper;

            private string presssource;
            internal string PressureSource => string.IsNullOrEmpty(presssource) ? "None" : presssource;
            private GlobalPropertyDelegate pressure;

            internal ExternalBodyData(string bodyname) => Body = bodyname;

            internal void SetWindFunc(string name, GlobalWindDelegate dlg)
            {
                windsource = name;
                windfunc = dlg;
            }
            internal void SetTemperatureFunc(string name, GlobalPropertyDelegate del)
            {
                tempsource = name;
                temper = del;
            }
            internal void SetPressureFunc(string name, GlobalPropertyDelegate del)
            {
                presssource = name;
                pressure = del;
            }

            internal bool HasWind => !string.IsNullOrEmpty(windsource) && windfunc != null;
            internal int GetWind(double lon, double lat, double alt, double time, out Vector3 windvec)
            {
                windvec = Vector3.zero;
                if (HasWind)
                {
                    windvec = windfunc.Invoke(Body, lon, lat, alt, time);
                    return windvec.IsFinite() ? 0 : -2;
                }
                return -1;
            }

            internal bool HasTemperature => !string.IsNullOrEmpty(tempsource) && temper != null;
            internal int GetTemperature(double lon, double lat, double alt, double time, out double temp)
            {
                temp = 0.0;
                if (HasTemperature)
                {
                    temp = temper.Invoke(Body, lon, lat, alt, time);
                    return double.IsFinite(temp) ? 0 : -2;
                }
                return -1;
            }

            internal bool HasPressure => !string.IsNullOrEmpty(presssource) && pressure != null;
            internal int GetPressure(double lon, double lat, double alt, double time, out double press)
            {
                press = 0.0;
                if (HasTemperature)
                {   
                    press = pressure.Invoke(Body, lon, lat, alt, time);
                    return double.IsFinite(press) ? 0 : -2;
                }
                return -1;
            }
        }
        internal static bool BodyExists(string body) => externalbodydata != null && externalbodydata.ContainsKey(body);

        //-------------GET DATA FROM MCWS----------------

        //Data in use by the flighthandler
        private static MCWS_FlightHandler Instance => MCWS_FlightHandler.Instance;
        private static bool CanGetData => HighLogic.LoadedSceneIsFlight && Instance != null;
        private const string NotFlightScene = "Cannot access Flight Handler data outside of the Flight scene.";
        public static Vector3 GetCurrentWindVec() => CanGetData ? Instance.transformedwind : throw new InvalidOperationException(NotFlightScene); // wind vector in the global coordinate frame
        public static Vector3 GetRawWindVec() => CanGetData ? Instance.normalwind : throw new InvalidOperationException(NotFlightScene); //wind vector in the local coordinate frame
        public static double GetCurrentTemperature() => CanGetData ? Instance.Temperature : throw new InvalidOperationException(NotFlightScene);
        public static double GetCurrentPressure() => CanGetData ? Instance.Pressure * 1000 : throw new InvalidOperationException(NotFlightScene); //convert back to Pa

        //Get Global Data for any body at any time. No interpolation is performed on MCWS's end here, so make sure you can deal with these values.
        private static MCWS_Startup InternalData => MCWS_Startup.Instance;

        public static float[][,,] GetGlobalWindData(string body, double time)
        {
            CheckInputs(body, time);
            try
            {
                CelestialBody bod = FlightGlobals.GetBodyByName(body);
                if(bod != null && bod.atmosphere)
                {
                    return InternalData.WindData(body, time);
                } 
            }
            catch (Exception ex)
            {
                Utils.LogAPIWarning("An Exception occurred when retrieving global wind data. A null array was returned as a failsafe. Exception thrown: " + ex.ToString());
            }
            return null;
        }
        public static float[,,] GetGlobalTemperatureData(string body, double time)
        {
            CheckInputs(body, time);
            try
            {
                CelestialBody bod = FlightGlobals.GetBodyByName(body);
                if(bod != null && bod.atmosphere)
                {
                    return InternalData.TemperatureData(body, time);
                }
            }
            catch (Exception ex)
            {
                Utils.LogAPIWarning("An Exception occurred when retrieving global temperature data. A null array was returned as a failsafe. Exception thrown: " + ex.ToString());
            }
            return null;
        }
        public static float[,,] GetGlobalPressureData(string body, double time)
        {
            CheckInputs(body, time);
            try
            {
                CelestialBody bod = FlightGlobals.GetBodyByName(body);
                if(bod != null && bod.atmosphere)
                {
                    return InternalData.PressureData(body, time);
                }  
            }
            catch (Exception ex) 
            { 
                Utils.LogAPIWarning("An Exception occurred when retrieving global pressure data. A null array was returned as a failsafe. Exception thrown: " + ex.ToString()); 
            }
            return null;
        }
        
        //Get Point Data
        public static Vector3 GetPointWindData(string body, double lon, double lat, double alt, double time)
        {
            CheckPosition(lon, lat, alt);
            CelestialBody bod = FlightGlobals.GetBodyByName(body);
            try
            {
                if (bod != null && bod.atmosphere && alt <= bod.atmosphereDepth)
                {
                    int extretcode = GetExternalWind(body, lon, lat, alt, time, out Vector3 extwind);
                    if (extretcode == 0)
                    {
                        return extwind;
                    }
                    int retcode = InternalData.GetWind(body, lon, lat, alt, time, out Vector3 datavec, out Vector3 flowmapvec, out DataInfo garbage); //dont need datainfo, get rid of it
                    switch (retcode)
                    {
                        case 0:
                            return datavec + flowmapvec;
                        case 1:
                            return datavec;
                        case 2:
                            return flowmapvec;
                        default:
                            return Vector3.zero;
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.LogAPIWarning("An Exception occurred when retrieving point wind data. A zero vector was returned as a failsafe. Exception thrown: " + ex.ToString());
            }
            return Vector3.zero;
        }

        public static double GetPointTemperatureData(string body, double lon, double lat, double alt, double time)
        {
            CheckPosition(lon, lat, alt);
            CelestialBody bod = FlightGlobals.GetBodyByName(body);
            bool validbody = bod != null && bod.atmosphere && alt <= bod.atmosphereDepth;
            try
            {
                int extretcode = GetExternalTemperature(body, lon, lat, alt, time, out double exttemp);
                if (extretcode == 0)
                {
                    return exttemp;
                }
                int retcode = InternalData.GetTemperature(body, lon, lat, alt, time, out double temp, out DataInfo garbage); //dont need datainfo, get rid of it
                switch (retcode)
                {
                    case 0:
                        return temp;
                    case 1:
                        double TempModelTop = InternalData.TemperatureModelTop(body);
                        double extralerp = (alt - TempModelTop) / (bod.atmosphereDepth - TempModelTop);
                        double realtemp = bod.GetTemperature(alt);
                        double newtemp = UtilMath.Lerp(temp, realtemp, Math.Pow(extralerp, 0.25));
                        return double.IsFinite(newtemp) ? newtemp : throw new NotFiniteNumberException();
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Utils.LogAPIWarning("An Exception occurred when retrieving point wind data. Stock temperature data was returned as a failsafe. Exception thrown: " + ex.ToString());
            }
            return validbody ? bod.GetTemperature(alt) : PhysicsGlobals.SpaceTemperature;
        }

        public static double GetPointPressureData(string body, double lon, double lat, double alt, double time)
        {
            CheckPosition(lon, lat, alt);
            CelestialBody bod = FlightGlobals.GetBodyByName(body);
            bool validbody = bod != null && bod.atmosphere && alt <= bod.atmosphereDepth;
            try
            {
                int extretcode = GetExternalPressure(body, lon, lat, alt, time, out double extpress);
                if (extretcode == 0)
                {
                    return extpress;
                }
                int retcode = InternalData.GetPressure(body, lon, lat, alt, time, out double press, out DataInfo garbage); //dont need datainfo, get rid of it
                switch (retcode)
                {
                    case 0:
                        return press;
                    case 1:
                        double PressModelTop = InternalData.PressureModelTop(body);
                        double extralerp = (alt - PressModelTop) / (bod.atmosphereDepth - PressModelTop);
                        double press0 = bod.GetPressure(0);
                        double press1 = bod.GetPressure(PressModelTop);
                        double scaleheight = PressModelTop / Math.Log(press0 / press1, Math.E);
                        double newpress = UtilMath.Lerp(press * Math.Pow(Math.E, -((alt - PressModelTop) / scaleheight)), bod.GetPressure(alt) * 1000, Math.Pow(extralerp, 0.125));
                        return double.IsFinite(newpress) ? newpress : throw new NotFiniteNumberException();
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Utils.LogAPIWarning("An Exception occurred when retrieving point wind data. Stock pressure data was returned as a failsafe. Exception thrown: " + ex.ToString());
            }
            return validbody ? bod.GetPressure(alt) : 0.0;
        }

        //-------------HELPER FUNCTIONS AND VALUES-----------------

        private static void CheckRegistration(string body)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                throw new InvalidOperationException("Cannot register data with MCWS during the Flight scene.");
            }
            if (externalbodydata == null)
            {
                externalbodydata = new Dictionary<string, ExternalBodyData>();
            }
            if (!externalbodydata.ContainsKey(body))
            {
                externalbodydata.Add(body, new ExternalBodyData(body));
            }
        }
        private static void CheckInputs(string body, double time)
        {
            if (string.IsNullOrEmpty(body))
            {
                throw new ArgumentNullException("Argument 'body' was null or empty.");
            }
            if (!double.IsFinite(time) || time < 0.0)
            {
                throw new ArgumentOutOfRangeException("Time argument was non-finite or less than 0.0.");
            }
        }
        private static void CheckPosition(double lon, double lat, double alt)
        {
            if (!double.IsFinite(alt))
            {
                throw new ArgumentOutOfRangeException("Altitude argument was non-finite.");
            }
            if (!double.IsFinite(lon) || lon > 180.0 || lon < -180.0)
            {
                throw new ArgumentOutOfRangeException("Longitude argument was non-finite or outside the range of actual longitude values.");
            }
            if (!double.IsFinite(lat) || lat > 90.0 || lat < -90.0)
            {
                throw new ArgumentOutOfRangeException("Latitude argument was non-finite or outside the range of actual latitude values.");
            }
        }

        private static void ToRegister(string type, string name, string body) => Utils.LogAPI(string.Format("Registering '{0}' as a {1} data source for {2}.", name, type, body));
        private static void SuccessfulRegistration(string type, string name, string body) => Utils.LogAPI(string.Format("Successfully registered '{0}' as a {1} data source for {2}.", name, type, body));
        private static void CannotRegister(string type, string name, string body) => Utils.LogAPIWarning(string.Format("Could not register '{0}' as a {1} data source for {2}. Another plugin has already registered.", name, type, body));
    }
}
