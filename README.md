# Modular Climate & Weather Systems
Modular Climate & Weather Systems (MCWS) is a plugin that provides a common interface for climate and weather simulations to interact with KSP's physics systems. 

The "Modular" part of MCWS comes from the fact that it can not only take in data from different sources for different bodies, including custom/modded ones, but can also take in data from separate sources for wind, temperature, and pressure. MCWS will automatically select its data sources based on the current main body, ensuring no conflicts between data sources for different bodies can occur.  It then uses this data to influence the game's aerodynamics and thermodynamics systems, including, to the best of my knowledge, the smoothest and fastest implementation of stockalike wind effects to date. 

A reader plugin included in the mod folder can be configured to read .bin files containing climate data that can then be used by MCWS. Mods featuring climate and/or weather data can also register with MCWS through its API to supply it with wind, temperature, and pressure data. Other mods can also read this data through the aforementioned API. 

Dependencies:
- ModularFlightIntegrator: https://forum.kerbalspaceprogram.com/topic/106369-19-modularflightintegrator-127-19-october-2019/
- Toolbar Controller: https://github.com/linuxgurugamer/ToolbarControl/releases
- ClickThrough Blocker: https://github.com/linuxgurugamer/ClickThroughBlocker/releases
- HarmonyKSP (bundled with download): https://github.com/KSPModdingLibs/HarmonyKSP/releases

## Mod Compatibility  
**Recommended Mods:**
- Kopernicus
- KSPCommunityFixes

**Compatible With:**
- FerramAerospaceResearch: If installed, MCWS will defer all relevant aero/thermal calculations to FAR and supply it with wind, temperature, and pressure information.
- Most, if not all parts mods

**Conflicts With:** 
- Other mods that modify the stock aerodynamics system

## Other Features
MCWS adds a few extra features to help you out when flying, including:
- A GUI which displays wind, temperature, and pressure information, along with other relevant aero- and thermodynamic information.
- A new pair of navball indicators which display prograde and retrograde adjusted for wind, which will only appear if you are in an atmosphere,  the Navball is set to "Surface" mode, the wind speed is greater than 0.5m/s, and the craft is in motion.

## Credits and Acknowledgements
- @sarbian, @ferram4, and @Starwaster for making the Modular Flight Integrator that allows interfacing with KSP's physics system.
