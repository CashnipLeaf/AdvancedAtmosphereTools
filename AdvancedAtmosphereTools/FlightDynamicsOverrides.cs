﻿using System;
using System.Reflection;
using KSP.Localization;
using UnityEngine;
using ModularFI;
using HarmonyLib;

namespace AdvancedAtmosphereTools
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class FlightDynamicsOverrides : MonoBehaviour
    {
        public static bool registeredoverrides = false;
        private static AAT_FlightHandler FH => AAT_FlightHandler.Instance;

        void Start()
        {
            if (!registeredoverrides) //make sure that things dont get patched more than once. That would be very bad.
            {
                Utils.LogInfo("Initializing Flight Dynamics Overrides.");
                //register overrides with ModularFI
                try
                {
                    Utils.LogInfo("Registering AdvancedAtmosphereTools with ModularFlightIntegrator.");
                    //If FAR is installed, do not override aerodynamics. Leave the aerodynamics calulations to FAR.
                    if (!Settings.FAR_Exists)
                    {
                        if (ModularFlightIntegrator.RegisterUpdateAerodynamicsOverride(NewAeroUpdate))
                        {
                            ModularFlightIntegrator.RegisterCalculateAerodynamicAreaOverride(AerodynamicAreaOverride);
                            Utils.LogInfo("Successfully registered AAT's Aerodynamics Overrides with ModularFlightIntegrator.");
                        }
                        else
                        {
                            Utils.LogWarning("Unable to register AAT's Aerodynamics Override with ModularFlightIntegrator.");
                        }
                    }
                    if (ModularFlightIntegrator.RegisterCalculatePressureOverride(CalcPressureOverride))
                    {
                        Utils.LogInfo("Successfully registered AAT's Pressure Override with ModularFlightIntegrator.");
                    }
                    else
                    {
                        Utils.LogWarning("Unable to register AAT's Pressure Override with ModularFlightIntegrator.");
                    }

                    if (ModularFlightIntegrator.RegistercalculateConstantsAtmosphereOverride(CalculateConstantsAtmosphereOverride))
                    {
                        Utils.LogInfo("Successfully registered AAT's Atmosphere and Thermodynamics Overrides with ModularFlightIntegrator.");
                    }
                    else
                    {
                        Utils.LogWarning("Unable to register AAT's Atmosphere and Thermodynamics Overrides with ModularFlightIntegrator.");
                    }
                    Utils.LogInfo("ModularFlightIntegrator Registration Complete.");
                }
                catch (Exception ex)
                {
                    Utils.LogError("ModularFlightIntegrator Registration Failed. Exception thrown: " + ex.ToString());
                }

                Utils.LogInfo("Patching Lifting Surface, Air Intake, and Kerbal Breathing behavior.");
                try
                {
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    Harmony harmony = new Harmony("AdvAtmoTools");
                    harmony.PatchAll(assembly);
                    Utils.LogInfo("Patching Complete.");
                }
                catch (Exception ex)
                {
                    Utils.LogError("Patching Failed. Exception thrown:" + ex.ToString());
                }
                registeredoverrides = true;
                Destroy(this);
            }
            else
            {
                Utils.LogWarning("Destroying duplicate Flight Dynamics Overrides. Check your install for duplicate mod folders.");
                DestroyImmediate(this);
            }
        }

        #region aerodynamics
        static void NewAeroUpdate(ModularFlightIntegrator fi, Part part)
        {
            //recalculate part static pressure
            if (FH != null && FH.HasPress)
            {
                double altitudeAtPos = FlightGlobals.getAltitudeAtPos((Vector3d)part.partTransform.position, fi.CurrentMainBody);
                //i dont wanna have to recalculate the pressure all over again for each part, so this is probably good enough. I'd rather not sandbag the runtime.
                double staticpress = fi.CurrentMainBody.GetPressure(altitudeAtPos) * FH.FIPressureMultiplier;
                if (fi.CurrentMainBody.ocean && altitudeAtPos <= 0.0)
                {
                    staticpress += fi.Vessel.gravityTrue.magnitude * -altitudeAtPos * fi.CurrentMainBody.oceanDensity;
                }
                staticpress *= 0.0098692326671601278;
                if (double.IsFinite(staticpress))
                {
                    part.staticPressureAtm = staticpress;
                }
            }

            //resume business as normal
            fi.BaseFIUpdateAerodynamics(part);
        }

        //Takes advantage of CalculateAerodynamicArea()'s placement inside UpdateAerodynamics() to inject a new drag vector into the part before UpdateAerodynamics() uses to calculate anything.
        static double AerodynamicAreaOverride(ModularFlightIntegrator fi, Part part)
        {
            Vector3 windvec = (FH != null && FH.HasWind) ? FH.InternalAppliedWind : Vector3.zero;
            float submerged = (float)part.submergedPortion;
            windvec.LerpWith(Vector3.zero, submerged * submerged);

            //add an offset to the velocity vector used for body drag/lift calcs and update the related fields.
            if (part.Rigidbody != null && windvec.IsFinite() && !Mathf.Approximately(windvec.magnitude, 0.0f))
            {
                part.dragVector = part.Rigidbody.velocity + Krakensbane.GetFrameVelocity() - windvec;
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

            //inlined CaclulateAerodynamicArea() to avoid passing an object reference again
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
        #endregion

        #region thermodynamics
        static void CalculateConstantsAtmosphereOverride(ModularFlightIntegrator fi)
        {
            Vector3 windvec = (FH != null && FH.HasWind) ? FH.InternalAppliedWind : Vector3.zero;
            if (windvec.IsFinite() && !Mathf.Approximately(windvec.magnitude, 0.0f))
            {
                fi.Vel -= windvec;
                fi.Vessel.speed = fi.spd = !fi.Vessel.IgnoreSpeedActive ? fi.Vel.magnitude : 0.0; ;
                fi.nVel = (fi.spd != 0.0) ? fi.Vel / (float)fi.spd : Vector3.zero;
            }

            fi.CurrentMainBody.GetSolarAtmosphericEffects(fi.sunDot, fi.density, out fi.solarAirMass, out fi.solarFluxMultiplier);
            fi.Vessel.solarFlux = (fi.solarFlux *= fi.solarFluxMultiplier);
            fi.Vessel.atmosphericTemperature = fi.atmosphericTemperature = (FH != null && FH.HasTemp) ? FH.Temperature : fi.CurrentMainBody.GetFullTemperature(fi.altitude, fi.atmosphereTemperatureOffset);
            
            double molarmass = FH.HasMolarMass ? FH.MolarMass : fi.CurrentMainBody.atmosphereMolarMass;
            fi.density = fi.Vessel.atmDensity = GetDensity(fi.staticPressurekPa, fi.atmosphericTemperature, molarmass); 
            fi.Vessel.dynamicPressurekPa = fi.dynamicPressurekPa = 0.0005 * fi.density * fi.spd * fi.spd;

            double adiabaticIndex = FH.HasAdiabaticIndex ? FH.AdiabaticIndex : fi.CurrentMainBody.atmosphereAdiabaticIndex;
            fi.Vessel.speedOfSound = GetSpeedOfSound(fi.staticPressurekPa, fi.density, adiabaticIndex);
            fi.Vessel.mach = fi.mach = fi.Vessel.speedOfSound > 0.0 ? fi.spd / fi.Vessel.speedOfSound : 0.0;

            fi.convectiveMachLerp = Math.Pow(UtilMath.Clamp01((fi.mach - PhysicsGlobals.NewtonianMachTempLerpStartMach) / (PhysicsGlobals.NewtonianMachTempLerpEndMach - PhysicsGlobals.NewtonianMachTempLerpStartMach)), PhysicsGlobals.NewtonianMachTempLerpExponent);
            fi.Vessel.externalTemperature = fi.externalTemperature = Math.Max(fi.atmosphericTemperature, fi.BaseFICalculateShockTemperature());
            fi.Vessel.convectiveCoefficient = fi.convectiveCoefficient = CalculateConvectiveCoeff(fi);
            fi.pseudoReynolds = fi.density * fi.spd;
            fi.pseudoReLerpTimeMult = 1.0 / (PhysicsGlobals.TurbulentConvectionEnd - PhysicsGlobals.TurbulentConvectionStart);
            fi.pseudoReDragMult = (double)PhysicsGlobals.DragCurvePseudoReynolds.Evaluate((float)fi.pseudoReynolds);
        }

        //CelestialBody.GetDensity() but manipulated for my own purposes
        static double GetDensity(double pressure, double temperature, double molarmass) => pressure > 0.0 && temperature > 0.0 ? (pressure * 1000 * molarmass) / (temperature * PhysicsGlobals.IdealGasConstant) : 0.0;
        
        //CelestialBody.GetSpeedOfSound() but manipulated for my own purposes
        static double GetSpeedOfSound(double pressure, double density, double adiabaticIndex) => pressure > 0.0 && density > 0.0 ? Math.Sqrt(adiabaticIndex * (pressure * 1000 /  density)) : 0.0;

        static double CalculateConvectiveCoeff(ModularFlightIntegrator fi) //I would love to clean this up, but it works and I dont wanna touch it.
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
        static double CalculateConvecCoeffNewtonian(double density, double spd)
        {
            double coeff = density <= 1.0 ? Math.Pow(density, PhysicsGlobals.NewtonianDensityExponent) : density;
            double multiplier = PhysicsGlobals.NewtonianConvectionFactorBase + Math.Pow(spd, PhysicsGlobals.NewtonianVelocityExponent);
            return coeff * multiplier * PhysicsGlobals.NewtonianConvectionFactorTotal;
        }
        static double CalculateConvecCoeffMach(double density, double spd) 
        {
            double coeff = density <= 1.0 ? Math.Pow(density, PhysicsGlobals.MachConvectionDensityExponent) : density;
            return coeff * 1E-07 * PhysicsGlobals.MachConvectionFactor * Math.Pow(spd, PhysicsGlobals.MachConvectionVelocityExponent);
        }

        static void CalcPressureOverride(ModularFlightIntegrator fi)
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
        #endregion
    }

    #region harmonypatches
    //--------------------------HARMONY PATCHES-------------------------------

    //Add an offset to the velocity vector used for wing lift calculations to account for wind.
    [HarmonyPatch(typeof(ModuleLiftingSurface), nameof(ModuleLiftingSurface.SetupCoefficients))]
    public static class WingVectorOverride
    {
        static void Prefix(ref Vector3 pointVelocity, ModuleLiftingSurface __instance)
        {
            AAT_FlightHandler FH = AAT_FlightHandler.Instance;
            if (!pointVelocity.IsFinite() || FH == null || !FH.HasWind || Settings.FAR_Exists)
            {
                return;
            }
            Vector3 windvec = FH.InternalAppliedWind;
            if (windvec.IsFinite() && !Mathf.Approximately(windvec.magnitude, 0.0f))
            {
                float submerged = (float)__instance.part.submergedPortion;
                windvec.LerpWith(Vector3.zero, submerged * submerged);
                pointVelocity -= windvec;
            }
        }
    }

    //Modify air intake behavior so wind affects intake performance.
    [HarmonyPatch(typeof(ModuleResourceIntake), nameof(ModuleResourceIntake.FixedUpdate))]
    public static class IntakeOverride
    {
        static bool Prefix(ModuleResourceIntake __instance) //This is an abomination. Please msg me if you have a cleaner implementation.
        {
            //fall back to stock behavior as a failsafe
            AAT_FlightHandler FH = AAT_FlightHandler.Instance;
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

    //replicates the functionality of Sigma Heat Shifter's maxTempAngleOffset
    [HarmonyPatch(typeof(CelestialBody),nameof(CelestialBody.GetAtmoThermalStats))]
    public static class AngleOffsetOverride
    {
        public static void Prefix(CelestialBody __instance, ref CelestialBody sunBody, ref Vector3d upAxis)
        {
            AAT_Startup Data = AAT_Startup.Instance;
            if (sunBody != __instance && Data != null && Data.BodyExists(__instance.name))
            {
                bool hasangleoffset = Data.HasMaxTempAngleOffset(__instance.name, out double angleoffset);
                if (hasangleoffset)
                {
                    Vector3 up = __instance.bodyTransform.up;
                    //rotate the vessel's upaxis to counteract the rotation applied by the game.
                    //default rotation is 45 degrees, so the default behavior is no rotation applied.
                    upAxis = Quaternion.AngleAxis((-45f + (float)angleoffset) * Mathf.Sign((float)__instance.rotationPeriod), up) * upAxis;
                }
            }
        }
    }

    //first of three harmony patches to decouple oxygen from breathability
    [HarmonyPatch(typeof(KerbalEVA),nameof(KerbalEVA.CanEVAWithoutHelmet))]
    public static class KerbalBreathHijacker1
    {
        public static void Postfix(ref bool __result, KerbalEVA __instance, ref string ___helmetUnsafeReason)
        {
            AAT_Startup Data = AAT_Startup.Instance;
            if (__result && Data != null)
            {
                string bodyname = __instance.vessel.mainBody.name;
                bool istoxic = Data.IsAtmosphereToxic(bodyname);
                if (istoxic)
                {
                    __result = false;
                    ___helmetUnsafeReason = Data.AtmosphereToxicMessage(bodyname);
                }
            }
        }
    }

    [HarmonyPatch(typeof(KerbalEVA), nameof(KerbalEVA.CanSafelyRemoveHelmet))]
    public static class KerbalBreathHijacker2
    {
        public static void Postfix(ref bool __result, KerbalEVA __instance, ref string ___helmetUnsafeReason)
        {
            AAT_Startup Data = AAT_Startup.Instance;
            if (__result && Data != null)
            {
                string bodyname = __instance.vessel.mainBody.name;
                bool istoxic = Data.IsAtmosphereToxic(bodyname);
                if (istoxic)
                {
                    __result = false;
                    ___helmetUnsafeReason = Data.AtmosphereToxicMessage(bodyname);
                }
            } 
        }
    }

    [HarmonyPatch(typeof(KerbalEVA), nameof(KerbalEVA.WillDieWithoutHelmet))]
    public static class KerbalBreathHijacker3
    {
        public static void Postfix(ref bool __result, KerbalEVA __instance, ref string ___helmetUnsafeReason)
        {
            AAT_Startup Data = AAT_Startup.Instance;
            if (!__result && Data != null)
            {
                string bodyname = __instance.vessel.mainBody.name;
                bool istoxic = Data.IsAtmosphereToxic(bodyname);
                if (istoxic)
                {
                    __result = true;
                    ___helmetUnsafeReason = Data.AtmosphereToxicMessage(bodyname);
                }
            }
        }
    }
    #endregion
}
