# Drone Clearing for Dyson Sphere Program

**DSP Drone Clearing** is a mod for the Unity game Dyson Sphere Program developed by Youthcat Studio and published by Gamera Game.  The game is available on [here](https://store.steampowered.com/app/1366540/Dyson_Sphere_Program/).

This mod will use mecha drones to clear trees and stones.  Ever get annoyed tripping over rocks?  This mod is for you.

## Config File

Settings include
 - Collect resources, or quickly clear the area.
 - Limit the number of drones used when clearing.
 - Control of clearing distance from mecha.
 - Conserve energy when Icarus' energy gets too low.
 - Clearing during walking and optionally while flying.
 - Inventory space limitations.
 - Control over clearing of rocks, trees, small rocks and ice.
 - Control over planet types clearing is performed on.

The configuration file is called greyhak.dysonsphereprogram.droneclearing.cfg.  It is generated the first time you run the game with this mod installed.  On Windows 10 it is located at
"%PROGRAMFILES(X86)%\Steam\steamapps\common\Dyson Sphere Program\BepInEx\config\greyhak.dysonsphereprogram.droneclearing.cfg".  

## Installation
This mod uses the BepInEx mod plugin framework.  So BepInEx must be installed to use this mod.  Find details for installing BepInEx [in their user guide](https://bepinex.github.io/bepinex_docs/master/articles/user_guide/installation/index.html#installing-bepinex-1).  This mod was tested with BepInEx x64 5.4.5.0 and Dyson Sphere Program 0.6.16.5775 on Windows 10.

To manually install this mod, add the DysonSphereDroneClearing.dll to your %PROGRAMFILES(X86)%\Steam\steamapps\common\Dyson Sphere Program\BepInEx\plugins\ folder.

This mod can also be installed using ebkr's [r2modman dsp](https://dsp.thunderstore.io/package/ebkr/r2modman_dsp/) mod manager by clicking "Install with Mod Manager" on the [DSP Modding](https://dsp.thunderstore.io/package/GreyHak/DSP_Star_Sector_Resource_Spreadsheet_Generator/) site.

## Open Source
The source code for this mod is available for download, review and forking on GitHub [here](https://github.com/GreyHak/dsp-csv-gen) under the BSD 3 clause license.

## Change Log
### v1.1.0 (inwork)
 - Improvement in drone usage calculation.  This provides a cleaner cut-off when clearing is shutoff due to drone count usage or energy availability.
 - Disable clearing when mecha energy level is too low (configurable).
 - Optimized mechanic for holding drone still while working.
### v1.0.0
 - Initial release.
