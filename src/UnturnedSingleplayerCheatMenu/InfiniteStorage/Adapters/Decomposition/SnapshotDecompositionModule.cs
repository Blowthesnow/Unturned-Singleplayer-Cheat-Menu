using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Composition;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Decomposition;

internal sealed class SnapshotDecompositionModule : ISnapshotStorageModule
{
	internal const string ModuleId = "decomposition";
	private readonly ISnapshotStorageRuntimePort _storage;

	internal SnapshotDecompositionModule(ISnapshotStorageRuntimePort storage, bool enabled)
	{
		_storage = storage ?? throw new ArgumentNullException(nameof(storage));
		IsEnabled = enabled;
	}

	public string Id => ModuleId;
	public string Label => "分解";
	public string Tooltip => "独立分解模块负责解释原生分解配方；快照内核不读取蓝图。";
	public SnapshotModuleKind Kind => SnapshotModuleKind.BucketAction;
	public bool IsEnabled { get; }

	public bool CanExecute(SnapshotModuleRequest request)
	{
		return IsEnabled && _storage.IsOperational &&
			TryResolveBlueprint(request, out _, out _, out _);
	}

	public SnapshotModuleExecutionResult Execute(SnapshotModuleRequest request)
	{
		if (!TryBuildRecipe(request, out SnapshotDecompositionRecipe recipe,
			out SnapshotDecompositionScope scope, out string errorCode))
			return SnapshotModuleExecutionResult.Failed(errorCode);
		if (!SnapshotDecompositionPlanner.TryPlan(_storage.Groups, request.Identity, request.Snapshot,
			scope, recipe, out SnapshotDecompositionPlan plan, out errorCode))
			return SnapshotModuleExecutionResult.Failed(errorCode);
		foreach (SnapshotDecompositionOutput output in recipe.Outputs)
		{
			SnapshotMutationResult registered = _storage.RegisterDefaultTemplate(output.Identity,
				output.DefaultSnapshot);
			if (!registered.Success) return SnapshotModuleExecutionResult.Failed(registered.ErrorCode);
		}
		SnapshotKernelTransaction transaction = _storage.BeginTransaction();
		SnapshotMutationResult applied = plan.Apply(transaction);
		if (!applied.Success) return SnapshotModuleExecutionResult.Failed(applied.ErrorCode);
		SnapshotTransactionCommitResult committed = _storage.Commit(transaction);
		if (!committed.Success) return SnapshotModuleExecutionResult.Failed(committed.ErrorCode);
		return SnapshotModuleExecutionResult.Succeeded(
			"已分解 " + plan.InputCount + " 个快照实例，生成 " + plan.OutputCount + " 个实例。", true);
	}

	private static bool TryBuildRecipe(SnapshotModuleRequest request, out SnapshotDecompositionRecipe recipe,
		out SnapshotDecompositionScope scope, out string errorCode)
	{
		recipe = null;
		scope = ParseScope(request?.Option);
		if (!TryResolveBlueprint(request, out ItemAsset sourceAsset, out Blueprint blueprint,
			out errorCode)) return false;
		List<SnapshotDecompositionOutput> outputs = new List<SnapshotDecompositionOutput>();
		foreach (BlueprintOutput output in blueprint.outputs)
		{
			ItemAsset outputAsset = output?.FindItemAsset();
			if (outputAsset == null)
			{
				errorCode = "output-asset-unavailable";
				return false;
			}
			ItemGroupIdentity outputIdentity = new ItemGroupIdentity(outputAsset.id, outputAsset.GUID);
			Item defaultItem = new Item(outputAsset.id, output.origin);
			ItemSnapshot defaultSnapshot = Canonicalize(outputAsset, defaultItem);
			Func<ItemSnapshot, ItemSnapshot> transformer = null;
			if (blueprint.transferState)
			{
				transformer = source => CreateTransferredSnapshot(blueprint, sourceAsset, outputAsset, source);
			}
			outputs.Add(new SnapshotDecompositionOutput(outputIdentity, defaultSnapshot, output.amount,
				blueprint.transferState, transformer));
		}
		recipe = new SnapshotDecompositionRecipe(outputs);
		return true;
	}

