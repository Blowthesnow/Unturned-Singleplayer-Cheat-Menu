# Backpack / Snapshot ammo refill repair — 2026-09-05

## Reproduction and root cause

The reported native recipe is `CN_Guerrilla_Low_60` (60044, 60 rounds) ->
`CN_Foray_20` (60080, currently 16/20). Expected result: box 56, magazine 20.

Read-only inspection of Workshop 3560661136 confirmed:

- 60080 directly accepts 119, 61080, 60080, 60044, 60045, and 62080.
- 60044 directly accepts 60044 and 60045.
- 60045 is `CN_Guerrilla_Low_200`, a 200-round Supply.

The shared allocator classified every persistent container with both incoming
and outgoing cross-asset recipes as an unsafe bridge. This rejected 60044 even
though 60080 explicitly accepts both 60044 and 60045. The backpack's independent
transition validator repeated the same blanket prohibition.

The native recipe semantics were checked against the current local
`lib/Assembly-CSharp.dll` (SHA-256
`A110F5FE471422254C594E29F90FC4058D04D357EB3AE9CB4D3EDA0FFEC33F10`):
`ItemAsset.PopulateBlueprintsLegacy` maps Ammo to FillTargetItem, counts supplies
by TotalAmount, and creates the owner as the target. Official reference:
[Blueprints](https://docs.smartlydressedgames.com/en/stable/items/blueprints.html).
The SDK graph returned no matching implementation text, and the plugin graph
returned a retired file path; neither stale/empty graph result was used as proof
of current behavior. Current source and local assembly checks took precedence.

## Repair

`AmmoRefillAssetRules.CanDonateWithoutIndirectConversionTo` checks each
donor/receiver pair. The receiver must directly accept the donor AND every
cross-asset input type accepted by that donor. No reverse or transitive edge is
invented. A different unsafe receiver no longer disables a safe receiver.

Both the shared allocator (used by backpack and Snapshot refill) and the
backpack transition validator apply this rule. Frozen source budgets, exact
container identities, quality/state preservation, capacity checks, and Snapshot
transactions are unchanged. A -> B -> C remains blocked if C lacks direct
compatibility with A, including repeated-button attempts.

## Verification

- Both screenshot regression tests failed against the original production code.
- Both pass after the repair: 60 + 16 -> 56 + 20, with container count,
  quality, state bytes, and all 76 rounds preserved.
- Added repeated-click, input-order, mixed safe/unsafe receiver, and forged
  ledger checks. Existing 20,000 seeded allocator idempotence cases pass.
- Release build: 0 warnings, 0 errors. All 27 recursively discovered Smoke
  projects rebuilt and passed. `git diff --check` passed.
- Candidate: `D:\Unturned BepInEx\artifacts\UnturnedSingleplayerCheatMenu.dll`,
  SHA-256 `CA6EAB895464A8F2DE595F471C59EB8F6C8FBDCF71DF9E5E1FB7FB4E03411185`.
- Evidence logs and the full-suite runner are under
  `D:\Unturned BepInEx\.codex-build\ammo-refill-diagnosis`.
- Test-authoring errors (constructor parameter order and array Reverse return
  type) were corrected before accepting results. A full-suite source-shape
  assertion still referenced a removed blanket-bridge variable; it was updated
  to require the direct-edge and donor/receiver checks instead.

## Scope and acceptance boundary

Only two production files changed. Test edits are limited to the two ammo suites
and the corresponding source-shape assertions in UiArchitectureSmoke; existing
Minimap work is preserved. No game DLL was deployed; no game configuration,
Workshop asset, legacy storage file, or Snapshot save was modified. No commit,
push, tag, or release was created.

The built DLL is a candidate containing the current checkout, including existing
unrelated changes. Automated tests are not live-game acceptance. After separately
approved deployment, verify in singleplayer with BattlEye disabled:

1. Backpack: enable magazine refill, arrange a 60-round box and 16/20 magazine;
   check 56/60 + 20/20 and a stable second click.
2. Infinite Storage: store the same pair, run ammo refill, withdraw and verify
   exact amounts, quality, and state; save/restart and check again.
3. Also check empty magazines, incompatible ammo, protected/hotkey-bound items,
   and an inventory without enough space for rearrangement.
