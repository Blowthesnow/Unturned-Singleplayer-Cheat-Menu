using System;
using UnturnedSingleplayerCheatMenu.Services;

static void Check(bool value, string message)
{
    if (!value)
        throw new InvalidOperationException(message);
}

RuntimeSessionState frontend = RuntimeSessionPolicy.Evaluate(
    false, false, false, false, string.Empty, false);
RuntimeSessionState loading = RuntimeSessionPolicy.Evaluate(
    true, false, true, false, "PublicServer", false);
RuntimeSessionState singleplayer = RuntimeSessionPolicy.Evaluate(
    true, true, true, false, "Singleplayer_0", true);
RuntimeSessionState multiplayer = RuntimeSessionPolicy.Evaluate(
    true, false, true, false, "PublicServer", true);
RuntimeSessionState listenServer = RuntimeSessionPolicy.Evaluate(
    true, true, true, false, "PrivateServer", true);
RuntimeSessionState dedicated = RuntimeSessionPolicy.Evaluate(
    true, true, false, true, "DedicatedServer", false);
RuntimeSessionState unknown = RuntimeSessionPolicy.Evaluate(
    true, false, false, false, "UnknownServer", true);

Check(frontend.Kind == RuntimeSessionKind.Frontend &&
      !frontend.CanOpenClientMenu && !frontend.CanUseSnapshotStorage,
    "Frontend must remain unavailable for menu, gameplay, and Snapshot storage.");
Check(loading.Kind == RuntimeSessionKind.Loading &&
      !loading.CanOpenClientMenu && !loading.CanUseSnapshotStorage,
    "Loading must remain unavailable until the local player exists.");
Check(singleplayer.Kind == RuntimeSessionKind.Singleplayer &&
      singleplayer.CanOpenClientMenu && singleplayer.CanUseSnapshotStorage,
    "Singleplayer must retain full supported access.");
Check(multiplayer.Kind == RuntimeSessionKind.MultiplayerClient &&
      multiplayer.CanOpenClientMenu && !multiplayer.CanUseSnapshotStorage,
    "Multiplayer must open the menu without opening Snapshot storage.");
Check(listenServer.Kind == RuntimeSessionKind.ListenServer &&
      listenServer.CanOpenClientMenu && !listenServer.CanUseSnapshotStorage,
    "A non-singleplayer listen host must use the unsupported client boundary.");
Check(dedicated.Kind == RuntimeSessionKind.DedicatedServer &&
      !dedicated.CanOpenClientMenu && !dedicated.CanUseSnapshotStorage,
    "Dedicated server must stay headless and inert.");
Check(unknown.Kind == RuntimeSessionKind.UnknownConnected &&
      !unknown.CanOpenClientMenu && !unknown.CanUseSnapshotStorage,
    "Unknown connected sessions must fail closed.");

Check(GameplayFeatureAccessPolicy.Evaluate(frontend, false) ==
      GameplayFeatureAccess.DeniedNotReady,
    "Frontend gameplay must remain unavailable.");
Check(GameplayFeatureAccessPolicy.Evaluate(loading, false) ==
      GameplayFeatureAccess.DeniedNotReady,
    "Loading gameplay must remain unavailable.");
Check(GameplayFeatureAccessPolicy.Evaluate(singleplayer, false) ==
      GameplayFeatureAccess.AllowedSupportedSingleplayer,
    "Supported singleplayer gameplay must not require acknowledgement.");
Check(GameplayFeatureAccessPolicy.Evaluate(multiplayer, false) ==
      GameplayFeatureAccess.RequiresMultiplayerAcknowledgement,
    "Multiplayer gameplay must wait for per-session acknowledgement.");
Check(GameplayFeatureAccessPolicy.Evaluate(multiplayer, true) ==
      GameplayFeatureAccess.AllowedUnsupportedMultiplayer,
    "Acknowledged multiplayer gameplay must use the unsupported path.");
Check(GameplayFeatureAccessPolicy.Evaluate(listenServer, true) ==
      GameplayFeatureAccess.AllowedUnsupportedMultiplayer,
    "Acknowledged listen-server gameplay must use the unsupported path.");
Check(GameplayFeatureAccessPolicy.Evaluate(dedicated, true) ==
      GameplayFeatureAccess.DeniedDedicatedServer,
    "Dedicated-server gameplay must remain unavailable after acknowledgement.");
Check(GameplayFeatureAccessPolicy.Evaluate(unknown, true) ==
      GameplayFeatureAccess.DeniedUnknownSession,
    "Unknown connected gameplay must remain unavailable after acknowledgement.");

MultiplayerRiskAcknowledgement acknowledgement = new();
Check(!acknowledgement.IsAccepted,
    "Acknowledgement must begin unset.");
Check(acknowledgement.Observe("MultiplayerClient|PublicServer|PEI|1001"),
    "First connection observation must report a session change.");
acknowledgement.AcceptCurrent();
Check(acknowledgement.IsAccepted,
    "Current multiplayer connection must be accepted.");
Check(!acknowledgement.Observe("MultiplayerClient|PublicServer|PEI|1001") &&
      acknowledgement.IsAccepted,
    "Repeated observation of the same connection must retain acknowledgement.");
Check(acknowledgement.Observe("MultiplayerClient|PublicServer|PEI|1002") &&
      !acknowledgement.IsAccepted,
    "A new player instance must reset acknowledgement.");
acknowledgement.AcceptCurrent();
acknowledgement.Reset();
Check(!acknowledgement.IsAccepted,
    "Reset must clear the current acknowledgement.");

Console.WriteLine("Runtime boundary smoke checks passed.");