	private static bool TryResolveBlueprint(SnapshotModuleRequest request, out ItemAsset sourceAsset,
		out Blueprint blueprint, out string errorCode)
	{
		sourceAsset = null;
		blueprint = null;
		errorCode = string.Empty;
		if (request?.Snapshot == null)
		{
			errorCode = "snapshot-missing";
			return false;
		}
		sourceAsset = Assets.find(EAssetType.ITEM, request.Identity.AssetId) as ItemAsset;
		if (sourceAsset == null || sourceAsset.GUID != request.Identity.AssetGuid)
		{
			errorCode = "source-asset-unavailable";
			return false;
		}
		if (sourceAsset is ItemSupplyAsset || sourceAsset.type == EItemType.SUPPLY)
		{
			errorCode = "material-not-decomposable";
			return false;
		}
		blueprint = FindSalvage(sourceAsset);
		if (!IsSupported(sourceAsset, blueprint))
		{
			errorCode = blueprint == null ? "salvage-blueprint-missing" : "salvage-blueprint-unsupported";
			return false;
		}
		if (blueprint.outputs.Any(output => output?.FindItemAsset() == null))
		{
			errorCode = "output-asset-unavailable";
			return false;
		}
		return true;
	}

	private static SnapshotDecompositionScope ParseScope(string value)
	{
		return string.Equals(value, "asset", StringComparison.OrdinalIgnoreCase)
			? SnapshotDecompositionScope.AssetGroup
			: string.Equals(value, "bucket", StringComparison.OrdinalIgnoreCase)
				? SnapshotDecompositionScope.Bucket
				: SnapshotDecompositionScope.Single;
	}

	private static Blueprint FindSalvage(ItemAsset asset)
	{
		Blueprint fallback = null;
		foreach (Blueprint blueprint in asset?.blueprints ?? new List<Blueprint>())
		{
			if (!IsCandidate(asset, blueprint)) continue;
			fallback ??= blueprint;
			if (blueprint.CategoryTagRef == EBlueprintTypeEx.salvageCategoryTagRef ||
				string.Equals(blueprint.Name, "Salvage", StringComparison.OrdinalIgnoreCase)) return blueprint;
		}
		return fallback;
	}

	private static bool IsSupported(ItemAsset asset, Blueprint blueprint)
	{
		if (!IsCandidate(asset, blueprint) || blueprint.outputs == null || blueprint.outputs.Length == 0) return false;
		BlueprintSupply input = blueprint.supplies[0];
		return input.ShouldConsume && input.amount == 1 &&
			input.CountingMethod == ECraftingInputCountingMethod.TotalItems &&
			blueprint.outputs.All(output => output != null && output.amount > 0);
	}

	private static bool IsCandidate(ItemAsset asset, Blueprint blueprint) =>
		asset != null && blueprint != null && blueprint.Operation == EBlueprintOperation.None &&
		blueprint.TargetItem == null && blueprint.supplies != null && blueprint.supplies.Length == 1 &&
		blueprint.supplies[0] != null && blueprint.supplies[0].IsItem(asset);

	private static ItemSnapshot CreateTransferredSnapshot(Blueprint blueprint, ItemAsset sourceAsset,
		ItemAsset outputAsset, ItemSnapshot source)
	{
		Item item = new Item(outputAsset.id, source.Amount, source.Quality, source.State);
		if (sourceAsset is ItemGunAsset && outputAsset is ItemGunAsset outputGun && item.state.Length >= 12)
		{
			if (blueprint.withoutAttachments) Array.Clear(item.state, 0, item.state.Length);
			EFiremode defaultFiremode = outputGun.hasAuto ? EFiremode.AUTO :
				outputGun.hasSemi ? EFiremode.SEMI : outputGun.hasBurst ? EFiremode.BURST : EFiremode.SAFETY;
			item.state[11] = (byte)defaultFiremode;
		}
		return Canonicalize(outputAsset, item);
	}

	private static ItemSnapshot Canonicalize(ItemAsset asset, Item item)
	{
		ItemSnapshot exact = new ItemSnapshot(item.id, item.amount, item.quality,
			item.state == null ? Array.Empty<byte>() : (byte[])item.state.Clone());
		bool isFarm = asset is ItemBarricadeAsset barricade && barricade.build == EBuild.FARM;
		return SnapshotCanonicalizer.Canonicalize(exact,
			new SnapshotCanonicalizationFacts(isFarm, asset.MaxAmountAsByte));
	}
}
