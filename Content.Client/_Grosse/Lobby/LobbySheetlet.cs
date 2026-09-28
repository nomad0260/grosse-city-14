using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Grosse.Lobby;

/// <summary>
/// Lobby-only panel looks: the dark translucent side panels and the fullscreen scrim over the
/// background image. These live in the theme rather than in a stylesheet local to
/// <c>LobbyGui</c>, so the lobby follows global style changes (including the selected UI font)
/// exactly like every other screen.
/// </summary>
[CommonSheetlet]
public sealed class LobbySheetlet<T> : Sheetlet<T> where T : PalettedStylesheet
{
    public const string Panel = "LobbyPanel";
    public const string Scrim = "LobbyScrim";

    private static readonly Color PanelBg = Color.FromHex("#222833").WithAlpha(0.90f);
    private static readonly Color PanelBorder = Color.FromHex("#4C5665").WithAlpha(0.85f);
    private static readonly Color ScrimBg = Color.FromHex("#070A0E").WithAlpha(0.30f);

    public override StyleRule[] GetRules(T sheet, object config)
    {
        return
        [
            E<PanelContainer>()
                .Class(Panel)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = PanelBg,
                    BorderColor = PanelBorder,
                    BorderThickness = 1f,
                    CornerRadius = 12f,
                    Padding = new Thickness(6f, 4f, 6f, 4f),
                }),
            E<PanelContainer>()
                .Class(Scrim)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = ScrimBg,
                    CornerRadius = 0f,
                }),
        ];
    }
}
