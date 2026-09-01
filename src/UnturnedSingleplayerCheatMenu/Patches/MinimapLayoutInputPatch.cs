using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Minimap;

namespace UnturnedSingleplayerCheatMenu.Patches;

internal static class MinimapLayoutInputPatch
{
	internal static void Install(Harmony harmony, ManualLogSource log)
	{
		if (harmony == null) throw new ArgumentNullException(nameof(harmony));
		MinimapLayoutInputGate.Reset();
		HarmonyMethod gameKeyPrefix = new HarmonyMethod(
			AccessTools.Method(typeof(MinimapLayoutInputPatch), nameof(GameKeyPrefix)))
		{
			priority = Priority.First
		};
		HarmonyMethod chatPrefix = new HarmonyMethod(
			AccessTools.Method(typeof(MinimapLayoutInputPatch), nameof(ChatOpenPrefix)))
		{
			priority = Priority.First
		};
		int installed = 0;
		installed += TryPatch(harmony, log,
			AccessTools.Method(typeof(InputEx), nameof(InputEx.GetKeyDown), new[] { typeof(KeyCode) }), gameKeyPrefix);
		installed += TryPatch(harmony, log,
			AccessTools.Method(typeof(InputEx), nameof(InputEx.GetKey), new[] { typeof(KeyCode) }), gameKeyPrefix);
		installed += TryPatch(harmony, log,
			AccessTools.Method(typeof(PlayerLifeUI), nameof(PlayerLifeUI.openChat), Type.EmptyTypes), chatPrefix);
		installed += TryPatch(harmony, log,
			AccessTools.Method(typeof(PlayerLifeUI), nameof(PlayerLifeUI.SendChatAndClose), Type.EmptyTypes), chatPrefix);
		log?.LogInfo("[Minimap] layout input interception registered on " + installed + "/4 native paths.");
	}

	private static int TryPatch(Harmony harmony, ManualLogSource log, MethodInfo target, HarmonyMethod prefix)
	{
		if (target == null)
		{
			log?.LogWarning("[Minimap] layout input interception target was not found in the current game assembly.");
			return 0;
		}
		try
		{
			harmony.Patch(target, prefix: prefix);
			return 1;
		}
		catch (Exception exception)
		{
			log?.LogWarning("[Minimap] unable to intercept layout-edit input at " +
				target.DeclaringType?.FullName + "." + target.Name + ".\n" + exception);
			return 0;
		}
	}

	private static bool GameKeyPrefix(ref bool __result)
	{
		if (!MinimapLayoutInputGate.ShouldBlockGameKeyQueries) return true;
		__result = false;
		return false;
	}

	private static bool ChatOpenPrefix()
	{
		return !MinimapLayoutInputGate.ShouldBlockChatAction();
	}

	internal static void Reset()
	{
		MinimapLayoutInputGate.Reset();
	}
}
