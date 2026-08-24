namespace UnturnedSingleplayerCheatMenu.Services;

internal enum WorkshopTranslationErrorKind
{
	None,
	Configuration,
	InvalidRequest,
	Authentication,
	RateLimit,
	Server,
	Timeout,
	Network,
	ResponseFormat,
	Cancelled,
	Unknown
}
