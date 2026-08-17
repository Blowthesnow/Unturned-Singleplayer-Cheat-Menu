# Security policy and usage boundary

## Supported version

Security and compatibility reports are currently accepted for the latest published version, `1.6.0`.

## Intended environment

This plugin is intentionally limited to:

- Unturned singleplayer worlds;
- Windows x64 Unity Mono;
- BepInEx 5;
- a launch where BattlEye is not active.

It is not a multiplayer cheat, does not provide anti-cheat bypass functionality, and should not be loaded into BattlEye-protected or multiplayer sessions.

The included launcher helper only opens Steam's launch-option dialog. It does not stop, patch, disable, or reconfigure BattlEye. If a BattlEye process or service is detected, the helper refuses to continue.

## Reporting

Please do not publish sensitive logs, Steam credentials, personal paths, private server addresses, or complete save files in a public Issue.

For a reproducible compatibility report, include:

- plugin version;
- Unturned version;
- BepInEx version;
- whether the issue occurs before or after entering a singleplayer world;
- the smallest relevant excerpt from `BepInEx/LogOutput.log`;
- reproduction steps.

Remove usernames and local filesystem paths before posting.
