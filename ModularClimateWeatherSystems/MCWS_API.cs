using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularClimateWeatherSystems
{
    using GlobalPropertyDelegate = Func<string, double, float[,,]>; //body, time, global property data (return value)
    
    //API for interfacing with this mod.
    public static class MCWS_API
    {
        private static Dictionary<string, BodyData> externalbodydata;

        //--------------------------REGISTER EXTERNAL DATA--------------------------
        public static bool RegisterTimestepWindData(string body, GlobalPropertyDelegate dlgX, GlobalPropertyDelegate dlgY, GlobalPropertyDelegate dlgZ, string name, double step)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(body) || dlgX == null || dlgY == null || dlgZ == null)
            {
                NullArgs();
            }
            CheckRegistration(body, step);
            if (!externalbodydata[body].HasWind)
            {
                ToRegister("Wind", name, body, step);
                //Sample some data to verify that the returned array is of the correct format
                float[,,] checkdataX = dlgX.Invoke(body, 0.0);
                float[,,] checkdataY = dlgY.Invoke(body, 0.0);
                float[,,] checkdataZ = dlgZ.Invoke(body, 0.0);
                if (checkdataX != null && checkdataY != null && checkdataZ != null)
                {
                    if(!CheckArraySizes(checkdataX, checkdataY, checkdataZ))
                    {
                        throw new DataMisalignedException("Returned Wind data arrays are not of identical dimensions.");
                    }
                    if (checkdataX.GetUpperBound(0) >= 1 && checkdataX.GetUpperBound(1) >= 1 && checkdataX.GetUpperBound(2) >= 1 && checkdataX.GetUpperBound(0) % 2 == 1 && checkdataX.GetUpperBound(1) % 2 == 1)
                    {
                        externalbodydata[body].SetWindFunc(name, dlgX, dlgY,dlgZ, step);
                        SuccessfulRegistration("Wind", name, body);
                        return true;
                    }
                    FormatError("Wind");
                }
                throw new NullReferenceException("One or more of the returned Wind data arrays was null.");
            }
            CannotRegister("Wind", name, body);
            return false;
        }
        public static bool RegisterWindData(string body, GlobalPropertyDelegate dlgx, GlobalPropertyDelegate dlgy, GlobalPropertyDelegate dlgz, string name) => RegisterTimestepWindData(body, dlgx,dlgy,dlgz, name, DEFAULTINTERVAL);

        private static bool CheckArraySizes(float[,,] arr1, float[,,] arr2, float[,,] arr3)
        {
            if(arr1.GetUpperBound(0) == arr2.GetUpperBound(0) && arr1.GetUpperBound(0) == arr3.GetUpperBound(0) && arr2.GetUpperBound(0) == arr3.GetUpperBound(0))
            {
                if (arr1.GetUpperBound(1) == arr2.GetUpperBound(1) && arr1.GetUpperBound(1) == arr3.GetUpperBound(1) && arr2.GetUpperBound(1) == arr3.GetUpperBound(1))
                {
                    if (arr1.GetUpperBound(2) == arr2.GetUpperBound(2) && arr1.GetUpperBound(2) == arr3.GetUpperBound(2) && arr2.GetUpperBound(2) == arr3.GetUpperBound(2))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool RegisterTimestepTemperatureData(string body, GlobalPropertyDelegate dlg, string name, double step)
        {
            if(string.IsNullOrEmpty(name) || string.IsNullOrEmpty(body) || dlg == null)
            {
                NullArgs();
            }
            CheckRegistration(body, step);
            if (!externalbodydata[body].HasTemperature)
            {
                ToRegister("Temperature", name, body, step);
                //Sample some data to verify that the returned array is of the correct format
                float[,,] checkdata = dlg.Invoke(body, 0.0);
                if (checkdata != null)
                {
                    if (checkdata.GetUpperBound(0) >= 1 && checkdata.GetUpperBound(1) >= 1 && checkdata.GetUpperBound(2) >= 1 && checkdata.GetUpperBound(0) % 2 == 1 && checkdata.GetUpperBound(1) % 2 == 1)
                    {
                        externalbodydata[body].SetTemperatureFunc(name, dlg, step);
                        SuccessfulRegistration("Temperature", name, body);
                        return true;
                    }
                    FormatError("Temperature");
                }
                BadArrayError("Temperature");
            }
            CannotRegister("Temperature", name, body);
            return false;
        }
        public static bool RegisterTemperatureData(string body, GlobalPropertyDelegate dlg, string name) => RegisterTimestepTemperatureData(body, dlg, name, DEFAULTINTERVAL);

        public static bool RegisterTimestepPressureData(string body, GlobalPropertyDelegate dlg, string name, double step)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(body) || dlg == null)
            {
                NullArgs();
            }
            CheckRegistration(body, step);
            if (!externalbodydata[body].HasPressure)
            {
                ToRegister("Pressure", name, body, step);
                //Sample some data to verify that the returned array is of the correct format
                float[,,] checkdata = dlg.Invoke(body, 0.0);
                if (checkdata != null)
                {
                    if (checkdata.GetUpperBound(0) >= 1 && checkdata.GetUpperBound(1) >= 1 && checkdata.GetUpperBound(2) >= 1 && checkdata.GetUpperBound(0) % 2 == 1 && checkdata.GetUpperBound(1) % 2 == 1)
                    {
                        externalbodydata[body].SetPressureFunc(name, dlg, step);
                        SuccessfulRegistration("Pressure", name, body);
                        return true;
                    }
                    FormatError("Pressure");
                }
                BadArrayError("Pressure");
            }
            CannotRegister("Pressure", name, body);
            return false;
        }
        public static bool RegisterPressureData(string body, GlobalPropertyDelegate dlg, string name) => RegisterTimestepPressureData(body, dlg, name, DEFAULTINTERVAL);

        //-------------FETCH EXTERNAL DATA-------------
        internal static bool[] HasExternalData(string body)
        {
            bool[] steps = new bool[3] { false, false, false };
            if (BodyExists(body))
            {
                steps[0] = externalbodydata[body].HasWind;
                steps[1] = externalbodydata[body].HasTemperature;
                steps[2] = externalbodydata[body].HasPressure;
            }
            return steps;
        }
        internal static double[] GetTimeSteps(string body)
        {
            double[] steps = new double[3] { double.NaN, double.NaN, double.NaN };
            if(BodyExists(body))
            {
                steps[0] = externalbodydata[body].WindTimeStep;
                steps[1] = externalbodydata[body].TemperatureTimeStep;
                steps[2] = externalbodydata[body].PressureTimeStep;
            }
            return steps;
        }
        internal static string[] GetSources(string body)
        {
            string[] sources = new string[3] { "None", "None", "None" };
            if (BodyExists(body))
            {
                sources[0] = externalbodydata[body].WindSource;
                sources[1] = externalbodydata[body].TemperatureSource;
                sources[2] = externalbodydata[body].PressureSource;
            }
            return sources;
        }

        //functions with less overhead for internal use
        internal static float[][,,] FetchGlobalWindData(string body, double time) => BodyExists(body) ? externalbodydata[body].GetWind(time) : null;
        internal static float[,,] FetchGlobalTemperatureData(string body, double time) => BodyExists(body) ? externalbodydata[body].GetTemperature(time) : null;
        internal static float[,,] FetchGlobalPressureData(string body, double time) => BodyExists(body) ? externalbodydata[body].GetPressure(time) : null;

        //-------------BODY DATA CLASS------------------
        internal class BodyData
        {
            internal string Body { get; private set; }

            private string windsource;
            internal string WindSource
            {
                get => string.IsNullOrEmpty(windsource) ? "None" : windsource;
                private set => windsource = value;
            }
            private GlobalPropertyDelegate windx;
            private GlobalPropertyDelegate windy;
            private GlobalPropertyDelegate windz;
            internal double WindTimeStep { get; private set; } = double.NaN;

            private string tempsource;
            internal string TemperatureSource
            {
                get => string.IsNullOrEmpty(tempsource) ? "None" : tempsource;
                private set => tempsource = value;
            }
            private GlobalPropertyDelegate temper;
            internal double TemperatureTimeStep { get; private set; } = double.NaN;

            private string presssource;
            internal string PressureSource 
            { 
                get => string.IsNullOrEmpty(presssource) ? "None": presssource;
                private set => presssource = value; 
            }
            private GlobalPropertyDelegate pressure;
            internal double PressureTimeStep { get; private set; } = double.NaN;

            internal BodyData(string bodyname)
            {
                Body = bodyname;
            }
            internal void SetWindFunc(string name, GlobalPropertyDelegate delx, GlobalPropertyDelegate dely, GlobalPropertyDelegate delz, double timestep)
            {
                WindSource = name;
                windx = delx;
                windy = dely;
                windz = delz;
                WindTimeStep = timestep;
            }
            internal void SetTemperatureFunc(string name, GlobalPropertyDelegate del, double timestep)
            {
                TemperatureSource = name;
                temper = del;
                TemperatureTimeStep = timestep;
            }
            internal void SetPressureFunc(string name, GlobalPropertyDelegate del, double timestep)
            {
                PressureSource = name;
                pressure = del;
                PressureTimeStep = timestep;
            }

            internal bool HasWind => !string.IsNullOrEmpty(windsource) && windx != null && windy != null && windz != null && double.IsFinite(WindTimeStep);
            internal float[][,,] GetWind(double time)
            {
                if (HasWind)
                {
                    float[,,] xWind = windx.Invoke(Body, time);
                    float[,,] yWind = windy.Invoke(Body, time);
                    float[,,] zWind = windz.Invoke(Body, time);
                    if(xWind != null && yWind != null && zWind != null)
                    {
                        return CheckArraySizes(xWind, yWind, zWind) ? new float[3][,,] { xWind, yWind, zWind } : throw new DataMisalignedException("Returned Wind data arrays are not of identical dimensions.");
                    }
                }
                return null;
            }

            internal bool HasTemperature => !string.IsNullOrEmpty(tempsource) && temper != null && double.IsFinite(TemperatureTimeStep);
            internal float[,,] GetTemperature(double time) => HasTemperature ? temper.Invoke(Body, time) : null;

            internal bool HasPressure => !string.IsNullOrEmpty(presssource) && pressure != null && double.IsFinite(PressureTimeStep);
            internal float[,,] GetPressure(double time) => HasPressure ? pressure.Invoke(Body, time) : null;
        }

        //-------------GET DATA FROM MCWS----------------

        //Data in use by the flighthandler
        private static MCWS_FlightHandler Instance => MCWS_FlightHandler.Instance;
        private static bool CanGetData => HighLogic.LoadedSceneIsFlight && Instance != null;
        private const string NotFlightScene = "Cannot access FlightHandler data outside of the Flight scene.";
        public static Vector3 GetCurrentWindVec() => CanGetData ? Instance.CachedWind : throw new InvalidOperationException(NotFlightScene); // wind vector in the global coordinate frame
        public static Vector3 GetRawWindVec() => CanGetData ? Instance.RawWind : throw new InvalidOperationException(NotFlightScene); //wind vector in the local coordinate frame
        public static double GetCurrentTemperature() => CanGetData ? Instance.Temperature : throw new InvalidOperationException(NotFlightScene);
        public static double GetCurrentPressure() => CanGetData ? Instance.Pressure : throw new InvalidOperationException(NotFlightScene);
        //nearest-neighbor interpolation for global data
        public static float[][,,] GetCurrentWindData()
        {
            double timelerp = CanGetData ? UtilMath.Clamp01((Instance.CurrentTime % Instance.Windtimestep) / Instance.Windtimestep) : throw new InvalidOperationException(NotFlightScene);
            return timelerp > 0.5 ? new float[][,,] { Instance.winddataX2, Instance.winddataY2, Instance.winddataZ2 } : new float[][,,] { Instance.winddataX1, Instance.winddataY1, Instance.winddataZ1 };
        }
        public static float[,,] GetCurrentTemperatureData()
        {
            double timelerp = CanGetData ? UtilMath.Clamp01((Instance.CurrentTime % Instance.Temptimestep) / Instance.Temptimestep) : throw new InvalidOperationException(NotFlightScene);
            return timelerp > 0.5 ? Instance.temperaturedata2 : Instance.temperaturedata1;
        }
        public static float[,,] GetCurrentPressureData()
        {
            double timelerp = CanGetData ? UtilMath.Clamp01((Instance.CurrentTime % Instance.Presstimestep) / Instance.Presstimestep) : throw new InvalidOperationException(NotFlightScene);
            return timelerp > 0.5 ? Instance.pressuredata2 : Instance.pressuredata1;
        }
        
        //Get Global Data for any body at any time. No interpolation is performed on MCWS's end here, so make sure you can deal with these values.
        public static float[][,,] GetGlobalWindData(string body, double time)
        {
            CheckInputs(body, time);
            try
            {
                CelestialBody bod = FlightGlobals.GetBodyByName(body);
                return (bod != null && bod.atmosphere && BodyExists(body)) ? externalbodydata[body].GetWind(time) : null;
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
                return (bod != null && bod.atmosphere && BodyExists(body)) ? externalbodydata[body].GetTemperature(time) : null;
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
                return (bod != null && bod.atmosphere && BodyExists(body)) ? externalbodydata[body].GetPressure(time) : null;
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
            float[][,,] winddata = GetGlobalWindData(body, time);
            float[,,] winddataX = winddata[0];
            float[,,] winddataY = winddata[1];
            float[,,] winddataZ = winddata[2];

            CelestialBody bod = FlightGlobals.GetBodyByName(body);
            try
            {
                if (bod != null && bod.atmosphere && alt <= bod.atmosphereDepth && winddata != null)
                {
                    //derive the locations of the data in the arrays
                    double mapx = UtilMath.WrapAround((lon * (winddataX.GetUpperBound(0) + 1)) - 0.5, 0, winddataX.GetUpperBound(1) + 1);
                    double mapy = (lat * (winddataX.GetUpperBound(1) + 1)) - 0.5;
                    double mapz = alt * (winddataX.GetUpperBound(2) + 1);

                    double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                    double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                    double lerpz = alt >= 0.0 ? UtilMath.Clamp01(mapz - Math.Truncate(mapz)) : 0.0;

                    int leftx = (int)Math.Truncate(mapx);
                    int rightx = UtilMath.WrapAround((int)Math.Truncate(mapx) + 1, 0, winddataX.GetUpperBound(0));

                    int bottomy = Utils.Clamp((int)Math.Truncate(mapy), 0, winddataX.GetUpperBound(1));
                    int topy = Utils.Clamp((int)Math.Truncate(mapy) + 1, 0, winddataX.GetUpperBound(1));

                    int bottomz = Utils.Clamp((int)Math.Truncate(mapz), 0, winddataX.GetUpperBound(2));
                    int topz = Utils.Clamp(bottomz + 1, 0, winddataX.GetUpperBound(2));

                    //Bilinearly interpolate on the longitude and latitude axes
                    float BottomPlaneX = Utils.BiLerp(winddataX[leftx, bottomy, bottomz], winddataX[rightx, bottomy, bottomz], winddataX[leftx, topy, bottomz], winddataX[rightx, topy, bottomz], (float)lerpx, (float)lerpy);
                    float TopPlaneX = Utils.BiLerp(winddataX[leftx, bottomy, topz], winddataX[rightx, bottomy, topz], winddataX[leftx, topy, topz], winddataX[rightx, topy, topz], (float)lerpx, (float)lerpy);

                    float BottomPlaneY = Utils.BiLerp(winddataY[leftx, bottomy, bottomz], winddataY[rightx, bottomy, bottomz], winddataY[leftx, topy, bottomz], winddataY[rightx, topy, bottomz], (float)lerpx, (float)lerpy);
                    float TopPlaneY = Utils.BiLerp(winddataY[leftx, bottomy, topz], winddataY[rightx, bottomy, topz], winddataY[leftx, topy, topz], winddataY[rightx, topy, topz], (float)lerpx, (float)lerpy);

                    float BottomPlaneZ = Utils.BiLerp(winddataZ[leftx, bottomy, bottomz], winddataZ[rightx, bottomy, bottomz], winddataZ[leftx, topy, bottomz], winddataZ[rightx, topy, bottomz], (float)lerpx, (float)lerpy);
                    float TopPlaneZ = Utils.BiLerp(winddataZ[leftx, bottomy, topz], winddataZ[rightx, bottomy, topz], winddataZ[leftx, topy, topz], winddataZ[rightx, topy, topz], (float)lerpx, (float)lerpy);

                    Vector3 BottomPlane = new Vector3(BottomPlaneX, BottomPlaneY, BottomPlaneZ);
                    Vector3 TopPlane = new Vector3(TopPlaneX, TopPlaneY, TopPlaneZ);

                    Vector3 Final = Vector3.Lerp(BottomPlane, TopPlane, (float)lerpz);
                    return Utils.IsVectorFinite(Final) ? Final : throw new NotFiniteNumberException();
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
            float[,,] temperaturedata = GetGlobalTemperatureData(body, time);
            CelestialBody bod = FlightGlobals.GetBodyByName(body);
            bool validbody = bod != null && bod.atmosphere && alt <= bod.atmosphereDepth;
            try
            {
                if (validbody && temperaturedata != null)
                {
                    double mapx = UtilMath.WrapAround(((lon + 180.0) / 360.0 * temperaturedata.Length) - 0.5, 0, temperaturedata.Length);
                    double mapy = ((180.0 - (lat + 90.0)) / 180.0 * (temperaturedata.GetUpperBound(1) + 1)) - 0.5;
                    double mapz = (alt / bod.atmosphereDepth) * (temperaturedata.GetUpperBound(2) + 1);

                    double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                    double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                    double lerpz = alt >= 0.0 ? UtilMath.Clamp01(mapz - Math.Truncate(mapz)) : 0.0f;

                    int leftx = (int)Math.Truncate(mapx);
                    int rightx = (int)UtilMath.WrapAround(mapx + 1, 0, temperaturedata.GetUpperBound(0));

                    int bottomy = Utils.Clamp((int)Math.Truncate(mapy), 0, temperaturedata.GetUpperBound(1));
                    int topy = Utils.Clamp((int)Math.Truncate(mapy) + 1, 0, temperaturedata.GetUpperBound(1));

                    int bottomz = Utils.Clamp((int)Math.Truncate(mapz), 0, temperaturedata.GetUpperBound(2));
                    int topz = Utils.Clamp(bottomz + 1, 0, temperaturedata.GetUpperBound(2));

                    float BottomPlane = Utils.BiLerp(temperaturedata[leftx,bottomy,bottomz], temperaturedata[rightx, bottomy, bottomz], temperaturedata[leftx, topy, bottomz], temperaturedata[rightx, topy, bottomz], (float)lerpx, (float)lerpy);
                    float TopPlane = Utils.BiLerp(temperaturedata[leftx, bottomy, topz], temperaturedata[rightx, bottomy, topz], temperaturedata[leftx,topy,topz], temperaturedata[rightx, topy, topz], (float)lerpx, (float)lerpy);

                    double Final = UtilMath.Lerp((double)BottomPlane, (double)TopPlane, lerpz);
                    return double.IsFinite(Final) ? Final : throw new NotFiniteNumberException();
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
            float[,,] pressuredata = GetGlobalPressureData(body, time);
            CelestialBody bod = FlightGlobals.GetBodyByName(body);
            bool validbody = bod != null && bod.atmosphere && alt <= bod.atmosphereDepth;
            try
            {
                if (validbody && pressuredata != null)
                {
                    double mapx = UtilMath.WrapAround(((lon + 180.0) / 360.0 * (pressuredata.GetUpperBound(0) + 1)) - 0.5, 0, (pressuredata.GetUpperBound(0) + 1));
                    double mapy = ((180.0 - (lat + 90.0)) / 180.0 * (pressuredata.GetUpperBound(1)+1)) - 0.5;
                    double mapz = (alt / bod.atmosphereDepth) * (pressuredata.GetUpperBound(2) + 1);

                    double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                    double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                    double lerpz = alt >= 0.0 ? UtilMath.Clamp01(mapz - Math.Truncate(mapz)) : 0.0f;

                    int leftx = (int)Math.Truncate(mapx);
                    int rightx = (int)UtilMath.WrapAround(mapx + 1, 0, pressuredata.Length);

                    int bottomy = Utils.Clamp((int)Math.Truncate(mapy), 0, pressuredata.GetUpperBound(1));
                    int topy = Utils.Clamp((int)Math.Truncate(mapy) + 1, 0, pressuredata.GetUpperBound(1));

                    int bottomz = Utils.Clamp((int)Math.Truncate(mapz), 0, pressuredata.GetUpperBound(2) - 1);
                    int topz = Utils.Clamp(bottomz + 1, 0, pressuredata.GetUpperBound(2));

                    float BottomPlane = Utils.BiLerp(pressuredata[leftx,bottomy,bottomz], pressuredata[rightx,bottomy,bottomz], pressuredata[leftx,topy,bottomz], pressuredata[rightx, topy, bottomz], (float)lerpx, (float)lerpy);
                    float TopPlane = Utils.BiLerp(pressuredata[leftx, bottomy, topz], pressuredata[rightx, bottomy, topz], pressuredata[leftx, topy, topz], pressuredata[rightx,topy,topz], (float)lerpx, (float)lerpy);

                    double Final = Utils.InterpolatePressure((double)BottomPlane, (double)TopPlane, lerpz);
                    return double.IsFinite(Final) ? Final : throw new NotFiniteNumberException();
                }
            }
            catch (Exception ex)
            {
                Utils.LogAPIWarning("An Exception occurred when retrieving point wind data. Stock pressure data was returned as a failsafe. Exception thrown: " + ex.ToString());
            }
            return validbody ? bod.GetPressure(alt) : 0.0;
        }

        //-------------HELPER FUNCTIONS AND VALUES-----------------
        internal const double DEFAULTINTERVAL = 300.0;
        internal static bool BodyExists(string body) => externalbodydata != null && externalbodydata.ContainsKey(body);

        private static void CheckRegistration(string body, double step)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                throw new InvalidOperationException("Cannot register data with MCWS during the Flight scene.");
            }
            if (!double.IsFinite(step) || step <= 0.0)
            {
                throw new ArgumentOutOfRangeException("Timestep argument was non-finite or less than or equal to zero.");
            }
            if (externalbodydata == null)
            {
                externalbodydata = new Dictionary<string, BodyData>();
            }
            if (!externalbodydata.ContainsKey(body))
            {
                externalbodydata.Add(body, new BodyData(body));
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

        private static void ToRegister(string type, string name, string body, double step) => Utils.LogAPI(string.Format("Registering '{0}' as a {1} data source for the Celestial Body {2} with timestep length {3:F1}.", name, type, body, step));
        private static void SuccessfulRegistration(string type, string name, string body) => Utils.LogAPI(string.Format("'{0}' has successfully registered as a {1} data source for the Celestial Body {2}.", name, type, body));
        private static void CannotRegister(string type, string name, string body) => Utils.LogAPIWarning(string.Format("Could not register '{0}' as a {1} data source for the Celestial Body {2}. Another plugin has already registered.", name, type, body));
        private static void FormatError(string type) => throw new FormatException(string.Format("Returned {0} Data array does not conform to the required format.", type));
        private static void BadArrayError(string type) => throw new NullReferenceException(string.Format("Returned {0} Data array is null.", type));
        private static void NullArgs() => throw new ArgumentNullException("One or more arguments was null or empty.");
    }
}
