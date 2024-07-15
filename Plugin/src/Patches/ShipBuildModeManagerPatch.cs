using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace FurnitureLock.Patches;

[HarmonyPatch(typeof(ShipBuildModeManager))]
internal class ShipBuildModeManagerPatch
{
    [HarmonyFinalizer]
    [HarmonyPatch(nameof(ShipBuildModeManager.StoreObjectServerRpc))]
    [HarmonyPriority(Priority.Last)]
    private static void PreventStore(ShipBuildModeManager __instance, NetworkObjectReference objectRef)
    {
        if (!__instance.IsServer)
            return;
        
        if (!objectRef.TryGet(out var networkObject))
            return;
    
        FurnitureLock.Log.LogDebug($"StoreObjectServerRpc 1");
    
        var shipObject = networkObject.gameObject.GetComponentInChildren<PlaceableShipObject>();
        if (shipObject == null)
            return;
    
        FurnitureLock.Log.LogDebug($"StoreObjectServerRpc 2");
    
        var unlockable = StartOfRound.Instance.unlockablesList.unlockables[shipObject.unlockableID];
        if(!unlockable.inStorage)
            return;
    
        FurnitureLock.Log.LogDebug($"StoreObjectServerRpc 3");
    
        if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
            return;
    
        FurnitureLock.Log.LogDebug($"StoreObjectServerRpc 4");
    
        if (!config.Locked)
            return;

        FurnitureLock.Log.LogDebug($"Prevented Store for {unlockable.unlockableName}");
        StartOfRound.Instance.ReturnUnlockableFromStorageServerRpc(shipObject.unlockableID);
    }
    
    

    [HarmonyPrefix]
    [HarmonyPatch(nameof(ShipBuildModeManager.PlaceShipObjectServerRpc))]
    [HarmonyPriority(Priority.Last)]
    private static void OnMoveFurniture(ShipBuildModeManager __instance, 
        ref Vector3 newPosition,
        ref Vector3 newRotation,
        NetworkObjectReference objectRef,
        ref int playerWhoMoved)
    {
        var networkManager = __instance.NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
            return;
        
        if (__instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Server 
            || !networkManager.IsServer 
            && !networkManager.IsHost 
            || !objectRef.TryGet(out var networkObject))
            return;
        
        var componentInChildren = networkObject.gameObject.GetComponentInChildren<PlaceableShipObject>();
        if (componentInChildren == null)
            return;
        
        var unlockable = StartOfRound.Instance.unlockablesList.unlockables[componentInChildren.unlockableID];
        if( unlockable.inStorage )
            return;
        
        if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
            return;
        
        if (!config.Locked)
            return;
        
        FurnitureLock.Log.LogDebug($"{unlockable.unlockableName} forced to pos:{config.Position} rot:{config.Rotation}");
        newPosition = config.Position;
        newRotation = config.Rotation;
        playerWhoMoved = -1;
    }
    
}