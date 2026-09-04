using System;
using System.Collections.Generic;
using System.Linq;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.Composition;

internal enum SnapshotModuleKind
{
	RuntimeHook = 0,
	GlobalAction = 1,
	BucketAction = 2
}

internal sealed class SnapshotModuleRequest
{
	internal static readonly SnapshotModuleRequest Empty = new SnapshotModuleRequest();

	internal SnapshotModuleRequest(ItemGroupIdentity identity = default, ItemSnapshot snapshot = null,
		long requestedCount = 0, string option = null)
	{
		Identity = identity;
		Snapshot = snapshot?.Clone();
		RequestedCount = requestedCount;
		Option = option ?? string.Empty;
	}

	internal ItemGroupIdentity Identity { get; }
	internal ItemSnapshot Snapshot { get; }
	internal long RequestedCount { get; }
	internal string Option { get; }
}

internal sealed class SnapshotModuleExecutionResult
{
	internal bool Success { get; private set; }
	internal bool Changed { get; private set; }
	internal string Message { get; private set; } = string.Empty;
	internal string ErrorCode { get; private set; } = string.Empty;

	internal static SnapshotModuleExecutionResult Succeeded(string message = "", bool changed = false) =>
		new SnapshotModuleExecutionResult { Success = true, Changed = changed, Message = message ?? string.Empty };

	internal static SnapshotModuleExecutionResult Failed(string errorCode) =>
		new SnapshotModuleExecutionResult { ErrorCode = errorCode ?? "module-failed" };
}

internal sealed class SnapshotModuleAction
{
	internal SnapshotModuleAction(string id, string label, string tooltip, SnapshotModuleKind kind)
	{
		Id = id;
		Label = label;
		Tooltip = tooltip;
		Kind = kind;
	}

	internal string Id { get; }
	internal string Label { get; }
	internal string Tooltip { get; }
	internal SnapshotModuleKind Kind { get; }
}

internal interface ISnapshotStorageModule
{
	string Id { get; }
	string Label { get; }
	string Tooltip { get; }
	SnapshotModuleKind Kind { get; }
	bool IsEnabled { get; }
	bool CanExecute(SnapshotModuleRequest request);
	SnapshotModuleExecutionResult Execute(SnapshotModuleRequest request);
}

internal sealed class SnapshotModuleRegistry
{
	private readonly Dictionary<string, ISnapshotStorageModule> _modules =
		new Dictionary<string, ISnapshotStorageModule>(StringComparer.Ordinal);

	internal IReadOnlyList<SnapshotModuleAction> Actions => _modules.Values
		.Where(module => module.IsEnabled && module.Kind != SnapshotModuleKind.RuntimeHook)
		.OrderBy(module => module.Kind)
		.ThenBy(module => module.Id, StringComparer.Ordinal)
		.Select(module => new SnapshotModuleAction(module.Id, module.Label, module.Tooltip, module.Kind))
		.ToArray();

	internal bool Register(ISnapshotStorageModule module)
	{
		if (module == null || string.IsNullOrWhiteSpace(module.Id) || _modules.ContainsKey(module.Id)) return false;
		_modules.Add(module.Id, module);
		return true;
	}

	internal SnapshotModuleExecutionResult Execute(string id, SnapshotModuleRequest request)
	{
		if (string.IsNullOrWhiteSpace(id) || !_modules.TryGetValue(id, out ISnapshotStorageModule module) ||
			!module.IsEnabled)
			return SnapshotModuleExecutionResult.Failed("module-unavailable");
		request ??= SnapshotModuleRequest.Empty;
		try
		{
			if (!module.CanExecute(request)) return SnapshotModuleExecutionResult.Failed("module-not-applicable");
			return module.Execute(request) ?? SnapshotModuleExecutionResult.Failed("module-returned-null");
		}
		catch (Exception exception)
		{
			return SnapshotModuleExecutionResult.Failed("module-threw:" + exception.GetType().Name);
		}
	}

	internal bool CanExecute(string id, SnapshotModuleRequest request)
	{
		if (string.IsNullOrWhiteSpace(id) || !_modules.TryGetValue(id, out ISnapshotStorageModule module) ||
			!module.IsEnabled) return false;
		try { return module.CanExecute(request ?? SnapshotModuleRequest.Empty); }
		catch (Exception) { return false; }
	}

}
