using System;
using System.Collections.Generic;
using System.Reflection;
using FurnitureLock.Dependency;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FurnitureLock.Config;
using FurnitureLock.Patches;
using HarmonyLib;
using MonoMod.RuntimeDetour;

// ReSharper disable MemberCanBePrivate.Global

namespace FurnitureLock
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("BMX.LobbyCompatibility", Flags:BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ainavt.lc.lethalconfig", Flags:BepInDependency.DependencyFlags.SoftDependency)]
    internal class FurnitureLock : BaseUnityPlugin
    {
        public const string GUID = MyPluginInfo.PLUGIN_GUID;
        public const string NAME = MyPluginInfo.PLUGIN_NAME;
        public const string VERSION = MyPluginInfo.PLUGIN_VERSION;

        internal static ManualLogSource Log;
        internal static readonly List<Hook> Hooks = [];
        internal static readonly Harmony Harmony = new (GUID);
        
        public static FurnitureLock INSTANCE { get; private set; }
        
        private void Awake()
        {
	        INSTANCE = this;
            Log = Logger;
            try
            {
				if (LobbyCompatibilityChecker.Enabled)
					LobbyCompatibilityChecker.Init();
				Log.LogInfo("Initializing Configs");

				PluginConfig.Init();
				
				Log.LogInfo("Patching Methods");
				
				ShipBuildModeManagerPatch.Init();
				StartOfRoundPatch.Init();

	            Harmony.PatchAll(Assembly.GetExecutingAssembly());
				
				Log.LogInfo(NAME + " v" + VERSION + " Loaded!");
            }
            catch (Exception ex)
            {
                Log.LogError("Exception while initializing: \n" + ex);
            }
        }
        
        internal static class PluginConfig
        {

	        internal static readonly Dictionary<UnlockableItem, UnlockableConfig> UnlockableConfigs = new();
            internal static void Init()
            {
                var config = INSTANCE.Config;

                if (LethalConfigProxy.Enabled)
                {
	                LethalConfigProxy.AddButton("Cleanup", "Clear old entries", "remove unused entries in the config file\n(IF RUN FROM MENU WILL DELETE ALL ITEMS!!)", "Clean&Save", CleanAndSave);
	                LethalConfigProxy.AddButton("Bulk Actions", "Copy All", "copy position and rotation of all the furniture in the lobby\n(THIS WILL OVERWRITE ALL VALUES!!)", "Copy All", CopyAll);
	                LethalConfigProxy.AddButton("Bulk Actions", "Lock All", "mark all the furniture as locked\n(THIS WILL PULL ALL THE FURNITURE OUT OF STORAGE)", "Lock All", LockAll);
	                LethalConfigProxy.AddButton("Bulk Actions", "Unlock All", "mark all the furniture as unlocked", "Unlock All", UnlockAll);
                }
                //Initialize Configs
	        }

            internal static void CleanAndSave()
            {
	            var config = INSTANCE.Config;
	            //remove unused options
	            var orphanedEntriesProp = config.GetType().GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance);

	            var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp!.GetValue(config, null);

	            orphanedEntries.Clear(); // Clear orphaned entries (Unbinded/Abandoned entries)
	            config.Save(); // Save the config file
            }
            
            internal static void CopyAll()
            {
	            var config = INSTANCE.Config;
	            
	            foreach (var unlockableConfig in UnlockableConfigs.Values)
	            {
		            if (!unlockableConfig.IsValid)
			            continue;
		            
		            unlockableConfig.CopyValues();
	            }

	            config.Save();
            }
            
            internal static void LockAll()
            {
	            var config = INSTANCE.Config;
	            
	            foreach (var unlockableConfig in UnlockableConfigs.Values)
	            {
		            if (!unlockableConfig.IsValid)
			            continue;
		            
		            unlockableConfig.Locked = true;
		            unlockableConfig.ApplyValues();
	            }

	            config.Save();
            }
            
            internal static void UnlockAll()
            {
	            var config = INSTANCE.Config;
	            
	            foreach (var unlockableConfig in UnlockableConfigs.Values)
	            {
		            if (!unlockableConfig.IsValid)
			            continue;
		            
		            unlockableConfig.Locked = false;
	            }

	            config.Save();
            }
            
        }

    }
}
