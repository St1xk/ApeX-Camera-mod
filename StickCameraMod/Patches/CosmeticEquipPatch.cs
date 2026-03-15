using StickCameraMod.Utils;
using HarmonyLib;

namespace StickCameraMod.Patches;

[HarmonyPatch(typeof(VRRig), nameof(VRRig.SetCosmeticsActive))]
public class CosmeticEquipPatch
{
    private static void Postfix(VRRig __instance) => RigUtils.OnRigCosmeticsChange?.Invoke(__instance);
}