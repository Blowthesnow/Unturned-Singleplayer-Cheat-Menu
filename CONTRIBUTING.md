# Contributing

Contributions that preserve the singleplayer-only boundary are welcome.

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
- New game actions must remain guarded by the same true-singleplayer checks.
- Do not add anti-cheat bypasses, multiplayer support, server abuse features, credential collection, telemetry, or hidden network calls.
- Persistence changes should include a round-trip test and explicit behavior for write failures.
- UI changes should be tested for cursor behavior, character-input isolation, reopening, and scene changes.

## Compatibility evidence

Compilation alone is not enough for runtime-sensitive changes. Clearly separate:

- source/API evidence;
- build and automated test evidence;
- actual singleplayer runtime evidence;
- anything that remains unverified.
