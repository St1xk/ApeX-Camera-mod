using StickCameraMod.Utils;
using HarmonyLib;

namespace StickCameraMod.Patches;

[HarmonyPatch(typeof(VRRig))]
internal static class OnCosmeticsLoadedPatch
{
    [HarmonyPatch("IUserCosmeticsCallback.OnGetUserCosmetics")]
    [HarmonyPostfix]
    private static void OnGetRigCosmetics(VRRig __instance) =>
            RigUtils.OnRigCosmeticsLoad?.Invoke(__instance);
}