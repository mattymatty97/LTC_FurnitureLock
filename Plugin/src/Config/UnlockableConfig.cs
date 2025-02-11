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
    public bool DefaultsInitialized { get; internal set; }
    private bool _stored;
    private Vector3? _position;
    private Vector3? _rotation;

    public bool IsValid
    {
        get
        {
            if (Unlockable.unlockableType == 0)
                return false;
            if (!Unlockable.IsPlaceable)
                return false;
            if (!_position.HasValue && !DefaultsInitialized)
                return false;
            if (!_rotation.HasValue && !DefaultsInitialized)
                return false;
            return true;
        }
    }

    public UnlockableItem Unlockable { get; }
    public int UnlockableID { get; }

    public Vector3 Position
    {
        get => _position ?? DefaultPosition;
        private set => _position = value;
    }

    public Vector3 Rotation
    {
        get => _rotation ?? DefaultRotation;
        private set => _rotation = value;
    }
    
    public Vector3 DefaultPosition { get; internal set; }
    public Vector3 DefaultRotation { get; internal set; }
    public bool Locked { get; internal set; }

    public bool Stored
    {
        get => _stored && !Locked;
        private set => _stored = value;
    }
    internal ConfigEntry<string> PositionConfig { get; private set; }
    internal ConfigEntry<string> RotationConfig { get; private set; }
    internal ConfigEntry<bool> LockedConfig { get; private set; }
    internal ConfigEntry<bool> StoredConfig { get; private set; }
    
    public UnlockableConfig(UnlockableItem unlockable, int unlockableID)
    {
        Unlockable = unlockable;
        UnlockableID = unlockableID;
        
        FurnitureLock.Log.LogInfo($"Registering {unlockable.unlockableName}");
        
        if (Unlockable.unlockableType == 0)
        {
            FurnitureLock.Log.LogWarning($"{unlockable.unlockableName} is a suit. SKIPPING!");
            return;
        }
        
        if (!Unlockable.IsPlaceable)
        {
            FurnitureLock.Log.LogWarning($"{unlockable.unlockableName} is not Placeable. SKIPPING!");
            return;
        }
        
        var name = unlockable.unlockableName;
        var strippedName = Regex.Replace(name,@"[\n\t\\\'\[\]]", "").Trim();

        var config = FurnitureLock.INSTANCE.Config;
        
        PositionConfig = config.Bind(strippedName, "position", "default",
            "default position of the Furniture piece.");
        RotationConfig = config.Bind(strippedName, "rotation", "default",
            "default rotation of the Furniture piece.");
        LockedConfig = config.Bind(strippedName, "locked", false,
            "if true the furniture piece will not be movable");
        if (unlockable.canBeStored)
            StoredConfig = config.Bind(strippedName, "spawn_stored", false,
                "if true the furniture piece will be stored immediately upon spawn");

        if (LethalConfigProxy.Enabled)
        {
            LethalConfigProxy.AddConfig(PositionConfig);
            LethalConfigProxy.AddConfig(RotationConfig);
            LethalConfigProxy.AddConfig(LockedConfig);
            if (unlockable.canBeStored)
                LethalConfigProxy.AddConfig(StoredConfig);

            LethalConfigProxy.AddButton(strippedName, "Set Values", "copy current position and rotation to config",
                "Copy",
                CopyValues);


            LethalConfigProxy.AddButton(strippedName, "Apply values", "apply current config values", "Apply",
                () => ApplyValues());
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

        FurnitureLock.Log.LogDebug(
            $"Placeable \"{unlockable.unlockableName}\" pos: {Position} rot: {Rotation} lock:{Locked} stored:{Stored}");
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
    }
    
    internal void ApplyValues(GameObject gameObject = null, bool placementSound = true)
    {
        try
        {
            var startOfRound = StartOfRound.Instance;
            if (startOfRound == null)
                return;

            if (!startOfRound.IsServer)
            {
                FurnitureLock.Log.LogError($"{Unlockable.unlockableName} Only the Host can apply values!");
                return;
            }

            if (!gameObject && !StartOfRound.Instance.SpawnedShipUnlockables.TryGetValue(UnlockableID, out gameObject))
            {
                var placeableShipObjects = UnityEngine.Object.FindObjectsOfType<PlaceableShipObject>();
                foreach (var shipObject in placeableShipObjects)
                {
                    if (shipObject.unlockableID == UnlockableID)
                        gameObject = shipObject.parentObject.gameObject;
                }

                if (gameObject == null)
                    return;
            }

            if ((!Stored || Locked) && Unlockable.inStorage)
            {
                startOfRound.ReturnUnlockableFromStorageServerRpc(UnlockableID);
                FurnitureLock.Log.LogDebug($"{Unlockable.unlockableName} Forced out of storage");
            }

            var placeableShipObject = gameObject.GetComponentInChildren<PlaceableShipObject>();
            if (IsValid)
            {
                ShipBuildModeManager.Instance.PlaceShipObject(Position, Rotation, placeableShipObject, placementSound);
                if (GameNetworkManager.Instance.localPlayerController != null)
                    ShipBuildModeManager.Instance.PlaceShipObjectServerRpc(Position, Rotation, gameObject,
                        (int)GameNetworkManager.Instance.localPlayerController.playerClientId);

                FurnitureLock.Log.LogDebug($"{Unlockable.unlockableName} moved to pos:{Position} rot:{Rotation}");
            }

            if (Stored && !Locked && !Unlockable.inStorage)
            {
                ShipBuildModeManager.Instance.StoreObjectServerRpc(gameObject, -1);
                FurnitureLock.Log.LogDebug($"{Unlockable.unlockableName} Forced in storage");
            }
        }
        catch (Exception ex)
        {
            FurnitureLock.Log.LogError($"{Unlockable.unlockableName} crashed while moving:\n{ex}");
        }
    }

    private void OnPositionConfigOnSettingChanged()
    {
        try
        {
            var sections = PositionConfig.Value.Split(',');

            var posArray = sections.Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();

            var newPos = new Vector3(posArray[0], posArray[1], posArray[2]);

            _position = newPos;
        }
        catch (Exception)
        {
            _position = null;
        }
    }

    private void OnRotationConfigOnSettingChanged()
    {
        try
        {
            var sections = RotationConfig.Value.Split(',');

            var posArray = sections.Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();

            var newRot = new Vector3(posArray[0], posArray[1], posArray[2]);

            _rotation = newRot;
        }
        catch (Exception)
        {
            _rotation = null;
        }
    }

    private void OnLockedConfigOnSettingChanged()
    {
        Locked = LockedConfig.Value;
        if (Locked)
            ApplyValues();
    }
        

    private void OnStoredConfigOnSettingChanged()
    {
        Stored = StoredConfig.Value;
    }

    public override string ToString()
    {
        return $"{Unlockable.unlockableName}({UnlockableID})";
    }
}