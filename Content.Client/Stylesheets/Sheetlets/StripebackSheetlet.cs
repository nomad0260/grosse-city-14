using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class StripebackSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet, IStripebackConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        // Flat header strip instead of the old tiled stripeback texture.
        var stripeBack = new RoundedStyleBox
        {
            BackgroundColor = Color.FromHex("#2E313B"),
            BorderColor = Color.FromHex("#414651"),
            BorderThickness = 1f,
            RadiusTopLeft = 10f,
            RadiusTopRight = 10f,
            RadiusBottomLeft = 0f,
            RadiusBottomRight = 0f,
            Padding = new Thickness(4f),
        };

        return
        [
            E<StripeBack>()
                .Prop(StripeBack.StylePropertyBackground, stripeBack),
        ];
    }
}
