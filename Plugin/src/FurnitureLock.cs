using System;
using System.Collections.Generic;
using System.Reflection;
using FurnitureLock.Dependency;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FurnitureLock.Config;
using HarmonyLib;
// ReSharper disable MemberCanBePrivate.Global

namespace FurnitureLock
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("BMX.LobbyCompatibility", Flags:BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ainavt.lc.lethalconfig", Flags:BepInDependency.DependencyFlags.SoftDependency)]
    internal class FurnitureLock : BaseUnityPlugin
    {
        public const string GUID = "mattymatty.FurnitureLock";
        public const string NAME = "FurnitureLock";
        public const string VERSION = "1.2.4";

        internal static ManualLogSource Log;
        
        public static FurnitureLock INSTANCE { get; private set; }
        
        private void Awake()
        {
	        INSTANCE = this;
            Log = Logger;
            try
            {
				if (LobbyCompatibilityChecker.Enabled)
					LobbyCompatibilityChecker.Init();
				if (AsyncLoggerProxy.Enabled)
					AsyncLoggerProxy.WriteEvent(NAME, "Awake", "Initializing");
				Log.LogInfo("Initializing Configs");

				PluginConfig.Init();
				
				Log.LogInfo("Patching Methods");
				var harmony = new Harmony(GUID);
				harmony.PatchAll(Assembly.GetExecutingAssembly());
				
				Log.LogInfo(NAME + " v" + VERSION + " Loaded!");
				if (AsyncLoggerProxy.Enabled)
					AsyncLoggerProxy.WriteEvent(NAME, "Awake", "Finished Initializing");
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
            
        }

    }
}
