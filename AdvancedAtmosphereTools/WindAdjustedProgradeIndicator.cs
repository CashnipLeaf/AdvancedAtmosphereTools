using UnityEngine;
using KSP.IO;
using KSP.UI.Screens.Flight;

namespace AdvancedAtmosphereTools
{
    //partially adatped from Docking Port Alignment Indicator
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class WindAdjustedProgradeIndicator : MonoBehaviour
    {
        public static WindAdjustedProgradeIndicator Instance { get; private set; }

        private NavBall navBall;
        private GameObject progradewind;
        private GameObject retrogradewind;
        Color Color => Settings.ProgradeMarkerColor;
        Vector3 navBallLocalScale = new Vector3(44, 44, 44);
        
        void Start()
        {
            if (Instance == null)
            {
                Settings.CheckGameSettings();
                Instance = this;
                GameEvents.onUIScaleChange.Add(ResizeIndicators);
            }
            else
            {
                DestroyImmediate(this);
            }
        }

        void LateUpdate()
        {
            AAT_FlightHandler FH = AAT_FlightHandler.Instance;
            if (FlightGlobals.fetch != null && FlightGlobals.ready && FlightGlobals.ActiveVessel != null && FlightGlobals.speedDisplayMode == FlightGlobals.SpeedDisplayModes.Surface && FH != null && FH.HasWind && Settings.AdjustedIndicatorsEnabled)
            {
                Vessel activevessel = FlightGlobals.ActiveVessel;
                Vector3 windvec = FH.InternalAppliedWind;
                if (activevessel.mainBody.atmosphere && activevessel.altitude <= activevessel.mainBody.atmosphereDepth && windvec.IsFinite() && windvec.magnitude >= 0.5f)
                {
                    if (navBall == null || progradewind == null || retrogradewind == null)
                    {
                        navBall = FindObjectOfType<NavBall>();

                        //set up the indicators.
                        progradewind = Instantiate(navBall.progradeVector.gameObject);
                        progradewind.transform.parent = navBall.progradeVector.parent;
                        progradewind.transform.position = navBall.progradeVector.position;

                        retrogradewind = Instantiate(navBall.retrogradeVector.gameObject);
                        retrogradewind.transform.parent = navBall.retrogradeVector.parent;
                        retrogradewind.transform.position = navBall.retrogradeVector.position;
                        
                        ResizeIndicators();
                    }
                    progradewind.transform.localScale = navBallLocalScale;
                    retrogradewind.transform.localScale = navBallLocalScale;

                    Vector3 srfv = FlightGlobals.ship_srfVelocity;
                    Vector3 displayV = srfv - windvec;
                    Vector3 displayVnormalized = displayV / displayV.magnitude;

                    bool vthresholdmet = srfv.magnitude > navBall.VectorVelocityThreshold;

                    Material progrademat = progradewind.GetComponent<MeshRenderer>().materials[0];
                    Material retrogrademat = retrogradewind.GetComponent<MeshRenderer>().materials[0];

                    float opacity1 = Mathf.Clamp01(Vector3.Dot(progradewind.transform.localPosition.normalized, Vector3.forward));
                    progrademat.SetFloat("_Opacity", opacity1);
                    progrademat.SetColor("_TintColor", Color);
                    progradewind.SetActive(progradewind.transform.localPosition.z > navBall.VectorUnitCutoff && vthresholdmet);
                    progradewind.transform.localPosition = navBall.attitudeGymbal * (displayVnormalized * navBall.VectorUnitScale);

                    float opacity2 = Mathf.Clamp01(Vector3.Dot(retrogradewind.transform.localPosition.normalized, Vector3.forward));
                    retrogrademat.SetFloat("_Opacity", opacity2);
                    retrogrademat.SetColor("_TintColor", Color);
                    retrogradewind.SetActive(retrogradewind.transform.localPosition.z > navBall.VectorUnitCutoff && vthresholdmet);
                    retrogradewind.transform.localPosition = navBall.attitudeGymbal * (-displayVnormalized * navBall.VectorUnitScale);
                    
                    return;
                }
            }

            progradewind?.SetActive(false);
            retrogradewind?.SetActive(false);
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

            if (Instance == this)
            {
                Instance = null;
            }
        }

        void ResizeIndicators()
        {
            float navballDefaultSize = 44f * GameSettings.UI_SCALE_NAVBALL;
            navBallLocalScale = new Vector3(navballDefaultSize, navballDefaultSize, navballDefaultSize);
        }
    }
}
