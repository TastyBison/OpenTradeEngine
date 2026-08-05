namespace OpenTradeEngine;

public static class ShortcutInputState
{
    public static bool BypassRequested { get; internal set; }

    public static bool ShouldUse(bool shortcutEnabled) => shortcutEnabled && !BypassRequested;
}
