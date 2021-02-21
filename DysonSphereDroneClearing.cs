﻿//
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
        public const string pluginVersion = "1.0.0.0";
        new internal static ManualLogSource Logger;
        new internal static BepInEx.Configuration.ConfigFile Config;
        Harmony harmony;

        public static bool configCollectResourcesFlag = true;
        public static uint configMaxClearingDroneCount = Mecha.kMaxDroneCount;
        public static float configLimitClearingDistance = 0.4f;
        public static bool configEnableClearingWhileFlying = false;
        public static uint configReservedInventorySpace = 10;
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

            public void DroneGameTick(long timei)
            {
                double num = 0.01666666753590107;
                PlanetFactory factory = this.player.factory;
                double num2 = this.player.mecha.miningPower * num;
                double num3;
                float num4;
                this.player.mecha.QueryEnergy(num2, out num3, out num4);
                int num5 = (int)(this.player.mecha.miningSpeed * num4 * 10000f + 0.49f);

                VegeData vegeData = factory.GetVegeData(this.miningId);
                this.miningProtoId = (int)vegeData.protoId;
                VegeProto vegeProto = LDB.veges.Select((int)vegeData.protoId);
                if (vegeProto != null)
                {
                    this.miningTick += num5;
                    this.player.mecha.coreEnergy -= num3;
                    this.player.mecha.MarkEnergyChange(5, -num2);
                    this.percent = Mathf.Clamp01((float)((double)this.miningTick / (double)(vegeProto.MiningTime * 10000)));
                    if (this.miningTick >= vegeProto.MiningTime * 10000)
                    {
                        System.Random random = new System.Random(vegeData.id + ((this.player.planetData.seed & 16383) << 14));
                        bool flag = false;
                        int num7 = 0;
                        for (int i = 0; i < vegeProto.MiningItem.Length; i++)
                        {
                            float num8 = (float)random.NextDouble();
                            if (num8 < vegeProto.MiningChance[i])
                            {
                                int num9 = vegeProto.MiningItem[i];
                                int num10 = (int)((float)vegeProto.MiningCount[i] * (vegeData.scl.y * vegeData.scl.y) + 0.5f);
                                if (num10 > 0 && LDB.items.Select(num9) != null)
                                {
                                    int num11 = this.player.package.AddItemStacked(num9, num10);
                                    if (num11 != 0)
                                    {
                                        UIItemup.Up(num9, num11);
                                        UIRealtimeTip.PopupItemGet(num9, num11, vegeData.pos + vegeData.pos.normalized, num7++);
                                    }
                                    else
                                    {
                                        flag = true;
                                    }
                                    if (num11 != num10)
                                    {
                                        UIRealtimeTip.PopupAhead("无法获得采集物品".Translate(), true, 0);
                                    }
                                }
                            }
                        }
                        VFEffectEmitter.Emit(vegeProto.MiningEffect, vegeData.pos, vegeData.rot);
                        VFAudio.Create(vegeProto.MiningAudio, null, vegeData.pos, true);
                        factory.RemoveVegeWithComponents(vegeData.id);
                        GameMain.gameScenario.NotifyOnVegetableMined((int)vegeData.protoId);
                        this.miningType = EObjectType.Entity;
                        this.miningId = 0;
                        if (flag)
                        {
                            Logger.LogInfo("Issuing abort");
                            this.player.AbortOrder();
                        }
                        this.miningTick = 0;
                    }
                }
                else
                {
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
        static List<DroneClearingMissionData> activeMissions = new List<DroneClearingMissionData> { };

        public void Awake()
        {
            DysonSphereDroneClearing.Logger = base.Logger;  // "C:\Program Files (x86)\Steam\steamapps\common\Dyson Sphere Program\BepInEx\LogOutput.log"
            DysonSphereDroneClearing.Config = base.Config;

            configCollectResourcesFlag = Config.Bind<bool>("Config", "CollectResources", configCollectResourcesFlag, "Take time to collect resources. If false, clearing will be quicker, but no resources will be collected.").Value;
            configMaxClearingDroneCount = Config.Bind<uint>("Config", "DroneCountLimit", configMaxClearingDroneCount, "Limit the number of drones that will be used when clearing.  Set to 0 to disable this mod.").Value;
            configLimitClearingDistance = Config.Bind<float>("Config", "ClearingDistance", configLimitClearingDistance, "Fraction of mecha build distance to perform clearing.  Min 0.0, Max 1.0").Value;
            configLimitClearingDistance = Math.Min(configLimitClearingDistance, 1.0f);
            configLimitClearingDistance = Math.Max(configLimitClearingDistance, 0.0f);
            configEnableClearingWhileFlying = Config.Bind<bool>("Config", "ClearWhileFlying", configEnableClearingWhileFlying, "Clearing is always works while walking.  This flag can be used to also enable clearing while flying.").Value;
            configReservedInventorySpace = Config.Bind<uint>("Config", "InventorySpace", configReservedInventorySpace, "Initiate clearing when there are this number of inventory spaces empty.  (Setting has no impact if CollectResources is false.)").Value;

            configEnableClearingItemTree = Config.Bind<bool>("Items", "IncludeTrees", configEnableClearingItemTree, "Enabling clearing of trees.").Value;
            configEnableClearingItemStone = Config.Bind<bool>("Items", "IncludeStone", configEnableClearingItemStone, "Enabling clearing of stones which can block the mecha's movement.").Value;
            configEnableClearingItemDetail = Config.Bind<bool>("Items", "IncludePebbles", configEnableClearingItemDetail, "Enabling clearing of tiny stones which won't block the mecha's movement.").Value;
            configEnableClearingItemIce = Config.Bind<bool>("Items", "IncludeIce", configEnableClearingItemIce, "Enabling clearing of ice.").Value;

            configEnableClearingPlanetGeneric = Config.Bind<bool>("Planets", "IncludeGeneric", configEnableClearingPlanetGeneric, "Enable clearing on generic planets.").Value;
            configEnableClearingPlanetVocano = Config.Bind<bool>("Planets", "IncludeVolcanic", configEnableClearingPlanetVocano, "Enable clearing on volcanic planets.").Value;
            configEnableClearingPlanetOcean = Config.Bind<bool>("Planets", "IncludeOcean", configEnableClearingPlanetOcean, "Enable clearing on ocean planets.").Value;
            configEnableClearingPlanetDesert = Config.Bind<bool>("Planets", "IncludeDesert", configEnableClearingPlanetDesert, "Enable clearing on desert planets.").Value;
            configEnableClearingPlanetIce = Config.Bind<bool>("Planets", "IncludeIce", configEnableClearingPlanetIce, "Enable clearing on ice planets.").Value;

            if (configMaxClearingDroneCount == 0)
            {
                Logger.LogInfo("Mod disabled.");
                return;
            }    

            // PlayerOrder::ReachTest, PlayerAction_Mine::GameTick
            // MechaDroneLogic::UpdateDrones -> MechaDroneLogic::Build -> PlanetFactory::BuildFinally
            // PlanetFactory::BuildFinally calls FlattenTerrain and SetSandCount.
            harmony = new Harmony("org.greyhak.plugins.dspdroneclearing");
            harmony.PatchAll(typeof(DysonSphereDroneClearing));
            //harmony.PatchAll(typeof(DroneAction_Mine));

            Logger.LogInfo("Initialization complete.");
        }

        [HarmonyPrefix, HarmonyPatch(typeof(MechaDroneLogic), "UpdateTargets")]
        public static void MechaDroneLogic_UpdateTargets_Prefix(MechaDroneLogic __instance, Player ___player)
        {
            if (___player.factory != null &&
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
                                if (prebuild.upEntity == vegeData.id)
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
                    //sb.AppendFormat("Initiating mining of {0} {1}", vegeProto.Type.ToString(), LDB.items.Select(vegeProto.MiningItem[0]).name);
                    //Logger.LogInfo(sb.ToString());

                    PrebuildData prebuild = default;
                    prebuild.protoId = -1;
                    prebuild.modelId = 0;  // Saves always open as 0
                    prebuild.recipeId = -1;
                    prebuild.refCount = 0;
                    prebuild.upEntity = vegeData.id;
                    prebuild.pos = prebuild.pos2 = ___player.controller.actionBuild.previewPose.position + ___player.controller.actionBuild.previewPose.rotation * vegeData.pos;
                    prebuild.rot = vegeData.rot;

                    // This operation will cause a drone to be assigned.
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
                            __instance.forward = missionData.forward;
                            __instance.position = missionData.position;

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

        [HarmonyPostfix, HarmonyPatch(typeof(PlanetFactory), "GameTick")]
        public static void PlanetFactory_GameTick_Postfix(long time)
        {
            for (int activeMissionIdx = 0; activeMissionIdx < activeMissions.Count; ++activeMissionIdx)
            {
                activeMissions[activeMissionIdx].mineAction.DroneGameTick(time);
            }
        }
    }
}
