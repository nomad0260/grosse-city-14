using System.Collections.Generic;
using Content.Client.Stylesheets.Colorspace;
using Content.Client.Stylesheets.Palette;
using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class ButtonSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet, IButtonConfig, IIconConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        IButtonConfig buttonCfg = sheet;
        IIconConfig iconCfg = sheet;

        var crossTex = sheet.GetTextureOr(iconCfg.CrossIconPath, NanotrasenStylesheet.TextureRoot);
        var refreshTex = sheet.GetTextureOr(iconCfg.RefreshIconPath, NanotrasenStylesheet.TextureRoot);
        var helpTex = sheet.GetTextureOr(iconCfg.HelpIconPath, NanotrasenStylesheet.TextureRoot);

        var rules = new List<StyleRule>
        {
            // Every button in the game shares these flat, rounded shapes.
            CButton()
                .Box(StyleBoxHelpers.ButtonStyleBox()),
            CButton()
                .Class(StyleClass.ButtonOpenLeft)
                .Box(StyleBoxHelpers.OpenLeftStyleBox()),
            CButton()
                .Class(StyleClass.ButtonOpenRight)
                .Box(StyleBoxHelpers.OpenRightStyleBox()),
            CButton()
                .Class(StyleClass.ButtonOpenBoth)
                .Box(StyleBoxHelpers.SquareStyleBox()),
            CButton()
                .Class(StyleClass.ButtonSquare)
                .Box(StyleBoxHelpers.SquareStyleBox()),
            CButton()
                .Class(StyleClass.ButtonSmall)
                .Box(StyleBoxHelpers.SmallStyleBox()),
            CButton()
                .Class(StyleClass.ButtonSmall)
                .ParentOf(E<Label>())
                .Font(sheet.BaseFont.GetFont(8)),
            CButton().Class(StyleClass.ButtonBig).ParentOf(E<Label>()).Font(sheet.BaseFont.GetFont(16)),

            // Cross Button (Red)
            E<TextureButton>()
                .Class(StyleClass.CrossButtonRed)
                .Prop(TextureButton.StylePropertyTexture, crossTex),

            // Refresh Button
            E<TextureButton>()
                .Class(StyleClass.RefreshButton)
                .Prop(TextureButton.StylePropertyTexture, refreshTex),

            // Help button
            E<TextureButton>()
                .Class(StyleClass.HelpButton)
                .Prop(TextureButton.StylePropertyTexture, helpTex),

            // Ensure labels in buttons are aligned.
            E<Label>()
                // ReSharper disable once AccessToStaticMemberViaDerivedType
                .Class(Button.StyleClassButton)
                .AlignMode(Label.AlignMode.Center),

            // Have disabled button's text be faded
            CButton().PseudoDisabled().ParentOf(E<Label>()).FontColor(Color.FromHex("#E5E5E581")),
            CButton().PseudoDisabled().ParentOf(E()).ParentOf(E<Label>()).FontColor(Color.FromHex("#E5E5E581")),
        };
        // Texture button modulation
        MakeButtonRules<TextureButton>(rules, Palettes.AlphaModulate, null);
        MakeButtonRules<TextureButton>(rules, sheet.NegativePalette, StyleClass.CrossButtonRed);

        MakeButtonRules(rules, buttonCfg.ButtonPalette, null);
        MakeButtonRules(rules, buttonCfg.PositiveButtonPalette, StyleClass.Positive);
        MakeButtonRules(rules, buttonCfg.NegativeButtonPalette, StyleClass.Negative);

        return rules.ToArray();
    }

    public static void MakeButtonRules<TC>(
        List<StyleRule> rules,
        ColorPalette palette,
        string? styleclass)
        where TC : Control
    {
        rules.AddRange([
            E<TC>().MaybeClass(styleclass).PseudoNormal().Modulate(palette.Element),
            E<TC>().MaybeClass(styleclass).PseudoHovered().Modulate(palette.HoveredElement),
            E<TC>().MaybeClass(styleclass).PseudoPressed().Modulate(palette.PressedElement),
            E<TC>().MaybeClass(styleclass).PseudoDisabled().Modulate(palette.DisabledElement),
        ]);
    }

    public static void MakeButtonRules(
        List<StyleRule> rules,
        ColorPalette palette,
        string? styleclass)
    {
        rules.AddRange([
            CButton()
                .MaybeClass(styleclass)
                .PseudoNormal()
                .Prop(Control.StylePropertyModulateSelf, ButtonStateLift.NormalColor(palette)),
            CButton()
                .MaybeClass(styleclass)
                .PseudoHovered()
                .Prop(Control.StylePropertyModulateSelf, ButtonStateLift.HoverColor(palette)),
            CButton()
                .MaybeClass(styleclass)
                .PseudoPressed()
                .Prop(Control.StylePropertyModulateSelf, ButtonStateLift.PressedColor(palette)),
            CButton()
                .MaybeClass(styleclass)
                .PseudoDisabled()
                .Prop(Control.StylePropertyModulateSelf, ButtonStateLift.Disabled),
        ]);
    }

    private static MutableSelectorElement CButton()
    {
        return E<ContainerButton>().Class(ContainerButton.StyleClassButton);
    }
}

