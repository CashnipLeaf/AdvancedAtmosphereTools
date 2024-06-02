using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularClimateWeatherSystems
{
    //Return codes: 
    //0 = all clear
    //1 = additional post-processing needed
    //-1 = no data
    //-2 = error or exception
    internal class MCWS_BodyData
    {
        private readonly string body;

        private float[][,,] WindDataX;
        private float[][,,] WindDataY;
        private float[][,,] WindDataZ;
        private double WindScaleFactor = double.NaN;
        private double WindTimeStep = double.NaN;
        internal double WindModelTop = 0.0;
        private double WindLonOffset = 0.0;
        internal bool HasWind => HasWindData || HasFlowmaps;
        internal bool HasWindData => (WindDataX != null && WindDataY != null && WindDataZ != null);

        private float[][,,] TempData;
        private double TempScaleFactor = double.NaN;
        private double TempTimeStep = double.NaN;
        internal double TempModelTop = 0.0;
        private double TempLonOffset = 0.0;
        internal bool HasTemperature => TempData != null;

        private float[][,,] PressData;
        private double PressScaleFactor = double.NaN;
        private double PressTimeStep = double.NaN;
        internal double PressModelTop = 0.0;
        private double PressLonOffset = 0.0;
        internal bool HasPressure => PressData != null;

        internal List<FlowMap> Flowmaps;
        internal bool HasFlowmaps => Flowmaps != null && Flowmaps.Count > 0;

        internal MCWS_BodyData(string body)
        {
            Flowmaps = new List<FlowMap>();
            this.body = body;
        }

        internal void AddWindData(float[][,,] WindX, float[][,,] WindY, float[][,,] WindZ, double scalefactor, double timestep, double modeltop, double lonoffset)
        {
            if (HasWindData)
            {
                Utils.LogWarning(string.Format("Wind data already exists for {0}.", body));
            }
            else
            {
                WindDataX = WindX;
                WindDataY = WindY;
                WindDataZ = WindZ;
                WindScaleFactor = scalefactor;
                WindTimeStep = timestep;
                WindModelTop = modeltop;
                WindLonOffset = lonoffset;
                Utils.LogInfo(string.Format("Successfully added Wind Data to {0}.", body));
            }
        }

        internal void AddTemperatureData(float[][,,] Temp, double scalefactor, double timestep, double modeltop, double lonoffset)
        {
            if (HasTemperature)
            {
                Utils.LogWarning(string.Format("Temperature data already exists for {0}.", body));
            }
            else
            {
                TempData = Temp;
                TempScaleFactor = scalefactor;
                TempTimeStep = timestep;
                TempModelTop = modeltop;
                TempLonOffset = lonoffset;
                Utils.LogInfo(string.Format("Successfully added Temperature Data to {0}.", body));
            }
        }

        internal void AddPressureData(float[][,,] Press, double scalefactor, double timestep, double modeltop, double lonoffset)
        {
            if (HasPressure)
            {
                Utils.LogWarning(string.Format("Pressure data already exists for {0}.", body));
            }
            else
            {
                PressData = Press;
                PressScaleFactor = scalefactor;
                PressTimeStep = timestep;
                PressModelTop = modeltop;
                PressLonOffset = lonoffset;
                Utils.LogInfo(string.Format("Successfully added Pressure Data to {0}.", body));
            }
        }

        internal void AddFlowMap(FlowMap flowmap) => Flowmaps.Add(flowmap);

        internal int GetWind(double lon, double lat, double alt, double time, out Vector3 windvec)
        {
            windvec = Vector3.zero;
            if (WindDataX != null && WindDataY != null && WindDataZ != null)
            {
                switch (1)
                {
                    default:
                        try
                        {
                            double normalizedlon = UtilMath.WrapAround(lon + 180.0 - WindLonOffset, 0.0, 360.0) / 360.0;
                            double normalizedlat = (180.0 - (lat + 90.0)) / 180.0;
                            double normalizedalt = UtilMath.Clamp01(alt / WindModelTop);
                            int timeindex = (int)Math.Floor(time / WindTimeStep) % WindDataX.GetLength(0);
                            int timeindex2 = (timeindex + 1) % WindDataX.GetLength(0);
                            //derive the locations of the data in the arrays

                            double mapx = UtilMath.WrapAround((normalizedlon * WindDataX[timeindex].GetLength(2)), 0, WindDataX[timeindex].GetLength(2));
                            double mapy = normalizedlat * WindDataX[timeindex].GetUpperBound(1);

                            int x1 = (int)UtilMath.Clamp(Math.Truncate(mapx), 0, WindDataX[timeindex].GetUpperBound(2));
                            int x2 = UtilMath.WrapAround(x1 + 1, 0, WindDataX[timeindex].GetLength(2));

                            int y1 = Utils.Clamp((int)Math.Floor(mapy), 0, WindDataX[timeindex].GetUpperBound(1));
                            int y2 = Utils.Clamp(y1 + 1, 0, WindDataX[timeindex].GetUpperBound(1));

                            double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                            double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                            double lerpz = Utils.ScaleAltitude(normalizedalt, WindScaleFactor, WindDataX[timeindex].GetUpperBound(0), out int z1, out int z2);
                            double lerpt = UtilMath.Clamp01((time % WindTimeStep) / WindTimeStep);

                            //Bilinearly interpolate on the longitude and latitude axes 
                            float BottomPlaneX1 = Utils.BiLerp(WindDataX[timeindex][z1, y1, x1], WindDataX[timeindex][z1, y1, x2], WindDataX[timeindex][z1, y2, x1], WindDataX[timeindex][z1, y2, x2], (float)lerpx, (float)lerpy);
                            float TopPlaneX1 = Utils.BiLerp(WindDataX[timeindex][z2, y1, x1], WindDataX[timeindex][z2, y1, x2], WindDataX[timeindex][z2, y2, x1], WindDataX[timeindex][z2, y2, x2], (float)lerpx, (float)lerpy);

                            float BottomPlaneX2 = Utils.BiLerp(WindDataX[timeindex2][z1, y1, x1], WindDataX[timeindex2][z1, y1, x2], WindDataX[timeindex2][z1, y2, x1], WindDataX[timeindex2][z1, y2, x2], (float)lerpx, (float)lerpy);
                            float TopPlaneX2 = Utils.BiLerp(WindDataX[timeindex2][z2, y1, x1], WindDataX[timeindex2][z2, y1, x2], WindDataX[timeindex2][z2, y2, x1], WindDataX[timeindex2][z2, y2, x2], (float)lerpx, (float)lerpy);

                            float BottomPlaneY1 = Utils.BiLerp(WindDataY[timeindex][z1, y1, x1], WindDataY[timeindex][z1, y1, x2], WindDataY[timeindex][z1, y2, x1], WindDataY[timeindex][z1, y2, x2], (float)lerpx, (float)lerpy);
                            float TopPlaneY1 = Utils.BiLerp(WindDataY[timeindex][z2, y1, x1], WindDataY[timeindex][z2, y1, x2], WindDataY[timeindex][z2, y2, x1], WindDataY[timeindex][z2, y2, x2], (float)lerpx, (float)lerpy);

                            float BottomPlaneY2 = Utils.BiLerp(WindDataY[timeindex2][z1, y1, x1], WindDataY[timeindex2][z1, y1, x2], WindDataY[timeindex2][z1, y2, x1], WindDataY[timeindex2][z1, y2, x2], (float)lerpx, (float)lerpy);
                            float TopPlaneY2 = Utils.BiLerp(WindDataY[timeindex2][z2, y1, x1], WindDataY[timeindex2][z2, y1, x2], WindDataY[timeindex2][z2, y2, x1], WindDataY[timeindex2][z2, y2, x2], (float)lerpx, (float)lerpy);

                            float BottomPlaneZ1 = Utils.BiLerp(WindDataZ[timeindex][z1, y1, x1], WindDataZ[timeindex][z1, y1, x2], WindDataZ[timeindex][z1, y2, x1], WindDataZ[timeindex][z1, y2, x2], (float)lerpx, (float)lerpy);
                            float TopPlaneZ1 = Utils.BiLerp(WindDataZ[timeindex][z2, y1, x1], WindDataZ[timeindex][z2, y1, x2], WindDataZ[timeindex][z2, y2, x1], WindDataZ[timeindex][z2, y2, x2], (float)lerpx, (float)lerpy);

                            float BottomPlaneZ2 = Utils.BiLerp(WindDataZ[timeindex2][z1, y1, x1], WindDataZ[timeindex2][z1, y1, x2], WindDataZ[timeindex2][z1, y2, x1], WindDataZ[timeindex2][z1, y2, x2], (float)lerpx, (float)lerpy);
                            float TopPlaneZ2 = Utils.BiLerp(WindDataZ[timeindex2][z2, y1, x1], WindDataZ[timeindex2][z2, y1, x2], WindDataZ[timeindex2][z2, y2, x1], WindDataZ[timeindex2][z2, y2, x2], (float)lerpx, (float)lerpy);

                            //Bilinearly interpolate on the altitude and time axes
                            double FinalX = UtilMath.Lerp(UtilMath.Lerp(BottomPlaneX1, TopPlaneX1, lerpz), UtilMath.Lerp(BottomPlaneX2, TopPlaneX2, lerpz), lerpt);
                            double FinalY = UtilMath.Lerp(UtilMath.Lerp(BottomPlaneY1, TopPlaneY1, lerpz), UtilMath.Lerp(BottomPlaneY2, TopPlaneY2, lerpz), lerpt);
                            double FinalZ = UtilMath.Lerp(UtilMath.Lerp(BottomPlaneZ1, TopPlaneZ1, lerpz), UtilMath.Lerp(BottomPlaneZ2, TopPlaneZ2, lerpz), lerpt);

                            //Create the wind vector
                            windvec = new Vector3((float)FinalX, (float)FinalY, (float)FinalZ);
                            if (!Utils.IsVectorFinite(windvec))
                            {
                                break;
                            }
                            else
                            {
                                return 0;
                            }
                        }
                        catch
                        {
                            break;
                        }
                }
            }
            
            if (HasFlowmaps)
            {
                windvec = Vector3.zero;
                try
                {
                    foreach (FlowMap map in Flowmaps)
                    {
                        windvec += map.GetWindVec(lon, lat, alt, time);
                    }
                    return Utils.IsVectorFinite(windvec) ? 0 : -2;
                }
                catch
                {
                    return -2;
                }
            }
            return -1;
        }
        internal int GetTemperature(double lon, double lat, double alt, double time, out double temp)
        {
            temp = 0.0;
            if (TempData != null)
            {
                try
                {
                    double normalizedlon = UtilMath.WrapAround(lon + 180.0 - TempLonOffset, 0.0, 360.0) / 360.0;
                    double normalizedlat = (180.0 - (lat + 90.0)) / 180.0;
                    double normalizedalt = UtilMath.Clamp01(alt / TempModelTop);
                    int timeindex = (int)Math.Floor(time / TempTimeStep) % TempData.GetLength(0);
                    int timeindex2 = (timeindex + 1) % TempData.GetLength(0);

                    //derive the locations of the data in the arrays
                    double mapx = UtilMath.WrapAround((normalizedlon * TempData[timeindex].GetLength(2)), 0, TempData[timeindex].GetLength(2));
                    double mapy = normalizedlat * TempData[timeindex].GetUpperBound(1);

                    int x1 = (int)UtilMath.Clamp(Math.Truncate(mapx), 0, TempData[timeindex].GetUpperBound(2));
                    int x2 = UtilMath.WrapAround(x1 + 1, 0, TempData[timeindex].GetLength(2));

                    int y1 = Utils.Clamp((int)Math.Floor(mapy), 0, TempData[timeindex].GetUpperBound(1));
                    int y2 = Utils.Clamp(y1 + 1, 0, TempData[timeindex].GetUpperBound(1));

                    double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                    double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                    double lerpz = Utils.ScaleAltitude(normalizedalt, TempScaleFactor, TempData[timeindex].GetUpperBound(0), out int z1, out int z2);
                    double lerpt = UtilMath.Clamp01((time % TempTimeStep) / TempTimeStep);

                    //Bilinearly interpolate on the longitude and latitude axes
                    float BottomPlane1 = Utils.BiLerp(TempData[timeindex][z1, y1, x1], TempData[timeindex][z1, y1, x2], TempData[timeindex][z1, y2, x1], TempData[timeindex][z1, y2, x2], (float)lerpx, (float)lerpy);
                    float TopPlane1 = Utils.BiLerp(TempData[timeindex][z2, y1, x1], TempData[timeindex][z2, y1, x2], TempData[timeindex][z2, y2, x1], TempData[timeindex][z2, y2, x2], (float)lerpx, (float)lerpy);

                    float BottomPlane2 = Utils.BiLerp(TempData[timeindex2][z1, y1, x1], TempData[timeindex2][z1, y1, x2], TempData[timeindex2][z1, y2, x1], TempData[timeindex2][z1, y2, x2], (float)lerpx, (float)lerpy);
                    float TopPlane2 = Utils.BiLerp(TempData[timeindex2][z2, y1, x1], TempData[timeindex2][z2, y1, x2], TempData[timeindex2][z2, y2, x1], TempData[timeindex2][z2, y2, x2], (float)lerpx, (float)lerpy);

                    //Bilinearly interpolate on the altitude and time axes
                    temp = UtilMath.Lerp(UtilMath.Lerp((double)BottomPlane1, (double)TopPlane1, lerpz), UtilMath.Lerp((double)BottomPlane2, (double)TopPlane2, lerpz), lerpt);
                    if (double.IsFinite(temp))
                    {
                        return alt > TempModelTop ? 1 : 0;
                    }
                    return -2;
                }
                catch
                {
                    return -2;
                }
            }
            else
            {
                return -1;
            }
        }
        internal int GetPressure(double lon, double lat, double alt, double time, out double press)
        {
            press = 0.0;
            if (PressData != null)
            {
                try
                {
                    double normalizedlon = UtilMath.WrapAround(lon + 180.0 - PressLonOffset, 0.0, 360.0) / 360.0;
                    double normalizedlat = (180.0 - (lat + 90.0)) / 180.0;
                    double normalizedalt = UtilMath.Clamp01(alt / PressModelTop);
                    int timeindex = (int)Math.Floor(time / PressTimeStep) % PressData.GetLength(0);
                    int timeindex2 = (timeindex + 1) % PressData.GetLength(0);

                    //derive the locations of the data in the arrays
                    double mapx = UtilMath.WrapAround((normalizedlon * PressData[timeindex].GetLength(2)), 0.0, PressData[timeindex].GetLength(2));
                    double mapy = normalizedlat * PressData[timeindex].GetUpperBound(1);

                    int x1 = (int)UtilMath.Clamp(Math.Truncate(mapx), 0, PressData[timeindex].GetUpperBound(2));
                    int x2 = UtilMath.WrapAround(x1 + 1, 0, PressData[timeindex].GetLength(2));

                    int y1 = Utils.Clamp((int)Math.Floor(mapy), 0, PressData[timeindex].GetUpperBound(1));
                    int y2 = Utils.Clamp(y1 + 1, 0, PressData[timeindex].GetUpperBound(1));

                    double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                    double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));
                    double lerpz = Utils.ScaleAltitude(normalizedalt, PressScaleFactor, PressData[timeindex].GetUpperBound(0), out int z1, out int z2);
                    double lerpt = UtilMath.Clamp01((time % PressTimeStep) / PressTimeStep);

                    //Bilinearly interpolate on the longitude and latitude axes
                    float BottomPlane1 = Utils.BiLerp(PressData[timeindex][z1, y1, x1], PressData[timeindex][z1, y1, x2], PressData[timeindex][z1, y2, x1], PressData[timeindex][z1, y2, x2], (float)lerpx, (float)lerpy);
                    float TopPlane1 = Utils.BiLerp(PressData[timeindex][z2, y1, x1], PressData[timeindex][z2, y1, x2], PressData[timeindex][z2, y2, x1], PressData[timeindex][z2, y2, x2], (float)lerpx, (float)lerpy);

                    float BottomPlane2 = Utils.BiLerp(PressData[timeindex2][z1, y1, x1], PressData[timeindex2][z1, y1, x2], PressData[timeindex2][z1, y2, x1], PressData[timeindex2][z1, y2, x2], (float)lerpx, (float)lerpy);
                    float TopPlane2 = Utils.BiLerp(PressData[timeindex2][z2, y1, x1], PressData[timeindex2][z2, y1, x2], PressData[timeindex2][z2, y2, x1], PressData[timeindex2][z2, y2, x2], (float)lerpx, (float)lerpy);

                    //Linearly interpolate on the time axis
                    double BottomPlaneFinal = UtilMath.Lerp((double)BottomPlane1, (double)BottomPlane2, lerpt);
                    double TopPlaneFinal = UtilMath.Lerp((double)TopPlane1, (double)TopPlane2, lerpt);

                    press = Utils.InterpolatePressure(BottomPlaneFinal, TopPlaneFinal, lerpz);
                    if (double.IsFinite(press))
                    {
                        return alt > PressModelTop ? 1 : 0;
                    }
                    return -2;
                }
                catch
                {
                    return -2;
                }
            }
            else
            {
                return -1;
            }
        }

        internal int GetWindData(double time, out float[][,,] dataarray)
        {
            dataarray = null;
            if (WindDataX != null && WindDataY != null && WindDataZ != null)
            {
                try
                {
                    int timeindex = (int)Math.Floor(time / WindTimeStep) % WindDataX.GetLength(0);
                    dataarray[0] = WindDataX[timeindex];
                    dataarray[1] = WindDataY[timeindex];
                    dataarray[2] = WindDataZ[timeindex];
                    return 0;
                }
                catch
                {
                    return -2;
                }
            }
            else
            {
                return -1;
            }
        }
        internal int GetTemperatureData(double time, out float[,,] dataarray)
        {
            dataarray = null;
            if (TempData != null)
            {
                try
                {
                    int timeindex = (int)Math.Truncate(time / TempTimeStep) % TempData.GetLength(0);
                    dataarray = TempData[timeindex];
                    return 0;
                }
                catch
                {
                    return -2;
                }
            }
            else
            {
                return -1;
            }
        }
        internal int GetPressureData(double time, out float[,,] dataarray)
        {
            dataarray = null;
            if (PressData != null)
            {
                try
                {
                    int timeindex = (int)Math.Truncate(time / PressTimeStep) % PressData.GetLength(0);
                    dataarray = PressData[timeindex];
                    return 0;
                }
                catch
                {
                    return -2;
                }
            }
            else
            {
                return -1;
            }
        }
    }

    internal class FlowMap
    {
        internal Texture2D flowmap;
        internal bool useThirdChannel; //whether or not to use the Blue channel to add a vertical component to the winds.
        internal FloatCurve AltitudeSpeedMultCurve;
        internal FloatCurve EW_AltitudeSpeedMultCurve;
        internal FloatCurve NS_AltitudeSpeedMultCurve;
        internal FloatCurve V_AltitudeSpeedMultCurve;
        internal FloatCurve WindSpeedMultiplierTimeCurve;
        internal float EWwind;
        internal float NSwind;
        internal float vWind;

        internal float timeoffset;

        internal int x;
        internal int y;

        internal FlowMap(Texture2D path, bool use3rdChannel, FloatCurve altmult, FloatCurve ewaltmultcurve, FloatCurve nsaltmultcurve, FloatCurve valtmultcurve, float EWwind, float NSwind, float vWind, FloatCurve speedtimecurve, float offset)
        {
            flowmap = path;
            useThirdChannel = use3rdChannel;
            AltitudeSpeedMultCurve = altmult;
            EW_AltitudeSpeedMultCurve = ewaltmultcurve;
            NS_AltitudeSpeedMultCurve = nsaltmultcurve;
            V_AltitudeSpeedMultCurve = valtmultcurve;
            WindSpeedMultiplierTimeCurve = speedtimecurve;
            this.EWwind = EWwind;
            this.NSwind = NSwind;
            this.vWind = vWind;
            timeoffset = WindSpeedMultiplierTimeCurve.maxTime != 0.0f ? offset % WindSpeedMultiplierTimeCurve.maxTime : 0.0f;

            x = flowmap.width;
            y = flowmap.height;
        }

        internal Vector3 GetWindVec(double lon, double lat, double alt, double time)
        {
            //AltitudeSpeedMultiplierCurve cannot go below 0.
            float speedmult = Math.Max(AltitudeSpeedMultCurve.Evaluate((float)alt), 0.0f) * WindSpeedMultiplierTimeCurve.Evaluate((float)(time - timeoffset + WindSpeedMultiplierTimeCurve.maxTime) % WindSpeedMultiplierTimeCurve.maxTime);
            if (speedmult > 0.0f)
            {
                //adjust longitude so the center of the map is the prime meridian for the purposes of these calculations
                double mapx = (((lon + 270.0) / 360.0) * x) - 0.5;
                double mapy = (((lat + 90.0) / 180.0) * y) - 0.5;
                double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));

                //locate the four nearby points, but don't go over the poles.
                int leftx = UtilMath.WrapAround((int)Math.Truncate(mapx), 0, x);
                int topy = Utils.Clamp((int)Math.Truncate(mapy), 0, y - 1);
                int rightx = UtilMath.WrapAround(leftx + 1, 0, x);
                int bottomy = Utils.Clamp(topy + 1, 0, y - 1);

                Color[] colors = new Color[4];
                Vector3[] vectors = new Vector3[4];
                colors[0] = flowmap.GetPixel(leftx, topy);
                colors[1] = flowmap.GetPixel(rightx, topy);
                colors[2] = flowmap.GetPixel(leftx, bottomy);
                colors[3] = flowmap.GetPixel(rightx, bottomy);

                for (int i = 0; i < 4; i++)
                {
                    Vector3 windvec = Vector3.zero;

                    windvec.z = (colors[i].r * 2.0f) - 1.0f;
                    windvec.x = (colors[i].g * 2.0f) - 1.0f;
                    windvec.y = useThirdChannel ? (colors[i].b * 2.0f) - 1.0f : 0.0f;
                    vectors[i] = windvec;
                }
                Vector3 wind = Vector3.Lerp(Vector3.Lerp(vectors[0], vectors[1], (float)lerpx), Vector3.Lerp(vectors[2], vectors[3], (float)lerpx), (float)lerpy);
                wind.x = wind.x * NSwind * NS_AltitudeSpeedMultCurve.Evaluate((float)alt);
                wind.y = wind.y * vWind * V_AltitudeSpeedMultCurve.Evaluate((float)alt);
                wind.z = wind.z * EWwind * EW_AltitudeSpeedMultCurve.Evaluate((float)alt);
                return wind * speedmult;
            }
            return Vector3.zero;
        }
    }
}
