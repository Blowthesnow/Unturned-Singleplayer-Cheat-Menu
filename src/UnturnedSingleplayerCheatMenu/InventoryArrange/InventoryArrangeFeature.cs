using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using SDG.Unturned;

using UnturnedSingleplayerCheatMenu.Services;
namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal sealed class InventoryArrangeResult
{
    internal bool Success;
    internal string Message;
    internal InventoryArrangePlan Plan;
}

internal sealed class InventoryArrangeFeature
{
    private sealed class CapturedInventory
    {
        internal List<InventoryArrangeContainer> Containers = new();
        internal Dictionary<string, Item> SourceItems = new(StringComparer.Ordinal);
        internal int UnsupportedItemCount;
        internal string LayoutFingerprint;
        internal string Fingerprint;
    }

    private readonly ManualLogSource _log;
    private readonly Func<InventoryArrangeSettings> _settings;
    private readonly InventoryArrangePlanner _planner = new();
    private bool _isRunning;
    private CapturedInventory _journalBefore;
    private string _journalAfterLayoutFingerprint;
    private List<byte> _journalPages;
    private InventoryArrangeScope _journalScope;
    private bool _hasLoggedPlayerReady;
    private QuickTransferState _quickTransfer;

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
    internal InventoryArrangeFeature(ManualLogSource log, Func<InventoryArrangeSettings> settings)
    {
        _log = log;
        _settings = settings;
    }

    internal InventoryArrangeResult LastResult { get; private set; }

    internal void OnPlayerInitialized()
    {
        if (_hasLoggedPlayerReady || Player.LocalPlayer == null)
            return;
        _hasLoggedPlayerReady = true;
        _log?.LogInfo("已检测到本地玩家初始化；背包整理快捷键和菜单入口可用。");
    }

    internal InventoryArrangeResult Preview(InventoryArrangeScope scope, InventoryArrangeAction action)
    {
        if (!TryCapture(scope, out CapturedInventory captured, out string reason))
            return Publish(false, reason, null);

        InventoryArrangeSettings settings = _settings?.Invoke() ?? new InventoryArrangeSettings();
        settings.Action = action;
        InventoryArrangePlan plan = _planner.CreatePlan(captured.Containers, settings);
        return Publish(plan.IsSuccess, plan.IsSuccess ? Describe(plan, "预览") : plan.FailureReason, plan);
    }

    internal InventoryArrangeResult Execute(InventoryArrangeScope scope, InventoryArrangeAction action, string source)
    {
        if (_isRunning)
            return Publish(false, "整理正在执行，已忽略重复请求。", null);
        if (!TryCapture(scope, out CapturedInventory captured, out string reason))
            return Publish(false, reason, null);

        InventoryArrangeSettings settings = _settings?.Invoke() ?? new InventoryArrangeSettings();
        settings.Action = action;
        InventoryArrangePlan plan = _planner.CreatePlan(captured.Containers, settings);
        if (!plan.IsSuccess)
            return Publish(false, plan.FailureReason, plan);

        _isRunning = true;
        try
        {
            PlayerInventory inventory = Player.LocalPlayer.inventory;
            if (!ApplyPlan(inventory, captured, plan, out reason))
            {
                bool restored = Restore(inventory, captured, out string restoreFailure);
                string failure = restored
                    ? "整理未完成，已恢复执行前库存：" + reason
                    : "整理失败且恢复失败；请停止继续操作并查看 BepInEx 日志。原因：" + reason + "；恢复：" + restoreFailure;
                _log?.LogError(failure);
                return Publish(false, failure, plan);
            }

            CapturedInventory after = Capture(inventory, GetPages(inventory, scope), settings);
            if (!string.Equals(captured.Fingerprint, after.Fingerprint, StringComparison.Ordinal))
            {
                bool restored = Restore(inventory, captured, out string restoreFailure);
                string failure = restored
                    ? "整理后校验发现物品指纹变化，已恢复执行前库存。"
                    : "整理后校验失败且恢复失败；请停止继续操作并查看日志。恢复：" + restoreFailure;
                _log?.LogError(failure);
                return Publish(false, failure, plan);
            }
            _journalPages = captured.Containers.Select(container => (byte)container.Page).Distinct().ToList();
            _journalBefore = captured;
            _journalAfterLayoutFingerprint = after.LayoutFingerprint;
            _journalScope = scope;

            _log?.LogInfo($"背包整理完成：来源={source}；{Describe(plan, "执行")}。");
            return Publish(true, Describe(plan, "已整理"), plan);
        }
        catch (Exception ex)
        {
            _log?.LogError("背包整理发生未处理异常。\n" + ex);
            return Publish(false, "整理出现异常；请查看 BepInEx 日志。", plan);
        }
        finally
        {
            _isRunning = false;
        }
    }

