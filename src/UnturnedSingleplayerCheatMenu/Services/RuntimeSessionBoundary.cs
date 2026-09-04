using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class RuntimeSessionBoundary
{
	internal static RuntimeSessionState CaptureCurrent()
	{
		return RuntimeSessionPolicy.Evaluate(
			Provider.isConnected,
			Provider.isServer,
			Provider.isClient,
			Dedicator.isStandaloneDedicatedServer,
			Provider.serverID,
			Player.LocalPlayer != null);
	}

	internal static string CaptureConnectionKey(RuntimeSessionState state)
	{
		int playerInstanceId = Player.LocalPlayer == null ? 0 : Player.LocalPlayer.GetInstanceID();
		return state.Kind + "|" + state.ServerId + "|" + (Provider.map ?? string.Empty) + "|" + playerInstanceId;
	}
}
