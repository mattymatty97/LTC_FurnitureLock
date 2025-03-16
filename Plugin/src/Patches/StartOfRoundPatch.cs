using System;
using FurnitureLock.Config;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using Unity.Netcode;
using Object = UnityEngine.Object;

namespace FurnitureLock.Patches;

[HarmonyPatch(typeof(StartOfRound))]
internal class StartOfRoundPatch
{

    private static bool _isShipResetting;

    internal static void Init()
    {

        var resetShipFurnitureMethod = AccessTools.Method(typeof(StartOfRound), nameof(StartOfRound.EndPlayersFiredSequenceClientRpc));

        FurnitureLock.Hooks.Add(new Hook(resetShipFurnitureMethod, OnEjectSequence));


        var loadUnlockablesMethod = AccessTools.Method(typeof(StartOfRound), nameof(StartOfRound.LoadUnlockables));

        FurnitureLock.Hooks.Add(new Hook(loadUnlockablesMethod, OnLoadUnlockables));

    }

    private static void OnEjectSequence(Action<StartOfRound> original, StartOfRound self)
    {
        _isShipResetting = true;
        try
        {
            //run vanilla code
            original(self);

            //on host
            if (!self.IsServer)
                return;

            var networkManager = self.NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
                return;

            if (self.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Client ||
                (!networkManager.IsClient && !networkManager.IsHost))
                return;

            //unlock extra furniture
            foreach (var unlockable in self.unlockablesList.unlockables)
            {
                if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
                    continue;

                if (config.AlreadyUnlocked && !unlockable.hasBeenUnlockedByPlayer)
                {
                    self.UnlockShipObject(config.UnlockableID);
                }
            }

            //move stuff to configured positions
            ApplyDefaults(self,false, true);
        }
        finally
        {
            _isShipResetting = false;
        }
    }

    private static void OnLoadUnlockables(Action<StartOfRound> original, StartOfRound self)
    {
        _isShipResetting = true;
        try
        {
            var isNewFile = !ES3.KeyExists("UnlockedShipObjects", GameNetworkManager.Instance.currentSaveFileName);

            //run vanilla function
            original(self);

            //register other unlockables
            for (var index = 0; index < self.unlockablesList.unlockables.Count; index++)
            {
                var unlockable = self.unlockablesList.unlockables[index];

                if (FurnitureLock.PluginConfig.UnlockableConfigs.ContainsKey(unlockable))
                    continue;

                try
                {
                    FurnitureLock.PluginConfig.UnlockableConfigs[unlockable] = new UnlockableConfig(unlockable, index);
                }
                catch (Exception ex)
                {
                    FurnitureLock.Log.LogError($"Exception registering {unlockable.unlockableName}: {ex}");
                }
            }

            //on host
            if (!self.IsServer)
                return;

            if (isNewFile)
            {
                //unlock extra furniture
                foreach (var unlockable in self.unlockablesList.unlockables)
                {
                    if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
                        continue;

                    if (config.AlreadyUnlocked && !unlockable.hasBeenUnlockedByPlayer)
                    {
                        self.UnlockShipObject(config.UnlockableID);
                        config.ApplyValues(null, false, true);
                    }
                }
            }

            //move stuff to configured positions
            ApplyDefaults(self, true, true, true);
        }
        finally
        {
            _isShipResetting = false;
        }
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

    private static void ApplyDefaults(StartOfRound startOfRound, bool skipMoved, bool silent = false, bool localOnly = false)
    {
       
        var placeableShipObjects = Object.FindObjectsOfType<PlaceableShipObject>();
        foreach (var shipObject in placeableShipObjects)
        {

            if (!shipObject)
                continue;

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
