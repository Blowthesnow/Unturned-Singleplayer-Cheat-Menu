namespace UnturnedSingleplayerCheatMenu.InfiniteStorage;

internal sealed class InfiniteStorageCardMetadata
{
	internal string Text { get; set; } = string.Empty;
	internal string WithdrawUnitLabel { get; set; } = "个";

	internal static InfiniteStorageCardMetadata Build(VirtualStorageEntry entry, bool assetResolved = false, bool showQuality = false)
	{
		if (entry == null) return new InfiniteStorageCardMetadata { Text = "库存 0" };
		string count = entry.StoredUnitCount.ToString("N0");
		string quality = showQuality && entry.QualityPolicy == QualityPolicy.Exact
			? " | 品质 " + entry.NativeQuality + "%"
			: string.Empty;
		switch (entry.DisplayPolicy)
		{
			case DisplayPolicy.ResourceTotal:
				return new InfiniteStorageCardMetadata { Text = "库存 " + count, WithdrawUnitLabel = "数量" };
			case DisplayPolicy.AmmoContainer:
				return new InfiniteStorageCardMetadata { Text = "库存 " + count + " 个 | 弹药 " + entry.NativeAmount + "/" + entry.NativeMaxStack + quality, WithdrawUnitLabel = "个" };
			case DisplayPolicy.FuelOrWaterContainer:
				string label = entry.AmountRole == AmountRole.WaterUnits ? "水" : "燃料";
				return new InfiniteStorageCardMetadata { Text = "库存 " + count + " 个 | " + label + " " + entry.NativeAmount + "/" + entry.NativeMaxStack + quality, WithdrawUnitLabel = "个" };
			case DisplayPolicy.ConservativeInstance:
				string state = !assetResolved
					? " | 保守状态隔离"
					: entry.StatePolicy == StatePolicy.ExactBytes && entry.NativeState != null && entry.NativeState.Length > 0
						? " | 状态独立保存"
						: string.Empty;
				return new InfiniteStorageCardMetadata { Text = "库存 " + count + " 个" + quality + state, WithdrawUnitLabel = "个" };
			default:
				return new InfiniteStorageCardMetadata { Text = "库存 " + count + " 个" + quality, WithdrawUnitLabel = "个" };
		}
	}
}
