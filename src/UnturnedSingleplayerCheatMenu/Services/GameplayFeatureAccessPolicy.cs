using System;

namespace UnturnedSingleplayerCheatMenu.Services;

internal enum GameplayFeatureAccess
{
	DeniedNotReady,
	AllowedSupportedSingleplayer,
	RequiresMultiplayerAcknowledgement,
	AllowedUnsupportedMultiplayer,
	DeniedUnsupportedSession,
	DeniedDedicatedServer,
	DeniedUnknownSession
}

internal static class GameplayFeatureAccessPolicy
{
	internal static GameplayFeatureAccess Evaluate(RuntimeSessionState state, bool acknowledged)
	{
		if (state.Kind == RuntimeSessionKind.DedicatedServer)
			return GameplayFeatureAccess.DeniedDedicatedServer;
		if (state.Kind == RuntimeSessionKind.Frontend || state.Kind == RuntimeSessionKind.Loading)
			return GameplayFeatureAccess.DeniedNotReady;
		if (state.Kind == RuntimeSessionKind.Singleplayer)
			return GameplayFeatureAccess.AllowedSupportedSingleplayer;
		if (state.IsUnsupportedClientSession)
		{
			return acknowledged
				? GameplayFeatureAccess.AllowedUnsupportedMultiplayer
				: GameplayFeatureAccess.RequiresMultiplayerAcknowledgement;
		}
		return GameplayFeatureAccess.DeniedUnknownSession;
	}

	internal static bool IsAllowed(GameplayFeatureAccess access)
	{
		return access == GameplayFeatureAccess.AllowedSupportedSingleplayer ||
			access == GameplayFeatureAccess.AllowedUnsupportedMultiplayer;
	}
}

internal sealed class MultiplayerRiskAcknowledgement
{
	private string _observedConnectionKey = string.Empty;
	private string _acceptedConnectionKey = string.Empty;

	internal bool Observe(string connectionKey)
	{
		string normalized = connectionKey ?? string.Empty;
		if (string.Equals(_observedConnectionKey, normalized, StringComparison.Ordinal))
			return false;
		_observedConnectionKey = normalized;
		_acceptedConnectionKey = string.Empty;
		return true;
	}

	internal void AcceptCurrent()
	{
		_acceptedConnectionKey = _observedConnectionKey;
	}

	internal bool IsAccepted =>
		_observedConnectionKey.Length > 0 &&
		string.Equals(_observedConnectionKey, _acceptedConnectionKey, StringComparison.Ordinal);

	internal void Reset()
	{
		_observedConnectionKey = string.Empty;
		_acceptedConnectionKey = string.Empty;
	}
}
