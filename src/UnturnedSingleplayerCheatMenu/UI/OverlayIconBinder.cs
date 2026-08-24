using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class OverlayIconBinder : MonoBehaviour
{
	private static readonly List<OverlayIconBinder> Pending = new List<OverlayIconBinder>();

	private RawImage _target;

	private Text _previewState;

	private Func<Texture2D> _loader;

	private bool _isVehicle;

	private float _nextAttempt;

	private int _attemptCount;

	public void Initialize(RawImage target, Text previewState, Func<Texture2D> loader, bool isVehicle)
	{
		_target = target;
		_previewState = previewState;
		_loader = loader;
		_isVehicle = isVehicle;
		if (!Pending.Contains(this))
		{
			Pending.Add(this);
		}
		TryLoad();
	}

	private void Update()
	{
		PumpIfDue();
	}

	internal static void PumpPending()
	{
		for (int num = Pending.Count - 1; num >= 0; num--)
		{
			OverlayIconBinder overlayIconBinder = Pending[num];
			if (overlayIconBinder == null || overlayIconBinder._loader == null)
			{
				Pending.RemoveAt(num);
			}
			else
			{
				overlayIconBinder.PumpIfDue();
				if (overlayIconBinder == null || overlayIconBinder._loader == null)
				{
					Pending.RemoveAt(num);
				}
			}
		}
	}

	private void PumpIfDue()
	{
		if (_loader != null && !(Time.unscaledTime < _nextAttempt))
		{
			TryLoad();
		}
	}

	private void TryLoad()
	{
		_attemptCount++;
		_nextAttempt = Time.unscaledTime + ((_attemptCount >= 12) ? 1f : 0.25f);
		Texture2D texture2D = _loader?.Invoke();
		if (texture2D == null)
		{
			if (_previewState != null && _attemptCount >= 12)
			{
				_previewState.text = PluginLocalization.Translate("无预览");
			}
			return;
		}
		_target.texture = texture2D;
		if (_previewState != null)
		{
			_previewState.gameObject.SetActive(value: false);
		}
		if (_isVehicle)
		{
			CheatMenuPlugin.Instance?.LogVehicleIconBound(texture2D);
		}
		_loader = null;
		base.enabled = false;
	}

	private void OnDestroy()
	{
		Pending.Remove(this);
		_target = null;
		_previewState = null;
		_loader = null;
		_isVehicle = false;
	}
}
