using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MCWS_BinFileReader
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class FileReader : MonoBehaviour
    {
        public static FileReader Instance {  get; private set; }

        internal Dictionary<string, BodyData> bodydata;
        internal static string GameDataPath => KSPUtil.ApplicationRootPath + "GameData/";
        private static readonly string[] acceptedotherstrings = { "temperature", "pressure", "spacer" };
        private static readonly string[] acceptedwindstrings = { "windx", "windy", "windz" };

        internal bool HasBody(string body) => bodydata != null && bodydata.ContainsKey(body);

        public FileReader()
        {
            if (Instance == null)
            {
                Utils.LogInfo("Initializing Binary File Reader for MCWS");
                Instance = this;
            }
            else
            {
                Utils.LogWarning("Destroying duplicate instance. Check your install for duplicate mod folders.");
                Destroy(this);
            }
        }

        void Start()
        {
            Utils.LogInfo("Loading configs");
            
            bodydata = new Dictionary<string, BodyData>();
            ConfigNode[] DataNodes = GameDatabase.Instance.GetConfigNodes("MCWS_DATA");
            foreach (ConfigNode node in DataNodes)
            {
                try
                {
                    string body = "";
                    if (!node.TryGetValue("body", ref body) || string.IsNullOrEmpty(body))
                    {
                        throw new ArgumentNullException("'body' key was not inputted or was an empty string.");
                    }
                    CelestialBody bod = FlightGlobals.GetBodyByName(body);
                    if (bod == null || !bod.atmosphere)
                    {
                        throw new ArgumentException(string.Format("Celestial Body {0} does not exist or does not have an atmosphere. Data will not be read to conserve memory.", body));
                    }

                    Utils.LogInfo(string.Format("Loading config for {0}.", body));
                    if (!bodydata.ContainsKey(body))
                    {
                        bodydata.Add(body, new BodyData(body));
                    }

                    int lon = 0;
                    int lat = 0;
                    int alt = 0;
                    int steps = 0;
                    double timestep = double.NaN;
                    

                    ConfigNode data = new ConfigNode();
                    if (node.TryGetNode("Combined_Data", ref data))
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
                                float scaleFactor = 1.0f;
                                data.TryGetValue("scaleFactor", ref scaleFactor);

                                int offset = 0;
                                data.TryGetValue("initialOffset", ref offset);

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
                                        if (components.Contains(readorder[i]))
                                        {
                                            throw new ArgumentException(string.Format("Duplicate value {0} was present in the readOrder list.", readorder[i]));
                                        }
                                        if (readorder[i] != "spacer")
                                        {
                                            components.Add(readorder[i]);
                                        }
                                    }

                                    float[][,,] windarrayx = new float[steps][,,];
                                    float[][,,] windarrayy = new float[steps][,,];
                                    float[][,,] windarrayz = new float[steps][,,];
                                    float[][,,] temparray = new float[steps][,,];
                                    float[][,,] pressarray = new float[steps][,,];

                                    using (BinaryReader reader = new BinaryReader(File.OpenRead(GameDataPath + path)))
                                    {
                                        if(offset > 0) //dispose of the initial offset
                                        {
                                            byte[] removeoffset = reader.ReadBytes(offset);
                                        }
                                        foreach (string line in readorder)
                                        {
                                            for (int i = 0; i < steps; i++)
                                            {
                                                float[,,] floatbuffer = new float[alt, lat, lon];
                                                byte[] bufferarray = reader.ReadBytes(lon * lat * alt * sizeof(float));
                                                Buffer.BlockCopy(bufferarray, 0, floatbuffer, 0, Buffer.ByteLength(bufferarray));
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
                    if (node.TryGetNode("Wind_Data", ref data))
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
                                data.TryGetValue("initialOffset", ref offset);

                                float scaleFactor = 1.0f;
                                data.TryGetValue("scaleFactor", ref scaleFactor);
                                if (lon >= 2 && lat >= 2 && alt >= 2 && steps >= 1 && timestep > 0.0 && scaleFactor >= 1.0f && offset >= 0)
                                {
                                    float[][,,] windarrayx = new float[steps][,,];
                                    float[][,,] windarrayy = new float[steps][,,];
                                    float[][,,] windarrayz = new float[steps][,,];

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
                                            using (BinaryReader reader = new BinaryReader(File.OpenRead(GameDataPath + path)))
                                            {
                                                if (offset > 0) //dispose of the initial offset
                                                {
                                                    byte[] removeoffset = reader.ReadBytes(offset);
                                                }
                                                foreach (string line in readorder)
                                                {
                                                    for (int i = 0; i < steps; i++)
                                                    {
                                                        float[,,] floatbuffer = new float[alt, lat, lon];
                                                        byte[] bufferarray = reader.ReadBytes(lon * lat * alt * sizeof(float));
                                                        Buffer.BlockCopy(bufferarray, 0, floatbuffer, 0, Buffer.ByteLength(bufferarray));
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
                                            using (BinaryReader reader = new BinaryReader(File.OpenRead(GameDataPath + pathx)))
                                            {
                                                if (offset > 0) //dispose of the initial offset
                                                {
                                                    byte[] removeoffset = reader.ReadBytes(offset);
                                                }
                                                for (int i = 0; i < steps; i++)
                                                {
                                                    float[,,] floatbuffer = new float[alt, lat, lon];
                                                    byte[] bufferarray = reader.ReadBytes(lon * lat * alt * sizeof(float));
                                                    Buffer.BlockCopy(bufferarray, 0, floatbuffer, 0, Buffer.ByteLength(bufferarray));
                                                    windarrayx[i] = floatbuffer;
                                                }
                                                reader.Close();
                                            }
                                            using (BinaryReader reader = new BinaryReader(File.OpenRead(GameDataPath + pathy)))
                                            {
                                                if (offset > 0) //dispose of the initial offset
                                                {
                                                    byte[] removeoffset = reader.ReadBytes(offset);
                                                }
                                                for (int i = 0; i < steps; i++)
                                                {
                                                    float[,,] floatbuffer = new float[alt, lat, lon];
                                                    byte[] bufferarray = reader.ReadBytes(lon * lat * alt * sizeof(float));
                                                    Buffer.BlockCopy(bufferarray, 0, floatbuffer, 0, Buffer.ByteLength(bufferarray));
                                                    windarrayy[i] = floatbuffer;
                                                }
                                                reader.Close();
                                            }
                                            using (BinaryReader reader = new BinaryReader(File.OpenRead(GameDataPath + pathz)))
                                            {
                                                if (offset > 0) //dispose of the initial offset
                                                {
                                                    byte[] removeoffset = reader.ReadBytes(offset);
                                                }
                                                for (int i = 0; i < steps; i++)
                                                {
                                                    float[,,] floatbuffer = new float[alt, lat, lon];
                                                    byte[] bufferarray = reader.ReadBytes(lon * lat * alt * sizeof(float));
                                                    Buffer.BlockCopy(bufferarray, 0, floatbuffer, 0, Buffer.ByteLength(bufferarray));
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
                    if (node.TryGetNode("Temperature_Data", ref data))
                    {
                        if (!bodydata[body].HasTemperature)
                        {
                            Utils.LogInfo(string.Format("Loading Temperature Data node for {0}.", body));
                            string path = "";

                            if (data.TryGetValue("path", ref path) && data.TryGetValue("sizeLon", ref lon) && data.TryGetValue("sizeLat", ref lat) && data.TryGetValue("sizeAlt", ref alt) &&
                                 data.TryGetValue("timesteps", ref steps) && data.TryGetValue("timestepLength", ref timestep) && !string.IsNullOrEmpty(path))
                            {
                                int offset = 0;
                                data.TryGetValue("initialOffset", ref offset);

                                float scaleFactor = 1.0f;
                                data.TryGetValue("scaleFactor", ref scaleFactor);
                                if (lon >= 2 && lat >= 2 && alt >= 2 && steps >= 1 && timestep > 0.0 && scaleFactor >= 1.0f && offset >= 0)
                                {
                                    float[][,,] temparray = new float[steps][,,];

                                    using (BinaryReader reader = new BinaryReader(File.OpenRead(GameDataPath + path)))
                                    {
                                        if (offset > 0) //dispose of the initial offset
                                        {
                                            byte[] removeoffset = reader.ReadBytes(offset);
                                        }
                                        for (int i = 0; i < steps; i++)
                                        {
                                            float[,,] floatbuffer = new float[alt, lat, lon];
                                            byte[] bufferarray = reader.ReadBytes(lon * lat * alt * sizeof(float));
                                            Buffer.BlockCopy(bufferarray, 0, floatbuffer, 0, Buffer.ByteLength(bufferarray));
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
                    if (node.TryGetNode("Pressure_Data", ref data))
                    {
                        if (bodydata[body].HasPressure)
                        {
                            Utils.LogInfo(string.Format("Loading Pressure Data node for {0}.", body));
                            string path = "";

                            if (data.TryGetValue("path", ref path) && data.TryGetValue("sizeLon", ref lon) && data.TryGetValue("sizeLat", ref lat) && data.TryGetValue("sizeAlt", ref alt) &&
                                 data.TryGetValue("timesteps", ref steps) && data.TryGetValue("timestepLength", ref timestep) && !string.IsNullOrEmpty(path))
                            {
                                int offset = 0;
                                data.TryGetValue("initialOffset", ref offset);

                                float scaleFactor = 1.0f;
                                data.TryGetValue("scaleFactor", ref scaleFactor);
                                if (lon >= 2 && lat >= 2 && alt >= 2 && steps >= 1 && timestep > 0.0 && scaleFactor >= 1.0f && offset >= 0)
                                {
                                    float[][,,] pressarray = new float[steps][,,];

                                    using (BinaryReader reader = new BinaryReader(File.OpenRead(GameDataPath + path)))
                                    {
                                        if (offset > 0) //dispose of the initial offset
                                        {
                                            byte[] removeoffset = reader.ReadBytes(offset);
                                        }
                                        for (int i = 0; i < steps; i++)
                                        {
                                            float[,,] floatbuffer = new float[alt, lat, lon];
                                            byte[] bufferarray = reader.ReadBytes(lon * lat * alt * sizeof(float));
                                            Buffer.BlockCopy(bufferarray, 0, floatbuffer, 0, Buffer.ByteLength(bufferarray));
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
                }
                catch (Exception ex)
                {
                    Utils.LogError("Exception thrown when loading config: " + ex.ToString());
                }
            }
            Utils.LogInfo("All configs loaded. Performing cleanup.");

            //clean up BodyData objects with no data in them.
            List<string> todelete = new List<string>();
            foreach (KeyValuePair<string, BodyData> pair in bodydata)
            {
                if (!pair.Value.HasWind && !pair.Value.HasTemperature && !pair.Value.HasPressure)
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
            Utils.LogInfo("Initialization Complete.");
            //ensure this thing exists to be registered.
            DontDestroyOnLoad(this);
        }
    }
}
