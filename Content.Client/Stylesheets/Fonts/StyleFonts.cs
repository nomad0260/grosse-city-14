using Content.Client.Resources;
using Content.Client.UserInterface;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.IoC;

namespace Content.Client.Stylesheets.Fonts;

/// <summary>
/// Font helpers for stylesheet code. <see cref="NotoFontFamilyStack"/> already routes the
/// regular/bold/italic fonts through the selectable UI font style; these cover the cases
/// that used to be hardcoded to a specific file.
/// </summary>
public static class StyleFonts
{
    private const string FallbackMono = "/EngineFonts/NotoSans/NotoSansMono-Regular.ttf";

    /// <summary>
    /// Monospace font honouring the selected UI font style.
    /// </summary>
    public static Font Mono(IResourceCache cache, int size)
    {
        if (IoCManager.Instance is { } ioc
            && ioc.TryResolveType<IUiFontStackManager>(out var fonts))
        {
            return fonts.GetStack(cache, "Mono-Regular", size);
        }

        return cache.GetFont(FallbackMono, size);
    }
}
