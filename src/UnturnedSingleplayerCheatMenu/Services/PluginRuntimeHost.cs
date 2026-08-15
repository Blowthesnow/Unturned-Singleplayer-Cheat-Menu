using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

public sealed class PluginRuntimeHost : MonoBehaviour
{
    private CheatMenuPlugin _plugin;

    internal static PluginRuntimeHost Create(CheatMenuPlugin plugin)
    {
        GameObject hostObject = new("UnturnedSingleplayerCheatMenu.RuntimeHost");
        DontDestroyOnLoad(hostObject);

        PluginRuntimeHost host = hostObject.AddComponent<PluginRuntimeHost>();
        host._plugin = plugin;
        host.enabled = true;
        hostObject.SetActive(true);
        plugin.LogRuntimeHostCreated(host);
        return host;
    }

    public void Start()
    {
        _plugin?.LogRuntimeHostStarted();
    }

    public void Update()
    {
        _plugin?.RunUpdateCallback("独立 RuntimeHost.Update");
    }

    public void OnGUI()
    {
        _plugin?.RunGuiCallback("独立 RuntimeHost.OnGUI");
    }

    private void OnDestroy()
    {
        _plugin = null;
    }
}
