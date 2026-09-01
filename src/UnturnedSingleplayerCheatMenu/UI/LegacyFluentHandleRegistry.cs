using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>Disposes subscriptions by rendered subtree without taking GameObject ownership.</summary>
internal sealed class LegacyFluentHandleRegistry : IDisposable
{
	private sealed class Scope
	{
		internal GameObject Root;
		internal readonly List<IDisposable> Handles = new List<IDisposable>();
	}

	private readonly List<Scope> _scopes = new List<Scope>();

	internal int ScopeCount => _scopes.Count;
	internal int HandleCount
	{
		get
		{
			int count = 0;
			foreach (Scope scope in _scopes)
			{
				count += scope.Handles.Count;
			}
			return count;
		}
	}

	internal void RegisterScope(GameObject root)
	{
		if (root == null)
		{
			throw new ArgumentNullException(nameof(root));
		}
		if (FindExact(root) == null)
		{
			_scopes.Add(new Scope { Root = root });
		}
	}

	internal void Track(GameObject controlRoot, IDisposable handle)
	{
		if (controlRoot == null)
		{
			throw new ArgumentNullException(nameof(controlRoot));
		}
		if (handle == null)
		{
			throw new ArgumentNullException(nameof(handle));
		}

		Scope scope = FindExact(controlRoot);
		if (scope == null)
		{
			scope = new Scope { Root = controlRoot };
			_scopes.Add(scope);
		}
		if (!scope.Handles.Contains(handle))
		{
			scope.Handles.Add(handle);
		}
	}

	internal void DisposeSubtree(GameObject root)
	{
		if (root == null)
		{
			return;
		}

		for (int index = _scopes.Count - 1; index >= 0; index--)
		{
			Scope scope = _scopes[index];
			if (scope.Root == null || IsWithin(scope.Root, root))
			{
				DisposeScope(scope);
				_scopes.RemoveAt(index);
			}
		}
	}

	internal void Clear()
	{
		for (int index = _scopes.Count - 1; index >= 0; index--)
		{
			DisposeScope(_scopes[index]);
		}
		_scopes.Clear();
	}

	public void Dispose() => Clear();

	private Scope FindExact(GameObject root)
	{
		foreach (Scope scope in _scopes)
		{
			if (ReferenceEquals(scope.Root, root))
			{
				return scope;
			}
		}
		return null;
	}

	private static bool IsWithin(GameObject candidate, GameObject root)
	{
		if (ReferenceEquals(candidate, root))
		{
			return true;
		}
		Transform candidateTransform = candidate != null ? candidate.transform : null;
		Transform rootTransform = root != null ? root.transform : null;
		return candidateTransform != null && rootTransform != null && candidateTransform.IsChildOf(rootTransform);
	}

	private static void DisposeScope(Scope scope)
	{
		for (int index = scope.Handles.Count - 1; index >= 0; index--)
		{
			scope.Handles[index]?.Dispose();
		}
		scope.Handles.Clear();
		scope.Root = null;
	}
}