/// <summary>
/// Shared lightness lifts applied to the theme palettes. The raw palettes are dark enough
/// that buttons blended into panels, so every state is brightened by a fixed amount.
/// Shared by every button-like sheetlet so they all stay consistent.
/// </summary>
public static class ButtonStateLift
{
    public const float Normal = 0.14f;
    public const float Hover = 0.19f;
    public const float Pressed = 0.07f;

    /// <summary>Neutral grey: visible, but clearly not the normal button color.</summary>
    public static readonly Color Disabled = Color.FromHex("#565B66");

    public static Color NormalColor(ColorPalette palette) => palette.Element.NudgeLightness(Normal);

    public static Color HoverColor(ColorPalette palette) => palette.HoveredElement.NudgeLightness(Hover);

    public static Color PressedColor(ColorPalette palette) => palette.PressedElement.NudgeLightness(Pressed);
}

// this is currently the only other "helper" type class, if any more crop up consider making a specific directory for them
public static class StyleBoxHelpers
{
    /// <summary>
    /// The shared shape of every button and panel in the game: a flat, rounded rectangle
    /// with a one pixel outline. Both fills are near-white so the theme palette's modulate
    /// decides the final color; the brighter outline comes from the border being full white.
    /// Buttons get a much lighter fill than panels so they never blend into the background.
    /// </summary>
    private static readonly Color FlatFill = new(0.98f, 0.98f, 1.0f);
    private static readonly Color PanelFill = new(0.58f, 0.58f, 0.62f);
    private static readonly Color FlatBorder = Color.White;

    public const float ButtonRadius = 8f;
    public const float PanelRadius = 10f;

    // Tight paddings: the old Nano boxes used 14px a side, which looked bloated on flat buttons.
    private const float ButtonPadH = 10f;
    private const float ButtonPadV = 3f;
    private const float JoinedPadH = 7f;
    private const float PanelPadH = 10f;
    private const float PanelPadV = 4f;

    public static RoundedStyleBox ButtonStyleBox()
    {
        return new RoundedStyleBox
        {
            BackgroundColor = FlatFill,
            BorderColor = FlatBorder,
            BorderThickness = 1f,
            CornerRadius = ButtonRadius,
            Padding = new Thickness(ButtonPadH, ButtonPadV, ButtonPadH, ButtonPadV),
        };
    }

    public static RoundedStyleBox OpenLeftStyleBox()
    {
        var box = RoundedStyleBox.OpenLeft(FlatFill, FlatBorder, 1f, ButtonRadius);
        box.Padding = new Thickness(JoinedPadH, ButtonPadV, ButtonPadH, ButtonPadV);
        return box;
    }

    public static RoundedStyleBox OpenRightStyleBox()
    {
        var box = RoundedStyleBox.OpenRight(FlatFill, FlatBorder, 1f, ButtonRadius);
        box.Padding = new Thickness(ButtonPadH, ButtonPadV, JoinedPadH, ButtonPadV);
        return box;
    }

    public static RoundedStyleBox SquareStyleBox()
    {
        return new RoundedStyleBox
        {
            BackgroundColor = FlatFill,
            BorderColor = FlatBorder,
            BorderThickness = 1f,
            CornerRadius = 0f,
            Padding = new Thickness(JoinedPadH, ButtonPadV, JoinedPadH, ButtonPadV),
        };
    }

    public static RoundedStyleBox SmallStyleBox()
    {
        return new RoundedStyleBox
        {
            BackgroundColor = FlatFill,
            BorderColor = FlatBorder,
            BorderThickness = 1f,
            CornerRadius = 5f,
            Padding = new Thickness(4f, 1f, 4f, 1f),
        };
    }

    public static RoundedStyleBox PanelStyleBox()
    {
        return new RoundedStyleBox
        {
            BackgroundColor = PanelFill,
            BorderColor = FlatBorder,
            BorderThickness = 1f,
            CornerRadius = PanelRadius,
            Padding = new Thickness(PanelPadH, PanelPadV, PanelPadH, PanelPadV),
        };
    }
}
