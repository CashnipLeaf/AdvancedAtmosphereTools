using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ModularClimateWeatherSystems
{
    partial class MCWS_Startup
    {
        private static readonly string[] acceptedotherstrings = { "temperature", "pressure", "spacer" };
        private static readonly string[] acceptedwindstrings = { "windx", "windy", "windz" };

        void ReadConfigs()
        {
            Utils.LogInfo("Loading configs");

            bodydata = new Dictionary<string, BodyData>();
            ConfigNode[] DataNodes = GameDatabase.Instance.GetConfigNodes("MCWS_DATA");
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

                Utils.LogInfo(string.Format("Loading config for {0}.", body));
                if (!bodydata.ContainsKey(body))
                {
                    bodydata.Add(body, new BodyData());
                }

                int lon = 0;
                int lat = 0;
                int alt = 0;
                int steps = 0;
                double timestep = double.NaN;

                ConfigNode data = new ConfigNode();
                if (node.TryGetNode("Combined_Data", ref data))
                {
                    try
                    {
                        if (!bodydata[body].HasWind && !bodydata[body].HasTemperature && !bodydata[body].HasPressure)
                        {
                            Utils.LogInfo(string.Format("Loading Combined Data node for {0}.", body));
                            string path = "";
                            string[] readorder = new string[1];
                            List<string> components = new List<string>();

                            if (data.TryGetValue("path", ref path) && data.TryGetValue("readOrder", ref readorder) && !string.IsNullOrEmpty(path) && data.TryGetValue("sizeLon", ref lon) &&
                                data.TryGetValue("sizeLat", ref lat) && data.TryGetValue("sizeAlt", ref alt) && data.TryGetValue("timesteps", ref steps) && data.TryGetValue("timestepLength", ref timestep))
                            {
                                int offset = 0;
                                float scaleFactor = 1.0f;
                                bool invertalt = false;
                                bool virtualtop = false;

                                data.TryGetValue("initialOffset", ref offset);
                                data.TryGetValue("scaleFactor", ref scaleFactor);
                                data.TryGetValue("invertAltitude", ref invertalt);
                                data.TryGetValue("virtualTopLayer", ref virtualtop);

                                if (lon >= 2 && lat >= 2 && alt >= 2 && steps >= 1 && timestep > 0.0 && scaleFactor >= 1.0f && readorder.Length > 1 && offset >= 0)
                                {
                                    for (int i = 0; i < readorder.Length; i++) //check the readOrder to make sure it is valid.
                                    {
                                        if (string.IsNullOrEmpty(readorder[i]))
                                        {
                                            throw new ArgumentNullException("A null or empty string was present in the readOrder list.");
                                        }
                                        readorder[i] = readorder[i].ToLower();
                                        if (!acceptedotherstrings.Contains(readorder[i]) && !acceptedwindstrings.Contains(readorder[i]))
                                        {
                                            throw new ArgumentException(string.Format("String {0} is not a valid value for the read order.", readorder[i]));
                                        }
                                        if (readorder[i] != "spacer" && components.Contains(readorder[i]))
                                        {
                                            throw new ArgumentException(string.Format("Duplicate value {0} was present in the readOrder list.", readorder[i]));
                                        }
                                        components.Add(readorder[i]);
                                    }

                                    float[][,,] windarrayx = new float[steps][,,];
                                    float[][,,] windarrayy = new float[steps][,,];
                                    float[][,,] windarrayz = new float[steps][,,];
                                    float[][,,] temparray = new float[steps][,,];
                                    float[][,,] pressarray = new float[steps][,,];
                                    int Blocksize = lon * lat * sizeof(float);

                                    using (BinaryReader reader = new BinaryReader(File.OpenRead(Utils.GameDataPath + path)))
                                    {
                                        if (offset > 0) //dispose of the initial offset
                                        {
                                            reader.ReadBytes(offset);
                                        }
                                        foreach (string line in readorder)
                                        {
                                            for (int i = 0; i < steps; i++)
                                            {
                                                float[,,] floatbuffer = new float[(virtualtop ? alt + 1 : alt), lat, lon];
                                                for (int j = 0; j < alt; j++)
                                                {
                                                    byte[] bufferarray = reader.ReadBytes(Blocksize);
                                                    Buffer.BlockCopy(bufferarray, 0, floatbuffer, Blocksize * (invertalt ? (alt - 1 - j) : j), Buffer.ByteLength(bufferarray));
                                                }
                                                if (virtualtop)
                                                {
                                                    Buffer.BlockCopy(floatbuffer, Blocksize * alt - 1, floatbuffer, Blocksize * alt, Blocksize); //add virtual top layer
                                                }
                                                switch (line)
                                                {
                                                    case "spacer":
                                                        break;
                                                    case "windx":
                                                        windarrayx[i] = floatbuffer;
                                                        break;
                                                    case "windy":
                                                        windarrayy[i] = floatbuffer;
                                                        break;
                                                    case "windz":
                                                        windarrayz[i] = floatbuffer;
                                                        break;
                                                    case "temperature":
                                                        temparray[i] = floatbuffer;
                                                        break;
                                                    case "pressure":
                                                        pressarray[i] = floatbuffer;
                                                        break;
                                                    default:
                                                        throw new ArgumentException(string.Format("String {0} is not a valid value for the read order.", readorder[i]));
                                                }
                                            }
                                        }
                                        reader.Close();
                                    }
                                    bodydata[body].AddWindData(windarrayx, windarrayy, windarrayz, scaleFactor, timestep);
                                    bodydata[body].AddTemperatureData(temparray, scaleFactor, timestep);
                                    bodydata[body].AddPressureData(pressarray, scaleFactor, timestep);
                                    Utils.LogInfo(string.Format("Successfully loaded Combined Data node for {0}.", body));
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
                        else
                        {
                            Utils.LogWarning(string.Format("Data already exists for {0}.", body));
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
                        if (!bodydata[body].HasWind)
                        {
                            Utils.LogInfo(string.Format("Loading Wind Data node for {0}.", body));
                            bool combined = false;
                            data.TryGetValue("combined", ref combined);

                            if (data.TryGetValue("sizeLon", ref lon) && data.TryGetValue("sizeLat", ref lat) && data.TryGetValue("sizeAlt", ref alt) &&
                                 data.TryGetValue("timesteps", ref steps) && data.TryGetValue("timestepLength", ref timestep))
                            {
                                int offset = 0;
                                float scaleFactor = 1.0f;
                                bool invertalt = false;
                                bool virtualtop = false;

                                data.TryGetValue("initialOffset", ref offset);
                                data.TryGetValue("scaleFactor", ref scaleFactor);
                                data.TryGetValue("invertAltitude", ref invertalt);
                                data.TryGetValue("virtualTopLayer", ref virtualtop);

                                if (lon >= 2 && lat >= 2 && alt >= 2 && steps >= 1 && timestep > 0.0 && scaleFactor >= 1.0f && offset >= 0)
                                {
                                    float[][,,] windarrayx = new float[steps][,,];
                                    float[][,,] windarrayy = new float[steps][,,];
                                    float[][,,] windarrayz = new float[steps][,,];
                                    int Blocksize = lon * lat * sizeof(float);

                                    if (combined)
                                    {
                                        string path = "";
                                        string[] readorder = new string[3];
                                        string[] components = new string[3];

                                        if (data.TryGetValue("path", ref path) && data.TryGetValue("readOrder", ref readorder) && !string.IsNullOrEmpty(path) && readorder.Length == 3)
                                        {
                                            for (int i = 0; i < 3; i++) //check the readOrder to make sure it is valid.
                                            {
                                                if (string.IsNullOrEmpty(readorder[i]))
                                                {
                                                    throw new ArgumentNullException("A null or empty string was present in the readOrder list.");
                                                }
                                                readorder[i] = readorder[i].ToLower();
                                                if (!acceptedwindstrings.Contains(readorder[i]))
                                                {
                                                    throw new ArgumentException(string.Format("String {0} is not a valid value for the Wind data read order.", readorder[i]));
                                                }
                                                if (components.Contains(readorder[i]))
                                                {
                                                    throw new ArgumentException(string.Format("Duplicate value {0} was present in the Wind Data readOrder list.", readorder[i]));
                                                }
                                                components[i] = readorder[i];
                                            }
                                            using (BinaryReader reader = new BinaryReader(File.OpenRead(Utils.GameDataPath + path)))
                                            {
                                                if (offset > 0) //dispose of the initial offset
                                                {
                                                    reader.ReadBytes(offset);
                                                }
                                                foreach (string line in readorder)
                                                {
                                                    for (int i = 0; i < steps; i++)
                                                    {
                                                        float[,,] floatbuffer = new float[(virtualtop ? alt + 1 : alt), lat, lon];
                                                        for (int j = 0; j < alt; j++)
                                                        {
                                                            byte[] bufferarray = reader.ReadBytes(Blocksize);
                                                            Buffer.BlockCopy(bufferarray, 0, floatbuffer, Blocksize * (invertalt ? (alt - 1 - j) : j), Buffer.ByteLength(bufferarray));
                                                        }
                                                        if (virtualtop)
                                                        {
                                                            Buffer.BlockCopy(floatbuffer, Blocksize * alt - 1, floatbuffer, Blocksize * alt, Blocksize);
                                                        }
                                                        switch (line)
                                                        {
                                                            case "windx":
                                                                windarrayx[i] = floatbuffer;
                                                                break;
                                                            case "windy":
                                                                windarrayy[i] = floatbuffer;
                                                                break;
                                                            case "windz":
                                                                windarrayz[i] = floatbuffer;
                                                                break;
                                                            default:
                                                                throw new ArgumentException(string.Format("String {0} is not a valid value for the read order.", readorder[i]));
                                                        }
                                                    }
                                                }
                                                reader.Close();
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
                                            using (BinaryReader reader = new BinaryReader(File.OpenRead(Utils.GameDataPath + pathx)))
                                            {
                                                if (offset > 0) //dispose of the initial offset
                                                {
                                                    reader.ReadBytes(offset);
                                                }
                                                for (int i = 0; i < steps; i++)
                                                {
                                                    float[,,] floatbuffer = new float[(virtualtop ? alt + 1 : alt), lat, lon];
                                                    for (int j = 0; j < alt; j++)
                                                    {
                                                        byte[] bufferarray = reader.ReadBytes(Blocksize);
                                                        Buffer.BlockCopy(bufferarray, 0, floatbuffer, Blocksize * (invertalt ? (alt - 1 - j) : j), Buffer.ByteLength(bufferarray));
                                                    }
                                                    if (virtualtop)
                                                    {
                                                        Buffer.BlockCopy(floatbuffer, Blocksize * alt - 1, floatbuffer, Blocksize * alt, Blocksize); //add virtual top layer
                                                    }
                                                    windarrayx[i] = floatbuffer;
                                                }
                                                reader.Close();
                                            }
                                            using (BinaryReader reader = new BinaryReader(File.OpenRead(Utils.GameDataPath + pathy)))
                                            {
                                                if (offset > 0) //dispose of the initial offset
                                                {
                                                    reader.ReadBytes(offset);
                                                }
                                                for (int i = 0; i < steps; i++)
                                                {
                                                    float[,,] floatbuffer = new float[(virtualtop ? alt + 1 : alt), lat, lon];
                                                    for (int j = 0; j < alt; j++)
                                                    {
                                                        byte[] bufferarray = reader.ReadBytes(Blocksize);
                                                        Buffer.BlockCopy(bufferarray, 0, floatbuffer, Blocksize * (invertalt ? (alt - 1 - j) : j), Buffer.ByteLength(bufferarray));
                                                    }
                                                    if (virtualtop)
                                                    {
                                                        Buffer.BlockCopy(floatbuffer, Blocksize * alt - 1, floatbuffer, Blocksize * alt, Blocksize); //add virtual top layer
                                                    }
                                                    windarrayy[i] = floatbuffer;
                                                }
                                                reader.Close();
                                            }
                                            using (BinaryReader reader = new BinaryReader(File.OpenRead(Utils.GameDataPath + pathz)))
                                            {
                                                if (offset > 0) //dispose of the initial offset
                                                {
                                                    reader.ReadBytes(offset);
                                                }
                                                for (int i = 0; i < steps; i++)
                                                {
                                                    float[,,] floatbuffer = new float[(virtualtop ? alt + 1 : alt), lat, lon];
                                                    for (int j = 0; j < alt; j++)
                                                    {
                                                        byte[] bufferarray = reader.ReadBytes(Blocksize);
                                                        Buffer.BlockCopy(bufferarray, 0, floatbuffer, Blocksize * (invertalt ? (alt - 1 - j) : j), Buffer.ByteLength(bufferarray));
                                                    }
                                                    if (virtualtop)
                                                    {
                                                        Buffer.BlockCopy(floatbuffer, Blocksize * alt - 1, floatbuffer, Blocksize * alt, Blocksize); //add virtual top layer
                                                    }
                                                    windarrayz[i] = floatbuffer;
                                                }
                                                reader.Close();
                                            }
                                        }
                                        else
                                        {
                                            throw new ArgumentNullException("One or more file paths were not present or were empty strings.");
                                        }
                                    }
                                    bodydata[body].AddWindData(windarrayx, windarrayy, windarrayz, scaleFactor, timestep);
                                    Utils.LogInfo(string.Format("Successfully loaded Wind Data node for {0}.", body));
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
                        else
                        {
                            Utils.LogWarning(string.Format("Wind Data already exists for {0}.", body));
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
                        if (!bodydata[body].HasTemperature)
                        {
                            Utils.LogInfo(string.Format("Loading Temperature Data node for {0}.", body));
                            string path = "";

                            if (data.TryGetValue("path", ref path) && data.TryGetValue("sizeLon", ref lon) && data.TryGetValue("sizeLat", ref lat) && data.TryGetValue("sizeAlt", ref alt) &&
                                 data.TryGetValue("timesteps", ref steps) && data.TryGetValue("timestepLength", ref timestep) && !string.IsNullOrEmpty(path))
                            {
                                int offset = 0;
                                float scaleFactor = 1.0f;
                                bool invertalt = false;
                                bool virtualtop = false;

                                data.TryGetValue("initialOffset", ref offset);
                                data.TryGetValue("scaleFactor", ref scaleFactor);
                                data.TryGetValue("invertAltitude", ref invertalt);
                                data.TryGetValue("virtualTopLayer", ref virtualtop);

                                if (lon >= 2 && lat >= 2 && alt >= 2 && steps >= 1 && timestep > 0.0 && scaleFactor >= 1.0f && offset >= 0)
                                {
                                    float[][,,] temparray = new float[steps][,,];
                                    int Blocksize = lon * lat * sizeof(float);

                                    using (BinaryReader reader = new BinaryReader(File.OpenRead(Utils.GameDataPath + path)))
                                    {
                                        if (offset > 0) //dispose of the initial offset
                                        {
                                            reader.ReadBytes(offset);
                                        }
                                        for (int i = 0; i < steps; i++)
                                        {
                                            float[,,] floatbuffer = new float[(virtualtop ? alt + 1 : alt), lat, lon];
                                            for (int j = 0; j < alt; j++)
                                            {
                                                byte[] bufferarray = reader.ReadBytes(Blocksize);
                                                Buffer.BlockCopy(bufferarray, 0, floatbuffer, Blocksize * (invertalt ? (alt - 1 - j) : j), Buffer.ByteLength(bufferarray));
                                            }
                                            if (virtualtop)
                                            {
                                                Buffer.BlockCopy(floatbuffer, Blocksize * alt - 1, floatbuffer, Blocksize * alt, Blocksize); //add virtual all-nearly zeroes layer
                                            }
                                            temparray[i] = floatbuffer;
                                        }
                                        reader.Close();
                                    }
                                    bodydata[body].AddTemperatureData(temparray, scaleFactor, timestep);
                                    Utils.LogInfo(string.Format("Successfully loaded Temperature Data node for {0}.", body));
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
                        else
                        {
                            Utils.LogWarning(string.Format("Temperature Data already exists for {0}.", body));
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
                        if (bodydata[body].HasPressure)
                        {
                            Utils.LogInfo(string.Format("Loading Pressure Data node for {0}.", body));
                            string path = "";

                            if (data.TryGetValue("path", ref path) && data.TryGetValue("sizeLon", ref lon) && data.TryGetValue("sizeLat", ref lat) && data.TryGetValue("sizeAlt", ref alt) &&
                                 data.TryGetValue("timesteps", ref steps) && data.TryGetValue("timestepLength", ref timestep) && !string.IsNullOrEmpty(path))
                            {
                                int offset = 0;
                                float scaleFactor = 1.0f;
                                bool invertalt = false;
                                bool virtualtop = false;

                                data.TryGetValue("initialOffset", ref offset);
                                data.TryGetValue("scaleFactor", ref scaleFactor);
                                data.TryGetValue("invertAltitude", ref invertalt);
                                data.TryGetValue("virtualTopLayer", ref virtualtop);

                                if (lon >= 2 && lat >= 2 && alt >= 2 && steps >= 1 && timestep > 0.0 && scaleFactor >= 1.0f && offset >= 0)
                                {
                                    float[][,,] pressarray = new float[steps][,,];
                                    int Blocksize = lon * lat * sizeof(float);

                                    using (BinaryReader reader = new BinaryReader(File.OpenRead(Utils.GameDataPath + path)))
                                    {
                                        if (offset > 0) //dispose of the initial offset
                                        {
                                            reader.ReadBytes(offset);
                                        }
                                        for (int i = 0; i < steps; i++)
                                        {
                                            float[,,] floatbuffer = new float[(virtualtop ? alt + 1 : alt), lat, lon];
                                            for (int j = 0; j < alt; j++)
                                            {
                                                byte[] bufferarray = reader.ReadBytes(Blocksize);
                                                Buffer.BlockCopy(bufferarray, 0, floatbuffer, Blocksize * (invertalt ? (alt - 1 - j) : j), Buffer.ByteLength(bufferarray));
                                            }
                                            if (virtualtop)
                                            {
                                                float[] zeroes = Enumerable.Repeat(float.Epsilon * 2, lat * lon).ToArray();
                                                Buffer.BlockCopy(zeroes, 0, floatbuffer, Blocksize * (alt), Blocksize); //add virtual all-nearly zeroes layer
                                            }
                                            pressarray[i] = floatbuffer;
                                        }
                                        reader.Close();
                                    }
                                    bodydata[body].AddPressureData(pressarray, scaleFactor, timestep);
                                    Utils.LogInfo(string.Format("Successfully loaded Pressure Data node for {0}.", body));
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
                        else
                        {
                            Utils.LogWarning(string.Format("Pressure Data already exists for {0}.", body));
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
                    Utils.LogInfo("Loading Flowmap Objects for " + body);
                    foreach (ConfigNode flowmap in flowmaps)
                    {
                        try
                        {
                            bodydata[body].AddFlowMap(ReadFlowMapNode(flowmap));
                        }
                        catch (Exception ex)
                        {
                            Utils.LogWarning("Unable to load Flowmap object: " + ex.Message);
                        }
                    }
                }
            }
            Utils.LogInfo("All configs loaded. Performing cleanup.");

            //clean up BodyData objects with no data in them.
            List<string> todelete = new List<string>();
            foreach (KeyValuePair<string, BodyData> pair in bodydata)
            {
                if (!pair.Value.HasWind && !pair.Value.HasTemperature && !pair.Value.HasPressure && !pair.Value.HasFlowmaps)
                {
                    todelete.Add(pair.Key);
                }
            }
            foreach (string deleteme in todelete)
            {
                Utils.LogInfo(string.Format("Removing empty data object for body {0}.", deleteme));
                bodydata.Remove(deleteme);
            }
            Utils.LogInfo("Cleanup Complete.");
        }

        internal FlowMap ReadFlowMapNode(ConfigNode cn)
        {
            bool thirdchannel = false;
            float minalt = 0.0f; //support wind currents affecting splashed craft
            float maxalt = 1000000000.0f; //1Gm. If you somehow have an atmosphere taller than this, you need professional help.
            float windSpeed = 0.0f;
            float EWwind = 0.0f;
            float NSwind = 0.0f;
            float vWind = 0.0f;
            string path = "";
            bool curveExists;

            ConfigNode floaty = new ConfigNode();

            cn.TryGetValue("useThirdChannel", ref thirdchannel);
            cn.TryGetValue("minAlt", ref minalt);
            cn.TryGetValue("maxAlt", ref maxalt);
            cn.TryGetValue("windSpeed", ref windSpeed);
            if (!cn.TryGetValue("eastWestWindSpeed", ref EWwind))
            {
                EWwind = windSpeed;
            }
            if (!cn.TryGetValue("northSouthWindSpeed", ref NSwind))
            {
                NSwind = windSpeed;
            }
            if (!cn.TryGetValue("verticalWindSpeed", ref vWind))
            {
                vWind = windSpeed;
            }
            cn.TryGetValue("map", ref path);

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("Flowmap field 'map' cannot be empty");
            }
            if (!File.Exists(Utils.GameDataPath + path))
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
                minalt -= difference;
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

            byte[] fileData = File.ReadAllBytes(path);
            Texture2D flowmap = new Texture2D(2, 2);
            ImageConversion.LoadImage(flowmap, fileData);
            return new FlowMap(flowmap, thirdchannel, AltitudeSpeedMultCurve, EWAltMult, NSAltMult, VertAltMult, EWwind, NSwind, vWind, WindSpeedMultTimeCurve, offset);
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
                curve.Add(0, backup, 0, 0);
                curve.Add(10000, backup, 0, 0);
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
                curve.Add(min + lowerfade, 1.0f, 1.0f / (lowerfade - min), 0.0f);
                curve.Add(max - upperfade, 1.0f, 0.0f, -1.0f / (max - upperfade));
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
    }
}
