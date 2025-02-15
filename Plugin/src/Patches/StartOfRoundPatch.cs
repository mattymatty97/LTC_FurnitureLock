using System;
using FurnitureLock.Config;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FurnitureLock.Patches;

[HarmonyPatch(typeof(StartOfRound))]
internal class StartOfRoundPatch
{

    private static bool _isShipResetting = false;

    internal static void Init()
    {

        var resetShipFurnitureMethod = AccessTools.Method(typeof(StartOfRound), nameof(StartOfRound.ResetShipFurniture));

        FurnitureLock.Hooks.Add(new Hook(resetShipFurnitureMethod, OnResetShipFurniture));


        var loadUnlockablesMethod = AccessTools.Method(typeof(StartOfRound), nameof(StartOfRound.LoadUnlockables));

        FurnitureLock.Hooks.Add(new Hook(loadUnlockablesMethod, OnLoadUnlockables));

    }

    private static void OnResetShipFurniture(Action<StartOfRound, bool, bool> orig, StartOfRound self, bool onlyClearBoughtFurniture,
        bool despawnProps)
    {
        _isShipResetting = true;
        try
        {
            orig(self, onlyClearBoughtFurniture, despawnProps);
        }
        finally
        {
            _isShipResetting = false;
        }
    }

    private static void OnLoadUnlockables(Action<StartOfRound> orig, StartOfRound self)
    {
        _isShipResetting = true;
        try
        {
            orig(self);
        }
        finally
        {
            _isShipResetting = false;
        }
    }


    [HarmonyFinalizer]
    [HarmonyPatch(nameof(StartOfRound.Start))]
    [HarmonyPriority(Priority.Last)]
    private static void BeforeStart(StartOfRound __instance)
    {
        //Bind config
        for (var index = 0; index < __instance.unlockablesList.unlockables.Count; index++)
        {
            var unlockable = __instance.unlockablesList.unlockables[index];

            if (FurnitureLock.PluginConfig.UnlockableConfigs.ContainsKey(unlockable))
                continue;
            try
            {
                FurnitureLock.PluginConfig.UnlockableConfigs[unlockable] = new UnlockableConfig(unlockable, index);
            }
            catch (Exception ex)
            {
                FurnitureLock.Log.LogError(ex);
            }
        }
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(nameof(StartOfRound.LoadUnlockables))]
    [HarmonyPriority(Priority.Last)]
    private static void AfterLoadUnlockables(StartOfRound __instance)
    {
        if (!__instance.IsServer)
            return;

        ApplyDefaults(__instance, true, true, true);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SpawnUnlockable))]
    private static void AfterUnlockableSpawn(StartOfRound __instance, int unlockableIndex)
    {
        if (!__instance.IsServer)
            return;

        if (_isShipResetting)
            return;
    
        var unlockable = __instance.unlockablesList.unlockables[unlockableIndex];
        
        if (!__instance.SpawnedShipUnlockables.TryGetValue(unlockableIndex, out var gameObject))
            return;
    
        if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
            return;
    
        var gameNetworkManager = GameNetworkManager.Instance;

        if (ES3.KeyExists("ShipUnlockMoved_" + unlockable.unlockableName, gameNetworkManager.currentSaveFileName))
        {
            FurnitureLock.Log.LogDebug($"{unlockable.unlockableName} was moved. locked? {config.Locked}");

            //if we're not locked use stored locations
            if (!config.Locked)
                return;
        }
        
        config.ApplyValues(gameObject, false, gameNetworkManager.localPlayerController == null);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(StartOfRound.EndPlayersFiredSequenceClientRpc))]
    private static void AfterEject(StartOfRound __instance)
    {
        var networkManager = __instance.NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
            return;
        if (__instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Client || !networkManager.IsClient && !networkManager.IsHost)
            return;
        
        if (!__instance.IsServer)
            return;

        ApplyDefaults(__instance,false, true);
    }

    private static void ApplyDefaults(StartOfRound startOfRound, bool skipMoved, bool silent = false, bool localOnly = false)
    {
       
        var placeableShipObjects = Object.FindObjectsOfType<PlaceableShipObject>();
        foreach (var shipObject in placeableShipObjects)
        {
            var unlockable = startOfRound.unlockablesList.unlockables[shipObject.unlockableID];
            try
            {
                var gameObject = shipObject.parentObject.gameObject;
                
                if (unlockable.unlockableType == 0)
                    continue;

                if (!unlockable.IsPlaceable)
                    continue;

                if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
                    continue;

                if (skipMoved && unlockable.hasBeenMoved && !config.Locked)
                    continue;

                config.ApplyValues(gameObject, false, silent || localOnly);
            }
            catch (Exception ex)
            {
                FurnitureLock.Log.LogError($"Error defaulting {unlockable.unlockableName}:\n{ex}");
            }
        }
        
        foreach (var unlockable in startOfRound.unlockablesList.unlockables)
        {
            if (unlockable.unlockableType == 0)
                continue;

            if (!unlockable.IsPlaceable)
                continue;

            if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
                continue;

            if (config.Locked && unlockable.inStorage)
            {
                startOfRound.ReturnUnlockableFromStorageServerRpc(config.UnlockableID);
            }
        }

        if (silent && !localOnly)
            StartOfRound.Instance.SyncShipUnlockablesServerRpc();
    }
}
