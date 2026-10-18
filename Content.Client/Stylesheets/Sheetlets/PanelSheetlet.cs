using Content.Client.Stylesheets.Colorspace;
using Content.Client.Stylesheets.SheetletConfigs;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class PanelSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet, IButtonConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        IButtonConfig buttonCfg = sheet;

        var boxLight = new StyleBoxFlat()
        {
            BackgroundColor = sheet.SecondaryPalette.BackgroundLight,
        };
        var boxDark = new StyleBoxFlat()
        {
            BackgroundColor = sheet.SecondaryPalette.BackgroundDark,
        };
        var boxPositive = new StyleBoxFlat { BackgroundColor = sheet.PositivePalette.Background };
        var boxNegative = new StyleBoxFlat { BackgroundColor = sheet.NegativePalette.Background };
        var boxHighlight = new StyleBoxFlat { BackgroundColor = sheet.HighlightPalette.Background };
        var boxDropTarget = new StyleBoxFlat
        {
            BackgroundColor = sheet.ButtonPalette.BackgroundDark.WithAlpha(0.5f),
            BorderColor = sheet.ButtonPalette.Base,
            BorderThickness = new(2)
        };

        return
        [
            E<PanelContainer>().Class(StyleClass.PanelLight).Panel(boxLight),
            E<PanelContainer>().Class(StyleClass.PanelDark).Panel(boxDark),
            E<PanelContainer>().Class(StyleClass.PanelDropTarget).Panel(boxDropTarget),

            E<PanelContainer>().Class(StyleClass.Positive).Panel(boxPositive),
            E<PanelContainer>().Class(StyleClass.Negative).Panel(boxNegative),
            E<PanelContainer>().Class(StyleClass.Highlight).Panel(boxHighlight),

            // TODO: this should probably be cleaned up but too many UIs rely on this hardcoded color so I'm scared to touch it
            E<PanelContainer>()
                .Class("BackgroundDark")
                .Prop(PanelContainer.StylePropertyPanel, new StyleBoxFlat(Color.FromHex("#25252A"))),

            // panels that have the same rounded corners as buttons.
            // Lifted in lightness: the raw palette backgrounds were nearly black.
            E()
                .Class(StyleClass.BackgroundPanel)
                .Prop(PanelContainer.StylePropertyPanel, StyleBoxHelpers.PanelStyleBox())
                .Modulate(sheet.SecondaryPalette.Background.NudgeLightness(0.16f)),
            E()
                .Class(StyleClass.BackgroundPanelDark)
                .Prop(PanelContainer.StylePropertyPanel, StyleBoxHelpers.PanelStyleBox())
                .Modulate(sheet.SecondaryPalette.BackgroundDark.NudgeLightness(0.12f)),
             E()
                .Class(StyleClass.BackgroundPanelOpenLeft)
                .Prop(PanelContainer.StylePropertyPanel, StyleBoxHelpers.OpenLeftStyleBox())
                .Modulate(sheet.SecondaryPalette.Background.NudgeLightness(0.16f)),
            E()
                .Class(StyleClass.BackgroundPanelOpenRight)
                .Prop(PanelContainer.StylePropertyPanel, StyleBoxHelpers.OpenRightStyleBox())
                .Modulate(sheet.SecondaryPalette.Background.NudgeLightness(0.16f)),

            // Legacy class from the old Nano theme that still appears in several XAML files
            // (loadouts, cargo bounties, salvage jobs). Without this it had no panel at all.
            E<PanelContainer>()
                .Class("AngleRect")
                .Prop(PanelContainer.StylePropertyPanel, StyleBoxHelpers.PanelStyleBox())
                .Modulate(sheet.SecondaryPalette.Background.NudgeLightness(0.16f)),
        ];
    }
}