    internal InventoryArrangeResult BeginQuickTransfer(byte sourcePage, byte sourceX, byte sourceY)
    {
        if (_isRunning || _quickTransfer != null)
            return Publish(false, "跨箱集中正在执行，已忽略重复请求。", null);
        if (!SingleplayerGuard.IsReady || Player.LocalPlayer == null || Player.LocalPlayer.inventory == null)
            return Publish(false, "当前没有可用的本地单人玩家库存。", null);

        PlayerInventory inventory = Player.LocalPlayer.inventory;
        if (!inventory.isStoring || !IsCarriedOrStoragePage(sourcePage))
            return Publish(false, "当前不在储物箱与随身库存之间，放行原生操作。", null);
        if (Player.LocalPlayer.equipment != null && Player.LocalPlayer.equipment.isBusy)
            return Publish(false, "角色装备切换中，放行原生操作。", null);

        ItemJar sourceJar = GetJar(inventory, sourcePage, sourceX, sourceY);
        ItemAsset sourceAsset = sourceJar?.GetAsset();
        if (sourceJar?.item == null || sourceAsset == null)
            return Publish(false, "源坐标没有可转移的有效物品，放行原生操作。", null);

        QuickTransferState state = new()
        {
            SourcePage = sourcePage,
            AssetId = sourceJar.item.id,
            AssetGuid = sourceAsset.GUID.ToString()
        };
        _quickTransfer = state;
        if (!TrySendNextQuickTransfer(inventory, state, sourcePage, sourceX, sourceY))
        {
            _quickTransfer = null;
            return Publish(false, "目标容器没有可用空位，放行原生操作。", null);
        }

        return Publish(true, "已接管原生快速转移：正在集中同一资产的物品。", null);
    }

    internal void PumpQuickTransfer()
    {
        QuickTransferState state = _quickTransfer;
        if (state == null)
            return;
        if (!SingleplayerGuard.IsReady || Player.LocalPlayer == null || Player.LocalPlayer.inventory == null)
        {
            _quickTransfer = null;
            return;
        }

        PlayerInventory inventory = Player.LocalPlayer.inventory;
        ItemJar lastJar = GetJar(inventory, state.LastPage, state.LastX, state.LastY);
        if (lastJar == null)
        {
            state.WaitFrames = 0;
            if (!TrySendNextQuickTransfer(inventory, state, byte.MaxValue, byte.MaxValue, byte.MaxValue))
            {
                _quickTransfer = null;
                Publish(true, $"同物品跨箱集中完成：已转移 {state.MovedCount} 件。", null);
            }
            return;
        }
        if (!string.Equals(ItemFingerprint(lastJar.item), state.LastItemFingerprint, StringComparison.Ordinal))
        {
            _quickTransfer = null;
            _log?.LogWarning("原生快速转移期间源格子被外部操作改变，已停止批量集中。");
            Publish(false, "源格子在批量转移期间发生变化，已停止集中。", null);
            return;
        }

        state.WaitFrames++;
        if (state.WaitFrames > 120)
        {
            _log?.LogWarning("原生快速转移在等待库存同步时超时，已停止批量集中；未确认的物品保留原位。");
            _quickTransfer = null;
            Publish(false, "原生库存未确认转移结果，已停止批量集中。", null);
        }
    }

