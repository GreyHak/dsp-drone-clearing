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
using System.Threading.Tasks;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using BepInEx.Logging;
using System.Security;

namespace DysonSphereDroneClearing
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("DSPGAME.exe")]
    public class DysonSphereDroneClearing : BaseUnityPlugin
    {
        public const string pluginGuid = "greyhak.dysonsphereprogram.droneclearing";
        public const string pluginName = "DSP Drone Clearing";
        public const string pluginVersion = "1.2.0.0";
        new internal static ManualLogSource Logger;
        new internal static BepInEx.Configuration.ConfigFile Config;
        Harmony harmony;

        public static bool configEnableMod = true;
        public static bool configCollectResourcesFlag = true;
        public static uint configMaxClearingDroneCount = Mecha.kMaxDroneCount;
        public static float configLimitClearingDistance = 0.4f;
        public static bool configEnableClearingWhileFlying = false;
        public static uint configReservedInventorySpace = 10;
        public static float configReservedPower = 0.4f;
        public static bool configEnableInstantClearing = false;
        public static bool configEnableClearingItemTree = true;
        public static bool configEnableClearingItemStone = true;
        public static bool configEnableClearingItemDetail = false;
        public static bool configEnableClearingItemIce = true;
        public static bool configEnableClearingPlanetGeneric = true;
        public static bool configEnableClearingPlanetVocano = true;
        public static bool configEnableClearingPlanetOcean = true;
        public static bool configEnableClearingPlanetDesert = true;
        public static bool configEnableClearingPlanetIce = true;

        // The following class was copied from PlayerAction_Mine
        public class DroneAction_Mine
        {
            public Player player;
            public float percent;
            public int miningProtoId;
            public EObjectType miningType;
            public int miningId;
            public int miningTick;

            public void DroneGameTick()
            {
                double powerFactor = 0.01666666753590107;
                PlanetFactory factory = this.player.factory;
                double miningEnergyCost = this.player.mecha.miningPower * powerFactor;
                double energyAvailable;
                float fractionOfEnergyAvailable;
                this.player.mecha.QueryEnergy(miningEnergyCost, out energyAvailable, out fractionOfEnergyAvailable);
                int miningTime = (int)(this.player.mecha.miningSpeed * fractionOfEnergyAvailable * 10000f + 0.49f);

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
                                    int inventoryOverflowCount = this.player.package.AddItemStacked(minedItem, minedItemCount);
                                    if (inventoryOverflowCount != 0)
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
                        VFAudio.Create(vegeProto.MiningAudio, null, vegeData.pos, true);
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
            return prebuild.protoId == -1 && prebuild.modelId == 0 && prebuild.recipeId == -1;
        }

        public class DroneClearingMissionData
        {
            public int prebuildId = -1;
            public Vector3 forward;
            public Vector3 position;
            public DroneAction_Mine mineAction = null;
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
                            totalDroneTaskCount++;
                        }
                    }
                }
            }
            return totalDroneTaskCount;
        }

        public void Awake()
        {
            DysonSphereDroneClearing.Logger = base.Logger;  // "C:\Program Files (x86)\Steam\steamapps\common\Dyson Sphere Program\BepInEx\LogOutput.log"
            DysonSphereDroneClearing.Config = base.Config;
            Config.ConfigReloaded += OnConfigReload;

            // PlayerOrder::ReachTest, PlayerAction_Mine::GameTick
            // MechaDroneLogic::UpdateDrones -> MechaDroneLogic::Build -> PlanetFactory::BuildFinally
            // PlanetFactory::BuildFinally calls FlattenTerrain and SetSandCount.
            harmony = new Harmony("org.greyhak.plugins.dspdroneclearing");
            harmony.PatchAll(typeof(DysonSphereDroneClearing));

            enabledSprite = GetSprite(new Color(0, 1, 0));  // Bright Green
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
            if (GameMain.instance != null && GameObject.Find("Game Menu/button-1-bg") && !GameObject.Find("greyhak-clearing-enable-button"))
            {
                RectTransform parent = GameObject.Find("Game Menu").GetComponent<RectTransform>();
                RectTransform prefab = GameObject.Find("Game Menu/button-1-bg").GetComponent<RectTransform>();
                Vector3 referencePosition = GameObject.Find("Game Menu/button-1-bg").GetComponent<RectTransform>().localPosition;
                enableDisableButton = GameObject.Instantiate<RectTransform>(prefab);
                enableDisableButton.gameObject.name = "greyhak-clearing-enable-button";
                enableDisableButton.GetComponent<UIButton>().tips.tipTitle = configEnableMod ? "Drone Clearing Enabled" : "Drone Clearing Disabled";
                enableDisableButton.GetComponent<UIButton>().tips.tipText = configEnableMod ? "Click to disable drone clearing" : "Click to enable drone clearing";
                enableDisableButton.GetComponent<UIButton>().tips.delay = 0f;
                enableDisableButton.transform.Find("button-1/icon").GetComponent<Image>().sprite =
                    configEnableMod ? enabledSprite : disabledSprite;
                enableDisableButton.SetParent(parent);
                enableDisableButton.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                enableDisableButton.localPosition = new Vector3(referencePosition.x + 96f, referencePosition.y + 161f, referencePosition.z);
                enableDisableButton.GetComponent<UIButton>().OnPointerDown(null);
                enableDisableButton.GetComponent<UIButton>().OnPointerEnter(null);
                enableDisableButton.GetComponent<UIButton>().button.onClick.AddListener(() =>
                {
                    configEnableMod = !DysonSphereDroneClearing.configEnableMod;
                    Config.GetSetting<bool>("Config", "Enable").Value = configEnableMod;

                    if (!configEnableMod)
                    {
                        clearDroneTaskingOnNextTick = true;
                    }

                    enableDisableButton.GetComponent<UIButton>().tips.tipTitle = configEnableMod ? "Drone Clearing Enabled" : "Drone Clearing Disabled";
                    enableDisableButton.GetComponent<UIButton>().tips.tipText = configEnableMod ? "Click to disable drone clearing" : "Click to enable drone clearing";
                    enableDisableButton.transform.Find("button-1/icon").GetComponent<Image>().sprite =
                        configEnableMod ? enabledSprite : disabledSprite;
                    enableDisableButton.GetComponent<UIButton>().UpdateTip();
                });
            }
        }

        public static Sprite GetSprite(Color color)
        {
            Texture2D tex = new Texture2D(48, 48, TextureFormat.RGBA32, false);
            for (int x = 0; x < 48; x++)
                for (int y = 0; y < 48; y++)
                    tex.SetPixel(x, y, new Color(0, 0, 0, 0));

            // Draw a plane like the one re[resending drones in the Mecha Panel...
            for (int x = 9; x <= 17; x++)
                for (int y = 3; y < 38; y++)
                    tex.SetPixel(x, y, color);

            for (int x = 15; x <= 23; x++)
                for (int y = 12; y < 38; y++)
                    tex.SetPixel(x, y, color);

            for (int x = 21; x <= 29; x++)
                for (int y = 18; y < 38; y++)
                    tex.SetPixel(x, y, color);

            for (int x = 27; x <= 35; x++)
                for (int y = 24; y < 38; y++)
                    tex.SetPixel(x, y, color);

            for (int x = 33; x <= 44; x++)
                for (int y = 33 - 3; y < 38; y++)
                    tex.SetPixel(x, y, color);

            tex.name = "greyhak-clearing-enable-icon";
            tex.Apply();

            return Sprite.Create(tex, new Rect(0f, 0f, 48f, 48f), new Vector2(0f, 0f), 1000);
        }

        public static void OnConfigReload(object sender, EventArgs e)
        {
            configEnableMod = Config.Bind<bool>("Config", "Enable", configEnableMod, "Enable/disable drone clearing mod.").Value;
            configCollectResourcesFlag = Config.Bind<bool>("Config", "CollectResources", configCollectResourcesFlag, "Take time to collect resources. If false, clearing will be quicker, but no resources will be collected.").Value;
            configMaxClearingDroneCount = Config.Bind<uint>("Config", "DroneCountLimit", configMaxClearingDroneCount, "Limit the number of drones that will be used when clearing.").Value;
            configLimitClearingDistance = Config.Bind<float>("Config", "ClearingDistance", configLimitClearingDistance, "Fraction of mecha build distance to perform clearing.  Min 0.0, Max 1.0").Value;
            configLimitClearingDistance = Math.Min(configLimitClearingDistance, 1.0f);
            configLimitClearingDistance = Math.Max(configLimitClearingDistance, 0.0f);
            configEnableClearingWhileFlying = Config.Bind<bool>("Config", "ClearWhileFlying", configEnableClearingWhileFlying, "Clearing is always works while walking.  This flag can be used to also enable clearing while flying.").Value;
            configReservedInventorySpace = Config.Bind<uint>("Config", "InventorySpace", configReservedInventorySpace, "Initiate clearing when there are this number of inventory spaces empty.  (Setting has no impact if CollectResources is false.)").Value;
            configReservedPower = Config.Bind<float>("Config", "PowerReserve", configReservedPower, "Initiate clearing only when there is at least this fraction of Icarus's power remaining.").Value;
            configEnableInstantClearing = Config.Bind<bool>("Config", "DSPCheats_InstantClearing", configEnableInstantClearing, "If the DSP Cheats mod is installed, and Instant-Build is enabled, should this mod work with that one and instantly clear?").Value;

            configEnableClearingItemTree = Config.Bind<bool>("Items", "IncludeTrees", configEnableClearingItemTree, "Enabling clearing of trees.").Value;
            configEnableClearingItemStone = Config.Bind<bool>("Items", "IncludeStone", configEnableClearingItemStone, "Enabling clearing of stones which can block the mecha's movement.  (This includes the space capsule at the start of a new game.)").Value;
            configEnableClearingItemDetail = Config.Bind<bool>("Items", "IncludePebbles", configEnableClearingItemDetail, "Enabling clearing of tiny stones which won't block the mecha's movement.").Value;
            configEnableClearingItemIce = Config.Bind<bool>("Items", "IncludeIce", configEnableClearingItemIce, "Enabling clearing of ice.").Value;

            configEnableClearingPlanetGeneric = Config.Bind<bool>("Planets", "IncludeGeneric", configEnableClearingPlanetGeneric, "Enable clearing on generic planets.").Value;
            configEnableClearingPlanetVocano = Config.Bind<bool>("Planets", "IncludeVolcanic", configEnableClearingPlanetVocano, "Enable clearing on volcanic planets.").Value;
            configEnableClearingPlanetOcean = Config.Bind<bool>("Planets", "IncludeOcean", configEnableClearingPlanetOcean, "Enable clearing on ocean planets.").Value;
            configEnableClearingPlanetDesert = Config.Bind<bool>("Planets", "IncludeDesert", configEnableClearingPlanetDesert, "Enable clearing on desert planets.").Value;
            configEnableClearingPlanetIce = Config.Bind<bool>("Planets", "IncludeIce", configEnableClearingPlanetIce, "Enable clearing on ice planets.").Value;

            Logger.LogInfo("Configuration loaded.");
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
                    if (configEnableInstantClearing)
                    {
                        player.factory.RemovePrebuildData(prebuildId);
                        player.factory.RemoveVegeWithComponents(prebuild.upEntity);
                        for (int activeMissionIdx = 0; activeMissionIdx < activeMissions.Count; ++activeMissionIdx)
                        {
                            DroneClearingMissionData missionData = activeMissions[activeMissionIdx];
                            if (missionData.prebuildId == prebuildId)
                            {
                                if (missionData.mineAction.miningType == EObjectType.Entity)
                                {
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
            if (configEnableMod &&
                ___player.factory != null &&
                (___player.movementState == EMovementState.Walk ||
                ___player.movementState == EMovementState.Fly) &&
                ___player.mecha.droneCount - ___player.mecha.idleDroneCount < configMaxClearingDroneCount &&
                ___player.mecha.coreEnergy > ___player.mecha.droneEjectEnergy)
            {
                if (___player.movementState == EMovementState.Fly && !configEnableClearingWhileFlying)
                {
                    //Logger.LogInfo("Skipping while flying.");
                    return;
                }
                if ((___player.planetData.type == EPlanetType.None && !configEnableClearingPlanetGeneric) ||
                    (___player.planetData.type == EPlanetType.Vocano && !configEnableClearingPlanetVocano) ||
                    (___player.planetData.type == EPlanetType.Ocean && !configEnableClearingPlanetOcean) ||
                    (___player.planetData.type == EPlanetType.Desert && !configEnableClearingPlanetDesert) ||
                    (___player.planetData.type == EPlanetType.Ice && !configEnableClearingPlanetIce))
                {
                    //Logger.LogInfo("Skipping planet type " + ___player.planetData.type.ToString());
                    return;
                }

                if (getTotalDroneTaskingCount() >= configMaxClearingDroneCount)
                {
                    //Logger.LogInfo("Skipping due to number of drone assignments.");
                    return;
                }

                if (___player.mecha.coreEnergy / ___player.mecha.coreEnergyCap < configReservedPower)
                {
                    //Logger.LogInfo("Skipping due to low power.");
                    return;
                }

                if (configCollectResourcesFlag)  // ... check configReservedInventorySpace
                {
                    uint numEmptyInventorySlots = 0;
                    for (int gridIdx = 0; gridIdx < ___player.package.size; ++gridIdx)
                    {
                        if (___player.package.grids[gridIdx].count == 0)
                        {
                            numEmptyInventorySlots++;
                        }
                    }
                    if (numEmptyInventorySlots < configReservedInventorySpace)
                    {
                        //Logger.LogInfo("Too few inventory slots");
                        return;
                    }
                }

                float closestVegeDistance = ___player.mecha.buildArea * configLimitClearingDistance * 2;
                int closestVegeId = -1;
                foreach (VegeData vegeData in ___player.factory.vegePool)
                {
                    VegeProto vegeProto = LDB.veges.Select((int)vegeData.protoId);
                    // vegeProto.Type == EVegeType.Detail covers grass and small minable rocks.  So the check includes vegeProto.MiningItem.Length instead.
                    if (vegeProto != null && vegeProto.MiningItem.Length > 0)
                    {
                        if ((vegeProto.Type == EVegeType.Tree && !configEnableClearingItemTree) ||
                            (vegeProto.Type == EVegeType.Stone && !configEnableClearingItemStone) ||
                            (vegeProto.Type == EVegeType.Detail && !configEnableClearingItemDetail) ||
                            (vegeProto.Type == EVegeType.Ice && !configEnableClearingItemIce))
                        {
                            continue;
                        }

                        float distanceToVege = Vector3.Distance(___player.position, vegeData.pos);
                        if (distanceToVege < closestVegeDistance)
                        {
                            bool vegeBeingProcessedFlag = false;
                            foreach (PrebuildData prebuild in ___player.factory.prebuildPool)
                            {
                                if (isDroneClearingPrebuild(prebuild) && prebuild.upEntity == vegeData.id)
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

                if (closestVegeDistance <= ___player.mecha.buildArea * configLimitClearingDistance)
                {
                    VegeData vegeData = ___player.factory.vegePool[closestVegeId];
                    VegeProto vegeProto = LDB.veges.Select((int)vegeData.protoId);

                    //var sb = new StringBuilder();
                    //sb.AppendFormat("Initiating mining of {0} to get {1} at power level {2}", vegeProto.Type.ToString(), LDB.items.Select(vegeProto.MiningItem[0]).name, ___player.mecha.coreEnergy / ___player.mecha.coreEnergyCap);
                    //Logger.LogInfo(sb.ToString());

                    PrebuildData prebuild = default;
                    prebuild.protoId = -1;
                    prebuild.modelId = 0;  // Saves always open as 0
                    prebuild.recipeId = -1;
                    prebuild.refCount = 0;
                    prebuild.upEntity = vegeData.id;
                    prebuild.pos = prebuild.pos2 = ___player.controller.actionBuild.previewPose.position + ___player.controller.actionBuild.previewPose.rotation * vegeData.pos;
                    prebuild.rot = vegeData.rot;

                    // This operation will cause a drone to be assigned by MechaDroneLogic::UpdateTargets.
                    int prebuildId = ___player.factory.AddPrebuildData(prebuild);
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
                                activeMissions.RemoveAt(activeMissionIdx);
                                factory.RemovePrebuildData(prebuildId);
                            }
                            else
                            {
                                __result = 0;
                            }
                            return;
                        }
                    }

                    if (configCollectResourcesFlag)
                    {
                        missionData = new DroneClearingMissionData();
                        missionData.prebuildId = prebuildId;
                        missionData.forward = __instance.forward;
                        missionData.position = __instance.position;
                        missionData.mineAction = new DroneAction_Mine();
                        missionData.mineAction.player = GameMain.mainPlayer;
                        missionData.mineAction.miningType = EObjectType.Vegetable;
                        missionData.mineAction.miningId = prebuild.upEntity;
                        missionData.mineAction.miningTick = 0;
                        activeMissions.Add(missionData);
                    }
                    else
                    {
                        factory.RemovePrebuildData(prebuildId);
                        factory.RemoveVegeWithComponents(prebuild.upEntity);
                    }

                    __result = 0;
                    return;
                }
            }
        }

        //public static long lastDisplayTime = 0;

        [HarmonyPostfix, HarmonyPatch(typeof(PlanetFactory), "GameTick")]
        public static void PlanetFactory_GameTick_Postfix(long time)
        {
            if (clearDroneTaskingOnNextTick)
            {
                clearDroneTaskingOnNextTick = false;
                foreach (PrebuildData prebuild in GameMain.mainPlayer.factory.prebuildPool)
                {
                    if (isDroneClearingPrebuild(prebuild))
                    {
                        GameMain.mainPlayer.factory.RemovePrebuildData(prebuild.id);
                    }
                }
                activeMissions.Clear();
            }

            /*if (time > lastDisplayTime + 30 || time < lastDisplayTime)
            {
                var sb = new StringBuilder();
                sb.AppendFormat("{0} active clearing drones. {1} active missions.", getTotalDroneTaskingCount(), activeMissions.Count);
                Logger.LogInfo(sb.ToString());

                lastDisplayTime = time;
            }*/

            for (int activeMissionIdx = 0; activeMissionIdx < activeMissions.Count; ++activeMissionIdx)
            {
                activeMissions[activeMissionIdx].mineAction.DroneGameTick();
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
            activeMissions.Clear();
        }
    }
}
