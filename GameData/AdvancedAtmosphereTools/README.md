# Advanced Atmosphere Tools
*This mod was originally named Modular Climate & Weather Systems. It was renamed to Advanced Atmosphere Tools reflect its new focus of manipulating atmospheres. Backwards compatibility with MCWS configs is provided.*

**Advanced Atmosphere Tools** is a plugin that enables manipulation of atmospheric properties on a less-than-planetary scale, allowing for far more variability in atmospheric conditions on a given body than would otherwise be seen in stock. The manipulated properties are then seamlessly integrated into KSP’s flight dynamics systems to influence how craft fly through the atmosphere, all while having a very low performance impact, even with high part counts.

Adv. Atmo. Tools provides ways to manipulate the following five atmospheric properties: 
- Temperature
- Pressure
- Molar Mass
- Adiabatic Index
- Wind
All properties can be influenced through a set of maps and floatcurves. Details on how to configure all the different options, as well as their effects on flight dynamics, can be found on the GitHub wiki. 
Link to wiki:

An API is also provided to allow other plugins to interact with Adv. Atmo. Tools.

Dependencies:
- ModularFlightIntegrator: https://github.com/sarbian/ModularFlightIntegrator
- Toolbar Controller: https://github.com/linuxgurugamer/ToolbarControl/releases
- ClickThrough Blocker: https://github.com/linuxgurugamer/ClickThroughBlocker/releases
- HarmonyKSP: https://github.com/KSPModdingLibs/HarmonyKSP/releases

## Mod Compatibility  
**Recommended Mods:**
- Kopernicus
- KSPCommunityFixes

**Compatible With:**
- FerramAerospaceResearch: If installed, Advanced Atmosphere Tools will defer all relevant aero/thermal calculations to FAR and supply it with wind, temperature, and pressure information.
- Most, if not all parts mods

**Conflicts With:** 
- Other mods that modify the stock aerodynamics system

## Other Features
Advanced Atmosphere Tools adds a few extra features to help you out when flying, including:
- A GUI which displays wind, temperature, and pressure information, along with other relevant aero- and thermodynamic information.
- A new pair of navball indicators which display prograde and retrograde adjusted for wind, which will only appear if you are in an atmosphere,  the Navball is set to "Surface" mode, the wind speed is greater than 0.5m/s, and the craft is in motion.

## Credits and Acknowledgements
- @sarbian, @ferram4, and @Starwaster for making the Modular Flight Integrator that allows interfacing with KSP's physics system.
