using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaHolographicClonesSystem : SharedNinjaHolographicClonesSystem
{
    private const int MaxSpawnAttempts = 20;
    [Dependency] private readonly NinjaSmokeAbilitySystem _ninjaSmoke = default!;
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly NinjaCloneSystem _ninjaClone = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaHolographicClonesComponent, NinjaHolographicClonesActionEvent>(OnUseHolographicClones);
    }

    private void OnUseHolographicClones(Entity<NinjaHolographicClonesComponent> ent, ref NinjaHolographicClonesActionEvent args)
    {
        var (uid, comp) = ent;
        var user = args.Performer;

        if (comp.CloneAmount < 1)
            return;

        if (!_ninja.TryUseCharge(user, comp.EnergyCost))
        {
            _popup.PopupEntity(Loc.GetString("ninja-no-power"), user, user);
            return;
        }

        if (TryComp<NinjaSmokeAbilityComponent>(uid, out var ninjaSmoke) && ninjaSmoke.AutoMode)
        {
            _ninjaSmoke.TrySpawnNinjaSmoke((uid, ninjaSmoke), true);
        }

        var origin = Transform(user).Coordinates;
        List<EntityUid> clones = new List<EntityUid>();

        for (var i = 0; i < comp.CloneAmount; i++)
        {
            if (GetFreeSpawnCoords(origin, comp.MinSpawnRadius, comp.MaxSpawnRadius) is not { } coords)
                continue;
            clones.Add(_ninjaClone.SpawnVisualClone(user, coords, comp.CloneProto));
        }

        if (clones.Count > 0)
        {
            var userCoordinates = Transform(user).Coordinates;
            var selectedClone = _random.Pick(clones);
            var selectedCloneCoordinates = Transform(selectedClone).Coordinates;

            _transform.SetCoordinates(user, selectedCloneCoordinates);
            _transform.SetCoordinates(selectedClone, userCoordinates);
        }

        args.Handled = true;
    }

    private EntityCoordinates? GetFreeSpawnCoords(EntityCoordinates origin, float minRadius, float maxRadius)
    {
        for (var attempt = 0; attempt < MaxSpawnAttempts; attempt++)
        {
            var offset = _random.NextVector2(minRadius, maxRadius);
            var coords = origin.Offset(offset);
            if (!_lookup.AnyEntitiesIntersecting(coords, LookupFlags.Static))
                return coords;
        }

        return null;
    }
}