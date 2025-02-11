using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace FurnitureLock.Patches;

[HarmonyPatch(typeof(ShipBuildModeManager))]
internal class ShipBuildModeManagerPatch
{
    private static uint? _returnUnlockableFromStorageClientRpcID;
    internal static void Init()
    {
        var methodInfo =
            AccessTools.Method(typeof(StartOfRound), nameof(StartOfRound.ReturnUnlockableFromStorageClientRpc));

        if (Utils.TryGetRpcID(methodInfo, out var id))
        {
            _returnUnlockableFromStorageClientRpcID = id;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(ShipBuildModeManager.StoreObjectLocalClient))]
    [HarmonyPriority(Priority.Last)]
    private static bool PreventStoreHost(ShipBuildModeManager __instance)
    {
        if (!__instance.IsServer)
            return true;

        if ( __instance.timeSincePlacingObject <= 0.25 ||
             !__instance.InBuildMode ||
             !__instance.placingObject ||
             !StartOfRound.Instance.unlockablesList.unlockables[__instance.placingObject.unlockableID].canBeStored)
            return true;

        var shipObject = __instance.placingObject;
        if (shipObject == null)
            return true;

        var unlockable = StartOfRound.Instance.unlockablesList.unlockables[shipObject.unlockableID];

        if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
            return true;

        if (!config.Locked)
            return true;

        FurnitureLock.Log.LogDebug($"Prevented Store for {unlockable.unlockableName}");
        __instance.CancelBuildMode(false);
        shipObject.GetComponent<AudioSource>().PlayOneShot(shipObject.placeObjectSFX);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(ShipBuildModeManager.StoreObjectServerRpc))]
    [HarmonyPriority(Priority.Last)]
    private static bool PreventStore(ShipBuildModeManager __instance, NetworkObjectReference objectRef, int playerWhoStored)
    {
        var networkManager = __instance.NetworkManager;
        if (__instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Server ||
            !networkManager.IsServer && !networkManager.IsHost)
            return true;
        
        if (!objectRef.TryGet(out var networkObject))
            return true;

        var shipObject = networkObject.gameObject.GetComponentInChildren<PlaceableShipObject>();
        if (shipObject == null)
            return true;
        
        var unlockable = StartOfRound.Instance.unlockablesList.unlockables[shipObject.unlockableID];

        if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
            return true;

        if (!config.Locked)
            return true;
        
        FurnitureLock.Log.LogDebug($"Prevented Store for {unlockable.unlockableName}");

        if (_returnUnlockableFromStorageClientRpcID.HasValue)
        {
            var rpcID = _returnUnlockableFromStorageClientRpcID.Value;
            var startOfRound = StartOfRound.Instance;
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds =
                    [
                        (ulong)playerWhoStored
                    ]
                }
            };
            FastBufferWriter bufferWriter = startOfRound.__beginSendClientRpc(rpcID, clientRpcParams, RpcDelivery.Reliable);
            BytePacker.WriteValueBitPacked(bufferWriter, shipObject.unlockableID);
            startOfRound.__endSendClientRpc(ref bufferWriter, rpcID, clientRpcParams, RpcDelivery.Reliable);
        }

        return false;
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
        
        if (!config.IsValid)
            return;
        
        FurnitureLock.Log.LogDebug($"{unlockable.unlockableName} forced to pos:{config.Position} rot:{config.Rotation}");
        newPosition = config.Position;
        newRotation = config.Rotation;
        playerWhoMoved = -1;
    }
    
}
