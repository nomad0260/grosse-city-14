using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class ProgressBarSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        // TODO: 1) hardcoded colors, 2) yuck
        var progressBarBackground = new RoundedStyleBox
        {
            BackgroundColor = new Color(0.25f, 0.25f, 0.25f),
            BorderColor = new Color(0.35f, 0.35f, 0.35f),
            BorderThickness = 1f,
            CornerRadius = 6f,
            Padding = new Thickness(0f, 14.5f, 0f, 14.5f),
        };
        var progressBarForeground = new RoundedStyleBox
        {
            BackgroundColor = new Color(0.25f, 0.50f, 0.25f),
            BorderColor = new Color(0.35f, 0.60f, 0.35f),
            BorderThickness = 1f,
            CornerRadius = 6f,
            Padding = new Thickness(0f, 14.5f, 0f, 14.5f),
        };

        return
        [
            E<ProgressBar>()
                .Prop(ProgressBar.StylePropertyBackground, progressBarBackground)
                .Prop(ProgressBar.StylePropertyForeground, progressBarForeground),
        ];
    }
}
