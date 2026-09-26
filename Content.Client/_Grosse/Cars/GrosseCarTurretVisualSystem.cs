using Content.Shared._Grosse.Cars;
using Robust.Client.GameObjects;

namespace Content.Client._Grosse.Cars;

/// <summary>
/// Draws the barrel above the hull. Render order is client-only and is not saved on the prototype.
/// </summary>
public sealed class GrosseCarTurretVisualSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<GrosseCarTurretComponent, SpriteComponent>();
        while (query.MoveNext(out _, out _, out var sprite))
        {
            if (sprite.RenderOrder != 1)
                sprite.RenderOrder = 1;
        }
    }
}
