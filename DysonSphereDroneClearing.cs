//
// Copyright (c) 2021, Aaron Shumate
// All rights reserved.
//
// This source code is licensed under the BSD-style license found in the
// LICENSE.txt file in the root directory of this source tree. 
//
// Dyson Sphere Program is developed by Youthcat Studio and published by Gamera Game.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using BepInEx.Logging;
using System.Security;
using System.Reflection;
using System.Security.Permissions;

[module: UnverifiableCode]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete
namespace DysonSphereDroneClearing
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("DSPGAME.exe")]
    [BepInProcess("Dyson Sphere Program.exe")]
    public class DysonSphereDroneClearing : BaseUnityPlugin
    {
        public const string pluginGuid = "greyhak.dysonsphereprogram.droneclearing";
        public const string pluginName = "DSP Drone Clearing";
        public const string pluginVersion = "1.4.9";
        new internal static ManualLogSource Logger;
        new internal static BepInEx.Configuration.ConfigFile Config;
        Harmony harmony;

        public static BepInEx.Configuration.ConfigEntry<bool> configEnableMod;
        public static BepInEx.Configuration.ConfigEntry<bool> configCollectResourcesFlag;
        public static BepInEx.Configuration.ConfigEntry<uint> configMaxClearingDroneCount;
        public static BepInEx.Configuration.ConfigEntry<float> configLimitClearingDistance;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingWhileDrifting;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingWhileFlying;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableRecallWhileFlying;
        public static BepInEx.Configuration.ConfigEntry<uint> configReservedInventorySpace;
        public static BepInEx.Configuration.ConfigEntry<float> configReservedPower;
        public static BepInEx.Configuration.ConfigEntry<float> configSpeedScaleFactor;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableInstantClearing;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableDebug;
        public static BepInEx.Configuration.ConfigEntry<float> configProgressColor_Red;
        public static BepInEx.Configuration.ConfigEntry<float> configProgressColor_Green;
        public static BepInEx.Configuration.ConfigEntry<float> configProgressColor_Blue;

        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingItemTree;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingItemStone;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingItemDetail;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingItemIce;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingItemSpaceCapsule;
        public static BepInEx.Configuration.ConfigEntry<string> configDisableClearingItemIds_StringConfigEntry;
        public static short[] configDisableClearingItemIds_ShortArray;

        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetAridDesert;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetAshenGelisol;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetBarrenDesert;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetGobi;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetIceFieldGelisol;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetLava;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetMediterranean;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetOceanWorld;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetOceanicJungle;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetPrairie;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetRedStone;
        public static BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetVolcanicAsh;

        // The following class was copied from PlayerAction_Mine
        public class DroneAction_Mine
        {
            public Player player;
            public float percent;
            public int miningProtoId;
            public EObjectType miningType;
            public int miningId;
            public int miningTick;

            // This function is based on PlayerAction_Mine.GameTick
            public void DroneGameTick()
            {
                double powerFactor = 0.01666666753590107;
                PlanetFactory factory = this.player.factory;
                if (factory == null)
                {
                    // This has only been seen briefly when the Render Distance mod is transitioning to or from a planet view.
                    return;
                }
                double miningEnergyCost = this.player.mecha.miningPower * powerFactor;
                double energyAvailable;
                float fractionOfEnergyAvailable;
                this.player.mecha.QueryEnergy(miningEnergyCost, out energyAvailable, out fractionOfEnergyAvailable);
                int miningTime = (int)(this.player.mecha.miningSpeed * configSpeedScaleFactor.Value * fractionOfEnergyAvailable * 10000f + 0.49f);

                VegeData vegeData = factory.GetVegeData(this.miningId);
                this.miningProtoId = (int)vegeData.protoId;
                VegeProto vegeProto = LDB.veges.Select((int)vegeData.protoId);
                if (vegeProto != null)
                {
                    this.miningTick += miningTime;
                    this.player.mecha.coreEnergy -= energyAvailable;
                    this.player.mecha.MarkEnergyChange(5, -miningEnergyCost);
                    this.percent = Mathf.Clamp01((float)((double)this.miningTick / (double)(vegeProto.MiningTime * 10000)));
                    if (this.miningTick >= vegeProto.MiningTime * 10000)
                    {
                        System.Random random = new System.Random(vegeData.id + ((this.player.planetData.seed & 16383) << 14));
                        bool inventoryOverflowFlag = false;
                        int popupQueueIndex = 0;
                        for (int itemIdx = 0; itemIdx < vegeProto.MiningItem.Length; itemIdx++)
                        {
                            float randomMiningChance = (float)random.NextDouble();
                            if (randomMiningChance < vegeProto.MiningChance[itemIdx])
                            {
                                int minedItem = vegeProto.MiningItem[itemIdx];
                                int minedItemCount = (int)((float)vegeProto.MiningCount[itemIdx] * (vegeData.scl.y * vegeData.scl.y) + 0.5f);
                                if (minedItemCount > 0 && LDB.items.Select(minedItem) != null)
                                {
                                    int inventoryOverflowCount = this.player.TryAddItemToPackage(minedItem, minedItemCount, 0, true, 0);
                                    GameMain.statistics.production.factoryStatPool[factory.index].AddProductionToTotalArray(minedItem, minedItemCount);
                                    GameMain.mainPlayer.controller.gameData.history.AddFeatureValue(2150000 + minedItem, minedItemCount);
                                    if (inventoryOverflowCount > 0)
                                    {
                                        UIItemup.Up(minedItem, inventoryOverflowCount);
                                        UIRealtimeTip.PopupItemGet(minedItem, inventoryOverflowCount, vegeData.pos + vegeData.pos.normalized, popupQueueIndex++);
                                    }
                                    else  // Unable to fit all items
                                    {
                                        inventoryOverflowFlag = true;
                                    }
                                }
                            }
                        }
                        VFEffectEmitter.Emit(vegeProto.MiningEffect, vegeData.pos, vegeData.rot);
                        VFAudio.Create(vegeProto.MiningAudio, null, vegeData.pos, true, 0);
                        factory.RemoveVegeWithComponents(vegeData.id);
                        GameMain.gameScenario.NotifyOnVegetableMined((int)vegeData.protoId);
                        this.miningType = EObjectType.Entity;  // This change will cause the mission to be completed.
                        this.miningId = 0;
                        if (inventoryOverflowFlag)
                        {
                            //Logger.LogInfo("Inventory overflow detected.");
                        }
                        this.miningTick = 0;
                    }
                }
                else
                {
                    //Logger.LogInfo("null vegeProto.  Icarus likely removed clearing target.");
                    this.miningType = EObjectType.Entity;  // This change will cause the mission to be completed.
                    this.miningId = 0;
                    this.miningTick = 0;
                    this.percent = 0f;
                    factory.RemoveVegeWithComponents(vegeData.id);
                }
            }
        }

        public static bool isDroneClearingPrebuild(PrebuildData prebuild)
        {
            return
                prebuild.protoId == -1 &&
                prebuild.colliderId == -1 &&
                prebuild.pickOffset == -1 &&
                prebuild.filterId == -1 &&
                prebuild.recipeId == -1;
        }

        public class DroneClearingMissionData
        {
            public int prebuildId = -1;
            public DroneAction_Mine mineAction = null;
            public CircleGizmo miningTargetGizmo = null;
            public bool miningFlag = false;
            public ParticleSystem torchEffect = null;
        }
        public static List<DroneClearingMissionData> activeMissions = new List<DroneClearingMissionData> { };

        public static int getTotalDroneTaskingCount()
        {
            int totalDroneTaskCount = 0;
            if (GameMain.mainPlayer != null &&
                GameMain.mainPlayer.factory != null)  // A null GameMain.mainPlayer.factory is normal during the load screen.
            {
                totalDroneTaskCount = GameMain.mainPlayer.mecha.droneLogic.serving.Count;

                for (int i = 1; i < GameMain.mainPlayer.factory.prebuildCursor; i++)
                {
                    if (GameMain.mainPlayer.factory.prebuildPool[i].id == i)
                    {
                        if (!GameMain.mainPlayer.mecha.droneLogic.serving.Contains(-i))
                        {
                            totalDroneTaskCount++;  // These are queued, but not yet assigned
                        }
                    }
                }

                if (configEnableDebug.Value)
                {
                    Logger.LogInfo(totalDroneTaskCount.ToString() + " tasked drone count made up of " + GameMain.mainPlayer.mecha.droneLogic.serving.Count.ToString() + " assigned and " + (totalDroneTaskCount - GameMain.mainPlayer.mecha.droneLogic.serving.Count).ToString() + " unassigned drones.");
                }
            }
            return totalDroneTaskCount;
        }

        public void Awake()
        {
            Logger = base.Logger;  // "C:\Program Files (x86)\Steam\steamapps\common\Dyson Sphere Program\BepInEx\LogOutput.log"
            Config = base.Config;
            InitialConfigSetup();

            // PlayerOrder.ReachTest, PlayerAction_Mine.GameTick
            // MechaDroneLogic.UpdateDrones -> MechaDroneLogic.Build -> PlanetFactory.BuildFinally
            // PlanetFactory.BuildFinally calls FlattenTerrain and SetSandCount.
            harmony = new Harmony(pluginGuid);
            harmony.PatchAll(typeof(DysonSphereDroneClearing));

            enabledSprite = GetSprite(new Color(0, 1, 0));  // Bright Green
            //Sprite ingameDrone = Resources.Load<Sprite>("ui/textures/sprites/icons/drone-icon");
            //enabledSprite = GameObject.Instantiate<Sprite>(ingameDrone);
            disabledSprite = GetSprite(new Color(0.5f, 0.5f, 0.5f));  // Medium Grey

            Logger.LogInfo("Initialization complete.");
        }

        public static RectTransform enableDisableButton;
        public static Sprite enabledSprite;
        public static Sprite disabledSprite;
        public static bool clearDroneTaskingOnNextTick = false;

        [HarmonyPrefix, HarmonyPatch(typeof(GameMain), "Begin")]
        public static void GameMain_Begin_Prefix()
        {
            Config.Reload();
            if (GameMain.instance != null && GameObject.Find("Game Menu/button-1-bg") && enableDisableButton == null)
            {
                RectTransform parent = GameObject.Find("Game Menu").GetComponent<RectTransform>();
                RectTransform prefab = GameObject.Find("Game Menu/button-1-bg").GetComponent<RectTransform>();
                Vector3 referencePosition = GameObject.Find("Game Menu/button-1-bg").GetComponent<RectTransform>().localPosition;
                enableDisableButton = GameObject.Instantiate<RectTransform>(prefab);
                enableDisableButton.gameObject.name = "greyhak-clearing-enable-button";
                enableDisableButton.GetComponent<UIButton>().tips.tipTitle = configEnableMod.Value ? "Drone Clearing Enabled" : "Drone Clearing Disabled";
                enableDisableButton.GetComponent<UIButton>().tips.tipText = configEnableMod.Value ? "Click to disable drone clearing" : "Click to enable drone clearing";
                enableDisableButton.GetComponent<UIButton>().tips.delay = 0f;
                enableDisableButton.transform.Find("button-1/icon").GetComponent<Image>().sprite =
                    configEnableMod.Value ? enabledSprite : disabledSprite;
                enableDisableButton.SetParent(parent);
                enableDisableButton.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                enableDisableButton.localPosition = new Vector3(referencePosition.x + 96f, referencePosition.y + 161f, referencePosition.z);
                enableDisableButton.GetComponent<UIButton>().OnPointerDown(null);
                enableDisableButton.GetComponent<UIButton>().OnPointerEnter(null);
                enableDisableButton.GetComponent<UIButton>().button.onClick.AddListener(() =>
                {
                    configEnableMod.Value = !configEnableMod.Value;
                    OnConfigEnableChanged();
                });
            }
        }

        public static void UpdateTipText(String details)
        {
            enableDisableButton.GetComponent<UIButton>().tips.tipText = configEnableMod.Value ? "Click to disable drone clearing" + "\n" + details : "Click to enable drone clearing";
            enableDisableButton.GetComponent<UIButton>().UpdateTip();
        }

        public static Sprite GetSprite(Color color)
        {
            Texture2D tex = new Texture2D(48, 48, TextureFormat.RGBA32, false);

            // Draw a plane like the one representing drones in the Mecha Panel...
            // The in-game asset is called ui/textures/sprites/icons/drone-icon
            for (int x = 0; x < 48; x++)
            {
                for (int y = 0; y < 48; y++)
                {
                    if (((x >= 9) && (x <= 17) && (y >= 2) && (y <= 38)) ||
                        ((x >= 15) && (x <= 23) && (y >= 12) && (y <= 38)) ||
                        ((x >= 21) && (x <= 29) && (y >= 18) && (y <= 38)) ||
                        ((x >= 27) && (x <= 35) && (y >= 24) && (y <= 38)) ||
                        ((x >= 33) && (x <= 44) && (y >= 30) && (y <= 38)))
                    {
                        tex.SetPixel(x, y, color);
                    }
                    else
                    {
                        tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                    }
                }
            }

            tex.name = "greyhak-clearing-enable-icon";
            tex.Apply();

            return Sprite.Create(tex, new Rect(0f, 0f, 48f, 48f), new Vector2(0f, 0f), 1000);
        }

        public void InitialConfigSetup()
        {
            bool configFileContainsOldFlagsFlag = false;
            FieldInfo[] fields = typeof(BepInEx.Configuration.ConfigFile).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                if (field.Name == "<OrphanedEntries>k__BackingField")
                {
                    Dictionary<BepInEx.Configuration.ConfigDefinition, string> orphanedEntries =
                        (Dictionary<BepInEx.Configuration.ConfigDefinition, string>)field.GetValue(Config);
                    foreach (BepInEx.Configuration.ConfigDefinition key in orphanedEntries.Keys)
                    {
                        if (key.Section == "Planets" && key.Key == "IncludeGeneric")
                        {
                            configFileContainsOldFlagsFlag = true;
                            break;
                        }
                    }
                    if (configFileContainsOldFlagsFlag)
                    {
                        break;
                    }
                }
            }

            configEnableMod = Config.Bind<bool>("Config", "Enable", true, "Enable/disable drone clearing mod.");
            configCollectResourcesFlag = Config.Bind<bool>("Config", "CollectResources", true, "Take time to collect resources. If false, clearing will be quicker, but no resources will be collected.");
            configMaxClearingDroneCount = Config.Bind<uint>("Config", "DroneCountLimit", Mecha.kMaxDroneCount, new BepInEx.Configuration.ConfigDescription("Limit the number of drones that will be used when clearing.", new BepInEx.Configuration.AcceptableValueRange<uint>(0, Mecha.kMaxDroneCount)));
            configLimitClearingDistance = Config.Bind<float>("Config", "ClearingDistance", 0.4f, new BepInEx.Configuration.ConfigDescription("Fraction of mecha build distance to perform clearing.", new BepInEx.Configuration.AcceptableValueRange<float>(0, 1)));
            configEnableClearingWhileDrifting = Config.Bind<bool>("Config", "ClearWhileDrifting", true, "This flag can be used to enable/disable clearing while drifing over oceans.");
            configEnableClearingWhileFlying = Config.Bind<bool>("Config", "ClearWhileFlying", false, "This flag can be used to enable/disable clearing while flying.");
            configEnableRecallWhileFlying = Config.Bind<bool>("Config", "RecallWhileFlying", true, "Enable this feature if you want drones assigned to clearing to be recalled when Icarus is flying. (This setting is only used if configEnableClearingWhileFlying is false.)");
            configReservedInventorySpace = Config.Bind<uint>("Config", "InventorySpace", 10, "Initiate clearing when there are this number of inventory spaces empty.  (Setting has no impact if CollectResources is false.)");
            configReservedPower = Config.Bind<float>("Config", "PowerReserve", 0.4f, new BepInEx.Configuration.ConfigDescription("Initiate clearing only when there is at least this fraction of Icarus's power remaining.", new BepInEx.Configuration.AcceptableValueRange<float>(0, 1)));
            configSpeedScaleFactor = Config.Bind<float>("Config", "SpeedScale", 1.0f, "Is this mod so great that it feels too much like cheating?  Slow the drones down with this setting.  They normally operate at the same speed as Icarus.  Too slow?  You can cheat too by setting a value greater than 1.");
            configEnableInstantClearing = Config.Bind<bool>("Config", "DSPCheats_InstantClearing", false, "If the DSP Cheats mod is installed, and Instant-Build is enabled, should this mod work with that one and instantly clear?");
            configEnableDebug = Config.Bind<bool>("Config", "EnableDebug", false, "Enabling debug will add more feedback to the BepInEx console.  This includes the reasons why drones are not clearing.");

            // Color from Configs.builtin.gizmoColors[2] = {1, 0.7052167, 0}, but it hasn't been loaded yet.
            configProgressColor_Red = Config.Bind<float>("Config", "ProgressColor_Red", 1.0f, new BepInEx.Configuration.ConfigDescription("The color of the clearing progress circle (red component).", new BepInEx.Configuration.AcceptableValueRange<float>(0, 1)));
            configProgressColor_Green = Config.Bind<float>("Config", "ProgressColor_Green", 0.7052167f, new BepInEx.Configuration.ConfigDescription("The color of the clearing progress circle (green component).", new BepInEx.Configuration.AcceptableValueRange<float>(0, 1)));
            configProgressColor_Blue = Config.Bind<float>("Config", "ProgressColor_Blue", 0.0f, new BepInEx.Configuration.ConfigDescription("The color of the clearing progress circle (blue component).", new BepInEx.Configuration.AcceptableValueRange<float>(0, 1)));

            configEnableClearingItemTree = Config.Bind<bool>("Items", "IncludeTrees", true, "Enabling clearing of trees.");
            configEnableClearingItemStone = Config.Bind<bool>("Items", "IncludeStone", true, "Enabling clearing of stones which can block the mecha's movement.");
            configEnableClearingItemDetail = Config.Bind<bool>("Items", "IncludePebbles", false, "Enabling clearing of tiny stones which won't block the mecha's movement.");
            configEnableClearingItemIce = Config.Bind<bool>("Items", "IncludeIce", true, "Enabling clearing of ice.");
            configEnableClearingItemSpaceCapsule = Config.Bind<bool>("Items", "IncludeSpaceCapsule", false, "Enabling clearing of space capsule.  (This setting is false by default just in case you set CollectResources to false, and then start a new game.)");
            configDisableClearingItemIds_StringConfigEntry = Config.Bind<string>("Items", "DisableItemIds", "", "Disable clearing of specific vege proto IDs.  String is a comma-separated list of shorts.  This mod will print to the debug console all vege proto IDs which are mined so you can see what IDs you're mining.  See README for this mod for more information.");

            configEnableClearingPlanetAridDesert = Config.Bind<bool>("Planets", "IncludeAridDesert", true, "Enable clearing on arid desert planets.");
            configEnableClearingPlanetAshenGelisol = Config.Bind<bool>("Planets", "IncludeAshenGelisol", true, "Enable clearing on ashen gelisol planets.");
            configEnableClearingPlanetBarrenDesert = Config.Bind<bool>("Planets", "IncludeBarrenDesert", true, "Enable clearing on barren desert planets.");
            configEnableClearingPlanetGobi = Config.Bind<bool>("Planets", "IncludeGobi", true, "Enable clearing on gobi planets.");
            configEnableClearingPlanetIceFieldGelisol = Config.Bind<bool>("Planets", "IncludeIceFieldGelisol", true, "Enable clearing on ice field gelisol planets.");
            configEnableClearingPlanetLava = Config.Bind<bool>("Planets", "IncludeLava", true, "Enable clearing on lava planets.");
            configEnableClearingPlanetMediterranean = Config.Bind<bool>("Planets", "IncludeMediterranean", true, "Enable clearing on mediterranean planets.");
            configEnableClearingPlanetOceanWorld = Config.Bind<bool>("Planets", "IncludeOceanWorld", true, "Enable clearing on ocean world planets.");
            configEnableClearingPlanetOceanicJungle = Config.Bind<bool>("Planets", "IncludeOceanicJungle", true, "Enable clearing on oceanic jungle planets.");
            configEnableClearingPlanetPrairie = Config.Bind<bool>("Planets", "IncludePrairie", true, "Enable clearing on prairie planets.");
            configEnableClearingPlanetRedStone = Config.Bind<bool>("Planets", "IncludeRedStone", true, "Enable clearing on red stone (mushroom) planets.");
            configEnableClearingPlanetVolcanicAsh = Config.Bind<bool>("Planets", "IncludeVolcanicAsh", true, "Enable clearing on volcanic ash planets.");

            // The following block of code handles converting from the v1.2.10 to v1.3.0 config settings.
            if (configFileContainsOldFlagsFlag)
            {
                Logger.LogDebug("Old (pre-v1.3.0) config settings exist.  Converting over now.");

                BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetGeneric = Config.Bind<bool>("Planets", "IncludeGeneric", true, "<Obsolete>");
                BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetVolcanic = Config.Bind<bool>("Planets", "IncludeVolcanic", true, "<Obsolete>");
                BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetOcean = Config.Bind<bool>("Planets", "IncludeOcean", true, "<Obsolete>");
                BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetDesert = Config.Bind<bool>("Planets", "IncludeDesert", true, "<Obsolete>");
                BepInEx.Configuration.ConfigEntry<bool> configEnableClearingPlanetIce = Config.Bind<bool>("Planets", "IncludeIce", true, "<Obsolete>");

                configEnableClearingPlanetLava.Value = configEnableClearingPlanetVolcanic.Value;
                configEnableClearingPlanetVolcanicAsh.Value = configEnableClearingPlanetVolcanic.Value;

                configEnableClearingPlanetMediterranean.Value = configEnableClearingPlanetOcean.Value;
                configEnableClearingPlanetOceanWorld.Value = configEnableClearingPlanetOcean.Value;
                configEnableClearingPlanetOceanicJungle.Value = configEnableClearingPlanetOcean.Value;
                configEnableClearingPlanetPrairie.Value = configEnableClearingPlanetOcean.Value;
                configEnableClearingPlanetRedStone.Value = configEnableClearingPlanetOcean.Value;

                configEnableClearingPlanetAridDesert.Value = configEnableClearingPlanetDesert.Value;
                configEnableClearingPlanetAshenGelisol.Value = configEnableClearingPlanetDesert.Value;
                configEnableClearingPlanetBarrenDesert.Value = configEnableClearingPlanetDesert.Value;
                configEnableClearingPlanetGobi.Value = configEnableClearingPlanetDesert.Value;

                configEnableClearingPlanetIceFieldGelisol.Value = configEnableClearingPlanetIce.Value;

                Config.Remove(configEnableClearingPlanetGeneric.Definition);
                Config.Remove(configEnableClearingPlanetVolcanic.Definition);
                Config.Remove(configEnableClearingPlanetOcean.Definition);
                Config.Remove(configEnableClearingPlanetDesert.Definition);
                Config.Remove(configEnableClearingPlanetIce.Definition);

                Config.Save();
            }

            OnConfigReload();
            Config.ConfigReloaded += OnConfigReload;
            Config.SettingChanged += OnConfigSettingChanged;
        }

        public static void OnConfigReload(object sender, EventArgs e)
        {
            OnConfigReload();
        }

        public static void OnConfigReload()
        {
            configLimitClearingDistance.Value = Math.Min(configLimitClearingDistance.Value, 1.0f);
            configLimitClearingDistance.Value = Math.Max(configLimitClearingDistance.Value, 0.0f);
            configSpeedScaleFactor.Value = Math.Max(configSpeedScaleFactor.Value, 0.0f);

            OnConfigDisableItemIdsChanged();
            OnConfigEnableChanged();

            Logger.LogInfo("Configuration loaded.");
        }

        public static void OnConfigSettingChanged(object sender, BepInEx.Configuration.SettingChangedEventArgs e)
        {
            BepInEx.Configuration.ConfigDefinition changedSetting = e.ChangedSetting.Definition;
            if (changedSetting.Section == "Config" && changedSetting.Key == "Enable")
            {
                OnConfigEnableChanged();
            }
            else if (changedSetting.Section == "Items" && changedSetting.Key == "DisableItemIds")
            {
                OnConfigDisableItemIdsChanged();
            }
        }

        public static void OnConfigEnableChanged()
        {
            if (enableDisableButton != null)
            {
                if (!configEnableMod.Value)
                {
                    clearDroneTaskingOnNextTick = true;
                }

                enableDisableButton.GetComponent<UIButton>().tips.tipTitle = configEnableMod.Value ? "Drone Clearing Enabled" : "Drone Clearing Disabled";
                enableDisableButton.transform.Find("button-1/icon").GetComponent<Image>().sprite =
                    configEnableMod.Value ? enabledSprite : disabledSprite;
                UpdateTipText("");
            }
        }

        public static void OnConfigDisableItemIdsChanged()
        {
            if (configDisableClearingItemIds_StringConfigEntry != null)
            {
                configDisableClearingItemIds_ShortArray = configDisableClearingItemIds_StringConfigEntry.Value.Split(',').Select(s => short.TryParse(s, out short n) ? n : (short)0).ToArray();
                if (configDisableClearingItemIds_ShortArray.Length == 1 && configDisableClearingItemIds_ShortArray[0] == 0)
                {
                    configDisableClearingItemIds_ShortArray = new short[] { };
                }

                foreach (short protoId in configDisableClearingItemIds_ShortArray)
                {
                    VegeProto vegeProto = LDB.veges.Select((int)protoId);
                    if (vegeProto == null)
                    {
                        Logger.LogError($"ERROR: Configured vege proto ID {protoId} is invalid.  Recommend removing this ID from the config file.");
                    }
                    else
                    {
                        Logger.LogInfo($"Configured to block vege proto ID {protoId} for {vegeProto.Name.Translate()}");
                    }
                }
            }
        }

        // This patch is for compatability with Windows10CE's DSP Cheats' Instant-Build feature.
        [HarmonyPrefix, HarmonyPatch(typeof(PlanetFactory), "BuildFinally")]
        public static bool PlanetFactory_BuildFinally_Prefix(Player player, int prebuildId)
        {
            if (player.factory != null && prebuildId != 0)
            {
                PrebuildData prebuild = player.factory.prebuildPool[prebuildId];
                if (isDroneClearingPrebuild(prebuild))
                {   // This will never happen unless DSP Cheats' Instant-Build feature is enabled.  So, let's do what it's telling us to.
                    if (configEnableInstantClearing.Value)
                    {
                        player.factory.RemovePrebuildWithComponents(prebuildId);
                        player.factory.RemoveVegeWithComponents(prebuild.modelId);
                        for (int activeMissionIdx = 0; activeMissionIdx < activeMissions.Count; ++activeMissionIdx)
                        {
                            DroneClearingMissionData missionData = activeMissions[activeMissionIdx];
                            if (missionData.prebuildId == prebuildId)
                            {
                                if (missionData.mineAction.miningType == EObjectType.Entity)
                                {
                                    missionData.miningTargetGizmo.Close();
                                    if (missionData.torchEffect != null && missionData.torchEffect.isPlaying)
                                    {
                                        missionData.torchEffect.Stop();
                                        ParticleSystem.Destroy(missionData.torchEffect);
                                    }
                                    activeMissions.RemoveAt(activeMissionIdx);
                                }
                            }
                        }
                    }
                    return false;
                }
            }
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(MechaDroneLogic), "UpdateTargets")]
        public static void MechaDroneLogic_UpdateTargets_Prefix(MechaDroneLogic __instance, Player ___player)
        {
            if (___player.factory != null)
            {
                if (GameMain.data.hidePlayerModel)
                {
                    if (configEnableDebug.Value)
                    {
                        Logger.LogInfo("Skipping because player model is hidden.  This is needed for compatability with the Render Distance mod.");
                    }
                    UpdateTipText("(Player hidden.)");
                    return;
                }

                if (!configEnableMod.Value)
                {
                    if (configEnableDebug.Value)
                    {
                        Logger.LogInfo("Skipping because mod is disabled.");
                    }
                    return;
                }

                if (___player.movementState == EMovementState.Sail)
                {
                    if (configEnableDebug.Value)
                    {
                        Logger.LogInfo("Skipping because movement state is Sail.");
                    }
                    UpdateTipText("(Waiting while Sailing.)");
                    RecallClearingDrones();
                    return;
                }

                for (int activeMissionIdx = 0; activeMissionIdx < activeMissions.Count; ++activeMissionIdx)
                {
                    DroneClearingMissionData missionData = activeMissions[activeMissionIdx];
                    PrebuildData prebuild = ___player.factory.prebuildPool[missionData.prebuildId];
                    if (!__instance.serving.Contains(-prebuild.id))  // If not yet assigned...
                    {
                        float distanceToVege = Vector3.Distance(GameMain.mainPlayer.position, prebuild.pos);
                        if (distanceToVege > GameMain.mainPlayer.mecha.buildArea)
                        {
                            //Logger.LogDebug($"{prebuild.id} unassigned and beyond build area.");
                            missionData.miningTargetGizmo.Close();
                            activeMissions.RemoveAt(activeMissionIdx--);
                            GameMain.mainPlayer.factory.RemovePrebuildWithComponents(prebuild.id);
                        }
                    }
                }

                if (___player.movementState == EMovementState.Drift && !configEnableClearingWhileDrifting.Value)
                {
                    if (configEnableDebug.Value)
                    {
                        Logger.LogInfo("Skipping while drifting.");
                    }
                    UpdateTipText("(Waiting while Drifting.)");
                    return;
                }

                if (___player.movementState == EMovementState.Fly && !configEnableClearingWhileFlying.Value)
                {
                    if (configEnableRecallWhileFlying.Value)
                    {
                        if (configEnableDebug.Value)
                        {
                            Logger.LogInfo("Recalling drones.");
                        }
                        RecallClearingDrones();
                    }
                    else if (configEnableDebug.Value)
                    {
                        Logger.LogInfo("Skipping while flying.");
                    }
                    UpdateTipText("(Waiting while Flying.)");
                    return;
                }

                if (___player.mecha.coreEnergy < ___player.mecha.droneEjectEnergy)
                {
                    if (configEnableDebug.Value)
                    {
                        Logger.LogInfo("Skipping because of insufficient mecha energy to eject a drone.");
                    }
                    UpdateTipText("(Waiting for ejection energy.)");
                    return;
                }

                // Filter based on planet.typeString
                // Themes are stored in Resources\prototypes\ThemeProtoSet.asset
                // Using DisplayName because it should be the same string regardless of translation.
                // Translations are stored in Resources\prototypes\StringProtoSet.asset
                string planetThemeName = LDB.themes.Select(___player.planetData.theme).DisplayName;
                if ((!configEnableClearingPlanetAridDesert.Value && planetThemeName == "干旱荒漠") ||  // Arid desert; PlanetType: 3 (Desert)
                    (!configEnableClearingPlanetAshenGelisol.Value && planetThemeName == "灰烬冻土") ||  // Ashen gelisol; PlanetType: 3 (Desert)
                    (!configEnableClearingPlanetBarrenDesert.Value && planetThemeName == "贫瘠荒漠") ||  // Barren desert; PlanetType: 3 (Desert)
                    (!configEnableClearingPlanetGobi.Value && planetThemeName == "戈壁") ||  // Gobi; PlanetType: 3 (Desert)
                    (!configEnableClearingPlanetIceFieldGelisol.Value && planetThemeName == "冰原冻土") ||  // Ice field gelisol; PlanetType: 4 (Ice)
                    (!configEnableClearingPlanetLava.Value && planetThemeName == "熔岩") ||  // Lava; PlanetType: 1 (Vocano)
                    (!configEnableClearingPlanetMediterranean.Value && planetThemeName == "地中海") ||  // Mediterranean; PlanetType: 2 (Ocean)
                    (!configEnableClearingPlanetOceanWorld.Value && planetThemeName == "水世界") ||  // Ocean world; PlanetType: 2 (Ocean)
                    (!configEnableClearingPlanetOceanicJungle.Value && planetThemeName == "海洋丛林") ||  // Oceanic jungle; PlanetType: 2 (Ocean)
                    (!configEnableClearingPlanetPrairie.Value && planetThemeName == "草原") ||  // Prairie; PlanetType: 2 (Ocean)
                    (!configEnableClearingPlanetRedStone.Value && planetThemeName == "红石") ||  // Red stone; PlanetType: 2 (Ocean)
                    (!configEnableClearingPlanetVolcanicAsh.Value && planetThemeName == "火山灰"))  // Volcanic ash; PlanetType: 1 (Vocano)
                {
                    if (configEnableDebug.Value)
                    {
                        Logger.LogInfo("Skipping planet type " + ___player.planetData.typeString);
                    }
                    UpdateTipText("(Waiting on this planet type.)");
                    return;
                }

                int totalDroneTaskingCount = getTotalDroneTaskingCount();
                if (totalDroneTaskingCount >= Math.Min(configMaxClearingDroneCount.Value, ___player.mecha.droneCount))
                {
                    if (configEnableDebug.Value)
                    {
                        var sbc = new StringBuilder();
                        sbc.AppendFormat("Skipping due to number of drone assignments: totalDroneTaskingCount={0}, configMaxClearingDroneCount={1}, player.mecha.droneCount={2}, player.mecha.idleDroneCount={3}",
                            totalDroneTaskingCount, configMaxClearingDroneCount, ___player.mecha.droneCount, ___player.mecha.idleDroneCount);
                        Logger.LogInfo(sbc.ToString());
                    }
                    UpdateTipText("(Available drones assigned.)");
                    return;
                }

                if (___player.mecha.coreEnergy / ___player.mecha.coreEnergyCap < configReservedPower.Value)
                {
                    if (configEnableDebug.Value)
                    {
                        Logger.LogInfo("Skipping due to low power.");
                    }
                    UpdateTipText("(Waiting for energy.)");
                    return;
                }

                if (configCollectResourcesFlag.Value)  // ... check configReservedInventorySpace
                {
                    uint numEmptyInventorySlots = 0;
                    for (int gridIdx = 0; gridIdx < ___player.package.size; ++gridIdx)
                    {
                        if (___player.package.grids[gridIdx].count == 0)
                        {
                            numEmptyInventorySlots++;
                        }
                    }
                    if (numEmptyInventorySlots < configReservedInventorySpace.Value)
                    {
                        if (configEnableDebug.Value)
                        {
                            Logger.LogInfo("Too few inventory slots");
                        }
                        UpdateTipText("(Waiting for inventory space.)");
                        return;
                    }
                }

                float closestVegeDistance = ___player.mecha.buildArea * configLimitClearingDistance.Value * 2;
                int closestVegeId = -1;
                foreach (VegeData vegeData in ___player.factory.vegePool)
                {
                    VegeProto vegeProto = LDB.veges.Select((int)vegeData.protoId);
                    // vegeProto.Type == EVegeType.Detail covers grass and small minable rocks.  So the check includes vegeProto.MiningItem.Length instead.
                    // VegeProto stored in Resources\prototypes\VegeProtoSet.asset
                    if (vegeProto != null && vegeProto.MiningItem.Length > 0)
                    {
                        if (vegeProto.Name == "飞行舱") // Space Capsule
                        {
                            if (!configEnableClearingItemSpaceCapsule.Value)
                            {
                                continue;
                            }
                        }
                        else if ((vegeProto.Type == EVegeType.Tree && !configEnableClearingItemTree.Value) ||
                            (vegeProto.Type == EVegeType.Stone && !configEnableClearingItemStone.Value) ||
                            (vegeProto.Type == EVegeType.Detail && !configEnableClearingItemDetail.Value) ||
                            (vegeProto.Type == EVegeType.Ice && !configEnableClearingItemIce.Value))
                        {
                            continue;
                        }

                        bool disabledById = false;
                        foreach (short disabledId in configDisableClearingItemIds_ShortArray)
                        {
                            if (vegeData.protoId == disabledId)
                            {
                                disabledById = true;
                                break;
                            }
                        }
                        if (disabledById)
                        {
                            continue;
                        }

                        float distanceToVege = Vector3.Distance(___player.position, vegeData.pos);
                        if (distanceToVege < closestVegeDistance)
                        {
                            bool vegeBeingProcessedFlag = false;
                            foreach (PrebuildData prebuild in ___player.factory.prebuildPool)
                            {
                                if (isDroneClearingPrebuild(prebuild) && prebuild.modelId == vegeData.id)
                                {
                                    vegeBeingProcessedFlag = true;
                                    break;
                                }
                            }

                            if (!vegeBeingProcessedFlag)
                            {
                                closestVegeDistance = distanceToVege;
                                closestVegeId = vegeData.id;
                            }
                        }
                    }
                }

                if (closestVegeDistance <= ___player.mecha.buildArea * configLimitClearingDistance.Value)
                {
                    VegeData vegeData = ___player.factory.vegePool[closestVegeId];
                    VegeProto vegeProto = LDB.veges.Select((int)vegeData.protoId);

                    //var sb = new StringBuilder();
                    //sb.AppendFormat("Initiating mining of {0} to get {1} at power level {2}", vegeProto.Type.ToString(), LDB.items.Select(vegeProto.MiningItem[0]).name, ___player.mecha.coreEnergy / ___player.mecha.coreEnergyCap);
                    //Logger.LogInfo(sb.ToString());

                    PrebuildData prebuild = default;
                    prebuild.protoId = -1;
                    prebuild.colliderId = -1;
                    prebuild.pickOffset = -1;
                    prebuild.filterId = -1;
                    prebuild.recipeId = -1;
                    prebuild.modelId = vegeData.id;
                    prebuild.pos = prebuild.pos2 = vegeData.pos;
                    prebuild.rot = vegeData.rot;

                    // This operation will cause a drone to be assigned by MechaDroneLogic.UpdateTargets.
                    int prebuildId = ___player.factory.AddPrebuildData(prebuild);

                    DroneClearingMissionData missionData = new DroneClearingMissionData
                    {
                        prebuildId = prebuildId,
                        mineAction = new DroneAction_Mine
                        {
                            player = GameMain.mainPlayer,
                            miningType = EObjectType.Vegetable,
                            miningId = vegeData.id,
                            miningTick = 0
                        }
                    };

                    // Reference PlayerContrGizmo.SetMiningTarget
                    missionData.miningTargetGizmo = CircleGizmo.Create(3, vegeData.pos, vegeProto.CircleRadius * 1.19f);  // These get cleaned automatically.  There's no need for the mod to destroy them.
                    missionData.miningTargetGizmo.multiplier = 2.5f;
                    missionData.miningTargetGizmo.alphaMultiplier = 0.15f;  // Assigned, but not mining
                    missionData.miningTargetGizmo.fadeInScale = 1.3f;
                    missionData.miningTargetGizmo.fadeInTime = 0.13f;
                    missionData.miningTargetGizmo.fadeInFalloff = 0.5f;
                    missionData.miningTargetGizmo.color = new Color(configProgressColor_Red.Value, configProgressColor_Green.Value, configProgressColor_Blue.Value); // Configs.builtin.gizmoColors[2]
                    missionData.miningTargetGizmo.rotateSpeed = 0f;
                    missionData.miningTargetGizmo.Open();

                    activeMissions.Add(missionData);

                    UpdateTipText("(Assigning drones.)");
                }
                else
                {
                    if (configEnableDebug.Value)
                    {
                        Logger.LogInfo("No enabled items within configured distance.");
                    }
                    UpdateTipText("(No more items in range.)");
                }
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(MechaDrone), "Update")]
        public static void MechaDrone_Update_Postfix(MechaDrone __instance, ref int __result, PrebuildData[] prebuildPool, Vector3 playerPos, float dt, ref double energy, ref double energyChange, double energyRate)
        {
            if (__result == 1)  // If the Prebuild would normally complete...
            {
                int prebuildId = -__instance.targetObject;
                PrebuildData prebuild = prebuildPool[prebuildId];
                if (isDroneClearingPrebuild(prebuild))
                {
                    PlanetFactory factory = GameMain.mainPlayer.factory;

                    DroneClearingMissionData missionData = null;
                    for (int activeMissionIdx = 0; activeMissionIdx < activeMissions.Count; ++activeMissionIdx)
                    {
                        missionData = activeMissions[activeMissionIdx];
                        if (missionData.prebuildId == prebuildId)
                        {
                            if (missionData.mineAction.miningType == EObjectType.Entity)
                            {
                                // Clearing completed
                                missionData.miningTargetGizmo.Close();
                                if (missionData.torchEffect != null && missionData.torchEffect.isPlaying)
                                {
                                    missionData.torchEffect.Stop();
                                    ParticleSystem.Destroy(missionData.torchEffect);
                                }
                                activeMissions.RemoveAt(activeMissionIdx);
                                factory.RemovePrebuildWithComponents(prebuildId);
                            }
                            else
                            {
                                if (configCollectResourcesFlag.Value)
                                {
                                    VegeData vegeData = factory.vegePool[prebuild.modelId];
                                    if (vegeData.id == 0)
                                    {
                                        Logger.LogDebug("Item already mined.");
                                        missionData.miningTargetGizmo.Close();
                                        if (missionData.torchEffect != null && missionData.torchEffect.isPlaying)
                                        {
                                            missionData.torchEffect.Stop();
                                            ParticleSystem.Destroy(missionData.torchEffect);
                                        }
                                        activeMissions.RemoveAt(activeMissionIdx);
                                        factory.RemovePrebuildWithComponents(prebuildId);
                                    }
                                    else
                                    {
                                        if (!missionData.miningFlag)
                                        {
                                            missionData.miningFlag = true;
                                            missionData.miningTargetGizmo.alphaMultiplier = 1f;
                                            missionData.torchEffect = Instantiate(
                                                GameMain.mainPlayer.effect.torchEffect,
                                                __instance.position,
                                                Quaternion.LookRotation(__instance.forward, __instance.position.normalized));
                                            ParticleSystem spark = missionData.torchEffect.transform.Find("spark").GetComponent< ParticleSystem>();
                                            ParticleSystem.MainModule sparkMain = spark.main;
                                            sparkMain.startLifetime = 0.7f;  // 0.5 is too small.  1.0 is too much.
                                            missionData.torchEffect.Play();
                                        }
                                        __result = 0;
                                    }
                                }
                                else
                                {
                                    missionData.miningTargetGizmo.Close();
                                    if (missionData.torchEffect != null && missionData.torchEffect.isPlaying)
                                    {
                                        missionData.torchEffect.Stop();
                                        ParticleSystem.Destroy(missionData.torchEffect);
                                    }
                                    activeMissions.RemoveAt(activeMissionIdx);
                                    factory.RemovePrebuildWithComponents(prebuildId);
                                    factory.RemoveVegeWithComponents(prebuild.modelId);
                                }
                            }
                            return;
                        }
                    }
                }
            }
        }

        public static void RecallClearingDrones()
        {
            if (GameMain.mainPlayer.factory != null)
            {
                foreach (PrebuildData prebuild in GameMain.mainPlayer.factory.prebuildPool)
                {
                    if (isDroneClearingPrebuild(prebuild))
                    {
                        GameMain.mainPlayer.factory.RemovePrebuildWithComponents(prebuild.id);
                    }
                }
            }
            foreach (DroneClearingMissionData missionData in activeMissions)
            {
                missionData.miningTargetGizmo.Close();
                if (missionData.torchEffect != null && missionData.torchEffect.isPlaying)
                {
                    missionData.torchEffect.Stop();
                    ParticleSystem.Destroy(missionData.torchEffect);
                }
            }
            activeMissions.Clear();
        }

        public static long lastDisplayTime = 0;

        // This is called by PlayerController.GameTick
        [HarmonyPostfix, HarmonyPatch(typeof(PlayerAction_Mine), "GameTick")]
        public static void PlayerAction_Mine_GameTick_Postfix(long timei)
        {
            if (clearDroneTaskingOnNextTick)
            {
                clearDroneTaskingOnNextTick = false;
                RecallClearingDrones();
            }

            if (configEnableDebug.Value)
            {
                if (timei > lastDisplayTime + 30 || timei < lastDisplayTime)
                {
                    var sb = new StringBuilder();
                    sb.AppendFormat("{0} drone tasks. {1} active missions.", getTotalDroneTaskingCount(), activeMissions.Count);
                    Logger.LogInfo(sb.ToString());

                    lastDisplayTime = timei;
                }
            }

            foreach (DroneClearingMissionData missionData in activeMissions)
            {
                if (missionData.miningFlag)
                {
                    missionData.mineAction.DroneGameTick();
                    missionData.miningTargetGizmo.percent = 1f - missionData.mineAction.percent;
                }
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrebuildData), "Export")]
        public static bool PrebuildData_Export_Prefix(PrebuildData __instance, BinaryWriter w)
        {
            if (isDroneClearingPrebuild(__instance))
            {
                // Do not save drone clearing tasks.  This would work unless the mod
                // gets uninstalled in which case it causes the game to issue an error.
                //Logger.LogInfo("Preventing saving of drone clearing prebuild.");
                PrebuildData generic = default;
                generic.id = __instance.id;
                generic.Export(w);
                return false;
            }
            else
            {
                return true;
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(GameData), "Destroy")]
        public static void GameData_Destroy_Postfix()
        {
            foreach (DroneClearingMissionData missionData in activeMissions)
            {
                missionData.miningTargetGizmo.Close();
                if (missionData.torchEffect != null && missionData.torchEffect.isPlaying)
                {
                    missionData.torchEffect.Stop();
                    ParticleSystem.Destroy(missionData.torchEffect);
                }
            }
            activeMissions.Clear();
        }

        [HarmonyPrefix, HarmonyPatch(typeof(GameScenarioLogic), "NotifyOnVegetableMined")]
        public static void GameScenarioLogic_NotifyOnVegetableMined_Prefix(int protoId)
        {
            VegeProto vegeProto = LDB.veges.Select(protoId);
            Logger.LogDebug($"Mined proto ID {protoId} (" + vegeProto.Name.Translate() + ")");
        }
    }
}
