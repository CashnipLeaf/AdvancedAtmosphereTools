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
        public static bool RegisterTimestepWindData(string body, GlobalPropertyDelegate dlgX, GlobalPropertyDelegate dlgY, GlobalPropertyDelegate dlgZ, string name, bool scaleLog, double step)
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
                    if (!CheckArraySizes(checkdataX, checkdataY, checkdataZ)) //check that the three arrays are of identical dimensions. Otherwise, MCWS can break at the edge cases.
                    {
                        throw new FormatException("The three returned Wind data arrays are not of identical dimensions.");
                    }
                    if (checkdataX.GetLength(0) >= 2 && checkdataX.GetLength(1) >= 2 && checkdataX.GetLength(2) >= 2)
                    {
                        externalbodydata[body].SetWindFunc(name, dlgX, dlgY,dlgZ, scaleLog, step);
                        SuccessfulRegistration("Wind", name, body);
                        return true;
                    }
                    FormatError("Wind");
                }
                throw new ArgumentNullException("One or more of the returned Wind data arrays was null.");
            }
            CannotRegister("Wind", name, body);
            return false;
        }
        public static bool RegisterWindData(string body, GlobalPropertyDelegate dlgx, GlobalPropertyDelegate dlgy, GlobalPropertyDelegate dlgz, string name, bool scaleLog) => RegisterTimestepWindData(body, dlgx,dlgy,dlgz, name, scaleLog, DEFAULTINTERVAL);

        public static bool RegisterTimestepTemperatureData(string body, GlobalPropertyDelegate dlg, string name, bool scaleLog, double step)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(body) || dlg == null)
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
                    if (checkdata.GetLength(0) >= 2 && checkdata.GetLength(1) >= 2 && checkdata.GetLength(2) >= 2)
                    {
                        externalbodydata[body].SetTemperatureFunc(name, dlg, scaleLog, step);
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
        public static bool RegisterTemperatureData(string body, GlobalPropertyDelegate dlg, string name, bool scaleLog) => RegisterTimestepTemperatureData(body, dlg, name, scaleLog, DEFAULTINTERVAL);

        public static bool RegisterTimestepPressureData(string body, GlobalPropertyDelegate dlg, string name, bool scaleLog, double step)
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
                    if (checkdata.GetLength(0) >= 2 && checkdata.GetLength(1) >= 2 && checkdata.GetLength(2) >= 2)
                    {
                        externalbodydata[body].SetPressureFunc(name, dlg, scaleLog, step);
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
        public static bool RegisterPressureData(string body, GlobalPropertyDelegate dlg, string name, bool scaleLog) => RegisterTimestepPressureData(body, dlg, name, scaleLog, DEFAULTINTERVAL);

        //-------------FETCH EXTERNAL DATA-------------

        //These are terrible. Don't do this. 
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
            if (BodyExists(body))
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
        internal static bool[] GetScaling(string body)
        {
            bool[] logs = new bool[3] { false, false, false };
            if (BodyExists(body))
            {
                logs[0] = externalbodydata[body].WindLogScaling;
                logs[1] = externalbodydata[body].TempLogScaling;
                logs[2] = externalbodydata[body].PressLogScaling;
            }
            return logs;
        }

        //functions with less overhead for internal use only
        internal static float[][,,] FetchGlobalWindData(string body, double time) => BodyExists(body) ? externalbodydata[body].GetWind(time) : null;
        internal static float[,,] FetchGlobalTemperatureData(string body, double time) => BodyExists(body) ? externalbodydata[body].GetTemperature(time) : null;
        internal static float[,,] FetchGlobalPressureData(string body, double time) => BodyExists(body) ? externalbodydata[body].GetPressure(time) : null;

        //-------------BODY DATA CLASS------------------
        internal class BodyData
        {
            internal string Body;

            private string windsource;
            internal string WindSource => string.IsNullOrEmpty(windsource) ? "None" : windsource;
            private GlobalPropertyDelegate windx;
            private GlobalPropertyDelegate windy;
            private GlobalPropertyDelegate windz;
            internal double WindTimeStep = double.NaN;
            internal bool WindLogScaling = false;

            private string tempsource;
            internal string TemperatureSource => string.IsNullOrEmpty(tempsource) ? "None" : tempsource;
            private GlobalPropertyDelegate temper;
            internal double TemperatureTimeStep = double.NaN;
            internal bool TempLogScaling = false;

            private string presssource;
            internal string PressureSource => string.IsNullOrEmpty(presssource) ? "None" : presssource;
            private GlobalPropertyDelegate pressure;
            internal double PressureTimeStep = double.NaN;
            internal bool PressLogScaling = false;

            internal BodyData(string bodyname) => Body = bodyname;

            internal void SetWindFunc(string name, GlobalPropertyDelegate delx, GlobalPropertyDelegate dely, GlobalPropertyDelegate delz, bool scaleLog, double timestep)
            {
                windsource = name;
                windx = delx;
                windy = dely;
                windz = delz;
                WindTimeStep = timestep;
                WindLogScaling = scaleLog;
            }
            internal void SetTemperatureFunc(string name, GlobalPropertyDelegate del, bool scaleLog, double timestep)
            {
                tempsource = name;
                temper = del;
                TemperatureTimeStep = timestep;
                TempLogScaling = scaleLog;
            }
            internal void SetPressureFunc(string name, GlobalPropertyDelegate del, bool scaleLog, double timestep)
            {
                presssource = name;
                pressure = del;
                PressureTimeStep = timestep;
                PressLogScaling = scaleLog;
            }

            internal bool HasWind => !string.IsNullOrEmpty(windsource) && windx != null && windy != null && windz != null && double.IsFinite(WindTimeStep);
            internal float[][,,] GetWind(double time)
            {
                if (HasWind)
                {
                    float[,,] xWind = windx.Invoke(Body, time);
                    float[,,] yWind = windy.Invoke(Body, time);
                    float[,,] zWind = windz.Invoke(Body, time);
                    if (xWind != null && yWind != null && zWind != null)
                    {
                        return CheckArraySizes(xWind, yWind, zWind) ? new float[3][,,] { xWind, yWind, zWind } : throw new FormatException("The three Wind data arrays are not of identical dimensions.");
                    }
                }
                return null;
            }

            internal bool HasTemperature => !string.IsNullOrEmpty(tempsource) && temper != null && double.IsFinite(TemperatureTimeStep);
            internal float[,,] GetTemperature(double time) => HasTemperature ? temper.Invoke(Body, time) : null;

            internal bool HasPressure => !string.IsNullOrEmpty(presssource) && pressure != null && double.IsFinite(PressureTimeStep);
            internal float[,,] GetPressure(double time) => HasPressure ? pressure.Invoke(Body, time) : null;
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
        public static double GetCurrentPressure() => CanGetData ? Instance.Pressure : throw new InvalidOperationException(NotFlightScene);
        //nearest-neighbor interpolation for global data
        public static float[][,,] GetCurrentWindData()
        {
            double timelerp = CanGetData ? UtilMath.Clamp01((Instance.CurrentTime % Instance.Windtimestep) / Instance.Windtimestep) : throw new InvalidOperationException(NotFlightScene);
            return timelerp > 0.5 ? new float[3][,,] { Instance.winddataX2, Instance.winddataY2, Instance.winddataZ2 } : new float[3][,,] { Instance.winddataX1, Instance.winddataY1, Instance.winddataZ1 };
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
            CelestialBody bod = FlightGlobals.GetBodyByName(body);
            try
            {
                float[][,,] winddata = GetGlobalWindData(body, time);
                if (BodyExists(body) && bod != null && bod.atmosphere && alt <= bod.atmosphereDepth && winddata != null)
                {
                    float[,,] winddataX = winddata[0];
                    float[,,] winddataY = winddata[1];
                    float[,,] winddataZ = winddata[2];

                    //derive the locations of the data in the arrays
                    double mapx = UtilMath.WrapAround((((lon + 180.0) / 360.0) * winddataX.GetLength(2)) - 0.5, 0, winddataX.GetLength(2));
                    double mapy = (lat * (winddataX.GetLength(1))) - 0.5;
                    double normalalt = (alt / bod.atmosphereDepth);
                    double mapz = (externalbodydata[body].WindLogScaling ? Utils.ScaleLog(normalalt) : normalalt) * winddataX.GetLength(0);

                    double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                    double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                    double lerpz = alt >= 0.0 ? UtilMath.Clamp01(mapz - Math.Truncate(mapz)) : 0.0;

                    int x1 = (int)UtilMath.Clamp(Math.Truncate(mapx), 0, winddataX.GetUpperBound(2));
                    int x2 = UtilMath.WrapAround(x1 + 1, 0, winddataX.GetUpperBound(2));

                    int y1 = Utils.Clamp((int)Math.Truncate(mapy), 0, winddataX.GetUpperBound(1));
                    int y2 = Utils.Clamp(y1 + 1, 0, winddataX.GetUpperBound(1));

                    int z1 = Utils.Clamp((int)Math.Truncate(mapz), 0, winddataX.GetUpperBound(0));
                    int z2 = Utils.Clamp(z1 + 1, 0, winddataX.GetUpperBound(0));

                    //Bilinearly interpolate on the longitude and latitude axes
                    float BottomPlaneX = Utils.BiLerp(winddataX[z1, y1, x1], winddataX[z1, y1, x2], winddataX[z1, y2, x1], winddataX[z1, y2, x2], (float)lerpx, (float)lerpy);
                    float TopPlaneX = Utils.BiLerp(winddataX[z2, y1, x1], winddataX[z2, y1, x2], winddataX[z2, y2, x1], winddataX[z2, y2, x2], (float)lerpx, (float)lerpy);

                    float BottomPlaneY = Utils.BiLerp(winddataY[z1, y1, x1], winddataY[z1, y1, x2], winddataY[z1, y2, x1], winddataY[z1, y2, x2], (float)lerpx, (float)lerpy);
                    float TopPlaneY = Utils.BiLerp(winddataY[z2, y1, x1], winddataY[z2, y1, x2], winddataY[z2, y2, x1], winddataY[z2, y2, x2], (float)lerpx, (float)lerpy);

                    float BottomPlaneZ = Utils.BiLerp(winddataZ[z1, y1, x1], winddataZ[z1, y1, x2], winddataZ[z1, y2, x1], winddataZ[z1, y2, x2], (float)lerpx, (float)lerpy);
                    float TopPlaneZ = Utils.BiLerp(winddataZ[z2, y1, x1], winddataZ[z2, y1, x2], winddataZ[z2, y2, x1], winddataZ[z2, y2, x2], (float)lerpx, (float)lerpy);

                    Vector3 BottomPlane = new Vector3(BottomPlaneX, BottomPlaneY, BottomPlaneZ);
                    Vector3 TopPlane = new Vector3(TopPlaneX, TopPlaneY, TopPlaneZ);

                    //Undo the logarithmic scaling if needed.
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
            CelestialBody bod = FlightGlobals.GetBodyByName(body);
            bool validbody = BodyExists(body) && bod != null && bod.atmosphere && alt <= bod.atmosphereDepth;
            try
            {
                float[,,] temperaturedata = GetGlobalTemperatureData(body, time);
                if (validbody && temperaturedata != null)
                {
                    double mapx = UtilMath.WrapAround((((lon + 180.0) / 360.0) * temperaturedata.GetLength(2)) - 0.5, 0, temperaturedata.GetLength(2));
                    double mapy = (((180.0 - (lat + 90.0)) / 180.0) * temperaturedata.GetLength(1)) - 0.5;
                    double normalalt = (alt / bod.atmosphereDepth);
                    double mapz = (externalbodydata[body].TempLogScaling ? Utils.ScaleLog(normalalt) : normalalt) * temperaturedata.GetLength(0);

                    double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                    double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                    double lerpz = alt >= 0.0 ? UtilMath.Clamp01(mapz - Math.Truncate(mapz)) : 0.0f;

                    int x1 = (int)UtilMath.Clamp(Math.Truncate(mapx), 0, temperaturedata.GetUpperBound(2));
                    int x2 = UtilMath.WrapAround(x1 + 1, 0, temperaturedata.GetUpperBound(2));

                    int y1 = Utils.Clamp((int)Math.Truncate(mapy), 0, temperaturedata.GetUpperBound(1));
                    int y2 = Utils.Clamp(y1 + 1, 0, temperaturedata.GetUpperBound(1));

                    int z1 = Utils.Clamp((int)Math.Truncate(mapz), 0, temperaturedata.GetUpperBound(0));
                    int z2 = Utils.Clamp(z1 + 1, 0, temperaturedata.GetUpperBound(0));

                    float BottomPlane = Utils.BiLerp(temperaturedata[z1, y1, x1], temperaturedata[z1, y1, x2], temperaturedata[z1, y2, x1], temperaturedata[z1, y2, x2], (float)lerpx, (float)lerpy);
                    float TopPlane = Utils.BiLerp(temperaturedata[z2, y1, x1], temperaturedata[z2, y1, x2], temperaturedata[z2, y2, x1], temperaturedata[z2, y2, x2], (float)lerpx, (float)lerpy);

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
            CelestialBody bod = FlightGlobals.GetBodyByName(body);
            bool validbody = BodyExists(body) && bod != null && bod.atmosphere && alt <= bod.atmosphereDepth;
            try
            {
                float[,,] pressuredata = GetGlobalPressureData(body, time);
                if (validbody && pressuredata != null)
                {
                    double mapx = UtilMath.WrapAround((((lon + 180.0) / 360.0) * pressuredata.GetLength(2)) - 0.5, 0, pressuredata.GetLength(2));
                    double mapy = (((180.0 - (lat + 90.0)) / 180.0) * pressuredata.GetLength(1)) - 0.5;
                    double normalalt = (alt / bod.atmosphereDepth);
                    double mapz = (externalbodydata[body].PressLogScaling ? Utils.ScaleLog(normalalt) : normalalt) * pressuredata.GetLength(0);

                    double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                    double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                    double lerpz = alt >= 0.0 ? UtilMath.Clamp01(mapz - Math.Truncate(mapz)) : 0.0f;

                    int x1 = (int)UtilMath.Clamp(Math.Truncate(mapx), 0, pressuredata.GetUpperBound(2));
                    int x2 = (int)UtilMath.WrapAround(mapx + 1, 0, pressuredata.GetUpperBound(2));

                    int y1 = Utils.Clamp((int)Math.Truncate(mapy), 0, pressuredata.GetUpperBound(1));
                    int y2 = Utils.Clamp((int)Math.Truncate(mapy) + 1, 0, pressuredata.GetUpperBound(1));

                    int z1 = Utils.Clamp((int)Math.Truncate(mapz), 0, pressuredata.GetUpperBound(0));
                    int z2 = Utils.Clamp(z1 + 1, 0, pressuredata.GetUpperBound(0));

                    float BottomPlane = Utils.BiLerp(pressuredata[z1, y1, x1], pressuredata[z1, y1, x2], pressuredata[z1, y2, x1], pressuredata[z1, y2, x2], (float)lerpx, (float)lerpy);
                    float TopPlane = Utils.BiLerp(pressuredata[z2, y1, x1], pressuredata[z2, y1, x2], pressuredata[z2, y2, x1], pressuredata[z2, y2, x2], (float)lerpx, (float)lerpy);

                    //apply logarithmic interpolation
                    double Final = Utils.InterpolateLog((double)BottomPlane, (double)TopPlane, lerpz);
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

        //I CBA to do a cleaner implementation of this.
        internal static bool CheckArraySizes(float[,,] arr1, float[,,] arr2, float[,,] arr3) => CheckArraySizes(arr1, arr2) && CheckArraySizes(arr1, arr3) && CheckArraySizes(arr2, arr3);
        internal static bool CheckArraySizes(float[,,] arr1, float[,,] arr2) => arr1.GetLength(0) == arr2.GetLength(0) && arr1.GetLength(1) == arr2.GetLength(1) && arr1.GetLength(2) == arr2.GetLength(2);

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
        private static void BadArrayError(string type) => throw new ArgumentNullException(string.Format("Returned {0} Data array is null.", type));
        private static void NullArgs() => throw new ArgumentNullException("One or more arguments was null or empty.");
    }
}
