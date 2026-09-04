using System;

namespace UnturnedSingleplayerCheatMenu.Services;

internal enum RuntimeSessionKind
{
	Frontend,
	Loading,
	Singleplayer,
	MultiplayerClient,
	ListenServer,
	DedicatedServer,
	UnknownConnected
}

internal readonly struct RuntimeSessionState
{
	internal RuntimeSessionKind Kind { get; }
	internal string ServerId { get; }
	internal bool HasLocalPlayer { get; }

	internal RuntimeSessionState(RuntimeSessionKind kind, string serverId, bool hasLocalPlayer)
	{
		Kind = kind;
		ServerId = serverId ?? string.Empty;
		HasLocalPlayer = hasLocalPlayer;
	}

	internal bool IsSupportedGameplay => Kind == RuntimeSessionKind.Singleplayer;

	internal bool IsUnsupportedClientSession =>
		Kind == RuntimeSessionKind.MultiplayerClient || Kind == RuntimeSessionKind.ListenServer;

	internal bool CanOpenClientMenu =>
		HasLocalPlayer && (Kind == RuntimeSessionKind.Singleplayer || IsUnsupportedClientSession);

	internal bool CanUseSnapshotStorage => Kind == RuntimeSessionKind.Singleplayer && HasLocalPlayer;
}

internal static class RuntimeSessionPolicy
{
	private const string SingleplayerServerIdPrefix = "Singleplayer_";

	internal static RuntimeSessionState Evaluate(
		bool isConnected,
		bool isServer,
		bool isClient,
		bool isStandaloneDedicatedServer,
		string serverId,
		bool hasLocalPlayer)
	{
		if (isStandaloneDedicatedServer)
			return new RuntimeSessionState(RuntimeSessionKind.DedicatedServer, serverId, false);
		if (!isConnected)
			return new RuntimeSessionState(RuntimeSessionKind.Frontend, serverId, false);
		if (!hasLocalPlayer)
			return new RuntimeSessionState(RuntimeSessionKind.Loading, serverId, false);
		if (isServer && isClient && !string.IsNullOrEmpty(serverId) &&
			serverId.StartsWith(SingleplayerServerIdPrefix, StringComparison.Ordinal))
		{
			return new RuntimeSessionState(RuntimeSessionKind.Singleplayer, serverId, true);
		}
		if (isServer && isClient)
			return new RuntimeSessionState(RuntimeSessionKind.ListenServer, serverId, true);
		if (isClient)
			return new RuntimeSessionState(RuntimeSessionKind.MultiplayerClient, serverId, true);
		return new RuntimeSessionState(RuntimeSessionKind.UnknownConnected, serverId, true);
	}
}
