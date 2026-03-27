using System;
using HarmonyLib;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

namespace CytrixTimeChanger.Patches
{
    /// <summary>
    /// Example patch demonstrating Harmony for VRRig instead of GorillaLocomotion.Player
    /// </summary>
    [HarmonyPatch(typeof(VRRig))]
    [HarmonyPatch("Awake", MethodType.Normal)]
    internal class VRRigPatch
    {
        private static void Postfix(VRRig __instance)
        {
            // Debug example: log the VRRig's head position
            if (__instance.head != null && __instance.head.rigTarget != null)
            {
                Debug.Log($"[VRRigPatch] VRRig Awake fired for actor: {GetActorNumber(__instance)}, head position: {__instance.head.rigTarget.position}");
            }
        }

        private static int GetActorNumber(VRRig rig)
        {
            // Try common fields that store Photon actor number
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

            var field =
                rig.GetType().GetField("photonViewOwnerId", flags) ??
                rig.GetType().GetField("creatorActorNumber", flags) ??
                rig.GetType().GetField("ownerActorNumber", flags);

            return field != null ? (int)field.GetValue(rig) : -1;
        }
    }
}