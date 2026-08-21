using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class MovementCheatService
{
    private readonly ManualLogSource _log;
    private bool _hasOriginalMultipliers;
    private float _originalItemGravity;
    private float _originalGravity;
    private float _originalSpeed;
    private float _originalJump;
    private bool _controllerWasEnabled;
    private CharacterController _controller;
    private bool _isResimulating;

    internal MovementCheatService(ManualLogSource log)
    {
        _log = log;
    }

    internal bool FlightEnabled { get; private set; }
    internal bool NoclipEnabled { get; private set; }
    internal float FlightSpeed { get; set; } = 3f;
    internal float VerticalSpeed { get; set; } = 2f;
    internal float HorizontalMetersPerSecond =>
        MovementSpeedProfile.GetMetersPerSecond(FlightSpeed);
    internal float VerticalMetersPerSecond =>
        MovementSpeedProfile.GetMetersPerSecond(VerticalSpeed);
    internal bool SafeExit { get; set; } = true;

    internal void SetSimulationResimulating(bool value)
    {
        _isResimulating = value;
    }

    internal void SetFlight(bool enabled)
    {
        if (enabled == FlightEnabled)
            return;
        if (enabled && !SingleplayerGuard.IsReady)
            return;

        FlightEnabled = enabled;
        if (enabled)
        {
            SaveOriginalMultipliers();
            ApplyNativeMovementState(Player.LocalPlayer?.movement);
            _log.LogInfo(
                $"[Movement] Flight 已开启：水平 {FlightSpeed:0.0}x，"
                + $"垂直 {VerticalSpeed:0.0}x；已切换到原生模拟移动。");
        }
        else
        {
            RestoreMultipliers();
            if (NoclipEnabled)
                SetNoclip(false);
            _log.LogInfo("[Movement] Flight 已关闭，移动倍率已恢复。");
        }
    }

    internal void SetNoclip(bool enabled)
    {
        if (enabled == NoclipEnabled)
            return;
        if (enabled && !SingleplayerGuard.IsReady)
            return;
        if (enabled && !FlightEnabled)
            SetFlight(true);

        NoclipEnabled = enabled;
        CharacterController controller = Player.LocalPlayer?.movement?.controller;
        if (enabled)
        {
            if (controller != null)
            {
                _controller = controller;
                _controllerWasEnabled = controller.enabled;
                controller.enabled = false;
            }
            _log.LogInfo("[Movement] Noclip 已开启。");
        }
        else
        {
            CharacterController restoreController = controller ?? _controller;
            if (restoreController != null)
                restoreController.enabled = _controllerWasEnabled;
            _controller = null;
            if (SafeExit)
                TrySafeExit();
            _log.LogInfo("[Movement] Noclip 已关闭。");
        }
    }

    internal void Pump()
    {
        if (!FlightEnabled && !NoclipEnabled)
            return;
        if (!SingleplayerGuard.IsReady
            || Player.LocalPlayer.life?.isDead == true
            || Player.LocalPlayer.movement?.getVehicle() != null)
        {
            Restore();
            return;
        }

        Player player = Player.LocalPlayer;
        PlayerMovement movement = player.movement;
        if (movement == null)
        {
            Restore();
            return;
        }

        ApplyNativeMovementState(movement);
        if (NoclipEnabled)
            MaintainNoclipControllerDisabled(movement);
    }

    /// <summary>
    /// Applies the plugin's native movement state before PlayerMovement.simulate.
    /// The actual displacement is performed by the game's simulation loop, not by
    /// the render-frame maintenance callback.
    /// </summary>
    internal void PrepareNativeSimulation(PlayerMovement movement)
    {
        if (!IsLocalMovement(movement) || !FlightEnabled && !NoclipEnabled)
            return;

        ApplyNativeMovementState(movement);
        if (NoclipEnabled)
            MaintainNoclipControllerDisabled(movement);
    }

    /// <summary>
    /// Runs after the game's native movement simulation. Flight keeps native
    /// horizontal movement and adds only vertical input here. Noclip moves the
    /// complete vector at the same fixed simulation cadence while the controller
    /// remains disabled.
    /// </summary>
    internal void SimulateMovement(PlayerMovement movement, float deltaTime)
    {
        if (!IsLocalMovement(movement)
            || (!FlightEnabled && !NoclipEnabled)
            || _isResimulating
            || !SingleplayerGuard.IsReady
            || Player.LocalPlayer.life?.isDead == true
            || Player.LocalPlayer.movement?.getVehicle() != null)
            return;

        Player player = Player.LocalPlayer;
        if (NoclipEnabled)
        {
            MaintainNoclipControllerDisabled(movement);
            if (IsMovementInputBlocked())
                return;

            Vector3 horizontal = GetHorizontalInput(player);
            float vertical = GetVerticalInput();
            Vector3 delta =
                (horizontal * HorizontalMetersPerSecond
                 + Vector3.up * (vertical * VerticalMetersPerSecond))
                * Mathf.Max(deltaTime, 0.001f);
            if (delta.sqrMagnitude > 0f)
                player.transform.position += delta;
            return;
        }

        if (movement.controller == null
            || !movement.controller.enabled
            || IsMovementInputBlocked())
            return;

        float verticalInput = GetVerticalInput();
        if (Mathf.Abs(verticalInput) > 0.001f)
        {
            movement.controller.Move(
                Vector3.up
                * (verticalInput * VerticalMetersPerSecond)
                * Mathf.Max(deltaTime, 0.001f));
        }
    }

    internal void Restore()
    {
        if (NoclipEnabled)
            SetNoclip(false);
        if (FlightEnabled)
            SetFlight(false);
    }

    private void SaveOriginalMultipliers()
    {
        if (_hasOriginalMultipliers)
            return;
        PlayerMovement movement = Player.LocalPlayer?.movement;
        if (movement == null)
            return;
        _originalItemGravity = movement.itemGravityMultiplier;
        _originalGravity = movement.pluginGravityMultiplier;
        _originalSpeed = movement.pluginSpeedMultiplier;
        _originalJump = movement.pluginJumpMultiplier;
        _hasOriginalMultipliers = true;
    }

    private void ApplyNativeMovementState(PlayerMovement movement)
    {
        if (movement == null)
            return;

        // Follow the Workshop levitation tools' architecture: keep movement in
        // Unturned's PlayerMovement.simulate loop. Gravity is neutralized here
        // so Space/Ctrl can provide a separately controlled vertical speed in
        // SimulateMovement, while WASD uses Unturned's native speed calculation.
        movement.itemGravityMultiplier = 0f;
        if (!Mathf.Approximately(movement.pluginGravityMultiplier, 0f))
            movement.sendPluginGravityMultiplier(0f);
        float normalizedSpeed = MovementSpeedProfile.NormalizeMultiplier(FlightSpeed);
        if (!Mathf.Approximately(movement.pluginSpeedMultiplier, normalizedSpeed))
            movement.sendPluginSpeedMultiplier(normalizedSpeed);
        if (!Mathf.Approximately(movement.pluginJumpMultiplier, 0f))
            movement.sendPluginJumpMultiplier(0f);
    }

    private void MaintainNoclipControllerDisabled(PlayerMovement movement)
    {
        CharacterController controller = movement?.controller;
        if (controller == null)
            return;

        if (_controller == null)
        {
            _controller = controller;
            _controllerWasEnabled = controller.enabled;
        }

        if (controller.enabled)
            controller.enabled = false;
    }

    private void RestoreMultipliers()
    {
        PlayerMovement movement = Player.LocalPlayer?.movement;
        if (!_hasOriginalMultipliers)
            return;

        if (movement != null)
        {
            movement.itemGravityMultiplier = _originalItemGravity;
            movement.sendPluginGravityMultiplier(_originalGravity);
            movement.sendPluginSpeedMultiplier(_originalSpeed);
            movement.sendPluginJumpMultiplier(_originalJump);
        }

        // When the player object has already been destroyed there is no target
        // left to restore. Clear the snapshot so it cannot leak into the next
        // singleplayer world.
        _hasOriginalMultipliers = false;
    }

    private bool IsLocalMovement(PlayerMovement movement)
    {
        return movement != null && ReferenceEquals(movement, Player.LocalPlayer?.movement);
    }

    private bool IsMovementInputBlocked()
    {
        return CheatMenuPlugin.Instance?.IsMenuOpen == true
            || PlayerUI.window?.showCursor == true;
    }

    private static Vector3 GetHorizontalInput(Player player)
    {
        Vector3 forward = player.look?.aim?.forward ?? player.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = player.transform.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 horizontal =
            forward * (Input.GetKey(KeyCode.W) ? 1f : 0f)
            - forward * (Input.GetKey(KeyCode.S) ? 1f : 0f)
            + right * (Input.GetKey(KeyCode.D) ? 1f : 0f)
            - right * (Input.GetKey(KeyCode.A) ? 1f : 0f);
        if (horizontal.sqrMagnitude > 1f)
            horizontal.Normalize();
        return horizontal;
    }

    private static float GetVerticalInput()
    {
        bool ascend = Input.GetKey(KeyCode.Space);
        bool descend = Input.GetKey(KeyCode.LeftControl)
            || Input.GetKey(KeyCode.RightControl);
        if (ascend == descend)
            return 0f;
        return ascend ? 1f : -1f;
    }

    private void TrySafeExit()
    {
        Player player = Player.LocalPlayer;
        if (player == null)
            return;

        if (player.adjustStanceOrTeleportIfStuck())
            return;
        if (player.stance.wouldHaveHeightClearanceAtPosition(player.transform.position, 0.5f))
            return;

        Vector3 origin = player.transform.position;
        for (int radius = 0; radius <= 4; radius++)
        {
            int samples = radius == 0 ? 1 : 8;
            for (int sample = 0; sample < samples; sample++)
            {
                float angle = sample * Mathf.PI * 2f / samples;
                Vector3 horizontal = new(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                for (int vertical = 0; vertical <= 6; vertical++)
                {
                    Vector3 candidate = origin + horizontal + Vector3.up * vertical;
                    if (!Level.checkSafeIncludingClipVolumes(candidate)
                        || !player.stance.wouldHaveHeightClearanceAtPosition(candidate, 0.5f))
                        continue;
                    player.teleportToLocation(candidate, player.transform.rotation.eulerAngles.y);
                    return;
                }
            }
        }

        _log.LogWarning("[Movement] 关闭 Noclip 后未找到安全落点；保留当前位置，请使用传送功能脱困。");
    }
}
