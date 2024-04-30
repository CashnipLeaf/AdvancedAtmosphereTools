using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MCWS_ExoPlaSimReader
{
    using PropertyDelegate = Func<string, double, float[,,]>; //body, time, global property data (return value)

    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    internal class MCWS_Register_ExoPlaSimData : MonoBehaviour
    {
        public static MCWS_Register_ExoPlaSimData Instance { get; private set; }

        private const string ModName = "MCWS ExoPlaSim File Reader";
        private static ExoPlaSim_FileReader Data => ExoPlaSim_FileReader.Instance;

        public MCWS_Register_ExoPlaSimData()
        {
            if(Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
            }
        }

        void Start()
        {
            Utils.LogInfo("Registering with MCWS");
            try
            {
                Type MCWS = null;
                foreach (var assembly in AssemblyLoader.loadedAssemblies)
                {
                    if (assembly.name == "ModularClimateWeatherSystems")
                    {
                        var types = assembly.assembly.GetExportedTypes();
                        foreach (var type in types)
                        {
                            if (type.FullName == "ModularClimateWeatherSystems.MCWS_API")
                            {
                                MCWS = type;
                            }
                        }
                    }
                }
                if (MCWS != null)
                {
                    MethodInfo wind = MCWS.GetMethod("RegisterWindData");
                    MethodInfo temp = MCWS.GetMethod("RegisterTemperatureData");
                    MethodInfo press = MCWS.GetMethod("RegisterPressureData");

                    foreach (KeyValuePair<string, ExoPlaSim_BodyData> pair in Data.bodydata)
                    {
                        string body = pair.Key;
                        Utils.LogInfo(string.Format("Registering Data for {0} with MCWS.", body));
                        
                        try
                        {
                            if (wind != null && pair.Value.HasWind)
                            {
                                Utils.LogInfo(string.Format("Registering Wind Data for {0}.", body));
                                PropertyDelegate windX = GetWindX;
                                PropertyDelegate windY = GetWindY;
                                PropertyDelegate windZ = GetWindZ;
                                _ = wind.Invoke(null, new object[] { body, windX, windY, windZ, ModName, pair.Value.WindScaleFactor, pair.Value.WindTimeStep });
                            }
                        }
                        catch (Exception ex)
                        {
                            Utils.LogError("Exception thrown when registering Wind Data with MCWS: " + ex.ToString());
                        }

                        try
                        {
                            if (temp != null && pair.Value.HasTemperature)
                            {
                                Utils.LogInfo(string.Format("Registering Temperature Data for {0}.", body));
                                _ = temp.Invoke(null, new object[] { body, (PropertyDelegate)GetTemp, ModName, pair.Value.TemperatureScaleFactor, pair.Value.TemperatureTimeStep });
                            }
                        }
                        catch (Exception ex)
                        {
                            Utils.LogError("Exception thrown when registering Temperature Data with MCWS: " + ex.ToString());
                        }

                        try
                        {
                            if (press != null && pair.Value.HasPressure)
                            {
                                Utils.LogInfo(string.Format("Registering Pressure Data for {0}.", body));
                                _ = press.Invoke(null, new object[] { body, (PropertyDelegate)GetPress, ModName, pair.Value.PressureScaleFactor, pair.Value.PressureTimeStep });
                            }
                        }
                        catch (Exception ex)
                        {
                            Utils.LogError("Exception thrown when registering Pressure Data with MCWS: " + ex.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.LogError("Exception thrown when registering with MCWS: " + ex.ToString());
            }
        }

        public static bool CanGetBody(string body) => Data != null && Data.HasBody(body);

        public float[,,] GetWindX(string body, double time) => CanGetBody(body) ? Data.bodydata[body].GetWindX(time) : null;
        public float[,,] GetWindY(string body, double time) => CanGetBody(body) ? Data.bodydata[body].GetWindY(time) : null;
        public float[,,] GetWindZ(string body, double time) => CanGetBody(body) ? Data.bodydata[body].GetWindZ(time) : null;
        public float[,,] GetTemp(string body, double time) => CanGetBody(body) ? Data.bodydata[body].GetTemperature(time) : null;
        public float[,,] GetPress(string body, double time) => CanGetBody(body) ? Data.bodydata[body].GetPressure(time) : null;
    }
}
