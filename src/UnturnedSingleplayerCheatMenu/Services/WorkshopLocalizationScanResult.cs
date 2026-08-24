using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class WorkshopLocalizationScanResult
{
	internal IReadOnlyList<WorkshopLocalizationCandidate> Candidates { get; set; }

	internal int ItemCount { get; set; }

	internal int VehicleCount { get; set; }
}
