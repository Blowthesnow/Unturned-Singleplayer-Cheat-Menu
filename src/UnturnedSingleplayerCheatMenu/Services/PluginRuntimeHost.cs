using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

public sealed class PluginRuntimeHost : MonoBehaviour
{
	private CheatMenuPlugin _plugin;

	internal static PluginRuntimeHost Create(CheatMenuPlugin plugin)
	{
		GameObject obj = new GameObject("UnturnedSingleplayerCheatMenu.RuntimeHost");
		Object.DontDestroyOnLoad(obj);
		PluginRuntimeHost pluginRuntimeHost = obj.AddComponent<PluginRuntimeHost>();
		pluginRuntimeHost._plugin = plugin;
		pluginRuntimeHost.enabled = true;
		obj.SetActive(value: true);
		plugin.LogRuntimeHostCreated(pluginRuntimeHost);
		return pluginRuntimeHost;
	}

	public void Start()
	{
		_plugin?.LogRuntimeHostStarted();
	}

	public void Update()
	{
		_plugin?.RunUpdateCallback("独立 RuntimeHost.Update");
	}

	public void LateUpdate()
	{
		_plugin?.RunLateUpdateCallback();
	}

	private void OnDestroy()
	{
		_plugin = null;
	}
}
