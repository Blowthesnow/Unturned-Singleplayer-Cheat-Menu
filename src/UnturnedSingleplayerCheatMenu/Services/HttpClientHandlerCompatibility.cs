using System;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class HttpClientHandlerCompatibility
{
	internal static bool TrySetMaxConnectionsPerServer(Action<int> setter, int maxConnections)
	{
		if (setter == null)
		{
			throw new ArgumentNullException(nameof(setter));
		}
		try
		{
			setter(Math.Max(1, maxConnections));
			return true;
		}
		catch (NotImplementedException)
		{
			return false;
		}
		catch (PlatformNotSupportedException)
		{
			return false;
		}
	}
}
