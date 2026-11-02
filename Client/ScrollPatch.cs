using EFT.Hideout;
using EFT.InputSystem;
using HarmonyLib;
using static EFT.InputSystem.InputNode;

namespace CultistCircle.Client;

/// <summary>The hideout zooms on the same wheel input the window scrolls with, so zoom is held off while the window is open.</summary>
[HarmonyPatch(typeof(HideoutScreenOverlay), nameof(HideoutScreenOverlay.TranslateCommand))]
internal static class ScrollPatch
{
    internal static bool Blocked;

    [HarmonyPrefix]
    private static bool Prefix(ECommand command, ref ETranslateResult __result)
    {
        if (Blocked && command is ECommand.ScrollPrevious or ECommand.ScrollNext)
        {
            __result = ETranslateResult.Block;
            return false;
        }

        return true;
    }
}
