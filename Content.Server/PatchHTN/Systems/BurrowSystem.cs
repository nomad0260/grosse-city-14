using Content.Server.PatchHTN.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.PatchHTN.Systems;

/// <summary>
/// Handles hiding / revealing NPCs that bury themselves via <see cref="BurrowComponent"/>.
/// </summary>
public sealed partial class BurrowSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ITileDefinitionManager _tileDefMan = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private VisibilitySystem _visibility = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BurrowComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<BurrowComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BurrowComponent>();
        var now = _timing.CurTime;

        while (query.MoveNext(out var uid, out var burrow))
        {
            if (!burrow.Burrowed)
                continue;

            if (now - burrow.BurrowedAt < TimeSpan.FromSeconds(burrow.MaxBurrowTime))
                continue;

            TrySurface(uid, burrow);
        }
    }

    public bool TryBurrow(EntityUid uid, BurrowComponent? burrow = null)
    {
        if (!Resolve(uid, ref burrow, false) || burrow.Burrowed || TerminatingOrDeleted(uid))
            return false;

        if (!CanBurrowAt(uid, burrow))
            return false;

        burrow.Burrowed = true;
        burrow.BurrowedAt = _timing.CurTime;

        var visibility = EnsureComp<VisibilityComponent>(uid);
        burrow.HadNormalVisibilityLayer = (visibility.Layer & (int) VisibilityFlags.Normal) != 0;
        SetNormalVisibility(uid, false);

        if (burrow.Sound != null)
            _audio.PlayPvs(burrow.Sound, uid);

        return true;
    }

    public bool TrySurface(EntityUid uid, BurrowComponent? burrow = null)
    {
        if (!Resolve(uid, ref burrow, false) || !burrow.Burrowed)
            return false;

        burrow.Burrowed = false;

        if (!TerminatingOrDeleted(uid))
            SetNormalVisibility(uid, burrow.HadNormalVisibilityLayer);

        burrow.HadNormalVisibilityLayer = false;

        if (burrow.Sound != null)
            _audio.PlayPvs(burrow.Sound, uid);

        return true;
    }

    /// <summary>
    /// Checks <see cref="BurrowComponent.AllowedTiles"/>. An empty list allows any tile.
    /// </summary>
    public bool CanBurrowAt(EntityUid uid, BurrowComponent burrow)
    {
        if (burrow.AllowedTiles.Count == 0)
            return true;

        if (!TryComp(uid, out TransformComponent? xform) ||
            xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return false;
        }

        return _map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tileRef) &&
               !tileRef.Tile.IsEmpty &&
               burrow.AllowedTiles.Contains(_tileDefMan[tileRef.Tile.TypeId].ID);
    }

    private void SetNormalVisibility(EntityUid uid, bool visible)
    {
        var visibility = EnsureComp<VisibilityComponent>(uid);

        if (visible)
            _visibility.AddLayer((uid, visibility), (int) VisibilityFlags.Normal, false);
        else
            _visibility.RemoveLayer((uid, visibility), (int) VisibilityFlags.Normal, false);

        _visibility.RefreshVisibility(uid, visibility);
    }

    private void OnDamageChanged(EntityUid uid, BurrowComponent burrow, DamageChangedEvent args)
    {
        if (args.DamageIncreased && burrow.Burrowed)
            TrySurface(uid, burrow);
    }

    private void OnShutdown(EntityUid uid, BurrowComponent burrow, ComponentShutdown args)
    {
        if (!burrow.Burrowed || Terminating(uid))
            return;

        burrow.Burrowed = false;
        SetNormalVisibility(uid, burrow.HadNormalVisibilityLayer);
        burrow.HadNormalVisibilityLayer = false;
    }
}
