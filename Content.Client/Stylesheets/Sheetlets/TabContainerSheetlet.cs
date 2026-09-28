using Content.Client.Stylesheets.Colorspace;
using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class TabContainerSheetlet<T> : Sheetlet<T> where T: PalettedStylesheet, ITabContainerConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        var tabContainerPanel = new RoundedStyleBox
        {
            BackgroundColor = Color.FromHex("#2C303A").WithAlpha(0.97f),
            BorderColor = Color.FromHex("#4C5665"),
            BorderThickness = 1f,
            CornerRadius = 10f,
        };

        var tabContainerBoxActive = new RoundedStyleBox
        {
            BackgroundColor = sheet.SecondaryPalette.Element.NudgeLightness(0.14f),
            BorderColor = Color.White,
            BorderThickness = 1f,
            RadiusTopLeft = 8f,
            RadiusTopRight = 8f,
            RadiusBottomLeft = 0f,
            RadiusBottomRight = 0f,
            Padding = new Thickness(6f, 3f, 6f, 3f),
        };
        var tabContainerBoxInactive = new RoundedStyleBox
        {
            BackgroundColor = sheet.SecondaryPalette.Background.NudgeLightness(0.10f),
            BorderColor = Color.FromHex("#4C5665"),
            BorderThickness = 1f,
            RadiusTopLeft = 8f,
            RadiusTopRight = 8f,
            RadiusBottomLeft = 0f,
            RadiusBottomRight = 0f,
            Padding = new Thickness(6f, 3f, 6f, 3f),
        };

        return
        [
            E<TabContainer>()
                .Prop(TabContainer.StylePropertyPanelStyleBox, tabContainerPanel)
                .Prop(TabContainer.StylePropertyTabStyleBox, tabContainerBoxActive)
                .Prop(TabContainer.StylePropertyTabStyleBoxInactive, tabContainerBoxInactive),
        ];
    }
}
