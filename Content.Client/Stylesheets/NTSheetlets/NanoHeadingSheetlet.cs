using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.NTSheetlets;

/// Not NTHeading because NanoHeading is the name of the element
[CommonSheetlet]
public sealed class NanoHeadingSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        // Flat and rounded instead of the old beveled tab.
        var nanoHeadingBox = new RoundedStyleBox
        {
            BackgroundColor = Color.FromHex("#3A3F4C"),
            BorderColor = Color.FromHex("#4C5665"),
            BorderThickness = 1f,
            RadiusTopLeft = 8f,
            RadiusTopRight = 8f,
            RadiusBottomLeft = 0f,
            RadiusBottomRight = 0f,
            Padding = new Thickness(10f, 4f, 10f, 4f),
        };

        return
        [
            E<NanoHeading>().ParentOf(E<PanelContainer>()).Panel(nanoHeadingBox),
        ];
    }
}
