using Content.Shared.NPC.Components;
using Content.Shared._Grosse.NPC.Components;
using Robust.Client.GameObjects;
using Content.Client.NPC;

namespace Content.Client._Grosse.NPC;

/// <summary>
/// Визуализирует состояние закапывания NPC (делает невидимым когда закопан)
/// </summary>
public sealed class BurrowableVisualizerSystem : VisualizerSystem<BurrowableVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, BurrowableVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // Проверяем закопан ли NPC
        if (AppearanceSystem.TryGetData<bool>(uid, BurrowVisuals.Burrowed, out var burrowed, args.Component))
        {
            // Делаем полностью невидимым или видимым
            args.Sprite.Color = args.Sprite.Color.WithAlpha(burrowed ? 0.0f : 1.0f);
        }
    }
}
