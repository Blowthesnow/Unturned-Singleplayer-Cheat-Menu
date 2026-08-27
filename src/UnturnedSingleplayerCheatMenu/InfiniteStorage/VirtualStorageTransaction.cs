using System;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage;

/// <summary>In-memory storage transaction with an all-or-nothing persistence commit.</summary>
internal sealed class VirtualStorageTransaction
{
	private readonly VirtualStorageService _storage;
	private readonly VirtualStorageDocument _before;
	private bool _completed;

	internal VirtualStorageTransaction(VirtualStorageService storage, string worldKey, string playerKey)
	{
		_storage = storage ?? throw new ArgumentNullException(nameof(storage));
		_before = storage.Snapshot(worldKey, playerKey);
	}

	internal bool Add(ItemStorageDescriptor descriptor)
	{
		return !_completed && _storage.Add(descriptor).Success;
	}

	internal bool Remove(ItemStorageDescriptor descriptor, long units)
	{
		return !_completed && _storage.Remove(descriptor, units);
	}

	internal bool Remove(string entryId, long count)
	{
		return !_completed && _storage.Remove(entryId, count).Success;
	}

	internal bool Commit(Func<bool> persist)
	{
		if (_completed || persist == null)
		{
			return false;
		}
		if (!persist())
		{
			Rollback();
			return false;
		}
		_completed = true;
		return true;
	}

	internal void Rollback()
	{
		if (_completed)
		{
			return;
		}
		_storage.RestoreSnapshot(_before);
		_completed = true;
	}
}
