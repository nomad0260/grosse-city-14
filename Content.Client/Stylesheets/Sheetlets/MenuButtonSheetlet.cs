using System.Numerics;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class MenuButtonSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet, IButtonConfig, IIconConfig
{
    private static MutableSelectorElement CButton()
    {
        return E<MenuButton>();
    }

    public override StyleRule[] GetRules(T sheet, object config)
    {
        IButtonConfig cfg = sheet;

        var rules = new List<StyleRule>
        {
            CButton().Class(StyleClass.ButtonSquare).Box(StyleBoxHelpers.SquareStyleBox()),
            CButton().Class(StyleClass.ButtonOpenLeft).Box(StyleBoxHelpers.OpenLeftStyleBox()),
            CButton().Class(StyleClass.ButtonOpenRight).Box(StyleBoxHelpers.OpenRightStyleBox()),
            CButton().Box(StyleBoxHelpers.ButtonStyleBox()),
            CButton()
                .Class(StyleClass.ButtonOpenBoth)
                .Prop(ContainerButton.StylePropertyStyleBox, StyleBoxHelpers.SquareStyleBox()),
            E<Label>()
                .Class(MenuButton.StyleClassLabelTopButton)
                .Prop(Label.StylePropertyFont, sheet.BaseFont.GetFont(14, FontKind.Bold)),
        };

        ButtonSheetlet<T>.MakeButtonRules<MenuButton>(rules, cfg.ButtonPalette, null);
        ButtonSheetlet<T>.MakeButtonRules<MenuButton>(rules, cfg.PositiveButtonPalette, StyleClass.Positive);
        ButtonSheetlet<T>.MakeButtonRules<MenuButton>(rules, cfg.NegativeButtonPalette, StyleClass.Negative);

        return rules.ToArray();
    }
}
