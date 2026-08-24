using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnturnedSingleplayerCheatMenu.Services;

internal interface IWorkshopTranslationClient
{
	Task<WorkshopTranslationBatchResult> TranslateAsync(IReadOnlyList<WorkshopTranslationRequest> requests, MachineTranslationSettings settings, CancellationToken cancellationToken);
}
