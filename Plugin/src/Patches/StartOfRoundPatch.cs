using System;
using FurnitureLock.Config;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

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

    [HarmonyPrefix]
    [HarmonyPatch(nameof(StartOfRound.LoadUnlockables))]
    [HarmonyPriority(Priority.Last)]
    private static void OnLoadUnlockables(StartOfRound __instance)
    {
        if (!__instance.IsServer)
            return;

        if (!ES3.KeyExists("UnlockedShipObjects", GameNetworkManager.Instance.currentSaveFileName))
        {
            using (ListPool<int>.Get(out var intList))
            {
                for (int i = 0; i < __instance.unlockablesList.unlockables.Count; i++)
                {
                    var unlockable = __instance.unlockablesList.unlockables[i];
                    if (unlockable.alreadyUnlocked)
                        intList.Add(i);
                }
                
                ES3.Save<int[]>("UnlockedShipObjects", intList.ToArray(), GameNetworkManager.Instance.currentSaveFileName);
            }
        }
        
        
        foreach (var unlockable in __instance.unlockablesList.unlockables)
        {
       
            if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
                continue;

            if (!config.Locked)
                continue;
            
            FurnitureLock.Log.LogDebug($"{unlockable.unlockableName} forced out of storage");
            
            //if the furniture is locked prevent storing it!
            ES3.Save<bool>("ShipUnlockStored_" + unlockable.unlockableName, false, GameNetworkManager.Instance.currentSaveFileName);
        }
    }

    
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SpawnUnlockable))]
    [HarmonyPriority(Priority.Last)]
    internal static class SpawnUnlockablePatch
    {
        private static void Prefix(StartOfRound __instance, bool __state, int unlockableIndex)
        {
            if (!__instance.IsServer)
                return;
        
            var unlockable = __instance.unlockablesList.unlockables[unlockableIndex];
        
            if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
                return;
        
            var gameNetworkManager = GameNetworkManager.Instance;

            //if we're not locked use stored locations
            if (ES3.KeyExists("ShipUnlockMoved_" + unlockable.unlockableName, gameNetworkManager.currentSaveFileName))
            {
                __state = true;
                
                FurnitureLock.Log.LogDebug($"{unlockable.unlockableName} was moved locked {config.Locked}");

                if (!config.Locked)
                    return;
            } 
            
            FurnitureLock.Log.LogDebug($"{unlockable.unlockableName} valid:{config.IsValid}");

            
            if (!config.IsValid)
                return;
        
            FurnitureLock.Log.LogDebug($"{unlockable.unlockableName} defaulted to pos:{config.Position} rot:{config.Rotation}");

            ES3.Save<bool>("ShipUnlockMoved_" + unlockable.unlockableName, true, gameNetworkManager.currentSaveFileName);
            ES3.Save<Vector3>("ShipUnlockPos_" + unlockable.unlockableName, config.Position, gameNetworkManager.currentSaveFileName);
            ES3.Save<Vector3>("ShipUnlockRot_" + unlockable.unlockableName, config.Rotation, gameNetworkManager.currentSaveFileName);
      
        }

        private static void Postfix(StartOfRound __instance, bool __state, int unlockableIndex)
        {
            //run only if it's the first time it spawns
            if (__state)
                return;
            
            var unlockable = __instance.unlockablesList.unlockables[unlockableIndex];
        
            if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
                return;
            
            if (!config.Stored)
                return;
            
            if (__instance.SpawnedShipUnlockables.TryGetValue(unlockableIndex, out var gameObject))
            {
                ShipBuildModeManager.Instance.StoreObjectServerRpc(gameObject, -1);
            }
        }
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

        foreach (var unlockable in __instance.unlockablesList.unlockables)
        {
            try
            {
                if (unlockable.unlockableType == 0)
                    continue;

                if (!unlockable.IsPlaceable)
                    continue;

                if (!unlockable.alreadyUnlocked && !unlockable.hasBeenUnlockedByPlayer &&
                    (!unlockable.unlockedInChallengeFile || !__instance.isChallengeFile))
                    continue;

                if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
                    continue;

                if (config.Stored &&
                    __instance.SpawnedShipUnlockables.TryGetValue(config.UnlockableID, out var gameObject))
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