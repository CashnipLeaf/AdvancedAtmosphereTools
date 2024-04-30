using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MCWS_ExoPlaSimReader
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class ExoPlaSim_FileReader : MonoBehaviour
    {
        public static ExoPlaSim_FileReader Instance { get; private set; }

        internal Dictionary<string, ExoPlaSim_BodyData> bodydata;
        internal static string GameDataPath => KSPUtil.ApplicationRootPath + "GameData/";
        private const int lon = 64;
        private const int lat = 32;
        private const int offset = 128;
        private static int Blocksize => lon * lat * sizeof(float);

        internal bool HasBody(string body) => bodydata != null && bodydata.ContainsKey(body);

        public ExoPlaSim_FileReader()
        {
            if (Instance == null)
            {
                Utils.LogInfo("Initializing ExoPlaSim File Reader");
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
            bodydata = new Dictionary<string, ExoPlaSim_BodyData>();

            ConfigNode[] DataNodes = GameDatabase.Instance.GetConfigNodes("MCWS_EXOPLASIM_DATA");
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
                        bodydata.Add(body, new ExoPlaSim_BodyData(body));
                    }

                    int alt = 0;
                    int steps = 0;
                    double timestep = double.NaN;

                    ConfigNode data = new ConfigNode();
                    if (node.TryGetNode("Wind_Data", ref data))
                    {
                        if (!bodydata[body].HasWind)
                        {
                            Utils.LogInfo(string.Format("Loading Wind Data node for {0}.", body));

                            if (data.TryGetValue("sizeAlt", ref alt) && data.TryGetValue("timesteps", ref steps) && data.TryGetValue("timestepLength", ref timestep))
                            {
                                float scaleFactor = 1.0f;
                                data.TryGetValue("scaleFactor", ref scaleFactor);

                                int startstep = 0;
                                data.TryGetValue("startStep", ref startstep);
                                if (alt >= 2 && steps >= 1 && timestep > 0.0 && scaleFactor >= 1.0f && startstep >= 0 && startstep < steps)
                                {
                                    float[][,,] windarrayx = new float[steps][,,];
                                    float[][,,] windarrayy = new float[steps][,,];
                                    float[][,,] windarrayz = new float[steps][,,];

                                    string pathx = "";
                                    string pathy = "";
                                    string pathz = "";

                                    if (data.TryGetValue("path_X", ref pathx) && data.TryGetValue("path_Y", ref pathy) && data.TryGetValue("path_Z", ref pathz) &&
                                        !string.IsNullOrEmpty(pathx) && !string.IsNullOrEmpty(pathy) && !string.IsNullOrEmpty(pathz))
                                    {
                                        using (BinaryReader reader = new BinaryReader(File.OpenRead(GameDataPath + pathx)))
                                        {
                                            reader.ReadBytes(offset); //nom the header
                                            for (int i = 0; i < steps; i++)
                                            {
                                                float[,,] floatbuffer = new float[alt + 1, lat, lon];
                                                for (int j = alt - 1; j >= 0; j--)
                                                {
                                                    byte[] bufferarray = reader.ReadBytes(Blocksize);
                                                    Buffer.BlockCopy(bufferarray, 0, floatbuffer, Blocksize * j, Blocksize);
                                                }
                                                Buffer.BlockCopy(floatbuffer, Blocksize * (alt - 1), floatbuffer, Blocksize * alt, Blocksize); //add the virtual top layer

                                                windarrayx[(i + startstep) % steps] = floatbuffer;
                                            }
                                            reader.Close();
                                        }
                                        using (BinaryReader reader = new BinaryReader(File.OpenRead(GameDataPath + pathy)))
                                        {
                                            reader.ReadBytes(offset); //nom the header
                                            for (int i = 0; i < steps; i++)
                                            {
                                                float[,,] floatbuffer = new float[alt + 1, lat, lon];
                                                for (int j = alt - 1; j >= 0; j--)
                                                {
                                                    byte[] bufferarray = reader.ReadBytes(Blocksize);
                                                    Buffer.BlockCopy(bufferarray, 0, floatbuffer, Blocksize * j, Blocksize);
                                                }
                                                Buffer.BlockCopy(floatbuffer, Blocksize * (alt - 1), floatbuffer, Blocksize * alt, Blocksize); //add the virtual top layer
                                                windarrayy[(i+startstep) % steps] = floatbuffer;
                                            }
                                            reader.Close();
                                        }
                                        using (BinaryReader reader = new BinaryReader(File.OpenRead(GameDataPath + pathz)))
                                        {
                                            reader.ReadBytes(offset); //nom the header
                                            for (int i = 0; i < steps; i++)
                                            {
                                                float[,,] floatbuffer = new float[alt + 1, lat, lon];
                                                for (int j = alt - 1; j >= 0; j--)
                                                {
                                                    byte[] bufferarray = reader.ReadBytes(Blocksize);
                                                    Buffer.BlockCopy(bufferarray, 0, floatbuffer, Blocksize * j, Blocksize);
                                                }
                                                Buffer.BlockCopy(floatbuffer, Blocksize * (alt - 1), floatbuffer, Blocksize * alt, Blocksize); //add the virtual top layer
                                                windarrayz[(i + startstep) % steps] = floatbuffer;
                                            }
                                            reader.Close();
                                        }
                                    }
                                    else
                                    {
                                        throw new ArgumentNullException("One or more file paths were not present or were empty strings.");
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

                            if (data.TryGetValue("path", ref path) && data.TryGetValue("sizeAlt", ref alt) && data.TryGetValue("timesteps", ref steps) && data.TryGetValue("timestepLength", ref timestep) && !string.IsNullOrEmpty(path))
                            {
                                float scaleFactor = 1.0f;
                                data.TryGetValue("scaleFactor", ref scaleFactor);

                                int startstep = 0;
                                data.TryGetValue("startStep", ref startstep);
                                if (alt >= 2 && steps >= 1 && timestep > 0.0 && scaleFactor >= 1.0f && startstep >= 0 && startstep < steps)
                                {
                                    float[][,,] temparray = new float[steps][,,];

                                    using (BinaryReader reader = new BinaryReader(File.OpenRead(GameDataPath + path)))
                                    {
                                        reader.ReadBytes(offset); //nom the header
                                        for (int i = 0; i < steps; i++)
                                        {
                                            float[,,] floatbuffer = new float[alt + 1, lat, lon];
                                            for (int j = alt - 1; j >= 0; j--)
                                            {
                                                byte[] bufferarray = reader.ReadBytes(Blocksize);
                                                Buffer.BlockCopy(bufferarray, 0, floatbuffer, Blocksize * j, Blocksize);
                                            }
                                            Buffer.BlockCopy(floatbuffer, Blocksize * (alt - 1), floatbuffer, Blocksize * alt, Blocksize); //add the virtual top layer
                                            temparray[(i + startstep) % steps] = floatbuffer;
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

                            if (data.TryGetValue("path", ref path) && data.TryGetValue("sizeAlt", ref alt) && data.TryGetValue("timesteps", ref steps) && data.TryGetValue("timestepLength", ref timestep) && !string.IsNullOrEmpty(path))
                            {
                                float scaleFactor = 1.0f;
                                data.TryGetValue("scaleFactor", ref scaleFactor);

                                int startstep = 0;
                                data.TryGetValue("startStep", ref startstep);
                                if (alt >= 2 && steps >= 1 && timestep > 0.0 && scaleFactor >= 1.0f && startstep >= 0 && startstep < steps)
                                {
                                    float[][,,] pressarray = new float[steps][,,];
                                    using (BinaryReader reader = new BinaryReader(File.OpenRead(GameDataPath + path)))
                                    {
                                        reader.ReadBytes(offset); //nom the header
                                        for (int i = 0; i < steps; i++)
                                        {
                                            float[,,] floatbuffer = new float[alt + 1, lat, lon];
                                            for (int j = alt - 1; j >= 0; j--)
                                            {
                                                byte[] bufferarray = reader.ReadBytes(Blocksize);
                                                Buffer.BlockCopy(bufferarray, 0, floatbuffer, Blocksize * j, Blocksize);
                                            }
                                            float[] zeroes = Enumerable.Repeat(float.Epsilon * 8, lat * lon).ToArray();
                                            Buffer.BlockCopy(zeroes, 0, floatbuffer, Blocksize * (alt), Blocksize); //add virtual all-nearly zeroes layer
                                            pressarray[(i + startstep) % steps] = floatbuffer;
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
            foreach (KeyValuePair<string, ExoPlaSim_BodyData> pair in bodydata)
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
