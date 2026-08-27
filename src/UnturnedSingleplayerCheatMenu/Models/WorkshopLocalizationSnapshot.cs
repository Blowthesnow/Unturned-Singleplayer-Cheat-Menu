using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UnturnedSingleplayerCheatMenu.Models;

internal sealed class WorkshopLocalizationSnapshot
{
	private sealed class ReferenceComparer : IEqualityComparer<object>
	{
		internal static readonly ReferenceComparer Instance = new ReferenceComparer();

		public new bool Equals(object left, object right)
		{
			return left == right;
		}

		public int GetHashCode(object value)
		{
			if (value != null)
			{
				return RuntimeHelpers.GetHashCode(value);
			}
			return 0;
		}
	}

	internal static readonly WorkshopLocalizationSnapshot Empty = new WorkshopLocalizationSnapshot(CreateLookup(), CreateLookup());

	private readonly IReadOnlyDictionary<object, IReadOnlyDictionary<WorkshopTextField, string>> _translations;

	private readonly IReadOnlyDictionary<object, IReadOnlyDictionary<WorkshopTextField, string>> _originals;

	internal WorkshopLocalizationSnapshot(IReadOnlyDictionary<object, IReadOnlyDictionary<WorkshopTextField, string>> translations, IReadOnlyDictionary<object, IReadOnlyDictionary<WorkshopTextField, string>> originals)
	{
		_translations = translations;
		_originals = originals;
	}

	internal bool TryGet(object instance, WorkshopTextField field, out string translation)
	{
		translation = null;
		if (instance != null && _translations.TryGetValue(instance, out var values))
		{
			return values.TryGetValue(field, out translation);
		}
		return false;
	}

	internal bool TryGetOriginal(object instance, WorkshopTextField field, out string original)
	{
		original = null;
		if (instance != null && _originals.TryGetValue(instance, out var values))
		{
			return values.TryGetValue(field, out original);
		}
		return false;
	}

	private static IReadOnlyDictionary<object, IReadOnlyDictionary<WorkshopTextField, string>> CreateLookup()
	{
		return new Dictionary<object, IReadOnlyDictionary<WorkshopTextField, string>>(ReferenceComparer.Instance);
	}
}
