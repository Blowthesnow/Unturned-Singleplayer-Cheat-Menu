using System.Collections.Generic;
using UnturnedSingleplayerCheatMenu.ItemStacking;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal sealed class InventoryArrangePlan
{
	internal bool IsSuccess;

	internal string FailureReason;

	internal List<InventoryArrangePlacement> Placements = new List<InventoryArrangePlacement>();

	internal int MoveCount;

	internal int MergeCount;

	internal int CrossPageCount;

	internal int AmmoChangedContainerCount;

	internal int InputItemCount;

	internal int OutputItemCount;

	internal List<AmmoRefillTransfer> AmmoTransfers = new List<AmmoRefillTransfer>();
}
