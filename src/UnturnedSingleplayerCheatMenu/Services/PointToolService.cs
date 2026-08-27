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
		internal TargetKind Kind { get; }

		internal object Value { get; }

		internal Transform Transform { get; }

		internal Target(TargetKind kind, object value, Transform transform)
		{
			Kind = kind;
			Value = value;
			Transform = transform;
		}
	}

	private static readonly KeyboardShortcut MiddleMouseShortcut = new KeyboardShortcut(KeyCode.Mouse2);

	private const ushort VehicleBatteryMaxCharge = 10000;

	private const ushort VehicleBatteryDisplayedFullThreshold = 9950;

	private const int TeleportCoordinateMask = -1877377023;

	private const int ResourceLayer = 14;

	private readonly ManualLogSource _log;

	private readonly PointToolHud _hud;

	private readonly NativeShortcutDetector _nativeMiddleMouse = new NativeShortcutDetector();

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

	internal bool Enabled { get; set; }

	internal PointToolMode Mode { get; set; }

	internal float Range { get; set; } = 100f;

	internal bool ShowTargetName { get; set; } = true;

	internal bool ShowId { get; set; } = true;

	internal bool ShowHealth { get; set; } = true;

	internal bool ShowHud { get; set; } = true;

	internal PointToolService(ManualLogSource log, PointToolHud hud)
	{
		_log = log;
		_hud = hud;
	}

	internal void Pump()
	{
		FlushPendingTeleportTrace();
		bool flag = _nativeMiddleMouse.IsPressed(MiddleMouseShortcut);
		if (!SingleplayerGuard.IsReady)
		{
			Enabled = false;
			ClearTarget();
			return;
		}
		if (!Enabled || IsInteractionPaused())
		{
			ClearTarget();
			_hud.Hide();
			return;
		}
		bool mouseButtonDown = Input.GetMouseButtonDown(2);
		if (mouseButtonDown | flag)
		{
			if (flag && !mouseButtonDown)
			{
				_log.LogInfo("[PointToolInput] Unity missed middle click; native fallback accepted it.");
			}
			if ((Mode != PointToolMode.Teleport && Mode != PointToolMode.Smart) || !_hasCurrentHit)
			{
				RefreshRaycast();
			}
			LogClickRaycastTrace();
			ExecuteCurrentTarget();
		}
		else if (Time.unscaledTime < _nextRaycast)
		{
			ShowCurrentHud();
		}
		else
		{
			_nextRaycast = Time.unscaledTime + 1f / 30f;
			RefreshRaycast();
			ShowCurrentHud();
		}
	}

	internal static bool IsInteractionPaused()
	{
		CheatMenuPlugin instance = CheatMenuPlugin.Instance;
		if ((object)instance == null || !instance.IsMenuOpen)
		{
			return PlayerUI.window?.showCursor ?? false;
		}
		return true;
	}

	private void RefreshRaycast()
	{
		_lastRaycastAt = Time.unscaledTime;
		Camera camera = MainCamera.instance ?? Camera.main;
		Transform transform = Player.LocalPlayer?.look?.aim;
		Ray ray = ((camera != null) ? camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)) : ((transform != null) ? new Ray(transform.position, transform.forward) : default(Ray)));
		if (camera == null && transform == null)
		{
			_hasCurrentHit = false;
			_currentHit = default(RaycastHit);
			_currentPoint = default(Vector3);
			_currentTarget = null;
			return;
		}
		float maxDistance = Mathf.Clamp(Range, 5f, 250f);
		if (Mode == PointToolMode.Smart)
		{
			RefreshSmartRaycast(ray, maxDistance);
			return;
		}
		int layerMask = ((Mode == PointToolMode.Teleport) ? (-1877377023) : (RayMasks.BLOCK_STANCE | RayMasks.DAMAGE_CLIENT));
		if (Physics.Raycast(ray, out _currentHit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
		{
			_hasCurrentHit = true;
			_currentPoint = _currentHit.point;
			_currentTarget = TryResolveTarget(_currentHit.collider, out var _);
		}
		else
		{
			_currentHit = default(RaycastHit);
			_hasCurrentHit = TryGetTerrainHit(ray, maxDistance, out _currentPoint);
			_currentTarget = null;
		}
	}

	private void RefreshSmartRaycast(Ray ray, float maxDistance)
	{
		_smartRayOrigin = ray.origin;
		_smartRayDirection = ray.direction;
		_smartCapturedFrame = Time.frameCount;
		_smartSemanticHit = false;
		_smartSemanticResolveSucceeded = false;
		_smartCoordinatePhysicsHit = false;
		_smartCoordinateSemanticFallback = false;
		_smartCoordinateTerrainFallback = false;
		_smartHasCoordinateHit = false;
		_smartCoordinateHit = default(RaycastHit);
		_smartCoordinatePoint = default(Vector3);
		_currentHit = default(RaycastHit);
		_currentPoint = default(Vector3);
		_currentTarget = null;
		_hasCurrentHit = false;
		bool flag = (_smartSemanticHit = Physics.Raycast(ray, out var hitInfo, maxDistance, RayMasks.BLOCK_STANCE | RayMasks.DAMAGE_CLIENT, QueryTriggerInteraction.Ignore));
		_smartSemanticResolveSucceeded = !flag;
		Target currentTarget = null;
		if (flag)
		{
			currentTarget = TryResolveTarget(hitInfo.collider, out _smartSemanticResolveSucceeded);
		}
		if (_smartHasCoordinateHit = (_smartCoordinatePhysicsHit = Physics.Raycast(ray, out _smartCoordinateHit, maxDistance, -1877377023, QueryTriggerInteraction.Ignore)))
		{
			_smartCoordinatePoint = _smartCoordinateHit.point;
		}
		else if (TryGetStaticSemanticCoordinateFallback(flag, hitInfo, out _smartCoordinatePoint))
		{
			_smartCoordinateHit = hitInfo;
			_smartHasCoordinateHit = true;
			_smartCoordinateSemanticFallback = true;
		}
		else
		{
			_smartHasCoordinateHit = TryGetTerrainHit(ray, maxDistance, out _smartCoordinatePoint);
			_smartCoordinateTerrainFallback = _smartHasCoordinateHit;
		}
		_smartSnapshot = new PointToolRaycastSnapshot(_smartSemanticHit, _smartSemanticResolveSucceeded, _smartCoordinatePhysicsHit, _smartCoordinateTerrainFallback, _smartCoordinateSemanticFallback);
		_smartHasCoordinateHit = _smartSnapshot.HasCoordinateHit;
		_currentTarget = currentTarget;
		_hasCurrentHit = _smartSnapshot.HasAnyHit;
		_currentPoint = (_smartSemanticHit ? hitInfo.point : _smartCoordinatePoint);
		if (_smartSemanticHit)
		{
			_currentHit = hitInfo;
		}
		else if (_smartCoordinatePhysicsHit)
		{
			_currentHit = _smartCoordinateHit;
		}
	}

	private void LogClickRaycastTrace()
	{
		string arg = ((!(_currentHit.collider == null)) ? _currentHit.collider.name : (_hasCurrentHit ? "TerrainHeightFallback" : "None"));
		int num = _currentHit.collider?.gameObject.layer ?? (-1);
		_log.LogInfo($"[PointTeleportClickTrace] mode={Mode}, hasHit={_hasCurrentHit}, " + $"point={_currentPoint}, normal={_currentHit.normal}, " + $"collider={arg}, layer={num}.");
		if (Mode == PointToolMode.Smart)
		{
			string text = ((_smartCoordinateHit.collider == null) ? GetSmartCoordinateSource() : _smartCoordinateHit.collider.name);
			int num2 = _smartCoordinateHit.collider?.gameObject.layer ?? (-1);
			_log.LogInfo($"[PointSmartRaycastTrace] frame={_smartCapturedFrame}, " + $"semanticHit={_smartSemanticHit}, " + $"semanticResolveSucceeded={_smartSemanticResolveSucceeded}, " + $"coordinatePhysicsHit={_smartCoordinatePhysicsHit}, " + $"coordinateSemanticFallback={_smartCoordinateSemanticFallback}, " + $"coordinateFallback={_smartCoordinateTerrainFallback}, " + "coordinateSource=" + GetSmartCoordinateSource() + ", " + $"coordinateHit={_smartHasCoordinateHit}, " + "coordinateCollider=" + text + ", " + $"coordinateLayer={num2}, " + $"coordinatePoint={_smartCoordinatePoint}.");
		}
	}

	private string GetSmartCoordinateSource()
	{
		if (_smartCoordinatePhysicsHit)
		{
			return "Physics";
		}
		if (_smartCoordinateSemanticFallback)
		{
			return "SemanticStaticFallback";
		}
		if (_smartCoordinateTerrainFallback)
		{
			return "TerrainHeightFallback";
		}
		return "None";
	}

	private static bool TryGetStaticSemanticCoordinateFallback(bool semanticHit, RaycastHit semanticHitInfo, out Vector3 point)
	{
		point = default(Vector3);
		Collider collider = semanticHitInfo.collider;
		if (!semanticHit || collider == null)
		{
			return false;
		}
		int layer = collider.gameObject.layer;
		if (layer != 0 && layer != 15 && layer != 16 && layer != 19 && layer != 20 && layer != 28 && layer != 31)
		{
			return false;
		}
		int num = 1 << layer;
		if ((-1877377023 & num) == 0)
		{
			return false;
		}
		point = semanticHitInfo.point;
		return true;
	}

	private bool TryGetTerrainHit(Ray ray, float maxDistance, out Vector3 hitPoint)
	{
		hitPoint = default(Vector3);
		if (ray.direction.y >= -0.0001f)
		{
			return false;
		}
		try
		{
			float num = 0f;
			if (ray.origin.y - LevelGround.getHeight(ray.origin) <= 0f)
			{
				return false;
			}
			float num2 = Mathf.Min(2f, maxDistance);
			while (num2 <= maxDistance)
			{
				Vector3 point = ray.GetPoint(num2);
				if (point.y - LevelGround.getHeight(point) <= 0f)
				{
					float num3 = num;
					float num4 = num2;
					for (int i = 0; i < 8; i++)
					{
						float num5 = (num3 + num4) * 0.5f;
						Vector3 point2 = ray.GetPoint(num5);
						if (point2.y - LevelGround.getHeight(point2) > 0f)
						{
							num3 = num5;
						}
						else
						{
							num4 = num5;
						}
					}
					hitPoint = ray.GetPoint(num4);
					hitPoint.y = LevelGround.getHeight(hitPoint);
					return true;
				}
				if (!(num2 >= maxDistance))
				{
					num = num2;
					num2 = Mathf.Min(num2 + 2f, maxDistance);
					continue;
				}
				break;
			}
		}
		catch (Exception ex)
		{
			if (!_hasLoggedTerrainRaycastFailure)
			{
				_hasLoggedTerrainRaycastFailure = true;
				_log.LogWarning("[PointTool] Failed to resolve terrain under the aim ray: " + ex.Message);
			}
		}
		return false;
	}

	private void ShowCurrentHud()
	{
		if (ShowHud)
		{
			if (Time.unscaledTime < _inspectionMessageUntil && !string.IsNullOrWhiteSpace(_inspectionMessage))
			{
				_hud.SetPoint(_inspectionMessage);
				return;
			}
			_inspectionMessage = null;
			_inspectionMessageUntil = 0f;
			_hud.SetPoint(_hasCurrentHit ? BuildHudText(_currentTarget) : BuildIdleHudText());
		}
	}

	private string BuildIdleHudText()
	{
		if (Mode == PointToolMode.Smart)
		{
			return PluginLocalization.Translate("准星交互工具") + "\n[" + PluginLocalization.Translate("智能 -> 无可用动作") + "]\n" + PluginLocalization.Translate("未命中世界表面");
		}
		return PluginLocalization.Translate("准星交互工具") + "\n[" + PluginLocalization.Translate(GetModeAction()) + "]\n" + PluginLocalization.Translate("触发键：鼠标中键；删除必须同时按住 Shift。");
	}

	private string GetModeAction()
	{
		return Mode switch
		{
			PointToolMode.Inspect => "检查",
			PointToolMode.Repair => "维修",
			PointToolMode.Teleport => "准星传送",
			PointToolMode.Utility => "实用",
			PointToolMode.Delete => "删除",
			PointToolMode.Smart => "智能",
			_ => "检查",
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
			Target result = ResolveTarget(collider);
			succeeded = true;
			return result;
		}
		catch (Exception ex)
		{
			succeeded = false;
			_log.LogWarning("[PointTool] Semantic target resolution failed for " + collider.name + "; the coordinate ray will still be evaluated. " + ex.GetType().Name + ": " + ex.Message);
			return null;
		}
	}

	private Target ResolveTarget(Collider collider)
	{
		if (collider == null)
		{
			return null;
		}
		InteractableVehicle componentInParent = collider.GetComponentInParent<InteractableVehicle>();
		if (componentInParent != null)
		{
			return new Target(TargetKind.Vehicle, componentInParent, componentInParent.transform);
		}
		if (TryComponent<Zombie>(collider, out var component))
		{
			return new Target(TargetKind.Zombie, component, component.transform);
		}
		if (TryComponent<Animal>(collider, out var component2))
		{
			return new Target(TargetKind.Animal, component2, component2.transform);
		}
		if (TryComponent<InteractableStorage>(collider, out var component3))
		{
			return new Target(TargetKind.Storage, component3, component3.transform);
		}
		if (TryComponent<InteractableDoor>(collider, out var component4))
		{
			return new Target(TargetKind.Door, component4, component4.transform);
		}
		if (TryComponent<InteractableGenerator>(collider, out var component5))
		{
			return new Target(TargetKind.Generator, component5, component5.transform);
		}
		if (TryComponent<InteractableSpot>(collider, out var component6))
		{
			return new Target(TargetKind.Light, component6, component6.transform);
		}
		if (TryComponent<InteractableFire>(collider, out var component7))
		{
			return new Target(TargetKind.Fire, component7, component7.transform);
		}
		if (TryComponent<InteractableOven>(collider, out var component8))
		{
			return new Target(TargetKind.Oven, component8, component8.transform);
		}
		if (TryComponent<InteractableOxygenator>(collider, out var component9))
		{
			return new Target(TargetKind.Oxygenator, component9, component9.transform);
		}
		if (TryComponent<InteractableSafezone>(collider, out var component10))
		{
			return new Target(TargetKind.Safezone, component10, component10.transform);
		}
		Transform transform = collider.transform;
		while (transform != null)
		{
			BarricadeDrop barricadeDrop = BarricadeManager.FindBarricadeByRootTransform(transform);
			if (barricadeDrop != null)
			{
				return new Target(TargetKind.Barricade, barricadeDrop, barricadeDrop.model);
			}
			StructureDrop structureDrop = StructureManager.FindStructureByRootTransform(transform);
			if (structureDrop != null)
			{
				return new Target(TargetKind.Structure, structureDrop, structureDrop.model);
			}
			if (transform.gameObject.layer == 14)
			{
				try
				{
					if (ResourceManager.tryGetRegion(transform, out var x, out var y, out var index))
					{
						ResourceSpawnpoint resourceSpawnpoint = ResourceManager.getResourceSpawnpoint(x, y, index);
						if (resourceSpawnpoint != null)
						{
							return new Target(TargetKind.Resource, resourceSpawnpoint, resourceSpawnpoint.model);
						}
					}
				}
				catch (Exception arg)
				{
					if (!_hasLoggedResourceResolutionFailure)
					{
						_hasLoggedResourceResolutionFailure = true;
						_log.LogWarning("[PointTool] Resource target lookup failed for a resource-layer " + $"collider; ordinary world surfaces are kept semantic-free. {arg}");
					}
				}
			}
			transform = transform.parent;
		}
		return null;
	}

	private string BuildHudText(Target target)
	{
		if (Mode == PointToolMode.Smart)
		{
			return BuildSmartHudText(target);
		}
		if (target == null)
		{
			string text = ((_currentHit.collider == null) ? PluginLocalization.Translate("世界表面") : _currentHit.collider.name);
			float num = ((Player.LocalPlayer == null) ? 0f : Vector3.Distance(Player.LocalPlayer.transform.position, _currentPoint));
			string text2 = PluginLocalization.Translate("表面") + " " + text + "\n" + string.Format("{0} {1}\n", PluginLocalization.Translate("层级"), _currentHit.collider?.gameObject.layer ?? (-1)) + string.Format("{0} {1:0.0}m\n", PluginLocalization.Translate("距离"), num) + FormatPosition(_currentPoint);
			return "[" + PluginLocalization.Translate(GetModeAction()) + "]\n" + text2;
		}
		string name = GetName(target);
		string details = GetDetails(target);
		string source = Mode switch
		{
			PointToolMode.Inspect => "检查",
			PointToolMode.Repair => "维修",
			PointToolMode.Teleport => "准星传送",
			PointToolMode.Utility => "实用",
			PointToolMode.Delete => "按住 Shift + 中键删除",
			_ => string.Empty,
		};
		string text3 = string.Join("\n", details.Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(PluginLocalization.Translate));
		return ((ShowTargetName ? name : string.Empty) + "\n[" + PluginLocalization.Translate(source) + "]\n" + text3).Trim();
	}

	private string BuildSmartHudText(Target target)
	{
		string source = DecideSmartAction() switch
		{
			PointToolAction.Repair => "智能 -> 维修",
			PointToolAction.Utility => "智能 -> 实用",
			PointToolAction.Teleport => "智能 -> 准星传送",
			PointToolAction.Delete => "智能 -> Shift + 中键删除",
			PointToolAction.Inspect => "智能 -> 仅检查",
			_ => "智能 -> 无可用动作",
		};
		string text = ((target == null) ? FormatPosition(_currentPoint) : GetDetails(target));
		string text2 = string.Join("\n", text.Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(PluginLocalization.Translate));
		string text3 = ((target == null || !ShowTargetName) ? string.Empty : (GetName(target) + "\n"));
		return (text3 + "[" + PluginLocalization.Translate(source) + "]\n" + text2).Trim();
	}

	private PointToolAction DecideSmartAction()
	{
		return PointToolActionGate.DecideSmart(new PointToolDecisionInput(_smartHasCoordinateHit, _currentTarget != null, CanRepairAtConfiguredRange(_currentTarget), IsCurrentTargetWithinConfiguredRange() && NeedsRepair(_currentTarget), CanUseAtConfiguredRange(_currentTarget), CanDelete(_currentTarget), Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)));
	}

	private bool IsCurrentTargetWithinConfiguredRange()
	{
		if (!_hasCurrentHit || _currentTarget == null || Player.LocalPlayer == null)
		{
			return false;
		}
		float num = Mathf.Clamp(Range, 5f, 250f);
		return Vector3.Distance(Player.LocalPlayer.transform.position, _currentPoint) <= num + 0.01f;
	}

	private bool CanRepairAtConfiguredRange(Target target)
	{
		if (IsCurrentTargetWithinConfiguredRange())
		{
			return CanRepair(target);
		}
		return false;
	}

	private bool CanUseAtConfiguredRange(Target target)
	{
		if (IsCurrentTargetWithinConfiguredRange())
		{
			return CanUse(target);
		}
		return false;
	}

	private string GetName(Target target)
	{
		object value = target.Value;
		if (!(value is InteractableVehicle interactableVehicle))
		{
			if (!(value is BarricadeDrop barricadeDrop))
			{
				if (!(value is StructureDrop structureDrop))
				{
					if (!(value is ResourceSpawnpoint resourceSpawnpoint))
					{
						if (!(value is Animal animal))
						{
							if (value is Zombie)
							{
								return "Zombie";
							}
							return FindWorldAssetName(target) ?? target.Kind.ToString();
						}
						return animal.asset?.FriendlyName ?? "Animal";
					}
					return resourceSpawnpoint.asset?.FriendlyName ?? "Resource";
				}
				return structureDrop.asset?.FriendlyName ?? "Structure";
			}
			return barricadeDrop.asset?.FriendlyName ?? "Barricade";
		}
		return interactableVehicle.asset?.FriendlyName ?? "Vehicle";
	}

	private string GetDetails(Target target)
	{
		string text = string.Empty;
		string text2 = string.Empty;
		object value = target.Value;
		if (!(value is InteractableVehicle interactableVehicle))
		{
			if (!(value is BarricadeDrop barricadeDrop))
			{
				if (!(value is StructureDrop structureDrop))
				{
					if (!(value is ResourceSpawnpoint resourceSpawnpoint))
					{
						if (!(value is Animal animal))
						{
							if (value is Zombie zombie)
							{
								if (ShowId)
								{
									text = $"实例 ID {zombie.id}";
								}
								if (ShowHealth)
								{
									text2 = $"生命 {zombie.GetHealth():0}/{zombie.GetMaxHealth():0}";
								}
							}
							else
							{
								BarricadeDrop barricadeDrop2 = FindBarricade(target.Transform);
								if (barricadeDrop2 != null)
								{
									if (ShowId)
									{
										text = $"ID {barricadeDrop2.asset?.id}  GUID {barricadeDrop2.asset?.GUID}";
									}
									if (ShowHealth)
									{
										Barricade barricade = barricadeDrop2.GetServersideData()?.barricade;
										text2 = ((barricade == null) ? string.Empty : $"耐久 {barricade.health}/{barricadeDrop2.asset?.health}");
									}
								}
								else
								{
									StructureDrop structureDrop2 = FindStructure(target.Transform);
									if (structureDrop2 != null)
									{
										if (ShowId)
										{
											text = $"ID {structureDrop2.asset?.id}  GUID {structureDrop2.asset?.GUID}";
										}
										if (ShowHealth)
										{
											Structure structure = structureDrop2.GetServersideData()?.structure;
											text2 = ((structure == null) ? string.Empty : $"耐久 {structure.health}/{structureDrop2.asset?.health}");
										}
									}
								}
							}
						}
						else
						{
							if (ShowId)
							{
								text = $"ID {animal.asset?.id}  GUID {animal.asset?.GUID}";
							}
							if (ShowHealth)
							{
								text2 = $"生命 {animal.GetHealth():0}";
							}
						}
					}
					else
					{
						if (ShowId)
						{
							text = $"ID {resourceSpawnpoint.asset?.id}  GUID {resourceSpawnpoint.asset?.GUID}";
						}
						if (ShowHealth)
						{
							text2 = $"耐久 {resourceSpawnpoint.health}";
						}
					}
				}
				else
				{
					if (ShowId)
					{
						text = $"ID {structureDrop.asset?.id}  GUID {structureDrop.asset?.GUID}";
					}
					if (ShowHealth)
					{
						Structure structure2 = structureDrop.GetServersideData()?.structure;
						text2 = ((structure2 == null) ? string.Empty : $"耐久 {structure2.health}/{structureDrop.asset?.health}");
					}
				}
			}
			else
			{
				if (ShowId)
				{
					text = $"ID {barricadeDrop.asset?.id}  GUID {barricadeDrop.asset?.GUID}";
				}
				if (ShowHealth)
				{
					Barricade barricade2 = barricadeDrop.GetServersideData()?.barricade;
					text2 = ((barricade2 == null) ? string.Empty : $"耐久 {barricade2.health}/{barricadeDrop.asset?.health}");
				}
			}
		}
		else
		{
			if (ShowId)
			{
				text = $"ID {interactableVehicle.asset?.id}  GUID {interactableVehicle.asset?.GUID}";
			}
			if (ShowHealth && interactableVehicle.asset != null)
			{
				text2 = $"耐久 {interactableVehicle.health}/{interactableVehicle.asset.health}";
			}
		}
		return string.Join("\n", text, text2).Trim();
	}

	private void ExecuteCurrentTarget()
	{
		if (!SingleplayerGuard.IsReady)
		{
			return;
		}
		bool deleteModifierHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		if (Mode == PointToolMode.Smart)
		{
			ExecuteSmartTarget(deleteModifierHeld);
			return;
		}
		PointToolActionGate.Failure failure = PointToolActionGate.GetFailure(Mode, _hasCurrentHit, _currentTarget != null, deleteModifierHeld);
		if (failure != PointToolActionGate.Failure.None)
		{
			ShowFailureMessage(failure);
			return;
		}
		if ((Mode == PointToolMode.Repair || Mode == PointToolMode.Utility) && !IsCurrentTargetWithinConfiguredRange())
		{
			ShowFailureMessage(PointToolActionGate.Failure.NoSemanticTarget);
			return;
		}
		try
		{
			bool flag = Mode switch
			{
				PointToolMode.Inspect => InspectCurrentTarget(),
				PointToolMode.Repair => Repair(_currentTarget),
				PointToolMode.Teleport => TeleportToCurrentCoordinate(),
				PointToolMode.Utility => Use(_currentTarget),
				PointToolMode.Delete => Delete(_currentTarget),
				_ => false,
			};
			if (Mode != PointToolMode.Inspect)
			{
				if (Mode == PointToolMode.Teleport)
				{
					SetInspectionMessage(flag ? (PluginLocalization.Translate("传送成功") + "\n" + FormatPosition(_currentPoint)) : (PluginLocalization.Translate("传送失败") + "\n" + PluginLocalization.Translate("没有可用的安全落点")), 3f);
				}
				else if (!flag && _currentTarget == null)
				{
					ShowFailureMessage(PointToolActionGate.Failure.NoSemanticTarget);
				}
				if (!flag && Mode != PointToolMode.Inspect && Mode != PointToolMode.Teleport && _currentTarget != null)
				{
					_log.LogWarning($"[PointTool] 当前目标 {_currentTarget.Kind} 不支持 {Mode}，未修改游戏状态。");
				}
			}
		}
		catch (Exception ex)
		{
			string arg = _currentTarget?.Kind.ToString() ?? "WorldHit";
			_log.LogWarning($"[PointTool] 执行 {arg}/{Mode} 失败：{ex.Message}");
		}
	}

	private void ExecuteSmartTarget(bool deleteModifierHeld)
	{
		PointToolAction pointToolAction = PointToolActionGate.DecideSmart(new PointToolDecisionInput(_smartHasCoordinateHit, _currentTarget != null, CanRepairAtConfiguredRange(_currentTarget), IsCurrentTargetWithinConfiguredRange() && NeedsRepair(_currentTarget), CanUseAtConfiguredRange(_currentTarget), CanDelete(_currentTarget), deleteModifierHeld));
		_log.LogInfo(string.Format("[PointTool] mode=Smart, action={0}, target={1}, ", pointToolAction, _currentTarget?.Kind.ToString() ?? "WorldHit") + $"reason={GetSmartReason(pointToolAction, deleteModifierHeld)}, frame={_smartCapturedFrame}, " + $"rayOrigin={_smartRayOrigin}, rayDirection={_smartRayDirection}.");
		switch (pointToolAction)
		{
		case PointToolAction.None:
		{
			string source = (deleteModifierHeld ? "当前目标不可删除" : "未命中世界表面");
			SetInspectionMessage("[" + PluginLocalization.Translate("智能 -> 无可用动作") + "]\n" + PluginLocalization.Translate(source), 3f);
			return;
		}
		case PointToolAction.Inspect:
			InspectCurrentTarget();
			return;
		}
		bool flag;
		try
		{
			flag = ExecuteAction(pointToolAction);
		}
		catch (Exception arg)
		{
			_log.LogWarning(string.Format("[PointTool] Smart action failed: action={0}, target={1}.\n{2}", pointToolAction, _currentTarget?.Kind.ToString() ?? "WorldHit", arg));
			SetInspectionMessage("[" + PluginLocalization.Translate(GetSmartActionLabel(pointToolAction)) + "]\n" + PluginLocalization.Translate("执行失败"), 3f);
			return;
		}
		SetInspectionMessage("[" + PluginLocalization.Translate(GetSmartActionLabel(pointToolAction)) + "]\n" + PluginLocalization.Translate(flag ? "执行成功" : "执行失败"), 3f);
		try
		{
			RefreshRaycast();
		}
		catch (Exception arg2)
		{
			_log.LogWarning($"[PointTool] Smart post-action refresh failed: action={pointToolAction}, success={flag}.\n{arg2}");
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
			_ => false,
		};
	}

	private bool TeleportToSmartCoordinate()
	{
		if (!_smartHasCoordinateHit)
		{
			return false;
		}
		_currentHit = _smartCoordinateHit;
		_currentPoint = _smartCoordinatePoint;
		_hasCurrentHit = true;
		return TeleportToCurrentCoordinate();
	}

	private static string GetSmartActionLabel(PointToolAction action)
	{
		return action switch
		{
			PointToolAction.Repair => "智能 -> 维修",
			PointToolAction.Utility => "智能 -> 实用",
			PointToolAction.Teleport => "智能 -> 准星传送",
			PointToolAction.Delete => "智能 -> 删除",
			_ => "智能 -> 仅检查",
		};
	}

	private string GetSmartReason(PointToolAction action, bool deleteModifierHeld)
	{
		switch (action)
		{
		case PointToolAction.Delete:
			return "delete-modifier-and-supported-target";
		case PointToolAction.Repair:
			return "repairable-target-is-damaged";
		case PointToolAction.Utility:
			return "healthy-or-nonrepairable-usable-target";
		case PointToolAction.Teleport:
			if (_currentTarget != null)
			{
				return "coordinate-ray-ignores-semantic-target";
			}
			return "world-surface-coordinate-candidate";
		case PointToolAction.Inspect:
			return "semantic-target-has-no-automatic-action";
		default:
			if (deleteModifierHeld)
			{
				return "delete-modifier-without-deletable-target";
			}
			return "no-world-hit";
		}
	}

	private bool TeleportToCurrentCoordinate()
	{
		if (!_hasCurrentHit)
		{
			return false;
		}
		Vector3 vector = Player.LocalPlayer?.transform.position ?? Vector3.zero;
		Vector3 horizontalPosition = new Vector3(_currentPoint.x, 0f, _currentPoint.z);
		bool flag = CheatMenuPlugin.Instance.Actions.TeleportToMapPosition(horizontalPosition, out var landingPoint);
		string arg = ((Mode == PointToolMode.Smart) ? GetSmartCoordinateSource() : ((_currentHit.collider == null) ? "TerrainHeightFallback" : _currentHit.collider.name));
		int num = _currentHit.collider?.gameObject.layer ?? (-1);
		_log.LogInfo($"[PointTeleportTrace] request: before={vector}, " + $"hit={_currentPoint}, normal={_currentHit.normal}, " + $"collider={arg}, layer={num}, " + $"landing={landingPoint}, apiAccepted={flag}.");
		if (flag)
		{
			_hasPendingTeleportTrace = true;
			_pendingTeleportTraceAt = Time.unscaledTime + 0.25f;
			_pendingTeleportBefore = vector;
			_pendingTeleportRequested = _currentPoint;
			_pendingTeleportLanding = landingPoint;
		}
		return flag;
	}

	private void FlushPendingTeleportTrace()
	{
		if (_hasPendingTeleportTrace && !(Time.unscaledTime < _pendingTeleportTraceAt))
		{
			_hasPendingTeleportTrace = false;
			Vector3 vector = Player.LocalPlayer?.transform.position ?? Vector3.zero;
			_log.LogInfo($"[PointTeleportTrace] result: before={_pendingTeleportBefore}, " + $"requested={_pendingTeleportRequested}, landing={_pendingTeleportLanding}, " + $"actual={vector}, moved={Vector3.Distance(_pendingTeleportBefore, vector):0.00}m, " + $"landingError={Vector3.Distance(_pendingTeleportLanding, vector):0.00}m.");
		}
	}

	private void ShowFailureMessage(PointToolActionGate.Failure failure)
	{
		string text = failure switch
		{
			PointToolActionGate.Failure.NoWorldHit => (Mode != PointToolMode.Teleport) ? (PluginLocalization.Translate("检查失败") + "\n" + PluginLocalization.Translate("未命中目标")) : (PluginLocalization.Translate("传送失败") + "\n" + PluginLocalization.Translate("未命中世界表面")),
			PointToolActionGate.Failure.NoSemanticTarget => PluginLocalization.Translate(GetFailureActionKey()) + "\n" + PluginLocalization.Translate("没有可支持的语义目标"),
			PointToolActionGate.Failure.DeleteModifierRequired => PluginLocalization.Translate("删除失败") + "\n" + PluginLocalization.Translate("删除需要按住 Shift"),
			_ => string.Empty,
		};
		if (!string.IsNullOrWhiteSpace(text))
		{
			SetInspectionMessage(text, 3f);
		}
	}

	private string GetFailureActionKey()
	{
		return Mode switch
		{
			PointToolMode.Repair => "维修失败",
			PointToolMode.Utility => "实用失败",
			PointToolMode.Delete => "删除失败",
			_ => "检查失败",
		};
	}

	private bool InspectCurrentTarget()
	{
		if (!_hasCurrentHit)
		{
			ShowFailureMessage(PointToolActionGate.Failure.NoWorldHit);
			return false;
		}
		SetInspectionMessage(PluginLocalization.Translate("检查成功") + "\n" + BuildHudText(_currentTarget), 3f);
		_log.LogInfo((_currentTarget == null) ? $"[PointTool] Inspected world surface at {_currentPoint}." : $"[PointTool] Inspected {_currentTarget.Kind} at {_currentPoint}.");
		return true;
	}

	private void SetInspectionMessage(string message, float seconds)
	{
		_inspectionMessage = message;
		_inspectionMessageUntil = Time.unscaledTime + Mathf.Max(0.25f, seconds);
		if (ShowHud)
		{
			_hud.SetPoint(message);
		}
	}

	private static string FormatPosition(Vector3 position)
	{
		return PluginLocalization.Translate("位置") + " " + $"{position.x:0.0}, {position.y:0.0}, {position.z:0.0}";
	}

	private bool Repair(Target target)
	{
		object value = target.Value;
		if (!(value is InteractableVehicle vehicle))
		{
			if (!(value is BarricadeDrop barricadeDrop))
			{
				if (value is StructureDrop structureDrop)
				{
					return InvokeRepair(typeof(StructureManager), structureDrop.model);
				}
				BarricadeDrop barricadeDrop2 = FindBarricade(target.Transform);
				if (barricadeDrop2 != null)
				{
					return Repair(new Target(TargetKind.Barricade, barricadeDrop2, barricadeDrop2.model));
				}
				StructureDrop structureDrop2 = FindStructure(target.Transform);
				if (structureDrop2 != null)
				{
					return Repair(new Target(TargetKind.Structure, structureDrop2, structureDrop2.model));
				}
				return false;
			}
			return InvokeRepair(typeof(BarricadeManager), barricadeDrop.model);
		}
		return RepairVehicle(vehicle);
	}

	private bool Use(Target target)
	{
		if (target == null || target.Value == null)
		{
			return false;
		}
		float num = Mathf.Clamp(Range, 5f, 250f);
		float num2 = ((Player.LocalPlayer == null) ? 0f : Vector3.Distance(Player.LocalPlayer.transform.position, _currentPoint));
		bool flag2;
		using (PointToolInteractionPatch.BeginRangeOverride(num))
		{
			object value = target.Value;
			bool flag = ((value is InteractableVehicle vehicle) ? UseVehicle(vehicle) : ((value is InteractableStorage storage) ? UseStorage(storage) : ((value is InteractableDoor door) ? ToggleDoor(door) : ((value is InteractableGenerator generator) ? ToggleGenerator(generator) : ((value is InteractableSpot spot) ? ToggleSpot(spot) : ((value is InteractableFire fire) ? ToggleFire(fire) : ((value is InteractableOven oven) ? ToggleOven(oven) : ((value is InteractableOxygenator oxygenator) ? ToggleOxygenator(oxygenator) : (value is InteractableSafezone safezone && ToggleSafezone(safezone))))))))));
			flag2 = flag;
		}
		_log.LogInfo($"[PointToolUseTrace] target={target.Kind}, range={num:0.##}m, " + $"targetDistance={num2:0.##}m, success={flag2}.");
		return flag2;
	}

	private static bool UseVehicle(InteractableVehicle vehicle)
	{
		vehicle.use();
		return Player.LocalPlayer?.movement?.getVehicle() == vehicle;
	}

	private static bool RepairVehicle(InteractableVehicle vehicle)
	{
		if (!IsRegisteredVehicle(vehicle))
		{
			return false;
		}
		bool flag = false;
		if (vehicle.asset.health != 0 && vehicle.health < vehicle.asset.health)
		{
			vehicle.askRepair((ushort)(vehicle.asset.health - vehicle.health));
			flag |= vehicle.health >= vehicle.asset.health;
		}
		if (vehicle.usesFuel && vehicle.fuel < vehicle.asset.fuel)
		{
			vehicle.askFillFuel((ushort)(vehicle.asset.fuel - vehicle.fuel));
			flag |= vehicle.fuel >= vehicle.asset.fuel;
		}
		if (IsVehicleBatteryBelowDisplayedFull(vehicle))
		{
			vehicle.askChargeBattery((ushort)(10000 - vehicle.batteryCharge));
			vehicle.sendBatteryChargeUpdate();
			flag |= vehicle.batteryCharge >= 10000;
		}
		Wheel[] tires = vehicle.tires;
		if (tires != null)
		{
			for (int i = 0; i < tires.Length; i++)
			{
				if (tires[i] != null && !tires[i].isAlive)
				{
					vehicle.askRepairTire(i);
					flag |= tires[i].isAlive;
				}
			}
		}
		return UprightVehicle(vehicle) | flag;
	}

	private static bool UprightVehicle(InteractableVehicle vehicle)
	{
		if (!TryGetUprightRotation(vehicle, out var uprightRotation))
		{
			return false;
		}
		Transform transform = vehicle.transform;
		Rigidbody component = vehicle.GetComponent<Rigidbody>();
		if (component != null)
		{
			component.velocity = Vector3.zero;
			component.angularVelocity = Vector3.zero;
			component.rotation = uprightRotation;
		}
		else
		{
			transform.rotation = uprightRotation;
		}
		vehicle.MarkForReplicationUpdate();
		return true;
	}

	private static bool TryGetUprightRotation(InteractableVehicle vehicle, out Quaternion uprightRotation)
	{
		uprightRotation = default(Quaternion);
		if (vehicle?.asset == null || vehicle.asset.engine == EEngine.TRAIN)
		{
			return false;
		}
		Transform transform = vehicle.transform;
		if (transform == null || transform.up.y >= 0.5f)
		{
			return false;
		}
		Vector3 vector = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
		if (vector.sqrMagnitude < 0.0001f)
		{
			return false;
		}
		uprightRotation = Quaternion.LookRotation(vector.normalized, Vector3.up);
		return Quaternion.Angle(transform.rotation, uprightRotation) > 0.5f;
	}

	private static bool DeleteVehicle(InteractableVehicle vehicle)
	{
		if (!IsRegisteredVehicle(vehicle) || vehicle.anySeatsOccupied)
		{
			return false;
		}
		VehicleManager.askVehicleDestroy(vehicle);
		return true;
	}

	private static bool IsRegisteredVehicle(InteractableVehicle vehicle)
	{
		if (!SingleplayerGuard.IsReady || vehicle == null || vehicle.asset == null || vehicle.IsPendingDestroy || vehicle.isExploded)
		{
			return false;
		}
		if (VehicleManager.vehicles != null && VehicleManager.vehicles.Contains(vehicle))
		{
			return true;
		}
		if (vehicle.instanceID != 0)
		{
			return (object)VehicleManager.findVehicleByNetInstanceID(vehicle.instanceID) == vehicle;
		}
		return false;
	}

	private static bool UseStorage(InteractableStorage storage)
	{
		PlayerInventory playerInventory = Player.LocalPlayer?.inventory;
		storage.ClientInteract(quickGrab: false);
		if (playerInventory != null && playerInventory.isStoring)
		{
			return playerInventory.storage == storage;
		}
		return false;
	}

	private static bool ToggleDoor(InteractableDoor door)
	{
		bool isOpen = door.isOpen;
		door.ClientToggle();
		return door.isOpen != isOpen;
	}

	private static bool ToggleGenerator(InteractableGenerator generator)
	{
		bool isPowered = generator.isPowered;
		generator.ClientToggle();
		return generator.isPowered != isPowered;
	}

	private static bool ToggleSpot(InteractableSpot spot)
	{
		bool isPowered = spot.isPowered;
		spot.ClientToggle();
		return spot.isPowered != isPowered;
	}

	private static bool ToggleFire(InteractableFire fire)
	{
		bool isLit = fire.isLit;
		fire.ClientToggle();
		return fire.isLit != isLit;
	}

	private static bool ToggleOven(InteractableOven oven)
	{
		bool isLit = oven.isLit;
		oven.ClientToggle();
		return oven.isLit != isLit;
	}

	private static bool ToggleOxygenator(InteractableOxygenator oxygenator)
	{
		bool isPowered = oxygenator.isPowered;
		oxygenator.ClientToggle();
		return oxygenator.isPowered != isPowered;
	}

	private static bool ToggleSafezone(InteractableSafezone safezone)
	{
		bool isPowered = safezone.isPowered;
		safezone.ClientToggle();
		return safezone.isPowered != isPowered;
	}

	private static bool Delete(Target target)
	{
		object value = target.Value;
		if (!(value is InteractableVehicle vehicle))
		{
			if (!(value is BarricadeDrop barricadeDrop))
			{
				if (!(value is StructureDrop structureDrop))
				{
					if (value is ResourceSpawnpoint resourceSpawnpoint)
					{
						if (!ResourceManager.tryGetRegion(resourceSpawnpoint.model, out var x, out var y, out var index))
						{
							return false;
						}
						ResourceManager.ServerSetResourceDead(x, y, index, Vector3.zero);
						return true;
					}
					BarricadeDrop barricadeDrop2 = FindBarricade(target.Transform);
					if (barricadeDrop2 != null)
					{
						return Delete(new Target(TargetKind.Barricade, barricadeDrop2, barricadeDrop2.model));
					}
					StructureDrop structureDrop2 = FindStructure(target.Transform);
					if (structureDrop2 != null)
					{
						return Delete(new Target(TargetKind.Structure, structureDrop2, structureDrop2.model));
					}
					return false;
				}
				StructureDrop resolvedStructureDrop = StructureManager.FindStructureByRootTransform(structureDrop.model);
				if (resolvedStructureDrop == null || !StructureManager.tryGetRegion(resolvedStructureDrop.model, out var x2, out var y2, out var _))
				{
					return false;
				}
				StructureManager.destroyStructure(resolvedStructureDrop, x2, y2, Vector3.zero, wasPickedUp: false);
				return true;
			}
			BarricadeDrop resolvedBarricadeDrop = BarricadeManager.FindBarricadeByRootTransform(barricadeDrop.model);
			if (resolvedBarricadeDrop == null || !BarricadeManager.tryGetRegion(resolvedBarricadeDrop.model, out var x3, out var y3, out var plant, out var _))
			{
				return false;
			}
			BarricadeManager.destroyBarricade(resolvedBarricadeDrop, x3, y3, plant);
			return true;
		}
		return DeleteVehicle(vehicle);
	}

	private static bool CanRepair(Target target)
	{
		object obj = target?.Value;
		if ((!(obj is InteractableVehicle) && !(obj is BarricadeDrop) && !(obj is StructureDrop)) || 1 == 0)
		{
			if (target != null)
			{
				if (FindBarricade(target.Transform) == null)
				{
					return FindStructure(target.Transform) != null;
				}
				return true;
			}
			return false;
		}
		return true;
	}

	private static bool NeedsRepair(Target target)
	{
		object obj = target?.Value;
		if (!(obj is InteractableVehicle vehicle))
		{
			if (!(obj is BarricadeDrop barricadeDrop))
			{
				if (obj is StructureDrop structureDrop)
				{
					Structure structure = structureDrop.GetServersideData()?.structure;
					if (structure != null && structureDrop.asset != null)
					{
						return structure.health < structureDrop.asset.health;
					}
					return false;
				}
				BarricadeDrop barricadeDrop2 = ((target == null) ? null : FindBarricade(target.Transform));
				if (barricadeDrop2 != null)
				{
					return NeedsRepair(new Target(TargetKind.Barricade, barricadeDrop2, barricadeDrop2.model));
				}
				StructureDrop structureDrop2 = ((target == null) ? null : FindStructure(target.Transform));
				if (structureDrop2 != null)
				{
					return NeedsRepair(new Target(TargetKind.Structure, structureDrop2, structureDrop2.model));
				}
				return false;
			}
			Barricade barricade = barricadeDrop.GetServersideData()?.barricade;
			if (barricade != null && barricadeDrop.asset != null)
			{
				return barricade.health < barricadeDrop.asset.health;
			}
			return false;
		}
		if (IsRegisteredVehicle(vehicle))
		{
			return VehicleNeedsRepair(vehicle);
		}
		return false;
	}

	private static bool VehicleNeedsRepair(InteractableVehicle vehicle)
	{
		if (vehicle.asset.health != 0 && vehicle.health < vehicle.asset.health)
		{
			return true;
		}
		if (vehicle.usesFuel && vehicle.fuel < vehicle.asset.fuel)
		{
			return true;
		}
		if (IsVehicleBatteryBelowDisplayedFull(vehicle))
		{
			return true;
		}
		Wheel[] tires = vehicle.tires;
		if (tires != null)
		{
			for (int i = 0; i < tires.Length; i++)
			{
				if (tires[i] != null && !tires[i].isAlive)
				{
					return true;
				}
			}
		}
		Quaternion uprightRotation;
		return TryGetUprightRotation(vehicle, out uprightRotation);
	}

	private static bool IsVehicleBatteryBelowDisplayedFull(InteractableVehicle vehicle)
	{
		if (vehicle.usesBattery)
		{
			return vehicle.batteryCharge < 9950;
		}
		return false;
	}

	private static bool CanUse(Target target)
	{
		object obj = target?.Value;
		if (obj is InteractableVehicle || obj is InteractableStorage || obj is InteractableDoor || obj is InteractableGenerator || obj is InteractableSpot || obj is InteractableFire || obj is InteractableOven || obj is InteractableOxygenator || obj is InteractableSafezone)
		{
			return true;
		}
		return false;
	}

	private static bool CanDelete(Target target)
	{
		object obj = target?.Value;
		if ((!(obj is InteractableVehicle) && !(obj is BarricadeDrop) && !(obj is StructureDrop) && !(obj is ResourceSpawnpoint)) || 1 == 0)
		{
			if (target != null)
			{
				if (FindBarricade(target.Transform) == null)
				{
					return FindStructure(target.Transform) != null;
				}
				return true;
			}
			return false;
		}
		return true;
	}

	private static string FindWorldAssetName(Target target)
	{
		object obj = FindBarricade(target.Transform)?.asset?.FriendlyName;
		if (obj == null)
		{
			StructureDrop structureDrop = FindStructure(target.Transform);
			if (structureDrop == null)
			{
				return null;
			}
			ItemStructureAsset asset = structureDrop.asset;
			if (asset == null)
			{
				return null;
			}
			obj = asset.FriendlyName;
		}
		return (string)obj;
	}

	private static BarricadeDrop FindBarricade(Transform transform)
	{
		Transform transform2 = transform;
		while (transform2 != null)
		{
			BarricadeDrop barricadeDrop = BarricadeManager.FindBarricadeByRootTransform(transform2);
			if (barricadeDrop != null)
			{
				return barricadeDrop;
			}
			transform2 = transform2.parent;
		}
		return null;
	}

	private static StructureDrop FindStructure(Transform transform)
	{
		Transform transform2 = transform;
		while (transform2 != null)
		{
			StructureDrop structureDrop = StructureManager.FindStructureByRootTransform(transform2);
			if (structureDrop != null)
			{
				return structureDrop;
			}
			transform2 = transform2.parent;
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
		_currentHit = default(RaycastHit);
		_currentPoint = default(Vector3);
		_smartCoordinateHit = default(RaycastHit);
		_smartCoordinatePoint = default(Vector3);
		_smartHasCoordinateHit = false;
		_smartSemanticHit = false;
		_smartSemanticResolveSucceeded = false;
		_smartCoordinatePhysicsHit = false;
		_smartCoordinateSemanticFallback = false;
		_smartCoordinateTerrainFallback = false;
		_smartSnapshot = default(PointToolRaycastSnapshot);
		_smartRayOrigin = default(Vector3);
		_smartRayDirection = default(Vector3);
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
		MethodInfo method = managerType.GetMethod("repair", BindingFlags.Static | BindingFlags.Public, null, new Type[3]
		{
			typeof(Transform),
			typeof(float),
			typeof(float)
		}, null);
		if (method == null)
		{
			return false;
		}
		method.Invoke(null, new object[3] { transform, 65535f, 1f });
		return true;
	}

	private static bool TryComponent<T>(Collider collider, out T component) where T : Component
	{
		component = collider.GetComponentInParent<T>();
		return component != null;
	}
}
