using UnityEngine;
using SDG.Unturned;
using UnityEngine.EventSystems;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>Owns the plugin's cursor and native Sleek window input capture.</summary>
internal sealed class UiInputCaptureController
{
	internal event System.Action NativeUiChanged;
	internal event System.Action<UiInputOwnershipState> StateChanged;

	private SleekWindow _capturedWindow;
	private bool _windowWasEnabled;
	private bool _windowWasShowingCursor;
	private bool _hasCapturedUnityCursor;
	private bool _cursorWasVisible;
	private CursorLockMode _cursorWasLocked;
	private GameObject _selectedBeforeCapture;
	private bool _windowOverrideApplied;
	private bool _nativeUiRequestPending;

	internal UiInputOwnershipState State { get; private set; } = UiInputOwnershipState.Hidden;

	internal void Capture()
	{
		if (!_hasCapturedUnityCursor)
		{
			_cursorWasVisible = Cursor.visible;
			_cursorWasLocked = Cursor.lockState;
			_hasCapturedUnityCursor = true;
			_selectedBeforeCapture = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
		}
		SleekWindow window = PlayerUI.window;
		if (window == null)
		{
			Maintain();
			return;
		}
		UiLifecyclePolicy.ReplaceBorrowed(ref _capturedWindow, window, _ => ReleaseCapturedWindow(ShouldRestoreBorrowedWindow()), CaptureWindow);
		Maintain(allowNativeUiShortcut: false);
	}

	internal void Maintain(bool allowNativeUiShortcut = true)
	{
		if (!_hasCapturedUnityCursor)
		{
			SetState(UiInputOwnershipState.Hidden);
			return;
		}
		if (_hasCapturedUnityCursor)
		{
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = false;
		}
		SleekWindow window = PlayerUI.window;
		if (window == null)
		{
			SetState(UiLifecyclePolicy.ResolveInputOwnership(pluginWindowOpen: true, IsNativeUiActive()));
			return;
		}
		if (_capturedWindow != window)
		{
			// PlayerUI replaces its SleekWindow during scene/player transitions. Restore only
			// the borrowed window state; keep the original Unity cursor snapshot captured.
			UiLifecyclePolicy.ReplaceBorrowed(ref _capturedWindow, window, _ => ReleaseCapturedWindow(ShouldRestoreBorrowedWindow()), CaptureWindow);
		}
		bool nativeUiActive = IsNativeUiActive();
		if (!nativeUiActive && allowNativeUiShortcut && State == UiInputOwnershipState.ModalPluginUi && IsNativeUiRequested())
		{
			if (_windowOverrideApplied)
			{
				window.isEnabled = _windowWasEnabled;
				window.showCursor = _windowWasShowingCursor;
			}
			_windowOverrideApplied = false;
			_nativeUiRequestPending = true;
			SetState(UiInputOwnershipState.NativeUiCoexistence);
			return;
		}
		if (nativeUiActive)
		{
			_nativeUiRequestPending = false;
			_windowOverrideApplied = false;
			SetState(UiInputOwnershipState.NativeUiCoexistence);
			return;
		}
		if (_nativeUiRequestPending)
		{
			// Give PlayerUI.Update one complete frame to consume the requested native menu key.
			_nativeUiRequestPending = false;
			return;
		}
		if (State == UiInputOwnershipState.NativeUiCoexistence)
		{
			// Native UI has finished. Capture the game-owned post-menu state before resuming
			// plugin modal ownership so closing the plugin restores the current game state.
			_windowWasEnabled = window.isEnabled;
			_windowWasShowingCursor = window.showCursor;
		}
		window.isEnabled = true;
		window.showCursor = true;
		_windowOverrideApplied = true;
		SetState(UiInputOwnershipState.ModalPluginUi);
	}

	internal void Release()
	{
		ReleaseCapturedWindow(ShouldRestoreBorrowedWindow());
		_nativeUiRequestPending = false;
		if (_hasCapturedUnityCursor)
		{
			Cursor.lockState = _cursorWasLocked;
			Cursor.visible = _cursorWasVisible;
			_hasCapturedUnityCursor = false;
			if (_selectedBeforeCapture != null && _selectedBeforeCapture.activeInHierarchy && EventSystem.current != null)
			{
				EventSystem.current.SetSelectedGameObject(_selectedBeforeCapture);
			}
			_selectedBeforeCapture = null;
		}
		SetState(UiInputOwnershipState.Hidden);
	}

	private void CaptureWindow(SleekWindow window)
	{
		_capturedWindow = window;
		_windowWasEnabled = window.isEnabled;
		_windowWasShowingCursor = window.showCursor;
		_windowOverrideApplied = false;
		NativeUiChanged?.Invoke();
	}

	private bool ShouldRestoreBorrowedWindow()
	{
		return _windowOverrideApplied && State != UiInputOwnershipState.NativeUiCoexistence && !IsNativeUiActive();
	}

	private void ReleaseCapturedWindow(bool restore)
	{
		if (restore && _capturedWindow != null)
		{
			_capturedWindow.isEnabled = _windowWasEnabled;
			_capturedWindow.showCursor = _windowWasShowingCursor;
		}
		_capturedWindow = null;
		_windowOverrideApplied = false;
	}

	private static bool IsNativeUiActive()
	{
		// Dashboard owns inventory, crafting, skills, map, quests, and player-list views.
		return PlayerDashboardUI.active || PlayerPauseUI.active ||
			MenuConfigurationOptionsUI.active || MenuConfigurationDisplayUI.active ||
			MenuConfigurationGraphicsUI.active || MenuConfigurationControlsUI.active;
	}

	private static bool IsNativeUiRequested()
	{
		return InputEx.GetKeyDown(ControlsSettings.dashboard) ||
			InputEx.GetKeyDown(ControlsSettings.inventory) ||
			InputEx.GetKeyDown(ControlsSettings.crafting) ||
			InputEx.GetKeyDown(ControlsSettings.skills) ||
			InputEx.GetKeyDown(ControlsSettings.map) ||
			InputEx.GetKeyDown(ControlsSettings.quests) ||
			InputEx.GetKeyDown(ControlsSettings.players);
	}

	private void SetState(UiInputOwnershipState state)
	{
		if (State == state)
		{
			return;
		}
		State = state;
		StateChanged?.Invoke(state);
	}
}
