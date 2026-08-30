using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.Patches;

/// <summary>
/// Static runtime bridge that remains callable after Unturned destroys BepInEx bootstrap
/// MonoBehaviours. PluginRuntimeContext de-duplicates the proven game loops by Time.frameCount.
/// </summary>
internal static class RuntimeUpdatePatch
{
	private static PluginRuntimeContext _context;

	internal static void Install(Harmony harmony, ManualLogSource log, PluginRuntimeContext context)
	{
		if (harmony == null)
		{
			throw new ArgumentNullException(nameof(harmony));
		}
		_context = context ?? throw new ArgumentNullException(nameof(context));
		MethodInfo postfix = AccessTools.Method(typeof(RuntimeUpdatePatch), nameof(Postfix));
		int installedTargets = 0;
		foreach (Type targetType in new[] { typeof(Provider), typeof(MenuUI), typeof(PlayerUI) })
		{
			try
			{
				MethodInfo target = AccessTools.DeclaredMethod(targetType, "Update", Type.EmptyTypes);
				if (target == null)
				{
					throw new MissingMethodException(targetType.FullName, "Update");
				}
				harmony.Patch(target, postfix: new HarmonyMethod(postfix));
				installedTargets++;
				log?.LogInfo("[Runtime] static Update bridge registered: " + targetType.FullName + ".Update.");
			}
			catch (Exception exception)
			{
				log?.LogWarning("[Runtime] unable to register Update bridge for " + targetType.FullName + "; other targets remain available.\n" + exception);
			}
		}
		if (installedTargets == 0)
		{
			_context = null;
			throw new MissingMethodException("No proven Unturned Update target could be patched.");
		}
	}

	private static void Postfix(MethodBase __originalMethod)
	{
		PluginRuntimeContext context = _context;
		context?.Update();
		context?.LateUpdate();
	}

	internal static void Reset(PluginRuntimeContext context)
	{
		if (ReferenceEquals(_context, context))
		{
			_context = null;
		}
	}
}
