using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal static class MinimapLayoutInputGate
{
	private static readonly MinimapLayoutInputState State = new MinimapLayoutInputState();

	internal static bool ShouldBlockGameKeyQueries => State.ShouldBlockGameKeyQueries(Time.frameCount);

	internal static void SetEditing(bool active)
	{
		State.SetEditing(active, Time.frameCount);
	}

	internal static void SuppressUntilRelease(KeyCode key)
	{
		State.SuppressUntilRelease(ToReservedKey(key), Time.frameCount);
	}

	internal static bool ShouldBlockChatAction()
	{
		return State.ShouldBlockChatAction(Time.frameCount);
	}

	internal static void Reset()
	{
		State.Reset();
	}

	private static MinimapLayoutReservedKey ToReservedKey(KeyCode key)
	{
		return key switch
		{
			KeyCode.Return => MinimapLayoutReservedKey.Return,
			KeyCode.KeypadEnter => MinimapLayoutReservedKey.KeypadEnter,
			KeyCode.Escape => MinimapLayoutReservedKey.Escape,
			KeyCode.End => MinimapLayoutReservedKey.End,
			_ => MinimapLayoutReservedKey.None
		};
	}
}
