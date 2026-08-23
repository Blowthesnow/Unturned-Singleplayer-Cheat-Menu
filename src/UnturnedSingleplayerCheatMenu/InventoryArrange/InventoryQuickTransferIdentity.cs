using System;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal static class InventoryQuickTransferIdentity
{
    internal static bool IsSameAsset(int sourceId, string sourceGuid, int candidateId, string candidateGuid)
    {
        return sourceId == candidateId
            && string.Equals(sourceGuid, candidateGuid, StringComparison.Ordinal);
    }
}