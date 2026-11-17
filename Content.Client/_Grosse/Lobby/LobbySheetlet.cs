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
    public const string MenuPanel = "PvpMenuPanel";
    public const string Divider = "LobbyDivider";
    public const string Accent = "LobbyAccent";
    public const string ActionDock = "LobbyActionDock";

    private static readonly Color PanelBg = Color.FromHex("#222833").WithAlpha(0.90f);
    private static readonly Color PanelBorder = Color.FromHex("#4C5665").WithAlpha(0.85f);
    private static readonly Color ScrimBg = Color.FromHex("#070A0E").WithAlpha(0.30f);
    private static readonly Color MenuBg = Color.FromHex("#141B25").WithAlpha(0.92f);
    private static readonly Color DividerColor = Color.FromHex("#596170").WithAlpha(0.45f);
    private static readonly Color DockBg = Color.FromHex("#12171F").WithAlpha(0.62f);
    private static readonly Color DockBorder = Color.FromHex("#3E4652").WithAlpha(0.75f);

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
                    CornerRadius = 10f,
                    Padding = new Thickness(6f, 4f, 6f, 4f),
                }),
            E<PanelContainer>()
                .Class(Scrim)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = ScrimBg,
                    CornerRadius = 0f,
                }),
            // Тонкие разделители внутри панелей: тише, чем золотые HLine.
            E<PanelContainer>()
                .Class(Divider)
                .Panel(new StyleBoxFlat(DividerColor)),
            // Короткая золотая черта под заголовком — визуальная подпись лобби.
            E<PanelContainer>()
                .Class(Accent)
                .Panel(new StyleBoxFlat(sheet.HighlightPalette.Text)),
            // Нижний блок действий: отдельная тёмная площадка, чтобы кнопки не висели в пустоте.
            E<PanelContainer>()
                .Class(ActionDock)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = DockBg,
                    BorderColor = DockBorder,
                    BorderThickness = 0f,
                    CornerRadius = 8f,
                    Padding = new Thickness(8f, 8f, 8f, 8f),
                }),
            // Команды в меню предвыбора: тёплый и холодный акценты.
            E<ContainerButton>()
                .Class("PvpTeamAttackers")
                .ParentOf(E<Label>())
                .FontColor(Color.FromHex("#D6A24B")),
            E<ContainerButton>()
                .Class("PvpTeamDefenders")
                .ParentOf(E<Label>())
                .FontColor(Color.FromHex("#74A7D4")),
            // Классы: чуть мельче и с выравниванием по левому краю, чтобы длинные
            // названия вроде «Повстанец (дробовик)» не обрезались с обеих сторон.
            E<ContainerButton>()
                .Class("PvpClassButton")
                .ParentOf(E<Label>())
                .Font(sheet.BaseFont.GetFont(10))
                .AlignMode(Label.AlignMode.Left),
            // Раскрытое меню предвыбора: более тёмная вкладка внутри панели лобби.
            E<PanelContainer>()
                .Class(MenuPanel)
                .Panel(new RoundedStyleBox
                {
                    BackgroundColor = MenuBg,
                    BorderColor = PanelBorder,
                    BorderThickness = 1f,
                    CornerRadius = 8f,
                    Padding = new Thickness(10f, 8f, 10f, 8f),
                }),
        ];
    }
}
