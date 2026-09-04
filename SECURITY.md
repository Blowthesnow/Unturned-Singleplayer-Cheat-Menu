# Security policy and usage boundary

## Supported version

Security and compatibility reports are currently accepted for the latest published version, `2.0.0`.

## Intended environment

Gameplay compatibility reports are supported only for Unturned singleplayer worlds on Windows x64 Unity Mono with BepInEx 5. The menu may open in a connected multiplayer client after the local player exists, but gameplay use there is unsupported, best-effort, and requires acknowledgement for the current connection. Snapshot Infinite Storage and zombie-region retention remain true-singleplayer-only. A standalone U3DS process is expected to remain inert and create no client UI or gameplay graph.

Workshop translation is a local content pipeline and is not controlled by gameplay authorization. Entering multiplayer, reconnecting, or changing acknowledgement state must not cancel an active translation batch.

Startup behavior and existing launcher helpers are unchanged by this boundary implementation. Successfully starting the game does not establish that BattlEye permits BepInEx, this plugin, or a multiplayer connection.

## Reporting

Please do not publish sensitive logs, Steam credentials, personal paths, private server addresses, or complete save files in a public Issue.

For a reproducible compatibility report, include:

- plugin version;
- Unturned version;
- BepInEx version;
- runtime session kind and whether a local player exists;
- BattlEye launch state;
- whether the BepInEx preloader and chainloader ran;
- whether the plugin was discovered;
- server type (singleplayer, multiplayer client/listen host, or standalone U3DS);
- the smallest relevant excerpt from `BepInEx/LogOutput.log`;
- reproduction steps.

Remove usernames, Steam identifiers, local filesystem paths, private server addresses, credentials, and unrelated save data before posting.
