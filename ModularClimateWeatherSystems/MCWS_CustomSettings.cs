using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace ModularClimateWeatherSystems
{
    public class MCWS_CustomSettings : GameParameters.CustomParameterNode
    {
        public override string Title => "#LOC_MCWS_GeneralSettings";
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => "ModularClimate&WeatherSystems";
        public override string DisplaySection => "ModularClimate&WeatherSystems";
        public override int SectionOrder => 2;
        public override bool HasPresets => true;

        [GameParameters.CustomParameterUI("#LOC_MCWS_DisplayAdjustedMarkers", toolTip = "#LOC_MCWS_AdjustedMarkersTip", autoPersistance = true)]
        public bool adjustedmarkers = true;

        [GameParameters.CustomParameterUI("#LOC_MCWS_DisableWindWhenStationary", toolTip = "#LOC_MCWS_StationaryTip", autoPersistance = true)]
        public bool disablestationarywind = false;

        [GameParameters.CustomStringParameterUI("#LOC_MCWS_LonLatUnits", autoPersistance = true)]
        public string minsforcoords = "Degrees";

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    break;
                case GameParameters.Preset.Normal:
                    break;
                case GameParameters.Preset.Moderate:
                    break;
                case GameParameters.Preset.Hard:
                    break;
                default:
                    break;
            }
        }

        public override IList ValidValues(MemberInfo member)
        {
            if(member.Name == "minsforcoords")
            {
                List<string> coordsunitlist = new List<string>
                {
                    "Degrees",
                    "Degrees, Minutes, Seconds"
                };
                return (IList)coordsunitlist;
            }
            return null;
        }
    }

    public class MCWS_CustomSettingsAero : GameParameters.CustomParameterNode
    {
        public override string Title => "#LOC_MCWS_FlightSettings";
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => "ModularClimate&WeatherSystems";
        public override string DisplaySection => "ModularClimate&WeatherSystems";
        public override int SectionOrder => 2;
        public override bool HasPresets => true;

        [GameParameters.CustomFloatParameterUI("#LOC_MCWS_WindSpeedMultiplier", toolTip = "#LOC_MCWS_WindSpeedTip", minValue = 0.01f, maxValue = 1.5f, stepCount = 15, logBase = 10f, displayFormat = "F2", autoPersistance = true)]
        public float windmult = 1.0f;

        [GameParameters.CustomIntParameterUI("#LOC_MCWS_WindSpeedVariability", toolTip = "#LOC_MCWS_WindVariabilityTip", minValue = 0, maxValue = 10, stepSize = 1, autoPersistance = true)]
        public int windvariability = 5;

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    windmult = 0.70f;
                    windvariability = 1;
                    break;
                case GameParameters.Preset.Normal:
                    windvariability = 5;
                    windmult = 1.0f;
                    break;
                case GameParameters.Preset.Moderate:
                    windvariability = 7;
                    windmult = 1.0f;
                    break;
                case GameParameters.Preset.Hard:
                    windvariability = 10;
                    windmult = 1.2f;
                    break;
                default:
                    windvariability = 5;
                    windmult = 1.0f;
                    break;
            }
        }
    }

    internal static class Settings
    {
        internal static bool DevMode = false;
        internal static bool Minutesforcoords = false;
        internal static bool AdjustedIndicatorsEnabled = false;
        internal static float GlobalWindSpeedMultiplier = 1.0f;
        internal static bool FAR_Exists = false;
        internal static bool DisableWindWhenStationary = false;
        internal static float WindSpeedVariability = 0.0f;

        internal static void CheckGameSettings() //fetch game settings.
        {
            Minutesforcoords = HighLogic.CurrentGame.Parameters.CustomParams<MCWS_CustomSettings>().minsforcoords == "Degrees, Minutes, Seconds";
            AdjustedIndicatorsEnabled = HighLogic.CurrentGame.Parameters.CustomParams<MCWS_CustomSettings>().adjustedmarkers;
            DisableWindWhenStationary = HighLogic.CurrentGame.Parameters.CustomParams<MCWS_CustomSettings>().disablestationarywind;
            GlobalWindSpeedMultiplier = HighLogic.CurrentGame.Parameters.CustomParams<MCWS_CustomSettingsAero>().windmult;
            WindSpeedVariability = ((float)HighLogic.CurrentGame.Parameters.CustomParams<MCWS_CustomSettingsAero>().windvariability) * 0.01f;
        }
    }
}
