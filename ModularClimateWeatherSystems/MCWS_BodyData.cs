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
        internal bool HasAtmo { get; private set; } = false;
        internal double Atmodepth { get; private set; } = 0.0;

        private float[][,,] WindDataX;
        private float[][,,] WindDataY;
        private float[][,,] WindDataZ;
        private double WindScaleFactor = double.NaN;
        private double WindTimeStep = double.NaN;
        private double windmodeltop = 0.0;
        internal double WindModelTop
        {
            get => windmodeltop;
            set => windmodeltop = (HasAtmo && (value <= 0.0 || value >= Atmodepth)) ? Atmodepth : value;
        }
        private double WindLonOffset = 0.0;
        private double WindTimeOffset = 0.0;
        private double VerticalWindMultiplier = 1.0;
        
        internal bool HasWindData => (WindDataX != null && WindDataY != null && WindDataZ != null);

        internal List<FlowMap> Flowmaps;
        internal bool HasFlowmaps => Flowmaps != null && Flowmaps.Count > 0;

        internal bool HasWind => HasWindData || HasFlowmaps;

        private float[][,,] TempData;
        private double TempScaleFactor = double.NaN;
        private double TempTimeStep = double.NaN;
        private double tempmodeltop = 0.0;
        internal double TempModelTop
        {
            get => tempmodeltop;
            set => tempmodeltop = (HasAtmo && (value <= 0.0 || value >= Atmodepth)) ? Atmodepth : value;
        }
        private double TempLonOffset = 0.0;
        private double TempTimeOffset = 0.0;

        private List<OffsetMap> TempOffsetMaps;
        private List<MultiplierMap> TempSwingMultiplierMaps;

        internal bool HasTemperature => HasTemperatureData || HasTemperatureOffsetMaps || HasTemperatureSwingMaps;
        internal bool HasTemperatureData => TempData != null;
        internal bool HasTemperatureOffsetMaps => TempOffsetMaps != null && TempOffsetMaps.Count > 0;
        internal bool HasTemperatureSwingMaps => TempSwingMultiplierMaps != null && TempSwingMultiplierMaps.Count > 0;

        private bool blendtempwithstock = false;
        internal bool BlendTempWithStock => (HasTemperatureData && (HasTemperatureOffsetMaps || HasTemperatureSwingMaps)) || blendtempwithstock;
        internal double BlendTempFactor { get; private set; } = 0.5;

        private float[][,,] PressData;
        private double PressScaleFactor = double.NaN;
        private double PressTimeStep = double.NaN;
        private double pressmodeltop = 0.0;
        internal double PressModelTop
        {
            get => pressmodeltop;
            set => pressmodeltop = (HasAtmo && (value <= 0.0 || value >= Atmodepth)) ? Atmodepth : value;
        }
        private double PressLonOffset = 0.0;
        private double PressTimeOffset = 0.0;
        internal bool HasPressure => HasPressureData || HasPressureMaps;
        internal bool HasPressureData => PressData != null;
        internal bool HasPressureMaps => PressMultiplierMaps != null && PressMultiplierMaps?.Count > 0;

        private List<MultiplierMap> PressMultiplierMaps;
        private bool blendpresswithstock = false;
        internal bool BlendPressWithStock => (HasPressureData && HasPressureMaps) || blendpresswithstock;
        internal double BlendPressFactor { get; private set; } = 0.5;


        internal MCWS_BodyData(string body, CelestialBody bod)
        {
            Flowmaps = new List<FlowMap>();
            TempOffsetMaps = new List<OffsetMap>();
            TempSwingMultiplierMaps = new List<MultiplierMap>();
            PressMultiplierMaps = new List<MultiplierMap>();

            this.body = body;
            HasAtmo = bod.atmosphere;
            if (HasAtmo)
            {
                Atmodepth = bod.atmosphereDepth;
            }
        }

        internal int AddWindData(float[][,,] WindX, float[][,,] WindY, float[][,,] WindZ, double scalefactor, double timestep, double modeltop, double lonoffset, double vertmult, double timeoffset)
        {
            if (HasWindData)
            {
                Utils.LogWarning(string.Format("Wind data already exists for {0}.", body));
                return -1;
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
                VerticalWindMultiplier = vertmult;
                WindTimeOffset = timeoffset;
                Utils.LogInfo(string.Format("Successfully added Wind Data to {0}.", body));
                return 0;
            }
        }

        internal int AddTemperatureData(float[][,,] Temp, double scalefactor, double timestep, double modeltop, double lonoffset, double timeoffset, bool blendwithstock, double blendfactor)
        {
            if (HasTemperature)
            {
                Utils.LogWarning(string.Format("Temperature data already exists for {0}.", body));
                return -1;
            }
            else
            {
                TempData = Temp;
                TempScaleFactor = scalefactor;
                TempTimeStep = timestep;
                TempModelTop = modeltop;
                TempLonOffset = lonoffset;
                TempTimeOffset = timeoffset;
                blendtempwithstock = blendwithstock;
                BlendTempFactor = blendfactor;
                Utils.LogInfo(string.Format("Successfully added Temperature Data to {0}.", body));
                return 0;
            }
        }

        internal int AddPressureData(float[][,,] Press, double scalefactor, double timestep, double modeltop, double lonoffset, double timeoffset)
        {
            if (HasPressure)
            {
                Utils.LogWarning(string.Format("Pressure data already exists for {0}.", body));
                return -1;
            }
            else
            {
                PressData = Press;
                PressScaleFactor = scalefactor;
                PressTimeStep = timestep;
                PressModelTop = modeltop;
                PressLonOffset = lonoffset;
                PressTimeOffset = timeoffset;
                Utils.LogInfo(string.Format("Successfully added Pressure Data to {0}.", body));
                return 0;
            }
        }

        internal int AddFlowMap(FlowMap flowmap)
        {
            Flowmaps?.Add(flowmap);
            return Flowmaps == null ? -1 : 0;
        }

        internal int AddTempOffsetMap(OffsetMap map)
        {
            TempOffsetMaps?.Add(map);
            return TempOffsetMaps == null ? -1 : 0;
        }

        internal int AddTempSwingMap(MultiplierMap map)
        {
            TempSwingMultiplierMaps?.Add(map);
            return TempSwingMultiplierMaps == null ? -1 : 0;
        }

        internal int AddPressMap(MultiplierMap map)
        {
            PressMultiplierMaps?.Add(map);
            return PressMultiplierMaps == null ? -1 : 0;
        }

        internal int GetWind(double lon, double lat, double alt, double time, ref Vector3 winddatavector, ref Vector3 flowmapvector, ref DataInfo windinfo)
        {
            bool winddatagood = false;
            bool flowmapgood = false;
            if (HasWindData)
            {
                try
                {
                    double normalizedlon = UtilMath.WrapAround(lon + 360.0 - WindLonOffset, 0.0, 360.0) / 360.0;
                    double normalizedlat = (180.0 - (lat + 90.0)) / 180.0;
                    double normalizedalt = UtilMath.Clamp01(alt / WindModelTop);
                    int timeindex = UtilMath.WrapAround((int)Math.Floor((time + WindTimeOffset) / WindTimeStep), 0, WindDataX.GetLength(0));
                    int timeindex2 = (timeindex + 1) % WindDataX.GetLength(0);
                    //derive the locations of the data in the arrays

                    double mapx = UtilMath.WrapAround(normalizedlon * WindDataX[timeindex].GetLength(2), 0, WindDataX[timeindex].GetLength(2));
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
                    float FinalX = Mathf.Lerp(Mathf.Lerp(BottomPlaneX1, TopPlaneX1, (float)lerpz), Mathf.Lerp(BottomPlaneX2, TopPlaneX2, (float)lerpz), (float)lerpt);
                    float FinalY = Mathf.Lerp(Mathf.Lerp(BottomPlaneY1, TopPlaneY1, (float)lerpz), Mathf.Lerp(BottomPlaneY2, TopPlaneY2, (float)lerpz), (float)lerpt) * (float)VerticalWindMultiplier;
                    float FinalZ = Mathf.Lerp(Mathf.Lerp(BottomPlaneZ1, TopPlaneZ1, (float)lerpz), Mathf.Lerp(BottomPlaneZ2, TopPlaneZ2, (float)lerpz), (float)lerpt);

                    //Create the wind vector
                    winddatavector.x = FinalX;
                    winddatavector.y = FinalY;
                    winddatavector.z = FinalZ;
                    winddatagood = winddatavector.IsFinite();
                    windinfo.SetNew(x1, x2, lerpx, y1, y2, lerpy, z1, z2, lerpz, timeindex, timeindex2, lerpt, alt > windmodeltop);
                }
                catch
                {
                    winddatagood = false;
                }
            }
            
            if (HasFlowmaps)
            {
                try
                {
                    int count = Flowmaps.Count;
                    if (count > 0)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            FlowMap map = Flowmaps[i];
                            flowmapvector.Add(map.GetWindVec(lon, lat, alt, time, Atmodepth));
                        }
                        flowmapgood = flowmapvector.IsFinite();
                    }
                }
                catch
                {
                    flowmapgood = false;
                }
            }

            //if both winddata and flowmaps are present and the value is usable, add them together. otherwise, return whichever one is applicable.
            if (winddatagood && flowmapgood)
            {
                return 0;
            }
            else if (!winddatagood && flowmapgood)
            {
                return 2;
            }
            else if (winddatagood && !flowmapgood)
            {
                return 1;
            }
            else
            {
                return -1;
            }
        }
        internal int GetTemperature(double lon, double lat, double alt, double time, out double temp, ref DataInfo tempinfo)
        {
            temp = 0.0;
            if (TempData != null)
            {
                try
                {
                    double normalizedlon = UtilMath.WrapAround(lon + 360.0 - TempLonOffset, 0.0, 360.0) / 360.0;
                    double normalizedlat = (180.0 - (lat + 90.0)) / 180.0;
                    double normalizedalt = UtilMath.Clamp01(alt / TempModelTop);
                    int timeindex = UtilMath.WrapAround((int)Math.Floor((time + TempTimeOffset) / TempTimeStep), 0, TempData.GetLength(0));
                    int timeindex2 = (timeindex + 1) % TempData.GetLength(0);

                    //derive the locations of the data in the arrays
                    double mapx = UtilMath.WrapAround(normalizedlon * TempData[timeindex].GetLength(2), 0, TempData[timeindex].GetLength(2));
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
                        tempinfo.SetNew(x1, x2, lerpx, y1, y2, lerpy, z1, z2, lerpz, timeindex, timeindex2, lerpt, alt > TempModelTop);
                        return alt > TempModelTop ? 1 : 0;
                    }
                    return -2;
                }
                catch
                {
                    return -2;
                }
            }
            return -1;
        }

        internal int GetTemperatureOffset(double lon, double lat, double alt, double time, out double offset)
        {
            offset = 0.0;
            int count = TempOffsetMaps.Count;
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    offset += TempOffsetMaps[i].GetOffset(lon, lat, alt, time, Atmodepth);
                }
                return 0;
            }
            return -1;
        }
        internal int GetTemperatureSwingMultiplier(double lon, double lat, double alt, double time, out double swing)
        {
            swing = 1.0;
            int count = TempSwingMultiplierMaps.Count;
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    swing *= TempSwingMultiplierMaps[i].GetMultiplier(lon, lat, alt, time, Atmodepth);
                }
                return 0;
            }
            return -1;
        }

        internal int GetPressure(double lon, double lat, double alt, double time, out double press, ref DataInfo pressinfo)
        {
            press = 0.0;
            if (PressData != null)
            {
                try
                {
                    double normalizedlon = UtilMath.WrapAround(lon + 360.0 - PressLonOffset, 0.0, 360.0) / 360.0;
                    double normalizedlat = (180.0 - (lat + 90.0)) / 180.0;
                    double normalizedalt = UtilMath.Clamp01(alt / PressModelTop);
                    int timeindex = UtilMath.WrapAround((int)Math.Floor((time + PressTimeOffset) / PressTimeStep), 0, PressData.GetLength(0));
                    int timeindex2 = (timeindex + 1) % PressData.GetLength(0);

                    //derive the locations of the data in the arrays
                    double mapx = UtilMath.WrapAround(normalizedlon * PressData[timeindex].GetLength(2), 0.0, PressData[timeindex].GetLength(2));
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
                        pressinfo.SetNew(x1, x2, lerpx, y1, y2, lerpy, z1, z2, lerpz, timeindex, timeindex2, lerpt, alt > PressModelTop);
                        return alt > PressModelTop ? 1 : 0;
                    }
                    return -2;
                }
                catch
                {
                    return -2;
                }
            }
            return -1;
        }

        internal int GetPressureMultiplier(double lon, double lat, double alt, double time, out double multiplier)
        {
            multiplier = 1.0;
            int count = PressMultiplierMaps.Count;
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    multiplier *= PressMultiplierMaps[i].GetMultiplier(lon, lat, alt, time, Atmodepth);
                }
                return 0;
            }
            return -1;
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
            return -1;
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
            return -1;
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
            return -1;
        }
    }

    #region mapclasses
    internal class FlowMap
    {
        private Texture2D flowmap;
        private bool useThirdChannel = false; //whether or not to use the Blue channel to add a vertical component to the winds.
        private bool normalizealtitudecurves = false;
        private FloatCurve AltitudeSpeedMultCurve;
        private FloatCurve EW_AltitudeSpeedMultCurve;
        private FloatCurve NS_AltitudeSpeedMultCurve;
        private FloatCurve V_AltitudeSpeedMultCurve;
        private FloatCurve WindSpeedMultiplierTimeCurve;
        private float EWwind = 0.0f;
        private float NSwind = 0.0f;
        private float vWind = 0.0f;

        private bool canscroll = false;
        private double scrollperiod = 0.0;

        private float timeoffset = 0.0f;

        private int x = 0;
        private int y = 0;

        internal FlowMap(Texture2D path, bool use3rdChannel, FloatCurve altmult, FloatCurve ewaltmultcurve, FloatCurve nsaltmultcurve, FloatCurve valtmultcurve, float EWwind, float NSwind, float vWind, FloatCurve speedtimecurve, float offset, bool canscroll, double scrollperiod, bool normalizealtitudecurves)
        {
            flowmap = path;
            useThirdChannel = use3rdChannel;
            AltitudeSpeedMultCurve = altmult;
            EW_AltitudeSpeedMultCurve = ewaltmultcurve;
            NS_AltitudeSpeedMultCurve = nsaltmultcurve;
            V_AltitudeSpeedMultCurve = valtmultcurve;
            WindSpeedMultiplierTimeCurve = speedtimecurve;
            this.normalizealtitudecurves = normalizealtitudecurves;
            this.scrollperiod = scrollperiod;
            this.canscroll = scrollperiod != 0.0 && canscroll;
            this.EWwind = EWwind;
            this.NSwind = NSwind;
            this.vWind = vWind;
            timeoffset = WindSpeedMultiplierTimeCurve.maxTime != 0.0f ? UtilMath.WrapAround(offset, 0.0f, WindSpeedMultiplierTimeCurve.maxTime) : 0.0f;

            x = flowmap.width;
            y = flowmap.height;
        }

        internal Vector3 GetWindVec(double lon, double lat, double alt, double time, double atmodepth)
        {
            //AltitudeSpeedMultiplierCurve cannot go below 0.
            if (normalizealtitudecurves)
            {
                alt = atmodepth > 0.0 ? alt / atmodepth : 1.0;
            }

            float speedmult = Math.Max(AltitudeSpeedMultCurve.Evaluate((float)alt), 0.0f) * WindSpeedMultiplierTimeCurve.Evaluate((float)(time - timeoffset + WindSpeedMultiplierTimeCurve.maxTime) % WindSpeedMultiplierTimeCurve.maxTime);
            if (speedmult > 0.0f)
            {
                double scroll = canscroll ? ((time / scrollperiod) * 360.0) % 360.0 : 0.0;
                //adjust longitude so the center of the map is the prime meridian for the purposes of these calculations
                double mapx = ((UtilMath.WrapAround(lon + 630.0 - scroll, 0, 360) / 360.0) * x) - 0.5;
                double mapy = (((lat + 90.0) / 180.0) * y) - 0.5;
                double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));

                //locate the four nearby points, but don't go over the poles.
                int leftx = UtilMath.WrapAround((int)Math.Truncate(mapx), 0, x);
                int topy = Utils.Clamp((int)Math.Truncate(mapy), 0, y - 1);
                int rightx = UtilMath.WrapAround(leftx + 1, 0, x);
                int bottomy = Utils.Clamp(topy + 1, 0, y - 1);

                Color[] colors = new Color[4];
                double[] windx = new double[4];
                double[] windy = new double[4];
                double[] windz = new double[4];
                colors[0] = flowmap.GetPixel(leftx, topy);
                colors[1] = flowmap.GetPixel(rightx, topy);
                colors[2] = flowmap.GetPixel(leftx, bottomy);
                colors[3] = flowmap.GetPixel(rightx, bottomy);

                for (int i = 0; i < 4; i++)
                {
                    windx[i] = (colors[i].r * 2.0f) - 1.0f;
                    windz[i] = (colors[i].g * 2.0f) - 1.0f;
                    windy[i] = useThirdChannel ? (colors[i].b * 2.0f) - 1.0f : 0.0f;
                }
                double windvecx = UtilMath.Lerp(UtilMath.Lerp(windx[0], windx[1], lerpx), UtilMath.Lerp(windx[2], windx[3], lerpx), lerpy) * NSwind * NS_AltitudeSpeedMultCurve.Evaluate((float)alt) * speedmult;
                double windvecy = UtilMath.Lerp(UtilMath.Lerp(windy[0], windy[1], lerpx), UtilMath.Lerp(windy[2], windy[3], lerpx), lerpy) * vWind * V_AltitudeSpeedMultCurve.Evaluate((float)alt) * speedmult;
                double windvecz = UtilMath.Lerp(UtilMath.Lerp(windz[0], windz[1], lerpx), UtilMath.Lerp(windz[2], windz[3], lerpx), lerpy) * EWwind * EW_AltitudeSpeedMultCurve.Evaluate((float)alt) * speedmult;

                return new Vector3((float)windvecx, (float)windvecy, (float)windvecz);
            }
            return Vector3.zero;
        }
    }

    internal class OffsetMap
    {
        private Texture2D offsetMap;
        private double deformity = 0.0;
        private double offset = 0.0;

        private bool normalizealtitudecurve = false;
        private FloatCurve AltitudeMultiplierCurve;
        private FloatCurve TimeMultiplierCurve;
        private bool canscroll = false;
        private double scrollperiod = 0.0;

        private int x = 0;
        private int y = 0;

        internal OffsetMap(Texture2D offsetMap, double deformity, double offset, FloatCurve altitudeMultiplierCurve, FloatCurve timeMultiplierCurve, bool canscroll, double scrollperiod, bool normalizealtitudecurve)
        {
            this.offsetMap = offsetMap;
            this.deformity = deformity;
            this.offset = offset;
            AltitudeMultiplierCurve = altitudeMultiplierCurve;
            TimeMultiplierCurve = timeMultiplierCurve;
            this.scrollperiod = scrollperiod;
            this.canscroll = scrollperiod != 0.0 && canscroll;
            this.normalizealtitudecurve = normalizealtitudecurve;

            x = offsetMap.width;
            y = offsetMap.height;
        }

        internal double GetOffset(double lon, double lat, double alt, double time, double atmodepth)
        {
            if (normalizealtitudecurve)
            {
                alt = atmodepth > 0.0 ? alt / atmodepth : 0.0;
            }

            double multiplier = (double)(AltitudeMultiplierCurve.Evaluate((float)alt) * TimeMultiplierCurve.Evaluate((float)time % TimeMultiplierCurve.maxTime));
            if (double.IsFinite(multiplier) && multiplier != 0.0)
            {
                double scroll = canscroll ? ((time / scrollperiod) * 360.0) % 360.0 : 0.0;
                double mapx = ((UtilMath.WrapAround(lon + 630.0 - scroll, 0, 360) / 360.0) * x) - 0.5;
                double mapy = (((lat + 90.0) / 180.0) * y) - 0.5;
                double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));

                //locate the four nearby points, but don't go over the poles.
                int leftx = UtilMath.WrapAround((int)Math.Truncate(mapx), 0, x);
                int topy = Utils.Clamp((int)Math.Truncate(mapy), 0, y - 1);
                int rightx = UtilMath.WrapAround(leftx + 1, 0, x);
                int bottomy = Utils.Clamp(topy + 1, 0, y - 1);

                Color TopLeft = offsetMap.GetPixel(leftx, topy);
                Color TopRight = offsetMap.GetPixel(rightx, topy);
                Color BottomLeft = offsetMap.GetPixel(leftx, bottomy);
                Color BottomRight = offsetMap.GetPixel(rightx, bottomy);

                //get sum now, divide by 3 later
                double TL = TopLeft.r + TopLeft.g + TopLeft.b;
                double TR = TopRight.r + TopRight.g + TopRight.b;
                double BL = BottomLeft.r + BottomLeft.g + BottomLeft.b;
                double BR = BottomRight.r + BottomRight.g + BottomRight.b;

                double value = ((UtilMath.Lerp(UtilMath.Lerp(TL, TR, lerpx), UtilMath.Lerp(BL, BR, lerpx), lerpy) * deformity) + offset) * multiplier * 0.3333333333;
                return double.IsFinite(value) ? value : 0;
            }

            return 0.0;
        }
    }

    internal class MultiplierMap
    {
        private Texture2D multiplierMap;
        private double deformity = 0.0;
        private double offset = 0.0;

        private bool normalizealtitudecurve = false;
        private FloatCurve AltitudeMultiplierCurve;
        private FloatCurve TimeMultiplierCurve;
        private bool canscroll = false;
        private double scrollperiod = 0.0;

        private int x = 0;
        private int y = 0;

        internal MultiplierMap(Texture2D multiplierMap, double deformity, double offset, FloatCurve altitudeMultiplierCurve, FloatCurve timeMultiplierCurve, bool canscroll, double scrollperiod, bool normalizealtitudecurve)
        {
            this.multiplierMap = multiplierMap;
            this.deformity = Math.Max(-1.0, deformity);
            this.offset = Math.Max(-1.0, offset);
            AltitudeMultiplierCurve = altitudeMultiplierCurve;
            TimeMultiplierCurve = timeMultiplierCurve;
            this.scrollperiod = scrollperiod;
            this.canscroll = scrollperiod != 0.0 && canscroll;
            this.normalizealtitudecurve = normalizealtitudecurve;

            x = multiplierMap.width;
            y = multiplierMap.height;
        }

        internal double GetMultiplier(double lon, double lat, double alt, double time, double atmodepth)
        {
            if (normalizealtitudecurve)
            {
                alt = atmodepth > 0.0 ? alt / atmodepth : 0.0;
            }

            double multiplier = (double)(AltitudeMultiplierCurve.Evaluate((float)alt) * TimeMultiplierCurve.Evaluate((float)time % TimeMultiplierCurve.maxTime));
            if (double.IsFinite(multiplier) && multiplier != 0.0)
            {
                double scroll = canscroll ? ((time / scrollperiod) * 360.0) % 360.0 : 0.0;
                double mapx = ((UtilMath.WrapAround(lon + 630.0 - scroll, 0, 360) / 360.0) * x) - 0.5;
                double mapy = (((lat + 90.0) / 180.0) * y) - 0.5;
                double lerpx = UtilMath.Clamp01(mapx - Math.Truncate(mapx));
                double lerpy = UtilMath.Clamp01(mapy - Math.Truncate(mapy));

                //locate the four nearby points, but don't go over the poles.
                int leftx = UtilMath.WrapAround((int)Math.Truncate(mapx), 0, x);
                int topy = Utils.Clamp((int)Math.Truncate(mapy), 0, y - 1);
                int rightx = UtilMath.WrapAround(leftx + 1, 0, x);
                int bottomy = Utils.Clamp(topy + 1, 0, y - 1);

                Color TopLeft = multiplierMap.GetPixel(leftx, topy);
                Color TopRight = multiplierMap.GetPixel(rightx, topy);
                Color BottomLeft = multiplierMap.GetPixel(leftx, bottomy);
                Color BottomRight = multiplierMap.GetPixel(rightx, bottomy);

                //get sum now, divide by 3 later
                double TL = TopLeft.r + TopLeft.g + TopLeft.b;
                double TR = TopRight.r + TopRight.g + TopRight.b;
                double BL = BottomLeft.r + BottomLeft.g + BottomLeft.b;
                double BR = BottomRight.r + BottomRight.g + BottomRight.b;

                double value = ((UtilMath.Lerp(UtilMath.Lerp(TL, TR, lerpx), UtilMath.Lerp(BL, BR, lerpx), lerpy) * deformity) + offset) * multiplier * 0.3333333333;
                return double.IsFinite(value) ? Math.Max(1.0 + value, 0.0) : 1.0;
            }

            return 1.0;
        }
    }
    #endregion
}
