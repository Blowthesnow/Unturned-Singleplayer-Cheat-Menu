using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>Maintains draft/committed search text and keyboard lifecycle on the unified runtime-driver tick.</summary>
internal sealed class UiSearchInputController : IDisposable
{
	private readonly InputField _input;
	private readonly UiDebouncer _debouncer;
	private readonly Action<string> _apply;
	private readonly UnityAction<string> _valueChanged;
	private readonly UiSearchEditState _state;
	private bool _wasFocused;
	private bool _wasComposing;
	private bool _suppress;

	internal UiSearchInputController(InputField input, Action<string> apply, float delaySeconds = 0.15f)
	{
		_input = input ?? throw new ArgumentNullException(nameof(input));
		_apply = apply ?? throw new ArgumentNullException(nameof(apply));
		_debouncer = new UiDebouncer(delaySeconds);
		_state = new UiSearchEditState(input.text);
		_wasFocused = input.isFocused;
		_valueChanged = OnValueChanged;
		_input.onValueChanged.AddListener(_valueChanged);
	}

	internal bool IsAlive => _input != null;
	internal bool IsFocused => _input != null && _input.isFocused;

	internal bool Pump()
	{
		if (_input == null)
		{
			return false;
		}
		bool consumedCancel = false;
		bool focused = _input.isFocused;
		bool composing = !string.IsNullOrEmpty(Input.compositionString);
		if (focused && !composing && Input.GetKeyDown(KeyCode.Escape))
		{
			Revert();
			_input.DeactivateInputField();
			consumedCancel = true;
		}
		else if (focused && !composing && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
		{
			Commit(_input.text);
			_input.DeactivateInputField();
		}
		else if (_wasFocused && !focused)
		{
			Commit(_input.text);
		}
		if (_wasComposing && !composing && focused)
		{
			Schedule(_input.text);
		}
		_wasFocused = focused;
		_wasComposing = composing;
		_debouncer.Tick();
		return consumedCancel;
	}

	private void OnValueChanged(string value)
	{
		if (_suppress || !string.IsNullOrEmpty(Input.compositionString))
		{
			return;
		}
		Schedule(value);
	}

	private void Schedule(string value)
	{
		_state.ChangeDraft(value);
		_debouncer.Schedule(delegate
		{
			if (_state.ApplyPreview(out string draft))
			{
				_apply(draft);
			}
		});
	}

	private void Commit(string value)
	{
		_debouncer.Cancel();
		if (_state.Commit(value, out string committed))
		{
			_apply(committed);
		}
	}

	private void Revert()
	{
		_debouncer.Cancel();
		bool apply = _state.Revert(out string committed);
		_suppress = true;
		try
		{
			_input.text = committed;
		}
		finally
		{
			_suppress = false;
		}
		if (apply)
		{
			_apply(committed);
		}
	}

	public void Dispose()
	{
		_input?.onValueChanged.RemoveListener(_valueChanged);
		_debouncer.Dispose();
	}
}
