using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal sealed class InventorySortProfile
{
	internal InventorySortPreset Preset;

	internal string DisplayName;

	internal List<InventorySortKeySpec> Keys = new List<InventorySortKeySpec>();

	internal InventorySortProfile(InventorySortPreset preset, string displayName, params InventorySortKeySpec[] keys)
	{
		Preset = preset;
		DisplayName = displayName;
		Keys.AddRange(keys);
	}
}
