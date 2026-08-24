using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal sealed class InventoryArrangeSettings
{
	internal InventoryArrangeAction Action;

	internal InventorySortPreset Preset;

	internal bool AutoRotate = true;

	internal bool AllowCrossPage = true;

	internal bool AutoRefillMagazines = true;

	internal List<int> PreferredPages = new List<int>();

	internal bool ProtectEquipped = true;

	internal List<InventorySortKeySpec> CustomKeys = new List<InventorySortKeySpec>();

	internal bool PreserveHotkeys = true;
}
