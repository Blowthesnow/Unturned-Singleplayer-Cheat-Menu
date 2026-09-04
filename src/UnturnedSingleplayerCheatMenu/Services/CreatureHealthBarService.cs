using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.UI;

namespace UnturnedSingleplayerCheatMenu.Services;

#pragma warning disable CS0618
internal sealed class CreatureHealthBarService : IDisposable
{
	private readonly ManualLogSource _log;
	private readonly CreatureHealthBarHud _hud;
	private readonly Func<bool> _canExecuteGameplay;
	private readonly Dictionary<int, CreatureHealthBarState> _states = new Dictionary<int, CreatureHealthBarState>();
	private readonly List<CreatureHealthBarRenderData> _renderBuffer = new List<CreatureHealthBarRenderData>();
	private readonly RaycastHit[] _occlusionHits = new RaycastHit[32];
	private CreatureHealthBarState _crosshairTarget;
	private float _nextCrosshairRefresh;
	private float _nextHealthRefresh;
	private float _nextOcclusionRefresh;
	private bool _subscribed;
	private bool _loggedInitialization;
	private bool _wasReady;
	private bool _disposed;
	private float _nextRuntimeDiagnostic;
	private float _nextCrosshairDiagnostic;
	private float _nextScopeProjectionDiagnostic;
	private readonly HashSet<int> _invalidMaxLogged = new HashSet<int>();
	private readonly HashSet<int> _boundsLogged = new HashSet<int>();

	internal CreatureHealthBarSettings Settings { get; }

	internal CreatureHealthBarService(ManualLogSource log, CreatureHealthBarSettings settings, CreatureHealthBarHud hud,
		Func<bool> canExecuteGameplay)
	{
		_log = log;
		Settings = settings.Normalize();
		_hud = hud;
		_canExecuteGameplay = canExecuteGameplay ?? throw new ArgumentNullException(nameof(canExecuteGameplay));
		Subscribe();
		_log.LogInfo($"[CreatureHealthBar] initialized: enabled={Settings.Enabled}, duration={Settings.DamageDisplayDuration:0.##}s, crosshairRange={Settings.CrosshairRange:0.##}m, displayRange={Settings.DisplayRange:0.##}m, maxBars={Settings.MaxVisibleBars}, scaleReference={Settings.BarScaleReferenceDistance:0.##}m, maxScale={Settings.MaxBarScale:0.##}, Assembly-CSharp={typeof(Zombie).Assembly.GetName().Version}.");
	}

	internal void Pump()
	{
		if (_disposed)
		{
			return;
		}
		bool ready = _canExecuteGameplay();
		float now = Time.unscaledTime;
		if (Settings.Diagnostics && now >= _nextRuntimeDiagnostic)
		{
			_nextRuntimeDiagnostic = now + 2f;
			bool menuOpen = CheatMenuPlugin.Instance?.IsMenuOpen == true;
			RuntimeSessionState session = CheatMenuPlugin.Instance?.CurrentSessionState ??
				new RuntimeSessionState(RuntimeSessionKind.Frontend, string.Empty, false);
			_log.LogInfo($"[CreatureHealthBar] runtime: ready={ready}, session={session.Kind}, supported={session.IsSupportedGameplay}, acknowledged={CheatMenuPlugin.Instance?.IsMultiplayerRiskAcknowledged == true}, enabled={Settings.Enabled}, menuOpen={menuOpen}");
		}
		if (!ready)
		{
			if (_wasReady)
			{
				ClearWorldState();
			}
			_wasReady = false;
			_hud.HideAll();
			return;
		}
		_wasReady = true;
		if (!Settings.Enabled || CheatMenuPlugin.Instance?.IsMenuOpen == true)
		{
			ClearCrosshairTarget();
			_hud.HideAll();
			return;
		}

		if (!_loggedInitialization)
		{
			_loggedInitialization = true;
			_log.LogInfo($"[CreatureHealthBar] active: duration={Settings.DamageDisplayDuration:0.##}s, range={Settings.DisplayRange:0.##}m, maxBars={Settings.MaxVisibleBars}, Assembly-CSharp={typeof(Zombie).Assembly.GetName().Version}.");
		}
		if (now >= _nextCrosshairRefresh)
		{
			_nextCrosshairRefresh = now + 1f / 30f;
			RefreshCrosshairTarget();
		}
		ConfirmDamageCandidates(now);
		if (now >= _nextHealthRefresh)
		{
			_nextHealthRefresh = now + 1f / Settings.HealthRefreshHz;
			RefreshHealth(now);
		}
		if (now >= _nextOcclusionRefresh)
		{
			_nextOcclusionRefresh = now + 1f / Settings.OcclusionCheckHz;
			RefreshOcclusion(now);
		}
		PruneStates(now);
	}

