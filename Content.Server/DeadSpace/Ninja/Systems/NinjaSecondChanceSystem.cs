using Content.Server.Cloning;
using Content.Shared.Clothing;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaSecondChanceSystem : EntitySystem
{
    private const string CloneSettingsId = "NinjaSecondChanceClone";
    private const string GearId = "SpaceNinjaGear";
    private const string SurvivalLoadoutId = "RoleSurvivalSpaceNinja";

    [Dependency] private readonly CloningSystem _cloning = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly SpiderOSSystem _spiderOS = default!;
    [Dependency] private readonly LoadoutSystem _loadout = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NinjaSecondChanceComponent, AutoDustEvent>(OnAutoDust);
    }

    private void OnAutoDust(Entity<NinjaSecondChanceComponent> ent, ref AutoDustEvent args)
    {
        if (ent.Comp.Used)
            return;

        var target = args.Target;
        if (!target.IsValid() || !_mind.TryGetMind(target, out var mindId, out var mind))
            return;

        if (!TryFindNearestCapsule(target, out var capsuleUid, out var coords))
            return;

        if (!_cloning.TryCloning(target, coords, CloneSettingsId, out var clone))
            return;

        ent.Comp.Used = true;
        Dirty(ent);

        RestoreSuitOnClone(clone.Value, args.SpiderOS);

        _mind.TransferTo(mindId, clone, ghostCheckOverride: true, mind: mind);
    }

    private void RestoreSuitOnClone(EntityUid clone, SpiderOSComponent? source)
    {
        _loadout.Equip(clone, new() { GearId }, new() { SurvivalLoadoutId });

        var ninja = EnsureComp<SpaceNinjaComponent>(clone);

        if (FindClonedSuit(clone) is not { } suitUid)
            return;

        _ninja.AssignSuit(new Entity<SpaceNinjaComponent>(clone, ninja), suitUid);

        if (source == null)
            return;

        _spiderOS.RestoreState(suitUid, source);
    }

    private EntityUid? FindClonedSuit(EntityUid clone)
    {
        if (!TryComp<InventoryComponent>(clone, out var inventory))
            return null;

        var enumerator = _inventory.GetSlotEnumerator((clone, inventory));
        while (enumerator.NextItem(out var item, out _))
        {
            if (TryComp<SpiderOSComponent>(item, out _))
                return item;
        }

        return null;
    }

    private bool TryFindNearestCapsule(EntityUid target, out EntityUid capsuleUid, out MapCoordinates coords)
    {
        capsuleUid = default;
        coords = default;

        var targetPos = _transform.GetMapCoordinates(target).Position;
        var query = EntityQueryEnumerator<NinjaRespawnCapsuleComponent>();
        var found = false;
        var bestDistSqr = float.MaxValue;

        while (query.MoveNext(out var uid, out _))
        {
            var capsulePos = _transform.GetMapCoordinates(uid).Position;
            var distSqr = (capsulePos - targetPos).LengthSquared();
            if (distSqr >= bestDistSqr)
                continue;

            bestDistSqr = distSqr;
            capsuleUid = uid;
            coords = _transform.GetMapCoordinates(uid);
            found = true;
        }

        return found;
    }
}