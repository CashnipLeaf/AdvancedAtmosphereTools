# Modular Climate & Weather Systems
Modular Climate & Weather Systems (MCWS) is a plugin that provides a common interface for climate and weather simulations to interact with KSP's physics systems. Mods featuring climate and/or weather data can register with MCWS through its API to supply it with wind, temperature, and pressure data. Other mods can also read this data through the aforementioned API. MCWS then uses this data to influence the game's aerodynamics and thermodynamics systems.

The concept of climate & weather simulations in KSP is not new. We can look to Kerbal Weather Project for a great example of that. But for all the cool things that KWP did, it's unfortunately hard-coded to stock Kerbin and doesn't provide any way to add data for other bodies. This severely limits its utility for planet mods, especially system replacers. MCWS seeks to fill this gap by providing a fully modular solution right out of the box.

The "Modular" part of MCWS comes from the fact that it can not only take in data from separate sources for wind, temperature, and pressure, but can take in data from different sources for different bodies, including custom/modded ones. MCWS will automatically select its data sources based on the current main body, ensuring no conflicts between data sources for different bodies can occur.

Dependencies:
- ModularFlightIntegrator: https://forum.kerbalspaceprogram.com/topic/106369-19-modularflightintegrator-127-19-october-2019/
- Toolbar Controller: https://github.com/linuxgurugamer/ToolbarControl/releases
- ClickThrough Blocker: https://github.com/linuxgurugamer/ClickThroughBlocker/releases
- Harmony for KSP (bundled): https://github.com/KSPModdingLibs/HarmonyKSP/releases

## Integration with other mods:
- FerramAerospaceResearch: If installed, MCWS will defer all relevant aero/thermal calculations to FAR and supply it with wind, temperature, and pressure information.

## Mod Compatibility  
**Recommended Mods:**
- Kopernicus
- KSPCommunityFixes

**Compatible With:**
- FerramAerospaceResearch
- Most, if not all parts mods

**Conflicts With:** 
- Other mods that modify the stock aerodynamics system

## Other Features
MCWS adds a few extra features to help you out when flying, including:
- A GUI which displays wind, temperature, and pressure information, along with other relevant aero- and thermodynamic information.
- A new pair of navball indicators which display prograde and retrograde adjusted for wind, which will only appear if you are in an atmosphere,  the Navball is set to "Surface" mode, the wind speed is greater than 0.5m/s, and the craft is in motion.

## Credits and Acknowledgements
- @sarbian, @ferram4, and @Starwaster for making the Modular Flight Integrator that allows interfacing with KSP's physics system.

## License Information
- MCWS is licensed under the MIT license.
