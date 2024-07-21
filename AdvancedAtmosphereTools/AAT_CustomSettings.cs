using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace AdvancedAtmosphereTools
{
    public class AAT_CustomSettings : GameParameters.CustomParameterNode
    {
        public override string Title => "#LOC_AAT_GeneralSettings";
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => "AdvancedAtmosphereTools";
        public override string DisplaySection => "AdvancedAtmosphereTools";
        public override int SectionOrder => 2;
        public override bool HasPresets => true;

        [GameParameters.CustomParameterUI("#LOC_AAT_DisplayAdjustedMarkers", toolTip = "#LOC_AAT_AdjustedMarkersTip", autoPersistance = true)]
        public bool adjustedmarkers = true;

        [GameParameters.CustomParameterUI("#LOC_AAT_DisableWindWhenStationary", toolTip = "#LOC_AAT_StationaryTip", autoPersistance = true)]
        public bool disablestationarywind = false;

        [GameParameters.CustomStringParameterUI("#LOC_AAT_LonLatUnits", autoPersistance = true)]
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
                    "Degrees, Minutes",
                    "Degrees, Minutes, Seconds"
                };
                return (IList)coordsunitlist;
            }
            return null;
        }
    }

    public class AAT_CustomSettingsAero : GameParameters.CustomParameterNode
    {
        public override string Title => "#LOC_AAT_FlightSettings";
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => "AdvancedAtmosphereTools";
        public override string DisplaySection => "AdvancedAtmosphereTools";
        public override int SectionOrder => 2;
        public override bool HasPresets => true;

        [GameParameters.CustomFloatParameterUI("#LOC_AAT_WindSpeedMultiplier", toolTip = "#LOC_AAT_WindSpeedTip", minValue = 0.01f, maxValue = 1.5f, stepCount = 15, logBase = 10f, displayFormat = "F2", autoPersistance = true)]
        public float windmult = 1.0f;

        [GameParameters.CustomIntParameterUI("#LOC_AAT_WindSpeedVariability", toolTip = "#LOC_AAT_WindVariabilityTip", minValue = 0, maxValue = 10, stepSize = 1, autoPersistance = true)]
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
        internal static string Minutesforcoords = "Degrees";
        internal static float GlobalWindSpeedMultiplier = 1.0f;
        internal static bool FAR_Exists = false;
        internal static float WindSpeedVariability = 0.0f;

        //hack fix to allow the buttons in the GUI to actually do something
        internal static bool AdjustedIndicatorsEnabled => buttonindicatorsenabled ^ settingsindicatorsenabled;
        internal static bool DisableWindWhenStationary => buttondisablewindstationary ^ settingsdisablewindstationary;

        internal static bool buttonindicatorsenabled = false; //GUI button
        internal static bool settingsindicatorsenabled = false;

        internal static bool buttondisablewindstationary = false; //GUI button
        internal static bool settingsdisablewindstationary = false;

        internal static bool debugmode = false;

        internal static void CheckGameSettings() //fetch game settings.
        {
            Minutesforcoords = HighLogic.CurrentGame.Parameters.CustomParams<AAT_CustomSettings>().minsforcoords;
            settingsindicatorsenabled = HighLogic.CurrentGame.Parameters.CustomParams<AAT_CustomSettings>().adjustedmarkers;
            settingsdisablewindstationary = HighLogic.CurrentGame.Parameters.CustomParams<AAT_CustomSettings>().disablestationarywind;
            GlobalWindSpeedMultiplier = HighLogic.CurrentGame.Parameters.CustomParams<AAT_CustomSettingsAero>().windmult;
            WindSpeedVariability = ((float)HighLogic.CurrentGame.Parameters.CustomParams<AAT_CustomSettingsAero>().windvariability) * 0.01f;
        }
    }
}
