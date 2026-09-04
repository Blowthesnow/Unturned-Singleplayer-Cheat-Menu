using System;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class MovementCheatService
{
	private readonly ManualLogSource _log;
	private readonly Func<bool> _canExecuteGameplay;

	private bool _hasOriginalMultipliers;

	private float _originalItemGravity;

	private float _originalGravity;

	private float _originalSpeed;

	private float _originalJump;

	private bool _controllerWasEnabled;

	private CharacterController _controller;

	private bool _isResimulating;

	internal bool FlightEnabled { get; private set; }

	internal bool NoclipEnabled { get; private set; }

	internal FlightMode Mode { get; private set; } = FlightMode.WorldAxis;

	internal float FlightSpeed { get; set; } = 3f;

	internal float VerticalSpeed { get; set; } = 2f;

	internal float HorizontalMetersPerSecond => MovementSpeedProfile.GetMetersPerSecond(FlightSpeed);

	internal float VerticalMetersPerSecond => MovementSpeedProfile.GetMetersPerSecond(VerticalSpeed);

	internal bool SafeExit { get; set; } = true;

	internal MovementCheatService(ManualLogSource log, Func<bool> canExecuteGameplay)
	{
		_log = log;
		_canExecuteGameplay = canExecuteGameplay ?? throw new ArgumentNullException(nameof(canExecuteGameplay));
	}

	internal void SetSimulationResimulating(bool value)
	{
		_isResimulating = value;
	}

	internal void SetFlightMode(FlightMode mode)
	{
		if (!Enum.IsDefined(typeof(FlightMode), mode))
		{
			mode = FlightMode.WorldAxis;
		}
		if (Mode == mode)
		{
			return;
		}
		Mode = mode;
		_log.LogInfo($"[Movement] 飞行模式已切换为 {Mode.ToDisplayName()}。" );
	}

	internal void SetFlight(bool enabled)
	{
		if (enabled == FlightEnabled || (enabled && !_canExecuteGameplay()))
		{
			return;
		}
		FlightEnabled = enabled;
		if (enabled)
		{
			SaveOriginalMultipliers();
			ApplyNativeMovementState(Player.LocalPlayer?.movement);
			_log.LogInfo($"[Movement] Flight 已开启：水平 {FlightSpeed:0.0}x，" + $"垂直 {VerticalSpeed:0.0}x；已切换到原生模拟移动。");
			return;
		}
		RestoreMultipliers();
		if (NoclipEnabled)
		{
			SetNoclip(enabled: false);
		}
		_log.LogInfo("[Movement] Flight 已关闭，移动倍率已恢复。");
	}

	internal void SetNoclip(bool enabled)
	{
		if (enabled == NoclipEnabled || (enabled && !_canExecuteGameplay()))
		{
			return;
		}
		if (enabled && !FlightEnabled)
		{
			SetFlight(enabled: true);
		}
		NoclipEnabled = enabled;
		CharacterController characterController = Player.LocalPlayer?.movement?.controller;
		if (enabled)
		{
			if (characterController != null)
			{
				_controller = characterController;
				_controllerWasEnabled = characterController.enabled;
				characterController.enabled = false;
			}
			_log.LogInfo("[Movement] Noclip 已开启。");
			return;
		}
		CharacterController characterController2 = characterController ?? _controller;
		if (characterController2 != null)
		{
			characterController2.enabled = _controllerWasEnabled;
		}
		_controller = null;
		if (SafeExit)
		{
			TrySafeExit();
		}
		_log.LogInfo("[Movement] Noclip 已关闭。");
	}

	internal void Pump()
	{
		if (!FlightEnabled && !NoclipEnabled)
		{
			return;
		}
		if (_canExecuteGameplay())
		{
			PlayerLife life = Player.LocalPlayer.life;
			if (((object)life == null || !life.isDead) && !(Player.LocalPlayer.movement?.getVehicle() != null))
			{
				PlayerMovement movement = Player.LocalPlayer.movement;
				if (movement == null)
				{
					Restore();
					return;
				}
				ApplyNativeMovementState(movement);
				if (NoclipEnabled)
				{
					MaintainNoclipControllerDisabled(movement);
				}
				return;
			}
		}
		Restore();
	}

	internal void PrepareNativeSimulation(PlayerMovement movement)
	{
		if (_canExecuteGameplay() && IsLocalMovement(movement) && (FlightEnabled || NoclipEnabled))
		{
			ApplyNativeMovementState(movement);
			if (NoclipEnabled)
			{
				MaintainNoclipControllerDisabled(movement);
			}
		}
	}

	internal void SimulateMovement(PlayerMovement movement, float deltaTime)
	{
		if (!IsLocalMovement(movement) || (!FlightEnabled && !NoclipEnabled) || _isResimulating || !_canExecuteGameplay())
		{
			return;
		}
		PlayerLife life = Player.LocalPlayer.life;
		if (((object)life != null && life.isDead) || Player.LocalPlayer.movement?.getVehicle() != null)
		{
			return;
		}
		Player localPlayer = Player.LocalPlayer;
		if (NoclipEnabled)
		{
			MaintainNoclipControllerDisabled(movement);
			if (!IsMovementInputBlocked())
			{
				Vector3 horizontalInput = GetHorizontalInput(localPlayer);
				float verticalInput = GetVerticalInput();
				Vector3 vector = (horizontalInput * HorizontalMetersPerSecond + Vector3.up * (verticalInput * VerticalMetersPerSecond)) * Mathf.Max(deltaTime, 0.001f);
				if (vector.sqrMagnitude > 0f)
				{
					localPlayer.transform.position += vector;
				}
			}
		}
		else if (Mode == FlightMode.ViewDirection && !(movement.controller == null) && movement.controller.enabled && !IsMovementInputBlocked())
		{
			Vector3 velocity = GetViewDirectionVelocity(localPlayer);
			if (velocity.sqrMagnitude > 0.0001f)
			{
				movement.controller.Move(velocity * Mathf.Max(deltaTime, 0.001f));
			}
		}
		else if (!(movement.controller == null) && movement.controller.enabled && !IsMovementInputBlocked())
		{
			float verticalInput2 = GetVerticalInput();
			if (Mathf.Abs(verticalInput2) > 0.001f)
			{
				movement.controller.Move(Vector3.up * (verticalInput2 * VerticalMetersPerSecond) * Mathf.Max(deltaTime, 0.001f));
			}
		}
	}

	internal void Restore()
	{
		if (NoclipEnabled)
		{
			SetNoclip(enabled: false);
		}
		if (FlightEnabled)
		{
			SetFlight(enabled: false);
		}
	}

	private void SaveOriginalMultipliers()
	{
		if (!_hasOriginalMultipliers)
		{
			PlayerMovement playerMovement = Player.LocalPlayer?.movement;
			if (!(playerMovement == null))
			{
				_originalItemGravity = playerMovement.itemGravityMultiplier;
				_originalGravity = playerMovement.pluginGravityMultiplier;
				_originalSpeed = playerMovement.pluginSpeedMultiplier;
				_originalJump = playerMovement.pluginJumpMultiplier;
				_hasOriginalMultipliers = true;
			}
		}
	}

	private void ApplyNativeMovementState(PlayerMovement movement)
	{
		if (!(movement == null))
		{
			movement.itemGravityMultiplier = 0f;
			if (!Mathf.Approximately(movement.pluginGravityMultiplier, 0f))
			{
				movement.sendPluginGravityMultiplier(0f);
			}
			// View-direction flight supplies its own complete velocity; disable native
			// horizontal movement to avoid moving twice during the same simulation tick.
			float num = (Mode == FlightMode.ViewDirection) ? 0f : MovementSpeedProfile.NormalizeMultiplier(FlightSpeed);
			if (!Mathf.Approximately(movement.pluginSpeedMultiplier, num))
			{
				movement.sendPluginSpeedMultiplier(num);
			}
			if (!Mathf.Approximately(movement.pluginJumpMultiplier, 0f))
			{
				movement.sendPluginJumpMultiplier(0f);
			}
		}
	}

	private void MaintainNoclipControllerDisabled(PlayerMovement movement)
	{
		CharacterController characterController = movement?.controller;
		if (!(characterController == null))
		{
			if (_controller == null)
			{
				_controller = characterController;
				_controllerWasEnabled = characterController.enabled;
			}
			if (characterController.enabled)
			{
				characterController.enabled = false;
			}
		}
	}

	private void RestoreMultipliers()
	{
		PlayerMovement playerMovement = Player.LocalPlayer?.movement;
		if (_hasOriginalMultipliers)
		{
			if (playerMovement != null)
			{
				playerMovement.itemGravityMultiplier = _originalItemGravity;
				playerMovement.sendPluginGravityMultiplier(_originalGravity);
				playerMovement.sendPluginSpeedMultiplier(_originalSpeed);
				playerMovement.sendPluginJumpMultiplier(_originalJump);
			}
			_hasOriginalMultipliers = false;
		}
	}

	private bool IsLocalMovement(PlayerMovement movement)
	{
		if (movement != null)
		{
			return (object)movement == Player.LocalPlayer?.movement;
		}
		return false;
	}

	private bool IsMovementInputBlocked()
	{
		CheatMenuPlugin instance = CheatMenuPlugin.Instance;
		if ((object)instance == null || !instance.IsMenuOpen)
		{
			return PlayerUI.window?.showCursor ?? false;
		}
		return true;
	}

	private static Vector3 GetHorizontalInput(Player player)
	{
		Vector3 vector = player.look?.aim?.forward ?? player.transform.forward;
		vector.y = 0f;
		if (vector.sqrMagnitude < 0.001f)
		{
			vector = player.transform.forward;
		}
		vector.Normalize();
		Vector3 normalized = Vector3.Cross(Vector3.up, vector).normalized;
		Vector3 result = vector * (Input.GetKey(KeyCode.W) ? 1f : 0f) - vector * (Input.GetKey(KeyCode.S) ? 1f : 0f) + normalized * (Input.GetKey(KeyCode.D) ? 1f : 0f) - normalized * (Input.GetKey(KeyCode.A) ? 1f : 0f);
		if (result.sqrMagnitude > 1f)
		{
			result.Normalize();
		}
		return result;
	}

	private Vector3 GetViewDirectionVelocity(Player player)
	{
		Vector3 forward = player.look?.aim?.forward ?? player.transform.forward;
		Vector3 fallbackForward = player.transform.forward;
		float forwardInput = (InputEx.GetKey(ControlsSettings.up) ? 1f : 0f) -
			(InputEx.GetKey(ControlsSettings.down) ? 1f : 0f);
		float strafeInput = (InputEx.GetKey(ControlsSettings.right) ? 1f : 0f) -
			(InputEx.GetKey(ControlsSettings.left) ? 1f : 0f);
		float verticalInput = GetVerticalInput();
		FlightDirectionVector velocity = FlightDirectionPolicy.ResolveVelocity(
			new FlightDirectionVector(forward.x, forward.y, forward.z),
			new FlightDirectionVector(fallbackForward.x, fallbackForward.y, fallbackForward.z),
			forwardInput, strafeInput, verticalInput,
			HorizontalMetersPerSecond, VerticalMetersPerSecond);
		return new Vector3(velocity.X, velocity.Y, velocity.Z);
	}

	private static float GetVerticalInput()
	{
		bool ascend = Input.GetKey(KeyCode.Space);
		bool descend = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
		if (ascend == descend)
		{
			return 0f;
		}
		if (!ascend)
		{
			return -1f;
		}
		return 1f;
	}

	private void TrySafeExit()
	{
		Player localPlayer = Player.LocalPlayer;
		if (localPlayer == null || localPlayer.adjustStanceOrTeleportIfStuck() || localPlayer.stance.wouldHaveHeightClearanceAtPosition(localPlayer.transform.position, 0.5f))
		{
			return;
		}
		Vector3 position = localPlayer.transform.position;
		for (int i = 0; i <= 4; i++)
		{
			int num = ((i == 0) ? 1 : 8);
			for (int j = 0; j < num; j++)
			{
				float f = (float)j * MathF.PI * 2f / (float)num;
				Vector3 vector = new Vector3(Mathf.Cos(f) * (float)i, 0f, Mathf.Sin(f) * (float)i);
				for (int k = 0; k <= 6; k++)
				{
					Vector3 vector2 = position + vector + Vector3.up * k;
					if (Level.checkSafeIncludingClipVolumes(vector2) && localPlayer.stance.wouldHaveHeightClearanceAtPosition(vector2, 0.5f))
					{
						localPlayer.teleportToLocation(vector2, localPlayer.transform.rotation.eulerAngles.y);
						return;
					}
				}
			}
		}
		_log.LogWarning("[Movement] 关闭 Noclip 后未找到安全落点；保留当前位置，请使用传送功能脱困。");
	}
}
