using HarmonyLib;

namespace StickCameraMod.Patches;

[HarmonyPatch(typeof(VRRig), nameof(VRRig.PostTick))]
public static class PCHeadMovementPatch
{
    public static bool IsEnabled;

    private static void Postfix(VRRig __instance)
    {
        if (!IsEnabled || !__instance.isLocal)
            return;

        __instance.head.rigTarget.rotation = GorillaTagger.Instance.headCollider.transform.rotation;
    }
}