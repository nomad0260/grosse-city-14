using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class LineEditSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet, ILineEditConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        var lineEditStylebox = new RoundedStyleBox
        {
            BackgroundColor = Color.FromHex("#1B1F26").WithAlpha(0.96f),
            BorderColor = Color.FromHex("#454F5C"),
            BorderThickness = 1f,
            CornerRadius = 6f,
            Padding = new Thickness(6f, 2f, 6f, 2f),
        };

        return
        [
            E<LineEdit>()
                .Prop(LineEdit.StylePropertyStyleBox, lineEditStylebox),
            // TODO: Hardcoded colors bad, kill.
            E<LineEdit>()
                .Class(LineEdit.StyleClassLineEditNotEditable)
                .Prop("font-color", new Color(192, 192, 192)),
            E<LineEdit>()
                .Pseudo(LineEdit.StylePseudoClassPlaceholder)
                .Prop("font-color", Color.Gray),
            E<TextEdit>()
                .Pseudo(TextEdit.StylePseudoClassPlaceholder)
                .Prop("font-color", Color.Gray),
        ];
    }
}
