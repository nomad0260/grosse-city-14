using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Grosse.Lobby;

/// <summary>
/// PvP-only lobby styles. Panel, scrim, divider and dossier classes are defined
/// by Content.Client.UIpatch.LobbySheetlet; keeping them here as well produced
/// two competing common sheetlets with the same class names.
/// </summary>
[CommonSheetlet]
public sealed class PvpLobbySheetlet<T> : Sheetlet<T> where T : PalettedStylesheet
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        return
        [
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
        ];
    }
}
