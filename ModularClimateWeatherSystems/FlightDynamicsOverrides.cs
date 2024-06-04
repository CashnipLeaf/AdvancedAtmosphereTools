using System;
using System.Reflection;
using KSP.Localization;
using UnityEngine;
using ModularFI;
using HarmonyLib;

namespace ModularClimateWeatherSystems
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class FlightDynamicsOverrides : MonoBehaviour
    {
        public static FlightDynamicsOverrides Instance { get; private set; }
        private static MCWS_FlightHandler FH => MCWS_FlightHandler.Instance;

        public FlightDynamicsOverrides() //make sure that things dont get patched more than once. That would be very bad.
        {
            if (Instance == null)
            {
                Utils.LogInfo("Initializing Flight Dynamics Overrides.");
                Instance = this;
            }
            else
            {
                Utils.LogWarning("Destroying duplicate Flight Dynamics Overrides. Check your install for duplicate mod folders.");
                DestroyImmediate(this);
            }
        }

        void Start()
        {
            //If FAR is installed, do not override flight dynamics. Leave the aerodynamics calulations to FAR.
            if (!Settings.FAR_Exists)
            {
                //register overrides with ModularFI
                Utils.LogInfo("Registering MCWS with ModularFlightIntegrator.");
                try
                {
                    if (ModularFlightIntegrator.RegisterUpdateAerodynamicsOverride(NewAeroUpdate))
                    {
                        ModularFlightIntegrator.RegisterCalculateAerodynamicAreaOverride(AerodynamicAreaOverride);
                        Utils.LogInfo("Successfully registered MCWS's Aerodynamics Overrides with ModularFlightIntegrator.");
                    }
                    else
                    {
                        Utils.LogWarning("Unable to register MCWS's Aerodynamics Override with ModularFlightIntegrator.");
                    }

                    if (ModularFlightIntegrator.RegisterCalculatePressureOverride(CalcPressureOverride))
                    {
                        Utils.LogInfo("Successfully registered MCWS's Pressure Override with ModularFlightIntegrator.");
                    }
                    else
                    {
                        Utils.LogWarning("Unable to register MCWS's Pressure Override with ModularFlightIntegrator.");
                    }

                    if (ModularFlightIntegrator.RegistercalculateConstantsAtmosphereOverride(CalculateConstantsAtmosphereOverride))
                    {
                        Utils.LogInfo("Successfully registered MCWS's Atmosphere and Thermodynamics Overrides with ModularFlightIntegrator.");
                    }
                    else
                    {
                        Utils.LogWarning("Unable to register MCWS's Atmosphere and Thermodynamics Overrides with ModularFlightIntegrator.");
                    }
                    Utils.LogInfo("ModularFlightIntegrator Registration Complete.");
                }
                catch (Exception ex)
                {
                    Utils.LogError("ModularFlightIntegrator Registration Failed. Exception thrown: " + ex.ToString());
                }
                Utils.LogInfo("Patching Lifting Surface and Air Intake behavior.");
                try
                {
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    Harmony harmony = new Harmony("MCWS_WingAndIntake");
                    harmony.PatchAll(assembly);
                    Utils.LogInfo("Patching Complete.");
                }
                catch (Exception ex)
                {
                    Utils.LogError("Patching Failed. Exception thrown:" + ex.ToString());
                }
            } 
        }

        void NewAeroUpdate(ModularFlightIntegrator fi, Part part)
        {
            //This override really just occupies the slot so that no other mod can take it.
            fi.BaseFIUpdateAerodynamics(part);
        }

        //Takes advantage of CalculateAerodynamicArea()'s placement inside UpdateAerodynamics()
        //to inject a new drag vector into the part before UpdateAerodynamics() uses to calculate anything.
        double AerodynamicAreaOverride(ModularFlightIntegrator fi, Part part)
        {
            Vector3 windvec = (FH != null && FH.HasWind) ? FH.InternalAppliedWind : Vector3.zero;
            float submerged = (float)part.submergedPortion;
            windvec.LerpWith(Vector3.zero, submerged * submerged);

            //add an offset to the velocity vector used for body drag/lift calcs and update the related fields.
            if (part.Rigidbody != null && windvec.IsFinite() && !Mathf.Approximately(windvec.magnitude, 0.0f))
            {
                part.dragVector.Subtract(windvec);
                part.dragVectorSqrMag = part.dragVector.sqrMagnitude;
                if (part.dragVectorSqrMag != 0.0)
                {
                    part.dragVectorMag = Mathf.Sqrt(part.dragVectorSqrMag);
                    part.dragVectorDir = part.dragVector / part.dragVectorMag;
                    part.dragVectorDirLocal = -part.partTransform.InverseTransformDirection(part.dragVectorDir);
                }
                else
                {
                    part.dragVectorMag = 0.0f;
                    part.dragVectorDir = part.dragVectorDirLocal = Vector3.zero;
                }
                part.dragScalar = 0.0f;
                if (!part.ShieldedFromAirstream && !(part.atmDensity <= 0 && part.submergedPortion <= 0.0) && !part.DragCubes.None)
                {
                    //update the drag from the drag cubes if they exist
                    part.DragCubes.SetDrag(part.dragVectorDirLocal, (float)fi.mach);
                }
            }

            //inline the rest of CaclulateAerodynamicArea() to avoid passing an object reference again
            if (!part.DragCubes.None)
            {
                return part.DragCubes.Area;
            }
            else
            {
                switch (part.dragModel)
                {
                    case Part.DragModel.DEFAULT:
                    case Part.DragModel.CUBE:
                        return (!PhysicsGlobals.DragCubesUseSpherical && !part.DragCubes.None) ? part.DragCubes.Area : part.maximum_drag;
                    case Part.DragModel.CONIC:
                        return part.maximum_drag;
                    case Part.DragModel.CYLINDRICAL: 
                        return part.maximum_drag;
                    case Part.DragModel.SPHERICAL: 
                        return part.maximum_drag;
                    default: 
                        return part.maximum_drag;
                }
            }
        }

        void CalculateConstantsAtmosphereOverride(ModularFlightIntegrator fi)
        {
            Vector3 windvec = (FH != null && FH.HasWind) ? FH.InternalAppliedWind : Vector3.zero;
            if (windvec.IsFinite() && !Mathf.Approximately(windvec.magnitude, 0.0f))
            {
                fi.Vel -= windvec;
                fi.spd = !fi.Vessel.IgnoreSpeedActive ? fi.Vel.magnitude : 0.0;
                fi.Vessel.speed = fi.spd;
                fi.nVel = (fi.spd != 0.0) ? fi.Vel / (float)fi.spd : Vector3.zero;
            }

            fi.CurrentMainBody.GetSolarAtmosphericEffects(fi.sunDot, fi.density, out fi.solarAirMass, out fi.solarFluxMultiplier);
            fi.Vessel.solarFlux = fi.solarFlux *= fi.solarFluxMultiplier;
            fi.Vessel.atmosphericTemperature = fi.atmosphericTemperature = (FH != null && FH.HasTemp) ? FH.Temperature : fi.CurrentMainBody.GetFullTemperature(fi.altitude, fi.atmosphereTemperatureOffset);
            fi.density = fi.Vessel.atmDensity = fi.CurrentMainBody.GetDensity(fi.staticPressurekPa, fi.atmosphericTemperature); //inlined CalculateAtmosphereDensity()
            fi.Vessel.dynamicPressurekPa = fi.dynamicPressurekPa = 0.0005 * fi.density * fi.spd * fi.spd;
            fi.Vessel.speedOfSound = fi.CurrentMainBody.GetSpeedOfSound(fi.staticPressurekPa, fi.density);
            fi.Vessel.mach = fi.mach = fi.Vessel.speedOfSound > 0.0 ? fi.spd / fi.Vessel.speedOfSound : 0.0;

            fi.convectiveMachLerp = Math.Pow(UtilMath.Clamp01((fi.mach - PhysicsGlobals.NewtonianMachTempLerpStartMach) / (PhysicsGlobals.NewtonianMachTempLerpEndMach - PhysicsGlobals.NewtonianMachTempLerpStartMach)), PhysicsGlobals.NewtonianMachTempLerpExponent);
            fi.Vessel.externalTemperature = fi.externalTemperature = Math.Max(fi.atmosphericTemperature, fi.BaseFICalculateShockTemperature());
            fi.Vessel.convectiveCoefficient = fi.convectiveCoefficient = CalculateConvectiveCoeff(fi);
            fi.pseudoReynolds = fi.density * fi.spd;
            fi.pseudoReLerpTimeMult = 1.0 / (PhysicsGlobals.TurbulentConvectionEnd - PhysicsGlobals.TurbulentConvectionStart);
            fi.pseudoReDragMult = (double)PhysicsGlobals.DragCurvePseudoReynolds.Evaluate((float)fi.pseudoReynolds);
        }

        double CalculateConvectiveCoeff(ModularFlightIntegrator fi) //I would love to clean this up, but it works and I dont wanna touch it.
        {
            double coeff;
            double density = fi.density;
            double spd = fi.spd;
            if (fi.Vessel.situation == Vessel.Situations.SPLASHED)
            {
                coeff = (PhysicsGlobals.ConvectionFactorSplashed * PhysicsGlobals.NewtonianConvectionFactorBase + Math.Pow(spd, PhysicsGlobals.NewtonianVelocityExponent) * 10.0) * PhysicsGlobals.NewtonianConvectionFactorTotal;
            }
            else if (fi.convectiveMachLerp == 0.0)
            {
                coeff = CalculateConvecCoeffNewtonian(density, spd); 
            }
            else if (fi.convectiveMachLerp == 1.0)
            {
                coeff = CalculateConvecCoeffMach(density, spd);
            }
            else
            {
                coeff = UtilMath.LerpUnclamped(CalculateConvecCoeffNewtonian(density, spd), CalculateConvecCoeffMach(density, spd), fi.convectiveMachLerp);
            }
            return coeff * fi.CurrentMainBody.convectionMultiplier;
        }
        double CalculateConvecCoeffNewtonian(double density, double spd)
        {
            double coeff = density <= 1.0 ? Math.Pow(density, PhysicsGlobals.NewtonianDensityExponent) : density;
            double multiplier = PhysicsGlobals.NewtonianConvectionFactorBase + Math.Pow(spd, PhysicsGlobals.NewtonianVelocityExponent);
            return coeff * multiplier * PhysicsGlobals.NewtonianConvectionFactorTotal;
        }
        double CalculateConvecCoeffMach(double density, double spd) //used to take in fi. now passes variables to prevent object references from being tossed around like hot potatoes
        {
            double coeff = density <= 1.0 ? Math.Pow(density, PhysicsGlobals.MachConvectionDensityExponent) : density;
            return coeff * 1E-07 * PhysicsGlobals.MachConvectionFactor * Math.Pow(spd, PhysicsGlobals.MachConvectionVelocityExponent);
        }

        void CalcPressureOverride(ModularFlightIntegrator fi)
        {
            if (FH == null || !FH.HasPress)
            {
                fi.BaseFICalculatePressure();
                return;
            }

            if (fi.CurrentMainBody.atmosphere && fi.altitude <= fi.CurrentMainBody.atmosphereDepth)
            {
                fi.staticPressurekPa = FH.Pressure;
                fi.staticPressureAtm = fi.staticPressurekPa * 0.0098692326671601278;
            }
            else
            {
                fi.staticPressureAtm = fi.staticPressurekPa = 0.0;
            }
        }
    }

    //--------------------------HARMONY PATCHES-------------------------------

    //Add an offset to the velocity vector used for wing lift calculations to account for wind.
    [HarmonyPatch(typeof(ModuleLiftingSurface), nameof(ModuleLiftingSurface.SetupCoefficients))]
    public static class WingVectorOverride
    {
        static void Prefix(ref Vector3 pointVelocity, ModuleLiftingSurface __instance)
        {
            MCWS_FlightHandler FH = MCWS_FlightHandler.Instance;
            if (!pointVelocity.IsFinite() || FH == null || !FH.HasWind)
            {
                return;
            }
            Vector3 windvec = FH.InternalAppliedWind;
            if (!windvec.IsFinite() || Mathf.Approximately(windvec.magnitude, 0.0f))
            {
                return;
            }
            float submerged = (float)__instance.part.submergedPortion;
            windvec.LerpWith(Vector3.zero, submerged * submerged);
            pointVelocity.Subtract(windvec);
        }
    }

    //Modify air intake behavior so wind affects intake performance.
    [HarmonyPatch(typeof(ModuleResourceIntake), nameof(ModuleResourceIntake.FixedUpdate))]
    public static class IntakeOverride
    {
        static bool Prefix(ModuleResourceIntake __instance) //This is an abomination. Please msg me if you have a cleaner implementation.
        {
            //fall back to stock behavior as a failsafe
            MCWS_FlightHandler FH = MCWS_FlightHandler.Instance;
            if (__instance == null || FH == null || !FH.HasWind)
            {
                return true;
            }
            Vector3 windvec = FH.InternalAppliedWind;
            if (!windvec.IsFinite() || Mathf.Approximately(windvec.magnitude, 0.0f))
            {
                return true;
            }
            float submerged = (float)__instance.part.submergedPortion;
            windvec.LerpWith(Vector3.zero, submerged * submerged);

            if (__instance.intakeEnabled && __instance.moduleIsEnabled && __instance.vessel != null && __instance.intakeTransform != null)
            {
                if (!__instance.part.ShieldedFromAirstream && !(__instance.checkNode && __instance.node.attachedPart != null))
                {
                    if (__instance.vessel.staticPressurekPa >= __instance.kPaThreshold && !(!__instance.vessel.mainBody.atmosphereContainsOxygen && __instance.checkForOxygen))
                    {
                        bool inocean = __instance.vessel.mainBody.ocean && FlightGlobals.getAltitudeAtPos(__instance.intakeTransform.position, __instance.vessel.mainBody) < 0.0;

                        //Get intake resource if one of the following is true:
                        //-both disableunderwater & underwateronly are false
                        //-disableunderwater is true and we're not in ocean
                        //-disableunderwater is false, underwateronly is true, and we're in ocean
                        if ((!__instance.disableUnderwater && !__instance.underwaterOnly) || (__instance.disableUnderwater && !inocean) || (!__instance.disableUnderwater && __instance.underwaterOnly && inocean))
                        {
                            //get intake resource
                            Vector3d vel = __instance.vessel.srf_velocity - (Vector3d)windvec;
                            double sqrmag = vel.sqrMagnitude;
                            double truespeed = Math.Sqrt(sqrmag);
                            Vector3d truedir = vel / truespeed;

                            double newmach = __instance.vessel.speedOfSound != 0.0 ? truespeed / __instance.vessel.speedOfSound : 0.0;

                            double intakeairspeed = (Mathf.Clamp01(Vector3.Dot((Vector3)truedir, __instance.intakeTransform.forward)) * truespeed) + __instance.intakeSpeed;
                            __instance.airSpeedGui = (float)intakeairspeed;
                            double intakemult = intakeairspeed * (__instance.unitScalar * __instance.area * (double)__instance.machCurve.Evaluate((float)newmach));
                            double airdensity = __instance.underwaterOnly ? __instance.vessel.mainBody.oceanDensity : __instance.vessel.atmDensity;
                            __instance.resourceUnits = intakemult * airdensity * __instance.densityRecip;

                            if (__instance.resourceUnits > 0.0)
                            {
                                __instance.airFlow = (float)__instance.resourceUnits;
                                __instance.resourceUnits *= (double)TimeWarp.fixedDeltaTime;
                                if (__instance.res.maxAmount - __instance.res.amount >= __instance.resourceUnits)
                                {
                                    __instance.part.TransferResource(__instance.resourceId, __instance.resourceUnits);
                                }
                                else
                                {
                                    __instance.part.RequestResource(__instance.resourceId, -__instance.resourceUnits);
                                }
                            }
                            else
                            {
                                __instance.resourceUnits = 0.0;
                                __instance.airFlow = 0.0f;
                            }
                            __instance.status = Localizer.Format("#autoLOC_235936");
                            return false;
                        } 
                    }
                    //drain the resource
                    __instance.airFlow = 0.0f;
                    __instance.airSpeedGui = 0.0f;
                    __instance.part.TransferResource(__instance.resourceId, double.MinValue);
                    __instance.status = Localizer.Format("#autoLOC_235946");
                    return false;
                }
                //do nothing
                __instance.airFlow = 0.0f;
                __instance.airSpeedGui = 0.0f;
                __instance.status = Localizer.Format("#autoLOC_235899");
                return false;
            }
            __instance.status = Localizer.Format("#autoLOC_8005416");
            return false;
        }
    }
}
