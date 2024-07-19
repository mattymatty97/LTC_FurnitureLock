using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using FurnitureLock.Dependency;
using UnityEngine;

namespace FurnitureLock.Config;

public class UnlockableConfig
{
    public bool IsValid => !(Unlockable.placedPosition.Equals(default) || Unlockable.placedRotation.Equals(default));
    
    public UnlockableItem Unlockable { get; }
    public int UnlockableID { get; }
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public bool Locked { get; set; }
    public bool Stored { get; set; }
    internal ConfigEntry<string> PositionConfig { get; private set; }
    internal ConfigEntry<string> RotationConfig { get; private set; }
    internal ConfigEntry<bool> LockedConfig { get; private set; }
    internal ConfigEntry<bool> StoredConfig { get; private set; }
    
    public UnlockableConfig(UnlockableItem unlockable, int unlockableID)
    {
        Unlockable = unlockable;
        UnlockableID = unlockableID;
        
        FurnitureLock.Log.LogInfo($"Registering {unlockable.unlockableName}");
        
        var name = unlockable.unlockableName;
        var strippedName = Regex.Replace(name,@"[\n\t\\\'\[\]]", "").Trim();

        var config = FurnitureLock.INSTANCE.Config;

        PositionConfig = config.Bind(strippedName, "position", "not set", "default position of the Furniture piece.");
        RotationConfig = config.Bind(strippedName, "rotation", "not set", "default rotation of the Furniture piece.");
        LockedConfig = config.Bind(strippedName, "locked", false, "if true the furniture piece will not be movable");
        if (unlockable.canBeStored)
            StoredConfig = config.Bind(strippedName, "spawn_stored", false, "if true the furniture piece will be stored immediately upon spawn");
       

        if (LethalConfigProxy.Enabled)
        {
            LethalConfigProxy.AddConfig(PositionConfig);
            LethalConfigProxy.AddConfig(RotationConfig);
            LethalConfigProxy.AddConfig(LockedConfig);
            if (unlockable.canBeStored)
                LethalConfigProxy.AddConfig(StoredConfig);

            LethalConfigProxy.AddButton(strippedName, "Set Values", "copy current position and rotation to config", "Copy",
                CopyValues);

            

            LethalConfigProxy.AddButton(strippedName, "Apply values", "apply current config values", "Apply",
                ApplyValues);
        }

        OnPositionConfigOnSettingChanged();
        PositionConfig.SettingChanged += (_, _) => OnPositionConfigOnSettingChanged();

        OnRotationConfigOnSettingChanged();
        RotationConfig.SettingChanged += (_, _) => OnRotationConfigOnSettingChanged();

        OnLockedConfigOnSettingChanged();
        LockedConfig.SettingChanged += (_, _) => OnLockedConfigOnSettingChanged();
        
        if (unlockable.canBeStored)
        {
            OnStoredConfigOnSettingChanged();
            StoredConfig!.SettingChanged += (_, _) => OnStoredConfigOnSettingChanged();
        }

        FurnitureLock.Log.LogDebug($"{unlockable.unlockableName} pos: {Position} rot: {Rotation} lock:{Locked} stored:{Stored}");
        return;

        
    }
    
    internal void CopyValues()
    {
        if (Unlockable.placedPosition.Equals(default) || Unlockable.placedRotation.Equals(default))
        {
            FurnitureLock.Log.LogError($"{Unlockable.unlockableName} Cannot copy values from default or missing furniture");
            return;
        }

        var pos = Unlockable.placedPosition;
        PositionConfig.Value = $"{pos.x.ToString(CultureInfo.InvariantCulture)}, {pos.y.ToString(CultureInfo.InvariantCulture)}, {pos.z.ToString(CultureInfo.InvariantCulture)}";
        var rot = Unlockable.placedRotation;
        RotationConfig.Value = $"{rot.x.ToString(CultureInfo.InvariantCulture)}, {rot.y.ToString(CultureInfo.InvariantCulture)}, {rot.z.ToString(CultureInfo.InvariantCulture)}";
        FurnitureLock.PluginConfig.CleanAndSave();
    }
    
    internal void ApplyValues()
    {
        if (Position.Equals(default) || Rotation.Equals(default))
        {
            FurnitureLock.Log.LogError($"{Unlockable.unlockableName} Cannot apply default values");
            return;
        }

        if (StartOfRound.Instance != null && StartOfRound.Instance.SpawnedShipUnlockables.TryGetValue(UnlockableID, out var gameObject))
        {
            ShipBuildModeManager.Instance.PlaceShipObjectServerRpc(Position, Rotation, gameObject, -1);
        }
    }
    
    void OnPositionConfigOnSettingChanged()
    {
        try
        {
            var sections = PositionConfig.Value.Split(',');

            var posArray = sections.Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();

            var newPos = new Vector3(posArray[0], posArray[1], posArray[2]);

            Position = newPos;
        }
        catch (Exception)
        {
            Position = default;
        }
    }

    private void OnRotationConfigOnSettingChanged()
    {
        try
        {
            var sections = RotationConfig.Value.Split(',');

            var posArray = sections.Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();

            var newRot = new Vector3(posArray[0], posArray[1], posArray[2]);

            Rotation = newRot;
        }
        catch (Exception)
        {
            Rotation = default;
        }
    }

    private void OnLockedConfigOnSettingChanged()
    {
        Locked = LockedConfig.Value;
    }
        

    private void OnStoredConfigOnSettingChanged()
    {
        Stored = StoredConfig.Value;
    }

}