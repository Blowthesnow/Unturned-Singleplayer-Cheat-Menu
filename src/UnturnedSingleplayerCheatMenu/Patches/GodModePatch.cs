using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.Patches;

internal static class GodModePatch
{
    public static bool Prefix(PlayerLife __instance, ref EPlayerKill kill)
    {
        CheatMenuPlugin plugin = CheatMenuPlugin.Instance;
        if (plugin == null || !plugin.Actions.GodModeEnabled || !SingleplayerGuard.IsSingleplayerWorld)
            return true;
        if (__instance != Player.LocalPlayer?.life)
            return true;
        kill = EPlayerKill.NONE;
        return false;
    }
}
