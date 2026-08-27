namespace UnturnedSingleplayerCheatMenu.Services;

internal static class PageNavigation
{
    public static bool TryMove(int page, int pageCount, float scrollY, bool pointerOverInput, out int nextPage)
    {
        nextPage = page;
        if (pointerOverInput || pageCount <= 0 || scrollY == 0f) return false;
        if (scrollY > 0f && page > 0) nextPage = page - 1;
        else if (scrollY < 0f && page < pageCount - 1) nextPage = page + 1;
        return nextPage != page;
    }
}