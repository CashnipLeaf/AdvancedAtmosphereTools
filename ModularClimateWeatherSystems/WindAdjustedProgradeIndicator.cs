using System;
using UnityEngine;
using KSP.IO;
using KSP.UI.Screens.Flight;

namespace ModularClimateWeatherSystems
{
    //partially adatped from Docking Port Alignment Indicator
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class WindAdjustedProgradeIndicator : MonoBehaviour
    {
        public static WindAdjustedProgradeIndicator Instance { get; private set; }

        private NavBall navBall;
        private GameObject progradewind;
        private GameObject retrogradewind;
        PluginConfiguration cfg;
        Color color;
        Vector3 navBallLocalScale = new Vector3(44, 44, 44);

        public WindAdjustedProgradeIndicator()
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
            cfg = PluginConfiguration.CreateForType<WindAdjustedProgradeIndicator>();
            cfg.load();
            Vector3 tmp = cfg.GetValue("alignmentmarkercolor", new Vector3(0f, 1f, 0.2f)); // default: light green
            color = new Color(tmp.x, tmp.y, tmp.z);
            cfg.save();
            GameEvents.onUIScaleChange.Add(ResizeIndicators);
        }

        void LateUpdate()
        {
            MCWS_FlightHandler FH = MCWS_FlightHandler.Instance;
            if (FlightGlobals.fetch != null && FlightGlobals.ready && FlightGlobals.ActiveVessel != null && FlightGlobals.speedDisplayMode == FlightGlobals.SpeedDisplayModes.Surface && FH != null && FH.HasWind && !Utils.AdjustedIndicatorsDisabled)
            {
                Vessel activevessel = FlightGlobals.ActiveVessel;
                Vector3 windvec = FH.AppliedWind;
                if (activevessel.mainBody.atmosphere && activevessel.altitude <= activevessel.mainBody.atmosphereDepth && Utils.IsVectorFinite(windvec) && windvec.magnitude >= 0.5f)
                {
                    if (navBall == null || progradewind == null || retrogradewind == null)
                    {
                        navBall = FindObjectOfType<NavBall>();

                        //set up the indicators
                        progradewind = Instantiate(navBall.progradeVector.gameObject);
                        progradewind.transform.parent = navBall.progradeVector.parent;
                        progradewind.transform.position = navBall.progradeVector.position;
                        progradewind.GetComponent<MeshRenderer>().materials[0].SetColor("_TintColor", color);

                        retrogradewind = Instantiate(navBall.retrogradeVector.gameObject);
                        retrogradewind.transform.parent = navBall.retrogradeVector.parent;
                        retrogradewind.transform.position = navBall.retrogradeVector.position;
                        retrogradewind.GetComponent<MeshRenderer>().materials[0].SetColor("_TintColor", color);

                        ResizeIndicators();
                    }
                    progradewind.transform.localScale = navBallLocalScale;
                    retrogradewind.transform.localScale = navBallLocalScale;

                    Vector3 srfv = FlightGlobals.ship_srfVelocity;
                    Vector3 displayV = srfv - windvec;
                    Vector3 displayVnormalized = displayV / displayV.magnitude;

                    bool vthresholdmet = srfv.magnitude > navBall.VectorVelocityThreshold;

                    float opacity1 = Mathf.Clamp01(Vector3.Dot(progradewind.transform.localPosition.normalized, Vector3.forward));
                    progradewind.GetComponent<MeshRenderer>().materials[0].SetFloat("_Opacity", opacity1);
                    progradewind.SetActive(progradewind.transform.localPosition.z > navBall.VectorUnitCutoff && vthresholdmet);
                    progradewind.transform.localPosition = navBall.attitudeGymbal * (displayVnormalized * navBall.VectorUnitScale);

                    float opacity2 = Mathf.Clamp01(Vector3.Dot(retrogradewind.transform.localPosition.normalized, Vector3.forward));
                    retrogradewind.GetComponent<MeshRenderer>().materials[0].SetFloat("_Opacity", opacity2);
                    retrogradewind.SetActive(retrogradewind.transform.localPosition.z > navBall.VectorUnitCutoff && vthresholdmet);
                    retrogradewind.transform.localPosition = navBall.attitudeGymbal * (-displayVnormalized * navBall.VectorUnitScale);
                    
                    return;
                }
            }

            if (progradewind != null)
            {
                progradewind.SetActive(false);
            }
            if (retrogradewind != null)
            {
                retrogradewind.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (progradewind != null)
            {
                Destroy(progradewind);
            }
            if (retrogradewind != null)
            {
                Destroy(retrogradewind);
            }
            GameEvents.onUIScaleChange.Remove(ResizeIndicators);
        }

        void ResizeIndicators()
        {
            float navballDefaultSize = 44f * GameSettings.UI_SCALE_NAVBALL;
            navBallLocalScale = new Vector3(navballDefaultSize, navballDefaultSize, navballDefaultSize);
        }
    }
}
