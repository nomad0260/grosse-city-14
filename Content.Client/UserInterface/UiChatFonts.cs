using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client.UserInterface;

/// <summary>
/// Font sizes for chat output panels (game chat, ahelp, etc.).
/// </summary>
public static class UiChatFonts
{
    public const int BaseSize = 12;
    public const float LineHeightScale = 1f;

    public static Font Get(IResourceCache cache)
    {
        return cache.GetChatStack("Regular", BaseSize);
    }
}
