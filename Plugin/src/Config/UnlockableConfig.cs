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
    public UnlockableItem Unlockable { get; private set; }
    public int UnlockableID { get; private set; }
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public bool Locked { get; set; }
    
    public bool Stored { get; set; }
    internal ConfigEntry<string> PositionConfig { get; private set; }
    internal ConfigEntry<string> RotationConfig { get; private set; }
    internal ConfigEntry<bool> LockedConfig { get; private set; }
    internal ConfigEntry<bool> StoredConfig { get; private set; }
    
    private Vector3 _defaultPosition;
    private Vector3 _defaultRotation;

    public UnlockableConfig(UnlockableItem unlockable, int unlockableID)
    {
        Unlockable = unlockable;
        UnlockableID = unlockableID;
        
        FurnitureLock.Log.LogInfo($"Registering {unlockable.unlockableName}");
        
        FindDefaults();
        
        var name = unlockable.unlockableName;
        var strippedName = Regex.Replace(name,@"[\n\t\\\'\[\]]", "").Trim();

        var config = FurnitureLock.INSTANCE.Config;

        PositionConfig = config.Bind(strippedName, "position", "0, 0, 0", "default position of the Furniture piece.\nVector3.zero means vanilla default");
        RotationConfig = config.Bind(strippedName, "rotation", "0, 0, 0", "default rotation of the Furniture piece.\nVector3.zero means vanilla default");
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
                () =>
                {
                    var pos = unlockable.placedPosition;
                    PositionConfig.Value = $"{pos.x}, {pos.y}, {pos.z}";
                    var rot = unlockable.placedRotation;
                    RotationConfig.Value = $"{rot.x}, {rot.y}, {rot.z}";
                    FurnitureLock.PluginConfig.CleanAndSave();
                });
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
            StoredConfig.SettingChanged += (_, _) => OnStoredConfigOnSettingChanged();
        }

        FurnitureLock.Log.LogDebug($"{unlockable.unlockableName} pos: {Position} rot: {Rotation} lock:{Locked} stored:{Stored}");
        return;

        void OnPositionConfigOnSettingChanged()
        {
            var sections = PositionConfig.Value.Split(',');
            if (sections.Length != 3) return;

            var posArray = sections.Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();

            var newPos = new Vector3(posArray[0], posArray[1], posArray[2]);
            
            if (newPos == Vector3.zero)
                newPos = _defaultPosition;

            Position = newPos;
        }

        void OnRotationConfigOnSettingChanged()
        {
            var sections = RotationConfig.Value.Split(',');
            if (sections.Length != 3) return;

            var posArray = sections.Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();

            var newRot = new Vector3(posArray[0], posArray[1], posArray[2]);
            
            if (newRot == Vector3.zero)
                newRot = _defaultRotation;

            Rotation = newRot;
        }

        void OnLockedConfigOnSettingChanged()
        {
            Locked = LockedConfig.Value;
        }
        

        void OnStoredConfigOnSettingChanged()
        {
            Stored = StoredConfig.Value;
        }
    }

    private void FindDefaults()
    {
        var unlockable = Unlockable;
        
        GameObject gameObject = null;
        if (unlockable.spawnPrefab)
            gameObject = unlockable.prefabObject;
        else
        {
            PlaceableShipObject[] objectsOfType = UnityEngine.Object.FindObjectsOfType<PlaceableShipObject>();
            for (int index = 0; index < objectsOfType.Length; ++index)
            {
                if (objectsOfType[index].unlockableID == UnlockableID)
                    gameObject = objectsOfType[index].parentObject.gameObject;
            }
        }
        
        if (gameObject == null)
            throw new NullReferenceException($"Cannot find default GameObject for {unlockable.unlockableName}");

        var shipObject = gameObject.GetComponentInChildren<PlaceableShipObject>();
        
        var parentObjectSecondary = shipObject.parentObjectSecondary;
        var parentObject = shipObject.parentObject;
        if (parentObjectSecondary != null)
        {
            var parentPos = parentObjectSecondary.position;
            var transform = shipObject.mainMesh.transform;
            var position = transform.position;
            _defaultPosition = parentPos -
                               ((parentObjectSecondary.transform.position - position) +
                                (position - shipObject.placeObjectCollider.transform.position));
            var rotation = parentObjectSecondary.rotation;
            var step1 = rotation * Quaternion.Inverse(transform.rotation);
            _defaultRotation = step1.eulerAngles;
        }
        else
        {
            var offset = parentObject.positionOffset;
            var localOffset = StartOfRound.Instance.elevatorTransform.TransformPoint(offset);
            var position = shipObject.mainMesh.transform.position;
            _defaultPosition = localOffset -
                                    (shipObject.parentObject.transform.position - position) -
                                    (position - shipObject.placeObjectCollider.transform.position);
            var rotation = Quaternion.Euler(parentObject.rotationOffset);
            var step1 = rotation * Quaternion.Inverse(parentObject.transform.rotation);
            var step2 = step1 * shipObject.mainMesh.transform.rotation;
            var placementRotation = step2.eulerAngles;
            _defaultRotation = placementRotation;
        }
    }

    public bool Equals(UnlockableConfig other)
    {
        return Equals(Unlockable, other.Unlockable);
    }

    public override bool Equals(object obj)
    {
        return obj is UnlockableConfig other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (Unlockable != null ? Unlockable.GetHashCode() : 0);
    }

    public static bool operator ==(UnlockableConfig o1, UnlockableConfig o2) => o1.Equals(o2);
    public static bool operator !=(UnlockableConfig o1, UnlockableConfig o2) => !o1.Equals(o2);
}