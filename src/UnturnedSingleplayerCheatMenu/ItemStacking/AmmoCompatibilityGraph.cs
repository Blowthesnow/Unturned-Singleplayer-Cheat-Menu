using System;
using System.Collections.Generic;
using System.Linq;

namespace UnturnedSingleplayerCheatMenu.ItemStacking;

internal sealed class AmmoCompatibilityGraph
{
	private readonly HashSet<string> _containerAssetGuids = new HashSet<string>(StringComparer.Ordinal);
	private readonly HashSet<string> _directSourceAssetGuids = new HashSet<string>(StringComparer.Ordinal);
	private readonly Dictionary<string, HashSet<string>> _sourcesByTarget =
		new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

	internal void AddContainer(string assetGuid)
	{
		string normalized = AmmoRefillIdentity.NormalizeAssetGuid(assetGuid);
		if (!string.IsNullOrEmpty(normalized)) _containerAssetGuids.Add(normalized);
	}

	internal void AddDirectTransfer(string sourceAssetGuid, string targetAssetGuid)
	{
		string source = AmmoRefillIdentity.NormalizeAssetGuid(sourceAssetGuid);
		string target = AmmoRefillIdentity.NormalizeAssetGuid(targetAssetGuid);
		if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return;
		_directSourceAssetGuids.Add(source);
		_containerAssetGuids.Add(target);
		if (!_sourcesByTarget.TryGetValue(target, out HashSet<string> sources))
		{
			sources = new HashSet<string>(StringComparer.Ordinal);
			_sourcesByTarget.Add(target, sources);
		}
		sources.Add(source);
	}

	internal AmmoCompatibilityCatalog Build()
	{
		return new AmmoCompatibilityCatalog(_containerAssetGuids, _directSourceAssetGuids, _sourcesByTarget);
	}
}

internal sealed class AmmoCompatibilityCatalog
{
	internal static readonly AmmoCompatibilityCatalog Empty = new AmmoCompatibilityGraph().Build();

	private readonly HashSet<string> _containerAssetGuids;
	private readonly HashSet<string> _directSourceAssetGuids;
	private readonly Dictionary<string, IReadOnlyList<string>> _sourcesByTarget;
	private readonly Dictionary<string, IReadOnlyList<string>> _targetsBySource;

	internal AmmoCompatibilityCatalog(IEnumerable<string> containerAssetGuids, IEnumerable<string> directSourceAssetGuids,
		IReadOnlyDictionary<string, HashSet<string>> sourcesByTarget)
	{
		_containerAssetGuids = new HashSet<string>((containerAssetGuids ?? Enumerable.Empty<string>())
			.Select(AmmoRefillIdentity.NormalizeAssetGuid).Where(value => !string.IsNullOrEmpty(value)), StringComparer.Ordinal);
		_directSourceAssetGuids = new HashSet<string>((directSourceAssetGuids ?? Enumerable.Empty<string>())
			.Select(AmmoRefillIdentity.NormalizeAssetGuid).Where(value => !string.IsNullOrEmpty(value)), StringComparer.Ordinal);
		_sourcesByTarget = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
		Dictionary<string, HashSet<string>> targetsBySource = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
		foreach (KeyValuePair<string, HashSet<string>> pair in sourcesByTarget ??
			new Dictionary<string, HashSet<string>>(StringComparer.Ordinal))
		{
			string target = AmmoRefillIdentity.NormalizeAssetGuid(pair.Key);
			string[] sources = (pair.Value ?? new HashSet<string>(StringComparer.Ordinal))
				.Select(AmmoRefillIdentity.NormalizeAssetGuid)
				.Where(value => !string.IsNullOrEmpty(value))
				.Distinct(StringComparer.Ordinal)
				.OrderBy(value => value, StringComparer.Ordinal)
				.ToArray();
			_sourcesByTarget[target] = sources;
			foreach (string source in sources)
			{
				if (!targetsBySource.TryGetValue(source, out HashSet<string> targets))
				{
					targets = new HashSet<string>(StringComparer.Ordinal);
					targetsBySource.Add(source, targets);
				}
				targets.Add(target);
			}
		}
		_targetsBySource = targetsBySource.ToDictionary(pair => pair.Key,
			pair => (IReadOnlyList<string>)pair.Value.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
			StringComparer.Ordinal);
	}

	internal IReadOnlyCollection<string> ContainerAssetGuids => _containerAssetGuids;
	internal IReadOnlyCollection<string> DirectSourceAssetGuids => _directSourceAssetGuids;
	internal IReadOnlyCollection<string> AmmoRelatedAssetGuids => _containerAssetGuids
		.Concat(_directSourceAssetGuids).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();

	internal bool IsContainerTarget(string assetGuid) =>
		_containerAssetGuids.Contains(AmmoRefillIdentity.NormalizeAssetGuid(assetGuid));

	internal bool IsDirectSource(string assetGuid) =>
		_directSourceAssetGuids.Contains(AmmoRefillIdentity.NormalizeAssetGuid(assetGuid));

	internal bool IsAmmoRelated(string assetGuid) => IsContainerTarget(assetGuid) || IsDirectSource(assetGuid);

	internal IReadOnlyList<string> GetDirectSources(string targetAssetGuid)
	{
		return _sourcesByTarget.TryGetValue(AmmoRefillIdentity.NormalizeAssetGuid(targetAssetGuid), out IReadOnlyList<string> sources)
			? sources
			: Array.Empty<string>();
	}

	internal IReadOnlyList<string> GetDirectTargets(string sourceAssetGuid)
	{
		return _targetsBySource.TryGetValue(AmmoRefillIdentity.NormalizeAssetGuid(sourceAssetGuid), out IReadOnlyList<string> targets)
			? targets
			: Array.Empty<string>();
	}

	internal bool CanTransfer(string sourceAssetGuid, string targetAssetGuid)
	{
		string source = AmmoRefillIdentity.NormalizeAssetGuid(sourceAssetGuid);
		return GetDirectSources(targetAssetGuid).Contains(source, StringComparer.Ordinal);
	}

	internal IReadOnlyList<string> ProjectDirectSources(IEnumerable<string> targetAssetGuids)
	{
		return (targetAssetGuids ?? Enumerable.Empty<string>())
			.SelectMany(GetDirectSources)
			.Distinct(StringComparer.Ordinal)
			.OrderBy(value => value, StringComparer.Ordinal)
			.ToArray();
	}

	internal AmmoRefillAssetRules GetRules(string assetGuid, bool isMagazine)
	{
		string normalized = AmmoRefillIdentity.NormalizeAssetGuid(assetGuid);
		return new AmmoRefillAssetRules(normalized, isMagazine, GetDirectTargets(normalized).Count > 0,
			GetDirectSources(normalized));
	}
}
