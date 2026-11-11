using Content.Client.Resources;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.Palette;
using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class WindowSheetlet<T> : Sheetlet<T>
    where T : PalettedStylesheet, IButtonConfig, IWindowConfig, IIconConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        IButtonConfig buttonCfg = sheet;
        IWindowConfig windowCfg = sheet;
        IIconConfig iconCfg = sheet;

        // Flat, rounded window chrome shared by every window in the game.
        const float windowRadius = 10f;
        var windowBorder = Color.FromHex("#414651");

        var headerStylebox = new RoundedStyleBox
        {
            BackgroundColor = Color.FromHex("#3A3F4C"),
            BorderColor = windowBorder,
            BorderThickness = 1f,
            RadiusTopLeft = windowRadius,
            RadiusTopRight = windowRadius,
            RadiusBottomLeft = 0f,
            RadiusBottomRight = 0f,
            Padding = new Thickness(6f, 4f, 6f, 4f),
        };
        // TODO: This would probably be better palette-based but we can leave it for now.
        var headerAlertStylebox = new RoundedStyleBox
        {
            BackgroundColor = Color.FromHex("#5A2523"),
            BorderColor = Color.FromHex("#8E3A36"),
            BorderThickness = 1f,
            RadiusTopLeft = windowRadius,
            RadiusTopRight = windowRadius,
            RadiusBottomLeft = 0f,
            RadiusBottomRight = 0f,
            Padding = new Thickness(6f, 4f, 6f, 4f),
        };
        var backgroundBox = new RoundedStyleBox
        {
            BackgroundColor = Color.FromHex("#2C303A").WithAlpha(0.97f),
            BorderColor = windowBorder,
            BorderThickness = 1f,
            RadiusTopLeft = 0f,
            RadiusTopRight = 0f,
            RadiusBottomLeft = windowRadius,
            RadiusBottomRight = windowRadius,
            Padding = new Thickness(2f),
        };
        var borderedBackgroundBox = new RoundedStyleBox
        {
            BackgroundColor = Color.FromHex("#333844").WithAlpha(0.97f),
            BorderColor = Color.FromHex("#525A66"),
            BorderThickness = 1f,
            CornerRadius = windowRadius,
            Padding = new Thickness(6f),
        };
        var closeButtonTex = sheet.GetTextureOr(iconCfg.CrossIconPath, NanotrasenStylesheet.TextureRoot);

        var leftPanel = StyleBoxHelpers.OpenLeftStyleBox();
        leftPanel.Padding = new Thickness(0f);

        // TODO: maybe also change everything here to `NanoWindow` or something
        return
        [
            // TODO: KILL DEFAULT WINDOW (in a bit)
            E<Label>()
                .Class(DefaultWindow.StyleClassWindowTitle)
                .FontColor(sheet.HighlightPalette.Text)
                .Font(sheet.BaseFont.GetFont(14, FontKind.Bold)),
            E<Label>()
                .Class("windowTitleAlert")
                .FontColor(Color.White)
                .Font(sheet.BaseFont.GetFont(14, FontKind.Bold)),
            // TODO: maybe also change everything here to `NanoWindow` or something
            E()
                .Class(DefaultWindow.StyleClassWindowPanel)
                .Panel(backgroundBox),
            E()
                .Class(DefaultWindow.StyleClassWindowHeader)
                .Panel(headerStylebox),
            E()
                .Class(StyleClass.AlertWindowHeader)
                .Panel(headerAlertStylebox),
            E()
                .Class(StyleClass.BorderedWindowPanel)
                .Panel(borderedBackgroundBox),

            // Close button
            E<TextureButton>()
                .Class(DefaultWindow.StyleClassWindowCloseButton)
                .Prop(TextureButton.StylePropertyTexture, closeButtonTex)
                .Margin(3),
            E<TextureButton>()
                .Class(DefaultWindow.StyleClassWindowCloseButton)
                .PseudoNormal()
                .Modulate(Palettes.Neutral.Element),
            E<TextureButton>()
                .Class(DefaultWindow.StyleClassWindowCloseButton)
                .PseudoHovered()
                .Modulate(Palettes.Red.HoveredElement),
            E<TextureButton>()
                .Class(DefaultWindow.StyleClassWindowCloseButton)
                .PseudoPressed()
                .Modulate(Palettes.Red.PressedElement),
            E<TextureButton>()
                .Class(DefaultWindow.StyleClassWindowCloseButton)
                .PseudoDisabled()
                .Modulate(Palettes.Red.DisabledElement),

            // Title
            // Title uses the selectable UI font instead of the hardcoded Boxfont.
            E<Label>()
                .Class("FancyWindowTitle") // TODO: hardcoding class name
                .Font(sheet.BaseFont.GetFont(14, FontKind.Bold))
                .FontColor(sheet.HighlightPalette.Text),

            // Help Button
            E<TextureButton>()
                .Class(FancyWindow.StyleClassWindowHelpButton)
                .Prop(TextureButton.StylePropertyTexture,
                    sheet.GetTextureOr(iconCfg.HelpIconPath, NanotrasenStylesheet.TextureRoot))
                .Prop(Control.StylePropertyModulateSelf, sheet.PrimaryPalette.Element),
            E<TextureButton>()
                .Class(FancyWindow.StyleClassWindowHelpButton)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, sheet.PrimaryPalette.HoveredElement),
            E<TextureButton>()
                .Class(FancyWindow.StyleClassWindowHelpButton)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, sheet.PrimaryPalette.PressedElement),

            // Footer
            E<Label>()
                .Class("WindowFooterText") // TODO: hardcoding font
                .Prop(Label.StylePropertyFont, sheet.BaseFont.GetFont(8))
                .Prop(Label.StylePropertyFontColor, Color.FromHex("#757575")),
        ];
    }
}
