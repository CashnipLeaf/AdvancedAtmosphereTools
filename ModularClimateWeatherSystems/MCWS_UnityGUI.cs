using System;
using UnityEngine;
using KSP.Localization;
using KSP.UI.Screens;
using ToolbarControl_NS;

namespace ModularClimateWeatherSystems
{
    //This portion of the FlightHandler runs the GUI
    partial class MCWS_FlightHandler
    {
        private ToolbarControl toolbarController;
        private bool toolbarButtonAdded = false;
        private bool GUIEnabled = false;

        //if developer mode is enabled, a modified green logo will replace the normal white Logo.
        internal static string LogoPath => "ModularClimateWeatherSystems/Textures/" + (Settings.DevMode ? "MCWS_Debug" : "MCWS_Logo");
        internal const string modNAME = "MCWS";
        internal const string modID = "MCWS_NS";

        private Rect windowPos;        
        private static float Xpos => 100f * UIscale;
        private static float Ypos => 100f * UIscale;
        private static float Xwidth => (Settings.DevMode ? 345.0f : 285.0f) * Mathf.Clamp(UIscale, 0.75f, 1.5f);
        private static float Yheight => 60f * UIscale;
        private static float UIscale => GameSettings.UI_SCALE;
        
        private static string Distunit => Localizer.Format(GetLOC("#LOC_MCWS_meter"));
        private static string Speedunit => Localizer.Format(GetLOC("#LOC_MCWS_meterspersec"));
        private static string Pressunit => Localizer.Format(GetLOC("#LOC_MCWS_kpa"));
        private static string Forceunit => Localizer.Format(GetLOC("#LOC_MCWS_kilonewton"));
        private static string Tempunit => Localizer.Format(GetLOC("#LOC_MCWS_kelvin"));
        private static string DensityUnit => Localizer.Format(GetLOC("#LOC_MCWS_kgmcubed"));
        private static string Degreesstr => Localizer.Format("°");
        private static string Minutesstr => Localizer.Format("'");
        private static string Secondsstr => Localizer.Format("″");
        private static readonly string[] directions = { "N", "S", "E", "W" };
        private static readonly string[] cardinaldirs = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };

        //retrieve localization tag.
        internal static string GetLOC(string name) => (Utils.LOCCache != null && Utils.LOCCache.ContainsKey(name)) ? Utils.LOCCache[name] : name;

        void Start()
        {
            //add to toolbar
            ApplicationLauncher.AppScenes scenes = ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW;
            toolbarController = gameObject.AddComponent<ToolbarControl>();
            if (!toolbarButtonAdded)
            {
                toolbarController.AddToAllToolbars(ToolbarButtonOnTrue, ToolbarButtonOnFalse, scenes, modID, "991294", LogoPath, LogoPath, Localizer.Format(modNAME));
                toolbarButtonAdded = true;
            }
            windowPos = new Rect(Xpos, Ypos, Xwidth, Yheight);
        }

        void OnGUI()
        {
            if (GUIEnabled)
            {
                windowPos = GUILayout.Window("MCWS".GetHashCode(), windowPos, DrawWindow, "MCWS v" + Utils.version);
            }
        }

        void DrawWindow(int windowID)
        {
            GUIStyle button = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(10, 10, 6, 0),
                margin = new RectOffset(2, 2, 2, 2),
                stretchWidth = true,
                stretchHeight = false,
                fontSize = 13
            }; //Hack fix. Unity does not allow calling GUI functions outside of OnGUI()

            GUILayout.BeginVertical();

