using BepInEx.Configuration;
using UnturnedSingleplayerCheatMenu.Services;

string root = Path.Combine(Path.GetTempPath(), "UnturnedSingleplayerCheatMenu.ConfigInitializationSmoke", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    CurrentBindingsAreSaved();
    UnboundPluginEntriesAreRemoved();
    ExistingFileIsRestoredAfterBindingFailure();
    MissingFileIsRestoredAfterBindingFailure();
    EmptyFileIsRestoredAfterSaveFailure();
    Console.WriteLine("Configuration initialization smoke checks passed.");
}
finally
{
    Directory.Delete(root, recursive: true);
}

void CurrentBindingsAreSaved()
{
    string path = Path.Combine(root, "current.cfg");
    ConfigFile config = NewConfig(path);
    bool saveOnConfigSet = config.SaveOnConfigSet;

    PluginConfigInitializationTransaction.Run(config, null, () =>
    {
        config.Bind("AutoPickup", "Range", 40f).Value = 72f;
        config.Bind("InventoryArrange", "FixHotkeyItems", true).Value = false;
    });

    string saved = File.ReadAllText(path);
    Check(saved.Contains("Range = 72", StringComparison.Ordinal), "Current range binding was not saved.");
    Check(saved.Contains("FixHotkeyItems = false", StringComparison.Ordinal), "Current inventory binding was not saved.");
    Check(config.SaveOnConfigSet == saveOnConfigSet, "SaveOnConfigSet was not restored after success.");
}

void UnboundPluginEntriesAreRemoved()
{
    string path = Path.Combine(root, "canonical.cfg");
    File.WriteAllText(path, "[Interface]\nScale = 1.25\n\n[Retired]\nUnusedSetting = obsolete\n");
    ConfigFile config = NewConfig(path);

    PluginConfigInitializationTransaction.Run(config, null, () =>
    {
        config.Bind("Interface", "Scale", 1f);
        config.Bind("AutoPickup", "Range", 40f);
    });

    string saved = File.ReadAllText(path);
    Check(saved.Contains("Scale = 1.25", StringComparison.Ordinal), "A bound current value was not preserved during canonicalization.");
    Check(saved.Contains("Range = 40", StringComparison.Ordinal), "A newly bound current value was not saved during canonicalization.");
    Check(!saved.Contains("UnusedSetting", StringComparison.Ordinal), "An unbound plugin-owned entry survived canonical configuration writeback.");
}

void ExistingFileIsRestoredAfterBindingFailure()
{
    string path = Path.Combine(root, "existing.cfg");
    string original = "[AutoPickup]\nRange = 19\n";
    File.WriteAllText(path, original);
    ConfigFile config = NewConfig(path);
    bool threw = false;
    try
    {
        PluginConfigInitializationTransaction.Run(config, null, () =>
        {
            config.Bind("AutoPickup", "Range", 40f).Value = 88f;
            throw new InvalidOperationException("expected binding failure");
        });
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }

    Check(threw, "Binding failure did not escape the transaction.");
    Check(File.ReadAllText(path) == original, "Existing configuration bytes were not restored.");
    Check(Math.Abs(config.Bind("AutoPickup", "Range", 40f).Value - 19f) < 0.001f,
        "The same ConfigFile instance retained failed-session entries.");
}

void MissingFileIsRestoredAfterBindingFailure()
{
    string path = Path.Combine(root, "missing.cfg");
    ConfigFile config = NewConfig(path);
    try
    {
        PluginConfigInitializationTransaction.Run(config, null, () =>
        {
            config.Bind("Interface", "Scale", 1f).Value = 1.25f;
            throw new InvalidOperationException("expected binding failure");
        });
    }
    catch (InvalidOperationException)
    {
    }

    Check(!File.Exists(path), "A failed first-run binding created a configuration file.");
    Check(Math.Abs(config.Bind("Interface", "Scale", 1f).Value - 1f) < 0.001f,
        "A failed first-run binding leaked an in-memory entry.");
}

void EmptyFileIsRestoredAfterSaveFailure()
{
    string path = Path.Combine(root, "empty.cfg");
    File.WriteAllBytes(path, Array.Empty<byte>());
    ConfigFile config = NewConfig(path);
    bool threw = false;
    try
    {
        PluginConfigInitializationTransaction.Run(
            config,
            null,
            () => config.Bind("Interface", "Language", "Auto").Value = "Chinese",
            _ => throw new IOException("expected save failure"));
    }
    catch (IOException)
    {
        threw = true;
    }

    Check(threw, "Save failure did not escape the transaction.");
    Check(File.Exists(path) && new FileInfo(path).Length == 0, "An empty original configuration was not restored exactly.");
}

static ConfigFile NewConfig(string path) => new(path, saveOnInit: false);

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
