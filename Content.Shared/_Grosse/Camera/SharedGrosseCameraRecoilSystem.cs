using Content.Shared._Grosse.Camera.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable.Components;

namespace Content.Shared._Grosse.Camera;

public sealed class SharedGrosseCameraRecoilSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GrosseGunWieldBonusComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
    }

    private void OnGunRefreshModifiers(EntityUid uid, GrosseGunWieldBonusComponent component, ref GunRefreshModifiersEvent args)
    {
        if (TryComp(uid, out WieldableComponent? wield) &&
            wield.Wielded)
        {
            args.CameraRecoilScalar += component.CameraRecoilScalar;
        }
    }
}
