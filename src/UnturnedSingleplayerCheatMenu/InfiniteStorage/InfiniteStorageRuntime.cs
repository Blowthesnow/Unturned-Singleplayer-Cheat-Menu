using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage;

internal sealed class InfiniteStorageRuntime : IDisposable
{
	private readonly ManualLogSource _log;
	private readonly VirtualStorageService _storage = new VirtualStorageService();
	private readonly InventoryBridge _bridge;
	private VirtualStoragePersistence _persistence;
	private string _worldKey = string.Empty;
	private string _playerKey = string.Empty;
	private bool _loaded;
	private bool _dirty;
	private readonly AmmoCompatibilityIndex _ammoCompatibilityIndex = new AmmoCompatibilityIndex();

	internal bool Enabled { get; set; }
	internal InfiniteStorageRuntime(ManualLogSource log) { _log = log; _bridge = new InventoryBridge(_storage); }
	internal VirtualStorageService Storage => _storage;
	internal IReadOnlyList<VirtualStorageEntry> Entries => _storage.Entries;
	internal IReadOnlyList<LegacyAmbiguousEntry> LegacyAmbiguousEntries => _storage.LegacyAmbiguousEntries;
	internal bool IsOperational => Enabled && _loaded;

	internal void Pump()
	{
		if (!Enabled)
		{
			if (_loaded && !Flush()) return;
			_loaded = false;
			return;
		}
		if (!SingleplayerGuard.IsReady || Player.LocalPlayer == null || Level.info == null)
		{
			if (_loaded)
			{
				if (!Flush()) return;
				_loaded = false; _worldKey = string.Empty; _playerKey = string.Empty;
			}
			return;
		}
		string world = Level.info.path ?? Level.info.name ?? string.Empty;
		object playerId = Player.LocalPlayer.channel?.owner?.playerID;
		object steamId = playerId?.GetType().GetProperty("steamID")?.GetValue(playerId, null);
		string player = steamId?.ToString() ?? "unknown-player";
		if (_loaded && string.Equals(world, _worldKey, StringComparison.Ordinal) && string.Equals(player, _playerKey, StringComparison.Ordinal)) return;
		if (_loaded && !Flush()) return;
		_worldKey = world; _playerKey = player;
		string path = Path.Combine(Paths.ConfigPath, "UnturnedSingleplayerCheatMenu", "InfiniteStorage", Hash(world), Hash(player) + ".json");
		_persistence = new VirtualStoragePersistence(path);
		if (!_persistence.TryLoad(out VirtualStorageDocument document, out string error))
		{
			_log?.LogError("InfiniteStorage load failed: " + error);
			_storage.Load(new VirtualStorageDocument()); _loaded = false; _dirty = false; return;
		}
		RefreshAmmoContainerClassification();
		if ((!string.IsNullOrWhiteSpace(document.WorldKey) && !string.Equals(document.WorldKey, world, StringComparison.Ordinal)) ||
			(!string.IsNullOrWhiteSpace(document.PlayerKey) && !string.Equals(document.PlayerKey, player, StringComparison.Ordinal)))
		{
			_log?.LogError("InfiniteStorage identity mismatch; refusing to load the document.");
			_storage.Load(new VirtualStorageDocument()); _loaded = false; return;
		}
		document.WorldKey = world; document.PlayerKey = player;
		int previousSchema = document.SchemaVersion;
		_storage.Load(document, ResolveAsset);
		_loaded = true;
		_dirty = previousSchema != VirtualStorageDocument.CurrentSchemaVersion;
		if (LegacyAmbiguousEntries.Count > 0)
		{
			_dirty = true;
			_log?.LogWarning("InfiniteStorage isolated " + LegacyAmbiguousEntries.Count + " ambiguous legacy entries. They are retained in LegacyAmbiguousEntries in the save file.");
		}
		if (_dirty) Flush();
	}

	internal bool Flush()
	{
		if (!_loaded || _persistence == null || !_dirty) return true;
		if (_persistence.TrySave(_storage.Snapshot(_worldKey, _playerKey), out string error)) { _dirty = false; return true; }
		_log?.LogError("InfiniteStorage save failed: " + error);
		return false;
	}

	internal bool StoreCandidate(Item item, ItemAsset asset = null) => StoreItem(item, asset);
	internal bool StoreProduced(Item item, ItemAsset asset = null) => StoreItem(item, asset);

	internal bool RollbackCandidate(Item item, ItemAsset asset = null)
	{
		if (!_loaded || item == null) return false;
		asset ??= ResolveAsset(item.id);
		if (asset == null) return false;
		ItemStorageDescriptor descriptor = ItemInstanceSemantics.Describe(asset, item);
		return RemoveDescriptor(descriptor, UnitsFor(descriptor));
	}

	internal bool RemoveVirtual(string entryId, long units)
	{
		if (!IsOperational) return false;
		VirtualStorageTransaction transaction = new VirtualStorageTransaction(_storage, _worldKey, _playerKey);
		if (!transaction.Remove(entryId, units)) return false;
		return Commit(transaction);
	}

	internal bool ConsumeVirtualTransaction(IReadOnlyList<VirtualStorageMaterialRequirement> requirements)
	{
		if (!IsOperational || requirements == null || requirements.Count == 0) return false;
		VirtualStorageTransaction transaction = new VirtualStorageTransaction(_storage, _worldKey, _playerKey);
		for (int i = 0; i < requirements.Count; i++)
		{
			VirtualStorageMaterialRequirement requirement = requirements[i];
			if (requirement?.Item == null || requirement.Asset == null || requirement.Units <= 0 ||
				!transaction.Remove(ItemInstanceSemantics.Describe(requirement.Asset, requirement.Item), requirement.Units))
			{
				transaction.Rollback(); return false;
			}
		}
		return Commit(transaction);
	}

