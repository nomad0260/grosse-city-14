using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Sheetlets;
using Content.Client.Stylesheets.Stylesheets;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.UserInterface.Controls;

[CommonSheetlet]
public sealed class ConfirmButtonSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        // Same lightness lift as normal buttons, otherwise the confirm state is too dark to see.
        var palette = sheet.NegativePalette;

        return [
            E<ConfirmButton>()
                .Pseudo(ConfirmButton.ConfirmPrefix + ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, ButtonStateLift.NormalColor(palette)),

            E<ConfirmButton>()
                .Pseudo(ConfirmButton.ConfirmPrefix + ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, ButtonStateLift.HoverColor(palette)),

            E<ConfirmButton>()
                .Pseudo(ConfirmButton.ConfirmPrefix + ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, ButtonStateLift.PressedColor(palette)),

            E<ConfirmButton>()
                .Pseudo(ConfirmButton.ConfirmPrefix + ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, ButtonStateLift.Disabled),
        ];
    }
}
