using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class MonotoneButtonSheetlet<T> : Sheetlet<T> where T : IButtonConfig
{
    private static readonly Color OutlineColor = Color.FromHex("#5A6675");
    private static readonly Color FillColor = Color.FromHex("#252A33").WithAlpha(0.85f);

    private static RoundedStyleBox Unfilled()
    {
        return new RoundedStyleBox
        {
            BackgroundColor = FillColor,
            BorderColor = OutlineColor,
            BorderThickness = 1f,
            CornerRadius = StyleBoxHelpers.ButtonRadius,
            Padding = new Thickness(12f, 2f, 12f, 2f),
        };
    }

    private static RoundedStyleBox Filled()
    {
        return new RoundedStyleBox
        {
            BackgroundColor = Color.FromHex("#4A5566"),
            BorderColor = Color.FromHex("#8194AB"),
            BorderThickness = 1f,
            CornerRadius = StyleBoxHelpers.ButtonRadius,
            Padding = new Thickness(12f, 2f, 12f, 2f),
        };
    }

    public override StyleRule[] GetRules(T sheet, object config)
    {
        // OpenLeft/OpenRight/Square variants keep the button group looking joined.
        var monotoneButtonOpenLeft = RoundedStyleBox.OpenLeft(FillColor, OutlineColor, 1f, StyleBoxHelpers.ButtonRadius);
        monotoneButtonOpenLeft.Padding = new Thickness(8f, 2f, 12f, 2f);

        var monotoneButtonOpenRight = RoundedStyleBox.OpenRight(FillColor, OutlineColor, 1f, StyleBoxHelpers.ButtonRadius);
        monotoneButtonOpenRight.Padding = new Thickness(12f, 2f, 8f, 2f);

        var monotoneButtonOpenBoth = new RoundedStyleBox
        {
            BackgroundColor = FillColor,
            BorderColor = OutlineColor,
            BorderThickness = 1f,
            CornerRadius = 0f,
            Padding = new Thickness(12f, 2f, 12f, 2f),
        };

        var filledOpenLeft = RoundedStyleBox.OpenLeft(Color.FromHex("#4A5566"), Color.FromHex("#8194AB"), 1f,
            StyleBoxHelpers.ButtonRadius);
        filledOpenLeft.Padding = new Thickness(8f, 2f, 12f, 2f);

        var filledOpenRight = RoundedStyleBox.OpenRight(Color.FromHex("#4A5566"), Color.FromHex("#8194AB"), 1f,
            StyleBoxHelpers.ButtonRadius);
        filledOpenRight.Padding = new Thickness(12f, 2f, 8f, 2f);

        var filledOpenBoth = new RoundedStyleBox
        {
            BackgroundColor = Color.FromHex("#4A5566"),
            BorderColor = Color.FromHex("#8194AB"),
            BorderThickness = 1f,
            CornerRadius = 0f,
            Padding = new Thickness(12f, 2f, 12f, 2f),
        };

        return
        [
            // Unfilled
            E<MonotoneButton>()
                .Box(Unfilled()),
            E<MonotoneButton>()
                .Class(StyleClass.ButtonOpenLeft)
                .Box(monotoneButtonOpenLeft),
            E<MonotoneButton>()
                .Class(StyleClass.ButtonOpenRight)
                .Box(monotoneButtonOpenRight),
            E<MonotoneButton>()
                .Class(StyleClass.ButtonOpenBoth)
                .Box(monotoneButtonOpenBoth),

            // Filled
            E<MonotoneButton>()
                .PseudoPressed()
                .Box(Filled())
                .Prop(Button.StylePropertyModulateSelf, Color.White),
            E<MonotoneButton>()
                .Class(StyleClass.ButtonOpenLeft)
                .PseudoPressed()
                .Box(filledOpenLeft)
                .Prop(Button.StylePropertyModulateSelf, Color.White),
            E<MonotoneButton>()
                .Class(StyleClass.ButtonOpenRight)
                .PseudoPressed()
                .Box(filledOpenRight)
                .Prop(Button.StylePropertyModulateSelf, Color.White),
            E<MonotoneButton>()
                .Class(StyleClass.ButtonOpenBoth)
                .PseudoPressed()
                .Box(filledOpenBoth)
                .Prop(Button.StylePropertyModulateSelf, Color.White),
        ];
    }
}
