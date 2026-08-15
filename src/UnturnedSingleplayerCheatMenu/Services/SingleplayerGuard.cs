using System;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class SingleplayerGuard
{
    private const string ServerIdPrefix = "Singleplayer_";

    public static bool IsSingleplayerWorld
    {
        get
        {
            return Provider.isConnected
                && Provider.isServer
                && Provider.isClient
                && Provider.serverID != null
                && Provider.serverID.StartsWith(ServerIdPrefix, StringComparison.Ordinal);
        }
    }

    public static bool IsReady => IsSingleplayerWorld && Player.LocalPlayer != null;

    public static string RejectionReason
    {
        get
        {
            if (!Provider.isConnected)
                return "尚未进入世界。";
            if (!IsSingleplayerWorld)
                return "此菜单只允许在真正的单人世界中使用。";
            if (Player.LocalPlayer == null)
                return "本地玩家尚未生成。";
            return string.Empty;
        }
    }
}
