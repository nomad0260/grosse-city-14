using Content.Client.Examine;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets.Hud;

[CommonSheetlet]
public sealed class TooltipSheetlet<T> : Sheetlet<T> where T: PalettedStylesheet, ITooltipConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        // Flat, rounded chat bubbles and tooltips instead of the old beveled patches.
        var tooltipBox = new RoundedStyleBox
        {
            BackgroundColor = Color.FromHex("#262A33").WithAlpha(0.97f),
            BorderColor = Color.FromHex("#57616F"),
            BorderThickness = 1f,
            CornerRadius = 8f,
            Padding = new Thickness(7f, 4f, 7f, 4f),
        };

        var whisperBox = new RoundedStyleBox
        {
            BackgroundColor = Color.FromHex("#262A33").WithAlpha(0.97f),
            BorderColor = Color.FromHex("#6A7482"),
            BorderThickness = 1f,
            CornerRadius = 8f,
            Padding = new Thickness(7f, 4f, 7f, 4f),
        };

        return
        [
            E<PanelContainer>()
                .Class(StyleClass.TooltipPanel)
                .Modulate(Color.Gray.WithAlpha(0.9f)) // TODO: you know the drill by now
                .Panel(tooltipBox),
            E<RichTextLabel>()
                .Class(StyleClass.TooltipTitle)
                .Font(sheet.BaseFont.GetFont(14, FontKind.Bold)),
            E<RichTextLabel>()
                .Class(StyleClass.TooltipDesc)
                .Font(sheet.BaseFont.GetFont(12)),

            E<Tooltip>()
                // ReSharper disable once AccessToStaticMemberViaDerivedType
                .Prop(Tooltip.StylePropertyPanel, tooltipBox),
            E<PanelContainer>()
                .Class(ExamineSystem.StyleClassEntityTooltip)
                .Panel(tooltipBox),
            E<PanelContainer>()
                .Class("speechBox", "sayBox")
                .Panel(tooltipBox),
            E<PanelContainer>()
                .Class("speechBox", "whisperBox")
                .Panel(whisperBox),

            E<PanelContainer>()
                .Class("speechBox", "whisperBox")
                .ParentOf(E<RichTextLabel>().Class("bubbleContent"))
                .Prop(Label.StylePropertyFont, sheet.BaseFont.GetFont(12, FontKind.Italic)),
            E<PanelContainer>()
                .Class("speechBox", "emoteBox")
                .ParentOf(E<RichTextLabel>().Class("bubbleContent"))
                .Prop(Label.StylePropertyFont, sheet.BaseFont.GetFont(12, FontKind.Italic)),
        ];
    }
}
