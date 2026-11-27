using System;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI.ProfileEditorControls;

public sealed partial class ProfilePreviewSpriteView : SpriteView
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private ISharedPlayerManager _playerManager = default!;

    /// <summary>
    /// Entity used for the profile editor preview
    /// </summary>
    public EntityUid PreviewDummy;

    public ProfilePreviewSpriteView()
    {
        IoCManager.InjectDependencies(this);
    }

    /// <summary>
    /// Reloads the entire dummy entity for preview.
    /// </summary>
    /// <remarks>
    /// This is expensive so not recommended to run if you have a slider.
    /// </remarks>
    public void LoadPreview(HumanoidCharacterProfile profile, JobPrototype? jobOverride = null, bool showClothes = true)
    {
        EntMan.DeleteEntity(PreviewDummy);
        PreviewDummy = EntityUid.Invalid;

        LoadHumanoidEntity(profile, jobOverride, showClothes);

        SetEntity(PreviewDummy);
        SetName(profile.Name);
    }

    /// <summary>
    /// Sets the preview entity's name without reloading anything else.
    /// </summary>
    public void SetName(string newName)
    {
        EntMan.System<MetaDataSystem>().SetEntityName(PreviewDummy, newName);
    }

    /// <summary>
    /// A slim reload that only updates the entity itself and not any of the job entities, etc.
    /// </summary>
    public void ReloadProfilePreview(HumanoidCharacterProfile profile)
    {
        ReloadHumanoidEntity(profile);
    }

    public void ClearPreview()
    {
        EntMan.DeleteEntity(PreviewDummy);
        PreviewDummy = EntityUid.Invalid;
    }

    private static readonly Direction[] CardinalDirections =
    {
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    };

    /// <summary>
    /// Rotates the dossier portrait with the mouse wheel. It keeps the fixed
    /// South-facing framing of the reference as the default.
    /// </summary>
    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (PreviewDummy == EntityUid.Invalid)
            return;

        var index = 2;
        if (OverrideDirection is { } direction)
        {
            var found = Array.IndexOf(CardinalDirections, direction);
            if (found >= 0)
                index = found;
        }

        index = (index + (args.Delta.Y > 0f ? 1 : -1) + CardinalDirections.Length)
            % CardinalDirections.Length;
        OverrideDirection = CardinalDirections[index];
        args.Handle();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        ClearPreview();
    }
}