	internal void RenderLate()
	{
		if (_disposed)
		{
			return;
		}
		if (!Settings.Enabled || !_canExecuteGameplay() || PointToolService.IsInteractionPaused())
		{
			_hud.HideAll();
			return;
		}
		Render(Time.unscaledTime);
	}

	internal void SetEnabled(bool enabled)
	{
		if (Settings.Enabled == enabled)
		{
			return;
		}
		Settings.Enabled = enabled;
		if (!enabled)
		{
			ClearWorldState();
			_hud.HideAll();
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		if (_subscribed)
		{
			DamageTool.zombieDamaged -= OnZombieDamaged;
			DamageTool.animalDamaged -= OnAnimalDamaged;
			_subscribed = false;
		}
		_states.Clear();
		ClearCrosshairTarget();
		_invalidMaxLogged.Clear();
		_boundsLogged.Clear();
		CreatureBoundsResolver.Clear();
		_hud.ResetWorld();
		_hud.Dispose();
	}

	private void Subscribe()
	{
		if (_subscribed)
		{
			return;
		}
		DamageTool.zombieDamaged += OnZombieDamaged;
		DamageTool.animalDamaged += OnAnimalDamaged;
		_subscribed = true;
		_log.LogInfo("[CreatureHealthBar] DamageTool zombie/animal callbacks subscribed.");
	}

	private void OnZombieDamaged(Zombie zombie, ref Vector3 direction, ref float damage, ref float times, ref bool shouldAllow)
	{
		RecordDamageCandidate(zombie, CreatureHealthBarKind.Zombie);
	}

	private void OnAnimalDamaged(Animal animal, ref Vector3 direction, ref float damage, ref float times, ref bool shouldAllow)
	{
		RecordDamageCandidate(animal, CreatureHealthBarKind.Animal);
	}

	private void RecordDamageCandidate(Component creature, CreatureHealthBarKind kind)
	{
		if (creature == null || !Settings.Enabled || !_canExecuteGameplay())
		{
			return;
		}
		int id = creature.GetInstanceID();
		CreatureHealthBarState state = GetOrCreateState(creature, kind);
		float before = GetCurrentHealth(creature, kind);
		if (!state.HasDamageCandidate)
		{
			state.CandidateBeforeHealth = before;
		}
		else
		{
			state.CandidateBeforeHealth = Mathf.Max(state.CandidateBeforeHealth, before);
		}
		state.CandidateAt = Time.unscaledTime;
		state.HasDamageCandidate = true;
		_states[id] = state;
	}

	private void ConfirmDamageCandidates(float now)
	{
		foreach (CreatureHealthBarState state in _states.Values.ToArray())
		{
			if (!state.HasDamageCandidate || now <= state.CandidateAt)
			{
				continue;
			}
			Component creature = state.Creature as Component;
			if (!IsValid(creature, state.Kind))
			{
				RemoveState(state);
				continue;
			}
			float current = GetCurrentHealth(creature, state.Kind);
			state.ConfirmDamage(current, now, Settings.DamageDisplayDuration);
		}
	}

	private void RefreshCrosshairTarget()
	{
		ClearCrosshairTarget();
		Camera camera = GetTargetingCamera();
		if (camera == null)
		{
			if (Settings.Diagnostics)
			{
				_log.LogWarning("[CreatureHealthBar] crosshair skipped: no camera.");
			}
			return;
		}
		Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
		RaycastInfo damageHit = DamageTool.raycast(ray, Settings.CrosshairRange, RayMasks.BLOCK_STANCE | RayMasks.DAMAGE_CLIENT, Player.LocalPlayer);
		Component creature = null;
		CreatureHealthBarKind kind = default;
		if (damageHit.zombie != null)
		{
			creature = damageHit.zombie;
			kind = CreatureHealthBarKind.Zombie;
		}
		else if (damageHit.animal != null)
		{
			creature = damageHit.animal;
			kind = CreatureHealthBarKind.Animal;
		}
		if (creature == null)
		{
			if (Settings.Diagnostics && Time.unscaledTime >= _nextCrosshairDiagnostic)
			{
				_nextCrosshairDiagnostic = Time.unscaledTime + 2f;
				string targetName = damageHit.transform != null ? damageHit.transform.name : "None";
				_log.LogInfo($"[CreatureHealthBar] bullet ray hit non-creature: transform={targetName}.");
			}
			return;
		}
		if (!IsConfiguredKind(kind) || !IsValid(creature, kind))
		{
			return;
		}
		_crosshairTarget = GetOrCreateState(creature, kind);
		if (Settings.Diagnostics && Time.unscaledTime >= _nextCrosshairDiagnostic)
		{
			_nextCrosshairDiagnostic = Time.unscaledTime + 2f;
			_log.LogInfo($"[CreatureHealthBar] crosshair target: kind={kind}, instance={_crosshairTarget.InstanceId}, health={_crosshairTarget.LastKnownHealth:0.##}/{_crosshairTarget.MaxHealth:0.##}.");
		}
		_crosshairTarget.IsCrosshairTarget = true;
		_nextOcclusionRefresh = 0f;
	}

	private void RefreshHealth(float now)
	{
		Camera camera = GetProjectionCamera();
		if (camera == null)
		{
			return;
		}
		foreach (CreatureHealthBarState state in _states.Values.ToArray())
		{
			Component creature = state.Creature as Component;
			if (!IsValid(creature, state.Kind))
			{
				RemoveState(state);
				continue;
			}
			state.SetHealth(GetCurrentHealth(creature, state.Kind), GetMaxHealth(creature, state.Kind));
			if (!state.IsHealthUsable && Settings.Diagnostics && _invalidMaxLogged.Add(state.InstanceId))
			{
				_log.LogWarning($"[CreatureHealthBar] hidden invalid maximum: kind={state.Kind}, instance={state.InstanceId}, max={state.MaxHealth}.");
			}
			state.DistanceSqr = (creature.transform.position - camera.transform.position).sqrMagnitude;
		}
	}

	private void RefreshOcclusion(float now)
	{
		Camera camera = GetProjectionCamera();
		if (camera == null)
		{
			return;
		}
		foreach (CreatureHealthBarState state in _states.Values)
		{
			state.IsRenderCandidate = false;
		}
		IReadOnlyList<CreatureHealthBarState> candidates = CreatureHealthBarSelection.SelectCandidates(
			_states.Values.Where(state => IsConfiguredKind(state.Kind)),
			now,
			Settings.DisplayRange * Settings.DisplayRange,
			Settings.MaxVisibleBars);
		foreach (CreatureHealthBarState state in candidates)
		{
			state.IsRenderCandidate = true;
			Component creature = state.Creature as Component;
			if (Settings.Diagnostics && _boundsLogged.Add(state.InstanceId))
			{
				_log.LogInfo($"[CreatureHealthBar] bounds: kind={state.Kind}, instance={state.InstanceId}, {CreatureBoundsResolver.GetDiagnostics(creature)}.");
			}
			Vector3 anchor = CreatureBoundsResolver.Resolve(creature);
			Vector3 direction = anchor - camera.transform.position;
			float distance = direction.magnitude;
			state.IsOccluded = distance > 0.01f && IsOccluded(camera.transform.position, direction / distance, distance, creature);
		}
	}

	private void Render(float now)
	{
		Camera camera = GetProjectionCamera();
		if (camera == null)
		{
			_hud.HideAll();
			return;
		}
		bool hasScopeProjection = TryGetScopeProjection(out Camera scopeProjectionCamera, out Rect scopeScreenRect, out bool isSingleRenderScope);
		IReadOnlyList<CreatureHealthBarState> visible = CreatureHealthBarSelection.Select(
			_states.Values.Where(state => IsConfiguredKind(state.Kind)),
			now,
			Settings.DisplayRange * Settings.DisplayRange,
			Settings.MaxVisibleBars);
		_renderBuffer.Clear();
		foreach (CreatureHealthBarState state in visible)
		{
			float distance = Mathf.Sqrt(Mathf.Max(0f, state.DistanceSqr));
			Vector3 anchor = CreatureBoundsResolver.Resolve(state.Creature as Component);
			if (hasScopeProjection && TryMapScopeAnchor(scopeProjectionCamera, scopeScreenRect, anchor, isSingleRenderScope, out Vector2 scopeScreenPosition))
			{
				_renderBuffer.Add(new CreatureHealthBarRenderData(state.InstanceId, anchor, CreatureHealthBarState.FormatHealth(state.LastKnownHealth, state.MaxHealth), state.LastKnownHealth / state.MaxHealth, Settings.GetBarScale(distance), scopeScreenPosition));
			}
			else
			{
				_renderBuffer.Add(new CreatureHealthBarRenderData(state.InstanceId, anchor, CreatureHealthBarState.FormatHealth(state.LastKnownHealth, state.MaxHealth), state.LastKnownHealth / state.MaxHealth, Settings.GetBarScale(distance)));
			}
		}
		_hud.Render(_renderBuffer, camera);
	}

	private bool TryGetScopeProjection(out Camera projectionCamera, out Rect scopeScreenRect, out bool isSingleRender)
	{
		projectionCamera = null;
		scopeScreenRect = default;
		isSingleRender = false;
		Player player = Player.LocalPlayer;
		UseableGun gun = player?.equipment?.useable as UseableGun;
		Camera main = MainCamera.instance ?? Camera.main;
		if (player == null || gun == null || !gun.isAiming || player.look == null || main == null)
		{
			return false;
		}
		Camera scopeCamera = player.look.scopeCamera;
		if (scopeCamera == null || !player.look.isScopeActive || IsUsing2DScope(player.look))
		{
			return false;
		}
		Transform scopeHook = GetScopeHook(gun);
		Camera viewmodelCamera = GetViewmodelCamera(player);
		Renderer scopeRenderer = scopeHook?.GetComponent<Renderer>();
		Rect projectedScopeRect = default;
		bool hasScopeRect = scopeRenderer != null && scopeRenderer.enabled && viewmodelCamera != null && TryProjectRendererRect(scopeRenderer, viewmodelCamera, out projectedScopeRect);
		if (Settings.Diagnostics && Time.unscaledTime >= _nextScopeProjectionDiagnostic)
		{
			_nextScopeProjectionDiagnostic = Time.unscaledTime + 2f;
			string texture = scopeCamera?.targetTexture == null ? "None" : $"{scopeCamera.targetTexture.width}x{scopeCamera.targetTexture.height}";
			_log.LogInfo($"[CreatureHealthBar] scope state: aiming={gun.isAiming}, scopeCamera={(scopeCamera == null ? "None" : scopeCamera.name)}, enabled={scopeCamera?.enabled ?? false}, targetTexture={texture}, scopePixelRect={(scopeCamera == null ? default : scopeCamera.pixelRect)}, viewmodelCamera={(viewmodelCamera == null ? "None" : viewmodelCamera.name)}, viewmodelPixelRect={(viewmodelCamera == null ? default : viewmodelCamera.pixelRect)}, hook={(scopeHook == null ? "None" : scopeHook.name)}, scopeRect={(hasScopeRect ? projectedScopeRect.ToString() : "Unavailable")}.");
		}
		if (!hasScopeRect)
		{
			return false;
		}
		// Both single-render and dual-render 3D scopes are visible through the
		// physical sight lens. Single-render scopes first crop the main view to
		// its centered square, while dual-render scopes use the scope camera's
		// square RenderTexture directly.
		isSingleRender = scopeCamera.targetTexture == null;
		projectionCamera = isSingleRender ? main : scopeCamera;
		scopeScreenRect = projectedScopeRect;
		return true;
	}

	private static bool TryMapScopeAnchor(Camera projectionCamera, Rect scopeScreenRect, Vector3 anchor, bool isSingleRender, out Vector2 screenPosition)
	{
		screenPosition = default;
		Vector3 viewport = projectionCamera.WorldToViewportPoint(anchor);
		if (viewport.z <= 0f || !ScopeTextureViewportMapper.TryMap(
			viewport.x,
			viewport.y,
			Screen.width,
			Screen.height,
			isSingleRender,
			out float textureX,
			out float textureY))
		{
			return false;
		}
		screenPosition = new Vector2(
			scopeScreenRect.xMin + textureX * scopeScreenRect.width,
			scopeScreenRect.yMin + textureY * scopeScreenRect.height);
		return true;
	}

	private static bool TryProjectRendererRect(Renderer renderer, Camera camera, out Rect screenRect)
	{
		screenRect = default;
		MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
		bool useLocalMeshBounds = meshFilter != null && meshFilter.sharedMesh != null;
		Bounds bounds = useLocalMeshBounds ? meshFilter.sharedMesh.bounds : renderer.bounds;
		Vector3 center = bounds.center;
		Vector3 extents = bounds.extents;
		float minX = float.PositiveInfinity;
		float minY = float.PositiveInfinity;
		float maxX = float.NegativeInfinity;
		float maxY = float.NegativeInfinity;
		for (int x = -1; x <= 1; x += 2)
		{
			for (int y = -1; y <= 1; y += 2)
			{
				for (int z = -1; z <= 1; z += 2)
				{
					Vector3 localPoint = center + Vector3.Scale(extents, new Vector3(x, y, z));
					Vector3 worldPoint = useLocalMeshBounds ? renderer.transform.TransformPoint(localPoint) : localPoint;
					Vector3 point = camera.WorldToScreenPoint(worldPoint);
					if (point.z <= 0f)
					{
						continue;
					}
					minX = Mathf.Min(minX, point.x);
					minY = Mathf.Min(minY, point.y);
					maxX = Mathf.Max(maxX, point.x);
					maxY = Mathf.Max(maxY, point.y);
				}
			}
		}
		if (float.IsInfinity(minX) || maxX - minX < 1f || maxY - minY < 1f)
		{
			return false;
		}
		screenRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
		return true;
	}

	private static Transform GetScopeHook(UseableGun gun)
	{
		object attachments = GetMemberValue(gun, "firstAttachments");
		return GetMemberValue(attachments, "scopeHook") as Transform;
	}

	private static Camera GetViewmodelCamera(Player player)
	{
		object animator = GetMemberValue(player, "animator");
		return GetMemberValue(animator, "viewmodelCamera") as Camera;
	}

	private static bool IsUsing2DScope(PlayerLook look)
	{
		object value = GetMemberValue(look, "IsUsing2DScope");
		return value is bool isUsing2DScope && isUsing2DScope;
	}

	private static object GetMemberValue(object instance, string name)
	{
		if (instance == null)
		{
			return null;
		}
		Type type = instance.GetType();
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		FieldInfo field = type.GetField(name, flags);
		if (field != null)
		{
			return field.GetValue(instance);
		}
		PropertyInfo property = type.GetProperty(name, flags);
		return property?.GetValue(instance, null);
	}

	private void PruneStates(float now)
	{
		foreach (CreatureHealthBarState state in _states.Values.ToArray())
		{
			Component creature = state.Creature as Component;
			if (!IsValid(creature, state.Kind) || (!state.IsCrosshairTarget && !state.HasDamageCandidate && state.ShowUntil <= now))
			{
				RemoveState(state);
			}
		}
	}

	private void ClearWorldState()
	{
		_states.Clear();
		_renderBuffer.Clear();
		ClearCrosshairTarget();
		_invalidMaxLogged.Clear();
		_boundsLogged.Clear();
		CreatureBoundsResolver.Clear();
	}

	private void RemoveState(CreatureHealthBarState state)
	{
		_states.Remove(state.InstanceId);
		CreatureBoundsResolver.Remove(state.InstanceId);
		_invalidMaxLogged.Remove(state.InstanceId);
		_boundsLogged.Remove(state.InstanceId);
		if (_crosshairTarget == state)
		{
			ClearCrosshairTarget();
		}
	}

	private CreatureHealthBarState GetOrCreateState(Component creature, CreatureHealthBarKind kind)
	{
		int id = creature.GetInstanceID();
		if (!_states.TryGetValue(id, out CreatureHealthBarState state))
		{
			state = new CreatureHealthBarState { Kind = kind, InstanceId = id, Creature = creature };
			state.SetHealth(GetCurrentHealth(creature, kind), GetMaxHealth(creature, kind));
			Camera camera = GetProjectionCamera();
			if (camera != null)
			{
				state.DistanceSqr = (creature.transform.position - camera.transform.position).sqrMagnitude;
			}
			_states.Add(id, state);
		}
		return state;
	}

	private static Camera GetTargetingCamera()
	{
		Camera main = MainCamera.instance ?? Camera.main;
		Player player = Player.LocalPlayer;
		Camera scope = player?.look?.scopeCamera;
		UseableGun gun = player?.equipment?.useable as UseableGun;
		if (scope != null && scope.enabled && scope.targetTexture != null && gun != null && gun.isAiming)
		{
			return scope;
		}
		return main;
	}

	private static Camera GetProjectionCamera()
	{
		// The health-bar canvas is screen-space overlay. During a 3D scope,
		// Unturned composites scopeCamera's RenderTexture into the MainCamera
		// frame, so scopeCamera viewport coordinates are not full-screen pixels.
		return MainCamera.instance ?? Camera.main;
	}

	private void ClearCrosshairTarget()
	{
		if (_crosshairTarget != null)
		{
			_crosshairTarget.IsCrosshairTarget = false;
		}
		_crosshairTarget = null;
	}

	private bool IsConfiguredKind(CreatureHealthBarKind kind)
	{
		return kind == CreatureHealthBarKind.Zombie ? Settings.ShowZombies : Settings.ShowAnimals;
	}

	private static bool TryResolveCreature(Collider collider, out Component creature, out CreatureHealthBarKind kind)
	{
		Zombie zombie = collider?.GetComponentInParent<Zombie>();
		if (zombie == null && collider != null)
		{
			zombie = collider.transform.root.GetComponentInChildren<Zombie>();
		}
		if (zombie != null)
		{
			creature = zombie;
			kind = CreatureHealthBarKind.Zombie;
			return true;
		}
		Animal animal = collider?.GetComponentInParent<Animal>();
		if (animal == null && collider != null)
		{
			animal = collider.transform.root.GetComponentInChildren<Animal>();
		}
		if (animal != null)
		{
			creature = animal;
			kind = CreatureHealthBarKind.Animal;
			return true;
		}
		creature = null;
		kind = default;
		return false;
	}

	private bool IsOccluded(Vector3 origin, Vector3 direction, float distance, Component creature)
	{
		int hitCount = Physics.RaycastNonAlloc(origin, direction, _occlusionHits, distance, RayMasks.BLOCK_STANCE, QueryTriggerInteraction.Ignore);
		float nearestDistance = float.PositiveInfinity;
		Collider nearestCollider = null;
		for (int i = 0; i < hitCount; i++)
		{
			Collider collider = _occlusionHits[i].collider;
			if (collider == null || BelongsTo(collider, Player.LocalPlayer))
			{
				continue;
			}
			if (_occlusionHits[i].distance < nearestDistance)
			{
				nearestDistance = _occlusionHits[i].distance;
				nearestCollider = collider;
			}
		}
		return nearestCollider != null && !BelongsTo(nearestCollider, creature);
	}

	private static bool BelongsTo(Collider collider, Component component)
	{
		if (collider == null || component == null)
		{
			return false;
		}
		Transform colliderTransform = collider.transform;
		Transform componentTransform = component.transform;
		return colliderTransform == componentTransform
			|| colliderTransform.IsChildOf(componentTransform)
			|| componentTransform.IsChildOf(colliderTransform)
			|| colliderTransform.root == componentTransform.root;
	}

	private static bool IsValid(Component creature, CreatureHealthBarKind kind)
	{
		if (creature == null || !creature.gameObject || !creature.gameObject.activeInHierarchy)
		{
			return false;
		}
		return kind == CreatureHealthBarKind.Zombie ? !((Zombie)creature).isDead : !((Animal)creature).isDead;
	}

	private static float GetCurrentHealth(Component creature, CreatureHealthBarKind kind)
	{
		return kind == CreatureHealthBarKind.Zombie ? ((Zombie)creature).GetHealth() : ((Animal)creature).GetHealth();
	}

	private static float GetMaxHealth(Component creature, CreatureHealthBarKind kind)
	{
		return kind == CreatureHealthBarKind.Zombie ? ((Zombie)creature).GetMaxHealth() : ((Animal)creature).asset?.health ?? 0f;
	}

	private sealed class CreatureBoundsResolver
	{
		private sealed class CacheEntry
		{
			public Renderer[] Renderers;
			public Collider[] SkullColliders;
			public Collider[] EntityColliders;
			public float Height;
			public float NextBoundsRefresh;
			public float NextRendererRescan;
			public int DirectChildCount;
			public int HierarchyCount;
			public bool UsedColliderFallback;
			public bool UsedRootFallback;
		}

		private static readonly Dictionary<int, CacheEntry> HeightCache = new Dictionary<int, CacheEntry>();

		internal static Vector3 Resolve(Component creature)
		{
			if (creature == null)
			{
				return Vector3.zero;
			}
			CacheEntry entry = GetEntry(creature);
			float padding = Mathf.Clamp(entry.Height * 0.06f, 0.1f, 0.35f);
			if (TryGetHighestColliderPoint(entry.SkullColliders, padding, out Vector3 skullTop))
			{
				return skullTop;
			}
			if (TryGetHighestColliderPoint(entry.EntityColliders, padding, out Vector3 entityTop))
			{
				return entityTop;
			}
			float top = creature.transform.position.y + entry.Height;
			return new Vector3(creature.transform.position.x, top + padding, creature.transform.position.z);
		}

		internal static string GetDiagnostics(Component creature)
		{
			CacheEntry entry = GetEntry(creature);
			string fallback = entry.UsedRootFallback ? "root" : (entry.UsedColliderFallback ? "collider" : "renderer");
			string anchor = entry.SkullColliders.Length > 0 ? "skull-hitbox" : (entry.EntityColliders.Length > 0 ? "entity-top" : "model-top");
			return $"height={entry.Height:0.###}, anchor={anchor}, skullColliderCount={entry.SkullColliders.Length}, entityColliderCount={entry.EntityColliders.Length}, rendererCount={entry.Renderers.Length}, hierarchyCount={entry.HierarchyCount}, fallback={fallback}";
		}

		internal static void Clear()
		{
			HeightCache.Clear();
		}

		internal static void Remove(int id)
		{
			HeightCache.Remove(id);
		}

		private static CacheEntry GetEntry(Component creature)
		{
			int id = creature.GetInstanceID();
			float now = Time.unscaledTime;
			if (!HeightCache.TryGetValue(id, out CacheEntry entry)
				|| entry.Renderers == null
				|| entry.SkullColliders == null
				|| entry.EntityColliders == null
				|| entry.DirectChildCount != creature.transform.childCount
				|| now >= entry.NextRendererRescan
				|| entry.Renderers.Any(renderer => renderer == null)
				|| entry.EntityColliders.Any(collider => collider == null))
			{
				entry = BuildEntry(creature, now);
				HeightCache[id] = entry;
			}
			else if (now >= entry.NextBoundsRefresh)
			{
				RefreshHeight(entry, creature, now);
			}
			return entry;
		}

		private static CacheEntry BuildEntry(Component creature, float now)
		{
			Renderer[] renderers = creature.GetComponentsInChildren<Renderer>(true)
				.Where(renderer => IsAcceptedRenderer(renderer, creature.transform))
				.ToArray();
			Collider[] entityColliders = FindEntityColliders(creature);
			CacheEntry entry = new CacheEntry
			{
				Renderers = renderers,
				SkullColliders = FindSkullColliders(entityColliders),
				EntityColliders = entityColliders,
				DirectChildCount = creature.transform.childCount,
				HierarchyCount = creature.GetComponentsInChildren<Transform>(true).Length,
				NextRendererRescan = now + 2f
			};
			RefreshHeight(entry, creature, now);
			return entry;
		}

		private static void RefreshHeight(CacheEntry entry, Component creature, float now)
		{
			float top = float.MinValue;
			foreach (Renderer renderer in entry.Renderers)
			{
				if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
				{
					top = Mathf.Max(top, renderer.bounds.max.y);
				}
			}
			entry.UsedColliderFallback = top == float.MinValue;
			if (entry.UsedColliderFallback)
			{
				Collider[] colliders = creature.GetComponentsInChildren<Collider>(true);
				foreach (Collider collider in colliders)
				{
					if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
					{
						top = Mathf.Max(top, collider.bounds.max.y);
					}
				}
			}
			entry.UsedRootFallback = top == float.MinValue;
			entry.Height = entry.UsedRootFallback ? 1.5f : Mathf.Max(0.1f, top - creature.transform.position.y);
			entry.NextBoundsRefresh = now + 0.2f;
		}

		private static Collider[] FindSkullColliders(IEnumerable<Collider> entityColliders)
		{
			return entityColliders
				.Where(collider => DamageTool.getLimb(collider.transform) == ELimb.SKULL)
				.ToArray();
		}

		private static Collider[] FindEntityColliders(Component creature)
		{
			return creature.GetComponentsInChildren<Collider>(true)
				.Where(IsEntityCollider)
				.ToArray();
		}

		private static bool IsEntityCollider(Collider collider)
		{
			return collider != null && collider.gameObject.layer == 23;
		}

		private static bool TryGetHighestColliderPoint(IEnumerable<Collider> colliders, float padding, out Vector3 point)
		{
			point = Vector3.zero;
			float top = float.MinValue;
			if (colliders == null)
			{
				return false;
			}
			foreach (Collider collider in colliders)
			{
				if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
				{
					continue;
				}
				Bounds bounds = collider.bounds;
				if (bounds.max.y > top)
				{
					top = bounds.max.y;
					point = new Vector3(bounds.center.x, top + padding, bounds.center.z);
				}
			}
			return top > float.MinValue;
		}

		private static bool IsAcceptedRenderer(Renderer renderer, Transform root)
		{
			if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer))
			{
				return false;
			}
			Transform current = renderer.transform;
			while (current != null && current != root)
			{
				string name = current.name ?? string.Empty;
				if (name.IndexOf("weapon", StringComparison.OrdinalIgnoreCase) >= 0
					|| name.IndexOf("effect", StringComparison.OrdinalIgnoreCase) >= 0
					|| name.IndexOf("drop", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return false;
				}
				current = current.parent;
			}
			return current == root;
		}
	}
}
#pragma warning restore CS0618
