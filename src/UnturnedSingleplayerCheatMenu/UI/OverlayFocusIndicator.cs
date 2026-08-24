using UnityEngine;
using UnityEngine.EventSystems;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class OverlayFocusIndicator : MonoBehaviour, ISelectHandler, IEventSystemHandler, IDeselectHandler
{
	private GameObject _indicator;

	public void Initialize(GameObject indicator)
	{
		_indicator = indicator;
		if (_indicator != null)
		{
			_indicator.SetActive(value: false);
		}
	}

	public void OnSelect(BaseEventData eventData)
	{
		if (_indicator != null)
		{
			_indicator.SetActive(value: true);
		}
	}

	public void OnDeselect(BaseEventData eventData)
	{
		if (_indicator != null)
		{
			_indicator.SetActive(value: false);
		}
	}
}
