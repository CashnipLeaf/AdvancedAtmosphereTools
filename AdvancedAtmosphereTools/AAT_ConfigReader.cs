﻿using System;
using System.IO;
using UnityEngine;

namespace AdvancedAtmosphereTools
{
    partial class AAT_Startup
    {
        #region readconfigs
        void ReadConfigs(ConfigNode[] DataNodes, bool legacy) //why did I put myself through the pain of writing this?
        {
            foreach (ConfigNode node in DataNodes)
            {
                string body = "";
                if (!node.TryGetValue("body", ref body) || string.IsNullOrEmpty(body))
                {
                    Utils.LogWarning("'body' key was not inputted or was an empty string.");
                    continue;
                }
                CelestialBody bod = FlightGlobals.GetBodyByName(body);
                if (bod == null || !bod.atmosphere)
                {
                    Utils.LogWarning(string.Format("Celestial Body {0} does not exist or does not have an atmosphere. Data will not be read to conserve memory.", body));
                    continue;
                }

                Utils.LogInfo(legacy ? string.Format("Loading Legacy MCWS config for {0}.", body) : string.Format("Loading config for {0}.", body));

                //create a bodydata object if one doesnt already exist
                if (!bodydata.ContainsKey(body))
                {
                    bodydata.Add(body, new AAT_BodyData(body, bod));
                }

                bool istoxic = false;
                node.TryGetValue("atmosphereIsToxic", ref istoxic);
                bodydata[body].AtmosphereIsToxic = istoxic;
                string toxicmsg = "";
                if (node.TryGetValue("atmosphereIsToxicMessage", ref toxicmsg) && !string.IsNullOrEmpty(toxicmsg))
                {
                    bodydata[body].atmosphereIsToxicMessage = toxicmsg;
                }

                ConfigNode TApressuremultholder = new ConfigNode();
                if (node.TryGetNode("TrueAnomalyPressureMultiplierCurve", ref TApressuremultholder))
                {
                    FloatCurve fc = new FloatCurve();
                    fc.Load(TApressuremultholder);
                    bodydata[body].TrueAnomalyPressureMultiplierCurve = fc;
                }

                ConfigNode TAMolarMassOffsetholder = new ConfigNode();
                if (node.TryGetNode("TrueAnomalyMolarMassOffsetCurve", ref TAMolarMassOffsetholder))
                {
                    FloatCurve fc = new FloatCurve();
                    fc.Load(TAMolarMassOffsetholder);
                    bodydata[body].TrueAnomalyMolarMassOffsetCurve = fc;
                }

                double maxtempangleoffset = 45.0;
                if (node.TryGetValue("maxTempAngleOffset", ref maxtempangleoffset))
                {
                    Utils.LogInfo(string.Format("Applied Max Temp Angle Offset of {0:F1} to {1}", maxtempangleoffset, body));
                    bodydata[body].maxTempAngleOffset = maxtempangleoffset;
                }

                ConfigNode data = new ConfigNode();
                if (node.TryGetNode("Combined_Data", ref data))
                {
                    try
                    {
                        Utils.LogInfo(string.Format("Loading Combined Data node for {0}.", body));
                        string path = "";
                        string[] readorder = new string[1];
                        bool hascommons = ReadCommons(data, out int lon, out int lat, out int alt, out int steps, out double timestep);

                        if (hascommons && data.TryGetValue("path", ref path) && !string.IsNullOrEmpty(path) && data.TryGetValue("readOrder", ref readorder))
                        {
                            ReadOptionals(data, out int offset, out double scaleFactor, out bool invertalt, out double modelTop, out double lonoffset, out double vertmult, out double timeoffset, out bool doubleprecision, out bool blendWithStock, out double offsetmultiplier); //offsetmultiplier should be named blendwithstock, but im lazy and cba to change it

                            if (lon >= 2 && lat >= 2 && alt >= 2 && steps >= 1 && timestep > 0.0 && scaleFactor >= 1.0f && readorder.Length > 1 && offset >= 0)
                            {
                                float[][,,] windarrayx = new float[1][,,];
                                bool windx = false;
                                float[][,,] windarrayy = new float[1][,,];
                                bool windy = false;
                                float[][,,] windarrayz = new float[1][,,];
                                bool windz = false;

                                float[][][,,] dataarray = ReadCombinedFile(path, lon, lat, alt, steps, readorder.Length, offset, invertalt, doubleprecision);
                                for (int v = 0; v < readorder.Length; v++)
                                {
                                    string line = readorder[v].ToLower();
                                    switch (line)
                                    {
                                        case "windx":
                                            windarrayx = dataarray[v];
                                            windx = true;
                                            break;
                                        case "windy":
                                            windarrayy = dataarray[v];
                                            windy = true;
                                            break;
                                        case "windz":
                                            windarrayz = dataarray[v];
                                            windz = true;
                                            break;
                                        case "temperature":
                                            bodydata[body].AddTemperatureData(dataarray[v], scaleFactor, timestep, modelTop, lonoffset, timeoffset, blendWithStock, offsetmultiplier);
                                            break;
                                        case "pressure":
                                            bodydata[body].AddPressureData(dataarray[v], scaleFactor, timestep, modelTop, lonoffset, timeoffset);
                                            break;
                                        default:
                                            break; //skip over something that isnt data
                                    }
                                }
                                if (windx || windy || windz)
                                {
                                    if (windx && windy && windz) //only add wind data if all three components are present.
                                    {
                                        bodydata[body].AddWindData(windarrayx, windarrayy, windarrayz, scaleFactor, timestep, modelTop, lonoffset, vertmult, timeoffset);
                                    }
                                    else
                                    {
                                        Utils.LogWarning(string.Format("Unable to add Wind data to {0}. One or more components is missing.", body));
                                    }
                                }
                            }
                            else
                            {
                                throw new ArgumentOutOfRangeException("One or more of the inputted keys was outside the range of acceptable values.");
                            }
                        }
                        else
                        {
                            throw new ArgumentNullException("One or more keys was not present or was empty.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.LogError("Exception thrown when loading Combined_Data node for " + body + ": " + ex.ToString());
                    }
                }
                if (node.TryGetNode("Wind_Data", ref data))
                {
                    try
                    {
                        Utils.LogInfo(string.Format("Loading Wind Data node for {0}.", body));
                        bool combined = false;
                        data.TryGetValue("combined", ref combined);
                        bool hascommons = ReadCommons(data, out int lon, out int lat, out int alt, out int steps, out double timestep);

                        if (hascommons)
                        {
                            ReadOptionals(data, out int offset, out double scaleFactor, out bool invertalt, out double modelTop, out double lonoffset, out double vertmult, out double timeoffset, out bool doubleprecision, out bool garbage, out double garbage2);

                            if (lon >= 2 && lat >= 2 && alt >= 2 && steps >= 1 && timestep > 0.0 && scaleFactor >= 1.0f && offset >= 0)
                            {
                                float[][,,] windarrayx = new float[1][,,];
                                bool windx = false;
                                float[][,,] windarrayy = new float[1][,,];
                                bool windy = false;
                                float[][,,] windarrayz = new float[1][,,];
                                bool windz = false;

                                if (combined)
                                {
                                    string path = "";
                                    string[] readorder = new string[3];

                                    if (data.TryGetValue("path", ref path) && data.TryGetValue("readOrder", ref readorder) && !string.IsNullOrEmpty(path) && readorder.Length == 3)
                                    {
                                        float[][][,,] windarr = ReadCombinedFile(path, lon, lat, alt, steps, 3, offset, invertalt, doubleprecision);
                                        for (int v = 0; v < 3; v++)
                                        {
                                            string line = readorder[v].ToLower();
                                            switch (line)
                                            {
                                                case "windx":
                                                    windarrayx = windarr[v];
                                                    windx = true;
                                                    break;
                                                case "windy":
                                                    windarrayy = windarr[v];
                                                    windy = true;
                                                    break;
                                                case "windz":
                                                    windarrayz = windarr[v];
                                                    windz = true;
                                                    break;
                                                default:
                                                    break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        throw new ArgumentException("The 'path' or 'readOrder' key was not inputted or was empty, or the 'readOrder' was an invalid length.");
                                    }
                                }
                                else
                                {
                                    string pathx = "";
                                    string pathy = "";
                                    string pathz = "";

                                    if (data.TryGetValue("path_X", ref pathx) && data.TryGetValue("path_Y", ref pathy) && data.TryGetValue("path_Z", ref pathz) &&
                                        !string.IsNullOrEmpty(pathx) && !string.IsNullOrEmpty(pathy) && !string.IsNullOrEmpty(pathz))
                                    {
                                        windarrayx = ReadBinaryFile(pathx, lon, lat, alt, steps, offset, invertalt, doubleprecision);
                                        windarrayy = ReadBinaryFile(pathy, lon, lat, alt, steps, offset, invertalt, doubleprecision);
                                        windarrayz = ReadBinaryFile(pathz, lon, lat, alt, steps, offset, invertalt, doubleprecision);
                                        windx = windy = windz = true;
                                    }
                                    else
                                    {
                                        throw new ArgumentNullException("One or more file paths were not present or were empty strings.");
                                    }
                                }
                                if (windx && windy && windz)
                                {
                                    bodydata[body].AddWindData(windarrayx, windarrayy, windarrayz, scaleFactor, timestep, modelTop, lonoffset, vertmult, timeoffset);
                                }
                                else
                                {
                                    Utils.LogWarning(string.Format("Unable to add Wind data to {0}. One or more components is missing.", body));
                                }
                            }
                            else
                            {
                                throw new ArgumentOutOfRangeException("One or more of the inputted keys was outside the range of acceptable values.");
                            }
                        }
                        else
                        {
                            throw new ArgumentNullException("One or more keys was not present or was empty.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.LogError("Exception thrown when loading Wind_Data node for " + body + ": " + ex.ToString());
                    }
                }
                if (node.TryGetNode("Temperature_Data", ref data))
                {
                    try
                    {
                        Utils.LogInfo(string.Format("Loading Temperature Data node for {0}.", body));
                        string path = "";
                        bool hascommons = ReadCommons(data, out int lon, out int lat, out int alt, out int steps, out double timestep);

                        if (hascommons && data.TryGetValue("path", ref path) && !string.IsNullOrEmpty(path))
                        {
                            ReadOptionals(data, out int offset, out double scaleFactor, out bool invertalt, out double modelTop, out double lonoffset, out double vertmult, out double timeoffset, out bool doubleprecision, out bool blendWithStock, out double offsetmultiplier); //offsetmultiplier should be named blendwithstock, but im lazy and cba to change it

                            if (lon >= 2 && lat >= 2 && alt >= 2 && steps >= 1 && timestep > 0.0 && scaleFactor >= 1.0f && offset >= 0)
                            {
                                float[][,,] temparray = ReadBinaryFile(path, lon, lat, alt, steps, offset, invertalt, doubleprecision);
                                bodydata[body].AddTemperatureData(temparray, scaleFactor, timestep, modelTop, lonoffset, timeoffset, blendWithStock, offsetmultiplier);
                            }
                            else
                            {
                                throw new ArgumentOutOfRangeException("One or more of the inputted keys was outside the range of acceptable values.");
                            }
                        }
                        else
                        {
                            throw new ArgumentNullException("One or more keys was not present or was empty.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.LogError("Exception thrown when loading Temperature_Data node for " + body + ": " + ex.ToString());
                    }
                }
                if (node.TryGetNode("Pressure_Data", ref data))
                {
                    try
                    {
                        Utils.LogInfo(string.Format("Loading Pressure Data node for {0}.", body));
                        string path = "";

                        bool hascommons = ReadCommons(data, out int lon, out int lat, out int alt, out int steps, out double timestep);
                        if (hascommons && data.TryGetValue("path", ref path) && !string.IsNullOrEmpty(path))
                        {
                            ReadOptionals(data, out int offset, out double scaleFactor, out bool invertalt, out double modelTop, out double lonoffset, out double vertmult, out double timeoffset, out bool doubleprecision, out bool garbage, out double garbage2);

                            if (lon >= 2 && lat >= 2 && alt >= 2 && steps >= 1 && timestep > 0.0 && scaleFactor >= 1.0f && offset >= 0)
                            {
                                float[][,,] pressarray = ReadBinaryFile(path, lon, lat, alt, steps, offset, invertalt, doubleprecision);
                                bodydata[body].AddPressureData(pressarray, scaleFactor, timestep, modelTop, lonoffset, timeoffset);
                            }
                            else
                            {
                                throw new ArgumentOutOfRangeException("One or more of the inputted keys was outside the range of acceptable values.");
                            }
                        }
                        else
                        {
                            throw new ArgumentNullException("One or more keys was not present or was empty.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.LogError("Exception thrown when loading Pressure_Data node for " + body + ": " + ex.ToString());
                    }
                }

                ConfigNode[] flowmaps = node.GetNodes("Flowmap");
                if(flowmaps.Length > 0)
                {
                    Utils.LogInfo("Loading Flowmaps for " + body);
                    foreach (ConfigNode flowmap in flowmaps)
                    {
                        try
                        {
                            bodydata[body].Flowmaps?.Add(ReadFlowMapNode(flowmap, bod.atmosphereDepth));
                        }
                        catch (Exception ex)
                        {
                            Utils.LogWarning("Unable to load Flowmap: " + ex.Message);
                        }
                    }
                }

                //TODO: add parsing for new classes
                ConfigNode[] tempoffsetmaps = node.GetNodes("TemperatureOffsetMap");
                if (tempoffsetmaps.Length > 0)
                {
                    Utils.LogInfo("Loading TemperatureOffsetMaps for " + body);
                    foreach (ConfigNode tempoffsetmap in tempoffsetmaps)
                    {
                        try
                        {
                            ReadMapValues(tempoffsetmap, bod.atmosphereDepth, out Texture2D map, out double deformity, out double offset, out FloatCurve altmult, out FloatCurve timemult, out bool canscroll, out double scrollperiod, out FloatCurve trueanomalycurve);
                            bodydata[body].TempOffsetMaps?.Add(new OffsetMap(map, deformity, offset, altmult, timemult, canscroll, scrollperiod, trueanomalycurve));
                        }
                        catch (Exception ex)
                        {
                            Utils.LogWarning("Unable to load TemperatureOffsetMap: " + ex.Message);
                        }
                    }
                }

                ConfigNode[] tempswingmaps = node.GetNodes("TemperatureSwingMultiplierMap");
                if (tempswingmaps.Length > 0)
                {
                    Utils.LogInfo("Loading TemperatureSwingMultiplierMaps for " + body);
                    foreach (ConfigNode tempswingmap in tempswingmaps)
                    {
                        try
                        {
                            ReadMapValues(tempswingmap, bod.atmosphereDepth, out Texture2D map, out double deformity, out double offset, out FloatCurve altmult, out FloatCurve timemult, out bool canscroll, out double scrollperiod, out FloatCurve trueanomalycurve);
                            bodydata[body].TempSwingMultiplierMaps?.Add(new MultiplierMap(map, deformity, offset, altmult, timemult, canscroll, scrollperiod, trueanomalycurve));
                        }
                        catch (Exception ex)
                        {
                            Utils.LogWarning("Unable to load TemperatureSwingMultiplierMap: " + ex.Message);
                        }
                    }
                }

                ConfigNode[] pressmaps = node.GetNodes("PressureMultiplierMap");
                if (pressmaps.Length > 0)
                {
                    Utils.LogInfo("Loading PressureMultiplierMaps for " + body);
                    foreach (ConfigNode pressmap in pressmaps)
                    {
                        try
                        {
                            ReadMapValues(pressmap, bod.atmosphereDepth, out Texture2D map, out double deformity, out double offset, out FloatCurve altmult, out FloatCurve timemult, out bool canscroll, out double scrollperiod, out FloatCurve trueanomalycurve);
                            bodydata[body].PressMultiplierMaps?.Add(new MultiplierMap(map, deformity, offset, altmult, timemult, canscroll, scrollperiod, trueanomalycurve));
                        }
                        catch (Exception ex)
                        {
                            Utils.LogWarning("Unable to load PressureMultiplerMap: " + ex.Message);
                        }
                    }
                }

                ConfigNode molarmasscurveholder = new ConfigNode();
                if (node.TryGetNode("MolarMassCurve", ref molarmasscurveholder))
                {
                    Utils.LogInfo("Setting MolarMassCurve for " + body);
                    FloatCurve fc = new FloatCurve();
                    fc.Load(molarmasscurveholder);
                    if (bodydata[body].MolarMassCurve != null)
                    {
                        Utils.LogWarning(body + " already has a MolarMassCurve.");
                    }
                    else
                    {
                        bodydata[body].MolarMassCurve = fc;
                    }
                }

                ConfigNode[] molarmassmaps = node.GetNodes("MolarMassOffsetMap");
                if (molarmassmaps.Length > 0)
                {
                    Utils.LogInfo("Loading MolarMassOffsetMaps for " + body);
                    foreach (ConfigNode molarmassmap in molarmassmaps)
                    {
                        try
                        {
                            ReadMapValues(molarmassmap, bod.atmosphereDepth, out Texture2D map, out double deformity, out double offset, out FloatCurve altmult, out FloatCurve timemult, out bool canscroll, out double scrollperiod, out FloatCurve trueanomalycurve);
                            bodydata[body].MolarMassOffsetMaps?.Add(new OffsetMap(map, deformity, offset, altmult, timemult, canscroll, scrollperiod, trueanomalycurve));
                        }
                        catch (Exception ex)
                        {
                            Utils.LogWarning("Unable to load MolarMassOffsetMap: " + ex.Message);
                        }
                    }
                }

                ConfigNode adiabaticindexcurveholder = new ConfigNode();
                if (node.TryGetNode("AdiabaticIndexCurve", ref adiabaticindexcurveholder))
                {
                    Utils.LogInfo("Setting AdiabaticIndexCurve for " + body);
                    FloatCurve fc = new FloatCurve();
                    fc.Load(adiabaticindexcurveholder);
                    if (bodydata[body].AdiabaticIndexCurve != null)
                    {
                        Utils.LogWarning(body + " already has an AdiabaticIndexCurve.");
                    }
                    else
                    {
                        bodydata[body].AdiabaticIndexCurve = fc;
                    }
                }

                ConfigNode[] adiabaticindexmaps = node.GetNodes("AdiabaticIndexOffsetMap");
                if (adiabaticindexmaps.Length > 0)
                {
                    Utils.LogInfo("Loading AdiabaticIndexOffsetMaps for " + body);
                    foreach (ConfigNode adiabaticindexmap in adiabaticindexmaps)
                    {
                        try
                        {
                            ReadMapValues(adiabaticindexmap, bod.atmosphereDepth, out Texture2D map, out double deformity, out double offset, out FloatCurve altmult, out FloatCurve timemult, out bool canscroll, out double scrollperiod, out FloatCurve trueanomalycurve);
                            bodydata[body].AdiabaticIndexOffsetMaps?.Add(new OffsetMap(map, deformity, offset, altmult, timemult, canscroll, scrollperiod, trueanomalycurve));
                        }
                        catch (Exception ex)
                        {
                            Utils.LogWarning("Unable to load AdiabaticIndexOffsetMap: " + ex.Message);
                        }
                    }
                }
            }
        }
        #endregion

        #region binaryhelpers
        //read a binary file containing one kind of data
        internal float[][,,] ReadBinaryFile(string path, int lon, int lat, int alt, int steps, int offset, bool invertalt, bool doubleprecision)
        {
            int Blocksize = lon * lat * sizeof(float);
            float[][,,] newarray = new float[steps][,,];
            using (BinaryReader reader = new BinaryReader(File.OpenRead(Utils.GameDataPath + path)))
            {
                if (offset > 0) //eat the initial offset
                {
                    reader.ReadBytes(offset);
                }
                for (int i = 0; i < steps; i++)
                {
                    if(reader.BaseStream.Position >= reader.BaseStream.Length)
                    {
                        throw new EndOfStreamException("Attempted to read beyond the end of the file. Verify that your size, timestep, and doublePrecision parameters match that of the actual file.");
                    }
                    float[,,] floatbuffer = new float[alt, lat, lon];
                    for (int j = 0; j < alt; j++)
                    {
                        if (doubleprecision)
                        {
                            //support for double precision files.
                            //this will run more slowly than reading single-precision files, but too bad. not performance-critical anyways.
                            for (int y = 0; y < lat; y++)
                            {
                                for (int x = 0; x < lon; x++)
                                {
                                    floatbuffer[(invertalt ? (alt - 1 - j) : j), y, x] = (float)reader.ReadDouble();
                                }
                            }
                        }
                        else
                        {
                            byte[] bufferarray = reader.ReadBytes(Blocksize);
                            Buffer.BlockCopy(bufferarray, 0, floatbuffer, Blocksize * (invertalt ? (alt - 1 - j) : j), Buffer.ByteLength(bufferarray));
                        }
                    }
                    newarray[i] = floatbuffer;
                }
                reader.Close();
            }
            return newarray;
        }

        //read a binary file containing multiple kinds of data
        internal float[][][,,] ReadCombinedFile(string path, int lon, int lat, int alt, int steps, int numvars, int offset, bool invertalt, bool doubleprecision)
        {
            int Blocksize = lon * lat * sizeof(float);
            float[][][,,] newbigarray = new float[numvars][][,,];
            using (BinaryReader reader = new BinaryReader(File.OpenRead(Utils.GameDataPath + path)))
            {
                if (offset > 0) //nom the initial offset
                {
                    reader.ReadBytes(offset);
                }
                for (int n = 0; n < numvars; n++)
                {
                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                    {
                        throw new EndOfStreamException("Attempted to read beyond the end of the file. Verify that your size, timesteps, initialOffset, doublePrecision, and readOrder parameters match that of the actual file.");
                    }
                    float[][,,] newarray = new float[steps][,,];
                    for (int i = 0; i < steps; i++)
                    {
                        float[,,] floatbuffer = new float[alt, lat, lon];
                        for (int j = 0; j < alt; j++)
                        {
                            if (doubleprecision)
                            {
                                //support for double precision files.
                                //this will run more slowly than reading single-precision files, but too bad. not performance-critical anyways
                                for (int y = 0; y < lat; y++) 
                                {
                                    for (int x = 0; x < lon; x++)
                                    {
                                        floatbuffer[(invertalt ? (alt - 1 - j) : j), y, x] = (float)reader.ReadDouble();
                                    }
                                }
                            }
                            else
                            {
                                byte[] bufferarray = reader.ReadBytes(Blocksize);
                                Buffer.BlockCopy(bufferarray, 0, floatbuffer, Blocksize * (invertalt ? (alt - 1 - j) : j), Buffer.ByteLength(bufferarray));
                            }
                        }
                        newarray[i] = floatbuffer;
                    }
                    newbigarray[n] = newarray;
                }
                reader.Close();
            }
            return newbigarray;
        }

        //get the optional variables easily.
        internal void ReadOptionals(ConfigNode cn, out int offset, out double scaleFactor, out bool invertalt, out double modelTop, out double lonoffset, out double verticalwindmult, out double timeoffset, out bool doubleprecision, out bool blendWithStock, out double blendFactor) 
        {
            offset = 0;
            scaleFactor = 1.0;
            invertalt = false;
            modelTop = float.MaxValue;
            lonoffset = 0.0;
            verticalwindmult = 1.0;
            timeoffset = 0.0;
            doubleprecision = false;
            blendWithStock = false;
            blendFactor = 0.5;

            cn.TryGetValue("initialOffset", ref offset);
            cn.TryGetValue("scaleFactor", ref scaleFactor);
            cn.TryGetValue("invertAltitude", ref invertalt);
            cn.TryGetValue("modelTop", ref modelTop);
            cn.TryGetValue("longitudeOffset", ref lonoffset);
            cn.TryGetValue("verticalWindMultiplier", ref verticalwindmult);
            cn.TryGetValue("timeOffset", ref timeoffset);
            cn.TryGetValue("doublePrecision", ref  doubleprecision);
            cn.TryGetValue("blendTemperatureWithStock", ref blendWithStock);
            cn.TryGetValue("temperatureBlendFactor", ref blendFactor);
        }

        internal bool ReadCommons(ConfigNode cn, out int lon, out int lat, out int alt, out int steps, out double timestep)
        {
            lon = lat = alt = steps = 0; 
            timestep = 0.0; 
            return cn.TryGetValue("sizeLon", ref lon) && cn.TryGetValue("sizeLat", ref lat) && cn.TryGetValue("sizeAlt", ref alt) && cn.TryGetValue("timesteps", ref steps) && cn.TryGetValue("timestepLength", ref timestep);
        }
        #endregion

        #region maphelpers
        internal FlowMap ReadFlowMapNode(ConfigNode cn, double atmodepth)
        {
            bool thirdchannel = false;
            float minalt = 0.0f;
            float maxalt = (float)atmodepth; //DO NOT, I repeat, DO NOT **EVER** use float.MaxValue for this 
            float windSpeed = 0.0f;
            string path = "";
            bool curveExists;

            bool canscroll = false;
            double scrollperiod = 0.0;

            ConfigNode floaty = new ConfigNode();

            cn.TryGetValue("useThirdChannel", ref thirdchannel);
            cn.TryGetValue("minAlt", ref minalt);
            cn.TryGetValue("maxAlt", ref maxalt);
            cn.TryGetValue("windSpeed", ref windSpeed);
            float EWwind = windSpeed;
            float NSwind = windSpeed;
            float vWind = windSpeed;

            cn.TryGetValue("eastWestWindSpeed", ref EWwind);
            cn.TryGetValue("northSouthWindSpeed", ref NSwind);
            cn.TryGetValue("verticalWindSpeed", ref vWind);

            cn.TryGetValue("canScroll", ref canscroll);
            double period = float.MaxValue;
            if (cn.TryGetValue("scrollPeriod", ref period))
            {
                scrollperiod = period;
            }
            else if (cn.TryGetValue("scrollRate", ref period))
            {
                scrollperiod = 1 / (2 * Math.PI * period);
            }
            else if (cn.TryGetValue("scrollRateD", ref period))
            {
                scrollperiod = 1 / (360 * period);
            }

            if (!double.IsFinite(scrollperiod) || scrollperiod == 0.0)
            {
                canscroll = false; //failsafe to prevent things from breaking down
            }

            cn.TryGetValue("map", ref path);

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("Flowmap field 'map' cannot be empty");
            }
            string gdpath = Utils.GameDataPath + path;
            if (!File.Exists(gdpath))
            {
                throw new FileNotFoundException("Could not locate Flowmap at file path: " + path + " . Verify that the given file path is correct.");
            }
            if (minalt >= maxalt) //You do not get to break my mod, boi. >:3
            {
                throw new ArgumentException("maxAlt cannot be less than or equal to minAlt.");
            }

            float difference = Math.Min(1000.0f, (maxalt - minalt) / 10.0f);
            if (minalt == 0.0f)
            {
                minalt = -difference;
            }
            float lowerfade = minalt + difference;
            float upperfade = maxalt - difference;

            ConfigNode altrange = new ConfigNode();
            if (cn.TryGetNode("AltitudeRange", ref altrange))
            {
                altrange.TryGetValue("startStart", ref minalt);
                altrange.TryGetValue("endEnd", ref maxalt);
                if (minalt >= maxalt)
                {
                    throw new ArgumentException("Invalid AltitudeRange Node: endEnd cannot be less than or equal to startStart.");
                }

                //fallback if these things dont get entered
                difference = Math.Min(1000.0f, (maxalt - minalt) / 10.0f);
                if (!altrange.TryGetValue("startEnd", ref lowerfade))
                {
                    lowerfade = minalt + difference;
                }
                if (!altrange.TryGetValue("endStart", ref upperfade))
                {
                    upperfade = maxalt - difference;
                }
            }
            //Clamp startEnd and endStart to prevent weird floatcurve shenanigans
            upperfade = Mathf.Clamp(upperfade, minalt + 0.001f, maxalt - 0.001f);
            lowerfade = Mathf.Clamp(lowerfade, minalt + 0.001f, upperfade - 0.001f);

            ConfigNode timesettings = new ConfigNode();
            bool timeexists = cn.TryGetNode("TimeSettings", ref timesettings);
            float interval = 1000.0f;
            float duration = 500.0f;
            float fadein = 50.0f;
            float fadeout = 50.0f;
            float offset = 0.0f;
            if (timeexists)
            {
                timesettings.TryGetValue("interval", ref interval);
                timesettings.TryGetValue("duration", ref duration);
                timesettings.TryGetValue("fadeIn", ref fadein);
                timesettings.TryGetValue("fadeOut", ref fadeout);
                timesettings.TryGetValue("offset", ref offset);
            }

            curveExists = cn.TryGetNode("TimeSpeedMultiplierCurve", ref floaty);
            FloatCurve WindSpeedMultTimeCurve = CreateSpeedTimeCurve(floaty, curveExists, timeexists, interval, duration, fadein, fadeout);

            curveExists = cn.TryGetNode("AltitudeSpeedMultiplierCurve", ref floaty);
            FloatCurve AltitudeSpeedMultCurve = CreateAltitudeCurve(floaty, curveExists, minalt, maxalt, lowerfade, upperfade);

            curveExists = cn.TryGetNode("EastWestAltitudeSpeedMultiplierCurve", ref floaty);
            FloatCurve EWAltMult = CheckCurve(floaty, 1.0f, curveExists);

            curveExists = cn.TryGetNode("NorthSouthAltitudeSpeedMultiplierCurve", ref floaty);
            FloatCurve NSAltMult = CheckCurve(floaty, 1.0f, curveExists);

            curveExists = cn.TryGetNode("VerticalAltitudeSpeedMultiplierCurve", ref floaty);
            FloatCurve VertAltMult = CheckCurve(floaty, 1.0f, curveExists);

            curveExists = cn.TryGetNode("TrueAnomalySpeedMultiplierCurve", ref floaty);
            FloatCurve trueanomalycurve = CheckCurve(floaty, 1.0f, curveExists);

            Texture2D flowmap = CreateTexture(gdpath);
            return new FlowMap(flowmap, thirdchannel, AltitudeSpeedMultCurve, EWAltMult, NSAltMult, VertAltMult, EWwind, NSwind, vWind, WindSpeedMultTimeCurve, offset, canscroll, scrollperiod, trueanomalycurve);
        }

        internal void ReadMapValues(ConfigNode cn, double maxalt, out Texture2D map, out double deformity, out double offset, out FloatCurve altitudemultcurve, out FloatCurve timemultcurve, out bool canscroll, out double scrollperiod, out FloatCurve trueanomalycurve)
        {
            deformity = offset =  0.0;
            altitudemultcurve = new FloatCurve();
            timemultcurve = new FloatCurve();
            trueanomalycurve = new FloatCurve();
            canscroll = false;
            scrollperiod = float.MaxValue;

            string path = "";
            if (!cn.TryGetValue("map", ref path))
            {
                throw new ArgumentNullException("Map cannot be empty");
            }
            string gdpath = Utils.GameDataPath + path;
            if (!File.Exists(gdpath))
            {
                throw new FileNotFoundException("Could not locate Map at file path: " + path + " . Verify that the given file path is correct.");
            }
            map = CreateTexture(gdpath);

            ConfigNode floatcurveholder = new ConfigNode();
            bool hascurve = cn.TryGetNode("AltitudeMultiplierCurve", ref floatcurveholder);
            if (hascurve)
            {
                altitudemultcurve.Load(floatcurveholder);
            }
            else
            {
                altitudemultcurve.Add(0.0f, 1.0f, 0.0f, (float)(-1.0 / (maxalt * 0.3)));
                altitudemultcurve.Add((float)(maxalt * 0.3), 1.0f, (float)(-1.0 / (maxalt * 0.3)), 0.0f);
            }

            hascurve = cn.TryGetNode("TimeMultiplierCurve", ref floatcurveholder);
            timemultcurve = CheckCurve(floatcurveholder, 1.0f, hascurve);

            hascurve = cn.TryGetNode("TrueAnomalyMultiplierCurve", ref floatcurveholder);
            trueanomalycurve = CheckCurve(floatcurveholder, 1.0f, hascurve);


            cn.TryGetValue("deformity", ref deformity);
            cn.TryGetValue("offset", ref offset);
            cn.TryGetValue("canScroll", ref canscroll);
            double period = float.MaxValue;
            if (cn.TryGetValue("scrollPeriod", ref period))
            {
                scrollperiod = period;
            }
            else if(cn.TryGetValue("scrollRate", ref period))
            {
                scrollperiod = 1 / (2 * Math.PI * period);
            }
            else if(cn.TryGetValue("scrollRateD", ref period))
            {
                scrollperiod = 1 / (360 * period);
            }
            if (!double.IsFinite(scrollperiod) || scrollperiod == 0.0)
            {
                canscroll = false; //failsafe to prevent things from breaking down
            }
        }

        //Creates the float curve, or if one isnt available, converts a relevant float value into one.
        internal FloatCurve CheckCurve(ConfigNode node, float backup, bool saved)
        {
            FloatCurve curve = new FloatCurve();
            if (saved)
            {
                curve.Load(node);
            }
            else
            {
                curve.Add(0.0f, backup, 0.0f, 0.0f);
                curve.Add(10000.0f, backup, 0.0f, 0.0f);
            }
            return curve;
        }
        internal FloatCurve CreateAltitudeCurve(ConfigNode node, bool saved, float min, float max, float lowerfade, float upperfade)
        {
            FloatCurve curve = new FloatCurve();
            if (saved)
            {
                curve.Load(node);
            }
            else //generate a default AltitudeSpeedMultCurve with the inputted fade information.
            {
                curve.Add(min, 0.0f, 0.0f, 1.0f / (lowerfade - min));
                curve.Add(lowerfade, 1.0f, 1.0f / (lowerfade - min), 0.0f);
                curve.Add(upperfade, 1.0f, 0.0f, -1.0f / (max - upperfade));
                curve.Add(max, 0.0f, -1.0f / (max - upperfade), 0.0f);
            }
            return curve;
        }
        internal FloatCurve CreateSpeedTimeCurve(ConfigNode node, bool curveexists, bool nodeexists, float interval, float duration, float fadein, float fadeout)
        {
            FloatCurve curve = new FloatCurve();
            if (curveexists)
            {
                curve.Load(node);
            }
            else if (nodeexists)
            {
                curve.Add(0.0f, 0.0f, 0.0f, 1 / fadein);
                curve.Add(fadein, 1.0f, 1 / fadein, 0.0f);
                curve.Add(duration - fadeout, 1.0f, 0.0f, -1.0f / fadeout);
                curve.Add(duration, 0.0f, -1.0f / fadeout, 0.0f);
                curve.Add(interval, 0.0f, 0.0f, 0.0f);
            }
            else
            {
                curve.Add(0, 1.0f, 0, 0);
                curve.Add(1000, 1.0f, 0, 0);
            }
            return curve;
        }

        internal Texture2D CreateTexture(string path)
        {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            ImageConversion.LoadImage(tex, fileData);
            return tex;
        }
        #endregion
    }
}
