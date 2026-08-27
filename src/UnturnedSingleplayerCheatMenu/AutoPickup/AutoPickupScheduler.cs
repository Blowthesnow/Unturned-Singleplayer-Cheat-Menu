using System;
using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.AutoPickup;

internal sealed class AutoPickupScheduler
{
	private sealed class FailureState
	{
		public int Count;

		public float RetryAt;
	}

	private readonly Dictionary<uint, FailureState> _failures = new Dictionary<uint, FailureState>();

	private float _nextScanAt;

	private float _requestTokens = 1f;

	private float _lastTokenTime;

	public bool ShouldScan(float now, float frequencyHz)
	{
		frequencyHz = Math.Max(AutoPickupSettings.MinFrequencyHz, Math.Min(AutoPickupSettings.MaxFrequencyHz, frequencyHz));
		if (now < _nextScanAt)
		{
			return false;
		}
		_nextScanAt = now + 1f / frequencyHz;
		return true;
	}

	public bool CanRequest(uint instanceId, float now)
	{
		return !_failures.TryGetValue(instanceId, out FailureState state) || now >= state.RetryAt;
	}

	public int TakeRequestBudget(float now, int desired)
	{
		desired = Math.Max(0, Math.Min(AutoPickupSettings.MaxRequestsPerTick, desired));
		float elapsed = Math.Max(0f, now - _lastTokenTime);
		_lastTokenTime = now;
		_requestTokens = Math.Min(10f, _requestTokens + elapsed * 10f);
		int granted = Math.Min(desired, (int)_requestTokens);
		_requestTokens -= granted;
		return granted;
	}

	public float MarkFailure(uint instanceId, float now)
	{
		if (!_failures.TryGetValue(instanceId, out FailureState state))
		{
			state = new FailureState();
			_failures.Add(instanceId, state);
		}
		state.Count = Math.Min(state.Count + 1, 4);
		float delay = 0.25f * (1 << (state.Count - 1));
		state.RetryAt = now + Math.Min(2f, delay);
		return state.RetryAt;
	}

	public float MarkCapacityBlocked(uint instanceId, float now)
	{
		if (!_failures.TryGetValue(instanceId, out FailureState state))
		{
			state = new FailureState();
			_failures.Add(instanceId, state);
		}
		state.RetryAt = now + 5f;
		return state.RetryAt;
	}

	public void MarkSuccess(uint instanceId)
	{
		_failures.Remove(instanceId);
	}

	public void Reset(float now = 0f)
	{
		_failures.Clear();
		_nextScanAt = now;
		_requestTokens = 1f;
		_lastTokenTime = now;
	}
}
