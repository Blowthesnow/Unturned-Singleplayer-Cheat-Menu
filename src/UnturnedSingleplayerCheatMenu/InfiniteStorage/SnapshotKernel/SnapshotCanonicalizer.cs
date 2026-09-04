using System;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

internal readonly struct SnapshotCanonicalizationFacts
{
	internal SnapshotCanonicalizationFacts(bool isFarmBuild, byte assetMaxAmount)
	{
		IsFarmBuild = isFarmBuild;
		AssetMaxAmount = assetMaxAmount;
	}

	internal bool IsFarmBuild { get; }
	internal byte AssetMaxAmount { get; }
}

internal static class SnapshotCanonicalizer
{
	internal static ItemSnapshot Canonicalize(ItemSnapshot snapshot, SnapshotCanonicalizationFacts facts)
	{
		if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
		byte[] state = snapshot.State;
		if (facts.IsFarmBuild && facts.AssetMaxAmount == 1 && snapshot.Amount == 1 &&
			state.Length == sizeof(uint))
			return new ItemSnapshot(snapshot.AssetId, snapshot.Amount, snapshot.Quality, new byte[sizeof(uint)]);
		return snapshot.Clone();
	}
}
