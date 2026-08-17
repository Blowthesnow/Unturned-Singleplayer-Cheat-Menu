namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class ShortcutToggleGate
{
    internal const float DuplicateInputWindowSeconds = 0.25f;

    private int _lastAcceptedFrame = -1;
    private float _lastAcceptedTime = float.NegativeInfinity;

    internal bool TryAccept(int frame, float realtimeSinceStartup)
    {
        if (_lastAcceptedFrame == frame)
            return false;

        float elapsed = realtimeSinceStartup - _lastAcceptedTime;
        if (elapsed >= 0f && elapsed < DuplicateInputWindowSeconds)
            return false;

        _lastAcceptedFrame = frame;
        _lastAcceptedTime = realtimeSinceStartup;
        return true;
    }
}
