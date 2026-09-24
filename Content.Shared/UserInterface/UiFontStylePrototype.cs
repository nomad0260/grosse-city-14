using Robust.Shared.Prototypes;

namespace Content.Shared.UserInterface;

/// <summary>
/// A selectable UI font style for interface text.
/// </summary>
[Prototype]
public sealed partial class UiFontStylePrototype : IPrototype, IComparable<UiFontStylePrototype>
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField("name", required: true)]
    public string Name { get; private set; } = string.Empty;

    [DataField(required: true)]
    public string Regular { get; private set; } = string.Empty;

    [DataField]
    public string? Italic { get; private set; }

    [DataField]
    public string? Bold { get; private set; }

    [DataField]
    public string? BoldItalic { get; private set; }

    [DataField]
    public string? DisplayRegular { get; private set; }

    [DataField]
    public string? DisplayBold { get; private set; }

    [DataField]
    public string? Mono { get; private set; }

    /// <summary>
    /// When true, missing glyphs fall back to Noto Sans after the primary font.
    /// </summary>
    [DataField]
    public bool NotoFallback = true;

    [DataField]
    public int Order;

    /// <summary>
    /// Extra pixels added to requested font sizes for this style (pixel fonts often need this).
    /// </summary>
    [DataField]
    public int SizeOffset;

    /// <summary>
    /// Extra pixels added to chat font size (pixel fonts look smaller at the same point size).
    /// Does not stack with <see cref="SizeOffset"/> — chat uses this instead.
    /// </summary>
    [DataField]
    public int ChatSizeOffset;

    /// <summary>
    /// Extra multiplier applied to chat font size after Noto height normalization.
    /// </summary>
    [DataField]
    public float ChatSizeScale = 1f;

    public string ResolveItalic() => Italic ?? Regular;

    public string ResolveBold() => Bold ?? Regular;

    public string ResolveBoldItalic() => BoldItalic ?? Bold ?? Regular;

    public string ResolveDisplayRegular() => DisplayRegular ?? Regular;

    public string ResolveDisplayBold() => DisplayBold ?? Bold ?? Regular;

    public string ResolveMono() => Mono ?? Regular;

    public int CompareTo(UiFontStylePrototype? other)
    {
        return Order.CompareTo(other?.Order);
    }
}