            //toggle the DisableWindWhenStationary setting
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Settings.DisableWindWhenStationary ? GetLOC("#LOC_MCWS_enablewind") : GetLOC("#LOC_MCWS_disablewind"), button))
            {
                Settings.DisableWindWhenStationary = !Settings.DisableWindWhenStationary;
            }
            GUILayout.EndHorizontal();

            //toggle the wind-adjusted prograde indicators
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Settings.AdjustedIndicatorsEnabled ? GetLOC("#LOC_MCWS_enableindicators") : GetLOC("#LOC_MCWS_disableindicators"), button))
            {
                Settings.AdjustedIndicatorsEnabled = !Settings.AdjustedIndicatorsEnabled;
            }
            GUILayout.EndHorizontal();

            if (activevessel != null && mainbody != null)
            {
                Vector3 craftdragvector = activevessel.srf_velocity;
                Vector3 craftdragvectorwind = activevessel.srf_velocity - InternalAppliedWind;
                Vector3 craftdragvectortransformed = Vesselframe.inverse * craftdragvector;
                Vector3 craftdragvectortransformedwind = Vesselframe.inverse * craftdragvectorwind;

                double alpha = 0.0;
                double slip = 0.0;
                Vector3 totaldrag = Vector3.zero;
                Vector3 totallift = Vector3.zero;
                double liftforce = 0.0;
                double dragforce = 0.0;
                double liftdragratio = 0.0;
                double liftinduceddrag = 0.0;
                string bodyname = mainbody.displayName.Split('^')[0];
                bool inatmo = mainbody.atmosphere && activevessel.staticPressurekPa > 0.0;

                string altitude = string.Format("{0:F1} {1}", activevessel.altitude, Distunit);
                double grndspd = Math.Sqrt(Math.Pow(craftdragvectortransformed.x, 2) + Math.Pow(craftdragvectortransformed.z, 2));
                string groundspeed = inatmo ? string.Format("{0:F1} {1}", grndspd, Speedunit) : GetLOC("#LOC_MCWS_na");
                string TAS = inatmo ? string.Format("{0:F1} {1}", craftdragvectorwind.magnitude, Speedunit) : GetLOC("#LOC_MCWS_na");
                string mach = inatmo ? string.Format("{0:F2}", activevessel.mach) : GetLOC("#LOC_MCWS_na");
                double trk = craftdragvector.magnitude > 0.0 ? UtilMath.WrapAround(Math.Atan2(craftdragvectortransformed.z, craftdragvectortransformed.x) * UtilMath.Rad2Deg, 0.0, 360.0) : 0.0;
                string track = inatmo && craftdragvector.magnitude > 0.1 ? string.Format("{0:F1} {1}", trk, Degreesstr) : GetLOC("#LOC_MCWS_na");

                string windspeed = string.Format("{0:F1} {1}", RawWind.magnitude, Speedunit);
                string v_windspeed = string.Format("{0:F1} {1}", RawWind.y, Speedunit);
                string h_windspeed = string.Format("{0:F1} {1}", Math.Sqrt(Math.Pow(RawWind.x, 2) + Math.Pow(RawWind.z, 2)), Speedunit);

                string windheading;
                string winddirection;
                if (RawWind.x == 0.0 && RawWind.z == 0.0)
                {
                    windheading = GetLOC("#LOC_MCWS_na");
                    winddirection = GetLOC("#LOC_MCWS_na");
                }
                else
                {
                    double heading = UtilMath.WrapAround((Math.Atan2(RawWind.z, RawWind.x) * UtilMath.Rad2Deg) + 180.0, 0.0, 360.0);
                    windheading = string.Format("{0:F1} {1}", heading, Degreesstr);
                    winddirection = cardinaldirs[(int)((heading / 22.5) + .5) % 16]; //this used to be a separate function but it got inlined to reduce file length.
                }

                string statictemp = string.Format("{0:F1} {1}", activevessel.atmosphericTemperature, Tempunit);
                string exttemp = string.Format("{0:F1} {1}", activevessel.externalTemperature, Tempunit);
                string staticpress = string.Format("{0:F3} {1}", activevessel.staticPressurekPa, Pressunit);
                string dynamicpress = string.Format("{0:F3} {1}", activevessel.dynamicPressurekPa, Pressunit);
                string density = string.Format("{0:F3} {1}", activevessel.atmDensity, DensityUnit);
                string soundspeed = string.Format("{0:F1} {1}", activevessel.speedOfSound, Speedunit);

                if (craftdragvectorwind.magnitude > 0.01)
                {
                    Vector3d nvel = (activevessel.srf_velocity - InternalAppliedWind).normalized;
                    Vector3d forward = (Vector3d)activevessel.transform.forward;
                    Vector3d vector3d = Vector3d.Exclude((Vector3d)activevessel.transform.right, nvel);
                    Vector3d normalized1 = vector3d.normalized;
                    alpha = Math.Asin(Vector3d.Dot(forward, normalized1)) * UtilMath.Rad2Deg;
                    alpha = double.IsNaN(alpha) ? 0.0 : alpha;

                    Vector3d up = (Vector3d)activevessel.transform.up;
                    vector3d = Vector3d.Exclude(forward, nvel);
                    Vector3d normalized2 = vector3d.normalized;
                    slip = Math.Acos(Vector3d.Dot(up, normalized2)) * UtilMath.Rad2Deg;
                    slip = double.IsNaN(slip) ? 0.0 : slip;

                    if (activevessel.atmDensity > 0.0)
                    {
                        foreach (Part p in activevessel.Parts)
                        {
                            totaldrag += p.dragScalar * -p.dragVectorDirLocal;
                            if (!p.hasLiftModule)
                            {
                                totallift += Vector3.ProjectOnPlane(p.transform.rotation * (p.bodyLiftScalar * p.DragCubes.LiftForce), -p.dragVectorDir);
                            }
                            if (p.hasLiftModule)
                            {
                                foreach (var m in p.Modules)
                                {
                                    if (m is ModuleLiftingSurface wing)
                                    {
                                        totallift += wing.liftForce;
                                        totaldrag += wing.dragForce;
                                    }
                                }
                            }
                        }
                        Vector3d totalforce = totallift + totaldrag;
                        Vector3d normalized = Vector3d.Exclude(nvel, totallift).normalized;
                        liftforce = Vector3d.Dot(totalforce, normalized);
                        dragforce = Vector3d.Dot(totalforce, -nvel);
                        liftinduceddrag = Vector3d.Dot(totallift, -nvel);
                        liftdragratio =  Math.Abs(liftforce) > 0.0001 ? liftforce / dragforce : 0.0;
                    }
                }

                string aoa = string.Format("{0:F2} {1}", alpha, Degreesstr);
                string sideslip = string.Format("{0:F2} {1}", slip, Degreesstr);
                string lift = string.Format("{0:F2} {1}", liftforce, Forceunit);
                string drag = string.Format("{0:F2} {1}", dragforce, Forceunit);
                string lid = string.Format("{0:F2} {1}", liftinduceddrag, Forceunit);
                string ldratio = string.Format("{0:F2}", liftdragratio);

                //Ground Track
                DrawHeader(GetLOC("#LOC_MCWS_grdtrk"));
                DrawElement(GetLOC("#LOC_MCWS_body"), Localizer.Format(bodyname));
                DrawElement(GetLOC("#LOC_MCWS_lon"), DegreesString(activevessel.longitude, 1)); //east/west
                DrawElement(GetLOC("#LOC_MCWS_lat"), DegreesString(activevessel.latitude, 0)); //north/south
                DrawElement(GetLOC("#LOC_MCWS_alt"), altitude);

                //Velocity Information
                DrawHeader(GetLOC("#LOC_MCWS_vel"));
                DrawElement(GetLOC("#LOC_MCWS_tas"), TAS);
                DrawElement(GetLOC("#LOC_MCWS_gs"), groundspeed);
                DrawElement(GetLOC("#LOC_MCWS_mach"), mach);
                DrawElement(GetLOC("#LOC_MCWS_track"), track);

                //Wind Information
                DrawHeader(GetLOC("#LOC_MCWS_windinfo"));
                if (inatmo)
                {
                    DrawElement(GetLOC("#LOC_MCWS_windspd"), windspeed);
                    DrawElement(GetLOC("#LOC_MCWS_windvert"), v_windspeed);
                    DrawElement(GetLOC("#LOC_MCWS_windhoriz"), h_windspeed);
                    DrawElement(GetLOC("#LOC_MCWS_heading"), windheading);
                    DrawElement(GetLOC("#LOC_MCWS_cardinal"), winddirection);
                }
                else
                {
                    DrawCentered(GetLOC("#LOC_MCWS_invac"));
                    GUILayout.FlexibleSpace();
                }

                if (Settings.DevMode) //Will probably deprecate
                {
                    DrawHeader("Developer Mode Information");
                    DrawElement("Connected to FAR", FARConnected.ToString()); //connected to FAR
                    DrawElement("Body Internal Name", mainbody.name); //internal name of the current celestial body
                    DrawElement("Wind Speed Multiplier", string.Format("{0:F2}", Settings.GlobalWindSpeedMultiplier));
                    //DrawElement("Wind Vector (Vessel)", RawWind.ToString()); //wind vector retrieved from the wind objects
                    //DrawElement("Wind Vector (World)", CachedWind.ToString()); //wind vector after being transformed relative to the craft's frame of reference
                    //DrawElement("Wind Vector (Applied)", AppliedWind.ToString()); //wind vector after being multiplied by the wind speed multiplier
                    DrawElement("World Position", activevessel.GetWorldPos3D().ToString("F1"));
                    DrawElement("Drag Vector (World)", craftdragvector.ToString());
                    DrawElement("Drag Vector (Vessel)", craftdragvectortransformed.ToString());
                    DrawElement("Drag Vector + Wind (World)", craftdragvectorwind.ToString());
                    DrawElement("Drag Vector + Wind (Vessel)", craftdragvectortransformedwind.ToString());
                    DrawElement("Universal Time", string.Format("{0:F1}", CurrentTime));
                }
                else
                {
                    //aerodynamics
                    DrawHeader(GetLOC("#LOC_MCWS_aero"));
                    DrawElement(GetLOC("#LOC_MCWS_staticpress"), staticpress);
                    DrawElement(GetLOC("#LOC_MCWS_dynamicpress"), dynamicpress);
                    DrawElement(GetLOC("#LOC_MCWS_density"), density);
                    DrawElement(GetLOC("#LOC_MCWS_statictemp"), statictemp);
                    DrawElement(GetLOC("#LOC_MCWS_exttemp"), exttemp);
                    DrawElement(GetLOC("#LOC_MCWS_soundspeed"), soundspeed);

                    DrawCentered("----------"); //gap between pressure/temperature and aero forces

                    DrawElement(GetLOC("#LOC_MCWS_aoa"), aoa);
                    DrawElement(GetLOC("#LOC_MCWS_sideslip"), sideslip);
                    DrawElement(GetLOC("#LOC_MCWS_lift"), lift);
                    DrawElement(GetLOC("#LOC_MCWS_drag"), drag);
                    DrawElement(GetLOC("#LOC_MCWS_LID"), lid);
                    DrawElement(GetLOC("#LOC_MCWS_lifttodrag"), ldratio);
                }
            }
            else
            {
                DrawCentered(GetLOC("#LOC_MCWS_NoVessel"));
                GUILayout.FlexibleSpace();
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        //GUILayout functions because things look neater this way.
        private void DrawHeader(string tag)
        {
            GUILayout.BeginHorizontal();
            GUI.skin.label.margin = new RectOffset(5, 5, 5, 5);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUILayout.Label(tag);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUILayout.EndHorizontal();
            GUI.skin.label.margin = new RectOffset(2, 2, 2, 2);
        }
        private void DrawElement(string tag, string value)
        {
            GUILayout.BeginHorizontal();
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUILayout.Label(tag);
            GUI.skin.label.alignment = TextAnchor.MiddleRight;
            GUILayout.Label(value);
            GUILayout.EndHorizontal();
        }
        private void DrawCentered(string tag)
        {
            GUILayout.BeginHorizontal();
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label(tag);
            GUILayout.EndHorizontal();
        }

        private void RemoveToolbarButton() //Remove from toolbar
        {
            if (toolbarButtonAdded)
            {
                toolbarController.OnDestroy();
                Destroy(toolbarController);
                toolbarButtonAdded = false;
            }
        }

        private void ToolbarButtonOnTrue() => GUIEnabled = true;
        private void ToolbarButtonOnFalse() => GUIEnabled = false;

        //display the longitude and latitude information as either degrees or degrees, minutes, and seconds + direction
        internal static string DegreesString(double deg, int axis)
        {
            if (Settings.Minutesforcoords)
            {
                double minutes = (deg % 1) * 60.0;
                double seconds = ((deg % 1) * 3600.0) % 60.0;
                string degs = string.Format("{0:F0}{1}", Math.Floor(Math.Abs(deg)), Degreesstr);
                string mins = string.Format("{0:F0}{1}", Math.Floor(Math.Abs(minutes)), Minutesstr);
                string secs = string.Format("{0:F0}{1}", Math.Floor(Math.Abs(seconds)), Secondsstr);
                return degs + " " + mins + " " + secs + " " + directions[(2 * axis) + (deg < 0.0 ? 1 : 0)];
            }
            return string.Format("{0:F2}{1}", deg, Degreesstr);
        }
    }
}
