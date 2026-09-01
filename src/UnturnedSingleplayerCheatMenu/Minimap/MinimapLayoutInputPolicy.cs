using System;

namespace UnturnedSingleplayerCheatMenu.Minimap;

[Flags]
internal enum MinimapLayoutReservedKey
{
	None = 0,
	Return = 1 << 0,
	KeypadEnter = 1 << 1,
	Escape = 1 << 2,
	End = 1 << 3
}

internal sealed class MinimapLayoutInputState
{
	private bool _editing;
	private int _blockThroughFrame = -1;
	private MinimapLayoutReservedKey _pendingKeyUps;
	private int _pendingKeyUpsExpireFrame = -1;

	internal bool IsEditing => _editing;

	internal void SetEditing(bool active, int frame)
	{
		if (_editing == active) return;
		if (_editing && !active)
		{
			_blockThroughFrame = Math.Max(_blockThroughFrame, frame);
		}
		_editing = active;
	}

	internal void SuppressUntilRelease(MinimapLayoutReservedKey key, int frame)
	{
		if (key == MinimapLayoutReservedKey.None) return;
		_pendingKeyUps |= key;
		_pendingKeyUpsExpireFrame = Math.Max(_pendingKeyUpsExpireFrame, frame + 4);
		_blockThroughFrame = Math.Max(_blockThroughFrame, frame);
	}

	internal bool ShouldBlockGameKeyQueries(int frame)
	{
		return _editing || frame <= _blockThroughFrame;
	}

	internal bool ShouldBlockChatAction(int frame)
	{
		if (frame > _pendingKeyUpsExpireFrame)
		{
			_pendingKeyUps = MinimapLayoutReservedKey.None;
			_pendingKeyUpsExpireFrame = -1;
		}
		const MinimapLayoutReservedKey enterKeys =
			MinimapLayoutReservedKey.Return | MinimapLayoutReservedKey.KeypadEnter;
		bool pendingEnterRelease = (_pendingKeyUps & enterKeys) != 0;
		if (pendingEnterRelease)
		{
			_pendingKeyUps &= ~enterKeys;
		}
		return ShouldBlockGameKeyQueries(frame) || pendingEnterRelease;
	}

	internal void Reset()
	{
		_editing = false;
		_blockThroughFrame = -1;
		_pendingKeyUps = MinimapLayoutReservedKey.None;
		_pendingKeyUpsExpireFrame = -1;
	}
}
