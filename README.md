# Drone Clearing for Dyson Sphere Program

**DSP Drone Clearing** is a mod for the Unity game Dyson Sphere Program developed by Youthcat Studio and published by Gamera Game.  The game is available on [here](https://store.steampowered.com/app/1366540/Dyson_Sphere_Program/).

This mod will use mecha drones to clear trees and stones.  Ever get annoyed tripping over rocks?  This mod is for you.

Enable and disable the clearing functionality in-game with this toggle button on your HUD.
![Enable Disable Button image](https://raw.githubusercontent.com/GreyHak/dsp-drone-clearing/master/EnableDisableButton.jpg)

If you like this mod, please click the thumbs up at the [top of the page](https://dsp.thunderstore.io/package/GreyHak/DSP_Drone_Clearing/) (next to the Total rating).  That would be a nice thank you for me, and help other people to find a mod you enjoy.

If you have issues with this mod, please report them on [GitHub](https://github.com/GreyHak/dsp-drone-clearing/issues).  You can also contact me at GreyHak#2995 on the [DSP Modding](https://discord.gg/XxhyTNte) Discord #tech-support channel.

## Config Settings
Configuration settings or loaded when you game is loaded.  So if you want to change the settings file, quit the game, but don't exit to desktop, then continue your game.

This mod is also compatible with [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager) which provides an in-game GUI for changing the settings in real-time.

Settings include
 - Mod enable flag.
 - Collect resources, or quickly clear the area.
 - Limit the number of drones used when clearing.
 - Control of clearing distance from mecha.
 - Conserve energy when Icarus' energy gets too low.
 - Clearing during walking and optionally while flying and drifting (floating over ocean).
 - Optionally recall drones tasked with clearing when Icarus starts flying.
 - Inventory space limitations.
 - Control over clearing of rocks, trees, small rocks and ice.
 - Control over planet types clearing is performed on.
 - Control the drone clearing speed.  Default is to clear at Icarus' mining speed.
 - Progress circle color.

![Config Settings Window image](https://raw.githubusercontent.com/GreyHak/dsp-drone-clearing/master/ConfigSettingsWindow.jpg)

Want fine-grained control over which items are not cleared?  If you tell the mod exactly what IDs not to clear using the DisableItemIds config setting, it will apply those rules.  DisableItemIds is a string containing a comma-separated list of vege proto ID shorts.  To determine what the ID of the item is you want to prevent clearing for, follow this process.
 - Update your `BepInEx.cfg` file, under `[Logging.Console]`, to set `Enabled = true`
 - In your `BepInEx.cfg` file, under `[Logging.Console]`, also set `LogLevels = All`
 - Load a save where you can access the item or items you wish to have the mod not clear.
 - If drone clearing is enabled, temporarily disable drone clearing using the toggle button on the HUD.
 - Use Icarus to remove the items you want this mod not to clear.
 - Each time Icarus removes an item this mod will print to the debug console the vege proto IDs which are removed.
 - Add that list of IDs as a comma-separated list, to the DisableItemIds setting in `greyhak.dysonsphereprogram.droneclearing.cfg`.
 - Once done, you can revert the settings in your `BepInEx.cfg` file if you want.

The configuration file is called `greyhak.dysonsphereprogram.droneclearing.cfg`.  It is generated the first time you run the game with this mod installed.  On Windows 10 it is located at
 - If you installed manually:  `%PROGRAMFILES(X86)%\Steam\steamapps\common\Dyson Sphere Program\BepInEx\config\greyhak.dysonsphereprogram.droneclearing.cfg`
 - If you installed with r2modman:  `C:\Users\<username>\AppData\Roaming\r2modmanPlus-local\DysonSphereProgram\profiles\Default\BepInEx\config\greyhak.dysonsphereprogram.droneclearing.cfg`

## Installation
This mod uses the BepInEx mod plugin framework.  So BepInEx must be installed to use this mod.  Find details for installing BepInEx [in their user guide](https://bepinex.github.io/bepinex_docs/master/articles/user_guide/installation/index.html#installing-bepinex-1).  This mod was tested with BepInEx x64 5.4.11.0 and Dyson Sphere Program 0.8.22.9331 on Windows 10.

To manually install this mod, add the `DysonSphereDroneClearing.dll` to your `%PROGRAMFILES(X86)%\Steam\steamapps\common\Dyson Sphere Program\BepInEx\plugins\` folder.

This mod can also be installed using ebkr's [r2modman](https://dsp.thunderstore.io/package/ebkr/r2modman/) mod manager by clicking "Install with Mod Manager" on the [DSP Modding](https://dsp.thunderstore.io/package/GreyHak/DSP_Star_Sector_Resource_Spreadsheet_Generator/) site.

## Open Source
The source code for this mod is available for download, review and forking on GitHub [here](https://github.com/GreyHak/dsp-drone-clearing) under the BSD 3 clause license.

## Change Log
### v1.4.5
 - Will now run with Dyson Sphere Program 0.8.22.9331 update.  Rebuild only.
### v1.4.4
 - Clearing will now count towards achievement statistics.
### v1.4.3
 - Save games will now work again with Dyson Sphere Program 0.8.22.8915 update.
### v1.4.2
 - Will now run with Dyson Sphere Program 0.7.18.6931 update.  Class variable swapped.
### v1.4.1
 - Unassigned drone clearing tasks which are beyond the build range will now be cancelled.  This will prevent unassignable tasks from queueing up and preventing new clearing tasks.
### v1.4.0
 - Drone torches with sparks.  This took me hours of work.  I hope someone appreciates it.
### v1.3.0
 - Drone clearing assignments will now be shown as soon as they're assigned.  You no longer need to wait until mining begins.
 - The progress circle color is now configurable.
### v1.2.14
 - Improved in-game configuration changes by improving compatibility with [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager).
### v1.2.13
 - Improved configurability to stop clearing for certain user-specified items.
### v1.2.12
 - Rebuild required for the recent Dyson Sphere Program [0.6.17.5932 update](https://store.steampowered.com/news/app/1366540/view/4178750236020840924).
### v1.2.11
 - Ability to enable/disable clearing of the space capsule separately from stones.  This is important when using the CollectResources=false setting so key early-game resources aren't lost.
 - Greater configurability as to which planet types to enable clearing on.
### v1.2.10
 - Bug fix to progress circle not clearing when drone tasking is cancelled.
### v1.2.9
 - Added progress circle like when Icarus mines rocks and trees.
### v1.2.8
 - Fixed a bug which was making the drones mine too fast, 4 times faster in the later game.  The speed was correct early game.
 - Added config setting to control the drone clearing speed, faster or slower.  If you got used to the drones clearing too quickly, you can restore that functionality.
### v1.2.7
 - Bug fixed with disabling plugin while off a planet in open space.
### v1.2.6
 - Added config settings for ClearWhileDrifting and RecallWhileFlying.
 - Drone performing clearing tasks will automatically be recalled when Icarus starts sailing.
 - Added tasking status to tool tip for enable/disable button.
 - Added bug reporting link to this README.
### v1.2.5
 - Major bug fixed which was causing drone clearing tasks to go unassigned causing drone clearing to stop.
### v1.2.4
 - Improved compatibility with the Render Distance mod.
### v1.2.3
 - Added a config option to enable more debug to the BepInEx console.
### v1.2.2
 - Fixed bug when loading the game with the mod for the first time.  (Bug added in v1.2.0 with the config reload functionality.)
 - Replaced deprecated function call.
 - Manifest description updated.
### v1.2.1
 - Fixed a bug where tasks were being assigned when drones weren't available.  This was causing configured energy constraints to be violated.  This issue was emphasized when fewer drones were available.
 - Small revision to the appearance of the enable/disable HUD image.
### v1.2.0
 - Added a button to the game HUD to enable and disable drone clearing.
 - Added a config setting to explicitly enable or disable the mod.
 - Reload configuration settings on each game start.
 - Reduced debug log messages.
### v1.1.1
 - Fixed compatibility with Windows10CE's DSP Cheats's Instant-Build feature to make instant clearing.
### v1.1.0
 - Fixed bug with drones getting locked up when Icarus completes their task first.
 - Clearing tasks no longer save.  This is a compromise to avoid issues when disabling/removing the mod.
 - Fixed bug when performing Quit Game that wasn't removing drone clearing tasks.
 - Improvement in drone usage calculation.  This provides a cleaner cut-off when clearing is shutoff due to drone count usage or energy availability.
 - Disable clearing when mecha energy level is too low (configurable).
 - Optimized mechanic for holding drone still while working.
### v1.0.0
 - Initial release.
