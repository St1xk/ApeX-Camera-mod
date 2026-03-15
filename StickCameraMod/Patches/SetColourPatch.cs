using System.Collections.Generic;
using StickCameraMod.Utils;
using HarmonyLib;
using UnityEngine;

namespace StickCameraMod.Patches;

[HarmonyPatch(typeof(VRRig), nameof(VRRig.SetColor))]
public static class SetColourPatch
{
    public static List<VRRig> SpawnedRigs = new();

    private static void Postfix(VRRig __instance, Color color)
    {
        if (!SpawnedRigs.Contains(__instance))
        {
            SpawnedRigs.Add(__instance);
            RigUtils.OnRigSpawned?.Invoke(__instance);
        }
        else
        {
            RigUtils.OnRigColourChange?.Invoke(__instance, color);
        }
    }
}