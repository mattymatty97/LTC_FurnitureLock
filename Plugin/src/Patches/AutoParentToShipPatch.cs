using FurnitureLock.Config;
using HarmonyLib;
using UnityEngine;

namespace FurnitureLock.Patches;

[HarmonyPatch]
internal class AutoParentToShipPatch
{

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AutoParentToShip),nameof(AutoParentToShip.Awake))]
    private static void AfterAwake(AutoParentToShip __instance)
    {
        var placeableObject = __instance.GetComponentInChildren<PlaceableShipObject>();
        if (!placeableObject)
            return;
        
        var unlockable = StartOfRound.Instance.unlockablesList.unlockables[placeableObject.unlockableID];

        if (!FurnitureLock.PluginConfig.UnlockableConfigs.TryGetValue(unlockable, out var config))
        {
            config = new UnlockableConfig(unlockable, placeableObject.unlockableID);
            FurnitureLock.PluginConfig.UnlockableConfigs.Add(unlockable,config);
        }
        
        if (placeableObject.parentObjectSecondary)
        {
            // Invert Position
            config.DefaultPosition = placeableObject.parentObject.startingPosition
                                                - (placeableObject.parentObjectSecondary.transform.position - placeableObject.mainMesh.transform.position)
                                                - (placeableObject.mainMesh.transform.position - placeableObject.placeObjectCollider.transform.position);

            // Invert Rotation
            var invertedFinalRotation = Quaternion.Inverse(Quaternion.Euler(placeableObject.parentObject.startingRotation));
            var invertedQuaternion = invertedFinalRotation * placeableObject.parentObjectSecondary.transform.rotation;
            config.DefaultRotation = (Quaternion.Inverse(placeableObject.mainMesh.transform.rotation) * invertedQuaternion).eulerAngles;
        }
        else
        {
            // Calculate rotation
            var transformedRotation = Quaternion.Euler(placeableObject.parentObject.rotationOffset);
            var placementRotationQuaternion = Quaternion.Inverse(placeableObject.parentObject.transform.rotation) * transformedRotation * placeableObject.mainMesh.transform.rotation;
            config.DefaultRotation = placementRotationQuaternion.eulerAngles;

            // Calculate position
            var inversePositionOffset = StartOfRound.Instance.elevatorTransform.TransformPoint(placeableObject.parentObject.positionOffset);
            config.DefaultPosition = inversePositionOffset - 
                                     (placeableObject.parentObject.transform.position - placeableObject.mainMesh.transform.position) - 
                                     (placeableObject.mainMesh.transform.position - placeableObject.placeObjectCollider.transform.position);
        }

        config.DefaultsInitialized = true;
        
        FurnitureLock.Log.LogDebug($"{config} defaults are Pos:{config.DefaultPosition} Rot:{config.DefaultRotation}");
    }
    
}