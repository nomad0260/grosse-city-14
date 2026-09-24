using System;
using System.Collections.Generic;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Shared.CCVar;
using Content.Shared.UserInterface;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.UserInterface;

public interface IUiFontStackManager
{
    UiFontStylePrototype ActiveStyle { get; }

    void Initialize();

    Font GetStack(IResourceCache cache, string variation = "Regular", int size = 10, bool display = false);

    /// <summary>
    /// Chat/output font at an exact pixel size — no <see cref="UiFontStylePrototype.SizeOffset"/>.
    /// </summary>
    Font GetChatStack(IResourceCache cache, string variation = "Regular", int size = 12, bool display = false);

    bool UsesPrimaryChatFontOverride { get; }

    Font GetStackWithPrimary(IResourceCache cache, string path, int size = 10);
}

public sealed partial class UiFontStackManager : IUiFontStackManager
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IConfigurationManager _configurationManager = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private FontTagHijackHolder _fontTagHijackHolder = default!;
    [Dependency] private IStylesheetManager _stylesheetManager = default!;
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill? _sawmill;

    private ISawmill Sawmill => _sawmill ??= _logManager.GetSawmill("ui.fonts");

    private const string DefaultStyleId = "Default";
    private const float AlternateFontSizeScale = 0.85f;

    private static readonly string[] NotoRegularFallback =
    [
        "/Fonts/NotoSans/NotoSans-Regular.ttf",
        "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf",
        "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf",
    ];

    private static readonly string[] NotoBoldFallback =
    [
        "/Fonts/NotoSans/NotoSans-Bold.ttf",
        "/Fonts/NotoSans/NotoSansSymbols-Bold.ttf",
        "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf",
    ];

    private static readonly string[] NotoItalicFallback =
    [
        "/Fonts/NotoSans/NotoSans-Italic.ttf",
        "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf",
        "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf",
    ];

    private static readonly string[] NotoBoldItalicFallback =
    [
        "/Fonts/NotoSans/NotoSans-BoldItalic.ttf",
        "/Fonts/NotoSans/NotoSansSymbols-Bold.ttf",
        "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf",
    ];

    public UiFontStylePrototype ActiveStyle { get; private set; } = default!;

    private bool _initialized;
    private string _cachedStyleId = DefaultStyleId;
    private readonly Dictionary<(string Path, int Size), int> _normalizedSizeCache = new();

    public void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        _fontTagHijackHolder.Hijack = ResolveMarkupFont;
        _configurationManager.OnValueChanged(CCVars.UiFontStyle, OnStyleChanged, invokeImmediately: true);
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            Initialize();
    }

    private void OnStyleChanged(string styleId)
    {
        if (!_prototypeManager.TryIndex(styleId, out UiFontStylePrototype? style))
        {
            Sawmill.Warning($"Unknown UI font style '{styleId}', falling back to '{DefaultStyleId}'.");
            style = _prototypeManager.Index<UiFontStylePrototype>(DefaultStyleId);
        }

        var changed = _cachedStyleId != style.ID;

        ActiveStyle = style;
        if (changed)
        {
            Sawmill.Debug($"UI font style is now '{style.ID}'.");
            _cachedStyleId = style.ID;
            _normalizedSizeCache.Clear();
        }

        _fontTagHijackHolder.HijackUpdated();

        // Style rules carry concrete Font instances, so the stylesheets have to be rebuilt for
        // the new style to reach the whole UI instead of only the markup font tags.
        if (changed)
            _stylesheetManager.ReloadSheets();
    }

    public Font GetStack(IResourceCache cache, string variation = "Regular", int size = 10, bool display = false)
    {
        EnsureInitialized();
        return BuildStackInternal(cache, variation, size, display, applyUiOffset: true);
    }

    public Font GetChatStack(IResourceCache cache, string variation = "Regular", int size = 12, bool display = false)
    {
        EnsureInitialized();
        return BuildStackInternal(cache, variation, size, display, applyUiOffset: false);
    }

    public bool UsesPrimaryChatFontOverride =>
        ActiveStyle?.ID != DefaultStyleId;

    public Font GetStackWithPrimary(IResourceCache cache, string path, int size = 10)
    {
        EnsureInitialized();
        size = ApplySizeOffset(size);
        if (ActiveStyle.ID != DefaultStyleId)
            size = NormalizeToNotoPixelSize(cache, path, size);

        if (ActiveStyle.ID == DefaultStyleId && !ActiveStyle.NotoFallback)
            return cache.GetFont(NotoStackWithPrimary(path), size);

        return BuildStack(cache, path, "Regular", size);
    }

    private Font? ResolveMarkupFont(ProtoId<FontPrototype> protoId, int size)
    {
        EnsureInitialized();

        var id = protoId.Id;
        var variation = id switch
        {
            "DefaultBoldItalic" or "NotoSansDisplayBoldItalic" => "BoldItalic",
            "DefaultBold" or "NotoSansDisplayBold" => "Bold",
            "DefaultItalic" or "NotoSansDisplayItalic" => "Italic",
            "Monospace" => "Mono-Regular",
            _ => "Regular",
        };

        var display = id.Contains("Display", StringComparison.Ordinal);

        // Markup tags inherit their pixel size from the current font stack; do not apply SizeOffset again.
        return GetChatStack(_resourceCache, variation, size, display);
    }

    private Font BuildStackInternal(
        IResourceCache cache,
        string variation,
        int size,
        bool display,
        bool applyUiOffset)
    {
        if (applyUiOffset)
            size = ApplySizeOffset(size);

        var primary = ResolvePrimaryPath(variation, display);
        if (ActiveStyle.ID != DefaultStyleId)
            size = NormalizeToNotoPixelSize(cache, primary, size);

        if (ActiveStyle.ID == DefaultStyleId && !ActiveStyle.NotoFallback)
            return BuildDefaultStack(cache, primary, variation, size);

        if (!ActiveStyle.NotoFallback)
            return MaybeFilterCozetteControls(primary, cache.GetFont(primary, size));

        return BuildStack(cache, primary, variation, size);
    }

    private string ResolvePrimaryPath(string variation, bool display)
    {
        if (variation == "Mono-Regular")
            return ActiveStyle.ResolveMono();

        if (display)
        {
            return variation.StartsWith("Bold", StringComparison.Ordinal)
                ? ActiveStyle.ResolveDisplayBold()
                : ActiveStyle.ResolveDisplayRegular();
        }

        return variation switch
        {
            "Italic" => ActiveStyle.ResolveItalic(),
            "Bold" => ActiveStyle.ResolveBold(),
            "BoldItalic" => ActiveStyle.ResolveBoldItalic(),
            _ when variation.StartsWith("Bold", StringComparison.Ordinal) => ActiveStyle.ResolveBold(),
            _ => ActiveStyle.Regular,
        };
    }

    private Font BuildStack(IResourceCache cache, string primary, string variation, int size)
    {
        if (!ActiveStyle.NotoFallback)
            return MaybeFilterCozetteControls(primary, cache.GetFont(primary, size));

        var fallback = variation switch
        {
            "Italic" => NotoItalicFallback,
            "Bold" => NotoBoldFallback,
            "BoldItalic" => NotoBoldItalicFallback,
            _ when variation.StartsWith("Bold", StringComparison.Ordinal) => NotoBoldFallback,
            _ => NotoRegularFallback,
        };

        var paths = new string[fallback.Length + 1];
        paths[0] = primary;
        fallback.CopyTo(paths, 1);
        return MaybeFilterCozetteControls(primary, cache.GetFont(paths, size));
    }

    private static Font MaybeFilterCozetteControls(string primaryPath, Font font)
    {
        if (!primaryPath.Contains("Cozette", StringComparison.OrdinalIgnoreCase))
            return font;

        if (font is not StackedFont stacked || stacked.Stack.Length == 0)
            return new C0ControlFilteredFont(font);

        var filtered = new Font[stacked.Stack.Length];
        filtered[0] = new C0ControlFilteredFont(stacked.Stack[0]);
        for (var i = 1; i < stacked.Stack.Length; i++)
            filtered[i] = stacked.Stack[i];

        return new StackedFont(filtered);
    }

    private static Font BuildDefaultStack(IResourceCache cache, string primary, string variation, int size)
    {
        var sv = variation.StartsWith("Bold", StringComparison.Ordinal) ? "Bold" : "Regular";

        return cache.GetFont(
        [
            primary,
            $"/Fonts/NotoSans/NotoSansSymbols-{sv}.ttf",
            "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf",
        ], size);
    }

    private int ApplySizeOffset(int size)
    {
        var offset = ActiveStyle?.SizeOffset ?? 0;
        return offset == 0 ? size : size + offset;
    }

    /// <summary>
    /// Scales alternate fonts toward Noto Sans height, then applies <see cref="AlternateFontSizeScale"/>.
    /// </summary>
    private int NormalizeToNotoPixelSize(IResourceCache cache, string primaryPath, int size)
    {
        if (size <= 0)
            return size;

        var cacheKey = (primaryPath, size);
        if (_normalizedSizeCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var normalized = size;
        try
        {
            var notoFont = cache.GetFont(NotoRegularFallback[0], size);
            var notoHeight = notoFont.GetHeight(1f);
            if (notoHeight > 0)
            {
                var styleFont = cache.GetFont(primaryPath, size);
                var styleHeight = styleFont.GetHeight(1f);
                if (styleHeight > 0 && !MathHelper.CloseToPercent(styleHeight, notoHeight))
                {
                    normalized = Math.Max(1, (int)Math.Round(size * (notoHeight / (float)styleHeight)));
                }
            }

            normalized = Math.Max(1, (int)Math.Round(normalized * AlternateFontSizeScale));
        }
        catch (Exception)
        {
            normalized = Math.Max(1, (int)Math.Round(size * AlternateFontSizeScale));
        }

        _normalizedSizeCache[cacheKey] = normalized;
        return normalized;
    }

    private static string[] NotoStackWithPrimary(string path)
    {
        return
        [
            path,
            "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf",
            "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf",
        ];
    }
}