    private bool TrySendNextQuickTransfer(PlayerInventory inventory, QuickTransferState state, byte preferredPage, byte preferredX, byte preferredY)
    {
        if (!TryFindMatchingSource(inventory, state, preferredPage, preferredX, preferredY, out byte page, out byte x, out byte y, out ItemJar jar))
            return false;
        if (!TryFindDestination(inventory, page, jar, out byte targetPage, out byte targetX, out byte targetY, out byte targetRotation))
            return false;

        inventory.sendDragItem(page, x, y, targetPage, targetX, targetY, targetRotation);
        state.LastPage = page;
        state.LastX = x;
        state.LastY = y;
        state.LastItemFingerprint = ItemFingerprint(jar.item);
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
        IEnumerable<byte> pages = state.SourcePage == PlayerInventory.STORAGE
            ? new[] { PlayerInventory.STORAGE }
            : Enumerable.Range(PlayerInventory.BACKPACK, PlayerInventory.STORAGE - PlayerInventory.BACKPACK).Select(value => (byte)value);

        if (preferredPage != byte.MaxValue && preferredPage != PlayerInventory.AREA && IsSourcePage(state, preferredPage))
        {
            ItemJar preferred = GetJar(inventory, preferredPage, preferredX, preferredY);
            if (IsMatchingAsset(preferred, state))
            {
                page = preferredPage;
                x = preferredX;
                y = preferredY;
                result = preferred;
                return true;
            }
        }

        foreach (byte candidatePage in pages)
        {
            Items items = candidatePage < inventory.items.Length ? inventory.items[candidatePage] : null;
            if (items == null)
                continue;
            for (byte index = 0; index < items.getItemCount(); index++)
            {
                ItemJar candidate = items.getItem(index);
                if (!IsMatchingAsset(candidate, state))
                    continue;
                page = candidatePage;
                x = candidate.x;
                y = candidate.y;
                result = candidate;
                return true;
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
            return false;

        if (sourcePage == PlayerInventory.STORAGE)
        {
            for (byte candidatePage = PlayerInventory.BACKPACK; candidatePage < PlayerInventory.STORAGE; candidatePage++)
            {
                Items items = candidatePage < inventory.items.Length ? inventory.items[candidatePage] : null;
                if (items != null && items.getItemCount() < 200 && items.tryFindSpace(jar.size_x, jar.size_y, out x, out y, out rotation))
                {
                    page = candidatePage;
                    return true;
                }
            }
            return false;
        }

        Items storage = inventory.items[PlayerInventory.STORAGE];
        if (storage == null || storage.getItemCount() >= 200 || !storage.tryFindSpace(jar.size_x, jar.size_y, out x, out y, out rotation))
            return false;
        page = PlayerInventory.STORAGE;
        return true;
    }

    private static bool IsSourcePage(QuickTransferState state, byte page)
    {
        return state.SourcePage == PlayerInventory.STORAGE
            ? page == PlayerInventory.STORAGE
            : page >= PlayerInventory.BACKPACK && page < PlayerInventory.STORAGE;
    }

    private static bool IsCarriedOrStoragePage(byte page)
    {
        return page == PlayerInventory.STORAGE || (page >= PlayerInventory.BACKPACK && page < PlayerInventory.STORAGE);
    }

    private static ItemJar GetJar(PlayerInventory inventory, byte page, byte x, byte y)
    {
        if (inventory?.items == null || page >= inventory.items.Length || page == PlayerInventory.AREA)
            return null;
        Items items = inventory.items[page];
        if (items == null)
            return null;
        byte index = items.getIndex(x, y);
        return index == byte.MaxValue ? null : items.getItem(index);
    }

    private static bool IsMatchingAsset(ItemJar jar, QuickTransferState state)
    {
        ItemAsset asset = jar?.GetAsset();
        return jar?.item != null
            && asset != null
            && InventoryQuickTransferIdentity.IsSameAsset(state.AssetId, state.AssetGuid, jar.item.id, asset.GUID.ToString());
    }
    private static string ItemFingerprint(Item item)
    {
        if (item == null)
            return string.Empty;
        return item.id + "|" + item.amount + "|" + item.quality + "|" + HashState(item.state);
    }
    private bool TryCapturePages(IEnumerable<byte> pages, out CapturedInventory captured, out string reason)
    {
        captured = null;
        reason = null;
        if (!SingleplayerGuard.IsReady || Player.LocalPlayer == null || Player.LocalPlayer.inventory == null)
        {
            reason = "当前没有可用的本地单人玩家库存。";
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
            return Publish(false, "整理正在执行，暂不能撤销。", null);
        if (_journalBefore == null)
            return Publish(false, "没有可撤销的整理记录。", null);
        string reason = null;
        if (_journalPages == null || !TryCapturePages(_journalPages, out CapturedInventory current, out reason))
            return Publish(false, reason, null);
        if (!string.Equals(current.LayoutFingerprint, _journalAfterLayoutFingerprint, StringComparison.Ordinal))
        {
            _journalBefore = null;
            _journalPages = null;
            _journalAfterLayoutFingerprint = null;
            return Publish(false, "撤销已失效：整理后的库存已被外部操作改变。", null);
        }

        _isRunning = true;
        try
        {
            if (!Restore(Player.LocalPlayer.inventory, _journalBefore, out reason))
                return Publish(false, "撤销失败，无法恢复原始位置：" + reason, null);

            CapturedInventory restored = Capture(Player.LocalPlayer.inventory, _journalPages, _settings?.Invoke() ?? new InventoryArrangeSettings());
            bool verified = string.Equals(restored.LayoutFingerprint, _journalBefore.LayoutFingerprint, StringComparison.Ordinal);
            _journalBefore = null;
            _journalAfterLayoutFingerprint = null;
            _journalPages = null;
            return Publish(verified, verified ? "已撤销上次整理。" : "撤销后校验失败，请查看 BepInEx 日志。", null);
        }
        finally
        {
            _isRunning = false;
        }
    }

    private bool TryCapture(InventoryArrangeScope scope, out CapturedInventory captured, out string reason)
    {
        captured = null;
        reason = null;
        if (!SingleplayerGuard.IsReady || Player.LocalPlayer == null || Player.LocalPlayer.inventory == null)
        {
            reason = "当前没有可用的本地单人玩家库存。";
            return false;
        }

        PlayerInventory inventory = Player.LocalPlayer.inventory;
        if (Player.LocalPlayer.equipment != null && Player.LocalPlayer.equipment.isBusy)
        {
            reason = "角色装备切换中，已取消整理。";
            return false;
        }

        InventoryArrangeSettings settings = _settings?.Invoke() ?? new InventoryArrangeSettings();
        captured = Capture(inventory, GetPages(inventory, scope), settings);
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

    private static IEnumerable<byte> GetPages(PlayerInventory inventory, InventoryArrangeScope scope)
    {
        if (scope == InventoryArrangeScope.CurrentContext && inventory.isStoring)
            return new[] { PlayerInventory.STORAGE };
        return Enumerable.Range(PlayerInventory.BACKPACK, PlayerInventory.PAGES - 2 - PlayerInventory.BACKPACK)
            .Select(page => (byte)page);
    }

    private CapturedInventory Capture(PlayerInventory inventory, IEnumerable<byte> pages, InventoryArrangeSettings settings, Func<byte, ItemJar, bool> selected = null)
    {
        CapturedInventory captured = new();
        HashSet<string> hotkeys = new(StringComparer.Ordinal);
        if (settings.PreserveHotkeys && Player.LocalPlayer?.equipment?.hotkeys != null)
        {
            foreach (HotkeyInfo hotkey in Player.LocalPlayer.equipment.hotkeys)
            {
                if (hotkey != null && hotkey.id != 0)
                    hotkeys.Add(hotkey.page + ":" + hotkey.x + ":" + hotkey.y);
            }
        }

        InventoryStateReader.RefreshAmmoCompatibility();

        int originalIndex = 0;
        foreach (byte page in pages)
        {
            Items items = page < inventory.items.Length ? inventory.items[page] : null;
            if (items == null || items.width == 0 || items.height == 0)
                continue;

            InventoryArrangeContainer container = new() { Page = page, Width = items.width, Height = items.height };
            for (byte index = 0; index < items.getItemCount(); index++)
            {
                ItemJar jar = items.getItem(index);
                Item item = jar?.item;
                ItemAsset asset = item?.GetAsset();
                if (jar == null || item == null || asset == null)
                {
                    captured.UnsupportedItemCount++;
                    continue;
                }
                bool isSelected = selected == null || selected(page, jar);
                string key = page + ":" + index;
                bool equipped = settings.ProtectEquipped && Player.LocalPlayer.equipment != null
                    && Player.LocalPlayer.equipment.checkSelection(page, jar.x, jar.y);
                bool isAmmoContainer = InventoryStateReader.TryReadAmmoContainerMetadata(asset, out bool isMagazine, out int ammoCapacity, out string ammoGroup);
                bool ammoAvailable = InventoryStateReader.TryReadAmmoCount(asset, item, out int ammoCount);
                bool hotkey = hotkeys.Contains(page + ":" + jar.x + ":" + jar.y);
                container.Items.Add(new InventoryArrangeItem
                {
                    ItemKey = key,
                    AssetGuid = asset.GUID.ToString(),
                    StateHash = HashState(item.state),
                    Name = asset.FriendlyName ?? string.Empty,
                    RarityRank = (int)asset.rarity,
                    TypeRank = (int)asset.type,
                    LegacyId = item.id,
                    Width = jar.size_x,
                    Height = jar.size_y,
                    Amount = item.amount,
                    MaxAmount = Math.Max(1, asset.MaxAmount),
                    IsMagazine = isMagazine,
                    IsAmmoSource = isAmmoContainer && !isMagazine,
                    AmmoCapacity = ammoCapacity,
                    AmmoGroup = ammoGroup,
                    AmmoCount = ammoAvailable ? ammoCount : -1,
                    Quality = item.quality,
                    SourcePage = page,
                    SourceX = jar.x,
                    SourceY = jar.y,
                    SourceRotation = jar.rot,
                    OriginalIndex = originalIndex++,
                    IsProtected = !isSelected || equipped || (settings.PreserveHotkeys && hotkey),
                    IsHotkeyBound = hotkey
                });
                captured.SourceItems.Add(key, Clone(item));
            }
            captured.Containers.Add(container);
        }
        captured.Fingerprint = Fingerprint(captured.SourceItems.Values);
        captured.LayoutFingerprint = LayoutFingerprint(captured.Containers);
        return captured;
    }

    private static bool ApplyPlan(PlayerInventory inventory, CapturedInventory captured, InventoryArrangePlan plan, out string reason)
    {
        reason = null;
        HashSet<byte> pages = new(plan.Placements.Select(placement => (byte)placement.Page));
        foreach (InventoryArrangeContainer container in captured.Containers)
            pages.Add((byte)container.Page);

        foreach (byte page in pages)
        {
            Items items = inventory.items[page];
            for (int index = items.getItemCount() - 1; index >= 0; index--)
            {
                ItemJar jar = items.getItem((byte)index);
                if (!IsProtected(captured, page, jar))
                    inventory.removeItem(page, (byte)index);
            }
        }

        foreach (InventoryArrangePlacement placement in plan.Placements.Where(placement => !placement.Item.IsProtected))
        {
            if (!captured.SourceItems.TryGetValue(placement.Item.ItemKey, out Item source))
            {
                reason = "计划引用了不存在的源物品。";
                return false;
            }

            Item output = new(source.id, (byte)placement.Item.Amount, source.quality, CloneState(source.state));
            if (!inventory.tryAddItem(output, (byte)placement.X, (byte)placement.Y, (byte)placement.Page, (byte)placement.Rotation))
            {
                reason = "原生库存 API 拒绝放置计划中的物品。";
                return false;
            }
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
                Items items = inventory.items[container.Page];
                for (int index = items.getItemCount() - 1; index >= 0; index--)
                    inventory.removeItem((byte)container.Page, (byte)index);
            }

            foreach (InventoryArrangeItem descriptor in captured.Containers.SelectMany(container => container.Items).OrderBy(item => item.OriginalIndex))
            {
                Item source = captured.SourceItems[descriptor.ItemKey];
                Item restored = Clone(source);
                if (!inventory.tryAddItem(restored, (byte)descriptor.SourceX, (byte)descriptor.SourceY, (byte)descriptor.SourcePage, (byte)descriptor.SourceRotation))
                {
                    reason = "原生 API 拒绝恢复原始位置。";
                    return false;
                }
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
        if (jar == null) return false;
        return captured.Containers.SelectMany(container => container.Items).Any(item => item.IsProtected && item.SourcePage == page && item.SourceX == jar.x && item.SourceY == jar.y);
    }

    private InventoryArrangeResult Publish(bool success, string message, InventoryArrangePlan plan)
    {
        LastResult = new InventoryArrangeResult { Success = success, Message = message, Plan = plan };
        return LastResult;
    }

    private static string Describe(InventoryArrangePlan plan, string prefix)
    {
        return $"{prefix}：移动 {plan.MoveCount}，归并 {plan.MergeCount}，跨页 {plan.CrossPageCount}。";
    }

    private static Item Clone(Item source) => new(source.id, source.amount, source.quality, CloneState(source.state));

    private static byte[] CloneState(byte[] state) => state == null ? Array.Empty<byte>() : (byte[])state.Clone();

    private static string HashState(byte[] state)
    {
        if (state == null || state.Length == 0) return string.Empty;
        StringBuilder builder = new(state.Length * 2);
        foreach (byte value in state) builder.Append(value.ToString("X2"));
        return builder.ToString();
    }

    private static string Fingerprint(IEnumerable<Item> items)
    {
        return string.Join(";", items.Select(item => item.id + "|" + item.amount + "|" + item.quality + "|" + HashState(item.state)).OrderBy(value => value, StringComparer.Ordinal));
    }
    private static string LayoutFingerprint(IEnumerable<InventoryArrangeContainer> containers)
    {
        return string.Join(";", containers.SelectMany(container => container.Items)
            .OrderBy(item => item.SourcePage).ThenBy(item => item.SourceX).ThenBy(item => item.SourceY)
            .Select(item => item.SourcePage + "|" + item.SourceX + "|" + item.SourceY + "|" + item.SourceRotation + "|" + item.AssetGuid + "|" + item.Amount + "|" + item.Quality + "|" + item.StateHash));
    }

}
