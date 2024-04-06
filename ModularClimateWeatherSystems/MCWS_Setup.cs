using System;
using UnityEngine;
using ToolbarControl_NS;

namespace ModularClimateWeatherSystems
{
    //Sets up plugin settings.
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class MCWS_Setup : MonoBehaviour
    {
        public static MCWS_Setup Instance { get; private set; }
        public MCWS_Setup()
        {
            //Check for duplicate instances and destroy any that might be present.
            if (Instance == null)
            {
                Instance = this;
                Utils.LogInfo("Initializing Modular Climate & Weather Systems: Version " + Utils.version);
            }
            else
            {
                Utils.LogWarning("Destroying duplicate instance. Check your install for duplicate mod folders.");
                Destroy(this);
            }
        }

        void Awake()
        {
            Utils.LogInfo("Loading Settings Config.");
            try
            {
                Utils.CheckSettings();
                string msgstring = "Settings Loaded: \n";
                msgstring += string.Format("[MCWS SETTINGS] GlobalWindSpeedMultiplier: {0:F2} \n", Utils.GlobalWindSpeedMultiplier);
                msgstring += string.Format("[MCWS SETTINGS] UseMOAForCoords: {0} \n", Utils.Minutesforcoords);
                msgstring += string.Format("[MCWS SETTINGS] DisableAdjustedProgradeIndicators: {0} \n", Utils.AdjustedIndicatorsDisabled);
                msgstring += string.Format("[MCWS SETTINGS] DeveloperMode: {0}", Utils.DevMode);
                Utils.LogInfo(msgstring);
                if (Utils.DevMode)
                {
                    Utils.LogInfo("Developer Mode Enabled. MCWS's GUI will display a bunch of raw data.");
                }
            }
            catch (Exception e)
            {
                Utils.LogError("Exception thrown when loading the settings config: " + e.ToString());
            }

            Utils.LogInfo("Caching Localization Tags.");
            try
            {
                Utils.CacheLOC();
                Utils.LogInfo("Successfully cached Localization Tags.");
            }
            catch (Exception ex)
            {
                Utils.LogError("Exception thrown when caching Localization Tags: " + ex.ToString());
            }

            Utils.LogInfo("Registering MCWS with the toolbar controller.");
            try
            {
                if (ToolbarControl.RegisterMod(MCWS_FlightHandler.modID, MCWS_FlightHandler.modNAME))
                {
                    Utils.LogInfo("Successfully registered MCWS with the toolbar controller.");
                }
                else
                {
                    Utils.LogWarning("Unable to register MCWS with the toolbar. MCWS's UI will not be available.");
                }
            }
            catch (Exception e)
            {
                Utils.LogError("Exception thrown when registering MCWS with the toolbar controller: " + e.ToString());
            }
            
            Utils.LogInfo("Checking for an instance of FerramAerospaceResearch.");
            try
            {
                Type FARAtm = null;
                foreach (var assembly in AssemblyLoader.loadedAssemblies)
                {
                    if (assembly.name == "FerramAerospaceResearch")
                    {
                        var types = assembly.assembly.GetExportedTypes();
                        foreach (Type t in types)
                        {
                            if (t.FullName.Equals("FerramAerospaceResearch.FARWind"))
                            {
                                FARAtm = t;
                            }
                            if (t.FullName.Equals("FerramAerospaceResearch.FARAtmosphere"))
                            {
                                FARAtm = t;
                            }
                        }
                    }
                }
                Utils.FAR_Exists = FARAtm != null;
                Utils.LogInfo(Utils.FAR_Exists ? "FerramAerospaceResearch detected. Flight Dynamics calculations will be deferred to FAR." : "No instances of FerramAerospaceResearch detected.");
            }
            catch (Exception ex)
            {
                Utils.LogError("Exception thrown when checking for FerramAerospaceResearch: " + ex.ToString());
                Utils.FAR_Exists = false;
            }
            Utils.LogInfo("MCWS Setup Complete.");
        }
    }
}
