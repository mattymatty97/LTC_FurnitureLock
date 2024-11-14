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
            // Invert Rotation
            // Original: finalRotation = (Euler(placementRotation) * Inverse(mainMeshRotation)) * initialRotation

            var finalRotation = placeableObject.parentObjectSecondary.transform.rotation;
            var initialRotation = placeableObject.parentObjectSecondary.transform.rotation;
            var mainMeshRotation = placeableObject.mainMesh.transform.rotation;

            // First get the quaternion by multiplying by inverse of initialRotation
            var quaternion = finalRotation * Quaternion.Inverse(initialRotation);

            // Then solve for placementRotation
            config.DefaultRotation = (quaternion * mainMeshRotation).eulerAngles;
            
            // Invert Position
            // Original: finalPosition = placementPosition + (parentPos - mainMeshPos) + (mainMeshPos - colliderPos)

            var finalPosition = placeableObject.parentObjectSecondary.position;
            var offset1 = (placeableObject.parentObjectSecondary.transform.position - placeableObject.mainMesh.transform.position);
            var offset2 = (placeableObject.mainMesh.transform.position - placeableObject.placeObjectCollider.transform.position);

            config.DefaultPosition = finalPosition - offset1 - offset2;
        }
        else
        {
            // Calculate rotation
            // Original: finalRotation = (Euler(placementRotation) * Inverse(mainMeshRotation)) * initialRotation
            // We also have rotationOffset = finalRotation.eulerAngles

            var finalRotation = Quaternion.Euler(placeableObject.parentObject.rotationOffset);
            var initialRotation = placeableObject.parentObject.transform.rotation;
            var mainMeshRotation = placeableObject.mainMesh.transform.rotation;

            // First get the quaternion
            var quaternion = finalRotation * Quaternion.Inverse(initialRotation);

            // Then solve for placementRotation
            config.DefaultRotation = (quaternion * mainMeshRotation).eulerAngles;

            // Calculate position
            // Original: positionOffset = elevatorTransform.InverseTransformPoint(placementPosition + offset1 + offset2)
            // Therefore: placementPosition = elevatorTransform.TransformPoint(positionOffset) - offset1 - offset2

            var offset1 = (placeableObject.parentObject.transform.position - placeableObject.mainMesh.transform.position);
            var offset2 = (placeableObject.mainMesh.transform.position - placeableObject.placeObjectCollider.transform.position);

            // Transform the offset back to world space and solve for placementPosition
            config.DefaultPosition = StartOfRound.Instance.elevatorTransform.TransformPoint(placeableObject.parentObject.positionOffset) - offset1 - offset2;
        }

        config.DefaultsInitialized = true;
        
        FurnitureLock.Log.LogDebug($"{config} defaults are Pos:{config.DefaultPosition} Rot:{config.DefaultRotation}");
    }
    
}