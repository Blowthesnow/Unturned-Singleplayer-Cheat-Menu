namespace UnturnedSingleplayerCheatMenu.Services;

internal static class FlightViewmodelPolicy
{
	internal static bool ShouldTreatAsGrounded(
		bool nativeIsGrounded,
		bool isLocalMovement,
		bool flightEnabled)
	{
		return nativeIsGrounded || (isLocalMovement && flightEnabled);
	}
}
