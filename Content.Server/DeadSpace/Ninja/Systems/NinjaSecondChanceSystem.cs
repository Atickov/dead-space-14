using Content.Server.Cloning;
using Content.Shared.Clothing;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaSecondChanceSystem : EntitySystem
{
    private const string CloneSettingsId = "NinjaSecondChanceClone";
    private const string BodyContainerId = "ninja_capsule_body";
    private const string GearId = "SpaceNinjaGear";
    private const string SurvivalLoadoutId = "RoleSurvivalSpaceNinja";

    [Dependency] private readonly CloningSystem _cloning = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly SpiderOSSystem _spiderOS = default!;
    [Dependency] private readonly LoadoutSystem _loadout = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NinjaSecondChanceComponent, MapInitEvent>(OnSecondChanceInit);
        SubscribeLocalEvent<NinjaSecondChanceComponent, AutoDustEvent>(OnAutoDust);
        SubscribeLocalEvent<NinjaRespawnCapsuleComponent, ComponentInit>(OnCapsuleInit);
    }

    private void OnSecondChanceInit(Entity<NinjaSecondChanceComponent> ent, ref MapInitEvent args)
    {
        if (TryFindNearestCapsule(ent, out var capsuleUid))
            ent.Comp.Capsule = capsuleUid;
    }

    private void OnCapsuleInit(Entity<NinjaRespawnCapsuleComponent> ent, ref ComponentInit args)
    {
        ent.Comp.BodyContainer = _container.EnsureContainer<ContainerSlot>(ent.Owner, BodyContainerId);
        _appearance.SetData(ent.Owner, NinjaRespawnCapsuleVisuals.Full, false);
    }

    private void OnAutoDust(Entity<NinjaSecondChanceComponent> ent, ref AutoDustEvent args)
    {
        if (ent.Comp.Used)
            return;

        var target = args.Target;
        if (!target.IsValid() || !_mind.TryGetMind(target, out var mindId, out var mind))
            return;

        if (!Exists(ent.Comp.Capsule))
            if (!TryFindNearestCapsule(target, out var capsuleUid))
                ent.Comp.Capsule = capsuleUid;

        if (!TryComp<NinjaRespawnCapsuleComponent>(ent.Comp.Capsule, out var capsule))
            return;

        if (!_cloning.TryCloning(target, null, CloneSettingsId, out var clone))
            return;

        ent.Comp.Used = true;
        Dirty(ent);

        RestoreSuitOnClone(clone.Value, args.SpiderOS);

        _container.Insert(clone.Value, capsule.BodyContainer, force: true);
        capsule.Timer = 0f;
        _appearance.SetData(ent.Comp.Capsule.Value, NinjaRespawnCapsuleVisuals.Full, true);
        _audio.PlayPvs(capsule.EnterSound, ent.Comp.Capsule.Value);

        _mind.TransferTo(mindId, clone, ghostCheckOverride: true, mind: mind);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NinjaRespawnCapsuleComponent>();
        while (query.MoveNext(out var uid, out var capsule))
        {
            if (capsule.BodyContainer.ContainedEntity is not { Valid: true } contained || !Exists(contained))
            {
                if (capsule.Timer > 0f)
                {
                    capsule.Timer = 0f;
                    _appearance.SetData(uid, NinjaRespawnCapsuleVisuals.Full, false);
                }

                continue;
            }

            capsule.Timer += frameTime;
            if (capsule.Timer < capsule.RespawnTime)
                continue;

            _container.Remove(contained, capsule.BodyContainer, force: true);
            capsule.Timer = 0f;
            _appearance.SetData(uid, NinjaRespawnCapsuleVisuals.Full, false);
            _audio.PlayPvs(capsule.ExitSound, uid);
        }
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

    private bool TryFindNearestCapsule(EntityUid target, out EntityUid capsuleUid)
    {
        capsuleUid = default;

        var targetPos = _transform.GetMapCoordinates(target).Position;
        var query = EntityQueryEnumerator<NinjaRespawnCapsuleComponent>();
        var found = false;
        var bestDistSqr = float.MaxValue;

        while (query.MoveNext(out var uid, out var capsule))
        {
            if (capsule.BodyContainer.ContainedEntity != null)
                continue;

            var capsulePos = _transform.GetMapCoordinates(uid).Position;
            var distSqr = (capsulePos - targetPos).LengthSquared();
            if (distSqr >= bestDistSqr)
                continue;

            bestDistSqr = distSqr;
            capsuleUid = uid;
            found = true;
        }

        return found;
    }
}