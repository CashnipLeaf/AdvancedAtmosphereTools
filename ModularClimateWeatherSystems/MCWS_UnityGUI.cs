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

        internal const string LogoPath = "ModularClimateWeatherSystems/Textures/MCWS_Logo";
        internal const string modNAME = "MCWS";
        internal const string modID = "MCWS_NS";

        internal string UIHeader => "MCWS v" + (Settings.debugmode ? Utils.version + " DEBUG MODE" : Utils.version);

        private Rect windowPos;        
        private static float Xpos => 100f * GameSettings.UI_SCALE;
        private static float Ypos => 100f * GameSettings.UI_SCALE;
        private static float Xwidth => 285.0f * Mathf.Clamp(GameSettings.UI_SCALE, 0.75f, 1.5f);
        private static float Yheight => 60f * GameSettings.UI_SCALE;
        
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
            Settings.buttondisablewindstationary = Settings.buttonindicatorsenabled = false;
        }

        void OnGUI()
        {
            if (GUIEnabled)
            {
                windowPos = GUILayout.Window("MCWS".GetHashCode(), windowPos, DrawWindow, UIHeader);
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
            }; //Unity does not allow calling GUI functions outside of OnGUI(). FML

            GUILayout.BeginVertical();

            //toggle the DisableWindWhenStationary setting
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Settings.DisableWindWhenStationary ? GetLOC("#LOC_MCWS_enablewind") : GetLOC("#LOC_MCWS_disablewind"), button))
            {
                Settings.buttondisablewindstationary = !Settings.buttondisablewindstationary;
            }
            GUILayout.EndHorizontal();

            //toggle the wind-adjusted prograde indicators
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Settings.AdjustedIndicatorsEnabled ? GetLOC("#LOC_MCWS_disableindicators") : GetLOC("#LOC_MCWS_enableindicators"), button))
            {
                Settings.buttonindicatorsenabled = !Settings.buttonindicatorsenabled;
            }
            GUILayout.EndHorizontal();

            if (activevessel != null && mainbody != null)
            {
                bool inatmo = mainbody.atmosphere && activevessel.staticPressurekPa > 0.0;
                string altitude = string.Format(Math.Abs(activevessel.altitude) > 1000000d ? "{0:0.#####E+00} {1}" : "{0:F2} {1}", activevessel.altitude, Localizer.Format(GetLOC("#LOC_MCWS_meter")));
                if (Settings.debugmode)
                {
                    string na = "N/A";
                    DrawHeader("Position Info");
                    DrawElement("Body", mainbody.name);
                    DrawElement("Longitude", DegreesString(activevessel.longitude, 1, true)); //east/west
                    DrawElement("Latitude", DegreesString(activevessel.latitude, 0, true)); //north/south
                    DrawElement("Altitude", altitude);
                    DrawElement("Universal Time", string.Format("{0:F1}", CurrentTime));

                    DrawHeader("Wind Info");
                    DrawElement("X Position", haswinddata && inatmo ? string.Format("{0},{1}", WindDataInfo.x1, WindDataInfo.x2) : na);
                    DrawElement("Y Position", haswinddata && inatmo ? string.Format("{0},{1}", WindDataInfo.y1, WindDataInfo.y2) : na);
                    string windzinfo = WindDataInfo.abovetop ? string.Format("{0}+", WindDataInfo.z2) : string.Format("{0},{1}", WindDataInfo.z1, WindDataInfo.z2);
                    DrawElement("Z Position", haswinddata && inatmo ? windzinfo : na);
                    DrawElement("Time Position", haswinddata && inatmo ? string.Format("{0},{1}", WindDataInfo.t1, WindDataInfo.t2) : na);
                    DrawElement("Data Wind Vec", haswinddata && inatmo ? datawind.ToString() : na);
                    DrawElement("Flowmap Wind Vec", hasflowmaps && inatmo ? flowmapwind.ToString() : na);
                    DrawElement("Combined Wind Vec", HasWind && inatmo ? RawWind.ToString() : na);

                    DrawHeader("Temperature Info");
                    DrawElement("X Position", HasTemp && inatmo ? string.Format("{0},{1}", TemperatureDataInfo.x1, TemperatureDataInfo.x2) : na);
                    DrawElement("Y Position", HasTemp && inatmo ? string.Format("{0},{1}", TemperatureDataInfo.y1, TemperatureDataInfo.y2) : na);
                    string tempzinfo = TemperatureDataInfo.abovetop ? string.Format("{0}+", TemperatureDataInfo.z2) : string.Format("{0},{1}", TemperatureDataInfo.z1, TemperatureDataInfo.z2);
                    DrawElement("Z Position", HasTemp && inatmo ? tempzinfo : na);
                    DrawElement("Time Position", HasTemp && inatmo ? string.Format("{0},{1}", TemperatureDataInfo.t1, TemperatureDataInfo.t2) : na);
                    DrawElement("Derived Temp", HasTemp && inatmo ? string.Format("{0:F1} {1}", Temperature, Tempunit) : na);
                    DrawElement("Stock Temp", string.Format("{0:F1} {1}", stocktemperature, Tempunit));

                    DrawHeader("Pressure Info");
                    DrawElement("X Position", HasPress && inatmo ? string.Format("{0},{1}", PressureDataInfo.x1, PressureDataInfo.x2) : na);
                    DrawElement("Y Position", HasPress && inatmo ? string.Format("{0},{1}", PressureDataInfo.y1, PressureDataInfo.y2) : na);
                    string presszinfo = PressureDataInfo.abovetop ? string.Format("{0}+", PressureDataInfo.z2) : string.Format("{0},{1}", PressureDataInfo.z1, PressureDataInfo.z2);
                    DrawElement("Z Position", HasPress && inatmo ? presszinfo : na);
                    DrawElement("Time Position", HasPress && inatmo ? string.Format("{0},{1}", PressureDataInfo.t1, PressureDataInfo.t2) : na);
                    DrawElement("Derived Press", HasPress && inatmo ? string.Format("{0:F3} {1}", Pressure, Pressunit) : na);
                    DrawElement("Stock Press", string.Format("{0:F3} {1}", stockpressure, Pressunit));
                }
                else
                {
                    Vector3 craftdragvector = activevessel.srf_velocity;
                    Vector3 craftdragvectorwind = activevessel.srf_velocity - InternalAppliedWind;
                    Vector3 craftdragvectortransformed = Vesselframe.inverse * craftdragvector;

                    double alpha = 0.0;
                    double slip = 0.0;
                    Vector3 totaldrag = Vector3.zero;
                    Vector3 totallift = Vector3.zero;
                    double liftforce = 0.0;
                    double dragforce = 0.0;
                    double liftdragratio = 0.0;
                    double liftinduceddrag = 0.0;
                    string bodyname = mainbody.displayName.Split('^')[0];

                    double grndspd = Math.Sqrt(Math.Pow(craftdragvectortransformed.x, 2) + Math.Pow(craftdragvectortransformed.z, 2));
                    string groundspeed = inatmo ? string.Format("{0:F1} {1}", grndspd, Speedunit) : GetLOC("#LOC_MCWS_na");
                    string TAS = inatmo ? string.Format("{0:F1} {1}", craftdragvectorwind.magnitude, Speedunit) : GetLOC("#LOC_MCWS_na");
                    string mach = inatmo ? string.Format("{0:F2}", activevessel.mach) : GetLOC("#LOC_MCWS_na");
                    double trk = craftdragvector.magnitude > 0.0 ? UtilMath.WrapAround(Math.Atan2(craftdragvectortransformed.z, craftdragvectortransformed.x) * UtilMath.Rad2Deg, 0.0, 360.0) : 0.0;
                    string track = inatmo && craftdragvector.magnitude > 0.1 ? string.Format("{0:F1} {1}", trk, Degreesstr) : GetLOC("#LOC_MCWS_na");

                    string windspeed = string.Format("{0:F1} {1}", RawWind.magnitude, Speedunit);
                    string v_windspeed = string.Format("{0:F1} {1}", RawWind.y, Speedunit);
                    string h_windspeed = string.Format("{0:F1} {1}", Math.Sqrt(Math.Pow(RawWind.x, 2) + Math.Pow(RawWind.z, 2)), Speedunit);

                    bool istherewind = RawWind.x != 0.0 || RawWind.z != 0.0;
                    double heading = istherewind ? UtilMath.WrapAround((Math.Atan2(RawWind.z, RawWind.x) * UtilMath.Rad2Deg) + 180.0, 0.0, 360.0) : 0.0;

                    string windheading = istherewind ? string.Format("{0:F1} {1}", heading, Degreesstr) : GetLOC("#LOC_MCWS_na");
                    string winddirection = istherewind ? cardinaldirs[(int)((heading / 22.5) + .5) % 16] : GetLOC("#LOC_MCWS_na");

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
                                totaldrag.Add(p.dragScalar * -p.dragVectorDir);
                                if (!p.hasLiftModule)
                                {
                                    totallift.Add(Vector3.ProjectOnPlane(p.transform.rotation * (p.bodyLiftScalar * p.DragCubes.LiftForce), -p.dragVectorDir));
                                }
                                foreach (var m in p.Modules)
                                {
                                    if (m is ModuleLiftingSurface wing)
                                    {
                                        totallift.Add(wing.liftForce);
                                        totaldrag.Add(wing.dragForce);
                                    }
                                }
                            }
                            Vector3d totalforce = totallift + totaldrag;
                            Vector3d normalized = Vector3d.Exclude(nvel, totallift).normalized;
                            liftforce = Vector3d.Dot(totalforce, normalized);
                            dragforce = Vector3d.Dot(totalforce, -nvel);
                            liftinduceddrag = Vector3d.Dot(totallift, -nvel);
                            liftdragratio = Math.Abs(liftforce) > 0.0001 ? liftforce / dragforce : 0.0;
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
                    DrawElement(GetLOC("#LOC_MCWS_lon"), DegreesString(activevessel.longitude, 1, false)); //east/west
                    DrawElement(GetLOC("#LOC_MCWS_lat"), DegreesString(activevessel.latitude, 0, false)); //north/south
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
        private static string DegreesString(double deg, int axis, bool debug)
        {
            double degrees = Math.Floor(Math.Abs(deg));
            double minutes = Math.Abs((deg % 1) * 60.0);
            double seconds = Math.Floor(Math.Abs(((deg % 1) * 3600.0) % 60.0));
            string dir = directions[(2 * axis) + (deg < 0.0 ? 1 : 0)];
            if (debug)
            {
                return string.Format("{0:F2}{1}", deg, Degreesstr);
            }
            else
            {
                switch (Settings.Minutesforcoords)
                {
                    case "Degrees, Minutes, Seconds":
                        return string.Format("{0:F0}{1} {2:F0}{3} {4:F0}{5} {6}", degrees, Degreesstr, Math.Floor(minutes), Minutesstr, seconds, Secondsstr, dir);
                    case "Degrees, Minutes":
                        return string.Format("{0:F0}{1} {2:F1}{3} {4}", degrees, Degreesstr, minutes, Minutesstr, dir);
                    default:
                        return string.Format("{0:F2}{1}", deg, Degreesstr);
                }
            }
        }
    }
}
