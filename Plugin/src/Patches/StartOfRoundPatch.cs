using FurnitureLock.Config;
using HarmonyLib;
using UnityEngine;

namespace FurnitureLock.Patches;

[HarmonyPatch(typeof(StartOfRound))]
internal class StartOfRoundPatch
{

    [HarmonyPrefix]
    [HarmonyPatch(nameof(StartOfRound.Start))]
    [HarmonyPriority(Priority.Last)]
    private static void BeforeStart(StartOfRound __instance)
    {
        if (!__instance.IsServer)
            return;
        
        //Bind config
        foreach (var unlockable in __instance.unlockablesList.unlockables)
        {
            //do not handle suits
            if (unlockable.unlockableType == 0)
                continue;
            
            if (!unlockable.IsPlaceable)
                continue;
            
            if (FurnitureLock.PluginConfig.UnlockableConfigs.ContainsKey(unlockable))
                continue;

            FurnitureLock.PluginConfig.UnlockableConfigs[unlockable] = new UnlockableConfig(unlockable);
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


    [HarmonyPrefix]
    [HarmonyPatch(nameof(StartOfRound.SpawnUnlockable))]
    [HarmonyPriority(Priority.Last)]
    private static void OnUnlockableSpawn(StartOfRound __instance, int unlockableIndex)
    {
        if (!__instance.IsServer)
            return;
        
        var unlockable = __instance.unlockablesList.unlockables[unlockableIndex];
        
        if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
            return;
        
        var gameNetworkManager = GameNetworkManager.Instance;

        //if we're not locked use stored locations
        if (ES3.KeyExists("ShipUnlockMoved_" + unlockable.unlockableName, gameNetworkManager.currentSaveFileName)
            && !config.Locked)
            return;
        
        if (!config.Locked)
            return;
        
        FurnitureLock.Log.LogDebug($"{unlockable.unlockableName} forced to pos:{config.Position} rot:{config.Rotation}");

        ES3.Save<bool>("ShipUnlockMoved_" + unlockable.unlockableName, true, gameNetworkManager.currentSaveFileName);
        ES3.Save<Vector3>("ShipUnlockPos_" + unlockable.unlockableName, config.Position, gameNetworkManager.currentSaveFileName);
        ES3.Save<Vector3>("ShipUnlockRot_" + unlockable.unlockableName, config.Rotation, gameNetworkManager.currentSaveFileName);
      
    }
}