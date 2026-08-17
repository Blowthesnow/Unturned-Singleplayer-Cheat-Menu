using UnturnedSingleplayerCheatMenu.Services;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

ShortcutToggleGate gate = new();
Check(gate.TryAccept(100, 10f), "The first shortcut source should be accepted.");
Check(!gate.TryAccept(100, 10f), "A duplicate callback in the same frame should be rejected.");
Check(
    !gate.TryAccept(101, 10f + ShortcutToggleGate.DuplicateInputWindowSeconds / 2f),
    "A second input source for the same physical key press should be rejected across frames.");
Check(
    gate.TryAccept(102, 10f + ShortcutToggleGate.DuplicateInputWindowSeconds),
    "A later physical key press should be accepted at the duplicate-window boundary.");
Check(
    gate.TryAccept(1, 0.1f),
    "A runtime clock reset should not leave the shortcut permanently blocked.");

Console.WriteLine("Shortcut toggle smoke checks passed.");
