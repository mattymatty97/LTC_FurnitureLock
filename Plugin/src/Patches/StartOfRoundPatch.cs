using System;
using FurnitureLock.Config;
using HarmonyLib;
using Unity.Netcode;

namespace FurnitureLock.Patches;

[HarmonyPatch(typeof(StartOfRound))]
internal class StartOfRoundPatch
{

    [HarmonyPrefix]
    [HarmonyPatch(nameof(StartOfRound.Start))]
    [HarmonyPriority(Priority.First)]
    private static void BeforeStart(StartOfRound __instance)
    {
        if (!__instance.IsServer)
            return;
        
        //Bind config
        for (var index = 0; index < __instance.unlockablesList.unlockables.Count; index++)
        {
            var unlockable = __instance.unlockablesList.unlockables[index];
            //do not handle suits
            if (unlockable.unlockableType == 0)
                continue;

            if (!unlockable.IsPlaceable)
                continue;

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

        FurnitureLock.PluginConfig.CleanAndSave();
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(StartOfRound.LoadUnlockables))]
    [HarmonyPriority(Priority.Last)]
    private static void AfterLoadUnlockables(StartOfRound __instance)
    {
        if (!__instance.IsServer)
            return;

        if (!ES3.KeyExists("UnlockedShipObjects", GameNetworkManager.Instance.currentSaveFileName))
        {
            ApplyDefaults(__instance, true);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SpawnUnlockable))]
    private static void AfterUnlockableSpawn(StartOfRound __instance, int unlockableIndex)
    {
        if (!__instance.IsServer)
            return;
    
        var unlockable = __instance.unlockablesList.unlockables[unlockableIndex];
        
        if (!__instance.SpawnedShipUnlockables.TryGetValue(unlockableIndex, out var gameObject))
            return;
    
        if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
            return;
    
        var gameNetworkManager = GameNetworkManager.Instance;

        if (ES3.KeyExists("ShipUnlockMoved_" + unlockable.unlockableName, gameNetworkManager.currentSaveFileName))
        {
            FurnitureLock.Log.LogDebug($"{unlockable.unlockableName} was moved locked {config.Locked}");

            //if we're not locked use stored locations
            if (!config.Locked)
                return;
        }
        else if (config.Stored)
        {
            ShipBuildModeManager.Instance.StoreObjectServerRpc(gameObject, -1);
        }

        FurnitureLock.Log.LogDebug($"{unlockable.unlockableName} valid:{config.IsValid}");
        
        if (!config.IsValid)
            return;
    
        FurnitureLock.Log.LogDebug($"{unlockable.unlockableName} defaulted to pos:{config.Position} rot:{config.Rotation}");

        config.ApplyValues();
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

        ApplyDefaults(__instance);
    }

    private static void ApplyDefaults(StartOfRound startOfRound, bool skipMoved=false)
    {
        foreach (var unlockable in startOfRound.unlockablesList.unlockables)
        {
            try
            {
                if (unlockable.unlockableType == 0)
                    continue;

                if (!unlockable.IsPlaceable)
                    continue;

                if (skipMoved && unlockable.hasBeenMoved)
                    continue;
                
                if (!unlockable.alreadyUnlocked && 
                    !unlockable.hasBeenUnlockedByPlayer && 
                    (!unlockable.unlockedInChallengeFile || !startOfRound.isChallengeFile))
                    continue;

                if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
                    continue;

                if (config.Stored &&
                    startOfRound.SpawnedShipUnlockables.TryGetValue(config.UnlockableID, out var gameObject))
                    ShipBuildModeManager.Instance.StoreObjectServerRpc(gameObject, -1);

                if (!config.IsValid)
                    continue;

                config.ApplyValues();
            }
            catch (Exception ex)
            {
                FurnitureLock.Log.LogError($"Error resetting {unlockable.unlockableName}:\n{ex}");
            }
        }
    }
}