using System;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Patches;
using UnturnedSingleplayerCheatMenu.UI;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class PointToolService
{
    private static readonly KeyboardShortcut MiddleMouseShortcut = new(KeyCode.Mouse2);

    private const int TeleportCoordinateMask = RayMasks.DEFAULT
        | RayMasks.LARGE
        | RayMasks.MEDIUM
        | RayMasks.ENVIRONMENT
        | RayMasks.GROUND
        | RayMasks.GROUND2
        | RayMasks.STRUCTURE;

    // Unturned's ResourceManager lookup is only valid for resource colliders.
    // Ordinary terrain is Layer 20 and must never enter that lookup path.
    private const int ResourceLayer = 14;

    private readonly ManualLogSource _log;
    private readonly PointToolHud _hud;
    private readonly NativeShortcutDetector _nativeMiddleMouse = new();
    private float _nextRaycast;
    private float _lastRaycastAt;
    private RaycastHit _currentHit;
    private Vector3 _currentPoint;
    private RaycastHit _smartCoordinateHit;
    private Vector3 _smartCoordinatePoint;
    private bool _smartHasCoordinateHit;
    private bool _smartSemanticHit;
    private bool _smartSemanticResolveSucceeded;
    private bool _smartCoordinatePhysicsHit;
    private bool _smartCoordinateSemanticFallback;
    private bool _smartCoordinateTerrainFallback;
    private PointToolRaycastSnapshot _smartSnapshot;
    private Vector3 _smartRayOrigin;
    private Vector3 _smartRayDirection;
    private int _smartCapturedFrame = -1;
    private bool _hasCurrentHit;
    private Target _currentTarget;
    private string _inspectionMessage;
    private float _inspectionMessageUntil;
    private bool _hasLoggedTerrainRaycastFailure;
    private bool _hasLoggedResourceResolutionFailure;
    private bool _hasPendingTeleportTrace;
    private float _pendingTeleportTraceAt;
    private Vector3 _pendingTeleportBefore;
    private Vector3 _pendingTeleportRequested;
    private Vector3 _pendingTeleportLanding;

    internal PointToolService(ManualLogSource log, PointToolHud hud)
    {
        _log = log;
        _hud = hud;
    }

    internal bool Enabled { get; set; }
    internal PointToolMode Mode { get; set; } = PointToolMode.Inspect;
    internal float Range { get; set; } = 100f;
    internal bool ShowTargetName { get; set; } = true;
    internal bool ShowId { get; set; } = true;
    internal bool ShowHealth { get; set; } = true;
    internal bool ShowHud { get; set; } = true;

    internal void Pump()
    {
        FlushPendingTeleportTrace();
        bool nativeMiddlePressed = _nativeMiddleMouse.IsPressed(MiddleMouseShortcut);

        if (!SingleplayerGuard.IsReady)
        {
            Enabled = false;
            ClearTarget();
            return;
        }
        if (!Enabled || CheatMenuPlugin.Instance?.IsMenuOpen == true)
        {
            ClearTarget();
            return;
        }

        bool unityMiddlePressed = Input.GetMouseButtonDown(2);
        bool middlePressed = unityMiddlePressed || nativeMiddlePressed;
        if (middlePressed)
        {
            if (nativeMiddlePressed && !unityMiddlePressed)
                _log.LogInfo("[PointToolInput] Unity missed middle click; native fallback accepted it.");

            bool useDisplayedTeleportHit = (Mode == PointToolMode.Teleport || Mode == PointToolMode.Smart)
                && _hasCurrentHit;
            if (!useDisplayedTeleportHit)
                RefreshRaycast();
            LogClickRaycastTrace();
            ExecuteCurrentTarget();
            return;
        }

        if (Time.unscaledTime < _nextRaycast)
        {
            ShowCurrentHud();
            return;
        }
        _nextRaycast = Time.unscaledTime + 1f / 30f;

        RefreshRaycast();
        ShowCurrentHud();
    }

    private void RefreshRaycast()
    {
        _lastRaycastAt = Time.unscaledTime;
        Camera camera = MainCamera.instance ?? Camera.main;
        Transform aim = Player.LocalPlayer?.look?.aim;
        Ray ray = camera != null
            ? camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : aim != null
                ? new Ray(aim.position, aim.forward)
                : default;
        if (camera == null && aim == null)
        {
            _hasCurrentHit = false;
            _currentHit = default;
            _currentPoint = default;
            _currentTarget = null;
            return;
        }

        float maxDistance = Mathf.Clamp(Range, 5f, 250f);
        if (Mode == PointToolMode.Smart)
        {
            RefreshSmartRaycast(ray, maxDistance);
            return;
        }

        int pointToolMask = Mode == PointToolMode.Teleport
            ? TeleportCoordinateMask
            : RayMasks.BLOCK_STANCE | RayMasks.DAMAGE_CLIENT;
        if (Physics.Raycast(
            ray,
            out _currentHit,
            maxDistance,
            pointToolMask,
            QueryTriggerInteraction.Ignore))
        {
            _hasCurrentHit = true;
            _currentPoint = _currentHit.point;
            _currentTarget = TryResolveTarget(_currentHit.collider, out _);
            return;
        }

        _currentHit = default;
        _hasCurrentHit = TryGetTerrainHit(ray, maxDistance, out _currentPoint);
        _currentTarget = null;
    }

    private void RefreshSmartRaycast(Ray ray, float maxDistance)
    {
        // Start a complete snapshot before either resolver runs. A failed
        // semantic lookup must not leave a previous target or coordinate in
        // the action gate, and it must never prevent the coordinate ray.
        _smartRayOrigin = ray.origin;
        _smartRayDirection = ray.direction;
        _smartCapturedFrame = Time.frameCount;
        _smartSemanticHit = false;
        _smartSemanticResolveSucceeded = false;
        _smartCoordinatePhysicsHit = false;
        _smartCoordinateSemanticFallback = false;
        _smartCoordinateTerrainFallback = false;
        _smartHasCoordinateHit = false;
        _smartCoordinateHit = default;
        _smartCoordinatePoint = default;
        _currentHit = default;
        _currentPoint = default;
        _currentTarget = null;
        _hasCurrentHit = false;

        bool semanticHit = Physics.Raycast(
            ray,
            out RaycastHit semanticRaycastHit,
            maxDistance,
            RayMasks.BLOCK_STANCE | RayMasks.DAMAGE_CLIENT,
            QueryTriggerInteraction.Ignore);
        _smartSemanticHit = semanticHit;
        _smartSemanticResolveSucceeded = !semanticHit;
        Target semanticTarget = null;
        if (semanticHit)
        {
            semanticTarget = TryResolveTarget(
                semanticRaycastHit.collider,
                out _smartSemanticResolveSucceeded);
        }

        bool coordinatePhysicsHit = Physics.Raycast(
            ray,
            out _smartCoordinateHit,
            maxDistance,
            TeleportCoordinateMask,
            QueryTriggerInteraction.Ignore);
        _smartCoordinatePhysicsHit = coordinatePhysicsHit;
        _smartHasCoordinateHit = coordinatePhysicsHit;
        if (coordinatePhysicsHit)
            _smartCoordinatePoint = _smartCoordinateHit.point;
        else if (TryGetStaticSemanticCoordinateFallback(
            semanticHit,
            semanticRaycastHit,
            out _smartCoordinatePoint))
        {
            // Some map surfaces are visible to the semantic query but do not
            // expose the same collider to this coordinate query. A static
            // surface hit is safe to reuse; dynamic semantic targets are not.
            _smartCoordinateHit = semanticRaycastHit;
            _smartHasCoordinateHit = true;
            _smartCoordinateSemanticFallback = true;
        }
        else
        {
            _smartHasCoordinateHit = TryGetTerrainHit(ray, maxDistance, out _smartCoordinatePoint);
            _smartCoordinateTerrainFallback = _smartHasCoordinateHit;
        }

        _smartSnapshot = new PointToolRaycastSnapshot(
            _smartSemanticHit,
            _smartSemanticResolveSucceeded,
            _smartCoordinatePhysicsHit,
            _smartCoordinateTerrainFallback,
            _smartCoordinateSemanticFallback);
        _smartHasCoordinateHit = _smartSnapshot.HasCoordinateHit;
        _currentTarget = semanticTarget;
        _hasCurrentHit = _smartSnapshot.HasAnyHit;
        _currentPoint = _smartSemanticHit ? semanticRaycastHit.point : _smartCoordinatePoint;
        if (_smartSemanticHit)
            _currentHit = semanticRaycastHit;
        else if (_smartCoordinatePhysicsHit)
            _currentHit = _smartCoordinateHit;
    }

    private void LogClickRaycastTrace()
    {
        string colliderName = _currentHit.collider == null
            ? (_hasCurrentHit ? "TerrainHeightFallback" : "None")
            : _currentHit.collider.name;
        int colliderLayer = _currentHit.collider?.gameObject.layer ?? -1;
        _log.LogInfo(
            $"[PointTeleportClickTrace] mode={Mode}, hasHit={_hasCurrentHit}, "
            + $"point={_currentPoint}, normal={_currentHit.normal}, "
            + $"collider={colliderName}, layer={colliderLayer}.");
        if (Mode == PointToolMode.Smart)
        {
            string coordinateColliderName = _smartCoordinateHit.collider == null
                ? GetSmartCoordinateSource()
                : _smartCoordinateHit.collider.name;
            int coordinateLayer = _smartCoordinateHit.collider?.gameObject.layer ?? -1;
            _log.LogInfo(
                $"[PointSmartRaycastTrace] frame={_smartCapturedFrame}, "
                + $"semanticHit={_smartSemanticHit}, "
                + $"semanticResolveSucceeded={_smartSemanticResolveSucceeded}, "
                + $"coordinatePhysicsHit={_smartCoordinatePhysicsHit}, "
                + $"coordinateSemanticFallback={_smartCoordinateSemanticFallback}, "
                + $"coordinateFallback={_smartCoordinateTerrainFallback}, "
                + $"coordinateSource={GetSmartCoordinateSource()}, "
                + $"coordinateHit={_smartHasCoordinateHit}, "
                + $"coordinateCollider={coordinateColliderName}, "
                + $"coordinateLayer={coordinateLayer}, "
                + $"coordinatePoint={_smartCoordinatePoint}.");
        }
    }

    private string GetSmartCoordinateSource()
    {
        if (_smartCoordinatePhysicsHit)
            return "Physics";
        if (_smartCoordinateSemanticFallback)
            return "SemanticStaticFallback";
        if (_smartCoordinateTerrainFallback)
            return "TerrainHeightFallback";
        return "None";
    }

    private static bool TryGetStaticSemanticCoordinateFallback(
        bool semanticHit,
        RaycastHit semanticHitInfo,
        out Vector3 point)
    {
        point = default;
        Collider collider = semanticHitInfo.collider;
        if (!semanticHit || collider == null)
            return false;

        int layer = collider.gameObject.layer;
        // These are the static surface layers included by the coordinate ray.
        // In particular, do not accept Entity, Vehicle, Barricade, Resource,
        // or other semantic-only layers as a teleport coordinate.
        if (layer != 0
            && layer != 15
            && layer != 16
            && layer != 19
            && layer != 20
            && layer != 28
            && layer != 31)
            return false;

        int layerBit = 1 << layer;
        if ((TeleportCoordinateMask & layerBit) == 0)
            return false;

        point = semanticHitInfo.point;
        return true;
    }

    private bool TryGetTerrainHit(Ray ray, float maxDistance, out Vector3 hitPoint)
    {
        hitPoint = default;
        if (ray.direction.y >= -0.0001f)
            return false;

        try
        {
            const float sampleStep = 2f;
            const int refinementSteps = 8;
            float previousDistance = 0f;
            if (ray.origin.y - LevelGround.getHeight(ray.origin) <= 0f)
                return false;

            float distance = Mathf.Min(sampleStep, maxDistance);
            while (distance <= maxDistance)
            {
                Vector3 samplePoint = ray.GetPoint(distance);
                float sampleGap = samplePoint.y - LevelGround.getHeight(samplePoint);
                if (sampleGap <= 0f)
                {
                    float low = previousDistance;
                    float high = distance;
                    for (int i = 0; i < refinementSteps; i++)
                    {
                        float middle = (low + high) * 0.5f;
                        Vector3 middlePoint = ray.GetPoint(middle);
                        float middleGap = middlePoint.y - LevelGround.getHeight(middlePoint);
                        if (middleGap > 0f)
                            low = middle;
                        else
                            high = middle;
                    }

                    hitPoint = ray.GetPoint(high);
                    hitPoint.y = LevelGround.getHeight(hitPoint);
                    return true;
                }

                if (distance >= maxDistance)
                    break;

                previousDistance = distance;
                distance = Mathf.Min(distance + sampleStep, maxDistance);
            }
        }
        catch (Exception ex)
        {
            if (!_hasLoggedTerrainRaycastFailure)
            {
                _hasLoggedTerrainRaycastFailure = true;
                _log.LogWarning($"[PointTool] Failed to resolve terrain under the aim ray: {ex.Message}");
            }
        }

        return false;
    }

    private void ShowCurrentHud()
    {
        if (!ShowHud)
            return;

        if (Time.unscaledTime < _inspectionMessageUntil
            && !string.IsNullOrWhiteSpace(_inspectionMessage))
        {
            _hud.SetPoint(_inspectionMessage);
            return;
        }

        _inspectionMessage = null;
        _inspectionMessageUntil = 0f;
        _hud.SetPoint(_hasCurrentHit
            ? BuildHudText(_currentTarget)
            : BuildIdleHudText());
    }

    private string BuildIdleHudText()
    {
        if (Mode == PointToolMode.Smart)
        {
            return $"{PluginLocalization.Translate("准星交互工具")}\n"
                + $"[{PluginLocalization.Translate("智能 -> 无可用动作")}]\n"
                + PluginLocalization.Translate("未命中世界表面");
        }

        return $"{PluginLocalization.Translate("准星交互工具")}\n"
            + $"[{PluginLocalization.Translate(GetModeAction())}]\n"
            + PluginLocalization.Translate("触发键：鼠标中键；删除必须同时按住 Shift。");
    }

    private string GetModeAction()
    {
        return Mode switch
        {
            PointToolMode.Inspect => "检查",
            PointToolMode.Repair => "维修",
            PointToolMode.Teleport => "\u51c6\u661f\u4f20\u9001",
            PointToolMode.Utility => "实用",
            PointToolMode.Delete => "删除",
            PointToolMode.Smart => "智能",
            _ => "检查"
        };
    }

    private Target TryResolveTarget(Collider collider, out bool succeeded)
    {
        if (collider == null)
        {
            succeeded = true;
            return null;
        }

        try
        {
            Target target = ResolveTarget(collider);
            succeeded = true;
            return target;
        }
        catch (Exception ex)
        {
            succeeded = false;
            _log.LogWarning(
                $"[PointTool] Semantic target resolution failed for {collider.name}; "
                    + "the coordinate ray will still be evaluated. "
                    + $"{ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private Target ResolveTarget(Collider collider)
    {
        if (collider == null)
            return null;

        InteractableVehicle vehicle = collider.GetComponentInParent<InteractableVehicle>();
        if (vehicle != null)
            return new Target(TargetKind.Vehicle, vehicle, vehicle.transform);
        if (TryComponent(collider, out Zombie zombie))
            return new Target(TargetKind.Zombie, zombie, zombie.transform);
        if (TryComponent(collider, out Animal animal))
            return new Target(TargetKind.Animal, animal, animal.transform);

        if (TryComponent(collider, out InteractableStorage storage))
            return new Target(TargetKind.Storage, storage, storage.transform);
        if (TryComponent(collider, out InteractableDoor door))
            return new Target(TargetKind.Door, door, door.transform);
        if (TryComponent(collider, out InteractableGenerator generator))
            return new Target(TargetKind.Generator, generator, generator.transform);
        if (TryComponent(collider, out InteractableSpot light))
            return new Target(TargetKind.Light, light, light.transform);
        if (TryComponent(collider, out InteractableFire fire))
            return new Target(TargetKind.Fire, fire, fire.transform);
        if (TryComponent(collider, out InteractableOven oven))
            return new Target(TargetKind.Oven, oven, oven.transform);
        if (TryComponent(collider, out InteractableOxygenator oxygenator))
            return new Target(TargetKind.Oxygenator, oxygenator, oxygenator.transform);
        if (TryComponent(collider, out InteractableSafezone safezone))
            return new Target(TargetKind.Safezone, safezone, safezone.transform);

        Transform cursor = collider.transform;
        while (cursor != null)
        {
            BarricadeDrop barricade = BarricadeManager.FindBarricadeByRootTransform(cursor);
            if (barricade != null)
                return new Target(TargetKind.Barricade, barricade, barricade.model);
            StructureDrop structure = StructureManager.FindStructureByRootTransform(cursor);
            if (structure != null)
                return new Target(TargetKind.Structure, structure, structure.model);
            if (cursor.gameObject.layer == ResourceLayer)
            {
                try
                {
                    if (ResourceManager.tryGetRegion(
                        cursor,
                        out byte rx,
                        out byte ry,
                        out ushort resourceIndex))
                    {
                        ResourceSpawnpoint spawnpoint = ResourceManager.getResourceSpawnpoint(
                            rx,
                            ry,
                            resourceIndex);
                        if (spawnpoint != null)
                            return new Target(TargetKind.Resource, spawnpoint, spawnpoint.model);
                    }
                }
                catch (Exception ex)
                {
                    if (!_hasLoggedResourceResolutionFailure)
                    {
                        _hasLoggedResourceResolutionFailure = true;
                        _log.LogWarning(
                            "[PointTool] Resource target lookup failed for a resource-layer "
                                + $"collider; ordinary world surfaces are kept semantic-free. {ex}");
                    }
                }
            }
            cursor = cursor.parent;
        }

        return null;
    }

    private string BuildHudText(Target target)
    {
        if (Mode == PointToolMode.Smart)
            return BuildSmartHudText(target);

        if (target == null)
        {
            string surface = _currentHit.collider == null
                ? PluginLocalization.Translate("世界表面")
                : _currentHit.collider.name;
            float distance = Player.LocalPlayer == null
                ? 0f
                : Vector3.Distance(Player.LocalPlayer.transform.position, _currentPoint);
            string surfaceDetails = $"{PluginLocalization.Translate("表面")} {surface}\n"
                + $"{PluginLocalization.Translate("层级")} {_currentHit.collider?.gameObject.layer ?? -1}\n"
                + $"{PluginLocalization.Translate("距离")} {distance:0.0}m\n"
                + FormatPosition(_currentPoint);
            return $"[{PluginLocalization.Translate(GetModeAction())}]\n"
                + surfaceDetails;
        }

        string name = GetName(target);
        string details = GetDetails(target);
        string action = Mode switch
        {
            PointToolMode.Inspect => "检查",
            PointToolMode.Repair => "维修",
            PointToolMode.Teleport => "\u51c6\u661f\u4f20\u9001",
            PointToolMode.Utility => "实用",
            PointToolMode.Delete => "按住 Shift + 中键删除",
            _ => string.Empty
        };
        string localizedDetails = string.Join(
            "\n",
            details.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(PluginLocalization.Translate));
        return $"{(ShowTargetName ? name : string.Empty)}\n[{PluginLocalization.Translate(action)}]\n{localizedDetails}".Trim();
    }

    private string BuildSmartHudText(Target target)
    {
        PointToolAction action = DecideSmartAction();
        string actionLabel = action switch
        {
            PointToolAction.Repair => "智能 -> 维修",
            PointToolAction.Utility => "智能 -> 实用",
            PointToolAction.Teleport => "智能 -> 准星传送",
            PointToolAction.Delete => "智能 -> Shift + 中键删除",
            PointToolAction.Inspect => "智能 -> 仅检查",
            _ => "智能 -> 无可用动作"
        };
        string details = target == null ? FormatPosition(_currentPoint) : GetDetails(target);
        string localizedDetails = string.Join(
            "\n",
            details.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(PluginLocalization.Translate));
        string name = target == null || !ShowTargetName ? string.Empty : GetName(target) + "\n";
        return $"{name}[{PluginLocalization.Translate(actionLabel)}]\n{localizedDetails}".Trim();
    }

    private PointToolAction DecideSmartAction()
    {
        return PointToolActionGate.DecideSmart(new PointToolDecisionInput(
            _smartHasCoordinateHit,
            _currentTarget != null,
            CanRepairAtConfiguredRange(_currentTarget),
            IsCurrentTargetWithinConfiguredRange() && NeedsRepair(_currentTarget),
            CanUseAtConfiguredRange(_currentTarget),
            CanDelete(_currentTarget),
            Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)));
    }

    private bool IsCurrentTargetWithinConfiguredRange()
    {
        if (!_hasCurrentHit || _currentTarget == null || Player.LocalPlayer == null)
            return false;

        float configuredRange = Mathf.Clamp(Range, 5f, 250f);
        float targetDistance = Vector3.Distance(
            Player.LocalPlayer.transform.position,
            _currentPoint);
        return targetDistance <= configuredRange + 0.01f;
    }

    private bool CanRepairAtConfiguredRange(Target target)
    {
        return IsCurrentTargetWithinConfiguredRange() && CanRepair(target);
    }

    private bool CanUseAtConfiguredRange(Target target)
    {
        return IsCurrentTargetWithinConfiguredRange() && CanUse(target);
    }

    private string GetName(Target target) => target.Value switch
    {
        InteractableVehicle vehicle => vehicle.asset?.FriendlyName ?? "Vehicle",
        BarricadeDrop barricade => barricade.asset?.FriendlyName ?? "Barricade",
        StructureDrop structure => structure.asset?.FriendlyName ?? "Structure",
        ResourceSpawnpoint resource => resource.asset?.FriendlyName ?? "Resource",
        Animal animal => animal.asset?.FriendlyName ?? "Animal",
        Zombie => "Zombie",
        _ => FindWorldAssetName(target) ?? target.Kind.ToString()
    };

    private string GetDetails(Target target)
    {
        string identity = string.Empty;
        string health = string.Empty;
        switch (target.Value)
        {
            case InteractableVehicle vehicle:
                if (ShowId)
                    identity = $"ID {vehicle.asset?.id}  GUID {vehicle.asset?.GUID}";
                if (ShowHealth && vehicle.asset != null)
                    health = $"耐久 {vehicle.health}/{vehicle.asset.health}";
                break;
            case BarricadeDrop barricade:
                if (ShowId)
                    identity = $"ID {barricade.asset?.id}  GUID {barricade.asset?.GUID}";
                if (ShowHealth)
                {
                    Barricade data = barricade.GetServersideData()?.barricade;
                    health = data == null ? string.Empty : $"耐久 {data.health}/{barricade.asset?.health}";
                }
                break;
            case StructureDrop structure:
                if (ShowId)
                    identity = $"ID {structure.asset?.id}  GUID {structure.asset?.GUID}";
                if (ShowHealth)
                {
                    Structure data = structure.GetServersideData()?.structure;
                    health = data == null ? string.Empty : $"耐久 {data.health}/{structure.asset?.health}";
                }
                break;
            case ResourceSpawnpoint resource:
                if (ShowId)
                    identity = $"ID {resource.asset?.id}  GUID {resource.asset?.GUID}";
                if (ShowHealth)
                    health = $"耐久 {resource.health}";
                break;
            case Animal animal:
                if (ShowId)
                    identity = $"ID {animal.asset?.id}  GUID {animal.asset?.GUID}";
                if (ShowHealth)
                    health = $"生命 {animal.GetHealth():0}";
                break;
            case Zombie zombie:
                if (ShowId)
                    identity = $"实例 ID {zombie.id}";
                if (ShowHealth)
                    health = $"生命 {zombie.GetHealth():0}/{zombie.GetMaxHealth():0}";
                break;
            default:
                BarricadeDrop parentBarricade = FindBarricade(target.Transform);
                if (parentBarricade != null)
                {
                    if (ShowId)
                        identity = $"ID {parentBarricade.asset?.id}  GUID {parentBarricade.asset?.GUID}";
                    if (ShowHealth)
                    {
                        Barricade data = parentBarricade.GetServersideData()?.barricade;
                        health = data == null
                            ? string.Empty
                            : $"耐久 {data.health}/{parentBarricade.asset?.health}";
                    }
                    break;
                }

                StructureDrop parentStructure = FindStructure(target.Transform);
                if (parentStructure != null)
                {
                    if (ShowId)
                        identity = $"ID {parentStructure.asset?.id}  GUID {parentStructure.asset?.GUID}";
                    if (ShowHealth)
                    {
                        Structure data = parentStructure.GetServersideData()?.structure;
                        health = data == null
                            ? string.Empty
                            : $"耐久 {data.health}/{parentStructure.asset?.health}";
                    }
                }
                break;
        }
        return string.Join("\n", new[] { identity, health }).Trim();
    }

    private void ExecuteCurrentTarget()
    {
        if (!SingleplayerGuard.IsReady)
            return;

        bool deleteModifierHeld = Input.GetKey(KeyCode.LeftShift)
            || Input.GetKey(KeyCode.RightShift);

        if (Mode == PointToolMode.Smart)
        {
            ExecuteSmartTarget(deleteModifierHeld);
            return;
        }

        PointToolActionGate.Failure failure = PointToolActionGate.GetFailure(
                Mode,
                _hasCurrentHit,
                _currentTarget != null,
                deleteModifierHeld);
        if (failure != PointToolActionGate.Failure.None)
        {
            ShowFailureMessage(failure);
            return;
        }

        if ((Mode == PointToolMode.Repair || Mode == PointToolMode.Utility)
            && !IsCurrentTargetWithinConfiguredRange())
        {
            ShowFailureMessage(PointToolActionGate.Failure.NoSemanticTarget);
            return;
        }

        try
        {
            bool success = Mode switch
            {
                PointToolMode.Inspect => InspectCurrentTarget(),
                PointToolMode.Repair => Repair(_currentTarget),
                PointToolMode.Teleport => TeleportToCurrentCoordinate(),
                PointToolMode.Utility => Use(_currentTarget),
                PointToolMode.Delete => Delete(_currentTarget),
                _ => false
            };
            if (Mode == PointToolMode.Inspect)
                return;

            if (Mode == PointToolMode.Teleport)
            {
                SetInspectionMessage(
                    success
                        ? $"{PluginLocalization.Translate("传送成功")}\n{FormatPosition(_currentPoint)}"
                        : $"{PluginLocalization.Translate("传送失败")}\n"
                            + PluginLocalization.Translate("没有可用的安全落点"),
                    3f);
            }
            else if (!success && _currentTarget == null)
                ShowFailureMessage(PointToolActionGate.Failure.NoSemanticTarget);

            if (!success
                && Mode != PointToolMode.Inspect
                && Mode != PointToolMode.Teleport
                && _currentTarget != null)
                _log.LogWarning($"[PointTool] 当前目标 {_currentTarget.Kind} 不支持 {Mode}，未修改游戏状态。");
        }
        catch (Exception ex)
        {
            string targetKind = _currentTarget?.Kind.ToString() ?? "WorldHit";
            _log.LogWarning($"[PointTool] 执行 {targetKind}/{Mode} 失败：{ex.Message}");
        }
    }

    private void ExecuteSmartTarget(bool deleteModifierHeld)
    {
        PointToolAction action = PointToolActionGate.DecideSmart(new PointToolDecisionInput(
            _smartHasCoordinateHit,
            _currentTarget != null,
            CanRepairAtConfiguredRange(_currentTarget),
            IsCurrentTargetWithinConfiguredRange() && NeedsRepair(_currentTarget),
            CanUseAtConfiguredRange(_currentTarget),
            CanDelete(_currentTarget),
            deleteModifierHeld));
        _log.LogInfo(
            $"[PointTool] mode=Smart, action={action}, target={_currentTarget?.Kind.ToString() ?? "WorldHit"}, "
                + $"reason={GetSmartReason(action, deleteModifierHeld)}, frame={_smartCapturedFrame}, "
                + $"rayOrigin={_smartRayOrigin}, rayDirection={_smartRayDirection}.");

        if (action == PointToolAction.None)
        {
            string message = deleteModifierHeld
                ? "当前目标不可删除"
                : "未命中世界表面";
            SetInspectionMessage(
                $"[{PluginLocalization.Translate("智能 -> 无可用动作")}]\n{PluginLocalization.Translate(message)}",
                3f);
            return;
        }

        if (action == PointToolAction.Inspect)
        {
            InspectCurrentTarget();
            return;
        }

        bool success;
        try
        {
            success = ExecuteAction(action);
        }
        catch (Exception ex)
        {
            _log.LogWarning($"[PointTool] Smart action failed: action={action}, target={_currentTarget?.Kind.ToString() ?? "WorldHit"}.\n{ex}");
            SetInspectionMessage(
                $"[{PluginLocalization.Translate(GetSmartActionLabel(action))}]\n"
                    + PluginLocalization.Translate("执行失败"),
                3f);
            return;
        }

        SetInspectionMessage(
            $"[{PluginLocalization.Translate(GetSmartActionLabel(action))}]\n"
                + PluginLocalization.Translate(success ? "执行成功" : "执行失败"),
            3f);
        try
        {
            RefreshRaycast();
        }
        catch (Exception ex)
        {
            _log.LogWarning($"[PointTool] Smart post-action refresh failed: action={action}, success={success}.\n{ex}");
            ClearRaycastSnapshot(preserveInspectionMessage: true);
        }
    }

    private bool ExecuteAction(PointToolAction action)
    {
        return action switch
        {
            PointToolAction.Inspect => InspectCurrentTarget(),
            PointToolAction.Repair => Repair(_currentTarget),
            PointToolAction.Teleport => TeleportToSmartCoordinate(),
            PointToolAction.Utility => Use(_currentTarget),
            PointToolAction.Delete => Delete(_currentTarget),
            _ => false
        };
    }

    private bool TeleportToSmartCoordinate()
    {
        if (!_smartHasCoordinateHit)
            return false;
        _currentHit = _smartCoordinateHit;
        _currentPoint = _smartCoordinatePoint;
        _hasCurrentHit = true;
        return TeleportToCurrentCoordinate();
    }

    private static string GetSmartActionLabel(PointToolAction action) => action switch
    {
        PointToolAction.Repair => "智能 -> 维修",
        PointToolAction.Utility => "智能 -> 实用",
        PointToolAction.Teleport => "智能 -> 准星传送",
        PointToolAction.Delete => "智能 -> 删除",
        _ => "智能 -> 仅检查"
    };

    private string GetSmartReason(PointToolAction action, bool deleteModifierHeld) => action switch
    {
        PointToolAction.Delete => "delete-modifier-and-supported-target",
        PointToolAction.Repair => "repairable-target-is-damaged",
        PointToolAction.Utility => "healthy-or-nonrepairable-usable-target",
        PointToolAction.Teleport when _currentTarget != null => "coordinate-ray-ignores-semantic-target",
        PointToolAction.Teleport => "world-surface-coordinate-candidate",
        PointToolAction.Inspect => "semantic-target-has-no-automatic-action",
        _ when deleteModifierHeld => "delete-modifier-without-deletable-target",
        _ => "no-world-hit"
    };

    private bool TeleportToCurrentCoordinate()
    {
        if (!_hasCurrentHit)
            return false;

        Player player = Player.LocalPlayer;
        Vector3 before = player?.transform.position ?? Vector3.zero;
        Vector3 mapPosition = new(_currentPoint.x, 0f, _currentPoint.z);
        bool success = CheatMenuPlugin.Instance.Actions.TeleportToMapPosition(
            mapPosition,
            out Vector3 landingPoint);
        string colliderName = Mode == PointToolMode.Smart
            ? GetSmartCoordinateSource()
            : _currentHit.collider == null
                ? "TerrainHeightFallback"
                : _currentHit.collider.name;
        int colliderLayer = _currentHit.collider?.gameObject.layer ?? -1;
        _log.LogInfo(
            $"[PointTeleportTrace] request: before={before}, "
            + $"hit={_currentPoint}, normal={_currentHit.normal}, "
            + $"collider={colliderName}, layer={colliderLayer}, "
            + $"landing={landingPoint}, apiAccepted={success}.");

        if (success)
        {
            _hasPendingTeleportTrace = true;
            _pendingTeleportTraceAt = Time.unscaledTime + 0.25f;
            _pendingTeleportBefore = before;
            _pendingTeleportRequested = _currentPoint;
            _pendingTeleportLanding = landingPoint;
        }

        return success;
    }

    private void FlushPendingTeleportTrace()
    {
        if (!_hasPendingTeleportTrace || Time.unscaledTime < _pendingTeleportTraceAt)
            return;

        _hasPendingTeleportTrace = false;
        Vector3 actual = Player.LocalPlayer?.transform.position ?? Vector3.zero;
        _log.LogInfo(
            $"[PointTeleportTrace] result: before={_pendingTeleportBefore}, "
            + $"requested={_pendingTeleportRequested}, landing={_pendingTeleportLanding}, "
            + $"actual={actual}, moved={Vector3.Distance(_pendingTeleportBefore, actual):0.00}m, "
            + $"landingError={Vector3.Distance(_pendingTeleportLanding, actual):0.00}m.");
    }

    private void ShowFailureMessage(PointToolActionGate.Failure failure)
    {
        string message = failure switch
        {
            PointToolActionGate.Failure.NoWorldHit when Mode == PointToolMode.Teleport =>
                $"{PluginLocalization.Translate("传送失败")}\n"
                    + PluginLocalization.Translate("未命中世界表面"),
            PointToolActionGate.Failure.NoWorldHit =>
                $"{PluginLocalization.Translate("检查失败")}\n"
                    + PluginLocalization.Translate("未命中目标"),
            PointToolActionGate.Failure.NoSemanticTarget =>
                $"{PluginLocalization.Translate(GetFailureActionKey())}\n"
                    + PluginLocalization.Translate("没有可支持的语义目标"),
            PointToolActionGate.Failure.DeleteModifierRequired =>
                $"{PluginLocalization.Translate("删除失败")}\n"
                    + PluginLocalization.Translate("删除需要按住 Shift"),
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(message))
            SetInspectionMessage(message, 3f);
    }

    private string GetFailureActionKey()
    {
        return Mode switch
        {
            PointToolMode.Repair => "维修失败",
            PointToolMode.Utility => "实用失败",
            PointToolMode.Delete => "删除失败",
            _ => "检查失败"
        };
    }

    private bool InspectCurrentTarget()
    {
        if (!_hasCurrentHit)
        {
            ShowFailureMessage(PointToolActionGate.Failure.NoWorldHit);
            return false;
        }

        SetInspectionMessage(
            $"{PluginLocalization.Translate("检查成功")}\n{BuildHudText(_currentTarget)}",
            3f);
        _log.LogInfo(
            _currentTarget == null
                ? $"[PointTool] Inspected world surface at {_currentPoint}."
                : $"[PointTool] Inspected {_currentTarget.Kind} at {_currentPoint}.");
        return true;
    }

    private void SetInspectionMessage(string message, float seconds)
    {
        _inspectionMessage = message;
        _inspectionMessageUntil = Time.unscaledTime + Mathf.Max(0.25f, seconds);
        if (ShowHud)
            _hud.SetPoint(message);
    }

    private static string FormatPosition(Vector3 position)
    {
        return $"{PluginLocalization.Translate("位置")} "
            + $"{position.x:0.0}, {position.y:0.0}, {position.z:0.0}";
    }

    private bool Repair(Target target)
    {
        switch (target.Value)
        {
            case InteractableVehicle vehicle:
                return RepairVehicle(vehicle);
            case BarricadeDrop barricade:
                return InvokeRepair(typeof(BarricadeManager), barricade.model);
            case StructureDrop structure:
                return InvokeRepair(typeof(StructureManager), structure.model);
            default:
                BarricadeDrop barricadeTarget = FindBarricade(target.Transform);
                if (barricadeTarget != null)
                    return Repair(new Target(TargetKind.Barricade, barricadeTarget, barricadeTarget.model));
                StructureDrop structureTarget = FindStructure(target.Transform);
                return structureTarget != null
                    && Repair(new Target(TargetKind.Structure, structureTarget, structureTarget.model));
        }
    }

    private bool Use(Target target)
    {
        if (target?.Value == null)
            return false;

        float configuredRange = Mathf.Clamp(Range, 5f, 250f);
        float targetDistance = Player.LocalPlayer == null
            ? 0f
            : Vector3.Distance(Player.LocalPlayer.transform.position, _currentPoint);
        bool success;

        using (PointToolInteractionPatch.BeginRangeOverride(configuredRange))
        {
            success = target.Value switch
            {
                InteractableVehicle vehicle => UseVehicle(vehicle),
                InteractableStorage storage => UseStorage(storage),
                InteractableDoor door => ToggleDoor(door),
                InteractableGenerator generator => ToggleGenerator(generator),
                InteractableSpot light => ToggleSpot(light),
                InteractableFire fire => ToggleFire(fire),
                InteractableOven oven => ToggleOven(oven),
                InteractableOxygenator oxygenator => ToggleOxygenator(oxygenator),
                InteractableSafezone safezone => ToggleSafezone(safezone),
                _ => false
            };
        }

        _log.LogInfo(
            $"[PointToolUseTrace] target={target.Kind}, range={configuredRange:0.##}m, "
                + $"targetDistance={targetDistance:0.##}m, success={success}.");
        return success;
    }

    private static bool UseVehicle(InteractableVehicle vehicle)
    {
        vehicle.use();
        return Player.LocalPlayer?.movement?.getVehicle() == vehicle;
    }

    private static bool RepairVehicle(InteractableVehicle vehicle)
    {
        if (!IsRegisteredVehicle(vehicle) || vehicle.asset.health == 0 || vehicle.health >= vehicle.asset.health)
            return false;

        vehicle.askRepair((ushort)(vehicle.asset.health - vehicle.health));
        return vehicle.health >= vehicle.asset.health;
    }

    private static bool DeleteVehicle(InteractableVehicle vehicle)
    {
        if (!IsRegisteredVehicle(vehicle) || vehicle.anySeatsOccupied)
            return false;

        VehicleManager.askVehicleDestroy(vehicle);
        return true;
    }

    private static bool IsRegisteredVehicle(InteractableVehicle vehicle)
    {
        if (!SingleplayerGuard.IsReady
            || vehicle == null
            || vehicle.asset == null
            || vehicle.IsPendingDestroy
            || vehicle.isExploded)
            return false;

        if (VehicleManager.vehicles != null && VehicleManager.vehicles.Contains(vehicle))
            return true;

        return vehicle.instanceID != 0
            && ReferenceEquals(
                VehicleManager.findVehicleByNetInstanceID(vehicle.instanceID),
                vehicle);
    }

    private static bool UseStorage(InteractableStorage storage)
    {
        PlayerInventory inventory = Player.LocalPlayer?.inventory;
        storage.ClientInteract(false);
        return inventory != null
            && inventory.isStoring
            && inventory.storage == storage;
    }

    private static bool ToggleDoor(InteractableDoor door)
    {
        bool before = door.isOpen;
        door.ClientToggle();
        return door.isOpen != before;
    }

    private static bool ToggleGenerator(InteractableGenerator generator)
    {
        bool before = generator.isPowered;
        generator.ClientToggle();
        return generator.isPowered != before;
    }

    private static bool ToggleSpot(InteractableSpot spot)
    {
        bool before = spot.isPowered;
        spot.ClientToggle();
        return spot.isPowered != before;
    }

    private static bool ToggleFire(InteractableFire fire)
    {
        bool before = fire.isLit;
        fire.ClientToggle();
        return fire.isLit != before;
    }

    private static bool ToggleOven(InteractableOven oven)
    {
        bool before = oven.isLit;
        oven.ClientToggle();
        return oven.isLit != before;
    }

    private static bool ToggleOxygenator(InteractableOxygenator oxygenator)
    {
        bool before = oxygenator.isPowered;
        oxygenator.ClientToggle();
        return oxygenator.isPowered != before;
    }

    private static bool ToggleSafezone(InteractableSafezone safezone)
    {
        bool before = safezone.isPowered;
        safezone.ClientToggle();
        return safezone.isPowered != before;
    }

    private static bool Delete(Target target)
    {
        switch (target.Value)
        {
            case InteractableVehicle vehicle:
                return DeleteVehicle(vehicle);
            case BarricadeDrop barricade:
#pragma warning disable CS0618
                if (!BarricadeManager.tryGetInfo(
                    barricade.model,
                    out byte bx,
                    out byte by,
                    out ushort plant,
                    out _,
                    out _))
                    return false;
#pragma warning restore CS0618
                BarricadeManager.destroyBarricade(barricade, bx, by, plant);
                return true;
            case StructureDrop structure:
#pragma warning disable CS0618
                if (!StructureManager.tryGetInfo(
                    structure.model,
                    out byte sx,
                    out byte sy,
                    out _,
                    out _))
                    return false;
#pragma warning restore CS0618
                StructureManager.destroyStructure(structure, sx, sy, Vector3.zero, false);
                return true;
            case ResourceSpawnpoint resource:
                if (!ResourceManager.tryGetRegion(resource.model, out byte rx, out byte ry, out ushort index))
                    return false;
                ResourceManager.ServerSetResourceDead(rx, ry, index, Vector3.zero);
                return true;
            default:
                BarricadeDrop barricadeTarget = FindBarricade(target.Transform);
                if (barricadeTarget != null)
                    return Delete(new Target(TargetKind.Barricade, barricadeTarget, barricadeTarget.model));
                StructureDrop structureTarget = FindStructure(target.Transform);
                return structureTarget != null
                    && Delete(new Target(TargetKind.Structure, structureTarget, structureTarget.model));
        }
    }

    private static bool CanRepair(Target target)
    {
        return target?.Value is InteractableVehicle or BarricadeDrop or StructureDrop
            || target != null
                && (FindBarricade(target.Transform) != null || FindStructure(target.Transform) != null);
    }

    private static bool NeedsRepair(Target target)
    {
        switch (target?.Value)
        {
            case InteractableVehicle vehicle:
                return vehicle.asset != null && vehicle.health < vehicle.asset.health;
            case BarricadeDrop barricade:
                Barricade barricadeData = barricade.GetServersideData()?.barricade;
                return barricadeData != null
                    && barricade.asset != null
                    && barricadeData.health < barricade.asset.health;
            case StructureDrop structure:
                Structure structureData = structure.GetServersideData()?.structure;
                return structureData != null
                    && structure.asset != null
                    && structureData.health < structure.asset.health;
            default:
                BarricadeDrop parentBarricade = target == null ? null : FindBarricade(target.Transform);
                if (parentBarricade != null)
                    return NeedsRepair(new Target(TargetKind.Barricade, parentBarricade, parentBarricade.model));
                StructureDrop parentStructure = target == null ? null : FindStructure(target.Transform);
                return parentStructure != null
                    && NeedsRepair(new Target(TargetKind.Structure, parentStructure, parentStructure.model));
        }
    }

    private static bool CanUse(Target target)
    {
        return target?.Value is InteractableVehicle
            or InteractableStorage
            or InteractableDoor
            or InteractableGenerator
            or InteractableSpot
            or InteractableFire
            or InteractableOven
            or InteractableOxygenator
            or InteractableSafezone;
    }

    private static bool CanDelete(Target target)
    {
        return target?.Value is InteractableVehicle or BarricadeDrop or StructureDrop or ResourceSpawnpoint
            || target != null
                && (FindBarricade(target.Transform) != null || FindStructure(target.Transform) != null);
    }

    private static string FindWorldAssetName(Target target)
    {
        return FindBarricade(target.Transform)?.asset?.FriendlyName
            ?? FindStructure(target.Transform)?.asset?.FriendlyName;
    }

    private static BarricadeDrop FindBarricade(Transform transform)
    {
        for (Transform cursor = transform; cursor != null; cursor = cursor.parent)
        {
            BarricadeDrop barricade = BarricadeManager.FindBarricadeByRootTransform(cursor);
            if (barricade != null)
                return barricade;
        }

        return null;
    }

    private static StructureDrop FindStructure(Transform transform)
    {
        for (Transform cursor = transform; cursor != null; cursor = cursor.parent)
        {
            StructureDrop structure = StructureManager.FindStructureByRootTransform(cursor);
            if (structure != null)
                return structure;
        }

        return null;
    }

    private void ClearTarget()
    {
        ClearRaycastSnapshot(preserveInspectionMessage: false);
        _hud.ClearPointIfCreated();
    }

    private void ClearRaycastSnapshot(bool preserveInspectionMessage)
    {
        _hasCurrentHit = false;
        _currentHit = default;
        _currentPoint = default;
        _smartCoordinateHit = default;
        _smartCoordinatePoint = default;
        _smartHasCoordinateHit = false;
        _smartSemanticHit = false;
        _smartSemanticResolveSucceeded = false;
        _smartCoordinatePhysicsHit = false;
        _smartCoordinateSemanticFallback = false;
        _smartCoordinateTerrainFallback = false;
        _smartSnapshot = default;
        _smartRayOrigin = default;
        _smartRayDirection = default;
        _smartCapturedFrame = -1;
        _currentTarget = null;
        if (!preserveInspectionMessage)
        {
            _inspectionMessage = null;
            _inspectionMessageUntil = 0f;
        }
    }

    private static bool InvokeRepair(Type managerType, Transform transform)
    {
        MethodInfo method = managerType.GetMethod(
            "repair",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(Transform), typeof(float), typeof(float) },
            null);
        if (method == null)
            return false;
        method.Invoke(null, new object[] { transform, (float)ushort.MaxValue, 1f });
        return true;
    }

    private static bool TryComponent<T>(Collider collider, out T component) where T : Component
    {
        component = collider.GetComponentInParent<T>();
        return component != null;
    }

    private enum TargetKind
    {
        Vehicle,
        Barricade,
        Structure,
        Storage,
        Door,
        Generator,
        Light,
        Fire,
        Oven,
        Oxygenator,
        Safezone,
        Resource,
        Zombie,
        Animal
    }

    private sealed class Target
    {
        internal Target(TargetKind kind, object value, Transform transform)
        {
            Kind = kind;
            Value = value;
            Transform = transform;
        }

        internal TargetKind Kind { get; }
        internal object Value { get; }
        internal Transform Transform { get; }
    }
}