	internal void AbortCrafting(VirtualStorageDocument before)
	{
		if (!IsOperational || before == null) return;
		_storage.RestoreSnapshot(before); _dirty = true; Flush();
	}

	internal long Withdraw(string entryId, long requested)
	{
		if (!IsOperational || requested <= 0 || Player.LocalPlayer?.inventory == null) return 0;
		VirtualStorageEntry entry = Entries.FirstOrDefault(value => string.Equals(value.EntryId, entryId, StringComparison.Ordinal));
		if (entry == null) return 0;
		IReadOnlyList<Item> items = ItemInstanceSemantics.CreateWithdrawItems(entry, requested);
		if (items.Count == 0) return 0;
		VirtualStorageDocument before = _storage.Snapshot(_worldKey, _playerKey);
		IReadOnlyList<InventoryBridge.CapturedItem> inventoryBefore = InventoryBridge.Capture(Player.LocalPlayer.inventory);
		long moved = 0;
		foreach (Item item in items)
		{
			if (!Player.LocalPlayer.inventory.tryAddItem(item, true)) break;
			moved += entry.UnitKind == StackUnitKind.ResourceUnits ? item.amount : 1;
		}
		if (moved == 0) return 0;
		if (!_storage.Remove(entry.EntryId, moved).Success)
		{
			InventoryBridge.Restore(Player.LocalPlayer.inventory, inventoryBefore); return 0;
		}
		_dirty = true;
		if (!Flush())
		{
			InventoryBridge.Restore(Player.LocalPlayer.inventory, inventoryBefore);
			_storage.RestoreSnapshot(before); _dirty = true; Flush();
			return 0;
		}
		return moved;
	}

	internal bool CanDecompose(string entryId)
	{
		if (!IsOperational || string.IsNullOrWhiteSpace(entryId)) return false;
		VirtualStorageEntry entry = Entries.FirstOrDefault(value => string.Equals(value.EntryId, entryId, StringComparison.Ordinal));
		if (entry == null) return false;
		return InfiniteStorageDecomposer.CanDecompose(entry, ResolveAsset(entry.AssetId));
	}

	internal InfiniteStorageDecomposeResult Decompose(string entryId, InfiniteStorageDecomposeScope scope)
	{
		if (!IsOperational)
		{
			return InfiniteStorageDecomposeResult.Failed(InfiniteStorageDecomposeFailure.StorageUnavailable);
		}
		try
		{
			return InfiniteStorageDecomposer.Execute(_storage, entryId, scope, ResolveAsset, Commit);
		}
		catch (Exception ex)
		{
			_log?.LogError("InfiniteStorage decomposition failed:\n" + ex);
			return InfiniteStorageDecomposeResult.Failed(InfiniteStorageDecomposeFailure.UnexpectedFailure);
		}
	}

	internal int StoreAllCarried()
	{
		if (!Enabled || !_loaded || Player.LocalPlayer?.inventory == null) return 0;
		VirtualStorageDocument before = _storage.Snapshot(_worldKey, _playerKey);
		return _bridge.StoreAllCarried(Player.LocalPlayer.inventory, delegate { _dirty = true; return Flush(); }, before);
	}

	internal bool TryExportLegacyAmbiguousEntries(out string exportPath, out string error)
	{
		exportPath = string.Empty;
		error = string.Empty;
		if (!_loaded || _persistence == null)
		{
			error = "Infinite storage is not loaded.";
			return false;
		}
		if (!_persistence.TryExportLegacyAmbiguousEntries(LegacyAmbiguousEntries, out exportPath, out error))
		{
			_log?.LogError("InfiniteStorage ambiguous legacy export failed: " + error);
			return false;
		}
		_log?.LogInfo("InfiniteStorage exported ambiguous legacy entries to " + exportPath);
		return true;
	}

	public void Dispose() => Flush();

	private bool StoreItem(Item item, ItemAsset asset)
	{
		if (item == null || !IsOperational) return false;
		asset ??= ResolveAsset(item.id);
		if (asset == null) return false;
		VirtualStorageTransaction transaction = new VirtualStorageTransaction(_storage, _worldKey, _playerKey);
		if (!transaction.Add(ItemInstanceSemantics.Describe(asset, item))) return false;
		return Commit(transaction);
	}

	private bool RemoveDescriptor(ItemStorageDescriptor descriptor, long units)
	{
		VirtualStorageTransaction transaction = new VirtualStorageTransaction(_storage, _worldKey, _playerKey);
		if (!transaction.Remove(descriptor, units)) return false;
		return Commit(transaction);
	}

	private bool Commit(VirtualStorageTransaction transaction)
	{
		bool previousDirty = _dirty;
		_dirty = true;
		if (transaction.Commit(Flush)) return true;
		_dirty = previousDirty;
		return false;
	}

	private static long UnitsFor(ItemStorageDescriptor descriptor) => descriptor.UnitKind == StackUnitKind.ResourceUnits ? descriptor.NativeAmount : 1;
	private static ItemAsset ResolveAsset(ushort assetId) => Assets.find(EAssetType.ITEM, assetId) as ItemAsset;
	private void RefreshAmmoContainerClassification()
	{
		List<ItemAsset> items = new List<ItemAsset>();
		Assets.find(items);
		_ammoCompatibilityIndex.Rebuild(items);
	}
	private static string Hash(string value) { using SHA256 sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty))).Replace("-", string.Empty).ToLowerInvariant(); }
}
