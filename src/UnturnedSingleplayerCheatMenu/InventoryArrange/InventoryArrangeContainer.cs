using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal sealed class InventoryArrangeContainer
{
	internal int Page;

	internal int Width;

	internal int Height;

	internal List<InventoryArrangeItem> Items = new List<InventoryArrangeItem>();
}
