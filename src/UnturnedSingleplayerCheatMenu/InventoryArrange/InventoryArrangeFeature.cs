using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.ItemStacking;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal sealed class InventoryArrangeFeature
{
	private sealed class CapturedInventory
	{
		internal List<InventoryArrangeContainer> Containers = new List<InventoryArrangeContainer>();

		internal Dictionary<string, Item> SourceItems = new Dictionary<string, Item>(StringComparer.Ordinal);

		internal List<HotkeyBinding> HotkeyBindings = new List<HotkeyBinding>();

		internal int UnsupportedItemCount;

		internal string LayoutFingerprint;

		internal string Fingerprint;
	}

	private sealed class HotkeyBinding
	{
		internal HotkeyInfo Hotkey;

		internal string ItemKey;

		internal byte OriginalPage;

		internal byte OriginalX;

		internal byte OriginalY;
	}

	private sealed class QuickTransferState
	{
		internal byte SourcePage;

		internal int AssetId;

		internal string AssetGuid;

		internal byte LastPage;

		internal byte LastX;

		internal byte LastY;

		internal string LastItemFingerprint;

		internal int WaitFrames;

		internal int MovedCount;
	}

	private readonly ManualLogSource _log;

	private readonly Func<InventoryArrangeSettings> _settings;
	private readonly Func<bool> _canExecuteGameplay;
	private readonly Func<string> _connectionKey;

	private readonly InventoryArrangePlanner _planner = new InventoryArrangePlanner();

	private bool _isRunning;

	private CapturedInventory _journalBefore;

	private string _journalAfterLayoutFingerprint;

	private string _journalAfterFingerprint;

	private List<byte> _journalPages;

	private InventoryArrangeScope _journalScope;
	private string _journalConnectionKey;

	private bool _hasLoggedPlayerReady;

	private QuickTransferState _quickTransfer;

	internal InventoryArrangeResult LastResult { get; private set; }

	internal InventoryArrangeFeature(ManualLogSource log, Func<InventoryArrangeSettings> settings,
		Func<bool> canExecuteGameplay, Func<string> connectionKey)
	{
		_log = log;
		_settings = settings;
		_canExecuteGameplay = canExecuteGameplay ?? throw new ArgumentNullException(nameof(canExecuteGameplay));
		_connectionKey = connectionKey ?? throw new ArgumentNullException(nameof(connectionKey));
	}

	internal void OnPlayerInitialized()
	{
		if (!_hasLoggedPlayerReady && !(Player.LocalPlayer == null))
		{
			_hasLoggedPlayerReady = true;
			_log?.LogInfo("已检测到本地玩家初始化；背包整理快捷键和菜单入口可用。");
		}
	}

	internal InventoryArrangeResult Preview(InventoryArrangeScope scope, InventoryArrangeAction action)
	{
		if (!TryCapture(scope, requireGameplayAccess: false, out var captured, out var reason))
		{
			return Publish(success: false, reason, null);
		}
		InventoryArrangeSettings inventoryArrangeSettings = _settings?.Invoke() ?? new InventoryArrangeSettings();
		inventoryArrangeSettings.Action = action;
		InventoryArrangePlan inventoryArrangePlan = _planner.CreatePlan(captured.Containers, inventoryArrangeSettings);
		return Publish(inventoryArrangePlan.IsSuccess, inventoryArrangePlan.IsSuccess ? Describe(inventoryArrangePlan, "预览") : inventoryArrangePlan.FailureReason, inventoryArrangePlan);
	}

	internal InventoryArrangeResult Execute(InventoryArrangeScope scope, InventoryArrangeAction action, string source)
	{
		if (_isRunning)
		{
			return Publish(success: false, "整理正在执行，已忽略重复请求。", null);
		}
		if (!TryCapture(scope, requireGameplayAccess: true, out var captured, out var reason))
		{
			return Publish(success: false, reason, null);
		}
		InventoryArrangeSettings inventoryArrangeSettings = _settings?.Invoke() ?? new InventoryArrangeSettings();
		inventoryArrangeSettings.Action = action;
		InventoryArrangePlan inventoryArrangePlan = _planner.CreatePlan(captured.Containers, inventoryArrangeSettings);
		if (!inventoryArrangePlan.IsSuccess)
		{
			return Publish(success: false, inventoryArrangePlan.FailureReason, inventoryArrangePlan);
		}
		if (!InventoryArrangeExecutionPolicy.RequiresInventoryRebuild(inventoryArrangePlan, key =>
			captured.SourceItems.TryGetValue(key, out Item original) ? original.amount : -1))
		{
			_log?.LogInfo("背包整理跳过原生库存重建：布局与物品载荷均未变化。");
			return Publish(success: true, Describe(inventoryArrangePlan, "已整理"), inventoryArrangePlan);
		}
		PlayerInventory inventory = Player.LocalPlayer.inventory;
		bool flag = false;
		_isRunning = true;
		try
		{
			if (!ApplyPlan(inventory, captured, inventoryArrangePlan, out reason))
			{
				string text = (Restore(inventory, captured, out var reason2)
					? ("整理未完成，已恢复执行前库存：" + PluginLocalization.Translate(reason))
					: ("整理失败且恢复失败；请停止继续操作并查看 BepInEx 日志。原因：" + PluginLocalization.Translate(reason) + "；恢复：" + PluginLocalization.Translate(reason2)));
				_log?.LogError(text);
				return Publish(success: false, text, inventoryArrangePlan);
			}
			flag = true;
			CapturedInventory capturedInventory = Capture(inventory, GetPages(inventory, scope, inventoryArrangeSettings), inventoryArrangeSettings);
			string expectedFingerprint = InventoryStackingSemantics.BuildConservationFingerprint(
				inventoryArrangePlan.Placements.Select(value => value.Item));
			if (!string.Equals(expectedFingerprint, capturedInventory.Fingerprint, StringComparison.Ordinal))
			{
				string text2 = (Restore(inventory, captured, out var reason3)
					? "整理后校验发现物品指纹变化，已恢复执行前库存。"
					: ("整理后校验失败且恢复失败；请停止继续操作并查看日志。恢复：" + PluginLocalization.Translate(reason3)));
				flag = false;
				_log?.LogError(text2);
				return Publish(success: false, text2, inventoryArrangePlan);
			}
			_journalPages = captured.Containers.Select((InventoryArrangeContainer container) => (byte)container.Page).Distinct().ToList();
			_journalBefore = captured;
			_journalAfterLayoutFingerprint = capturedInventory.LayoutFingerprint;
			_journalAfterFingerprint = capturedInventory.Fingerprint;
			_journalScope = scope;
			_journalConnectionKey = _connectionKey();
			_log?.LogInfo("背包整理完成：来源=" + source + "；" + Describe(inventoryArrangePlan, "执行") + "。");
			return Publish(success: true, Describe(inventoryArrangePlan, "已整理"), inventoryArrangePlan);
		}
		catch (Exception ex)
		{
			string text3 = "整理出现异常；请查看 BepInEx 日志。";
			if (flag)
			{
				text3 = (Restore(inventory, captured, out var reason4)
					? "整理出现异常，已恢复执行前库存和热键坐标。"
					: ("整理出现异常且恢复失败；请停止继续操作并查看 BepInEx 日志。恢复：" + PluginLocalization.Translate(reason4)));
			}
			_log?.LogError(text3 + "\n" + ex);
			return Publish(success: false, text3, inventoryArrangePlan);
		}
		finally
		{
			_isRunning = false;
		}
	}

	internal InventoryArrangeResult BeginQuickTransfer(byte sourcePage, byte sourceX, byte sourceY)
	{
		if (_isRunning || _quickTransfer != null)
		{
			return Publish(success: false, "跨箱集中正在执行，已忽略重复请求。", null);
		}
		if (!_canExecuteGameplay() || Player.LocalPlayer == null || Player.LocalPlayer.inventory == null)
		{
			return Publish(success: false, "当前没有获得玩法授权的本地玩家库存。", null);
		}
		PlayerInventory inventory = Player.LocalPlayer.inventory;
		if (!inventory.isStoring || !IsCarriedOrStoragePage(sourcePage))
		{
			return Publish(success: false, "当前不在储物箱与随身库存之间，放行原生操作。", null);
		}
		if (Player.LocalPlayer.equipment != null && Player.LocalPlayer.equipment.isBusy)
		{
			return Publish(success: false, "角色装备切换中，放行原生操作。", null);
		}
		ItemJar jar = GetJar(inventory, sourcePage, sourceX, sourceY);
		ItemAsset itemAsset = jar?.GetAsset();
		if (jar?.item == null || itemAsset == null)
		{
			return Publish(success: false, "源坐标没有可转移的有效物品，放行原生操作。", null);
		}
		if (!TrySendNextQuickTransfer(inventory, _quickTransfer = new QuickTransferState
		{
			SourcePage = sourcePage,
			AssetId = jar.item.id,
			AssetGuid = itemAsset.GUID.ToString()
		}, sourcePage, sourceX, sourceY))
		{
			_quickTransfer = null;
			return Publish(success: false, "目标容器没有可用空位，放行原生操作。", null);
		}
		return Publish(success: true, "已接管原生快速转移：正在集中同一资产的物品。", null);
	}

	internal void PumpQuickTransfer()
	{
		QuickTransferState quickTransfer = _quickTransfer;
		if (quickTransfer == null)
		{
			return;
		}
		if (!_canExecuteGameplay() || Player.LocalPlayer == null || Player.LocalPlayer.inventory == null)
		{
			_quickTransfer = null;
			return;
		}
		PlayerInventory inventory = Player.LocalPlayer.inventory;
		ItemJar jar = GetJar(inventory, quickTransfer.LastPage, quickTransfer.LastX, quickTransfer.LastY);
		if (jar == null)
		{
			quickTransfer.WaitFrames = 0;
			if (!TrySendNextQuickTransfer(inventory, quickTransfer, byte.MaxValue, byte.MaxValue, byte.MaxValue))
			{
				_quickTransfer = null;
				Publish(success: true, $"同物品跨箱集中完成：已转移 {quickTransfer.MovedCount} 件。", null);
			}
			return;
		}
		if (!string.Equals(ItemFingerprint(jar.item), quickTransfer.LastItemFingerprint, StringComparison.Ordinal))
		{
			_quickTransfer = null;
			_log?.LogWarning("原生快速转移期间源格子被外部操作改变，已停止批量集中。");
			Publish(success: false, "源格子在批量转移期间发生变化，已停止集中。", null);
			return;
		}
		quickTransfer.WaitFrames++;
		if (quickTransfer.WaitFrames > 120)
		{
			_log?.LogWarning("原生快速转移在等待库存同步时超时，已停止批量集中；未确认的物品保留原位。");
			_quickTransfer = null;
			Publish(success: false, "原生库存未确认转移结果，已停止批量集中。", null);
		}
	}

	private bool TrySendNextQuickTransfer(PlayerInventory inventory, QuickTransferState state, byte preferredPage, byte preferredX, byte preferredY)
	{
		if (!TryFindMatchingSource(inventory, state, preferredPage, preferredX, preferredY, out var page, out var x, out var y, out var result))
		{
			return false;
		}
		if (!TryFindDestination(inventory, page, result, out var page2, out var x2, out var y2, out var rotation))
		{
			return false;
		}
		inventory.sendDragItem(page, x, y, page2, x2, y2, rotation);
		state.LastPage = page;
		state.LastX = x;
		state.LastY = y;
		state.LastItemFingerprint = ItemFingerprint(result.item);
		state.WaitFrames = 0;
		state.MovedCount++;
		return true;
	}

	private static bool TryFindMatchingSource(PlayerInventory inventory, QuickTransferState state, byte preferredPage, byte preferredX, byte preferredY, out byte page, out byte x, out byte y, out ItemJar result)
	{
		page = byte.MaxValue;
		x = byte.MaxValue;
		y = byte.MaxValue;
		result = null;
		IEnumerable<byte> enumerable;
		if (state.SourcePage != PlayerInventory.STORAGE)
		{
			enumerable = from value in Enumerable.Range(PlayerInventory.BACKPACK, PlayerInventory.STORAGE - PlayerInventory.BACKPACK)
				select (byte)value;
		}
		else
		{
			IEnumerable<byte> enumerable2 = new byte[1] { PlayerInventory.STORAGE };
			enumerable = enumerable2;
		}
		IEnumerable<byte> enumerable3 = enumerable;
		if (preferredPage != byte.MaxValue && preferredPage != PlayerInventory.AREA && IsSourcePage(state, preferredPage))
		{
			ItemJar jar = GetJar(inventory, preferredPage, preferredX, preferredY);
			if (IsMatchingAsset(jar, state))
			{
				page = preferredPage;
				x = preferredX;
				y = preferredY;
				result = jar;
				return true;
			}
		}
		foreach (byte item2 in enumerable3)
		{
			Items items = ((item2 < inventory.items.Length) ? inventory.items[item2] : null);
			if (items == null)
			{
				continue;
			}
			for (byte b = 0; b < items.getItemCount(); b++)
			{
				ItemJar item = items.getItem(b);
				if (IsMatchingAsset(item, state))
				{
					page = item2;
					x = item.x;
					y = item.y;
					result = item;
					return true;
				}
			}
		}
		return false;
	}

	private static bool TryFindDestination(PlayerInventory inventory, byte sourcePage, ItemJar jar, out byte page, out byte x, out byte y, out byte rotation)
	{
		page = byte.MaxValue;
		x = byte.MaxValue;
		y = byte.MaxValue;
		rotation = 0;
		if (jar == null)
		{
			return false;
		}
		if (sourcePage == PlayerInventory.STORAGE)
		{
			for (byte b = PlayerInventory.BACKPACK; b < PlayerInventory.STORAGE; b++)
			{
				Items items = ((b < inventory.items.Length) ? inventory.items[b] : null);
				if (items != null && items.getItemCount() < 200 && items.tryFindSpace(jar.size_x, jar.size_y, out x, out y, out rotation))
				{
					page = b;
					return true;
				}
			}
			return false;
		}
		Items items2 = inventory.items[PlayerInventory.STORAGE];
		if (items2 == null || items2.getItemCount() >= 200 || !items2.tryFindSpace(jar.size_x, jar.size_y, out x, out y, out rotation))
		{
			return false;
		}
		page = PlayerInventory.STORAGE;
		return true;
	}

	private static bool IsSourcePage(QuickTransferState state, byte page)
	{
		if (state.SourcePage != PlayerInventory.STORAGE)
		{
			if (page >= PlayerInventory.BACKPACK)
			{
				return page < PlayerInventory.STORAGE;
			}
			return false;
		}
		return page == PlayerInventory.STORAGE;
	}

	private static bool IsCarriedOrStoragePage(byte page)
	{
		if (page != PlayerInventory.STORAGE)
		{
			if (page >= PlayerInventory.BACKPACK)
			{
				return page < PlayerInventory.STORAGE;
			}
			return false;
		}
		return true;
	}

	private static ItemJar GetJar(PlayerInventory inventory, byte page, byte x, byte y)
	{
		if (inventory?.items == null || page >= inventory.items.Length || page == PlayerInventory.AREA)
		{
			return null;
		}
		Items items = inventory.items[page];
		if (items == null)
		{
			return null;
		}
		byte index = items.getIndex(x, y);
		if (index != byte.MaxValue)
		{
			return items.getItem(index);
		}
		return null;
	}

	private static bool IsMatchingAsset(ItemJar jar, QuickTransferState state)
	{
		ItemAsset itemAsset = jar?.GetAsset();
		if (jar?.item != null && itemAsset != null)
		{
			return InventoryQuickTransferIdentity.IsSameAsset(state.AssetId, state.AssetGuid, jar.item.id, itemAsset.GUID.ToString());
		}
		return false;
	}

	private static string ItemFingerprint(Item item)
	{
		if (item == null)
		{
			return string.Empty;
		}
		return item.id + "|" + item.amount + "|" + item.quality + "|" + HashState(item.state);
	}

	private bool TryCapturePages(IEnumerable<byte> pages, out CapturedInventory captured, out string reason)
	{
		captured = null;
		reason = null;
		if (!_canExecuteGameplay() || Player.LocalPlayer == null || Player.LocalPlayer.inventory == null)
		{
			reason = "当前没有获得玩法授权的本地玩家库存。";
			return false;
		}
		if (Player.LocalPlayer.equipment != null && Player.LocalPlayer.equipment.isBusy)
		{
			reason = "角色装备切换中，无法撤销。";
			return false;
		}
		InventoryArrangeSettings settings = _settings?.Invoke() ?? new InventoryArrangeSettings();
		captured = Capture(Player.LocalPlayer.inventory, pages, settings);
		if (captured.UnsupportedItemCount > 0)
		{
			reason = "库存包含无法解析的物品，无法安全撤销。";
			return false;
		}
		return true;
	}

	internal InventoryArrangeResult Undo()
	{
		if (_isRunning)
		{
			return Publish(success: false, "整理正在执行，暂不能撤销。", null);
		}
		if (_journalBefore == null)
		{
			return Publish(success: false, "没有可撤销的整理记录。", null);
		}
		if (!string.Equals(_journalConnectionKey, _connectionKey(), StringComparison.Ordinal))
		{
			_journalBefore = null;
			_journalPages = null;
			_journalAfterLayoutFingerprint = null;
			_journalAfterFingerprint = null;
			_journalConnectionKey = null;
			return Publish(success: false, "撤销已失效：连接会话已经变化。", null);
		}
		string reason = null;
		if (_journalPages == null || !TryCapturePages(_journalPages, out var captured, out reason))
		{
			return Publish(success: false, reason, null);
		}
		if (!string.Equals(captured.LayoutFingerprint, _journalAfterLayoutFingerprint, StringComparison.Ordinal) ||
			!string.Equals(captured.Fingerprint, _journalAfterFingerprint, StringComparison.Ordinal))
		{
			_journalBefore = null;
			_journalPages = null;
			_journalConnectionKey = null;
			_journalAfterLayoutFingerprint = null;
			_journalAfterFingerprint = null;
			return Publish(success: false, "撤销已失效：整理后的库存已被外部操作改变。", null);
		}
		_isRunning = true;
		try
		{
			if (!Restore(Player.LocalPlayer.inventory, _journalBefore, out reason))
			{
				return Publish(success: false, "撤销失败，无法恢复原始位置：" + PluginLocalization.Translate(reason), null);
			}
			CapturedInventory restored = Capture(Player.LocalPlayer.inventory, _journalPages,
				_settings?.Invoke() ?? new InventoryArrangeSettings());
			bool flag = string.Equals(restored.LayoutFingerprint, _journalBefore.LayoutFingerprint, StringComparison.Ordinal) &&
				string.Equals(restored.Fingerprint, _journalBefore.Fingerprint, StringComparison.Ordinal);
			_journalBefore = null;
			_journalAfterLayoutFingerprint = null;
			_journalAfterFingerprint = null;
			_journalPages = null;
			_journalConnectionKey = null;
			return Publish(flag, flag ? "已撤销上次整理。" : "撤销后校验失败，请查看 BepInEx 日志。", null);
		}
		finally
		{
			_isRunning = false;
		}
	}

	private bool TryCapture(InventoryArrangeScope scope, bool requireGameplayAccess, out CapturedInventory captured, out string reason)
	{
		captured = null;
		reason = null;
		if ((requireGameplayAccess && !_canExecuteGameplay()) || Player.LocalPlayer == null || Player.LocalPlayer.inventory == null)
		{
			reason = "当前没有可用的本地玩家库存或玩法授权。";
			return false;
		}
		PlayerInventory inventory = Player.LocalPlayer.inventory;
		if (Player.LocalPlayer.equipment != null && Player.LocalPlayer.equipment.isBusy)
		{
			reason = "角色装备切换中，已取消整理。";
			return false;
		}
		InventoryArrangeSettings settings = _settings?.Invoke() ?? new InventoryArrangeSettings();
		captured = Capture(inventory, GetPages(inventory, scope, settings), settings);
		if (captured.UnsupportedItemCount > 0)
		{
			reason = "库存包含无法解析的物品，已取消整理以避免丢失物品。";
			return false;
		}
		if (captured.Containers.Count == 0)
		{
			reason = "当前上下文没有可用库存容器。";
			return false;
		}
		return true;
	}

	private static IEnumerable<byte> GetPages(PlayerInventory inventory, InventoryArrangeScope scope, InventoryArrangeSettings settings)
	{
		if (scope == InventoryArrangeScope.CurrentContext && inventory.isStoring)
		{
			return new byte[1] { PlayerInventory.STORAGE };
		}
		List<byte> pages = new List<byte> { PlayerInventory.SLOTS };
		if (settings?.IncludeBackpackPage ?? true)
		{
			pages.Add(PlayerInventory.BACKPACK);
		}
		if (settings?.IncludeVestPage ?? true)
		{
			pages.Add(PlayerInventory.VEST);
		}
		if (settings?.IncludeShirtPage ?? true)
		{
			pages.Add(PlayerInventory.SHIRT);
		}
		if (settings?.IncludePantsPage ?? true)
		{
			pages.Add(PlayerInventory.PANTS);
		}
		return pages;
	}

	private CapturedInventory Capture(PlayerInventory inventory, IEnumerable<byte> pages, InventoryArrangeSettings settings, Func<byte, ItemJar, bool> selected = null)
	{
		CapturedInventory capturedInventory = new CapturedInventory();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		Dictionary<string, List<HotkeyInfo>> dictionary = new Dictionary<string, List<HotkeyInfo>>(StringComparer.Ordinal);
		if (Player.LocalPlayer?.equipment?.hotkeys != null)
		{
			HotkeyInfo[] hotkeys = Player.LocalPlayer.equipment.hotkeys;
			foreach (HotkeyInfo hotkeyInfo in hotkeys)
			{
				if (hotkeyInfo != null && hotkeyInfo.id != 0)
				{
					string text = hotkeyInfo.page + ":" + hotkeyInfo.x + ":" + hotkeyInfo.y;
					hashSet.Add(text);
					if (!dictionary.TryGetValue(text, out var value))
					{
						value = new List<HotkeyInfo>();
						dictionary.Add(text, value);
					}
					value.Add(hotkeyInfo);
				}
			}
		}
		InventoryStateReader.RefreshAmmoCompatibility();
		int num = 0;
		foreach (byte page in pages)
		{
			Items items = ((page < inventory.items.Length) ? inventory.items[page] : null);
			if (items == null || items.width == 0 || items.height == 0)
			{
				continue;
			}
			InventoryArrangeContainer inventoryArrangeContainer = new InventoryArrangeContainer
			{
				Page = page,
				Width = items.width,
				Height = items.height
			};
			for (byte b = 0; b < items.getItemCount(); b++)
			{
				ItemJar item = items.getItem(b);
				Item item2 = item?.item;
				ItemAsset itemAsset = item2?.GetAsset();
				if (item == null || item2 == null || itemAsset == null)
				{
					capturedInventory.UnsupportedItemCount++;
				}
				else
				{
					bool flag = selected?.Invoke(page, item) ?? true;
					string text = page + ":" + b;
					bool flag2 = settings.ProtectEquipped && Player.LocalPlayer.equipment != null && Player.LocalPlayer.equipment.checkSelection(page, item.x, item.y);
					InventoryStateReader.TryReadAmmoContainerMetadata(itemAsset, out var capacity, out AmmoRefillAssetRules ammoRules);
					bool flag4 = InventoryStateReader.TryReadAmmoCount(itemAsset, item2, out var ammoCount);
					ItemStackingKind stackingKind = UnturnedItemStackingClassifier.Classify(itemAsset, item2,
						ammoRules?.IsDirectSource == true, ammoRules?.IsDirectTarget == true);
					string text2 = page + ":" + item.x + ":" + item.y;
					bool flag5 = hashSet.Contains(text2);
					inventoryArrangeContainer.Items.Add(new InventoryArrangeItem
					{
						ItemKey = text,
						AssetGuid = itemAsset.GUID.ToString(),
						StateHash = HashState(item2.state),
						Name = (itemAsset.FriendlyName ?? string.Empty),
						RarityRank = (int)itemAsset.rarity,
						TypeRank = (int)itemAsset.type,
						LegacyId = item2.id,
						Width = item.size_x,
						Height = item.size_y,
						Amount = item2.amount,
						OriginalAmount = item2.amount,
						MaxAmount = Math.Max(1, itemAsset.MaxAmount),
						StackingKind = stackingKind,
						IsMagazine = ammoRules?.IsMagazine ?? false,
						IsAmmoSource = ammoRules?.IsDirectSource ?? false,
						IsAmmoTarget = ammoRules?.IsDirectTarget ?? false,
						AmmoCapacity = capacity,
						AmmoDirectSourceAssetGuids = ammoRules?.DirectSourceAssetGuids.ToArray() ?? Array.Empty<string>(),
						AmmoCount = (flag4 ? ammoCount : (-1)),
						Quality = item2.quality,
						SourcePage = page,
						SourceX = item.x,
						SourceY = item.y,
						SourceRotation = item.rot,
						OriginalIndex = num++,
						IsProtected = ((!flag | flag2) || (settings.FixHotkeyItems & flag5)),
						IsHotkeyBound = flag5
					});
					capturedInventory.SourceItems.Add(text, Clone(item2));
					if (dictionary.TryGetValue(text2, out var value2))
					{
						foreach (HotkeyInfo hotkeyInfo2 in value2)
						{
							capturedInventory.HotkeyBindings.Add(new HotkeyBinding
							{
								Hotkey = hotkeyInfo2,
								ItemKey = text,
								OriginalPage = hotkeyInfo2.page,
								OriginalX = hotkeyInfo2.x,
								OriginalY = hotkeyInfo2.y
							});
						}
					}
				}
			}
			capturedInventory.Containers.Add(inventoryArrangeContainer);
		}
		capturedInventory.Fingerprint = InventoryStackingSemantics.BuildConservationFingerprint(
			capturedInventory.Containers.SelectMany(container => container.Items));
		capturedInventory.LayoutFingerprint = LayoutFingerprint(capturedInventory.Containers);
		return capturedInventory;
	}

	private bool ApplyPlan(PlayerInventory inventory, CapturedInventory captured, InventoryArrangePlan plan, out string reason)
	{
		reason = null;
		HashSet<byte> hashSet = new HashSet<byte>(plan.Placements.Select((InventoryArrangePlacement placement) => (byte)placement.Page));
		foreach (InventoryArrangeContainer container in captured.Containers)
		{
			hashSet.Add((byte)container.Page);
		}
		foreach (byte item3 in hashSet)
		{
			Items items = inventory.items[item3];
			for (int num = items.getItemCount() - 1; num >= 0; num--)
			{
				ItemJar item = items.getItem((byte)num);
				if (!IsProtected(captured, item3, item))
				{
					inventory.removeItem(item3, (byte)num);
				}
			}
		}
		foreach (InventoryArrangePlacement item4 in plan.Placements.Where((InventoryArrangePlacement placement) => !placement.Item.IsProtected))
		{
			if (!captured.SourceItems.TryGetValue(item4.Item.ItemKey, out var value))
			{
				reason = "计划引用了不存在的源物品。";
				return false;
			}
			Item item2 = new Item(value.id, (byte)item4.Item.Amount, value.quality, CloneState(value.state));
			if (!inventory.tryAddItem(item2, (byte)item4.X, (byte)item4.Y, (byte)item4.Page, (byte)item4.Rotation))
			{
				reason = "原生库存 API 拒绝放置计划中的物品。";
				return false;
			}
		}
		int num2 = 0;
		foreach (HotkeyBinding hotkeyBinding in captured.HotkeyBindings)
		{
			InventoryArrangePlacement inventoryArrangePlacement = plan.Placements.FirstOrDefault((InventoryArrangePlacement placement) => string.Equals(placement.Item.ItemKey, hotkeyBinding.ItemKey, StringComparison.Ordinal));
			if (inventoryArrangePlacement != null)
			{
				if (hotkeyBinding.Hotkey.page != (byte)inventoryArrangePlacement.Page || hotkeyBinding.Hotkey.x != (byte)inventoryArrangePlacement.X || hotkeyBinding.Hotkey.y != (byte)inventoryArrangePlacement.Y)
				{
					num2++;
				}
				hotkeyBinding.Hotkey.page = (byte)inventoryArrangePlacement.Page;
				hotkeyBinding.Hotkey.x = (byte)inventoryArrangePlacement.X;
				hotkeyBinding.Hotkey.y = (byte)inventoryArrangePlacement.Y;
			}
		}
		if (InventoryArrangeExecutionPolicy.RequiresHotkeyDisplayRefresh(
			inventoryWasRebuilt: true, capturedBindingCount: captured.HotkeyBindings.Count))
		{
			Player.LocalPlayer?.equipment?.onHotkeysUpdated?.Invoke();
			_log?.LogInfo("[InventoryArrangeHotkeys] Refreshed " + captured.HotkeyBindings.Count +
				" bound shortcut displays; relocated coordinates=" + num2 + ".");
		}
		return true;
	}

	private static bool Restore(PlayerInventory inventory, CapturedInventory captured, out string reason)
	{
		reason = null;
		try
		{
			foreach (InventoryArrangeContainer container in captured.Containers)
			{
				for (int num = inventory.items[container.Page].getItemCount() - 1; num >= 0; num--)
				{
					inventory.removeItem((byte)container.Page, (byte)num);
				}
			}
			foreach (InventoryArrangeItem item2 in from inventoryArrangeItem in captured.Containers.SelectMany((InventoryArrangeContainer container) => container.Items)
				orderby inventoryArrangeItem.OriginalIndex
				select inventoryArrangeItem)
			{
				Item item = Clone(captured.SourceItems[item2.ItemKey]);
				if (!inventory.tryAddItem(item, (byte)item2.SourceX, (byte)item2.SourceY, (byte)item2.SourcePage, (byte)item2.SourceRotation))
				{
					reason = "原生 API 拒绝恢复原始位置。";
					return false;
				}
			}
			int updatedHotkeyCount = 0;
			foreach (HotkeyBinding hotkeyBinding in captured.HotkeyBindings)
			{
				if (hotkeyBinding.Hotkey.page != hotkeyBinding.OriginalPage || hotkeyBinding.Hotkey.x != hotkeyBinding.OriginalX || hotkeyBinding.Hotkey.y != hotkeyBinding.OriginalY)
				{
					updatedHotkeyCount++;
				}
				hotkeyBinding.Hotkey.page = hotkeyBinding.OriginalPage;
				hotkeyBinding.Hotkey.x = hotkeyBinding.OriginalX;
				hotkeyBinding.Hotkey.y = hotkeyBinding.OriginalY;
			}
			if (InventoryArrangeExecutionPolicy.RequiresHotkeyDisplayRefresh(
				inventoryWasRebuilt: true, capturedBindingCount: captured.HotkeyBindings.Count))
			{
				Player.LocalPlayer?.equipment?.onHotkeysUpdated?.Invoke();
			}
			return true;
		}
		catch (Exception ex)
		{
			reason = ex.Message;
			return false;
		}
	}

	private static bool IsProtected(CapturedInventory captured, byte page, ItemJar jar)
	{
		if (jar == null)
		{
			return false;
		}
		return captured.Containers.SelectMany((InventoryArrangeContainer container) => container.Items).Any((InventoryArrangeItem item) => item.IsProtected && item.SourcePage == page && item.SourceX == jar.x && item.SourceY == jar.y);
	}

	private InventoryArrangeResult Publish(bool success, string message, InventoryArrangePlan plan)
	{
		LastResult = new InventoryArrangeResult
		{
			Success = success,
			Message = PluginLocalization.Translate(message),
			Plan = plan
		};
		return LastResult;
	}

	private static string Describe(InventoryArrangePlan plan, string prefix)
	{
		string ammo = plan.AmmoChangedContainerCount > 0 ? $"，弹药调整 {plan.AmmoChangedContainerCount}" : string.Empty;
		return $"{prefix}：移动 {plan.MoveCount}，归并 {plan.MergeCount}，跨页 {plan.CrossPageCount}{ammo}。";
	}

	private static Item Clone(Item source)
	{
		return new Item(source.id, source.amount, source.quality, CloneState(source.state));
	}

	private static byte[] CloneState(byte[] state)
	{
		if (state != null)
		{
			return (byte[])state.Clone();
		}
		return Array.Empty<byte>();
	}

	private static string HashState(byte[] state)
	{
		if (state == null || state.Length == 0)
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder(state.Length * 2);
		foreach (byte b in state)
		{
			stringBuilder.Append(b.ToString("X2"));
		}
		return stringBuilder.ToString();
	}

	private static string LayoutFingerprint(IEnumerable<InventoryArrangeContainer> containers)
	{
		return string.Join(";", from item in containers.SelectMany((InventoryArrangeContainer container) => container.Items)
			orderby item.SourcePage, item.SourceX, item.SourceY
			select item.SourcePage + "|" + item.SourceX + "|" + item.SourceY + "|" + item.SourceRotation + "|" + item.AssetGuid + "|" + item.Amount + "|" + item.Quality + "|" + item.StateHash);
	}
}
