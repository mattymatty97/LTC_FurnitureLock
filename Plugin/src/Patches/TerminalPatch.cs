using System;
using FurnitureLock.Config;
using HarmonyLib;

namespace FurnitureLock.Patches;

[HarmonyPatch(typeof(Terminal))]
public class TerminalPatch
{
    [HarmonyFinalizer]
    [HarmonyPatch(nameof(Terminal.Awake))]
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
}