# NPC Dialogue Localization Implementation Audit

Date: 2026-08-26

## Implemented and verified

- The scanner discovers supported official and Workshop Dialogue, NPC, Quest, Object, and Vendor assets.
- Dialogue pages and responses use asset-scoped v2 identities containing asset type, GUID, localization key, field, languages, and normalized source text.
- Existing v1 item, vehicle, and item-action cache entries remain compatible.
- Snapshot lookup is generic by asset instance and field, so new text domains can be published without reusing item action fields.
- Cache entries retain schema, asset type, and localization-key metadata for v2 matching.
- The Harmony display-only patches cover dialogue page/response text, NPC character name, quest name/description, object name/dialogue name, vendor name/description, and custom object interact text.
- Vendor buying and selling description overrides are also discovered by `Buying_#_Description` / `Selling_#_Description` and patched through the concrete `displayDesc` getters.
- Object holiday suffixes such as `[HW]` / `[XMAS]` are preserved when the localized object name replaces the base name.
- The object hint patch replaces text only when it equals the scanned custom Interact source. Condition failure text is left unchanged.
- No dialogue response request, quest state/reward, vendor transaction, or network handler is patched.
- Protected NPC dialogue tags, including color tags, name tokens, line breaks, and pauses, are rejected when missing, duplicated, unknown, or reordered.
- Asset-scoped identity isolation is smoke tested for GUID, localization key, field, and asset type.

## Automated evidence

- `dotnet build src/UnturnedSingleplayerCheatMenu/UnturnedSingleplayerCheatMenu.csproj --no-restore`: succeeded with 0 warnings and 0 errors.
- All 12 `*Smoke.csproj` projects under `tests` passed.
- `tests/AssemblyMetadataSmoke` passed against the current `lib/Assembly-CSharp.dll`, including all new getter and raw-field signatures.
- Built DLL was copied to the installed plugin directory while Unturned was not running.
- Source and installed DLL SHA-256: `E11C45AE5F034C6825505568FA6190655FB809156C113ED71BDDD3700294B3E7`.
- Source and installed assembly version: `1.7.0.0`.

## Acceptance still requiring game evidence

- A legal local test map or copy containing multi-page dialogue, multiple responses, an NPC, a quest, a vendor, and a custom object interact prompt.
- Native `zh-CN` asset localization-file verification, including preservation when author-provided Chinese exists.
- Screenshots and logs for dialogue open/page/back/reopen, all response buttons, quest accept/track/complete/abandon, vendor open, and object approach/leave/re-enter.
- Singleplayer, host, and remote-client checks that dialogue choices, quest progress, rewards, vendor state, and network behavior remain unchanged.
- Rollback checks after disabling the feature or clearing the relevant cache.

The remaining items are deliberately not marked complete because build, smoke, and file-hash evidence cannot substitute for in-game UI and multiplayer acceptance.
