using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

/// <summary>
/// CVars этого форка. Держим их отдельным классом с собственным
/// <see cref="CVarDefsAttribute"/>, как просит комментарий в <c>CCVars.cs</c>.
/// </summary>
[CVarDefs]
public static class GrosseCVars
{
    /// <summary>
    /// Клиент не показывает обучающий бриффинг при входе в лобби. Ставится
    /// кнопкой «больше не показывать»; кнопка «обучение» в лобби открывает
    /// бриффинг вручную независимо от этого значения.
    /// </summary>
    public static readonly CVarDef<bool> LobbyTutorialDisabled =
        CVarDef.Create("lobby.tutorial_disabled", false, CVar.CLIENTONLY | CVar.ARCHIVE,
            "Skip the lobby tutorial briefing when entering the lobby.");
}
