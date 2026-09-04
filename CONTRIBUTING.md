# Contributing

Contributions that preserve the singleplayer gameplay-support boundary and the independent local Workshop-translation lifecycle are welcome.

## Development setup

1. Install Unturned and BepInEx 5.4.23.5 x64 legally.
2. Prepare local references:

   ```powershell
   .\scripts\Prepare-References.ps1 -GameRoot 'C:\path\to\Unturned'
   ```

3. Build and test:

   ```powershell
   dotnet build .\UnturnedSingleplayerCheatMenu.slnx -c Release
   dotnet run --project .\tests\FavoritesSerializationSmoke\FavoritesSerializationSmoke.csproj -c Release
   dotnet run --project .\tests\TeleportSerializationSmoke\TeleportSerializationSmoke.csproj -c Release
   ```

## Pull requests

- Keep changes focused and explain the player-visible impact.
- Do not commit files from `lib/`, `vendor/`, `artifacts/`, `dist/`, `backups/`, game directories, logs, saves, or personal configuration.
- New game actions must use the shared gameplay-authorization boundary. Multiplayer remains unsupported best-effort, and Snapshot plus zombie-region retention must remain true-singleplayer-only.
- Do not apply the gameplay gate to Workshop scanning, caching, translation requests, progress, or local text application.
- Do not add anti-cheat bypasses, custom multiplayer protocol/RPC adaptation, server abuse features, credential collection, telemetry, or hidden network calls.
- Persistence changes should include a round-trip test and explicit behavior for write failures.
- UI changes should be tested for cursor behavior, character-input isolation, reopening, and scene changes.

## Compatibility evidence

Compilation alone is not enough for runtime-sensitive changes. Clearly separate:

- source/API evidence;
- build and automated test evidence;
- deployment hash and loader/plugin-discovery evidence;
- BattlEye launch and multiplayer connection evidence;
- Workshop translation continuity across connection changes;
- actual singleplayer runtime evidence;
- anything that remains unverified.

Never describe multiplayer/BattlEye Smoke or launcher success as gameplay compatibility. Dedicated-server claims require an actual disposable U3DS run; otherwise report them as not run.
