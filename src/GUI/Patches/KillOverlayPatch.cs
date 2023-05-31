using HarmonyLib;
using Lotus.Logging;
using Lotus.Utilities;

namespace Lotus.GUI.Patches;

[HarmonyPatch(typeof(KillOverlay), nameof(KillOverlay.ShowKillAnimation))]
public class KillOverlayPatch
{
    private static readonly FixedUpdateLock FixedUpdateLock = new(0.5f);

    public static bool Prefix(KillOverlay __instance)
    {
        if (!FixedUpdateLock.AcquireLock()) return false;
        
        DevLogger.Log("Showing Kill Animation");

        return true;
    }
}