using Content.Shared._Grosse.DarkMatter;
using Robust.Client.UserInterface;

namespace Content.Client._Grosse.DarkMatter;

public sealed class ResearchTerminalBoundUserInterface : BoundUserInterface
{
    private ResearchTerminalWindow? _window;

    public ResearchTerminalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ResearchTerminalWindow>();
        _window.OnSynthesize += name => SendMessage(new ResearchSynthesizeMessage(name));
        _window.OnSaveFormula += (name, graph) => SendMessage(new ResearchSaveFormulaMessage(name, graph));
        _window.OnLoadFormula += id => SendMessage(new ResearchLoadFormulaMessage(id));
        _window.OnRenameFormula += (id, name) => SendMessage(new ResearchRenameFormulaMessage(id, name));
        _window.OnDeleteFormula += id => SendMessage(new ResearchDeleteFormulaMessage(id));
        _window.OnCopyFormula += id => SendMessage(new ResearchCopyFormulaMessage(id));
        _window.OnCrystallize += (name, graph) => SendMessage(new ResearchCrystallizeMessage(name, graph));
        _window.OnReflashCrystal += (name, graph) => SendMessage(new ResearchReflashCrystalMessage(name, graph));
        _window.OnTest += (name, graph) => SendMessage(new ResearchTestMessage(name, graph));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ResearchTerminalState terminalState)
            _window?.UpdateState(terminalState);
    }
}
